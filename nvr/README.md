# Le CMOS non volatil des machines qui en ont un.

`nvrfopen` (`nvr.c:33-54`) compose « nvr/<config>.<machine>.nvr » en premier, et retombe
sur « nvr/default/<machine>.nvr » en LECTURE SEULE — le CMOS de reference qu'un emulateur
peut livrer. Les trois fichiers de `default/` sont ceux de PCem (`pcem-dev/nvr/`), bit pour bit :
`at.nvr`, `ami286.nvr`, et `ami386.nvr` depuis G3.1 (somme 0x10-0x2D = 0x022C, meme disposition),
`ami386dx_opti495.nvr` depuis G3.2 et `ami486.nvr` depuis G6.3.

CE REPERTOIRE DOIT EXISTER : nvrfopen rend NULL en ecriture si le chemin manque, et
savenvr ne verifie pas (PB-33). PCem livre le sien pour la meme raison.

`config_name` n'est jamais affecte dans ce depot, donc le premier chemin est
« nvr/.at.nvr » et « nvr/.ami286.nvr » — des fichiers CACHES, que `ls nvr/*.nvr` ne
montre pas. Ils sont gitignores : ce sont des sorties de session, pas des references.

## La somme de controle, MESUREE dans les deux ROM

Elle n'est ecrite nulle part dans PCem : elle vit dans les BIOS, et les fichiers livres
en sont le produit. Verifiee par trois voies independantes.

> `somme = Σ octets[0x10 … 0x2D]` sur **16 bits**, non tronquee a 8 ·
> `[0x2E]` = poids **fort**, `[0x2F]` = poids faible ·
> **une somme nulle est rejetee**, meme si 0x2E/0x2F concordent.

| voie | ou | ce qu'elle montre |
|---|---|---|
| Les fichiers livres | `default/ami286.nvr` → 0x0AB6 · `default/at.nvr` → 0x00E5 · `default/ami386.nvr` → 0x022C | les trois concordent, gros-boutiste |
| IBM AT, verification | `62x0820`+`62x0821` entrelaces, `0x06fe-0x0727` | `mov cl,90h` / `mov ch,0AEh` : de 0x10 inclus a 0x2E exclu ; `or bx,bx / jz` rejette la somme nulle |
| AMI 286, **ecriture** | `amic206.bin:0xacd0-0xad11` | le SETUP ecrit 0x10-0x3F sauf 0x32, resomme 0x10-0x2D, pose 0x2E puis 0x2F, met **0x0E et 0x0F a zero**, puis saute a F000:FFF0 |

La troisieme est la plus utile : c'est le geste qu'un generateur doit reproduire, et il
vient du BIOS lui-meme.

## Ce que le POST verifie, MESURE dans amic206.bin

Les messages ne sont pas references par adresse : ils sont indexes par numero de bit dans
deux mots portes par BP (indices 0-15) et BX (16-31), affiches par `0x91b6`. La table de
pointeurs est en `0x7ab2`.

| octet | lu par | condition d'echec |
|---|---|---|
| 0x0D bit 7 | `0x8232` | a 0 → « CMOS battery state low » |
| 0x0E bit 7 | `0x8254` | a 1 → « CMOS system options not set » |
| 0x10-0x2D, 0x2E, 0x2F | `0x825a-0x8296` | somme fausse ou nulle → « CMOS checksum failure » |
| 0x15/0x16 | `0x8e73` | `((0x16<<8)\|0x15) >> 6 ≠ [0040:0013]` → « CMOS memory size mismatch » |
| 0x17/0x18 vs 0x30/0x31 | `0x8e85` | `((0x18<<8)\|0x17) >> 6 ≠ ((0x31<<8)\|0x30)` → meme message |
| 0x14 bits 5-4 | `0x97dd` | **vaut 0x00** → « CMOS display type mismatch », TOUJOURS ; ou 0x30 avec `[0040:0010] & 0x30 ≠ 0x30` ; ou 0x10/0x20 avec `[0040:0010] & 0x30 == 0x30` |
| 0x14 bit 0 | `0x82e2` | a 0 → « CMOS system options not set » |
| 0x0A bit 7 | AT `0x12fe` | UIP qui ne se libere pas → « 163-Time & Date Not Set » |

Deux gardes a connaitre, mesurees : les deux tests de taille memoire sont **sautes** si
`0x0E & 0xC0 ≠ 0` (`0x8e61`), et les deux messages d'affichage sont **effaces avant
affichage** si le bit 1 de `0x37` est a 0 (`0x90b4`). Les supprimer par la est possible
et ce serait une triche : c'est 0x14 qu'il faut poser juste.

Cote IBM AT, `0x0E` est le vecteur entre detection et affichage : bit 7 → « 161 », bits 6
ou 5 → « 162 », bit 4 → « 164 », bit 2 → « 163 ». Un CMOS a zero echoue comme un CMOS a
0xFF ; **l'ecran ne departage pas deux CMOS**, seule la somme le fait.

## Deux pieges d'ecriture

`loadnvr` ecrase `nvrram[0x0A] = 6` et `nvrram[0x0B] = RTC_2412` **apres** lecture, donc
un fichier ne peut jamais porter autre chose pour ces deux registres. Mais
`time_internal_set_nvrram` tourne **avant** l'ecrasement et decode 0x00-0x09 et 0x32 avec
le 0x0B **du fichier** : un fichier a 0x0B = 0 fait lire les heures en mode 12 heures.

`savenvr` s'execute a chaque sortie de `--boot` et du mode fenetre, sans condition. Un
fichier de session fabrique a la main est donc reecrit au premier lancement, et la graine
de `default/` ne reprend la main que si le fichier de session est supprime.

## Les deux references de PCem, annotees

| off | `at.nvr` | `ami286.nvr` | |
|---|---|---|---|
| 0x0E | `30` | `28` | diagnostic deja non nul dans les deux |
| 0x10 | `22` | `44` | deux 1,2 Mo · deux 1,44 Mo |
| 0x12 | `00` | `00` | aucun disque dur, dans les deux |
| 0x14 | `41` | `45` | bits 5-4 = **00** dans les deux → plainte inevitable |
| 0x15/16 | `80 02` | `80 02` | 640 Ko de base |
| 0x17/18 | `00 00` | `00 04` | 0 Ko · 1024 Ko etendus |
| 0x2E/2F | `00 E5` | `0A B6` | sommes valides |
| 0x30/31 | `00 0C` | `00 04` | ce que le POST avait trouve, chez PCem |
| 0x32 | `19` | `20` | siecle BCD |
