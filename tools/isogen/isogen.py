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
# Les feuilles r9-*.cue font tomber PCem (cdrom_image.cpp:183, :427) ou divergent par l'hôte (r9-repertoire.cue,
# GR.2e) : elles ne servent qu'à la survie r9-cue, en C# seul, jamais à l'oracle. La campagne de CUE (GR.2e) :
# camp-v-*.cue et camp-m-*.cue, nommées avec leur empreinte dans campagne.lst.
#
# L'image creuse de 2,5 Go (PB-107) n'occupe que ses 32 premiers secteurs : une sonde vérifie avant
# de l'écrire que le système de fichiers fait des trous (st_blocks, exact) et que le quota de
# l'utilisateur ne les compte pas (quotactl_fd, comme par.sh) ; elle est effacée sur tout échec.
# L'appelant l'efface après la porte. Au quota, l'écart mesuré comprend les écritures des autres
# processus de l'utilisateur pendant la mesure — dans une série, les neuf autres voies : son seuil
# est la moitié de la sonde, que des trous comptés dépasseraient en entier.
#
# Usage :
#   python3 tools/isogen/isogen.py DOSSIER          écrit les images dans DOSSIER, qui doit exister
#   python3 tools/isogen/isogen.py DOSSIER NOM…     n'écrit que les images nommées (G10.4 : la recette g5w
#                                                   y prend iso-2048.iso) ; un nom inconnu est refusé

import ctypes
import math
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


def somme_banc(donnees):
    """GR.2b — la somme tournante d'ATAPIBNC.COM sur une phase de données : pour chaque mot (petit-boutiste), la
    somme tournée d'un bit à gauche, puis le mot ajouté, sur 16 bits (atapibanc.py, words2)."""
    s = 0
    for i in range(0, len(donnees), 2):
        s = ((s << 1) | (s >> 15)) & 0xFFFF
        s = (s + donnees[i] + (donnees[i + 1] << 8)) & 0xFFFF
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


def ton(secondes, frequence, gauche):
    """Un son pur d'écoute (GR.3i, D6) : 44 100 trames stéréo par seconde, frequence en Hz sur un seul canal, l'autre
    muet ; 8 000 d'amplitude sur 32 767 (-12 dB)."""
    out = bytearray()
    for i in range(secondes * 44100):
        v = round(8000 * math.sin(2 * math.pi * frequence * i / 44100))
        out += struct.pack('<hh', v, 0) if gauche else struct.pack('<hh', 0, v)
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

# Les images demandées (G10.4) ; vide : toutes.
SEULES = set(sys.argv[2:])
ECRITES = set()


def ecrire(dossier, nom, contenu):
    if SEULES and nom not in SEULES:
        return
    ECRITES.add(nom)
    chemin = os.path.join(dossier, nom)
    os.makedirs(os.path.dirname(chemin), exist_ok=True)
    with open(chemin, 'wb') as f:
        f.write(contenu)


def cue(*lignes, fin='\n', derniere=True):
    """Une feuille CUE : les lignes, chacune terminée par fin ; la dernière sans fin si derniere est faux."""
    texte = fin.join(lignes)
    return (texte + (fin if derniere else '')).encode('ascii')


# ---- GR.2e : la campagne de CUE ------------------------------------------------------------------------------
# Des feuilles valides combinées (un à trois FILE, les quatre formats et l'audio, jusqu'à 99 pistes, INDEX 00 à 99,
# PREGAP, et les styles : casse, tabulations, CRLF, guillemets, zéros de tête), puis des feuilles malformées par
# mutation de feuilles valides (nombres à plusieurs chiffres, négatifs, au-delà de 2^31 ; MSF hors bornes ; un PREGAP
# qui porte le lead-out au-delà de 255 minutes ; guillemets, blancs, fins de ligne, casse ; lignes de 511 et 512 ;
# mots inconnus, lignes ôtées, doublées, échangées) ; enfin des feuilles ciblées, sans tirage : chaque nombre et chaque
# MSF de ces listes à chaque place de deux feuilles de base, et des formes brutes (vide, NUL, BOM…). Une graine
# fixe : deux exécutions écrivent les mêmes feuilles, que campagne.lst nomme avec leur empreinte (isogen.sha256
# porte celle de campagne.lst).
# Écartées avant l'oracle : les formes de PB-110 (une piste avant tout FILE, qui fait tomber PCem) et un FILE qui
# désigne un répertoire (DEVIATION, cdrom_image.cs : ifstream l'ouvre, FileStream le refuse ; r9-cue).
GRAINE = 20261021
VALIDES, MUTEES = 150, 250

# Les fichiers des feuilles : le nom, le type de ses pistes (None : mixte.bin, une piste de données puis l'audio), ses
# secteurs.
BINS = [('iso-2048.iso', 'MODE1/2048', SECTEURS), ('iso-2352-mode1.bin', 'MODE1/2352', SECTEURS),
        ('iso-2352-mode2.bin', 'MODE2/2352', SECTEURS), ('iso-2336-mode2.bin', 'MODE2/2336', SECTEURS),
        ('mixte.bin', None, SECTEURS + 65), ('multi-piste2.bin', 'AUDIO', 25), ('multi-piste3.bin', 'AUDIO', 16)]
MOTS = ('FILE', 'TRACK', 'INDEX', 'PREGAP', 'POSTGAP', 'REM', 'CATALOG', 'BINARY', 'AUDIO', 'MODE1/2048',
        'MODE1/2352', 'MODE2/2336', 'MODE2/2352', 'FLAGS', 'TITLE')


def msf(f, court=False):
    m, s, fr = f // 4500, f // 75 % 60, f % 75
    return f'{m}:{s}:{fr}' if court else f'{m:02d}:{s:02d}:{fr:02d}'


def feuille_valide(r, grande=False):
    """Les lignes d'une feuille valide (sans fin de ligne) ; grande : 99 pistes dans mixte.bin."""
    lignes = []
    if r.random() < 0.3:
        lignes.append('REM campagne isogen')
    if r.random() < 0.15:
        lignes.append('CATALOG 0123456789012')
    if r.random() < 0.08:
        lignes.append(r.choice(['TITLE "Le disque"', 'PERFORMER "iXtal"', 'CDTEXTFILE "style.cdt"']))
    choix = [BINS[4]] if grande else [r.choice(BINS) for _ in range(r.choice([1, 1, 2, 3]))]
    piste = 1
    for nom, genre, n in choix:
        lignes.append(f'FILE "{nom}" BINARY')
        npistes = 99 if grande else (1 if genre not in ('AUDIO', None) else r.choice([1, 2, 3, 5, 12]))
        frame = 0
        for j in range(npistes):
            if piste > 99:
                break
            genre_p = genre or ('MODE1/2352' if j == 0 else 'AUDIO')
            lignes.append(f'  TRACK {piste:02d} {genre_p}')
            if piste > 1 and r.random() < 0.2:
                lignes.append(f'    PREGAP {msf(r.randrange(0, 300))}')
            if j > 0 and r.random() < 0.4:
                lignes.append(f'    INDEX 00 {msf(frame)}')
                frame += r.randrange(1, 4)
            lignes.append(f'    INDEX 01 {msf(frame)}')
            if r.random() < 0.15:
                lignes.append(f'    INDEX {r.randrange(2, 100):02d} {msf(frame + r.randrange(1, 5))}')
            if r.random() < 0.1:
                lignes.append('    FLAGS DCP')
            if r.random() < 0.08:
                lignes.append(r.choice(['    ISRC FRXXX0000001', '    TITLE "piste"', '    PERFORMER "iXtal"',
                                        '    SONGWRITER "isogen"', '    POSTGAP 00:00:10']))
            frame += 1 if grande else r.randrange(1, max(2, n // npistes + 1))
            piste += 1
    return lignes


def style(r, lignes):
    """Une mise en forme tirée : casse des mots, blancs, retraits, zéros de tête, MSF courts, guillemets ôtés."""
    casse = r.choice([str.upper, str.upper, str.lower, str.capitalize])
    sep = r.choice([' ', ' ', '\t', '  ', ' \t'])
    retrait = r.choice([None, '', '\t', '   '])
    zeros = r.choice([None, 1, 3])
    court = r.random() < 0.2
    nus = r.random() < 0.3
    out = []
    for l in lignes:
        mots = l.split()
        if retrait is not None:
            l = retrait * (len(l) - len(l.lstrip()) > 0) + l.lstrip()
        tete = l[:len(l) - len(l.lstrip())]
        mots = [casse(m) if m in MOTS else m for m in mots]
        if mots[0].upper() in ('TRACK', 'INDEX') and zeros is not None:
            mots[1] = f'{int(mots[1]):0{zeros}d}'
        if mots[0].upper() in ('INDEX', 'PREGAP') and court:
            mn, sc, fr = (int(x) for x in mots[-1].split(':'))
            mots[-1] = f'{mn}:{sc}:{fr}'
        if mots[0].upper() == 'FILE' and nus:
            mots[1] = mots[1].strip('"')
        out.append(tete + sep.join(mots))
    return out


NOMBRES = ['0', '00', '-1', '+1', '-0', '2147483647', '2147483648', '-2147483649', '4294967297',
           '99999999999999999999', '1x', 'x1', '0x10', '1e3', '1.5']
MSFS = ['00:00:75', '00:60:00', '00:00:-1', '-1:00:00', '100:00:00', '255:59:74', '0:0:0', '00:00', '00:00:00:00',
        '00:00:00x', '::', '2147483647:00:00', '00:2147483647:00', '00:00:2147483648', '+1:+1:+1', ' 1: 2: 3',
        '1 :2:3', '99999999999:00:00', '00:00:099', '99999999999999999999:00:00', '00:00:-99999999999999999999']
TYPES = ['MODE1/2048', 'MODE1/2352', 'MODE2/2336', 'MODE2/2352', 'AUDIO', 'MODE3/2352', 'CDG', 'MODE1', '']


def mute(r, lignes):
    """Une à trois mutations d'une feuille valide."""
    l = list(lignes)
    for _ in range(r.choice([1, 1, 2, 3])):
        k = r.randrange(len(l))
        mots = l[k].split()
        genre = r.randrange(13)
        if genre == 0 and mots and mots[0].upper() in ('TRACK', 'INDEX') and len(mots) > 1:
            l[k] = l[k].replace(mots[1], r.choice(NOMBRES), 1)
        elif genre == 1 and mots and mots[0].upper() in ('INDEX', 'PREGAP'):
            l[k] = l[k].rsplit(mots[-1], 1)[0] + r.choice(MSFS)
        elif genre == 2:
            l.insert(k + 1, f'    PREGAP {r.choice(["255:00:00", "254:59:74", "300:00:00", "999:00:00"])}')
        elif genre == 3:
            l.insert(k, r.choice(['FOO bar', 'TRACKS 01 AUDIO', 'INDEXX 01 00:00:00', 'FILE', 'TRACK', 'INDEX 01']))
        elif genre == 4 and len(l) > 1:
            del l[k]
        elif genre == 5:
            l.insert(k, l[k])
        elif genre == 6 and k + 1 < len(l):
            l[k], l[k + 1] = l[k + 1], l[k]
        elif genre == 7 and mots and mots[0].upper() == 'FILE':
            l[k] = r.choice([l[k].replace('"', '', 1), l[k][::-1].replace('"', '', 1)[::-1], l[k].replace('"', '""', 1),
                             l[k].replace('.', ' .', 1)])
        elif genre == 8 and mots and mots[0].upper() == 'TRACK' and len(mots) > 2:
            l[k] = l[k].rsplit(mots[-1], 1)[0] + r.choice(TYPES)
        elif genre == 9 and mots and mots[0].upper() == 'FILE':
            l[k] = l[k].rsplit(mots[-1], 1)[0] + r.choice(['WAVE', 'MOTOROLA', 'AIFF', 'binary', ''])
        elif genre == 10:
            l.insert(k, 'REM ' + 'y' * r.choice([505, 506, 507, 508, 600]))
        elif genre == 11:
            l[k] = r.choice([l[k].replace(' ', '\t'), l[k] + '   ', '\t' + l[k], l[k] + '\r', l[k].lower()])
        elif genre == 12 and mots and mots[0].upper() == 'TRACK' and len(mots) > 1:
            try:
                l[k] = l[k].replace(mots[1], f'{int(mots[1]) + r.choice([-1, 1, 2]):02d}', 1)
            except ValueError:
                pass
    return l


def nom_de_file(ligne):
    """Le nom que GetCueString tire d'une ligne FILE (cdrom_image.cpp:526-541)."""
    reste = ligne.lstrip()
    reste = reste[len(reste.split()[0]):] if reste.split() else ''
    mots = reste.split()
    s = mots[0] if mots else ''
    if not s.startswith('"'):
        return s
    if s.endswith('"'):
        return s[1:-1]
    i = reste.find('"')
    apres = reste[i + 1:]
    j = apres.find('"')
    return (apres[:j] if j >= 0 else apres)[:255]


def ecartee(d, lignes):
    """Vrai pour une forme de PB-110 (une ligne TRACK avant toute ligne FILE) ou un FILE qui désigne un répertoire."""
    vu_file = False
    for l in lignes:
        mots = l.split()
        mot = mots[0].upper() if mots else ''
        if mot == 'TRACK' and not vu_file:
            return True
        if mot == 'FILE':
            vu_file = True
            n = nom_de_file(l)
            for c in (n, os.path.join(d, n), n.replace('\\', '/'), os.path.join(d, n.replace('\\', '/'))):
                if n.strip() in ('', '.', '..') or os.path.isdir(c):
                    return True
    return False


def ciblees():
    """Les feuilles ciblées, sans tirage : chaque nombre et chaque MSF à chaque place de deux feuilles de base (le
    numéro de piste, le numéro d'INDEX, le MSF d'INDEX 01, un PREGAP), et les PREGAP qui portent le lead-out au-delà de
    255 minutes."""
    une = ['FILE "iso-2048.iso" BINARY', '  TRACK {p} MODE1/2048', '    INDEX {i} {m}']
    deux = ['FILE "mixte.bin" BINARY', '  TRACK 01 MODE1/2352', '    INDEX 01 00:00:00', '  TRACK {p} AUDIO',
            '    PREGAP {g}', '    INDEX 00 00:00:32', '    INDEX {i} {m}']
    msfs = MSFS + ['255:00:00', '254:59:74', '300:00:00', '00:00:00']
    out = []
    for n in NOMBRES:
        out.append([l.format(p=n, i='01', m='00:00:00') for l in une])
        out.append([l.format(p=n, i='01', m='00:00:42', g='00:00:00') for l in deux])
        out.append([l.format(p='01', i=n, m='00:00:00') for l in une])
    for m in msfs:
        out.append([l.format(p='01', i='01', m=m) for l in une])
        out.append([l.format(p='02', i='01', m=m, g='00:00:00') for l in deux])
        out.append([l.format(p='02', i='01', m='00:00:42', g=m) for l in deux])
    return out


def campagne(d):
    """Les feuilles de la campagne, écrites dans d, et campagne.lst."""
    import hashlib
    import random
    r = random.Random(GRAINE)
    valides = []
    while len(valides) < VALIDES:
        lignes = style(r, feuille_valide(r, grande=len(valides) % 25 == 0))
        valides.append((lignes, r.choice(['\n', '\n', '\r\n']), r.random() < 0.9))
    mutees, ecartees = [], 0
    while len(mutees) < MUTEES:
        lignes, fin, derniere = r.choice(valides)
        m = mute(r, lignes)
        if ecartee(d, m):
            ecartees += 1
            continue
        mutees.append((m, fin, derniere))
    assert ecartees < MUTEES, ecartees
    liste = []
    cibles = [(c, '\n', True) for c in ciblees() if not ecartee(d, c)]
    for pref, feuilles in (('camp-v', valides), ('camp-m', mutees), ('camp-c', cibles)):
        for i, (lignes, fin, derniere) in enumerate(feuilles):
            nom = f'{pref}-{i:03d}.cue'
            octets = cue(*lignes, fin=fin, derniere=derniere)
            ecrire(d, nom, octets)
            liste.append(f'{hashlib.sha256(octets).hexdigest()}  {nom}')
    # Les formes brutes : la feuille vide, un NUL (le « probably a binary file » de getline), le BOM d'UTF-8, deux FILE
    # du même nom, un INDEX avant toute piste, CATALOG sans argument et entre guillemets.
    base = b'FILE "iso-2048.iso" BINARY\n  TRACK 01 MODE1/2048\n    INDEX 01 00:00:00\n'
    brutes = [b'', b'\n', base.replace(b'BINARY', b'BIN\x00ARY'), b'\x00' * 600, b'\xef\xbb\xbf' + base,
              base + b'FILE "iso-2048.iso" BINARY\n  TRACK 02 MODE1/2048\n    INDEX 01 00:00:00\n',
              b'INDEX 01 00:00:00\n' + base, b'CATALOG\n' + base, b'CATALOG "0123456789012"\n' + base]
    for i, octets in enumerate(brutes):
        nom = f'camp-b-{i:03d}.cue'
        ecrire(d, nom, octets)
        liste.append(f'{hashlib.sha256(octets).hexdigest()}  {nom}')
    ecrire(d, 'campagne.lst', ('\n'.join(liste) + '\n').encode('ascii'))
    return ecartees


def main():
    if len(sys.argv) < 2 or not os.path.isdir(sys.argv[1]):
        sys.exit('usage : isogen.py DOSSIER [NOM…]   (un répertoire existant ; NOM : une image à écrire seule)')
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
    # GR.3i, D6 — la feuille d'écoute, sur la disposition de mixte.cue (la piste 2 en LBA 42, les adresses fixes
    # d'ATAPIAUD y tombent sur de l'audio) : la piste de données, 10 secteurs de prégap dans le fichier, 440 Hz à gauche
    # pendant dix secondes, un PREGAP de deux secondes hors du fichier, 880 Hz à droite pendant dix secondes. Le disque
    # finit en LBA 1692.
    ecrire(d, 'ecoute.bin', m1 + bytes(10 * BRUT) + ton(10, 440, True) + ton(10, 880, False))
    ecrire(d, 'ecoute.cue', cue(
        'REM GR.3i : 440 Hz a gauche, puis 880 Hz a droite',
        'FILE "ecoute.bin" BINARY',
        '  TRACK 01 MODE1/2352',
        '    INDEX 01 00:00:00',
        '  TRACK 02 AUDIO',
        '    INDEX 00 00:00:32',
        '    INDEX 01 00:00:42',
        '  TRACK 03 AUDIO',
        '    PREGAP 00:02:00',
        '    INDEX 01 00:10:42'))
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

    # GR.2e : la campagne.
    ecartees = campagne(d)
    if not SEULES:
        print(f'isogen : la campagne de CUE, {VALIDES + MUTEES} feuilles tirées et les ciblées ; {ecartees} mutations '
              'écartées (PB-110, un FILE répertoire).')

    # R9 : C# seul (r9-cue).
    ecrire(d, 'r9-sans-file.cue', cue('TRACK 01 AUDIO', '  INDEX 01 00:00:00', 'TRACK 02 AUDIO',
                                      '  INDEX 01 00:00:10'))
    ecrire(d, 'r9-piste-avant-file.cue', cue('TRACK 01 AUDIO', '  INDEX 01 00:00:00', 'FILE "mixte.bin" BINARY',
                                             '  TRACK 02 AUDIO', '  INDEX 01 00:00:00'))
    # GR.2e : un FILE qui désigne un répertoire (DEVIATION : ifstream l'ouvre, FileStream le refuse), C# seul.
    ecrire(d, 'r9-repertoire.cue', cue('FILE "sous" BINARY', '  TRACK 01 MODE1/2048', '    INDEX 01 00:00:00'))

    if SEULES:
        if SEULES - ECRITES:
            sys.exit(f'isogen : image(s) inconnue(s) : {", ".join(sorted(SEULES - ECRITES))}')
        print(f'isogen : {", ".join(sorted(ECRITES))} écrite(s) dans {d}.')
        return

    # PB-107 : 0xA0000000 octets, au-delà de 2 Gio ; le descripteur annonce 1 310 720 secteurs.
    creuse = 0xA0000000
    tete = bytearray(iso)
    tete[16 * SECTEUR + 80:16 * SECTEUR + 88] = deux32(creuse // SECTEUR)
    alloues, compte = ecrire_creuse(d, 'creuse-2g5.iso', bytes(tete), creuse)
    print(f'isogen : images écrites dans {d} ; creuse-2g5.iso, {creuse} octets apparents, {alloues} alloués, '
          f'{"sans quota" if compte is None else f"{compte} comptés au quota (autres écritures comprises)"}.')


if __name__ == '__main__':
    main()
