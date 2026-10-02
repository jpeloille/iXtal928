# G8 — Le son : la Sound Blaster Pro v2 ; le plan, sur reconnaissance

> Écrit le 2 octobre 2026, après G1. Bloc G8 de `PLAN.md` (section G8, e776097).
> Chaque constat cite la ligne de C qui le fonde, sur `pcem-dev/` tel que vendoré.
> Les décisions à trancher sont en fin de fichier ; rien ne s'écrit avant leur validation.
> **La carte** : la Sound Blaster Pro v2 (décision utilisateur du 02/10) ; **l'OPL** : DBOPL, le
> défaut de PCem (décision de l'orchestrateur sous mandat, 02/10) ; NukedOPL omis et marqué.

## Où on en est

Le son du dépôt s'arrête au haut-parleur (M9) : `Sound/sound.cs` porte `sound_add_handler`,
`sound_poll` (minuterie à 48 kHz, `sound.c:218-258`), `sound_reset` et le hachage `sound_hash`
des échantillons mixés ; `sound_speaker.cs` y verse le haut-parleur. L'oracle recopie la boucle de
`sound.c` dans `harness.c:100-234`, inclut `sound_speaker.c` et souche `givealbuffer`. **La sonde
de M9** (`h_speaker_probe`, `BootDiff.SpeakerProbe`, VERIFICATION.md § M9) compare `sound_hash`
en fin de boot-diff : tout générateur qui s'enregistre comme handler y entre de lui-même. Le
registre `SOUND_CARD` et la clé `sndcard` sont omis (`sound.cs:8`).

| | ce que la SB Pro v2 atteint | lignes |
|---|---|---:|
| `sound_sb.c` | `sb_pro_v2_init` (`:998-1024`), `sb_get_buffer_sbpro` (`:87-125`), le mélangeur CT1345 (`:400-531`), `sb_close`, `sb_speed_changed`, la config (`:1230-1259` : 220h/240h, IRQ 2/5/7/10, DMA 1/3, `opl_emu`) | ≈ 260 |
| `sound_sb_dsp.c` | le DSP SBPRO2 en 8 bits : reset, commandes (`:317-715`), ports (`:717-816`), DMA 8 bits, `pollsb` / `sb_poll_i` 8 bits, mode direct, `sb_dsp_update` ; omis : 16 bits, SB16/AWE (`sb_type >= SB16`), Aztech | ≈ 700 sur 1 326 |
| `sound_opl.c` | `opl3_read/write`, `opl3_update2`, les deux minuteries (`:111-121`) ; l'AdLib : `opl2_*` | 134 |
| `sound_dbopl.cc` | `opl_init`, l'état d'état et des minuteries, `opl_write/read`, `opl2/3_update` | 175 |
| `dosbox/dbopl.cpp` | Operator, Channel, Chip, `Setup`, `InitTables` (mode `WAVE_TABLEMUL`) | ≈ 1 300 sur 1 494 |

**Le C++ et l'oracle.** Rien n'est compilé en C++ dans `tools/oracle` aujourd'hui ; `g++` est
présent. `sound_dbopl.cc` + `dbopl.cpp` s'y compilent avec les mêmes `-I`, un lien par `g++`
(constructeurs statiques de `opl[2]`) ; NukedOPL, que `sound_dbopl.cc` embarque, est remplacé par
quatre souches fatales (`OPL3_Reset`, `OPL3_WriteAddr`, `OPL3_WriteReg`, `OPL3_GenerateStream`).
Côté C#, les pointeurs de fonctions membres (`synthHandler`, `volHandler`) deviennent des
délégués ; les tables d'offsets fabriquées par transtypage de pointeurs nuls (`:1430-1450`), des
indices.

**Le risque numérique.** `InitTables` (`dbopl.cpp:1311-1464`) remplit trois tables par `pow(2.0,
…)` et `sin(…)` de la libm de l'oracle (`MulTable` `:1337`, `WaveTable` `:1343`, `:1348`). Un ulp
d'écart entre glibc et .NET change un échantillon, et le hachage le voit. Le reste de `Setup` est
de l'arithmétique `double` déterministe. Le filtre `sb_iir` (float, état `static` local,
`filters.h:107`), actif après un reset, est le second point flottant (décision n° 4).

**Ce que le logiciel LIT** (et donc ce que le diff d'instructions voit) : l'état de l'OPL (port
388h : drapeaux des minuteries, `status_mask`, `dbopl.cc:112-117`) — la détection AdLib de tous
les jeux —, chaque lecture coûtant `isa_timing * 8` cycles (`sound_opl.c:15`, `:61`) ; les ports
du DSP (reset, version 0x302, octet occupé piloté par la minuterie `wb_timer`, données) ; le
mélangeur. Les **échantillons**, eux, ne sont vus que par `sound_hash`.

## Les étapes

### G8.0 — L'outillage : le C++ dans l'oracle, les tables de DBOPL  ✅ *fait, VERIFICATION.md § G8.0*

Le Makefile compile `sound_dbopl.cc`, `dbopl.cpp` (et lie par `g++`) ; souches NukedOPL. Une
porte **`opl-tables-check`** : `InitTables` des deux côtés, les tables `MulTable`, `WaveTable`
(et les autres tables d'`InitTables`) comparées octet à octet. **Porte** : la table identique,
toutes les séries identiques. Si la libm diverge, décision n° 3.

### G8.1 — L'OPL : `sound_opl.c`, `sound_dbopl.cc`, `dbopl.cpp` ; l'AdLib  ✅ *fait, VERIFICATION.md § G8.1*

Transcription de l'OPL3 par DBOPL et de l'AdLib (`adlib_device`, `sound_adlib.c`, un device) ; la
clé `sndcard` et le registre `SOUND_CARD` réduit (comme `VIDEO_CARD`). La sonde du son gagne
l'état de l'OPL (adresse, état, masque, minuteries) à côté de `sound_hash`. **Porte** :
boot-diff 5150 + AdLib et ami486 + AdLib jusqu'au DOS, sonde du son identique ; un banc dirigé
(.COM saisi dans DEBUG) : la détection AdLib (minuteries 1 et 2, lecture de 388h), des notes sur
les neuf voix, le mode OPL3 — sous boot-diff, `sound_hash` comparé.

### G8.2 — Le DSP et la SB Pro v2  ✅ *fait, VERIFICATION.md § G8.2*

`sound_sb_dsp.c` (SBPRO2, 8 bits), le CT1345, `sb_pro_v2_device` ; `sound_set_cd_volume` des deux
côtés. **Porte** : boot-diff avec la carte jusqu'au DOS ; un banc dirigé : reset et version du
DSP, sortie directe (0x10), DMA 8 bits simple et automatique (0x14, 0x1C, 0x48), mono et stéréo
(mélangeur), vitesse (0x40), pause et reprise (0xD0/0xD4), ADPCM 4 bits, lecture du mélangeur,
IRQ — sous boot-diff, `sound_hash` et l'état du DSP comparés.

### G8.3 — Les machines et les témoins

La carte sur les profils (décision n° 5) ; témoins `--boot` : la détection par un programme DOS
s'il en existe un sur les disques, Windows 3.1 avec un pilote Sound Blaster s'il y en a un, et
l'écoute réelle par l'hôte (SDL) — au moins un témoin audible consigné.

## Les défauts de PCem déjà relevés (à lire à la ligne, puis inscrire)

1. ADPCM 2 bits : `pollsb` ne décrémente jamais `sb_8_length` (`sound_sb_dsp.c:1016-1020`) — les
   commandes simples 0x16/0x17 ne finissent jamais, sans IRQ. Rien ne tombe : à reproduire.
2. `DMA_OVER` (0x10000) entre dans `sbdat2` au dernier octet ADPCM (`:943`, `:984`, `:1019`) et
   sature l'échantillon : à reproduire.
3. IRQ 10 proposée par la configuration mais perdue sur une machine sans second PIC (`pic.c:303-310`) :
   à reproduire.
4. L'état statique `opl[nr]` (état, masque, minuteries, adresse) n'est jamais remis à zéro par
   `opl_init` (`dbopl.cc:30-46`) : il traverse les amorçages d'un même processus — la phase 2 du
   boot-diff hériterait de la phase 1. Déviation de l'oracle (effacement à l'amorçage), le C#
   repartant de zéro.
5. L'AdLib et la SB partagent `opl[0]` si les deux étaient montées : une carte son à la fois.
6. `record_buffer[0xFFFF]` lu à l'indice 0xFFFF+1 (`:1150`…) : formats stéréo du SB16 seulement,
   hors d'atteinte sur la SBPRO2 — à noter.

## La vérification

- **Oracle** : PCem seul, `sound.c` recopié et DBOPL compilé en C++.
- **Diff d'instructions** : ce que le logiciel lit (OPL, DSP, mélangeur, DMA, IRQ).
- **Sonde du son** : `sound_hash` (tous les échantillons mixés, M9) et l'état de la carte.
- **Tables** : `opl-tables-check`.

## Les risques

1. **La libm** (décision n° 3). DBOPL calcule ses tables une fois ; c'est le seul point flottant
   non déterministe a priori.
2. **Le temps.** Les échantillons sont produits paresseusement jusqu'à `sound_pos_global` : le
   moindre écart de minuterie (48 kHz, `sblatcho`, minuteries de l'OPL) déplace le hachage sans
   changer une instruction. La sonde doit garder l'état des minuteries.
3. **La taille** : ≈ 2 500 lignes de C et C++ ; DBOPL est le plus dense.

## Les décisions à trancher

1. **La carte et l'OPL** : décidé (SB Pro v2, DBOPL ; NukedOPL omis).
2. **L'AdLib seule** (`adlib_device`, OPL2) : transcrite en G8.1, un device de plus *(proposé :
   oui, c'est le chemin le plus court vers une porte OPL, et la détection AdLib de tous les jeux)*.
3. **Si la libm diverge** sur `pow`/`sin` d'`InitTables` : les tables sont calculées par la libm de
   l'hôte des deux côtés — l'écart serait celui de deux bibliothèques, pas de PCem. *(proposé :
   le C# reprend les valeurs EXACTES calculées par l'oracle — l'oracle les exporte, le C# les
   recalcule et les compare au démarrage de l'outillage ; en cas d'écart, une table figée tirée
   de glibc, DEVIATION inscrite)*.
4. **Le filtre de sortie** `sb_iir` : ce n'est pas une option — l'invité le pilote par le bit 5
   du registre 0Eh du CT1345 (`output_filter = !(regs[0xE] & 0x20)`, `sound_sb.c:471`), actif
   après un reset. Il calcule en `float` avec un état `static` LOCAL à la fonction
   (`filters.h:107`), que le harnais ne peut pas remettre à zéro sans toucher le vendoré : il
   traverse les amorçages d'un processus. *(proposé : le transcrire en `float` à l'identique,
   l'état en statique C# qui traverse de même les amorçages — le boot-diff joue les deux phases
   dans le même ordre des deux côtés —, et le dire ; si le flottant diverge, une décision)*.
5. **Les profils** : la SB Pro v2 sur les deux profils ami486 et l'ami386, à 220h, IRQ 5, DMA 1
   *(proposé ; IRQ 5 plutôt que le 7 par défaut, que l'imprimante revendique sur ces machines —
   ou le défaut de PCem, IRQ 7, si l'on préfère ne rien choisir)*.
6. **Ce fichier** : `PLAN-G8.md`.
