# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# SB16BANC.COM — le banc de la Sound Blaster 16 (G12.1, PLAN-G12.md).
#
# Un programme qui parle à la carte lui-même, à 220h (le DSP 4.05, le CT1745), 330h (le MPU-401) et par le 8237
# (le canal 1 en 8 bits, le canal 5 en 16 bits). Saisi dans DEBUG.EXE par KeyScript, écrit sur C:, lancé — sous
# boot-diff, qui compare chaque instruction : chaque octet que la carte rend est relu dans un registre (et sommé
# dans BX), donc vu par la trace ; ce qu'elle JOUE n'est vu que par la sonde du son (sound_hash). La somme
# s'affiche à la fin, pour les yeux.
#
# Machine attendue : l'ami486, la SB 16 à 220h, IRQ 7 (masquée ici : le banc relit 82h, 2xEh et 2xFh lui-même),
# DMA 1 et DMA 16 bits 5 ; DOS 5 sur le disque SCSI de G11. Dans l'ordre :
#   1. le DSP : reset, E1h (4.05), E3h (45 octets), E0h ; l'ASP : 08h (18h), 0Eh/0Fh, F9h, 01h (les octets avalés) ;
#      F2h et F3h, 82h, les deux acquits ;
#   2. le CT1745 : reset, relectures, 0Ah et sa troncature (PB-153), 01h (FFh), 3Bh = C0h (PB-152), 80h et 81h (le
#      dernier bit gagne, 0 ne change rien), la compatibilité Pro (22h), les basses, les aigus et le gain de sortie ;
#   3. le MPU-401 : FFh (l'ACK FEh, PB-154), 3Fh, des notes (l'empreinte MIDI), ACh (sans ACK) ;
#   4. le 8 bits par C0h-CFh (41h 44 100 Hz) : simple, automatique, signé, et le mode 01h (l'ADPCM, PB-149) ;
#   5. le 16 bits par B0h-BFh : mono, stéréo, signé et non signé, simple et automatique, D5h/D6h/D9h, le 8237 haut en
#      décrément, puis à la frontière de 128 Ko (la page reboucle), et son état relu (D0h, DAh, DEh) ;
#   6. la tonalité adverse : 41h FFFFh puis 40h F4h (83 333 Hz), un carré pleine échelle presque alterné en 16 bits
#      stéréo, dont les signes retombent sur ceux du FIR — qui y dépasse le gain unité : la conversion déborde
#      (PB-155) ;
#   7. l'entrée 16 bits (42h, B8h) ;
#   8. l'enregistrement stéréo 8 bits (C8h, mode 20h) au-delà de FFFEh (PB-151) : 20h fait avancer record_pos_read à
#      45 454 Hz, une sortie 16 bits tourne en même temps (buffer[0] non nul), et la fenêtre de 16 Ko couvre
#      l'indice FFFFh ; l'enregistrement est relu.
# JAMAIS 41h ou 42h à 0000h : l'oracle figerait (R9, PB-150).
#
# Construit par sb16banc.py (GNU as en mode 16 bits, ld en binaire plat à 100h) ; sb16banc.keys en est tiré.

.intel_syntax noprefix
.code16
.text
.globl _start

.set SB,     0x220
.set MPU,    0x330
.set BUF8,   0x1000          # 4 Ko : une dent de scie, en 8 bits
.set BUF16,  0x2000          # 8 Ko : 4 096 échantillons de 16 bits pseudo-aléatoires, pleine échelle
.set RECB,   0x4000          # 16 Ko : l'enregistrement
.set RES,    0x8000          # les relevés
.set HIGH,   0x1FFF          # le segment de l'essai de la frontière de 128 Ko (l'adresse physique 1FFF0h)

_start:
    cld
    xor bx, bx
    mov di, RES
    in al, 0x21                     # l'IRQ 7 masquée
    mov [oldmask], al
    or al, 0x80
    out 0x21, al
    call fill

# ---- 1. le DSP et l'ASP ---------------------------------------------------------------------------------
    call reset
    call dsp                        # la version, 4.05
    .byte 1, 0xE1
    call dsprn
    .byte 2
    call dsp                        # le copyright, 44 caractères puis 0
    .byte 1, 0xE3
    call dsprn
    .byte 45
    call dsp                        # l'identification : ~AAh
    .byte 2, 0xE0, 0xAA
    call dsprn
    .byte 1
    call dsp                        # 08h : 18h (SB16) ; 0Eh pose le registre 5 de l'ASP, 0Fh le relit
    .byte 2, 0x08, 0x00
    call dsprn
    .byte 1
    call dsp
    .byte 3, 0x0E, 0x05, 0x77
    call dsp
    .byte 2, 0x0F, 0x05
    call dsprn
    .byte 1
    call dsp                        # F9h : FFh, 07h, 38h, 00h
    .byte 8, 0xF9, 0x0E, 0xF9, 0x0F, 0xF9, 0x37, 0xF9, 0x42
    call dsprn
    .byte 4
    call dsp                        # 01h 02h 00h : un 0 tout de suite ; trois octets avalés, puis un 0
    .byte 6, 0x01, 0x02, 0x00, 0x11, 0x22, 0x33
    call dsprn
    .byte 2
    call irqs                       # aucune IRQ : 82h vaut 0 (le | 4000h perdu, PB-153)
    call dsp                        # F2h, l'IRQ 8 bits : 82h = 1, puis 2xEh l'acquitte
    .byte 1, 0xF2
    call irqs
    call dsp                        # F3h, l'IRQ 16 bits : 82h = 2, puis 2xFh l'acquitte
    .byte 1, 0xF3
    call mixr
    .byte 1, 0x82
    call inp
    .byte 1
    .word SB + 0xF
    call mixr
    .byte 1, 0x82

# ---- 2. le CT1745 -------------------------------------------------------------------------------------
    call mixw                       # reset
    .byte 1, 0x00, 0x00
    call mixr                       # les volumes (F8h), les sélecteurs, les gains, les basses et les aigus
    .byte 22, 0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x3B, 0x3C, 0x3D, 0x3E, 0x3F
    .byte 0x40, 0x41, 0x42, 0x44, 0x46, 0x01
    call mixr                       # 0Ah après le reset : (0 - 10) / 3 = -3, FDh (PB-153) ; 04h, 22h, 26h, 28h, 2Eh
    .byte 6, 0x0A, 0x04, 0x22, 0x26, 0x28, 0x2E
    call mixw                       # 0Ah = 03h : 3Ah = 19, relu 3 ; 0Ah = 60h : 3Ah tronqué à 42, relu 10
    .byte 1, 0x0A, 0x03
    call mixr
    .byte 2, 0x0A, 0x3A
    call mixw
    .byte 1, 0x0A, 0x60
    call mixr
    .byte 2, 0x0A, 0x3A
    call mixw                       # 3Bh = C0h (PB-152 : l'indice hors de la table, l'oracle survit), puis 30h
    .byte 2, 0x3B, 0xC0, 0x30, 0xF0
    call mixr
    .byte 3, 0x3B, 0x30, 0x48
    call mixr                       # 80h : IRQ 7 ; 81h : DMA 1 et DMA 16 bits 5
    .byte 2, 0x80, 0x81
    call mixw                       # 80h = 0Ah : l'IRQ 5 puis la 10, la dernière gagne (PB-153)
    .byte 1, 0x80, 0x0A
    call mixr
    .byte 1, 0x80
    call mixw                       # 80h = 00h : rien ne change ; puis 80h = 04h, l'IRQ 7
    .byte 1, 0x80, 0x00
    call mixr
    .byte 1, 0x80
    call mixw
    .byte 2, 0x80, 0x04, 0x81, 0x00
    call mixr
    .byte 2, 0x80, 0x81
    call mixw                       # 81h = A2h : les DMA 1, 5 et 7, le dernier gagne ; puis 22h (DMA 1 et 5)
    .byte 1, 0x81, 0xA2
    call mixr
    .byte 1, 0x81
    call mixw
    .byte 1, 0x81, 0x22
    call mixr
    .byte 1, 0x81
    call mixw                       # la compatibilité Pro : 22h = DDh, copié en 30h et 31h
    .byte 1, 0x22, 0xDD
    call mixr
    .byte 3, 0x22, 0x30, 0x31
    call mixw                       # le maître et les voix au maximum, les aigus +, les basses -, le gain de sortie x2
    .byte 10, 0x30, 0xF8, 0x31, 0xF8, 0x32, 0xF8, 0x33, 0xF8, 0x44, 0xA0, 0x45, 0xC0, 0x46, 0x30, 0x47, 0x50
    .byte 0x41, 0x40, 0x42, 0x40
    call mixr
    .byte 6, 0x44, 0x45, 0x46, 0x47, 0x41, 0x42

# ---- 3. le MPU-401 ------------------------------------------------------------------------------------
    call outp                       # FFh : l'ACK FEh ; l'état, la donnée
    .byte 1
    .word MPU + 1
    .byte 0xFF
    call inp
    .byte 3
    .word MPU + 1, MPU, MPU + 1
    call outp                       # 3Fh : le mode UART, l'ACK
    .byte 1
    .word MPU + 1
    .byte 0x3F
    call inp
    .byte 2
    .word MPU, MPU + 1
    call outp                       # des notes : l'empreinte MIDI
    .byte 9
    .word MPU
    .byte 0x90
    .word MPU
    .byte 0x3C
    .word MPU
    .byte 0x7F
    .word MPU
    .byte 0x90
    .word MPU
    .byte 0x40
    .word MPU
    .byte 0x60
    .word MPU
    .byte 0x80
    .word MPU
    .byte 0x3C
    .word MPU
    .byte 0x00
    call outp                       # ACh : ignorée, sans ACK ; la donnée relue est l'ACK périmé (PB-154)
    .byte 1
    .word MPU + 1
    .byte 0xAC
    call inp
    .byte 2
    .word MPU + 1, MPU

# ---- 4. le 8 bits par C0h-CFh --------------------------------------------------------------------------
    call dsp                        # 41h : 44 100 Hz (l'octet fort d'abord) ; haut-parleur
    .byte 4, 0x41, 0xAC, 0x44, 0xD1
    call dma8                       # C0h : 8 bits, mono non signé, simple, 1 024 octets
    .byte 0x49
    .word BUF8, 1023
    call dsp
    .byte 4, 0xC0, 0x00, 0xFF, 0x03
    call ticks
    .byte 3
    call irqs
    call dma8                       # C6h : automatique, mono signé, blocs de 512 ; DAh au bout de deux tics
    .byte 0x59
    .word BUF8, 4095
    call dsp
    .byte 4, 0xC6, 0x10, 0xFF, 0x01
    call ticks
    .byte 2
    call irqs
    call dsp
    .byte 1, 0xDA
    call ticks
    .byte 1
    call irqs
    call dma8                       # C0h en mode 01h : le chemin de l'ADPCM 4 bits, sur un état périmé (PB-149)
    .byte 0x49
    .word BUF8 + 2048, 511
    call dsp
    .byte 4, 0xC0, 0x01, 0xFF, 0x00
    call ticks
    .byte 2
    call irqs

# ---- 5. le 16 bits par B0h-BFh -------------------------------------------------------------------------
    call dma16                      # B0h : mono signé, simple, 2 048 échantillons
    .byte 0x49
    .word 0, BUF16, 2047
    call dsp
    .byte 4, 0xB0, 0x10, 0xFF, 0x07
    call ticks
    .byte 3
    call irqs
    call dma16                      # B6h : automatique, stéréo signé, blocs de 1 024 ; D5h, D6h, puis D9h
    .byte 0x59
    .word 0, BUF16, 4095
    call dsp
    .byte 4, 0xB6, 0x30, 0xFF, 0x03
    call ticks
    .byte 2
    call irqs
    call dsp
    .byte 1, 0xD5
    call ticks
    .byte 1
    call dsp
    .byte 1, 0xD6
    call ticks
    .byte 1
    call dsp
    .byte 1, 0xD9
    call ticks
    .byte 2
    call irqs
    call dma16                      # B0h : stéréo non signé, le 8237 en décrément depuis la fin du tampon
    .byte 0x69
    .word 0, BUF16 + 8190, 2047
    call dsp
    .byte 4, 0xB0, 0x20, 0xFF, 0x07
    call ticks
    .byte 3
    call irqs
    call dma16                      # B0h : 32 mots depuis 1FFF0h ; la page de 128 Ko reboucle en 00000h
    .byte 0x49
    .word HIGH, 0, 31
    call dsp
    .byte 4, 0xB0, 0x10, 0x1F, 0x00
    call ticks
    .byte 1
    call irqs
    call inp                        # l'état du 8237 haut (D0h), DAh et DEh : le dernier octet écrit (PB-157)
    .byte 3
    .word 0xD0, 0xDA, 0xDE

# ---- 6. la tonalité adverse ----------------------------------------------------------------------------
    call carre                      # BUF16 : le carré presque alterné, dont les signes retombent sur ceux du FIR
    call dsp                        # 41h FFFFh : 65 535 Hz, le FIR au-delà du gain unité
    .byte 3, 0x41, 0xFF, 0xFF
    call dma16
    .byte 0x59
    .word 0, BUF16, 4095
    call dsp
    .byte 4, 0xB6, 0x30, 0xFF, 0x07
    call ticks
    .byte 4
    call dsp                        # 40h F4h : 83 333 Hz, Σ|coef| = 3
    .byte 2, 0x40, 0xF4
    call ticks
    .byte 4
    call dsp
    .byte 1, 0xD9
    call ticks
    .byte 2
    call irqs

# ---- 7. l'entrée 16 bits -------------------------------------------------------------------------------
    call dsp                        # 42h : 22 050 Hz ; B8h : mono non signé, simple, 512 échantillons
    .byte 3, 0x42, 0x56, 0x22
    call dma16
    .byte 0x45
    .word 0, RECB, 511
    call dsp
    .byte 4, 0xB8, 0x00, 0xFF, 0x01
    call ticks
    .byte 2
    call irqs
    mov si, RECB
    mov cx, 1024
    call sum

# ---- 8. l'enregistrement stéréo au-delà de FFFEh -------------------------------------------------------
    call reset                      # record_pos_read = 0
    call dsp                        # 41h 22 050 Hz ; B6h : une sortie 16 bits automatique, qui tourne
    .byte 3, 0x41, 0x56, 0x22
    call dma16
    .byte 0x59
    .word 0, BUF16, 4095
    call dsp
    .byte 4, 0xB6, 0x10, 0xFF, 0x0F
    call dsp                        # 20h : l'entrée directe, record_pos_read avance à 45 454 Hz
    .byte 1, 0x20
    call dsprn
    .byte 1
    call ticks                      # onze tics (0,6 s) : record_pos_read vers 27 000 couples
    .byte 11
    call dma8                       # C8h : 8 bits stéréo non signé, simple, 16 384 octets (8 192 couples)
    .byte 0x45
    .word RECB, 16383
    call dsp
    .byte 4, 0xC8, 0x20, 0xFF, 0x3F
    call ticks
    .byte 6
    call irqs
    call dsp
    .byte 1, 0xD9
    call ticks
    .byte 1
    call irqs
    mov si, RECB                    # l'enregistrement relu : l'octet de l'indice FFFFh est buffer[0] (PB-151)
    mov cx, 16384
    call sum
    call reset

# ---- la fin -------------------------------------------------------------------------------------------
    mov al, [oldmask]
    out 0x21, al
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
# keep : AL relevé (stosb) et sommé dans BX.
keep:
    stosb
    rol bx, 1
    add bl, al
    adc bh, 0
    ret

# sum : CX octets en DS:SI, sommés.
sum:
    lodsb
    call keep
    loop sum
    ret

# carre : BUF16, 2 048 trames stéréo d'un carré pleine échelle dont la phase avance de 32 000 sur 65 536 par trame :
#   une période de 2,05 échantillons, presque une alternance. Rééchantillonné à 48 kHz, il retombe par endroits sur
#   les signes des coefficients du FIR, et la sortie du filtre passe 2^31 / 32 767 — de 1,34 fois à 65 535 Hz, de
#   1,5 fois à 83 333 Hz (mesuré sur un modèle du filtre) : la conversion déborde (PB-155). Une salve signée au
#   hasard n'y arrive pas.
carre:
    mov si, BUF16
    mov cx, 2048
    xor dx, dx
24: mov ax, 0x7FFF
    test dh, 0x80
    jz 25f
    mov ax, 0x8000
25: mov [si], ax
    mov [si + 2], ax
    add si, 4
    add dx, 32000
    loop 24b
    ret

# fill : BUF8, une dent de scie ; BUF16, 4 096 mots pleine échelle, signés au hasard (un LFSR de 16 bits) ; les
#   32 octets de 1FFF:0000, un motif.
fill:
    mov si, BUF8
    mov cx, 4096
    xor al, al
20: mov [si], al
    add al, 7
    inc si
    loop 20b
    mov si, BUF16
    mov cx, 4096
    mov dx, 0xACE1
21: mov ax, dx
    shr ax, 1
    jnc 22f
    xor ax, 0xB400
22: mov dx, ax
    mov ax, 0x7FFF
    test dl, 1
    jz 23f
    mov ax, 0x8000
23: mov [si], ax
    add si, 2
    loop 21b
    push es
    mov ax, HIGH
    mov es, ax
    push di
    xor di, di
    mov cx, 32
    mov al, 0x5A
24: stosb
    add al, 0x13
    loop 24b
    pop di
    pop es
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

# dspw : AL au DSP, après le bit 7 de 22Ch à zéro (au plus 65 535 lectures).
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

# mixw : N paires (index, valeur) en ligne, au CT1745.
mixw:
    pop si
    lodsb
    xor ch, ch
    mov cl, al
36: mov dx, SB + 4
    lodsb
    out dx, al
    inc dx
    lodsb
    out dx, al
    loop 36b
    push si
    ret

# mixr : N index en ligne, chacun relu.
mixr:
    pop si
    lodsb
    xor ch, ch
    mov cl, al
37: mov dx, SB + 4
    lodsb
    out dx, al
    inc dx
    in al, dx
    call keep
    loop 37b
    push si
    ret

# inp : N ports (des mots) en ligne, chacun lu et relevé.
inp:
    pop si
    lodsb
    xor ch, ch
    mov cl, al
38: lodsw
    mov dx, ax
    in al, dx
    call keep
    loop 38b
    push si
    ret

# outp : N paires (port en mot, octet) en ligne.
outp:
    pop si
    lodsb
    xor ch, ch
    mov cl, al
39: lodsw
    mov dx, ax
    lodsb
    out dx, al
    loop 39b
    push si
    ret

# irqs : 82h, puis 2xEh (l'acquit 8 bits) et 2xFh (l'acquit 16 bits), relevés.
irqs:
    call mixr
    .byte 1, 0x82
    call inp
    .byte 2
    .word SB + 0xE, SB + 0xF
    ret

# ticks : N tics de l'horloge du BIOS (0040:006Ch, 18,2 Hz), en ligne ; HLT entre deux interruptions.
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

# phys : l'adresse physique de DS:AX dans EAX.
phys:
    push edx
    movzx eax, ax
    xor edx, edx
    mov dx, ds
    shl edx, 4
    add eax, edx
    pop edx
    ret

# dma8 : le canal 1 ; en ligne : le mode, le déplacement, la longueur - 1.
dma8:
    pop si
    mov al, 0x05
    out 0x0A, al                    # masquer le canal 1
    out 0x0C, al                    # la bascule à zéro
    lodsb
    out 0x0B, al
    lodsw
    call phys
    out 0x02, al
    mov al, ah
    out 0x02, al
    shr eax, 16
    out 0x83, al                    # la page du canal 1
    lodsw
    out 0x03, al
    mov al, ah
    out 0x03, al
    mov al, 0x01
    out 0x0A, al                    # démasquer
    push si
    ret

# dma16 : le canal 5 ; en ligne : le mode, le segment (0 : celui du banc), le déplacement, la longueur - 1 en mots.
#   L'adresse est rangée en mots dans sa page de 128 Ko (C4h) ; la page, bits 17 à 23, en 8Bh.
dma16:
    pop si
    mov al, 0x05
    out 0xD4, al                    # masquer le canal 5
    out 0xD8, al                    # la bascule à zéro
    lodsb
    out 0xD6, al
    lodsw                           # le segment
    mov dx, ax
    lodsw                           # le déplacement
    test dx, dx
    jz 42f
    push ds
    mov ds, dx
    call phys
    pop ds
    jmp 43f
42: call phys
43: mov edx, eax
    shr eax, 1
    out 0xC4, al
    mov al, ah
    out 0xC4, al
    shr edx, 16
    mov al, dl
    and al, 0xFE
    out 0x8B, al                    # la page du canal 5
    lodsw
    out 0xC6, al
    mov al, ah
    out 0xC6, al
    mov al, 0x01
    out 0xD4, al                    # démasquer
    push si
    ret

oldmask: .byte 0
fin: .ascii "SB16BANC "
hex: .ascii "0000"
     .byte 13, 10
     .ascii "$"
