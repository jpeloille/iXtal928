#!/usr/bin/env bash
# par.sh DLL SORTIE.tsv [PARALLÈLE=10] [RÉFÉRENCE.tsv]
#
# Joue la série de tools/gates/series.sh EN PARALLÈLE. Chaque porte devient un petit script
# (numéroté dans l'ordre canonique), puis xargs -P les lance, les plus longues d'abord (durées
# de la référence). Les portes disque tournent chacune dans $WORK/par/NOM (CMOS et
# configurations copiés), les autres dans $WORK/run/NOM (CMOS de nvr/default/ seulement). Le .tsv est reconstruit à la fin dans l'ordre canonique, un journal
# par porte à côté de lui : SORTIE-NOM.txt.
#
# DLL doit être une COPIE FIGÉE (avec libixtal26oracle.so à côté) : on ne joue jamais une série
# sur le répertoire de build. Ne jamais toucher $WORK ni $TMPDIR_SERIE pendant une série.
#   REPO : la racine du dépôt (défaut : déduite de l'emplacement de par.sh ; à poser quand on
#          joue une copie figée de par.sh, hors du dépôt).
#   WORK : l'espace des portes disque, fabriqué par g5w-recipe.sh (défaut /tmp/g5w).
#   TMPDIR_SERIE : le TMPDIR des portes, un par série (défaut : /tmp/ixtal-par/NOM, NOM étant la
#          sortie sans .tsv : en mémoire, sur le tmpfs). Chaque boot-diff y écrit la trace de
#          l'oracle, huit octets par instruction, jusqu'à 2,5 Go, et ses copies d'images. Le disque
#          (/var/tmp/ixtal-par/NOM) se demande explicitement ; mesuré, il ne coûte rien
#          (VERIFICATION.md, § Les outils). Le répertoire est vidé au départ, et effacé à la fin si
#          toutes les portes sont vertes ; gardé sinon, pour la relecture. Ce ménage ne vaut QUE pour
#          un répertoire juste sous /tmp/ixtal-par/ ou /var/tmp/ixtal-par/ : un TMPDIR_SERIE fourni
#          ailleurs (/tmp lui-même, partagé avec d'autres sessions) n'est jamais vidé.
#   ESPACE_MIN_GO : l'espace libre exigé avant de lancer, en Go (défaut : 1,5 par voie, 15 pour
#          dix). L'espace libre est celui de df, BORNÉ PAR LE QUOTA de l'utilisateur, que df ne
#          montre pas : /tmp est un tmpfs à quota par utilisateur, 80 % de sa taille, lu par
#          quotactl_fd. C'est ce quota qu'a dépassé l'incident du 4 octobre (VERIFICATION.md
#          § G10.0). En dessous, la série est refusée, retour 3.
#   PORTES : des noms de portes, séparés par des espaces ; seules celles-là sont jouées (une
#          validation ciblée). Un nom inconnu refuse la série, retour 2.
#
# Une série à la fois sur la machine : un verrou flock sur /var/tmp/ixtal-par/.verrou-machine,
# pris avant l'examen de l'espace et gardé jusqu'à la fin. Une seconde série attend, sous le
# message « en attente du verrou machine ». Le descripteur passe aux portes : une série tuée
# garde la machine tant qu'une de ses portes tourne encore.
set -u
DLL=$1; OUT=$2; P=${3:-10}; REF=${4:-}
REPO=${REPO:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}
WORK=${WORK:-/tmp/g5w}
D="${OUT%.tsv}.par"; rm -rf "$D"; mkdir -p "$D/g" "$D/r"
LOGP="${OUT%.tsv}"
T=${TMPDIR_SERIE:-/tmp/ixtal-par/$(basename "$LOGP")}
# Le ménage ne touche QUE le répertoire propre à la série, résolu (realpath : ni « .. » ni lien
# pour en sortir) et juste sous /tmp/ixtal-par/ ou /var/tmp/ixtal-par/ : jamais /tmp lui-même,
# partagé avec d'autres sessions, ni un TMPDIR_SERIE fourni ailleurs par l'appelant.
PROPRE=0; [[ $(realpath -m -- "$T") =~ ^(/var)?/tmp/ixtal-par/[^/]+$ ]] && PROPRE=1
# L'espace libre d'un répertoire, en Go : statvfs, borné par le quota de l'utilisateur s'il en a un
# (quotactl_fd, Q_GETQUOTA, USRQUOTA ; la limite dure en Kio, l'usage en octets).
libre_go() { python3 - "$1" << 'PY'
import ctypes, os, struct, sys
p = sys.argv[1]
st = os.statvfs(p)
libre = st.f_bavail * st.f_frsize
try:
    libc = ctypes.CDLL(None, use_errno=True)
    buf = ctypes.create_string_buffer(72)
    fd = os.open(p, os.O_RDONLY | os.O_DIRECTORY)
    r = libc.syscall(443, fd, ctypes.c_uint((0x800007 << 8) | 0), os.getuid(), buf)
    os.close(fd)
    if r == 0:
        dure, _, utilise = struct.unpack('QQQ', buf.raw[:24])
        if dure > 0:
            libre = min(libre, dure * 1024 - utilise)
except OSError:
    pass
print(max(0, libre) // 10**9)
PY
}
N=0
EMIS=""
emit() { local f; N=$((N+1)); f=$(printf '%s/g/%03d-%s.sh' "$D" $N "$1"); EMIS="$EMIS $1"
  if [ -n "${PORTES:-}" ] && [[ " $PORTES " != *" $1 "* ]]; then cat > /dev/null; else cat > "$f"; fi; }
# run : une porte « dépôt ». Elle tourne dans $WORK/run/NOM, où chaque entrée du dépôt est un
# lien, SAUF nvr/ et os/ : une copie de nvr/default/ et de os/pcdos20/ seulement (G9.1, après
# l'incident du 03/10 : aucun bac à sable ne lie plus les répertoires de l'utilisateur ; boot-diff
# copie de toute façon ses images avant de les monter). Aucune porte ne lit donc un CMOS de
# session de l'utilisateur (nvr/.MACHINE.nvr), qu'il peut modifier à tout moment en se servant de
# l'émulateur — mesuré en G1.0 : les boot-diffs de l'ami386dx en dépendaient.
run() { local name=$1; shift; local W="$WORK/run/$name"
  emit "$name" <<G
#!/usr/bin/env bash
t0=\$SECONDS
rm -rf $(printf %q "$W"); mkdir -p $(printf %q "$W/nvr") $(printf %q "$W/os")
for e in $(printf %q "$REPO")/*; do case "\$(basename "\$e")" in nvr|os) ;; *) ln -s "\$e" $(printf %q "$W")/;; esac; done
cp -r $(printf %q "$REPO/nvr/default") $(printf %q "$W/nvr/")
cp -r $(printf %q "$REPO/os/pcdos20") $(printf %q "$W/os/")
res=\$(cd $(printf %q "$W") && dotnet $(printf %q "$DLL") $(printf '%q ' "$@") 2>&1); rc=\$?
printf '%s\trc=%d\t%ds\t%s\n' $(printf %q "$name") \$rc \$((SECONDS-t0)) "\$(echo "\$res" | grep -v '^\s*\$' | tail -1)" > $(printf %q "$D/r/$(printf %03d $N)-$name.line")
echo "\$res" > $(printf %q "$LOGP-$name.txt")
G
}
runw() { local name=$1 nvr=$2 dest=$3; shift 3; local W="$WORK/par/$name"
  emit "$name" <<G
#!/usr/bin/env bash
t0=\$SECONDS
rm -rf $(printf %q "$W"); mkdir -p $(printf %q "$W/nvr"); ln -s $(printf %q "$REPO/roms") $(printf %q "$W/roms")
cp -r $(printf %q "$WORK/nvr/default") $(printf %q "$W/nvr/"); cp $(printf %q "$WORK")/*.cfg $(printf %q "$W/")
cp $(printf %q "$WORK/$nvr") $(printf %q "$W/nvr/.$dest.nvr")
res=\$(cd $(printf %q "$W") && dotnet $(printf %q "$DLL") $(printf '%q ' "$@") 2>&1); rc=\$?
printf '%s\trc=%d\t%ds\t%s\n' $(printf %q "$name") \$rc \$((SECONDS-t0)) "\$(echo "\$res" | grep -v '^\s*\$' | grep -E 'Vert|DIVERG|TRONQUÉE|Image [C-I]' | tr '\n' ' ')" > $(printf %q "$D/r/$(printf %03d $N)-$name.line")
echo "\$res" > $(printf %q "$LOGP-$name.txt")
G
}
cd "$REPO"
source "$REPO/tools/gates/series.sh"
for p in ${PORTES:-}; do
  [[ " $EMIS " == *" $p "* ]] || { echo "par.sh : porte inconnue : $p" >&2; exit 2; }
done
for f in "$D"/g/*.sh; do n=$(basename "$f" .sh); n=${n#*-}
  s=""; [ -n "$REF" ] && s=$(awk -F'\t' -v n="$n" '$1==n {gsub("s","",$3); print $3}' "$REF")
  echo "${s:-9999} $f"; done | sort -rn | cut -d' ' -f2 > "$D/ordre"
[ -n "${DRY:-}" ] && exit 0
# Le verrou machine (l'accélération du 4 octobre) : deux séries de front se disputent les vingt
# processeurs et le quota de /tmp. Il est pris avant de vider le répertoire de la série et
# d'examiner l'espace, et ne se rend qu'à la sortie.
mkdir -p /var/tmp/ixtal-par
exec 9> /var/tmp/ixtal-par/.verrou-machine
if ! flock -n 9; then
  echo "par.sh : en attente du verrou machine (une autre série tourne)…" >&2
  flock 9
  echo "par.sh : verrou machine obtenu." >&2
fi
# Le répertoire n'est créé, et vidé, qu'au moment de lancer : un refus n'en laisse pas de vide.
mkdir -p "$T"
[ $PROPRE = 1 ] && find "$T" -mindepth 1 -delete
need=${ESPACE_MIN_GO:-$(( (P * 3 + 1) / 2 ))}
avail=$(libre_go "$T")
if [ "${avail:-0}" -lt "$need" ]; then
  echo "par.sh : ${avail:-?} Go libres pour $T, quota compris ; il en faut $need (ESPACE_MIN_GO) ; série refusée." >&2
  [ $PROPRE = 1 ] && rmdir "$T" 2> /dev/null
  exit 3
fi
export TMPDIR="$T"
xargs -a "$D/ordre" -P "$P" -I{} bash {}
cat "$D"/r/*.line > "$OUT"; echo FIN >> "$OUT"
if awk -F'\t' '$1 != "FIN" && $2 != "rc=0" { bad = 1 } END { exit bad }' "$OUT"; then
  [ $PROPRE = 1 ] && rm -rf "$T"
else
  echo "par.sh : des portes ne sont pas vertes ; leurs fichiers temporaires restent dans $T" >&2
fi
