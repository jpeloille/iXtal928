# Constats : ce que l'écriture a trouvé

> Les écarts découverts en écrivant ce dossier et absents du registre `PCEM_BUGS.md`. Pour chacun, Julien décide s'il
> devient un PB. Ce dossier n'écrit jamais dans le registre : c'est la session G13 qui le tient.

Chaque constat répond aux mêmes questions : ce que fait la machine réelle, ce que font PCem et notre code, ce que cela
change, et avec quel niveau de preuve.

## C1 — `roms/ibm-basic-1.10.rom` n'est jamais lu

*Trouvé le 7 octobre 2026, en recensant le cœur.*

- **Le code.** `mem_load_basic("ibmpc")` cherche `ibmpc/ibm-basic-1.10.rom` dans chaque dossier de ROM
  (`Memory/mem_bios.cs#mem_load_basic`, `Flash/rom.cs#romfopen`), c'est-à-dire `roms/ibmpc/ibm-basic-1.10.rom`. Or le
  fichier est à la racine de `roms/`. Le chargeur se rabat donc sur les quatre `basicc11.*`, comme le fait PCem
  (`pcem-dev/src/memory/mem_bios.c:16-53`).
- **L'effet.** Aucun. La concaténation des quatre `basicc11.*` a le même SHA-256 que lui (`3033d1a5…`). Il manque aussi
  au manifeste `roms/roms.sha256`.
- **Le niveau.** *Mesuré* pour les sommes, *déduit* pour le chemin, d'après le code.
- **Les suites possibles.** Le retirer, le ranger dans `roms/ibmpc/`, ou le laisser où il est. L'émulateur ne change pas.

## C2 — Deux `nmi_mask` là où PCem n'en a qu'un

*Trouvé le 7 octobre 2026, en recensant le cœur.*

- **Le vrai PC.** À la mise sous tension, la NMI du 8088 est masquée. On l'autorise en écrivant 80h au port A0h, on la
  masque en y écrivant 00h [T1 p. 1-24]. Le BIOS 10/27/82 l'autorise à la fin du POST, par `MOV AL,80h` puis
  `OUT 0A0h,AL` [R1 F000:E5BC].
- **PCem.** Une seule variable, `nmi_mask` (`pcem-dev/src/models/nmi.c:5`). Le port A0h l'écrit
  (`pcem-dev/src/models/nmi.c:7`) et le 8088 la lit (`pcem-dev/src/cpu/808x.c:3953`).
- **Notre code.** Deux variables.
  - Le port A0h écrit `nmi_mask` de la classe `nmi` (`Models/nmi.cs#nmi_write`), que personne ne relit.
  - Le 8088 lit le sien, déclaré dans `_808x` (`Cpu/808x.cs#nmi_mask`), que seuls l'AT (`Models/model.cs#at_init`) et le
    CMOS (`Devices/nvr.cs#writenvr`) écrivent.
  - Sur un 5150, ce second masque reste donc à 0.
- **L'effet.** Rien aujourd'hui, car rien ne déclenche de NMI sur un 5150. Mais la correction de PB-69 proposée pour le
  mode matériel de G13 passe par ce chemin : l'erreur du 8087 lève `nmi`, et le 8088 la sert si `nmi_mask` est posé.
  Sur un 5150 ou un XT, l'écriture du BIOS en A0h ne lèverait pas le masque, et la NMI ne serait jamais servie.
- **Le niveau.** *Documenté* pour le port A0h [T1 p. 1-24] et pour l'écriture du BIOS [R1 F000:E5BC] ; *déduit* pour
  la séparation, lue dans le code.
- **La suite possible.** Une seule variable, comme en C. C'est à faire avant PB-69.

## C3 — INTO (CEh) manque au 8088 de PCem

*Trouvé le 7 octobre 2026, en écrivant le contrôleur de la table des opcodes.*

- **Le vrai 8088.** INTO tient en un octet, CEh. Si OF vaut 1, l'instruction déclenche l'interruption de type 4
  « immédiatement, à la fin de son exécution » [I1 p. 2-25]. Sinon, l'exécution passe à l'instruction suivante.
- **PCem.** `pcem-dev/src/cpu/808x.c` n'a pas de `case 0xCE`. L'opcode tombe dans le `default`
  (`pcem-dev/src/cpu/808x.c:3902-3905`), qui exécute `FETCH()` et compte 8 cycles. `FETCH()` lit un octet de plus et
  avance l'IP : INTO devient une instruction de deux octets qui n'interrompt jamais.
- **Notre code.** La même chose, transcrite : `Cpu/808x.cs#execx86`, au `default` ; `Cpu/808x.cs#FETCH` avance
  `cpu_state.pc`. Sur 256 opcodes, le `switch (opcode)` en traite 255 par un `case`. CEh est le seul qui manque.
- **L'effet.** Un programme qui contrôle ses débordements par INTO ne reçoit jamais son INT 4, et l'octet qui suit INTO
  est avalé, si bien que l'instruction suivante est mal décodée.
- **Pourquoi rien ne l'a vu.** Le corpus SingleStepTests du dépôt n'a pas de forme CE : ni `vectors/sst/MANIFEST.sha256`
  ni `sst-baseline.tsv` n'en ont.
- **Le niveau.** *Documenté* pour le vrai 8088 [I1 p. 2-25] ; *déduit* pour PCem et notre code, lus. Aucune mesure : la
  forme CE du corpus SingleStepTests 8088 la donnerait.
- **La suite possible.** Inscrire le PB et ajouter la forme CE au manifeste SST. La correction relève du mode matériel
  de G13.
