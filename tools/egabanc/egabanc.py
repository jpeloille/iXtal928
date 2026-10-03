#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# EGABANC.COM — le banc dirigé de l'EGA d'IBM, G9.2 (PLAN-G9.md).
#
# Par le BIOS de la carte (INT 10h) il passe en 0Dh, 0Eh, 10h, 04h puis revient en texte ; entre
# deux, il programme le contrôleur graphique et le séquenceur directement : masque de plans,
# set/reset, rotation et fonctions logiques, masque de bits, modes d'écriture 0, 1 et 2, lecture
# en mode 1 (comparaison de couleur) et sélection du plan lu, les verrous ; il remplit les plans,
# relit des octets et échantillonne 3DAh. Le diff d'instructions voit les lectures ; la sonde de
# l'EGA, les registres, la palette, la VRAM de 256 Ko et le balayage.
# Instructions 8086 seules ; saisi dans DEBUG (disquette supplémentaire de PC-DOS 2.00 en B:),
# écrit sur B:, lancé sous boot-diff.
#
# Usage :
#   python3 tools/egabanc/egabanc.py           écrit egabanc.keys, affiche le listing
#   python3 tools/egabanc/egabanc.py --com F   écrit aussi le .COM

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
case(8, 'op8', 'done')
ins(None, 'push es', 1, lambda L, a: [0x06])
ins(None, 'lodsw                ; segment', 1, lambda L, a: [0xAD])
ins(None, 'mov es,ax', 2, lambda L, a: [0x8E, 0xC0])
ins(None, 'lodsw                ; déplacement', 1, lambda L, a: [0xAD])
ins(None, 'mov bx,ax', 2, lambda L, a: [0x89, 0xC3])
ins(None, 'lodsb                ; octet', 1, lambda L, a: [0xAC])
ins(None, 'mov [es:bx],al', 3, lambda L, a: [0x26, 0x88, 0x07])
ins(None, 'pop es', 1, lambda L, a: [0x07])
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
A = 0xA000

S = []
S += PEEK(0xB800, 0x0000) + SAMPLE(0x3DA, 2000)          # texte : 3DAh (et le hack ^ 30h, PB-99)
S += MODE(0x000D)                                       # 320×200, 16 couleurs
S += POKE(A, 0x0000, 0xFF)                              # mode d'écriture 0, quatre plans
S += GC(0, 0x05) + GC(1, 0x0F) + POKE(A, 0x0001, 0x00)  # set/reset : plans 0 et 2 à FF
S += GC(1, 0x00)
S += GC(3, 0x03) + POKE(A, 0x0002, 0x81)                # rotation de 3
S += GC(3, 0x18) + PEEK(A, 0x0002) + POKE(A, 0x0002, 0xFF)   # XOR sur les verrous
S += GC(3, 0x00) + GC(8, 0x0F) + POKE(A, 0x0003, 0xFF) + GC(8, 0xFF)   # masque de bits
S += SEQ(2, 0x05) + POKE(A, 0x0004, 0xAA) + SEQ(2, 0x0F)              # masque de plans
S += GC(5, 0x01) + PEEK(A, 0x0000) + POKE(A, 0x0010, 0x00)            # mode 1 : copie des verrous
S += GC(5, 0x02) + POKE(A, 0x0020, 0x05) + POKE(A, 0x0021, 0x0A)      # mode 2 : couleurs
S += GC(5, 0x08) + GC(2, 0x05) + GC(7, 0x0F) + PEEK(A, 0x0020) + PEEK(A, 0x0021)   # lecture mode 1
S += GC(7, 0x05) + PEEK(A, 0x0021)                                    # « don't care »
S += GC(5, 0x00)
for p in range(4):                                       # le plan lu
    S += GC(4, p) + PEEK(A, 0x0000) + PEEK(A, 0x0001) + PEEK(A, 0x0002) + PEEK(A, 0x0003) + PEEK(A, 0x0010) + PEEK(A, 0x0020)
S += FILL(A, 0x0100, 0x1F00, 0x11, 0x0B)                # un motif sur l'écran
S += WAIT(3) + SAMPLE(0x3DA, 2000)
S += MODE(0x000E) + FILL(A, 0x0000, 0x4000, 0x3C, 0x05) + WAIT(3)     # 640×200
S += MODE(0x0010) + FILL(A, 0x0000, 0x6D60, 0x55, 0x01) + WAIT(3)     # 640×350
S += GC(4, 2) + PEEK(A, 0x1234) + PEEK(A, 0x6D5F)
S += MODE(0x0004) + FILL(0xB800, 0x0000, 0x2000, 0xE4, 0x03) + WAIT(3)   # 320×200, 4 couleurs
S += PEEK(0xB800, 0x0001) + PEEK(0xB800, 0x1FFF)
for r in (0x00, 0x01, 0x06, 0x09, 0x0C, 0x13, 0x14, 0x17):            # le CRTC se relit (PB-99)
    S += OUTB(0x3D4, r) + INB(0x3D5)
S += OUTB(0x3C0, 0x10) + INB(0x3C1) + INB(0x3DA)                      # l'attribut 10h se relit
S += MODE(0x0003)                                       # retour au texte
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
    lines += ['N EGABANC.COM', 'R CX', f'{len(code):X}', 'W', 'Q', 'EGABANC']
    return lines


if __name__ == '__main__':
    code, listing = assemble()
    print('\n'.join(listing))
    print(f'\n{len(code)} octets, script {len(S)} octets')
    here = os.path.dirname(os.path.abspath(__file__))
    with open(os.path.join(here, 'egabanc.keys'), 'w') as f:
        f.write('\n'.join(keys(code)) + '\n')
    if '--com' in sys.argv:
        with open(sys.argv[sys.argv.index('--com') + 1], 'wb') as f:
            f.write(code)
