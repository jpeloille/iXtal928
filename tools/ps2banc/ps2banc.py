#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# PS2BANC.COM — le banc dirigé de la souris PS/2 par le 8042, PS2.0 (PLAN-PS2.md).
#
# Il parle au contrôleur directement (64h commandes, 60h données, D4h « write to mouse »),
# clavier coupé (ADh) pour qu'aucune frappe ne se mêle aux octets de la souris, IRQ 12 coupée
# à l'octet de commande et masquée au second PIC : le BIOS ne consomme rien, le banc lit tout.
# Chaque lecture relève DEUX octets : l'état (64h) puis la donnée (60h), ou EEh si rien n'est
# venu (une commande sans réponse, PB-94, se voit ainsi). Les mouvements viennent de l'hôte,
# injectés par boot-diff --mouse-at pendant les attentes ; le banc les lit en flux (F4h) et à
# distance (EBh). Le même banc sert aux deux souris : avec l'Intellimouse (mouse_type 3), le
# knock F3h C8h F3h 64h F3h 50h fait passer l'identifiant à 03h et les paquets à 4 octets.
# Instructions 8086 seules ; saisi dans DEBUG (disquette supplémentaire de PC-DOS 2.00 en B:),
# écrit sur B:, lancé sous boot-diff.
#
# Usage :
#   python3 tools/ps2banc/ps2banc.py           écrit ps2banc.keys, affiche le listing
#   python3 tools/ps2banc/ps2banc.py --com F   écrit aussi le .COM

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

# op 2 : écrire au 8042 : port (64h ou 60h), octet ; attendre IBF (bit 1 de 64h) à zéro
case(2, 'op2', 'op3')
ins(None, 'mov cx,0FFFFh', 3, lambda L, a: [0xB9, 0xFF, 0xFF])
ins('w2', 'in al,64h', 2, lambda L, a: [0xE4, 0x64])
ins(None, 'test al,2', 2, lambda L, a: [0xA8, 0x02])
ins(None, 'jz w2x', 2, lambda L, a: [0x74, rel8(L, a + 2, 'w2x')])
ins(None, 'loop w2', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'w2')])
ins('w2x', 'lodsw                ; port', 1, lambda L, a: [0xAD])
ins(None, 'mov dx,ax', 2, lambda L, a: [0x89, 0xC2])
ins(None, 'lodsb                ; octet', 1, lambda L, a: [0xAC])
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

# op 4 : lire le 8042 : attendre OBF (bit 0 de 64h), au plus 65 535 lectures ; relever l'état,
#        puis la donnée de 60h — ou EEh si rien n'est venu
case(4, 'op4', 'op5')
ins(None, 'mov cx,0FFFFh', 3, lambda L, a: [0xB9, 0xFF, 0xFF])
ins('w4', 'in al,64h', 2, lambda L, a: [0xE4, 0x64])
ins(None, 'test al,1', 2, lambda L, a: [0xA8, 0x01])
ins(None, 'jnz w4x', 2, lambda L, a: [0x75, rel8(L, a + 2, 'w4x')])
ins(None, 'loop w4', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'w4')])
ins(None, 'stosb                ; état, rien de venu', 1, lambda L, a: [0xAA])
ins(None, 'mov al,0EEh', 2, lambda L, a: [0xB0, 0xEE])
ins(None, 'stosb', 1, lambda L, a: [0xAA])
nxt()
ins('w4x', 'stosb                ; état', 1, lambda L, a: [0xAA])
ins(None, 'in al,60h', 2, lambda L, a: [0xE4, 0x60])
ins(None, 'stosb', 1, lambda L, a: [0xAA])
nxt()

# op 5 : IN AL,port → relevé (1 octet)
case(5, 'op5', 'op6')
ins(None, 'lodsw                ; port', 1, lambda L, a: [0xAD])
ins(None, 'mov dx,ax', 2, lambda L, a: [0x89, 0xC2])
ins(None, 'in al,dx', 1, lambda L, a: [0xEC])
ins(None, 'stosb', 1, lambda L, a: [0xAA])
nxt()

# op 6 : garder l'octet de commande du 8042 (lu par 20h) dans BP
case(6, 'op6', 'done')
ins(None, 'mov al,0ADh', 2, lambda L, a: [0xB0, 0xAD])
ins(None, 'out 64h,al            ; clavier coupé', 2, lambda L, a: [0xE6, 0x64])
ins(None, 'mov al,20h', 2, lambda L, a: [0xB0, 0x20])
ins(None, 'out 64h,al', 2, lambda L, a: [0xE6, 0x64])
ins(None, 'mov cx,0FFFFh', 3, lambda L, a: [0xB9, 0xFF, 0xFF])
ins('w6', 'in al,64h', 2, lambda L, a: [0xE4, 0x64])
ins(None, 'test al,1', 2, lambda L, a: [0xA8, 0x01])
ins(None, 'loopz w6', 2, lambda L, a: [0xE1, rel8(L, a + 2, 'w6')])
ins(None, 'in al,60h', 2, lambda L, a: [0xE4, 0x60])
ins(None, 'stosb                ; octet de commande', 1, lambda L, a: [0xAA])
ins(None, 'mov ah,0', 2, lambda L, a: [0xB4, 0x00])
ins(None, 'mov bp,ax', 2, lambda L, a: [0x89, 0xC5])
nxt()

# op 0 (et tout inconnu) : rendre l'octet de commande (60h, BP), le clavier (AEh), puis
#   afficher les relevés, seize octets par ligne, et sortir
ins('done', 'mov al,60h', 2, lambda L, a: [0xB0, 0x60])
ins(None, 'out 64h,al', 2, lambda L, a: [0xE6, 0x64])
ins(None, 'mov cx,0FFFFh', 3, lambda L, a: [0xB9, 0xFF, 0xFF])
ins('wd', 'in al,64h', 2, lambda L, a: [0xE4, 0x64])
ins(None, 'test al,2', 2, lambda L, a: [0xA8, 0x02])
ins(None, 'loopnz wd', 2, lambda L, a: [0xE0, rel8(L, a + 2, 'wd')])
ins(None, 'mov ax,bp', 2, lambda L, a: [0x89, 0xE8])
ins(None, 'out 60h,al', 2, lambda L, a: [0xE6, 0x60])
ins(None, 'mov cx,0FFFFh', 3, lambda L, a: [0xB9, 0xFF, 0xFF])
ins('wd2', 'in al,64h', 2, lambda L, a: [0xE4, 0x64])
ins(None, 'test al,2', 2, lambda L, a: [0xA8, 0x02])
ins(None, 'loopnz wd2', 2, lambda L, a: [0xE0, rel8(L, a + 2, 'wd2')])
ins(None, 'mov al,0AEh', 2, lambda L, a: [0xB0, 0xAE])
ins(None, 'out 64h,al            ; clavier rendu', 2, lambda L, a: [0xE6, 0x64])
ins(None, 'mov si,results', 3, lambda L, a: [0xBE] + w(L['results']))
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
def KCMD(c): return [2] + w(0x64) + [c]
def KDAT(v): return [2] + w(0x60) + [v]
def MOUSE(*bs):
    out = []
    for b in bs:
        out += KCMD(0xD4) + KDAT(b)
    return out
def WAIT(n): return [3, n]
def READ(n=1): return [4] * n
def INB(port): return [5] + w(port)
SAVE = [6]


S = []
S += WAIT(4)                                          # que la frappe d'Entrée s'éteigne
S += INB(0xA1) + OUTB(0xA1, 0xFF)                     # le second PIC : relevé, puis tout masqué
S += SAVE                                             # clavier coupé, octet de commande gardé
S += KCMD(0x60) + KDAT(0x44)                          # commande : IRQ coupées, horloges actives
S += KCMD(0xA8) + KCMD(0xA9) + READ()                 # port souris activé ; test (00h)
S += MOUSE(0xFF) + READ(3)                            # reset : FAh AAh 00h
S += MOUSE(0xF2) + READ(2)                            # identifiant : FAh 00h
S += MOUSE(0xE8) + READ() + MOUSE(0x02) + READ()      # résolution 2
S += MOUSE(0xF3) + READ() + MOUSE(0x64) + READ()      # 100 échantillons/s
S += MOUSE(0xE7) + READ() + MOUSE(0xE9) + READ(4)     # échelle 2:1 ; état
S += MOUSE(0xE6) + READ()                             # échelle 1:1
S += MOUSE(0xF6) + READ()                             # PB-94 : pas de réponse (EEh)
S += MOUSE(0xF4) + READ()                             # flux activé
for _ in range(6):                                    # les mouvements injectés arrivent ici
    S += WAIT(6) + READ(4)
S += MOUSE(0xEB) + READ(4)                            # lecture à distance
S += MOUSE(0xE9) + READ(4)                            # état (PB-95 si le bouton du milieu)
S += MOUSE(0xF3) + READ() + MOUSE(0xC8) + READ()      # le knock Intellimouse
S += MOUSE(0xF3) + READ() + MOUSE(0x64) + READ()
S += MOUSE(0xF3) + READ() + MOUSE(0x50) + READ()
S += MOUSE(0xF2) + READ(2)                            # identifiant : 03h pour l'Intellimouse
for _ in range(3):
    S += WAIT(6) + READ(5)
S += MOUSE(0xF5) + READ()                             # flux coupé
S += KCMD(0xA7) + INB(0x64)                           # port souris coupé
S += OUTB(0xA1, 0xEF) + [0]                           # second PIC : IRQ 12 seule masquée (BIOS)

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
    lines += ['N PS2BANC.COM', 'R CX', f'{len(code):X}', 'W', 'Q', 'PS2BANC']
    return lines


if __name__ == '__main__':
    code, listing = assemble()
    print('\n'.join(listing))
    print(f'\n{len(code)} octets, script {len(S)} octets')
    here = os.path.dirname(os.path.abspath(__file__))
    with open(os.path.join(here, 'ps2banc.keys'), 'w') as f:
        f.write('\n'.join(keys(code)) + '\n')
    if '--com' in sys.argv:
        with open(sys.argv[sys.argv.index('--com') + 1], 'wb') as f:
            f.write(code)
