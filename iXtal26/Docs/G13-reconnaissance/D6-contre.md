# G13 — D6, la carte mère et les périphériques : contre-lecture

Contre-lecture de `D6-carte-mere.md`, le 6 octobre 2026, au commit `bc609ce`. Lecture seule : aucun
fichier du dépôt modifié, rien de construit ni lancé. Les ROM du dépôt ont été lues en copie
temporaire, effacée depuis : moitiés paires et impaires réentrelacées comme le fait `mem_bios.c`,
puis désassemblées en 8086 (`objdump -m i8086`). Les adresses sont données en `F000:xxxx`, telles que
la machine les voit.

**Niveaux** — ceux du rapport. *Documenté (ROM)* : les octets du BIOS du dépôt, désassemblés ; c'est
la preuve primaire de ce que fait CE BIOS, pas de ce que fait le matériel.

**Sources ajoutées** (en plus de celles du § 10 du rapport) :
- S1 — IBM, *Personal System/2 Model 25 Technical Reference*, 84X0672, juin 1987 (texte : `g13/src/m25.txt`).
- S2 — ROM du dépôt : `roms/ibmat/62x0820.u27`+`62x0821.u47` (BIOS du 15/11/85), `roms/ami286/amic206.bin`
  (15/10/90), `roms/ami386/ami386.bin` et `roms/ami386dx/opt495sx.ami` (06/06/92), `roms/ami486/ami486.bin`
  (04/04/93), `roms/ibmxt/xt.rom` (09/05/86), `roms/ibmpc/pc102782.bin` (27/10/82),
  `roms/olivetti_m24/…1.43_low/high.bin`, `roms/pc1512/40043.v1`+`40044.v1`.
- S3 — 86Box, `src/device/mouse_ps2.c` (descendant de PCem ; secondaire) —
  https://raw.githubusercontent.com/86Box/86Box/master/src/device/mouse_ps2.c
- S4 — Amstrad, *PC1512 Technical Reference Manual*, Section 1, § 1.5, 1.5.1, 1.7.2 (transcription
  J. Elliott) — https://www.seasip.info/AmstradXT/1512tech/section1.html

---

## 1. Confirmé

**C1. PB-95 : l'entrée du registre est fausse, le rapport a raison.** *Documenté.*
- L'octet d'état d'E9h met le gauche en bit 2, le droit en bit 0 ; le bit 1 est « Reserved » chez IBM.
  Trois textes IBM concordent : 15F0306, INT 15h C2h AL=06h BH=00h « Status byte 1 », p. 2-97 ;
  la même disposition pour l'ABIOS, fonction 03h, mot à l'offset 12h, p. 6-118 ; S1, INT 15h C2h,
  p. 5-48. Ce sont trois descriptions de l'interface du BIOS : que l'octet brut de la souris soit le
  même est *déduit* (le BIOS relaie les trois octets d'E9h).
- Secondaires concordants : Brouwer (« Bit 2: left, Bit 1: middle, Bit 0: right ») ; S3, qui code
  exactement la correction proposée (gauche → 4, droit → 1, milieu → 2 pour l'Intellimouse seule).
- PCem : `mouse_ps2.c:79-84` met le gauche en bit 0, le droit en bit 1, le milieu en `|= 3`.
  L'effet élargi décrit par le rapport (gauche lu « droit », droit lu « milieu/réservé », milieu lu
  « droit + milieu ») est juste.

**C2. PB-03 : les −5,87 ppm viennent de `pc.c:473`, pas de PB-11.** *Documenté + calcul.*
- `cycles_to_run = cpu_get_speed() / 100` : 4 772 728 / 100 = 47 727,28, tronqué à 47 727.
  1 − 47 727 / 47 727,28 = 5,867·10⁻⁶. Sur cent tranches, 4 772 700 cycles au lieu de 4 772 728.
- Le budget se ferme : −5,87 − 53,9 = −59,77 ppm, contre −59,79 ± 3,3 mesurés
  (`VERIFICATION.md:545`, `:554-556`). `TimerCheck.cs:77-82` impute déjà la troncature à `pc.c:473`.
- PB-11 (`808x.c:74`, `memcycs` d'une lecture mot auto-référentielle) n'a aucun rapport avec le budget.
  L'erreur date de la création du registre (`git show 3b1db78`). Le domaine UC l'a relevée aussi
  (`D1-uc.md:96-98`).

**C3. PB-07 : neutralisé des deux côtés, sans marqueur.** *Documenté.*
- Oracle : `tools/oracle/harness.c:562-566`, `calloc(n + 4, 1)`. C# : `Memory/mem.cs:1417`,
  `new byte[mem_size * 1024 + 4]`. Le registre cite encore `harness.c:99` et `mem.cs:706` (périmés),
  comme le registre des omissions de `TRANSCRIPTION.md`.
- L'entrée n'a pas de ligne « Reproduit » ; elle se dit « seule exception à la règle ».
- `PLAN.md:347` range 07 parmi « les 14 PB reproduits » de la section B. C'est faux, comme pour 17
  (« non reproductible »). `plan-tete.md` reprend ces 14.
- Le commentaire de `mem.cs:1414-1416` n'est d'aucune des sortes de R1 : il lui faut un `// DEVIATION:`.
- « Seule déviation de l'oracle » est périmé : `harness_stubs.c:196` (`rom_init`, PB-24),
  `harness.c:2075-2079` (le `nvrram` de la M24 mis à zéro), le fil du S3 (`TRANSCRIPTION.md`).

**C4. Le tableau des marqueurs (§ 3.1) est exact.** *Documenté.* Sept sites échappent au grep :
`808x.cs:280-291` (PB-03, aucun marqueur) ; `mem.cs:1414-1417` (PB-07) ; `pic.cs:70`, `:163`, `:433`,
`device.cs:327`, `mem_bios.cs:284` (marqueur sans identifiant). R1(d) doit être amendée ;
`TRANSCRIPTION.md` fait 218 lignes sur 240.

**C5. N1 tient.** *Documenté.*
- `pic.c:359` teste la cascade dès `c = 0`, indépendamment de `c` ; C# `pic.cs:421`.
- Le vrai ordre est IR0 > IR1 > IR2 (8 à 15) > IR3…IR7 : 8259A p. 15 (« IR0 the highest priority »)
  et IBM AT TR p. 1-10/1-11 (liste « in decreasing priority »).

**C6. Le top d'horloge perdu tient.** *Déduit du C.*
- Sur un AT, le cœur 286+ accepte sur `pic_intpending` (`386.c:249`), puis `picinterrupt` descend
  dans l'esclave (N1), sert IRQ 8, et `pic.c:369` efface le bit 0 du maître (PB-05) : l'IRQ 0 est
  perdue.
- Conditions : IRQ 2 démasquée au maître, IRQ 8 démasquée à l'esclave, une interruption de la RTC
  autorisée (registre B : PIE, AIE ou UIE ; `nvr.c:81-86`, `:110-126`), et les deux demandes dans
  l'IRR au même INTA (section CLI, ou gestionnaire plus prioritaire en cours).
- Fréquence dans les charges du dépôt : *inconnu*. Sous DOS, la RTC n'interrompt pas sauf
  INT 15h AH=83h/86h.

**C7. N2 tient.** *Documenté.*
- Fautif : `808x.c:55` (`IRQTEST`) et `:3985` (`takeint`), `pic.c:356` (`temp` sans `mask2`) ;
  C# `808x.cs:700`, `:3826`.
- Vrai comportement : 8259A p. 15 (« all further interrupts of the same or lower priority are
  inhibited ») et p. 18 (« in the normal nested mode a slave is masked out when its request is in
  service »).
- Le scénario du 5150 tient (ROM) : l'INT 08h du BIOS du 27/10/82 commence par `STI` en `F000:FEA5`
  et n'envoie l'EOI qu'en `FEE5-FEE7`, après `INT 1Ch`.
- Le cœur 286+ respecte `mask2` à l'acceptation (`386.c:249`). N2(a) est donc propre au 808x, et
  N2(b) ne se manifeste qu'à travers N1.

**C8. « HOT TIMER 1 OUTPUT » tient, sur l'XT du dépôt.** *Documenté (ROM).*
- `xt.rom` (09/05/86), `F000:E141-E15D` : mode 58h du canal 0, `OUT 08h,00h`, `OUT 0Ah,00h`,
  `OUT 41h,12h`, puis `IN AL,08h / AND AL,10h / JZ / HLT`.
- Même code dans le listing (XT TR 6361459, p. 5-30). Un modèle naïf des bits de requête arrête ce POST.

**C9. Le reste du domaine est confirmé.** *Documenté*, sauf mention contraire.
- PB-157 : `:345` devient `:349`. Les bits de requête manquent sur les deux 8237 (`dma.c:83-85`,
  `:344-347`). Le registre de commande s'efface au Reset et au Master Clear (8237A p. 7, p. 9).
- PB-06 : aucun effet ; à classer en C (*déduit*). PB-13 : aucun effet (`pic_updatepending`
  recalcule). PB-08, PB-10 : (d).
- PB-101 : `lpt.c:166` retire en 379h, les gestionnaires de LPT2 sont en 278h (`lpt.c:144`).
- PB-103, PB-104 : mécanismes conformes au C (`joystick_*.c`, `pc.c:799-804`).
- PB-33 : l'oracle n'appelle jamais `savenvr` (seulement `loadnvr`, `harness.c:2080`).
- Chemins chauds : PB-03 logeable dans la branche rare ; PB-07 sur chaque accès mot ; N2 dans
  `IRQTEST`, testé à chaque itération des REP (`808x.c:971-1105`).
- N9 : `keyboard_at.c:239-241`. N4, N5, N7 : `dma.c` comme cité.

---

## 2. Corrigé

**K1. N6 n'est PAS bloquant : le 8237 haut n'est pas désactivé après le POST.**
*Documenté (ROM, et listing de 1984).* Les cinq BIOS AT du dépôt réécrivent **00h** en 08h **et** en D0h
après leurs master clear :

| BIOS (S2) | 04h en 08h et D0h | master clear 0Dh / DAh | 00h en 08h et D0h | D2h ← 0, D4h ← 0 |
|---|---|---|---|---|
| ibmat 15/11/85 | `F000:01C5-01CB` | `0221` / `025F` | **`02A6-02AB`** (`2A C0 E6 08 E6 D0`) | `1400-1407` |
| ami286 15/10/90 | `C476-C482` | `C46E` / `C474` | **`C4E8-C4F4`** | `C4FD-C505` |
| ami386, ami386dx 06/06/92 | `E2CE-E2DA` | `E2C6` / `E2CC` | **`E361-E36D`** | `E376-E37E` |
| ami486 04/04/93 | `E2CE-E2DA` (et `D98C`, point 11h) | `E2C6` / `E2CC` | **`E361-E36D`** | `E376-E37E` |

- Le listing de 1984 du même AT dit la même chose : « SET DMA COMMAND … CONTROLLER ENABLE … SAME TO
  SECOND CONTROLLER », après le test 07 (AT TR 1502494, listing TEST1, p. 5-37). Le rapport avait
  arrêté sa lecture au master clear (p. 5-36/5-37).
- Preuve indirecte : en mode PCem, le master clear n'efface pas `dma_command` (`dma.c:153-156`), et
  `dma_channel_read` refuse tout si son bit 2 est posé (`:503-505`). La disquette de l'ibmat ne
  marcherait pas sans ce 00h.
- Conséquences :
  - PB-157 (la commande rangée) se corrige SEUL sans risque pour ces cinq BIOS ;
  - N6 reste un défaut documenté (8237A p. 9), mais son lien est faible et facultatif ;
  - à retirer : la puce 2 du § 1, le « lien fort » de N6 (§ 3.3) et le premier tiret du risque 1 (§ 8).
- « Le PIC et le DMA ne se corrigent pas entrée par entrée » ne vaut plus que pour le PIC.

**K2. Les attendus de DMABANC sont faux sur deux lignes.** *Documenté.*
- « master clear en DAh → le DMA repart » : non. Le master clear POSE les quatre masques (8237A p. 9 ;
  PCem aussi, `dma.c:419`). Le DMA ne repart qu'après un démasquage par D4h : canal 5, et canal 4 si
  N8 est modélisé.
- « écriture en DCh → masques 5-7 effacés » : ce sont les quatre masques, 4 à 7 (8237A p. 9,
  « Clear Mask Register »).

**K3. Les lectures « illégales » ne se limitent pas à DCh et DEh.** *Documenté.*
- La figure 6 (8237A p. 9) rend illégales les lectures de 9h, Ah, Bh, Ch, Eh et Fh.
- PCem rend le dernier octet écrit pour TOUTES : 09h-0Ch, 0Eh, 0Fh au bas (`dma.c:91`),
  D2h-D8h, DCh, DEh au haut (`dma.c:349`). Seul 0Dh rend 0 (`:87-88`).
- Une convention (Q5) doit couvrir les deux contrôleurs, sinon le mode matériel les rend asymétriques.
- L'effet écrit par le registre et repris au § 2 (« DEh ne rend pas le masque ») n'est pas une vérité
  matérielle : la lecture de Fh est illégale, et l'AT ne documente DEh qu'en écriture
  (AT TR p. 1-14, « Write All Mask Register Bits »).

**K4. PB-03 ne concerne vraiment que le 5150 et l'XT.** *Documenté (C).*
- Seul `xt_init` branche le rafraîchissement sur la DMA (`model.c:205`). `ams_init`
  (`model.c:259-270`) et `olim24_init` (`:292-300`) ne le font pas.
- Sur la M24 et le PC1512, PB-03 ne mord que pendant une DMA de périphérique (la disquette) : effet
  faible, non mesuré.
- `--timer-check` n'y discrimine pas les deux modes à l'invite. « 3 × 47 727 par tranche » ne vaut
  qu'à 4,77 MHz ; `TimerCheck` calcule l'attendu de chaque machine :
  - PC1512 (8086/8, `cpu_tables.c:63-66`) : 80 000 × 14 318 184 / 8 000 000 ≈ 143 181,8 ;
  - M24 du dépôt (`cpu = 0`, soit 8086/7,16, `cpu_tables.c:53-55`) : 71 590 × 2 = 143 180.

**K5. PB-95 : les pages précises.** *Documenté.* L'octet d'état est en p. 2-97, pas « 2-96/2-97 ».
AL=05h (état après initialisation) est en p. 2-97 ; AL=01h commence en p. 2-95, son état est en p. 2-96.
Le mot d'état de l'ABIOS est en p. 6-118 (la fonction commence en 6-117). Les bas de page font foi :
« See Return for (AL) = 00H on page 2-95 ».

**K6. PB-03 : la TR du 5150 est une source faible.** *Documenté.*
- « four clocks » est en p. 2-4, « every 72 clocks … five clocks » en p. 2-8 (pas 2-3/2-4).
- La même p. 2-4 inverse les compteurs : le 0 pour le rafraîchissement, le 1 pour l'heure.
- Le listing (« START TIMER 1 ») et la ROM font foi.

**K7. PB-05 : le tableau des victimes est à corriger.**
- La 1542C du dépôt est en IRQ 10, pas 11 (`ixtal26-486-scsi.cfg:13`). Sa victime est le bit 2 du
  maître (la cascade), que `pic_updatepending` recalcule : aucune perte. *Déduit du C.*
- Ligne manquante, la plus proche du dépôt : IRQ 13 (le coprocesseur) contre IRQ 5. L'IRQ 5 est
  celle de la SB Pro v2 des profils 386 et 486 (`ixtal26-486.cfg:49`, `ixtal26-386.cfg:51`).
  Dans PCem, IRQ 13 ne part que sur une division par zéro démasquée (PB-59). *Déduit.*

**K8. La perte d'un top ne demande que PB-05 et N1.** *Déduit du C.* N2 n'y est pour rien : la phrase
« Ensemble, ils… » du § 1 l'y inclut à tort. Les conditions sont celles de C6.

**K9. Le renvoi « p. 19 » de PB-05 n'est pas littéral.** *Documenté.* La p. 19 (Cascade Mode) ne dit pas
que le maître ne remet que l'IRR de l'esclave. C'est la p. 7 qui fonde la correction : « the highest
priority ISR bit is set and the corresponding IRR bit is reset ».

**K10. Le blocage de N8 est documenté, et pas seulement déduit.** *Documenté par composition.*
- L'esclave passe par DREQ/DACK d'un canal du premier contrôleur (8237A p. 5-6, « Cascade Mode »).
- Un masque inhibe le DREQ (p. 8), le bit 2 de la commande désactive le contrôleur (p. 7-8).
- Côté AT : le canal 4 sert de cascade (AT TR p. 1-13) ; un maître de bus passe par un canal en
  cascade (AT TR p. 1-26, « -MASTER »).

**K11. N10 ne tient pas comme défaut du mode matériel.** *Déduit.*
- −5,87 ppm, c'est un rythme d'hôte, invisible de l'invité : l'horloge de la RTC est interne quand
  `enable_sync = 0` (registre des omissions).
- C'est sous la tolérance d'un quartz.
- L'écart dépend de la machine : nul quand la vitesse est un multiple de 100 Hz (286/6, 8086/8 du
  PC1512), −12,85 ppm sur la M24 du dépôt (7 159 092 / 100 = 71 590,92).
- À citer seulement pour corriger l'imputation de PB-03.

**K12. N11 touche les deux chemins.** *Déduit du C.* Le chemin lent `readmemwl(s + a)` lit aussi
`s + a + 1` (`mem.c:484-522`), pas seulement le chemin rapide. Le domaine UC le couvre déjà sous
PB-87 (`D1-uc.md:60`, `:112-115`).

**K13. L'effet de PB-101 sur le PC1512 n'est plus « inconnu ».** *Documenté (ROM) ; effet déduit.*
- Le BIOS (40043/40044 v1) pose LPT1 = 378h sans sonde (`F000:C8D0`). Il sonde ensuite 3BCh puis
  278h par AAh/55h sur le registre de données (`C8D6-C8FA`, `MOV DX,0278h` en `C8F5`). S'il trouve,
  il range le port en 0040:000A et ajoute 40h à 0040:0011.
- `lpt2_read` rend `lpt2_dat` (`lpt.c:130-139`) : la sonde réussit.
- Attendu en mode PCem : LPT2 = 0278h au BDA et une imprimante de plus. LPTBANC le mesurera.

**K14. PB-103 n'est pas une correction de l'hôte (Q3 l'y range).** *Documenté.*
- C'est du code de périphérique transcrit (`joystick_ch_flightstick_pro.c`, `joystick_tm_fcs.c`),
  exécuté par l'oracle : `bd-pc-joy-ch-banc` et `-tm-banc` injectent 315° (`series.sh:318-327`).
- Il ne peut se corriger que sous l'interrupteur matériel.
- Précédent à trancher en Q6 : le contrôle négatif du registre a corrigé la TM par `<= 315`, donc
  315° → gauche (`PCEM_BUGS.md`, PB-103). Le rapport propose `>= 315`, donc 315° → haut.

**K15. PB-104 et PB-33 sont traités de façon incohérente.** *Documenté.*
- Les deux sont des chemins de l'hôte que l'oracle n'atteint jamais :
  - `load_joysticks` ne lit les correspondances que si `plat_joystick_nr ≠ 0` (`pc.c:790`) ;
    l'oracle n'a pas de manette d'hôte ;
  - `savenvr` n'est jamais appelé par l'oracle.
- Le rapport classe pourtant PB-104 en (a) « en mode matériel » et PB-33 en (d) avec une correction
  dans les deux modes.
- Ni l'un ni l'autre n'a de vérité matérielle : ils demandent la même décision (Q2 et Q3 à fusionner).

**K16. PB-07 : les deux points « à vérifier » se ferment.** *Documenté (C).*
- `timing_misaligned` vaut 0 sur le 8088 et le 8086 (`cpu.c:314`, `:319-321`) : le chemin lent ne
  coûte rien de plus.
- Seule nuance : `addreadlookup` débite 9 cycles quand la page n'est pas en cache (`mem.c:379` ;
  `TRANSCRIPTION.md` dit `:378`).
  C'est le cas du repli à 1 Mo sur la RAM plate, pas du haut de RAM, dont la page est déjà en cache.

**K17. PB-07 : le corpus SST ne vérifiera probablement rien.** *Déduit.* Le domaine UC conclut que le
générateur évite les franchissements de FFFFh (`D1-uc.md:112-115`). Seul le test dirigé reste.

**K18. L'« IRQ 7 fantôme » n'est pas un comportement inconnu (Q5).** *Documenté.*
- La fiche le décrit : vecteur d'IR7 sans bit d'ISR (8259A p. 7 et p. 18).
- Ce qui est inconnu, c'est si PCem atteint la situation : `picinterrupt` rend FFh (`pic.c:393`),
  que `808x.c:3968` et `386.c:251` ignorent.

**K19. Il n'est pas vrai qu'« aucun listing » AMI ne se lise.** *Documenté (ROM).* Les ROM AMI du dépôt ne
sont pas compressées, et leurs séquences DMA et PIC se lisent directement (K1, A3, A8). Les témoins
restent nécessaires ; la lecture des ROM réduit le risque 2 (§ 8) avant tout code.

**K20. L'exécuteur de bancs en C# seul existe en partie.** *Documenté.*
- Déjà là : `--boot` avec `--type`, `@wait`, `@shot`, `@souris` (`BootTest.cs:89-176`,
  `KeyScript.cs:142-183`).
- Manquent : l'injection de manette (`--joy-at` est à iXtal26.Diff seul), `--force-ps2` (iXtal26.Diff
  seul, `VERIFICATION.md` § PS2.1) et la comparaison à un fichier d'attendus.
- C'est à étendre plutôt qu'à écrire.

**K21. Le scénario 2 de PICBANC repose sur une commande que l'IBM AT ne documente pas.** *Documenté.*
- La liste du 8042 de l'AT (AT TR p. 1-42 : C0h, D0h, D1h, E0h, F0h-FFh) n'a pas D2h. D2h n'est
  documentée que pour le contrôleur PS/2 (HITR Common Interfaces, chapitre « Keyboard/Auxiliary
  Device Controller », p. 13).
- Sur l'ibmat, il faut lever l'IRQ 1 par une frappe injectée. Sur les AMI, D2h dépend du
  microprogramme de leur 8042, qui n'a pas été lu : *inconnu*. Côté SB : la SB 16 n'a pas de clé d'IRQ
  (`ixtal26-486-sb16.cfg:26-28`, il faut le registre 80h du CT1745) ; la SB Pro v2 a `irq =`.

---

## 3. Ajouté

**A1. PB-33 : la réponse à la question posée.**
- **Pas une violation de R9 par la lettre.** *Documenté.* R9 vise un défaut « par lequel le code invité
  arrête… l'émulateur » (`TRANSCRIPTION.md`, R9). Ici le déclencheur est l'environnement de l'hôte :
  un `nvr_path` non inscriptible, que l'invité ne peut pas atteindre. C'est un fichier non vérifié
  (`nvr.c:50-52` rend NULL ; `fwrite` et `fclose` en `:780-781`). Le registre et le rapport citent
  `:770-772`, qui tombent sur `case ROM_VS440FX` : le renvoi est à corriger en D6.0.
- **Mais l'effet est plus grave que « le CMOS perdu ».** *Déduit.* `savenvr` est appelé AVANT
  `pc.closepc()` (`BootTest.cs:64-65`, `SdlHost.cs:1356-1357`). L'exception saute `closepc`, que
  `SdlHost.cs:1344-1348` désigne comme le SEUL endroit qui vide les tampons d'écriture des images.
  La dernière écriture disque ou disquette de la session peut donc être perdue.
- Conclusion : une correction d'hôte dans les deux modes se défend (inerte pour l'oracle), mais elle
  demande une décision de règle (R8/R9), à prendre avec PB-104 (K15). Le test se fait sur un
  répertoire temporaire, jamais `nvr/`.

**A2. La M24 lit aussi le bit de requête du canal 0.** *Documenté (ROM).*
- BIOS 1.43, `F000:DC41-DC62` : rafraîchissement programmé (mode 58h, `OUT 08h,00h`, `OUT 0Ah,00h`,
  compteur 1 en mode 2, 13h), puis `IN AL,08h / TEST AL,10h / JNZ` vers une erreur. C'est un second
  témoin pour les bits de requête.
- À l'inverse, le 5150 (27/10/82) ne lit jamais l'état du 8237 (aucun `IN AL,08h` dans
  `pc102782.bin`) : il ne témoigne de rien pour (d).

**A3. N12, nouveau : ICW1 ne remet pas la lecture sur l'IRR.** *Documenté.*
- Fiche : « Status Read is set to IRR » (8259A p. 10, point e) ; « After initialization the 8259A
  is set to IRR » (p. 17).
- PCem : l'ICW1 (`pic.c:106-113`, `:214-221`) ne touche pas `read` ; `pic_reset` pose `pic.read = 1`,
  c'est-à-dire l'ISR (`:36`), et ne touche jamais `pic2.read`.
- Effet : une lecture de 20h sans OCW3 après l'initialisation rend l'ISR au lieu de l'IRR.
  - Dans les ROM du dépôt, les lectures de 20h repérées suivent toutes un OCW3 : XT `F000:E036-E044`,
    AT `F000:1BD8`, M24. Le 5150 ne lit jamais 20h. *Documenté (ROM).*
  - Des lectures de A0h dans les ROM AMI restent à examiner ; l'effet sur les logiciels est *inconnu*.

**A4. N13, nouveau : ni la M24 ni le PC1512 n'ont de rafraîchissement par DMA.** *Documenté ; effet déduit.*
- Les deux machines rafraîchissent par le canal 0, demandé par la sortie 1 du 8253 :
  - PC1512 : S4, § 1.5, 1.5.1, 1.7.2 (15,13 µs) ;
  - M24 : sa ROM programme le rafraîchissement (A2).
- PCem ne branche `pit_refresh_timer_xt` que dans `xt_init` (`model.c:205`). Ces deux UC tournent
  donc sans le vol de cycles du rafraîchissement : plus vite que le vrai.
- C'est le pendant de PB-03, pour la fidélité du temps.

**A5. N14, nouveau : le coût du cycle DMA est débité même quand le transfert est refusé.** *Documenté ; effet déduit.*
- `refreshread()` est appelé avant les tests de masque et de mode (`dma.c:511-517`, `:579-585`).
- Sur le vrai 8237A, un canal masqué ignore son DREQ (p. 8) : pas de cycle de bus.
- Effet : du temps facturé à tort sur les machines non AT, quand le canal 0 est masqué ou pas encore
  programmé, et à chaque tentative refusée d'un périphérique.
- Lien : corriger PB-03 seul porterait ces cycles fantômes au TSC. N14 va avec PB-03.

**A6. N9 vaut aussi pour la file du clavier.** *Documenté.*
- `key_queue` (`keyboard_at.c:233-234` ; C# `keyboard_at.cs:302-303`) et `key_ctrl_queue`
  (`keyboard_at.c:153` ; C# `:262`) n'ont pas de garde non plus.
- Le clavier de l'AT documente son tampon : 16 codes, le 17e remplacé par 00h (« overrun »). Les
  seize codes stockés partent normalement, seuls les suivants sont perdus (AT TR p. 4-3,
  « Keyboard Buffer »).
- PCem : au 17e octet non lu, la file paraît vide et les 16 sont perdus.

**A7. N8 s'étend aux maîtres de bus.** *Documenté ; effet déduit.* La 1542C écrit et lit la mémoire
directement (`scsi_aha1540.c:411` et suivantes, `mem_readb_phys`). Le masque et le mode cascade de son
canal 7, et la commande du 8237 haut, sont ignorés. Un modèle de cascade limité aux canaux 0 à 3
laisserait cet écart.

**A8. N3 vaut aussi pour l'esclave, et OCW2 40h (« no operation ») fait un EOI.** *Documenté.*
- `pic2_write` a le même OCW2 et le même OCW3 (`pic.c:222-244`).
- OCW2 40h tombe dans l'EOI non spécifique (`pic.c:126-145`), comme les rotations et le « set
  priority ».
- Aucun BIOS du dépôt n'émet ces formes : le relevé des OUT immédiats en 20h et A0h ne trouve que
  20h, 60h-67h, 0Ah, 0Bh, 11h et 13h. Le 80h écrit en A0h sur les machines XT vise le masque NMI,
  pas un 8259. *Documenté (ROM).*

**A9. Une question de périmètre oubliée : PB-07.**
- Neutralisé, PB-07 est de la famille de PB-24 (« non reproduit »). Or `PLAN.md` § G13 dit que les
  non-reproduits « sont déjà réglés ».
- Lui donner un comportement matériel est donc un ajout au périmètre de G13 : c'est à Julien de le
  décider.

**A10. Ce qui manque aux étapes D6.0 à D6.5.**
- D6.1 mêle des corrections d'hôte, inertes pour l'oracle (PB-33, PB-104), à des corrections de
  périphérique (PB-101, PB-103). Selon la règle des séries, les premières n'appellent qu'un
  sous-ensemble ciblé de moins de 15 min, les secondes une série entière. À scinder.
- D6.2 : sans N6 obligatoire (K1), l'étape tient en PB-157 (la commande, DAh), plus N4, N7 et N8 selon Q1.
  - Témoins à ajouter : le POST de la M24 (A2). Le 5150 n'en est pas un.
  - DMABANC est à faire tourner aussi sous l'oracle en mode PCem, pour figer la reproduction.
- D6.3 touche `IRQTEST`, un chemin chaud : il lui faut la mesure de `tools/perfbanc` (avant et après,
  dans les deux modes). Le rapport ne la prévoit qu'en D6.4.
- D6.4 : PB-03 avec N14, et N13 si Q1 l'admet ; `--timer-check` sur le 5150 et l'XT. La M24 et le
  PC1512 seulement avec une DMA de disquette dans la fenêtre (K4).
- D6.5 « avec D6.4 » contredit « une étape = un commit après sa série » : fusionner les deux étapes
  ou donner une série à D6.5.

**A11. Les questions qui manquent pour Julien.**
1. Les octets d'une ROM du dépôt valent-ils « documenté » pour ce que fait ce BIOS ? (K1, K13, A2, C8)
2. N13 (le rafraîchissement de la M24 et du PC1512) entre-t-il dans le mode matériel ? Il change le
   temps de ces deux machines.
3. PB-33 et PB-104 : une seule décision pour les défauts d'hôte inertes pour l'oracle (deux modes, ou
   interrupteur « hôte »). PB-103 n'en fait pas partie (K14).
4. PB-07 entre-t-il dans G13 alors qu'il n'est pas reproduit (A9) ?
5. Une seule convention pour toutes les lectures illégales du 8237, sur les deux contrôleurs (K3) ?
6. PB-103 : faut-il garder le précédent « 315° → gauche » du contrôle négatif, ou adopter « haut » ? (K14)

---

## 4. Classement par PB après contre-lecture

| PB | Rapport | Après contre-lecture | Ce qui change |
|---|---|---|---|
| PB-03 | (a) | **(a)**, 5150 et XT | Machines (K4) ; à corriger avec N14 (A5) ; marqueur ; imputation à `pc.c:473` (C2) |
| PB-05 | (a) | **(a)**, avec N1 (et N2 sur le 808x) | Source p. 7 (K9) ; victimes (K7) ; scénario 2 (K21) |
| PB-06 | (d) | **(d)**, section C | — |
| PB-157 | (a) commande et temporaire ; (c) DCh/DEh, requêtes | **(a)** commande et DAh, **sans dépendance à N6** ; **(c)** toutes les lectures illégales et les bits de requête | K1, K2, K3 ; témoin M24 (A2) |
| PB-94 | (b) | **(b)** | — |
| PB-95 | (a) | **(a)** | Pages (K5) ; S1 et S3 en appui (C1) |
| PB-101 | (a) | **(a)** | Effet prévisible par la ROM du PC1512 (K13) |
| PB-103 | (a), convention | **(a)**, convention, sous l'interrupteur matériel seulement | Pas « hôte » ; précédent `<= 315` (K14) |
| PB-104 | (a), hôte | **(d)** en mode matériel ; correction d'hôte à décider avec PB-33 | K15 |
| PB-07 | (a) | **(a)** pour le mécanisme (l'octet haut suit la projection mémoire, repli sur 20 bits) ; **(c)** pour la valeur du bus flottant | Statut « neutralisé » ; périmètre (A9) ; K16, K17 |
| PB-08 | (d) | **(d)** | — |
| PB-10 | (d) | **(d)** | — |
| PB-13 | (d) | **(d)** | — |
| PB-33 | (d), hôte dans les deux modes | **(d)** en mode matériel ; correction d'hôte hors R9 par la lettre, à décider avec PB-104 | Effet aggravé : `closepc` sauté (A1) |

---

## 5. Défauts non inscrits : la liste vérifiée

Ceux qui tiennent, avec le C fautif et la source du vrai comportement.

| N | Défaut | C de PCem | Vrai comportement | Tient ? |
|---|---|---|---|---|
| N1 | Cascade servie avant IRQ 0 et IRQ 1 | `pic.c:359` | 8259A p. 15 ; AT TR p. 1-10/1-11 | **oui**, documenté |
| N2 | Masque de service ignoré : (a) acceptation du 808x, même priorité comprise ; (b) descente dans l'esclave malgré l'ISR 2, par N1 | `808x.c:55`, `:3985` ; `pic.c:356` | 8259A p. 15 et p. 18 | **oui**, documenté ; (a) propre au 808x |
| N3 | OCW2 (rotations, set priority, 40h) traité en EOI non spécifique ; OCW3 poll et masque spécial ignorés ; maître et esclave | `pic.c:126-153`, `:222-244` | 8259A p. 13-16 | **oui**, documenté ; aucun usage dans les BIOS du dépôt (A8) |
| N4 | Clear Mask (0Eh, DCh) sans effet | `dma.c:94-162`, `:352-426` | 8237A p. 9 | **oui**, documenté ; inutilisé par les BIOS du dépôt (OUT immédiats) |
| N5 | Registre de requête (09h, D2h) ignoré | idem | 8237A p. 7 | **oui**, documenté ; le POST de l'AT écrit 00h en D2h (`F000:1402`) |
| N6 | Le master clear n'efface ni la commande ni l'état | `dma.c:153-156`, `:417-420` | 8237A p. 9 | **oui**, documenté ; **non bloquant** (K1) |
| N7 | `dma_reset` laisse les masques à 0 | `dma.c:43` | 8237A p. 2 et p. 8 | **oui**, documenté ; sans effet sur les BIOS du dépôt, qui font un master clear avant usage |
| N8 | Cascade non modélisée (canal 4, contrôleur haut) ; maîtres de bus hors `dma.c` | `dma.c:498-517`, `:568-585` ; `scsi_aha1540.c:411` | 8237A p. 5-8 ; AT TR p. 1-13, p. 1-26 | **oui**, documenté par composition (K10, A7) |
| N9 | Files du 8042 sans garde (souris, clavier, contrôle) | `keyboard_at.c:153`, `:233-234`, `:240-241` | PS/2 HITR Common Interfaces, « Auxiliary Device and System Timings », p. 14-15 du chapitre ; AT TR p. 4-3 | **oui**, documenté, élargi (A6) |
| N10 | Troncature `cpu_get_speed() / 100` | `pc.c:473` | — | **non** comme défaut du mode matériel : rythme d'hôte, invisible de l'invité (K11) |
| N11 | Mot à l'offset FFFFh lu en `s + 10000h`, chemins rapide ET lent | `808x.c:73-80`, `:105-111` ; `mem.c:484-522` | repli dans le segment (domaine UC) | **oui**, déjà au domaine UC sous PB-87 (K12) |
| N12 | ICW1 ne remet pas la lecture sur l'IRR ; `pic_reset` la met sur l'ISR | `pic.c:36`, `:106-113`, `:214-221` | 8259A p. 10 (e), p. 17 | **oui**, documenté ; effet inconnu (A3) |
| N13 | Pas de rafraîchissement par DMA sur la M24 et le PC1512 | `model.c:259-270`, `:292-300` (contre `:205`) | S4 § 1.5, 1.7.2 ; ROM M24 `F000:DC41-DC5A` | **oui**, documenté ; effet sur le temps déduit (A4) |
| N14 | Coût du cycle DMA débité avant les tests de masque et de mode | `dma.c:511-517`, `:579-585` | 8237A p. 8 | **oui**, documenté ; effet déduit, lié à PB-03 (A5) |
