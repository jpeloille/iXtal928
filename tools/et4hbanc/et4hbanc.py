#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# ET4HBANC.COM — le banc HiColor de la Tseng ET4000AX et de son RAMDAC, GR.1d (PLAN-GR.md).
#
# Par les services HiColor du BIOS de la carte (INT 10h 10F1h, 10F2h, 10F0h ; pas de VESA), il relève le type de
# RAMDAC que la détection du BIOS trouve (10F1h) et la profondeur courante (10F2h). Puis, en 2Eh (640 × 480, 256
# couleurs), il arme le registre de commande par quatre lectures de 3C6h et y écrit FFh (PB-100 (c) : le masque des
# pixels, pas la commande), relit la commande et le masque ; il écrit les codes 40h et C5h (32 bits), 60h et E5h
# (24 bits), E4h (16 bits ; R1-contre K5), chacun relu, 3DAh échantillonné après chacun ; il rend 00h. Il imprime
# ses relevés, attend une touche, puis pose le HiColor par 10F0h (BL = 2Eh, 640 × 480 en 15 bits ; le 1024 × 768
# n'est pas offert, R1-contre A4), remplit les dix banques de 64 Ko, relit 10F2h, imprimé en points, et rend la main
# à DOS dans ce mode. Le diff d'instructions voit les lectures ; la sonde
# SVGA, la VRAM de 1 Mo, la profondeur et le rendu du mode testé.
# L'interprète est celui d'ET4BANC, avec deux ops de plus. Instructions 8086 seules ; saisi dans DEBUG (disquette
# supplémentaire de PC-DOS 2.00 en B:), écrit sur B:, lancé sous boot-diff.
#
# Usage :
#   python3 tools/et4hbanc/et4hbanc.py           écrit et4hbanc.keys, affiche le listing
#   python3 tools/et4hbanc/et4hbanc.py --com F   écrit aussi le .COM

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

# op 9 (GR.1c) : afficher les relevés, attendre une touche (INT 16h), puis continuer le script ; les relevés
# repartent de zéro
case(9, 'op9', 'op10')
ins(None, 'push si', 1, lambda L, a: [0x56])
ins(None, 'call imprime', 3, lambda L, a: [0xE8] + rel16(L, a + 3, 'imprime'))
ins(None, 'pop si', 1, lambda L, a: [0x5E])
ins(None, 'mov di,results', 3, lambda L, a: [0xBF] + w(L['results']))
ins(None, 'xor ah,ah', 2, lambda L, a: [0x30, 0xE4])
ins(None, 'int 16h', 2, lambda L, a: [0xCD, 0x16])
nxt()

# op 10 (GR.1d) : IN AL,port sans relevé (armer l'IPF du RAMDAC sans noyer les relevés)
case(10, 'op10', 'op11')
ins(None, 'lodsw                ; port', 1, lambda L, a: [0xAD])
ins(None, 'mov dx,ax', 2, lambda L, a: [0x89, 0xC2])
ins(None, 'in al,dx', 1, lambda L, a: [0xEC])
nxt()

# op 11 (GR.1d) : INT 10h avec AX et BX donnés → relevé AX puis BX (AL, AH, BL, BH)
case(11, 'op11', 'done')
ins(None, 'lodsw                ; AX', 1, lambda L, a: [0xAD])
ins(None, 'mov dx,ax', 2, lambda L, a: [0x89, 0xC2])
ins(None, 'lodsw                ; BX', 1, lambda L, a: [0xAD])
ins(None, 'mov bx,ax', 2, lambda L, a: [0x89, 0xC3])
ins(None, 'mov ax,dx', 2, lambda L, a: [0x89, 0xD0])
ins(None, 'push si', 1, lambda L, a: [0x56])
ins(None, 'push di', 1, lambda L, a: [0x57])
ins(None, 'int 10h', 2, lambda L, a: [0xCD, 0x10])
ins(None, 'pop di', 1, lambda L, a: [0x5F])
ins(None, 'pop si', 1, lambda L, a: [0x5E])
ins(None, 'cld', 1, lambda L, a: [0xFC])
ins(None, 'stosw                ; AX', 1, lambda L, a: [0xAB])
ins(None, 'mov ax,bx', 2, lambda L, a: [0x89, 0xD8])
ins(None, 'stosw                ; BX', 1, lambda L, a: [0xAB])
nxt()

# op 0 (et tout inconnu) : afficher les relevés, seize octets par ligne, puis sortir
ins('done', 'call imprime', 3, lambda L, a: [0xE8] + rel16(L, a + 3, 'imprime'))
ins(None, 'mov ax,4C00h', 3, lambda L, a: [0xB8, 0x00, 0x4C])
ins(None, 'int 21h', 2, lambda L, a: [0xCD, 0x21])
ins('imprime', 'mov si,results', 3, lambda L, a: [0xBE] + w(L['results']))
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
ins('fin', 'ret', 1, lambda L, a: [0xC3])
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
def PAUSE(): return [9]
def INX(port): return [10] + w(port)
def INT10(ax, bx): return [11] + w(ax) + w(bx)
A = 0xA000
def BANK(v): return OUTB(0x3CD, v)
def ARME(): return INX(0x3C8) + INX(0x3C6) + INX(0x3C6) + INX(0x3C6) + INX(0x3C6)   # l'IPF armé, sans relevé


S = []
S += INT10(0x10F1, 0x0000)                              # le type de RAMDAC que la détection du BIOS trouve
S += INT10(0x10F2, 0x0000)                              # la profondeur courante
S += MODE(0x002E) + BANK(0x00) + FILL(A, 0x0000, 0x8000, 0x00, 0x01)   # 640×480, 256 couleurs
S += ARME() + OUTB(0x3C6, 0xFF)                         # FFh écrit armé (PB-100 (c))
S += ARME() + INB(0x3C6) + INB(0x3C6)                   # la commande, puis le masque
for c in (0x40, 0xC5, 0x60, 0xE5, 0xE4):                # 32, 32 (code 7), 24, 24, 16 bits
    S += ARME() + OUTB(0x3C6, c) + WAIT(1) + SAMPLE(0x3DA, 2000) + ARME() + INB(0x3C6)
S += ARME() + OUTB(0x3C6, 0x00) + INT10(0x10F2, 0x0000)   # rendu à 8 bits ; 10F2h le relit
S += MODE(0x0003)
S += PAUSE()                                            # les relevés, une touche
S += INT10(0x10F0, 0x002E)                              # puis le HiColor : 640×480 en 15 bits
for b in range(10):                                     # 600 Ko, dix banques de 64 Ko
    S += BANK(b << 4 | b) + FILL(A, 0x0000, 0x8000, b * 0x19, 0x01) + FILL(A, 0x8000, 0x8000, 0xF0 - b, 0x07)
S += INT10(0x10F2, 0x0000)                              # la profondeur posée, imprimée en points
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
    lines += ['N ET4HBANC.COM', 'R CX', f'{len(code):X}', 'W', 'Q', 'ET4HBANC']
    return lines


if __name__ == '__main__':
    code, listing = assemble()
    print('\n'.join(listing))
    print(f'\n{len(code)} octets, script {len(S)} octets')
    here = os.path.dirname(os.path.abspath(__file__))
    with open(os.path.join(here, 'et4hbanc.keys'), 'w') as f:
        f.write('\n'.join(keys(code)) + '\n')
    if '--com' in sys.argv:
        with open(sys.argv[sys.argv.index('--com') + 1], 'wb') as f:
            f.write(code)
