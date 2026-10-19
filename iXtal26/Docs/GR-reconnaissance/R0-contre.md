# GR.0 — Les pixels : contre-lecture du rapport R0

Le 20 octobre 2026. Contre-lecture de `R0-pixels.md`, en lecture seule du dépôt (`master`, `d56d161`) : rien de
construit, rien de lancé. Le C, le C# et les outils ont été relus aux lignes citées ; les comptes ont été refaits par
des commandes (notées entre parenthèses). Seul ce fichier est écrit.

**Niveaux** : *documenté* (le texte du plan, du registre ou du code le dit en toutes lettres), *déduit* (raisonné sur
le code relu), *inconnu* (à mesurer). **Numérotation** : **C** = confirmé, **K** = corrigé, **A** = ajouté.

## Verdict

Le rapport est solide : son état des lieux (rien de GR.0 n'est fait, rien n'est caduc) est exact, ses citations sont
justes à quelques lignes près, ses trois étapes tiennent. Six corrections (K1 à K6), dont deux changent le texte à
écrire (la liste des polices que PCem écrit dans `fontdat`, la façon dont le mode matériel est refusé) ; neuf ajouts
(A1 à A9), dont un oubli de procédure (les portes neuves doivent être déclarées à `ModeOutils` et à `series.sh`) et un
défaut hors portée (le composite est offert par la boîte de configuration de la CGA). Sur ses trois décisions
ouvertes, aucune n'est vraiment ouverte : voir § 3.

## 1. Confirmé (C)

- **C1. `loadfont` et `cgapal_rebuild` sont vides dans l'oracle** — *documenté*, relu. `harness_stubs.c:466`
  (`cgapal_rebuild` : `(void)display_type; (void)contrast;`), `:616` (`loadfont` vide), `:467-470` (`fontdat`,
  `fontdatm` à zéro), `:452` (`initvideo` l'appelle), en-tête `:367-371` « Stubé à zéro … À REPRENDRE ». Aucun
  commit de GR depuis `d56d161` : `git status` ne montre que `GR-reconnaissance/` en non suivi.
- **C2. L'image EGA de l'oracle est déjà juste** — *déduit*, solide. `grep -n "cgapal\|fontdat\|loadfont"` sur
  `pcem-dev/src/video/vid_ega.c` : aucune occurrence. `pallook16`/`pallook64` sont des statiques bâties à l'init de
  la carte (`vid_ega.c:16`, `:940-993`), `vram` et les polices sont dans la carte (plan 2). Le M24 en revanche lit bien
  `cgapal` (`vid_olivetti_m24.c:152-181`) et `fontdatm` (`:186`, `:194`, `:221`).
- **C3. Les 113 valeurs de `cgapal_rebuild` sont identiques** — *documenté*. Recompté :
  `grep -c "cgapal\[.*\] = makecol"` rend 113 sur `video.c` et 113 sur `Video/video.cs` ; un script jetable
  (`re.findall(r'cgapal\[(\w+)\]\s*=\s*makecol\(([^;]*)\)\s*;')` sur les deux fichiers) rend deux listes égales
  (`a == b` vrai). Dans `video.c:1162-1298`, 113 lignes portent `cgapal[` et aucune n'est sans `makecol`. Les sept
  `display_type` sont bien 0 à 6 (`video.h:108-114`) : RGB, composite, RGB sans brun, vert, ambre, blanc, couleurs
  vraies ; composite et couleurs vraies tombent dans le `default` (`video.c:1275`). « Sept × deux contrastes » : juste.
- **C4. La CGA n'a pas de sonde** — *documenté*. `h_vga_probe` rend `out[0] = 0` sans carte SVGA ni M24, PC1512, MDA,
  Hercules, EGA (`harness.c:2241-2254`) ; `CompareVga` sort sans rien comparer quand `o[0] == 0 && c[0] == 0`
  (`BootDiff.cs:1015-1016`). Le rapport cite bien la ligne.
- **C5. Aucun hachage de `buffer32` hors VGA** — *documenté*. `grep -n buffer32 tools/oracle/harness_*.c` : le seul
  hachage est `harness.c:2264` ; côté C#, `vid_svga.cs:2105` (la seule occurrence de `Buffer32` dans `Probe`). Les
  sondes M24, PC1512, MDA, Hercules, EGA écrivent aux places 122 et suivantes sans toucher à la place 3 (relu pour
  la M24, `harness_m24.c:51-70`, et l'EGA, `harness_ega.c:59-101`).
- **C6. Le nom des champs et le compte du message vert** — *documenté*. `VgaProbe.Fields` compte 163 noms (script :
  extraction des chaînes du tableau), `Oracle.VgaProbeN = 163` (`Oracle.cs:367`) ; les places 122-142 sont nommées
  `m24.*` et 143-162 `pc1512.*` ; `CompareVga` imprime `VgaProbe.Fields.Length` pour tous les genres (`BootDiff.cs:
  1033-1050`) et nomme le champ divergent sans regarder le genre (`:1019-1024`).
- **C7. La VRAM de la CGA n'est pas effacée** — *documenté*. `vid_cga.c:427` : `cga->vram = malloc(0x4000);`, après
  un `memset` de la seule structure (`:418`). Les trois autres cartes sont effacées par `h_*_attach`
  (PLAN-G9.md:151-152,
  décision n° 4).
- **C8. Les clés `font` et `io_base`** — *documenté*. `vid_cga.cs:659` (`fontbase = 256` si `font != 0`), `:712-713`
  (la clé, valeur 0 par défaut) ; `vid_ega.cs:1288-1290` et `:1348` (`io_base`, 0x300 par défaut). Aucune des deux
  n'existe dans `vid_cga.c`/`vid_ega.c`. Aucun `.cfg` du dépôt ne les pose (`grep -rn "font *=\|io_base *="
  tools/gates/cfg` : vide ; seul `ixtal26.cfg` à la racine porte une clé de la famille `display_type`, sans effet
  ici).
- **C9. Les 163 champs, les genres, les places** — le tableau de § 1.2 du rapport est juste (genres 2 à 6, places
  122-142, 143-162, 122-138, 122-138, 122-151). Les vingt-deux champs proposés pour la CGA se retrouvent un à un dans
  `cga_t` (`includes/private/video/vid_cga.h:3-36`) : `crtcreg`, `crtc`, `cgastat`, `cgamode`, `cgacol`, `fontbase`,
  `linepos`, `displine`, `sc`, `vc`, `cgadispon`, `con`, `coff`, `cursoron`, `cgablink`, `vsynctime`, `vadj`, `ma`,
  `maback`, `oddeven`, `dispontime`, `dispofftime`, `timer`, `firstline`, `lastline`, `drawcursor`, `vram`,
  `charbuffer[256]`, `revision`, `composite`, `snow_enabled`.
- **C10. Les bancs qui reviennent au texte** — *documenté*. `hercbanc.py:206` (« retour au texte », puis écran
  effacé), `egabanc.py:237` (`MODE(0x0003)`). `pc1512banc.py:170-185` : `INT10(0x0006)` puis les plans ; avec
  `cgamode & 0x12 == 0x12` (`vid_pc1512.c:75, 118, 179`) l'écran final est bien le mode plan.
- **C11. Les six pannes proposées** : chaque ligne citée existe et porte l'instruction décrite (`vid_cga.cs:319`,
  `vid_mda.cs:413`, `vid_hercules.cs:448` et `:243`, `vid_ega.cs:1218-1219`, `vid_olivetti_m24.cs:261`,
  `vid_pc1512.cs:418`).
- **C12. Pas de rand dans les rendus** — `grep -n rand` sur `vid_cga.c`, `vid_ega.c`, `vid_mda.c`, `vid_hercules.c`,
  `vid_pc1512.c`, `vid_olivetti_m24.c` : aucune occurrence. La neige de la CGA (`snow_enabled`, défaut 1,
  `vid_cga.c:76, 88, 473`) dépend de l'état de la carte, donc du temps : déterministe si le diff d'instructions l'est.

## 2. Corrigé (K) et ajouté (A)

### Corrections

- **K1. Les formats de police de PCem n'écrivent pas tous « jamais dans `fontdat` »** (rapport § 1.1). Trois
  formats écrivent dans `fontdat` ou `fontdatm` : `FONT_PC200` (`video.c:961-974`, `fontdatm` et `fontdat`),
  `FONT_T3100E` (`:998-1021`, `fontdatm` et `fontdat`) et `FONT_SIGMA400` (`:1029-1039`, `fontdat` et `fontdatm`) ;
  `grep -n "case\|fontdat" video.c` sur 984-1047. Les quatre autres (WY700, MDSI, KSC5601, IM1024) écrivent ailleurs.
  Même inexactitude dans le commentaire de `video.cs:556-558` (« ne touchent jamais fontdat »). *Sans effet
  aujourd'hui* : `mem_bios.c:114, 127, 919` n'est atteint que par des machines non portées (PC200, T3100e), et le C#
  omet ces cases (`video.cs:550-554`). Mais la porte `video-tables-check` « pour chaque romset du dépôt » ne verra
  ces cas que le jour où un de ces romsets entre ; et la recopie verbatim dans l'oracle les compile. À écrire dans le
  commentaire du harnais, pas à corriger. *Documenté.*
- **K2. Le refus du mode matériel n'est pas que `BootDiff.cs:428-435`** (rapport § 1.8, risque 4). Ce bloc ne
  traite que la clé `hardware_mode` d'un `.cfg` (`loadconfig` refuse de changer le mode figé). Le refus de l'option
  `--hardware-mode` est `Program.cs:160-163` + `ModeOutils.Accepte` (`ModeOutils.cs:24-66`) : `boot-diff` n'est pas
  dans `Toujours`, la raison est « la commande compare le C# à l'oracle, qui est PCem », retour 2. Sans l'option ni
  la clé, l'outil reste en mode PCem (`ModeMateriel.Description` : `!Actif ? "PCem"`) ; le « tout » de l'utilisateur
  (`DefautUtilisateur`, `ModeMateriel.cs:292`) est posé par le lanceur, pas par l'outil de diff. *Documenté.*
- **K3. `MALLOC_PERTURB_=85` n'est pas la règle de la série** (rapport § 1.9 et risque 2 : « que la série remplit
  d'octets 0xAA »). `grep -rn MALLOC_PERTURB tools/gates tools/*.sh` : aucune occurrence ; il n'apparaît que dans
  `VERIFICATION.md` (§ G7.1 à G7.4, lignes 4744-4962) et `PCEM_BUGS.md` (7180, 7494) comme un réglage de preuve posé
  à certaines séries. Sans lui, la VRAM de la CGA contient ce que le tas rend (le plus souvent des zéros d'une page
  fraîche, parfois des restes) : l'image de la CGA serait *indéterminée*, pas 0xAA. La conclusion du rapport (effacer
  au montage, déviation de l'oracle) reste la bonne et gagne en force : sans effacement, la porte serait
  intermittente. *Déduit.*
- **K4. `@shot` existe déjà** (rapport, décision n° 2 : une empreinte « `@image` … comme `@shot` »).
  `BootTest.cs:117-121` : `@shot FICHIER` écrit déjà l'image `xsize × ysize` en PPM ; `@vram` (`:125-128`, G13.9g)
  couvre l'EGA, la MDA, l'Hercules et le socle SVGA mais *pas* la CGA, la M24 ni le PC1512. Un `@image` serait un
  hachage de ce que `@shot` écrit, non une fonction neuve. *Documenté.*
- **K5. Le coût : 88 portes et 1 470 s ne se refont pas exactement.** Le total de la série se refait : 764 portes,
  47 104 s (`g13b.tsv`, somme de la colonne 3 ; la série développée par `source series.sh` avec `run`/`runw`
  remplacés par un `printf` donne aussi 764 commandes). Mais la partie « non SVGA » dépend de la définition :
  - cartes nommées non SVGA (`--gfxcard` cga/mda/hercules/ega, ou `--model` olivetti_m24/pc1512) : **32 portes,
    346 s** (MDA 5, Hercules 6, EGA 8, M24 4, PC1512 5, CGA nommée 4), ce qui reprend exactement le tableau du
    rapport pour les cinq cartes à sonde ;
  - plus les boot-diffs de machines 8088/XT/AT sans carte nommée ni `.cfg` de 286 et plus (CGA par défaut : les
    8087, les bancs du son, de LPT et de manette sur le 5150, `bd-xt-xtide-format`, …) : **42 portes, 931 s** ;
  - total **74 portes, 1 277 s** (2,7 %), non 88 et 1 470 s. Le rapport ne dit pas quelles portes il a rangées en
    plus (un `.cfg` de 286+ sans carte nommée rend la carte par défaut du modèle, inconnue ici). L'ordre de grandeur
    (3 %) est confirmé ; le chiffre exact est à fixer par la liste des noms de portes dans l'étape, pas par une
    phrase. *Compté.*
- **K6. « ABI (58) »** (rapport § 2, GR.0a) : l'ABI du dépôt est 57 (`Oracle.cs:90`, `g13b.tsv` ligne 1 « ABI 57
  OK »). Le rapport veut dire « la prochaine » : à écrire 57 → 58, car la porte `abi` est la première de la série et
  son message change dans les 764 lignes de comparaison de M2 aux deux étapes qui touchent l'oracle. *Documenté.*

### Ajouts

- **A1. Les portes neuves doivent être déclarées au mode matériel** (oubli de procédure). `ModeOutils.cs:9-11` : une
  commande neuve qui compare à l'oracle est refusée (retour 2) tant qu'elle n'est pas inscrite ; `series.sh` doit
  la marquer `pcem_seul NOM RAISON` pour que la série sous « tout » (G13.10c) ne la compte pas rouge. Cela vaut pour
  `video-tables-check` (GR.0a) et pour chaque porte neuve de GR.0c. Dans les décomptes de M2/M0 de la clôture : une
  porte de plus parmi les « 228 refusées » (d56d161). Le rapport n'en dit rien.
- **A2. Le rapport ne coordonne pas GR.0c avec GR.1.** GR.1 (PLAN.md:497-501) étend déjà EGABANC (AND, OR), crée
  MDABANC et ET4BANC qui finit en 256 couleurs ; GR.0c (variantes d'EGABANC en 10h, d'HERCBANC en graphique) touche
  aux mêmes générateurs `.py`. L'ordre doit être dit : GR.0c d'abord (la sonde de l'image), puis GR.1 ajoute ses
  octets *dans* un banc dont l'image est déjà comparée — c'est ce que « Une panne rougit chaque sonde » demande. À
  l'inverse, étendre EGABANC en GR.1 puis le couper en GR.0c double le travail.
- **A3. La sonde et `h_closepc` : un piège de conception, sans danger tel quel.** `BootDiff.cs:670` appelle
  `Oracle.h_closepc()` *avant* `h_vga_probe` (`:678-682`). Pour une sonde de CGA qui lirait un `cga_t` libéré, ce
  serait un use-after-free. Il n'y en a pas : `h_closepc` (`harness.c:2638-2644`) ne ferme pas les cartes (le
  `device_close_all` fidèle a été retiré, PB-31, `harness.c:2607-2631`, justement parce que `cga_close` libère la
  carte sans retirer son `mem_mapping`). À écrire dans l'étape : `h_cga_probe` lit une structure qui reste vivante
  parce que ce retrait tient ; si un jour `h_closepc` redevient fidèle, la sonde de CGA doit passer avant.
  *Documenté.*
- **A4. Le composite est offert par la configuration de la CGA, et faux dans les deux modes.** `cga_config`
  propose `display_type` en `RGB`, `RGB (no brown)`, …, composite (`vid_cga.cs:703-704`), et le chemin composite est
  omis (`vid_cga.cs:484-487`) : un utilisateur qui choisit le composite voit des couleurs RGB, et en mode matériel
  la règle « faire ce que fait le vrai matériel » n'est pas tenue. C'est un candidat, hors de la portée de GR.0
  (voir l'annexe), mais le rapport doit le dire à côté du refus sous boot-diff : le refus protège l'outil, non
  l'utilisateur. *Documenté.*
- **A5. Le curseur, le clignotement et la trame en cours : confirmés déterministes, avec une réserve.** (a) Le
  hachage est pris en fin de course, l'oracle ayant tout joué avant le C# (`BootDiff.cs:668-682`, `:802-803`) : même
  point, mêmes `frames`. (b) `cursoron`/`blink`/`cgablink` sont dans les champs sondés, et la CGA dessine le curseur
  par `cgablink & 8 (vid_cga.c:346)` (compteur de trames) : identique si le temps l'est. (c) La trame en cours est mêlée
  à la
  précédente dans `buffer32` : même chose. (d) *Réserve* : sous `--lockstep` (`BootDiff.cs:1150-1180`), la sonde est
  relevée à chaque tranche qui en vaut la peine, et la VGA hache déjà 16 Mo à chaque relevé ; ajouter ce coût aux
  genres 2 à 7 est sans effet sur la série (aucune porte ne lance `--lockstep`, `grep lockstep series.sh` : vide).
  *Déduit.*
- **A6. L'entrée `video_16to32` et le `makecol32` de l'EGA** : la boucle d'init du harnais construit
  `video_16to32` (`harness_stubs.c:448-451`) ; l'EGA n'en dépend pas, mais la sonde `#pallook` de l'EGA (champ 9)
  doit hacher `ega->pallook` avec le même ordre d'octets que `harness.c:2257-2259` (`uint32_t` en petit-boutiste).
  Le rapport le dit ; ce qu'il ne dit pas, c'est que `pallook` pointe vers `pallook16` ou `pallook64` selon `vres`
  (`vid_ega.c:51`), si bien que le hachage change *sans écriture de palette* quand 3C2h bascule `vres` : c'est un
  bon contrôle négatif de plus (écrire 3C2h avec le bit 7 inversé, côté C#). *Déduit.*
- **A7. Les statiques de l'EGA survivent aux remises.** `pallook16`/`pallook64` sont des statiques de fichier
  (`vid_ega.c:16`), rebâties à chaque `ega_init` ; la sonde sera donc juste après une remise. Mais `ega_rotate`, de
  même,
  est global : sans effet sur l'image. À noter pour la phase 2 (GR.4), qui rejoue l'oracle dans le même processus.
  *Déduit.*
- **A8. La M24 : la ROM donne la police, la carte la palette.** La M24 lit `fontdatm` chargé par `loadbios` à
  l'appel de tête `mem_bios.c:59` (`mda.rom`), non par la ROM de la machine ; une divergence de `loadfont` la touche
  donc dès la première passe, comme la MDA. Le PC1512 charge `fontdat` par `mem_bios.c:85`
  (`pc1512/40078.ic127`, 8 192 o, vérifié `ls -l`). `roms/mda.rom` fait 8 192 o. *Documenté.*
- **A9. Les types de cartes non portées** : `grep -n "GFX_" Video/video.cs` donne dix cartes (CGA, EGA, Hercules,
  MDA, ET4000AX, CL-GD5429, Trio64, TVGA8900D, TVGA9000B, VGA) plus la M24 et le PC1512 en carte intégrée. Les
  autres rendus de `pcem-dev/src/video/` (Tandy, PCjr, PC200, T1000, T3100e, Genius, Compaq CGA, Plantronics,
  Incolor, Wyse, IM1024, Sigma, PGC) ne sont pas portés : aucune carte non SVGA n'a été oubliée par le rapport.
  *Documenté.*

## 3. Les décisions ouvertes : le sont-elles vraiment ?

Règle de Julien (18/10) : une question seulement si les décisions de Julien la laissent ouverte.

1. **Les modes graphiques sous l'oracle — non ouverte.** Le principe est décidé deux fois : PLAN.md:34-35 (« des
   bancs qui finissent dans le mode testé », renvoyé à GR, PLAN.md:327-328) et la porte de GR (PLAN.md:532-533 : «
   toutes
   les portes vidéo vertes, pixels compris, et une panne qui rougit chaque sonde »). GR.0c n'est que l'application :
   les variantes d'EGABANC et d'HERCBANC, un banc de la CGA, le PC1512 déjà couvert. Ce qui reste est de l'*exécution*,
   avec deux inconnues à mesurer, non à trancher : le mode du BIOS de la M24 qui pose le 640 × 400 (rapport : « inconnu
   »),
   et le banc de la CGA (à écrire, car aucun banc n'est nommé). L'autre voie du rapport (la chaîne des trames, un
   hachage par rectangle remonté) ne s'appuie sur aucune décision : elle change le mécanisme de « la sonde prise en fin
   de course » (PLAN.md:35, point 3 de « Ce qu'un bloc livre ») ; à écarter, non à demander. *Proposition :* GR.0c
   comme le rapport le dit, ordre de A2, sans question.
2. **Les images en mode matériel — non ouverte, résolue par la décision n° 12 de PLAN-G13 et par G13.9.**
   PLAN-G13.md:930 : « vérifiée en C# seul ; les images viendront avec GR.0 » ; PLAN-G13.md:704-714 le détaille :
   « les bancs … en C# seul ; les images quand GR.0 les aura ». Le sens est : GR.0 donne les images *contre l'oracle*
   (mode PCem) ; le mode matériel n'a pas d'oracle et reste vérifié par ses cas (`MaterielCas*`), ce que le rapport
   établit lui-même (§ 1.8). Il n'y a donc rien à trancher sur la comparaison. Reste une *option de portée*, pas une
   décision : un témoin de non-régression par empreinte (hachage de ce que `@shot` écrit, K4) pour EGABANC et HERCBANC
   en C# seul ; le précédent de G13.9g est `@vram` pour BLTBANC (`BootTest.cs:125-128`). *Proposition :* ne pas la
   mettre à GR.0 ; à ajouter à GR.0c comme un contrôle de plus si son coût est nul, sinon à ne pas faire. Aucune
   question à Julien.
3. **Les clés sans pendant chez PCem sous boot-diff — non ouverte, conséquence mécanique.** Le refus est le
   traitement déjà décidé des entrées qu'un outil de comparaison ne peut pas rendre à l'oracle (`hardware_mode` :
   `BootDiff.cs:428-435` ; `ModeOutils.cs:9-11`). Les décisions n° 14 et 17 de PLAN-G13 posent `font` et `io_base`
   « au défaut de PCem » dans les deux modes : un `.cfg` qui les écarte du défaut n'est plus une comparaison à
   PCem. *Proposition :* le refus (code 2, un message), pour `font`, `io_base`, et — la même cause — `display_type =
   composite` (A4) ; une ligne dans l'étape GR.0b, sans question.

Il ne reste donc aucune question à poser à Julien pour GR.0. Deux *mesures* sont à faire dans l'étape (le mode du
BIOS de la M24 en 640 × 400 ; la liste exacte des portes comptées dans le coût, K5).

## 4. Ce que le rapport a manqué ou à surveiller : le risque de rouge

- **Premier vert non acquis** (rapport, risque 1) : confirmé, et le plus vraisemblable. Les chemins de points n'ont
  jamais été comparés hors de la VGA ; `charbuffer` de 512 contre 256 (PB-09) est sans effet sur le hachage mais
  fait lire le C# au-delà de 256 quand `crtc[1] > 128` (le C écrit alors hors de `charbuffer`, sur `revision`,
  `composite`, `snow_enabled`) : un état inconnu de la sonde proposée (`revision|composite|snow_enabled`, champ 22).
  À surveiller : si la sonde rougit sur ce champ seul, c'est PB-09 et non une transcription fausse. *Déduit.*
- **L'oracle d'abord** : la porte `video-tables-check` doit être verte avant la moindre sonde (GR.0a), et la panne
  « `loadfont` revenu au stub » doit rougir *tous* les boot-diffs en texte une fois GR.0b faite. Le rapport le dit ;
  c'est bien l'ordre.
- **Le coût d'une série entière** : GR.0b change l'émulateur (méthodes de sonde dans `vid_svga.cs`). La règle du
  18/10 (série entière seulement si l'émulateur change) l'impose : deux séries (PCem, « tout ») de 764 portes ;
  sous « tout » les boot-diffs sont refusés, la part utile se joue en mode PCem. Pour GR.0a (outillage seul), le
  sous-ensemble ciblé du rapport suffit ; K5 donne sa taille réelle (74 portes, 1 277 s selon ce comptage).

## 5. Annexe — candidats, hors de la portée

Les défauts de PCem ou d'iXtal que cette lecture a vus, sans les proposer à corriger :

- **Composite offert et non rendu** (A4) : `vid_cga.cs:703-704` (le choix) contre `:484-487` (le chemin omis). Défaut
  d'iXtal (omission), non de PCem ; présent dans les deux modes.
- **`charbuffer` de 256 octets et `crtc[1] > 128`** (vid_cga.h:31) : le C écrit hors du tableau (PB-09 déjà inscrit).
- **Le commentaire de `video.cs:556-558`** (« ne touchent jamais fontdat ») est faux pour PC200, T3100e, SIGMA400 (K1).
- **La M24 hérite de `cgapal` d'`initvideo`** : le rapport l'inscrit déjà en annexe ; confirmé relu
  (`vid_olivetti_m24.c` ne contient aucun `cgapal_rebuild`), non mesuré.
