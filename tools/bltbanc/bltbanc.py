#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# BLTBANC.COM — le banc dirigé du blitter de la GD5429, G7.2 (PLAN-G7.md).
#
# Le blitter (gd5429_start_blit, vid_cl5429.c:1302-1599) n'est atteint par aucun BIOS : il faut
# un programme qui le programme. BLTBANC.COM est un petit interprète, sur le patron d'IDECHK
# (G5.2) : une table d'opérations — écrire un registre par OUT DX,AX, appeler l'INT 10h,
# remplir la VRAM par la fenêtre A000 à travers une banque, alimenter un BitBLT « source
# système », attendre la fin du BitBLT, relire la VRAM en somme tournante, écrire en MMIO
# (B8000), relire un registre. Saisi dans DEBUG.EXE par KeyScript, écrit sur C:, lancé — sous
# boot-diff, qui compare chaque instruction, les registres, et la sonde VGA (VRAM entière,
# registres de la carte, framebuffer) à la fin. Les relevés s'affichent en hexadécimal, pour
# les yeux : la preuve est le diff.
#
# Ce qu'il exerce, en 640 × 480 × 256 (mode 5Fh du BIOS Cirrus), pas de 640 :
#   copie avant et arrière ; motif 8 × 8 (0x40) ; expansion de couleur (0x80), transparente
#   (0x88) ; motif en expansion (0xC0) ; les seize ROP du switch (vid_cl5429.c:1466-1526) ;
#   le masque de début de ligne (GR2F) ; 16 bpp (bit 4 du mode : copie, motif, expansion) ;
#   source système (0x04), copie et expansion, alimentée par des mots à la fenêtre A000 ;
#   et un BitBLT entier programmé en MMIO (SR17 bit 2, B8000-B80FF).
#
# Usage :
#   python3 tools/bltbanc/bltbanc.py            écrit bltbanc.keys, affiche le listing
#   python3 tools/bltbanc/bltbanc.py --com F    écrit aussi le .COM (objdump -D -b binary
#                                               -mi8086 --adjust-vma=0x100 F)

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
# SI = pointeur de script, DI = pointeur de relevés ; ES = CS sauf pendant un accès VRAM.
ins('start', 'mov si,script', 3, lambda L, a: [0xBE] + w(L['script']))
ins(None, 'mov di,results', 3, lambda L, a: [0xBF] + w(L['results']))
ins(None, 'cld', 1, lambda L, a: [0xFC])
ins('next', 'lodsb', 1, lambda L, a: [0xAC])

# op 1 : OUT port,AX (AL = index, AH = valeur)
case(1, None, 'op2')
ins(None, 'lodsw                ; port', 1, lambda L, a: [0xAD])
ins(None, 'mov dx,ax', 2, lambda L, a: [0x89, 0xC2])
ins(None, 'lodsw                ; index, valeur', 1, lambda L, a: [0xAD])
ins(None, 'out dx,ax', 1, lambda L, a: [0xEF])
nxt()

# op 2 : INT 10h, AX et BX du script
case(2, 'op2', 'op3')
ins(None, 'lodsw', 1, lambda L, a: [0xAD])
ins(None, 'push ax', 1, lambda L, a: [0x50])
ins(None, 'lodsw', 1, lambda L, a: [0xAD])
ins(None, 'mov bx,ax', 2, lambda L, a: [0x89, 0xC3])
ins(None, 'pop ax', 1, lambda L, a: [0x58])
ins(None, 'push si', 1, lambda L, a: [0x56])
ins(None, 'push di', 1, lambda L, a: [0x57])
ins(None, 'int 10h', 2, lambda L, a: [0xCD, 0x10])
ins(None, 'pop di', 1, lambda L, a: [0x5F])
ins(None, 'pop si', 1, lambda L, a: [0x5E])
ins(None, 'push cs', 1, lambda L, a: [0x0E])
ins(None, 'pop es', 1, lambda L, a: [0x07])
nxt()

# op 3 : banque (GR9), puis remplir N mots en A000:0, graine, + pas par mot
# op 5 : idem SANS toucher la banque — l'alimentation d'un BitBLT « source système »
case(3, 'op3', 'op5')
ins(None, 'lodsb                ; banque', 1, lambda L, a: [0xAC])
ins(None, 'mov ah,al', 2, lambda L, a: [0x88, 0xC4])
ins(None, 'mov al,9', 2, lambda L, a: [0xB0, 0x09])
ins(None, 'mov dx,3CEh', 3, lambda L, a: [0xBA, 0xCE, 0x03])
ins(None, 'out dx,ax', 1, lambda L, a: [0xEF])
ins(None, 'jmp fill', 2, lambda L, a: [0xEB, rel8(L, a + 2, 'fill')])
case(5, 'op5', 'op4')
ins('fill', 'lodsw                ; mots', 1, lambda L, a: [0xAD])
ins(None, 'mov cx,ax', 2, lambda L, a: [0x89, 0xC1])
ins(None, 'lodsw                ; graine', 1, lambda L, a: [0xAD])
ins(None, 'push ax', 1, lambda L, a: [0x50])
ins(None, 'lodsw                ; pas', 1, lambda L, a: [0xAD])
ins(None, 'mov bx,ax', 2, lambda L, a: [0x89, 0xC3])
ins(None, 'mov ax,0A000h', 3, lambda L, a: [0xB8, 0x00, 0xA0])
ins(None, 'mov es,ax', 2, lambda L, a: [0x8E, 0xC0])
ins(None, 'pop ax', 1, lambda L, a: [0x58])
ins(None, 'push di', 1, lambda L, a: [0x57])
ins(None, 'xor di,di', 2, lambda L, a: [0x31, 0xFF])
ins('f3', 'stosw', 1, lambda L, a: [0xAB])
ins(None, 'add ax,bx', 2, lambda L, a: [0x01, 0xD8])
ins(None, 'loop f3', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'f3')])
ins(None, 'pop di', 1, lambda L, a: [0x5F])
ins(None, 'push cs', 1, lambda L, a: [0x0E])
ins(None, 'pop es', 1, lambda L, a: [0x07])
nxt()

# op 4 : attendre la fin du BitBLT (GR31 bit 0), au plus 65 535 lectures ; relevé : GR31
case(4, 'op4', 'op6')
ins(None, 'mov dx,3CEh', 3, lambda L, a: [0xBA, 0xCE, 0x03])
ins(None, 'mov al,31h', 2, lambda L, a: [0xB0, 0x31])
ins(None, 'out dx,al', 1, lambda L, a: [0xEE])
ins(None, 'inc dx', 1, lambda L, a: [0x42])
ins(None, 'mov cx,0FFFFh', 3, lambda L, a: [0xB9, 0xFF, 0xFF])
ins('w4', 'in al,dx', 1, lambda L, a: [0xEC])
ins(None, 'test al,1', 2, lambda L, a: [0xA8, 0x01])
ins(None, 'jz w4x', 2, lambda L, a: [0x74, rel8(L, a + 2, 'w4x')])
ins(None, 'loop w4', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'w4')])
ins('w4x', 'stosb', 1, lambda L, a: [0xAA])
nxt()

# op 6 : banque, puis somme tournante de N mots lus en A000:0 → relevé (2 octets)
case(6, 'op6', 'op7')
ins(None, 'lodsb                ; banque', 1, lambda L, a: [0xAC])
ins(None, 'mov ah,al', 2, lambda L, a: [0x88, 0xC4])
ins(None, 'mov al,9', 2, lambda L, a: [0xB0, 0x09])
ins(None, 'mov dx,3CEh', 3, lambda L, a: [0xBA, 0xCE, 0x03])
ins(None, 'out dx,ax', 1, lambda L, a: [0xEF])
ins(None, 'lodsw                ; mots', 1, lambda L, a: [0xAD])
ins(None, 'mov cx,ax', 2, lambda L, a: [0x89, 0xC1])
ins(None, 'mov ax,0A000h', 3, lambda L, a: [0xB8, 0x00, 0xA0])
ins(None, 'mov es,ax', 2, lambda L, a: [0x8E, 0xC0])
ins(None, 'xor bx,bx', 2, lambda L, a: [0x31, 0xDB])
ins(None, 'xor dx,dx', 2, lambda L, a: [0x31, 0xD2])
ins('s6', 'mov ax,es:[bx]', 3, lambda L, a: [0x26, 0x8B, 0x07])
ins(None, 'rol dx,1', 2, lambda L, a: [0xD1, 0xC2])
ins(None, 'add dx,ax', 2, lambda L, a: [0x01, 0xC2])
ins(None, 'add bx,2', 3, lambda L, a: [0x83, 0xC3, 0x02])
ins(None, 'loop s6', 2, lambda L, a: [0xE2, rel8(L, a + 2, 's6')])
ins(None, 'push cs', 1, lambda L, a: [0x0E])
ins(None, 'pop es', 1, lambda L, a: [0x07])
ins(None, 'mov ax,dx', 2, lambda L, a: [0x89, 0xD0])
ins(None, 'stosw', 1, lambda L, a: [0xAB])
nxt()

# op 7 : écrire un octet en MMIO, B800:déplacement
case(7, 'op7', 'op8')
ins(None, 'lodsw                ; déplacement', 1, lambda L, a: [0xAD])
ins(None, 'mov bx,ax', 2, lambda L, a: [0x89, 0xC3])
ins(None, 'mov ax,0B800h', 3, lambda L, a: [0xB8, 0x00, 0xB8])
ins(None, 'mov es,ax', 2, lambda L, a: [0x8E, 0xC0])
ins(None, 'lodsb                ; valeur', 1, lambda L, a: [0xAC])
ins(None, 'mov es:[bx],al', 3, lambda L, a: [0x26, 0x88, 0x07])
ins(None, 'push cs', 1, lambda L, a: [0x0E])
ins(None, 'pop es', 1, lambda L, a: [0x07])
nxt()

# op 8 : relever un registre indexé : OUT port,index ; IN port+1 → relevé (1 octet)
case(8, 'op8', 'op9')
ins(None, 'lodsw                ; port', 1, lambda L, a: [0xAD])
ins(None, 'mov dx,ax', 2, lambda L, a: [0x89, 0xC2])
ins(None, 'lodsb                ; index', 1, lambda L, a: [0xAC])
ins(None, 'out dx,al', 1, lambda L, a: [0xEE])
ins(None, 'inc dx', 1, lambda L, a: [0x42])
ins(None, 'in al,dx', 1, lambda L, a: [0xEC])
ins(None, 'stosb', 1, lambda L, a: [0xAA])
nxt()

# op 9 : lire-modifier-écrire un registre indexé : (x AND et) OR ou
case(9, 'op9', 'op10')
ins(None, 'lodsw                ; port', 1, lambda L, a: [0xAD])
ins(None, 'mov dx,ax', 2, lambda L, a: [0x89, 0xC2])
ins(None, 'lodsb                ; index', 1, lambda L, a: [0xAC])
ins(None, 'out dx,al', 1, lambda L, a: [0xEE])
ins(None, 'inc dx', 1, lambda L, a: [0x42])
ins(None, 'lodsw                ; et, ou', 1, lambda L, a: [0xAD])
ins(None, 'mov bx,ax', 2, lambda L, a: [0x89, 0xC3])
ins(None, 'in al,dx', 1, lambda L, a: [0xEC])
ins(None, 'and al,bl', 2, lambda L, a: [0x20, 0xD8])
ins(None, 'or al,bh', 2, lambda L, a: [0x08, 0xF8])
ins(None, 'out dx,al', 1, lambda L, a: [0xEE])
nxt()

# op 10 : un BitBLT par GR — dix-sept valeurs dans l'ordre de grtab, puis GR31 = 02 (départ)
case(10, 'op10', 'done')
ins(None, 'mov dx,3CEh', 3, lambda L, a: [0xBA, 0xCE, 0x03])
ins(None, 'mov bx,grtab', 3, lambda L, a: [0xBB] + w(L['grtab']))
ins(None, 'mov cx,17', 3, lambda L, a: [0xB9, 17, 0])
ins('b10', 'lodsb', 1, lambda L, a: [0xAC])
ins(None, 'mov ah,al', 2, lambda L, a: [0x88, 0xC4])
ins(None, 'mov al,[bx]', 2, lambda L, a: [0x8A, 0x07])
ins(None, 'out dx,ax', 1, lambda L, a: [0xEF])
ins(None, 'inc bx', 1, lambda L, a: [0x43])
ins(None, 'loop b10', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'b10')])
ins(None, 'mov ax,0231h', 3, lambda L, a: [0xB8, 0x31, 0x02])
ins(None, 'out dx,ax', 1, lambda L, a: [0xEF])
nxt()

# op 0 (et tout inconnu) : afficher les relevés, seize octets par ligne, puis sortir
ins('done', 'mov si,results', 3, lambda L, a: [0xBE] + w(L['results']))
ins(None, 'xor bp,bp', 2, lambda L, a: [0x31, 0xED])
ins('p0', 'cmp si,di', 2, lambda L, a: [0x39, 0xFE])
ins(None, 'jae fin', 2, lambda L, a: [0x73, rel8(L, a + 2, 'fin')])
ins(None, 'lodsb', 1, lambda L, a: [0xAC])
ins(None, 'push ax', 1, lambda L, a: [0x50])
ins(None, 'shr al,4', 3, lambda L, a: [0xC0, 0xE8, 0x04])
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
def OUTW(port, idx, val): return [1] + w(port) + [idx, val]
def INT10(ax, bx=0): return [2] + w(ax) + w(bx)
def FILL(bank, n, seed, step): return [3, bank] + w(n) + w(seed) + w(step)
WAIT = [4]
def FEED(n, seed, step): return [5] + w(n) + w(seed) + w(step)
def SUM(bank, n): return [6, bank] + w(n)
def MMIO(off, val): return [7] + w(off) + [val]
def RDREG(port, idx): return [8] + w(port) + [idx]
def RMW(port, idx, et, ou): return [9] + w(port) + [idx, et, ou]


def GR(idx, val): return OUTW(0x3CE, idx, val)
def SR(idx, val): return OUTW(0x3C4, idx, val)


PITCH = 640

# Les registres du BitBLT par GR (vid_cl5429.c:279-336) : chaque GR passe par
# gd5429_mmio_write, comme son octet MMIO en B8000 + (déplacement de la table ci-dessous).
BLT = [  # (GR, déplacement MMIO)
    ('w0', 0x20, 0x08), ('w1', 0x21, 0x09), ('h0', 0x22, 0x0A), ('h1', 0x23, 0x0B),
    ('dp0', 0x24, 0x0C), ('dp1', 0x25, 0x0D), ('sp0', 0x26, 0x0E), ('sp1', 0x27, 0x0F),
    ('d0', 0x28, 0x10), ('d1', 0x29, 0x11), ('d2', 0x2A, 0x12),
    ('s0', 0x2C, 0x14), ('s1', 0x2D, 0x15), ('s2', 0x2E, 0x16),
    ('mask', 0x2F, 0x17), ('mode', 0x30, 0x18), ('rop', 0x32, 0x1A),
]


def blit(w_bytes, h, dst, src, mode, rop=0x0D, mask=0, dpitch=PITCH, spitch=PITCH, mmio=False):
    v = {'w0': (w_bytes - 1) & 0xFF, 'w1': (w_bytes - 1) >> 8, 'h0': (h - 1) & 0xFF,
         'h1': (h - 1) >> 8, 'dp0': dpitch & 0xFF, 'dp1': dpitch >> 8,
         'sp0': spitch & 0xFF, 'sp1': spitch >> 8,
         'd0': dst & 0xFF, 'd1': (dst >> 8) & 0xFF, 'd2': dst >> 16,
         's0': src & 0xFF, 's1': (src >> 8) & 0xFF, 's2': src >> 16,
         'mask': mask, 'mode': mode, 'rop': rop}
    if not mmio:
        return [10] + [v[k] for k, _, _ in BLT]
    out = []
    for k, _, off in BLT:
        out += MMIO(off, v[k])
    return out + MMIO(0x40, 0x02)


def colours(fg, bg):
    # GR1 / GR0 : octet bas de fg / bg ; GR11 / GR10 : octet haut (16 bpp).
    return GR(0x01, fg & 0xFF) + GR(0x11, fg >> 8) + GR(0x00, bg & 0xFF) + GR(0x10, bg >> 8)


def at(x, y, base=0):
    return base + y * PITCH + x


ROPS = [0x00, 0x05, 0x06, 0x09, 0x0B, 0x0D, 0x0E, 0x50, 0x59, 0x6D, 0x90, 0x95, 0xAD, 0xD0, 0xD6, 0xDA]

S = []
S += INT10(0x005F)                                   # 640 × 480 × 256, BIOS Cirrus
S += SR(0x06, 0x12)                                  # extensions déverrouillées
S += GR(0x0B, 0x00)                                  # une banque, granularité 4 Ko
S += RDREG(0x3C4, 0x06) + RDREG(0x3CE, 0x0B) + RDREG(0x3C4, 0x07)
# -- la VRAM : 0-64 Ko (lignes 0-102) et 64-128 Ko (102-204) remplis de motifs ---------------
S += FILL(0x00, 0x8000, 0x0100, 0x0203)
S += FILL(0x10, 0x8000, 0x8001, 0x0507)
# -- 8 bpp : copie avant, arrière ; motif ; expansion, transparente ; motif en expansion -----
S += blit(100, 50, at(0, 210), at(20, 10), 0x00) + WAIT
S += blit(100, 50, at(149, 300), at(119, 59), 0x01) + WAIT          # arrière : derniers octets
S += blit(200, 32, at(300, 210), 0x10000 + 64 * 5, 0x40) + WAIT     # motif 8 × 8
S += colours(0x0F, 0x20)
S += blit(128, 16, at(0, 270), 0x0400, 0x80) + WAIT                 # expansion
S += blit(128, 16, at(200, 270), 0x0440, 0x88) + WAIT               # expansion transparente
S += blit(64, 24, at(400, 270), 0x0800, 0xC0) + WAIT                # motif en expansion
for k, rop in enumerate(ROPS):
    S += blit(32, 8, at(40 * k, 310), at(8 * k, 30), 0x00, rop=rop) + WAIT
S += blit(64, 8, at(0, 320), at(0, 40), 0x00, mask=3) + WAIT        # masque de début de ligne
# -- 16 bpp (bit 4 du mode) : copie, motif (128 octets), expansion -----------------------------
S += colours(0x1234, 0xABCD)
S += blit(200, 20, at(0, 340), at(50, 70), 0x10) + WAIT
S += blit(160, 16, at(220, 340), 0x10000 + 128 * 3, 0x50) + WAIT
S += blit(128, 16, at(400, 340), 0x0C00, 0x90) + WAIT
# -- source système : copie 8 bpp (32 octets × 8 : 8 mots doubles par ligne), puis
#    expansion (64 points × 8 : 2 mots doubles par ligne) ; alimentation par la fenêtre A000 --
S += GR(0x09, 0x00)
S += blit(32, 8, at(0, 380), 0, 0x04) + FEED(2 * 8 * 8, 0x1357, 0x0F0F) + WAIT
S += colours(0x0C, 0x01)
S += blit(64, 8, at(100, 380), 0, 0x84) + FEED(2 * 2 * 8, 0xA5C3, 0x3311) + WAIT
# -- le même BitBLT en MMIO : SR17 bit 2 ouvre B8000-B80FF ---------------------------------------
S += RMW(0x3C4, 0x17, 0xFF, 0x04) + RDREG(0x3C4, 0x17)
S += blit(120, 40, at(300, 400), at(0, 0), 0x00, rop=0x59, mmio=True) + WAIT
S += RMW(0x3C4, 0x17, 0xFB, 0x00)
# -- relevés : sommes de la VRAM touchée (lignes 204-460, banques 0x20-0x47) -------------------
for bank in range(0x20, 0x48, 0x08):
    S += SUM(bank, 0x4000)
S += SUM(0x00, 0x8000) + SUM(0x10, 0x8000)
S += GR(0x09, 0x00)
S += [0]

ins('script', 'db script', len(S), lambda L, a, S=S: list(S))
ins('crlf', "db 13,10,'$'", 3, lambda L, a: [13, 10, 0x24])
ins('grtab', 'db GR20…GR32', len(BLT), lambda L, a: [gr for _, gr, _ in BLT])
# `results` : la fin du programme ; la zone n'est pas écrite par DEBUG.
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
    lines = ['DEBUG']
    for i in range(0, len(code), 16):
        lines.append(f'E {ORG + i:X} ' + ' '.join(f'{b:02X}' for b in code[i:i + 16]))
    lines += ['N BLTBANC.COM', 'R CX', f'{len(code):X}', 'W', 'Q', 'BLTBANC']
    return lines


if __name__ == '__main__':
    code, listing = assemble()
    print('\n'.join(listing))
    print(f'\n{len(code)} octets, script {len(S)} octets')
    here = os.path.dirname(os.path.abspath(__file__))
    with open(os.path.join(here, 'bltbanc.keys'), 'w') as f:
        f.write('\n'.join(keys(code)) + '\n')
    if '--com' in sys.argv:
        with open(sys.argv[sys.argv.index('--com') + 1], 'wb') as f:
            f.write(code)
