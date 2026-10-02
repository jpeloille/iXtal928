#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# P1512.COM — le banc dirigé du mode plan de l'Amstrad PC1512, G1.2 (PLAN-G1.md).
#
# En mode 6 (640 × 200), le PC1512 passe en 16 couleurs : (cgamode & 0x12) == 0x12 et la
# fenêtre B800 donne sur QUATRE plans de 16 Ko (vid_pc1512.c:111-141) — 3DDh choisit les plans
# écrits (un masque), 3DEh le plan lu, 3DFh la bordure (:84-92), 3DAh bascule son bit 0 à chaque
# lecture (:104-106). Aucun BIOS ni DOS ne l'exerce. P1512.COM est un petit interprète, sur le
# patron de BLTBANC (G7.2) : écrire un port, appeler l'INT 10h, remplir la fenêtre, relire la
# fenêtre en somme tournante, lire un port. Saisi dans DEBUG.EXE (PC-DOS 2.00, disquette
# supplémentaire en B:), écrit sur B:, lancé — sous boot-diff, qui compare chaque instruction et
# la sonde du PC1512 (VRAM des quatre plans, registres). La preuve est le diff.
#
# Usage :
#   python3 tools/pc1512banc/pc1512banc.py           écrit pc1512banc.keys, affiche le listing
#   python3 tools/pc1512banc/pc1512banc.py --com F   écrit aussi le .COM

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

# op 2 : INT 10h, AX du script
case(2, 'op2', 'op3')
ins(None, 'lodsw', 1, lambda L, a: [0xAD])
ins(None, 'push si', 1, lambda L, a: [0x56])
ins(None, 'push di', 1, lambda L, a: [0x57])
ins(None, 'int 10h', 2, lambda L, a: [0xCD, 0x10])
ins(None, 'pop di', 1, lambda L, a: [0x5F])
ins(None, 'pop si', 1, lambda L, a: [0x5E])
nxt()

# op 3 : remplir N mots en B800:déplacement, graine, + pas par mot
case(3, 'op3', 'op4')
ins(None, 'lodsw                ; déplacement', 1, lambda L, a: [0xAD])
ins(None, 'push di', 1, lambda L, a: [0x57])
ins(None, 'mov di,ax', 2, lambda L, a: [0x89, 0xC7])
ins(None, 'lodsw                ; mots', 1, lambda L, a: [0xAD])
ins(None, 'mov cx,ax', 2, lambda L, a: [0x89, 0xC1])
ins(None, 'lodsw                ; graine', 1, lambda L, a: [0xAD])
ins(None, 'push ax', 1, lambda L, a: [0x50])
ins(None, 'lodsw                ; pas', 1, lambda L, a: [0xAD])
ins(None, 'mov bx,ax', 2, lambda L, a: [0x89, 0xC3])
ins(None, 'mov ax,0B800h', 3, lambda L, a: [0xB8, 0x00, 0xB8])
ins(None, 'mov es,ax', 2, lambda L, a: [0x8E, 0xC0])
ins(None, 'pop ax', 1, lambda L, a: [0x58])
ins('f3', 'stosw', 1, lambda L, a: [0xAB])
ins(None, 'add ax,bx', 2, lambda L, a: [0x01, 0xD8])
ins(None, 'loop f3', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'f3')])
ins(None, 'pop di', 1, lambda L, a: [0x5F])
ins(None, 'push cs', 1, lambda L, a: [0x0E])
ins(None, 'pop es', 1, lambda L, a: [0x07])
nxt()

# op 4 : somme tournante de N mots lus en B800:déplacement → relevé (2 octets)
case(4, 'op4', 'op5')
ins(None, 'lodsw                ; déplacement', 1, lambda L, a: [0xAD])
ins(None, 'mov bx,ax', 2, lambda L, a: [0x89, 0xC3])
ins(None, 'lodsw                ; mots', 1, lambda L, a: [0xAD])
ins(None, 'mov cx,ax', 2, lambda L, a: [0x89, 0xC1])
ins(None, 'mov ax,0B800h', 3, lambda L, a: [0xB8, 0x00, 0xB8])
ins(None, 'mov es,ax', 2, lambda L, a: [0x8E, 0xC0])
ins(None, 'xor dx,dx', 2, lambda L, a: [0x31, 0xD2])
ins('s4', 'mov ax,es:[bx]', 3, lambda L, a: [0x26, 0x8B, 0x07])
ins(None, 'rol dx,1', 2, lambda L, a: [0xD1, 0xC2])
ins(None, 'add dx,ax', 2, lambda L, a: [0x01, 0xC2])
ins(None, 'add bx,2', 3, lambda L, a: [0x83, 0xC3, 0x02])
ins(None, 'loop s4', 2, lambda L, a: [0xE2, rel8(L, a + 2, 's4')])
ins(None, 'push cs', 1, lambda L, a: [0x0E])
ins(None, 'pop es', 1, lambda L, a: [0x07])
ins(None, 'mov ax,dx', 2, lambda L, a: [0x89, 0xD0])
ins(None, 'stosw', 1, lambda L, a: [0xAB])
nxt()

# op 5 : IN AL,port → relevé (1 octet)
case(5, 'op5', 'done')
ins(None, 'lodsw                ; port', 1, lambda L, a: [0xAD])
ins(None, 'mov dx,ax', 2, lambda L, a: [0x89, 0xC2])
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
def INT10(ax): return [2] + w(ax)
def FILL(off, n, seed, step): return [3] + w(off) + w(n) + w(seed) + w(step)
def SUM(off, n): return [4] + w(off) + w(n)
def INB(port): return [5] + w(port)


S = []
S += INT10(0x0006)                                   # 640 × 200 : le mode plan du PC1512
S += OUTB(0x3DD, 0x0F) + FILL(0x0000, 0x2000, 0x0000, 0x0000)        # quatre plans effacés
for p in range(4):                                   # un motif par plan
    S += OUTB(0x3DD, 1 << p) + FILL(0x0400 * p, 0x0800, 0x1111 * (p + 1), 0x0103 + p)
S += OUTB(0x3DD, 0x05) + FILL(0x2000, 0x0400, 0xA55A, 0x0201)        # plans 0 et 2 ensemble
S += OUTB(0x3DD, 0x0A) + FILL(0x2800, 0x0400, 0x5AA5, 0x0102)        # plans 1 et 3 ensemble
S += OUTB(0x3DF, 0x05)                               # la bordure
for p in range(4):                                   # chaque plan relu
    S += OUTB(0x3DE, p) + SUM(0x0000, 0x2000)
S += OUTB(0x3DE, 0x07) + SUM(0x0000, 0x0100)         # 3DEh masqué à 3 (vid_pc1512.c:88)
S += INB(0x3DA) + INB(0x3DA) + INB(0x3DA)            # le bit 0 bascule à chaque lecture
S += OUTB(0x3DD, 0x0F) + OUTB(0x3DE, 0x00)
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
    lines += ['N P1512.COM', 'R CX', f'{len(code):X}', 'W', 'Q', 'P1512']
    return lines


if __name__ == '__main__':
    code, listing = assemble()
    print('\n'.join(listing))
    print(f'\n{len(code)} octets, script {len(S)} octets')
    here = os.path.dirname(os.path.abspath(__file__))
    with open(os.path.join(here, 'pc1512banc.keys'), 'w') as f:
        f.write('\n'.join(keys(code)) + '\n')
    if '--com' in sys.argv:
        with open(sys.argv[sys.argv.index('--com') + 1], 'wb') as f:
            f.write(code)
