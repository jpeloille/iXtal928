# LPTPLAY.COM — l'écoute du port parallèle (GR.3i, PLAN-GR.md ; R3-clotures.md § GR.3i). Une gamme d'ut majeur,
# do 3 à do 4 puis retour (261,63 à 523,25 Hz, quinze notes de 0,3 s), en signal carré de 8 bits (50h et B0h autour de
# 80h), écrite sur LPT1 (378h) :
#   LPTPLAY D   la Disney Sound Source : un octet dès que sa file a de la place (le bit 6 de 379h tombé), la file
#               vidée à 7 kHz par l'appareil lui-même ;
#   LPTPLAY C   le Covox mono : un octet à chaque front montant de la sortie du canal 2 du PIT (le bit 5 de 61h), le
#               canal programmé en mode 3 à 1193182 / 149 = 8008 Hz, son haut-parleur coupé (le bit 1 de 61h) ;
#   LPTPLAY S   le Covox stéréo (lpt_dac_stereo), au même rythme : la gamme montante à gauche (le bit 0 de 37Ah à 0),
#               puis descendante à droite (le bit 0 à 1), l'autre canal au repos (80h).
# La note est une phase de 16 bits, augmentée à chaque échantillon d'un pas f × 65536 / R (R le rythme : 7000 ou
# 8008 Hz) ; son bit 15 choisit le niveau. Les interruptions restent permises : l'horloge du BIOS continue. À la fin,
# la porte du canal 2 et le haut-parleur sont coupés (les bits 0 et 1 de 61h ; le mode 3 et le compte 149 restent,
# comme après un BEEP), la donnée à 80h, le contrôle à 0Ch. Assemblé par lptplay.py (GNU as, ld en
# binaire plat à 100h).
.intel_syntax noprefix
.code16
.text
.globl _start
_start:
    cld
    mov si, 0x81
1:  lodsb
    cmp al, ' '
    je 1b
    and al, 0xDF
    cmp al, 'D'
    je dss
    cmp al, 'C'
    je covox
    cmp al, 'S'
    je stereo
    mov dx, offset usage
    jmp message

dss:
    mov dx, offset m_dss
    call ecrire
    mov word ptr [duree], 2100
    mov bp, offset ecrit_dss
    mov si, offset pas_dss
    mov bx, 15
    call gamme
    jmp fin

covox:
    mov dx, offset m_covox
    call ecrire
    call pit
    mov bp, offset ecrit_covox
    mov si, offset pas_covox
    mov bx, 15
    call gamme
    jmp fin

stereo:
    mov dx, offset m_stereo
    call ecrire
    call pit
    mov bp, offset ecrit_covox
    mov dx, 0x37A
    mov al, 0x0D
    out dx, al
    mov dx, 0x378
    mov al, 0x80
    out dx, al
    mov dx, 0x37A
    mov al, 0x0C
    out dx, al
    mov si, offset pas_covox
    mov bx, 8
    call gamme
    mov dx, 0x378
    mov al, 0x80
    out dx, al
    mov dx, 0x37A
    mov al, 0x0D
    out dx, al
    mov si, offset pas_covox_desc
    mov bx, 8
    call gamme
    jmp fin

# pit : le canal 2 en mode 3 à 8008 Hz, sa porte ouverte, le haut-parleur coupé ; la durée d'une note.
pit:
    mov word ptr [duree], 2402
    mov al, 0xB6
    out 0x43, al
    mov al, 149
    out 0x42, al
    xor al, al
    out 0x42, al
    in al, 0x61
    and al, 0xFC
    or al, 0x01
    out 0x61, al
    ret

# gamme : BX notes, leurs pas en DS:SI, [duree] échantillons chacune, écrits par la routine en BP.
gamme:
    lodsw
    mov [pas], ax
    mov cx, [duree]
    xor di, di
2:  add di, [pas]
    mov al, 0x50
    test di, 0x8000
    jz 3f
    mov al, 0xB0
3:  call bp
    loop 2b
    dec bx
    jnz gamme
    ret

# ecrit_dss : AL dans la file de la Sound Source, dès qu'elle n'est plus pleine.
ecrit_dss:
    mov ah, al
    mov dx, 0x379
4:  in al, dx
    test al, 0x40
    jnz 4b
    mov al, ah
    dec dx
    out dx, al
    ret

# ecrit_covox : AL sur le Covox, au front montant suivant de la sortie du canal 2.
ecrit_covox:
    mov ah, al
    mov dx, 0x61
5:  in al, dx
    test al, 0x20
    jnz 5b
6:  in al, dx
    test al, 0x20
    jz 6b
    mov al, ah
    mov dx, 0x378
    out dx, al
    ret

fin:
    in al, 0x61
    and al, 0xFC
    out 0x61, al
    mov dx, 0x378
    mov al, 0x80
    out dx, al
    mov dx, 0x37A
    mov al, 0x0C
    out dx, al
    mov dx, offset m_fin
message:
    call ecrire
    mov ax, 0x4C00
    int 0x21

ecrire:
    mov ah, 9
    int 0x21
    ret

# Les pas : do 3, ré, mi, fa, sol, la, si, do 4, puis la descente. 7000 Hz pour la Sound Source, 8008 pour le Covox.
pas_dss:
    .word 2449, 2749, 3086, 3270, 3670, 4119, 4624, 4899, 4624, 4119, 3670, 3270, 3086, 2749, 2449
pas_covox:
    .word 2141, 2403, 2698, 2858, 3208, 3601, 4042, 4282, 4042, 3601, 3208, 2858, 2698, 2403, 2141
pas_covox_desc:
    .word 4282, 4042, 3601, 3208, 2858, 2698, 2403, 2141
duree: .word 0
pas:   .word 0
usage:
    .ascii "LPTPLAY D : la gamme sur la Disney Sound Source (LPT1, 378h)\r\n"
    .ascii "LPTPLAY C : sur le Covox mono\r\n"
    .ascii "LPTPLAY S : sur le Covox stereo, montante a gauche, descendante a droite\r\n$"
m_dss:    .ascii "Disney Sound Source, 7 kHz : do 3 a do 4, et retour\r\n$"
m_covox:  .ascii "Covox mono, 8008 Hz : do 3 a do 4, et retour\r\n$"
m_stereo: .ascii "Covox stereo, 8008 Hz : la gamme montante a gauche, puis descendante a droite\r\n$"
m_fin:    .ascii "Fin.\r\n$"
