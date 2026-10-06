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
#   *.cfg              : tools/gates/cfg/*.cfg.in, @WORK@ remplacé ;
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
cp "$REPO/os/Dos 5.0/Disk01.img" "$WORK/dos5-1.img"              # G11
cp "$REPO/os/Dos 5.0/Disk03.img" "$WORK/dos5-3.img"
cp "$REPO/os/Windows_3-11/Disk3.IMA" "$WORK/win3-3.img"
cp "$REPO/os/sbpro2-disk1.img" "$WORK/sbpro2-1.img"             # G12.3
printf '@echo off\r\n' > "$WORK/ae-dos5.bat"
python3 "$G/fatpatch.py" "$WORK/dos5-1.img" AUTOEXEC.BAT "$WORK/ae-dos5.bat" --disquette; rm "$WORK/ae-dos5.bat"
truncate -s 20971520 "$WORK/scsi20.img"
cp "$WORK/scsi20.img" "$WORK/scsic.img"
for t in "$G"/cfg/*.cfg.in; do sed "s|@WORK@|$WORK|g" "$t" > "$WORK/$(basename "$t" .in)"; done
cd "$WORK"
dotnet "$BIN" --make-nvr ami286-mfm.cfg c.nvr --force > /dev/null
dotnet "$BIN" --make-nvr ami286-cd.cfg cd.nvr --force > /dev/null
dotnet "$BIN" --make-nvr ami386dx-ide.cfg c386.nvr --force > /dev/null
dotnet "$BIN" --make-nvr ami486-ide.cfg c486.nvr --force > /dev/null   # G6.4
dotnet "$BIN" --make-nvr ami386dx-fd.cfg f386.nvr --force > /dev/null   # PS2.0
dotnet "$BIN" --make-nvr ami486-aha.cfg aha486.nvr --force > /dev/null   # G11
dotnet "$BIN" --make-nvr ami386dx-aha.cfg aha386.nvr --force > /dev/null
dotnet "$BIN" --make-nvr ami286-aha.cfg aha286.nvr --force > /dev/null
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
( cd "$WORK" && sha256sum c286.img c386.img vierge46.img c.nvr cd.nvr c386.nvr c486.nvr f386.nvr c8088.img c8088dos.img iso-2048.iso mixte.cue mixte.bin zip100.img dos5-1.img dos5-3.img win3-3.img sbpro2-1.img scsi20.img scsic.img aha486.nvr aha386.nvr aha286.nvr ) > "$WORK/empreintes"
if [ $FIX = 1 ]; then cp "$WORK/empreintes" "$G/g5w.sha256"; echo "empreintes de référence réécrites"; exit 0; fi
if diff -u "$G/g5w.sha256" "$WORK/empreintes"; then echo "$WORK : conforme aux empreintes."
else echo "$WORK : les empreintes diffèrent (disques de os/ modifiés ?) — voir l'en-tête." >&2; exit 1; fi
