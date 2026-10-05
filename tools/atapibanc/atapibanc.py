#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# ATAPIBNC.COM — le banc ATAPI de G10.4 (PLAN-G10.md), le témoin du lecteur de CD-ROM (décision n° 5).
#
# Aucun pilote ATAPI DOS ni MSCDEX sur les disques de l'utilisateur, et le BIOS de l'ami486 ne touche
# jamais le canal secondaire (mesuré, VERIFICATION.md § G10.4) : il faut un programme qui parle ATAPI
# lui-même. ATAPIBNC.COM est un petit interprète sur le patron d'IDECHK (G5.2) : une table
# d'opérations — écrire un registre, attendre BSY ou DRQ, envoyer un paquet de douze octets, lire la
# phase de données bloc DRQ par bloc DRQ (le compte d'octets relu en 174h-175h), relever les sept
# registres, l'IIR (172h) lu quand le lecteur demande le paquet et à chaque bloc de données. Saisi
# dans DEBUG.EXE par KeyScript, écrit sur C:, lancé — sous boot-diff, qui compare
# chaque instruction, donc chaque octet lu aux ports. Les relevés s'affichent en hexadécimal, pour
# les yeux : la preuve est le diff.
#
# Machine attendue : l'ami486, C: amorçable (DOS 5 sans KEYB FR), le CD-ROM en maître secondaire
# (cdrom_channel = 2), chargé de iso-2048.iso d'isogen ou vide. Le même programme sert aux deux :
#   la signature (172h-175h : 01 01 14 EB) ; IDENTIFY DEVICE refusé ; IDENTIFY PACKET DEVICE ;
#   TEST UNIT READY (UNIT ATTENTION, puis GOOD ; NOT READY sans disque) ; REQUEST SENSE ; INQUIRY ;
#   READ CAPACITY (PB-117 : 33 pour 32 secteurs) ; READ(10) du PVD (LBA 16) ; READ(10) de quatre
#   secteurs sous une limite de 2 048 octets (quatre blocs DRQ) ; READ(10) du dernier secteur (31),
#   puis de celui que READ CAPACITY annonce (32), refusé ; READ TOC en LBA et en MSF ; MODE SENSE(10)
#   des pages 2Ah et 3Fh ; MODE SELECT(10) de la page audio (la phase de données sortante), relue par
#   MODE SENSE(10) ; MODE SELECT(6), dont PCem lit l'en-tête sur huit octets (PB-119) ; GET EVENT
#   STATUS NOTIFICATION deux fois (PB-118) ; un code inconnu.
# Les interruptions restent permises (IRQ 15) ; l'état lu en 177h les abaisse.
#
# Usage :
#   python3 tools/atapibanc/atapibanc.py            écrit atapibanc.keys, affiche le listing
#   python3 tools/atapibanc/atapibanc.py --com F    écrit aussi le .COM (objdump -D -b binary
#                                                   -mi8086 --adjust-vma=0x100 F)

import os
import struct
import sys

ORG = 0x100


def w(v):
    return list(struct.pack('<H', v & 0xFFFF))


PROG = []


def ins(label, text, size, enc):
    PROG.append((label, text, size, enc))


def rel8(L, end, target):
    d = L[target] - end
    assert -128 <= d <= 127, (target, d)
    return d & 0xFF


def rel16(L, end, target):
    return w(L[target] - end)


def nxt():
    ins(None, 'jmp next', 3, lambda L, a: [0xE9] + rel16(L, a + 3, 'next'))


# ---- l'interprète -----------------------------------------------------------------------
# BP = base du canal (170h), BX = registre de contrôle (base + 206h), SI = script, DI = relevés.
ins('start', 'mov si,script', 3, lambda L, a: [0xBE] + w(L['script']))
ins(None, 'mov di,results', 3, lambda L, a: [0xBF] + w(L['results']))
ins(None, 'cld', 1, lambda L, a: [0xFC])
ins('next', 'lodsb', 1, lambda L, a: [0xAC])

# op 1 : OUT base+reg, val
ins(None, 'cmp al,1', 2, lambda L, a: [0x3C, 0x01])
ins(None, 'jne op2', 2, lambda L, a: [0x75, rel8(L, a + 2, 'op2')])
ins(None, 'lodsb                ; reg', 1, lambda L, a: [0xAC])
ins(None, 'xor ah,ah', 2, lambda L, a: [0x30, 0xE4])
ins(None, 'mov dx,bp', 2, lambda L, a: [0x89, 0xEA])
ins(None, 'add dx,ax', 2, lambda L, a: [0x01, 0xC2])
ins(None, 'lodsb                ; val', 1, lambda L, a: [0xAC])
ins(None, 'out dx,al', 1, lambda L, a: [0xEE])
nxt()

# op 2 : attendre BSY à zéro (au plus 65 535 lectures)
ins('op2', 'cmp al,2', 2, lambda L, a: [0x3C, 0x02])
ins(None, 'jne op3', 2, lambda L, a: [0x75, rel8(L, a + 2, 'op3')])
ins(None, 'call bsy', 3, lambda L, a: [0xE8] + rel16(L, a + 3, 'bsy'))
nxt()

# op 3 : lire N mots du port de données, somme tournante, les K premiers octets gardés
ins('op3', 'cmp al,3', 2, lambda L, a: [0x3C, 0x03])
ins(None, 'jne op5', 2, lambda L, a: [0x75, rel8(L, a + 2, 'op5')])
ins(None, 'lodsw                ; N', 1, lambda L, a: [0xAD])
ins(None, 'mov cx,ax', 2, lambda L, a: [0x89, 0xC1])
ins(None, 'lodsb                ; K', 1, lambda L, a: [0xAC])
ins(None, 'xor ah,ah', 2, lambda L, a: [0x30, 0xE4])
ins(None, 'mov [keep],ax', 3, lambda L, a: [0xA3] + w(L['keep']))
ins(None, 'mov word [sum],0', 6, lambda L, a: [0xC7, 0x06] + w(L['sum']) + [0, 0])
ins(None, 'call words', 3, lambda L, a: [0xE8] + rel16(L, a + 3, 'words'))
ins(None, 'mov ax,[sum]', 3, lambda L, a: [0xA1] + w(L['sum']))
ins(None, 'stosw', 1, lambda L, a: [0xAB])
nxt()

# op 5 : relever 177h, 171h, 172h … 176h (sept octets)
ins('op5', 'cmp al,5', 2, lambda L, a: [0x3C, 0x05])
ins(None, 'jne op6', 2, lambda L, a: [0x75, rel8(L, a + 2, 'op6')])
for r in (7, 1, 2, 3, 4, 5, 6):
    ins(None, 'mov dx,bp', 2, lambda L, a: [0x89, 0xEA])
    ins(None, f'add dx,{r}', 3, lambda L, a, r=r: [0x83, 0xC2, r])
    ins(None, 'in al,dx', 1, lambda L, a: [0xEC])
    ins(None, 'stosb', 1, lambda L, a: [0xAA])
nxt()

# op 6 : attendre BSY à zéro ET (DRQ ou ERR)
ins('op6', 'cmp al,6', 2, lambda L, a: [0x3C, 0x06])
ins(None, 'jne op7', 2, lambda L, a: [0x75, rel8(L, a + 2, 'op7')])
ins(None, 'mov dx,bp', 2, lambda L, a: [0x89, 0xEA])
ins(None, 'add dx,7', 3, lambda L, a: [0x83, 0xC2, 0x07])
ins(None, 'mov cx,0FFFFh', 3, lambda L, a: [0xB9, 0xFF, 0xFF])
ins('w6', 'in al,dx', 1, lambda L, a: [0xEC])
ins(None, 'test al,80h', 2, lambda L, a: [0xA8, 0x80])
ins(None, 'jnz w6n', 2, lambda L, a: [0x75, rel8(L, a + 2, 'w6n')])
ins(None, 'test al,09h', 2, lambda L, a: [0xA8, 0x09])
ins(None, 'jnz w6x', 2, lambda L, a: [0x75, rel8(L, a + 2, 'w6x')])
ins('w6n', 'loop w6', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'w6')])
ins('w6x', 'jmp next', 3, lambda L, a: [0xE9] + rel16(L, a + 3, 'next'))

# op 7 : choisir le canal (base) ; contrôle = base + 206h
ins('op7', 'cmp al,7', 2, lambda L, a: [0x3C, 0x07])
ins(None, 'jne op10', 2, lambda L, a: [0x75, rel8(L, a + 2, 'op10')])
ins(None, 'lodsw', 1, lambda L, a: [0xAD])
ins(None, 'mov bp,ax', 2, lambda L, a: [0x89, 0xC5])
ins(None, 'add ax,206h', 3, lambda L, a: [0x05, 0x06, 0x02])
ins(None, 'mov bx,ax', 2, lambda L, a: [0x89, 0xC3])
nxt()

# op 10 : le paquet, douze octets du script en six mots au port de données
ins('op10', 'cmp al,10', 2, lambda L, a: [0x3C, 0x0A])
ins(None, 'jne op11', 2, lambda L, a: [0x75, rel8(L, a + 2, 'op11')])
ins(None, 'mov dx,bp', 2, lambda L, a: [0x89, 0xEA])
ins(None, 'mov cx,6', 3, lambda L, a: [0xB9, 0x06, 0x00])
ins('p10', 'lodsw', 1, lambda L, a: [0xAD])
ins(None, 'out dx,ax', 1, lambda L, a: [0xEF])
ins(None, 'loop p10', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'p10')])
nxt()

# op 11 : la phase de données, bloc DRQ par bloc DRQ. Après chaque attente de BSY : DRQ absent, fin ;
# sinon le compte d'octets relu en 174h-175h, (compte + 1) / 2 mots lus. Les K premiers octets du flux
# gardés, puis le nombre de blocs (1 octet), le total des comptes (2) et la somme tournante (2).
ins('op11', 'cmp al,11', 2, lambda L, a: [0x3C, 0x0B])
ins(None, 'jne op12', 2, lambda L, a: [0x75, rel8(L, a + 2, 'op12')])
ins(None, 'lodsb                ; K', 1, lambda L, a: [0xAC])
ins(None, 'xor ah,ah', 2, lambda L, a: [0x30, 0xE4])
ins(None, 'mov [keep],ax', 3, lambda L, a: [0xA3] + w(L['keep']))
ins(None, 'xor ax,ax', 2, lambda L, a: [0x31, 0xC0])
ins(None, 'mov [tot],ax', 3, lambda L, a: [0xA3] + w(L['tot']))
ins(None, 'mov [sum],ax', 3, lambda L, a: [0xA3] + w(L['sum']))
ins(None, 'mov [blk],al', 3, lambda L, a: [0xA2] + w(L['blk']))
ins(None, 'mov [iir],al', 3, lambda L, a: [0xA2] + w(L['iir']))
ins('d11', 'call bsy', 3, lambda L, a: [0xE8] + rel16(L, a + 3, 'bsy'))
ins(None, 'test al,08h          ; DRQ ?', 2, lambda L, a: [0xA8, 0x08])
ins(None, 'jz e11', 2, lambda L, a: [0x74, rel8(L, a + 2, 'e11')])
ins(None, 'inc byte [blk]', 4, lambda L, a: [0xFE, 0x06] + w(L['blk']))
ins(None, 'mov dx,bp', 2, lambda L, a: [0x89, 0xEA])
ins(None, "add dx,2             ; l'IIR du bloc", 3, lambda L, a: [0x83, 0xC2, 0x02])
ins(None, 'in al,dx', 1, lambda L, a: [0xEC])
ins(None, 'or [iir],al', 4, lambda L, a: [0x08, 0x06] + w(L['iir']))
ins(None, 'add dx,2', 3, lambda L, a: [0x83, 0xC2, 0x02])
ins(None, 'in al,dx', 1, lambda L, a: [0xEC])
ins(None, 'mov cl,al', 2, lambda L, a: [0x88, 0xC1])
ins(None, 'inc dx', 1, lambda L, a: [0x42])
ins(None, 'in al,dx', 1, lambda L, a: [0xEC])
ins(None, 'mov ch,al', 2, lambda L, a: [0x88, 0xC5])
ins(None, 'add [tot],cx', 4, lambda L, a: [0x01, 0x0E] + w(L['tot']))
ins(None, 'inc cx', 1, lambda L, a: [0x41])
ins(None, 'shr cx,1', 2, lambda L, a: [0xD1, 0xE9])
ins(None, 'jcxz e11             ; compte nul : fin', 2, lambda L, a: [0xE3, rel8(L, a + 2, 'e11')])
ins(None, 'call words', 3, lambda L, a: [0xE8] + rel16(L, a + 3, 'words'))
ins(None, 'jmp d11', 2, lambda L, a: [0xEB, rel8(L, a + 2, 'd11')])
ins('e11', 'mov al,[blk]', 3, lambda L, a: [0xA0] + w(L['blk']))
ins(None, 'stosb', 1, lambda L, a: [0xAA])
ins(None, 'mov al,[iir]', 3, lambda L, a: [0xA0] + w(L['iir']))
ins(None, 'stosb', 1, lambda L, a: [0xAA])
ins(None, 'mov ax,[tot]', 3, lambda L, a: [0xA1] + w(L['tot']))
ins(None, 'stosw', 1, lambda L, a: [0xAB])
ins(None, 'mov ax,[sum]', 3, lambda L, a: [0xA1] + w(L['sum']))
ins(None, 'stosw', 1, lambda L, a: [0xAB])
nxt()

# op 12 : relever un registre, base + reg (l'IIR quand le lecteur demande le paquet)
ins('op12', 'cmp al,12', 2, lambda L, a: [0x3C, 0x0C])
ins(None, 'jne op13', 2, lambda L, a: [0x75, rel8(L, a + 2, 'op13')])
ins(None, 'lodsb                ; reg', 1, lambda L, a: [0xAC])
ins(None, 'xor ah,ah', 2, lambda L, a: [0x30, 0xE4])
ins(None, 'mov dx,bp', 2, lambda L, a: [0x89, 0xEA])
ins(None, 'add dx,ax', 2, lambda L, a: [0x01, 0xC2])
ins(None, 'in al,dx', 1, lambda L, a: [0xEC])
ins(None, 'stosb', 1, lambda L, a: [0xAA])
nxt()

# op 13 : la phase de données sortante — N mots du script, écrits au port de données si le lecteur lève
# DRQ (rep outsw), sautés sinon
ins('op13', 'cmp al,13', 2, lambda L, a: [0x3C, 0x0D])
ins(None, 'jne done', 2, lambda L, a: [0x75, rel8(L, a + 2, 'done')])
ins(None, 'lodsb                ; N', 1, lambda L, a: [0xAC])
ins(None, 'xor ah,ah', 2, lambda L, a: [0x30, 0xE4])
ins(None, 'mov cx,ax', 2, lambda L, a: [0x89, 0xC1])
ins(None, 'push cx', 1, lambda L, a: [0x51])
ins(None, 'call bsy', 3, lambda L, a: [0xE8] + rel16(L, a + 3, 'bsy'))
ins(None, 'pop cx', 1, lambda L, a: [0x59])
ins(None, 'test al,08h          ; DRQ ?', 2, lambda L, a: [0xA8, 0x08])
ins(None, 'jz s13', 2, lambda L, a: [0x74, rel8(L, a + 2, 's13')])
ins(None, 'mov dx,bp', 2, lambda L, a: [0x89, 0xEA])
ins(None, 'rep outsw', 2, lambda L, a: [0xF3, 0x6F])
nxt()
ins('s13', 'shl cx,1', 2, lambda L, a: [0xD1, 0xE1])
ins(None, 'add si,cx', 2, lambda L, a: [0x01, 0xCE])
nxt()

# op 0 (et tout inconnu) : afficher les relevés, seize octets par ligne, puis sortir
ins('done', 'mov si,results', 3, lambda L, a: [0xBE] + w(L['results']))
ins(None, 'xor bp,bp', 2, lambda L, a: [0x31, 0xED])
ins('p0', 'cmp si,di', 2, lambda L, a: [0x39, 0xFE])
ins(None, 'jae fin', 2, lambda L, a: [0x73, rel8(L, a + 2, 'fin')])
ins(None, 'lodsb', 1, lambda L, a: [0xAC])
ins(None, 'push ax', 1, lambda L, a: [0x50])
ins(None, 'shr al,4', 3, lambda L, a: [0xC0, 0xE8, 0x04])
ins(None, 'call nib', 3, lambda L, a: [0xE8] + rel16(L, a + 3, 'nib'))
ins(None, 'pop ax', 1, lambda L, a: [0x58])
ins(None, 'and al,0Fh', 2, lambda L, a: [0x24, 0x0F])
ins(None, 'call nib', 3, lambda L, a: [0xE8] + rel16(L, a + 3, 'nib'))
ins(None, 'inc bp', 1, lambda L, a: [0x45])
ins(None, 'test bp,0Fh', 4, lambda L, a: [0xF7, 0xC5, 0x0F, 0x00])
ins(None, 'jnz p0', 2, lambda L, a: [0x75, rel8(L, a + 2, 'p0')])
ins(None, 'mov dx,crlf', 3, lambda L, a: [0xBA] + w(L['crlf']))
ins(None, 'mov ah,9', 2, lambda L, a: [0xB4, 0x09])
ins(None, 'int 21h', 2, lambda L, a: [0xCD, 0x21])
ins(None, 'jmp p0', 2, lambda L, a: [0xEB, rel8(L, a + 2, 'p0')])
ins('fin', 'mov ax,4C00h', 3, lambda L, a: [0xB8, 0x00, 0x4C])
ins(None, 'int 21h', 2, lambda L, a: [0xCD, 0x21])
ins('nib', "add al,'0'", 2, lambda L, a: [0x04, 0x30])
ins(None, "cmp al,'9'", 2, lambda L, a: [0x3C, 0x39])
ins(None, 'jbe nibp', 2, lambda L, a: [0x76, rel8(L, a + 2, 'nibp')])
ins(None, 'add al,7', 2, lambda L, a: [0x04, 0x07])
ins('nibp', 'mov dl,al', 2, lambda L, a: [0x88, 0xC2])
ins(None, 'mov ah,2', 2, lambda L, a: [0xB4, 0x02])
ins(None, 'int 21h', 2, lambda L, a: [0xCD, 0x21])
ins(None, 'ret', 1, lambda L, a: [0xC3])

# bsy : attendre BSY à zéro sur base+7 (au plus 65 535 lectures) ; rend l'état dans AL
ins('bsy', 'mov dx,bp', 2, lambda L, a: [0x89, 0xEA])
ins(None, 'add dx,7', 3, lambda L, a: [0x83, 0xC2, 0x07])
ins(None, 'mov cx,0FFFFh', 3, lambda L, a: [0xB9, 0xFF, 0xFF])
ins('wb', 'in al,dx', 1, lambda L, a: [0xEC])
ins(None, 'test al,80h', 2, lambda L, a: [0xA8, 0x80])
ins(None, 'jz wbx', 2, lambda L, a: [0x74, rel8(L, a + 2, 'wbx')])
ins(None, 'loop wb', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'wb')])
ins('wbx', 'ret', 1, lambda L, a: [0xC3])

# words : CX mots lus au port de données, somme tournante ; tant que [keep] > 0, le mot est gardé
ins('words', 'mov dx,bp', 2, lambda L, a: [0x89, 0xEA])
ins('rw', 'in ax,dx', 1, lambda L, a: [0xED])
ins(None, 'rol word [sum],1', 4, lambda L, a: [0xD1, 0x06] + w(L['sum']))
ins(None, 'add [sum],ax', 4, lambda L, a: [0x01, 0x06] + w(L['sum']))
ins(None, 'cmp word [keep],0', 5, lambda L, a: [0x83, 0x3E] + w(L['keep']) + [0x00])
ins(None, 'je rwn', 2, lambda L, a: [0x74, rel8(L, a + 2, 'rwn')])
ins(None, 'stosw', 1, lambda L, a: [0xAB])
ins(None, 'sub word [keep],2', 5, lambda L, a: [0x83, 0x2E] + w(L['keep']) + [0x02])
ins('rwn', 'loop rw', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'rw')])
ins(None, 'ret', 1, lambda L, a: [0xC3])


# ---- le script -----------------------------------------------------------------------------
def OUT(reg, val): return [1, reg, val]
WAIT = [2]
def RDS(n, keep): return [3] + w(n) + [keep]
REC = [5]
WDRQ = [6]
def BASE(b): return [7] + w(b)


def CDB(*b):
    assert len(b) <= 12
    return list(b) + [0] * (12 - len(b))


def PACKET(cdb, keep=0, limite=0xFFFE):
    """Un paquet ATAPI, PIO : features 0, la limite du compte d'octets en 174h-175h, A0h ; quand le
    lecteur lève DRQ, l'IIR (C/D = 1) relevé, puis le paquet ; la phase de données (op 11, l'IIR de
    chaque bloc en OU), puis les sept registres."""
    assert keep % 2 == 0
    return (OUT(1, 0) + OUT(4, limite & 0xFF) + OUT(5, limite >> 8) + OUT(7, 0xA0) + WDRQ + [12, 2]
            + [10] + cdb + [11, keep] + REC)


def PACKET_OUT(cdb, donnees, keep=0, limite=0xFFFE):
    """Un paquet suivi d'une phase de données sortante : les octets du script, écrits en mots (op 13)."""
    assert len(donnees) % 2 == 0 and keep % 2 == 0
    return (OUT(1, 0) + OUT(4, limite & 0xFF) + OUT(5, limite >> 8) + OUT(7, 0xA0) + WDRQ + [12, 2]
            + [10] + cdb + [13, len(donnees) // 2] + donnees + [11, keep] + REC)


# La page audio (0Eh) : les sélecteurs et volumes des ports de sortie, reconnaissables.
PAGE_0E = [0x0E, 0x0E, 0x05, 0x04, 0x00, 0x80, 0x00, 75, 0x01, 0x80, 0x02, 0x40, 0, 0, 0, 0]

TUR = CDB(0x00)
SENSE = CDB(0x03, 0, 0, 0, 18)


def READ10(lba, n):
    return CDB(0x28, 0, (lba >> 24) & 0xFF, (lba >> 16) & 0xFF, (lba >> 8) & 0xFF, lba & 0xFF, 0, n >> 8, n & 0xFF)


S = []
S += BASE(0x170) + OUT(6, 0xA0) + WAIT + REC                      # la signature : 01 01 14 EB
S += OUT(7, 0xEC) + WAIT + REC                                    # IDENTIFY DEVICE : ABRT
S += OUT(7, 0xA1) + WDRQ + RDS(256, 96) + REC                     # IDENTIFY PACKET DEVICE (modèle : 54-93)
S += PACKET(TUR)                                                  # UNIT ATTENTION (vide : NOT READY)
S += PACKET(SENSE, 18)
S += PACKET(TUR)                                                  # GOOD
S += PACKET(CDB(0x12, 0, 0, 0, 36), 36)                           # INQUIRY
S += PACKET(CDB(0x25), 8)                                         # READ CAPACITY (PB-117)
S += PACKET(READ10(16, 1), 16)                                    # le PVD : 01 « CD001 » 01
S += PACKET(READ10(20, 4), 0, 0x0800)                             # quatre blocs DRQ de 2 048 octets
S += PACKET(READ10(31, 1))                                        # le dernier secteur de l'image
S += PACKET(READ10(32, 1))                                        # celui que READ CAPACITY annonce : refusé
S += PACKET(SENSE, 18)                                            # ILLEGAL REQUEST, LBA OUT OF RANGE
S += PACKET(CDB(0x43, 0, 0, 0, 0, 0, 0, 0, 0x64), 20)             # READ TOC, LBA
S += PACKET(CDB(0x43, 2, 0, 0, 0, 0, 0, 0, 0x64), 20)             # READ TOC, MSF
S += PACKET(CDB(0x5A, 0, 0x2A, 0, 0, 0, 0, 0, 0x40), 28)          # MODE SENSE(10), page 2Ah
S += PACKET(CDB(0x5A, 0, 0x3F, 0, 0, 0, 0, 0, 0x80))              # MODE SENSE(10), toutes les pages
S += PACKET_OUT(CDB(0x55, 0x10, 0, 0, 0, 0, 0, 0, 24), [0] * 8 + PAGE_0E)   # MODE SELECT(10), la page audio
S += PACKET(CDB(0x5A, 0, 0x0E, 0, 0, 0, 0, 0, 0x20), 24)          # MODE SENSE(10), page 0Eh : relue
S += PACKET_OUT(CDB(0x15, 0x10, 0, 0, 20), [0] * 4 + PAGE_0E)     # MODE SELECT(6) : en-tête de 8 lu (PB-119)
S += PACKET(SENSE, 18)
S += PACKET(CDB(0x4A, 1, 0, 0, 0x10, 0, 0, 0, 8), 8)              # GET EVENT STATUS NOTIFICATION (PB-118)
S += PACKET(CDB(0x4A, 1, 0, 0, 0x10, 0, 0, 0, 8), 8)              # encore : toujours NEW_MEDIA
S += PACKET(CDB(0xD8))                                            # un code inconnu : ILLEGAL REQUEST
S += PACKET(SENSE, 18)
S += [0]

ins('script', 'db script', len(S), lambda L, a, S=S: list(S))
ins('sum', 'dw 0', 2, lambda L, a: [0, 0])
ins('tot', 'dw 0', 2, lambda L, a: [0, 0])
ins('keep', 'dw 0', 2, lambda L, a: [0, 0])
ins('blk', 'db 0', 1, lambda L, a: [0])
ins('iir', 'db 0', 1, lambda L, a: [0])
ins('crlf', "db 13,10,'$'", 3, lambda L, a: [13, 10, 0x24])
# `results` : la fin du programme ; la zone n'est pas écrite par DEBUG.
ins('results', 'results:', 0, lambda L, a: [])


def assemble():
    labels, a = {}, ORG
    for label, _, size, _ in PROG:
        if label:
            labels[label] = a
        a += size
    out, listing, a = [], [], ORG
    for label, text, size, enc in PROG:
        bs = enc(labels, a)
        assert len(bs) == size, (text, bs)
        shown = ' '.join(f'{b:02X}' for b in bs[:8]) + (' …' if len(bs) > 8 else '')
        listing.append(f'{a:04X}  {shown:<27} {label + ":" if label else "":<9}{text}')
        out += bs
        a += size
    return bytes(out), listing


def keys(code):
    lines = ['DEBUG']
    for i in range(0, len(code), 16):
        lines.append(f'E {ORG + i:X} ' + ' '.join(f'{b:02X}' for b in code[i:i + 16]))
    lines += ['N ATAPIBNC.COM', 'R CX', f'{len(code):X}', 'W', 'Q', 'ATAPIBNC']
    return lines


if __name__ == '__main__':
    code, listing = assemble()
    print('\n'.join(listing))
    print(f'\n{len(code)} octets, script {len(S)} octets')
    here = os.path.dirname(os.path.abspath(__file__))
    with open(os.path.join(here, 'atapibanc.keys'), 'w') as f:
        f.write('\n'.join(keys(code)) + '\n')
    if '--com' in sys.argv:
        with open(sys.argv[sys.argv.index('--com') + 1], 'wb') as f:
            f.write(code)
