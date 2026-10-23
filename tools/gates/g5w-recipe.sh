#!/usr/bin/env bash
# g5w-recipe.sh [WORK=/tmp/g5w] — fabrique l'espace des portes disque de series.sh.
#
# Les portes disque (G5) amorcent les disques de l'utilisateur. Les images ne sont pas
# versionnées (os/ ne l'est pas) : on versionne la RECETTE, et les empreintes qu'elle doit
# rendre. Tout est fait sur des COPIES ; os/ n'est jamais écrit.
#   c286.img, c386.img : os/286-HDD-C.img et os/386-HDD-C.img, AUTOEXEC.BAT sans KEYB FR
#                        (KeyScript tape en QWERTY) ;
#   vierge46.img       : 156 Mo de zéros, type 46 ;
#   c8088.img          : os/8088-HDD-C.img, VIERGE (306 × 4 × 17), lue seulement (G10.2) ;
#   c8088dos.img       : c8088.img partitionnée et formatée PAR ÉMULATION, en C# seul (iXtal26
#                        --boot --in-place, xtide-format.keys : FDISK, « a » au menu du XTIDE,
#                        FORMAT C:/S), avec une copie de la disquette PC-DOS 2.00 en A: ; égale
#                        octet par octet à l'image C: que bd-xt-xtide-format écrit des deux côtés,
#                        par le même script aux mêmes tranches (VERIFICATION.md § G10.2) ;
#   iso-2048.iso       : l'ISO de 32 secteurs d'isogen (tools/isogen), le disque des portes du CD (G10.4) ;
#   mixte.cue, mixte.bin : la feuille d'isogen, une piste de données et deux pistes audio (G10.5) ;
#   zip100.img         : un disque ZIP 100 vierge, 100 663 296 octets nuls (G10.6) ;
#   zipro.img          : le disque de ZIPESC, trois secteurs remplis (atapibanc.py --image-ro), en lecture seule
#                        (chmod 444 : le lecteur ZIP l'ouvre protégé en écriture) (GR.2d2) ;
#   dos5-1.img         : os/Dos 5.0/Disk01.img, AUTOEXEC.BAT ramené à « @echo off » (sans SETUP) (G11) ;
#   dos5-3.img         : os/Dos 5.0/Disk03.img, telle quelle (EXPAND.EXE, en B:) (G11) ;
#   win3-3.img         : os/Windows_3-11/Disk3.IMA, telle quelle (MSD.EXE, le témoin de G11.3) ;
#   sbpro2-1.img       : os/sbpro2-disk1.img, telle quelle (TEST-SBP.EXE, le témoin de G12.3) ;
#   scsi20.img         : un disque SCSI vierge de 20 Mio, 64 × 32 × 20, à la taille exacte (G11) ;
#   scsic.img          : scsi20.img partitionné et formaté PAR ÉMULATION, en C# seul, sur l'AHA-1542C
#                        (aha-format.keys : FDISK, FORMAT C:/S, DEBUG, MEM et CHKDSK décompressés par
#                        EXPAND, DIR, MD) ;
#                        égal octet par octet à l'image C: que
#                        bd-ami486-aha-format écrit des deux côtés (G11) ;
#   aha486.nvr, aha386.nvr, aha286.nvr : CMOS sans disque des trois machines de l'AHA-1542C (G11) ;
#   at-mda.nvr, herc286.nvr : CMOS sans disque de l'IBM AT et de la MDA, de l'ami286 et de l'Hercules, 30h en
#                        0x14 (GR.3c) ;
#   xtide-outils.img   : une disquette de 360 Ko, BIOSDRVS.COM et XTIDECFG.COM de roms/xtide (GR.3f) ;
#   c286-ega.img, c286-herc.img : c286.img, Windows réglé par son SETUP DOS pour l'EGA et l'Hercules, PAR ÉMULATION
#                        en C# seul (GR.3g) ; c286herc.nvr : leur CMOS de l'Hercules (30h, le disque C:) ;
#   *.cfg              : tools/gates/cfg/*.cfg.in, @WORK@ remplacé ;
#   ixtal26-486-porte.cfg : le profil ixtal26-486.cfg, son disque remplacé par c386.img, son D: retiré (GR.3b) ;
#   c.nvr, cd.nvr, c386.nvr, c486.nvr, f386.nvr : CMOS fabriqués par --make-nvr (type 46 en C:,
#                        en C: et D: ; ami386dx ; ami486, G6.4 ; ami386dx sans disque, PS2.0) ;
#   nvr/default        : les CMOS de référence de PCem.
# Puis les empreintes sont comparées à tools/gates/g5w.sha256. Un écart veut dire que les
# disques de os/ ont changé (on s'en sert : ce sont les disques des profils Rider) — les
# portes restent valables, mais leurs comptes d'instructions changent : refaire la référence
# (`--fix`, et le noter dans VERIFICATION.md).
set -eu
REPO=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
WORK=${1:-/tmp/g5w}; [ "${1:-}" = --fix ] && WORK=/tmp/g5w
FIX=0; for a in "$@"; do [ "$a" = --fix ] && FIX=1; done
G="$REPO/tools/gates"
BIN="$REPO/iXtal26/bin/Release/net10.0/iXtal26.dll"
[ -f "$BIN" ] || dotnet build "$REPO/iXtal26/iXtal26.csproj" -c Release > /dev/null
rm -rf "$WORK"; mkdir -p "$WORK/nvr"
cp -r "$REPO/nvr/default" "$WORK/nvr/"
for m in 286 386; do
  cp "$REPO/os/$m-HDD-C.img" "$WORK/c$m.img"
  python3 "$G/fatpatch.py" "$WORK/c$m.img" AUTOEXEC.BAT "$G/autoexec-sans-keyb.bat"
done
truncate -s 159805440 "$WORK/vierge46.img"
cp "$REPO/os/8088-HDD-C.img" "$WORK/c8088.img"                   # G10.2
cp "$WORK/c8088.img" "$WORK/c8088dos.img"
cp "$REPO/os/pcdos20/pcdos20b.img" "$WORK/pcdos20b-xtide.img"
python3 "$REPO/tools/isogen/isogen.py" "$WORK" iso-2048.iso mixte.cue mixte.bin > /dev/null   # G10.4, G10.5
truncate -s 100663296 "$WORK/zip100.img"                         # G10.6
python3 "$REPO/tools/atapibanc/atapibanc.py" --image-ro "$WORK/zipro.img"; chmod 444 "$WORK/zipro.img"   # GR.2d2
[ ! -w "$WORK/zipro.img" ] || { echo "zipro.img reste inscriptible (root ?) : ZIPESC rougirait" >&2; exit 1; }
cp "$REPO/os/Dos 5.0/Disk01.img" "$WORK/dos5-1.img"              # G11
cp "$REPO/os/Dos 5.0/Disk03.img" "$WORK/dos5-3.img"
cp "$REPO/os/Windows_3-11/Disk3.IMA" "$WORK/win3-3.img"
cp "$REPO/os/sbpro2-disk1.img" "$WORK/sbpro2-1.img"             # G12.3
printf '@echo off\r\n' > "$WORK/ae-dos5.bat"
python3 "$G/fatpatch.py" "$WORK/dos5-1.img" AUTOEXEC.BAT "$WORK/ae-dos5.bat" --disquette; rm "$WORK/ae-dos5.bat"
truncate -s 20971520 "$WORK/scsi20.img"
cp "$WORK/scsi20.img" "$WORK/scsic.img"
for t in "$G"/cfg/*.cfg.in; do sed "s|@WORK@|$WORK|g" "$t" > "$WORK/$(basename "$t" .in)"; done
# GR.3b — le profil ixtal26-486.cfg lui-même, son disque remplacé par la copie du WORK (bd-cfg-486-cd). Son D:
#   (os/486-HDD-D.img) retiré : la porte ne touche pas os/, et c486.nvr ne déclare que C:.
sed -e "s|^hdc_fn = os/486-HDD-C.img\$|hdc_fn = $WORK/c386.img|" -e '/^hdd_\(cylinders\|heads\|sectors\|fn\) = /d' \
  "$REPO/ixtal26-486.cfg" > "$WORK/ixtal26-486-porte.cfg"
grep -q "^hdc_fn = $WORK/c386.img\$" "$WORK/ixtal26-486-porte.cfg"
if grep -q '^hdd_\(cylinders\|heads\|sectors\|fn\) = ' "$WORK/ixtal26-486-porte.cfg"; then echo "ixtal26-486-porte.cfg : D: non retiré" >&2; exit 1; fi
cd "$WORK"
dotnet "$BIN" --make-nvr ami286-mfm.cfg c.nvr --force > /dev/null
dotnet "$BIN" --make-nvr ami286-cd.cfg cd.nvr --force > /dev/null
dotnet "$BIN" --make-nvr ami386dx-ide.cfg c386.nvr --force > /dev/null
dotnet "$BIN" --make-nvr ami486-ide.cfg c486.nvr --force > /dev/null   # G6.4
dotnet "$BIN" --make-nvr ami386dx-fd.cfg f386.nvr --force > /dev/null   # PS2.0
dotnet "$BIN" --make-nvr ami486-aha.cfg aha486.nvr --force > /dev/null   # G11
dotnet "$BIN" --make-nvr ami386dx-aha.cfg aha386.nvr --force > /dev/null
dotnet "$BIN" --make-nvr ami286-aha.cfg aha286.nvr --force > /dev/null
dotnet "$BIN" --make-nvr ibmat-mda.cfg at-mda.nvr --force > /dev/null   # GR.3c, 0x14 = 30h
dotnet "$BIN" --make-nvr ami286-hercules.cfg herc286.nvr --force > /dev/null
# GR.3f — la disquette des outils du XTIDE : BIOSDRVS et XTIDECFG, datés du jour du volume d'isogen (--floppy-put
#   prend l'heure du fichier de l'hôte : sans elle, l'empreinte suivrait le clone).
dotnet "$BIN" --create-floppy 360k xtide-outils.img > /dev/null
for o in biosdrvs.com xtidecfg.com; do cp "$REPO/roms/xtide/$o" "$o"; touch -d "1995-06-15 12:00:00" "$o"; done
dotnet "$BIN" --floppy-put xtide-outils.img biosdrvs.com xtidecfg.com > /dev/null
rm biosdrvs.com xtidecfg.com
# G10.2 — le disque amorçable du XTIDE, en C# seul, sur les copies du WORK (le journal reste là).
mapfile -t XF < "$G/xtide-format.keys"; XT=(); for l in "${XF[@]}"; do XT+=(--type "$l"); done
dotnet "$BIN" --boot "$REPO/roms" 8000 --config xt-xtide-prep.cfg --in-place --settle 20 "${XT[@]}" > xtide-prep.log
# G11 — le disque SCSI amorçable, en C# seul, par le script de bd-ami486-aha-format aux mêmes tranches, sur
#   l'ami486 et son CMOS sans disque (nvr/.ami486.nvr, effacé ensuite), la disquette DOS 5 en A:.
mapfile -t AF < "$G/aha-format.keys"; AT=(); for l in "${AF[@]}"; do AT+=(--type "$l"); done
cp aha486.nvr nvr/.ami486.nvr
sed "s|$WORK/scsi20.img|$WORK/scsic.img|" ami486-aha-vierge.cfg > aha-prep.cfg
cp dos5-1.img dos5-prep.img; cp dos5-3.img dos5-prep3.img
dotnet "$BIN" --boot "$REPO/roms" 2500 --config aha-prep.cfg --floppy-a dos5-prep.img --floppy-b dos5-prep3.img --in-place --settle 20 "${AT[@]}" > aha-prep.log
rm nvr/.ami486.nvr aha-prep.cfg dos5-prep.img dos5-prep3.img
# GR.3g — les disques de Windows réglés pour l'EGA et l'Hercules, en C# seul, sur des copies de c286.img : le SETUP DOS
#   de Windows (C:\WINDOWS\SETUP), l'avertissement de SETUP.INF passé, la ligne « Display » ouverte, la liste en butée
#   puis 2 (EGA) ou 5 (Hercules Monochrome) lignes plus bas, « Complete Changes » ; les disquettes 1, 2, 3 et 5 de
#   Windows 3.11 insérées à la demande (des copies : --in-place écrirait aussi sur elles) ; pour l'EGA, CONFIG.SYS
#   modifié par SETUP (l'option 1 ; l'Hercules ne le demande pas, l'Entrée tombe sur DOS). La carte de
#   ami286-mfm.cfg et c.nvr pendant le SETUP, dont le choix ne dépend pas.
for n in 1 2 3 5; do cp "$REPO/os/Windows_3-11/Disk$n.IMA" "win-$n.img"; done
for v in ega:2:c286-ega hercules:5:c286-herc; do
  IFS=: read -r carte bas img <<< "$v"
  cp c286.img "$img.img"; sed "s|^hdc_fn = .*|hdc_fn = $WORK/$img.img|" ami286-mfm.cfg > win-prep.cfg
  K=(--type "CD \\WINDOWS" --type "SETUP" --type "@wait 1500" --type "" --type "@wait 900")
  for i in 1 2 3 4 5 6 7; do K+=(--type $'\x18^'); done
  K+=(--type "" --type "@wait 900"); for i in $(seq 25); do K+=(--type $'\x18^'); done
  for i in $(seq "$bas"); do K+=(--type $'\x19^'); done; K+=(--type "" --type "@wait 900")
  for i in 1 2 3 4 5 6 7; do K+=(--type $'\x19^'); done; K+=(--type "" --type "@wait 1500")
  for n in 1 2 3 5; do K+=(--type "@A:$WORK/win-$n.img" --type "" --type "@wait 2000"); done
  K+=(--type "@A:" --type "@wait 600" --type "" --type "@wait 1500")
  cp c.nvr nvr/.ami286.nvr
  dotnet "$BIN" --boot "$REPO/roms" 60000 --config win-prep.cfg --in-place --settle 600 "${K[@]}" \
    > "win-prep-$carte.log"
  rm nvr/.ami286.nvr win-prep.cfg
done
rm win-1.img win-2.img win-3.img win-5.img
dotnet "$BIN" --make-nvr ami286-win-hercules.cfg c286herc.nvr --force > /dev/null   # 30h et le disque C:
( cd "$WORK" && sha256sum c286.img c386.img vierge46.img c.nvr cd.nvr c386.nvr c486.nvr f386.nvr c8088.img \
    c8088dos.img iso-2048.iso mixte.cue mixte.bin zip100.img zipro.img dos5-1.img dos5-3.img win3-3.img sbpro2-1.img \
    scsi20.img scsic.img aha486.nvr aha386.nvr aha286.nvr at-mda.nvr herc286.nvr xtide-outils.img c286-ega.img \
    c286-herc.img c286herc.nvr ) > "$WORK/empreintes"
if [ $FIX = 1 ]; then cp "$WORK/empreintes" "$G/g5w.sha256"; echo "empreintes de référence réécrites"; exit 0; fi
if diff -u "$G/g5w.sha256" "$WORK/empreintes"; then echo "$WORK : conforme aux empreintes."
else echo "$WORK : les empreintes diffèrent (disques de os/ modifiés ?) — voir l'en-tête." >&2; exit 1; fi
