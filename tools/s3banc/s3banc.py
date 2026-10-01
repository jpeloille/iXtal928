#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# S3BANC.COM — le banc dirigé de l'accélérateur de la Trio64, G7.3 (PLAN-G7.md).
#
# L'accélérateur 2D de la S3 (s3_accel_start, vid_s3.c:1777-2637), rendu SYNCHRONE des deux
# côtés (décision n° 3), n'est atteint par aucun BIOS. S3BANC.COM est un petit interprète, sur
# le patron de BLTBANC (G7.2) : écrire un port 16 bits, appeler l'INT 10h, remplir la VRAM par
# une banque (CR6A), attendre la fin d'une commande (GP_STAT 9AE8 bit 9), alimenter le port de
# transfert E2E8, relire la VRAM en somme tournante, écrire un mot en MMIO (A000:port), relire
# un registre indexé ou un port 16 bits. Saisi dans DEBUG.EXE par KeyScript, écrit sur C:,
# lancé — sous boot-diff, qui compare chaque instruction et la sonde VGA (VRAM, registres de la
# carte et de l'accélérateur, framebuffer). La preuve est le diff.
#
# Ce qu'il exerce, en 640 × 480 × 256 (VESA 101h), pas de 640 : rectangle plein sous les seize
# mélanges ; BitBLT avant et arrière ; motif 8 × 8 ; lignes radiales dans les huit directions ;
# données du CPU par E2E8, en couleur puis en expansion monochrome ; sélection par la mémoire
# d'affichage ; ciseaux ; un rectangle entier programmé en MMIO (CR53 bit 4, A000:82E8…).
#
# Usage :
#   python3 tools/s3banc/s3banc.py            écrit s3banc.keys, affiche le listing
#   python3 tools/s3banc/s3banc.py --com F    écrit aussi le .COM (objdump -D -b binary
#                                             -mi8086 --adjust-vma=0x100 F)

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

# op 1 : OUT port,AX (un mot : registre 16 bits, ou index + valeur en 3D4)
case(1, None, 'op2')
ins(None, 'lodsw                ; port', 1, lambda L, a: [0xAD])
ins(None, 'mov dx,ax', 2, lambda L, a: [0x89, 0xC2])
ins(None, 'lodsw                ; mot', 1, lambda L, a: [0xAD])
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

# op 3 : banque (CR6A, 64 Ko), puis remplir N mots en A000:0, graine, + pas par mot
case(3, 'op3', 'op5')
ins(None, 'lodsb                ; banque', 1, lambda L, a: [0xAC])
ins(None, 'mov ah,al', 2, lambda L, a: [0x88, 0xC4])
ins(None, 'mov al,6Ah', 2, lambda L, a: [0xB0, 0x6A])
ins(None, 'mov dx,3D4h', 3, lambda L, a: [0xBA, 0xD4, 0x03])
ins(None, 'out dx,ax', 1, lambda L, a: [0xEF])
ins(None, 'lodsw                ; mots', 1, lambda L, a: [0xAD])
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

# op 5 : alimenter le port de transfert E2E8 : N mots, graine, + pas par mot
case(5, 'op5', 'op4')
ins(None, 'lodsw                ; mots', 1, lambda L, a: [0xAD])
ins(None, 'mov cx,ax', 2, lambda L, a: [0x89, 0xC1])
ins(None, 'lodsw                ; graine', 1, lambda L, a: [0xAD])
ins(None, 'push ax', 1, lambda L, a: [0x50])
ins(None, 'lodsw                ; pas', 1, lambda L, a: [0xAD])
ins(None, 'mov bx,ax', 2, lambda L, a: [0x89, 0xC3])
ins(None, 'pop ax', 1, lambda L, a: [0x58])
ins(None, 'mov dx,0E2E8h', 3, lambda L, a: [0xBA, 0xE8, 0xE2])
ins('f5', 'out dx,ax', 1, lambda L, a: [0xEF])
ins(None, 'add ax,bx', 2, lambda L, a: [0x01, 0xD8])
ins(None, 'loop f5', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'f5')])
nxt()

# op 4 : attendre la fin de la commande (9AE8 bit 9), au plus 65 535 lectures ; relevé : 9AE8
case(4, 'op4', 'op6')
ins(None, 'mov dx,9AE8h', 3, lambda L, a: [0xBA, 0xE8, 0x9A])
ins(None, 'mov cx,0FFFFh', 3, lambda L, a: [0xB9, 0xFF, 0xFF])
ins('w4', 'in ax,dx', 1, lambda L, a: [0xED])
ins(None, 'test ah,2', 3, lambda L, a: [0xF6, 0xC4, 0x02])
ins(None, 'jz w4x', 2, lambda L, a: [0x74, rel8(L, a + 2, 'w4x')])
ins(None, 'loop w4', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'w4')])
ins('w4x', 'stosw', 1, lambda L, a: [0xAB])
nxt()

# op 6 : banque (CR6A), puis somme tournante de N mots lus en A000:0 → relevé (2 octets)
case(6, 'op6', 'op7')
ins(None, 'lodsb                ; banque', 1, lambda L, a: [0xAC])
ins(None, 'mov ah,al', 2, lambda L, a: [0x88, 0xC4])
ins(None, 'mov al,6Ah', 2, lambda L, a: [0xB0, 0x6A])
ins(None, 'mov dx,3D4h', 3, lambda L, a: [0xBA, 0xD4, 0x03])
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

# op 7 : écrire un mot en MMIO, A000:déplacement
case(7, 'op7', 'op8')
ins(None, 'lodsw                ; déplacement', 1, lambda L, a: [0xAD])
ins(None, 'mov bx,ax', 2, lambda L, a: [0x89, 0xC3])
ins(None, 'mov ax,0A000h', 3, lambda L, a: [0xB8, 0x00, 0xA0])
ins(None, 'mov es,ax', 2, lambda L, a: [0x8E, 0xC0])
ins(None, 'lodsw                ; mot', 1, lambda L, a: [0xAD])
ins(None, 'mov es:[bx],ax', 3, lambda L, a: [0x26, 0x89, 0x07])
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

# op 10 : IN AX,port → relevé (2 octets)
case(10, 'op10', 'done')
ins(None, 'lodsw                ; port', 1, lambda L, a: [0xAD])
ins(None, 'mov dx,ax', 2, lambda L, a: [0x89, 0xC2])
ins(None, 'in ax,dx', 1, lambda L, a: [0xED])
ins(None, 'stosw', 1, lambda L, a: [0xAB])
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
def OUTW(port, val): return [1] + w(port) + w(val)
def INT10(ax, bx=0): return [2] + w(ax) + w(bx)
def FILL(bank, n, seed, step): return [3, bank] + w(n) + w(seed) + w(step)
WAIT = [4]
def FEED(n, seed, step): return [5] + w(n) + w(seed) + w(step)
def SUM(bank, n): return [6, bank] + w(n)
def MMIO(off, val): return [7] + w(off) + w(val)
def RDREG(port, idx): return [8] + w(port) + [idx]
def RMW(port, idx, et, ou): return [9] + w(port) + [idx, et, ou]
def INW(port): return [10] + w(port)


def CR(idx, val): return OUTW(0x3D4, idx | (val << 8))


# Les registres de l'accélérateur (vid_s3.c:186-556), par leur port 16 bits.
CUR_Y, CUR_X, DESTY, DESTX, ERR, MAJ, CMD = 0x82E8, 0x86E8, 0x8AE8, 0x8EE8, 0x92E8, 0x96E8, 0x9AE8
BKGD, FRGD, WRTMASK, RDMASK, BMIX, FMIX, MULTI = 0xA2E8, 0xA6E8, 0xAAE8, 0xAEE8, 0xB6E8, 0xBAE8, 0xBEE8

# Commandes (bits 15-13 : 1 ligne, 2 rectangle, 6 BitBLT, 7 motif) ; bits 7/5 : +Y/+X ;
# bit 4 : tracer ; bit 0 : écrire ; bit 3 : ligne radiale (direction en bits 7-5) ;
# bit 8 : données du bus ; bits 10-9 = 01 : bus 16 bits.
RECT, BLT, PAT = 0x40B1, 0xC0B1, 0xE0B1


def regs(*pairs, mmio=False):
    out = []
    for port, val in pairs:
        out += MMIO(port, val) if mmio else OUTW(port, val)
    return out


def rect(x, y, wd, h, cmd=RECT, mmio=False):
    return regs((CUR_X, x), (CUR_Y, y), (MAJ, wd - 1), (MULTI, 0x0000 | (h - 1)), (CMD, cmd), mmio=mmio)


def blit(sx, sy, dx, dy, wd, h, cmd=BLT):
    return regs((CUR_X, sx), (CUR_Y, sy), (DESTX, dx), (DESTY, dy), (MAJ, wd - 1), (MULTI, h - 1), (CMD, cmd))


S = []
S += INT10(0x4F02, 0x0101)                           # VESA 101h : 640 × 480 × 256
S += CR(0x38, 0x48) + CR(0x39, 0xA5)                 # registres S3 déverrouillés
S += RMW(0x3D4, 0x40, 0xFF, 0x01)                    # fonctions étendues (CR40 bit 0)
S += RDREG(0x3D4, 0x30) + RDREG(0x3D4, 0x2E) + RDREG(0x3D4, 0x31) + RDREG(0x3D4, 0x50)
# -- la VRAM : banques 0 et 1 (lignes 0-204) remplies de motifs --------------------------------
S += FILL(0x00, 0x8000, 0x0100, 0x0203)
S += FILL(0x01, 0x8000, 0x8001, 0x0507)
# -- l'état commun : masques pleins, ciseaux ouverts, sélection par la couleur ------------------
S += regs((WRTMASK, 0xFFFF), (RDMASK, 0xFFFF), (MULTI, 0x1000), (MULTI, 0x2000), (MULTI, 0x3000),
          (MULTI, 0x4FFF), (MULTI, 0x5FFF), (MULTI, 0xA000), (BKGD, 0x0011), (BMIX, 0x0003))
# -- rectangles pleins sous les seize mélanges (couleur de premier plan) --------------------------
S += OUTW(FRGD, 0x0033)
for m in range(16):
    S += OUTW(FMIX, 0x20 | m) + rect(40 * m, 210, 32, 12) + WAIT
# -- BitBLT avant, puis arrière (sans +X ni +Y : coordonnées du coin bas droit) ----------------
S += OUTW(FMIX, 0x67)                                # source : la mémoire d'affichage
S += blit(20, 10, 0, 230, 128, 40) + WAIT
S += blit(147, 99, 299, 299, 128, 40, cmd=BLT & ~0xA0) + WAIT
# -- motif 8 × 8 pris en (0, 100), répété sur 160 × 32 ----------------------------------------
S += blit(0, 100, 320, 230, 160, 32, cmd=PAT) + WAIT
# -- lignes radiales dans les huit directions, longueur 40 --------------------------------------
S += OUTW(FMIX, 0x27) + OUTW(FRGD, 0x00C5)
for d in range(8):
    S += regs((CUR_X, 560), (CUR_Y, 260), (MAJ, 39), (CMD, 0x2019 | (d << 5))) + WAIT
# -- données du CPU en couleur : 32 × 8 points, un point par octet, deux par mot (E2E8) ------------
S += OUTW(FMIX, 0x47)                                # source : le CPU
S += rect(0, 320, 32, 8, cmd=RECT | 0x0300) + FEED(32 * 8 // 2, 0x1357, 0x0F0F) + WAIT
# -- données du CPU en expansion monochrome : 64 × 8 points, seize par mot ----------------------
S += OUTW(MULTI, 0xA080) + OUTW(FMIX, 0x27) + OUTW(BMIX, 0x07) + OUTW(FRGD, 0x000C) + OUTW(BKGD, 0x0001)
S += rect(100, 320, 64, 8, cmd=RECT | 0x0300) + FEED(64 * 8 // 16, 0xA5C3, 0x3311) + WAIT
# -- sélection par la mémoire d'affichage : BitBLT où le plan lu choisit fg / bg -----------------
S += OUTW(MULTI, 0xA0C0) + blit(0, 0, 200, 320, 96, 24) + WAIT + OUTW(MULTI, 0xA000)
# -- ciseaux : un rectangle de 200 × 40 rogné à (420..479, 330..349) ---------------------------
S += regs((MULTI, 0x1000 | 330), (MULTI, 0x2000 | 420), (MULTI, 0x3000 | 349), (MULTI, 0x4000 | 479))
S += OUTW(FMIX, 0x27) + OUTW(FRGD, 0x007E) + rect(400, 320, 200, 40) + WAIT
S += regs((MULTI, 0x1000), (MULTI, 0x2000), (MULTI, 0x3FFF), (MULTI, 0x4FFF))
# -- le même rectangle en MMIO : CR53 bit 4 ouvre A000:8000-FFFF --------------------------------
S += RMW(0x3D4, 0x53, 0xFF, 0x10) + RDREG(0x3D4, 0x53)
S += OUTW(FRGD, 0x0099) + rect(300, 400, 120, 40, mmio=True) + WAIT
S += RMW(0x3D4, 0x53, 0xEF, 0x00)
# -- relevés : statut, puis sommes de la VRAM touchée (lignes 204-460, banques 3-4) ----------------
S += INW(0x9AE8) + INW(0x42E8)
S += SUM(0x03, 0x8000) + SUM(0x04, 0x8000) + SUM(0x00, 0x8000) + SUM(0x01, 0x8000)
S += CR(0x6A, 0x00)
S += [0]

ins('script', 'db script', len(S), lambda L, a, S=S: list(S))
ins('crlf', "db 13,10,'$'", 3, lambda L, a: [13, 10, 0x24])
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
    lines += ['N S3BANC.COM', 'R CX', f'{len(code):X}', 'W', 'Q', 'S3BANC']
    return lines


if __name__ == '__main__':
    code, listing = assemble()
    print('\n'.join(listing))
    print(f'\n{len(code)} octets, script {len(S)} octets')
    here = os.path.dirname(os.path.abspath(__file__))
    with open(os.path.join(here, 's3banc.keys'), 'w') as f:
        f.write('\n'.join(keys(code)) + '\n')
    if '--com' in sys.argv:
        with open(sys.argv[sys.argv.index('--com') + 1], 'wb') as f:
            f.write(code)
