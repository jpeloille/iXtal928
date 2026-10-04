#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# JOYBANC.COM — le banc dirigé du port jeu et des manettes, G10.1 (PLAN-G10.md).
#
# Douze tours ; chacun, sur le port 201h :
#   - CHRONO : une écriture arme les quatre chronomètres d'axe (gameport_write), puis on lit le
#     port jusqu'à ce que les quatre bits d'axe tombent, en comptant pour chacun les lectures où il
#     est encore levé : le temps de chaque axe, donc sa valeur (gameport_time). La première lecture
#     (les boutons, le chapeau de la CH) est gardée ;
#   - RAFALE : une écriture, puis 128 lectures, dont on garde la somme et le nombre de changements
#     — les paquets numériques de la SideWinder défilent sur les bits 4 à 7 ;
#   - ID : une écriture, l'attente de la chute de l'axe 0 (a0_over), un délai qui croît d'un tour à
#     l'autre (4 + 3 × tour boucles), une seconde écriture, puis 160 lectures (somme, changements) :
#     dans la fenêtre de 60 à 100 µs après la chute, la SideWinder envoie son paquet
#     d'identification (joystick_sw_pad.c:122) ;
#   - une attente d'environ 0,47 s (2 × 65 536 boucles), pendant laquelle l'outil change l'état
#     de la manette (iXtal26.Diff --joy-at).
# Puis une ligne par tour, 17 octets en hexadécimal : le premier octet lu, les quatre comptes
# d'axe (mots), la somme et les changements de la rafale, ceux de l'ID. Le diff d'instructions
# voit chaque lecture ; l'écran garde les douze lignes.
#
# Usage :
#   python3 tools/joybanc/joybanc.py           écrit joybanc.keys, affiche le listing
#   python3 tools/joybanc/joybanc.py --com F   écrit aussi le .COM

import os
import struct
import sys

ORG = 0x100
TOURS = 12
OCTETS_PAR_TOUR = 17


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


# ---- les douze tours --------------------------------------------------------------------------
ins('start', 'mov di,results', 3, lambda L, a: [0xBF] + w(L['results']))
ins(None, 'cld', 1, lambda L, a: [0xFC])
ins(None, 'mov dx,201h', 3, lambda L, a: [0xBA] + w(0x201))
ins(None, 'xor si,si            ; le tour', 2, lambda L, a: [0x31, 0xF6])

# CHRONO
ins('tour', 'out dx,al            ; CHRONO : arme les chronomètres', 1, lambda L, a: [0xEE])
ins(None, 'in al,dx', 1, lambda L, a: [0xEC])
ins(None, 'stosb                ; la première lecture', 1, lambda L, a: [0xAA])
ins(None, 'xor ax,ax', 2, lambda L, a: [0x31, 0xC0])
ins(None, 'mov [di],ax', 2, lambda L, a: [0x89, 0x05])
ins(None, 'mov [di+2],ax', 3, lambda L, a: [0x89, 0x45, 0x02])
ins(None, 'mov [di+4],ax', 3, lambda L, a: [0x89, 0x45, 0x04])
ins(None, 'mov [di+6],ax', 3, lambda L, a: [0x89, 0x45, 0x06])
ins(None, 'mov cx,1000h', 3, lambda L, a: [0xB9] + w(0x1000))
ins('t0', 'in al,dx', 1, lambda L, a: [0xEC])
ins(None, 'test al,0Fh', 2, lambda L, a: [0xA8, 0x0F])
ins(None, 'jz t1', 2, lambda L, a: [0x74, rel8(L, a + 2, 't1')])
for k in range(4):
    ins(None, 'shr al,1', 2, lambda L, a: [0xD0, 0xE8])
    if k == 0:
        ins(None, 'adc word [di],0', 3, lambda L, a: [0x83, 0x15, 0x00])
    else:
        ins(None, f'adc word [di+{2 * k}],0', 4, lambda L, a, k=k: [0x83, 0x55, 2 * k, 0x00])
ins(None, 'loop t0', 2, lambda L, a: [0xE2, rel8(L, a + 2, 't0')])
ins('t1', 'add di,8', 3, lambda L, a: [0x83, 0xC7, 0x08])

# RAFALE
ins(None, 'out dx,al            ; RAFALE', 1, lambda L, a: [0xEE])
ins(None, 'mov cx,128', 3, lambda L, a: [0xB9] + w(128))
ins(None, 'call sample', 3, lambda L, a: [0xE8] + rel16(L, a + 3, 'sample'))

# ID
ins(None, 'out dx,al            ; ID', 1, lambda L, a: [0xEE])
ins(None, 'mov cx,1000h', 3, lambda L, a: [0xB9] + w(0x1000))
ins('i0', 'in al,dx', 1, lambda L, a: [0xEC])
ins(None, 'test al,1', 2, lambda L, a: [0xA8, 0x01])
ins(None, 'loopnz i0            ; jusqu’à la chute de l’axe 0', 2, lambda L, a: [0xE0, rel8(L, a + 2, 'i0')])
ins(None, 'mov cx,si', 2, lambda L, a: [0x89, 0xF1])
ins(None, 'add cx,cx', 2, lambda L, a: [0x01, 0xC9])
ins(None, 'add cx,si', 2, lambda L, a: [0x01, 0xF1])
ins(None, 'add cx,4             ; 4 + 3 × tour', 3, lambda L, a: [0x83, 0xC1, 0x04])
ins('i1', 'loop i1', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'i1')])
ins(None, 'out dx,al', 1, lambda L, a: [0xEE])
ins(None, 'mov cx,160', 3, lambda L, a: [0xB9] + w(160))
ins(None, 'call sample', 3, lambda L, a: [0xE8] + rel16(L, a + 3, 'sample'))

# l'attente
ins(None, 'mov bl,2', 2, lambda L, a: [0xB3, 0x02])
ins(None, 'xor cx,cx', 2, lambda L, a: [0x31, 0xC9])
ins('w0', 'loop w0', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'w0')])
ins(None, 'dec bl', 2, lambda L, a: [0xFE, 0xCB])
ins(None, 'jnz w0', 2, lambda L, a: [0x75, rel8(L, a + 2, 'w0')])
ins(None, 'inc si', 1, lambda L, a: [0x46])
ins(None, f'cmp si,{TOURS}', 3, lambda L, a: [0x83, 0xFE, TOURS])
ins(None, 'jae pr', 2, lambda L, a: [0x73, rel8(L, a + 2, 'pr')])
ins(None, 'jmp tour', 3, lambda L, a: [0xE9] + rel16(L, a + 3, 'tour'))

# ---- l'impression : une ligne par tour --------------------------------------------------------
ins('pr', 'mov si,results', 3, lambda L, a: [0xBE] + w(L['results']))
ins('pl', f'mov bh,{OCTETS_PAR_TOUR}', 2, lambda L, a: [0xB7, OCTETS_PAR_TOUR])
ins('pb', 'lodsb', 1, lambda L, a: [0xAC])
ins(None, 'call hex2', 3, lambda L, a: [0xE8] + rel16(L, a + 3, 'hex2'))
ins(None, "mov dl,' '", 2, lambda L, a: [0xB2, 0x20])
ins(None, 'mov ah,2', 2, lambda L, a: [0xB4, 0x02])
ins(None, 'int 21h', 2, lambda L, a: [0xCD, 0x21])
ins(None, 'dec bh', 2, lambda L, a: [0xFE, 0xCF])
ins(None, 'jnz pb', 2, lambda L, a: [0x75, rel8(L, a + 2, 'pb')])
ins(None, 'mov dx,crlf', 3, lambda L, a: [0xBA] + w(L['crlf']))
ins(None, 'mov ah,9', 2, lambda L, a: [0xB4, 0x09])
ins(None, 'int 21h', 2, lambda L, a: [0xCD, 0x21])
ins(None, 'cmp si,fin', 4, lambda L, a: [0x81, 0xFE] + w(L['results'] + TOURS * OCTETS_PAR_TOUR))
ins(None, 'jb pl', 2, lambda L, a: [0x72, rel8(L, a + 2, 'pl')])
ins(None, 'mov ax,4C00h', 3, lambda L, a: [0xB8, 0x00, 0x4C])
ins(None, 'int 21h', 2, lambda L, a: [0xCD, 0x21])

# hex2 : AL en deux chiffres (nib en queue d'appel)
ins('hex2', 'push ax', 1, lambda L, a: [0x50])
ins(None, 'mov cl,4', 2, lambda L, a: [0xB1, 0x04])
ins(None, 'shr al,cl', 2, lambda L, a: [0xD2, 0xE8])
ins(None, 'call nib', 3, lambda L, a: [0xE8] + rel16(L, a + 3, 'nib'))
ins(None, 'pop ax', 1, lambda L, a: [0x58])
ins(None, 'and al,0Fh', 2, lambda L, a: [0x24, 0x0F])
ins('nib', "add al,'0'", 2, lambda L, a: [0x04, 0x30])
ins(None, "cmp al,'9'", 2, lambda L, a: [0x3C, 0x39])
ins(None, 'jbe np', 2, lambda L, a: [0x76, rel8(L, a + 2, 'np')])
ins(None, 'add al,7', 2, lambda L, a: [0x04, 0x07])
ins('np', 'mov dl,al', 2, lambda L, a: [0x88, 0xC2])
ins(None, 'mov ah,2', 2, lambda L, a: [0xB4, 0x02])
ins(None, 'int 21h', 2, lambda L, a: [0xCD, 0x21])
ins(None, 'ret', 1, lambda L, a: [0xC3])

# sample : CX lectures de DX ; somme (mot) et changements (mot) rangés en ES:DI
ins('sample', 'xor bx,bx', 2, lambda L, a: [0x31, 0xDB])
ins(None, 'xor bp,bp', 2, lambda L, a: [0x31, 0xED])
ins(None, 'in al,dx', 1, lambda L, a: [0xEC])
ins(None, 'mov ah,al', 2, lambda L, a: [0x88, 0xC4])
ins('s0', 'in al,dx', 1, lambda L, a: [0xEC])
ins(None, 'add bl,al', 2, lambda L, a: [0x00, 0xC3])
ins(None, 'adc bh,0', 3, lambda L, a: [0x80, 0xD7, 0x00])
ins(None, 'cmp al,ah', 2, lambda L, a: [0x38, 0xE0])
ins(None, 'je s1', 2, lambda L, a: [0x74, rel8(L, a + 2, 's1')])
ins(None, 'inc bp', 1, lambda L, a: [0x45])
ins('s1', 'mov ah,al', 2, lambda L, a: [0x88, 0xC4])
ins(None, 'loop s0', 2, lambda L, a: [0xE2, rel8(L, a + 2, 's0')])
ins(None, 'mov ax,bx', 2, lambda L, a: [0x89, 0xD8])
ins(None, 'stosw', 1, lambda L, a: [0xAB])
ins(None, 'mov ax,bp', 2, lambda L, a: [0x89, 0xE8])
ins(None, 'stosw', 1, lambda L, a: [0xAB])
ins(None, 'ret', 1, lambda L, a: [0xC3])

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
    # « ^ » : une ligne vide sans Entrée, qui ne tape rien et laisse un délai de plus (--type-settle)
    # — le temps que DEBUG se charge depuis B:, puis que W écrive le fichier.
    lines = ['B:', 'DEBUG', '^', '^', '^']
    for i in range(0, len(code), 16):
        lines.append(f'E {ORG + i:X} ' + ' '.join(f'{b:02X}' for b in code[i:i + 16]))
    lines += ['N JOYBANC.COM', 'R CX', f'{len(code):X}', 'W', '^', '^', 'Q', 'JOYBANC']
    return lines


if __name__ == '__main__':
    code, listing = assemble()
    print('\n'.join(listing))
    print(f'\n{len(code)} octets')
    here = os.path.dirname(os.path.abspath(__file__))
    with open(os.path.join(here, 'joybanc.keys'), 'w') as f:
        f.write('\n'.join(keys(code)) + '\n')
    if '--com' in sys.argv:
        with open(sys.argv[sys.argv.index('--com') + 1], 'wb') as f:
            f.write(code)
