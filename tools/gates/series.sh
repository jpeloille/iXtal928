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
# G12.0 — les SB 1.0, 1.5, 2.0 et Pro v1 (sound_sb.c : sb_1_init à sb_pro_v1_init, le CT1335) : le 5150 et la carte,
# l'ami486 avec la 2.0 et son mélangeur en 250h, et la Pro v1, sous --expect-sb ; le banc SBBANC par carte
# (sbbanc.py --carte : les commandes que garde la version du DSP, les paramètres périmés de 7Dh, D1h après le
# lancement d'un DMA, le mélangeur de la carte, l'OPL2 et son miroir, les deux OPL2 de la Pro v1), sonde du son.
run bd-pc-sb1 boot-diff roms 3000 --sndcard sb
run bd-pc-sb15 boot-diff roms 3000 --sndcard sb1.5
run bd-pc-sb20 boot-diff roms 3000 --sndcard sb2.0
run bd-pc-sbpro1 boot-diff roms 3000 --sndcard sbprov1
run bd-ami486-sb20-mix boot-diff roms 3000 --config $C/ami486-sb20-mix.cfg --expect-sb 220,7,1
run bd-ami486-sbpro1 boot-diff roms 3000 --config $C/ami486-dx2.cfg --sndcard sbprov1 --expect-sb 220,7,1
for SBC in sb1:sb sb15:sb1.5 sb20:sb2.0 sbpro1:sbprov1; do
  mapfile -t SBK < tools/sbbanc/sbbanc-${SBC%%:*}.keys
  SBB=(); for l in "${SBK[@]}"; do SBB+=(--type "$l"); done
  run bd-pc-${SBC%%:*}-banc boot-diff roms 11000 --sndcard ${SBC#*:} --fda $DOS --fdb os/pcdos20/pcdos20s.img --type-at 7000 --type-settle 600 --type "" --type "" "${SBB[@]}" --type "^" --type "^"
done
mapfile -t SBK < tools/sbbanc/sbbanc-sb20mix.keys
SBB=(); for l in "${SBK[@]}"; do SBB+=(--type "$l"); done
run bd-pc-sb20mix-banc boot-diff roms 11000 --config $C/pc-sb20-mix.cfg --fda $DOS --fdb os/pcdos20/pcdos20s.img --type-at 7000 --type-settle 600 --type "" --type "" "${SBB[@]}" --type "^" --type "^"
# G12.1 — la SB 16 (le DSP 16 bits, le CT1745, le MPU-401, le FIR) : le POST de l'ami486 sous --expect-sb (le DMA 16
# bits compris) et la sonde du DMA ; le refus sur l'XT (la règle ISA 16 bits) ; les 51 coefficients du FIR, oracle
# contre C#, pour 1 à 65 535 Hz et les 256 constantes de 40h ; SB16BANC (tools/sb16banc, GNU as) saisi dans DEBUG,
# DOS 5 amorcé du disque SCSI de G11 : le DSP 4.05 et l'ASP, le CT1745, le MPU-401, le DMA 8 et 16 bits, la
# tonalité adverse (PB-155), l'enregistrement stéréo au-delà de FFFEh (PB-151). r9-sb16, en C# seul : la
# fréquence 0 (PB-150), le refus sur les quatre machines 8 bits, deux survies.
run bd-ami486-sb16 boot-diff roms 3000 --config $C/ami486-dx2.cfg --sndcard sb16 --expect-sb 220,7,1,5
run bd-ibmxt-sb16-refus boot-diff roms 3000 --model ibmxt --sndcard sb16
run sb16-filter-check sb16-filter-check
mapfile -t S16K < tools/sb16banc/sb16banc.keys
S16B=(); for l in "${S16K[@]}"; do S16B+=(--type "$l"); done
runw bd-ami486-sb16-banc aha486.nvr ami486 boot-diff roms 3000 --config ami486-sb16-banc.cfg --type-at 3000 --type-settle 40 --type "" --type "" "${S16B[@]}" --type "@wait 3000" --expect-aha --expect-sb 220,7,1,5
run r9-sb16 r9-sb16
# G12.2 — l'AWE32 et son EMU8000 : les onze tables d'emu8k_init et la ROM chargée, oracle contre C#, au bit près ;
# les sept noyaux (peigne, diffuseur, queue, amortisseur, chorus, réverbération, volume) sur des états fabriqués ;
# le POST de l'ami486 sous --expect-emu (la puce en 620h, sa RAM à la taille dite, la ROM des mesures exigée), à
# 512 Ko, sans RAM et à 28 Mo ; le refus sur l'XT ; AWEBANC (tools/awebanc, GNU as) saisi dans DEBUG, DOS 5 amorcé
# du disque SCSI de G11 : la détection, l'initialisation, la DRAM et son repli, la ROM, des notes, le chorus et la
# réverbération, un chorus extrême (PB-162). r9-emu8k et r9-awecfg, en C# seul : la réverbération au-delà de ses
# tampons (PB-161) ; la ROM absente ou courte, l'XT, les clés hors liste (PB-93).
run emu8k-tables-check emu8k-tables-check
run emu8k-kernel-check emu8k-kernel-check
run bd-ami486-awe32 boot-diff roms 3000 --config $C/ami486-dx2.cfg --sndcard sbawe32 --expect-sb 220,7,1,5 --expect-emu 620,512
run bd-ami486-awe32-ram0 boot-diff roms 3000 --config $C/ami486-awe32-ram0.cfg --expect-sb 220,7,1,5 --expect-emu 620,0
run bd-ami486-awe32-ram28 boot-diff roms 3000 --config $C/ami486-awe32-ram28.cfg --expect-sb 220,7,1,5 --expect-emu 620,28672
run bd-ibmxt-awe32-refus boot-diff roms 3000 --model ibmxt --sndcard sbawe32
mapfile -t AWK < tools/awebanc/awebanc.keys
AWB=(); for l in "${AWK[@]}"; do AWB+=(--type "$l"); done
runw bd-ami486-awe32-banc aha486.nvr ami486 boot-diff roms 3000 --config ami486-awe32-banc.cfg --type-at 3000 --type-settle 40 --type "" --type "" "${AWB[@]}" --type "@wait 3000" --expect-aha --expect-sb 220,7,1,5 --expect-emu 620,512
run r9-emu8k r9-emu8k
run r9-awecfg r9-awecfg
# G12.3 — les témoins, sous l'oracle : TEST-SBP.EXE 1.91 (la disquette 1 de la SB Pro v2, sbpro2-1.img de la recette
# g5w, en B:), DOS 5 amorcé du disque SCSI de G11. Sur la Pro v2 et la Pro v1 : la détection (220h, l'IRQ 5 essayée
# puis la 7, le DMA 1, la version du DSP), puis le menu entier — la musique FM à deux et à quatre opérateurs, le son
# numérisé (14h) — et Échap. Sur la 16 et l'AWE32, le programme trouve 220h puis s'arrête sur « Error code: 0100 ».
TSP=(--type "B:" --type "TEST-SBP" --type "@wait 300")
for i in 1 2 3 4 5 6 7; do TSP+=(--type "" --type "@wait 300"); done
TSP+=(--type "" --type "@wait 1500" --type $'\x19' --type "@wait 4000" --type $'\x19' --type "@wait 1500" --type $'\x1b' --type "@wait 300")
TS16=(--type "B:" --type "TEST-SBP" --type "@wait 300")
for i in 1 2 3 4; do TS16+=(--type "" --type "@wait 300"); done
runw bd-ami486-sbpro2-testsbp aha486.nvr ami486 boot-diff roms 3000 --config ami486-sb16-banc.cfg --sndcard sbprov2 --fdb "$WORK/sbpro2-1.img" --type-at 3000 --type-settle 40 --type "" --type "" "${TSP[@]}" --expect-aha --expect-sb 220,7,1
runw bd-ami486-sbpro1-testsbp aha486.nvr ami486 boot-diff roms 3000 --config ami486-sb16-banc.cfg --sndcard sbprov1 --fdb "$WORK/sbpro2-1.img" --type-at 3000 --type-settle 40 --type "" --type "" "${TSP[@]}" --expect-aha --expect-sb 220,7,1
runw bd-ami486-sb16-testsbp aha486.nvr ami486 boot-diff roms 3000 --config ami486-sb16-banc.cfg --fdb "$WORK/sbpro2-1.img" --type-at 3000 --type-settle 40 --type "" --type "" "${TS16[@]}" --expect-aha --expect-sb 220,7,1,5
runw bd-ami486-awe32-testsbp aha486.nvr ami486 boot-diff roms 3000 --config ami486-awe32-banc.cfg --fdb "$WORK/sbpro2-1.img" --type-at 3000 --type-settle 40 --type "" --type "" "${TS16[@]}" --expect-aha --expect-sb 220,7,1,5 --expect-emu 620,512
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
# G10.3 — le moteur d'images de CD (cdrom_image.cpp, cdrom-image.cc, cdrom-null.c), hors machine. Les
# deux portes écrivent elles-mêmes les images d'isogen dans le TMPDIR de la série, les vérifient contre
# tools/isogen/isogen.sha256 et les effacent en sortant, l'image creuse de 2,5 Go comprise (PB-107).
# cdimage-check : les deux côtés appel par appel, état comparé après chacun ; r9-cue : les deux feuilles
# CUE qui font tomber PCem, en C# seul.
run cdimage-check cdimage-check
run r9-cue r9-cue
# G10.4 — l'ATAPI (ide_atapi.c, scsi.c, scsi_cd.c) : le CD-ROM en maître secondaire de l'ami486
# (cdrom_channel = 2). bd-ami486-cd-vide et -iso : le POST entier, lecteur vide puis chargé de
# iso-2048.iso d'isogen (recette g5w), CR-587-B à 4x ; --expect-cd exige le lecteur et son pilote des
# deux côtés — le BIOS ne touche jamais le canal secondaire (mesuré), le diff d'instructions ne le
# verrait pas perdu. ATAPIBANC (tools/atapibanc) saisi dans DEBUG sur C: et lancé, lecteur chargé
# puis vide : signature, IDENTIFY PACKET, TEST UNIT READY, REQUEST SENSE, INQUIRY, READ CAPACITY
# (PB-117), READ(10) du PVD et de quatre blocs DRQ, le secteur 32 refusé, READ TOC, MODE SENSE,
# GET EVENT STATUS (PB-118), un code inconnu. r9-atapi et r9-cdcfg, en C# seul : les sites où PCem
# s'arrête (PB-113 à PB-116) et les clés du lecteur (PB-93, PB-110).
runw bd-ami486-cd-vide c.nvr aucune boot-diff roms 40000 --config ami486-cd-vide.cfg --expect-cd 2,vide
runw bd-ami486-cd-iso c.nvr aucune boot-diff roms 40000 --config ami486-cd-iso.cfg --expect-cd 2,image
mapfile -t ABK < tools/atapibanc/atapibanc.keys
ABB=(); for l in "${ABK[@]}"; do ABB+=(--type "$l"); done
runw bd-ami486-atapi-banc c486.nvr ami486 boot-diff roms 60000 --config ami486-atapi.cfg --type-at 60000 --type-settle 600 "${ABB[@]}" --type "^" --type "^" --type "^" --expect-cd 2,image
runw bd-ami486-atapi-banc-vide c486.nvr ami486 boot-diff roms 60000 --config ami486-atapi-vide.cfg --type-at 60000 --type-settle 600 "${ABB[@]}" --type "^" --type "^" --type "^" --expect-cd 2,vide
# G10.5 — l'audio CD dans la machine : le fil CD de sound.c à l'échéance de sound_poll (décision n° 7),
# givealbuffer_cd comparé par la sonde du CD. ATAPIAUD (atapibanc.py --audio) sur mixte.cue d'isogen,
# la SB Pro v2 montée (le volume CD du CT1345) : READ TOC, PLAY AUDIO sur la piste de données (refusé),
# PLAY AUDIO(10) en LBA 42 (rien ne joue : la position prise décalée de 150), la page audio (canaux
# croisés, volumes 80h et 40h), PLAY AUDIO MSF de la piste 2, PAUSE, RESUME, PLAY AUDIO(12) de la piste 3
# à l'adresse plus 150 (refusé, PB-123), STOP, READ SUB-CHANNEL entre chaque ; --expect-cd-son exige des
# échantillons non nuls des deux côtés.
mapfile -t AAK < tools/atapibanc/atapibanc-audio.keys
AAB=(); for l in "${AAK[@]}"; do AAB+=(--type "$l"); done
runw bd-ami486-atapi-audio c486.nvr ami486 boot-diff roms 60000 --config ami486-atapi-audio.cfg --type-at 60000 --type-settle 600 "${AAB[@]}" --type "^" --type "^" --type "^" --expect-cd 2,image --expect-cd-son
# G10.6 — le lecteur ZIP 100 (scsi_zip.c) en maître secondaire de l'ami486 (zip_channel = 2), zip100.img de la
# recette g5w, chargé à la fin de l'amorçage (zip_path, DEVIATION). ZIPBANC (atapibanc.py --zip) : la signature,
# IDENTIFY PACKET, TEST UNIT READY (UNIT ATTENTION puis GOOD), INQUIRY, READ CAPACITY (PB-126), READ FORMAT
# CAPACITIES, MODE SENSE, IOMEGA SENSE, WRITE(10) et WRITE(6) d'un secteur relus par READ(10) et READ(6), le secteur
# 196 608 (PB-126), VERIFY, SEEK, REZERO, SEND DIAGNOSTIC, RESERVE, RELEASE, une LUN non nulle, un code inconnu,
# START STOP UNIT (PB-126) ; l'image ZIP comparée des deux côtés. r9-zip, en C# seul : les sites où PCem s'arrête ou
# déborde (PB-125).
mapfile -t ZBK < tools/atapibanc/zipbanc.keys
ZBB=(); for l in "${ZBK[@]}"; do ZBB+=(--type "$l"); done
runw bd-ami486-zip-banc c486.nvr ami486 boot-diff roms 60000 --config ami486-zip.cfg --type-at 60000 --type-settle 600 "${ZBB[@]}" --type "^" --type "^" --type "^"
run r9-zip r9-zip
run r9-atapi r9-atapi
run r9-cdcfg r9-cdcfg
# G11 — l'Adaptec AHA-1542C (scsi_aha1540.c) et ses disques (scsi_hd.c), sous la sonde de la carte (--expect-aha :
# les trois machines d'états, les mailbox, le CCB, l'EEPROM, la RAM d'ombre, le bus, chaque disque ; et aucun
# fatal() de l'oracle). bd-ami486-aha-post : le POST de la ROM v1.01, un disque vierge à l'ID 0. -format : FDISK,
# FORMAT C:/S, DEBUG.EXE décompressé par EXPAND (la disquette 3 en B:), DIR, MD depuis la disquette DOS 5
# (aha-format.keys, le script dont la recette g5w tire scsic.img) ;
# l'image C: comparée, la dernière écriture prouvant le vidage de closepc (décision n° 7). -boot : DOS 5 amorcé
# depuis le disque SCSI par l'INT 13h de la ROM, sur les trois machines AT ; -c8 : la carte en 330h, sa ROM en
# C8000h (la section du device). bd-ibmxt-aha-refus : la règle ISA 16 bits, la carte refusée des deux côtés.
runw bd-ami486-aha-post aha486.nvr ami486 boot-diff roms 6000 --config ami486-aha-vierge.cfg --expect-aha
mapfile -t AFK < tools/gates/aha-format.keys
AFB=(); for l in "${AFK[@]}"; do AFB+=(--type "$l"); done
runw bd-ami486-aha-format aha486.nvr ami486 boot-diff roms 2500 --config ami486-aha-vierge.cfg --fda "$WORK/dos5-1.img" --fdb "$WORK/dos5-3.img" --type-at 2500 --type-settle 20 "${AFB[@]}" --expect-aha
AHB=(--type-at 3000 --type-settle 300 --type "" --type "" --type "VER" --type "DIR C:" --type "MD C:\G11" --type "DIR C:" --expect-aha)
runw bd-ami486-aha-boot aha486.nvr ami486 boot-diff roms 3000 --config ami486-aha.cfg "${AHB[@]}"
runw bd-ami386dx-aha-boot aha386.nvr ami386dx_opti495 boot-diff roms 3000 --config ami386dx-aha.cfg "${AHB[@]}"
runw bd-ami286-aha-boot aha286.nvr ami286 boot-diff roms 3000 --config ami286-aha.cfg "${AHB[@]}"
runw bd-ami486-aha-c8 aha486.nvr ami486 boot-diff roms 3000 --config ami486-aha-c8.cfg "${AHB[@]}"
run bd-ibmxt-aha-refus boot-diff roms 3000 --model ibmxt --hdd-controller aha1542c
# G11.3 — les témoins, sous l'oracle : DOS 5 amorcé du disque SCSI, VER, MEM, CHKDSK C:, et MSD /S (la disquette 3
# de Windows 3.11 en B:), qui voit le disque C:.
runw bd-ami486-aha-temoins aha486.nvr ami486 boot-diff roms 3000 --config ami486-aha.cfg --fdb "$WORK/win3-3.img" --type-at 3000 --type-settle 600 --type "" --type "" --type "VER" --type "MEM" --type "CHKDSK C:" --type "@wait 1500" --type "B:MSD /S" --type "@wait 3000" --expect-aha
# G11.2 — AHABANC (tools/ahabanc, ahabanc.S) : un programme qui parle à la carte comme un pilote ASPI, saisi dans
# DEBUG : les commandes d'hôte (dont une invalide, RETURN SETUP DATA de 44 octets, l'EEPROM, le canal 2, les
# interrupteurs), trente-six CCB de mailbox sur les disques des ID 0 et 1 (INQUIRY, EVPD, READ CAPACITY, READ et
# WRITE (6) et (10), le secteur de la capacité, MODE SENSE et MODE SELECT, le sense retenu, le LUN fantôme, l'ID 5
# vide, la CDB courte et la CDB vide, les commandes simulées, le résidu, la dispersion, un READ de 600 secteurs
# au-delà de data_in), les commandes BIOS 03h qui achèvent un CCB périmé, ABORT, 22h au-delà de params. Chaque
# octet rendu ou écrit par la carte relu par le programme ; l'image D: (l'ID 1) comparée. r9-aha et r9-scsihd,
# en C# seul : les sites où PCem s'arrête ou déborde (PB-135, PB-128), et les configurations ramenées.
mapfile -t AHK < tools/ahabanc/ahabanc.keys
AHBB=(); for l in "${AHK[@]}"; do AHBB+=(--type "$l"); done
runw bd-ami486-aha-banc aha486.nvr ami486 boot-diff roms 3000 --config ami486-aha-banc.cfg --type-at 3000 --type-settle 40 --type "" --type "" "${AHBB[@]}" --type "@wait 1500" --expect-aha
run r9-aha r9-aha
run r9-scsihd r9-scsihd
# G13.0 — ce qui n'attendait pas G13 (PLAN-G13.md), en C# seul. r9-cga et r9-m24 : R1 au-delà de 128 en 80 colonnes
# (PB-09, le tampon de la CGA à 512 octets ; PB-88, la garde de la M24 atteinte). r9-disquette : une piste de plus de
# 20 Ko (PB-17), une BPB à 0 secteur par piste (PB-168), un fichier vide. reset-scsi-check : l'image SCSI à travers un
# reset matériel (PB-121). r9-filet : savenvr sans nvr/ (PB-33), puis, dans un processus à part, un plantage injecté
# après l'écriture d'un secteur du disque dur de l'XT, contre une sortie normale (le filet des images).
run r9-cga r9-cga
run r9-m24 r9-m24
run r9-disquette r9-disquette
run reset-scsi-check reset-scsi-check
run r9-filet r9-filet
# G13.1 — le recensement : PCEM_BUGS.md contre les marqueurs du code. Chaque marqueur porte son numéro, qui a son
# entrée ; chaque défaut reproduit des sections A et B a ses sites, sa source, son cas qui discrimine et son champ G13 ;
# aucun marqueur ne contredit le statut de son entrée.
run recensement recensement
# G13.2 — le mode matériel (PLAN-G13.md, R10) : son mécanisme, puis le pilote PB-01, l'AF d'ADC et de SBB du 8088.
# materiel-mode : les refus (l'oracle est PCem), la clé hardware_mode, --hardware-mode, le gel. materiel-cas : le cas qui
# discrimine PB-01, la valeur de PCem en mode PCem, celle du 8088 en mode matériel. banc-pb01 : le même défaut sous DEBUG
# de PC-DOS 2.00 (STC, MOV AL,9, ADC AL,6, DAA : AX=0010 contre AX=0016), chaque mode joué deux fois, le déterminisme.
# fuite-pb01 : le fuzzeur, PB-01 seul corrigé ; toute divergence avec l'oracle tombe dans son périmètre (AF d'ADC et de
# SBB, et son image empilée), et au moins une s'y produit.
run materiel-mode materiel-mode
run materiel-cas-pb01 materiel-cas PB-01
run materiel-cas-pb01-materiel --hardware-mode PB-01 materiel-cas PB-01
run banc-pb01 banc tools/gates/bancs/pb01-debug.attendus -- --boot roms 8000 --model ibmxt --floppy-a $DOS \
  --floppy-b os/pcdos20/pcdos20s.img --settle 600 --type '' --type '' --type B:DEBUG --type 'A 100' --type STC \
  --type 'MOV AL,9' --type 'ADC AL,6' --type DAA --type 'INT 3' --type '' --type G=100 --type Q
run banc-pb01-materiel --hardware-mode PB-01 banc tools/gates/bancs/pb01-debug.attendus -- --boot roms 8000 \
  --model ibmxt --floppy-a $DOS --floppy-b os/pcdos20/pcdos20s.img --settle 600 --type '' --type '' --type B:DEBUG \
  --type 'A 100' --type STC --type 'MOV AL,9' --type 'ADC AL,6' --type DAA --type 'INT 3' --type '' --type G=100 \
  --type Q
run fuite-pb01 --hardware-mode PB-01 fuzz --mode single --iter 100000 $ALL --fuite
# G13.2 — SingleStepTests, le silicium, en C# seul : le 8088 (AMD D8088) et le 8086 (Intel), 103 formes chacun, corpus
# récupérés par tools/fetch-sst.sh et fetch-sst8086.sh dans vectors/ (gitignoré, décision n° 5) ; une forme dont les
# vecteurs manquent rend la porte rouge. En mode PCem, le C# rend les lignes de base de l'oracle à l'identique ; en
# mode matériel, celles du mode : depuis G13.3, tout le domaine du processeur corrigé (PB-01 en G13.2).
run sst8088 sst-probe --target csharp --limit 10000 --attendu sst-baseline.tsv
run sst8088-materiel --hardware-mode processeur sst-probe --target csharp --limit 10000 \
  --attendu sst-baseline-materiel.tsv
run sst8086 sst-probe --cpu 8086 --target csharp --limit 10000 --attendu sst8086-baseline.tsv
run sst8086-materiel --hardware-mode processeur sst-probe --cpu 8086 --target csharp --limit 10000 \
  --attendu sst8086-baseline-materiel.tsv
# G13.3 — le 8088 et le 8086 (PLAN-G13.md) : dix-sept corrections, en trois groupes qui ne valent qu'ensemble
# (PB-03 et PB-257 ; PB-07 et PB-179 ; PB-45, PB-169 et PB-258). Pour chacune, le cas qui discrimine en C# seul : la
# valeur de PCem en mode PCem, celle du matériel en mode matériel, la sonde qui dit que la correction a servi. PB-03
# joue --timer-check sur le 5150 (aucun cycle consommé hors du TSC) ; PB-257, le 8237 d'un PC, canal masqué.
for pb in 02 03 07 45 87 169 170 171 172 173 174 175 176 177 179 257 258; do
  run materiel-cas-pb$pb materiel-cas PB-$pb
  run materiel-cas-pb$pb-materiel --hardware-mode PB-$pb materiel-cas PB-$pb
done
# L'IDIV qu'un REP précède rend l'opposé de son quotient : la correction d'IDIV (PB-45), dans l'instruction que PB-177
# garde entière.
run materiel-cas-pb177-idiv --hardware-mode PB-177,PB-45 materiel-cas PB-177
# Le contrôle de fuite des corrections qui ont un périmètre d'instruction (Fuzzer.HorsDuPerimetre) : toute divergence
# avec l'oracle tombe dans l'instruction qu'une correction vise.
run fuite-g133 --hardware-mode PB-02,PB-45,PB-87,PB-170,PB-171,PB-172,PB-173,PB-174,PB-175,PB-176,PB-177 fuzz \
  --mode single --iter 100000 $ALL --fuite
# G13.3, suite — le temps d'un REP devant autre chose qu'une chaîne (PB-177), le cœur contre la trace du silicium : le
# surcoût du REP devant IDIV, dans les cas que SST fait partir d'une file vide comme la sonde, dans deux erreurs types.
run sst-rep-temps-materiel --hardware-mode processeur sst-rep-temps --op F6.7 --op F7.7 --controle
# G13.4 — le 8259 (PLAN-G13.md) : PB-05, PB-246, PB-247, PB-248 et PB-255, chacun son cas en C# seul dans les deux
# modes ; puis PICBANC (tools/picbanc), saisi dans DEBUG sur l'XT et sur l'IBM AT (F1 au POST : le bac à sable n'a
# pas d'at.nvr, et le BIOS dit 162), contre l'oracle en mode PCem (boot-diff) et, en C# seul, contre ses attendus dans
# les deux modes ; en mode matériel, tout le domaine de la carte mère.
for pb in 05 246 247 248 255; do
  run materiel-cas-pb$pb materiel-cas PB-$pb
  run materiel-cas-pb$pb-materiel --hardware-mode PB-$pb materiel-cas PB-$pb
done
mapfile -t PCK < tools/picbanc/picbanc.keys
PCB=(); for l in "${PCK[@]}"; do PCB+=(--type "$l"); done
run bd-xt-picbanc boot-diff roms 9000 --model ibmxt --fda $DOS --fdb os/pcdos20/pcdos20s.img --type-at 6000 \
  --type-settle 600 --type "" --type "" "${PCB[@]}" --type "@wait 1500"
run bd-ibmat-picbanc boot-diff roms 9000 --model ibmat --fda $DOS --fdb os/pcdos20/pcdos20s.img --type-at 3000 \
  --type-settle 600 --type $'\x01' --type "@wait 900" --type "" --type "" "${PCB[@]}" --type "@wait 1500"
for m in "" "-materiel"; do
  h=(); [ -n "$m" ] && h=(--hardware-mode carte-mere)
  run banc-picbanc-xt$m "${h[@]}" banc tools/gates/bancs/picbanc-xt.attendus -- --boot roms 9000 --model ibmxt \
    --floppy-a $DOS --floppy-b os/pcdos20/pcdos20s.img --settle 600 --type "" --type "" "${PCB[@]}" --type "@wait 1500"
  run banc-picbanc-at$m "${h[@]}" banc tools/gates/bancs/picbanc-at.attendus -- --boot roms 3000 --model ibmat \
    --floppy-a $DOS --floppy-b os/pcdos20/pcdos20s.img --settle 600 --type $'\x01' --type "@wait 900" --type "" \
    --type "" "${PCB[@]}" --type "@wait 1500"
done
# G13.4 — le 8237 : PB-157, PB-249 à PB-253, chacun son cas en C# seul dans les deux modes ; puis DMABANC
# (tools/dmabanc), comme PICBANC, sur l'XT et sur l'IBM AT.
for pb in 157 249 250 251 252 253; do
  run materiel-cas-pb$pb materiel-cas PB-$pb
  run materiel-cas-pb$pb-materiel --hardware-mode PB-$pb materiel-cas PB-$pb
done
mapfile -t DCK < tools/dmabanc/dmabanc.keys
DCB=(); for l in "${DCK[@]}"; do DCB+=(--type "$l"); done
run bd-xt-dmabanc boot-diff roms 9000 --model ibmxt --fda $DOS --fdb os/pcdos20/pcdos20s.img --type-at 6000 \
  --type-settle 600 --type "" --type "" "${DCB[@]}" --type "@wait 900"
run bd-ibmat-dmabanc boot-diff roms 9000 --model ibmat --fda $DOS --fdb os/pcdos20/pcdos20s.img --type-at 3000 \
  --type-settle 600 --type $'\x01' --type "@wait 900" --type "" --type "" "${DCB[@]}" --type "@wait 900"
for m in "" "-materiel"; do
  h=(); [ -n "$m" ] && h=(--hardware-mode carte-mere)
  run banc-dmabanc-xt$m "${h[@]}" banc tools/gates/bancs/dmabanc-xt.attendus -- --boot roms 9000 --model ibmxt \
    --floppy-a $DOS --floppy-b os/pcdos20/pcdos20s.img --settle 600 --type "" --type "" "${DCB[@]}" --type "@wait 900"
  run banc-dmabanc-at$m "${h[@]}" banc tools/gates/bancs/dmabanc-at.attendus -- --boot roms 3000 --model ibmat \
    --floppy-a $DOS --floppy-b os/pcdos20/pcdos20s.img --settle 600 --type $'\x01' --type "@wait 900" --type "" \
    --type "" "${DCB[@]}" --type "@wait 900"
done
# G13.4 — le 8042, la souris PS/2 et les ports : PB-254, PB-94, PB-95, PB-261, PB-101 et PB-103, chacun son cas en C#
# seul dans les deux modes ; puis, en C# seul sous --boot et contre leurs attendus dans les deux modes, PS2BANC (la souris montée
# par --force-ps2, porte de vérification ; le bouton du milieu tenu par @souris) et JOYBANC sur la CH et la TM (le
# chapeau à 315° par @manette) — boot-diff les joue contre l'oracle plus haut, et refuse le mode matériel.
for pb in 94 95 101 103 254 261; do
  run materiel-cas-pb$pb materiel-cas PB-$pb
  run materiel-cas-pb$pb-materiel --hardware-mode PB-$pb materiel-cas PB-$pb
done
for m in "" "-materiel"; do
  h=(); [ -n "$m" ] && h=(--hardware-mode carte-mere)
  for mt in 2 3; do
    runw banc-ps2banc-$mt$m f386.nvr ami386dx_opti495 "${h[@]}" banc "$REPO/tools/gates/bancs/ps2banc-$mt.attendus" -- \
      --boot roms 2500 --config ami386dx-fd.cfg --force-ps2 $mt --floppy-a "$REPO/$DOS" \
      --floppy-b "$REPO/os/pcdos20/pcdos20s.img" --settle 600 --type "" --type "" --type "@souris 0,0,4" "${P2B[@]}" \
      --type "@wait 3000"
  done
  for t in 4:ch 6:tm; do
    run banc-joybanc-${t#*:}$m "${h[@]}" banc tools/gates/bancs/joybanc-${t#*:}.attendus -- --boot roms 7000 \
      --joystick-type ${t%%:*} --floppy-a $DOS --floppy-b os/pcdos20/pcdos20s.img --settle 150 \
      --type "@manette 0,0,0,0,315" --type "" --type "" "${JOB[@]}" --type "@wait 1500"
  done
done
# G13.5a — le cœur 286/386/486 (PLAN-G13.md) : l'AF d'ADC, LOCK, BT, BTS, BTR et BTC (le décalage signé, l'adresse de
# 16 bits qui replie, l'immédiat modulo 16), MOVSX r16, AAA et AAS, AAD et AAM, AAM 0, DAS. Pour chacune, le cas qui
# discrimine en C# seul : la valeur de PCem en mode PCem, celle du matériel en mode matériel. Puis le silicium : les
# corpus SST du 386 (386EX) et du 286 (Harris N80C286-12, sur la carte de 16 Mo), 150 cas par forme ; en mode PCem, le
# C# rend la ligne de l'oracle sur ces cas ; en mode matériel, celle du mode. Les corpus entiers : sst386-baseline-
# materiel.tsv et sst286-baseline-materiel.tsv (VERIFICATION.md § G13.5a).
for pb in 181 182 183 184 185 186 187 188 262; do
  run materiel-cas-pb$pb materiel-cas PB-$pb
  run materiel-cas-pb$pb-materiel --hardware-mode PB-$pb materiel-cas PB-$pb
done
for c in 386 286; do
  run sst$c sst$c-probe --target csharp --limit 150 --attendu tools/gates/sst/sst$c-150.tsv
  run sst$c-materiel --hardware-mode processeur sst$c-probe --target csharp --limit 150 \
    --attendu tools/gates/sst/sst$c-materiel-150.tsv
done
