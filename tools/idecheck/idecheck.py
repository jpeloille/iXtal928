#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# IDECHK.COM — l'ide-check dirigé de G5.2 (PLAN-G5.md).
#
# Le harnais n'expose pas les ports d'E/S : les commandes ATA sont donc écrites par L'INVITÉ.
# IDECHK.COM est un petit interprète : une table d'opérations (écrire un registre, attendre,
# lire ou écrire N mots, relever les sept registres…) déroulée sur le canal primaire puis sur
# le secondaire. Il est saisi dans DEBUG.EXE par KeyScript, écrit sur C:, puis lancé — sous
# boot-diff, qui compare chaque instruction, les registres, et les images des disques.
# Les relevés s'affichent en hexadécimal à la fin, pour les yeux : la preuve est le diff.
#
# Machine attendue : un AT à contrôleur « ide » ; C: (maître primaire) amorçable, DOS 5 sans
# KEYB FR ; E: (maître secondaire, clé hde_) une image vierge de type 46. C: n'est QUE LU
# (IDENTIFY, READ, READ MULTIPLE, VERIFY) ; les écritures vont sur E:, que le BIOS ignore.
# READ/WRITE MULTIPLE sans SET MULTIPLE MODE ne sont pas tirés : PCem s'y arrête (PB-73).
#
# Usage :
#   python3 tools/idecheck/idecheck.py            écrit idecheck.keys, affiche le listing
#   python3 tools/idecheck/idecheck.py --com F    écrit aussi le .COM (objdump -D -b binary
#                                                 -mi8086 --adjust-vma=0x100 F)
#   python3 tools/idecheck/idecheck.py --survie   la variante R9, survie.keys (C# seul)

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


# ---- l'interprète -----------------------------------------------------------------------
# BP = base du canal (0x1F0 / 0x170), BX = registre de contrôle (base + 0x206),
# SI = pointeur de script, DI = pointeur de relevés.
ins('start', 'mov si,script', 3, lambda L, a: [0xBE] + w(L['script']))
ins(None, 'mov di,results', 3, lambda L, a: [0xBF] + w(L['results']))
ins(None, 'cld', 1, lambda L, a: [0xFC])
ins('next', 'lodsb', 1, lambda L, a: [0xAC])

# op 1 : OUT base+reg, val
ins(None, 'cmp al,1', 2, lambda L, a: [0x3C, 0x01])
ins(None, 'jne op2', 2, lambda L, a: [0x75, rel8(L, a + 2, 'op2')])
ins(None, 'lodsb                ; reg', 1, lambda L, a: [0xAC])
ins(None, 'xor ah,ah', 2, lambda L, a: [0x30, 0xE4])
ins(None, 'mov dx,bp', 2, lambda L, a: [0x89, 0xEA])
ins(None, 'add dx,ax', 2, lambda L, a: [0x01, 0xC2])
ins(None, 'lodsb                ; val', 1, lambda L, a: [0xAC])
ins(None, 'out dx,al', 1, lambda L, a: [0xEE])
ins(None, 'jmp next', 3, lambda L, a: [0xE9] + rel16(L, a + 3, 'next'))

# op 2 : attendre BUSY à zéro (au plus 65 535 lectures)
ins('op2', 'cmp al,2', 2, lambda L, a: [0x3C, 0x02])
ins(None, 'jne op3', 2, lambda L, a: [0x75, rel8(L, a + 2, 'op3')])
ins(None, 'mov dx,bp', 2, lambda L, a: [0x89, 0xEA])
ins(None, 'add dx,7', 3, lambda L, a: [0x83, 0xC2, 0x07])
ins(None, 'mov cx,0FFFFh', 3, lambda L, a: [0xB9, 0xFF, 0xFF])
ins('w2', 'in al,dx', 1, lambda L, a: [0xEC])
ins(None, 'test al,80h', 2, lambda L, a: [0xA8, 0x80])
ins(None, 'jz w2x', 2, lambda L, a: [0x74, rel8(L, a + 2, 'w2x')])
ins(None, 'loop w2', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'w2')])
ins('w2x', 'jmp next', 3, lambda L, a: [0xE9] + rel16(L, a + 3, 'next'))

# op 3 : lire N mots du port de données, somme tournante → relevé (2 octets)
ins('op3', 'cmp al,3', 2, lambda L, a: [0x3C, 0x03])
ins(None, 'jne op4', 2, lambda L, a: [0x75, rel8(L, a + 2, 'op4')])
ins(None, 'lodsw', 1, lambda L, a: [0xAD])
ins(None, 'mov cx,ax', 2, lambda L, a: [0x89, 0xC1])
ins(None, 'mov dx,bp', 2, lambda L, a: [0x89, 0xEA])
ins('r3', 'in ax,dx', 1, lambda L, a: [0xED])
ins(None, 'rol word [sum],1', 4, lambda L, a: [0xD1, 0x06] + w(L['sum']))
ins(None, 'add [sum],ax', 4, lambda L, a: [0x01, 0x06] + w(L['sum']))
ins(None, 'loop r3', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'r3')])
ins(None, 'mov ax,[sum]', 3, lambda L, a: [0xA1] + w(L['sum']))
ins(None, 'stosw', 1, lambda L, a: [0xAB])
ins(None, 'mov word [sum],0', 6, lambda L, a: [0xC7, 0x06] + w(L['sum']) + [0, 0])
ins(None, 'jmp next', 3, lambda L, a: [0xE9] + rel16(L, a + 3, 'next'))

# op 4 : écrire N mots, motif graine, +0101h par mot
ins('op4', 'cmp al,4', 2, lambda L, a: [0x3C, 0x04])
ins(None, 'jne op5', 2, lambda L, a: [0x75, rel8(L, a + 2, 'op5')])
ins(None, 'lodsw', 1, lambda L, a: [0xAD])
ins(None, 'mov cx,ax', 2, lambda L, a: [0x89, 0xC1])
ins(None, 'lodsw                ; graine', 1, lambda L, a: [0xAD])
ins(None, 'mov dx,bp', 2, lambda L, a: [0x89, 0xEA])
ins('w4', 'out dx,ax', 1, lambda L, a: [0xEF])
ins(None, 'add ax,0101h', 3, lambda L, a: [0x05, 0x01, 0x01])
ins(None, 'loop w4', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'w4')])
ins(None, 'jmp next', 3, lambda L, a: [0xE9] + rel16(L, a + 3, 'next'))

# op 5 : relever 1F7, 1F1, 1F2 … 1F6 (sept octets)
ins('op5', 'cmp al,5', 2, lambda L, a: [0x3C, 0x05])
ins(None, 'jne op6', 2, lambda L, a: [0x75, rel8(L, a + 2, 'op6')])
for r in (7, 1, 2, 3, 4, 5, 6):
    ins(None, 'mov dx,bp', 2, lambda L, a: [0x89, 0xEA])
    ins(None, f'add dx,{r}', 3, lambda L, a, r=r: [0x83, 0xC2, r])
    ins(None, 'in al,dx', 1, lambda L, a: [0xEC])
    ins(None, 'stosb', 1, lambda L, a: [0xAA])
ins(None, 'jmp next', 3, lambda L, a: [0xE9] + rel16(L, a + 3, 'next'))

# op 6 : attendre BUSY à zéro ET (DRQ ou ERR)
ins('op6', 'cmp al,6', 2, lambda L, a: [0x3C, 0x06])
ins(None, 'jne op7', 2, lambda L, a: [0x75, rel8(L, a + 2, 'op7')])
ins(None, 'mov dx,bp', 2, lambda L, a: [0x89, 0xEA])
ins(None, 'add dx,7', 3, lambda L, a: [0x83, 0xC2, 0x07])
ins(None, 'mov cx,0FFFFh', 3, lambda L, a: [0xB9, 0xFF, 0xFF])
ins('w6', 'in al,dx', 1, lambda L, a: [0xEC])
ins(None, 'test al,80h', 2, lambda L, a: [0xA8, 0x80])
ins(None, 'jnz w6n', 2, lambda L, a: [0x75, rel8(L, a + 2, 'w6n')])
ins(None, 'test al,09h', 2, lambda L, a: [0xA8, 0x09])
ins(None, 'jnz w6x', 2, lambda L, a: [0x75, rel8(L, a + 2, 'w6x')])
ins('w6n', 'loop w6', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'w6')])
ins('w6x', 'jmp next', 3, lambda L, a: [0xE9] + rel16(L, a + 3, 'next'))

# op 7 : choisir le canal (base) ; contrôle = base + 206h
ins('op7', 'cmp al,7', 2, lambda L, a: [0x3C, 0x07])
ins(None, 'jne op8', 2, lambda L, a: [0x75, rel8(L, a + 2, 'op8')])
ins(None, 'lodsw', 1, lambda L, a: [0xAD])
ins(None, 'mov bp,ax', 2, lambda L, a: [0x89, 0xC5])
ins(None, 'add ax,206h', 3, lambda L, a: [0x05, 0x06, 0x02])
ins(None, 'mov bx,ax', 2, lambda L, a: [0x89, 0xC3])
ins(None, 'jmp next', 3, lambda L, a: [0xE9] + rel16(L, a + 3, 'next'))

# op 8 : OUT contrôle, val
ins('op8', 'cmp al,8', 2, lambda L, a: [0x3C, 0x08])
ins(None, 'jne op9', 2, lambda L, a: [0x75, rel8(L, a + 2, 'op9')])
ins(None, 'lodsb', 1, lambda L, a: [0xAC])
ins(None, 'mov dx,bx', 2, lambda L, a: [0x89, 0xDA])
ins(None, 'out dx,al', 1, lambda L, a: [0xEE])
ins(None, 'jmp next', 3, lambda L, a: [0xE9] + rel16(L, a + 3, 'next'))

# op 9 : attente fixe, N × 256 tours de LOOP
ins('op9', 'cmp al,9', 2, lambda L, a: [0x3C, 0x09])
ins(None, 'jne done', 2, lambda L, a: [0x75, rel8(L, a + 2, 'done')])
ins(None, 'lodsb', 1, lambda L, a: [0xAC])
ins(None, 'mov ch,al', 2, lambda L, a: [0x88, 0xC5])
ins(None, 'xor cl,cl', 2, lambda L, a: [0x30, 0xC9])
ins('d9', 'loop d9', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'd9')])
ins(None, 'jmp next', 3, lambda L, a: [0xE9] + rel16(L, a + 3, 'next'))

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
def OUT(reg, val): return [1, reg, val]
WAIT = [2]
def RD(n): return [3] + w(n)
def WR(n, seed): return [4] + w(n) + w(seed)
REC = [5]
WDRQ = [6]
def BASE(b): return [7] + w(b)
def CTL(v): return [8, v]
def DELAY(n): return [9, n]


def regs(head_sel, sector, cyl_lo, cyl_hi, count):
    return OUT(6, head_sel) + OUT(3, sector) + OUT(4, cyl_lo) + OUT(5, cyl_hi) + OUT(2, count)


S = []
# -- canal primaire, C: : lectures seulement --------------------------------------------------
S += BASE(0x1F0) + OUT(6, 0xA0) + WAIT + REC
S += OUT(7, 0xEC) + WDRQ + RD(256) + REC                         # IDENTIFY
S += OUT(2, 4) + OUT(7, 0xC6) + WAIT + REC                       # SET MULTIPLE MODE 4
S += regs(0xE0, 0, 0, 0, 8) + OUT(7, 0xC4)                       # READ MULTIPLE, LBA 0, 8
S += WDRQ + RD(1024) + REC + WDRQ + RD(1024) + REC
S += regs(0xA0, 17, 0, 0, 2) + OUT(7, 0x20)                      # READ CHS 0/0/17, 2 : change de tête
S += WDRQ + RD(256) + WDRQ + RD(256) + REC
S += regs(0xA0, 1, 0, 0, 5) + OUT(7, 0x40) + WAIT + REC          # VERIFY 5 (PB-74)
S += OUT(7, 0xE5) + WAIT + REC                                   # CHECK POWER MODE
S += OUT(7, 0xE3) + WAIT + REC                                   # SETIDLE1 : ABRT
S += OUT(7, 0x99) + WAIT + REC                                   # commande inconnue : ABRT
S += OUT(7, 0xEF) + WAIT + REC                                   # SET FEATURES sur disque : ABRT
S += OUT(6, 0xB0) + REC + OUT(7, 0xEC) + REC + OUT(6, 0xA0) + WAIT + REC   # esclave absent
# -- canal secondaire, E: vierge : écritures ---------------------------------------------------
S += BASE(0x170) + OUT(6, 0xA0) + WAIT + REC
S += OUT(7, 0xEC) + WDRQ + RD(256) + REC                         # IDENTIFY
S += regs(0xE0, 100, 0, 0, 2) + OUT(7, 0x30)                     # WRITE LBA 100, 2
S += WDRQ + WR(256, 0x1111) + WDRQ + WR(256, 0x2222) + WAIT + REC
S += regs(0xE0, 100, 0, 0, 2) + OUT(7, 0x20)                     # relecture
S += WDRQ + RD(256) + WDRQ + RD(256) + REC
S += OUT(2, 2) + OUT(7, 0xC6) + WAIT + REC                       # SET MULTIPLE MODE 2
S += regs(0xE0, 200, 0, 0, 4) + OUT(7, 0xC5)                     # WRITE MULTIPLE LBA 200, 4
S += WDRQ + WR(512, 0x3333) + WDRQ + WR(512, 0x4444) + WAIT + REC
S += regs(0xE0, 200, 0, 0, 4) + OUT(7, 0xC4)                     # READ MULTIPLE relecture
S += WDRQ + RD(512) + REC + WDRQ + RD(512) + REC
S += regs(0xA0, 1, 5, 0, 17) + OUT(7, 0x50)                      # FORMAT cyl 5 tête 0
S += WDRQ + WR(256, 0) + WAIT + REC
S += OUT(2, 17) + OUT(6, 0xAE) + OUT(7, 0x91) + WAIT + REC       # SPECIFY 17 secteurs, 15 têtes
S += OUT(7, 0x90) + WAIT + REC                                   # DIAGNOSTICS
S += OUT(7, 0x10) + WAIT + REC                                   # RECALIBRATE
S += CTL(0x06) + DELAY(4) + CTL(0x02) + WAIT + REC               # reset logiciel (SRST)
S += CTL(0x06) + CTL(0x02) + OUT(6, 0xB5) + REC                  # sélection pendant le reset (PB-72)
S += OUT(6, 0xA0) + WAIT + REC
S += CTL(0x02) + OUT(7, 0xE5) + WAIT + REC + CTL(0x00)           # nIEN : pas d'IRQ
S += [0]

# ---- --survie : la règle R9 (TRANSCRIPTION.md), en C# SEUL -----------------------------------
# Les trois chemins où PCem appelle fatal() sur une commande de l'invité, et qu'iXtal26 refuse
# (ABRT) au lieu de s'arrêter. Jamais envoyé à l'oracle, qui s'y arrêterait : `--boot` seul. Le
# même programme sert aux deux contrôleurs de l'AT, sur C: au canal primaire :
#   - READ MULTIPLE, WRITE MULTIPLE sans SET MULTIPLE MODE : PB-73 (ide) ; commande inconnue
#     du mfm_at, ABRT de toute façon ;
#   - une commande à l'unité 1 absente : PB-75 (mfm_at) ; ignorée par l'IDE (IDE_NONE) ;
#   - READ LONG (22h) et WRITE LONG (32h) : PB-76 (mfm_at) ; commandes inconnues de l'IDE, ABRT.
SURVIE = '--survie' in sys.argv
if SURVIE:
    S = []
    S += BASE(0x1F0) + OUT(6, 0xA0) + WAIT + REC
    S += regs(0xE0, 0, 0, 0, 2) + OUT(7, 0xC4) + WAIT + REC     # READ MULTIPLE sans bloc
    S += regs(0xE0, 0, 0, 0, 2) + OUT(7, 0xC5) + WAIT + REC     # WRITE MULTIPLE sans bloc
    S += OUT(6, 0xB0) + OUT(7, 0x20) + WAIT + REC + OUT(6, 0xA0) + WAIT + REC   # unité 1 absente
    S += regs(0xA0, 1, 0, 0, 1) + OUT(7, 0x22) + WAIT + REC     # READ LONG
    S += regs(0xA0, 1, 0, 0, 1) + OUT(7, 0x32) + WAIT + REC     # WRITE LONG
    S += [0]

ins('script', 'db script', len(S), lambda L, a, S=S: list(S))
ins('sum', 'dw 0', 2, lambda L, a: [0, 0])
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
    nom = 'SURVIE' if SURVIE else 'IDECHK'
    lines += [f'N {nom}.COM', 'R CX', f'{len(code):X}', 'W', 'Q', nom]
    return lines


if __name__ == '__main__':
    code, listing = assemble()
    print('\n'.join(listing))
    print(f'\n{len(code)} octets, script {len(S)} octets')
    here = os.path.dirname(os.path.abspath(__file__))
    with open(os.path.join(here, 'survie.keys' if SURVIE else 'idecheck.keys'), 'w') as f:
        f.write('\n'.join(keys(code)) + '\n')
    if '--com' in sys.argv:
        with open(sys.argv[sys.argv.index('--com') + 1], 'wb') as f:
            f.write(code)
