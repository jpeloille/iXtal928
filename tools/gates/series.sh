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
for g in cga vga tvga8900d tvga9000b; do
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
