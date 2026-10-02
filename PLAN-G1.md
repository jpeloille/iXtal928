# G1 — Le 8086 : l'Olivetti M24 et l'Amstrad PC1512 ; le plan, sur reconnaissance

> Écrit le 2 octobre 2026, après G7. Bloc G1 de `PLAN.md`.
> Chaque constat cite la ligne de C qui le fonde, sur `pcem-dev/` tel que vendoré.
> Les décisions à trancher sont en fin de fichier ; rien ne s'écrit avant leur validation.
> **Les deux machines**, décision de l'utilisateur (PLAN.md, G1).
>
> **G1 est fait** (2 octobre 2026) : G1.0 à G1.3, VERIFICATION.md § G1.0 à § G1.3 ; PB-87 à PB-89.
>
> **Validé** le 2 octobre 2026 (orchestrateur, sous mandat) : décisions n° 2 à n° 6 telles que
> proposées ; défauts 1 et 2 sous R9, 3 et 6c en déviation de l'oracle, 4, 5, 6a et 6b
> reproduits ; `pc1512.nvr` fabriqué dans `nvr/default/`.

## Où on en est

**Le cœur 8086 est déjà là.** PCem n'a pas de cœur 8086 distinct : `cpu_set` pose
`is8086 = (cpu_type > CPU_8088)` (`cpu.c:181`) et le cas `CPU_8086` ne fait rien d'autre
(`cpu.c:319-321`). Tout le reste est dans `808x.c`, et le C# l'a transcrit en M1 :

| `808x.c` | ce que fait `is8086` | C# |
|---|---|---|
| `:75`, `:106` | `readmemw` / `writememw` : `memcycs += 8 >> is8086` (pas de pénalité d'adresse impaire) | `808x.cs:115`, `:140` |
| `:149` | FETCH, file vide, `pc` impair : un octet de plus | `808x.cs:177` |
| `:178`, `:181`, `:183` | FETCHADD : arrêt à 4 au lieu de 3, file de **6** octets au lieu de 4, paires paires | `808x.cs:205-211` |
| `:208`, `:214` | FETCHCOMPLETE, même logique | `808x.cs:233`, `:239` |

Ce qui manque au CPU : les tables `cpus_8086` (`cpu_tables.c:53-61`, six vitesses de 7,16 à
16 MHz, `fpus_8088`) et `cpus_pc1512` (`:63-66`, 8086/8 seul), omises (`cpu_tables.cs:176`) ;
et trois remises à zéro d'`is8086` (C# `808x.State.cs:94`, `386.State.cs:83` ; oracle
`harness.c:499`) qui écraseraient `cpu_set`. **Pas de NEC V20/V30 dans PCem.**

| | Olivetti M24 | Amstrad PC1512 |
|---|---|---|
| modèle | `m_olivetti_m24` (`model.c:948-957`), `MODEL_GFX_FIXED \| MODEL_OLIM24`, 128 à 640 Ko | `m_pc1512` (`model.c:885-894`), `MODEL_GFX_FIXED \| MODEL_AMSTRAD`, 512 à 640 Ko |
| CPU | `cpus_8086` | `cpus_pc1512` (8086/8) |
| init | `olim24_init` (`model.c:292-300`) | `ams_init` (`model.c:259-270`) + `ams1512_device` |
| propre à la machine | `olivetti_m24.c` (15 lignes : ports 66h/67h) ; `keyboard_olim24.c` (318, clavier et souris par la file du clavier) ; `vid_olivetti_m24.c` (445, CGA 640 × 400, 32 Ko, police `mda.rom`) | `amstrad.c` (197 : souris 78h/7Ah, reset logiciel 66h, 378h-37Ah, langue) ; `keyboard_amstrad.c` (163, PPI 60h-65h) ; `vid_pc1512.c` (483, CGA + mode plan 640 × 200 × 16, 64 Ko) |
| communs | `nvr_device` (CMOS ; pas de cas M24 dans `loadnvr`), `nmi_init`, `gameport_device` | `nvr_device` (`pc1512.nvr`, IRQ 2 sur Amstrad), `nmi_init`, `gameport_device`, `lpt1_remove`, `fdc_set_dskchg_activelow` |
| ROM | `olivetti_m24/…_1.43_low.bin` / `_high.bin`, entrelacées (`mem_bios.c:234-245`) — **présentes** | `pc1512/40043.v1` / `40044.v1`, entrelacées, police `40078.ic127` (`mem_bios.c:74-86`) — **présentes** |
| vidéo | par le romset, pas `gfxcard` (`video.c:264-265`, `:800-802`) ; temps `timing_m24` (`:200`) | idem (`video.c:246-247`, `:775-777`) ; `timing_pc1512` à zéro, le pilote facture `cycles -= 12` lui-même (`:197`) |
| PIT | pas de rafraîchissement DMA (canal 1 sur `pit_null_timer`, contrairement à `xt_init`, `model.c:205`) | idem |
| disques | FDC standard ; pas de disque dur intégré (`hdd_controller` « none » par défaut) | idem, changement de disque actif bas |

Déjà transcrits : `nvr.c` (branches `AMSTRAD` comprises, `nvr.cs:153`, `:197`, `:209`, `:311`,
`:332` ; les cas de romset de `loadnvr`/`savenvr` réduits), `nmi.c`, `fdc_set_dskchg_activelow`,
`ROM_OLIM24` = 9 et `ROM_PC1512` = 11 (`pc.cs:65`, `:67`). Absents : le champ `device` de
`MODEL` (`model.cs:43-45`), `gameport.c` (178 lignes), `lpt.c` (172), `mouse.c` (42), et le
verrou `amstrad_latch` d'`inb` (`io.c:111-116`, omis en `io.cs:167-169` — seul le PC1640 le lit).

L'horloge du CMOS ne pose pas de problème : `time_get` lit l'horloge interne émulée ;
`time_internal_sync`, seule porte vers l'heure de l'hôte, est omise (`rtc.cs`).

## Les étapes

### G1.0 — Les tables 8086 et le fuzzeur 8086  ✅ *fait, VERIFICATION.md § G1.0*

`cpus_8086`, `cpus_pc1512` ; les trois remises à zéro d'`is8086` respectent `cpu_set` ; l'oracle
gagne `h_model_olim24` et `h_model_pc1512` (modèles de la table, init NULL comme les autres) et
`--model olivetti_m24` / `--cpu N` au fuzzeur sur le cœur 8088 (`CPU_8086` y mène déjà,
`harness.c:1215-1221`). **Porte** : `cpu-config-check` dans les deux ordres (les deux machines y entrent avec leur
amorçage, en G1.1 et G1.2 : le balayage amorce chaque machine des deux côtés) ;
fuzzeur 8086 single et flux sur les 256 opcodes, graines 1 et 7, x87 8087 compris
(`fpus_8088`) ; toutes les séries existantes identiques — le 8088 ne doit pas bouger d'un cycle.

### G1.1 — L'Olivetti M24  ✅ *fait, VERIFICATION.md § G1.1 ; PB-88, PB-89*

`olivetti_m24.c`, `keyboard_olim24.c` (clavier et souris), `vid_olivetti_m24.c`, le cas
`ROM_OLIM24` de `loadbios`, `m_olivetti_m24`, la branche M24 de `h_boot` (pas de
rafraîchissement, clavier propre, `nvr_device`, `nmi_init`, `gameport_device`, ports 66h/67h).
L'oracle lie les trois fichiers ; `mouse.c` si `keyboard_olim24.c` l'exige. Sonde vidéo propre
(registres et `vram` de la M24, sa `charbuffer`). **Porte** : boot-diff M24 jusqu'au bout du POST,
puis PC-DOS 2.00 sur disquette ; `cpu-config-check` sur la M24.

### G1.2 — L'Amstrad PC1512  ✅ *fait, VERIFICATION.md § G1.2*

`amstrad.c`, `keyboard_amstrad.c`, `vid_pc1512.c`, le cas `ROM_PC1512` (police comprise), le
champ `device` de `MODEL` et `ams1512_device`, `AMSTRAD`, `lpt1_remove`, le CMOS `pc1512.nvr`
(le dépôt n'en porte pas : à fabriquer, comme les autres, par `--make-nvr` ou à reprendre de
`pcem-dev/nvr/`). **Porte** : boot-diff PC1512 jusqu'au POST et PC-DOS 2.00 ; le mode plan
640 × 200 × 16 par un petit banc dirigé (écritures 3DDh/3DEh) sous boot-diff.

### G1.3 — Les machines et les témoins  ✅ *fait, VERIFICATION.md § G1.3 ; G1 fait*

Profils Rider (décision n° 5) ; témoins `--boot` sur copies dans /tmp : POST des deux BIOS,
PC-DOS 2.00, la souris, le mode plan du PC1512 ; le disque dur XT (`8088-HDD-C.img`, contrôleur
Xebec ajouté par la configuration) si la décision n° 6 le retient.

## Les défauts de PCem déjà relevés (à lire à la ligne, puis inscrire)

1. `vid_olivetti_m24.c:414-415` : `charbuffer[256]` écrit jusqu'à l'index 509 — R1 du CRTC
   n'est pas masqué (`crtcmask[1] = 0xFF`, `:41`) ; l'invité écrase les champs qui suivent
   (`ctrl`, `base`…). En C, des champs voisins ; en C#, une exception : **R9**.
2. M24 et PC1512 : l'abscisse dépasse les 2 048 points de `buffer32` quand R1 est grand
   (`vid_olivetti_m24.c:154-166`, `:218-231`, `:258-259` ; `vid_pc1512.c:182-193`, `:317-319`,
   `:376-378`) — écriture hors du tableau à la dernière ligne : **R9** (comme PB-85).
3. VRAM par `malloc` sans `memset` (`vid_pc1512.c:439`, `vid_olivetti_m24.c:424`) : déviation
   de l'ORACLE (memset), même arbitrage que la VRAM de `svga_init`.
4. Files de touches de 16 entrées sans contrôle de débordement (`keyboard_amstrad.c:53-57`,
   `keyboard_olim24.c:63-68`) : le contenu se perd, sans accès hors tableau — à reproduire.
5. Souris M24 : bornes mortes (`keyboard_olim24.c:251-252`, `:258-259`), déplacements négatifs
   en complément à deux — à reproduire.
6. CMOS de la M24 : IRQ 8 sur une machine à un seul PIC (`nvr.c:85-86`), le port 70h touche
   `nmi_mask` (`nvr.c:201-203`), `nvrram` jamais initialisé (`nvr.c:521-522`) — reproduire les
   deux premiers ; le troisième est du tas, à initialiser dans l'oracle comme en C# (0).

## La vérification

- **Oracle** : PCem seul, cœur 8088 de M1 avec `is8086` ; aucun corpus SingleStepTests 8086
  dans le dépôt (seulement 8088, `tools/fetch-sst*.sh`).
- **Fuzzeur** : 8086, cycles compris (file de 6, accès de mots sans pénalité).
- **Boot-diffs** : M24 et PC1512, POST et DOS ; sondes vidéo propres aux deux cartes.

## Les risques

1. **Le 8088 ne doit pas bouger.** Les trois remises à zéro d'`is8086` sont touchées : la
   moindre fuite d'`is8086 = 1` d'une machine à la suivante change toutes les mesures du 5150.
   D'où `cpu-config-check` dans les deux ordres, comme en G6.
2. **La vidéo par le romset.** Les deux cartes ne passent pas par `gfxcard` : le registre
   `VIDEO_CARD` et `video_init` doivent suivre `video.c:775-802`, sans casser les sept cartes.
3. **Le PC1512 et le reset logiciel** (port 66h → `softresetx86`) : un chemin que le 5150 n'a
   jamais pris.

## Les décisions à trancher

1. **Les deux machines** : décidé (utilisateur).
2. **La manette** (`gameport_device`, ajoutée par les deux inits) : transcrire `gameport.c`
   sans manette branchée — le port 201h rend ce qu'il rend chez PCem *(proposé : oui ; sinon
   201h rendrait 0xFF au lieu de la valeur de PCem et le diff le verrait)*.
3. **Les souris** de la M24 et de l'Amstrad : transcrire leurs chemins (ports, file du
   clavier), et les brancher sur la souris de l'hôte *(proposé : oui, le branchement hôte en
   G1.3)*.
4. **La langue du PC1512** (`ams1512_device`, config « language », défaut 7) : le défaut de
   PCem *(proposé)*.
5. **Les profils Rider** : deux profils, « Olivetti M24 » et « Amstrad PC1512 », sur PC-DOS
   2.00 en disquette *(proposé)*.
6. **Le disque dur XT** sur ces machines (Xebec par la configuration, `8088-HDD-C.img`) :
   témoin seulement, pas de profil *(proposé)*.
7. **Ce fichier** : `PLAN-G1.md`.
