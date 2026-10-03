#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# HERCBANC.COM — le banc dirigé de la carte Hercules, G9.1 (PLAN-G9.md).
#
# Il programme la carte comme un logiciel graphique de 1987 : 3BFh (graphique autorisé, seconde
# page ouverte), 3B8h (graphique, page 0 puis 1, vidéo active), les douze registres du 6845 du
# mode 720×348, puis remplit les deux pages de 32 Ko de motifs (B000:0000 et B800:0000), relit
# des octets, échantillonne 3BAh (le bit 7 du retour vertical), et repasse en texte. Le diff
# d'instructions voit les lectures ; la sonde de la carte, les registres, la VRAM et le balayage.
# Instructions 8086 seules ; saisi dans DEBUG (disquette supplémentaire de PC-DOS 2.00 en B:),
# écrit sur B:, lancé sous boot-diff.
#
# Usage :
#   python3 tools/hercbanc/hercbanc.py           écrit hercbanc.keys, affiche le listing
#   python3 tools/hercbanc/hercbanc.py --com F   écrit aussi le .COM

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

# op 6 : N lectures de 3BAh, relevé du OU et du ET des valeurs lues (le bit 7 a-t-il bougé ?)
case(6, 'op6', 'done')
ins(None, 'lodsw                ; N', 1, lambda L, a: [0xAD])
ins(None, 'mov cx,ax', 2, lambda L, a: [0x89, 0xC1])
ins(None, 'mov dx,3BAh', 3, lambda L, a: [0xBA, 0xBA, 0x03])
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
def VSAMPLE(n): return [6] + w(n)
def CRTC(regs):
    out = []
    for i, v in enumerate(regs):
        out += OUTB(0x3B4, i) + OUTB(0x3B5, v)
    return out

GFX = [0x35, 0x2D, 0x2E, 0x07, 0x5B, 0x02, 0x57, 0x57, 0x02, 0x03, 0x00, 0x00]
TXT = [0x61, 0x50, 0x52, 0x0F, 0x19, 0x06, 0x19, 0x19, 0x02, 0x0D, 0x0B, 0x0C]

S = []
S += VSAMPLE(3000)                                   # texte : le bit 7 de 3BAh bouge
S += INB(0x3BA) + PEEK(0xB000, 0x0000)               # la cellule (0,0) de l'écran texte
S += OUTB(0x3BF, 0x03)                               # graphique autorisé, seconde page ouverte
S += OUTB(0x3B8, 0x02)                               # graphique, vidéo coupée pendant la programmation
S += CRTC(GFX)
S += FILL(0xB000, 0x0000, 0x8000, 0x00, 0x01)        # page 0 : une rampe
S += FILL(0xB800, 0x0000, 0x8000, 0xAA, 0x37)        # page 1 : un motif
S += OUTB(0x3B8, 0x0A)                               # graphique, page 0, vidéo active
S += WAIT(4) + VSAMPLE(3000)
S += OUTB(0x3B8, 0x8A)                               # page 1 affichée
S += WAIT(4) + VSAMPLE(3000)
for seg, off in ((0xB000, 0x0000), (0xB000, 0x2001), (0xB000, 0x7FFF), (0xB800, 0x0000), (0xB800, 0x4567), (0xB800, 0x7FFF)):
    S += PEEK(seg, off)
for r in range(12):                                  # le 6845 se relit (PB-97)
    S += OUTB(0x3B4, r) + INB(0x3B5)
S += OUTB(0x3BF, 0x01)                               # seconde page fermée : B800 ne répond plus
S += PEEK(0xB800, 0x0000)
S += OUTB(0x3B8, 0x00) + CRTC(TXT) + OUTB(0x3BF, 0x00) + OUTB(0x3B8, 0x28)   # retour au texte
S += FILL(0xB000, 0x0000, 0x0FA0, 0x20, 0x00)        # écran effacé (espaces ; attributs aussi)
S += FILL(0xB000, 0x0001, 0x0001, 0x07, 0x00)
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
    lines += ['N HERCBANC.COM', 'R CX', f'{len(code):X}', 'W', 'Q', 'HERCBANC']
    return lines


if __name__ == '__main__':
    code, listing = assemble()
    print('\n'.join(listing))
    print(f'\n{len(code)} octets, script {len(S)} octets')
    here = os.path.dirname(os.path.abspath(__file__))
    with open(os.path.join(here, 'hercbanc.keys'), 'w') as f:
        f.write('\n'.join(keys(code)) + '\n')
    if '--com' in sys.argv:
        with open(sys.argv[sys.argv.index('--com') + 1], 'wb') as f:
            f.write(code)
