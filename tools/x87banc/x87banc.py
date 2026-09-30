#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# X87BANC.COM — le banc du coprocesseur de G4.7 (PLAN-G4.md, décision n° 6 révisée).
#
# QBASIC 1.1 ne convient pas : il force à 0 son drapeau « 8087 présent » et n'exécute jamais
# un ESC (VERIFICATION.md § G4.7). Ce banc-ci est écrit par nous, en octets, et SAISI DANS
# DEBUG.EXE par KeyScript (`--boot … --type`) : aucune dépendance à un outil de l'hôte, et la
# machine émulée fabrique elle-même son .COM. Rejouable à l'identique.
#
# Ce qu'il fait :
#   1. détection : SMSW (CR0.EM posé = pas de coprocesseur, et un ESC y ferait INT 7), puis
#      FNINIT / FNSTSW m16 sur un mot pré-rempli à 5A5Ah : un coprocesseur y écrit 0000 ;
#      sinon « PAS DE FPU » et sortie par INT 21h/4C01h ;
#   2. une boucle fixe de 4 x 65 535 tours : FLD m64 (0,5), FPTAN, FDIVP, FSQRT, FMUL m64,
#      FSTP m64 — des instructions COMMUNES au 287 et au 387 (FSIN et FCOS n'existent pas
#      sur le 287 ; 0,5 est dans le domaine [0, pi/4] qu'exige le FPTAN du 287) ;
#   3. le chronométrage par INT 1Ah/AH=0 (tics BIOS, 18,2 par seconde), avant et après ;
#   4. l'affichage du nombre de tics, en décimal, suivi de « TICS ».
#
# Usage :
#   python3 tools/x87banc/x87banc.py            écrit x87banc.keys (les lignes à taper) et
#                                               affiche le listing
#   python3 tools/x87banc/x87banc.py --com F    écrit aussi le .COM, pour le désassembler
#                                               (objdump -D -b binary -mi8086 --adjust-vma=0x100)
#
# Les lignes de x87banc.keys se passent une par une en `--type` à `iXtal26 --boot`, disquette
# inscriptible en B: (voir VERIFICATION.md § G4.7).

import os
import struct
import sys

ORG = 0x100
OUTER = 4          # tours de la boucle externe (SI)
INNER = 0xFFFF     # tours de la boucle interne (CX, LOOP)


def w(v):
    return list(struct.pack('<H', v & 0xFFFF))


# Chaque entrée : (étiquette ou None, mnémonique lisible, fonction labels -> octets, taille).
# La taille est fixe par instruction : deux passes suffisent.
PROG = []


def ins(label, text, size, enc):
    PROG.append((label, text, size, enc))


def rel8(L, here_end, target):
    d = L[target] - here_end
    assert -128 <= d <= 127, (target, d)
    return d & 0xFF


# ---- 1. détection -------------------------------------------------------------------
ins('start', 'smsw ax', 3, lambda L, a: [0x0F, 0x01, 0xE0])
ins(None, 'test al,4            ; CR0.EM', 2, lambda L, a: [0xA8, 0x04])
ins(None, 'jnz nofpu', 2, lambda L, a: [0x75, rel8(L, a + 2, 'nofpu')])
ins(None, 'fninit', 2, lambda L, a: [0xDB, 0xE3])
ins(None, 'mov word [sw],5A5Ah', 6, lambda L, a: [0xC7, 0x06] + w(L['sw']) + [0x5A, 0x5A])
ins(None, 'fnstsw [sw]', 4, lambda L, a: [0xDD, 0x3E] + w(L['sw']))
ins(None, 'cmp byte [sw],0', 5, lambda L, a: [0x80, 0x3E] + w(L['sw']) + [0x00])
ins(None, 'jnz nofpu', 2, lambda L, a: [0x75, rel8(L, a + 2, 'nofpu')])
# ---- 2. chronomètre, départ ----------------------------------------------------------
ins(None, 'mov ah,0', 2, lambda L, a: [0xB4, 0x00])
ins(None, 'int 1Ah              ; CX:DX = tics', 2, lambda L, a: [0xCD, 0x1A])
ins(None, 'mov [t0],dx', 4, lambda L, a: [0x89, 0x16] + w(L['t0']))
ins(None, 'mov si,OUTER', 3, lambda L, a: [0xBE] + w(OUTER))
ins('outer', 'mov cx,INNER', 3, lambda L, a: [0xB9] + w(INNER))
# ---- 3. la boucle -------------------------------------------------------------------
ins('inner', 'fld qword [x]        ; 0,5', 4, lambda L, a: [0xDD, 0x06] + w(L['x']))
ins(None, 'fptan                ; st0 = 1, st1 = tan x', 2, lambda L, a: [0xD9, 0xF2])
ins(None, 'fdivp st1,st0', 2, lambda L, a: [0xDE, 0xF9])
ins(None, 'fsqrt', 2, lambda L, a: [0xD9, 0xFA])
ins(None, 'fmul qword [x]', 4, lambda L, a: [0xDC, 0x0E] + w(L['x']))
ins(None, 'fstp qword [acc]', 4, lambda L, a: [0xDD, 0x1E] + w(L['acc']))
ins(None, 'loop inner', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'inner')])
ins(None, 'dec si', 1, lambda L, a: [0x4E])
ins(None, 'jnz outer', 2, lambda L, a: [0x75, rel8(L, a + 2, 'outer')])
ins(None, 'fwait', 1, lambda L, a: [0x9B])
# ---- 4. chronomètre, arrivée ; affichage ---------------------------------------------
ins(None, 'mov ah,0', 2, lambda L, a: [0xB4, 0x00])
ins(None, 'int 1Ah', 2, lambda L, a: [0xCD, 0x1A])
ins(None, 'sub dx,[t0]', 4, lambda L, a: [0x2B, 0x16] + w(L['t0']))
ins(None, 'mov ax,dx', 2, lambda L, a: [0x89, 0xD0])
ins(None, 'call pdec', 3, lambda L, a: [0xE8] + w(L['pdec'] - (a + 3)))
ins(None, 'mov dx,msg_tics', 3, lambda L, a: [0xBA] + w(L['msg_tics']))
ins(None, 'mov ah,9', 2, lambda L, a: [0xB4, 0x09])
ins(None, 'int 21h', 2, lambda L, a: [0xCD, 0x21])
ins(None, 'mov ax,4C00h', 3, lambda L, a: [0xB8, 0x00, 0x4C])
ins(None, 'int 21h', 2, lambda L, a: [0xCD, 0x21])
ins('nofpu', 'mov dx,msg_nofpu', 3, lambda L, a: [0xBA] + w(L['msg_nofpu']))
ins(None, 'mov ah,9', 2, lambda L, a: [0xB4, 0x09])
ins(None, 'int 21h', 2, lambda L, a: [0xCD, 0x21])
ins(None, 'mov ax,4C01h', 3, lambda L, a: [0xB8, 0x01, 0x4C])
ins(None, 'int 21h', 2, lambda L, a: [0xCD, 0x21])
# ---- pdec : AX en décimal, par INT 21h/AH=2 ------------------------------------------
ins('pdec', 'mov bx,10', 3, lambda L, a: [0xBB, 0x0A, 0x00])
ins(None, 'xor cx,cx', 2, lambda L, a: [0x31, 0xC9])
ins('pd1', 'xor dx,dx', 2, lambda L, a: [0x31, 0xD2])
ins(None, 'div bx', 2, lambda L, a: [0xF7, 0xF3])
ins(None, 'push dx', 1, lambda L, a: [0x52])
ins(None, 'inc cx', 1, lambda L, a: [0x41])
ins(None, 'test ax,ax', 2, lambda L, a: [0x85, 0xC0])
ins(None, 'jnz pd1', 2, lambda L, a: [0x75, rel8(L, a + 2, 'pd1')])
ins('pd2', 'pop dx', 1, lambda L, a: [0x5A])
ins(None, "add dl,'0'", 3, lambda L, a: [0x80, 0xC2, 0x30])
ins(None, 'mov ah,2', 2, lambda L, a: [0xB4, 0x02])
ins(None, 'int 21h', 2, lambda L, a: [0xCD, 0x21])
ins(None, 'loop pd2', 2, lambda L, a: [0xE2, rel8(L, a + 2, 'pd2')])
ins(None, 'ret', 1, lambda L, a: [0xC3])


def data(label, text, bs):
    ins(label, text, len(bs), lambda L, a, bs=bs: list(bs))


data('x', 'dq 0.5', struct.pack('<d', 0.5))
data('acc', 'dq 0', bytes(8))
data('sw', 'dw 0', bytes(2))
data('t0', 'dw 0', bytes(2))
data('msg_tics', "db ' TICS',13,10,'$'", b' TICS\r\n$')
data('msg_nofpu', "db 'PAS DE FPU',13,10,'$'", b'PAS DE FPU\r\n$')


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
        listing.append(f'{a:04X}  {" ".join(f"{b:02X}" for b in bs):<24} {label + ":" if label else "":<10}{text}')
        out += bs
        a += size
    return bytes(out), listing


def keys(code):
    # DEBUG : « E adresse octets… » par lignes de 16, puis N, R CX (DEBUG demande la valeur
    # sur la ligne suivante), W, Q. KeyScript tape les majuscules avec Maj.
    lines = ['DEBUG']
    for i in range(0, len(code), 16):
        lines.append(f'E {ORG + i:X} ' + ' '.join(f'{b:02X}' for b in code[i:i + 16]))
    lines += ['N B:X87BANC.COM', 'R CX', f'{len(code):X}', 'W', 'Q']
    return lines


if __name__ == '__main__':
    code, listing = assemble()
    print('\n'.join(listing))
    print(f'\n{len(code)} octets')
    here = os.path.dirname(os.path.abspath(__file__))
    with open(os.path.join(here, 'x87banc.keys'), 'w') as f:
        f.write('\n'.join(keys(code)) + '\n')
    if '--com' in sys.argv:
        with open(sys.argv[sys.argv.index('--com') + 1], 'wb') as f:
            f.write(code)
