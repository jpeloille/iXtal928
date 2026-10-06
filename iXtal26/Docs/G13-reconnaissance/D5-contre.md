# D5 — Contre-lecture : le son (G13)

> Contre-lecture adverse de `D5-son.md`, le 6 octobre 2026, au commit `bc609ce`. LECTURE SEULE : rien n'a été construit,
> lancé ni écrit dans le dépôt ; les sources externes ont été copiées hors du dépôt, dans le répertoire de travail.
> Sources relues, en plus de celles du rapport :
> - le guide de Creative lui-même (même URL que [CL], 141 p., créé le 23/10/1996), page à page ;
> - [EMU] (26 p.), [MPU] (84 p.), [8254], [3039], aux URL du rapport ;
> - `github.com/schlae/sb-firmware`, commit `a455808` (13/11/2020) ; `github.com/S95Sedan/CT1741_DSP`, commit `c64cff9`
>   (29/09/2026) ; les octets des images ont été relus, pas seulement les `.asm` ;
> - MAME master : `src/devices/sound/ct1741.cpp`, `src/devices/bus/isa/sb16.cpp` ;
> - DOSBox-X master `src/hardware/sblaster.cpp` ; 86Box master `src/sound/snd_sb.c`, `snd_emu8k.c` ;
>   Linux master `sound/isa/sb/sb16_csp.c` (tous lus le 06/10/2026) ;
> - IBM, *Technical Reference, Personal Computer AT* (mars 1984), copie texte de `g13/src/` ;
> - DOS Days, page CT1330 ; stason.org (TULARC), fiche CT1330 ; Wikipédia, « Sound Blaster ».
>
> Niveaux, comme le rapport : **documenté**, **déduit**, **inconnu**. « Vérifié au binaire » : l'instruction ou la
> donnée citée a été retrouvée dans l'image du micrologiciel (`.bin`), à l'adresse donnée, et pas seulement dans le
> `.asm` commenté. Les lignes de C se rapportent à `pcem-dev/` vendoré.

---

## 0. Verdict

- **Les trois renversements tiennent** : PB-147 (la pause est écrite par Creative), PB-145 (le CD au minimum est la
  valeur documentée), PB-165 (08h sans paramètre, vérifié au binaire sur 4.04, 4.05, 4.11, 4.12, 4.13 et 4.16).
- **PB-92** : la conclusion est plausible, mais la source citée (DOS Days) affirme le contraire ; à refonder.
- **SBBANC et PB-146** : exact, le scénario ne distingue pas.
- **Constats neufs** : N1, N2, N3, N5, N6, N7, N9 et N10 tiennent. **N4 est faux pour le reset**, **N8 est à
  réécrire** (le micrologiciel quantifie, il ne fait pas que borner), N11 est à préciser, **N12 est sans fondement**.
- **Manques importants** :
  - le reset ordinaire du DSP remet ses valeurs par défaut : bloc 07FFh, constante 9Ch, sortie coupée ;
  - la MIDI de la SB (38h, 34h-37h) n'existe pas chez PCem, et les octets MIDI y sont exécutés comme commandes ;
  - la grande vitesse accepte des commandes chez PCem ;
  - le MPU : 3Fh acquittée en UART, et une logique d'IRQ dormante inverse du document ;
  - les micrologiciels ont leurs propres défauts (40h C8h et la retenue de 41h en 4.04-4.12, la saturation de
    l'ADPCM 2 bits jusqu'en 4.13) : une question de principe manque.
- **Vérifications à refaire** : l'attendu de PB-146, le WC d'AWEBANC, la relecture de HWCF3 (illisible sur le vrai
  EMU8000), les valeurs de N8.

---

## 1. Confirmé

**C1 — PB-147 : la pause est documentée ; le défaut résiduel est la coupure.**
[CL] p. 6-25 (PDF 110) et p. 6-26 (PDF 111), mot pour mot : « On version 1.xx, the DSP will pause the DMA transfer
after executing this command », pour D1h et pour D3h ; « Available » coche 1.xx. D1h « refers to the connection of the
digitized sound output to the amplifier input » (p. 6-25). Le 2.02 et le 3.02 agissent sur la broche de coupure
(`sbv202.asm:1827-1866` ; `v302_4k_4701c5fc.asm:1413-1430`). La note 2 (4.xx sans effet) se vérifie au code : en 4.05,
le drapeau 23h.4 n'est lu que par D8h (`v405-8k_e51aff23.asm:2961`, `:2979`, `:2987`).
*Niveau* : documenté pour la pause et la fonction de D1h/D3h ; déduit pour la coupure sur le 1.05 (image absente).

**C2 — PB-165 renversé, vérifié au binaire.**
Dans chaque image 4.xx d'origine du dépôt S95Sedan (4.04, 4.05, 4.11, 4.12, 4.13 en 6 et 8 Ko, 4.16 en 6 et 8 Ko),
l'entrée 08h de la table du groupe 0 pointe sur `78 82 E2 12 hh ll 02 …` (`mov r0,#82h ; movx a,@r0 ;
lcall dsp_data_write ; ljmp …`), sans `lcall dsp_data_read`. En 4.05 : table en 0659h, routine en 06C5h ;
`dsp_data_read` en 1164h (`30 91 FD …`), `dsp_data_write` en 116Dh (`20 90 FD …`), octets relus.
En `.asm` : `v405-8k_e51aff23.asm:1326-1330`, `v413-8k_e22e9001.asm:1503-1507`.
Corroboration secondaire : Linux envoie 08h 03h, lit un octet, puis réinitialise le DSP « after getversion! »
(`sb16_csp.c:562-563`, `:573-581`), ce que la lecture du micrologiciel explique : 03h est exécutée comme commande
(lecture du port 80h du bus X) et laisse un octet de trop.
Donc l'AWE32 de PCem est juste sur le nombre de paramètres ; la SB 16 de PCem est fausse (`sound_sb_dsp.c:168-171`, N7).
*Niveau* : documenté (code machine).

**C3 — PB-145 : les valeurs de reset du CD sont celles du guide.**
- CT1335, 08h : « Default is 0 ⇒ −46 dB » ([CL] p. 4-5, PDF 63) ;
- CT1345, 28h : « Default is 0 ⇒ −46 dB » (p. 4-9, PDF 67) ;
- CT1745, 36h/37h : « Default is 0 ⇒ −62 dB » (p. 4-16, PDF 74) ; 28h : « Default is 0 ⇒ −60 dB » (p. 4-15, PDF 73).

Le CT1335 n'équipe que la « Sound Blaster 2.0 CD Interface card » (p. 4-1, PDF 59 ; p. 4-4, PDF 62). Le chiffre 20
du rapport est juste dans la convention de PCem : 0 dB vaut 32 767 dans les tables (`sound_sb.c:28-35`), d'où
8 230 × 164 / 65 535 ≈ 20,6, soit −58 dB sous le 0 dB du mélangeur (16 383).
*Niveau* : documenté.

**C4 — SBBANC ne distingue pas PB-146 : exact.**
`tools/sbbanc/sbbanc.py:312` envoie `40h A5h`, programme le 8237, puis `48h 7Fh 01h`, puis `7Dh`. 7Dh s'exécute dès
l'octet de commande (`sb_commands[0x7D] = 0`, `sound_sb_dsp.c:45-54`) et lit `sb_data[0..1]` = 7Fh 01h : exactement
`sb_8_autolen` (017Fh). Aucun autre banc ne départage : le script de la Pro v2 joue 75h, pas 7Dh (`sbbanc.py:279`), et
SB16BANC n'envoie ni 1Fh, ni 2Ch, ni 7Dh, ni 7Fh.
*Niveau* : documenté (lecture du code).

**C5 — PB-149 : seuls les bits 4 et 5 de l'octet de mode comptent.**
`cmd_dma8` (4.05) range l'octet en 2Ch, puis ne teste que 2Ch.4 et 2Ch.5 (`v405-8k_e51aff23.asm:1031-1039`) ; de même
`cmd_dma16` pour 2Dh (`:1133-1141`). DOSBox-X ne lit que `& 0x10` et `& 0x20` (`sblaster.cpp:2339-2344`). L'étiquette
`dma8_autoreinit_en` (2Ch.4) désigne en fait le bit « signé » : le rapport l'a vu (risque 3).
*Niveau* : documenté.

**C6 — PB-91 et PB-146 : documentés deux fois.**
- Guide : 16h/17h, « the number of bytes to transfer less 1 » (p. 6-7, PDF 92) ; 1Fh, 2Ch, 7Dh et 7Fh sans
  paramètre (p. 6-9, 6-10, 6-18, 6-19 ; 7Dh et 7Fh renvoient à 1Fh, qui renvoie à 1Ch) ; 48h (p. 6-16, PDF 101) ;
  marche à suivre p. 3-15/3-16 (PDF 44-45).
- 2.02 : décrément du compte et fin du bloc ADPCM 2 bits (`sbv202.asm:343-440`) ; 2Ch sur `dma_blk_len`
  (`:1395-1398`) ; 1Fh, 7Dh, 7Fh sur `dma_blk_len` (`:1612-1616`) ; 4.05 : `dma_dac1_autoinit` (`:2603-2614`).

Le code machine corrobore ; il ne « tranche » pas ce que le guide tranchait déjà (voir K16).
*Niveau* : documenté.

**C7 — N1, l'octet de référence de 1Fh, 7Dh et 7Fh : réel.**
Intitulés « with reference byte » (p. 6-9, 6-18, 6-19 ; liste de la p. 3-15). 2.02 : le bit 0 de la commande choisit
la référence (`sbv202.asm:1659-1672` ; pour 1Fh, `:1560-1590`) ; 4.05 : `dma_dac1_reference` (`:2708-2717`) ;
DOSBox-X pose `haveref` pour 7Dh, 7Fh et 1Fh (`sblaster.cpp:2245-2264`). La référence n'est lue qu'au lancement : à
chaque nouveau bloc, le 2.02 recharge le compte et lit un octet de données (`sbv202.asm:377-385`, X016e).
*Niveau* : documenté.

**C8 — N2, la fin d'un bloc ADPCM : réel.**
Le 2.02 ne lève l'IRQ que lorsque le dernier échantillon du dernier octet est sorti (`r3` revenu à 0 avec un compte
nul : `sbv202.asm:343-440` en 2 bits, `:445-530` en 4 bits). PCem arrête le bloc à la lecture de cet octet, sans le
décoder (`sound_sb_dsp.c:943-944`, `:1036-1044`). La précision apportée à PB-90 en simple cycle (pas de clic, un
octet perdu) est exacte. En automatique, la même avance existe : PCem lève l'IRQ à la lecture du dernier octet du
bloc, le 2.02 après l'avoir joué, au moment où il lit le premier octet du bloc suivant.
*Niveau* : documenté (code machine 2.02) ; déduit pour 1.05, 2.00 et 2.01.

**C9 — N3, `DMA_NODATA` dans l'ADPCM : réel.**
−1 est décodé et le compte décrémenté (`sound_sb_dsp.c:943`, `:984`, `:1019`) ; les chemins PCM sautent le tic
(`:868-872`). Le 2.02 attend l'octet (`X01ac`, `sbv202.asm:424`). Nuance à porter dans le correctif : le vrai DSP
attend DANS sa routine d'interruption, occupé, et ne lit plus aucune commande avant l'octet ou un reset ; « sauter le
tic » comme en PCM est une approximation.
*Niveau* : documenté.

**C10 — N5, N6 et N9 : documentés.**
- N5 : 3Dh par défaut « 0 0 1 0 1 0 1 » = 15h, 3Eh « 0 0 0 1 0 1 1 » = 0Bh ([CL] p. 4-16, PDF 74) ; PCem pose
  55h/2Bh, MIDI compris (`sound_sb.c:565-566` ; `INPUT_MIDI_L` = 64, `includes/private/sound/sound_sb.h:78`).
- N6 : le 0Ah du CT1745 a 3 bits, « 0 to 7 ⇒ −42 dB to 0 dB, in 6 dB steps » (p. 4-15) ; 3Ah a 5 bits en D7:D3
  (fig. 4-3, p. 4-12). Preuve interne en plus : PCem lit lui-même `regs[0x3A] >> 3` (`sound_sb.c:654`), mais écrit
  `0Ah × 3 + 10` sans décalage (`:601`). Réserve : le guide se contredit (« Default is 0 ⇒ −48 dB » à côté de
  « 0 ⇒ −42 dB »), ce qui fragilise la formule déduite au bas de l'échelle ; le classement (b) est donc juste.
- N9 : CT1335 général et MIDI 4 ⇒ −11 dB, voix 0 ⇒ −46 dB (p. 4-5) ; CT1345 voix, général et MIDI 4 ⇒ −11 dB
  (p. 4-9) ; CT1745 30h-35h 24 ⇒ −14 dB (p. 4-15) ; contre les « changed default » de PCem (`sound_sb.c:346-347`,
  `:413-414`, `:550`).

*Niveau* : documenté.

**C11 — PB-153 : les points (a) et (c) sont bien classés.**
82h bits 0-2 (p. 2-5, PDF 27), traduction 16→8 (p. 2-7, PDF 29 ; [3039], « Use Low »), sélection additive (fig. 4-5,
p. 4-14), 0Eh absent du CT1745 (p. 4-11 ; fig. 4-3) : documentés. Les bits hauts de 82h divergent bien d'une source
à l'autre : 86Box 10h/20h/80h selon 4.04, 4.05 ou 4.11 et plus (`snd_sb.c:2347-2365`), DOSBox-X 20h
(`sblaster.cpp:3162-3165`), MAME 80h (`sb16.cpp:67-71`). 80h à plusieurs bits ou à 0 : DOSBox-X fait gagner le plus bas,
0 = aucune IRQ (`sblaster.cpp:2921-2939`), PCem le dernier. *Niveau* : documenté pour (a), inconnu pour (c).

**C12 — PB-154 : les citations sont exactes.**
IRQ partagée et acquittement par 3x0h (p. 2-5) ; IRQ au passage en UART (p. 5-9, PDF 84) et à l'arrivée d'un octet
MIDI (p. 5-10, PDF 85) ; 300h ou 330h, 330h d'usine (p. 5-5, PDF 80 ; p. A-10, PDF 124) ; Roland : pas d'ACK pour FFh
qui quitte l'UART ([MPU] PDF 17, folio 14 ; PDF 25, folio 22, § 5.3) ; DSR* « usually connected to the HOST's
INTERRUPT input » (PDF 10, folio 7). *Niveau* : documenté (voir K10 pour ce qui manque).

**C13 — EMU8000 : les citations sont exactes.**
p. 7 (pas d'accès par octet ; octet haut du pointeur « random (actually a VLSI test register) ») ; p. 11-12 (MT et
FULL au bit 31, « bits 30-24 are zero on read ») ; p. 14 (WC « continuously incrementing at the sample rate »,
1,486 s par tour) ; p. 16 (DCYSUSV bit 7) ; p. 17 (attaque 0 « never attack ») ; p. 21 (HWCF3 « cleared on reset »,
0004h en fin d'initialisation). 86Box garde le `>> 1` (`snd_emu8k.c:1533-1540`) : ce n'est pas une source
indépendante, comme dit. *Niveau* : documenté.

**C14 — N10 et N11 : réels, dans leur cas principal.**
- N10 : la lecture de 2xEh efface les deux drapeaux et la ligne (`sound_sb_dsp.c:797-799`).
- N11 : [CL] p. 6-8 (1Ch : « The DSP will, at the end of the current block transfer, exit auto-init mode and process
  the new DMA mode I/O command ») et p. 3-16 (2b) ; 4.05 : Cxh simple cycle pendant un automatique 8 bits
  (`v405-8k_e51aff23.asm:1003-1012`), 14h/16h/17h aussi (`dma_dac1_normal`, `:2623-2634`) ; 2.02 : 14h
  (`sbv202.asm:1519-1530`), 74h (`:1618-1631`). PCem relance sur-le-champ (`sb_start_dma`, `sound_sb_dsp.c:201-230`).

*Niveau* : documenté (voir K5 et K14 pour les précisions).

**C15 — Les mesures annexes du rapport.**
- R2, par la commande de TRANSCRIPTION.md:39 : `sound_mpu401_uart.cs` 65 / 54 = 1,20 ; `sound_sb_dsp.cs` 0,88 ;
  `sound_sb.cs` 0,98 ; `sound_emu8k.cs` 0,98 ; `pit.cs` 1,06 — ceux du rapport à l'arrondi près.
- Les sondes : 62 champs (9 + 2 × 6 + 41, `tools/oracle/harness.c:2553-2559`, `harness.h:576`), DMA 29
  (`harness.h:755`), EMU8000 72 (`harness.h:761`). `sbdat2` est le champ 9 de la partie SB, `muted` est dans le
  champ 3 (`h_sb_probe`).
- PB-21 : la convention du compte 0 est bien en [8254] PDF p. 17.
- Les marqueurs manquants du § 6 (B8h, C8h, `sb_doreset`, les appels de `record_lu`) se vérifient
  (`sound_sb_dsp.cs:283-301`, `:628-661`, `:1251`, `:1258`, `:1301`, `:1309`).

*Niveau* : documenté (mesure et lecture).

---

## 2. Corrigé

**K1 — PB-92 : la source dit le contraire.**
DOS Days, page CT1330 : « The Pro is still an 8-bit ISA card, as all the previous Sound Blaster cards are, even though
at first glance it looks like a 16-bit card because of the 'AT' section on the connector, but note that these are not
wired to anything. » Le rapport lui prête l'inverse (« ne sert qu'à l'IRQ 10 (et au DMA 0) »). [3039] est une fiche
générique des cartes SB 16 et AWE (DMA haut, CSP, MPU, SCSI) : elle ne dit rien de la CT1330.
Le fondement juste :
- le brochage : IRQ10 en D3, DACK0 et DRQ0 en D8 et D9, sur la seule rallonge 16 bits (IBM, *Technical Reference PC
  AT*, p. 1-21) ;
- les cavaliers de la CT1330 proposent IRQ 10 et DMA 0, bus « 8-bit ISA » (stason.org, fiche TULARC, secondaire) ;
- Wikipédia : « It uses the 16-bit extension to the ISA bus to provide the user with an additional choice for an IRQ
  (10) and DMA (0) » (tertiaire).

Si DOS Days avait raison, l'IRQ 10 ne marcherait sur aucune machine, et c'est PCem sur AT qui serait faux.
*Niveau* : déduit, sources secondaires contradictoires. Classement (d) provisoire ; part (c) à lever par un relevé du
connecteur d'une CT1330.

**K2 — N4 est faux pour le reset.**
Le reset (2x6h) remet le microcontrôleur à zéro : le micrologiciel n'a aucune broche « reset » à lire, et distingue
le démarrage à froid du démarrage « chaud » par une signature en RAM (34h/12h), qu'il ne pose qu'en grande vitesse ou
en mode MIDI.
- 2.02 : `sbv202.asm:855-907` ; signature posée `:1227-1228` (MIDI), `:1496-1497` (grande vitesse) ; effacée
  `:160-161`, `:241-242`, `:1283-1284`.
- 3.02 : `v302_4k_4701c5fc.asm:561-616`.
- 4.05 : `v405-8k_e51aff23.asm:781-851` ; signature `:2125-2126`, `:2298-2299`, `:2560-2561` ; `status_reg` mis à 0
  sur le chemin à froid, vérifié au binaire (`75 23 00` en 043Bh).
- MAME modélise ce reset comme une impulsion sur la ligne RESET du 80C52 (`ct1741.cpp:547-553`).
- Le guide : le reset « returns it to its default state » ([CL] p. 2-2, PDF 24).

Un reset ordinaire passe donc par le chemin à froid : drapeau du haut-parleur effacé et, sur 2.02 et 3.02, broche de
coupure (P2.0) laissée à 1 par le reset du 8051 : sortie coupée. (« The port pins will be driven to their reset
condition », fiche Intel 8XC5X jointe au dépôt S95Sedan, `documents/intel_80c52_datasheet.pdf` p. 4 ; la valeur 1 est
la convention MCS-51 : déduit.) Seul un reset reçu en grande vitesse ou en mode MIDI UART restaure l'état antérieur
(p. 6-12, 6-20 ; `sbv202.asm:873-887`). Secondaire mesuré : DOSBox-X, « Real SBPro2 has it disabled » à
l'initialisation (`sblaster.cpp:4431-4433`).
Chez PCem, `sb_dsp_reset` (`sound_sb_dsp.c:123-157`) ne touche pas `muted` : D8h annonce « éteint » pendant que la
sortie joue.
N4 corrigé : « coupé au démarrage ET à chaque reset ordinaire ; restauré seulement par un reset en grande vitesse ou en
mode MIDI ». *Niveau* : documenté (guide, micrologiciels 2.02, 3.02 et 4.05) ; déduit pour 1.05.

**K3 — N12 est sans fondement : les décodeurs sont équivalents.**
Le décodeur 4 bits du 2.02 calcule `delta = n × r5 + r5/2`, `r5` ∈ {1, 2, 4, 8} (`sbv202.asm:2075-2150`) :
r5 = 1 donne 0..7 ; 2 donne 1, 3, …, 15 ; 4 donne 2, 6, …, 30 ; 8 donne 4, 12, …, 60, soit les quatre rangées de
`scaleMap4` (`sound_sb_dsp.c:59-62`). L'adaptation (n = 0 : r5/2, plancher 1 ; n ≥ 5 : r5 × 2, plafond 8) est
`adjustMap4`. « Accumulateur à 1 » = rangée 0 = `sbstep = 0`. N12 est à retirer. Les vrais écarts du micrologiciel
sont ailleurs (A3).
*Niveau* : documenté (code machine et tables).

**K4 — N8 est à réécrire : le micrologiciel quantifie.**
- La routine `X09a7` (`v405-8k_e51aff23.asm:1793-1856`) convertit toute fréquence de 41h/42h en un registre de
  8 bits, `v ≈ 23 × f / 4096` arrondi : des pas d'environ 178 Hz, pas une fréquence libre.
- Les seuils ne portent que sur l'octet fort : ≥ B1h donne FFh, < 13h donne 1Ch (vérifié au binaire de 4.04 à 4.13 :
  `E5 14 B4 B1 05 74 FF …`, `94 13 50 05 74 1C`). 1Ch n'est pas « le minimum » : de 1300h à 13FFh, le calcul donne
  1Bh. Le 4.16 (AWE64, hors PCem) traite autrement les fréquences ≥ B100h : il pose le bit 0 du port 18h du bus X au
  lieu de rendre FFh (`v416-8k_b15514ef.asm`, `X09a7`).
- La correspondance registre → Hz n'est pas dans le micrologiciel. « 0FFh = 45.32kHz » est un commentaire du
  désassembleur ; MAME pose 46,61512 MHz / 1024 / 256 ≈ 177,82 Hz par pas (`ct1741.cpp:247-257`) : 22 050 et
  44 100 Hz tombent juste, 8 000 Hz donne 8 002 Hz, FFh 45 344 Hz, 1Ch 4 979 Hz.
- 40h : la constante est bornée à EBh (`v405-8k_e51aff23.asm:1688-1692`), puis traduite par une table de 236 octets
  (`:1754-1784`),
  fausse en 4.04-4.12 (A3).

La vérification « 41h FFFFh → sb_freq = 45 312 ; 41h 0000h → 4 864 » prend les seuils pour des sorties : la remplacer
par « registre FFh », « registre 1Ch », avec leur valeur en Hz marquée secondaire (MAME).
*Niveau* : documenté pour le registre, déduit (MAME) pour les Hz.

**K5 — N11 est à préciser.**
1. « Bxh » est hors sujet pour un automatique 8 bits : `cmd_dma16` ne regarde que l'automatique 16 bits
   (`dma16_mode`, `v405-8k_e51aff23.asm:1105`) ; le même mécanisme existe côté 16 bits.
2. Sur le 2.02, la mise en file vaut aussi pendant un simple cycle en cours (test de `pin_dma_enablel`,
   `sbv202.asm:1520`, `:1619`) ; sur le 4.05, seulement pendant un automatique (`v405-8k_e51aff23.asm:1003`,
   `:2625`).
3. En 4.05, l'octet de mode d'un Cxh mis en file est lu puis jeté (`v405-8k_e51aff23.asm:1005-1006`) : le dernier
   bloc garde l'ancien format.
4. N11 n'est rattaché à aucune étape du § 7.

*Niveau* : documenté (code machine).

**K6 — PB-146 : l'attendu « IRQ après 180h octets » ne vaut que pour PB-146 seul.**
Son-1 regroupe PB-146 et N1 (et N2). Avec la référence, le 2.02 prend la référence, puis 17Fh octets de données, puis
l'octet qui ouvre le bloc suivant, et lève alors l'IRQ : 181h octets pris au 8237 (`sbv202.asm:1659-1672`, `:445-530`,
X01ee). Chaque attendu doit être recalculé pour la combinaison de corrections retenue, et le dire.
*Niveau* : déduit (code machine 2.02).

**K7 — PB-158 : un niveau sous-estimé, une vérification qui ne discrimine pas.**
1. CPF, CVCF et CCCA « courants » sont documentés, pas déduits : CPF « This register is constantly being overwritten
   with new data » ([EMU] p. 8) ; CVCF, même phrase (p. 9) ; CCCA, « current address » (p. 10-11).
2. « AWEBANC § 6 rend deux valeurs différentes en mode matériel » ne tient pas. Les deux lectures de WC sont
   consécutives (`tools/awebanc/awebanc.S:405-410`, deux `call rd`), à moins d'une microseconde ; WC avance toutes les
   22,7 µs : le vrai EMU8000 rendrait le plus souvent deux fois la même valeur. La troisième lecture, après un tic du
   BIOS (environ 55 ms, `:411-414` ; `ticks`, `:844-860`), diffère dans les deux modes, puisque PCem avance WC à
   chaque tampon.

Seul le premier essai du rapport (ΔWC sur t ms cadencées) discrimine, à condition que t reste bien inférieur à la
période d'un tampon de PCem. *Niveau* : documenté (1) ; déduit (2).

**K8 — PB-160 a : la vérification relit un registre que le matériel ne rend pas.**
« Note: Due to a VLSI error, this register will not be correctly read by the processor », pour HWCF1, HWCF2 et HWCF3
([EMU] p. 14 ; p. 21). « hwcf3 relu 0 » n'est pas un observable du vrai matériel : vérifier par la sonde de l'EMU8000
ou par le silence de la sortie. Voir A10 pour la relecture que fait AWEBANC.
*Niveau* : documenté.

**K9 — PB-159 : la durée du drapeau n'est pas connue.**
La visibilité au bit 31 et le masque de SMARW sont documentés (a). « À zéro à la lecture suivante » reprend
l'approximation de PCem, que le rapport dit lui-même inconnue : l'essai ne doit pas la figer. Le guide décrit autre
chose, un mécanisme d'I/O WAIT qui retient l'accès tant que SMLD/SMRD est plein ou vide ([EMU] p. 13-14), que PCem ne
modélise pas. Classement : (a) pour la visibilité et le masque, (c) pour la durée.
*Niveau* : documenté (bit, masque, attente) ; inconnu (durée).

**K10 — PB-154 : « PCem ignore les autres sans ACK : fidèle » est faux pour 3Fh.**
En mode UART, PCem rend encore FEh pour 3Fh (`sound_mpu401_uart.c:36-44`, sans test de `uart_mode`). Documenté :
« Once UART mode is entered, the only command the interface recognizes is Reset » ([CL] p. 5-5) ; [MPU] § 5.3
(PDF 25). Par ailleurs, l'IRQ « levée quand un octet est mis à disposition » n'est écrite par Creative que pour 3Fh
(p. 5-9) et pour la MIDI entrante (p. 5-10) ; pour l'ACK de FFh, elle est déduite de Roland (DSR*).
*Niveau* : documenté (3Fh) ; déduit (IRQ sur l'ACK de FFh).

**K11 — PB-90 : deux sites, pas trois.**
`sound_sb_dsp.cs:1123` porte PB-91, pas PB-90 ; en 2 bits, le `& 3` masque déjà le drapeau (`sound_sb_dsp.c:999`),
comme le dit l'entrée. Aux commandes, ce sont les six lectures de `sbdat2` qui comptent ; celles de `sbref` sont
tronquées à 8 bits. *Niveau* : documenté (lecture du code).

**K12 — PB-21 : l'effet n'est pas « jusqu'à la prochaine écriture au PIT ».**
`l[0]` vaut 0 dès que le BIOS programme le canal 0 à 18,2 Hz (compte 0), et le reste. Chaque écriture au PIT recalcule
`speakval` avec ce 0 (`pit.c:418`) : tant que le canal 0 garde le compte du BIOS, le mode 0/4 du canal 2 rend toujours
INT_MIN, donc le silence (`sound_speaker.c:24`, `:35`). Hérité de l'entrée PB-21 : à corriger aux deux endroits.
*Niveau* : déduit (lecture de `pit.c:405-423` et `sound_speaker.c:17-35`).

**K13 — La valeur des micrologiciels comme source est surestimée sur trois points.**
1. « Réassemblables à l'identique » ne se vérifie dans les dépôts que pour les images 4.xx présentes.
   - Le 2.02 : aucune image dans `schlae/sb-firmware` (deux `.asm` et un README) ; l'identité repose sur
     l'affirmation de son auteur.
   - Le 3.02 : aucune image dans `CT1741_DSP` (le dossier `firmware/` ne contient que des 4.xx).
   - 4.12 : l'image s'appelle `v412-8k_e5d3a248.bin`, mais son CRC32 vaut f69d1672, celui du `.asm`. Le dépôt bouge
     encore (dernier commit le 29/09/2026, « Fix firmware filename for version 4.12 ») : citer avec le commit et le
     CRC32 de l'image.
2. Le dépôt contient aussi des images MODIFIÉES : 4.13 patch3, patch4, patch5, et une « 4.17 » faite maison
   (`firmware/README.md`, `assembly/README.md`). Elles ne doivent jamais servir de référence.
3. Les commentaires sont des interprétations de qualité inégale :
   - `dma8_autoreinit_en` pour le bit « signé » ;
   - en 3.02, `mov tmod,#len_left_hi` (un symbole substitué à la constante 22h) et la signature lue en 2Eh/2Fh alors
     que les équivalences disent 31h/32h ;
   - des « Debug code? » et « Impossible to be called? ».

   Seuls les octets font foi.

Ce qui renforce la provenance, et que le rapport ne dit pas, est en A1. *Niveau* : documenté (empreintes calculées le
06/10, dépôts relus).

**K14 — N10 : un niveau sous-estimé.**
Que 2xEh n'acquitte que l'IRQ 8 bits et SB-MIDI, 2xFh la 16 bits et 3x0h le MPU, est écrit ([CL] p. 2-5, PDF 27).
Seule la ligne partagée (le OU des sources) est déduite. *Niveau* : documenté, sauf la ligne (déduit).

**K15 — PB-148 : la correction oublie des faits documentés.**
- L'enregistrement mono ne prend que le mélangeur d'entrée gauche : « samples will only be taken from the left input
  mixer » ([CL] p. 4-16).
- Le CT1345 a un filtre d'entrée passe-bas à 3,2 ou 8,8 kHz, actif par défaut (0Ch:3 et 0Ch:5, p. 4-8).
- Le CT1745 a la commande automatique de gain du micro active par défaut (43h, p. 4-17).
- A0h/A8h sont réservées au DSP 3.xx (p. 6-22, PDF 107) : la garde de PCem admet la 2.0 (A9).

« Aucune fonction de transfert documentée » est trop fort : les coupures et les gains (p. 4-17) le sont, pas leurs
réponses exactes. *Niveau* : documenté.

**K16 — § 0.2 : le code machine ne « tranche » pas PB-91, PB-146 ni PB-149.**
Le guide les tranchait déjà (C6, C5) ; le micrologiciel corrobore. Il ne tranche seul que PB-165, et en partie N2,
N3, N7, N11, et les ajouts A3, A4, A6. *Niveau* : documenté.

---

## 3. Ajouté

**A1 — Provenance du 4.13 : plus forte que ne le dit le rapport.**
L'image chiffrée `firmware - encrypted/v413-8k_5181892f.bin` (CRC32 5181892f, SHA-1 5b42f1c34c4e…aa77) et sa clé de
64 octets (CRC32 5243d15a) sont identiques aux ROM « ct1741_v413@80c52.bin » et « ct1741_v413_xor.bin » de MAME
(`ct1741.cpp:33-39`). MAME la déchiffre par XOR cyclique et l'exécute sur un 80C52, dans `sb16_lle_device`
(« SoundBlaster 16 Audio Adapter LLE », `sb16.cpp:22`). Deux conséquences :
1. le 4.13 de l'AWE32 de PCem a une provenance indépendante ;
2. MAME est un oracle EXÉCUTABLE du DSP 4.13 (paramètres, IRQ, registre de fréquence), plus sûr qu'une lecture de
   désassemblage ; limites : la puce d'interface y est approchée, et le MPU-401 y est un « dummy » (`sb16.cpp:122`).

Les dumps 4.04, 4.05 et 4.11 ont aussi leur forme chiffrée et leur clé dans le dépôt. Le procédé est celui que décrit
la fiche Intel 87C5X jointe au dépôt (`documents/intel_80c52_datasheet.pdf` p. 21) : la vérification de l'EPROM
interne rend chaque octet combiné (XNOR) avec un « Encryption Array » de 64 octets, et les zones non programmées (FFh)
révèlent ce tableau ; d'où les clés de 64 octets. Le 4.05 de la SB 16 de PCem n'a pas d'équivalent dans MAME.
*Niveau* : documenté (empreintes calculées).

**A2 — La couverture réelle des types de PCem** (`sb_dsp_versions`, `sound_sb_dsp.c:57` ; `ibm.h:354-360`).
- SB 1.0 : DSP 1.05, aucune image.
- SB 1.5 et SB 2.0 : 2.00 et 2.01, aucune image ; 2.02 par analogie.
- Pro v1 : 3.00, aucune image ; 3.02 par analogie, et le 3.02 n'a qu'un `.asm`. Le rapport ne nomme pas ce trou.
- Pro v2 : 3.02, `.asm` sans image.
- SB 16 : 4.05, image.
- AWE32 : 4.13, image et MAME.

*Niveau* : documenté.

**A3 — Le micrologiciel a ses propres défauts.**
Vérifié au binaire :
1. **40h, constante C8h** — en 4.04, 4.05, 4.11 et 4.12, la table porte C8h à l'indice C8h (octets `62 C8 66`), là où
   4.13 et 4.16 portent 64h (`62 64 66`) ; « Table has an error near the bottom » (`v405-8k_e51aff23.asm:1752`). La
   constante C8h (environ 17,9 kHz) donne le registre C8h, environ 35,6 kHz au pas de MAME : le double.
2. **41h et 42h, une retenue perdue** — de 4.04 à 4.12 (`E5 18 34 00 E5 18 13`, « Error, shouldnt be here. »,
   `:1823`) ; corrigé en 4.13 et 4.16 (`E5 18 34 00 13`). Sur les 40 448 valeurs de 1300h à B0FFh, 1 652 donnent un
   registre plus bas de 10h, soit environ 2,85 kHz de moins : 41h 22 990 Hz joue vers 20,1 kHz au lieu de 22,9 kHz.
   Les fréquences usuelles (8 000, 11 025, 16 000, 22 050, 32 000, 44 100) ne sont pas touchées.
3. **ADPCM 2 bits, saturation haute** — en 2.02 et de 4.04 à 4.13, elle charge l'adresse directe FFh (`mov a,0ffh`, un
   registre spécial inexistant, valeur indéfinie) au lieu de la constante FFh (`sbv202.asm:1990-1991`, « BUG: this
   should be #0ffh » ; `v405-8k_e51aff23.asm:3353-3354`, octets `E5 FF`) ; corrigé en 4.16 et par le « patch5 »
   (`74 FF`). PCem sature à FFh (`sound_sb_dsp.c:1006-1011`).

Un mode matériel « fidèle au micrologiciel » de la SB 16 (4.05) jouerait 40h C8h deux fois trop vite et certaines
fréquences de 41h 2,85 kHz trop bas ; « fidèle au guide », non. C'est une question de principe (Q11, en A18).
*Niveau* : documenté (code machine) ; effet en Hz déduit (MAME) ; valeur de 2 bits saturée inconnue.

**A4 — Constat neuf : le reset ordinaire remet les valeurs par défaut du DSP, PCem non.**
Chemin à froid (K2) :
- taille de bloc 07FFh : 2.02 (`sbv202.asm:905-906`), 3.02 (`:607-608`), et toutes les images 4.xx (octets
  `75 0E FF 75 0F 07`, en 0432h pour 4.05) ;
- constante de temps 9Ch, soit 10 kHz : 2.02 (`:899`), 3.02 (`:601`) ;
- haut-parleur éteint et sortie coupée (K2).

PCem : `sb_8_autolen = 0xffff` (`sound_sb_dsp.c:129-130`) ; constante et `muted` conservés. Effets :
- un 1Ch ou un 90h sans 48h préalable lève une IRQ tous les 2 048 octets sur la carte, tous les 65 536 chez PCem ;
- une constante non renvoyée après un reset garde l'ancienne fréquence chez PCem, pas sur la carte.

Inversement, un reset reçu en grande vitesse ou en mode MIDI UART restaure l'état antérieur (p. 6-12, 6-20 ; chemin
« chaud », `sbv202.asm:873-887`) ; PCem fait toujours un reset complet. *Niveau* : documenté ([CL] p. 2-2 et
micrologiciels). Classement (a). Rattachement : PB neuf, avec N4 corrigé.

**A5 — Constat neuf : la MIDI de la SB n'existe pas chez PCem, et les octets MIDI y deviennent des commandes.**
38h (« Send command 38h. Send MIDI data. », [CL] p. 6-14, PDF 99 ; p. 5-3, PDF 78) vaut −1 dans `sb_commands`
(`sound_sb_dsp.c:45-54`) et ne fait rien (`:696`, « TODO: AZTECH MIDI-related? »). L'octet MIDI suivant est donc
exécuté comme une commande :
- 90h (note on) lance une sortie DMA à grande vitesse, sur une 2.0 et plus ;
- 80h (note off) une pause du CNA, qui avalera deux octets ;
- 40h une constante de temps.

34h-37h (UART, p. 6-12 et 6-13, DSP 2.00 et plus) connaissent le même sort : les écritures suivantes restent des
commandes, et la sortie par reset n'existe pas. 30h/31h (entrée) sont sans effet faute de source MIDI. Reproduit en C#
(`sound_sb_dsp.cs:801`, table `:143`). Ni le registre ni le rapport n'en parlent.
*Niveau* : documenté. Classement (a) pour 38h et la sortie UART (le puits `midi_write` existe, décision n° 8 de
G12) ; (c) pour l'entrée. Témoin possible : tout programme réglé sur « Sound Blaster MIDI ».

**A6 — Constat neuf : la grande vitesse accepte des commandes chez PCem.**
Sur 2.01 et 3.xx : « In high-speed mode, the DSP will not accept any other commands. To terminate high-speed mode, send
a DSP reset command » ([CL] p. 6-20, PDF 105). Le 2.02 reste occupé (bit 7 de 2xCh à 1) jusqu'à la fin d'un 91h, ou
jusqu'au reset pour 90h (`sbv202.asm:1482-1506`, boucle X0748). PCem traite 90h comme 1Ch (`sound_sb_dsp.c:448-462`)
et continue d'exécuter les commandes. Le 4.05, lui, implémente 90h-99h et revient à sa boucle de commandes
(`v405-8k_e51aff23.asm:2512-2584`), alors que le guide ne les donne pas pour 4.xx (« Available » : 2.01+ et 3.xx) :
la colonne « Available » n'est pas fiable pour 4.xx, le micrologiciel l'emporte.
*Niveau* : documenté. Classement (a) pour 2.01-3.xx.

**A7 — Constat neuf : la logique d'IRQ du MPU de PCem est l'inverse du document.**
Dormante pour la SB 16 et l'AWE32 (`irq` = −1), elle lève l'IRQ sur FFh et non sur 3Fh, hors Aztech
(`sound_mpu401_uart.c:21-45`) ; Creative : « An interrupt is generated when the interface is set to UART mode »
(p. 5-9). La lecture de 3x0h ne retire rien (`:54-62`). Corriger PB-154 en « passant l'IRQ de la carte » produirait
donc le contraire du document : il faut réécrire la logique, pas seulement brancher la ligne. Le commentaire de PCem
(« the IRQ test in the same driver wants this to raise no interrupts! », `:23-25`) observe un clone Aztech, pas une
carte Creative. *Niveau* : documenté.

**A8 — Piste pour l'ACK de FFh en UART.**
Le 4.05 n'écrit qu'un seul FEh : en entrant en UART (`midi_uart_init`, `v405-8k_e51aff23.asm:2288-2299`, port 2 du
bus X) ; en sortant (broche P1.6 retombée), il n'en écrit aucun (`:2327-2330`). L'ACK de FFh, en UART ou non,
viendrait donc de la puce d'interface, hors du micrologiciel : la part (c) de PB-154 ne se tranche pas au code.
*Niveau* : déduit ; à mesurer (Q7).

**A9 — Des gardes de version contraires au guide, sans effet aujourd'hui.**
- A0h/A8h passent la garde sur la 2.0 (DSP 2.01, `sound_sb_dsp.c:470`) alors qu'elles sont « 3.xx » (p. 6-22) : à
  corriger quand PB-148 les implémentera.
- 48h et D8h sont acceptées sur la 1.0, sans garde, alors que « Available » commence à 2.00 (p. 6-16, 6-28) : ce que
  fait le 1.05 est inconnu.

*Niveau* : documenté (guide) ; inconnu (1.05).

**A10 — Voisin : HWCF1 à HWCF3 sont lisibles chez PCem.**
Sur le vrai EMU8000, « will not be correctly read by the processor » ([EMU] p. 14, p. 21). La valeur lue est
inconnue : (c), ou (d) si l'on juge l'effet nul. AWEBANC s'appuie sur leur relecture (`awebanc.S:77-85`).
*Niveau* : documenté (le défaut de lecture) ; inconnu (la valeur).

**A11 — Voisin (source secondaire mesurée) : les ports du DSP se répètent par paires avant la SB 16.**
2x7h répète 2x6h, 2xBh 2xAh, 2xDh 2xCh, 2xFh 2xEh : « verified on real hardware » sur une SB 2.0 et une SB Pro 3.1
(DOSBox-X, `sblaster.cpp:3204-3211`, `:4454-4466`). PCem installe 2x6h-2x7h et 2xAh-2xFh (`sound_sb_dsp.c:847-848`)
mais ignore les écritures en 2x7h et 2xDh, et traite 2xFh en acquit 16 bits sur toutes les cartes (`:809-813`). Un
`out dx,ax` en 2x6h achève l'impulsion de reset sur la carte, pas chez PCem.
*Niveau* : déduit (secondaire) ; classement (b).

**A12 — Voisin : D1h et D3h prennent du temps.**
« The DSP takes a maximum of 112 milliseconds » et « 220 milliseconds » ([CL] p. 6-25, 6-26). Le 2.02 fait une rampe
du CNA (`sbv202.asm:1827-1866`), pas le 3.02 (`v302_4k_4701c5fc.asm:1413-1430`) ; PCem est instantané.
*Niveau* : documenté. Classement (b), faible enjeu.

**A13 — Voisin : la valeur rendue par 08h.**
DOSBox-X, mesuré sur des cartes : 10h avec l'ASP ; FFh sur une ViBRA 16 PnP sans ASP (`sblaster.cpp:2013-2025`,
`:4418-4420`). PCem rend 18h. La part (c) de N7/PB-165 peut s'affiner en « FFh sans CSP, 10h avec (secondaire) ».
DOSBox-X modélise lui aussi 08h avec un paramètre : il ne départage pas le nombre de paramètres, le micrologiciel si.
*Niveau* : déduit (secondaire mesuré).

**A14 — À dire, hors registre : la SB 1.0 sans C/MS.**
La vraie 1.0 porte ses deux SAA1099 en 2x0h-2x3h (commentaire de `sb_1_init`, `sound_sb.c:868-871` ; [CL] tableau A-2,
p. A-2). iXtal n'a pas de C/MS (décisions n° 1 et n° 3 de G12). PLAN-G12 l'avait relevé (« Pas de CMS sur la SB
1.0 », PLAN-G12.md:92) sans le numéroter. Ce n'est pas un défaut reproduit, mais une limite du mode matériel, à
écrire. *Niveau* : documenté.

**A15 — Pour PB-153 (80h/81h) et Q5 : quelle SB 16 ?**
« Registers 80h and 81h are Read Only for PnP boards » ([CL] p. 2-7) ; certaines SB 16 se règlent par cavaliers, DMA
haut compris (« Use Low », [3039]), d'autres par ces registres. La traduction 16→8 « par 81h » suppose une carte ni PnP
ni à cavalier de DMA haut. MAME code en dur IRQ 5, DMA 1, DMA haut 5 (`sb16.cpp:174`, `:181`) : un indice de plus pour
le réglage d'usine. *Niveau* : documenté (guide) ; inconnu (le modèle de carte que vise PCem).

**A16 — Voisin : l'EMU8000 à la mise sous tension.**
« On power-up, the most EMU8000 registers contain random data » ([EMU] p. 21) ; PCem part de zéros. Le mode matériel
garde le déterminisme, ce qui est un choix à écrire (comme PB-24), pas un défaut.
*Niveau* : documenté.

**A17 — Méthode : citer les octets, pas le commentaire.**
L'erreur de N12 montre qu'une lecture de désassemblage peut se tromper sans faute de l'annotateur. Chaque valeur
attendue tirée d'un micrologiciel devrait citer :
- l'image (nom, CRC32, commit du dépôt) ;
- l'adresse et les octets ;
- puis seulement la ligne du `.asm`.

C'est ce qui a été fait ici pour 08h, la table de 40h, `X09a7`, le reset et l'ADPCM 2 bits.
*Niveau* : déduit (méthode).

**A18 — Ce qui manque au plan (§ 7) et aux questions (§ 9).**
Au plan :
- N11, A4, A5 et A6 n'ont pas d'étape : ils relèvent de Son-1 (le DSP 8 bits) ; A5 touche aussi `midi_write` ;
- une étape 0 « registre », avant tout code :
  - amender PB-92 (sa source), PB-145, 147, 160, 165 et PB-21 (K12) ;
  - inscrire les constats retenus ; retirer N12 ;
- les attendus des essais :
  - à recalculer selon la combinaison de corrections (K6) ;
  - ceux qui ne discriminent pas (K7) ou visent un registre illisible (K8) : à remplacer ;
- une référence externe possible pour le 4.13, MAME (A1), à cadrer : outil tiers, hors dépôt ;
- les témoins : rien n'exerce aujourd'hui la MIDI de la SB ni la grande vitesse.

Aux questions :
- **Q11 — Fidèle au micrologiciel ou au guide ?** Les défauts réels se reproduisent-ils en mode matériel ? A3, le
  « hanging note » que corrigent les patchs de 4.13, les claquements du simple cycle notés « Todo » par S95Sedan
  (`assembly/README.md`). Le choix décide des attendus de N2, N8, de l'ADPCM 2 bits et de 40h C8h.
- **Q12 — Les versions sans image** (1.05, 2.00, 2.01, 3.00) : admet-on l'analogie avec 2.02 et 3.02, ou ces parts
  restent-elles en (c) ?
- **Q13 — MAME comme référence de mesure du 4.13 ?**
- **Q14 — La MIDI de la SB (A5) et la grande vitesse (A6) entrent-elles dans G13 ?**
- **Q15 — La sémantique du reset (A4)** : modéliser le chemin « chaud » oblige à suivre l'état grande vitesse et MIDI
  du DSP.

*Niveau* : déduit (analyse du plan).

---

## 4. Le classement par PB, après contre-lecture

| PB | Rapport | Après contre-lecture | Ce qui change, et pourquoi |
|---|---|---|---|
| 90 | (a) | (a) | deux sites utiles (le 2 bits est masqué) ; marqueurs : 2 et non 3 (K11) |
| 91 | (a) | (a) | inchangé (C6) |
| 92 | (d), fidèle | (d) provisoire, part (c) | source inversée (DOS Days) ; refonder sur le brochage AT (K1) |
| 145 | (a) pour la 2.0 sans mélangeur ; fidèle ailleurs | inchangé | confirmé (C3) ; la 2.0 sans mélangeur : déduit, choix de modèle |
| 146 | (a) | (a) | attendu à recalculer avec N1 et N2 : 181h et non 180h (K6) |
| 147 | (a) coupure, (d) pause | inchangé | pause documentée (C1) ; coupure déduite pour le 1.05 ; voir N4 corrigé (K2) |
| 148 | (b) | (b) | correction à compléter : mélangeur gauche en mono, filtres, CAG, A0h/A8h (K15) |
| 149 | (a) | (a) | confirmé au code (C5) |
| 153 | mixte a/b/c/d | inchangé | + modèle de carte pour 80h/81h (A15) |
| 154 | mixte a/c | mixte a/c | + 3Fh acquittée en UART : (a) (K10) ; logique d'IRQ à réécrire (A7) ; ACK de FFh : piste (A8) |
| 155 | (a) | (a) saturation ; bornage → N8 réécrit | artefact de l'émulateur (Q6) ; N8 quantifie (K4) |
| 158 | mixte a/b/c | inchangé | registres courants documentés (K7) ; vérification AWEBANC à remplacer |
| 159 | (a) | (a) visibilité et masque ; (c) durée | l'essai ne doit pas figer « zéro à la lecture suivante » (K9) |
| 160 | mixte a/b/c/d | inchangé | point a : vérifier sans relire HWCF3 (K8) |
| 165 | (d) côté AWE32 ; N7 (a) ; valeur (c) | confirmé | vérifié au binaire, 4.04 à 4.16 (C2) ; valeur : FFh ou 10h, secondaire (A13) |
| 21 | (b) | (b) | effet permanent tant que `l[0]` = 0 (K12) |
| 151 | (a) | (a) | lecture ET écriture (`record_ecrit`, `sound_sb.cs:493-501`) dans le même anneau |
| 156 | (a) | (a) | inchangé ; inatteignable si N8 est appliqué |
| 162 | (b) | (b) | inchangé |
| 163 | (b) | (b) | inchangé |
| 164 | (d) | (d) | inchangé |
| 167 | (d) | (d) | inchangé |

Comptes après correction : (a) 9 (90, 91, 145 en partie, 146, 147 en partie, 149, 151, 155, 156) ; (b) 4 (21, 148,
162, 163) ; (d) 4 (92 provisoire, 164, 165, 167) ; mixtes 5 (153, 154, 158, 159, 160). Les parts (c) : 92, 153,
154, 158, 159, 160, 165 (valeur).

---

## 5. Les constats neufs qui tiennent

| N | Constat (énoncé corrigé) | Niveau | Source principale | Rattachement |
|---|---|---|---|---|
| N1 | 1Fh, 7Dh, 7Fh lisent un octet de référence au lancement ; PCem non | documenté | [CL] p. 6-9, 6-18, 6-19 ; `sbv202.asm:1659-1672` ; `v405…:2708-2717` | PB-146 élargi |
| N2 | fin d'un bloc ADPCM (simple cycle et automatique) : IRQ après le dernier échantillon joué ; PCem arrête à la lecture du dernier octet | documenté (2.02) | `sbv202.asm:343-530` | PB neuf |
| N3 | `DMA_NODATA` décodé en ADPCM ; le vrai DSP attend, occupé | documenté | `sbv202.asm:424` ; `sound_sb_dsp.c:943`, `:984`, `:1019` | PB neuf |
| N4 | sortie coupée au démarrage ET à chaque reset ordinaire ; restaurée seulement par un reset en grande vitesse ou en MIDI ; PCem : `muted` à 0 au départ et jamais remis | documenté | [CL] p. 2-2 ; `sbv202.asm:855-907` ; `v302…:561-616` ; `v405…:781-851` | PB neuf, avec N13 |
| N5 | CT1745 3Dh/3Eh au reset : 15h/0Bh, MIDI ouvert ; PCem 55h/2Bh | documenté | [CL] p. 4-16 | PB-153 élargi |
| N6 | CT1745 0Ah→3Ah sans `<< 3` ni `& 7` ; PCem relit lui-même `>> 3` | documenté (échelle) ; déduit (formule) | [CL] p. 4-15, fig. 4-3 ; `sound_sb.c:601`, `:654` | PB-153 élargi |
| N7 | SB 16 (4.05) : 08h sans paramètre ; PCem en attend un | documenté (vérifié au binaire) | image 4.05, 06C5h ; `v405…:1326-1330` | PB-165 renversé |
| N8 | 41h/42h quantifiés en un registre de 8 bits (`v ≈ 23 f / 4096`), seuils B1h et 13h sur l'octet fort, 40h bornée à EBh puis table ; PCem : fréquence libre de 1 Hz à 1 MHz | documenté (registre) ; déduit (Hz, MAME) | `v405…:1688-1856` ; `ct1741.cpp:247-257` | PB-150 et PB-155 élargis |
| N9 | volumes de reset des trois mélangeurs relevés par PCem (0 dB au lieu de −11, −14 ou −46 dB) | documenté | [CL] p. 4-5, 4-9, 4-15 | PB neuf |
| N10 | la lecture de 2xEh efface aussi l'IRQ 16 bits et la ligne partagée | documenté (acquits séparés) ; déduit (ligne) | [CL] p. 2-5 ; `sound_sb_dsp.c:797-799` | PB neuf (lié à PB-154) |
| N11 | une commande simple cycle reçue pendant un automatique attend la fin du bloc (2.02 : pendant tout transfert ; 4.05 : pendant un automatique, octet de mode jeté) ; PCem relance sur-le-champ | documenté | [CL] p. 6-8, 3-16 ; `sbv202.asm:1519-1530`, `:1618-1631` ; `v405…:1003-1012`, `:2623-2634` | PB neuf |
| N13 | le reset ordinaire remet bloc 07FFh, constante 9Ch et haut-parleur éteint ; PCem : bloc FFFFh, constante et `muted` gardés | documenté | A4 | PB neuf |
| N14 | 38h et 34h-37h (MIDI de la SB) absents : les octets MIDI sont exécutés comme commandes | documenté | A5 ; [CL] p. 6-12 à 6-14, 5-3 | PB neuf |
| N15 | grande vitesse (2.01-3.xx) : PCem accepte des commandes, et son reset ne restaure rien | documenté | A6 ; [CL] p. 6-20 ; `sbv202.asm:1482-1506` | PB neuf |
| N16 | MPU : 3Fh acquittée en UART ; logique d'IRQ dormante inverse du document | documenté | K10, A7 ; [CL] p. 5-5, 5-9 | PB-154 élargi |
| N17 | gardes de version : A0h/A8h admises sur la 2.0 ; 48h et D8h sur la 1.0 | documenté (guide) ; inconnu (1.05) | A9 | PB-148 et PB neuf |
| N18 | HWCF1 à HWCF3 lisibles chez PCem, illisibles sur l'EMU8000 | documenté ; valeur inconnue | A10 ; [EMU] p. 14, 21 | PB-160 élargi |
| N19 | ports du DSP non répétés par paires avant la SB 16 | déduit (secondaire mesuré) | A11 | PB neuf, (b) |
| N20 | durée de D1h/D3h (112 et 220 ms au plus) non modélisée | documenté | A12 | PB neuf, (b) |

**Retiré** : N12 (décodeurs équivalents, K3).

**Faits du micrologiciel, pas défauts de PCem** (Q11) : 40h C8h et la retenue perdue de 41h/42h (4.04-4.12), la
saturation de l'ADPCM 2 bits (2.02, 4.04-4.13) — A3.

N13 à N20 sont des numéros proposés pour cette contre-lecture ; leur inscription relève de Q4.
