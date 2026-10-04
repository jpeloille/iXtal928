# La série de portes du dépôt — une LISTE, pas un programme : chaque `run NOM args…` est un
# appel à iXtal26.Diff depuis la racine du dépôt ; chaque `runw NOM CMOS DEST args…` est un
# boot-diff disque, lancé dans son propre répertoire sous $WORK (voir g5w-recipe.sh), avec le
# CMOS `CMOS` copié en nvr/.DEST.nvr. L'ordre des lignes est l'ORDRE CANONIQUE du .tsv : on
# compare deux séries ligne à ligne. Sourcé par par.sh, qui définit run et runw.
# Historique : G4.0 à G5.2 (VERIFICATION.md) ; les gardes « depuis quelle étape » ont disparu,
# tout existe désormais.
C=tools/gates/cfg
DOS=os/pcdos20/pcdos20b.img
ALL=$(for i in $(seq 0 255); do printf -- '--op %02X ' $i; done)
# D4 exclu du 8088 sous --fpu-state : AAM 0 fait mourir PCem de SIGFPE (PB-46).
FPUX=$(for i in $(seq 0 255); do case $i in 212) ;; *) printf -- '--op %02X ' $i;; esac; done)
ALL0F=$(for i in $(seq 0 255); do printf -- '--0f %02X ' $i; done)
run abi abi
run ops-count ops-count
for g in cga vga tvga8900d tvga9000b mda hercules ega; do
  run bd-pc-$g boot-diff roms 6000 --gfxcard $g
  run bd-pcdos-$g boot-diff roms 7000 --fda $DOS --gfxcard $g
  run bd-xt-$g boot-diff roms 6000 --model ibmxt --gfxcard $g
  run bd-xtdos-$g boot-diff roms 7000 --config ixtal26-xt.cfg --fda $DOS --gfxcard $g
done
run bd-xtcfg boot-diff roms 6000 --config ixtal26-xt.cfg
run bd-pc-cpu3 boot-diff roms 6000 --cpu 3
run bd-ibmat boot-diff roms 3000 --model ibmat
run bd-ibmat-vga boot-diff roms 3000 --model ibmat --gfxcard vga
run bd-ami286 boot-diff roms 3000 --model ami286
run bd-ami386 boot-diff roms 3000 --model ami386
run bd-ami386-4m boot-diff roms 3000 --config $C/ami386-4m.cfg
run bd-ami386dx-4m boot-diff roms 3000 --config $C/ami386dx-4m.cfg
run bd-ami386dx-4m-dos boot-diff roms 2000 --config $C/ami386dx-4m.cfg --fda $DOS
run bd-xt-8087 boot-diff roms 6000 --config $C/xt-8087.cfg
run bd-xtdos-8087 boot-diff roms 7000 --config $C/xt-8087.cfg --fda $DOS
run bd-pc-8087 boot-diff roms 6000 --config $C/pc-8087.cfg
run bd-pcdos-8087 boot-diff roms 7000 --config $C/pc-8087.cfg --fda $DOS
run bd-ibmat-287 boot-diff roms 3000 --config $C/ibmat-287.cfg
run bd-ibmat-287xl boot-diff roms 3000 --config $C/ibmat-287xl.cfg
run bd-ami286-287 boot-diff roms 3000 --config $C/ami286-287.cfg
run bd-ami386-387 boot-diff roms 3000 --config $C/ami386-387.cfg
run bd-ami386dx-387 boot-diff roms 3000 --config $C/ami386dx-387.cfg
run bd-ami386dx-387-dos boot-diff roms 2000 --config $C/ami386dx-387.cfg --fda $DOS
run bd-ami386dx-387-post boot-diff roms 25000 --config $C/ami386dx-387.cfg
run bd-ami486-dx2 boot-diff roms 3000 --config $C/ami486-dx2.cfg
run bd-ami486-dx2-post boot-diff roms 40000 --config $C/ami486-dx2.cfg
run fuzz8088 fuzz --mode single --iter 100000 $ALL
run fuzz8088-stream fuzz --rounds 1500 --instr 200 $ALL
run fuzz286 fuzz --core 286 --mode single --iter 80000 $ALL
run fuzz386-s1 fuzz --core 386 --mode single --iter 80000 --seed 1 $ALL
run fuzz386-s7 fuzz --core 386 --mode single --iter 80000 --seed 7 $ALL
run fuzz8088-fpu fuzz --mode single --iter 100000 --fpu-state $ALL
run fuzz286-fpu fuzz --core 286 --mode single --iter 80000 --fpu-state $ALL
run fuzz386-fpu fuzz --core 386 --mode single --iter 80000 --fpu-state $ALL
run fuzz8088-fpu-stream fuzz --rounds 1500 --instr 200 --fpu-state $ALL
run fuzz286-stream fuzz --core 286 --rounds 1500 --instr 200 $ALL
run fuzz386-stream fuzz --core 386 --rounds 1500 --instr 200 $ALL
run popss-check popss-check
X87="--x87 mem --fpu-state --op D9 --op DB --op DD --op DF"
run x87-386-s1 fuzz --core 386 --mode single --iter 80000 --seed 1 --fpu 387 $X87 --op 66 --op 67
run x87-386-s7 fuzz --core 386 --mode single --iter 80000 --seed 7 --fpu 387 $X87 --op 66 --op 67
run x87-286-s1 fuzz --core 286 --mode single --iter 80000 --seed 1 --fpu 287 $X87
A87="--fpu-state --op D8 --op DA --op DC --op DE"
run x87a-386-s1 fuzz --core 386 --mode single --iter 80000 --seed 1 --fpu 387 $A87 --op 66 --op 67
run x87a-386-s7 fuzz --core 386 --mode single --iter 80000 --seed 7 --fpu 387 $A87 --op 66 --op 67
run x87a-286-s1 fuzz --core 286 --mode single --iter 80000 --seed 1 --fpu 287 $A87
run x87a-386-flux fuzz --core 386 --rounds 1500 --instr 200 --fpu 387 $A87
run x87-cases x87-cases
M87="--x87 g44 --fpu-state --op D9 --op DB --op DD --op DF"
run x87m-386-s1 fuzz --core 386 --mode single --iter 80000 --seed 1 --fpu 387 $M87 --op 66 --op 67
run x87m-386-s7 fuzz --core 386 --mode single --iter 80000 --seed 7 --fpu 387 $M87 --op 66 --op 67
run x87m-286-s1 fuzz --core 286 --mode single --iter 80000 --seed 1 --fpu 287 $M87
run x87m-386-flux fuzz --core 386 --rounds 1500 --instr 200 --fpu 387 --fpu-state --op D9 --op DB --op DD --op DF
run x87-pm-fuzz pm-fuzz --fpu 387 --iter 20000 --op D8 --op D9 --op DA --op DB --op DC --op DD --op DE --op DF
T87="--x87 all --fpu-state --op D8 --op D9 --op DA --op DB --op DC --op DD --op DE --op DF"
run x87t-386-s1 fuzz --core 386 --mode single --iter 80000 --seed 1 --fpu 387 $T87 --op 66 --op 67
run x87t-386-s7 fuzz --core 386 --mode single --iter 80000 --seed 7 --fpu 387 $T87 --op 66 --op 67
run x87t-286-s1 fuzz --core 286 --mode single --iter 80000 --seed 1 --fpu 287 $T87
run x87t-386-flux fuzz --core 386 --rounds 1500 --instr 200 --fpu 387 --fpu-state --op D8 --op D9 --op DA --op DB --op DC --op DD --op DE --op DF
# Les disques AT sous oracle (G5) : copies des disques de l'utilisateur, CMOS type 46 fabriqués.
mapfile -t ARC < tools/diskarc/fdisk-format-d.keys
A=(); for l in "${ARC[@]}"; do A+=(--type "$l"); done
mapfile -t IC < tools/idecheck/idecheck.keys
B=(); for l in "${IC[@]}"; do B+=(--type "$l"); done
runw bd-ami286-mfm-ecriture c.nvr ami286 boot-diff roms 100000 --config ami286-mfm.cfg --type-at 60000 --type "MD G5" --type "COPY AUTOEXEC.BAT G5" --type "DIR G5"
runw bd-ami286-mfm-fdisk cd.nvr ami286 boot-diff roms 60000 --config ami286-cd.cfg --type-at 60000 --type-settle 1500 "${A[@]}"
runw bd-ami286-ide-ecriture c.nvr ami286 boot-diff roms 100000 --config ami286-ide.cfg --type-at 60000 --type "MD G5" --type "COPY AUTOEXEC.BAT G5" --type "DIR G5"
runw bd-ami286-ide-fdisk cd.nvr ami286 boot-diff roms 60000 --config ami286-ide-cd.cfg --type-at 60000 --type-settle 1500 "${A[@]}"
runw bd-ami286-ide-check c.nvr ami286 boot-diff roms 60000 --config ami286-ide-e.cfg --type-at 60000 --type-settle 600 "${B[@]}" --type "^" --type "^" --type "^"
runw bd-ami386dx-ide-ecriture c386.nvr ami386dx_opti495 boot-diff roms 100000 --config ami386dx-ide.cfg --type-at 60000 --type "MD G5" --type "COPY AUTOEXEC.BAT G5" --type "DIR G5"
# G6.4 — l'ami486 DX2/66 en IDE : écriture sur C:, puis l'ide-check sur les deux canaux.
runw bd-ami486-ide-ecriture c486.nvr ami486 boot-diff roms 100000 --config ami486-ide.cfg --type-at 60000 --type "MD G5" --type "COPY AUTOEXEC.BAT G5" --type "DIR G5"
runw bd-ami486-ide-check c486.nvr ami486 boot-diff roms 60000 --config ami486-ide-e.cfg --type-at 60000 --type-settle 600 "${B[@]}" --type "^" --type "^" --type "^"
E87="--fpu-state --op D8 --op D9 --op DA --op DB --op DC --op DD --op DE --op DF"
run x87-8088-s1 fuzz --mode single --iter 100000 --seed 1 --fpu 8087 --x87 all $E87
run x87-8088-s7 fuzz --mode single --iter 100000 --seed 7 --fpu 8087 --x87 all $E87
run x87-8088-flux fuzz --rounds 1500 --instr 200 --fpu 8087 $E87
run x87-8088-flux-tout fuzz --rounds 1500 --instr 200 --fpu 8087 --fpu-state $FPUX
# G6 — le cœur 486 (ami486, cpus_i486) : défaut i486SX/16, --cpu 10 = i486DX2/66, 12 = iDX4/100.
run fuzz486-s1 fuzz --core 486 --mode single --iter 80000 --seed 1 $ALL
run fuzz486-s7 fuzz --core 486 --mode single --iter 80000 --seed 7 $ALL
run fuzz486-dx2 fuzz --core 486 --cpu 10 --mode single --iter 80000 --seed 3 $ALL
run fuzz486-dx4 fuzz --core 486 --cpu 12 --mode single --iter 80000 --seed 5 $ALL
run fuzz486-stream fuzz --core 486 --rounds 1500 --instr 200 $ALL
run fuzz486-dx2-stream fuzz --core 486 --cpu 10 --rounds 1500 --instr 200 $ALL
run fuzz486-0f fuzz --core 486 --mode single --iter 40000 --0f 08 --0f 09 --0f 01 --0f A2 --0f B0 --0f B1 --0f C0 --0f C1 --0f C8 --0f C9 --0f CA --0f CB --0f CC --0f CD --0f CE --0f CF
run fuzz486-dx4-0f fuzz --core 486 --cpu 12 --mode single --iter 40000 --0f A2 --0f 20 --0f 22 --0f 01
run x87-486-dx2 fuzz --core 486 --cpu 10 --mode single --iter 80000 --seed 1 $T87 --op 66 --op 67
run x87-486-dx2-flux fuzz --core 486 --cpu 10 --rounds 1500 --instr 200 $T87
run fuzz386-0f fuzz --core 386 --mode single --iter 40000 $ALL0F
run page-check page-check
run pm-fuzz pm-fuzz --iter 20000
run core286-check core286-check
run pm-check pm-check
run pm-check-386 pm-check --core 386
run cpu-config-check cpu-config-check roms
run config-check config-check
# G6.2 — le mode protégé et la pagination du 486 ; R9 : la survie à une table hors RAM (C# seul).
run page-check-486 page-check --core 486
run pm-check-486 pm-check --core 486
run pm-fuzz-486 pm-fuzz --core 486 --iter 20000
run x87-pm-fuzz-486 pm-fuzz --core 486 --fpu 387 --iter 20000 --op D8 --op D9 --op DA --op DB --op DC --op DD --op DE --op DF
run r9-mmu r9-mmu
# G7.0 — la fenêtre linéaire du socle SVGA, appelée des deux côtés sur une VGA amorcée.
run svga-linear-check svga-linear-check --iter 50000
# G7.1 — la Cirrus GD5429 : ami486 (VLB) jusqu'au bout du POST puis DOS sur IDE, ami386dx
# (ISA, has_vlb = 0) ; R9 : la survie aux index hors VRAM (PB-81, PB-82), C# seul.
run bd-ami486-gd5429 boot-diff roms 3000 --config $C/ami486-dx2.cfg --gfxcard cl_gd5429
run bd-ami486-gd5429-post boot-diff roms 40000 --config $C/ami486-dx2.cfg --gfxcard cl_gd5429
run bd-ami386dx-gd5429 boot-diff roms 3000 --config $C/ami386dx-4m.cfg --gfxcard cl_gd5429
runw bd-ami486-gd5429-dos c486.nvr ami486 boot-diff roms 100000 --config ami486-ide.cfg --gfxcard cl_gd5429 --type-at 60000 --type "VER" --type "DIR"
run r9-cl5429 r9-cl5429
# G7.2 — le blitter de la GD5429 : BLTBANC.COM saisi dans DEBUG, écrit sur C:, lancé.
mapfile -t BLK < tools/bltbanc/bltbanc.keys
BL=(); for l in "${BLK[@]}"; do BL+=(--type "$l"); done
runw bd-ami486-gd5429-blt c486.nvr ami486 boot-diff roms 60000 --config ami486-ide.cfg --gfxcard cl_gd5429 --type-at 60000 --type-settle 600 "${BL[@]}" --type "^" --type "^" --type "^"
# G7.3 — la S3 Trio64 Phoenix, accélérateur synchrone (décision n° 3) : ami486 jusqu'au POST
# puis DOS sur IDE, ami386dx ; le banc S3BANC ; R9 : PB-84 à PB-86, C# seul.
run bd-ami486-trio64 boot-diff roms 3000 --config $C/ami486-dx2.cfg --gfxcard px_trio64
run bd-ami486-trio64-post boot-diff roms 40000 --config $C/ami486-dx2.cfg --gfxcard px_trio64
run bd-ami386dx-trio64 boot-diff roms 3000 --config $C/ami386dx-4m.cfg --gfxcard px_trio64
runw bd-ami486-trio64-dos c486.nvr ami486 boot-diff roms 100000 --config ami486-ide.cfg --gfxcard px_trio64 --type-at 60000 --type "VER" --type "DIR"
mapfile -t S3K < tools/s3banc/s3banc.keys
S3B=(); for l in "${S3K[@]}"; do S3B+=(--type "$l"); done
runw bd-ami486-trio64-accel c486.nvr ami486 boot-diff roms 60000 --config ami486-ide.cfg --gfxcard px_trio64 --type-at 60000 --type-settle 600 "${S3B[@]}" --type "^" --type "^" --type "^"
run r9-s3 r9-s3
# G1.0 — le 8086 (le MÊME execx86, is8086 posé par cpu_set sur l'Olivetti M24) ; le balayage
# des tables de CPU dans l'autre ordre (risque n° 1 de PLAN-G1.md : le 8088 ne doit pas bouger).
run cpu-config-check-inverse cpu-config-check roms --inverse
run fuzz8086-s1 fuzz --core 8086 --mode single --iter 80000 --seed 1 $ALL
run fuzz8086-s7 fuzz --core 8086 --mode single --iter 80000 --seed 7 $ALL
run fuzz8086-16-s3 fuzz --core 8086 --cpu 5 --mode single --iter 80000 --seed 3 $ALL
run fuzz8086-stream fuzz --core 8086 --rounds 1500 --instr 200 $ALL
run x87-8086-s1 fuzz --core 8086 --mode single --iter 100000 --seed 1 --fpu 8087 --x87 all $E87
run x87-8086-flux fuzz --core 8086 --rounds 1500 --instr 200 --fpu 8087 $E87
# G1.1 — l'Olivetti M24 : POST, PC-DOS 2.00 en disquette, son clavier (date, heure, DIR).
run bd-m24 boot-diff roms 3000 --model olivetti_m24
run bd-m24-dos boot-diff roms 7000 --model olivetti_m24 --fda $DOS
run bd-m24-dir boot-diff roms 9000 --model olivetti_m24 --fda $DOS --type-at 7000 --type "" --type "" --type "DIR"
# G1.2 — l'Amstrad PC1512 : POST, PC-DOS 2.00, son clavier ; le mode plan 640 × 200 × 16 par
# P1512.COM, saisi dans DEBUG (disquette supplémentaire en B:).
run bd-pc1512 boot-diff roms 3000 --model pc1512
run bd-pc1512-dos boot-diff roms 7000 --model pc1512 --fda $DOS
run bd-pc1512-dir boot-diff roms 9000 --model pc1512 --fda $DOS --type-at 7000 --type "" --type "" --type "DIR"
mapfile -t P1K < tools/pc1512banc/pc1512banc.keys
P1B=(); for l in "${P1K[@]}"; do P1B+=(--type "$l"); done
run bd-pc1512-plan boot-diff roms 9000 --model pc1512 --fda $DOS --fdb os/pcdos20/pcdos20s.img --type-at 7000 --type-settle 600 --type "" --type "" "${P1B[@]}" --type "^" --type "^"
# G8.0 — les tables de DBOPL (pow, sin de la libm), oracle (C++) contre C#.
run opl-tables-check opl-tables-check
# G8.1 — l'OPL (DBOPL) et l'AdLib : le 5150 et l'ami486 avec la carte ; le banc OPLBANC (détection
# AdLib par les minuteries, neuf voix, percussions) saisi dans DEBUG, sous boot-diff, sonde du son.
run bd-pc-adlib boot-diff roms 3000 --sndcard adlib
run bd-ami486-adlib boot-diff roms 3000 --config $C/ami486-dx2.cfg --sndcard adlib
mapfile -t OPK < tools/oplbanc/oplbanc.keys
OPB=(); for l in "${OPK[@]}"; do OPB+=(--type "$l"); done
run bd-pc-adlib-banc boot-diff roms 10000 --sndcard adlib --fda $DOS --fdb os/pcdos20/pcdos20s.img --type-at 7000 --type-settle 600 --type "" --type "" "${OPB[@]}" --type "^" --type "^"
# G8.2 — le DSP SBPRO2 et le CT1345 : le 5150 et l'ami486 avec la SB Pro v2 ; le banc SBBANC
# (reset et version, sortie directe, DMA simple et automatique, vitesse, pause, mélangeur, stéréo,
# filtre, ADPCM 4 bits, OPL3) saisi dans DEBUG, sous boot-diff, sonde du son.
run bd-pc-sbpro boot-diff roms 3000 --sndcard sbprov2
run bd-ami486-sbpro boot-diff roms 3000 --config $C/ami486-dx2.cfg --sndcard sbprov2 --expect-sb 220,7,1
mapfile -t SBK < tools/sbbanc/sbbanc.keys
SBB=(); for l in "${SBK[@]}"; do SBB+=(--type "$l"); done
run bd-pc-sbpro-banc boot-diff roms 11000 --sndcard sbprov2 --fda $DOS --fdb os/pcdos20/pcdos20s.img --type-at 7000 --type-settle 600 --type "" --type "" "${SBB[@]}" --type "^" --type "^"
# G8.3 — les sections de device du .cfg, lues des deux côtés (device.c:94-104 ; h_set_device_config) :
# l'ami486 + SB Pro v2 des profils, 220h, IRQ 5, DMA 1, exigés par --expect-sb (sans section,
# bd-ami486-sbpro ci-dessus exige le défaut, IRQ 7). r9-sbcfg, en C# seul : clés inconnues, valeurs
# hors liste → défaut averti (PB-93 : SB et les trois cartes SVGA), opl_emu = 1.
run bd-ami486-sbpro-irq5 boot-diff roms 3000 --config $C/ami486-sbpro-irq5.cfg --expect-sb 220,5,1
run r9-sbcfg r9-sbcfg
# PS2.0, PS2.1 — la souris PS/2 (mouse_ps2.c) par le 8042. Aucune machine du dépôt n'a MODEL_PS2
# (leurs BIOS AMI ne rendent pas INT 15h C2h ; décision utilisateur du 03/10, VERIFICATION.md
# § PS2.1) : la souris PS/2 et l'Intellimouse demandées sont refusées, la série à leur place —
# vérifié par --expect-mouse-type 0. Le banc PS2BANC, saisi dans DEBUG sur l'ami386dx (CMOS
# f386.nvr), passe par --force-ps2 : PORTE DE VÉRIFICATION, PAS UNE MACHINE OFFERTE — il garde la
# transcription de mouse_ps2.c contre l'oracle. Mouvements injectés par --mouse-at, bouton du
# milieu tenu dès 45 560 (PB-95), en souris à 2 boutons puis en Intellimouse.
run bd-ami486-ps2-refus boot-diff roms 3000 --config $C/ami486-dx2.cfg --mouse-type 2 --expect-mouse-type 0
run bd-ami386dx-im-refus boot-diff roms 3000 --config $C/ami386dx-4m.cfg --mouse-type 3 --expect-mouse-type 0
run bd-ami286-ps2-refus boot-diff roms 3000 --model ami286 --mouse-type 2 --expect-mouse-type 0
mapfile -t P2K < tools/ps2banc/ps2banc.keys
P2B=(); for l in "${P2K[@]}"; do P2B+=(--type "$l"); done
P2M=(); for t in $(seq 45500 30 46400); do b=0; [ $t -ge 45560 ] && b=4; P2M+=(--mouse-at "$t:3,-2,0,$b"); done
P2M+=(--mouse-at "45560:40,25,-1,4")
for mt in 2 3; do
  runw bd-ami386dx-ps2-banc-$mt f386.nvr ami386dx_opti495 boot-diff roms 48500 --config ami386dx-fd.cfg --mouse-type $mt --force-ps2 --fda "$REPO/$DOS" --fdb "$REPO/os/pcdos20/pcdos20s.img" --type-at 2500 --type-settle 600 --type "" --type "" "${P2B[@]}" "${P2M[@]}"
done
# G9.0 — la MDA : le 5150, le XT (POST et DOS) par la boucle des cartes ci-dessus ; l'IBM AT.
run bd-ibmat-mda boot-diff roms 3000 --model ibmat --gfxcard mda
# G9.1 — l'Hercules : le 5150, le XT (POST et DOS) par la boucle des cartes ci-dessus ; l'IBM AT ; le
# banc HERCBANC (graphique 720×348, deux pages, 3BAh, le 6845 relu) saisi dans DEBUG sur le 5150.
run bd-ibmat-hercules boot-diff roms 3000 --model ibmat --gfxcard hercules
mapfile -t HBK < tools/hercbanc/hercbanc.keys
HBB=(); for l in "${HBK[@]}"; do HBB+=(--type "$l"); done
run bd-pc-herc-banc boot-diff roms 11000 --gfxcard hercules --fda $DOS --fdb os/pcdos20/pcdos20s.img --type-at 7000 --type-settle 600 --type "" --type "" "${HBB[@]}" --type "^" --type "^"
# G9.2 — l'EGA d'IBM : le 5150, le XT (POST et DOS) par la boucle des cartes ci-dessus ; l'IBM AT,
# l'ami286, l'ami386 ; le banc EGABANC (modes 0Dh, 0Eh, 10h, 04h ; plans, set/reset, rotation,
# fonctions logiques, masque de bits, modes d'écriture 0-2, lecture en mode 1) saisi dans DEBUG sur
# le 5150.
run bd-ibmat-ega boot-diff roms 3000 --model ibmat --gfxcard ega
run bd-ami286-ega boot-diff roms 3000 --model ami286 --gfxcard ega
run bd-ami386-ega boot-diff roms 3000 --model ami386 --gfxcard ega
mapfile -t EBK < tools/egabanc/egabanc.keys
EBB=(); for l in "${EBK[@]}"; do EBB+=(--type "$l"); done
run bd-pc-ega-banc boot-diff roms 11000 --gfxcard ega --fda $DOS --fdb os/pcdos20/pcdos20s.img --type-at 7000 --type-settle 600 --type "" --type "" "${EBB[@]}" --type "^" --type "^"
# G9.3 — la Tseng ET4000AX (carte ISA) : l'ami286 et l'ami386dx (sans VLB), l'ami486 (POST court et
# complet) ; le banc ET4BANC (modes Tseng 2Eh et 30h, banques de 3CDh, RAMDAC SC1502x, CR33-CR35)
# saisi dans DEBUG sur l'ami386dx (CMOS f386.nvr). Le BIOS de la carte ne rend pas VESA (mesuré).
run bd-ami286-et4000 boot-diff roms 3000 --model ami286 --gfxcard et4000ax
run bd-ami386dx-et4000 boot-diff roms 3000 --config $C/ami386dx-4m.cfg --gfxcard et4000ax
run bd-ami486-et4000 boot-diff roms 3000 --config $C/ami486-dx2.cfg --gfxcard et4000ax
run bd-ami486-et4000-post boot-diff roms 40000 --config $C/ami486-dx2.cfg --gfxcard et4000ax
mapfile -t T4K < tools/et4banc/et4banc.keys
T4B=(); for l in "${T4K[@]}"; do T4B+=(--type "$l"); done
runw bd-ami386dx-et4-banc f386.nvr ami386dx_opti495 boot-diff roms 40000 --config ami386dx-fd.cfg --gfxcard et4000ax --fda "$REPO/$DOS" --fdb "$REPO/os/pcdos20/pcdos20s.img" --type-at 2500 --type-settle 600 --type "" --type "" "${T4B[@]}" --type "^" --type "^"
# G10.0 — LPT1 et LPT2 (lpt.c) posés sur toutes les machines par common_init, comme chez PCem ;
# LPT1 porte un périphérique (pc.c:810-818, lpt1_device) : la Disney Sound Source (lpt_dss.c), le
# Covox (lpt_dac.c) et le Covox stéréo, chacun sous le banc LPTBANC (BDA des ports, registres de
# 378h/37Ah et 278h/27Ah, statut 379h, salve de 24 octets dans la FIFO de la DSS écoutée pendant
# sa vidange, rampe de 256 échantillons, canal alterné par 37Ah bit 0) saisi dans DEBUG, sonde du
# son ; l'ami486 avec le Covox stéréo à l'amorçage.
mapfile -t LPK < tools/lptbanc/lptbanc.keys
LPB=(); for l in "${LPK[@]}"; do LPB+=(--type "$l"); done
for d in dss lpt_dac lpt_dac_stereo; do
  run bd-pc-lpt-$d-banc boot-diff roms 11000 --lpt1 $d --fda $DOS --fdb os/pcdos20/pcdos20s.img --type-at 7000 --type-settle 600 --type "" --type "" "${LPB[@]}" --type "^" --type "^"
done
run bd-ami486-lpt-dac-stereo boot-diff roms 3000 --config $C/ami486-dx2.cfg --lpt1 lpt_dac_stereo
# G10.1 — la manette : les sept types de joystick_list (gameport.c:17-19), chacun sous le banc
# JOYBANC (tools/joybanc) saisi dans DEBUG sur le 5150 : douze tours, chacun chronométrant les
# quatre axes par 201h (boutons à l'armement), une rafale de lectures (les paquets de la SideWinder)
# et une sonde du paquet d'identification ; douze états injectés des deux côtés par --joy-at, un
# par tour, au milieu de l'attente qui précède, dont le chapeau à 315° (PB-103 : la CH le lit
# centré, la TM en bas) ; les manettes 1 à 3 en route (le second manche du type 0, les paquets à
# plusieurs manettes de la SideWinder). r9-joycfg, en C# seul : joystick_type et [Joysticks] hors
# borne, ramenés au défaut et avertis (PB-93, R9). trace-hash-check : le hachage plié de la trace
# contre l'ancien, en C et en C# (l'accélération du 4 octobre).
mapfile -t JOK < tools/joybanc/joybanc.keys
JOB=(); for l in "${JOK[@]}"; do JOB+=(--type "$l"); done
JOE=("0,0,0,0,-1" "-32768,-32768,1,-32768,0" "32767,32767,2,32767,90" "-16384,16384,4,0,180" "16384,-16384,8,0,270"
     "0,0,0,0,315" "1000,-1000,15,5000,45" "-20000,20000,0x30,-5000,135" "30000,-30000,0xC0,0,225" "0,0,0x300,0,-1"
     "-32768,32767,0x3FF,0,315" "0,0,0,0,-1")
JOA=(); for r in "${!JOE[@]}"; do JOA+=(--joy-at "$((18840 + 55 * r)):${JOE[$r]}"); done
JOA+=(--joy-at "$((18840 + 55 * 3))/1:5000,-5000,3" --joy-at "$((18840 + 55 * 6))/1:-25000,25000,1")
JOA+=(--joy-at "$((18840 + 55 * 9))/1:32767,-32768,2" --joy-at "$((18840 + 55 * 6))/2:100,200,0x10")
JOA+=(--joy-at "$((18840 + 55 * 8))/3:-100,-200,0x200")
JON=(std 4b 6b 8b ch sw tm)
for t in 0 1 2 3 4 5 6; do
  run bd-pc-joy-${JON[$t]}-banc boot-diff roms 20000 --joystick-type $t --fda $DOS --fdb os/pcdos20/pcdos20s.img --type-at 7000 --type-settle 150 --type "" --type "" "${JOB[@]}" "${JOA[@]}"
done
run r9-joycfg r9-joycfg
run trace-hash-check trace-hash-check
# G10.2 — le XTIDE (xtide.c, la version XT) : l'IDE de G5 derrière une carte 8 bits, sa ROM (XTIDE
# Universal BIOS v2.0.0 β3) en C800. bd-xt-xtide-format : le disque VIERGE (c8088.img, copie de
# os/8088-HDD-C.img) partitionné par FDISK, la machine réamorcée sur A: par la touche « a » du
# menu du XTIDE, puis FORMAT C:/S et DIR C: — le script (xtide-format.keys) dont la recette g5w
# tire c8088dos.img en C# seul, aux mêmes tranches. bd-*-xtide-boot : le 5150, le XT, la M24 et le
# PC1512 amorcent c8088dos.img sans disquette, puis VER, DIR et une écriture (MD, COPY).
mapfile -t XFK < tools/gates/xtide-format.keys
XF=(); for l in "${XFK[@]}"; do XF+=(--type "$l"); done
runw bd-xt-xtide-format c.nvr aucune boot-diff roms 8000 --config xt-xtide.cfg --fda "$REPO/$DOS" --type-at 8000 --type-settle 20 "${XF[@]}"
for m in ibmpc:pc ibmxt:xt olivetti_m24:m24 pc1512:pc1512; do
  runw bd-${m#*:}-xtide-boot c.nvr aucune boot-diff roms 8000 --config xt-xtide-dos.cfg --model ${m%:*} --type-at 8000 --type-settle 300 --type "" --type "" --type "VER" --type "DIR" --type "MD G10" --type "COPY COMMAND.COM G10" --type "DIR G10"
done
