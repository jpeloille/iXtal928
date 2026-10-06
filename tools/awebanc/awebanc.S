# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# AWEBANC.COM — le banc de l'EMU8000 de la Sound Blaster AWE32 (G12.2, PLAN-G12.md).
#
# Un programme qui parle à la puce elle-même, par ses trois fenêtres de quatre ports (620h, A20h, E20h ; le
# pointeur en E22h : le registre << 5 | le canal) — et au DSP 4.13 de la carte, en 220h. Saisi dans DEBUG.EXE par
# KeyScript, écrit sur C:, lancé — sous boot-diff, qui compare chaque instruction : chaque mot que la puce rend est
# relu dans un registre (et sommé dans BX), donc vu par la trace ; ce qu'elle JOUE n'est vu que par la sonde du son
# et par celle de l'EMU8000. La somme s'affiche à la fin, pour les yeux.
#
# Machine attendue : l'ami486, l'AWE32 en 220h, l'EMU8000 en 620h, 512 Ko de RAM ; DOS 5 sur le disque SCSI de G11.
# Dans l'ordre :
#   1. le DSP : E1h (4.13) ; 08h, qui rend 18h tout de suite, son paramètre pris pour une commande (PB-165) ;
#   2. la détection : trois lectures du pointeur (l'octet haut varie), les trois mots de configuration, des
#      lectures OCTET aux ports pairs et impairs (`>> 1`, PB-158), l'identifiant (E20h, registre 7), le registre 6
#      de Data3, les registres inconnus 4 et 5 de Data0 ;
#   3. l'initialisation : init1 à init4 en deux passes sous init1[0] = 03FFh (les effets ne bougent pas), puis les
#      effets : la réverbération (le mélange, les filtres, les gains, les retours, l'amortissement, la taille de la
#      pièce au quartet Dh, la dernière qui tienne), le chorus (le retour, le délai, la profondeur, hwcf4, hwcf5) ;
#      hwcf1 à hwcf3 ;
#   4. la DRAM : un motif en 200000h par SMALW et SMLD ; 280000h, au-delà des 512 Ko, se replie en 200000h ; le
#      tout relu par SMALR et SMLD (le premier mot rendu est le tampon périmé) ; SMALR relu en A22h : le bit vide
#      ne s'y voit pas (PB-159) ; SMARW, sans masque ; une écriture OCTET au port pair et au port impair (PB-158) ;
#   5. la ROM : les mots 0, 1 et 7FFFEh, 7FFFFh par SMARR et SMRD ;
#   6. un échantillon en RAM (une dent de scie de 2 048 mots), une note sur le canal 0 : enveloppes, LFO, hauteur,
#      filtre, panoramique, envois aux effets ; WC relu deux fois sans écriture entre (figé, PB-158), puis après
#      un tic ; CCCA, CPF, CVCF relus pendant qu'elle joue ; le relâchement ;
#   7. hwcf3 remis à 0 : le moteur d'enveloppes rallumé le remet à 4 (le correctif de Doom, PB-160) ; une note avec
#      maintien sur le canal 1, redéclenchée sans réécrire ses enveloppes ; IFATN ignoré sur le canal 5 (le
#      correctif d'Impulse Tracker, PB-160) ;
#   8. un chorus extrême (le délai 1FFFh, la profondeur FFh, hwcf4 1FFFFFh, hwcf5 4000h) : le canal droit lit sous
#      son tampon (PB-162) ; la réverbération liée (init1[7] = 8474h) et ses deux queues ;
#   9. la fin sur une note tenue (canal 2) : les sondes voient des tampons non nuls.
# JAMAIS le quartet Eh ou Fh en init2 canal 14h (ni 16h quand la réverbération est liée) : les tampons de la
# réverbération déborderaient (R9, PB-161).
#
# Construit par awebanc.py (GNU as en mode 16 bits, ld en binaire plat à 100h) ; awebanc.keys en est tiré.

.intel_syntax noprefix
.code16
.text
.globl _start

.set SB,     0x220
.set D0,     0x620           # Data0, le mot bas ; D0 + 2 : le mot haut
.set D1,     0xA20           # Data1 (emu_addr + 400h)
.set D2,     0xA22           # Data2
.set D3,     0xE20           # Data3 (emu_addr + 800h)
.set PTR,    0xE22           # le pointeur
.set RES,    0x8000          # les relevés

_start:
    cld
    xor bx, bx
    mov di, RES

# ---- 1. le DSP 4.13 -----------------------------------------------------------------------------------
    call reset
    call dsp                        # E1h : 04h 0Dh
    .byte 1, 0xE1
    call dsprn
    .byte 2
    call dsp                        # 08h : 18h tout de suite (sb_commands[8] = -1 hors SB16, PB-165) ;
                                    #   E1h, une commande
    .byte 2, 0x08, 0xE1
    call dsprn
    .byte 3

# ---- 2. la détection ----------------------------------------------------------------------------------
    call rd                         # le pointeur, trois fois
    .word PTR
    call rd
    .word PTR
    call rd
    .word PTR
    call rrw                        # les trois mots de configuration : A20h, registre 1, canaux 29 à 31
    .byte 1, 29
    .word D1
    call rrw
    .byte 1, 30
    .word D1
    call rrw
    .byte 1, 31
    .word D1
    call wr                         # des octets : IP du canal 0, posé à E5A3h, lu en E20h et E21h (`>> 1`, PB-158) ;
    .byte 0, 0                      #   le pointeur en E23h et E22h
    .word D3, 0xE5A3
    call rdb
    .word D3
    call rdb
    .word D3 + 1
    call rdb
    .word PTR + 1
    call rdb
    .word PTR
    call rrw                        # l'identifiant : 1Ch ; puis 2 écrit, FF1Eh relu ; puis 0
    .byte 7, 0
    .word D3
    call wr
    .byte 7, 0
    .word D3, 0x0002
    call rrw
    .byte 7, 0
    .word D3
    call wr
    .byte 7, 0
    .word D3, 0x0000
    call rrw                        # Data3, registre 6 : FFFFh
    .byte 6, 3
    .word D3
    call wr                         # Data0, registres 4 et 5 du canal 3 : des mots que rien n'interprète
    .byte 4, 3
    .word D0, 0x1234
    call wr
    .byte 4, 3
    .word D0 + 2, 0x5678
    call wr
    .byte 5, 3
    .word D0 + 2, 0x9ABC
    call rrw
    .byte 4, 3
    .word D0
    call rrw
    .byte 4, 3
    .word D0 + 2
    call rrw
    .byte 5, 3
    .word D0 + 2

# ---- 3. l'initialisation ------------------------------------------------------------------------------
    mov cx, 2                       # deux passes sous init1[0] = 03FFh
50: push cx
    xor si, si
51: mov ax, 0x03FF                  # init1 : 03FFh au canal 0, le canal << 6 ailleurs
    test si, si
    jz 52f
    mov ax, si
    shl ax, 6
52: push ax
    call ptrsi
    .byte 2
    pop ax
    mov dx, D1
    out dx, ax
    call ptrsi                      # init3 : le canal
    .byte 3
    mov ax, si
    mov dx, D1
    out dx, ax
    call ptrsi                      # init2 : le canal << 2
    .byte 2
    mov ax, si
    shl ax, 2
    mov dx, D2
    out dx, ax
    call ptrsi                      # init4 : le canal
    .byte 3
    mov ax, si
    mov dx, D2
    out dx, ax
    inc si
    cmp si, 32
    jb 51b
    pop cx
    loop 50b
    call wr                         # init1[0] ≠ 03FFh : les effets suivent désormais
    .byte 2, 0
    .word D1, 0x0000
    call wr                         # init1 : le mélange (canal 3), les filtres (5), la réverbération non liée (7)
    .byte 2, 3
    .word D1, 0x00C0
    call wr
    .byte 2, 5
    .word D1, 0x00A0
    call wr
    .byte 2, 7
    .word D1, 0x0000
    call wr                         # les gains (0Fh, 17h, 1Fh) et les retours (09h, 11h, 19h) des réflexions 0 à 2
    .byte 2, 0x0F
    .word D1, 0x00B0
    call wr
    .byte 2, 0x17
    .word D1, 0x00A0
    call wr
    .byte 2, 0x1F
    .word D1, 0x0090
    call wr
    .byte 2, 0x09
    .word D1, 0x0007
    call wr
    .byte 2, 0x11
    .word D1, 0x0006
    call wr
    .byte 2, 0x19
    .word D1, 0x0005
    call wr                         # init2 : la taille de la pièce au quartet Dh (Eh déborderait, PB-161)
    .byte 2, 0x14
    .word D2, 0x0D00
    call wr                         # les gains (07h, 0Fh, 17h) et les retours (01h, 09h, 11h) des réflexions 3 à 5
    .byte 2, 0x07
    .word D2, 0x0070
    call wr
    .byte 2, 0x0F
    .word D2, 0x0060
    call wr
    .byte 2, 0x17
    .word D2, 0x0050
    call wr
    .byte 2, 0x01
    .word D2, 0x0004
    call wr
    .byte 2, 0x09
    .word D2, 0x0003
    call wr
    .byte 2, 0x11
    .word D2, 0x0002
    call wr                         # l'amortissement (1Dh)
    .byte 2, 0x1D
    .word D2, 0x0080
    call wr                         # init3 : le retour du chorus (9), son délai (12), l'entrée des réflexions (1)
    .byte 3, 9
    .word D1, 0x0060
    call wr
    .byte 3, 12
    .word D1, 0x0400
    call wr
    .byte 3, 1
    .word D1, 0x00C0
    call wr                         # init4 : la profondeur du chorus (3), calculée avec le délai ; le retour des queues
    .byte 3, 3
    .word D2, 0x0040
    call wr
    .byte 3, 0x1F
    .word D2, 0x0080
    call wr                         # hwcf4 (le décalage droit du chorus) puis hwcf5 (sa vitesse) : le mot haut décide
    .byte 1, 9
    .word D1, 0x0200
    call wr
    .byte 1, 9
    .word D2, 0x0000
    call wr
    .byte 1, 10
    .word D1, 0x0BC4
    call wr
    .byte 1, 10
    .word D2, 0x0000
    call wr                         # hwcf1, hwcf2, hwcf3 = 4 (le son actif)
    .byte 1, 29
    .word D1, 0x0059
    call wr
    .byte 1, 30
    .word D1, 0x0020
    call wr
    .byte 1, 31
    .word D1, 0x0004
    call rrw                        # relus : les deux premiers mots de configuration, init2[14h], init1[3]
    .byte 1, 29
    .word D1
    call rrw
    .byte 1, 30
    .word D1
    call rrw
    .byte 2, 0x14
    .word D2
    call rrw
    .byte 2, 3
    .word D1

# ---- 4. la DRAM ---------------------------------------------------------------------------------------
    call wr                         # SMALW = 200000h (A20h le mot bas, A22h le haut) ; huit mots par SMLD (canal 26)
    .byte 1, 22
    .word D1, 0x0000
    call wr
    .byte 1, 22
    .word D2, 0x0020
    call ptrw
    .byte 1, 26
    mov cx, 8
    mov ax, 0x1357
    mov dx, D1
53: out dx, ax
    add ax, 0x2468
    loop 53b
    call wr                         # au-delà des 512 Ko : 280000h se replie en 200000h
    .byte 1, 22
    .word D1, 0x0000
    call wr
    .byte 1, 22
    .word D2, 0x0028
    call wr
    .byte 1, 26
    .word D1, 0xBEEF
    call wr                         # SMALR = 200000h, puis neuf lectures de SMLD
    .byte 1, 20
    .word D1, 0x0000
    call wr
    .byte 1, 20
    .word D2, 0x0020
    mov cx, 9
54: call rrw
    .byte 1, 26
    .word D1
    loop 54b
    call rrw                        # SMALR relu en A22h : le bit vide (8000h, dans le mot bas) tombe au
                                    #   décalage (PB-159)
    .byte 1, 20
    .word D2
    call wr                         # SMARW = 200100h ; deux mots par A22h, canal 26 : l'adresse croît sans masque
    .byte 1, 23
    .word D1, 0x0100
    call wr
    .byte 1, 23
    .word D2, 0x0020
    call wr
    .byte 1, 26
    .word D2, 0x4321
    call wr
    .byte 1, 26
    .word D2, 0x8765
    call ptrw                       # deux OCTETS par SMLD, en SMALW : 42h au port pair (le mot 0042h), 43h à
                                    #   l'impair (4300h)
    .byte 1, 26
    mov dx, D1
    mov al, 0x42
    out dx, al
    mov dx, D1 + 1
    mov al, 0x43
    out dx, al
    call rrw                        # SMARW et SMALW relus
    .byte 1, 23
    .word D1
    call rrw
    .byte 1, 23
    .word D2
    call rrw
    .byte 1, 22
    .word D1
    call wr                         # SMALR = 200100h : les deux mots de SMARW, relus
    .byte 1, 20
    .word D1, 0x0100
    call wr
    .byte 1, 20
    .word D2, 0x0020
    mov cx, 3
55: call rrw
    .byte 1, 26
    .word D1
    loop 55b
    call wr                         # SMALR = 200001h : les deux octets ont suivi SMALW (280001h, replié en 200001h)
    .byte 1, 20
    .word D1, 0x0001
    call wr
    .byte 1, 20
    .word D2, 0x0020
    mov cx, 3
56: call rrw
    .byte 1, 26
    .word D1
    loop 56b

# ---- 5. la ROM ----------------------------------------------------------------------------------------
    call wr                         # SMARR = 0 : les mots 0 et 1 de la ROM par SMRD (A22h, canal 26)
    .byte 1, 21
    .word D1, 0x0000
    call wr
    .byte 1, 21
    .word D2, 0x0000
    mov cx, 3
57: call rrw
    .byte 1, 26
    .word D2
    loop 57b
    call wr                         # SMARR = 7FFFEh : les deux derniers mots
    .byte 1, 21
    .word D1, 0xFFFE
    call wr
    .byte 1, 21
    .word D2, 0x0007
    mov cx, 3
58: call rrw
    .byte 1, 26
    .word D2
    loop 58b

# ---- 6. un échantillon et une note --------------------------------------------------------------------
    call wr                         # SMALW = 200200h ; une dent de scie de 2 048 mots
    .byte 1, 22
    .word D1, 0x0200
    call wr
    .byte 1, 22
    .word D2, 0x0020
    call ptrw
    .byte 1, 26
    mov cx, 2048
    mov ax, 0x8000
    mov dx, D1
59: out dx, ax
    add ax, 0x0101
    loop 59b
    mov al, 0                       # le canal 0
    call note
    call ticks
    .byte 3
    call ptrw                       # WC deux fois, sans écriture entre : le même mot (PB-158) ; puis après un tic
    .byte 1, 27
    call rd
    .word D2
    call rd
    .word D2
    call ticks
    .byte 1
    call rd
    .word D2
    call rv                         # CCCA (les deux mots), CPF et CVCF (le mot haut) du canal 0
    .byte 0
    .word D1
    call rv
    .byte 0
    .word D2
    call rv
    .byte 0
    .word D0 + 2
    call rv
    .byte 2
    .word D0 + 2
    call wv                         # le relâchement : DCYSUSV, bit 15
    .byte 5
    .word D1, 0x807F
    call ticks
    .byte 2

# ---- 7. les correctifs ; une note avec maintien, redéclenchée -----------------------------------------
    call wr                         # hwcf3 = 0 : le mot 2 perd son bit 40h...
    .byte 1, 31
    .word D1, 0x0000
    call rrw
    .byte 1, 30
    .word D1
    mov al, 1
    call note
    call rrw                        # ... que le moteur rallumé lui rend (hwcf3 = 4, PB-160)
    .byte 1, 30
    .word D1
    call wv                         # ATKHLDV avec maintien : l'enveloppe du volume redéclenchée
    .byte 4
    .word D2, 0x407F
    call wv3                        # PEFE : la hauteur et le filtre par l'enveloppe de modulation
    .byte 2
    .word 0x7F20
    call wv                         # ATKHLD avec maintien : l'enveloppe de modulation redéclenchée
    .byte 6
    .word D2, 0x407F
    call ticks
    .byte 3
    call wv                         # DCYSUSV coupe puis rallume le moteur, sans réécrire les enveloppes
    .byte 5
    .word D1, 0x0080
    call wv
    .byte 5
    .word D1, 0x7F7F
    call ticks
    .byte 3
    call rv
    .byte 2
    .word D0 + 2
    mov byte ptr [voix], 5          # le canal 5, intact : DCYSUSV = 80h, IP, CVCF et VTFT à zéro
    call wv
    .byte 5
    .word D1, 0x0080
    call wv3                        # IFATN = FF00h : ignoré (PB-160)
    .byte 1
    .word 0xFF00
    call rv
    .byte 1
    .word D3
    call wv3                        # IFATN = FF01h : écrit
    .byte 1
    .word 0xFF01
    call rv
    .byte 1
    .word D3

# ---- 8. un chorus extrême ; la réverbération liée -----------------------------------------------------
    call wr                         # le délai central 1FFFh, PUIS la profondeur FFh (calculée avec lui)
    .byte 3, 12
    .word D1, 0x1FFF
    call wr
    .byte 3, 3
    .word D2, 0x00FF
    call wr                         # hwcf4 = 1FFFFFh : le canal droit lit sous son tampon (PB-162)
    .byte 1, 9
    .word D1, 0xFFFF
    call wr
    .byte 1, 9
    .word D2, 0x001F
    call wr                         # hwcf5 = 4000h
    .byte 1, 10
    .word D1, 0x4000
    call wr
    .byte 1, 10
    .word D2, 0x0000
    call wr                         # init1[7] = 8474h : la réverbération liée ; ses deux queues, 16h puis 14h
    .byte 2, 7
    .word D1, 0x8474
    call wr
    .byte 2, 0x16
    .word D2, 0x0D00
    call wr
    .byte 2, 0x14
    .word D2, 0x0C00
    call ticks
    .byte 4

# ---- 9. la fin sur une note tenue ---------------------------------------------------------------------
    mov al, 2
    call note
    call ticks
    .byte 2
    call rrw
    .byte 1, 27
    .word D2
    mov ax, bx                      # la somme, en hexadécimal
    mov si, offset hex + 4
    mov cx, 4
15: dec si
    mov dl, al
    and dl, 0x0F
    add dl, '0'
    cmp dl, '9'
    jbe 16f
    add dl, 7
16: mov [si], dl
    shr ax, 4
    loop 15b
    mov dx, offset fin
    mov ah, 0x09
    int 0x21
    mov ax, 0x4C00
    int 0x21

# ---- les aides ----------------------------------------------------------------------------------------
# keep : AL relevé (stosb) et sommé dans BX ; keepw : AX, ses deux octets.
keep:
    stosb
    rol bx, 1
    add bl, al
    adc bh, 0
    ret
keepw:
    call keep
    mov al, ah
    jmp keep

# ptrw : le pointeur (E22h) ; en ligne : le registre, le canal.
ptrw:
    pop si
    lodsb
    shl al, 5
    mov ah, al
    lodsb
    or al, ah
    xor ah, ah
    mov dx, PTR
    out dx, ax
    push si
    ret

# ptrsi : le pointeur sur le registre en ligne et le canal SI (gardé).
ptrsi:
    pop dx
    xchg dx, si
    lodsb
    xchg dx, si
    push dx
    shl al, 5
    mov ah, al
    mov dx, si
    mov al, dl
    and al, 0x1F
    or al, ah
    xor ah, ah
    mov dx, PTR
    out dx, ax
    ret

# wr : un mot dans un registre ; en ligne : le registre, le canal, le port, la valeur.
wr:
    pop si
    lodsb
    shl al, 5
    mov ah, al
    lodsb
    or al, ah
    xor ah, ah
    mov dx, PTR
    out dx, ax
    lodsw
    mov dx, ax
    lodsw
    out dx, ax
    push si
    ret

# rrw : un mot relu ; en ligne : le registre, le canal, le port.
rrw:
    pop si
    lodsb
    shl al, 5
    mov ah, al
    lodsb
    or al, ah
    xor ah, ah
    mov dx, PTR
    out dx, ax
    lodsw
    mov dx, ax
    push si
    in ax, dx
    jmp keepw

# rd : un mot du port en ligne ; rdb : un octet.
rd:
    pop si
    lodsw
    push si
    mov dx, ax
    in ax, dx
    jmp keepw
rdb:
    pop si
    lodsw
    push si
    mov dx, ax
    in al, dx
    jmp keep

# wv : un mot dans un registre du canal [voix] ; en ligne : le registre, le port, la valeur.
wv:
    pop si
    lodsb
    shl al, 5
    or al, [voix]
    xor ah, ah
    mov dx, PTR
    out dx, ax
    lodsw
    mov dx, ax
    lodsw
    out dx, ax
    push si
    ret

# wv3 : de même en E20h ; en ligne : le registre, la valeur.
wv3:
    pop si
    lodsb
    shl al, 5
    or al, [voix]
    xor ah, ah
    mov dx, PTR
    out dx, ax
    lodsw
    mov dx, D3
    out dx, ax
    push si
    ret

# rv : un mot relu d'un registre du canal [voix] ; en ligne : le registre, le port.
rv:
    pop si
    lodsb
    shl al, 5
    or al, [voix]
    xor ah, ah
    mov dx, PTR
    out dx, ax
    lodsw
    mov dx, ax
    push si
    in ax, dx
    jmp keepw

# note : une note sur le canal AL, de l'échantillon en 200200h, en boucle de 200210h à 200A00h. Les registres dans
#   l'ordre des pilotes : le moteur coupé, les enveloppes, les LFO, la hauteur (IP avant IFATN : le correctif
#   d'Impulse Tracker ne mord pas), les cibles, les boucles, le départ, puis le moteur rallumé.
note:
    mov [voix], al
    call wv                         # DCYSUSV : le moteur coupé (bit 7)
    .byte 5
    .word D1, 0x0080
    call wv                         # ENVVOL, ENVVAL : sans délai
    .byte 4
    .word D1, 0x8000
    call wv
    .byte 6
    .word D1, 0x8000
    call wv                         # DCYSUS : la modulation, son palier et son déclin
    .byte 7
    .word D1, 0x7F7F
    call wv                         # ATKHLDV, ATKHLD : l'attaque 7Fh, sans maintien
    .byte 4
    .word D2, 0x7F7F
    call wv
    .byte 6
    .word D2, 0x7F7F
    call wv                         # LFO1VAL, LFO2VAL : sans délai
    .byte 5
    .word D2, 0x8000
    call wv
    .byte 7
    .word D2, 0x8000
    call wv3                        # IP : E000h, la hauteur sans décalage ; IFATN : le filtre ouvert, aucune
                                    #   atténuation
    .byte 0
    .word 0xE000
    call wv3
    .byte 1
    .word 0xFF00
    call wv3                        # PEFE, FMMOD, TREMFRQ, FM2FRQ2 : un peu de vibrato et de trémolo
    .byte 2
    .word 0x0000
    call wv3
    .byte 3
    .word 0x0820
    call wv3
    .byte 4
    .word 0x1030
    call wv3
    .byte 5
    .word 0x0840
    call wv                         # PTRX : la hauteur cible 4000h, l'envoi à la réverbération 40h ; CPF : 4000h
    .byte 1
    .word D0 + 2, 0x4000
    call wv
    .byte 1
    .word D0, 0x4000
    call wv
    .byte 0
    .word D0 + 2, 0x4000
    call wv                         # VTFT : les cibles ; CVCF : le volume courant nul, le filtre ouvert
    .byte 3
    .word D0 + 2, 0xFFFF
    call wv
    .byte 3
    .word D0, 0xFFFF
    call wv
    .byte 2
    .word D0 + 2, 0x0000
    call wv
    .byte 2
    .word D0, 0xFFFF
    call wv                         # PSST : le début de boucle 200210h, le panoramique 80h ; CSL : la fin
                                    #   200A00h, le chorus 60h
    .byte 6
    .word D0, 0x0210
    call wv
    .byte 6
    .word D0 + 2, 0x8020
    call wv
    .byte 7
    .word D0, 0x0A00
    call wv
    .byte 7
    .word D0 + 2, 0x6020
    call wv                         # CCCA : le départ 200200h, la résonance 2
    .byte 0
    .word D1, 0x0200
    call wv
    .byte 0
    .word D2, 0x2020
    call wv                         # DCYSUSV : le moteur rallumé (bit 7 à zéro), le palier et le déclin
    .byte 5
    .word D1, 0x7F7F
    ret

# reset : 226h à 1 puis 0, l'octet AAh relevé.
reset:
    mov dx, SB + 6
    mov al, 1
    out dx, al
    in al, dx
    in al, dx
    in al, dx
    xor al, al
    out dx, al
    call dsprn
    .byte 1
    ret

# dspw : AL au DSP, après le bit 7 de 22Ch à zéro.
dspw:
    push cx
    push ax
    mov dx, SB + 0xC
    mov cx, 0xFFFF
30: in al, dx
    test al, 0x80
    jz 31f
    loop 30b
31: pop ax
    out dx, al
    pop cx
    ret

# dsp : N octets en ligne, au DSP.
dsp:
    pop si
    lodsb
    xor ch, ch
    mov cl, al
32: lodsb
    call dspw
    loop 32b
    push si
    ret

# dspr : un octet du DSP, après le bit 7 de 22Eh à un ; relevé.
dspr:
    push cx
    mov dx, SB + 0xE
    mov cx, 0xFFFF
33: in al, dx
    test al, 0x80
    jnz 34f
    loop 33b
34: mov dx, SB + 0xA
    in al, dx
    pop cx
    jmp keep

# dsprn : N octets du DSP (N en ligne).
dsprn:
    pop si
    lodsb
    push si
    xor ch, ch
    mov cl, al
35: call dspr
    loop 35b
    ret

# ticks : N tics de l'horloge du BIOS (0040:006Ch), en ligne ; HLT entre deux interruptions.
ticks:
    pop si
    lodsb
    push si
    xor ah, ah
    mov cx, ax
    push ds
    mov ax, 0x40
    mov ds, ax
40: mov ax, [0x6C]
41: sti
    hlt
    cmp ax, [0x6C]
    je 41b
    loop 40b
    pop ds
    ret

voix: .byte 0
fin: .ascii "AWEBANC "
hex: .ascii "0000"
     .byte 13, 10
     .ascii "$"
