#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# isogen — les images de CD de la porte cdimage-check, G10.3 (PLAN-G10.md).
#
# Aucun outil de CD n'est installé sur l'hôte (ni xorriso, ni genisoimage, ni pycdlib) : ce
# générateur écrit lui-même un volume ISO 9660 minimal, puis ses variantes, puis des feuilles CUE et
# leurs fichiers BIN. Python et sa bibliothèque standard seulement. DÉTERMINISTE : dates figées,
# contenus calculés ; deux exécutions écrivent les mêmes octets, et la série vérifie isogen.sha256
# avant la porte.
#
# Le volume : ISO 9660 niveau 1, 32 secteurs logiques de 2 048 octets — la zone système (0-15) à
# zéro, le descripteur primaire (16), le terminateur (17), les tables de chemins L (18) et M (19), la
# racine (20), SUBDIR (21), README.TXT (22), DATA.BIN (23-27, 10 000 octets), SUBDIR/NESTED.TXT (28),
# trois secteurs de bourrage (29-31).
#
# Les secteurs bruts (2 352 octets) portent la synchronisation, l'en-tête (MSF en BCD, mode) et le
# sous-en-tête du mode 2 forme 1 ; EDC et ECC restent à zéro : le moteur ne les lit pas
# (cdrom_image.cpp:169-184), et les calculer serait du code sans porte. Un secteur de 2 336 octets
# est le secteur brut du mode 2 sans ses 16 premiers octets : sous-en-tête, puis les données.
#
# Les feuilles r9-*.cue font tomber PCem (cdrom_image.cpp:183, :427) : elles ne servent qu'à la
# survie r9-cue, en C# seul, jamais à l'oracle.
#
# L'image creuse de 2,5 Go (PB-107) n'occupe que ses 32 premiers secteurs : une sonde vérifie avant
# de l'écrire que le système de fichiers fait des trous (st_blocks, exact) et que le quota de
# l'utilisateur ne les compte pas (quotactl_fd, comme par.sh) ; elle est effacée sur tout échec.
# L'appelant l'efface après la porte. Au quota, l'écart mesuré comprend les écritures des autres
# processus de l'utilisateur pendant la mesure — dans une série, les neuf autres voies : son seuil
# est la moitié de la sonde, que des trous comptés dépasseraient en entier.
#
# Usage :
#   python3 tools/isogen/isogen.py DOSSIER   écrit les images dans DOSSIER, qui doit exister

import ctypes
import os
import struct
import sys

SECTEUR = 2048
BRUT = 2352
SECTEURS = 32

# Le 15 juin 1995 à midi, UTC : la date de tout le volume.
DATE_REPERTOIRE = bytes([95, 6, 15, 12, 0, 0, 0])
DATE_VOLUME = b'1995061512000000' + b'\x00'
DATE_NULLE = b'0000000000000000' + b'\x00'


def deux16(v):
    return struct.pack('<H', v) + struct.pack('>H', v)


def deux32(v):
    return struct.pack('<I', v) + struct.pack('>I', v)


def champ(texte, n):
    b = texte.encode('ascii')
    assert len(b) <= n
    return b + b' ' * (n - len(b))


def enregistrement(ident, extent, taille, repertoire):
    """Un enregistrement de répertoire (ECMA-119 § 9.1), de longueur paire."""
    bourre = 1 if len(ident) % 2 == 0 else 0
    longueur = 33 + len(ident) + bourre
    r = (bytes([longueur, 0]) + deux32(extent) + deux32(taille) + DATE_REPERTOIRE
         + bytes([2 if repertoire else 0, 0, 0]) + deux16(1) + bytes([len(ident)]) + ident
         + b'\x00' * bourre)
    assert len(r) == longueur
    return r


def secteur(*morceaux):
    s = b''.join(morceaux)
    assert len(s) <= SECTEUR
    return s + b'\x00' * (SECTEUR - len(s))


README = (b'iXtal26, porte cdimage-check (G10.3) : volume ISO 9660 genere par '
          b'tools/isogen/isogen.py.\r\n')
DATA = bytes(((i * 37) ^ (i >> 7)) & 0xFF for i in range(10000))
NESTED = b'SUBDIR\\NESTED.TXT\r\n'

L_RACINE, L_SUBDIR, L_README, L_DATA, L_NESTED = 20, 21, 22, 23, 28


def volume():
    """Les 32 secteurs de 2 048 octets du volume."""
    racine = secteur(
        enregistrement(b'\x00', L_RACINE, SECTEUR, True),
        enregistrement(b'\x01', L_RACINE, SECTEUR, True),
        enregistrement(b'DATA.BIN;1', L_DATA, len(DATA), False),
        enregistrement(b'README.TXT;1', L_README, len(README), False),
        enregistrement(b'SUBDIR', L_SUBDIR, SECTEUR, True))
    subdir = secteur(
        enregistrement(b'\x00', L_SUBDIR, SECTEUR, True),
        enregistrement(b'\x01', L_RACINE, SECTEUR, True),
        enregistrement(b'NESTED.TXT;1', L_NESTED, len(NESTED), False))

    # Les tables de chemins (§ 9.4) : la racine (identifiant 00h, parent 1), puis SUBDIR.
    def table(fmt32, fmt16):
        return (bytes([1, 0]) + struct.pack(fmt32, L_RACINE) + struct.pack(fmt16, 1) + b'\x00\x00'
                + bytes([6, 0]) + struct.pack(fmt32, L_SUBDIR) + struct.pack(fmt16, 1) + b'SUBDIR')
    table_l = table('<I', '<H')
    table_m = table('>I', '>H')
    assert len(table_l) == len(table_m) == 24

    pvd = secteur(
        bytes([1]), b'CD001', bytes([1, 0]),
        champ('IXTAL26', 32), champ('IXTAL_G10_3', 32), b'\x00' * 8,
        deux32(SECTEURS), b'\x00' * 32,
        deux16(1), deux16(1), deux16(SECTEUR),
        deux32(len(table_l)), struct.pack('<I', 18), struct.pack('<I', 0),
        struct.pack('>I', 19), struct.pack('>I', 0),
        enregistrement(b'\x00', L_RACINE, SECTEUR, True),
        champ('', 128), champ('', 128), champ('', 128), champ('ISOGEN', 128),
        champ('', 37), champ('', 37), champ('', 37),
        DATE_VOLUME, DATE_VOLUME, DATE_NULLE, DATE_NULLE,
        bytes([1, 0]))
    assert pvd[881] == 1 and pvd[156] == 34
    terminateur = secteur(bytes([255]), b'CD001', bytes([1]))

    s = [b'\x00' * SECTEUR] * 16
    s += [pvd, terminateur, secteur(table_l), secteur(table_m), racine, subdir, secteur(README)]
    for i in range(5):
        s.append(secteur(DATA[i * SECTEUR:(i + 1) * SECTEUR]))
    s.append(secteur(NESTED))
    s += [b'\x00' * SECTEUR] * (SECTEURS - len(s))
    assert len(s) == SECTEURS and all(len(x) == SECTEUR for x in s)
    return s


def bcd(n):
    return ((n // 10) << 4) | (n % 10)


def entete(lba, mode):
    f = lba + 150
    return bytes([bcd(f // 4500), bcd(f // 75 % 60), bcd(f % 75), mode])


SYNC = b'\x00' + b'\xff' * 10 + b'\x00'
SOUS_ENTETE = bytes([0, 0, 0x08, 0, 0, 0, 0x08, 0])


def brut_mode1(lba, donnees):
    return SYNC + entete(lba, 1) + donnees + b'\x00' * 288


def brut_mode2(lba, donnees):
    return SYNC + entete(lba, 2) + SOUS_ENTETE + donnees + b'\x00' * 280


def mode2_2336(lba, donnees):
    return brut_mode2(lba, donnees)[16:]


def audio(secteurs, graine):
    """Des secteurs audio : 588 trames stéréo de 16 bits chacun, un motif propre à chaque graine."""
    out = bytearray()
    for i in range(secteurs * 588):
        out += struct.pack('<HH', (i * 97 + graine * 1009) & 0xFFFF, ((i * 61) ^ (graine * 0x3C5A)) & 0xFFFF)
    return bytes(out)


def high_sierra(s):
    """Le descripteur et le terminateur au format High Sierra : le second test de CanReadPVD
    (cdrom_image.cpp:242), « CDROM » en 9. Le reste du volume reste celui de l'ISO : le moteur ne
    lit que là."""
    t = list(s)
    t[16] = secteur(deux32(16), bytes([1]), b'CDROM', bytes([1, 0]), champ('IXTAL26', 32),
                    champ('IXTAL_G10_3_HSF', 32), b'\x00' * 8, deux32(SECTEURS))
    t[17] = secteur(deux32(17), bytes([255]), b'CDROM', bytes([1]))
    return t


# --- l'image creuse ---------------------------------------------------------------------------

TROU_MAX = 1 << 20     # plus d'un Mio alloué au fichier lui-même (st_blocks) : il n'est pas creux
SONDE = 64 << 20       # la taille apparente de la sonde : bornée, si le système ne fait pas de trous
BRUIT = SONDE // 2     # au quota : des trous comptés y mettraient toute la sonde, les voisins non


def usage_quota(dossier):
    """Les octets comptés au quota de l'utilisateur sur le système de fichiers de dossier, ou None
    sans quota : quotactl_fd (443), Q_GETQUOTA, USRQUOTA — l'appel de par.sh (libre_go)."""
    try:
        libc = ctypes.CDLL(None, use_errno=True)
        buf = ctypes.create_string_buffer(72)
        fd = os.open(dossier, os.O_RDONLY | os.O_DIRECTORY)
        try:
            r = libc.syscall(443, fd, ctypes.c_uint((0x800007 << 8) | 0), os.getuid(), buf)
        finally:
            os.close(fd)
        if r != 0:
            return None
        return struct.unpack('QQQ', buf.raw[:24])[2]
    except OSError:
        return None


def alloue(chemin):
    return os.stat(chemin).st_blocks * 512


def ecrire_creuse(dossier, nom, tete, taille):
    chemin = os.path.join(dossier, nom)
    sonde = os.path.join(dossier, '.sonde-creuse')
    avant = usage_quota(dossier)
    try:
        with open(sonde, 'wb') as f:
            f.truncate(SONDE)
        blocs = alloue(sonde)
        apres = usage_quota(dossier)
    finally:
        if os.path.exists(sonde):
            os.unlink(sonde)
    compte = None if avant is None or apres is None else apres - avant
    if blocs > TROU_MAX or (compte is not None and compte > BRUIT):
        sys.exit(f'isogen : {dossier} ne fait pas de fichiers creux (sonde de {SONDE} octets : {blocs} alloués, '
                 f'{compte} comptés au quota) ; {nom} non écrite.')
    try:
        avant = usage_quota(dossier)
        with open(chemin, 'wb') as f:
            f.write(tete)
            f.truncate(taille)
        apres = usage_quota(dossier)
        compte = None if avant is None or apres is None else apres - avant
        if alloue(chemin) > len(tete) + TROU_MAX or (compte is not None and compte > len(tete) + BRUIT):
            raise RuntimeError(f'{nom} : {alloue(chemin)} octets alloués, {compte} comptés au quota')
    except BaseException:
        if os.path.exists(chemin):
            os.unlink(chemin)
        raise
    return alloue(chemin), compte


# --- les images ---------------------------------------------------------------------------------

def ecrire(dossier, nom, contenu):
    chemin = os.path.join(dossier, nom)
    os.makedirs(os.path.dirname(chemin), exist_ok=True)
    with open(chemin, 'wb') as f:
        f.write(contenu)


def cue(*lignes, fin='\n', derniere=True):
    """Une feuille CUE : les lignes, chacune terminée par fin ; la dernière sans fin si derniere est faux."""
    texte = fin.join(lignes)
    return (texte + (fin if derniere else '')).encode('ascii')


def main():
    if len(sys.argv) != 2 or not os.path.isdir(sys.argv[1]):
        sys.exit('usage : isogen.py DOSSIER   (un répertoire existant)')
    d = sys.argv[1]

    s = volume()
    iso = b''.join(s)
    m1 = b''.join(brut_mode1(i, x) for i, x in enumerate(s))
    m2 = b''.join(brut_mode2(i, x) for i, x in enumerate(s))
    m2336 = b''.join(mode2_2336(i, x) for i, x in enumerate(s))
    assert len(m1) == len(m2) == SECTEURS * BRUT and len(m2336) == SECTEURS * 2336

    # Les ISO.
    ecrire(d, 'iso-2048.iso', iso)
    ecrire(d, 'iso-2048-queue.iso', iso + b'\xee' * 100)
    ecrire(d, 'iso-2048-sa-texte.iso', b'ISOGEN\n' + iso[7:])
    ecrire(d, 'iso-2048-sa-rem.iso', b'REM ISOGEN\r\n\n' + iso[13:])
    ecrire(d, 'iso-2352-mode1.bin', m1)
    ecrire(d, 'iso-2352-mode2.bin', m2)
    ecrire(d, 'iso-2336-mode2.bin', m2336)
    ecrire(d, 'hsf-2048.iso', b''.join(high_sierra(s)))
    ecrire(d, 'vide.iso', b'')
    ecrire(d, 'sans-pvd.iso', iso[:16 * SECTEUR] + b'\x00' * (2 * SECTEUR) + iso[18 * SECTEUR:])
    ecrire(d, 'tronque-pvd.iso', iso[:16 * SECTEUR + 7])

    # Les BIN des feuilles.
    ecrire(d, 'mixte.bin', m1 + audio(10, 1) + audio(30, 2) + audio(25, 3))
    ecrire(d, 'multi-piste2.bin', audio(5, 4) + audio(20, 5))
    ecrire(d, 'multi-piste3.bin', audio(3, 6) + audio(12, 7) + audio(1, 8)[:1000])
    ecrire(d, 'style donnees.iso', iso)
    ecrire(d, 'sous/piste.bin', audio(8, 9))

    # Une piste de données brute, puis deux pistes audio dans le même fichier : INDEX 00 et INDEX 01
    # (10 secteurs de prégap dans le fichier), PREGAP (150 secteurs hors du fichier).
    ecrire(d, 'mixte.cue', cue(
        'FILE "mixte.bin" BINARY',
        '  TRACK 01 MODE1/2352',
        '    INDEX 01 00:00:00',
        '  TRACK 02 AUDIO',
        '    INDEX 00 00:00:32',
        '    INDEX 01 00:00:42',
        '  TRACK 03 AUDIO',
        '    PREGAP 00:02:00',
        '    INDEX 01 00:00:72'))
    # Un fichier par piste ; INDEX 00 en 00:00:00 ; le dernier secteur de la piste 3 incomplet.
    ecrire(d, 'multi.cue', cue(
        'REM plusieurs fichiers',
        'FILE "iso-2048.iso" BINARY',
        '  TRACK 01 MODE1/2048',
        '    INDEX 01 00:00:00',
        'FILE "multi-piste2.bin" BINARY',
        '  TRACK 02 AUDIO',
        '    INDEX 00 00:00:00',
        '    INDEX 01 00:00:05',
        'FILE "multi-piste3.bin" BINARY',
        '  TRACK 03 AUDIO',
        '    INDEX 00 00:00:00',
        '    INDEX 01 00:00:03'))
    # Les trois formats de données, dont MODE2/2336 (PB-106 dans ReadSector).
    ecrire(d, 'formats.cue', cue(
        'FILE "iso-2048.iso" BINARY',
        '  TRACK 01 MODE1/2048',
        '    INDEX 01 00:00:00',
        'FILE "iso-2336-mode2.bin" BINARY',
        '  TRACK 02 MODE2/2336',
        '    INDEX 01 00:00:00',
        'FILE "iso-2352-mode2.bin" BINARY',
        '  TRACK 03 MODE2/2352',
        '    INDEX 01 00:00:00'))
    # CRLF, minuscules, guillemets avec espace, nom sans guillemets, chemin à barre oblique inverse,
    # les commandes ignorées, CATALOG ; la dernière ligne sans fin de ligne.
    ecrire(d, 'style.cue', cue(
        'REM style : CRLF, minuscules, guillemets',
        'CATALOG 0123456789012',
        'CDTEXTFILE "style.cdt"',
        'TITLE "Le disque"',
        'PERFORMER "iXtal"',
        'SONGWRITER "isogen"',
        'file "style donnees.iso" binary',
        '  track 01 mode1/2048',
        '    flags DCP',
        '    isrc FRXXX0000001',
        '    index 01 00:00:00',
        '    postgap 00:00:10',
        'FILE iso-2352-mode1.bin BINARY',
        '  TRACK 02 MODE1/2352',
        '    INDEX 01 00:00:00',
        'FILE "sous\\piste.bin" BINARY',
        '  TRACK 03 AUDIO',
        '    INDEX 01 00:00:00', fin='\r\n', derniere=False))
    # Une ligne de 511 caractères : getline la prend entière (cdrom_image.cpp:281).
    ecrire(d, 'ok-ligne511.cue', cue(
        'REM ' + 'x' * 507,
        'FILE "iso-2048.iso" BINARY',
        '  TRACK 01 MODE1/2048',
        '    INDEX 01 00:00:00'))
    # Un INDEX qui recule : acceptée, avec une longueur négative.
    ecrire(d, 'bizarre-recul.cue', cue(
        'FILE "mixte.bin" BINARY',
        '  TRACK 01 MODE1/2352',
        '    INDEX 01 00:00:10',
        '  TRACK 02 AUDIO',
        '    INDEX 01 00:00:05'))

    # Les refus.
    ecrire(d, 'refus-piste2.cue', cue('FILE "iso-2048.iso" BINARY', '  TRACK 02 MODE1/2048',
                                      '    INDEX 01 00:00:00'))
    ecrire(d, 'refus-trou.cue', cue('FILE "iso-2048.iso" BINARY', '  TRACK 01 MODE1/2048',
                                    '    INDEX 01 00:00:00', 'FILE "iso-2352-mode1.bin" BINARY',
                                    '  TRACK 03 MODE1/2352', '    INDEX 01 00:00:00'))
    ecrire(d, 'refus-index.cue', cue('FILE "mixte.bin" BINARY', '  TRACK 01 MODE1/2352',
                                     '    INDEX 01 00:00:00', '  TRACK 02 AUDIO', '    INDEX 01 00:00:42',
                                     '    INDEX 00 00:00:50'))
    ecrire(d, 'refus-commande.cue', cue('FILE "iso-2048.iso" BINARY', '  TRACK 01 MODE1/2048',
                                        '    INDEX 01 00:00:00', 'FOO bar'))
    ecrire(d, 'refus-type.cue', cue('FILE "iso-2048.iso" BINARY', '  TRACK 01 MODE3/2352',
                                    '    INDEX 01 00:00:00'))
    ecrire(d, 'refus-wave.cue', cue('FILE "iso-2048.iso" WAVE', '  TRACK 01 AUDIO', '    INDEX 01 00:00:00'))
    ecrire(d, 'refus-absent.cue', cue('FILE "absent.bin" BINARY', '  TRACK 01 MODE1/2048',
                                      '    INDEX 01 00:00:00'))
    ecrire(d, 'refus-guillemet.cue', cue('FILE "iso 2048.iso BINARY', '  TRACK 01 MODE1/2048',
                                         '    INDEX 01 00:00:00'))
    ecrire(d, 'refus-ligne512.cue', cue('REM ' + 'x' * 508, 'FILE "iso-2048.iso" BINARY',
                                        '  TRACK 01 MODE1/2048', '    INDEX 01 00:00:00'))
    ecrire(d, 'refus-msf.cue', cue('FILE "iso-2048.iso" BINARY', '  TRACK 01 MODE1/2048',
                                   '    INDEX 01 00:aa:00'))
    ecrire(d, 'refus-sans-piste.cue', cue('FILE "iso-2048.iso" BINARY'))

    # R9 : C# seul (r9-cue).
    ecrire(d, 'r9-sans-file.cue', cue('TRACK 01 AUDIO', '  INDEX 01 00:00:00', 'TRACK 02 AUDIO',
                                      '  INDEX 01 00:00:10'))
    ecrire(d, 'r9-piste-avant-file.cue', cue('TRACK 01 AUDIO', '  INDEX 01 00:00:00', 'FILE "mixte.bin" BINARY',
                                             '  TRACK 02 AUDIO', '  INDEX 01 00:00:00'))

    # PB-107 : 0xA0000000 octets, au-delà de 2 Gio ; le descripteur annonce 1 310 720 secteurs.
    creuse = 0xA0000000
    tete = bytearray(iso)
    tete[16 * SECTEUR + 80:16 * SECTEUR + 88] = deux32(creuse // SECTEUR)
    alloues, compte = ecrire_creuse(d, 'creuse-2g5.iso', bytes(tete), creuse)
    print(f'isogen : images écrites dans {d} ; creuse-2g5.iso, {creuse} octets apparents, {alloues} alloués, '
          f'{"sans quota" if compte is None else f"{compte} comptés au quota (autres écritures comprises)"}.')


if __name__ == '__main__':
    main()
