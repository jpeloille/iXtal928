# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# AHABANC.COM — le banc de l'Adaptec AHA-1542C et de ses disques SCSI (G11.2, PLAN-G11.md).
#
# Un programme qui parle à la carte lui-même, aux ports 334h-337h, comme un pilote ASPI : les commandes
# d'hôte, la mailbox normale (CCB construits dans son segment), les commandes BIOS 03h. Saisi dans DEBUG.EXE
# par KeyScript, écrit sur C:, lancé — sous boot-diff, qui compare chaque instruction : chaque octet que la
# carte rend ou écrit en mémoire est relu dans un registre (et sommé dans BX), donc vu par la trace. La
# somme s'affiche à la fin, pour les yeux.
#
# Machine attendue : l'ami486, la carte à 334h (IRQ 10, masquée ici), scsic.img à l'ID 0 (DOS 5, lu
# seulement), un disque vierge de 20 Mio à l'ID 1 (écrit). L'ORDRE COMPTE (PLAN-G11.md, G11.2) :
#   1. les commandes d'hôte, dont une invalide ; MAILBOX INIT ;
#   2. des CCB de mailbox — ccb.addr pointe alors dans ce segment, et les écritures périmées des
#      commandes BIOS 03h (PB-137, PB-138) y restent visibles ;
#   3. les commandes BIOS 03h (sous-fonctions 00h, 01h, 05h, 08h, 09h, 15h, 02h), toutes sur des ID
#      présents, après une commande réussie ;
#   4. ABORT (PB-139), 22h sur la moitié haute de l'EEPROM (PB-136), relue par 23h.
# JAMAIS ce qui fait tomber l'oracle : les sept commandes de :679, un CCB d'opcode 1, une CDB plus longue que
# son groupe, 15h sur un ID vide ou non disque, 02h/03h/04h sur un ID vide ou après un échec ; ni une phase
# de longueur nulle (PB-132 : le bus perdu). Les DATA IN au moins aussi longs que la cible, les DATA OUT égaux.
#
# Construit par ahabanc.py (GNU as en mode 16 bits, ld en binaire plat à 100h) ; ahabanc.keys en est tiré.

.intel_syntax noprefix
.code16
.text
.globl _start

.set BASE,   0x334
.set CCB,    0x0E00          # le CCB (64 octets)
.set MBOX,   0x0E40          # deux MBO puis deux MBI (16 octets)
.set SGL,    0x0E50          # la liste de dispersion (12 octets)
.set SMALL,  0x0E60          # les quatre octets des sous-fonctions 08h et 15h
.set CH2A,   0x0E80          # 64 octets écrits par 1Ah
.set CH2B,   0x0EC0          # 64 octets relus par 1Bh
.set BUF,    0x1000          # 4 Ko
.set BUF2,   0x2000          # 4 Ko
.set RES,    0x3000          # les relevés

_start:
    cld
    xor bx, bx
    mov di, RES
    in al, 0xA1                     # l'IRQ 10 masquée : le banc lit l'ISR lui-même
    mov [oldmask], al
    or al, 0x04
    out 0xA1, al

# ---- 1. les registres et les commandes d'hôte ------------------------------------------------------
    call regs                       # état, ISR, et quatre lectures de 337h (« ADAP »)
    mov dx, BASE + 3
    mov cx, 4
1:  in al, dx
    call keep
    loop 1b
    call cmd                        # ADAPTER INQUIRY
    .byte 1, 0x04
    call res
    .byte 4
    call cmd                        # RETURN CONFIG DATA
    .byte 1, 0x0B
    call res
    .byte 3
    call cmd                        # INQUIRE INSTALLED DEVICES
    .byte 1, 0x0A
    call res
    .byte 8
    call cmd                        # RETURN SETUP DATA, 44 octets (PB-140)
    .byte 2, 0x0D, 44
    call res
    .byte 44
    call cmd                        # ECHO
    .byte 2, 0x1F, 0x5A
    call res
    .byte 1
    call cmd                        # RETURN EEPROM DATA, les 32 premiers octets
    .byte 4, 0x23, 0, 32, 0
    call res
    .byte 32
    call cmd                        # GET EXT BIOS INFO
    .byte 1, 0x28
    call res
    .byte 2
    call cmd                        # SET SELECTION TIMEOUT
    .byte 5, 0x06, 1, 0, 0x00, 0xFA
    call hacc
    call cmd                        # BUS ON, BUS OFF, AT BUS SPEED, MBO IRQ ENA
    .byte 2, 0x07, 9
    call hacc
    call cmd
    .byte 2, 0x08, 4
    call hacc
    call cmd
    .byte 2, 0x09, 0x33
    call hacc
    call cmd
    .byte 2, 0x05, 0
    call hacc
    call cmd                        # NOP
    .byte 1, 0x00
    call hacc
    call cmd                        # une commande invalide (40h) : INVDCMD, HACC
    .byte 1, 0x40
    call hacc
    mov ax, CH2A                    # WRITE CHANNEL 2 BUF, puis READ CHANNEL 2 BUF
    mov cx, 64
    mov si, ax
2:  mov [si], cl
    inc si
    loop 2b
    call phys
    mov [p_1a + 2], al
    mov [p_1a + 1], ah
    shr eax, 16
    mov [p_1a], al
    call cmd
    .byte 4, 0x1A
p_1a: .byte 0, 0, 0
    call hacc
    mov ax, CH2B
    call phys
    mov [p_1b + 2], al
    mov [p_1b + 1], ah
    shr eax, 16
    mov [p_1b], al
    call cmd
    .byte 4, 0x1B
p_1b: .byte 0, 0, 0
    call hacc
    mov si, CH2B
    mov cx, 64
    call sum
    push ds                         # les interrupteurs en 3F7Eh et 3F7Fh de la fenêtre de ROM
    mov ax, 0xDC00
    mov ds, ax
    mov al, [0x3F7E]
    mov ah, [0x3F7F]
    pop ds
    call keep
    mov al, ah
    call keep
    mov ax, MBOX                    # MAILBOX INIT : deux MBO, deux MBI
    call phys
    mov [p_mb + 3], al
    mov [p_mb + 2], ah
    shr eax, 16
    mov [p_mb + 1], al
    call cmd
    .byte 5, 0x01
p_mb: .byte 2, 0, 0, 0
    call hacc

# ---- 2. les CCB de mailbox ---------------------------------------------------------------------------
# ccb : opcode, ID/LUN, longueur de CDB, octet de sense, longueur des données (3, grand-boutien), l'offset
#   des données (FFFFh : l'adresse physique suit, 3 octets), le nombre d'octets de CDB écrits, la CDB.
    call ccb                        # INQUIRY, ID 0
    .byte 0, 0x00, 6, 0, 0, 0, 36
    .word BUF
    .byte 6, 0x12, 0, 0, 0, 36, 0
    call ccb                        # INQUIRY EVPD, page 80h : les données standard (PB-131)
    .byte 0, 0x00, 6, 0, 0, 0, 96
    .word BUF
    .byte 6, 0x12, 1, 0x80, 0, 96, 0
    call ccb                        # TEST UNIT READY, ID 1
    .byte 0, 0x20, 6, 0, 0, 0, 0
    .word BUF
    .byte 6, 0, 0, 0, 0, 0, 0
    call ccb                        # READ CAPACITY, ID 1 (PB-129 : 40 960)
    .byte 0, 0x20, 10, 0, 0, 0, 8
    .word BUF
    .byte 10, 0x25, 0, 0, 0, 0, 0, 0, 0, 0, 0
    call ccb                        # READ(10), ID 0, LBA 0, huit secteurs
    .byte 0, 0x00, 10, 0, 0, 0x10, 0
    .word BUF
    .byte 10, 0x28, 0, 0, 0, 0, 0, 0, 0, 8, 0
    mov si, BUF
    mov cx, 4096
    call sum
    mov si, BUF2                    # le motif, puis WRITE(10), ID 1, LBA 100, huit secteurs
    mov cx, 4096
3:  mov ax, cx
    xor al, ah
    mov [si], al
    inc si
    loop 3b
    call ccb
    .byte 0, 0x20, 10, 0, 0, 0x10, 0
    .word BUF2
    .byte 10, 0x2A, 0, 0, 0, 0, 100, 0, 0, 8, 0
    call ccb                        # READ(10), ID 1, LBA 100 : la relecture
    .byte 0, 0x20, 10, 0, 0, 0x10, 0
    .word BUF
    .byte 10, 0x28, 0, 0, 0, 0, 100, 0, 0, 8, 0
    mov si, BUF
    mov cx, 4096
    call sum
    call ccb                        # WRITE(10), ID 1, LBA 200, un secteur ; puis le LBA 40 960 (PB-129)
    .byte 0, 0x20, 10, 0, 0, 0x02, 0
    .word BUF2 + 0x200
    .byte 10, 0x2A, 0, 0, 0, 0, 200, 0, 0, 1, 0
    call ccb
    .byte 0, 0x20, 10, 0, 0, 0x02, 0
    .word BUF
    .byte 10, 0x28, 0, 0, 0, 0xA0, 0x00, 0, 0, 1, 0
    mov si, BUF
    mov cx, 512
    call sum
    call ccb                        # READ(6), ID 0, LBA 5 ; WRITE(6), ID 1, LBA 6
    .byte 0, 0x00, 6, 0, 0, 0x02, 0
    .word BUF
    .byte 6, 0x08, 0, 0, 5, 1, 0
    call ccb
    .byte 0, 0x20, 6, 0, 0, 0x02, 0
    .word BUF
    .byte 6, 0x0A, 0, 0, 6, 1, 0
    call ccb                        # MODE SENSE(6), page 3Fh, 255 (PB-130 : 170 octets rendus)
    .byte 0, 0x20, 6, 0, 0, 0, 255
    .word BUF
    .byte 6, 0x1A, 0, 0x3F, 0, 255, 0
    call ccb                        # MODE SENSE(6), page 00h, 36 (24 rendus) ; page 04h, 4
    .byte 0, 0x20, 6, 0, 0, 0, 36
    .word BUF
    .byte 6, 0x1A, 0, 0x00, 0, 36, 0
    call ccb
    .byte 0, 0x20, 6, 0, 0, 0, 4
    .word BUF
    .byte 6, 0x1A, 0, 0x04, 0, 4, 0
    call ccb                        # MODE SELECT(6), 12 octets sortants
    .byte 0, 0x28, 6, 0, 0, 0, 12
    .word BUF2
    .byte 6, 0x15, 0x10, 0, 0, 12, 0
    call ccb                        # un code inconnu, sans sense (octet 3 = 1) : 05/25h retenu
    .byte 0, 0x20, 6, 1, 0, 0, 0
    .word BUF
    .byte 6, 0xC0, 0, 0, 0, 0, 0
    call ccb                        # TEST UNIT READY sans sense, puis REQUEST SENSE : 05/25h (PB-131)
    .byte 0, 0x20, 6, 1, 0, 0, 0
    .word BUF
    .byte 6, 0, 0, 0, 0, 0, 0
    call ccb
    .byte 0, 0x20, 6, 1, 0, 0, 18
    .word BUF
    .byte 6, 0x03, 0, 0, 0, 18, 0
    call ccb                        # INQUIRY, LUN 1 dans le CCB seul (PB-133), puis dans la CDB (refusé)
    .byte 0, 0x21, 6, 0, 0, 0, 36
    .word BUF
    .byte 6, 0x12, 0, 0, 0, 36, 0
    call ccb
    .byte 0, 0x20, 6, 0, 0, 0, 36
    .word BUF
    .byte 6, 0x12, 0x20, 0, 0, 36, 0
    call ccb                        # l'ID 5, vide : sélection ratée (statut d'hôte 11h)
    .byte 0, 0xA0, 6, 0, 0, 0, 0
    .word BUF
    .byte 6, 0, 0, 0, 0, 0, 0
    call ccb                        # READ(10) déclaré sur 9 octets : le dixième, un zéro (PB-143)
    .byte 0, 0x00, 9, 0, 0, 0x02, 0
    .word BUF
    .byte 9, 0x28, 0, 0, 0, 0, 7, 0, 0, 1
    call ccb                        # une CDB de longueur 0 : TEST UNIT READY (PB-143)
    .byte 0, 0x00, 0, 0, 0, 0, 0
    .word BUF
    .byte 0
    call ccb                        # FORMAT UNIT, SEND DIAGNOSTIC, RESERVE, RELEASE, SEEK(10), START STOP,
    .byte 0, 0x20, 6, 0, 0, 0, 0    #   VERIFY(10), REZERO, PREVENT
    .word BUF
    .byte 6, 0x04, 0, 0, 0, 0, 0
    call ccb
    .byte 0, 0x20, 6, 0, 0, 0, 0
    .word BUF
    .byte 6, 0x1D, 0x04, 0, 0, 0, 0
    call ccb
    .byte 0, 0x20, 6, 0, 0, 0, 0
    .word BUF
    .byte 6, 0x16, 0, 0, 0, 0, 0
    call ccb
    .byte 0, 0x20, 6, 0, 0, 0, 0
    .word BUF
    .byte 6, 0x17, 0, 0, 0, 0, 0
    call ccb
    .byte 0, 0x20, 10, 0, 0, 0, 0
    .word BUF
    .byte 10, 0x2B, 0, 0, 0, 0, 50, 0, 0, 0, 0
    call ccb
    .byte 0, 0x20, 6, 0, 0, 0, 0
    .word BUF
    .byte 6, 0x1B, 0, 0, 0, 1, 0
    call ccb
    .byte 0, 0x20, 10, 0, 0, 0, 0
    .word BUF
    .byte 10, 0x2F, 0, 0, 0, 0, 50, 0, 0, 4, 0
    call ccb
    .byte 0, 0x20, 6, 0, 0, 0, 0
    .word BUF
    .byte 6, 0x01, 0, 0, 0, 0, 0
    call ccb
    .byte 0, 0x20, 6, 0, 0, 0, 0
    .word BUF
    .byte 6, 0x1E, 0, 0, 0, 1, 0
    call ccb                        # READ(10) avec résidu (opcode 3) : 1 024 annoncés, 512 lus
    .byte 3, 0x00, 10, 0, 0, 0x04, 0
    .word BUF
    .byte 10, 0x28, 0, 0, 0, 0, 9, 0, 0, 1, 0
    mov ax, BUF                     # la dispersion avec résidu (opcode 4) : 256 + 512 annoncés, 512 lus
    call phys
    mov si, SGL
    mov word ptr [si], 0
    mov byte ptr [si + 2], 0
    mov [si + 5], al
    mov [si + 4], ah
    shr eax, 16
    mov [si + 3], al
    mov ax, BUF + 0x100
    call phys
    mov word ptr [si + 6], 0x0200
    mov byte ptr [si + 8], 0
    mov [si + 11], al
    mov [si + 10], ah
    shr eax, 16
    mov [si + 9], al
    mov byte ptr [si + 1], 1
    call ccb
    .byte 4, 0x00, 10, 0, 0, 0, 12
    .word SGL
    .byte 10, 0x28, 0, 0, 0, 0, 11, 0, 0, 1, 0
    mov si, BUF
    mov cx, 768
    call sum
    call ccb                        # READ(10) de 600 secteurs à 1 Mo : data_in déborde dans data_out,
    .byte 0, 0x00, 10, 0, 0x04, 0xB0, 0x00  # sans dommage (décision n° 9)
    .word 0xFFFF
    .byte 0x10, 0x00, 0x00
    .byte 10, 0x28, 0, 0, 0, 0, 0, 0, 0x02, 0x58, 0

# ---- 3. les commandes BIOS 03h (ccb.addr pointe dans ce segment) -------------------------------------
    mov ax, SMALL
    call phys
    mov [p_b08 + 9], al
    mov [p_b08 + 8], ah
    mov [p_b15 + 9], al
    mov [p_b15 + 8], ah
    shr eax, 16
    mov [p_b08 + 7], al
    mov [p_b15 + 7], al
    call bios                       # 00h, 01h : DF, 0
    .byte 0x00, 0, 0, 0, 0, 0, 0, 0, 0, 0
    call bios
    .byte 0x01, 0, 0, 0, 0, 0, 0, 0, 0, 0
    call bios                       # 08h, ID 1 : la capacité, et l'achèvement du CCB périmé (PB-138)
p_b08: .byte 0x08, 1, 0, 0, 0, 0, 0, 0, 0, 0
    call small
    call bios                       # 09h : DF, 0 ; 05h : refusée (INVDCMD)
    .byte 0x09, 1, 0, 0, 0, 0, 0, 0, 0, 0
    call bios
    .byte 0x05, 1, 0, 0, 0, 0, 0, 0, 0, 0
    mov ax, BUF                     # 02h, ID 1, cylindre 0 tête 0 secteur 1 : le LBA 1 (PB-144)
    call phys
    mov [p_b02 + 9], al
    mov [p_b02 + 8], ah
    shr eax, 16
    mov [p_b02 + 7], al
    call bios
p_b02: .byte 0x02, 1, 0, 0, 0, 1, 1, 0, 0, 0
    call bios                       # 15h, ID 0 : INQUIRY puis capacité ; from_mailbox à 0
p_b15: .byte 0x15, 0, 0, 0, 0, 0, 0, 0, 0, 0
    call small
    mov si, offset p_b02                   # 02h encore : le sense va à ccb.addr + 12h + 10, périmé (PB-137)
    call bios_si
    mov si, CCB
    mov cx, 64
    call sum

# ---- 4. ABORT, 22h, 23h --------------------------------------------------------------------------------
    mov si, MBOX                    # ABORT à l'emplacement 1 : efface mba + 8, le code du MBI 0 (PB-139)
    mov dword ptr [si], 0
    mov ax, CCB
    call phys
    mov [si + 7], al
    mov [si + 6], ah
    shr eax, 16
    mov [si + 5], al
    mov byte ptr [si + 4], 2
    call mbif
    mov byte ptr [MBOX + 4], 0
    call cmd                        # 22h : 224 octets à partir de 32, lus au-delà de params (PB-136)
    .byte 36, 0x22, 0, 224, 32
    .byte 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10
    .byte 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20
    call hacc
    call cmd                        # 23h : relue
    .byte 4, 0x23, 0, 224, 32
    call res
    .byte 224
    call cmd                        # RETURN SETUP DATA encore : la mailbox posée
    .byte 2, 0x0D, 20
    call res
    .byte 20
    call regs

    mov al, [oldmask]
    out 0xA1, al
    mov ax, bx                      # la somme, pour les yeux
    mov di, offset hex
    mov cx, 4
4:  rol ax, 4
    push ax
    and al, 0x0F
    add al, 0x30
    cmp al, 0x39
    jbe 5f
    add al, 7
5:  mov [di], al
    inc di
    pop ax
    loop 4b
    mov dx, offset fin
    mov ah, 9
    int 0x21
    ret

# ---- les sous-programmes ---------------------------------------------------------------------------
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

# regs : l'état (334h) et l'ISR (336h).
regs:
    mov dx, BASE
    in al, dx
    call keep
    mov dx, BASE + 2
    in al, dx
    jmp keep

# wst : attendre (état & AH) == AL, au plus 65 535 lectures.
wst:
    push cx
    mov dx, BASE
    mov cx, 0xFFFF
6:  in al, dx
    and al, ah
    cmp al, bh
    je 7f
    loop 6b
7:  pop cx
    ret

# cmd : un octet de commande puis ses paramètres, en ligne : compte, octets. Chacun attend CDF à zéro.
cmd:
    pop si
    lodsb
    xor ch, ch
    mov cl, al
8:  push bx
    mov ah, 0x08
    mov bh, 0
    call wst
    pop bx
    lodsb
    mov dx, BASE + 1
    out dx, al
    loop 8b
    push si
    ret

# res : N octets de résultat (en ligne), chacun après DF.
res:
    pop si
    lodsb
    push si
    xor ch, ch
    mov cl, al
9:  push bx
    mov ah, 0x04
    mov bh, 0x04
    call wst
    pop bx
    mov dx, BASE + 1
    in al, dx
    call keep
    loop 9b
    jmp hacc

# hacc : attendre HACC dans l'ISR, relever état et ISR, IRST.
hacc:
    mov dx, BASE + 2
    mov cx, 0xFFFF
10: in al, dx
    test al, 0x04
    jnz 11f
    loop 10b
11: call regs
    mov dx, BASE
    mov al, 0x20
    out dx, al
    ret

# mbif : 02h, puis attendre MBIF (au plus 64 × 65 535 lectures), relever, IRST ; les deux MBI relevés et
#   remis à zéro.
mbif:
    push bx
    mov ah, 0x08
    mov bh, 0
    call wst
    pop bx
    mov dx, BASE + 1
    mov al, 0x02
    out dx, al
    mov dx, BASE + 2
    mov si, 64
12: mov cx, 0xFFFF
13: in al, dx
    test al, 0x01
    jnz 14f
    loop 13b
    dec si
    jnz 12b
14: call regs
    mov dx, BASE
    mov al, 0x20
    out dx, al
    mov si, MBOX
    mov cx, 16
    call sum
    mov byte ptr [MBOX + 8], 0
    mov byte ptr [MBOX + 12], 0
    ret

# ccb : le CCB en ligne (voir l'en-tête de la partie 2), puis le MBO 0, 02h, MBIF ; relevés : le CCB entier
#   (statuts en 0Dh-0Fh, résidu en 4-6, la zone de sense après la CDB).
ccb:
    pop si
    push di
    mov di, CCB
    mov cx, 32
    xor ax, ax
    rep stosw
    pop di
    lodsb
    mov [CCB], al
    lodsb
    mov [CCB + 1], al
    lodsb
    mov [CCB + 2], al
    lodsb
    mov [CCB + 3], al
    lodsb
    mov [CCB + 4], al
    lodsb
    mov [CCB + 5], al
    lodsb
    mov [CCB + 6], al
    lodsw
    cmp ax, 0xFFFF
    je 15f
    call phys
    jmp 16f
15: xor eax, eax
    lodsb
    shl eax, 16
    lodsb
    mov ah, al
    lodsb
16: mov [CCB + 9], al
    mov [CCB + 8], ah
    shr eax, 16
    mov [CCB + 7], al
    lodsb
    xor ch, ch
    mov cl, al
    push di
    mov di, CCB + 0x12
    rep movsb
    pop di
    push si
    mov si, MBOX
    mov ax, CCB
    call phys
    mov [si + 3], al
    mov [si + 2], ah
    shr eax, 16
    mov [si + 1], al
    mov byte ptr [si], 1
    call mbif
    mov si, CCB
    mov cx, 64
    call sum
    ret

# bios : la commande 03h et ses dix paramètres en ligne ; attendre DF, lire l'octet, puis HACC.
bios:
    pop si
    push si
    call bios_si
    pop si
    add si, 10
    push si
    ret
bios_si:
    push si
    call cmd
    .byte 1, 0x03
    pop si
    mov cx, 10
17: push cx
    push bx
    mov ah, 0x08
    mov bh, 0
    call wst
    pop bx
    lodsb
    mov dx, BASE + 1
    out dx, al
    pop cx
    loop 17b
    mov dx, BASE + 2                # l'achèvement : HACC (avec ou sans DF)
    mov si, 64
18: mov cx, 0xFFFF
19: in al, dx
    test al, 0x04
    jnz 20f
    loop 19b
    dec si
    jnz 18b
20: call regs
    mov dx, BASE + 1
    in al, dx
    call keep
    mov dx, BASE
    mov al, 0x20
    out dx, al
    mov si, MBOX
    mov cx, 16
    call sum
    mov byte ptr [MBOX + 8], 0
    mov byte ptr [MBOX + 12], 0
    ret

# small : les quatre octets écrits par 08h ou 15h.
small:
    mov si, SMALL
    mov cx, 4
    jmp sum

# phys : AX (un offset de ce segment) → EAX, l'adresse physique.
phys:
    push edx
    movzx eax, ax
    xor edx, edx
    mov dx, cs
    shl edx, 4
    add eax, edx
    pop edx
    ret

oldmask: .byte 0
fin: .ascii "AHABANC "
hex: .ascii "0000"
     .byte 13, 10
     .ascii "$"
