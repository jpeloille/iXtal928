#!/usr/bin/env bash
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# ecoute.sh — l'écoute de GR.3i (PLAN-GR.md ; R3-clotures.md § GR.3i, D5 et D6), dans la fenêtre d'iXtal26.
#
#   tools/ecoute/ecoute.sh                     prépare /var/tmp/ixtal-gr/ et dit quoi écouter
#   tools/ecoute/ecoute.sh dss|covox|stereo|cd [ARGS…]
#                                              ouvre la fenêtre sur ce profil, la disquette d'écoute en B: ;
#                                              ARGS passés à iXtal26 (par exemple --hardware-mode tout)
#
# Rien n'est ajouté à os/ (PLAN-GR.md, décision n° 11) : tout est écrit dans /var/tmp/ixtal-gr/ —
#   ecoute.img          la disquette d'écoute, 360 Ko : LPTPLAY.COM (tools/lptplay), ATAPIAUD.COM et CDECOUTE.COM
#                       (tools/atapibanc, --audio et --ecoute) ;
#   iso/ecoute.cue      la feuille d'écoute d'isogen (D6) : 440 Hz à gauche, puis 880 Hz à droite, dix secondes
#                       chacune ;
#   dss.cfg, covox.cfg  les profils d'écoute de GR.3b (ixtal26-486-dss.cfg, ixtal26-486-covox.cfg), tels quels ;
#   stereo.cfg          le profil du Covox, lpt1_device = lpt_dac_stereo ;
#   cd.cfg              ixtal26-486.cfg, la feuille d'écoute chargée dans son lecteur de CD.
# Les profils gardent leur disque (os/486-HDD-C.img) et leur CMOS (nvr/.ami486.nvr). La fenêtre est celle du Release
# que Rider a construit (iXtal26/bin/Release/net10.0) : le script ne compile rien.
set -eu
REPO=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
E=/var/tmp/ixtal-gr
DLL=${DLL:-$REPO/iXtal26/bin/Release/net10.0/iXtal26.dll}

preparer() {
    mkdir -p "$E/iso"
    python3 "$REPO/tools/isogen/isogen.py" "$E/iso" ecoute.cue ecoute.bin > /dev/null
    python3 "$REPO/tools/lptplay/lptplay.py" --com "$E/LPTPLAY.COM" > /dev/null
    python3 "$REPO/tools/atapibanc/atapibanc.py" --audio --com "$E/ATAPIAUD.COM" > /dev/null
    python3 "$REPO/tools/atapibanc/atapibanc.py" --ecoute --com "$E/CDECOUTE.COM" > /dev/null
    rm -f "$E/ecoute.img"
    dotnet "$DLL" --create-floppy 360k "$E/ecoute.img" > /dev/null
    ( cd "$E" && touch -d "1995-06-15 12:00:00" LPTPLAY.COM ATAPIAUD.COM CDECOUTE.COM &&
      dotnet "$DLL" --floppy-put ecoute.img LPTPLAY.COM ATAPIAUD.COM CDECOUTE.COM > /dev/null &&
      rm LPTPLAY.COM ATAPIAUD.COM CDECOUTE.COM )
    cp "$REPO/ixtal26-486-dss.cfg" "$E/dss.cfg"
    cp "$REPO/ixtal26-486-covox.cfg" "$E/covox.cfg"
    sed 's/^lpt1_device = lpt_dac$/lpt1_device = lpt_dac_stereo/' "$REPO/ixtal26-486-covox.cfg" > "$E/stereo.cfg"
    sed "s|^cdrom_drive = -1$|cdrom_drive = 200\ncdrom_path = $E/iso/ecoute.cue|" "$REPO/ixtal26-486.cfg" > "$E/cd.cfg"
    grep -q '^lpt1_device = lpt_dac_stereo$' "$E/stereo.cfg" && grep -q '^cdrom_drive = 200$' "$E/cd.cfg" ||
        { echo "ecoute.sh : un profil n'a pas la clé attendue (lpt1_device, cdrom_drive)." >&2; exit 1; }
}

[ -f "$DLL" ] || { echo "ecoute.sh : $DLL introuvable — construire iXtal26 en Release (Rider)." >&2; exit 1; }
if [ $# = 0 ]; then
    preparer
    cat <<FIN
$E prêt. Quatre écoutes, chacune dans sa fenêtre, à l'invite C:\> :

  tools/ecoute/ecoute.sh dss      B:LPTPLAY D   la gamme d'ut, do 3 à do 4 et retour (4,5 s), sur la Sound Source
  tools/ecoute/ecoute.sh covox    B:LPTPLAY C   la même gamme sur le Covox mono
  tools/ecoute/ecoute.sh stereo   B:LPTPLAY S   la gamme montante à gauche, puis descendante à droite
  tools/ecoute/ecoute.sh cd       B:CDECOUTE    22 s : 440 Hz à gauche (10 s), puis 880 Hz à droite
                                  B:ATAPIAUD    ensuite seulement (CDECOUTE monte le CD du mélangeur, au plus bas
                                                sinon) : le banc audio de GR.2c, des sons brefs, voies croisées

Ce que les témoins en C# seul ont mesuré (VERIFICATION.md § GR.3i) : les notes justes à 0,2 Hz près (le Covox mono,
en mode PCem, à 1 % près) ; le CD, 440 Hz de 0 à 10 s, puis 880 Hz pendant 12 s — les deux secondes de silence du
prégap de la piste 3 manquent (un défaut candidat de PCem). La fenêtre part en mode matériel, où le CD sort 12 dB
plus bas qu'en mode PCem (le général du mélangeur, PB-237) ; pour comparer, ajouter --hardware-mode pcem.
Un vrai CD (D6) : son image (.iso ou .cue) à la place de iso/ecoute.cue, dans la clé cdrom_path de $E/cd.cfg.
FIN
    exit 0
fi
case $1 in
    dss|covox|stereo|cd) ;;
    *) echo "ecoute.sh : dss, covox, stereo ou cd, pas « $1 »." >&2; exit 2 ;;
esac
[ -f "$E/ecoute.img" ] && [ -f "$E/$1.cfg" ] || preparer
p=$1; shift
cd "$REPO" && exec dotnet "$DLL" --config "$E/$p.cfg" --floppy-b "$E/ecoute.img" "$@"
