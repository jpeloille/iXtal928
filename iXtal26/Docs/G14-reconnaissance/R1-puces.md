# G14, reconnaissance — R1 : l'inventaire des puces

Lecture du 23 octobre 2026, au commit `1d49a7f`, en lecture seule : rien n'a été construit ni lancé ; les comptes
viennent de `grep`, `git grep` et d'un script Python jetable hors du dépôt (annexe B). Niveaux : **vérifié** (relu
dans le code ou le texte cité), **déduit** (tiré du code sans être écrit nulle part), **inconnu**.

## 0. En bref

1. iXtal26/ compte 226 fichiers `.cs` ; aucun répertoire `Ics/` n'existe ; aucun module CRTC séparé n'existe.
2. Quatorze modules modélisent une puce (ou deux exemplaires d'une puce) et peuvent changer de nom sans rien
   découper : `pic`, `pit`, `dma`, `keyboard_at`, `ppi_c`, `nvr`, `serial`, `sound_emu8k`, les deux RAMDAC, les
   quatre jeux de puces. Les trois exemples de PLAN.md sont du nombre.
3. Le 6845 n'a pas de module : cinq cartes (CGA, MDA, Hercules, PC1512, M24) portent chacune son état et ses
   registres en ligne, comme PCem, qui recopie trois fois le même masque. « Le CRTC → `Motorola6845` » demanderait
   d'extraire un module neuf, ce qu'un renommage mécanique ne fait pas (décision D2).
4. PCem ne nomme presque aucune puce dans son code : ni 8259, ni 8253/8254, ni 8237, ni 8042, ni 8255, ni 6845
   dans les fichiers transcrits. Il nomme le MC146818, le DS12C887, l'OPTi 82C495, le TKD8001, le Sierra 1502x,
   l'EMU8000, les YM3812/YMF262, les mélangeurs CT1335/1345/1745 et, dans un commentaire, le 82077AA. Le reste
   vient des en-têtes d'iXtal26.
5. Le renommage des quatorze classes (seize avec `rtc` et `rtc_h`) touche 69 fichiers d'iXtal26/ (233 sites) et
   11 d'iXtal26.Diff (174 sites) ; aucun dans perfbanc ni SoftFloat. Les chemins de fichiers sont cités 217 fois
   dans les fichiers suivis, dont 24 lignes d'`oracle.tsv`, que `check-oracle.sh` vérifie.
6. Aucune sonde ne désigne une classe candidate par une chaîne ; les seuls noms en chaîne sont des membres d'autres
   classes. Mais `listings-jit.sh compare` indexe les méthodes par leur nom complet : sans table de correspondance,
   tout renommage s'y lit comme des méthodes disparues et apparues.
7. Deux pièges : le glob de `.editorconfig` ne connaît pas `Ics/`, et le projet a `EnforceCodeStyleInBuild` et
   `TreatWarningsAsErrors` (que la construction échoue : inconnu) ; R1(e) réserve `// noms:` à `Floppy/` et `Disc/`.
8. PLAN.md laisse ouverte la portée (le nom du module seul, ou aussi ses membres) : proposition, le module seul.
9. Sept décisions ouvertes (§ 4), six étapes proposées (G14.0 à G14.5, § 2), six constats hors de la portée (annexe A).
10. `pit` modélise la commande de relecture du 8254 sur toutes les machines, le 5150 compris : le nom décidé,
   `Intel8253`, dit moins que le code ; c'est un constat, pas une décision ouverte.

## 1. L'état, point par point

### 1.1 Les comptes de départ

- 226 fichiers `.cs` sous iXtal26/ (hors `bin/`, `obj/`), dont 37 dans `Host/`, 55 dans `Cpu/`, 44 `*.Materiel.cs` :
  `find iXtal26 -name '*.cs' -not -path '*/bin/*' -not -path '*/obj/*' | wc -l` (et `-name '*.Materiel.cs'`).
- La règle actuelle du nommage est écrite : « Le dépôt nomme ses classes d'après le FICHIER C dont elles
  viennent — rtc.c donne rtc, comme io.c donne io et pic.c donne pic » (`iXtal26/rtc.cs:28-31`) ; et
  `TRANSCRIPTION.md:131` : « `snake_case` de PCem **verbatim** ; `Floppy/` et `Disc/` exceptés ». Vérifié.
- `ls iXtal26/Ics` : absent. Le répertoire est entièrement à créer. Vérifié.
- La convention répertoire = namespace (`iXtal26.<Rép>`) tient pour les 226 fichiers, sauf `Materiel/`, qui est en
  `namespace iXtal26`, et `GlobalUsings.cs` et `Program.cs`, qui n'en ont pas (boucle `find … | grep -m1 '^namespace'`
  comparée au répertoire). Vérifié.

### 1.2 La table des modules

Colonnes : fichier(s), classe(s) (les types transcrits des `struct` du C compris), le fichier PCem transcrit (d'après
l'en-tête `// ORACLE:` de chaque fichier), la nature — **P** une puce, **C** une carte ou un *device*, **M** une
machine, **U** le cœur UC, **G** la colle (bus, mémoire, chronomètres, registres de cartes, média), **H** l'hôte ou
l'outillage, **R** du mode matériel (classe partielle, même nature que son fichier principal) — et la puce, avec la
source de l'attribution. « (+M) » : le fichier a son partiel `*.Materiel.cs`.

**Racine** (`namespace iXtal26`)

| Fichier | Classe(s) | PCem | Nat. | Puce, source |
|---|---|---|---|---|
| `io.cs` | `io` | `io.c` | G | — |
| `pc.cs` | `pc`, `CarteVideo` | `pc.c` | M | — (la boucle de la machine) |
| `ppi.cs` | `PPI`, `ppi_c` | `ppi.c` | P | 8255A réduit à `pa`, `pb`, `s2` (`ppi.cs:17-21`) ; déduit |
| `rtc.cs` | `rtc_h`, `rtc` | `rtc.c`, `rtc.h` | P | l'horloge du MC146818 (`rtc.cs:10`) ; PCem : DS12C887 |
| `timer.cs` | `pc_timer_t`, `timer` | `timer.c` | G | — |
| `BootTest.cs`, `TimerCheck.cs`, `Program.cs`, `GlobalUsings.cs` | … | — | H | — |

**Cpu/** (55 fichiers)

| Fichier(s) | Classe(s) | PCem | Nat. | Puce, source |
|---|---|---|---|---|
| `808x.cs` (+M), `808x.State.cs` (H) | `_808x`, `Carte286` | `808x.c` | U | 8088 et 8086 (`is8086`, `808x.cs:46`) |
| `x87_8087.cs`, `x87_ops_808x*.cs` (2) | `_808x` | `8087.h`, `x87_ops*.h` | U | le 8087 |
| `386.cs` (+M), `386_common.cs`, `386.State.cs` (H) | `_386`, `_386_common` | `386.c`, `386_common.c` | U | 286 à 486 |
| `386_ops_*.cs` (28) | `_386` | `x86_ops_*.h`, `386_ops.h` | U | (`cpu.cs:30-41`) |
| `x87_ops*.cs` (5) | `_386`, `x87_ts`, `x87_td` | `x87_ops*.h` | U | 287 et 387 |
| `cpu.cs`, `cpu_tables.cs` | `cpu_c`, `FPU`, `CPU`, `cpu_tables` | `cpu.c`, `cpu_tables.c` | U | — |
| `x86.cs`, `x86_flags.cs`, `x86seg.cs` | `x86…`, `x86_flags`, `x86seg_c` | `x86.h`, … | U | — |
| `x87.cs` (+M), `x87_timings.cs` | `x87_c`, `_x87_materiel`, `x87_timings_*` | `x87.c`, … | U | — |
| `x87_noyau*.Materiel.cs` (3), `x87_transcendantes…` | `_x87_noyau`, `_386_noyau`… | — | R | — |

**Devices/, Diag/, Disc/, Flash/, Floppy/**

| Fichier | Classe(s) | PCem | Nat. | Puce, source |
|---|---|---|---|---|
| `Devices/cassette.cs` | `cassette` | `cassette.c` | C | — (interface sans lecteur) |
| `Devices/nvr.cs` | `nvr` | `devices/nvr.c` | P | MC146818 : `nvr.c:802` « Motorola MC146818 RTC » |
| `Diag/*.cs` (3) | `Counters`, `HState`, `R9` | — | H | — |
| `Disc/disc.cs` (+M), `disc_img*.cs`, `disc_sector.cs` | `disc`, `disc_img`, `disc_sector`… | `disc*.c` | G | — |
| `Disc/hdd.cs`, `Disc/hdd_file.cs` | `hdd_c`, `hdd_file`… | `hdd*.c` | G | — |
| `Flash/rom.cs` | `rom_t`, `rom` | `flash/rom.c` | G | — |
| `Floppy/fdc.cs` (+M) | `FDC`, `fdc_c` | `floppy/fdc.c` | C | µPD765 / 82077 (`fdc.cs:6`) ; carte (`PLAN.md:549`) |
| `Floppy/fdd.cs` | `fdd_t`, `drive_type_t`, `fdd_c` | `floppy/fdd.c` | C | — (le lecteur) |

**Host/** (37 fichiers : `SdlHost`, `SdlMenu`, `FatImage`, `CommandLine/*`…) : H, aucune puce.

**Ide/, Joystick/, Keyboard/, Lpt/, Materiel/, Memory/, Mfm/**

| Fichier | Classe(s) | PCem | Nat. | Puce, source |
|---|---|---|---|---|
| `Ide/ide.cs` (+M) | `IDE`, `ide` | `ide/ide.c` | C | — (ATA) |
| `Ide/ide_atapi.cs` | `ATAPI`, `atapi_device_t`, `ide_atapi` | `ide_atapi.c` | G | — (le pont) |
| `Ide/xtide.cs` | `xtide_t`, `xtide` | `ide/xtide.c` | C | — |
| `Joystick/gameport.cs` | `gameport`, `joystick_*`… | `gameport.c`, `joystick_standard.c` | C | aucune (§ 1.3 f) |
| `Joystick/joystick_*.cs` (3, dont 2 +M) | `joystick_*_c`, `sw_data` | `joystick_*.c` | C | — (les manettes) |
| `Keyboard/keyboard.cs` | `scancode`, `keyboard` | `keyboard.c` | G | — (les tables) |
| `Keyboard/keyboard_at.cs` (+M) | `keyboard_at_t`, `keyboard_at` | `keyboard_at.c` | P | 8042 (`:5`, `:9`) |
| `Keyboard/keyboard_xt.cs` | `keyboard_xt_t`, `keyboard_xt` | `keyboard_xt.c` | C | — (60h-62h du 8255) |
| `Keyboard/keyboard_amstrad.cs` | `keyboard_amstrad*` | `keyboard_amstrad.c` | C | — (60h-65h du PC1512) |
| `Keyboard/keyboard_olim24.cs` | `keyboard_olim24*`, `mouse_olim24_t` | `keyboard_olim24.c` | C | — |
| `Lpt/lpt.cs`, `lpt_dac.cs`, `lpt_dss.cs` | `lpt`, `lpt_dac`, `lpt_dss`… | `lpt*.c` | C | — |
| `Materiel/*.cs` (2) | `materiel`, `ModeMateriel` | — | H | — |
| `Memory/mem.cs`, `mem_bios.cs` | `mem_mapping_t`, `mem`, `mem_bios` | `mem.c`, `mem_bios.c` | G | — |
| `Mfm/mfm_at.cs` (+M), `mfm_xebec.cs` (+M) | `mfm_at`, `mfm_xebec`… | `mfm_*.c` | C | — |

**Models/** (17 fichiers)

| Fichier | Classe(s) | PCem | Nat. | Puce, source |
|---|---|---|---|---|
| `ali1429.cs` | `ali1429` | `ali1429.c` | P | « ALi 1429 » (`ali1429.cs:8`) ; PCem : le nom seul |
| `amstrad.cs` (+M) | `mouse_amstrad_t`, `amstrad` | `amstrad.c` | M | — |
| `dma.cs` (+M) | `dma_t`, `dma` | `dma.c` | P | deux 8237 (`dma.cs:5`) ; fiche 231466-005 (`dma.Materiel.cs:9`) |
| `headland.cs` | `headland` | `headland.c` | P | « HEADLAND HT18 » (`headland.cs:10`) ; PCem : le nom |
| `model.cs` (+M) | `MODEL_cpu`, `MODEL`, `model_c` | `model.c` | M | — |
| `neat.cs` | `neat` | `neat.c` | P | « Chips & Technologies NEAT » (`neat.cs:10-11`) |
| `nmi.cs` | `nmi` | `nmi.c` | G | — (le masque de la NMI) |
| `olivetti_m24.cs` | `olivetti_m24` | `olivetti_m24.c` | M | — |
| `opti495.cs` | `opti495` | `opti495.c` | P | OPTi 82C495 (`opti495.c:1`, `mem_bios.c:359`) |
| `pic.cs` (+M) | `PIC`, `pic` | `pic.c` | P | deux 8259A ; fiche 231468-003 (`pic.Materiel.cs:9`) |
| `pit.cs` (+M) | `PIT_nr`, `PIT`, `pit` | `pit.c` | P | « 8253/8254 » (`pit.cs:5`) ; fiche 8254 (`pit.Materiel.cs:9`) |
| `serial.cs` | `SERIAL`, `serial` | `serial.c` | P | deux UART de la famille 8250 (§ 1.3 a) ; déduit |

**Mouse/, PluginApi/, Scsi/**

| Fichier | Classe(s) | PCem | Nat. | Puce, source |
|---|---|---|---|---|
| `Mouse/mouse.cs`, `mouse_ps2.cs` (+M), `mouse_serial.cs` | `mouse`, `mouse_ps2`… | `mouse*.c` | C | — |
| `PluginApi/config.cs`, `device.cs`, `paths.cs` | `config`, `device`, `paths`… | `plugin-api/*.c` | G | — |
| `Scsi/scsi.cs` | `scsi_device_t`, `scsi_bus_t`, `scsi` | `scsi.c` | G | — (le bus) |
| `Scsi/scsi_aha1540.cs` (+M) | `aha154x_*`, `scsi_aha1540`… | `scsi_aha1540.c` | C | — (l'AHA-1542C) |
| `Scsi/scsi_cd.cs`, `scsi_hd.cs`, `scsi_zip.cs` (+M) | `scsi_cd_c`, `scsi_hd_c`, `scsi_zip_c` | `scsi_*.c` | C | — |

**Sound/** (16 fichiers)

| Fichier | Classe(s) | PCem | Nat. | Puce, source |
|---|---|---|---|---|
| `dbopl.cs` | `DBOPL`, `Operator`, `Channel`, `Chip` | `dosbox/dbopl.cpp` | P | YMF262 et YM3812 (`dbopl.cpp:20`) |
| `sound_dbopl.cs` | `sound_dbopl` | `sound_dbopl.cc` | P | la même, sa face d'E/S (§ 1.3 b) |
| `sound_opl.cs` | `opl_t`, `sound_opl` | `sound_opl.c` | G | — (`ym3812_timer_set_0`, `sound_opl.c:90`) |
| `sound_adlib.cs` | `adlib_t`, `sound_adlib` | `sound_adlib.c` | C | — |
| `sound.cs` (+M) | `sound`, `sound_handler_t`, `SOUND_CARD` | `sound.c` | G | — (le mixeur) |
| `sound_emu8k.cs` (+M) | `emu8k_*` (20 types), `sound_emu8k` | `sound_emu8k.c` | P | EMU8000 (`sound_emu8k.c:564`) |
| `sound_mpu401_uart.cs` (+M) | `mpu401_uart_t`, `sound_mpu401_uart` | `sound_mpu401_uart.c` | C | — (l'interface) |
| `sound_sb.cs` (+M) | `sb_ct1335/1345/1745_mixer_t`, `sb_t`… | `sound_sb.c` | C | trois mélangeurs en ligne |
| `sound_sb_dsp.cs` (+M) | `sb_dsp_t`, `sound_sb_dsp` | `sound_sb_dsp.c` | C | le DSP, puce inconnue |
| `sound_speaker.cs` (+M) | `sound_speaker` | `sound_speaker.c` | G | aucune (§ 1.3 f) |

**Video/** (27 fichiers)

| Fichier | Classe(s) | PCem | Nat. | Puce, source |
|---|---|---|---|---|
| `video.cs` (+M) | `video`, `VIDEO_CARD`, `LigneBornee`… | `video.c` | G | — |
| `vid_cga.cs` (+M) | `cga_t`, `vid_cga`, `Sonde` | `vid_cga.c` | C | MC6845 en ligne (`vid_cga.Materiel.cs:10`) |
| `vid_mda.cs` (+M) | `mda_t`, `vid_mda`, `Masque6845` | `vid_mda.c` | C | MC6845 en ligne (`vid_mda.Materiel.cs:10`) |
| `vid_hercules.cs` (+M) | `hercules_t`, `vid_hercules` | `vid_hercules.c` | C | un 6845, déduit (`….Materiel:10`) |
| `vid_pc1512.cs` (+M) | `pc1512_t`, `vid_pc1512` | `vid_pc1512.c` | C | « la puce … n'a pas été lue » (`:71`) |
| `vid_olivetti_m24.cs` (+M) | `m24_t`, `vid_olivetti_m24` | `vid_olivetti_m24.c` | C | de même (`:64`) |
| `vid_ega.cs` (+M) | `ega_t`, `vid_ega` | `vid_ega.c` | C | CRTC de l'EGA (CR11 bit 7, `vid_ega.cs:262`) |
| `vid_vga.cs` | `vga_t`, `vid_vga` | `vid_vga.c` | C | — |
| `vid_svga.cs` (+M), `vid_svga_render.cs` (+M) | `svga_t`, `vid_svga`, … | `vid_svga*.c` | G | — (socle, palette) |
| `vid_et4000.cs` (+M) | `et4000_t`, `vid_et4000` | `vid_et4000.c` | C | Tseng ET4000AX (`vid_et4000.c:610-611`) |
| `vid_cl5429.cs` | `gd5429_*`, `vid_cl5429` | `vid_cl5429.c` | C | Cirrus Logic GD5429 (`vid_cl5429.c:2165`) |
| `vid_s3.cs` | `s3_t`, `vid_s3`… | `vid_s3.c` | C | S3 Trio64 (`vid_s3.c:3134`) |
| `vid_tvga.cs` | `tvga_t`, `vid_tvga` | `vid_tvga.c` | C | TVGA 8900D et 9000B (`vid_tvga.c:412-415`) |
| `vid_tkd8001_ramdac.cs` | `tkd8001_ramdac_t`, `vid_tkd8001_ramdac` | `vid_tkd8001_ramdac.c` | P | TKD8001 (`:1`) |
| `vid_unk_ramdac.cs` (+M) | `unk_ramdac_t`, `vid_unk_ramdac` | `vid_unk_ramdac.c` | P | Sierra 1502x (`:4-7`) |

Bilan : dix-sept fichiers principaux sont de nature P. Quatorze se renomment tels quels (les trois exemples,
`keyboard_at`, `ppi`, `nvr`, `serial`, `sound_emu8k`, les deux RAMDAC, les quatre jeux de puces) ; `rtc` est une
moitié de puce (D6) ; `dbopl` et `sound_dbopl` sont une puce en trois modules (§ 1.3 b). Les cartes nommées d'après
leur puce (ET4000AX, GD5429, Trio64, TVGA) restent des cartes : `PLAN.md:549` nomme « VGA » parmi les *devices*.

### 1.3 Les cas limites

**a) Un module, plusieurs puces.**

- `pit` : l'en-tête dit « le 8253/8254 du 5150 » (`pit.cs:5`) ; la commande de relecture (`(val & 0xC0) == 0xC0`,
  `pit.cs:449`, transcrite de `pit.c:328`) est propre au 8254 et n'est gardée par aucune machine (`grep -n
  'AT\|romset' pcem-dev/src/models/pit.c` ne rend que le commentaire de la ligne 1). Le PC et le XT portent un
  8253-5 (`PCEM_BUGS.md:152`, l'IBM PC TR p. 2-22). Un module, le 8254 modélisé, le 8253 nommé. Le code : vérifié ;
  que le 8253 n'ait pas la relecture : déduit (la fiche du 8253 n'a pas été relue ici).
- `pic` : deux exemplaires du même 8259A, `pic_` et `pic2` (`pic.cs:35`) ; ICW4 et l'AEOI y sont (`pic.cs:18`,
  `:103`) : vérifié ; qu'ils distinguent le 8259A du 8259 : déduit.
- `dma` : deux 8237 (`dma.cs:5-7`) et, dans la même classe, les registres de page (`dma_page_write`, `dma.cs:452`),
  qui ne sont pas dans le 8237 (déduit : ils sont aux ports 80h-8Fh, hors du jeu de registres du 8237).
- Les cœurs UC : `_808x` sert le 8088 et le 8086 (`is8086`, `808x.cs:46`) ; `_386` sert le 286, le 386SX, le
  386DX et le 486 (`CPU_286` à `CPU_i486DX`, `cpu.cs:30-41`) ; le 8087, le 287 et le 387 sont des tables de ces
  mêmes classes (`cpu_tables.cs:84-88`). Aucun ne porte une seule puce. Vérifié.
- `dbopl` : un moteur pour deux puces, `opl3Active` choisit (`dbopl.cs:494`, `:694`) ; DOSBox le dit
  (`dbopl.cpp:20` : « combined Yamaha YMF262 and Yamaha YM3812 emulator »). Vérifié.
- `sound_sb` : trois mélangeurs Creative en ligne dans la carte, `sb_ct1335_mixer_t`, `sb_ct1345_mixer_t`,
  `sb_ct1745_mixer_t` (`sound_sb.cs:38`, `:50`, `:73`), leurs fonctions aux lignes 647, 731 et 877. Vérifié.
- `sound_sb_dsp` : le DSP de toutes les Sound Blaster, versions 1.05 à 3.00 et celle de la SB 16
  (`sound_sb_dsp.cs:5-14`) ; ni PCem ni iXtal26 ne nomment la puce. Inconnu.
- `vid_unk_ramdac` : PCem hésite puis tranche (« possibly a Sierra 1502x » puis « this *IS* a Sierra 1502x »,
  `vid_unk_ramdac.c:4-7`) ; le mode matériel cite la fiche SC15025/SC15026 (`vid_unk_ramdac.Materiel.cs:8`) : une
  famille, pas une référence. Vérifié.
- `keyboard_at` : le 8042, mais aussi le clavier de l'AT (sa file, ses commandes) et le port 61h, que l'en-tête
  dit « le port B du PPI, pas le 8042 » (`keyboard_at.cs:542-545`). Vérifié.
- `serial` : deux UART (`serial1`, `serial2`). Le registre de brouillon (`serial.cs:212-213`), le FCR et la FIFO
  (`serial.cs:162-164`, `:252`) sont dans le code ; `common_init` pose la FIFO sur toutes les machines
  (`model.cs:441-442`, comme `model.c:198-199`). Ni PCem ni iXtal26 ne nomment la puce : un 16550, déduit ; la note
  de National qui compare l'INS8250, le NS16450 et le NS16550AF est dans `sources/` (AN-0493, `MANIFEST.tsv`, S2),
  non relue ici.

**b) Une puce, plusieurs modules.**

- Le 6845 : cinq cartes, cinq copies (§ 1.4).
- Le MC146818 : `nvr.cs` (les ports 70h-71h, les registres, les chronomètres, les fichiers CMOS de l'hôte) et
  `rtc.cs` (l'horloge interne et ses conversions BCD et 12/24 h). `nvr` appelle `rtc` sept fois (`nvr.cs:111`,
  `:232`, `:287-289`, `:434`, `:446`, `:454`). PCem nomme la puce deux fois, et pas la même : « Motorola MC146818
  RTC » (`nvr.c:802`) et « Dallas Semiconductor DS12C887 » (`rtc.c:1-2`). Vérifié.
- Le 8255 : son état dans `ppi.cs` (`pa`, `pb`, `s2`, et `ppi_reset`), ses ports dans les claviers : `keyboard_xt.cs`
  (60h-62h, `:102`, `:144`, `:166`, `:170`), `keyboard_amstrad.cs` (60h-65h), `keyboard_olim24.cs` (61h),
  `keyboard_at.cs:545` (61h). Le mot de commande 63h n'est traité nulle part sur le XT (`grep -n 'case 0x63'` :
  seul `keyboard_amstrad.cs:117`). Le 8255 n'est pas modélisé en tant que tel : deux verrous. Vérifié.
- L'OPL : `dbopl.cs` (la synthèse), `sound_dbopl.cs` (les registres d'index, le statut et les deux chronomètres de la
  puce : `opl_write`, `opl_read`, `opl_status_update`, `opl_timer_over`, `sound_dbopl.cs:93-158`), `sound_opl.cs`
  (le câblage des cartes : `opl2_*`, `opl3_*`, `ym3812_timer_set_0/1`, `ymf262_timer_set`). Vérifié.
- Les RAMDAC : `vid_unk_ramdac` et `vid_tkd8001_ramdac` ne portent que le registre de commande caché derrière 3C6h ;
  la palette elle-même (3C7h-3C9h) est dans `vid_svga.cs:482-488`. Vérifié.

**c) Une puce noyée dans une carte.** Le CRTC de la CGA (et des quatre autres, § 1.4) ; les trois mélangeurs de la
Sound Blaster ; le 765/82077 dans `fdc.c` — mais `PLAN.md:549` range explicitement le FDC parmi les cartes ; la
puce VGA de chaque carte SVGA. Le DSP de la Sound Blaster et l'EMU8000, eux, ont leur propre module.

**d) Les jeux de puces déjà nommés d'après leur puce.** Quatre modules, tous en `iXtal26.Models`, chacun un fichier
d'index et de données en 22h, qu'appelle un `at_*_init` de `model.c` (`model.c:452-504`) :

| Module | Ce que le code modélise | La référence dans le dépôt | Niveau |
|---|---|---|---|
| `opti495` | 0x21 bit 4 (cache), 0x22 bit 7 (l'ombre du BIOS) | OPTi 82C495 (`opti495.c:1`) | vérifié |
| `headland` | le remappage et l'EMS de toute la RAM | HT18 (`headland.cs:10`) ; PCem : le nom | vérifié (iXtal26) |
| `neat` | 0x6E et les quatre pages EMS | « NEAT » (`neat.cs:10-11`) ; CS8221 nulle part | inconnue |
| `ali1429` | 0x13 et 0x14, l'ombre de C0000h-FFFFFh | « ALi 1429 » (`ali1429.cs:8`) ; M1429 nulle part | inconnue |

Leur nom actuel est déjà celui de la puce, en minuscules et sans fabricant ; le commentaire d'`opti495.c:53-57`
décrit en fait le 82C493 (`opti495.cs:24-26`). `git grep -i 'cs8221\|m1429'` : aucune occurrence. Vérifié.

**e) Le RTC et la NVR.** Voir b) : une puce, deux modules, deux noms dans PCem. Décision D6.

**f) Le haut-parleur, le port jeu, la NMI.** `sound_speaker` n'est pas une puce : il échantillonne la sortie du
canal 2 du PIT et la porte du port 61h (`keyboard_at.cs:546-553`). Le port jeu : `grep -rn -i '558\|555'` sur
`Joystick/` et sur `pcem-dev/src/joystick/` ne rend rien ; `gameport_time` calcule la durée de la bascule, aucune
puce n'est modélisée. `nmi.cs` est le masque de la NMI (les 12 lignes de `nmi.c`, `nmi.cs:5`). Vérifié.

**g) Les RAMDAC.** Voir a) et b) : deux modules d'une puce chacun, la palette ailleurs.

### 1.4 Le 6845, en particulier

**Ce qui existe.** Aucun module CRTC : `grep -rn 'class .*6845\|class .*[Cc]rtc' iXtal26 --include=*.cs` ne rend que
`Masque6845` (`vid_mda.Materiel.cs:38`). Chaque carte à 6845 porte en ligne :

- l'état : `crtc = new uint8_t[32]` et `crtcreg` (`vid_cga.cs:38-39`, `vid_mda.cs:33-34`, `vid_hercules.cs:34-35`,
  `vid_pc1512.cs:43-44`, `vid_olivetti_m24.cs:34-35`) ;
- l'écriture et la lecture des registres dans `*_out` et `*_in` (`vid_cga.cs:89`, `vid_mda.cs:71`,
  `vid_hercules.cs:64`, `vid_pc1512.cs:76`, `vid_olivetti_m24.cs:69`) ;
- le masque d'écriture `crtcmask`, recopié trois fois par PCem lui-même (`vid_cga.c:16`, `vid_pc1512.c:45`,
  `vid_olivetti_m24.c:41`, transcrits en `vid_cga.cs:85`, `vid_pc1512.cs:72`, `vid_olivetti_m24.cs:65`) ; la MDA et
  l'Hercules n'en ont pas (PB-97) ;
- les compteurs du balayage (`sc`, `vc`, `ma`, `con`, `coff`, `vadj`… : `vid_cga.cs:46-50`) mêlés au rendu de la
  carte dans `*_poll` (`cga_poll`, `vid_cga.cs:218-633` ; `mda_poll`, `vid_mda.cs:179-379`).

Le mode matériel ajoute trois éléments communs, dans des classes à part : `vid_mda.Masque6845.largeur` (PB-97,
`vid_mda.Materiel.cs:38-49`, lu aussi par `vid_hercules.Materiel.cs:44`), `video.LigneBornee` (PB-89,
`video.Materiel.cs:28`) et, dans l'hôte, `SdlHost.Lignes6845` (`Host/SdlHost.cs:929`). L'EGA et les SVGA ont leur
propre CRTC (`vid_ega.cs:41-42`, `vid_svga.cs:64-65`, 128 registres), qui n'est pas un 6845 : la protection de R0-R7
par CR11 bit 7 (`vid_ega.cs:262`) n'est pas du 6845 (déduit). Vérifié pour le reste.

**Ce que la ligne de PLAN.md demanderait.** « Le CRTC → `Motorola6845` » (`PLAN.md:548`) suppose un module à
renommer ; il n'existe pas. Le créer, c'est : un type d'état commun (R0-R17 et l'index), une écriture et une lecture
communes, avec deux masques (celui de la CGA, et aucun pour la MDA), sans doute les compteurs ; et récrire cinq
fichiers transcrits, où la ligne de PCem `cga->crtc[cga->crtcreg] = val & crtcmask[cga->crtcreg]` (`vid_cga.c:37`)
deviendrait un appel. Ce n'est plus un renommage mécanique : la parité de lignes (R2), les `// pcem:` des fonctions
touchées et le code machine des `*_poll` (un chemin chaud du rendu) changeraient. Décision D2.

### 1.5 Les noms proposés

Sur le modèle de Julien (`PLAN.md:548` : fabricant et référence, sans tiret ; `Intel8259A` garde son A,
`Intel8237` et `Intel8253` n'en ont pas, `Motorola6845` perd le MC). Les trois premiers sont décidés ; les autres sont
des propositions, dont la forme exacte est la décision D3. Namespace proposé : `iXtal26.Ics` (D4).

| Classe actuelle (namespace) | Puce | Nom proposé | Fichier(s) dans `Ics/` |
|---|---|---|---|
| `pic` (Models) | 8259A ×2 | `Intel8259A` (décidé) | `Intel8259A.cs`, `Intel8259A.Materiel.cs` |
| `pit` (Models) | 8253/8254 | `Intel8253` (décidé) | `Intel8253.cs`, `Intel8253.Materiel.cs` |
| `dma` (Models) | 8237 ×2 | `Intel8237` (décidé) | `Intel8237.cs`, `Intel8237.Materiel.cs` |
| `keyboard_at` (Keyboard) | 8042 | `Intel8042` | `Intel8042.cs`, `Intel8042.Materiel.cs` |
| `ppi_c` (iXtal26) | 8255A | `Intel8255` | `Intel8255.cs` |
| `nvr` (Devices) | MC146818 | `Motorola146818` | `Motorola146818.cs` |
| `serial` (Models) | 16550 | `National16550` | `National16550.cs` |
| `sound_emu8k` (Sound) | EMU8000 | `Emu8000` | `Emu8000.cs`, `Emu8000.Materiel.cs` |
| `vid_tkd8001_ramdac` (Video) | TKD8001 | `TridentTKD8001` | `TridentTKD8001.cs` |
| `vid_unk_ramdac` (Video) | SC1502x | `SierraSC1502x` | `SierraSC1502x.cs`, `….Materiel.cs` |
| `opti495` (Models) | 82C495 | `Opti82C495` | `Opti82C495.cs` |
| `headland` (Models) | HT18 | `HeadlandHT18` | `HeadlandHT18.cs` |
| `ali1429` (Models) | « 1429 » | `AliM1429` si la fiche le confirme | `AliM1429.cs` |
| `neat` (Models) | « NEAT » | `ChipsCS8221` si la fiche le confirme | `ChipsCS8221.cs` |

Les types transcrits des `struct` du C (`PIC`, `PIT`, `PIT_nr`, `dma_t`, `PPI`, `SERIAL`, `keyboard_at_t`,
`emu8k_*`, `unk_ramdac_t`, `tkd8001_ramdac_t`) ne sont pas des modules : ils gardent leur nom et suivent leur fichier.

**Ce qui reste dans les autres répertoires.** `Models/` garde les machines (`model`, `amstrad`, `olivetti_m24`) et
`nmi` ; `Keyboard/` garde `keyboard`, `keyboard_xt`, `keyboard_amstrad`, `keyboard_olim24` (les ports 60h-65h du
8255 y restent) ; `Devices/` garde `cassette` ; la racine garde `io`, `pc`, `timer` et `rtc` (D6) ; `Sound/` garde
les cartes (`sound_sb`, ses mélangeurs, `sound_sb_dsp`, `sound_adlib`, `sound_mpu401_uart`), l'OPL (`dbopl`,
`sound_dbopl`, `sound_opl`) et le mixeur ; `Video/` garde toutes les cartes, leurs 6845 en ligne, `video`, le socle
SVGA et sa palette ; `Floppy/` garde `fdc_c` ; `Cpu/` reste entier. Les fonctions de câblage restent où elles sont :
`model.cs` appelle `opti495.opti495_init()` et ses voisins (un site qualifié chacun).

**Non retenus, et pourquoi** (sauf décision contraire, D5) : l'OPL (deux puces, trois modules, et une bibliothèque
tierce, DOSBox, que le dépôt nomme d'après elle comme `softfloat`, `.editorconfig:75-76`) ; les mélangeurs de la Sound
Blaster (en ligne dans la carte, à découper) ; le DSP (puce inconnue, plusieurs versions) ; le MPU-401 (une
interface) ; les cœurs UC (plusieurs puces chacun) ; le FDC et les cartes SVGA (des cartes par `PLAN.md:549`) ; le
6845 (D2) ; `rtc` (D6) ; le haut-parleur, le port jeu, la NMI (aucune puce).

### 1.6 La convention `// noms:`

**Où elle vit.** Cinq blocs, tous en tête de fichier, après l'en-tête `// ORACLE:` : `Floppy/fdc.cs:10-49` (40
lignes), `Floppy/fdd.cs:9-23` (15), `Disc/disc.cs:10-28` (19), `Disc/disc_img.cs:9-19` (11),
`Disc/disc_sector.cs:9-25` (17). `disc_img_probe.cs`, `disc.Materiel.cs`, `hdd.cs`, `hdd_file.cs` et
`fdc.Materiel.cs` n'en ont pas (compté par `grep -n '^// noms:'` et la fin du bloc au premier commentaire qui n'est
plus indenté de trois espaces). Vérifié.

**Son format exact** (`TRANSCRIPTION.md:23-29`, R1(e)) : « dans `Floppy/` et `Disc/` seulement, un bloc `// noms:`
en en-tête : une ligne par identifiant, **sans phrase**, 40 lignes au plus. Deux colonnes — l'identifiant, ce qu'il
désigne — et **trois dès qu'un nom est modifié** : nom PCem, nom iXtal26, ce qu'il désigne ». La première ligne dit
la forme : `// noms: PCem → iXtal26 → ce qu'il désigne — R1(e). « = » : nom inchangé.` (`fdc.cs:10`,
`disc_sector.cs:9`) ou `// noms: ce que les identifiants de fdd.c désignent — R1(e).` (`fdd.cs:9`). Les lignes
suivantes sont indentées de trois espaces. Elle est la « contrepartie obligatoire du nommage explicite » : « sans
elle, plus rien ne relie le fichier à `pcem-dev/` » (`TRANSCRIPTION.md:26-27`).

**Ce qu'elle liste.** Les champs et les globales (`stat → msr`, `res[] → result[]`, `fwriteprot[] →
forced_writeprot[]`), les constantes (`SECTOR_FIRST/NEXT`, `FLAG_HOLE0/1/2`), les morts (`MORTS — PB-20`), et une
ligne `TYPES :` qui nomme les types et **le conteneur** : « `FDC` = la struct de fdc.c:27-78 ; `fdc_c` = le
conteneur du fichier, suffixe `_c` parce que PCem n'a pas de classe et que `fdc` est pris par l'instance »
(`fdc.cs:45-46`) ; de même `fdd.cs:21-23`, `disc.cs:27-28`. Et une règle : « Les FONCTIONS gardent toutes les noms
de PCem : elles sont l'API entre modules » (`fdc.cs:47`). Le renommage de `Floppy/` et `Disc/` a porté sur les
champs, jamais sur les fonctions ni sur la classe. Vérifié.

**Ce qu'elle deviendrait pour un module renommé** (si D1 retient le module seul) : seule la classe change, donc la
table a au moins une ligne de trois colonnes, celle du conteneur, et la ligne `TYPES :` dit les types inchangés. Par
exemple, pour `Ics/Intel8259A.cs` :

```
// noms: PCem → iXtal26 → ce qu'il désigne — R1(e). « = » : nom inchangé.
//   pic.c       Intel8259A  le conteneur du fichier : les deux 8259A, maître et esclave
//   pic         pic_        le maître (pic.c:7) ; « _ » : `pic` était le nom du conteneur (CS0542)
//   pic2        =           l'esclave, IRQ 8 à 15
//   TYPES : PIC = la struct d'un 8259A (ibm.h:144-150), inchangée.
//   Les FONCTIONS gardent toutes les noms de PCem : elles sont l'API entre modules.
```

Deux points en suspens : R1(e) limite le bloc à `Floppy/` et `Disc/` et `TRANSCRIPTION.md:131` et `:153-165`
réservent le nommage explicite à ces deux répertoires ; il faut les amender pour `Ics/` (le fichier est à 237 lignes
sur 260, `wc -l TRANSCRIPTION.md` et R3, `TRANSCRIPTION.md:48`). Et PLAN.md dit « en tête de chaque fichier »
(`PLAN.md:553`) : les `*.Materiel.cs` ne sont pas des fichiers transcrits (R1 ne s'y applique pas) ; proposition :
le bloc dans le fichier transcrit seulement, le partiel le désignant par son en-tête (décision mineure, D7).

### 1.7 La portée, selon le texte de PLAN.md

Ce que dit le texte, mot pour mot :

- « Les **modules** qui modélisent une puce prennent le nom de la puce, dans un répertoire `Ics/` : `pic` →
  `Intel8259A`, `pit` → `Intel8253`, `dma` → `Intel8237`, le CRTC → `Motorola6845`, et ainsi de suite »
  (`PLAN.md:547-549`). Les exemples sont des noms de modules (de classes et de fichiers).
- « **Pourquoi pas avant** : pendant les transcriptions, les noms verbatim de PCem évitent de traduire chaque appel
  (`picint`, `dma_channel_read`…) dans chaque nouveau fichier » (`PLAN.md:551-552`). Les exemples sont des
  **membres**. Deux lectures tiennent : (i) les membres changent aussi en G14 ; (ii) « traduire chaque appel »,
  c'est requalifier `pic.picint` en `Intel8259A.picint` (41 sites qualifiés de `pic` dans iXtal26/, § 1.9).
- « **Comment** : renommage mécanique (refactor Rider), table `// noms:` en tête de chaque fichier, comme dans
  `Floppy/` et `Disc/` » (`PLAN.md:553-554`) : or `Floppy/` et `Disc/` n'ont renommé que des champs, en gardant
  les fonctions (`fdc.cs:47`).
- « **Porte** : … seuls des noms changent » (`PLAN.md:556-557`) : vaut pour les deux lectures.
- G15 : « les conventions .NET (PascalCase, `private` explicite, `enum` …) » (`PLAN.md:566-567`) : G15 renomme
  de toute façon chaque membre (`picint` devient au moins `Picint` ; déduit du mot « PascalCase ») ; « Conservés :
  les marqueurs `// pcem:` » (`PLAN.md:569`).

Le texte ne tranche pas : c'est la décision D1.

### 1.8 Ce que le harnais et les sondes désignent par leur nom

`PLAN.md:554-555` demande de le vérifier d'abord. Ce qui a été relu :

- Le harnais C (`tools/oracle/*.c`) ne connaît que les noms de PCem ; `Oracle.cs` l'appelle par ses symboles
  `h_*` (P/Invoke). `h_state` (`harness.h:95`) est copié par disposition, ses champs portent des noms C
  (`HState.cs:146-147`, `n_picint`, `n_picinterrupt`) : aucun nom de classe C#. Vérifié.
- boot-diff : ses étiquettes sont des chaînes aux noms des globales de PCem, `"dma[{c}].ab|ac"` (`BootDiff.cs:1898`),
  `"disc_track[0]"` (`:2091`), et `fdc.cs:48-49` le dit : elles « désignent les globales du C, pas les champs
  d'ici ». Un renommage de classe ne les touche pas ; il ne faut pas les renommer. Vérifié.
- sst-probe et sst386-probe lisent le JSON des corpus (`SstProbe.cs:533-573`) : le cœur UC seul. Vérifié.
- La réflexion par chaîne : `GetField("speaker_pos")`, `GetField("speaker_buffer")` (`MaterielCasPit.cs:52`, `:55`,
  sur `sound_speaker`), `GetField("memcycs")` (`MaterielCas.cs:513`, `_808x`), `GetMethod("setadc8")`
  (`perfbanc/GardeBanc.cs:93`, `_808x`), `GetMethod("ILLEGAL")` (`386.Materiel.cs:314`, `_386`) et un filtre sur
  les suffixes `_iir_x`/`_iir_y` des champs de `sound_sb` (`MaterielCasFrequence.cs:85-87`). Ce sont des membres de
  classes non candidates ; les `typeof(...)` sont résolus à la compilation. Commande : `grep -rn -E
  'GetField|GetProperty|GetMethod|typeof\(|BindingFlags|nameof\(' --include=*.cs iXtal26 tools`. Vérifié.
- Les chaînes interpolées citent des classes candidates (`$"IRR{irq}={(pic.pic_.pend >> irq) & 1}"`,
  `MaterielCasLigne.cs:20` ; `TimerCheck.cs:157-160`, `:574-580`) : ce sont du code, que le refactor renomme.
- `listings-jit.sh` : la comparaison découpe les listings par l'en-tête `; Assembly listing for method NOM` et
  compare les corps par leur texte (`tools/listings-jit.sh:178-217`). Les noms y sont complets : dans la capture
  `/var/tmp/ixtal-g13/M2-13b`, `call [iXtal26.Models.pic:picint(ushort)]` revient 168 fois, et quinze des seize
  classes candidates (toutes sauf `rtc_h`) y ont 108 méthodes listées et 5 574 mentions dans les corps (commande :
  `grep -h '^; Assembly listing for method' *.txt | grep -E "[.]pic:" | sort -u | wc -l`, et `grep -h -E
  "[.]pic:[A-Za-z_]"` hors en-têtes, classe par classe). Sans table de correspondance, chaque méthode renommée se
  lirait « disparue » et « apparue », chaque appelant « changé ». Vérifié.

### 1.9 Les comptes

Méthode (annexe B) : pour chaque classe, ses membres statiques sont relevés (la profondeur d'accolades), puis on
compte dans le code sans commentaires (les chaînes gardées, pour les trous des chaînes interpolées) : les
déclarations `class X` (une par fichier partiel), les `using static …X;`, les sites qualifiés `X.membre` (le
membre devant être un statique de X, ce qui écarte les variables locales homonymes, `PIT pit` dans `pit.cs`), et les
`typeof(X)`/`nameof(X)`. Commande : `python3 -I compte-g14.py /home/jpeloille/RiderProjects/iXtal928
pic,pit,dma,…`. Les appels non qualifiés (via `using static`) ne changent pas si seul le module change.

| Classe | iXtal26/ : fichiers, sites (décl., using, qualifiés) | iXtal26.Diff : fichiers, sites |
|---|---|---|
| `pic` | 28 fichiers, 56 sites (2, 13, 41) | 5 fichiers, 80 sites |
| `pit` | 30, 98 (2, 4, 92) | 2, 9 |
| `dma` | 10, 11 (2, 4, 5) | 2, 24 |
| `keyboard_at` | 5, 5 (2, 2, 1) | 1, 24 |
| `ppi_c` | 6, 6 (1, 4, 1) | — |
| `nvr` | 7, 10 (1, 0, 9) | 1, 4 |
| `rtc` / `rtc_h` | 2, 8 (1, 0, 7) / 2, 3 (1, 2, 0) | — |
| `serial` | 5, 14 (1, 0, 13) | — |
| `sound_emu8k` | 4, 6 (2, 0, 4) | 4, 33 |
| `vid_tkd8001_ramdac` | 2, 2 (1, 1, 0) | — |
| `vid_unk_ramdac` | 3, 3 (2, 1, 0) | — |
| `ali1429` | 3, 5 (1, 0, 4) | — |
| `headland`, `neat`, `opti495` | 2, 2 (1, 0, 1) chacun | — |
| **Les seize, union** | **69 fichiers, 233 sites** | **11 fichiers, 174 sites** |
| Les trois décidés, union | 51 fichiers, 165 sites | 7 fichiers, 113 sites |

perfbanc, `iXtal26.SoftFloat` et `tools/softfloat` : aucun site (le même script ; et `grep -nw -E
'pic|pit|dma|nvr|…' tools/perfbanc/*.cs` : rien). Les scripts des portes (`tools/gates/*.sh`) ne citent aucun
chemin de `Models/` ni aucun nom qualifié (`grep -rn -E 'Models/|iXtal26\.Models'` : rien).

Pour la décision D1, les appels des membres que PLAN.md cite (`grep -rnw --include=*.cs NOM`, commentaires
exclus) : `picint` 27 lignes dans 18 fichiers d'iXtal26/ et 20 dans tools/ ; `picintc` 21 lignes (14 fichiers) ;
`dma_channel_read` 10 (6) ; `dma_channel_write` 8 (5) et 22 dans tools/. Les trois globales renommées par collision
avec leur conteneur (`pic_`, `pit_`, `dma_`, `pit.cs:79-81` : « CS0542 : la globale `PIT pit` … ne peut pas porter
le nom de sa classe conteneur ») : 106, 50 et 70 lignes (`grep -rnw --include=*.cs pic_ iXtal26 tools | wc -l`).

Les chemins des fichiers : `git grep -h -o -E "(^|[^A-Za-z0-9_])(pic|pit|dma|nvr|rtc|ppi|keyboard_at|serial|
sound_emu8k|vid_tkd8001_ramdac|vid_unk_ramdac|ali1429|headland|neat|opti495)(\.Materiel)?\.cs" -- . ':!pcem-dev' |
wc -l` rend 217 mentions : `PCEM_BUGS.md` 48, `VERIFICATION.md` 31, `oracle.tsv` 24, `Docs/G13-reconnaissance/` 33,
`Docs/GR-reconnaissance/` 8, `Docs/ibm5150/` 9, `TRANSCRIPTION.md` 3, des `PLAN-*.md`, et des commentaires du code
et du harnais C (`harness.c`, `harness_stubs.c`, une ligne chacun) (`git grep -c` du même motif).

Les lignes qui passeraient 120 colonnes avec les noms proposés : une simulation par substitution (`X.` → `Nom.` hors
commentaires) en trouve 14, dont 8 sont de vrais sites de classe : `808x.cs:768`, `:4091` (111 et 110 colonnes,
`pic.pic_` deux fois), `808x.Materiel.cs:444`, `:448`, `nvr.cs:264` (`pit.RTCCONST`), `MaterielCas.cs:3530`,
`:3588`, `:3594` ; les six autres (`pit.cs:280`, `:340`, `:436`, `serial.cs:86`, `:91`, `:96`) sont des variables
locales homonymes. Relu ; à recompter sur le code renommé, avec les noms que D3 retiendra.

## 2. Ce qu'il faut faire, et les étapes proposées

Préalable, hors de cette lecture : la référence de performance figée « avant G14 » (`PLAN.md:586-588`) ; et
« plus aucun fichier PCem ne reste à transcrire » (`PLAN.md:544-545`).

**G14.0 — Les outils et les règles, sans toucher l'émulateur.**
- Contenu : `listings-jit.sh compare` reçoit une table de correspondance (ancien nom complet → nouveau, par classe
  et namespace), appliquée aux en-têtes et aux corps avant la comparaison ; `.editorconfig` ajoute `Ics` au glob
  du code transcrit (`.editorconfig:40`) et retire de son commentaire la raison « leur nom doit être celui du fichier
  C » pour les classes qui partent (`:47-53`) ; `TRANSCRIPTION.md` amende R1(e) (`:23`), la ligne Nommage (`:131`)
  et « Nommage explicite » (`:153-165`) pour `Ics/`, dans le plafond de R3.
- Portes et preuves : la construction attendue identique (sha256 de `iXtal26.dll` avant et après ; rien de
  l'émulateur ne change, et `.editorconfig` ne règle que des diagnostics : déduit, à constater) ; `compare` d'une
  capture contre elle-même : 0 changée ; `wc -l TRANSCRIPTION.md` ≤ 260.
- Contrôles négatifs : une table qui renomme une méthode inexistante est refusée ; une capture prise sur un
  binaire où une constante a changé rend « changée » avec la table : la table n'efface pas un vrai changement.

**G14.1 — Les trois puces décidées : `Intel8259A`, `Intel8253`, `Intel8237`.**
- Contenu : `git mv` des six fichiers (`pic`, `pit`, `dma` et leurs `.Materiel.cs`) vers `Ics/` ; le renommage des
  classes et du namespace au refactor de Rider, **sans** l'option qui renomme aussi dans les commentaires et les
  chaînes ; le bloc `// noms:` de chaque fichier transcrit ; `oracle.tsv` (6 lignes), les mentions vivantes
  (`TRANSCRIPTION.md`, `PCEM_BUGS.md`, les commentaires du code et du harnais) ; les lignes devenues trop longues.
- Portes et preuves : la construction sans avertissement ; `check-oracle.sh` 0 dérive ; toutes les séries
  identiques à l'unité (boot-diffs, SST, fuzzeurs, portes) contre leur référence ; `listings-jit.sh compare` avec
  la table : 0 changée, 0 disparue, 0 apparue ; le banc contre la référence d'origine, aucune régression
  significative ; la `.so` de l'oracle inchangée (sha256) ; `grep` des anciens noms qualifiés et des `using static
  iXtal26.Models.pic;` : 0 ; aucune ligne > 120.
- Contrôles négatifs : `compare` **sans** la table rend les méthodes des trois classes disparues et apparues (la
  capture les voit, la table fait le travail) ; `oracle.tsv` laissé à l'ancien chemin fait sortir `check-oracle.sh`
  en « ABSENT C# » (`tools/check-oracle.sh:50-51`) ; les étiquettes de boot-diff (`"dma[{c}]…"`) sont inchangées
  (`git diff` sur `BootDiff.cs` limité aux sites qualifiés).

**G14.2 — Les autres puces de la carte mère : `Intel8042`, `Intel8255`, `Motorola146818`, l'UART.** Selon D3, D5
et D6. Même contenu, mêmes portes, mêmes contrôles négatifs ; en plus, `nvr` change aussi de répertoire
(`Devices/` → `Ics/`) et `ppi_c` quitte la racine et la section `[**/{io,timer,pc,ppi}.cs]` de `.editorconfig:97`.

**G14.3 — Les puces des cartes : `Emu8000`, `TridentTKD8001`, `SierraSC1502x`.** Même contenu, mêmes portes ; les
portes du son (SonBanc, sb16banc, awebanc) et de la vidéo (et4banc, la 8900D) sont dans les séries.

**G14.4 — Les jeux de puces : `Opti82C495`, `HeadlandHT18`, et les deux autres si D3 les confirme.** Mêmes portes ;
les boot-diffs de l'ami286, de l'ami386, de l'ami386dx et de l'ami486 sont ceux qui les exercent.

**G14.5 — La clôture.** `VERIFICATION.md` § G14 : les commits, les séries, les comparaisons de listings avec et
sans la table, les mesures du banc ; `PLAN.md` « G14 est fait » ; le sort du 6845 (D2) écrit.

Grouper les puces par commit réduit le nombre de séries entières et de mesures du banc, qui se font à chaque commit
(`PLAN.md:589`) ; une étape par puce reste possible si Julien la préfère.

## 3. Les risques

1. **Le refactor renomme dans les commentaires et les chaînes.** `pic.c` deviendrait `Intel8259A.c` dans les
   `// pcem: pic.c:7` et les `// ORACLE:`, et les étiquettes de boot-diff changeraient. Parade : l'option de texte
   coupée, puis une revue du diff ligne à ligne ; contrôle : `git diff | grep '^[-+].*// pcem:'` vide.
2. **Le glob de `.editorconfig`.** Un fichier déplacé dans `Ics/` sort de `[**/{Cpu,…,Mouse}/**.cs]`
   (`.editorconfig:40`) ; ses membres en `snake_case` tombent sous IDE1006 et les autres règles neutralisées, avec
   `EnforceCodeStyleInBuild` et `TreatWarningsAsErrors` (`iXtal26.csproj:38-39`). Que la construction échoue
   réellement : inconnu (rien construit) ; la parade est en G14.0.
3. **La comparaison des listings sans table** donne un faux rouge massif (§ 1.8) ; avec une table fausse, un faux
   vert. Les deux contrôles négatifs de G14.0 et G14.1 le couvrent.
4. **Fusionner deux classes** (`nvr` et `rtc` en une seule) changerait le moment où leurs champs statiques
   s'initialisent : l'initialiseur de type devient commun. Ce n'est plus « seuls des noms changent ». D6 l'évite.
5. **Les lignes de plus de 120 colonnes** (huit, § 1.9), dont deux dans `808x.cs`, le chemin chaud du
   8088 : les recouper ne change pas le code machine (déduit), mais change la parité de lignes de R2 d'une ligne.
6. **Les `using` devenus inutiles** (`using iXtal26.Models;` dans un fichier qui n'y prenait que `pic`) : IDE0005 ;
   qu'il soit bloquant à la construction : inconnu.
7. **Le nom dit moins que le code** : `Intel8253` porte la relecture du 8254 (§ 1.3 a) ; `National16550` porterait
   aussi le 16450 sans FIFO ; `Intel8042` porte aussi le clavier de l'AT. Le bloc `// noms:` doit le dire.
8. **Les documents datés** (`VERIFICATION.md`, `Docs/*-reconnaissance/`) gardent les anciens chemins : une recherche
   par l'ancien nom les retrouve, une recherche par le nouveau non (D7).
9. **L'historique** : un `git mv` suivi d'une récriture de plus de la moitié du fichier ferait perdre la filiation à
   `git log --follow` ; ici seules quelques lignes changent par fichier (déduit).

## 4. Les décisions ouvertes

**D1 — La portée : le nom du module seul, ou aussi ses membres.** Le texte cite des modules en exemple et des
membres en justification (§ 1.7). Proposition : **le module seul** (la classe, son fichier, son répertoire, son
namespace) ; les membres gardent leurs noms, `pic_`, `pit_` et `dma_` compris. Raisons : `Floppy/` et `Disc/`, le
modèle que PLAN.md désigne, ont gardé les fonctions (`fdc.cs:47`) ; G15 renommera chaque membre en PascalCase
(`PLAN.md:566`), et les renommer deux fois doublerait le travail et les séries ; le bloc `// noms:` reste court (une
ligne de conteneur et les types). Variante, si Julien veut le verbatim là où la collision disparaît : rendre aux
trois globales leur nom de PCem (`pic_` → `pic`, `pit_` → `pit`, `dma_` → `dma` ; 226 lignes, § 1.9), ce qui est un
renommage de membre, donc à décider expressément.

**D2 — Le 6845.** Trois voies : (a) pas de `Motorola6845` en G14, le 6845 reste dans les cinq cartes, comme dans
PCem, qui le recopie par carte (§ 1.4), et la ligne de PLAN.md est notée « sans module à renommer » ; (b) un module
neuf `Ics/Motorola6845.cs`, extrait des cinq cartes : un refactoring, hors de « renommage mécanique » et de « seuls
des noms changent », avec un risque sur le chemin chaud du rendu ; (c) déplacer seulement `vid_mda.Masque6845`
(la largeur des registres du MC6845, code du mode matériel, deux lecteurs) en `Ics/Motorola6845`, renommage
mécanique d'une classe qui porte déjà le nom de la puce, mais qui ne modélise pas la puce. Proposition : **(a)**,
parce que (b) sort de la porte de G14 et que (c) donnerait le nom de la puce à une table qui n'en est pas le
modèle ; (b) reste à Julien, comme une étape à part s'il la veut.

**D3 — La forme des noms et les références incertaines.** Les exemples de Julien ne fixent pas tout : le suffixe
(`8259A` gardé, `8237A` et `8253-5` non), le préfixe (`MC` retiré). Proposition : le fabricant, puis la référence
telle que la fiche l'imprime, sans le préfixe `MC` de Motorola (l'exemple) ni le grade de vitesse, le suffixe gardé
seulement quand il change le comportement modélisé (le A du 8259A : ICW4 et l'AEOI) ; d'où `Intel8042`,
`Intel8255`, `Motorola146818`, `National16550`, `Emu8000` (la référence porte déjà le fabricant), `TridentTKD8001`,
`SierraSC1502x` (la famille que PCem nomme ; `SierraSC15025` si Julien tient la référence de la fiche citée),
`Opti82C495`, `HeadlandHT18`. Pour `neat` et `ali1429`, aucune référence n'est écrite dans le dépôt : renommer
seulement sur la foi d'une fiche (`ChipsCS8221`, `AliM1429`), sinon les laisser à G15.

**D4 — Le namespace.** PLAN.md dit le répertoire (`Ics/`), pas le namespace. Proposition : **`iXtal26.Ics`**,
parce que répertoire et namespace coïncident partout sauf `Materiel/` (§ 1.1) ; le coût est un `using iXtal26.Ics;`
dans les fichiers qui voyaient la classe par leur propre namespace (le refactor l'ajoute).

**D5 — Les puces retenues au-delà des trois exemples.** « Et ainsi de suite » (`PLAN.md:549`) ne fait pas la liste.
Proposition : les onze autres de § 1.5, parce que chacune est un module d'une puce (ou de deux exemplaires) qui se
renomme sans découpage ; et ne pas retenir l'OPL, les mélangeurs et le DSP de la Sound Blaster, le MPU-401, les
cœurs UC, le FDC et les cartes SVGA (raisons en § 1.5). L'OPL est le seul cas discutable : `sound_dbopl` porte la
face d'E/S de la puce ; le laisser évite de donner un nom de puce à deux modules sur trois.

**D6 — Le MC146818 en deux modules.** Proposition : `nvr` → `Motorola146818`, et `rtc` (avec `rtc_h`) reste à la
racine sous son nom. Raisons : fusionner les deux classes change l'initialisation de leurs statiques (risque 4) ;
donner le même nom de puce à deux classes demanderait un suffixe inventé ; PCem attribue `rtc.c` à une autre puce
(le DS12C887, `rtc.c:1-2`). Le bloc `// noms:` de `Motorola146818` dit que l'horloge est dans `rtc.cs`.

**D7 — Les documents datés, et le bloc des `*.Materiel.cs`.** Proposition : les documents vivants suivent
(`oracle.tsv`, `TRANSCRIPTION.md`, `PCEM_BUGS.md`, les commentaires du code et du harnais) ; les journaux datés
(`VERIFICATION.md` avant G14, `Docs/*-reconnaissance/`) restent tels qu'écrits, et la section G14 de
`VERIFICATION.md` donne la table des anciens et nouveaux chemins. Le bloc `// noms:` va dans le fichier transcrit
seulement ; le `*.Materiel.cs` le désigne par son en-tête.

## Annexe A — Hors de la portée

Des constats faits en route, sans proposition ; aucun n'est nécessaire à G14.

1. `pit` exécute la relecture du 8254 sur le 5150 et le XT, qui portent un 8253 (`pit.c:328` sans garde de machine,
   `PCEM_BUGS.md:152`) ; aucune entrée ne le dit (`grep -n -i 'read-back\|relecture' PCEM_BUGS.md | grep -i -E
   '825[34]|pit'` : rien).
2. `common_init` pose la FIFO du 16550 sur toutes les machines, le 5150 compris (`model.cs:441-442`,
   `model.c:198-199`) ; `grep -n -i 'has_fifo\|16550' PCEM_BUGS.md` : rien.
3. `video.Materiel.cs:4-5` range l'EGA parmi les « cartes à 6845 » ; son CRTC n'en est pas un (§ 1.4, déduit).
4. `.editorconfig:90-91` annonce que « G14 et G15 » renommeront `materiel` et `pb_nn` ; le texte de G14 ne vise que
   les puces : ce sera G15.
5. `MaterielCasFrequence.cs:87` choisit des champs de `sound_sb` par leur suffixe (`_iir_x`, `_iir_y`) : le
   PascalCase de G15 le casserait sans erreur de compilation.
6. PCem attribue la même horloge à deux puces (`nvr.c:802`, `rtc.c:1-2`), et `opti495.c` décrit le 82C493 sous le
   nom du 82C495 (`opti495.c:53-57`).

## Annexe B — La commande de compte

Le script, jetable, hors du dépôt ; lancé par `python3 -I compte-g14.py /home/jpeloille/RiderProjects/iXtal928
pic,pit,dma,nvr,rtc,rtc_h,ppi_c,keyboard_at,serial,sound_emu8k,vid_tkd8001_ramdac,vid_unk_ramdac,ali1429,headland,
neat,opti495` (la liste sans retour à la ligne).

```python
#!/usr/bin/env python3
# Les sites qu'un renommage de classe toucherait. python3 -I compte-g14.py DEPOT classe1,classe2,...
import os, re, sys
from collections import defaultdict
depot, classes = sys.argv[1], sys.argv[2].split(',')
zones = {'iXtal26': 'iXtal26', 'iXtal26.Diff': 'tools/iXtal26.Diff', 'perfbanc': 'tools/perfbanc',
         'SoftFloat': 'iXtal26.SoftFloat', 'SoftFloatBanc': 'tools/softfloat'}

def fichiers(d):
    for dp, dn, fn in os.walk(os.path.join(depot, d)):
        dn[:] = [x for x in dn if x not in ('bin', 'obj')]
        yield from (os.path.join(dp, f) for f in fn if f.endswith('.cs'))

def sans_commentaires(t):  # les chaînes restent : les trous des chaînes interpolées comptent
    return re.sub(r'//[^\n]*|/\*.*?\*/', lambda m: re.sub(r'[^\n]', ' ', m.group(0)), t, flags=re.S)

DECL = re.compile(r'\b(?:class|struct)\s+([A-Za-z_]\w*)')
MEMBRE = re.compile(r'\bstatic\b[^;=({]*?\b([A-Za-z_]\w*)\s*(?:[;=({]|=>)')
statiques = defaultdict(set)
for d in zones.values():
    for f in fichiers(d):
        pile, prof, attente = [], 0, None
        for ligne in sans_commentaires(open(f, encoding='utf-8').read()).split('\n'):
            md = DECL.search(ligne)
            if md:
                attente = md.group(1)
            elif pile and prof == pile[-1][1] + 1 and pile[-1][0] in classes:
                mm = MEMBRE.search(ligne)
                if mm:
                    statiques[pile[-1][0]].add(mm.group(1))
                    reste = ligne[ligne.find('static'):].split(';')[0]
                    if '(' not in reste.split('=')[0] and ',' in reste:  # int a, b, c;
                        for seg in reste.split(','):
                            ids = re.findall(r'[A-Za-z_]\w*', seg.split('=')[0])
                            if ids:
                                statiques[pile[-1][0]].add(ids[-1])
            for ch in ligne:
                if ch == '{':
                    if attente:
                        pile.append((attente, prof)); attente = None
                    prof += 1
                elif ch == '}':
                    prof -= 1
                    if pile and pile[-1][1] == prof:
                        pile.pop()
            if attente and ligne.rstrip().endswith(';'):
                attente = None

union = defaultdict(set)
for c in classes:
    alt = '|'.join(sorted(map(re.escape, statiques[c]), key=len, reverse=True)) or r'\b\B'
    motifs = {'decl': r'\b(?:class|struct)\s+' + c + r'\b',
              'using': r'(?m)^\s*using\s+static\s+[\w.]*\b' + c + r'\s*;',
              'qualif': r'(?<!\w)' + c + r'\.(?:' + alt + r')\b',
              'typeof': r'\b(?:typeof|nameof)\(\s*[\w.]*\b' + c + r'\b'}
    print(f'{c} : {len(statiques[c])} membres statiques')
    for z, d in zones.items():
        n, fic = defaultdict(int), set()
        for f in fichiers(d):
            t = sans_commentaires(open(f, encoding='utf-8').read())
            for k, m in motifs.items():
                k_n = len(re.findall(m, t))
                if k_n:
                    n[k] += k_n; fic.add(os.path.relpath(f, depot))
        if fic:
            union[z] |= fic
            print(f'  {z} : {len(fic)} fichiers, {sum(n.values())} sites {dict(n)}')
for z, f in union.items():
    print(f'union {z} : {len(f)} fichiers')
```

Ses limites : un `//` dans une chaîne est pris pour un commentaire ; un membre statique déclaré sur plusieurs lignes
peut échapper au relevé (les comptes sont alors des minorants) ; un site `X.m` où `X` serait une variable locale et
`m` un statique homonyme de la classe serait compté à tort (aucun cas relevé à la relecture des sites de `pit`).
