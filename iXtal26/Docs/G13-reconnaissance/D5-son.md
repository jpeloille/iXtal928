# D5 — Le son : les défauts de PCem reproduits, et ce que le mode matériel en ferait (G13)

> Lecture de reconnaissance du 6 octobre 2026, au commit `bc609ce` (arbre de travail : `iXtal26.csproj` modifié,
> `tools/perfbanc/` non commité). LECTURE SEULE : rien n'a été construit, lancé ni mesuré dans le dépôt.
> Lu d'abord : `PLAN.md` § G13, `PLAN-G12.md`, `TRANSCRIPTION.md` § Règles, `PCEM_BUGS.md` (en-tête et les 22 entrées
> du domaine). Les lignes de C se rapportent à `pcem-dev/` tel que vendoré ; les lignes de C# à l'arbre au commit.
>
> **Niveaux** de chaque affirmation sur le vrai matériel : **documenté** (une source primaire le dit : document du
> constructeur, ou le code machine du micrologiciel), **déduit** (raisonnement depuis des sources), **inconnu** (aucune
> source trouvée). **Classement** : (a) corrigeable et vérifiable ; (b) corrigeable, vérification faible ; (c) vrai
> comportement inconnu, à mesurer sur matériel réel, laissé reproduit d'ici là ; (d) à ne pas corriger.
> Le mécanisme de l'interrupteur (gardes, `static readonly`, granularité, oracle intact) est l'objet de D7 ; on n'en
> retient ici que ce qui touche le son.

---

## 0. L'essentiel

1. **22 PB lus** (15 de la section A, 6 de la B, 1 de la C). Classement principal : **(a) 10** (PB-90, 91, 145, 146,
   147, 149, 151, 155, 156, 159) ; **(b) 4** (PB-21, 148, 162, 163) ; **(d) 4** (PB-92, 164, 165, 167) ; **mixtes 4**
   (PB-153, 154, 158, 160), qui mêlent (a), (b), (c) et (d) point par point. Aucun PB n'est (c) en entier ; les parts
   (c) sont dans les quatre mixtes.
2. **Une source primaire neuve, décisive** : les micrologiciels des DSP, désassemblés et réassemblables à l'identique
   (DSP 2.02, commenté par Eric Schlaepfer ; DSP 3.02, 4.04, 4.05, 4.11 à 4.13, 4.16). Le code machine tranche quatre
   questions que le registre laissait ouvertes : la longueur de 1Fh/2Ch/7Dh/7Fh (PB-146), la fin de l'ADPCM 2 bits
   (PB-91), l'octet de mode de Bxh/Cxh (PB-149) et 08h (PB-165).
3. **Trois PB où PCem est fidèle, en tout ou en partie** :
   - **PB-147** : la pause du DMA par D1h/D3h sur un DSP 1.xx est *documentée* par Creative (p. 6-25 et 6-26). Le vrai
     défaut est ailleurs : sur la SB 1.0, D3h ne coupe pas le son.
   - **PB-165** : selon les micrologiciels 4.05 ET 4.13, 08h ne prend AUCUN paramètre. L'AWE32 de PCem est donc juste,
     et c'est la SB 16 de PCem (`sb_commands[8] = 1`) qui se trompe : le défaut est renversé.
   - **PB-145** : le CD au minimum après le reset d'un mélangeur est la valeur *documentée* des trois mélangeurs. Seul
     reste le cas de la SB 2.0 sans mélangeur.
4. **PB-92 est fidèle** (déduit) : l'IRQ 10 de la SB Pro passe par la rallonge AT du connecteur, absente d'un
   emplacement 8 bits ; sur un PC ou un XT, elle n'arrive jamais.
5. **Le scénario PB-146 de SBBANC ne distingue pas les deux comportements** : 48h 7Fh 01h précède immédiatement 7Dh,
   et les « octets périmés » sont justement la taille de bloc (`tools/sbbanc/sbbanc.py:311-312`). Pour vérifier la
   correction, il faut intercaler une commande à paramètre.
6. **Douze constats neufs ou voisins** (§ 6), sourcés, sont à inscrire avant toute correction : l'octet de référence de
   l'ADPCM automatique, la fin d'un bloc ADPCM, l'état du haut-parleur au démarrage, les valeurs de reset des volumes
   relevées par PCem, 3Dh/3Eh, 0Ah→3Ah sans décalage, le bornage des fréquences par le micrologiciel 4.xx, la ligne
   d'IRQ partagée, entre autres.
7. **Chemins chauds** : `pollsb` (PB-90, 91), `sb_poll_i` (PB-151), les boucles de mélange (PB-148, 153, 155, 160),
   `emu8k_update` et les effets de l'EMU8000 (PB-158, 160, 162, 163). Tout le reste est en entrée-sortie ou à
   l'initialisation.
8. **Proposition** : cinq étapes (§ 7), du DSP 8 bits (le mieux documenté, le moins cher) à l'EMU8000 (le moins
   vérifiable). Chacune est vérifiée par des essais en C# seul, aux valeurs de la documentation. Chaque essai cite sa
   page et a son contrôle négatif : en mode PCem, l'essai rougit.

---

## 1. Les sources

### 1.1 Documents des constructeurs (primaires)

| Clé | Document | Où |
|---|---|---|
| **[CL]** | Creative Technology, *Sound Blaster Series Hardware Programming Guide*, PDF du 23/10/1996, 141 p. Les pages sont citées sous la forme « p. 6-25 (PDF 110) » : le folio du document, puis le rang dans le PDF. | https://pdos.csail.mit.edu/6.828/2018/readings/hardware/SoundBlaster.pdf (miroir : https://www.phatcode.net/articles.php?id=243) |
| **[EMU]** | Dave Rossum, E-mu/Creative, *AWE32/EMU8000 Programmer's Guide*, rév. 1.00, 1994-1996 (folios « Page N » = rang PDF) | https://www.dosdays.co.uk/media/creative/emu8kpgm.pdf |
| **[MPU]** | Roland, *MPU-401 Technical Reference Manual*, 84 p. PDF | https://archive.org/details/mpu401technicalreferencemanual ; copie lue : http://archives.oldskool.org/pub/misc/Hardware/Roland/MPU-401%20technical%20reference%20manual.pdf |
| **[8254]** | Intel, *8254 Programmable Interval Timer*, Order No. 231164-005, sept. 1993 | https://www.scs.stanford.edu/10wi-cs140/pintos/specs/8254.pdf |
| **[3039]** | Creative, *Jumper Settings of Sound Blaster Audio Cards*, Solution ID 3039 | https://www.philscomputerlab.com/uploads/3/7/2/3/37231621/jumper_settings_of_sound_blaster_audio_cards.pdf |

### 1.2 Micrologiciels des DSP (primaires par le code ; étiquettes et commentaires de tiers)

| Clé | Contenu | Où |
|---|---|---|
| **[FW202]** | DSP 2.02 (proche des 2.00 et 2.01 de la SB 1.5 et de la SB 2.0), désassemblé et commenté par Eric Schlaepfer (« TubeTime »), 2020. Selon le dépôt, il se réassemble à l'identique avec as31. | https://github.com/schlae/sb-firmware, `sbv202.asm` |
| **[FW302]**, **[FW405]**, **[FW413]** | DSP 3.02 (SB Pro 2), 4.05 (SB 16 de PCem), 4.13 (AWE32 de PCem) ; aussi 4.04, 4.11, 4.12 et 4.16. Selon le dépôt, ils se réassemblent à l'identique. | https://github.com/S95Sedan/CT1741_DSP, `assembly/v302_4k_4701c5fc.asm`, `v405-8k_e51aff23.asm`, `v413-8k_e22e9001.asm` |

Le DSP 1.05 (SB 1.0) n'est dans aucun des deux dépôts : ce qui le concerne s'appuie sur [CL], et sur 2.02 et 3.02
par analogie (déduit).

### 1.3 Sources secondaires (émulateurs, pilotes, sites)

- **DOSBox-X**, `src/hardware/sblaster.cpp` et `mpu401.cpp` (github.com/joncampbell123/dosbox-x, branche master lue le
  06/10/2026). Ses commentaires rapportent des essais sur de vraies cartes (« Real CT1600 notes »).
- **86Box**, `src/sound/snd_sb.c`, `snd_sb_dsp.c`, `snd_mpu401.c` et `snd_emu8k.c` (github.com/86Box/86Box, master,
  06/10/2026). Son EMU8000 dérive de celui de PCem, dont il garde les défauts (`>> 1`, WC figé) : ce n'est pas une
  source indépendante pour l'EMU8000.
- **Linux**, `sound/isa/sb/sb16_csp.c` et `sound/isa/sb/emu8000.c` (torvalds/linux, master).
- DOS Days, *Creative Labs Sound Blaster Pro CT1330 (1991)* :
  https://www.dosdays.co.uk/topics/Manufacturers/creative/ct1330.php ; retronn.de, *Sound Blaster Configuration Guide* :
  https://retronn.de/imports/soundblaster_config_guide.html ; flaterco, *PC audio for luddites* :
  https://flaterco.com/kb/audio/ISA/index.html.

### 1.4 Ce qui n'a pas servi

- **Yamaha YM3812/YMF262** : aucun PB du domaine ne touche l'OPL lui-même. Le DBOPL est un émulateur de DOSBox, et ses
  écarts éventuels n'ont jamais été inscrits.
- **IBM PC Technical Reference** (haut-parleur, 8255 PB0/PB1, 8253 voie 2) : pas relu, page non relevée. PB-21 se
  règle par la convention du compteur à 0 de [8254].
- **La Sound Source, les Covox, l'AdLib, l'audio CD lui-même** : aucun PB de la liste. PB-145 et PB-148 touchent
  l'audio CD par son volume et par l'enregistrement.

---

## 2. Tableau récapitulatif

| PB | Titre court | Effet observable (mode PCem) | Classement | Chemin chaud | Vérification du mode matériel |
|---|---|---|---|---|---|
| 90 | `DMA_OVER` dans l'ADPCM | un saut (clic) au premier échantillon de l'octet du terme de comptage du 8237, quand le flux continue | (a) | oui, `pollsb` | essai d'invariance en C# seul : même flux, terme du 8237 déplacé, mêmes échantillons |
| 91 | ADPCM 2 bits sans fin | 16h/17h jouent sans fin, sans IRQ ; 1Fh ne recharge pas | (a) | oui, `pollsb` | IRQ et fin du DMA après len + 1 octets ; témoin possible : VPLAY d'un VOC 2 bits |
| 92 | IRQ 10 perdue sans second PIC | aucune IRQ sur PC/XT réglé à 10 | (d), fidèle | non | aucune (un avertissement hôte, si décidé) |
| 145 | CD de la SB 2.0 sans mélangeur | −70 dB sur la 2.0 sans CT1335 ; minimum au reset documenté | (a), la 2.0 sans mélangeur seule | non | volume CD 65 535 ; registres de reset aux valeurs de [CL] |
| 146 | 1Fh/2Ch/7Dh/7Fh, paramètres périmés | premier bloc de longueur fausse, IRQ tôt ou tard | (a) | non | IRQ après (48h) + 1 octets, une commande à paramètre intercalée |
| 147 | SB 1.0, D1h/D3h | la pause (fidèle) ; D3h ne coupe pas le son (défaut) | (a) pour la coupure, (d) pour la pause | non | `muted` après D3h ; sortie nulle |
| 148 | enregistrement muet | toute entrée rend 80h | (b) | oui, mélange | FM et CD enregistrés non nuls, micro et ligne à 80h ; témoin VREC |
| 149 | octet de mode de Bxh/Cxh | bits réservés → ADPCM, ou blocage sans IRQ | (a) | non | C0h en mode 01h : un 8 bits normal, IRQ |
| 153 | CT1745 : 80h/81h/82h, 0Ah, 01h, 0Eh, sélection | relectures fausses, pas de traduction 16→8, MPU absent de 82h, IRQ 7 | mixte (a/b/c/d) | la sélection : oui | relectures de [CL] (fig. 4-3, p. 2-5 à 2-7) |
| 154 | MPU-401 : 330h, sans IRQ, ACK de FFh | pas d'IRQ MIDI ; FEh après FFh en UART | mixte (a/c) | non | IRQ après 3Fh, effacée par la lecture de 3x0h ; 82h bit 2 |
| 155 | FIR, conversion → INT_MIN | claquements pleins négatifs au-delà de 34,7 kHz | (a) | oui, mélange | tonalité adverse saturée ; 41h FFFFh → ~45,3 kHz (micrologiciel) |
| 165 | AWE32 : 08h sans paramètre | le paramètre exécuté comme commande | (d) côté AWE32 ; défaut neuf côté SB 16 | non | SB 16 : 08h 00h → un octet, puis 00h exécuté |
| 158 | EMU8000 par octets ; WC figé | octets décalés ; WC figé entre deux écritures | mixte (a/b/c) | WC : oui (`emu8k_update`) | ΔWC ≈ 44 100/s sans écriture |
| 159 | bits plein/vide ; SMARW | drapeau invisible ; SMARW au-delà de 24 bits | (a) | non | bit 15 de A22h après écriture de SMALR ; repli à 24 bits |
| 160 | EMU8000 : enveloppes, rustines, approximations | timbres, redéclenchements, carte non initialisée qui sonne | mixte (a/b/c/d) | oui, `emu8k_update` | essais ciblés en C# seul ; AWEBANC en fumée |
| 21 | `speakval`, l[0] = 0 | INT_MIN, donc silence en mode 0/4 jusqu'à la prochaine écriture au PIT | (b) | non, `pit_write` | `--speaker-check` : compte 0 = 65 536 |
| 151 | `record_buffer[0xFFFF]` | un échantillon de sortie entre dans l'enregistrement | (a) | oui, `sb_poll_i` | SB16BANC § 8 en C# seul : jamais `buffer[0]` |
| 156 | `len × sb_freq` | `record_pos_write` enveloppé (muet tant que PB-148) | (a) | non, par tampon | essai arithmétique |
| 162 | chorus droit | fuite gauche→droite ; fraction du gauche | (b) | oui | rampe linéaire, indices bornés |
| 163 | conversions de la réverbération | INT_MIN au lieu de saturer | (b) | oui | noyaux sur états fabriqués : saturation monotone |
| 164 | attaque de l'enveloppe de modulation | aucun | (d) | — | — |
| 167 | `emu8k_close` | aucun | (d) | — | — |

---

## 3. Section A — ce qui fausse un résultat observable

### PB-90 — Le drapeau `DMA_OVER` entre dans l'échantillon ADPCM

1. **Défaut, effet.** `dma_channel_read` rend `octet | DMA_OVER` (10000h) pour l'octet qui fait passer sous zéro le
   compteur du 8237, et les chemins ADPCM rangent cette valeur entière dans `sbdat2`. `sbdat2 >> 4` (4 bits) ou `>> 5`
   (2,6 bits) sature alors `tempi` à 63 (39), et le premier échantillon décodé de cet octet saute de `scaleMap4[63]` =
   −60 (`scaleMap26[39]` = −35). *Effet* : un clic à chaque terme de comptage du 8237 qui tombe dans un flux ADPCM qui
   continue — en automatique (7Dh, 7Fh), à chaque tour du tampon du 8237 ; en simple cycle, seulement si le terme tombe
   au milieu du bloc.
   Précision relevée en lisant `pollsb` : en simple cycle, quand le compteur du 8237 égale le bloc, l'octet marqué est
   le dernier lu. Le bloc s'arrête dès sa lecture (`sound_sb_dsp.c:1036-1044`), et l'octet n'est jamais décodé : pas de
   clic, mais un octet perdu (§ 6, N2).
2. **Sites.**
   - C : `sound_sb_dsp.c:943`, `:984` et `:1019` (`pollsb`) ; aux commandes, `:342`, `:356`, `:412`, `:423`, `:432` et
     `:439` (le même drapeau si le 8237 finit sur le premier octet) ; `dma.c:551-565` (`temp | DMA_OVER`).
   - C# : `iXtal26/Sound/sound_sb_dsp.cs:1042-1046`, `:1086-1089`, `:1123-1127` (marqueurs PB-90) ; aux commandes,
     `:458`, `:474`, `:545`, `:557`, `:567` et `:575` (sans marqueur).
   - Banc : SBBANC, 75h (`tools/sbbanc/sbbanc.py:315`).
3. **Vrai comportement.** Le DSP reçoit chaque octet de DMA par son registre d'entrée de 8 bits : `movx a,@r0` après
   la requête (`setb/clr pin_drequest`), puis il le décode ([FW202] `vector_dma_dac_adpcm2`, `sbv202.asm:343-440` ; même
   schéma pour l'ADPCM 4 bits, `:445-530`). Le terme de comptage du 8237 ne passe pas par la donnée. *Documenté*
   (micrologiciel). `DMA_OVER` est une convention interne de PCem (`dma.h:9`).
4. **Correction.**
   - **Quoi** : aux trois sites de `pollsb`, garder l'octet seul (`v & 0xFF` quand `v != DMA_NODATA`) ; de même aux six
     lectures des commandes. Sans masque aveugle : −1 (`DMA_NODATA`) deviendrait FFh. Voir N3, au § 6, pour ce que
     devrait faire `DMA_NODATA`.
   - **Taille** : 3 à 9 lignes.
   - **Chemin chaud** : oui, `pollsb` au rythme de l'échantillon, mais seulement en ADPCM ; une comparaison de plus.
   - **Temps visibles** : aucun ; seules les valeurs des échantillons changent.
   - **États sondés** : `sbdat2` (champ 9 de la partie SB de la sonde du son), `sbdatl`/`sbdatr`, `sbref`/`sbstep` et
     `sound_hash`. Aucune porte ne les compare en mode matériel.
5. **Vérification.** Un essai d'invariance en C# seul :
   - le même flux ADPCM est joué deux fois, une fois avec le terme du 8237 sur le dernier octet du bloc automatique,
     une fois au-delà ;
   - en mode matériel, les deux `dsp.buffer` doivent être identiques ; en mode PCem, ils diffèrent : c'est le contrôle
     négatif.

   Le témoin possible est VPLAY, sur un VOC compressé par VEDIT2. Les deux viennent de la disquette SB Pro 2
   (`os/Sound Blaster Pro Ver 2/Disk 1/README.TXT`), qu'il faudrait installer sur une copie (§ 9, Q10).
6. **Classement.** (a).
7. **Dépendances.** PB-91 (même code) ; PB-146 (l'automatique) ; PB-149 (en mode PCem, les modes 01h à 03h de Cxh
   mènent à ce chemin) ; N2 et N3.

### PB-91 — L'ADPCM 2 bits ne finit jamais

1. **Défaut, effet.** La branche `ADPCM_2` lit l'octet suivant sans décrémenter `sb_8_length`. *Effet* : 16h et 17h
   jouent sans fin ce que rend le DMA, sans IRQ de fin, et 1Fh ne recharge jamais ; un programme qui attend l'IRQ
   attend toujours.
2. **Sites.** C : `sound_sb_dsp.c:1016-1020` (à comparer à `:944` et `:985`). C# : `iXtal26/Sound/sound_sb_dsp.cs:1123-1128`
   (marqueur PB-91).
3. **Vrai comportement.** *Documenté* :
   - [FW202], `vector_dma_dac_adpcm2` (`sbv202.asm:343-440`), décrémente le compte à chaque octet lu (`X01a2: dec
     len_left_hi`, `X01a4: dec len_left_lo`) ;
   - quand le compte est nul, il arrête la lecture et lève l'IRQ (`clr pin_irequest` / `setb pin_irequest`, `X0159`),
     ou recharge le bloc de 48h en automatique (`X016e`) ;
   - [CL] p. 6-7 (PDF 92) : 16h et 17h prennent « the number of bytes to transfer less 1 ».
4. **Correction.**
   - **Quoi** : une ligne, `dsp.sb_8_length--;` après la lecture, comme pour les 4 bits et les 2,6 bits.
   - **Chemin chaud** : `pollsb`, coût nul.
   - **Temps visibles** : oui. L'IRQ arrive enfin, et le DMA s'arrête à la fin du bloc.
   - **États sondés** : `sb_8_length`, `sb_8_enable`, `sb_irq8`, et la sonde du DMA (le compteur du canal).
5. **Vérification.** Un essai en C# seul :
   - 16h de longueur N (puis 17h, puis 1Fh après 48h) ;
   - attendus : `sb_irq8` posé, le bit de l'IRQ au registre IRR du PIC, N + 1 octets pris au 8237, et en
     automatique la recharge à `sb_8_autolen` ;
   - en mode PCem, l'IRQ ne vient jamais : contrôle négatif.

   Témoin fort, si la disquette SB Pro 2 est installée : VPLAY d'un VOC 2 bits fige en mode PCem et finit en mode
   matériel.
6. **Classement.** (a).
7. **Dépendances.** PB-90 ; PB-146 (1Fh) ; N2 (le moment de l'IRQ en fin de bloc).

### PB-92 — L'IRQ 10 de la Sound Blaster se perd sur une machine sans second PIC

1. **Défaut, effet.** Les configurations de la SB Pro v1 et v2 proposent l'IRQ 10 sans regarder la machine ; sans `AT`,
   `picint` jette `1 << 10`. *Effet* : sur un PC ou un XT réglé à l'IRQ 10, la carte ne signale jamais rien, sans un
   mot.
2. **Sites.**
   - C : `sound_sb_dsp.c:107-114` (`sb_irq`), `pic.c:298-311` (`num <= 0xff`), `sound_sb.c:1220` (la Pro v1) et
     `:1230-1259` (la Pro v2).
   - C# : `iXtal26/Sound/sound_sb_dsp.cs:234-237` (marqueur PB-92) ; `iXtal26/Sound/sound_sb.cs:1516` (commentaire).
3. **Vrai comportement.** La SB Pro (CT1330) est une carte 8 bits. La partie AT de son connecteur ne sert qu'à l'IRQ 10
   (et au DMA 0) : DOS Days, page CT1330 (source secondaire). [3039] montre le cavalier « IRQ 10 » parmi ceux des
   cartes. Dans un emplacement 8 bits d'XT, ce contact n'existe pas, et l'interruption n'atteint jamais le processeur.
   *Déduit* : l'effet de PCem est le vrai.
4. **Correction.** Aucune pour l'invité. Au plus, côté hôte, un avertissement de configuration dans `pc.check_sndcard`
   (« IRQ 10 sans second PIC : la carte ne signalera rien »), de 5 à 10 lignes. Ni chaud ni sondé.
5. **Vérification.** Si l'avertissement est retenu : un essai en C# seul qui le voit sortir.
6. **Classement.** (d), fidèle. L'entrée PB-92 est à amender : « fidèle au matériel, la configuration seule est en
   cause ».
7. **Dépendances.** La règle ISA 16 bits de G12 (décision n° 4) ; PB-93.

### PB-145 — La SB 2.0 rend l'audio CD presque muet ; le reset d'un mélangeur met le CD au minimum

1. **Défaut, effet.** `sb_2_init` réinitialise le CT1335 même sans `mixaddr`, ce qui pose `sound_set_cd_volume(20, 20)`.
   *Effet* : sur une SB 2.0 sans option CD, l'audio CD est à −70 dB, et l'invité n'a aucun port pour le relever. Avec
   mélangeur, le CD reste au minimum après chaque reset (les trois mélangeurs).
2. **Sites.**
   - C : `sound_sb.c:952` (le reset dans `sb_2_init`), `:341-347` et `:363-369` (CT1335), `:413-416` (CT1345),
     `:557-558` (CT1745) ; `sound.c:265` (65 535 posé par `sound_reset`).
   - C# : `iXtal26/Sound/sound_sb.cs:613-655` (marqueur à `:615`), `:828` (CT1745), `:1184-1185` (`sb_2_init`) ;
     `iXtal26/Sound/sound.cs:179`.
3. **Vrai comportement.**
   - **Le CT1335** n'équipe que la « Sound Blaster 2.0 CD Interface card » ([CL] p. 4-1 (PDF 59), p. 4-4 (PDF 62)) :
     *documenté*. Une SB 2.0 simple n'a pas de mélangeur, donc rien qui règle un volume CD. Comme pour la SB 1.0 et la
     1.5, le CD doit rester à 65 535 : *déduit*, par cohérence avec le modèle de PCem, puisque ce n'est pas une
     grandeur de la carte.
   - **Les valeurs de reset du CD**, toutes au minimum, *documentées* :
     - CT1335, 08h : « Default is 0 ⇒ −46 dB » ([CL] p. 4-5, PDF 63) ;
     - CT1345, 28h : « Default is 0 ⇒ −46 dB » (p. 4-9, PDF 67) ;
     - CT1745, 36h/37h : « Default is 0 ⇒ −62 dB » (p. 4-16, PDF 74), et 28h « 0 ⇒ −60 dB » (p. 4-15, PDF 73).

     **PCem est fidèle sur ce point.**
   - **Voisin, non numéroté** (N9) : PCem relève exprès les volumes de voix, général et MIDI (« changed default… »,
     `sound_sb.c:346-347`, `:413-414`, `:550`). Les valeurs documentées sont :
     - CT1335 : général 4 ⇒ −11 dB, MIDI 4, voix 0 ⇒ −46 dB (p. 4-5) ;
     - CT1345 : voix, général et MIDI 4 ⇒ −11 dB (p. 4-9) ;
     - CT1745 : 30h-35h à 24 ⇒ −14 dB (p. 4-15).
4. **Correction.**
   - **Quoi** : en mode matériel, `sb_2_init` ne remet le CT1335 à zéro que si `mixaddr > 0` (ou repose 65 535) ; 2 à
     3 lignes, ni chaudes ni dans le temps.
   - **États sondés** : le volume CD de la carte (sonde du son). Sans reset, les champs du CT1335 (`MixerKind` = 1)
     restent nuls.
   - **N9** (restaurer les volumes documentés) relève d'une question de principe (§ 9, Q1).
5. **Vérification.** En C# seul :
   - SB 2.0 sans mélangeur : `sound.cd_vol_l == 65535` ;
   - SB 2.0 avec mélangeur : 08h relu 0 et volume CD de 20, la valeur de PCem et celle du document (−46 dB × −11 dB) ;
   - Pro, puis 16 : 28h, puis 36h/37h relus 0 après reset.
6. **Classement.** (a) pour la SB 2.0 sans mélangeur ; le reste est fidèle (à écrire dans l'entrée).
7. **Dépendances.** L'audio CD (PB-123 et PB-124, hors de ce domaine) ; N9.

### PB-146 — 1Fh, 2Ch, 7Dh et 7Fh prennent les paramètres de la commande précédente

1. **Défaut, effet.** `sb_commands` vaut 0 pour ces quatre commandes, qui lisent pourtant `sb_data[0..1]`, les octets
   de la commande précédente. *Effet* : le premier bloc de ces transferts automatiques a une longueur fausse, d'où une
   IRQ trop tôt ou trop tard ; les suivants rechargent `sb_8_autolen`.
2. **Sites.**
   - C : `sound_sb_dsp.c:45-54` (la table), `:352-358` (1Fh), `:374-377` (2Ch), `:428-434` (7Dh), `:435-441` (7Fh).
   - C# : `iXtal26/Sound/sound_sb_dsp.cs:468-477`, `:492-497`, `:562-569`, `:570-577` (marqueurs PB-146).
   - Banc : SBBANC (`tools/sbbanc/sbbanc.py:311-312`).
3. **Vrai comportement.** *Documenté*, deux fois :
   - **[CL]** :
     - 1Ch : « Output 1Ch » (sans paramètre), et une IRQ après chaque bloc « of size set by command 48h » (p. 6-8,
       PDF 93) ;
     - 1Fh, 2Ch, 7Dh et 7Fh renvoient à 1Ch (p. 6-9, 6-10, 6-18 et 6-19) ; 48h (p. 6-16) ;
     - la marche à suivre « 8-bit Mono Auto-initialize Transfer » (p. 3-15/3-16, PDF 44-45).
   - **Les micrologiciels** :
     - [FW202] : `cmd_dac_autoinit_adpcm` copie `dma_blk_len` dans le compte (`sbv202.asm:1612-1616`), et
       `cmd_adc_autoinit_direct` fait de même pour 2Ch (`:1395-1398`) ;
     - [FW405] : `dma_dac1_autoinit` (`v405-8k_e51aff23.asm:2603-2614`).
   - **De plus, ces commandes « with reference byte » lisent d'abord un octet de référence** : [FW202], « Least
     significant bit of command byte indicates reference mode » (`sbv202.asm:1659-1672`) ; [FW405] `dma_dac1_reference`
     (`:2708-2717`). PCem ne le lit pas : constat neuf N1, au § 6.
4. **Correction.**
   - **Quoi** : `dsp.sb_8_autolen` à la place de `sb_data[0] + (sb_data[1] << 8)` aux quatre sites (4 lignes) ; et,
     pour 1Fh, 7Dh et 7Fh, l'octet de référence lu comme à 17h, 75h et 77h (environ 3 lignes chacune, N1).
   - **Chaud** : non.
   - **Temps visibles** : oui, la longueur du premier bloc, donc l'instant de la première IRQ.
   - **États sondés** : `sb_8_length`, `sbref`/`sbstep`, et la sonde du DMA.
5. **Vérification.** En C# seul :
   - la suite 48h 7Fh 01h, puis **40h A5h** (pour changer `sb_data[0]`), puis 7Dh ;
   - attendu : l'IRQ après 180h octets, le bloc de 48h ; le mode PCem la place après 1A6h octets, d'où le contrôle
     négatif.

   **Le scénario actuel de SBBANC ne distingue pas** : 48h 7Fh 01h y précède immédiatement 7Dh, et les octets
   « périmés » valent la taille du bloc.
6. **Classement.** (a).
7. **Dépendances.** PB-90 ; PB-91 (1Fh) ; N1.

### PB-147 — Sur la SB 1.0, « haut-parleur actif » met le DMA en pause

1. **Défaut, effet (selon l'entrée).** Sur un DSP antérieur à 1.5, D1h et D3h posent `sb_8_pause = 1` au lieu de toucher
   le son. *Effet* : un D1h après le lancement d'un DMA suspend la lecture jusqu'à D4h, et D3h ne coupe pas le son.
2. **Sites.**
   - C : `sound_sb_dsp.c:529-535` (D1h), `:536-542` (D3h), `:207` (`sb_start_dma` remet la pause à zéro).
   - C# : `iXtal26/Sound/sound_sb_dsp.cs:666-680` (marqueur PB-147).
   - Banc : SBBANC, D1h après 14h (`tools/sbbanc/sbbanc.py:303-304`).
3. **Vrai comportement.**
   - **La pause est documentée** : [CL] D1h, p. 6-25 (PDF 110) : « On version 1.xx, the DSP will pause the DMA
     transfer after executing this command » ; D3h, p. 6-26 (PDF 111), le même texte. **PCem est fidèle sur la
     pause.**
   - **Mais D1h et D3h allument et éteignent aussi le haut-parleur, sur toutes les versions** : « The speaker here
     refers to the connection of the digitized sound output to the amplifier input » (p. 6-25) ; la colonne
     « Available » coche 1.xx. *Documenté*. Sur la SB 1.0, PCem ne touche pas `muted` : **D3h ne coupe pas le son, et
     c'est là le défaut.**
   - Le micrologiciel 1.05 manque. [FW202] et [FW302] commandent la broche de coupure (`cmd_speaker_on/off`,
     `sbv202.asm:1827-1866` ; `v302_4k_4701c5fc.asm:1413-1430`) : *déduit* pour 1.05.
   - Sur un DSP 4.xx, D1h et D3h sont sans effet sur la sortie (p. 6-25, note 2) : PCem y est fidèle (`< SB16`).
4. **Correction.**
   - **Quoi** : sur la SB 1.0, D1h fait pause et `muted = 0`, D3h fait pause et `muted = 1` ; 2 lignes.
   - **Chaud, temps visibles** : non ; la pause ne change pas.
   - **États sondés** : `muted` (sonde du son).
   - **Voisin** (N4, au § 6) : l'état du haut-parleur au démarrage et au reset du DSP.
5. **Vérification.** En C# seul :
   - SB 1.0 : D3h, puis 14h et D4h → `dsp.buffer` nul ; D1h, puis 14h et D4h → non nul ;
   - D8h n'existe pas sur 1.xx (« Available » à partir de 2.00, p. 6-28) : ne pas l'employer dans l'essai.
6. **Classement.** (a) pour la coupure ; (d) pour la pause. L'entrée est à scinder et à amender.
7. **Dépendances.** N4.

### PB-148 — L'enregistrement rend toujours du silence

1. **Défaut, effet.** `sb_enable_i` n'est écrit nulle part. Seules la SB 16 et l'AWE32 ont un bloc d'enregistrement,
   mort par conséquent ; les cartes 8 bits n'en ont aucun ; A0h/A8h ne font rien. *Effet* : toute entrée DMA ou directe
   rend 80h.
2. **Sites.**
   - C : `sound_sb_dsp.h:63` ; `sound_sb.c:48`, `:72` et `:113` (TODO), `:180-197` (SB 16), `:272-315` (AWE32) ;
     `sound_sb_dsp.c:282` (`memset`) et `:468-473` (A0h/A8h).
   - C# : `iXtal26/Sound/sound_sb_dsp.cs:83-87`, `:384`, `:604-609` ; `iXtal26/Sound/sound_sb.cs:459-477` et `:568-590`
     (marqueurs PB-148).
3. **Vrai comportement.** *Documenté* :
   - **CT1745** : les interrupteurs d'entrée 3Dh/3Eh mènent MIDI (l'OPL), ligne, CD et micro au mélangeur d'entrée,
     qui les additionne. Sources : figure 4-5 (p. 4-14, PDF 72) ; 3Dh/3Eh, p. 4-16 (PDF 74) ; gain d'entrée 3Fh/40h,
     p. 4-17. On peut donc enregistrer l'OPL et le CD.
   - **CT1345** : une seule source, le micro, le CD ou la ligne (0Ch:1,2 ; p. 4-8, PDF 66) ; A0h/A8h règlent l'entrée
     mono ou stéréo, DSP 3.xx seulement (p. 6-4, PDF 89).
   - **SB 1.x et 2.0** : le micro seul (*déduit* : aucun sélecteur d'entrée).
   - Sans entrée audio de l'hôte, le micro et la ligne rendent le silence (80h) : *déduit*. PCem est donc juste pour
     le micro et la ligne, faux pour le CD et l'OPL.
4. **Correction.**
   - **Quoi** :
     - poser `sb_enable_i` tant qu'une entrée DMA tourne, et le retirer à la fin et au reset ;
     - SB 16 et AWE32 : remplir `record_buffer` avec l'OPL selon 3Dh/3Eh (le bloc existe), plus le CD, à prendre au
       tampon CD de `sound.cs` ;
     - SB Pro : le CD quand 0Ch le choisit, et A0h/A8h.
   - **Taille** : 60 à 100 lignes.
   - **Chemin chaud** : oui, les boucles de mélange.
   - **Temps visibles** : non (le rythme du DMA d'entrée ne change pas) ; seules les données changent.
   - **États sondés** : l'empreinte de `record_buffer`, `record_pos_write`, et les octets écrits en mémoire de l'invité.
5. **Vérification (faible).** En C# seul :
   - SB 16, 3Dh/3Eh sur MIDI, une note de l'OPL, puis C8h en stéréo : des échantillons non nuls, corrélés à
     `sb.opl.buffer` ;
   - SB Pro, 0Ch sur le CD, PLAY AUDIO sur la piste audio de `mixte.cue` : des échantillons non nuls ;
   - micro et ligne : 80h.

   Aucune fonction de transfert documentée (filtres, AGC, gains exacts) : d'où « faible ». Témoin : VREC, de la
   disquette SB Pro 2, une fois installée.
6. **Classement.** (b).
7. **Dépendances.** PB-151 et PB-156 deviennent observables ; PB-153 (la sélection mal parenthésée, N5) ; PB-123 et
   PB-124 (le CD) ; PB-145.

### PB-149 — L'octet de mode de B0h à CFh n'est pas masqué

1. **Défaut, effet.** `sb_data[0]` passe tel quel comme format. En 8 bits, 01h à 03h prennent le chemin ADPCM ; toute
   valeur hors de 00h/10h/20h/30h n'a pas de `case`. *Effet* : un octet de mode aux bits réservés posés fige le
   transfert, sans fin ni IRQ.
2. **Sites.**
   - C : `sound_sb_dsp.c:484`, `:497`, `:510` et `:523` ; `pollsb`, `:866-1034` et `:1051-1086`.
   - C# : `iXtal26/Sound/sound_sb_dsp.cs:619-622` et `:646-649` (marqueurs), `:633-635` et `:660-662` (B8h et C8h,
     **sans marqueur**), `:1194-1197`.
   - Banc : SB16BANC (`tools/sb16banc/sb16banc.S:221`).
3. **Vrai comportement.** *Documenté* :
   - [CL] p. 6-23 (PDF 108) : bMode = D7-D6 à 0, D5 stéréo, D4 signé, D3-D0 à 0 ; p. 6-24 pour Cxh ;
   - [FW405] et [FW413] : `cmd_dma8` range l'octet de mode dans `dma8_config_temp` (2Ch) et ne teste que ses bits 4 et
     5 (`v405-8k_e51aff23.asm:979-1050`, `v413-8k_e22e9001.asm:1175-1240`) ; les autres bits sont ignorés ;
   - DOSBox-X ne lit lui aussi que ces deux bits (`sblaster.cpp`, cas B0h-CFh) : source secondaire.
4. **Correction.**
   - **Quoi** : `sb_data[0] & 0x30` aux quatre sites ; 4 lignes, à la commande, pas sur un chemin chaud.
   - **Temps visibles** : oui ; le transfert tourne et lève son IRQ.
   - **États sondés** : `sb_8_format` et `sb_16_format`, les longueurs, et la sonde du DMA.
5. **Vérification.** SB16BANC § 4 (C0h en mode 01h) en C# seul : un 8 bits non signé mono, IRQ après len + 1 et 82h
   bit 0. S'y ajoute un essai en C# seul sur B0h en mode 01h.
6. **Classement.** (a).
7. **Dépendances.** PB-90 : le chemin ADPCM n'est plus atteint par Cxh.

### PB-153 — Le CT1745 : 80h, 81h, 82h, 0Ah, 01h, la stéréo et la sélection d'entrée

1. **Défaut, effet.** Un faisceau de défauts du mélangeur de la SB 16 et de l'AWE32, détaillé ci-dessous. *Effets* :
   un pilote ne voit jamais le MPU dans 82h ; le micro réglé par 0Ah se relit faux ; un jeu réglé à l'IRQ 5 par BLASTER
   n'a pas d'IRQ ; 16 bits sur canal 8 bits impossible.
2. **Sites.**
   - C : `sound_sb.c:143-148` et `:235-240` (la sélection), `:540-546` (01h), `:550-566` (le reset), `:601` (0Ah→3Ah),
     `:611-636` (80h et 81h écrits), `:695` (0Ah relu), `:711-765` (80h et 81h relus), `:770` (82h), `:1059` et
     `:1084` (TODO) ; `sound_sb_dsp.c:826-828` (IRQ 7, DMA 1, DMA 16 bits 5).
   - C# : `iXtal26/Sound/sound_sb.cs:420-425`, `:530-535`, `:898`, `:909-938`, `:1000-1002`, `:1077-1079` (marqueurs
     PB-153), `:1380` (commentaire).
   - Banc : SB16BANC § 2.
3. **Vrai comportement, point par point.**

| Point | Ce que fait PCem | Vrai comportement | Niveau, source | Classement |
|---|---|---|---|---|
| 80h, plusieurs bits ; 0 | le dernier bit gagne ; 0 ne change rien | « Note that only a bit can be set on at any one time » ; le cas contraire n'est pas décrit | inconnu ([CL] p. 2-6, PDF 28). DOSBox-X : le plus bas gagne, et 0 = aucune IRQ ; 86Box : comme PCem | (c) |
| 81h, plusieurs bits | le dernier gagne | « only a bit on the 16-bit DMA channel… can be set on at any one time. This applies for the 8-bit DMA channel » | inconnu ([CL] p. 2-7, PDF 29) | (c) |
| 81h, aucun bit 16 bits | rien ne change | « DSP version 4.xx also supports the transfer of 16-bit digitized sound data through 8-bit DMA channel. To make this possible, set all the 16-bit DMA channel bits to '0' » ; cavalier « High DMA… Use Low » | documenté ([CL] p. 2-7 ; [3039]). L'ordre des octets, le faible d'abord, est déduit (86Box, `snd_sb_dsp.c`, `sb_16_read_dma`) | (a) |
| 82h, bits 0-2 | bit 2 (MPU) jamais posé | D0 : 8 bits ou SB-MIDI ; D1 : 16 bits ; D2 : MPU-401 | documenté ([CL] p. 2-5, PDF 27) | (a), avec PB-154 |
| 82h, bits hauts | `| 0x4000` perdu dans un `uint8_t`, donc 0 | « reserved » | inconnu : DOSBox-X rend 20h sur la SB 16 ; 86Box 10h (4.04), 20h (4.05), 80h (4.11 et plus) ; le commentaire de PCem (`sound_sb.c:768-769`) dit sans doute 20h, 40h et 80h | (c) |
| 0Ah ↔ 3Ah | `3Ah = 0Ah × 3 + 10`, sans `<< 3` ni `& 7` ; relu `(3Ah − 10) / 3` | 0Ah : micro sur 3 bits D2:D0, « 0 to 7 ⇒ −42 dB to 0 dB, in 6 dB steps » ; 3Ah : 5 bits D7:D3, pas de 2 dB. D'où `3Ah = ((0Ah & 7) × 3 + 10) << 3` | documenté pour l'échelle ([CL] p. 4-15, PDF 73 ; fig. 4-3, p. 4-12, PDF 70) ; déduit pour la formule. Les émulateurs divergent : 86Box `(0Ah << 5) \| 18h`, DOSBox-X `((v & 7) << 2) \| 1` | (b) ; l'entrée est à élargir : le décalage manque, il n'y a pas qu'une troncature |
| 01h | relu FFh | « sets bit 7 if previous mixer index invalid » : une note citée par PCem (`sound_sb.c:541-546`), de source inconnue ; 01h est absent de la fig. 4-3 | inconnu | (c) |
| 0Eh (stéréo de la Pro) | non branché sur la SB 16 | « There is also no need for the Stereo Switch bit as on the CT1345 » ; 0Eh n'est pas dans la carte du CT1745 | documenté ([CL] p. 4-11, PDF 69 ; fig. 4-3). DOSBox-X : la stéréo de la Pro n'est pas prise en charge par une SB 16 | (d), fidèle |
| sélection MIDI | `a ? out_l : ((0 + b) ? out_r : 0)` | le mélangeur d'entrée additionne les interrupteurs fermés | documenté (fig. 4-5, p. 4-14) | (a), observable avec PB-148 |
| IRQ et DMA au démarrage | IRQ 7, DMA 1, DMA 16 bits 5 | réglage d'usine : IRQ 5 (« Possible IRQ lines are at 2, 5, 7 and 10, with a factory default of 5 », pour la ligne que le MPU partage avec le DSP) ; DMA 1, DMA 16 bits 5 | IRQ : documenté ([CL] p. 5-5, PDF 80 ; ligne partagée, p. 2-5) ; DMA : secondaire (retronn.de, flaterco) | question (§ 9, Q5) |
| 3Dh/3Eh au reset (constat neuf, N5) | 55h / 2Bh (MIDI fermé) | défauts 15h (Line.L, CD.L, Mic) et 0Bh (Line.R, CD.R, Mic), MIDI ouvert | documenté ([CL] p. 4-16, PDF 74) | (a) |

4. **Correction.**
   - **Ce qui est retenu** :
     - 82h, bits 0-2 (2 lignes ; le bit 2 avec PB-154) ;
     - la traduction 16→8 bits : 81h sans bit 16 bits, puis `sb_16_read_dma` et `sb_16_write_dma` font deux
       transferts 8 bits par échantillon (environ 40 lignes) ;
     - 0Ah↔3Ah selon l'échelle documentée (3 lignes) ;
     - la sélection MIDI additionnée (4 lignes × 2 fonctions) ;
     - 3Dh/3Eh au reset (2 lignes).
   - **Chemin chaud** : la sélection seule, dans les boucles de mélange.
   - **Temps visibles** : oui pour la traduction (le canal 1 du 8237 sert deux fois par échantillon) et pour l'IRQ
     au démarrage si elle change.
   - **États sondés** : l'empreinte des registres du mélangeur, les champs du CT1745 (sélecteurs, `mic`),
     `sb_irqnum`, `sb_8_dmanum`, `sb_16_dmanum`, et la sonde du DMA.
5. **Vérification.** En C# seul, des relectures aux valeurs de [CL] :
   - 82h = 01h après F2h, 02h après F3h ;
   - 3Ah = F8h après 0Ah = 07h ;
   - 3Dh/3Eh = 15h/0Bh après le reset ;
   - 81h = 02h, puis B0h : la lecture passe par le canal 1, deux octets par échantillon, et les échantillons valent
     les mots du tampon.

   SB16BANC en C# seul, en fumée.
6. **Classement.** Mixte : (a) pour 82h bits 0-2, la traduction, la sélection et 3Dh/3Eh ; (b) pour 0Ah↔3Ah ; (c) pour
   80h/81h à plusieurs bits ou à 0, 01h et les bits hauts de 82h ; (d) pour 0Eh ; une question pour l'IRQ au
   démarrage.
7. **Dépendances.** PB-154 (82h bit 2, l'IRQ suit 80h) ; PB-148 (la sélection) ; PB-157 (le 8237 haut, contourné par
   la traduction) ; PB-152 (3Bh) ; PB-145 (le reset du CT1745) ; N5.

### PB-154 — Le MPU-401 de la SB 16 et de l'AWE32 : 330h fixe, sans IRQ, en UART seul

1. **Défaut, effet.** `mpu401_uart_init(…, 0x330, -1, 0)`. FFh rend FEh même en mode UART ; aucune entrée MIDI. *Effet* :
   un test d'IRQ du MPU échoue, et un pilote qui n'attend pas d'ACK après FFh en UART lit un FEh.
2. **Sites.**
   - C : `sound_sb.c:1066` et `:1091` ; `sound_mpu401_uart.c:9-14` (l'IRQ), `:16-50` (dont `:26`, l'ACK), `:52-62`.
   - C# : `iXtal26/Sound/sound_mpu401_uart.cs:43-48`, `:52-87` (marqueur `:53`), `:89-100`, `:102-111` ;
     `iXtal26/Sound/sound_sb.cs:1406` et `:1445`.
   - Banc : SB16BANC § 3.
3. **Vrai comportement.**
   - **L'IRQ** est partagée avec le DSP : « four interrupts use the same Interrupt Request (IRQ) line… and MPU-401 MIDI
     UART mode interrupts » ([CL] p. 2-5, PDF 27).
     - Elle est levée au passage en UART : « An interrupt is generated when the interface is set to UART mode…
       reading from the Data port will clear the interrupt signal » (p. 5-9, PDF 84) ; et à l'arrivée d'un octet
       MIDI (p. 5-10).
     - On l'acquitte par une lecture de 3x0h (p. 2-5), et 82h bit 2 la signale.
     - Chez Roland, la sortie DSR* (donnée disponible) mène l'interruption de l'hôte ([MPU] p. 10 du PDF).
     - *Documenté*.
   - **Le port** : 300h ou 330h par cavalier, 330h d'usine ([CL] p. 5-5 et A-10, PDF 80 et 124) ; [3039] montre les
     cavaliers « MIDI Port Address » et « MPU-401 Emulation Enabled/Disabled ». *Documenté*.
   - **Les commandes** : « Once UART mode is entered, the only command the interface recognizes is Reset » (p. 5-5) ;
     « only two commands are recognized… Reset and Enter UART mode » (p. 5-7). PCem ignore les autres sans ACK :
     **fidèle**.
   - **FFh en mode UART** : Roland dit « An ACK will not be sent back upon sending a SYSTEM RESET to leave the UART MODE
     ($3F) » ([MPU] p. 14, PDF 17), puis « The MPU-401 will not return an ACK ($FE) in this one case » (§ 5.3,
     p. 22, PDF 25). Creative écrit « After resetting the interface, a Command Acknowledge byte, 0FEh, should be read
     back » (p. 5-8, PDF 83), sans distinguer le mode UART. **Pour le MPU de la SB 16, inconnu.** DOSBox-X et 86Box
     suivent Roland (sources secondaires).
   - **Une lecture sans donnée disponible** : inconnu.
4. **Correction.**
   - **Quoi** :
     - l'IRQ du MPU = celle de la carte, qui suit 80h ;
     - levée quand un octet est mis à disposition (l'ACK de 3Fh, celui de FFh hors UART), effacée par la lecture de
       3x0h ;
     - un drapeau `sb_irq401` pour 82h bit 2 ;
     - la ligne partagée (N10) : n'effacer l'IRQ au PIC que si aucune source ne reste.
   - **Taille** : 25 à 35 lignes ; une clé de port 300h/330h (et « désactivé ») en ajouterait une dizaine, avec sa
     liste `selection` (le patron de PB-93).
   - **Chaud** : non. **Temps visibles** : oui, des IRQ nouvelles.
   - **États sondés** : les champs du MPU (`status`, `rx_data`, `uart_mode`, `addr`, `irq`).
   - **Fichier serré pour R2** : `sound_mpu401_uart.cs` compte 65 lignes vives contre 54 en C, soit 1,20, au niveau du
     fichier (§ 8, risque 6).
5. **Vérification.** En C# seul :
   - 3Fh → bit de l'IRQ de la carte au registre IRR du PIC, et 82h = 04h ;
   - lecture de 330h → FEh, IRR effacé, 82h = 00h ;
   - FFh hors UART → ACK et IRQ.

   SB16BANC (FFh, 3Fh, ACh) en C# seul.
6. **Classement.** Mixte : (a) pour l'IRQ partagée, l'acquittement et 82h bit 2 ; (c) pour l'ACK de FFh en UART et la
   lecture sans donnée ; le port est une question de configuration (§ 9, Q5).
7. **Dépendances.** PB-153 (82h, et 80h qui fixe l'IRQ) ; G12, décision n° 7 (le MPU fixe, et l'avertissement si
   l'AHA-1542C est en 330h) ; N10.

### PB-155 — Le FIR de la SB 16 dépasse le gain unité au-delà de 34,7 kHz, et la conversion déborde

1. **Défaut, effet.** Au-delà de 34 736 Hz, Σ|coef| dépasse 2. Un signal pleine échelle dont les signes suivent ceux des
   coefficients déborde alors `(int32_t)(low_fir_sb16(…) × voice)`, et `cvttss2si` rend INT_MIN. *Effet* : des
   claquements pleins négatifs sur les sons forts et aigus, et du repliement au-delà de 48 kHz.
2. **Sites.**
   - C : `sound_sb_dsp.c:79-105` (`recalc_sb16_filter`), `filters.h:270-295`, `sound_sb.c:150-151` et `:242-243`.
   - C# : `iXtal26/Sound/sound_sb_dsp.cs:93-118` ; `iXtal26/Sound/sound_sb.cs:428-431` et `:538-541` (marqueurs).
   - Banc : SB16BANC § 6 ; `tools/gates/series.sh:210`.
3. **Vrai comportement.**
   - Le FIR est le filtre de rééchantillonnage de PCem, un artefact de l'émulateur. Un CNA ne s'enroule pas : il
     sature. *Déduit*.
   - Les fréquences valides : 41h, « Valid sampling rates range from 5000 to 45 000 Hz inclusive » ([CL] p. 6-15,
     PDF 100) ; table 3-2, 5 000 à 44 100 Hz (p. 3-9, PDF 38).
   - **Le micrologiciel borne la fréquence**, *documenté* :
     - [FW405] `X09a7` (`v405-8k_e51aff23.asm:1793-1855`), appelé par 41h (`:1704-1717`) : un octet fort ≥ B1h
       (≥ 45 312 Hz) donne le maximum (« 0FFh = 45.32kHz ») ; < 13h (< 4 864 Hz), le minimum ;
     - [FW413], la même chose (`v413-8k_e22e9001.asm:1995-…`).

     Au-dessus de 45,3 kHz, la part du défaut n'existe donc pas sur une vraie carte. Entre 34,7 et 45,3 kHz (dans les
     normes), seul reste le débordement de la conversion.
4. **Correction.**
   - **La conversion** : saturante, aux quatre sites ; 4 à 8 lignes. Chemin chaud (une comparaison par échantillon et
     par voie). Aucun temps visible ; état sondé : `sound_hash` seul.
   - **En option (N8)** : le bornage du micrologiciel à 41h et 42h, et à 40h par sa table ; environ 6 lignes.
     - Temps visibles : oui, la vitesse de lecture au-delà des normes.
     - En mode matériel, il remplace la garde de 1 Hz de PB-150 : 0 donne ~4,9 kHz, comme sur la carte.
5. **Vérification.**
   - SB16BANC § 6 en C# seul : aucune sortie de mélange au motif INT_MIN ; un essai sur le FIR avec le carré adverse,
     dont la sortie sature de façon monotone.
   - En option : 41h FFFFh → `sb_freq` = 45 312 ; 41h 0000h → 4 864. Valeurs du micrologiciel, à affiner par sa table.
6. **Classement.** (a) pour la saturation ; (a) pour le bornage, en option.
7. **Dépendances.** PB-150 (non reproduit, R9) ; PB-160 (le débordement de la sortie de l'EMU8000) ; PB-156 (rendu
   inatteignable par le bornage).

### PB-165 — L'AWE32 est un DSP de type SB16 + 1 : 08h n'a plus de paramètre

1. **Défaut, effet (selon l'entrée).** `sb_doreset` ne pose `sb_commands[8] = 1` que pour `== SB16`. Sur l'AWE32 (type
   SB16 + 1), 08h s'exécute dès l'octet de commande, rend 18h, et son paramètre est pris pour une commande.
2. **Sites.**
   - C : `sound_sb.c:1073-1095`, `sound_sb_dsp.c:57`, `:159-178` (dont `:168-171`), `:620-645` ; `ibm.h:359-360`.
   - C# : `iXtal26/Sound/sound_sb.cs:1434-1436` (marqueur) ; `iXtal26/Sound/sound_sb_dsp.cs:283-301` (`sb_doreset`,
     **sans marqueur**), `:753-762` (08h).
   - Bancs : AWEBANC (`tools/awebanc/awebanc.S:64-66`) ; SB16BANC (`tools/sb16banc/sb16banc.S:70-71`).
3. **Vrai comportement.** *Documenté* par le code des micrologiciels :
   - [FW413] : le gestionnaire de 08h, `cmd_csp_version` (`v413-8k_e22e9001.asm:1503-1507`), lit le port 82h du bus X
     (`mov r0,#csp_control_port` / `movx a,@r0`) et rend l'octet, **sans lire d'octet de l'hôte** ; l'aiguillage
     n'en lit pas non plus (`:1053-1110`) ;
   - [FW405] : identique (`v405-8k_e51aff23.asm:1326-1330`, et `:881-913`).

   Sur le vrai DSP, 08h n'a donc **aucun paramètre, ni sur la SB 16 (4.05) ni sur l'AWE32 (4.13)** :
   - l'AWE32 de PCem est juste ;
   - la SB 16 de PCem (`sb_commands[8] = 1`) est fausse ;
   - la valeur rendue dépend du CSP s'il est monté : inconnu sans la puce. PCem rend 18h. Linux envoie 08h 03h et
     attend 10h à 1Fh pour reconnaître un CSP (`sb16_csp.c:573-590`) : source secondaire.
4. **Correction.** Rien pour l'AWE32 : PCem est fidèle. Côté SB 16 (N7, à inscrire), `sb_commands[8] = -1` en mode
   matériel ; 1 à 2 lignes dans `sb_doreset`. Rien de chaud.
   - Temps visibles : le flot de commandes change (l'octet suivant 08h est exécuté).
   - États sondés : `sb_read_data`, `sb_data_stat`. `sb_commands` est `static` et hors sonde.
5. **Vérification.** En C# seul : sur la SB 16, 08h 00h rend un octet, puis 00h s'exécute (sans effet) ; l'AWE32 ne
   change pas.
6. **Classement.** (d) pour PB-165 tel qu'écrit. L'entrée est à amender : le micrologiciel 4.13 donne raison à PCem.
   Le défaut de la SB 16 (N7) est (a) pour le nombre de paramètres et (c) pour la valeur rendue.
7. **Dépendances.** N7.

### PB-158 — L'EMU8000 lu et écrit par octet ; WC figé entre deux écritures

1. **Défaut, effet.** `emu8k_inb` rend `>> 1` pour un port impair, et `emu8k_outb` écrit un mot entier par octet, avec
   tous les effets de bord à chaque octet. `emu8k_inw` n'appelle jamais `emu8k_update` : WC, CPF, CVCF et CCCA relus
   restent figés, puis sautent d'un tampon. *Effet* : des octets décalés à la détection ; une attente par WC dure
   jusqu'au tampon suivant ; CCCA avance par bonds.
2. **Sites.**
   - C : `sound_emu8k.c:1405-1411` (`inb`), `:1413-1420` (`outb`), `:342-716` (`inw` ; TODO `:647-649` ; WC `:651`),
     `:718-724` (`update` à l'écriture), `:2006` ; `sound_sb.c:221`.
   - C# : `iXtal26/Sound/sound_emu8k.cs:502-505` (marqueur), `:660-661` (WC), `:1337-1345` et `:1347-1356`
     (marqueurs).
   - Banc : AWEBANC (`tools/awebanc/awebanc.S:86`, `:405`).
3. **Vrai comportement.**
   - **Les octets** : « All I/O transactions must be performed as word or "doubleword" I/O transactions; no byte I/O
     transactions are allowed » ([EMU] p. 7). Le comportement sur un accès par octet n'est donc pas décrit : inconnu.
     La valeur d'une lecture d'octet à adresse impaire sur une carte 16 bits, D15-D8 aiguillés par le bus AT, est
     déduite ; les effets de bord par octet sont inconnus.
   - **WC** : « This word register provides a counter continuously incrementing at the sample rate. There is no
     mechanism to reset this counter, which cycles through 65536 value every 1.486 seconds » ([EMU] p. 14).
     *Documenté*.
   - **CPF, CVCF, CCCA** sont des registres « courants » (le synoptique de [EMU] p. 20 ; PCem lui-même : « The docs
     says that this value is constantly updating ») : *déduit*.
4. **Correction.**
   - **WC et registres courants** : en mode matériel, `emu8k_inw` appelle `emu8k_update` avant de les lire ; 1 à
     3 lignes.
     - Coût : `emu8k_update` rend la main tout de suite sans échantillon neuf (`sound_emu8k.c:1608-1610`) ; sinon il
       avance la synthèse plus tôt. Le travail se déplace, il ne s'ajoute pas.
     - Chemin chaud : `emu8k_update`, la routine la plus lourde du son, appelée plus souvent.
     - Temps visibles : oui ; les attentes sur WC durent ce que dit la documentation.
     - États sondés : la sonde de l'EMU8000 (`wc`, `pos`, les tampons).
   - **La lecture impaire** : `>> 8` (1 ligne).
   - **Les écritures par octet** : inchangées.
5. **Vérification.** En C# seul :
   - deux lectures de WC séparées par t ms de temps invité (une boucle cadencée par le PIT) : ΔWC ≈ 44 100 × t, à une
     unité près par tic du son ;
   - AWEBANC § 6 (WC lu deux fois sans écriture) rend deux valeurs différentes en mode matériel.
6. **Classement.** Mixte : (a) pour WC et les registres courants ; (b) pour la lecture impaire ; (c) pour l'écriture
   par octet et les effets de bord par octet.
7. **Dépendances.** PB-159 (mêmes fonctions) ; PB-160 (le rythme de la synthèse).

### PB-159 — Les bits plein et vide de la DRAM sont invisibles ; SMARW n'est pas masqué

1. **Défaut, effet.** `dmareadbit` et `dmawritebit` (8000h) sont OU-és à la valeur de 32 bits, mais A22h rend le mot
   HAUT : le drapeau n'apparaît jamais, et sa remise à zéro agit à vide. `smarw++` n'est pas masqué à 24 bits. *Effet* :
   un programme qui attend « non plein » passe ; au-delà de FFFFFFh, SMARW relu montre ses bits 24 et plus.
2. **Sites.**
   - C : `sound_emu8k.c:1136`, `:1140`, `:1150` (pose) ; `:619-637` (lecture et effacement) ; `:207-215` (`READ16`) ;
     `:1152` (`smarw++`) ; `:894`, `:560`, `:644` (les masques).
   - C# : `iXtal26/Sound/sound_emu8k.cs:622-648` (marqueur `:624`), `:1065-1085` (marqueur `:1083`), `:366-367`.
   - Banc : AWEBANC (`tools/awebanc/awebanc.S:306`).
3. **Vrai comportement.** *Documenté* :
   - SMALR et SMARR, bit 31 = MT (vide) ; SMALW et SMARW, bit 31 = FULL ;
   - « Bits 31-24 are Don't Care on write, and bits 30-24 are zero on read » ; « Bits 23-0 are the sound memory
     address » ([EMU] p. 11-12) ;
   - la marche à suivre attend ces bits ([EMU] p. 22-23). ALSA attend `& 0x80000000` (`emu8000.c:112-131`) : source
     secondaire.
   - Combien de temps le drapeau reste posé : inconnu (« until data can be transfered ») ; « posé jusqu'à la première
     lecture » est l'approximation de PCem.
4. **Correction.** Le drapeau au bit 31 (`| ((uint32_t)dmareadbit << 16)` aux quatre lectures) et `smarw = (smarw + 1) &
   EMU8K_MEM_ADDRESS_MASK` ; environ 5 lignes, ni chaudes ni dans le temps (les attentes de l'invité font un tour de
   plus). La sonde de l'EMU8000 ne change pas de représentation si le décalage se fait à la lecture.
5. **Vérification.** En C# seul : SMALR écrit, puis A22h lu → bit 15 posé, et à zéro à la lecture suivante ; SMARW à
   FFFFFFh suivi d'une écriture de SMRD → 000000h relu.
6. **Classement.** (a).
7. **Dépendances.** PB-158.

### PB-160 — L'EMU8000 de PCem : enveloppes, rustines et approximations

1. **Défaut, effet.** Un faisceau. *Effets* : des timbres autres que ceux de l'AWE32, des redéclenchements sans délai,
   des notes muettes à l'attaque 0, une carte non initialisée qui sonne.
2. **Sites.** Ceux du tableau ci-dessous ; C# : `iXtal26/Sound/sound_emu8k.cs`, marqueurs PB-160, et
   `iXtal26/Sound/sound_sb.cs:504-527`.
3. **Vrai comportement, point par point.**

| Point | C (`sound_emu8k.c`) ; C# (`sound_emu8k.cs`) | Vrai comportement | Niveau, source | Correction | Classement |
|---|---|---|---|---|---|
| a. hwcf3 forcé à 4 (la rustine de Doom) | `:989-994` ; `:930-936` | le bit d'activation de la sortie de HWCF3 est effacé au reset ; le logiciel écrit 0004h une fois l'initialisation finie | documenté ([EMU] p. 14, p. 21) | retirer (1 ligne). Un programme qui n'initialise pas la carte se tait, sauf si un utilitaire (AWEUTIL) l'a fait | (a), sous réserve de Q1 |
| b. IFATN ignoré sous cinq conditions (la rustine d'Impulse Tracker) | `:1326-1331` ; `:1256-1265` | IFATN 00h = aucune atténuation ([EMU] p. 18) ; DCYSUSV bit 7 (moteur arrêté) « prevents the envelope engine from… writing to the channel's pitch, volume, and filter target registers » ([EMU] p. 16) | documenté pour les deux faits ; la règle qui en découle est déduite | moteur arrêté → IFATN ne touche pas VTFT (environ 5 lignes), à la place de la rustine | (b) |
| c. au bout du maintien, l'enveloppe de modulation passe en RAMP_UP | `:1843-1847` ; `:1757-1762` | la décroissance suit le maintien ; la branche sans maintien va bien en RAMP_DOWN (`:1836-1840`) | déduit (cohérence interne, modèle DAHDSR) | RAMP_DOWN (1 ligne) | (b) |
| d. relâchement depuis délai, attaque ou maintien en `>> 9`/`<< 9` | `:1069` ; `:1000-1006` | même table que l'attaque, en `>> 5`/`<< 5` (`:1831`) | déduit (cohérence interne) | `>> 5`/`<< 5` (1 ligne) | (b) |
| e. délais et maintiens consommés | `:1757-1760`, `:1782`, `:1821-1824`, `:1844`, `:1870-1879` ; `:1784-1797` | la marche à suivre réécrit ENVVOL, ENVVAL, LFOxVAL et ATKHLD(V) à chaque note ([EMU] p. 23), décrits comme des valeurs « at the beginning of the note » (p. 15-17) ; si la puce les décompte sur place n'est pas dit | inconnu | aucune | (c) |
| f. attaque 0 = la voix se tait | `:1004-1005`, `:1255-1256` ; `:945-950` | « 0x00 being never attack » ([EMU] p. 17) | documenté ; « jamais d'attaque », donc silence, est déduit | aucune (PCem plausible) | (d) |
| g. pas d'initialisation repérés par init1[0] = 03FFh | `:912`, `:957`, `:1099`, `:1109`, `:1160`, `:1220` | les tableaux d'init sont des paramètres du processeur d'effets ([EMU] p. 25-26) ; leur sémantique interne n'est pas décrite | inconnu | aucune | (c) |
| h. octet haut du pointeur, compteur 80h-9Fh | `:710-711` | « random (actually a VLSI test register) during reads » ([EMU] p. 7) | documenté (aléatoire) | aucune | (d) |
| i. interpolation cubique, au lieu de la « 3-point » brevetée | `:290-317` | algorithme non publié | inconnu | aucune | (c) |
| j. chorus et réverbération « workalike », égaliseur vide | `:1422`, `:1537`, `:1587-1589` ; `:1543-1548` | microcode des effets non publié | inconnu | aucune | (c) |
| k. hauteur et coupure posées à leur cible ; volume glissant de 400h | `:1969`, `:1971`, `:1591-1602` | non décrit | inconnu | aucune | (c) |
| l. débordement de la sortie en cubique non filtré | `:1734` ; `:1641-1645` | un chemin de données à virgule fixe sature, il ne s'enroule pas | déduit | saturer (2 lignes) | (b) |
| m. 44,1 → 48 kHz par répétition | `sound_sb.c:226` ; `sound_sb.cs:504-527` | la carte sort à 44,1 kHz : c'est la conversion de l'hôte qui est en cause, pas le matériel | déduit | interpolation linéaire (environ 8 lignes) | (b) |

4. **Correction.** Les points retenus (a, b, c, d, l, m) font 20 à 30 lignes.
   - **Chemin chaud** : oui, `emu8k_update` (c, d, l) et `sb_get_buffer_emu8k` (m).
   - **Temps visibles** : aucun, sauf a (le silence d'une carte non initialisée).
   - **États sondés** : la sonde de l'EMU8000 (voix, hwcf3, tampons) et `sound_hash`.
5. **Vérification (faible, sauf a).** En C# seul :
   - a : hwcf3 relu 0 après l'allumage d'un moteur sans écriture de HWCF3 ;
   - b : IFATN 00h écrit sur une voix au moteur arrêté → VTFT inchangé ;
   - c : une voix avec maintien → la valeur de l'enveloppe de modulation décroît du sommet au palier, au taux de
     DCYSUS ;
   - d : la continuité entre la valeur d'attaque et le début du relâchement ;
   - l : saturation ;
   - m : une sinusoïde de 10 kHz rééchantillonnée, dont l'erreur se compare à une interpolation exacte.

   AWEBANC en C# seul, en fumée.
6. **Classement.** Mixte : (a) pour a ; (b) pour b, c, d, l et m ; (c) pour e, g, i, j et k ; (d) pour f et h.
7. **Dépendances.** PB-158 (le rythme) ; PB-162 et PB-163 (les effets) ; PB-164 (l'attaque de modulation) ; Q1 (le
   point a).

---

## 4. Section B — comportement indéfini en C, reproduit

### PB-21 — `speakval` divise par `pit->l[0]` sans le tester, et le POST y passe

1. **Défaut, effet.** `speakval = l[2] / l[0] × 4000h − 2000h` est recalculé à toute écriture au PIT, même quand l[0]
   vaut 0. +inf devient INT_MIN (`cvttss2si`), que le plafond ne rattrape pas. *Effet* : jusqu'à la prochaine écriture
   au PIT, en mode 0 ou 4 du canal 2, le haut-parleur sort `(int16_t)INT_MIN` = 0, le silence, là où la valeur nominale
   donnerait une tension.
2. **Sites.** C : `models/pit.c:418-423` ; `sound/sound_speaker.c:24` et `:35`. C# : `iXtal26/Models/pit.cs:545-557`
   (marqueur `:545`) ; `iXtal26/Sound/sound_speaker.cs:39-41`. Outil : `--speaker-check`.
3. **Vrai comportement.** `speakval` est le modèle analogique de PCem, sans pendant matériel. La convention *documentée*
   pour un compte initial de 0 : « The largest possible initial count is 0; this is equivalent to 2^16 for binary
   counting » ([8254], fig. 22, PDF p. 17). PCem l'applique déjà dans `pit_load` (`pit.c:119`, `l ? l : 0x10000`).
4. **Correction.** `l0 = l[0] != 0 ? l[0] : 0x10000` ; 1 à 2 lignes dans `pit_write`, pas sur un chemin chaud. Aucun
   temps visible. État sondé : la sonde du haut-parleur, champ 4 (`speakval`).
5. **Vérification.** En C# seul, à la tranche 231 d'un amorçage de 640 Ko : `speakval` = (65 535 / 65 536) × 4000h −
   2000h ≈ 1FFFh, au lieu d'INT_MIN (`--speaker-check`).
6. **Classement.** (b). La correction est certaine ; la valeur audible reste celle du modèle de PCem.
7. **Dépendances.** Aucune.

### PB-151 — L'entrée stéréo lit un élément au-delà de `record_buffer`

1. **Défaut, effet.** `record_buffer[record_pos_read + 1]` atteint l'indice FFFFh d'un tableau de FFFFh éléments, et
   en C on lit `buffer[0]`. *Effet* : tous les 32 768 couples, l'enregistrement reçoit un échantillon de la sortie en
   cours.
2. **Sites.**
   - C : `sound_sb_dsp.c:1150`, `:1157`, `:1217` et `:1225` ; `sound_sb_dsp.h:82`.
   - C# : `iXtal26/Sound/sound_sb_dsp.cs:1247-1258` et `:1298-1309` (les appels, sans marqueur), `:1337-1340`
     (`record_lu`, marqueur) ; `iXtal26/Sound/sound_sb.cs:493-501` (`record_ecrit`).
   - Outillage : `tools/oracle/harness.c:2410-2413` (l'assertion statique) ; `tools/iXtal26.Diff/R9Sb16.cs:102-120` ;
     SB16BANC § 8.
3. **Vrai comportement.** Le tampon d'enregistrement est une construction de l'émulateur, sans pendant matériel. Le
   comportement juste est l'absence d'alias : *déduit*, et certain.
4. **Correction.** En mode matériel, un anneau de 65 536 entrées (ou `& 0xFFFF` sur un tableau de 10000h) ; 2 à 4
   lignes.
   - Chemin chaud : `sb_poll_i`, au rythme de l'entrée.
   - Temps visibles : aucun.
   - États sondés : les octets écrits par le DMA d'entrée en mémoire de l'invité.
5. **Vérification.** SB16BANC § 8 en C# seul : l'octet à l'indice FFFFh est l'échantillon enregistré, nul tant que
   PB-148 n'est pas corrigé, et jamais `buffer[0]`.
6. **Classement.** (a).
7. **Dépendances.** PB-148.

### PB-156 — `len × sb_freq` déborde l'entier signé

1. **Défaut, effet.** Après 40h FFh, `len × sb_freq` atteint 2 400 × 1 000 000, au-delà de 2^31. *Effet* : aucun
   d'audible tant que l'enregistrement est muet (PB-148).
2. **Sites.** C : `sound_sb.c:202` et `:325`. C# : `iXtal26/Sound/sound_sb.cs:483-485` et `:603-605` (marqueurs).
3. **Vrai comportement.** De la comptabilité de l'émulateur : le juste est l'arithmétique exacte, *déduit*. De plus,
   40h FFh (1 MHz) sort des normes (table 3-2 de [CL]), et le micrologiciel 4.xx le borne (PB-155, N8).
4. **Correction.** `(int)(((long)len * sb_freq) / 48000) * 2` ; 2 lignes, une fois par tampon. Ni temps visibles ni
   sonde, hors `record_pos_write`.
5. **Vérification.** Un essai arithmétique en C# seul. L'effet n'est observable qu'avec PB-148.
6. **Classement.** (a).
7. **Dépendances.** PB-148 ; PB-155 (le bornage rend le cas inatteignable).

### PB-162 — Le chorus droit lit sous son tampon, et prend la fraction du gauche

1. **Défaut, effet.** Un seul repli de l'indice : aux réglages extrêmes, il reste négatif et lit dans le tampon gauche.
   La voie droite interpole avec la fraction de la gauche. *Effet* : du signal gauche fuit à droite, et l'interpolation
   droite est décalée.
2. **Sites.**
   - C : `sound_emu8k.c:1435`, `:1453-1468` ; `sound_emu8k.h:95-107`.
   - C# : `iXtal26/Sound/sound_emu8k.cs:55-75` (le type, un tableau commun), `:1362-1420` (marqueur).
   - Banc et outil : AWEBANC § 8 ; `emu8k-kernel-check`.
3. **Vrai comportement.** Le chorus de PCem est un « workalike » (`:1422`) : l'algorithme réel est inconnu. Deux choses
   sont sûres, *déduit* : une ligne à retard ne lit pas hors d'elle-même, et chaque voie interpole à sa propre position.
4. **Correction.** La fraction propre à la voie droite, et un repli complet (une boucle ou un modulo) ; environ 6 lignes.
   Chemin chaud : `emu8k_work_chorus`, à chaque échantillon. Ni temps visibles ; sonde : les tampons du chorus.
5. **Vérification (faible).** Un essai sur noyau en C# seul : avec une rampe linéaire en entrée, la sortie droite vaut
   l'interpolation exacte au retard droit. Aux réglages extrêmes, les indices restent dans [0, taille).
6. **Classement.** (b).
7. **Dépendances.** PB-160 (le « workalike »).

### PB-163 — Les conversions de la réverbération débordent l'int32

1. **Défaut, effet.** Six conversions float → `int32_t` ; hors bornes, `cvttss2si` rend INT_MIN, même pour un
   dépassement positif, et `-in` déborde pour INT_MIN. *Effet* : une réverbération saturée claque à pleine amplitude
   négative.
2. **Sites.** C : `sound_emu8k.c:1489`, `:1491`, `:1498`, `:1505`, `:1506` et `:1533`. C# :
   `iXtal26/Sound/sound_emu8k.cs:1426-1490` (marqueur `:1427` ; conversions à `:1435`, `:1437`, `:1444`, `:1453`,
   `:1454` et `:1486`). Outil : `tools/iXtal26.Diff/EmuKernelCheck.cs`.
3. **Vrai comportement.** Inconnu, puisque l'effet est un « workalike » ; un processeur de signal à virgule fixe
   sature : *déduit*.
4. **Correction.** Une conversion saturante aux six sites ; environ 8 lignes. Chemin chaud : les noyaux de la
   réverbération. Sonde : les tampons de la réverbération.
5. **Vérification (faible).** Les noyaux sur états fabriqués (le patron d'`emu8k-kernel-check`) : une saturation
   monotone, jamais INT_MIN pour un dépassement positif.
6. **Classement.** (b).
7. **Dépendances.** PB-161 (non reproduit) ; PB-160 l.

### PB-164 — L'attaque de l'enveloppe de modulation lit au-delà de sa table

1. **Défaut, effet.** L'indice dépasse la table avant d'être borné, et la valeur lue est aussitôt écrasée. *Effet* :
   aucun.
2. **Sites.** C : `sound_emu8k.c:1830-1834`. C# : `iXtal26/Sound/sound_emu8k.cs:1740-1745` (déjà borné : DEVIATION de
   forme, résultat identique).
3. **Vrai comportement.** Sans objet.
4. **Correction.** Aucune : le comportement est déjà identique. Le marqueur pourrait devenir « sans objet ».
5. **Vérification.** Sans objet.
6. **Classement.** (d).
7. **Dépendances.** PB-160 (le point c, l'enveloppe de modulation).

---

## 5. Section C — sans conséquence observable

### PB-167 — `emu8k_close` ne libère pas le bloc vide

1. **Défaut, effet.** Une fuite de 128 Kio à chaque fermeture de l'AWE32 ; rien pour l'invité.
2. **Sites.** C : `sound_emu8k.c:2031` et `:2234-2237`. C# : sans objet. `emu8k_t.mem` porte la ROM, le bloc vide et
   la RAM dans un seul tableau, que le ramasse-miettes rend avec la carte.
3. **Vrai comportement.** Sans objet.
4. **Correction.** **Aucune n'a de sens** : en C#, aucune mémoire n'est libérée à la main.
5. **Vérification.** Sans objet.
6. **Classement.** (d).
7. **Dépendances.** Aucune.

---

## 6. Constats neufs et voisins, à inscrire avant toute correction

| N | Constat | C (PCem) | Source du vrai comportement | Niveau | Rattachement proposé |
|---|---|---|---|---|---|
| N1 | 1Fh, 7Dh et 7Fh, « with reference byte » : PCem ne lit pas l'octet de référence | `sound_sb_dsp.c:352-358`, `:428-441` | [CL] p. 6-9, 6-18, 6-19 (les intitulés) ; [FW202] `sbv202.asm:1659-1672` ; [FW405] `:2708-2717` ; DOSBox-X `haveref` | documenté | PB-146 élargi, ou PB neuf |
| N2 | fin d'un bloc ADPCM en simple cycle : PCem lève l'IRQ dès la lecture du dernier octet et ne le décode pas ; le micrologiciel le joue avant l'IRQ | `sound_sb_dsp.c:943-944`, `:1036-1044` | [FW202] `vector_dma_dac_adpcm2/4`, `sbv202.asm:343-530` | documenté | PB neuf (lié à PB-90 et PB-91) |
| N3 | `DMA_NODATA` dans l'ADPCM : −1 est décodé, et le compte décrémenté ; les chemins PCM, eux, attendent (`:868-872`) | `sound_sb_dsp.c:943`, `:984`, `:1019` | [FW202] : après sa requête DMA, le DSP attend l'octet (`X01ac: jnb pin_dav_dsp,X01ac`, `sbv202.asm:424` ; `X07a7`, `:1570`) | documenté | PB neuf |
| N4 | haut-parleur : coupé au démarrage à froid, conservé au reset du DSP ; PCem démarre avec `muted = 0`, et son reset efface `sb_speaker` sans toucher `muted` | `sound_sb_dsp.c:123-157` | [FW202] `start`/`cold_boot`, `sbv202.asm:855-905` ; [FW302] `v302_4k_4701c5fc.asm:570-616` | documenté (DSP 2.02, 3.02) ; déduit pour 1.05 et 2.00/2.01 | PB neuf (lié à PB-147) |
| N5 | CT1745, 3Dh/3Eh au reset : 55h/2Bh au lieu de 15h/0Bh (MIDI fermé) | `sound_sb.c:565-566` | [CL] p. 4-16 (PDF 74) | documenté | PB-153 élargi |
| N6 | CT1745, 0Ah→3Ah : sans `<< 3` ni `& 7` (pas seulement tronqué) | `sound_sb.c:601`, `:695` | [CL] p. 4-15/4-16, fig. 4-3 | documenté (échelle) | PB-153 élargi |
| N7 | SB 16 (DSP 4.05) : 08h prend un paramètre chez PCem, aucun dans le micrologiciel | `sound_sb_dsp.c:168-171` | [FW405] `:1326-1330` | documenté | PB-165 renversé |
| N8 | 41h et 42h (et 40h par sa table) bornés par le micrologiciel 4.xx à ~4,9-45,3 kHz ; PCem accepte 1 à 65 535 Hz et 1 MHz par 40h | `sound_sb_dsp.c:389-402` (41h, 42h), `:379-388` (40h) | [FW405] `X09a7` `:1793-1855` ; [FW413] `:1995-…` | documenté | PB-150 et PB-155 élargis |
| N9 | les volumes de reset relevés exprès par PCem : voix, général et MIDI à 0 dB, contre −46 dB et −11 dB (CT1335), −11 dB (CT1345), −14 dB (CT1745) | `sound_sb.c:346-347`, `:413-414`, `:550` | [CL] p. 4-5, 4-9, 4-15 | documenté | PB neuf, déjà relevé par PLAN-G12 (« Les valeurs de reset sont changées exprès ») et jamais numéroté |
| N10 | ligne d'IRQ partagée : la lecture de 2xEh efface l'IRQ au PIC même si une IRQ 16 bits (ou celle du MPU) reste pendante | `sound_sb_dsp.c:797-799` | [CL] p. 2-5 (les sources partagent une ligne ; 82h les distingue) | déduit | PB neuf (lié à PB-154) |
| N11 | Cxh ou Bxh, 74h, 14h reçus pendant un automatique 8 bits : le DSP finit le bloc courant puis enchaîne ; PCem relance sur-le-champ | `sound_sb_dsp.c:201-230` (`sb_start_dma`) | [CL] p. 6-8 (« The DSP will, at the end of the current block transfer, exit auto-init mode and process the new DMA mode I/O command ») ; [FW405] `cmd_dma8` `:1003-1012` ; [FW202] « DMA is already running, so update block length » `sbv202.asm:1618-1630` | documenté | PB neuf |
| N12 | le décodage ADPCM de PCem reprend les tables de DOSBox (« borrowed from DOSBox », `sound_sb_dsp.c:59`) ; le décodeur du micrologiciel est autre : en mode référence, il part d'un « accumulateur » à 1 (`sbv202.asm:1586`, `:1670`), là où PCem pose `sbstep = 0` | `sound_sb_dsp.c:59-73`, `:336-338` | [FW202] `adpcm_*_decode` | documenté (à instruire) | PB neuf, ou une étude à part |

**Marqueurs manquants dans le domaine**, pour le recensement que D7 demande :
- PB-149 aux sites B8h et C8h (`sound_sb_dsp.cs:634` et `:661`) ;
- le mécanisme de PB-165 (`sb_doreset`, `sound_sb_dsp.cs:283-296`) ;
- les quatre appels de `record_lu` (PB-151, `:1251`, `:1258`, `:1301`, `:1309`) ;
- PB-90 aux six lectures des commandes.

---

## 7. Proposition d'étapes pour le domaine

Hypothèses : l'interrupteur de D7 existe, figé avant `initpc` ; le marqueur est admis par R1 ; les constats N1 à N12
sont inscrits ou écartés ; les entrées PB-92, 145, 147, 160 et 165 sont amendées. Pour chaque étape :
- une série entière en mode PCem, puisque l'émulateur change, qui doit rendre les verdicts à l'identique ;
- des essais en C# seul, chacun citant la page qui fonde sa valeur attendue ;
- pour chaque essai, son contrôle négatif : forcé en mode PCem, il doit rougir ;
- un commit par étape, dès sa série verte.

| Étape | Contenu | Lignes C# (ordre de grandeur) | Vérification |
|---|---|---:|---|
| **Son-1 — Le DSP 8 bits et l'ADPCM** | PB-90, PB-91, PB-146 (et N1), PB-147 (la coupure ; N4 si Q1 le veut), PB-149, N7 (08h de la SB 16) ; N2 et N3 si inscrits | 40-70 | un verbe `hw-dsp` en C# seul : les IRQ et les comptes du 8237 aux valeurs de [CL] et des micrologiciels ; SBBANC (cinq cartes) et SB16BANC en C# seul, en fumée. Le mieux documenté, le moins cher : par là qu'on commence |
| **Son-2 — Le CT1745, le MPU-401 et l'IRQ partagée** | PB-153 (82h, traduction 16→8, 0Ah/3Ah, sélection, N5), PB-154 (IRQ, 82h bit 2, acquittement ; le port si Q5), N10 ; PB-145 | 100-140 | `hw-ct1745` et `hw-mpu` : relectures, IRQ au PIC, traduction contrôlée au 8237 ; SB16BANC en fumée. Témoin à surveiller : TEST-SBP sur la 16 (« Error code: 0100 » des deux côtés en G12.3), dont l'issue pourrait changer ; un indice, pas une preuve |
| **Son-3 — L'enregistrement** | PB-148 (et A0h/A8h), PB-151, PB-156 | 80-120 | `hw-rec` : l'OPL et le CD (`mixte.cue`) enregistrés, non nuls et corrélés ; micro et ligne à 80h ; l'indice FFFFh. Témoin : VREC (Q10). Après Son-2, à cause de la sélection et de 3Dh/3Eh |
| **Son-4 — L'EMU8000** | PB-158 (WC, lecture impaire), PB-159, PB-160 (a selon Q1 ; b, c, d, l, m), PB-162, PB-163 | 50-80 | `hw-emu8k` : WC au rythme de 44,1 kHz, bits 31, repli à 24 bits, IFATN moteur arrêté, enveloppe de modulation ; une variante d'`emu8k-kernel-check` dont les références sont calculées dans l'essai ; AWEBANC en fumée. Le moins vérifiable : en dernier des corrections |
| **Son-5 — La clôture du domaine** | PB-21, PB-155 (saturation ; bornage N8 si décidé, qui touche PB-150) ; l'avertissement de PB-92 si décidé ; registre et marqueurs | 15-30 | `--speaker-check` ; la tonalité adverse ; la série complète ; un contrôle négatif global (le mode matériel forcé dans une porte du son → rouge) ; PERFBANC sur l'ami486 avec la SB 16, puis l'AWE32 (le coût des gardes chaudes, de l'appel d'`emu8k_update` depuis `emu8k_inw` et du rééchantillonnage linéaire) |

L'ordre suit les dépendances (Son-2 avant Son-3) et le niveau de preuve, du documenté vers l'inconnu. Son-4 ne dépend
d'aucune autre étape du son, mais passe après elles : on garde pour la fin la partie la moins vérifiable.

---

## 8. Les risques

1. **Une fuite du mode matériel dans le mode PCem.** États statiques partagés : `sb_commands`, global réécrit par
   `sb_doreset` ; les états des filtres, jamais remis à zéro ; `dmareadbit` et `dmawritebit`. Un outil qui monte
   plusieurs machines dans un processus les croiserait. Parades :
   - l'interrupteur figé avant `initpc` (D7) ;
   - `sb_commands[8]` recalculé à chaque reset (il l'est déjà) ;
   - le contrôle négatif global de Son-5.
2. **Plus d'oracle.** Les essais traduisent une lecture de la documentation ; une mauvaise lecture devient un faux
   vert. Parades : chaque valeur attendue porte sa page, et les essais passent en contre-lecture.
3. **Les micrologiciels sont des désassemblages de tiers.** Le code est exact (réassemblage identique, selon les
   dépôts), mais les étiquettes interprètent (par exemple `dma8_autoreinit_en` pour le bit « signé »). Le DSP 1.05
   manque, et 2.00/2.01 ne sont connus que par 2.02 (déduit).
4. **Des comportements fidèles mais surprenants** : un son coupé au démarrage (N4), un EMU8000 muet sans AWEUTIL
   (PB-160 a), des volumes à −11 ou −14 dB (N9). L'utilisateur peut y voir une régression ; le mode matériel doit le
   dire (la bannière, la documentation).
5. **Les temps changent** : la fin de l'ADPCM, les IRQ du MPU, WC, le premier bloc de 7Dh. Toute empreinte en C# seul
   existante (l'amorçage de Windows, les captures) bouge en mode matériel, qui demande ses propres références,
   neuves.
6. **R2 (+25 %).** `sound_mpu401_uart.cs` est à 1,20 (65 lignes vives contre 54) au niveau du fichier ; s'il compte,
   le code du mode matériel l'en fait sortir. Les autres fichiers ont de la marge : `sound_sb_dsp.cs` 0,87,
   `sound_sb.cs` 0,98, `sound_emu8k.cs` 0,97 et `pit.cs` 1,05, ratios de fichier seulement indicatifs.
7. **Le coût.** Une garde par échantillon dans `pollsb`, les boucles de mélange et `emu8k_update` ; deux lectures DMA
   par échantillon avec la traduction 16→8 ; le rééchantillonnage linéaire. Faible, mais à mesurer (PERFBANC).
8. **Peu de témoins.** Aucun logiciel SB 16 ni AWE32 (décision n° 21 de G12). Ceux de la disquette SB Pro 2 (VPLAY,
   VREC, VEDIT2, CT-VOICE.DRV, CDPLYR, d'après son README.TXT) supposent une installation sur une copie.
9. **La politique de configuration** : l'IRQ d'usine à 5 et le MPU en 300h touchent `ixtal26-486-sb16.cfg`, et le
   témoin Windows 3.11 réglé à l'IRQ 7 (VERIFICATION.md § G12.3).

---

## 9. Les questions de principe à poser à l'utilisateur

1. **Q1 — Quel « vrai matériel » pour le son ?** L'état documenté au reset et à la mise sous tension : volumes à
   −11/−14/−46 dB (N9), haut-parleur coupé au démarrage (N4), EMU8000 muet jusqu'à HWCF3 = 4 (PB-160 a). Ou l'état que
   laissent les utilitaires de Creative (SBP-SET, DIAGNOSE /S, AWEUTIL /S) ? Une option du mode matériel, « état laissé
   par les utilitaires », est possible.
2. **Q2 — Le micrologiciel désassemblé est-il une source primaire, au rang du guide de Creative ?** Il donne raison à
   PCem pour l'AWE32 (PB-165) et tort pour la SB 16 (N7), et il fonde PB-91, PB-146, PB-149, N1 à N4, N8 et N11.
3. **Q3 — Les PB où PCem est fidèle** (PB-92 ; la pause de PB-147 ; PB-145 avec mélangeur ; PB-165 côté AWE32 ;
   PB-160 f et h ; 0Eh de PB-153) : amender les entrées en « fidèle au matériel », et avec quel marqueur, puisqu'il
   n'y a rien à corriger ?
4. **Q4 — Les constats neufs N1 à N12** : les numéroter (PB-168 et suivants) avant G13, ou les traiter en élargissant
   des entrées existantes ? Le rattachement proposé est au tableau du § 6.
5. **Q5 — Des clés propres au mode matériel ?** Le port du MPU (300h, 330h, désactivé) ; l'IRQ et les DMA d'usine de la
   SB 16 et de l'AWE32 (5, 1, 5) au lieu de 7, 1, 5. Ou un mode matériel sans clé neuve, dans l'esprit de la décision
   n° 6 de G12 ?
6. **Q6 — Les artefacts de l'émulateur** (le FIR de la SB 16, PB-155 ; la répétition de 44,1 à 48 kHz, PB-160 m ; le
   modèle de `speakval`, PB-21) : relèvent-ils du mode matériel, ou de G16 (robustesse et confort) ?
7. **Q7 — Des mesures sur de vraies cartes sont-elles possibles ?** Une SB 1.0, 2.0, Pro, 16 (DSP 4.05) et une AWE32
   (DSP 4.13), avec de quoi enregistrer leur sortie. Sinon, les parts (c) restent reproduites indéfiniment. Liste
   courte des mesures les plus utiles :
   - 80h et 81h à plusieurs bits, ou à 0 ;
   - l'ACK de FFh en UART ;
   - les bits hauts de 82h ;
   - la valeur de 08h sans CSP ;
   - les délais consommés de l'EMU8000.
8. **Q8 — Le marqueur et R2.** « fixed in hardware mode », alors que le mode PCem reproduit toujours : faut-il écrire
   les deux (« reproduced; fixed in hardware mode: PB-nn ») ? Le code du mode matériel compte-t-il dans R2, ou vit-il
   dans des fichiers à part, les sites n'en gardant qu'une ligne d'appel (R4 : appels statiques simples) ?
9. **Q9 — PB-150 (non reproduit, R9).** En mode matériel, la fréquence 0 doit-elle suivre le micrologiciel (~4,9 kHz,
   N8) plutôt que la garde à 1 Hz ?
10. **Q10 — Les témoins.** Installer le logiciel de la disquette SB Pro 2 (INSTALL.EXE) sur une copie de disque, pour
    VPLAY et VEDIT2 (ADPCM : PB-90, 91, 146), VREC (PB-148) et CDPLYR ?

---

## Annexe — les comptes

| Classement principal | PB | Nombre |
|---|---|---:|
| (a) corrigeable et vérifiable | 90, 91, 145, 146, 147, 149, 151, 155, 156, 159 | 10 |
| (b) corrigeable, vérification faible | 21, 148, 162, 163 | 4 |
| (c) inconnu, à mesurer | aucun en entier ; des parts de 153, 154, 158 et 160 | 0 |
| (d) à ne pas corriger | 92 (fidèle), 164 (sans effet), 165 (fidèle côté AWE32), 167 (sans objet) | 4 |
| mixtes | 153 (a/b/c/d), 154 (a/c), 158 (a/b/c), 160 (a/b/c/d) | 4 |
| **Total** | 15 de la section A, 6 de la B, 1 de la C | **22** |

Sondes de l'oracle touchées en mode matériel (à garder intactes en mode PCem) :
- **la sonde du son, 62 champs** (9 du haut-parleur, 2 × 6 de l'OPL, 41 de la SB) : PB-21, 90, 91, 145-149, 151, 153,
  154, 155, 156 et 165 ;
- **la sonde du DMA, 29 champs** : PB-91, 146, 148, 149, 151 et la traduction de PB-153 ;
- **la sonde de l'EMU8000, 72 champs** : PB-158, 159, 160, 162 et 163.
