# G12 — Les autres Sound Blaster : 1.0, 1.5, 2.0, Pro v1, 16 et AWE32 ; le plan, sur reconnaissance

> **G12 est fait** (6 octobre 2026) : G12.0 à G12.3, VERIFICATION.md §§ G12.0 à G12.3 ; PB-145 à PB-167.

> Écrit le 6 octobre 2026. Bloc G12 de `PLAN.md` (décision utilisateur du 03/10). Feu vert le 05/10, avec
> G11, « en totale autonomie » : les décisions de la fin de fichier sont prises sous ce mandat, chacune datée.
> Chaque constat cite la ligne de C qui le fonde, sur `pcem-dev/` tel que vendoré. Reconnaissance : trois
> lectures (les cartes et leurs mélangeurs ; le DSP et le DMA 16 bits ; l'EMU8000 et l'intégration), chacune
> suivie d'une contre-lecture qui l'a corrigée. On retient ici les contre-lectures.

## Où on en est

- **Le son de G8** : la SB Pro v2 seule (`Sound/sound_sb.cs`), l'AdLib et le DBOPL. Le registre
  `SOUND_CARD` n'a que trois entrées, none, adlib et sbprov2 (`sound.cs:184-200`). L'OPL est complet :
  `opl2_init` à deux puces, `opl2_l_*` et `opl2_r_*`, `opl3_init` (`sound_opl.cs`).
- **Le DSP** (`Sound/sound_sb_dsp.cs`) est transcrit pour le chemin SBPRO2 en 8 bits. Les gardes de version
  (`< SB15`, `< SB2`, `< SB16`) sont recopiées : **les SB 1.0, 1.5, 2.0 et Pro v1 n'exigent aucun code de DSP
  neuf.** Restent omis le DMA 16 bits et tout ce que garde `sb_type >= SB16`, soit environ 160 lignes vives.
- **L'oracle** lie `sound_sb.c` et `sound_sb_dsp.c` (`Makefile:58-59`). Les fonctions de l'EMU8000 et
  `mpu401_uart_init` sont des souches fatales (`harness_stubs.c:1246-1249`).
  - `h_boot` ne monte qu'adlib et sbprov2, et un nom inconnu n'y monte rien, sans un mot (`harness.c:1968-1971`).
  - `h_sb_probe` ne cherche que `&sb_pro_v2_device` (`:2384`) et ne lit que `mixer_sbpro`.
- **La machine.** Le DMA 16 bits est transcrit (`dma.cs:239-362`), et `dma16_init` est posé sur l'AT
  (`model.cs:174`). Mais aucune porte ne l'a jamais mis sous l'oracle : seuls ses registres, au POST.
- **La règle ISA 16 bits** n'existe que pour les cartes de disque (`Host/HardDiskControllers.cs`,
  `pc.check_hdd_controller`). Les `device_t` de la SB 16 et de l'AWE32 ont des drapeaux à 0
  (`sound_sb.c:1349-1352`) ; l'écran de PCem ne filtre les cartes son que par le MCA et par la ROM
  (`wx-config.c:190-193`).
- **L'écran de construction** ne propose pas de carte son (`SdlSetup.cs:31-32`).
- **La ROM de l'AWE32** : `roms/awe32.raw`, 1 048 576 octets, sha256 `4e143b94…710e`, ignorée par git
  (`.gitignore:12`). C'est une image AWE-DUMP : `emu8k_init` en corrige le décalage d'un mot
  (`sound_emu8k.c:2024-2029`).

## Ce qu'il y a à transcrire

| | lignes | atteint |
|---|---:|---|
| `sound/sound_sb.c` | 1 352 | le reste, sans les deux MCV : `sb_get_buffer_sb2` et `_sb2_mixer` (`:38-85`), `_sb16` (`:127-208`), `_emu8k` (`:214-331`) ; le CT1335 (`:333-398`) et le CT1745 (`:533-785`) ; six `init`, `sb_awe32_available` et `_close` ; cinq configurations, six devices |
| `sound/sound_sb_dsp.c` | 1 326 | le DMA 16 bits ; 01h, 41h/42h, B0h-CFh, D5h/D6h/D9h, E3h, 08h, 0Eh/0Fh, F9h ; `recalc_sb16_filter` |
| `filters.h` | 297 | `low_iir`, `low_cut_iir`, `high_iir`, `high_cut_iir` (`:6-101`), `low_fir_sb16` (`:270-295`) |
| `sound/sound_mpu401_uart.c` | 82 | tout, sauf `update_addr` et `update_irq` (seul l'Aztech les appelle) |
| `sound/sound_emu8k.c` + `.h` | 2 237 + 768 | les régions vivantes, environ 1 300 et 380 lignes |
| `sound.c:39-47` | 9 | six entrées du registre, à leur place relative (`:270-291`) |

## Les défauts relevés (à lire à la ligne, puis inscrire)

**R9 — l'invité ou la configuration arrête l'hôte** (non reproduits ; `// pcem bug, not reproduced`). Le
`fatal()` de l'oracle rend la main (`harness_stubs.c:776-784`) : ces chemins restent hors des portes comparées,
et leurs tests sont en C# seul.

1. **Le registre 3Bh du CT1745.** `sb_att_2dbstep_5bits[regs[0x3B] * 3 + 22]` (`sound_sb.c:655`) indexe une
   table de 32 entrées jusqu'à 787. Le calcul est refait à chaque écriture de donnée du mélangeur dès que 3Bh
   vaut 04h ou plus, l'écriture de 3Bh comprise. En C, une lecture dans `.rodata`, sans lecteur (`speaker`
   n'est lu nulle part) ; en C#, une exception.
2. **La fréquence 0 par 41h/42h** (`sound_sb_dsp.c:393`). `(uint64_t)(TIMER_USEC * (1000000.0f / 0.0f))` vaut 0
   en C (mesuré, `-O2` sans `-march`) et `ulong.MaxValue` en .NET. Dans les deux cas l'échéance ne recule pas,
   et `timer_process` boucle sans fin dès qu'une minuterie du DSP est armée : un DMA lancé ou en cours, une
   pause, 20h (`:363-369`). Même cause par `sb_dsp_speed_changed` (`:184`, `:189`, `sb_timeo` = 256).
3. **L'AWE32 sans sa ROM.** `sound_card_init` (`sound.c:102-106`) ne teste pas `device_available` :
   `fatal("AWE32.RAW not found")` (`sound_emu8k.c:2019`). Dans l'oracle, `fatal()` rend la main, et `fread`
   sur NULL (`:2022`) le fait tomber. Une ROM de moins de 1 Mio laisse une partie du tampon non initialisée
   (`:2021-2022`) : du non-déterminisme.
4. **La réverbération de l'EMU8000 déborde ses tampons** (`sound_emu8k.c:1162-1174`). Les quartets Eh et Fh
   d'init2 voix 14h (et 16h quand `link_return_type` vaut 1) donnent des tailles de 33 × 242 et 34 × 242, au-delà
   de `MAX_REFL_SIZE` = 7 744 (`sound_emu8k.h:110`). En silence, le peigne s'effondre sans tomber ; avec du
   signal, les écritures parcourent `sb_t` et le tas.
5. **La configuration hors liste** (PB-93 à élargir) : `addr`, `mixaddr` et `emu_addr` près de FFFFh
   (`io.c:44-63`) ; `onboard_ram` négatif, `malloc` nul puis `memset` (`sound_emu8k.c:2046-2047`) ; `irq` ou
   `dma` hors liste (`dma.c:11`).
6. **La SB 16 et l'AWE32 sur un 8088 ou un 8086** : rien ne tombe, mais la machine est inutilisable. Sans
   `dma16_init`, `dma_channel_read(5)` rend `DMA_NODATA` (`dma.c:516-517`) : la sortie 16 bits n'avance jamais,
   sans IRQ.
7. **L'oracle lui-même** : un nom de carte inconnu n'y monte rien, sans un mot (`harness.c:1968-1971`).

**Défauts observables, reproduits** (à numéroter à l'inscription, à partir de PB-145) :
- **Les cartes et les mélangeurs.**
  - La SB 2.0 sans mélangeur rend l'audio CD presque muet : `sb_2_init` réinitialise toujours le CT1335
    (`:952`), qui pose `sound_set_cd_volume(20, 20)` (`:368-369`), soit −70 dB, et l'invité n'a aucun port
    pour le relever. Le reset de chaque mélangeur laisse le CD au minimum : 20/65535 (CT1335), 81/65535
    (CT1345, déjà connu de G10.5), 12/65535 (CT1745).
  - Les valeurs de reset sont changées exprès (`:346-347`, `:413-414`, `:550`).
  - L'enregistrement est toujours muet : `sb_enable_i` n'est jamais écrit (`sound_sb_dsp.h:63` ; lu à
    `sound_sb.c:180` et `:272`). La sélection d'entrée MIDI est mal parenthésée (`:143-148`, `:235-240`).
  - 82h est rendu dans un `uint8_t` (`:770`) : le `| 0x4000` disparaît.
  - 80h et 81h : quand plusieurs bits sont posés, le dernier gagne ; un 0 ne change rien ; la traduction des
    requêtes 16 bits vers le canal 8 bits n'existe pas (`:612-635`, `:728-734`).
  - Le CT1745 : 01h n'est pas tenu (`:540-546`) ; 0Ah vers 3Ah tronqué (`:601`), relu juste pour 0Ah ≤ 51h
    (`:695`) ; la stéréo à la manière de la Pro (0Eh) n'est pas branchée.
  - Le MPU-401 de la SB 16 et de l'AWE32 : fixe en 330h, sans IRQ (`:1066`, `:1091`), UART seul ; FFh rend un
    ACK même en mode UART (`sound_mpu401_uart.c:26`).
  - L'IRQ 7, le DMA 1 et le DMA 16 bits 5 de la SB 16 et de l'AWE32 n'ont pas de clé (TODO `:1059`, `:1084`).
  - Pas de CMS sur la SB 1.0 ; le miroir OPL2 de la 2.0 en 2x0 dépend de l'option globale `gameblaster`
    (`:955`).
- **Le DSP.**
  - Les paramètres périmés de 1Fh, 2Ch, 7Dh et 7Fh : `sb_commands` vaut 0 pour les quatre, qui lisent
    `sb_data[0..1]`, les octets de la commande précédente (`:355`, `:377`, `:431`, `:438`). La SB Pro v2 de G8
    l'atteint déjà.
  - L'octet de mode de B0h-CFh n'est pas masqué (`:484`, `:497`, `:510`, `:523`) : 01h à 03h prennent le chemin
    ADPCM, toute autre valeur hors de 00/10/20/30 cale, sans IRQ.
  - L'AWE32 hérite de `sb_commands[8] = -1` (`:168`, `== SB16`) : 08h rend 18h sur-le-champ, et son paramètre
    est pris pour une commande.
  - La SB 1.0 : D1h et D3h mettent le DMA 8 bits en pause (`:530-531`, `:537-538`).
  - `record_buffer[record_pos_read + 1]` lit l'indice 0xFFFF d'un tableau de 0xFFFF (`:1150`, `:1157`,
    `:1217`, `:1225`), atteignable sur la SB 16 par C8h-CFh et B8h-BFh : en C, `dsp.buffer[0]` (la disposition
    est mesurée, sans bourrage). Reproduit par un alias, sans exception côté C#.
  - Le FIR du DSP 16 bits passe le gain unité au-delà de ~34,7 kHz (Σ|coef| jusqu'à 3,0, `recalc_sb16_filter`,
    `:79-105`) ; un signal pleine échelle déborde alors `(int32_t)` (`sound_sb.c:150-151`, `:242-243`) : INT_MIN
    en C, un claquement.
  - `len * sb_freq` déborde après 40h FFh (`sound_sb.c:202`, `:325`) : comportement indéfini, enveloppé des deux
    côtés.
- **Le 8237 haut** (`dma.c`) : `dma16_command` reste à 0 (`:389-390`) ; `dma_stat_rq` n'est lu que par le PS/2
  (`:198-204`) ; DAh rend le dernier octet écrit ; le masque ne se relit pas.
- **L'EMU8000** (les défauts D1 à D18 de la contre-lecture) :
  - `emu8k_inb` rend `>> 1` au lieu de `>> 8` (`sound_emu8k.c:1408-1409`) ; `emu8k_outb` écrit un mot entier
    (`:1416-1419`) ;
  - WC et les registres courants n'avancent qu'aux écritures (`:342-716`, `:724`) ;
  - l'enveloppe de modulation après le maintien (`:1843-1847`, `:1862`) ; les délais et maintiens consommés
    (`:1757-1879`) ; le relâchement de l'enveloppe de modulation à une autre échelle (`:1069` contre `:1831`) ;
  - le bit plein de la DRAM invisible (`:213`, `:621-636`) ; SMARW non masqué (`:1152`) ;
  - le chorus droit interpole avec la fraction du gauche et lit le tampon gauche aux réglages extrêmes
    (`:1435`, `:1453-1468`) ;
  - le débordement de la sortie en cubique non filtré (`:1734`) ;
  - l'attaque 0 muette (`:1004-1005`) ; les rustines (`:989-994`, `:1326-1331`, `:710-711`) ;
  - le rééchantillonnage de 44,1 à 48 kHz par répétition (`sound_sb.c:226`).

## Les étapes

### G12.0 — Le socle, et les quatre cartes 8 bits : SB 1.0, 1.5, 2.0 et Pro v1  ✅ *fait, VERIFICATION.md § G12.0*

- **Le registre** gagne `sc_sb`, `sc_sb1_5`, `sc_sb2_0` et `sc_sbprov1`, à leur place relative
  (`sound.c:270-291`).
- **`sb_t`** : l'union des trois mélangeurs devient trois champs (décision n° 2).
- **Les transcriptions** : `sb_get_buffer_sb2` et `sb_get_buffer_sb2_mixer`, le CT1335, `sb_1_init`,
  `sb_15_init`, `sb_2_init` et `sb_pro_v1_init`, `sb_config`, `sb2_config` et `sb_pro_v1_config`, les quatre
  devices.
- **`GAMEBLASTER`** vaut 0 ; la clé `gameblaster` est lue, et refusée avec un avertissement si elle vaut 1
  (décision n° 3).
- **L'oracle** :
  - `h_boot` monte les quatre cartes, et un nom inconnu l'arrête bruyamment ;
  - `h_sb_probe` trouve n'importe quelle SB et sonde le mélangeur de sa carte ;
  - la sonde du son gagne le type du DSP, le CT1335 et le volume CD de la carte ; ABI 54.
- **`r9-sbcfg`** couvre les trois configurations neuves.
- **Portes.**
  - `bd-pc-sb1`, `bd-pc-sb15`, `bd-pc-sb20` et `bd-pc-sbpro1` : le 5150 et la carte jusqu'au DOS.
  - `bd-ami486-sb20-mix` (le mélangeur en 250h) et `bd-ami486-sbpro1`, sous `--expect-sb`.
  - SBBANC paramétré par carte (`sbbanc.py --carte`), sur le 5150 : la version, les commandes que garde la
    version, la pause de D1h sur la 1.0, le CT1335 (dont l'index 01h, relu FFh), la double OPL2 de la Pro v1 et
    388h.
- **Contrôles négatifs** : le reset du CT1335 sans `sound_set_cd_volume` ; une garde de version déplacée ; un
  octet du mélangeur changé côté C# seul (la sonde mord).

### G12.1 — La Sound Blaster 16  ✅ *fait, VERIFICATION.md § G12.1*

- **La règle ISA 16 bits pour le son** : `pc.check_sndcard`, appelé par `check_cpu` à côté de
  `check_hdd_controller`, sur une table hôte (`Host/SoundCards.cs`, `RequiresAtMachine`) (décision n° 4).
- **Le DSP 16 bits** : le DMA 16 bits, les commandes, `recalc_sb16_filter`, `sb16_copyright` ; `sb_enable_i`
  rétabli (toujours nul) ; la garde de la fréquence 0 (décision n° 10) ; l'alias de `record_buffer`
  (décision n° 11).
- **`sound_sb.c`** : `sb_get_buffer_sb16`, avec l'aide de conversion du C (décision n° 12) ; le CT1745 et sa
  borne de 3Bh (décision n° 9) ; `sb_16_init`, `sb_16_config`, `sb_16_device`. `filters.h` : les quatre IIR et
  le FIR, leur état statique jamais remis à zéro (décision n° 14).
- **Le MPU-401** (`Sound/sound_mpu401_uart.cs`) et `midi_write`, une empreinte des deux côtés (décision n° 8).
- **L'oracle** :
  - le Makefile lie `sound_mpu401_uart.c` ; la souche de `mpu401_uart_init` part ;
  - `h_boot` monte la SB 16 ;
  - la sonde du son gagne le DSP 16 bits, le CT1745 (sans `speaker`), le MPU et l'empreinte MIDI ;
  - `harness_dma.c` inclut `dma.c` et porte `h_dma_probe` (décision n° 17) ;
  - une assertion statique fige la disposition de `record_buffer` et `buffer` ;
  - `h_sb16_filter` pour `sb16-filter-check` ; ABI 55.
- **Portes.**
  - `bd-ami486-sb16`, sous `--expect-sb 220,7,1,5` ; `bd-ibmxt-sb16-refus`.
  - `sb16-filter-check` : les 51 coefficients comparés bit à bit, pour 1 à 65 535 Hz et les 256 constantes
    de 40h.
  - SB16BANC (GNU as, saisi dans DEBUG sur l'ami486, DOS 5 amorcé du disque SCSI de G11 : 3 000 tranches, là où le
    disque IDE de l'utilisateur en demande 60 000) : E1h, E3h, les commandes de l'ASP, 41h/42h, B0h-CFh dans
    tous leurs modes, D5h/D6h/D9h/DAh, l'acquit en 2xFh, 80h/81h/82h, le 8237 haut en incrément et en
    décrément, simple et automatique, à la frontière des 128 Ko, l'enregistrement stéréo au-delà de FFFEh,
    3Bh = C0h, le MPU (FFh, 3Fh, des notes), et une tonalité adverse : 41h FFFFh, une salve pleine échelle aux
    signes alternés.
  - `r9-sb16`, en C# seul : 3Bh, la fréquence 0 (41h 0000h puis un DMA automatique), le refus sur un 8088.
- **Contrôles négatifs** : l'aide de conversion remplacée par le transtypage .NET ; l'alias rendu nul ; un ulp
  sur le gain du FIR ; `ac + 1` au lieu de `ac + 2` dans le 8237 haut ; la sonde mord.

### G12.2 — L'AWE32 et l'EMU8000  ✅ *fait, VERIFICATION.md § G12.2*

- **`Sound/sound_emu8k.cs`** : les régions vivantes de `sound_emu8k.c` et `.h`, les unions en structures à
  disposition explicite, la mémoire en un seul tableau (décision n° 15). L'aide de conversion aux six sites de
  la réverbération, l'alias du chorus droit, les tampons de réverbération de 8 228 entrées et leurs gardes
  R9, la borne de forme de `:1831`.
- **`sound_sb.c`** : `sb_get_buffer_emu8k`, `sb_awe32_available`, `sb_awe32_init`, `sb_awe32_close`,
  `sb_awe32_config`, `sb_awe32_device`. `check_sndcard` refuse l'AWE32 sans sa ROM, ou avec une ROM qui n'a pas
  1 Mio (décision n° 5).
- **L'oracle** : `harness_emu8k.c` inclut `sound_emu8k.c` (les tables `static`, `random_helper`) ; les souches
  partent ; `h_boot` monte l'AWE32 et la refuse bruyamment sans sa ROM ; `h_emu8k_probe`, `h_emu8k_tables`,
  et les points d'entrée des noyaux ; ABI 56.
- **Portes.**
  - `emu8k-tables-check` : chaque table de `emu8k_init`, bit pour bit, et l'empreinte de la ROM chargée.
  - `emu8k-kernel-check` : les peignes, les diffuseurs, le chorus et le volume, sur des états fabriqués, là où
    aucun invité n'arrive en temps de porte.
  - `bd-ami486-awe32` sous `--expect-emu 620,512` (la carte exigée des deux côtés, l'empreinte de la ROM
    vérifiée) ; `-ram0` et `-ram28` ; `bd-ibmxt-awe32-refus`.
  - AWEBANC : la détection, l'initialisation, le dimensionnement de la DRAM, la ROM, le téléversement, des
    notes, les chorus et réverbérations documentés, un chorus extrême.
  - `r9-emu8k` et `r9-awecfg`, en C# seul.
- **Contrôles négatifs** : `>> 8` dans `emu8k_inb` ; la conversion .NET dans un peigne ; une voix permutée dans
  la sonde.

### G12.3 — La clôture : les machines et les témoins ; G12 fait  ✅ *fait, VERIFICATION.md § G12.3*

- **Les profils** : `ixtal26-486-sb16.cfg` et `ixtal26-486-awe32.cfg`, et leurs lignes dans
  `launchSettings.json`.
- **L'écran de construction** propose la carte son, filtrée par `Host/SoundCards` (décision n° 18) ;
  `--setup-check` contrôle que la SB 16 et l'AWE32 sont absentes des machines sans MODEL_AT, et l'AWE32
  absente sans sa ROM.
- **Les témoins**, sous `--boot` et sous boot-diff :
  - TEST-SBP.EXE (la disquette 1 de la SB Pro v2) sur la Pro v2, la Pro v1, la 16 et l'AWE32 ;
  - Windows 3.11 et ses pilotes (SNDBLST.DRV, SNDBLST2.DRV) sur la 1.0, la 2.0 et la 16 ;
  - les voix numérisées de Chuck Yeager's Air Combat (`os/yeager.img`) ;
  - le comportement surprenant rejoué sous boot-diff : la SB 2.0 sans mélangeur et son CD muet, la pause de
    D1h sur la 1.0.

  *(À l'exécution : TEST-SBP devient quatre portes de la série, `bd-ami486-*-testsbp`. Sur la 16 et l'AWE32, il
  s'arrête après la détection sur « Error code: 0100 », des deux côtés. Windows est mené en C# seul, comme en G8.
  Yeager s'arrête sur sa question de sécurité, tirée d'un manuel que nous n'avons pas. Le comportement
  surprenant est déjà rejoué à chaque série par deux portes de G12.0, `bd-pc-sb20` et `bd-pc-sb1-banc`.)*
- **Les originaux intacts** : `os.sha256`, `g5w.sha256` (ajout seulement : `sbpro2-1.img`).
- **La série complète.**

## La vérification

- Chaque porte avec une carte : la sonde du son identique et son empreinte sortie de la graine (deux
  silences ne prouvent rien), `h_n_fatal == 0`, `R9.Atteints` vide.
- Une série complète par étape qui change l'émulateur (G12.0, G12.1, G12.2), sous `PORTES=` ciblé sinon ;
  une série complète finale pour le bilan.
- `check-oracle` sans dérive, et `make -C tools/oracle selftest`.
- Les contrôles négatifs de chaque étape, chacun construit à part et chacun rouge.

## Les décisions

1. **La portée.** Les six cartes ISA. La SB MCV et la SB Pro MCV (DEVICE_MCA) et l'AdLib Gold restent exclues,
   comme le CMS (`sound_cms.c`). NukedOPL est ramené au DBOPL avec un avertissement, comme en G8.
   *(validé sous mandat, 06/10)*
2. **L'union des mélangeurs** (`sound_sb.h:94-98`) devient trois champs : aucun lecteur ne croise les membres,
   l'Aztech étant exclu (`dsp->parent` ne sert qu'à `azt2316a_enable_wss`, `sound_sb_dsp.c:688-691`).
   *(validé sous mandat, 06/10)*
3. **`GAMEBLASTER` vaut 0.** La clé `gameblaster` (`pc.c:638`) est lue ; à 1, elle est refusée avec un
   avertissement (le CMS n'est pas transcrit), et le miroir OPL2 de la SB 2.0 reste posé. *(validé sous
   mandat, 06/10)*
4. **La règle ISA 16 bits pour le son.** Une table hôte, `Host/SoundCards.cs`, sur le patron de
   `HardDiskControllers` : `RequiresAtMachine` pour sb16 et sbawe32. Les `device_t` restent verbatim (PCem met 0).
   `pc.check_sndcard` juge la machine finale, aux points d'appel de `check_hdd_controller`, avant toute poussée
   vers l'oracle : un avertissement, et aucune carte (garde R9 `sound_sb.c:1349`). C'est une DEVIATION de
   configuration : une vraie SB 16 tolérait un connecteur 8 bits, sans DMA 16 bits ni IRQ haute.
   *(validé sous mandat, 06/10)*
5. **La ROM de l'AWE32.** Absente, ou d'une autre taille que 1 048 576 octets : refus averti, aucune carte
   (garde `sound_emu8k.c:2019`), par `device_available(sb_awe32_device)`, la fonction de PCem
   (`sound_sb.c:1071`), et non un nom recopié. `h_boot` la refuse aussi, bruyamment. *(validé sous mandat,
   06/10)*
6. **L'IRQ et les DMA de la SB 16 et de l'AWE32** restent fixes (7, 1 et 5), sans clé neuve (R1) : l'invité les
   change par 80h et 81h. `--expect-sb` lit l'état final. *(validé sous mandat, 06/10)*
7. **Le MPU-401** fixe en 330h, sans IRQ, en UART seul : reproduit. Un avertissement si l'AHA-1542C est réglée
   en 330h avec une SB 16 ou une AWE32 (les deux partagent les ports, `io.c:106-109`). *(validé sous mandat,
   06/10)*
8. **`midi_write`** : un puits vide, avec une empreinte (le compte et un FNV des octets) des deux côtés ; la
   souche de l'oracle est obligatoire (`-Wl,--no-undefined`). La sortie MIDI vers l'hôte revient à G17.
   *(validé sous mandat, 06/10)*
9. **3Bh** : un indice borné, `pcem bug, not reproduced`, garde R9 `sound_sb.c:655` ; `speaker` hors de la
   sonde. L'oracle survit à la lecture : 3Bh peut entrer dans un banc. *(validé sous mandat, 06/10 ; amendé à
   l'exécution : SANS garde R9, puisque PCem ne s'y arrête pas — une lecture sans lecteur, PB-152, section B. Une
   garde ferait rougir la sonde de l'AHA-1542C, qui n'en admet aucune, sous SB16BANC)*
10. **La fréquence 0** de 41h/42h est ramenée à 1 Hz, en C# seul (DEVIATION, garde R9
    `sound_sb_dsp.c:393`). Le reste de la commande est gardé : `sb_timeo` = 257, aucune division par zéro dans
    `sb_dsp_speed_changed`, des coefficients finis. Aucune porte comparée n'envoie 0. *(validé sous mandat,
    06/10)*
11. **`record_buffer[0xFFFF]`** : l'alias `dsp.buffer[0]` reproduit (`pcem bug, reproduced`), par une aide
    bornée ; une assertion statique de l'oracle fige la disposition. *(validé sous mandat, 06/10)*
12. **Les conversions vers entier.** L'aide qui rend la sémantique de `cvttss2si` (INT_MIN hors bornes et pour
    NaN) est obligatoire à `sound_sb.c:150-151` et `:242-243` et aux six sites de la réverbération ; ailleurs,
    la plage est garantie. Les entiers débordent comme en C (`CheckForOverflowUnderflow` faux). *(validé sous
    mandat, 06/10)*
13. **La libm.** Mesurée par `sb16-filter-check` et `emu8k-tables-check`. En cas d'écart, la règle de G8
    (décision n° 3) : un P/Invoke de la libm de l'oracle, ou une table figée, dits en DEVIATION. *(validé sous
    mandat, 06/10)*
14. **Les états statiques** des filtres (`filters.h`), de `random_helper`, `dmareadbit` et `dmawritebit`
    (`sound_emu8k.c:99-101`) ne sont jamais remis à zéro, comme `sb_iir` (PLAN-G8, décision n° 4). *(validé sous
    mandat, 06/10)*
15. **L'EMU8000.** Les unions en structures à disposition explicite ; la ROM, le bloc vide et la RAM en un seul
    tableau ; `emu8k_t` alloué pour l'AWE32 seulement. Le chorus droit à indice négatif est reproduit par un
    tableau commun (la contiguïté est mesurée). Le débordement de la réverbération ne l'est pas : des tampons
    de 34 × 242 = 8 228 entrées, qui tiennent le délai demandé, et des gardes R9 aux quatre affectations. La
    lecture hors table de `:1831`, écrasée aussitôt (`:1834`), est bornée sans garde. *(validé sous mandat,
    06/10)*
16. **La sonde du son** est généralisée à toutes les SB ; l'EMU8000 a la sienne. *(validé sous mandat, 06/10)*
17. **La sonde du DMA** (`harness_dma.c`) : le DMA 16 bits passe sous l'oracle à G12.1. *(validé sous mandat,
    06/10)*
18. **L'écran de construction** propose la carte son, filtrée par le même registre que `check_sndcard`.
    *(validé sous mandat, 06/10)*
19. **Les profils** : `ixtal26-486-sb16.cfg`, `ixtal26-486-awe32.cfg` (l'AWE32 à 512 Ko, le défaut).
    *(validé sous mandat, 06/10)*
20. **Les numéros** : PB-145 et suivants ; PB-92 et PB-93 élargis. *(validé sous mandat, 06/10)*
21. **Aucun logiciel SB 16 ni AWE32** sur les disques de l'utilisateur : l'EMU8000 n'a pour preuves qu'AWEBANC
    et les deux contrôles de l'oracle, limite dite (comme le VESA de l'ET4000 en G9), et signalée au bilan.
    *(validé sous mandat, 06/10)*
22. **Ce fichier** est commité avec G12.0. *(validé sous mandat, 06/10)*

## Les risques

- **La libm.** Les coefficients du FIR viennent de `sin` et `cos`, les tables de l'EMU8000 d'`exp2`, `pow`,
  `log10`, `log2`, `exp` et `sin`. `exp2` n'a pas d'équivalent direct en .NET.
- **La taille.** L'EMU8000 est le fichier le plus dense du bloc : deux enveloppes à six états, deux LFO, un
  filtre à quatre pôles, un interpolateur cubique, le chorus et la réverbération.
- **Le temps.** Les échantillons sont produits paresseusement : le moindre écart de minuterie déplace
  l'empreinte sans changer une instruction. 40h FFh fait rappeler `pollsb` à 1 MHz : coûteux, mais fini.
- **La mémoire.** L'AWE32 à 28 Mo en alloue autant de chaque côté.
- **Les témoins.** Ni pilote Creative, ni AWEUTIL, ni banque de sons : le DSP 4.xx et l'EMU8000 n'ont pour
  témoins que nos bancs.
