#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# DMABANC.COM — le banc dirigé du 8237, G13.4 (PLAN-G13.md) : les cas qui discriminent PB-157, PB-249, PB-250, PB-251
# et PB-253 sur une machine qui tourne, sous DOS, en mode PCem (contre l'oracle, par boot-diff) et en mode matériel
# (contre les attendus de la fiche du 8237A, par le banc en C# seul). PB-252, le reset, n'a de cas qu'en C# seul. Tout
# se fait sous CLI. Il imprime une ligne, « DMABANC », puis ses relevés en hexadécimal :
#   1. L'XT seulement : le canal 0, celui du rafraîchissement, que le PIT relance toutes les 15 µs. Les quatre masques
#      posés (OUT 0Fh,0Fh), le compte du canal 0 relu deux fois : 00h, il ne bouge pas. Puis Clear Mask (OUT 0Eh,00h) et
#      de même : 01h, il bouge (PB-249 ; fiche p. 9) ; PCem 00h, le canal reste masqué.
#   2. Le canal 1 masqué, en bloc et en vérification (81h), compte 0003h ; la requête logicielle (OUT 09h,05h) : l'état
#      (bit 0 écarté, le TC du rafraîchissement de l'XT), puis le compte. Le 8237A fait quatre transferts, non
#      masquables : 02h, FFFFh (PB-250 ; p. 7) ; PCem 00h, 0003h.
#   3. Le 8237 bas désactivé (OUT 08h,04h), puis le master clear (OUT 0Dh) et la même requête : le compte. Le master
#      clear efface la commande (PB-251 ; p. 9) : FFFFh ; PCem 0003h.
#   4. L'AT seulement (octet de modèle FCh) : le canal 4 masqué (OUT D4h,04h), la même requête : le compte, 0003h, le
#      8237 bas n'a pas le bus (PB-253 ; AT TR p. 1-13) ; puis Clear Mask au 8237 haut (OUT DCh,00h) : FFFFh, la
#      requête servie (PB-249). Le 8237 haut désactivé (OUT D0h,04h) : 0003h ; réactivé : FFFFh (PB-157, PB-253).
#      Enfin le master clear du haut (OUT DAh,5Ah) et IN DAh, le temporaire : 00h (PB-157) ; PCem 5Ah. PCem rend
#      0003h partout : il n'a pas de requête logicielle.
# À la fin, les canaux remis comme le BIOS les laisse : sur l'XT, le canal 0 démasqué ; sur l'AT, le canal 4 en cascade
# démasqué, les canaux 5 à 7 masqués ; les deux commandes à 00h. Le BIOS démasque le canal 2 à chaque accès disque.
#
# Usage :
#   python3 tools/dmabanc/dmabanc.py           écrit dmabanc.keys, affiche le listing
#   python3 tools/dmabanc/dmabanc.py --com F   écrit aussi le .COM

import os
import struct
import sys

ORG = 0x100


def w(v):
    return list(struct.pack('<H', v & 0xFFFF))


PROG = []


def ins(label, text, size, enc):
    PROG.append((label, text, size, enc))


def b(label, text, *octets):
    ins(label, text, len(octets), lambda L, a, o=list(octets): o)


def rel8(L, end, target):
    d = L[target] - end
    assert -128 <= d <= 127, (target, d)
    return d & 0xFF


def jcc(label, op, mnem, target):
    ins(label, f'{mnem} {target}', 2, lambda L, a: [op, rel8(L, a + 2, target)])


def call(label, target):
    ins(label, f'call {target}', 3, lambda L, a: [0xE8] + w(L[target] - (a + 3)))


def out(port, val, label=None):
    b(label, f'mov al,{val:02X}h', 0xB0, val)
    b(None, f'out {port:02X}h,al', 0xE6, port)


def movb(var):
    """MOV [var],AL."""
    ins(None, f'mov [{var}],al', 3, lambda L, a, v=var: [0xA2] + w(L[v]))


def movw(var):
    """MOV [var],AX : l'octet bas, puis le haut."""
    ins(None, f'mov [{var}],ax', 3, lambda L, a, v=var: [0xA3] + w(L[v]))


def at_seulement(sinon):
    ins(None, 'cmp byte [modele],0FCh', 5, lambda L, a: [0x80, 0x3E] + w(L['modele']) + [0xFC])
    jcc(None, 0x75, 'jne', sinon)


# ---- le programme ------------------------------------------------------------------------
b('start', 'cli', 0xFA)
b(None, 'mov ax,0F000h', 0xB8, 0x00, 0xF0)
b(None, 'mov es,ax', 0x8E, 0xC0)
b(None, 'mov al,es:[0FFFEh]', 0x26, 0xA0, 0xFE, 0xFF)
movb('modele')

# 1. L'XT : Clear Mask, vu par le rafraîchissement.
ins(None, 'cmp byte [modele],0FCh', 5, lambda L, a: [0x80, 0x3E] + w(L['modele']) + [0xFC])
jcc(None, 0x74, 'je', 'r2')
out(0x0F, 0x0F)
call(None, 'bouge')
movb('r_cm1')
out(0x0E, 0x00)
call(None, 'bouge')
movb('r_cm2')
out(0x0A, 0x00)
out(0x0F, 0x0E)

# 2. La requête logicielle.
call('r2', 'prog1')
b(None, 'in al,08h', 0xE4, 0x08)
out(0x09, 0x05)
b(None, 'in al,08h', 0xE4, 0x08)
b(None, 'and al,0FEh', 0x24, 0xFE)
movb('r_st')
call(None, 'compte')
movw('r_n1')

# 3. Le master clear efface la commande.
out(0x08, 0x04)
out(0x0D, 0x00)
call(None, 'prog1')
out(0x09, 0x05)
call(None, 'compte')
movw('r_n2')
out(0x08, 0x00)
out(0x0A, 0x00)

# 4. L'AT : la cascade, Clear Mask et la commande du 8237 haut, le temporaire.
at_seulement('fin')
out(0xD4, 0x04)
call(None, 'prog1')
out(0x09, 0x05)
call(None, 'compte')
movw('r_a1')
out(0xDC, 0x00)
call(None, 'compte')
movw('r_a2')
out(0xDE, 0x0E)
out(0xD0, 0x04)
call(None, 'prog1')
out(0x09, 0x05)
call(None, 'compte')
movw('r_b1')
out(0xD0, 0x00)
call(None, 'compte')
movw('r_b2')
out(0xDA, 0x5A)
b(None, 'in al,0DAh', 0xE4, 0xDA)
movb('r_dah')
out(0xD4, 0x00)

# Fin : STI, l'impression.
b('fin', 'sti', 0xFB)
ins(None, 'mov dx,titre', 3, lambda L, a: [0xBA] + w(L['titre']))
b(None, 'mov ah,9', 0xB4, 0x09)
b(None, 'int 21h', 0xCD, 0x21)
ins(None, 'mov si,releves', 3, lambda L, a: [0xBE] + w(L['releves']))
ins(None, 'mov bp,NREL', 3, lambda L, a: [0xBD] + w(L['finrel'] - L['releves']))
b('p0', 'lodsb', 0xAC)
b(None, 'push ax', 0x50)
b(None, 'mov cl,4', 0xB1, 0x04)
b(None, 'shr al,cl', 0xD2, 0xE8)
call(None, 'nib')
b(None, 'pop ax', 0x58)
b(None, 'and al,0Fh', 0x24, 0x0F)
call(None, 'nib')
b(None, "mov dl,' '", 0xB2, 0x20)
b(None, 'mov ah,2', 0xB4, 0x02)
b(None, 'int 21h', 0xCD, 0x21)
b(None, 'dec bp', 0x4D)
jcc(None, 0x75, 'jnz', 'p0')
b(None, 'mov ax,4C00h', 0xB8, 0x00, 0x4C)
b(None, 'int 21h', 0xCD, 0x21)
b('nib', "add al,'0'", 0x04, 0x30)
b(None, "cmp al,'9'", 0x3C, 0x39)
jcc(None, 0x76, 'jbe', 'nibp')
b(None, 'add al,7', 0x04, 0x07)
b('nibp', 'mov dl,al', 0x88, 0xC2)
b(None, 'mov ah,2', 0xB4, 0x02)
b(None, 'int 21h', 0xCD, 0x21)
b(None, 'ret', 0xC3)

# Le canal 1 : masqué, en bloc et en vérification (81h), compte 0003h.
out(0x0A, 0x05, 'prog1')
out(0x0B, 0x81)
out(0x0C, 0x00)
out(0x03, 0x03)
out(0x03, 0x00)
b(None, 'ret', 0xC3)

# Le compte courant du canal 1 dans AX, la bascule remise à zéro d'abord.
b('compte', 'out 0Ch,al', 0xE6, 0x0C)
b(None, 'in al,03h', 0xE4, 0x03)
b(None, 'mov ah,al', 0x88, 0xC4)
b(None, 'in al,03h', 0xE4, 0x03)
b(None, 'xchg al,ah', 0x86, 0xC4)
b(None, 'ret', 0xC3)

# L'octet bas du compte du canal 0, relu après 512 tours de LOOP (une centaine de rafraîchissements à 4,77 MHz) :
# AL = 01h s'il a bougé, 00h sinon.
call('bouge', 'lire0')
b(None, 'mov bl,al', 0x88, 0xC3)
b(None, 'mov cx,0200h', 0xB9, 0x00, 0x02)
b('bg1', 'loop bg1', 0xE2, 0xFE)
call(None, 'lire0')
b(None, 'cmp al,bl', 0x38, 0xD8)
b(None, 'mov al,0', 0xB0, 0x00)
jcc(None, 0x74, 'je', 'bg2')
b(None, 'mov al,1', 0xB0, 0x01)
b('bg2', 'ret', 0xC3)
b('lire0', 'out 0Ch,al', 0xE6, 0x0C)
b(None, 'in al,01h', 0xE4, 0x01)
b(None, 'mov ah,al', 0x88, 0xC4)
b(None, 'in al,01h', 0xE4, 0x01)
b(None, 'mov al,ah', 0x88, 0xE0)
b(None, 'ret', 0xC3)

# Les données. Les relevés, dans l'ordre de l'impression.
ins('titre', "db 'DMABANC $'", 9, lambda L, a: list(b'DMABANC $'))
ins('releves', '', 0, lambda L, a: [])
for nom, n in (('r_cm1', 1), ('r_cm2', 1), ('r_st', 1), ('r_n1', 2), ('r_n2', 2), ('r_a1', 2), ('r_a2', 2),
               ('r_b1', 2), ('r_b2', 2), ('r_dah', 1)):
    ins(nom, f'{nom} db {n} dup (0)', n, lambda L, a, n=n: [0] * n)
ins('finrel', '', 0, lambda L, a: [])
ins('modele', 'modele db 0', 1, lambda L, a: [0])


def assemble():
    labels, a = {}, ORG
    for label, _, size, _ in PROG:
        if label:
            labels[label] = a
        a += size
    out_, listing, a = [], [], ORG
    for label, text, size, enc in PROG:
        bs = enc(labels, a)
        assert len(bs) == size, (text, bs)
        shown = ' '.join(f'{x:02X}' for x in bs[:8]) + (' …' if len(bs) > 8 else '')
        listing.append(f'{a:04X}  {shown:<27} {label + ":" if label else "":<10}{text}')
        out_ += bs
        a += size
    return bytes(out_), listing


def keys(code):
    lines = ['B:', 'DEBUG']
    for i in range(0, len(code), 16):
        lines.append(f'E {ORG + i:X} ' + ' '.join(f'{x:02X}' for x in code[i:i + 16]))
    lines += ['N DMABANC.COM', 'R CX', f'{len(code):X}', 'W', 'Q', 'DMABANC']
    return lines


if __name__ == '__main__':
    code, listing = assemble()
    print('\n'.join(listing))
    print(f'\n{len(code)} octets')
    here = os.path.dirname(os.path.abspath(__file__))
    with open(os.path.join(here, 'dmabanc.keys'), 'w') as f:
        f.write('\n'.join(keys(code)) + '\n')
    if '--com' in sys.argv:
        with open(sys.argv[sys.argv.index('--com') + 1], 'wb') as f:
            f.write(code)
