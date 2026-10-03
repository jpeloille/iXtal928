#!/usr/bin/env bash
# g5w-recipe.sh [WORK=/tmp/g5w] — fabrique l'espace des portes disque de series.sh.
#
# Les portes disque (G5) amorcent les disques de l'utilisateur. Les images ne sont pas
# versionnées (os/ ne l'est pas) : on versionne la RECETTE, et les empreintes qu'elle doit
# rendre. Tout est fait sur des COPIES ; os/ n'est jamais écrit.
#   c286.img, c386.img : os/286-HDD-C.img et os/386-HDD-C.img, AUTOEXEC.BAT sans KEYB FR
#                        (KeyScript tape en QWERTY) ;
#   vierge46.img       : 156 Mo de zéros, type 46 ;
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
for t in "$G"/cfg/*.cfg.in; do sed "s|@WORK@|$WORK|g" "$t" > "$WORK/$(basename "$t" .in)"; done
cd "$WORK"
dotnet "$BIN" --make-nvr ami286-mfm.cfg c.nvr --force > /dev/null
dotnet "$BIN" --make-nvr ami286-cd.cfg cd.nvr --force > /dev/null
dotnet "$BIN" --make-nvr ami386dx-ide.cfg c386.nvr --force > /dev/null
dotnet "$BIN" --make-nvr ami486-ide.cfg c486.nvr --force > /dev/null   # G6.4
dotnet "$BIN" --make-nvr ami386dx-fd.cfg f386.nvr --force > /dev/null   # PS2.0
( cd "$WORK" && sha256sum c286.img c386.img vierge46.img c.nvr cd.nvr c386.nvr c486.nvr f386.nvr ) > "$WORK/empreintes"
if [ $FIX = 1 ]; then cp "$WORK/empreintes" "$G/g5w.sha256"; echo "empreintes de référence réécrites"; exit 0; fi
if diff -u "$G/g5w.sha256" "$WORK/empreintes"; then echo "$WORK : conforme aux empreintes."
else echo "$WORK : les empreintes diffèrent (disques de os/ modifiés ?) — voir l'en-tête." >&2; exit 1; fi
