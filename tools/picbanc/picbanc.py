#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# PICBANC.COM — le banc dirigé du 8259, G13.4 (PLAN-G13.md) : les cas qui discriminent PB-05, PB-246, PB-247, PB-248
# et PB-255 sur une machine qui tourne, sous DOS, en mode PCem (contre l'oracle, par boot-diff) et en mode matériel
# (contre les attendus de la fiche du 8259A, par le banc en C# seul). Il imprime une ligne, « PICBANC », puis ses
# relevés en hexadécimal :
#   1. ICW1 à ICW4 rejouées (celles du BIOS), OCW1 FFh, un tic du PIT attendu, IN 20h : la lecture après ICW1 (PB-255 ;
#      fiche p. 10, « Status Read is set to IRR ») ; le 8259A 01h (l'IRR), PCem l'ISR.
#   2. Le poll (PB-248 ; p. 16) : l'IRR (OCW3 0Ah), puis OCW3 0Ch et IN 20h, le mot de poll (80h, IR0 demande), puis
#      l'ISR (OCW3 0Bh) : le poll vaut acquittement, IR0 en service (01h) ; fin non spécifique ensuite.
#   3. Dans un gestionnaire d'INT 08h (IR0 en service) : OCW2 40h, « no operation », puis l'ISR (PB-248 : 01h, PCem
#      l'efface) ; puis STI et une attente de plusieurs tics : le nombre d'entrées dans le gestionnaire (PB-247 : une
#      seule, le 8259A retient l'IR0 tant qu'elle est en service ; PCem y rentre à chaque tic).
#   4. L'AT seulement (octet de modèle FCh) : la RTC en périodique (registre A 26h, registre B PIE), l'IRQ 0 et l'IRQ 8
#      en attente
#      sous CLI, puis STI : l'ordre des gestionnaires, « T » pour l'INT 08h, « R » pour l'INT 70h. Le 8259A sert l'IRQ
#      0 avant la cascade (PB-246 ; p. 15, AT TR p. 1-10), et servir l'IRQ 8 ne perd pas l'IRQ 0 (PB-05 ; p. 7) :
#      « TR » ; PCem « R », l'IRQ 0 perdue. Avant lui, l'IRR de l'esclave (01h : l'IRQ 8 attend) et le registre B (42h).
#      À la fin, PIE levé, le registre A laissé à 26h.
#
# Usage :
#   python3 tools/picbanc/picbanc.py           écrit picbanc.keys, affiche le listing
#   python3 tools/picbanc/picbanc.py --com F   écrit aussi le .COM

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


def inp(port, res):
    """IN AL,port puis le relevé res (CS:)."""
    b(None, f'in al,{port:02X}h', 0xE4, port)
    ins(None, f'mov cs:[{res}],al', 4, lambda L, a, r=res: [0x2E, 0xA2] + w(L[r]))


def csmovb(var, val, label=None):
    ins(label, f'mov byte cs:[{var}],{val:02X}h', 6, lambda L, a, v=var, x=val: [0x2E, 0xC6, 0x06] + w(L[v]) + [x])


def ligne_haute(label):
    """Attend que l'IRR (OCW3 0Ah) montre l'IRQ 0 : la sortie du PIT, en mode 3, n'est haute qu'une demi-période, et
    l'IRR suit la ligne (PCem retire la demande à la descente, picintc)."""
    out(0x20, 0x0A, label)
    b(None, 'in al,20h', 0xE4, 0x20)
    b(None, 'test al,1', 0xA8, 0x01)
    jcc(None, 0x74, 'jz', label)


# ---- le programme ------------------------------------------------------------------------
b('start', 'cli', 0xFA)
b(None, 'mov ax,0F000h', 0xB8, 0x00, 0xF0)
b(None, 'mov es,ax', 0x8E, 0xC0)
b(None, 'mov al,es:[0FFFEh]', 0x26, 0xA0, 0xFE, 0xFF)
ins(None, 'mov [modele],al', 3, lambda L, a: [0xA2] + w(L['modele']))
b(None, 'in al,21h', 0xE4, 0x21)
ins(None, 'mov [imr],al', 3, lambda L, a: [0xA2] + w(L['imr']))

# 1. ICW1 à ICW4, celles du BIOS (AT : 11h, 08h, 04h, 01h ; PC et XT : 13h, 08h, 09h), au début d'une demi-période
#    haute du PIT : l'IRQ 0 est encore là quand on lit.
out(0x21, 0xFF)
ligne_haute('sync1')
out(0x20, 0x0B)
ins(None, 'cmp byte [modele],0FCh', 5, lambda L, a: [0x80, 0x3E] + w(L['modele']) + [0xFC])
jcc(None, 0x75, 'jne', 'icwxt')
out(0x20, 0x11)
out(0x21, 0x08)
out(0x21, 0x04)
out(0x21, 0x01)
jcc(None, 0xEB, 'jmp', 'icwfin')
out(0x20, 0x13, 'icwxt')
out(0x21, 0x08)
out(0x21, 0x09)
out(0x21, 0xFF, 'icwfin')
inp(0x20, 'r_icw')

# 2. Le poll, l'IR0 en attente et démasquée (le poll ne voit que les demandes hors de l'IMR), sous CLI.
out(0x21, 0xFE)
ligne_haute('sync2')
b(None, 'mov [r_irr],al', 0xA2, 0, 0)
PROG[-1] = (None, 'mov [r_irr],al', 3, lambda L, a: [0xA2] + w(L['r_irr']))
out(0x20, 0x0C)
inp(0x20, 'r_poll')
out(0x20, 0x0B)
inp(0x20, 'r_isr')
out(0x20, 0x20)
out(0x20, 0x0A)

# 3. Le gestionnaire d'INT 08h : IR0 seule démasquée.
b(None, 'xor ax,ax', 0x31, 0xC0)
b(None, 'mov es,ax', 0x8E, 0xC0)
b(None, 'mov ax,es:[20h]', 0x26, 0xA1, 0x20, 0x00)
ins(None, 'mov [old8],ax', 3, lambda L, a: [0xA3] + w(L['old8']))
b(None, 'mov ax,es:[22h]', 0x26, 0xA1, 0x22, 0x00)
ins(None, 'mov [old8+2],ax', 3, lambda L, a: [0xA3] + w(L['old8'] + 2))
ins(None, 'mov word es:[20h],h8', 7, lambda L, a: [0x26, 0xC7, 0x06, 0x20, 0x00] + w(L['h8']))
b(None, 'mov ax,cs', 0x8C, 0xC8)
b(None, 'mov es:[22h],ax', 0x26, 0xA3, 0x22, 0x00)
out(0x21, 0xFE)
b(None, 'sti', 0xFB)
ins('att3', 'cmp byte [fini],0', 5, lambda L, a: [0x80, 0x3E] + w(L['fini']) + [0x00])
jcc(None, 0x74, 'je', 'att3')
b(None, 'cli', 0xFA)
ins(None, 'mov ax,[old8]', 3, lambda L, a: [0xA1] + w(L['old8']))
b(None, 'mov es:[20h],ax', 0x26, 0xA3, 0x20, 0x00)
ins(None, 'mov ax,[old8+2]', 3, lambda L, a: [0xA1] + w(L['old8'] + 2))
b(None, 'mov es:[22h],ax', 0x26, 0xA3, 0x22, 0x00)

# 4. L'AT : l'IRQ 0 et l'IRQ 8 ensemble.
ins(None, 'cmp byte [modele],0FCh', 5, lambda L, a: [0x80, 0x3E] + w(L['modele']) + [0xFC])
jcc(None, 0x74, 'je', 'at4')
ins(None, 'jmp fin4', 3, lambda L, a: [0xE9] + w(L['fin4'] - (a + 3)))
b('at4', 'mov ax,es:[1C0h]', 0x26, 0xA1, 0xC0, 0x01)
ins(None, 'mov [old70],ax', 3, lambda L, a: [0xA3] + w(L['old70']))
b(None, 'mov ax,es:[1C2h]', 0x26, 0xA1, 0xC2, 0x01)
ins(None, 'mov [old70+2],ax', 3, lambda L, a: [0xA3] + w(L['old70'] + 2))
ins(None, 'mov word es:[20h],t8', 7, lambda L, a: [0x26, 0xC7, 0x06, 0x20, 0x00] + w(L['t8']))
b(None, 'mov ax,cs', 0x8C, 0xC8)
b(None, 'mov es:[22h],ax', 0x26, 0xA3, 0x22, 0x00)
ins(None, 'mov word es:[1C0h],t70', 7, lambda L, a: [0x26, 0xC7, 0x06, 0xC0, 0x01] + w(L['t70']))
b(None, 'mov es:[1C2h],ax', 0x26, 0xA3, 0xC2, 0x01)
b(None, 'in al,0A1h', 0xE4, 0xA1)
ins(None, 'mov [imr2],al', 3, lambda L, a: [0xA2] + w(L['imr2']))
out(0x70, 0x0A)                                          # le registre A : 32 768 Hz, 1 024 interruptions par
out(0x71, 0x26)                                          # seconde (la valeur du BIOS ; l'écrire arme la RTC)
out(0x70, 0x0B)
b(None, 'in al,71h', 0xE4, 0x71)
ins(None, 'mov [regb],al', 3, lambda L, a: [0xA2] + w(L['regb']))
b(None, 'or al,40h', 0x0C, 0x40)
b(None, 'mov ah,al', 0x88, 0xC4)
out(0x70, 0x0B)
b(None, 'mov al,ah', 0x88, 0xE0)
b(None, 'out 71h,al', 0xE6, 0x71)
out(0xA1, 0xFE)
out(0x21, 0xFA)
call(None, 'attente')
ligne_haute('sync4')
out(0xA0, 0x0A)
inp(0xA0, 'r_irr2')
out(0x70, 0x0B)
b(None, 'in al,71h', 0xE4, 0x71)
ins(None, 'mov [r_regb],al', 3, lambda L, a: [0xA2] + w(L['r_regb']))
b(None, 'sti', 0xFB)
b(None, 'mov cx,0800h', 0xB9, 0x00, 0x08)
b('d4', 'loop d4', 0xE2, 0xFE)
b(None, 'cli', 0xFA)
out(0x70, 0x0B)
ins(None, 'mov al,[regb]', 3, lambda L, a: [0xA0] + w(L['regb']))
b(None, 'and al,0BFh', 0x24, 0xBF)
b(None, 'out 71h,al', 0xE6, 0x71)
out(0x70, 0x0C)
b(None, 'in al,71h', 0xE4, 0x71)
ins(None, 'mov al,[imr2]', 3, lambda L, a: [0xA0] + w(L['imr2']))
b(None, 'out 0A1h,al', 0xE6, 0xA1)
ins(None, 'mov ax,[old8]', 3, lambda L, a: [0xA1] + w(L['old8']))
b(None, 'mov es:[20h],ax', 0x26, 0xA3, 0x20, 0x00)
ins(None, 'mov ax,[old8+2]', 3, lambda L, a: [0xA1] + w(L['old8'] + 2))
b(None, 'mov es:[22h],ax', 0x26, 0xA3, 0x22, 0x00)
ins(None, 'mov ax,[old70]', 3, lambda L, a: [0xA1] + w(L['old70']))
b(None, 'mov es:[1C0h],ax', 0x26, 0xA3, 0xC0, 0x01)
ins(None, 'mov ax,[old70+2]', 3, lambda L, a: [0xA1] + w(L['old70'] + 2))
b(None, 'mov es:[1C2h],ax', 0x26, 0xA3, 0xC2, 0x01)

# Fin : le masque d'avant, STI, l'impression.
ins('fin4', 'mov al,[imr]', 3, lambda L, a: [0xA0] + w(L['imr']))
b(None, 'out 21h,al', 0xE6, 0x21)
b(None, 'sti', 0xFB)
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

# L'attente : 4 × 65 536 tours de LOOP, plus d'un tic du PIT sur toutes les machines du banc.
b('attente', 'mov bx,4', 0xBB, 0x04, 0x00)
b('att0', 'xor cx,cx', 0x31, 0xC9)
b('att1', 'loop att1', 0xE2, 0xFE)
b(None, 'dec bx', 0x4B)
jcc(None, 0x75, 'jnz', 'att0')
b(None, 'ret', 0xC3)

# Le gestionnaire du 3 : IR0 en service. Une entrée imbriquée (PCem) ressort sans EOI ; après la mesure, une entrée de
# plus (le tic retenu par le 8259A) fait sa fin d'interruption.
b('h8', 'push ax', 0x50)
b(None, 'push bx', 0x53)
b(None, 'push cx', 0x51)
ins(None, 'cmp byte cs:[fini],0', 6, lambda L, a: [0x2E, 0x80, 0x3E] + w(L['fini']) + [0x00])
jcc(None, 0x75, 'jne', 'h8eoi')
ins(None, 'inc byte cs:[r_entrees]', 5, lambda L, a: [0x2E, 0xFE, 0x06] + w(L['r_entrees']))
ins(None, 'cmp byte cs:[dedans],0', 6, lambda L, a: [0x2E, 0x80, 0x3E] + w(L['dedans']) + [0x00])
jcc(None, 0x75, 'jne', 'h8sort')
csmovb('dedans', 1)
out(0x20, 0x40)
out(0x20, 0x0B)
inp(0x20, 'r_noop')
out(0x20, 0x0A)
b(None, 'sti', 0xFB)
call(None, 'attente')
b(None, 'cli', 0xFA)
out(0x20, 0x20)
csmovb('fini', 1)
jcc(None, 0xEB, 'jmp', 'h8sort')
out(0x20, 0x20, 'h8eoi')
b('h8sort', 'pop cx', 0x59)
b(None, 'pop bx', 0x5B)
b(None, 'pop ax', 0x58)
b(None, 'iret', 0xCF)

# Les gestionnaires du 4 : « T » puis le BIOS ; « R », la RTC remise hors périodique (une seule IRQ 8 : PIE levé, que
# le BIOS de l'AT laisse posé, 42h), le registre C lu, les deux EOI. Quatre marques au plus.
def marque(lettre, sortie):
    ins(None, 'mov bx,cs:[ordrep]', 5, lambda L, a: [0x2E, 0x8B, 0x1E] + w(L['ordrep']))
    ins(None, 'cmp bx,ordre+4', 4, lambda L, a: [0x81, 0xFB] + w(L['ordre'] + 4))
    jcc(None, 0x73, 'jae', sortie)
    b(None, f"mov byte cs:[bx],'{lettre}'", 0x2E, 0xC6, 0x07, ord(lettre))
    ins(None, 'inc word cs:[ordrep]', 5, lambda L, a: [0x2E, 0xFF, 0x06] + w(L['ordrep']))


b('t8', 'push bx', 0x53)
marque('T', 't8s')
b('t8s', 'pop bx', 0x5B)
ins(None, 'jmp far cs:[old8]', 5, lambda L, a: [0x2E, 0xFF, 0x2E] + w(L['old8']))
b('t70', 'push ax', 0x50)
b(None, 'push bx', 0x53)
marque('R', 't70s')
out(0x70, 0x0B, 't70s')
ins(None, 'mov al,cs:[regb]', 4, lambda L, a: [0x2E, 0xA0] + w(L['regb']))
b(None, 'and al,0BFh', 0x24, 0xBF)
b(None, 'out 71h,al', 0xE6, 0x71)
out(0x70, 0x0C)
b(None, 'in al,71h', 0xE4, 0x71)
out(0xA0, 0x20)
out(0x20, 0x20)
b(None, 'pop bx', 0x5B)
b(None, 'pop ax', 0x58)
b(None, 'iret', 0xCF)

# Les données. Les relevés, dans l'ordre de l'impression ; l'ordre du 4, quatre octets (00h : rien).
ins('titre', "db 'PICBANC $'", 9, lambda L, a: list(b'PICBANC $'))
ins('releves', 'r_icw', 0, lambda L, a: [])
for nom in ('r_icw', 'r_irr', 'r_poll', 'r_isr', 'r_noop', 'r_entrees', 'r_irr2', 'r_regb'):
    ins(nom, f'{nom} db 0', 1, lambda L, a: [0])
ins('ordre', 'ordre db 4 dup (0)', 4, lambda L, a: [0, 0, 0, 0])
ins('finrel', '', 0, lambda L, a: [])
ins('ordrep', 'ordrep dw ordre', 2, lambda L, a: w(L['ordre']))
for nom, n in (('modele', 1), ('imr', 1), ('imr2', 1), ('regb', 1), ('dedans', 1), ('fini', 1), ('old8', 4),
               ('old70', 4)):
    ins(nom, f'{nom} db {n} dup (0)', n, lambda L, a, n=n: [0] * n)


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
    lines += ['N PICBANC.COM', 'R CX', f'{len(code):X}', 'W', 'Q', 'PICBANC']
    return lines


if __name__ == '__main__':
    code, listing = assemble()
    print('\n'.join(listing))
    print(f'\n{len(code)} octets')
    here = os.path.dirname(os.path.abspath(__file__))
    with open(os.path.join(here, 'picbanc.keys'), 'w') as f:
        f.write('\n'.join(keys(code)) + '\n')
    if '--com' in sys.argv:
        with open(sys.argv[sys.argv.index('--com') + 1], 'wb') as f:
            f.write(code)
