#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# SBBANC.COM — le banc dirigé du DSP de la Sound Blaster Pro v2, G8.2 (PLAN-G8.md).
#
# Ce que le logiciel lit du DSP (2xAh, 2xCh, 2xEh) passe au diff d'instructions ; ce qu'il JOUE
# — sortie directe, DMA 8 bits, ADPCM — ne se voit qu'au hachage de la sonde du son (sound_hash).
# SBBANC.COM exerce les deux, à 220h, IRQ masquée au PIC (le DSP lève quand même son IRQ ; le banc
# l'acquitte par 2xEh) : reset et version, sortie directe (10h), constante de temps (40h), DMA
# simple (14h) et automatique (48h, 1Ch) avec pause et reprise (D0h, D4h) et sortie (DAh),
# stéréo et lecture du mélangeur CT1345, ADPCM 4 bits (75h), haut-parleur (D1h, D3h), et l'OPL3
# de la carte (mode OPL3, banque haute). Le DMA est programmé à l'exécution depuis l'adresse
# physique du tampon (CS × 16 + déplacement). Instructions 8086 seules ; saisi dans DEBUG
# (disquette supplémentaire de PC-DOS 2.00 en B:), écrit sur B:, lancé sous boot-diff.
#
# Usage :
#   python3 tools/sbbanc/sbbanc.py           écrit sbbanc.keys, affiche le listing
#   python3 tools/sbbanc/sbbanc.py --com F   écrit aussi le .COM

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


def case(n, label, skip):
    ins(label, f'cmp al,{n}', 2, lambda L, a: [0x3C, n])
    ins(None, f'jne {skip}', 2, lambda L, a: [0x75, rel8(L, a + 2, skip)])


# ---- l'interprète -----------------------------------------------------------------------
ins('start', 'mov si,script', 3, lambda L, a: [0xBE] + w(L['script']))
ins(None, 'mov di,results', 3, lambda L, a: [0xBF] + w(L['results']))
ins(None, 'cld', 1, lambda L, a: [0xFC])
ins('next', 'lodsb', 1, lambda L, a: [0xAC])

# op 1 : OUT port,AL
case(1, None, 'op2')
ins(None, 'lodsw                ; port', 1, lambda L, a: [0xAD])
ins(None, 'mov dx,ax', 2, lambda L, a: [0x89, 0xC2])
ins(None, 'lodsb                ; valeur', 1, lambda L, a: [0xAC])
ins(None, 'out dx,al', 1, lambda L, a: [0xEE])
nxt()

# op 2 : écrire un octet au DSP : attendre le bit 7 de 22Ch à zéro (au plus 65 535 lectures)
case(2, 'op2', 'op3')
ins(None, 'mov dx,22Ch', 3, lambda L, a: [0xBA, 0x2C, 0x02])
ins(None, 'mov cx,0FFFFh', 3, lambda L, a: [0xB9, 0xFF, 0xFF])
ins('w2', 'in al,dx', 1, lambda L, a: [0xEC])
ins(None, 'test al,80h', 2, lambda L, a: [0xA8, 0x80])
ins(None, 'jz w2x', 2, lambda L, a: [0x74, rel8(L, a + 2, 'w2x')])
ins(None, 'loop w2', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'w2')])
ins('w2x', 'lodsb                ; octet', 1, lambda L, a: [0xAC])
ins(None, 'out dx,al', 1, lambda L, a: [0xEE])
nxt()

# op 3 : attente, N × 65 536 tours de LOOP
case(3, 'op3', 'op4')
ins(None, 'lodsb                ; N', 1, lambda L, a: [0xAC])
ins(None, 'mov bl,al', 2, lambda L, a: [0x88, 0xC3])
ins('w3', 'xor cx,cx', 2, lambda L, a: [0x31, 0xC9])
ins('w3b', 'loop w3b', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'w3b')])
ins(None, 'dec bl', 2, lambda L, a: [0xFE, 0xCB])
ins(None, 'jnz w3', 2, lambda L, a: [0x75, rel8(L, a + 2, 'w3')])
nxt()

# op 4 : lire un octet du DSP : attendre le bit 7 de 22Eh à un, lire 22Ah → relevé
case(4, 'op4', 'op5')
ins(None, 'mov dx,22Eh', 3, lambda L, a: [0xBA, 0x2E, 0x02])
ins(None, 'mov cx,0FFFFh', 3, lambda L, a: [0xB9, 0xFF, 0xFF])
ins('w4', 'in al,dx', 1, lambda L, a: [0xEC])
ins(None, 'test al,80h', 2, lambda L, a: [0xA8, 0x80])
ins(None, 'jnz w4x', 2, lambda L, a: [0x75, rel8(L, a + 2, 'w4x')])
ins(None, 'loop w4', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'w4')])
ins('w4x', 'mov dx,22Ah', 3, lambda L, a: [0xBA, 0x2A, 0x02])
ins(None, 'in al,dx', 1, lambda L, a: [0xEC])
ins(None, 'stosb', 1, lambda L, a: [0xAA])
nxt()

# op 5 : IN AL,port → relevé (1 octet)
case(5, 'op5', 'op6')
ins(None, 'lodsw                ; port', 1, lambda L, a: [0xAD])
ins(None, 'mov dx,ax', 2, lambda L, a: [0x89, 0xC2])
ins(None, 'in al,dx', 1, lambda L, a: [0xEC])
ins(None, 'stosb', 1, lambda L, a: [0xAA])
nxt()

# op 6 : remplir N octets du tampon (CS:buffer + déplacement), graine, + pas par octet
case(6, 'op6', 'op7')
ins(None, 'push di', 1, lambda L, a: [0x57])
ins(None, 'lodsw                ; déplacement', 1, lambda L, a: [0xAD])
ins(None, 'add ax,buffer', 3, lambda L, a: [0x05] + w(L['buffer']))
ins(None, 'mov di,ax', 2, lambda L, a: [0x89, 0xC7])
ins(None, 'lodsw                ; N', 1, lambda L, a: [0xAD])
ins(None, 'mov cx,ax', 2, lambda L, a: [0x89, 0xC1])
ins(None, 'lodsb                ; graine', 1, lambda L, a: [0xAC])
ins(None, 'mov ah,al', 2, lambda L, a: [0x88, 0xC4])
ins(None, 'lodsb                ; pas', 1, lambda L, a: [0xAC])
ins(None, 'mov bl,al', 2, lambda L, a: [0x88, 0xC3])
ins(None, 'mov al,ah', 2, lambda L, a: [0x88, 0xE0])
ins('f6', 'stosb', 1, lambda L, a: [0xAA])
ins(None, 'add al,bl', 2, lambda L, a: [0x00, 0xD8])
ins(None, 'loop f6', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'f6')])
ins(None, 'pop di', 1, lambda L, a: [0x5F])
nxt()

# op 7 : programmer le canal DMA 1 sur le tampon : mode, déplacement, longueur - 1
case(7, 'op7', 'op8')
ins(None, 'mov al,5', 2, lambda L, a: [0xB0, 0x05])
ins(None, 'out 0Ah,al            ; masquer le canal 1', 2, lambda L, a: [0xE6, 0x0A])
ins(None, 'out 0Ch,al            ; bascule à zéro', 2, lambda L, a: [0xE6, 0x0C])
ins(None, 'lodsb                ; mode', 1, lambda L, a: [0xAC])
ins(None, 'out 0Bh,al', 2, lambda L, a: [0xE6, 0x0B])
ins(None, 'mov ax,cs', 2, lambda L, a: [0x8C, 0xC8])
ins(None, 'mov dx,ax', 2, lambda L, a: [0x89, 0xC2])
ins(None, 'mov cl,4', 2, lambda L, a: [0xB1, 0x04])
ins(None, 'shl ax,cl', 2, lambda L, a: [0xD3, 0xE0])
ins(None, 'mov cl,12', 2, lambda L, a: [0xB1, 0x0C])
ins(None, 'shr dx,cl', 2, lambda L, a: [0xD3, 0xEA])
ins(None, 'mov bx,ax', 2, lambda L, a: [0x89, 0xC3])
ins(None, 'lodsw                ; déplacement', 1, lambda L, a: [0xAD])
ins(None, 'add ax,buffer', 3, lambda L, a: [0x05] + w(L['buffer']))
ins(None, 'add ax,bx', 2, lambda L, a: [0x01, 0xD8])
ins(None, 'adc dl,0', 3, lambda L, a: [0x80, 0xD2, 0x00])
ins(None, 'out 02h,al', 2, lambda L, a: [0xE6, 0x02])
ins(None, 'mov al,ah', 2, lambda L, a: [0x88, 0xE0])
ins(None, 'out 02h,al', 2, lambda L, a: [0xE6, 0x02])
ins(None, 'mov al,dl', 2, lambda L, a: [0x88, 0xD0])
ins(None, 'out 83h,al            ; page du canal 1', 2, lambda L, a: [0xE6, 0x83])
ins(None, 'lodsw                ; longueur - 1', 1, lambda L, a: [0xAD])
ins(None, 'out 03h,al', 2, lambda L, a: [0xE6, 0x03])
ins(None, 'mov al,ah', 2, lambda L, a: [0x88, 0xE0])
ins(None, 'out 03h,al', 2, lambda L, a: [0xE6, 0x03])
ins(None, 'mov al,1', 2, lambda L, a: [0xB0, 0x01])
ins(None, 'out 0Ah,al            ; démasquer', 2, lambda L, a: [0xE6, 0x0A])
nxt()

# op 8 : écrire un registre indexé : OUT port,index ; OUT port+1,valeur (mélangeur, OPL)
case(8, 'op8', 'op9')
ins(None, 'lodsw                ; port', 1, lambda L, a: [0xAD])
ins(None, 'mov dx,ax', 2, lambda L, a: [0x89, 0xC2])
ins(None, 'lodsb                ; index', 1, lambda L, a: [0xAC])
ins(None, 'out dx,al', 1, lambda L, a: [0xEE])
ins(None, 'mov cx,8', 3, lambda L, a: [0xB9, 0x08, 0x00])
ins('w8', 'in al,dx', 1, lambda L, a: [0xEC])
ins(None, 'loop w8', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'w8')])
ins(None, 'inc dx', 1, lambda L, a: [0x42])
ins(None, 'lodsb                ; valeur', 1, lambda L, a: [0xAC])
ins(None, 'out dx,al', 1, lambda L, a: [0xEE])
ins(None, 'dec dx', 1, lambda L, a: [0x4A])
ins(None, 'mov cx,35', 3, lambda L, a: [0xB9, 0x23, 0x00])
ins('w8b', 'in al,dx', 1, lambda L, a: [0xEC])
ins(None, 'loop w8b', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'w8b')])
nxt()

# op 9 : lire un registre indexé : OUT port,index ; IN port+1 → relevé
case(9, 'op9', 'done')
ins(None, 'lodsw                ; port', 1, lambda L, a: [0xAD])
ins(None, 'mov dx,ax', 2, lambda L, a: [0x89, 0xC2])
ins(None, 'lodsb                ; index', 1, lambda L, a: [0xAC])
ins(None, 'out dx,al', 1, lambda L, a: [0xEE])
ins(None, 'inc dx', 1, lambda L, a: [0x42])
ins(None, 'in al,dx', 1, lambda L, a: [0xEC])
ins(None, 'stosb', 1, lambda L, a: [0xAA])
nxt()

# op 0 (et tout inconnu) : afficher les relevés, seize octets par ligne, puis sortir
ins('done', 'mov si,results', 3, lambda L, a: [0xBE] + w(L['results']))
ins(None, 'xor bp,bp', 2, lambda L, a: [0x31, 0xED])
ins('p0', 'cmp si,di', 2, lambda L, a: [0x39, 0xFE])
ins(None, 'jae fin', 2, lambda L, a: [0x73, rel8(L, a + 2, 'fin')])
ins(None, 'lodsb', 1, lambda L, a: [0xAC])
ins(None, 'push ax', 1, lambda L, a: [0x50])
ins(None, 'mov cl,4', 2, lambda L, a: [0xB1, 0x04])
ins(None, 'shr al,cl', 2, lambda L, a: [0xD2, 0xE8])
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


# ---- le script -----------------------------------------------------------------------------
def OUTB(port, val): return [1] + w(port) + [val]
def DSPW(*bs):
    out = []
    for b in bs:
        out += [2, b]
    return out
def WAIT(n): return [3, n]
DSPR = [4]
def INB(port): return [5] + w(port)
def FILL(off, n, seed, step): return [6] + w(off) + w(n) + [seed, step]
def DMA(mode, off, n): return [7, mode] + w(off) + w(n - 1)
def REGW(port, idx, val): return [8] + w(port) + [idx, val]
def REGR(port, idx): return [9] + w(port) + [idx]


BUF = 4096
S = []
# -- le tampon : une dent de scie, puis des octets ADPCM
S += FILL(0, 2048, 0x80, 0x07) + FILL(2048, 1024, 0x40, 0x0D) + FILL(3072, 1024, 0x5A, 0x31)
# -- reset du DSP (226h : 1 puis 0), l'octet AAh attendu ; version (E1h) : 3.02
S += OUTB(0x226, 1) + WAIT(1) + OUTB(0x226, 0) + DSPR + DSPW(0xE1) + DSPR + DSPR
S += DSPW(0xD1)                                      # haut-parleur
# -- sortie directe (10h), 64 échantillons
for k in range(64):
    S += DSPW(0x10, (k * 9) & 0xFF)
# -- constante de temps (40h) : A5h ≈ 11 kHz ; DMA simple 8 bits (14h), 2 048 octets
S += DSPW(0x40, 0xA5) + DMA(0x49, 0, 2048) + DSPW(0x14, 0xFF, 0x07) + WAIT(3) + INB(0x22E)
# -- DMA automatique : taille de bloc (48h), 14h en boucle (1Ch), pause (D0h), reprise (D4h),
#    sortie de l'automatique (DAh)
S += DMA(0x59, 0, 2048) + DSPW(0x48, 0xFF, 0x03) + DSPW(0x1C) + WAIT(2) + INB(0x22E)
S += DSPW(0xD0) + WAIT(1) + DSPW(0xD4) + WAIT(2) + INB(0x22E) + DSPW(0xDA) + WAIT(2) + INB(0x22E)
# -- le mélangeur CT1345 : stéréo (0Eh bit 1), volumes, relus
S += REGW(0x224, 0x22, 0xFF) + REGW(0x224, 0x04, 0xDD) + REGW(0x224, 0x26, 0x99) + REGW(0x224, 0x0E, 0x02)
S += REGR(0x224, 0x22) + REGR(0x224, 0x04) + REGR(0x224, 0x0E) + REGR(0x224, 0x0C)
S += DSPW(0x40, 0xD3) + DMA(0x49, 2048, 1024) + DSPW(0x14, 0xFF, 0x03) + WAIT(2) + INB(0x22E)
S += REGW(0x224, 0x0E, 0x20)                         # mono, filtre de sortie coupé
S += DSPW(0x40, 0xA5) + DMA(0x49, 0, 1024) + DSPW(0x14, 0xFF, 0x03) + WAIT(2) + INB(0x22E)
S += REGW(0x224, 0x00, 0x00) + REGR(0x224, 0x22)     # reset du mélangeur
# -- ADPCM 4 bits avec octet de référence (75h), 1 024 octets
S += DMA(0x49, 3072, 1024) + DSPW(0x75, 0xFF, 0x03) + WAIT(3) + INB(0x22E)
# -- l'OPL3 de la carte : mode OPL3 (105h = 1, banque haute par 222h/223h), une note sur chaque banque
S += REGW(0x222, 0x05, 0x01) + REGW(0x220, 0x01, 0x20)
for port in (0x220, 0x222):
    S += REGW(port, 0x20, 0x21) + REGW(port, 0x23, 0x01) + REGW(port, 0x40, 0x10) + REGW(port, 0x43, 0x00)
    S += REGW(port, 0x60, 0xF2) + REGW(port, 0x63, 0xF4) + REGW(port, 0x80, 0x54) + REGW(port, 0x83, 0x56)
    S += REGW(port, 0xC0, 0x36) + REGW(port, 0xA0, 0x98) + REGW(port, 0xB0, 0x31)
S += WAIT(3) + REGW(0x220, 0xB0, 0x11) + REGW(0x222, 0xB0, 0x11) + INB(0x388) + INB(0x220)
S += DSPW(0xD3) + DSPW(0xD8) + DSPR                  # haut-parleur coupé, son état
S += [0]

ins('script', 'db script', len(S), lambda L, a, S=S: list(S))
ins('crlf', "db 13,10,'$'", 3, lambda L, a: [13, 10, 0x24])
ins('results', 'results:', 64, lambda L, a: [0] * 64)
# `buffer` : la fin du programme ; la zone n'est pas écrite par DEBUG.
ins('buffer', 'buffer:', 0, lambda L, a: [])


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
    lines = ['B:', 'DEBUG']
    for i in range(0, len(code), 16):
        lines.append(f'E {ORG + i:X} ' + ' '.join(f'{b:02X}' for b in code[i:i + 16]))
    lines += ['N SBBANC.COM', 'R CX', f'{len(code):X}', 'W', 'Q', 'SBBANC']
    return lines


if __name__ == '__main__':
    code, listing = assemble()
    print('\n'.join(listing))
    print(f'\n{len(code)} octets, script {len(S)} octets')
    here = os.path.dirname(os.path.abspath(__file__))
    with open(os.path.join(here, 'sbbanc.keys'), 'w') as f:
        f.write('\n'.join(keys(code)) + '\n')
    if '--com' in sys.argv:
        with open(sys.argv[sys.argv.index('--com') + 1], 'wb') as f:
            f.write(code)
