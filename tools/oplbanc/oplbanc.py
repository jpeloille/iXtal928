#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# OPLBANC.COM — le banc dirigé de l'OPL, G8.1 (PLAN-G8.md).
#
# Le diff d'instructions voit ce que le logiciel LIT de l'OPL — l'état en 388h, ses deux
# minuteries (sound_opl.c, sound_dbopl.cc:48-117) — et rien de ce qu'il JOUE : les échantillons
# ne se voient qu'au hachage de la sonde du son (sound_hash, M9). OPLBANC.COM fait les deux : la
# détection AdLib de tous les jeux (minuteries 1 et 2, masques, remise à zéro de l'IRQ), puis
# des notes sur les neuf voix, avec des attentes pour que la puce produise. Petit interprète sur
# le patron de P1512 (G1.2), instructions 8086 seules ; saisi dans DEBUG (disquette
# supplémentaire de PC-DOS 2.00 en B:), écrit sur B:, lancé sous boot-diff.
#
# Usage :
#   python3 tools/oplbanc/oplbanc.py           écrit oplbanc.keys, affiche le listing
#   python3 tools/oplbanc/oplbanc.py --com F   écrit aussi le .COM

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

# op 2 : écrire un registre de l'OPL : OUT 388h,index ; 6 IN 388h ; OUT 389h,valeur ;
#        35 IN 388h — les attentes que la documentation AdLib prescrit.
case(2, 'op2', 'op3')
ins(None, 'mov dx,388h', 3, lambda L, a: [0xBA, 0x88, 0x03])
ins(None, 'lodsb                ; index', 1, lambda L, a: [0xAC])
ins(None, 'out dx,al', 1, lambda L, a: [0xEE])
ins(None, 'mov cx,6', 3, lambda L, a: [0xB9, 0x06, 0x00])
ins('w2a', 'in al,dx', 1, lambda L, a: [0xEC])
ins(None, 'loop w2a', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'w2a')])
ins(None, 'inc dx', 1, lambda L, a: [0x42])
ins(None, 'lodsb                ; valeur', 1, lambda L, a: [0xAC])
ins(None, 'out dx,al', 1, lambda L, a: [0xEE])
ins(None, 'dec dx', 1, lambda L, a: [0x4A])
ins(None, 'mov cx,35', 3, lambda L, a: [0xB9, 0x23, 0x00])
ins('w2b', 'in al,dx', 1, lambda L, a: [0xEC])
ins(None, 'loop w2b', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'w2b')])
nxt()

# op 3 : attente, N × 65 536 tours de LOOP
case(3, 'op3', 'op5')
ins(None, 'lodsb                ; N', 1, lambda L, a: [0xAC])
ins(None, 'mov bl,al', 2, lambda L, a: [0x88, 0xC3])
ins('w3', 'xor cx,cx', 2, lambda L, a: [0x31, 0xC9])
ins('w3b', 'loop w3b', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'w3b')])
ins(None, 'dec bl', 2, lambda L, a: [0xFE, 0xCB])
ins(None, 'jnz w3', 2, lambda L, a: [0x75, rel8(L, a + 2, 'w3')])
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
def REG(idx, val): return [2, idx, val]
def WAIT(n): return [3, n]
def INB(port): return [5] + w(port)


S = []
# -- la détection AdLib : minuteries remises à zéro, état lu ; minuterie 1 à FFh, lancée, attendue,
#    état relu (C0h attendu sur une carte présente) ; puis remise à zéro.
S += REG(0x04, 0x60) + REG(0x04, 0x80) + INB(0x388)
S += REG(0x02, 0xFF) + REG(0x04, 0x21) + WAIT(1) + INB(0x388)
S += REG(0x04, 0x60) + REG(0x04, 0x80) + INB(0x388)
# -- la minuterie 2 (période 16 fois plus longue), masque de la 1
S += REG(0x03, 0xF0) + REG(0x04, 0x42) + WAIT(2) + INB(0x388) + REG(0x04, 0x80) + INB(0x388)
# -- un instrument sur les neuf voix, puis neuf notes tenues, puis relâchées
S += REG(0x01, 0x20)                                 # formes d'onde permises
OPS = [0x00, 0x01, 0x02, 0x08, 0x09, 0x0A, 0x10, 0x11, 0x12]
for ch, op in enumerate(OPS):
    for o, (mult, tl, ar, sl, ws) in ((op, (0x21, 0x10, 0xF2, 0x54, 0x00)),
                                     (op + 3, (0x01, 0x00, 0xF4, 0x56, ch & 3))):
        S += REG(0x20 + o, mult) + REG(0x40 + o, tl) + REG(0x60 + o, ar) + REG(0x80 + o, sl) + REG(0xE0 + o, ws)
    S += REG(0xC0 + ch, 0x06 + (ch & 1))             # rétroaction, connexion
for ch in range(9):
    fnum = 0x157 + 0x30 * ch
    S += REG(0xA0 + ch, fnum & 0xFF) + REG(0xB0 + ch, 0x20 | (4 << 2) | (fnum >> 8))
S += WAIT(4)
S += REG(0xBD, 0x20) + REG(0xBD, 0x3F) + WAIT(2)     # le mode rythme, les cinq percussions
for ch in range(9):
    S += REG(0xB0 + ch, 0x10)                        # relâchées
S += REG(0xBD, 0x00) + WAIT(2) + INB(0x388)
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
    lines += ['N OPLBANC.COM', 'R CX', f'{len(code):X}', 'W', 'Q', 'OPLBANC']
    return lines


if __name__ == '__main__':
    code, listing = assemble()
    print('\n'.join(listing))
    print(f'\n{len(code)} octets, script {len(S)} octets')
    here = os.path.dirname(os.path.abspath(__file__))
    with open(os.path.join(here, 'oplbanc.keys'), 'w') as f:
        f.write('\n'.join(keys(code)) + '\n')
    if '--com' in sys.argv:
        with open(sys.argv[sys.argv.index('--com') + 1], 'wb') as f:
            f.write(code)
