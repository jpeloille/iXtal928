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
# (cdrom_channel = 2), chargé de iso-2048.iso d'isogen ou vide. GR.2b : aussi l'ami286 et l'ami386dx,
# chargés. Le même programme sert à tous. Depuis GR.2b, son script est une table (TABLE_LIGNES) : une
# ligne par commande et par cas limite, sur les 30 étiquettes de scsi_cd_command, READ CD MSF et un code
# inconnu ; le prologue (UNIT ATTENTION, la LUN 1, NOT READY sans disque) ; DATA.BIN, README.TXT et les
# secteurs du volume lus de bout en bout, chaque somme égale à celle qu'isogen calcule (somme_banc). Chaque
# ligne s'imprime aussitôt, une pause toutes les 23 : le même banc sert en C# seul, contre des attendus
# (tools/gates/bancs/atapibanc.attendus et -vide), et sous boot-diff, contre l'oracle.
# Les interruptions restent permises (IRQ 15) ; l'état lu en 177h les abaisse.
#
# Usage :
#   python3 tools/atapibanc/atapibanc.py            écrit atapibanc.keys, affiche le listing et, par
#                                                   ligne de la table, la somme qu'isogen attend
#   python3 tools/atapibanc/atapibanc.py --com F    écrit aussi le .COM (objdump -D -b binary
#                                                   -mi8086 --adjust-vma=0x100 F)

import os
import struct
import sys

ORG = 0x100
# GR.2d1 — le tampon d'un secteur de l'op 17, ZBUF octets après le début des relevés (une ligne en prend au plus 40)
ZBUF = 256

# G10.5 — `--audio` : ATAPIAUD.COM, le même interprète avec l'op 14 (attendre N tops de l'horloge du BIOS),
# et le script de l'audio CD sur mixte.cue d'isogen. Sans lui, ATAPIBNC.COM reste octet pour octet celui
# de G10.4.
# GR.3i — `--ecoute` : CDECOUTE.COM, l'interprète d'ATAPIAUD, et l'écoute de ecoute.cue d'isogen (D6).
ECOUTE = '--ecoute' in sys.argv
AUDIO = '--audio' in sys.argv or ECOUTE
# G10.6 — `--zip` : ZIPBANC.COM, le même interprète (sans l'op 14), et le script du lecteur ZIP 100.
# GR.2d2 — `--esclave` : ZIPESC.COM, l'interprète de ZIPBANC octet pour octet, et le script du ZIP en esclave du CD,
# sur un disque protégé en écriture (zipro.img, IMAGE_RO).
ESC = '--esclave' in sys.argv
# GR.2d3 — `--format` : ZIPFMT.COM, l'interprète de ZIPBANC, et FORMAT UNIT du ZIP 100, dans sa porte (100 Mo écrits).
FMT = '--format' in sys.argv
ZIP = '--zip' in sys.argv or ESC or FMT
# GR.2b — ATAPIBNC.COM tiré d'une table (PLAN-GR.md, GR.2b). L'interprète gagne un paquet compact (op 15), une ligne
# par paquet imprimée aussitôt (op 16), une pause (op 9). GR.2c : ATAPIAUD.COM aussi, avec l'op 14. GR.2d1 : ZIPBANC.COM
# aussi, avec l'op 17 (un secteur rempli d'une suite arithmétique par une boucle, au lieu de ses 512 octets saisis) ;
# l'ancien interprète (les ops 10 à 13) est retiré.


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
ins(None, 'lodsb                ; S', 1, lambda L, a: [0xAC])
ins(None, 'mov [skp],ax', 3, lambda L, a: [0xA3] + w(L['skp']))
ins(None, 'call words2', 3, lambda L, a: [0xE8] + rel16(L, a + 3, 'words2'))
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
ins(None, 'jne op9', 2, lambda L, a: [0x75, rel8(L, a + 2, 'op9')])
ins(None, 'lodsw', 1, lambda L, a: [0xAD])
ins(None, 'mov bp,ax', 2, lambda L, a: [0x89, 0xC5])
ins(None, 'add ax,206h', 3, lambda L, a: [0x05, 0x06, 0x02])
ins(None, 'mov bx,ax', 2, lambda L, a: [0x89, 0xC3])
nxt()


# op 14 (G10.5, ATAPIAUD.COM seul) : attendre N tops de l'horloge du BIOS (0040:006Ch, 18,2 par seconde),
# le temps que le lecteur joue. GR.2c : émise après l'op 16, ATAPIAUD.COM passant par la table.
def emet_op14():
    ins('op14', 'cmp al,14', 2, lambda L, a: [0x3C, 0x0E])
    ins(None, 'jne done', 2, lambda L, a: [0x75, rel8(L, a + 2, 'done')])
    ins(None, 'lodsb                ; N', 1, lambda L, a: [0xAC])
    ins(None, 'xor ah,ah', 2, lambda L, a: [0x30, 0xE4])
    ins(None, 'mov cx,ax', 2, lambda L, a: [0x89, 0xC1])
    ins(None, 'push es', 1, lambda L, a: [0x06])
    ins(None, 'mov ax,40h', 3, lambda L, a: [0xB8, 0x40, 0x00])
    ins(None, 'mov es,ax', 2, lambda L, a: [0x8E, 0xC0])
    ins(None, 'mov dx,[es:6Ch]', 5, lambda L, a: [0x26, 0x8B, 0x16, 0x6C, 0x00])
    ins('t14', 'mov ax,[es:6Ch]', 4, lambda L, a: [0x26, 0xA1, 0x6C, 0x00])
    ins(None, 'sub ax,dx', 2, lambda L, a: [0x29, 0xD0])
    ins(None, 'cmp ax,cx', 2, lambda L, a: [0x39, 0xC8])
    ins(None, 'jb t14', 2, lambda L, a: [0x72, rel8(L, a + 2, 't14')])
    ins(None, 'pop es', 1, lambda L, a: [0x07])
    nxt()

# GR.2b (en table) — op 9 : une pause, une touche attendue (INT 16h) ; la page de relevés reste à l'écran.
ins('op9', 'cmp al,9', 2, lambda L, a: [0x3C, 0x09])
ins(None, 'jne op15', 2, lambda L, a: [0x75, rel8(L, a + 2, 'op15')])
ins(None, 'xor ah,ah', 2, lambda L, a: [0x30, 0xE4])
ins(None, 'int 16h', 2, lambda L, a: [0xCD, 0x16])
nxt()

# op 15 : le paquet compact — drapeaux, n, les n premiers octets du paquet (le reste à zéro), S, K, et, si le
# bit 1 des drapeaux, M puis M mots sortants. Bit 0 : sur ERR, REQUEST SENSE aussitôt, ASC et ASCQ ajoutés au
# relevé (la clé de sense est déjà dans 171h, bits 7-4). Bit 2 : la limite du compte d'octets à 0800h. Puis la
# ligne du paquet.
ins('op15', 'cmp al,15', 2, lambda L, a: [0x3C, 0x0F])
ins(None, 'jne op16', 2, lambda L, a: [0x75, rel8(L, a + 2, 'op16')])
ins(None, 'lodsb                ; drapeaux', 1, lambda L, a: [0xAC])
ins(None, 'mov [flg],al', 3, lambda L, a: [0xA2] + w(L['flg']))
ins(None, 'call raz', 3, lambda L, a: [0xE8] + rel16(L, a + 3, 'raz'))
ins(None, 'lodsb                ; n', 1, lambda L, a: [0xAC])
ins(None, 'xor ah,ah', 2, lambda L, a: [0x30, 0xE4])
ins(None, 'mov cx,ax', 2, lambda L, a: [0x89, 0xC1])
ins(None, 'push di', 1, lambda L, a: [0x57])
ins(None, 'mov di,cdbb', 3, lambda L, a: [0xBF] + w(L['cdbb']))
ins(None, 'rep movsb', 2, lambda L, a: [0xF3, 0xA4])
ins(None, 'pop di', 1, lambda L, a: [0x5F])
ins(None, 'lodsb                ; S', 1, lambda L, a: [0xAC])
ins(None, 'mov [skp],ax', 3, lambda L, a: [0xA3] + w(L['skp']))
ins(None, 'lodsb                ; K', 1, lambda L, a: [0xAC])
ins(None, 'mov [keep],ax', 3, lambda L, a: [0xA3] + w(L['keep']))
ins(None, 'call pkt', 3, lambda L, a: [0xE8] + rel16(L, a + 3, 'pkt'))
ins(None, 'test byte [flg],1', 5, lambda L, a: [0xF6, 0x06] + w(L['flg']) + [0x01])
ins(None, 'jz l15', 2, lambda L, a: [0x74, rel8(L, a + 2, 'l15')])
ins(None, 'test byte [st],1     ; ERR ?', 5, lambda L, a: [0xF6, 0x06] + w(L['st']) + [0x01])
ins(None, 'jz l15', 2, lambda L, a: [0x74, rel8(L, a + 2, 'l15')])
ins(None, 'call raz', 3, lambda L, a: [0xE8] + rel16(L, a + 3, 'raz'))
ins(None, 'mov byte [cdbb],3    ; REQUEST SENSE', 5, lambda L, a: [0xC6, 0x06] + w(L['cdbb']) + [0x03])
ins(None, 'mov byte [cdbb+4],18', 5, lambda L, a: [0xC6, 0x06] + w(L['cdbb'] + 4) + [18])
ins(None, 'mov word [skp],12', 6, lambda L, a: [0xC7, 0x06] + w(L['skp']) + [12, 0])
ins(None, 'mov word [keep],2', 6, lambda L, a: [0xC7, 0x06] + w(L['keep']) + [2, 0])
ins(None, 'mov byte [flg],80h   ; muet : ASC et ASCQ seuls', 5,
    lambda L, a: [0xC6, 0x06] + w(L['flg']) + [0x80])
ins(None, 'call pkt', 3, lambda L, a: [0xE8] + rel16(L, a + 3, 'pkt'))
ins('l15', 'call line', 3, lambda L, a: [0xE8] + rel16(L, a + 3, 'line'))
nxt()

# op 16 : la ligne des relevés depuis la dernière (la signature, IDENTIFY)
ins('op16', 'cmp al,16', 2, lambda L, a: [0x3C, 0x10])
APRES16 = 'op17' if ZIP else 'op14' if AUDIO else 'done'
ins(None, f'jne {APRES16}', 2, lambda L, a: [0x75, rel8(L, a + 2, APRES16)])
ins(None, 'call line', 3, lambda L, a: [0xE8] + rel16(L, a + 3, 'line'))
nxt()

# op 17 (GR.2d1) : le tampon d'un secteur (512 octets, ZBUF après les relevés), rempli de (i × A + B) & FFh
if ZIP:
    ins('op17', 'cmp al,17', 2, lambda L, a: [0x3C, 0x11])
    ins(None, 'jne done', 2, lambda L, a: [0x75, rel8(L, a + 2, 'done')])
    ins(None, 'lodsb                ; A', 1, lambda L, a: [0xAC])
    ins(None, 'mov dl,al', 2, lambda L, a: [0x88, 0xC2])
    ins(None, 'lodsb                ; B', 1, lambda L, a: [0xAC])
    ins(None, 'push di', 1, lambda L, a: [0x57])
    ins(None, 'mov di,zbuf', 3, lambda L, a: [0xBF] + w(L['results'] + ZBUF))
    ins(None, 'mov cx,512', 3, lambda L, a: [0xB9, 0x00, 0x02])
    ins('f17', 'stosb', 1, lambda L, a: [0xAA])
    ins(None, 'add al,dl', 2, lambda L, a: [0x00, 0xD0])
    ins(None, 'loop f17', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'f17')])
    ins(None, 'pop di', 1, lambda L, a: [0x5F])
    nxt()

if AUDIO:
    emet_op14()

# op 0 (et tout inconnu) : afficher les relevés, seize octets par ligne, puis sortir
# GR.2b : chaque ligne est déjà imprimée ; il reste la dernière, s'il en est une
ins('done', 'cmp di,results', 4, lambda L, a: [0x81, 0xFF] + w(L['results']))
ins(None, 'je fin', 2, lambda L, a: [0x74, rel8(L, a + 2, 'fin')])
ins(None, 'call line', 3, lambda L, a: [0xE8] + rel16(L, a + 3, 'line'))
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

# bsy : attendre BSY à zéro sur base+7 (au plus 65 535 lectures) ; rend l'état dans AL. GR.2b, en table : 32 fois
# 65 535 lectures, la lecture de 40 secteurs à 4x durant plus que 65 535 lectures sur l'ami286 (déduit)
ins('bsy', 'mov dx,bp', 2, lambda L, a: [0x89, 0xEA])
ins(None, 'add dx,7', 3, lambda L, a: [0x83, 0xC2, 0x07])
ins(None, 'mov byte [bso],32', 5, lambda L, a: [0xC6, 0x06] + w(L['bso']) + [32])
ins('wb0', 'mov cx,0FFFFh', 3, lambda L, a: [0xB9, 0xFF, 0xFF])
ins('wb', 'in al,dx', 1, lambda L, a: [0xEC])
ins(None, 'test al,80h', 2, lambda L, a: [0xA8, 0x80])
ins(None, 'jz wbx', 2, lambda L, a: [0x74, rel8(L, a + 2, 'wbx')])
ins(None, 'loop wb', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'wb')])
ins(None, 'dec byte [bso]', 4, lambda L, a: [0xFE, 0x0E] + w(L['bso']))
ins(None, 'jnz wb0', 2, lambda L, a: [0x75, rel8(L, a + 2, 'wb0')])
ins('wbx', 'ret', 1, lambda L, a: [0xC3])

# wdrq : attendre BSY à zéro et DRQ ou ERR, sur le même compte
ins('wdrq', 'mov dx,bp', 2, lambda L, a: [0x89, 0xEA])
ins(None, 'add dx,7', 3, lambda L, a: [0x83, 0xC2, 0x07])
ins(None, 'mov byte [bso],32', 5, lambda L, a: [0xC6, 0x06] + w(L['bso']) + [32])
ins('wq0', 'mov cx,0FFFFh', 3, lambda L, a: [0xB9, 0xFF, 0xFF])
ins('wq', 'in al,dx', 1, lambda L, a: [0xEC])
ins(None, 'test al,80h', 2, lambda L, a: [0xA8, 0x80])
ins(None, 'jnz wqn', 2, lambda L, a: [0x75, rel8(L, a + 2, 'wqn')])
ins(None, 'test al,09h', 2, lambda L, a: [0xA8, 0x09])
ins(None, 'jnz wqx', 2, lambda L, a: [0x75, rel8(L, a + 2, 'wqx')])
ins('wqn', 'loop wq', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'wq')])
ins(None, 'dec byte [bso]', 4, lambda L, a: [0xFE, 0x0E] + w(L['bso']))
ins(None, 'jnz wq0', 2, lambda L, a: [0x75, rel8(L, a + 2, 'wq0')])
ins('wqx', 'ret', 1, lambda L, a: [0xC3])


# raz : le paquet à zéro (douze octets en cdbb)
ins('raz', 'push di', 1, lambda L, a: [0x57])
ins(None, 'push cx', 1, lambda L, a: [0x51])
ins(None, 'mov di,cdbb', 3, lambda L, a: [0xBF] + w(L['cdbb']))
ins(None, 'mov cx,6', 3, lambda L, a: [0xB9, 0x06, 0x00])
ins(None, 'xor ax,ax', 2, lambda L, a: [0x31, 0xC0])
ins(None, 'rep stosw', 2, lambda L, a: [0xF3, 0xAB])
ins(None, 'pop cx', 1, lambda L, a: [0x59])
ins(None, 'pop di', 1, lambda L, a: [0x5F])
ins(None, 'ret', 1, lambda L, a: [0xC3])

# pkt : le paquet de cdbb, comme PACKET et PACKET_OUT : features 0, la limite en 174h-175h, A0h ; l'IIR quand le
# lecteur demande le paquet ; le paquet ; la phase sortante (bit 1) ; la phase entrante, bloc DRQ par bloc DRQ,
# S octets sautés puis K gardés ; le nombre de blocs, l'IIR des blocs en OU, le total et la somme ; les sept
# registres, 177h d'abord, gardé en [st]. Muet (bit 7) : les K octets seuls, et 177h lu.
ins('pkt', 'mov dx,bp', 2, lambda L, a: [0x89, 0xEA])
ins(None, 'inc dx', 1, lambda L, a: [0x42])
ins(None, 'xor al,al', 2, lambda L, a: [0x30, 0xC0])
ins(None, 'out dx,al            ; 171h', 1, lambda L, a: [0xEE])
ins(None, 'mov ax,0FFFEh', 3, lambda L, a: [0xB8, 0xFE, 0xFF])
ins(None, 'test byte [flg],4', 5, lambda L, a: [0xF6, 0x06] + w(L['flg']) + [0x04])
ins(None, 'jz pk1', 2, lambda L, a: [0x74, rel8(L, a + 2, 'pk1')])
ins(None, 'mov ax,0800h', 3, lambda L, a: [0xB8, 0x00, 0x08])
ins('pk1', 'add dx,3', 3, lambda L, a: [0x83, 0xC2, 0x03])
ins(None, 'out dx,al            ; 174h', 1, lambda L, a: [0xEE])
ins(None, 'inc dx', 1, lambda L, a: [0x42])
ins(None, 'mov al,ah', 2, lambda L, a: [0x88, 0xE0])
ins(None, 'out dx,al            ; 175h', 1, lambda L, a: [0xEE])
ins(None, 'add dx,2', 3, lambda L, a: [0x83, 0xC2, 0x02])
ins(None, 'mov al,0A0h', 2, lambda L, a: [0xB0, 0xA0])
ins(None, 'out dx,al            ; 177h : PACKET', 1, lambda L, a: [0xEE])
ins(None, 'call wdrq', 3, lambda L, a: [0xE8] + rel16(L, a + 3, 'wdrq'))
ins(None, 'mov dx,bp', 2, lambda L, a: [0x89, 0xEA])
ins(None, 'add dx,2', 3, lambda L, a: [0x83, 0xC2, 0x02])
ins(None, 'in al,dx             ; l\'IIR', 1, lambda L, a: [0xEC])
ins(None, 'test byte [flg],80h', 5, lambda L, a: [0xF6, 0x06] + w(L['flg']) + [0x80])
ins(None, 'jnz pk2', 2, lambda L, a: [0x75, rel8(L, a + 2, 'pk2')])
ins(None, 'stosb', 1, lambda L, a: [0xAA])
ins('pk2', 'mov dx,bp', 2, lambda L, a: [0x89, 0xEA])
ins(None, 'mov cx,6', 3, lambda L, a: [0xB9, 0x06, 0x00])
ins(None, 'push si', 1, lambda L, a: [0x56])
ins(None, 'mov si,cdbb', 3, lambda L, a: [0xBE] + w(L['cdbb']))
ins(None, 'rep outsw', 2, lambda L, a: [0xF3, 0x6F])
ins(None, 'pop si', 1, lambda L, a: [0x5E])
if ZIP:
    # GR.2d1 (ZIPBANC seul) : la phase sortante depuis le tampon de l'op 17
    ins(None, 'test byte [flg],8', 5, lambda L, a: [0xF6, 0x06] + w(L['flg']) + [0x08])
    ins(None, 'jz pk5', 2, lambda L, a: [0x74, rel8(L, a + 2, 'pk5')])
    ins(None, 'lodsw                ; M, depuis le tampon', 1, lambda L, a: [0xAD])
    ins(None, 'mov cx,ax', 2, lambda L, a: [0x89, 0xC1])
    ins(None, 'push cx', 1, lambda L, a: [0x51])
    ins(None, 'call bsy', 3, lambda L, a: [0xE8] + rel16(L, a + 3, 'bsy'))
    ins(None, 'pop cx', 1, lambda L, a: [0x59])
    ins(None, 'test al,08h          ; DRQ ?', 2, lambda L, a: [0xA8, 0x08])
    ins(None, 'jz pk3', 2, lambda L, a: [0x74, rel8(L, a + 2, 'pk3')])
    ins(None, 'mov dx,bp', 2, lambda L, a: [0x89, 0xEA])
    ins(None, 'push si', 1, lambda L, a: [0x56])
    ins(None, 'mov si,zbuf', 3, lambda L, a: [0xBE] + w(L['results'] + ZBUF))
    ins(None, 'rep outsw', 2, lambda L, a: [0xF3, 0x6F])
    ins(None, 'pop si', 1, lambda L, a: [0x5E])
    ins(None, 'jmp pk3', 3, lambda L, a: [0xE9] + rel16(L, a + 3, 'pk3'))
ins('pk5' if ZIP else None, 'test byte [flg],2', 5, lambda L, a: [0xF6, 0x06] + w(L['flg']) + [0x02])
ins(None, 'jz pk3', 2, lambda L, a: [0x74, rel8(L, a + 2, 'pk3')])
ins(None, 'lodsb                ; M', 1, lambda L, a: [0xAC])
ins(None, 'xor ah,ah', 2, lambda L, a: [0x30, 0xE4])
ins(None, 'mov cx,ax', 2, lambda L, a: [0x89, 0xC1])
ins(None, 'push cx', 1, lambda L, a: [0x51])
ins(None, 'call bsy', 3, lambda L, a: [0xE8] + rel16(L, a + 3, 'bsy'))
ins(None, 'pop cx', 1, lambda L, a: [0x59])
ins(None, 'test al,08h          ; DRQ ?', 2, lambda L, a: [0xA8, 0x08])
ins(None, 'jz pk4', 2, lambda L, a: [0x74, rel8(L, a + 2, 'pk4')])
ins(None, 'mov dx,bp', 2, lambda L, a: [0x89, 0xEA])
ins(None, 'rep outsw', 2, lambda L, a: [0xF3, 0x6F])
ins(None, 'jmp pk3', 2, lambda L, a: [0xEB, rel8(L, a + 2, 'pk3')])
ins('pk4', 'shl cx,1', 2, lambda L, a: [0xD1, 0xE1])
ins(None, 'add si,cx', 2, lambda L, a: [0x01, 0xCE])
ins('pk3', 'xor ax,ax', 2, lambda L, a: [0x31, 0xC0])
ins(None, 'mov [tot],ax', 3, lambda L, a: [0xA3] + w(L['tot']))
ins(None, 'mov [sum],ax', 3, lambda L, a: [0xA3] + w(L['sum']))
ins(None, 'mov [blk],al', 3, lambda L, a: [0xA2] + w(L['blk']))
ins(None, 'mov [iir],al', 3, lambda L, a: [0xA2] + w(L['iir']))
ins('d15', 'call bsy', 3, lambda L, a: [0xE8] + rel16(L, a + 3, 'bsy'))
ins(None, 'test al,08h          ; DRQ ?', 2, lambda L, a: [0xA8, 0x08])
ins(None, 'jz e15', 2, lambda L, a: [0x74, rel8(L, a + 2, 'e15')])
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
ins(None, 'jcxz e15             ; compte nul : fin', 2, lambda L, a: [0xE3, rel8(L, a + 2, 'e15')])
ins(None, 'call words2', 3, lambda L, a: [0xE8] + rel16(L, a + 3, 'words2'))
ins(None, 'jmp d15', 2, lambda L, a: [0xEB, rel8(L, a + 2, 'd15')])
ins('e15', 'mov dx,bp', 2, lambda L, a: [0x89, 0xEA])
ins(None, 'add dx,7', 3, lambda L, a: [0x83, 0xC2, 0x07])
ins(None, 'test byte [flg],80h', 5, lambda L, a: [0xF6, 0x06] + w(L['flg']) + [0x80])
ins(None, 'jz e15r', 2, lambda L, a: [0x74, rel8(L, a + 2, 'e15r')])
ins(None, 'in al,dx', 1, lambda L, a: [0xEC])
ins(None, 'ret', 1, lambda L, a: [0xC3])
ins('e15r', 'mov al,[blk]', 3, lambda L, a: [0xA0] + w(L['blk']))
ins(None, 'stosb', 1, lambda L, a: [0xAA])
ins(None, 'mov al,[iir]', 3, lambda L, a: [0xA0] + w(L['iir']))
ins(None, 'stosb', 1, lambda L, a: [0xAA])
ins(None, 'mov ax,[tot]', 3, lambda L, a: [0xA1] + w(L['tot']))
ins(None, 'stosw', 1, lambda L, a: [0xAB])
ins(None, 'mov ax,[sum]', 3, lambda L, a: [0xA1] + w(L['sum']))
ins(None, 'stosw', 1, lambda L, a: [0xAB])
ins(None, 'in al,dx             ; 177h', 1, lambda L, a: [0xEC])
ins(None, 'mov [st],al', 3, lambda L, a: [0xA2] + w(L['st']))
ins(None, 'stosb', 1, lambda L, a: [0xAA])
for r in (1, 2, 3, 4, 5, 6):
    ins(None, 'mov dx,bp', 2, lambda L, a: [0x89, 0xEA])
    ins(None, f'add dx,{r}', 3, lambda L, a, r=r: [0x83, 0xC2, r])
    ins(None, 'in al,dx', 1, lambda L, a: [0xEC])
    ins(None, 'stosb', 1, lambda L, a: [0xAA])
ins(None, 'ret', 1, lambda L, a: [0xC3])

# words2 : comme words, les [skp] premiers octets sautés avant les [keep] gardés
ins('words2', 'mov dx,bp', 2, lambda L, a: [0x89, 0xEA])
ins('rw2', 'in ax,dx', 1, lambda L, a: [0xED])
ins(None, 'rol word [sum],1', 4, lambda L, a: [0xD1, 0x06] + w(L['sum']))
ins(None, 'add [sum],ax', 4, lambda L, a: [0x01, 0x06] + w(L['sum']))
ins(None, 'cmp word [skp],0', 5, lambda L, a: [0x83, 0x3E] + w(L['skp']) + [0x00])
ins(None, 'je rw2k', 2, lambda L, a: [0x74, rel8(L, a + 2, 'rw2k')])
ins(None, 'sub word [skp],2', 5, lambda L, a: [0x83, 0x2E] + w(L['skp']) + [0x02])
ins(None, 'jmp rw2n', 2, lambda L, a: [0xEB, rel8(L, a + 2, 'rw2n')])
ins('rw2k', 'cmp word [keep],0', 5, lambda L, a: [0x83, 0x3E] + w(L['keep']) + [0x00])
ins(None, 'je rw2n', 2, lambda L, a: [0x74, rel8(L, a + 2, 'rw2n')])
ins(None, 'stosw', 1, lambda L, a: [0xAB])
ins(None, 'sub word [keep],2', 5, lambda L, a: [0x83, 0x2E] + w(L['keep']) + [0x02])
ins('rw2n', 'loop rw2', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'rw2')])
ins(None, 'ret', 1, lambda L, a: [0xC3])

# line : le numéro de la ligne, puis les relevés depuis la précédente, en hexadécimal ; la zone est rendue
ins('line', 'push si', 1, lambda L, a: [0x56])
ins(None, 'mov al,[rown]', 3, lambda L, a: [0xA0] + w(L['rown']))
ins(None, 'call hex2', 3, lambda L, a: [0xE8] + rel16(L, a + 3, 'hex2'))
ins(None, 'inc byte [rown]', 4, lambda L, a: [0xFE, 0x06] + w(L['rown']))
ins(None, "mov dl,' '", 2, lambda L, a: [0xB2, 0x20])
ins(None, 'mov ah,2', 2, lambda L, a: [0xB4, 0x02])
ins(None, 'int 21h', 2, lambda L, a: [0xCD, 0x21])
ins(None, 'mov si,results', 3, lambda L, a: [0xBE] + w(L['results']))
ins('ln1', 'cmp si,di', 2, lambda L, a: [0x39, 0xFE])
ins(None, 'jae ln2', 2, lambda L, a: [0x73, rel8(L, a + 2, 'ln2')])
ins(None, 'lodsb', 1, lambda L, a: [0xAC])
ins(None, 'call hex2', 3, lambda L, a: [0xE8] + rel16(L, a + 3, 'hex2'))
ins(None, 'jmp ln1', 2, lambda L, a: [0xEB, rel8(L, a + 2, 'ln1')])
ins('ln2', 'mov dx,crlf', 3, lambda L, a: [0xBA] + w(L['crlf']))
ins(None, 'mov ah,9', 2, lambda L, a: [0xB4, 0x09])
ins(None, 'int 21h', 2, lambda L, a: [0xCD, 0x21])
ins(None, 'mov di,results', 3, lambda L, a: [0xBF] + w(L['results']))
ins(None, 'pop si', 1, lambda L, a: [0x5E])
ins(None, 'ret', 1, lambda L, a: [0xC3])
ins('hex2', 'push ax', 1, lambda L, a: [0x50])
ins(None, 'shr al,4', 3, lambda L, a: [0xC0, 0xE8, 0x04])
ins(None, 'call nib', 3, lambda L, a: [0xE8] + rel16(L, a + 3, 'nib'))
ins(None, 'pop ax', 1, lambda L, a: [0x58])
ins(None, 'and al,0Fh', 2, lambda L, a: [0x24, 0x0F])
ins(None, 'jmp nib', 3, lambda L, a: [0xE9] + rel16(L, a + 3, 'nib'))


# ---- le script -----------------------------------------------------------------------------
def OUT(reg, val): return [1, reg, val]
WAIT = [2]
REC = [5]
WDRQ = [6]
def BASE(b): return [7] + w(b)


# La page audio (0Eh) : les sélecteurs et volumes des ports de sortie, reconnaissables.
PAGE_0E = [0x0E, 0x0E, 0x05, 0x04, 0x00, 0x80, 0x00, 75, 0x01, 0x80, 0x02, 0x40, 0, 0, 0, 0]

def WAITT(n): return [14, n]


def SUBQ(msf):
    """READ SUB-CHANNEL, format 1 (position courante), données SUBQ : l'état audio, la piste, l'index et
    les deux positions, absolue et relative, en LBA ou en MSF."""
    return [0x42, 2 if msf else 0, 0x40, 1, 0, 0, 0, 0, 16]



# ---- GR.2b : la table d'ATAPIBNC.COM -------------------------------------------------------------------------------
def P(cdb, keep=0, skip=0, sense=True, limite=None, sortie=None, tampon=0):
    """Une ligne compacte (op 15) : les octets du paquet jusqu'au dernier non nul, S et K, la phase sortante : les
    octets du script (sortie), ou TAMPON mots du secteur que l'op 17 a rempli (bit 3, GR.2d1)."""
    assert len(cdb) <= 12 and keep % 2 == 0 and skip % 2 == 0 and keep <= 22 and not (sortie and tampon)
    n = len(cdb)
    while n and cdb[n - 1] == 0:
        n -= 1
    flg = (1 if sense else 0) | (2 if sortie else 0) | (4 if limite == 0x0800 else 0) | (8 if tampon else 0)
    r = [15, flg, n] + list(cdb[:n]) + [skip, keep]
    if sortie:
        assert len(sortie) % 2 == 0 and len(sortie) // 2 <= 255
        r += [len(sortie) // 2] + list(sortie)
    if tampon:
        r += w(tampon)
    return r


def SUITE(a, b):
    """Les 512 octets que REMPLIR(a, b) pose dans le tampon."""
    return bytes((i * a + b) & 0xFF for i in range(512))


def REMPLIR(a, b):
    """L'op 17 : le secteur du tampon, ses 512 octets valant (i × A + B) & FFh (GR.2d1)."""
    return [17, a & 0xFF, b & 0xFF]


def LIGNE():
    return [16]


PAUSE = [9]


def C(*b):
    return list(b)


def R10(lba, n):
    return C(0x28, 0, (lba >> 24) & 0xFF, (lba >> 16) & 0xFF, (lba >> 8) & 0xFF, lba & 0xFF, 0, n >> 8, n & 0xFF)


def R12(lba, n):
    return C(0xA8, 0, (lba >> 24) & 0xFF, (lba >> 16) & 0xFF, (lba >> 8) & 0xFF, lba & 0xFF,
             (n >> 24) & 0xFF, (n >> 16) & 0xFF, (n >> 8) & 0xFF, n & 0xFF)


def _iso():
    sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'isogen'))
    import isogen
    return isogen


def SOMME(lba, n):
    """La somme d'isogen des secteurs lba à lba + n - 1 de iso-2048.iso (GR.2b, l'attendu absolu)."""
    iso = _iso()
    return iso.somme_banc(b''.join(iso.volume()[lba:lba + n]))


# (nom, rangée de script, K, S, la somme d'isogen attendue ou None). L'ordre est celui du relevé ; une PAUSE n'a pas de
# ligne.
TABLE_LIGNES = []


def T(nom, rangee, k=0, s_=0, attendu=None):
    # une pause toutes les PAGE lignes : la page reste à l'écran (25 lignes, l'invite et la commande comprises)
    # (un nom qui commence par « - » n'imprime pas de ligne : la pause, l'attente de l'op 14)
    # Les lignes comptées depuis la dernière pause : une pause explicite (T('-', PAUSE)) place la page.
    lignes = 0
    for t in reversed(TABLE_LIGNES):
        if t[1] == PAUSE:
            break
        lignes += not t[0].startswith('-')
    if lignes == PAGE and not nom.startswith('-') and rangee != PAUSE:
        TABLE_LIGNES.append(('-', PAUSE, 0, 0, None))
    TABLE_LIGNES.append((nom, rangee, k, s_, attendu))


PAGE = 23


if not AUDIO and not ZIP:
    T('signature', BASE(0x170) + OUT(6, 0xA0) + WAIT + REC + LIGNE())               # 01 01 14 EB
    T('IDENTIFY DEVICE', OUT(7, 0xEC) + WAIT + REC + LIGNE())                        # ABRT
    T('IDENTIFY PACKET DEVICE', OUT(7, 0xA1) + WDRQ + [3] + w(256) + [24, 54] + REC + LIGNE(), 24, 54)  # le modèle
    T('TEST UNIT READY', P(C(0x00), sense=False))                                    # UNIT ATTENTION, 28h, gardée
    T('TEST UNIT READY, sous l\'attention', P(C(0x00), sense=False))                 # is_error : rendu sans effacer
    T('INQUIRY, sous l\'attention', P(C(0x12, 0, 0, 0, 36), sense=False))            # ALLOW_UA : passe, efface
    T('TEST UNIT READY', P(C(0x00)))                                                 # GOOD
    T('TEST UNIT READY, LUN 1', P(C(0x00, 0x20)))                                    # le prologue : 25h
    T('REQUEST SENSE', P(C(0x03, 0, 0, 0, 18), 14, sense=False))                     # le 25h de la LUN 1, rendu
    T('REQUEST SENSE, longueur 0', P(C(0x03), sense=False))
    T('INQUIRY', P(C(0x12, 0, 0, 0, 36), 22, 8), 22, 8)                              # le fabricant et le modèle
    T('INQUIRY, EVPD 00h', P(C(0x12, 1, 0x00, 0, 0xFF), 8), 8)
    T('INQUIRY, EVPD 83h', P(C(0x12, 1, 0x83, 0, 0xFF), 22), 22)
    T('INQUIRY, EVPD 83h, 20 octets', P(C(0x12, 1, 0x83, 0, 20)))                    # trop court : 4Bh
    T('INQUIRY, EVPD 83h, 40 octets', P(C(0x12, 1, 0x83, 0, 40), 8, 24), 8, 24)      # sans le second descripteur
    T('INQUIRY, EVPD 80h', P(C(0x12, 1, 0x80, 0, 0xFF)))                             # page absente : 24h
    T('INQUIRY, 5 octets', P(C(0x12, 0, 0, 0, 5), 6), 6)                              # la page courte
    T('READ CAPACITY', P(C(0x25), 8), 8)                                              # PB-117
    T('READ(10), le PVD', P(R10(16, 1), 8), 8, 0, SOMME(16, 1))
    T('READ(10), le terminateur', P(R10(17, 1)), 0, 0, SOMME(17, 1))
    T('READ(10), les tables de chemins', P(R10(18, 2)), 0, 0, SOMME(18, 2))
    T('READ(10), la racine', P(R10(20, 1), 22, 68), 22, 68, SOMME(20, 1))           # l'entrée de DATA.BIN
    T('READ(10), SUBDIR', P(R10(21, 1)), 0, 0, SOMME(21, 1))
    T('READ(10), README.TXT', P(R10(22, 1), 22), 22, 0, SOMME(22, 1))
    T('READ(10), DATA.BIN', P(R10(23, 5)), 0, 0, SOMME(23, 5))
    T('READ(10), NESTED.TXT', P(R10(28, 1), 18), 18, 0, SOMME(28, 1))
    T('READ(10), quatre blocs DRQ', P(R10(20, 4), limite=0x0800), 0, 0, SOMME(20, 4))
    T('READ CD, données', P(C(0xBE, 0, 0, 0, 0, 16, 0, 0, 1, 0x10), 8), 8, 0, SOMME(16, 1))
    T('READ CD, brut', P(C(0xBE, 0, 0, 0, 0, 16, 0, 0, 1, 0xF8), 16), 16)           # piste cuite : data_in tel quel
    T('READ CD, octet 9 à 08h', P(C(0xBE, 0, 0, 0, 0, 16, 0, 0, 1, 0x08)))            # 20h
    T('READ CD MSF', P(C(0xB9, 0, 0, 0, 2, 16, 0, 2, 17, 0x10), 8), 8)               # PB-271
    T('READ(10), le dernier secteur', P(R10(31, 1)), 0, 0, SOMME(31, 1))
    T('READ(10), le secteur 32', P(R10(32, 1)))                                       # PB-117 : 21h
    T('READ(10), 30 à 33', P(R10(30, 4)))                                             # PB-268
    T('READ(10), 40 secteurs', P(R10(0, 40)))                                         # PB-268
    T('READ(10), longueur 0', P(R10(16, 0)))
    T('READ(6)', P(C(0x08, 0, 0, 16, 1), 8), 8, 0, SOMME(16, 1))                     # PB-270
    T('READ(6), longueur 0', P(C(0x08, 0, 0, 16, 0)))                                 # PB-270
    T('READ(6), 40 secteurs', P(C(0x08, 0, 0, 0, 40)))                                # PB-268, PB-270
    T('READ(12)', P(R12(16, 1), 8), 8, 0, SOMME(16, 1))
    T('READ(12), longueur 0', P(R12(16, 0)))
    T('READ(12), 40 secteurs', P(R12(0, 40)))                                         # PB-268
    T('READ CD, 40 secteurs', P(C(0xBE, 0, 0, 0, 0, 0, 0, 0, 40, 0x10)))             # PB-268
    T('READ CD brut, 31 et 32', P(C(0xBE, 0, 0, 0, 0, 31, 0, 0, 2, 0xF8)))           # PB-268
    T('SEEK(6)', P(C(0x0B, 0, 0, 16)))                                                # PB-270
    T('SEEK(10)', P(C(0x2B, 0, 0, 0, 0, 16)))
    T('REZERO UNIT', P(C(0x01)))                                                      # PB-270
    T('READ TOC, LBA', P(C(0x43, 0, 0, 0, 0, 0, 0, 0, 0x64), 20), 20)               # PB-122
    T('READ TOC, MSF', P(C(0x43, 2, 0, 0, 0, 0, 0, 0, 0x64), 20), 20)
    T('READ TOC, sessions', P(C(0x43, 0, 1, 0, 0, 0, 0, 0, 0x64), 12), 12)
    T('READ TOC, brute', P(C(0x43, 0, 2, 0, 0, 0, 0, 0, 0x64), 22), 22)             # PB-120
    T('READ TOC, format 3', P(C(0x43, 0, 3, 0, 0, 0, 0, 0, 0x64)))                    # 21h
    T('READ TOC, format en octet 9', P(C(0x43, 0, 0, 0, 0, 0, 0, 0, 0x64, 0x40), 12), 12)  # cdb[9] >> 6 : sessions
    T('READ HEADER', P(C(0x44, 0, 0, 0, 0, 16, 0, 0, 8), 8), 8)
    T('READ HEADER, MSF', P(C(0x44, 2, 0, 0, 0, 16, 0, 0, 8)))                        # 20h
    T('READ SUB-CHANNEL', P(C(0x42, 0, 0x40, 1, 0, 0, 0, 0, 16), 16), 16)
    T('READ SUB-CHANNEL, format 2', P(C(0x42, 0, 0x40, 2, 0, 0, 0, 0, 16)))           # 20h
    T('READ SUB-CHANNEL, sans SubQ', P(C(0x42, 0, 0, 1, 0, 0, 0, 0, 16), 4), 4)
    T('MODE SENSE(10), 2Ah', P(C(0x5A, 0, 0x2A, 0, 0, 0, 0, 0, 0x40), 22), 22)
    T('MODE SENSE(10), toutes', P(C(0x5A, 0, 0x3F, 0, 0, 0, 0, 0, 0x80)))
    T('MODE SENSE(10), 01h', P(C(0x5A, 0, 0x01, 0, 0, 0, 0, 0, 0x20), 16), 16)
    T('MODE SENSE(10), 0Dh', P(C(0x5A, 0, 0x0D, 0, 0, 0, 0, 0, 0x20), 16), 16)
    T('MODE SENSE(10), longueur 0', P(C(0x5A, 0, 0x2A)))
    T('MODE SENSE(10), 08h', P(C(0x5A, 0, 0x08, 0, 0, 0, 0, 0, 0x20)))              # page absente : 24h
    T('MODE SENSE(6)', P(C(0x1A, 0, 0x3F, 0, 0xFF), 8), 8)                            # PB-270
    T('MODE SELECT(10), 0Eh', P(C(0x55, 0x10, 0, 0, 0, 0, 0, 0, 24), sortie=[0] * 8 + PAGE_0E))
    T('MODE SENSE(10), 0Eh', P(C(0x5A, 0, 0x0E, 0, 0, 0, 0, 0, 0x20), 22, 2), 22, 2)  # relue
    T('MODE SELECT(6)', P(C(0x15, 0x10, 0, 0, 20), sortie=[0] * 4 + PAGE_0E))        # PB-119, PB-270
    T('GET EVENT STATUS', P(C(0x4A, 1, 0, 0, 0x10, 0, 0, 0, 8), 8), 8)              # PB-118, PB-270
    T('GET EVENT STATUS', P(C(0x4A, 1, 0, 0, 0x10, 0, 0, 0, 8), 8), 8)
    T('GET EVENT STATUS, asynchrone', P(C(0x4A, 0, 0, 0, 0x10, 0, 0, 0, 8)))
    T('GET EVENT STATUS, alimentation', P(C(0x4A, 1, 0, 0, 0x04, 0, 0, 0, 8), 8), 8)
    T('PREVENT ALLOW, empêcher', P(C(0x1E, 0, 0, 0, 1)))
    T('PREVENT ALLOW, permettre', P(C(0x1E)))
    T('START STOP UNIT, démarrer', P(C(0x1B, 0, 0, 0, 1)))
    T('START STOP UNIT, arrêter', P(C(0x1B)))
    T('SET SPEED, le maximum', P(C(0xBB, 0, 0xFF, 0xFF)))
    T('SET SPEED, 1', P(C(0xBB, 0, 0, 1)))
    T('SET SPEED, 2x', P(C(0xBB, 0, 0x01, 0x61)))                                     # 353 Ko/s, entre les bornes
    T('MECHANISM STATUS', P(C(0xBD, 0, 0, 0, 0, 0, 0, 0, 0, 8), 8), 8)
    T('MECHANISM STATUS, 4 octets', P(C(0xBD, 0, 0, 0, 0, 0, 0, 0, 0, 4), 4), 4)    # PB-269
    T('READ DISC INFORMATION', P(C(0x51, 0, 0, 0, 0, 0, 0, 0, 0x22), 22), 22)       # PB-270
    T('PLAY AUDIO(10)', P(C(0x45, 0, 0, 0, 0, 0, 0, 0, 10)))                          # piste de données : 64h
    T('PLAY AUDIO MSF', P(C(0x47, 0, 0, 0, 2, 0, 0, 2, 10)))
    T('PLAY AUDIO(12)', P(C(0xA5, 0, 0, 0, 0, 0, 0, 0, 0, 10)))                      # PB-270
    T('PAUSE', P(C(0x4B)))
    T('STOP PLAY/SCAN', P(C(0x4E)))
    T('SEND DVD STRUCTURE', P(C(0xAD)))
    T('un code inconnu', P(C(0xD8)))


if AUDIO:
    # GR.2c — ATAPIAUD sur la même table (PLAN-GR.md, GR.2c), sur mixte.cue d'isogen : la piste 1 de données, 32
    # secteurs en LBA 0 (iso-2048.iso) ; la piste 2 audio en LBA 42, 30 secteurs ; la piste 3 audio en LBA 222, 25
    # secteurs, après un PREGAP de 150. PCem prend la position d'un PLAY AUDIO comme celle du lecteur, décalée de 150
    # (PB-123). L'attente (op 14) compte les tops de l'horloge du BIOS, 18,2 par seconde.
    PAGE_0E_AUDIO = [0x0E, 0x0E, 0x05, 0x04, 0x00, 0x80, 0x00, 75,
                     0x02, 0x80, 0x01, 0x40, 0, 0, 0, 0]   # les deux canaux croisés, volumes 80h et 40h

    def MSF(lba):
        a = lba + 150
        return a // 4500, (a // 75) % 60, a % 75

    def PLAYMSF(debut, fin):
        return C(0x47, 0, 0, *MSF(debut), *MSF(fin))

    def ATTENTE(n):
        T('-attente', WAITT(n))

    SENSE18 = C(0x03, 0, 0, 0, 18)

if ECOUTE:
    # GR.3i — l'écoute, par Julien, sur ecoute.cue d'isogen (D6) : la disposition de mixte.cue, la piste 2 (440 Hz à
    # gauche, dix secondes) en LBA 42, la piste 3 (880 Hz à droite, dix secondes) en LBA 942, après un PREGAP de deux
    # secondes ; la fin du disque en LBA 1692. D'abord le CD du mélangeur de la SB Pro (28h) au maximum : le guide et
    # PCem le mettent au plus bas, et l'audio du CD ne s'entendrait pas. La page audio remise droite (le canal 1 à
    # gauche, le 2 à droite, au maximum), ATAPIAUD la laissant croisée ; puis un seul PLAY de 22 s, que trois relevés
    # de la position suivent : dans la piste 2, dans la piste 3 (12,9 s, après le prégap), à la fin.
    PAGE_0E_ECOUTE = [0x0E, 0x0E, 0x04, 0x00, 0x00, 0x80, 0x00, 75,
                      0x01, 0xFF, 0x02, 0xFF, 0, 0, 0, 0]
    T('-mélangeur', BASE(0x220) + OUT(4, 0x28) + OUT(5, 0xEE))
    T('signature', BASE(0x170) + OUT(6, 0xA0) + WAIT + REC + LIGNE())
    T('TEST UNIT READY', P(C(0x00), sense=False))                                    # UNIT ATTENTION, 28h
    T('INQUIRY, sous l\'attention', P(C(0x12, 0, 0, 0, 36), sense=False))
    T('TEST UNIT READY', P(C(0x00)))
    T('READ TOC, MSF', P(C(0x43, 2, 0, 0, 0, 0, 0, 0, 0x64), 22), 22)
    T('MODE SELECT(10), la page audio', P(C(0x55, 0x10, 0, 0, 0, 0, 0, 0, 24), sortie=[0] * 8 + PAGE_0E_ECOUTE))
    T('PLAY AUDIO MSF, les pistes 2 et 3', P(PLAYMSF(42, 1692)))
    ATTENTE(5)
    T('READ SUB-CHANNEL, MSF, 440 Hz', P(SUBQ(1), 16), 16)                           # la piste 2
    ATTENTE(230)
    T('READ SUB-CHANNEL, MSF, 880 Hz', P(SUBQ(1), 16), 16)                           # la piste 3
    ATTENTE(175)
    T('READ SUB-CHANNEL, MSF, finie', P(SUBQ(1), 16), 16)                            # 13h

if AUDIO and not ECOUTE:
    T('signature', BASE(0x170) + OUT(6, 0xA0) + WAIT + REC + LIGNE())
    T('TEST UNIT READY', P(C(0x00), sense=False))                                    # UNIT ATTENTION, 28h, gardée
    T('INQUIRY, sous l\'attention', P(C(0x12, 0, 0, 0, 36), sense=False))            # ALLOW_UA : passe, efface
    T('TEST UNIT READY', P(C(0x00)))                                                 # GOOD
    T('READ TOC, LBA', P(C(0x43, 0, 0, 0, 0, 0, 0, 0, 0x64), 22), 22)               # trois pistes et le lead-out
    T('READ TOC, MSF', P(C(0x43, 2, 0, 0, 0, 0, 0, 0, 0x64), 22), 22)
    T('PLAY AUDIO(10), la piste de données', P(C(0x45, 0, 0, 0, 0, 0, 0, 0, 10)))     # 64h
    T('PLAY AUDIO(10), la piste 2', P(C(0x45, 0, 0, 0, 0, 42, 0, 0, 30)))             # PB-123
    ATTENTE(3)
    T('READ SUB-CHANNEL, LBA', P(SUBQ(0), 16), 16)
    T('REQUEST SENSE', P(SENSE18, 14, sense=False), 14)
    T('MODE SELECT(10), la page audio', P(C(0x55, 0x10, 0, 0, 0, 0, 0, 0, 24), sortie=[0] * 8 + PAGE_0E_AUDIO))
    T('PLAY AUDIO MSF, la piste 2', P(PLAYMSF(42, 72)))
    ATTENTE(2)
    T('READ SUB-CHANNEL, MSF, en lecture', P(SUBQ(1), 16), 16)                       # 11h
    T('REQUEST SENSE, en lecture', P(SENSE18, 14, sense=False), 14)                  # 00/11h
    T('PAUSE', P(C(0x4B)))
    T('READ SUB-CHANNEL, MSF, en pause', P(SUBQ(1), 16), 16)                         # 12h
    T('REQUEST SENSE, en pause', P(SENSE18, 14, sense=False), 14)                    # 00/12h
    ATTENTE(2)
    T('RESUME', P(C(0x4B, 0, 0, 0, 0, 0, 0, 0, 1)))
    ATTENTE(3)
    T('READ SUB-CHANNEL, LBA, reprise', P(SUBQ(0), 16), 16)
    ATTENTE(6)
    T('READ SUB-CHANNEL, LBA, finie', P(SUBQ(0), 16), 16)                           # 13h
    T('REQUEST SENSE, audio arrêté', P(SENSE18, 14, sense=False), 14)               # « en pause » : scsi_cd.c:947-950
    T('-', PAUSE)                                    # la touche avant les deux pistes, qui ne durent que 0,6 s
    T('PLAY AUDIO MSF, pistes 2 et 3', P(PLAYMSF(42, 247)))
    ATTENTE(2)
    T('READ SUB-CHANNEL, MSF, pistes 2 et 3', P(SUBQ(1), 16), 16)
    T('READ(10) pendant l\'audio', P(R10(16, 1), 8), 8, 0, SOMME(16, 1))            # le PVD
    T('READ SUB-CHANNEL, après la lecture', P(SUBQ(0), 16), 16)
    T('READ SUB-CHANNEL, format 2', P(C(0x42, 0, 0x40, 2, 0, 0, 0, 0, 16)))          # 20h
    T('READ SUB-CHANNEL, sans SubQ', P(C(0x42, 0, 0, 1, 0, 0, 0, 0, 16), 4), 4)     # l'état audio seul
    T('SEEK(10), la piste 3', P(C(0x2B, 0, 0, 0, 0, 230)))
    T('READ SUB-CHANNEL, après SEEK', P(SUBQ(0), 16), 16)
    T('PLAY AUDIO(12), la piste 3', P(C(0xA5, 0, 0, 0, 1, 116, 0, 0, 0, 25)))        # 372 = 222 + 150 ; PB-123, 270
    T('PLAY AUDIO MSF, la piste 3', P(PLAYMSF(222, 247)))
    ATTENTE(3)
    T('READ SUB-CHANNEL, piste 3', P(SUBQ(1), 16), 16)
    T('STOP PLAY/SCAN', P(C(0x4E)))
    T('READ SUB-CHANNEL, arrêté', P(SUBQ(0), 16), 16)
    T('PREVENT ALLOW, empêcher', P(C(0x1E, 0, 0, 0, 1)))
    T('START STOP UNIT, éjecter', P(C(0x1B, 0, 0, 0, 2)))                            # image_eject, vide chez PCem
    T('TEST UNIT READY, après l\'éjection', P(C(0x00)))
    T('START STOP UNIT, charger', P(C(0x1B, 0, 0, 0, 3)))                            # image_load, vide
    T('TEST UNIT READY, après le chargement', P(C(0x00)))
    T('PREVENT ALLOW, permettre', P(C(0x1E)))
    T('READ TOC, LBA, de nouveau', P(C(0x43, 0, 0, 0, 0, 0, 0, 0, 0x64), 22), 22)


if ZIP and not ESC and not FMT:
    # G10.6 — le lecteur ZIP 100 en maître secondaire (zip_channel = 2), zip100.img de la recette g5w (100 663 296
    # octets nuls). GR.2d1 : sur la table ; les secteurs écrits, remplis par l'op 17 (100 : i × 7 + 3 ; 101 et 550 :
    # i × 13 + 5Ah ; 102 : i × 3 + 11h), sont relus, et l'image est comparée des deux côtés en fin de porte. Les
    # commandes ajoutées : PREVENT ALLOW, SEEK(6), WRITE AND VERIFY(10), IOMEGA EJECT (R2-g10.md § 1.2) ; FORMAT est
    # dans sa porte (GR.2d3), le disque en lecture seule et l'esclave dans GR.2d2. L'op 15 garde au plus 22 octets :
    # IDENTIFY (96 avant), INQUIRY (36) et MODE SENSE(6) (24) en gardent moins ; la somme couvre le reste.
    SENSE18 = C(0x03, 0, 0, 0, 18)
    T('signature', BASE(0x170) + OUT(6, 0xA0) + WAIT + REC + LIGNE())                # 01 01 14 EB
    T('IDENTIFY PACKET DEVICE', OUT(7, 0xA1) + WDRQ + [3] + w(256) + [24, 54] + REC + LIGNE(), 24, 54)  # le modèle
    T('TEST UNIT READY', P(C(0x00)))                                                 # UNIT ATTENTION (le disque)
    T('TEST UNIT READY', P(C(0x00)))                                                 # GOOD
    T('INQUIRY', P(C(0x12, 0, 0, 0, 36), 22, 8), 22, 8)
    T('READ CAPACITY', P(C(0x25), 8), 8)                                              # 196 608 (PB-126)
    T('READ FORMAT CAPACITIES', P(C(0x23, 0, 0, 0, 0, 0, 0, 0, 20), 20), 20)
    T('MODE SENSE(6), toutes', P(C(0x1A, 0, 0x3F, 0, 0xFF), 22), 22)
    T('MODE SENSE(10), 2Fh', P(C(0x5A, 0, 0x2F, 0, 0, 0, 0, 0, 0x20), 16), 16)
    T('MODE SENSE(10), 08h', P(C(0x5A, 0, 0x08, 0, 0, 0, 0, 0, 0x20)))               # refusée, 24h
    T('IOMEGA SENSE, page 2', P(C(0x06, 0, 2, 0, 0x40), 22), 22)                     # la protection : 0
    T('-remplir', REMPLIR(7, 3))
    T('WRITE(10), LBA 100', P(C(0x2A, 0, 0, 0, 0, 100, 0, 0, 1), tampon=256))
    T('-remplir', REMPLIR(13, 0x5A))
    T('WRITE(6), LBA 101', P(C(0x0A, 0, 0, 101, 1), tampon=256))
    T('READ(10), 100 et 101', P(R10(100, 2), 16), 16, 0, _iso().somme_banc(SUITE(7, 3) + SUITE(13, 0x5A)))
    T('WRITE(10), LBA 550', P(C(0x2A, 0, 0, 0, 0x02, 0x26, 0, 0, 1), tampon=256))    # G13.7d1
    T('READ(10), 600 secteurs', P(R10(0, 600)))                                       # data_in déborde (G13.7d1)
    T('READ(6), LBA 101', P(C(0x08, 0, 0, 101, 1), 8), 8, 0, _iso().somme_banc(SUITE(13, 0x5A)))
    T('READ(10), LBA 0', P(R10(0, 1), 8), 8)                                          # nul
    T('READ(10), LBA 196 608', P(R10(196608, 1), 8), 8)                              # au-delà de la fin (PB-126)
    T('VERIFY(10)', P(C(0x2F, 0, 0, 0, 0, 100, 0, 0, 2)))
    T('SEEK(10)', P(C(0x2B, 0, 0, 0, 0, 100)))
    T('SEEK(6)', P(C(0x0B, 0, 0, 100)))                                               # GR.2d1
    T('REZERO UNIT', P(C(0x01)))
    T('SEND DIAGNOSTIC, autotest', P(C(0x1D, 4)))                                     # GOOD
    T('SEND DIAGNOSTIC, autre', P(C(0x1D, 0)))                                        # refusé
    T('RESERVE', P(C(0x16)))
    T('RELEASE', P(C(0x17)))
    T('TEST UNIT READY, LUN 1', P(C(0x00, 0x20)))                                    # 25h
    T('REQUEST SENSE', P(SENSE18, 14, sense=False), 14)
    T('un code inconnu', P(C(0xD8)))
    T('PREVENT ALLOW, empêcher', P(C(0x1E, 0, 0, 0, 1)))                              # GR.2d1
    T('PREVENT ALLOW, permettre', P(C(0x1E)))
    T('-remplir', REMPLIR(3, 0x11))
    T('WRITE AND VERIFY(10), LBA 102', P(C(0x2E, 0, 0, 0, 0, 102, 0, 0, 1), tampon=256))  # GR.2d1
    T('READ(10), LBA 102', P(R10(102, 1), 8), 8, 0, _iso().somme_banc(SUITE(3, 0x11)))
    T('START STOP UNIT, LOEJ', P(C(0x1B, 0, 0, 0, 2)))                               # n'éjecte pas (PB-274)
    T('TEST UNIT READY', P(C(0x00)))                                                 # GOOD
    T('IOMEGA EJECT', P(C(0x0D)))                                                     # éjecte (GR.2d1)
    T('TEST UNIT READY, éjecté', P(C(0x00)))                                          # NOT READY, 3Ah
    T('READ FORMAT CAPACITIES, sans disque', P(C(0x23, 0, 0, 0, 0, 0, 0, 0, 20), 20), 20)
    T('START STOP UNIT, éjecter', P(C(0x1B)))                                         # START = LOEJ = 0
    T('TEST UNIT READY, encore', P(C(0x00)))


# GR.2d2 — zipro.img : le disque de ZIPESC, écrit par la recette g5w (--image-ro) puis protégé (chmod 444) ; zip_load
# l'ouvre alors en lecture seule (scsi_zip.c:110-113). Trois secteurs remplis : LBA → (A, B) de SUITE.
IMAGE_RO = {100: (5, 0x21), 101: (11, 0x77), 196607: (17, 0xC3)}


def image_ro(chemin):
    with open(chemin, 'wb') as f:
        f.truncate(100663296)
        for lba, (a, b) in IMAGE_RO.items():
            f.seek(lba * 512)
            f.write(SUITE(a, b))


if ESC:
    # GR.2d2 — le CD en maître secondaire (cdrom_channel = 2, iso-2048.iso), le ZIP en esclave (zip_channel = 3,
    # zipro.img, protégé). Chaque unité choisie par 176h (A0h, B0h), le choix gardé d'une ligne à l'autre : les
    # signatures, la protection vue par MODE SENSE et IOMEGA SENSE, chaque écriture refusée (07/27h) et FORMAT UNIT
    # aussi, les secteurs relus inchangés ; puis le CD entre deux commandes du ZIP, les registres de chaque unité
    # gardés (l'erreur du ZIP, laissée sans REQUEST SENSE, n'est pas celle du CD), DEVICE RESET du seul ZIP (le CD
    # relu aussitôt), IOMEGA EJECT qui laisse le CD chargé. Les écritures de l'hôte vont aux deux unités (174h-175h du CD : FFFEh).
    def SECT(lba):
        return _iso().somme_banc(SUITE(*IMAGE_RO[lba]))
    def W10(lba, n):
        return C(0x2A, 0, (lba >> 24) & 0xFF, (lba >> 16) & 0xFF, (lba >> 8) & 0xFF, lba & 0xFF, 0, n >> 8, n & 0xFF)
    CD, ZP = OUT(6, 0xA0), OUT(6, 0xB0)
    T('signature, le CD', BASE(0x170) + CD + WAIT + REC + LIGNE())                    # 01 01 14 EB
    T('signature, le ZIP', ZP + WAIT + REC + LIGNE())                                 # 01 01 14 EB
    T('IDENTIFY PACKET DEVICE', OUT(7, 0xA1) + WDRQ + [3] + w(256) + [24, 54] + REC + LIGNE(), 24, 54)  # le modèle
    T('TEST UNIT READY', P(C(0x00)))                                                 # UNIT ATTENTION (le disque)
    T('TEST UNIT READY', P(C(0x00)))                                                 # GOOD
    T('INQUIRY', P(C(0x12, 0, 0, 0, 36), 22, 8), 22, 8)
    T('READ CAPACITY', P(C(0x25), 8), 8)                                              # 196 608 (PB-126)
    T('MODE SENSE(6), toutes', P(C(0x1A, 0, 0x3F, 0, 0xFF), 22), 22)                # la protection : 80h
    T('MODE SENSE(10), 2Fh', P(C(0x5A, 0, 0x2F, 0, 0, 0, 0, 0, 0x20), 16), 16)      # de même
    T('IOMEGA SENSE, page 2', P(C(0x06, 0, 2, 0, 0x40), 22), 22)                     # la protection : 2
    T('READ(10), 100 et 101', P(R10(100, 2), 16), 16, 0,
      _iso().somme_banc(SUITE(*IMAGE_RO[100]) + SUITE(*IMAGE_RO[101])))
    T('READ(10), LBA 196 607', P(R10(196607, 1), 8), 8, 0, SECT(196607))            # le dernier
    T('READ(6), LBA 101', P(C(0x08, 0, 0, 101, 1), 8), 8, 0, SECT(101))
    T('-remplir', REMPLIR(0xE5, 0x0F))
    T('WRITE(10), LBA 100', P(W10(100, 1), tampon=256))                              # 07/27h
    T('WRITE(6), LBA 101', P(C(0x0A, 0, 0, 101, 1), tampon=256))                    # 07/27h
    T('WRITE AND VERIFY(10), LBA 100', P(C(0x2E) + W10(100, 1)[1:], tampon=256))    # 07/27h
    T('WRITE(10), LBA 196 608', P(W10(196608, 1), tampon=256))                       # 07/27h, avant la plage
    T('WRITE(10), compte 0', P(W10(100, 0)))
    T('FORMAT UNIT', P(C(0x04)))                                                      # 07/27h, rien d'effacé
    T('READ(10), LBA 100, inchangé', P(R10(100, 1), 8), 8, 0, SECT(100))
    T('VERIFY(10)', P(C(0x2F, 0, 0, 0, 0, 100, 0, 0, 2)))                          # GOOD : une lecture
    T('le CD, choisi', CD + WAIT + REC + LIGNE())
    T('IDENTIFY PACKET DEVICE', OUT(7, 0xA1) + WDRQ + [3] + w(256) + [24, 54] + REC + LIGNE(), 24, 54)  # le CD
    T('TEST UNIT READY, le CD', P(C(0x00)))                                          # UNIT ATTENTION (le disque)
    T('TEST UNIT READY, le CD', P(C(0x00)))                                          # GOOD
    T('INQUIRY, le CD', P(C(0x12, 0, 0, 0, 36), 22, 8), 22, 8)
    T('READ CAPACITY, le CD', P(C(0x25), 8), 8)                                       # PB-117
    T('READ(10), le PVD', P(R10(16, 1), 8), 8, 0, SOMME(16, 1))
    T('le ZIP, choisi', ZP + WAIT + REC + LIGNE())
    T('TEST UNIT READY', P(C(0x00)))                                                 # GOOD
    T('READ(10), LBA 101', P(R10(101, 1), 8), 8, 0, SECT(101))
    T('WRITE(10), LBA 101, sans sense', P(W10(101, 1), sense=False, tampon=256))      # ERR gardé
    T('le CD, choisi, sans erreur', CD + WAIT + REC + LIGNE())
    T('le ZIP, choisi, son erreur', ZP + WAIT + REC + LIGNE())
    T('DEVICE RESET, le ZIP', OUT(7, 0x08) + WAIT + REC + LIGNE())                   # la signature
    # le CD aussitôt : un paquet du ZIP réécrirait 174h-175h des deux unités, et cacherait un reset qui toucherait le CD
    T('le CD, après le reset du ZIP', CD + WAIT + REC + LIGNE())                      # FFFEh, pas 14EBh
    T('TEST UNIT READY, le CD', P(C(0x00)))                                          # GOOD
    T('le ZIP, choisi', ZP + WAIT + REC + LIGNE())                                    # 00h et 01h ; FFFEh, du CD
    T('TEST UNIT READY', P(C(0x00)))
    T('IOMEGA EJECT', P(C(0x0D)))
    T('TEST UNIT READY, éjecté', P(C(0x00)))                                          # NOT READY, 3Ah
    T('le CD, choisi', CD + WAIT + REC + LIGNE())
    T('TEST UNIT READY, le CD', P(C(0x00)))                                          # GOOD
    T('READ(10), le PVD, encore', P(R10(16, 1), 8), 8, 0, SOMME(16, 1))

if FMT:
    # GR.2d3 — FORMAT UNIT du ZIP, sur zip100.img (maître secondaire, ami486-zip.cfg) : deux secteurs écrits et relus,
    # FORMAT UNIT avec FmtData et une liste de 12 octets (l'en-tête, puis 196 608 blocs de 512 octets ; la forme de
    # SFF-8070i d'après sa description, le texte non relu ; PCem ne demande pas la liste, scsi_zip.c:872-883), les mêmes
    # secteurs relus nuls, la capacité et
    # l'état d'après, un secteur écrit après FORMAT ; l'image comparée des deux côtés en fin de porte (boot-diff).
    def W10(lba, n):
        return C(0x2A, 0, (lba >> 24) & 0xFF, (lba >> 16) & 0xFF, (lba >> 8) & 0xFF, lba & 0xFF, 0, n >> 8, n & 0xFF)
    NUL = _iso().somme_banc(bytes(512))
    LISTE = [0x00, 0xA0, 0x00, 0x08, 0x00, 0x03, 0x00, 0x00, 0x00, 0x00, 0x02, 0x00]
    T('signature', BASE(0x170) + OUT(6, 0xA0) + WAIT + REC + LIGNE())                # 01 01 14 EB
    T('TEST UNIT READY', P(C(0x00)))                                                 # UNIT ATTENTION (le disque)
    T('TEST UNIT READY', P(C(0x00)))                                                 # GOOD
    T('READ FORMAT CAPACITIES', P(C(0x23, 0, 0, 0, 0, 0, 0, 0, 20), 20), 20)
    T('-remplir', REMPLIR(7, 3))
    T('WRITE(10), LBA 100', P(W10(100, 1), tampon=256))
    T('-remplir', REMPLIR(13, 0x5A))
    T('WRITE(10), LBA 196 607', P(W10(196607, 1), tampon=256))                       # le dernier
    T('READ(10), LBA 100', P(R10(100, 1), 8), 8, 0, _iso().somme_banc(SUITE(7, 3)))
    T('READ(10), LBA 196 607', P(R10(196607, 1), 8), 8, 0, _iso().somme_banc(SUITE(13, 0x5A)))
    T('FORMAT UNIT, la liste', P(C(0x04, 0x17), sortie=LISTE))                        # aucune phase sortante
    T('TEST UNIT READY, après', P(C(0x00)))
    T('REQUEST SENSE', P(C(0x03, 0, 0, 0, 18), 14, sense=False), 14)
    T('READ(10), LBA 100, nul', P(R10(100, 1), 8), 8, 0, NUL)
    T('READ(10), LBA 196 607, nul', P(R10(196607, 1), 8), 8, 0, NUL)
    T('READ CAPACITY', P(C(0x25), 8), 8)                                              # PB-126
    T('READ FORMAT CAPACITIES, après', P(C(0x23, 0, 0, 0, 0, 0, 0, 0, 20), 20), 20)
    T('-remplir', REMPLIR(3, 0x11))
    T('WRITE(10), LBA 200, après', P(W10(200, 1), tampon=256))
    T('READ(10), LBA 200', P(R10(200, 1), 8), 8, 0, _iso().somme_banc(SUITE(3, 0x11)))
    T('FORMAT UNIT, sans liste', P(C(0x04)))                                          # GOOD, tout effacé
    T('READ(10), LBA 200, nul', P(R10(200, 1), 8), 8, 0, NUL)
    T('-remplir', REMPLIR(5, 0x21))
    T('WRITE(10), LBA 300, à la fin', P(W10(300, 1), tampon=256))                      # l'image : ce seul secteur
    T('READ(10), LBA 300', P(R10(300, 1), 8), 8, 0, _iso().somme_banc(SUITE(5, 0x21)))

S = []
# GR.2b — la table (PLAN-GR.md, GR.2b ; R2-g10.md § 1.2) : une ligne par commande et par cas limite, sur les
# 30 étiquettes de scsi_cd_command, sans les sites R9 (R2-g10.md § 1.3 : MECHANISM STATUS de longueur 0,
# scsi_cd.c:993 ; data_out au-delà de 256 Ko, :1581, où mène MODE SELECT de longueur 0 ; un PACKET en DMA ; un
# PACKET pendant une phase ; READ(12) d'un compte de 2^31 ou plus ; et tout transfert au-delà de data_in, aucune
# ligne ne demandant plus de 40 secteurs). Chaque ligne imprime aussitôt son relevé : son numéro, l'IIR quand le
# lecteur demande le paquet, les K octets gardés, le nombre de blocs DRQ, l'IIR des blocs en OU, le total des
# comptes, la somme tournante, puis 177h, 171h (la clé de sense en bits 7-4) à 176h ; sur ERR, ASC et ASCQ.
# Une pause toutes les 23 lignes (PAGE) : la page reste à l'écran jusqu'à la touche. Hors de la table, renvoyés
# à ATAPIAUD (GR.2c) : START STOP UNIT qui éjecte puis charge (cdb[4] = 2, 3), le disque changé qui s'ensuit, et
# RESUME ; l'audio qui joue (READ SUB-CHANNEL, REQUEST SENSE « audio terminé »).
for nom, rangee, k, s_, attendu in TABLE_LIGNES:
    S += rangee
S += [0]
ins('script', 'db script', len(S), lambda L, a, S=S: list(S))
ins('sum', 'dw 0', 2, lambda L, a: [0, 0])
ins('tot', 'dw 0', 2, lambda L, a: [0, 0])
ins('keep', 'dw 0', 2, lambda L, a: [0, 0])
ins('blk', 'db 0', 1, lambda L, a: [0])
ins('iir', 'db 0', 1, lambda L, a: [0])
ins('crlf', "db 13,10,'$'", 3, lambda L, a: [13, 10, 0x24])
ins('skp', 'dw 0', 2, lambda L, a: [0, 0])
ins('flg', 'db 0', 1, lambda L, a: [0])
ins('st', 'db 0', 1, lambda L, a: [0])
ins('rown', 'db 0', 1, lambda L, a: [0])
ins('bso', 'db 0', 1, lambda L, a: [0])
ins('cdbb', 'db 12 dup 0', 12, lambda L, a: [0] * 12)
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


NOM = ('CDECOUTE' if ECOUTE else 'ATAPIAUD' if AUDIO else 'ZIPESC' if ESC else 'ZIPFMT' if FMT else 'ZIPBANC' if ZIP
       else 'ATAPIBNC')


def keys(code):
    lines = ['DEBUG']
    for i in range(0, len(code), 16):
        lines.append(f'E {ORG + i:X} ' + ' '.join(f'{b:02X}' for b in code[i:i + 16]))
    lines += [f'N {NOM}.COM', 'R CX', f'{len(code):X}', 'W', 'Q', NOM]
    return lines


if __name__ == '__main__':
    if '--image-ro' in sys.argv:
        image_ro(sys.argv[sys.argv.index('--image-ro') + 1])
        sys.exit(0)
    code, listing = assemble()
    print('\n'.join(listing))
    print(f'\n{len(code)} octets, script {len(S)} octets')
    n = 0
    for nom, rangee, k, s_, attendu in TABLE_LIGNES:
        if nom.startswith('-'):
            continue
        # la ligne imprimée : son numéro, puis au plus 38 octets (l'IIR, K, six de la phase, sept registres,
        # ASC et ASCQ) — 79 colonnes
        assert 3 + 2 * (16 + k) <= 79 or nom == 'IDENTIFY PACKET DEVICE', nom
        if attendu is not None:
            print(f'  ligne {n:02X} {nom:<34} somme attendue {attendu & 0xFF:02X}{attendu >> 8:02X} '
                  f'(octets du relevé)')
        n += 1
    here = os.path.dirname(os.path.abspath(__file__))
    fichier = ('cdecoute.keys' if ECOUTE else 'atapibanc-audio.keys' if AUDIO else 'zipesc.keys' if ESC else
               'zipfmt.keys' if FMT else 'zipbanc.keys' if ZIP else 'atapibanc.keys')
    with open(os.path.join(here, fichier), 'w') as f:
        f.write('\n'.join(keys(code)) + '\n')
    if '--com' in sys.argv:
        with open(sys.argv[sys.argv.index('--com') + 1], 'wb') as f:
            f.write(code)
