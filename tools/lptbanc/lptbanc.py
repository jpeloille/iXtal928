#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# LPTBANC.COM — le banc dirigé du port parallèle, G10.0 (PLAN-G10.md).
#
# Il relit ce que le BIOS a trouvé (les adresses des ports en 0040:0008, le nombre d'imprimantes
# dans l'octet d'équipement), écrit et relit les ports de données et de contrôle de LPT1 (378h) et
# LPT2 (278h), lit l'état ; puis il alimente le périphérique de LPT1 : une rafale de 24 octets
# (la file de 16 de la Sound Source se remplit : bit 6 de 379h), mille lectures de 379h
# pendant que la file se vide à 7 kHz (l'instant où le bit 6 tombe), une attente, une rampe de 256 échantillons (le Covox), des écritures qui alternent le canal par le bit
# 0 de 37Ah (le Covox stéréo). Le même banc sert aux trois périphériques ; le diff d'instructions
# voit l'état, la sonde du son les échantillons.
#
# Usage :
#   python3 tools/lptbanc/lptbanc.py           écrit lptbanc.keys, affiche le listing
#   python3 tools/lptbanc/lptbanc.py --com F   écrit aussi le .COM

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

# op 2 : remplir N octets en segment:déplacement, graine, + pas par octet
case(2, 'op2', 'op3')
ins(None, 'push di', 1, lambda L, a: [0x57])
ins(None, 'push es', 1, lambda L, a: [0x06])
ins(None, 'lodsw                ; segment', 1, lambda L, a: [0xAD])
ins(None, 'mov es,ax', 2, lambda L, a: [0x8E, 0xC0])
ins(None, 'lodsw                ; déplacement', 1, lambda L, a: [0xAD])
ins(None, 'mov di,ax', 2, lambda L, a: [0x89, 0xC7])
ins(None, 'lodsw                ; N', 1, lambda L, a: [0xAD])
ins(None, 'mov cx,ax', 2, lambda L, a: [0x89, 0xC1])
ins(None, 'lodsb                ; graine', 1, lambda L, a: [0xAC])
ins(None, 'mov ah,al', 2, lambda L, a: [0x88, 0xC4])
ins(None, 'lodsb                ; pas', 1, lambda L, a: [0xAC])
ins(None, 'mov bl,al', 2, lambda L, a: [0x88, 0xC3])
ins(None, 'mov al,ah', 2, lambda L, a: [0x88, 0xE0])
ins('f2', 'stosb', 1, lambda L, a: [0xAA])
ins(None, 'add al,bl', 2, lambda L, a: [0x00, 0xD8])
ins(None, 'loop f2', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'f2')])
ins(None, 'pop es', 1, lambda L, a: [0x07])
ins(None, 'pop di', 1, lambda L, a: [0x5F])
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

# op 4 : lire un octet en segment:déplacement → relevé
case(4, 'op4', 'op5')
ins(None, 'push ds', 1, lambda L, a: [0x1E])
ins(None, 'lodsw                ; segment', 1, lambda L, a: [0xAD])
ins(None, 'mov dx,ax', 2, lambda L, a: [0x89, 0xC2])
ins(None, 'lodsw                ; déplacement', 1, lambda L, a: [0xAD])
ins(None, 'mov bx,ax', 2, lambda L, a: [0x89, 0xC3])
ins(None, 'mov ds,dx', 2, lambda L, a: [0x8E, 0xDA])
ins(None, 'mov al,[bx]', 2, lambda L, a: [0x8A, 0x07])
ins(None, 'pop ds', 1, lambda L, a: [0x1F])
ins(None, 'stosb', 1, lambda L, a: [0xAA])
nxt()

# op 5 : IN AL,port → relevé (1 octet)
case(5, 'op5', 'op6')
ins(None, 'lodsw                ; port', 1, lambda L, a: [0xAD])
ins(None, 'mov dx,ax', 2, lambda L, a: [0x89, 0xC2])
ins(None, 'in al,dx', 1, lambda L, a: [0xEC])
ins(None, 'stosb', 1, lambda L, a: [0xAA])
nxt()

# op 6 : N lectures d'un port, relevé du OU et du ET des valeurs lues (les bits ont-ils bougé ?)
case(6, 'op6', 'op7')
ins(None, 'lodsw                ; port', 1, lambda L, a: [0xAD])
ins(None, 'mov dx,ax', 2, lambda L, a: [0x89, 0xC2])
ins(None, 'lodsw                ; N', 1, lambda L, a: [0xAD])
ins(None, 'mov cx,ax', 2, lambda L, a: [0x89, 0xC1])
ins(None, 'xor bl,bl              ; OU', 2, lambda L, a: [0x30, 0xDB])
ins(None, 'mov bh,0FFh            ; ET', 2, lambda L, a: [0xB7, 0xFF])
ins('w6', 'in al,dx', 1, lambda L, a: [0xEC])
ins(None, 'or bl,al', 2, lambda L, a: [0x08, 0xC3])
ins(None, 'and bh,al', 2, lambda L, a: [0x20, 0xC7])
ins(None, 'loop w6', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'w6')])
ins(None, 'mov al,bl', 2, lambda L, a: [0x88, 0xD8])
ins(None, 'stosb', 1, lambda L, a: [0xAA])
ins(None, 'mov al,bh', 2, lambda L, a: [0x88, 0xF8])
ins(None, 'stosb', 1, lambda L, a: [0xAA])
nxt()

# op 7 : INT 10h avec AX donné (le choix d'un mode : AH = 0)
case(7, 'op7', 'op8')
ins(None, 'lodsw                ; AX', 1, lambda L, a: [0xAD])
ins(None, 'push si', 1, lambda L, a: [0x56])
ins(None, 'push di', 1, lambda L, a: [0x57])
ins(None, 'int 10h', 2, lambda L, a: [0xCD, 0x10])
ins(None, 'pop di', 1, lambda L, a: [0x5F])
ins(None, 'pop si', 1, lambda L, a: [0x5E])
ins(None, 'cld', 1, lambda L, a: [0xFC])
nxt()

# op 8 : écrire un octet en segment:déplacement
case(8, 'op8', 'op9')
ins(None, 'push es', 1, lambda L, a: [0x06])
ins(None, 'lodsw                ; segment', 1, lambda L, a: [0xAD])
ins(None, 'mov es,ax', 2, lambda L, a: [0x8E, 0xC0])
ins(None, 'lodsw                ; déplacement', 1, lambda L, a: [0xAD])
ins(None, 'mov bx,ax', 2, lambda L, a: [0x89, 0xC3])
ins(None, 'lodsb                ; octet', 1, lambda L, a: [0xAC])
ins(None, 'mov [es:bx],al', 3, lambda L, a: [0x26, 0x88, 0x07])
ins(None, 'pop es', 1, lambda L, a: [0x07])
nxt()

# op 9 : N octets vers un port, graine, + pas par octet, puis D tours de LOOP entre deux octets
# (D = 1 : la rafale, plus rapide que les 7 kHz de la Sound Source, qui remplit sa file ;
# D = 40, ~151 µs par octet sur le 5150 : plus lent que sa vidange, la file ne se remplit pas)
case(9, 'op9', 'done')
ins(None, 'lodsw                ; port', 1, lambda L, a: [0xAD])
ins(None, 'mov dx,ax', 2, lambda L, a: [0x89, 0xC2])
ins(None, 'lodsw                ; N', 1, lambda L, a: [0xAD])
ins(None, 'mov cx,ax', 2, lambda L, a: [0x89, 0xC1])
ins(None, 'lodsb                ; graine', 1, lambda L, a: [0xAC])
ins(None, 'mov ah,al', 2, lambda L, a: [0x88, 0xC4])
ins(None, 'lodsb                ; pas', 1, lambda L, a: [0xAC])
ins(None, 'mov bl,al', 2, lambda L, a: [0x88, 0xC3])
ins(None, 'lodsb                ; D', 1, lambda L, a: [0xAC])
ins(None, 'mov bh,al', 2, lambda L, a: [0x88, 0xC7])
ins(None, 'mov al,ah', 2, lambda L, a: [0x88, 0xE0])
ins('o9', 'out dx,al', 1, lambda L, a: [0xEE])
ins(None, 'add al,bl', 2, lambda L, a: [0x00, 0xD8])
ins(None, 'push cx', 1, lambda L, a: [0x51])
ins(None, 'mov cl,bh', 2, lambda L, a: [0x88, 0xF9])
ins(None, 'mov ch,0', 2, lambda L, a: [0xB5, 0x00])
ins('d9', 'loop d9', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'd9')])
ins(None, 'pop cx', 1, lambda L, a: [0x59])
ins(None, 'loop o9', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'o9')])
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
def FILL(seg, off, n, seed, step): return [2] + w(seg) + w(off) + w(n) + [seed, step]
def WAIT(n): return [3, n]
def PEEK(seg, off): return [4] + w(seg) + w(off)
def INB(port): return [5] + w(port)
def SAMPLE(port, n): return [6] + w(port) + w(n)
def MODE(m): return [7] + w(m)
def POKE(seg, off, v): return [8] + w(seg) + w(off) + [v]
def GC(i, v): return OUTB(0x3CE, i) + OUTB(0x3CF, v)
def SEQ(i, v): return OUTB(0x3C4, i) + OUTB(0x3C5, v)
def OUTS(port, n, seed, step, d=40): return [9] + w(port) + w(n) + [seed, step, d]

S = []
for off in (0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x11):          # ce que le BIOS a trouvé
    S += PEEK(0x0040, off)
S += INB(0x379) + INB(0x37A) + INB(0x279)
S += OUTB(0x378, 0x5A) + INB(0x378) + OUTB(0x37A, 0x0C) + INB(0x37A)   # LPT1
S += OUTB(0x278, 0xA5) + INB(0x278) + OUTB(0x27A, 0x04) + INB(0x27A)   # LPT2
S += OUTS(0x378, 24, 0x10, 0x09, 1) + INB(0x379)         # rafale : la file de la Sound Source pleine
S += SAMPLE(0x379, 1000)                                 # ~7 ms d'écoute : chaque lecture passe au
#   diff d'instructions, qui voit donc l'instant où le bit 6 tombe (la file à 15, après une vidange
#   à 7 kHz) — un seuil ou une cadence faux rougit la porte, ce que deux lectures isolées, pleine
#   puis vide, ne voyaient pas (contrôle négatif de G10.0, VERIFICATION.md)
S += WAIT(2) + INB(0x379)                                # vidée
S += OUTS(0x378, 256, 0x00, 0x01) + INB(0x379)           # une rampe (Covox)
for k in range(8):                                       # canal gauche / droit (Covox stéréo)
    S += OUTB(0x37A, k & 1) + OUTS(0x378, 16, 0x80 + k * 8, 0x11)
S += WAIT(3) + INB(0x379) + OUTB(0x37A, 0x0C) + OUTB(0x378, 0x80)
S += [0]

ins('script', 'db script', len(S), lambda L, a, S=S: list(S))
ins('crlf', "db 13,10,'$'", 3, lambda L, a: [13, 10, 0x24])
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
    lines = ['B:', 'DEBUG']
    for i in range(0, len(code), 16):
        lines.append(f'E {ORG + i:X} ' + ' '.join(f'{b:02X}' for b in code[i:i + 16]))
    lines += ['N LPTBANC.COM', 'R CX', f'{len(code):X}', 'W', 'Q', 'LPTBANC']
    return lines


if __name__ == '__main__':
    code, listing = assemble()
    print('\n'.join(listing))
    print(f'\n{len(code)} octets, script {len(S)} octets')
    here = os.path.dirname(os.path.abspath(__file__))
    with open(os.path.join(here, 'lptbanc.keys'), 'w') as f:
        f.write('\n'.join(keys(code)) + '\n')
    if '--com' in sys.argv:
        with open(sys.argv[sys.argv.index('--com') + 1], 'wb') as f:
            f.write(code)
