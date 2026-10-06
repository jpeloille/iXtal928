# D7 — Contre-lecture : le mécanisme du mode matériel

> Contre-lecture de `D7-mecanisme.md`, le 6 octobre 2026, au commit `bc609ce` (arbre de travail :
> `iXtal26/iXtal26.csproj` modifié, `tools/perfbanc/` non suivi). LECTURE SEULE : rien n'a été construit ni lancé.
> Niveaux : **documenté** (fichier du dépôt, page de learn.microsoft.com, document de conception de dotnet/runtime,
> documentation d'outil) ; **déduit** (code de dotnet/runtime, branche `release/10.0`, lu le 06/10/2026, numéros de
> ligne de ce jour ; ou conséquence tirée du dépôt) ; **inconnu** (à mesurer). Les commandes de recomptage se rejouent
> depuis la racine du dépôt. Liste canonique des 110 PB : `PLAN.md` § G13, points 1 et 2 (A : 96, B : 14).

---

## 1. Confirmé

1. **Trois projets sans paliers, et eux seuls.** `iXtal26/iXtal26.csproj:34`, `tools/iXtal26.Diff/iXtal26.Diff.csproj:23`,
   `tools/perfbanc/iXtal26.PerfBanc.csproj:20` ; `"System.Runtime.TieredCompilation": false` dans
   `iXtal26/bin/Release/net10.0/iXtal26.runtimeconfig.json` et `tools/iXtal26.Diff/bin/Release/net10.0/iXtal26.Diff.runtimeconfig.json` ;
   job mutateur `DOTNET_TieredCompilation=0` (`tools/perfbanc/Program.cs:30-31`). Hors `obj/` et `pcem-dev/`, `find -name '*.csproj'`
   ne rend que ces trois projets ; ni `Directory.Build.*` ni `global.json` ; `iXtal26.sln` ne contient qu'`iXtal26.csproj`.
   — *documenté*.
2. **Le compte de 28 se rejoue avec la méthode du rapport.** Méthode : un PB est « numéroté » si `PB-nn` paraît dans
   `grep -rh -A4 "pcem bug, reproduced:" --include=*.cs iXtal26`. Manquent : A — 01, 02, 03, 04, 05, 06, 48, 50, 51, 52, 54,
   55, 56, 59, 60, 62, 70, 78, 87, 109, 129, 130, 131 (23) ; B — 07, 08, 09, 10, 163 (5). Liste identique à celle du rapport.
   — *documenté* (voir Corrigé n° 1 pour le classement et la sensibilité à la méthode).
3. **215 marqueurs dans 51 fichiers, tous sous `iXtal26/`**, aucun sous `tools/` :
   `grep -rn "pcem bug, reproduced" --include=*.cs iXtal26 tools | wc -l`. — *documenté*.
4. **Les fichiers générés et leurs marqueurs** : table des générateurs (`tools/x87gen/README.md`), « Ne pas éditer à la main »
   (`Cpu/x87_ops_808x.cs:14`), table du 386 « GÉNÉRÉ par tools/ops386-table.py » (`Cpu/386_ops_table386.cs:6-7`) ;
   16 (`x87_ops_808x.cs`) + 12 (`x87_ops_misc.cs`) + 2 (`x87_ops_arith.cs`) = 30 marqueurs, aucun dans les tables.
   — *documenté* (voir Corrigé n° 3 pour ce que ces 30 recouvrent).
5. **boot-diff lit le `.cfg` côté C#** : `pc.loadconfig` à `tools/iXtal26.Diff/BootDiff.cs:427`, scalaires poussés à l'oracle
   `:592-620`, sections de device par `PousserConfigDevices` `:345-350` ; `config.c` n'est pas lié dans l'oracle
   (`iXtal26/PluginApi/config.cs:10-15`). — *documenté*.
6. **Les portes sur un profil de la racine** : `tools/gates/series.sh:20` (`bd-xtdos-$g`) et `:22` (`bd-xtcfg`), `--config ixtal26-xt.cfg` ;
   ce sont les deux seules (`grep -n 'ixtal26[-a-z0-9]*\.cfg' tools/gates/series.sh`). Précision qui aggrave : le bac à sable de
   `run` LIE chaque entrée de la racine sauf `nvr/` et `os/` (`tools/gates/par.sh:85`) ; ces portes lisent le profil vivant.
   — *documenté*.
7. **par.sh transmet l'environnement** : `dotnet $DLL` lancé sans filtre par `run` (`par.sh:88`) et `runw` (`:100`), sous
   `xargs` (`:135`), après `export TMPDIR` (`:134`). — *documenté*.
8. **Le précédent `lpt_jeu_hors_service`** : déclaration `iXtal26/Models/model.cs:418`, lectures `:179`, `:426`, `:443` ;
   `tools/iXtal26.Diff/Program.cs:131-141` ; `tools/oracle/harness.c:529`, `:2378-2380` ; ABI 44 en G10.0
   (`tools/iXtal26.Diff/Oracle.cs:74-75`, ABI courante 56, `:89`). — *documenté*.
9. **Les faits du JIT du § 4.1 (n° 1 à 8 et 11), relus dans `release/10.0`** :
   - `src/coreclr/jit/importer.cpp`, `impImportStaticReadOnlyField` (3813-3836) : sortie si `!opts.OptimizationEnabled()`, puis
     `getStaticFieldContent` ; appelée (9483-9493) pour les accesseurs `CORINFO_FIELD_STATIC_SHARED_STATIC_HELPER`,
     `STATIC_ADDRESS` et `STATIC_RELOCATABLE` (pas le seul `STATIC_ADDRESS`), sous `CORINFO_ACCESS_GET` et `CORINFO_FLG_FIELD_FINAL` ;
   - `src/coreclr/vm/jitinterface.cpp`, `CEEInfo::getStaticFieldContent` (12123-12151) :
     `!IsThreadStatic() && IsClassInitedOrPreinited() && IsFdInitOnly(...)` ; `CEEInfo::initClass` (3776-3928) ne lance jamais
     l'initialiseur et rend `CORINFO_INITCLASS_USE_HELPER` (3924) ;
   - `src/coreclr/vm/methodtable.h:1179-1181` (le commentaire cité) ; `methodtable.cpp`, `IsInitedIfStaticDataAllocated`
     (3954-3991) : `HasClassConstructor()` ⇒ jamais « pré-initialisée » ;
   - `importer.cpp` 7705-7811 : `CEE_BRTRUE`/`BRFALSE`, `COND_JUMP`, « The conditional jump becomes an unconditional jump »,
     `Metrics.ImporterBranchFold` ;
   - `src/coreclr/jit/compiler.h:9913-9917` (la macro s'appelle `DEFAULT_MIN_OPTS_CODE_SIZE`) ; `compiler.cpp:3713-3716`
     (contrôle de la version livrée), appel de `compSetOptimizationLevel` à 7030, après `fgFindBasicBlocks` (6906, 6942) ;
   - `fgbasic.cpp:938-943` (constantes du pré-examen), `inline.h:1096` (`ALWAYS_INLINE_SIZE` = 16), `compiler.h:11254`
     (`DEFAULT_MAX_INLINE_SIZE` = 100) ;
   - `docs/design/features/tiered-compilation.md:56` ; `docs/design/coreclr/jit/viewing-jit-dumps.md:241-252`.
   — *déduit* (code) ; *documenté* (les deux documents de conception).
10. **Les pages de learn citées disent bien ce que le rapport leur fait dire** : *Compilation config settings* (la PGO dynamique
    « works hand-in-hand with tiered compilation […] during tier 0 » ; ReadyToRun utilisé « when it's available ») ;
    `FieldInfo.SetValue`, Remarques (« an exception is thrown if you attempt to set a value on a static, init-only field »).
    — *documenté*.
11. **L'option A n'a aucun problème d'ordre.** Un `static bool` sans initialiseur ne crée pas de constructeur de type ; la classe
    est « pré-initialisée » dès l'allocation de ses statiques (`methodtable.cpp:3969-3979`), et l'accès est une adresse constante
    `IAT_VALUE`, sans `CORINFO_FLG_FIELD_INITCLASS` (`jitinterface.cpp:1505-1518`). — *déduit*.
12. **L'outillage** : 14 commandes `r9-*` ; 84 formes dans `sst-baseline.tsv` et dans `vectors/sst/MANIFEST.sha256`, 941 dans
    `sst386-baseline.tsv` ; vecteurs absents (`.gitignore:28-36`) ; `sst-probe` hors de `series.sh` ; 160 592 / 166 998 cas en
    10 min 20 s (`VERIFICATION.md:879`, `:892`). — *documenté*.
13. **Les règles** : `TRANSCRIPTION.md` fait 218 lignes, plafond 240 (`:3`, `:45`) ; R1(d) est une liste close (`:21`) ; l'en-tête
    de `PCEM_BUGS.md:8-10` ; `tools/check-oracle.sh:46-58` saute toute ligne dont `c_path` vaut `-` sans lire `status`.
    — *documenté*.
14. **La règle du 0 % ne couvre pas G13** : elle vaut « à chaque commit de G14 et de G15 » contre une référence prise « avant G14 »
    (`PLAN.md:517-523`). À ajouter : GR s'intercale entre G13 et G14 (`PLAN.md:427`, `:476`) et change aussi l'émulateur ; la
    décision demandée par le rapport doit couvrir G13 ET GR. — *documenté*.
15. **Les numéros de ligne du registre ont dérivé** : PB-01 cité `:554, 591, 629, 667`, réel `Cpu/808x.cs:571, 608, 646, 684` ;
    PB-03 `:270` → `:280` ; PB-02 `:2989, :3017` → `:3070, :3098` ; PB-08 `device.cs:268` → `:327` ; PB-10 `mem_bios.cs:115` →
    `:284` ; la marge de PB-07, citée `mem.cs:706`, est à `Memory/mem.cs:1415-1417`. Recenser par marqueur, jamais par ligne.
    — *documenté*.

---

## 2. Corrigé

1. **Le classement des 28 en « trois cas » oublie six PB et sous-estime le pire cas.** Le rapport en range 22 ; manquent 07, 51, 54,
   56, 70, 131. Classement complet :
   - *ni marqueur ni numéro nulle part dans `iXtal26/**/*.cs`* : 01, 03, 07, 51, 54, 56, 70 — sept, pas deux ; 51, 54, 56 et 70
     sont « reproduits par la transcription » ou « verbatim » selon leurs entrées, PB-07 par la marge de quatre octets ;
   - *marqueur sans numéro* : 02, 04, 05, 06, 08, 09, 10 ;
   - *numéro hors du marqueur* (en-tête, commentaire, fin de ligne, plage) : 48, 50, 52, 55, 59, 60, 62, 78, 87, 109, 129, 130,
     163, et 131, qui n'existe que dans la plage « PB-129 à PB-133 » de l'en-tête (`Scsi/scsi_hd.cs:14`).

   Le compte dépend de la méthode : **29** en exigeant le numéro sur la ligne du marqueur (PB-144 s'ajoute :
   `grep -rhoE "pcem bug, reproduced:.*" …`) ; **28** avec `-A4` ; **15** en acceptant toute mention `PB-nn` du C#
   (01 à 06, 51, 54, 56, 70, 131 ; 07 à 10 : `grep -rhoE "PB-[0-9]+" --include=*.cs iXtal26`). Les « 97 PB distincts » de
   `plan-tete.md` sont 81 PB de A et B plus 16 de la section C (15, 19, 20, 26, 27, 32 à 38, 42, 53, 98, 121). Le plan doit fixer
   UNE méthode. — *documenté*.
2. **Les 215 sites ne sont pas 215 sites de G13.** En rattachant chaque marqueur au premier `PB-nn` de ses quatre lignes : 161 de A,
   12 de B, 29 de C, 13 sans numéro. Des 13 : PB-11 (`Cpu/808x.cs:111`), PB-12 (`:775`) et PB-13 (`Models/pic.cs:163`) sont de la
   section C ; `Keyboard/keyboard_olim24.cs:287` et `:298` n'ont AUCUNE entrée au registre (`grep -ci olim24 PCEM_BUGS.md` → 0),
   alors que leur commentaire décrit un effet observable (l'octet de souris de la M24 part en complément à deux, pas en
   signe-amplitude) ; `Host/HddImage.cs:67` est un renvoi, pas un site. — *documenté*.
3. **« 30 sites » dans les fichiers générés compte deux fois les mêmes points.** Les 16 marqueurs de `x87_ops_808x.cs` sont les
   copies des 12 de `x87_ops_misc.cs`, des 2 de `x87_ops_arith.cs` et des 2 de `x87_ops_loadstore.cs`. Points distincts : 16, dont
   les deux de `x87_ops_loadstore.cs`, qui sont PB-53 (section C) et viennent d'un fichier transcrit à la main que `gen46.py`
   recopie (README : « n'a pas de générateur »). Reste pour A : 14 points × 2 instanciations. `gen43.py` et `gen44.py` LISENT le C
   vendoré : une garde n'y peut être qu'un changement de générateur. — *documenté* (`tools/x87gen/README.md`).
4. **La liste des remises à zéro des harnais est incomplète.** Il y en a cinq : `_808x.Reset()` et `Reset8086()`
   (`Cpu/808x.State.cs:289-296`), `Reset286()`, `Reset386()`, `Reset486()` (`Cpu/386.State.cs:33-60`). Utilisateurs oubliés :
   `SstProbe.cs:261`, `:281` (`_808x.Reset()`) — l'outil même dont le rapport fait l'oracle du mode matériel —,
   `Core286Check.cs:56`, `:629` et `PmCheck.cs:209` (`Reset286`). Le fuzzeur appelle les cinq (`Fuzzer.cs:121-129`, `:640-648`).
   — *documenté*.
5. **`iXtal26.Diff` a 48 commandes, pas 45** (`grep -cE '^    case "[a-z0-9-]+"' tools/iXtal26.Diff/Program.cs`). Les « C# seul »
   oubliés au § 1.6 : `speed-check`, `boot-profile`, `refresh-check` (`BootProfile.Refresh`, qui étudie le mécanisme même de PB-03,
   `BootProfile.cs:157-171`), `ops-count`. Hors du tableau du § 3.4 : `ops-count`, `abi`, `speed-check`, `boot-profile`,
   `refresh-check`, `fdc-trace`, `popss-check`. Et ce tableau REFUSE `bench`, que M3 et M4 emploient sous le mode
   (`bench --side csharp`, § 4.4) : contradiction. — *documenté*.
6. **« Les `r9-*` dans les deux modes » est faux pour au moins trois d'entre elles.** r9-atapi n'atteint son site
   `scsi_cd.c:1581` que par la phase sans fin de PB-119 (`R9Atapi.cs:89-97`, « DRQ attendu (la phase sans fin, PB-119) ») ;
   r9-zip atteint `scsi_zip.c:979` par celle de PB-126 (`R9Zip.cs:46-55`) ; r9-aha joue `scsi_hd.c:717` par « le bus perdu » de
   PB-132 (`R9Aha.cs:166`). Ces PB corrigés, les scénarios n'atteignent plus la garde R9, et une survie qui n'y est pas passée
   « ne prouve rien » (`iXtal26/Diag/R9.cs:7-9`). Chaque `r9-*` est à relire : autre scénario, verdict par mode, ou refus.
   — *documenté* (code) ; *déduit* (conséquence).
7. **« Le défaut d'ordre se paie en vitesse, jamais en justesse » ne vaut que pour l'ordre de COMPILATION.** Pour l'ordre
   d'EXÉCUTION, avec B, la première lecture d'une garde avant le point de gel lance l'initialiseur de type : c'est le gel, au
   mauvais moment. Cas réel : le site de PB-104 est `load_joysticks()` (`iXtal26/pc.cs:674-677`), appelé à la fin de `loadconfig`
   (`pc.cs:658`), par le chargement d'une machine à l'écran (`Host/SdlSetup.cs:785`) et par `--joystick-check`
   (`Host/SdlJoystick.cs:412`), tous avant `initpc`. Or la précédence documentée applique la ligne de commande APRÈS le fichier
   (`iXtal26/Docs/ligne-de-commande.md:21`, `Host/CommandLine/Launcher.cs:271-283`). Gardé par un champ figé, PB-104 fige le mode
   PCem pendant `loadconfig`, et `--hardware-mode`, la clé et la ligne d'écran sont ensuite refusés : tel que proposé, le mode ne
   s'allumerait jamais avec un profil. Il faut (a) l'inventaire des sites atteints avant le gel (PB-104 au moins ; à vérifier :
   `check_cpu`, `TryMountHardDisks`, l'écran) ; (b) une règle : ces sites lisent la DEMANDE, jamais le champ figé ; (c) un
   initialiseur qui se sait appelé hors de `figer()` et le dit. — *documenté* (sites) ; *déduit* (conséquence).
8. **« Rien ne surcharge ces réglages » (§ 1.2) est faux.** Depuis .NET 9, une variable d'environnement l'emporte sur la propriété
   MSBuild et sur `runtimeconfig.json` (learn, *.NET Runtime config options*, § Environment variables) ; `par.sh` les transmet
   toutes. La recette M0/M2 du rapport pose elle-même `DOTNET_JitDisasm*`, `DOTNET_JitStdOutFile`, `DOTNET_TieredCompilation` :
   une exportation oubliée change la compilation ou noie les journaux d'une série, dont le verdict est la dernière ligne
   (`par.sh:89`). Huit variables `IXTAL26_*` existent déjà — `CARTE_COURTE`, `FAUTE_ANNEAU`, `FAUTE_GARDE`, `FAUTE_RAM`,
   `FNV_REF`, `LPT_JEU_HORS_SERVICE`, `TRACE_COPIE`, `VERIF_RAZ` (`808x.State.cs:112-121`, `Fuzzer.cs:1106`, `BootDiff.cs:667`,
   `:1361`, `Diff/Program.cs:135`) : « refuser une `IXTAL26_*` inconnue » exige une liste blanche, et la même défense doit viser
   `DOTNET_Jit*`, `DOTNET_TieredCompilation`, `DOTNET_TieredPGO`, `DOTNET_ReadyToRun`. — *documenté*.
9. **« Marge large » sur `execx86` (§ 4.2) : la contrainte qui mord n'est pas le seuil de blocs.** C'est le budget de locales de
   l'inliner : `lvaHaveManyLocals(0.9f)` (`fginline.cpp:1133` ; `compiler.hpp:2327-2331` ; `JitMaxLocalsToTrack` = 0x400,
   `jitconfigvalues.h:550`), soit 922 locales, au-delà desquelles plus rien n'est inliné ; le dépôt le documente
   (`VERIFICATION.md:834-847` ; `iXtal26/Docs/audit-performance-2026-09-26.md:179-180` ;
   `iXtal26/Docs/statique-instance-et-ecart-oracle.md:103`). Les locales IL déclarées pour du code matériel dans `execx86` entrent
   dans `lvaCount` même sous une branche morte, et l'IL de la branche morte compte dans les seuils MinOpts (pris avant
   l'importation, `compiler.cpp:7030`). La marge actuelle en locales est inconnue. Règle : dans `execx86` et `exec386`, une garde
   et un appel vers `*.Materiel.cs`, aucune locale, aucun code en ligne. — *documenté* (dépôt) ; *déduit* (branche morte) ;
   *inconnu* (marge).
10. **Le fait n° 14 n'est pas inconnu.** BenchmarkDotNet lance un processus par combinaison méthode/job/paramètres (`LaunchCount`
    fois) et appelle `GlobalSetup` avant le pilote (documentation BenchmarkDotNet, *How it works*,
    benchmarkdotnet.org/articles/guides/how-it-works.html) : chaque valeur d'un `[Params]` a son propre gel. — *documenté*.
11. **Les aides du x87 ne tournent pas « à chaque instruction x87 » (§ 1.3).** `x87_ld80`/`x87_st80` (PB-55, 56) ne servent qu'aux
    chargements et stockages m80 et à FSAVE/FRSTOR/FSTENV (appels : `x87_ops_loadstore.cs:365`, `:380`…) ; `x87_div` (PB-59) aux
    divisions ; `X87AddSd`/`X87MulSd` (PB-60) aux additions et multiplications. Surtout : `x87_ld80` et `x87_st80` existent DEUX
    fois, écrites à la main — `Cpu/x87_ops.cs:201`, `:227` (386) et `Cpu/x87_8087.cs:115`, `:141` (8087) —, la seconde copie sans
    marqueur ni numéro ; le *Reproduit* de PB-55 et PB-56 ne cite que `x87_ops.cs`. — *documenté*.
12. **SST ne discrimine aujourd'hui ni PB-02 ni PB-45.** Le manifeste et `sst-baseline.tsv` (84 formes) n'ont ni `D1.2`, `D1.3`,
    `D3.2`, `D3.3` (RCL/RCR mot) ni `F6.7` (IDIV octet) ; seul PB-01 est couvert (10–15, 18–1D, écart sur le seul bit 0x0010). Le
    verdict « strictement plus grand sur les formes visées » (§ 3.4) et le pilote « PB-45, discriminé par SST » (§ 9) exigent
    d'ajouter ces formes (`tools/fetch-sst.sh D3.2 D3.3 F6.7`, qui complète le manifeste, `fetch-sst.sh:14`) et de régénérer la
    ligne de base : un téléchargement, donc une question pour Julien, plus large que la question n° 6. Le corpus 386 est celui d'un
    386EX en mode réel (`Diff/Program.cs:110`), pas des 386SX/DX ni du 486 des machines. — *documenté*.
13. **Le témoin de PB-03 (§ 6.2) et la question n° 7 reposent sur une attribution contestée.** `PCEM_BUGS.md:91` impute les
    −5,87 ppm à PB-11 ; `VERIFICATION.md:554` les impute à la division entière `cpu_get_speed() / 100` de `pc.c:473`, qu'aucune
    entrée PB ne couvre, et l'entrée PB-11 n'annonce aucun effet en ppm. Le rapport reprend la première sans la recouper : l'attendu
    du `--timer-check` en mode matériel et l'argument de la question n° 7 sont à réécrire après arbitrage. — *documenté*.
14. **La R10 proposée se contredit.** (a) « tout outil qui le compare refuse le mode » contre le contrôle de fuite du § 6.3, qui
    compare à l'oracle EN mode matériel (fuzz, boot-diff) ; (b) « la région transcrite ne reçoit que les gardes […] et ce qui tient
    sous son plafond » : deux règles ; (c) « figé avant le premier `initpc` » lie la règle à B alors que la question n° 4 est
    ouverte, et oublie les remises des harnais, la tête d'`iXtal26.Diff` et le banc ; (d) « n'appelle aucune fonction de l'hôte à
    résultat variable » : en mode matériel, les chemins x87 non corrigés de PCem appellent toujours `Math.*`
    (`Cpu/x87_ops_misc.cs:6-9`) — la règle ne peut viser que le code PROPRE au mode. — *documenté* (textes) ; *déduit*.
15. **Le filtre de R2 `| grep -v 'materiel\.'` est trop large.** Il ôte toute ligne qui nomme `materiel.`, y compris un ternaire
    porteur de C de PCem (`x = materiel.pb_63 ? … : <expression de PCem>`) ou un appel `materiel.f(…)`. R2 veut « une COMMANDE,
    pas une intention » (`TRANSCRIPTION.md:36-39`) : ancrer la forme (`grep -vE '^\s*(else\s+)?if \(!?materiel\.pb_[0-9]+\)\s*$'`
    et `^\s*else\s*$`) et interdire toute autre forme de garde. — *documenté* (R2) ; *déduit*.
16. **L'option C ne s'applique pas telle quelle au 8087, ni simplement au 286/386.** `execx86` indexe directement les
    `static readonly OpFn[] ops_808x_fpu_*_a16` (`Cpu/808x.cs:3232-3288` ; `Cpu/x87_ops_808x_tables.cs:15`…) : aucun pointeur de
    table à rediriger — seulement muter en place les éléments au montage, ou une DEVIATION dans `execx86`. `ops_386` partage les
    délégués d'`ops_286` pour les gestionnaires de même nom (`386_ops_table386.cs:6-9`) : une variante se pose dans les deux. Une
    correction « autour » de tous les gestionnaires (PB-62, FIP/FDP ; PB-59) ne s'écrit pas en remplacement d'entrée sans fermeture,
    que G15 interdit sur les chemins chauds (`PLAN.md:505-507`), ou sans générateur. — *documenté* ; *déduit*.
17. **Le premier point de gel est dans du code transcrit.** `pc.cs` est dans la Portée transcrite (`TRANSCRIPTION.md:81-83`) :
    `materiel.figer()` en tête d'`initpc` est une ligne `// DEVIATION:` que R2 compte. « Aucune méthode chaude n'est compilée avant
    `initpc` » est plausible mais non mesuré : M0 doit le vérifier par l'ordre de `JitDisasmSummary`. — *documenté* ; *déduit*.
18. **`AllowUnsafeBlocks=false` ne protège pas un `readonly`.** `Unsafe.AsRef` « reinterprets the given read-only reference as a
    mutable reference » sans bloc `unsafe`, et « the runtime contains internal logic predicated on the assumption that readonly
    references truly are immutable » (learn, `Unsafe.AsRef`) : une écriture après le gel désaccorderait le code plié et le champ.
    Le dépôt n'en a aucune (`grep -rn "Unsafe\." --include=*.cs` → 0) ; une interdiction greppable suffit. — *documenté*.
19. **Deux motifs d'exclusion à redresser.** Le `const` : CS0162 ne bloque pas, la Portée admet des `#pragma warning disable`
    énumérés et commentés (`TRANSCRIPTION.md:86-88`) ; l'objection qui tient est celle des deux binaires. Les *feature switches* :
    ils ne se plient pas « qu'au découpage de NativeAOT » mais par les substitutions d'ILLink de toute application découpée
    (dotnet/runtime, `docs/workflow/trimming/feature-switches.md`) ; mis en cache dans un `static readonly`, ils se plient comme B.
    — *documenté* ; *déduit*.
20. **Images de la recette** : « sans variable d'environnement rien ne peut l'y mettre » — une clé `hardware_mode` dans un gabarit
    `tools/gates/cfg/*.cfg.in`, recopié dans `$WORK` par `g5w-recipe.sh:65`, le pourrait. La porte du recensement (§ 6.1-5) doit
    lire aussi les `.cfg.in` et les copies de `$WORK`. — *documenté*.
21. **`--hardware-mode [LISTE]` à valeur facultative est ambigu** dans `iXtal26.Diff`, où l'option globale est retirée avant
    l'aiguillage (`Program.cs:135-137`) : dans `iXtal26.Diff --hardware-mode boot-diff roms …`, `boot-diff` serait lu comme LISTE et
    sortirait en 2 (§ 3.2). Valeur obligatoire (`tout`), ou forme `--hardware-mode=LISTE`. — *déduit*.
22. **Détails.** L'ordre du § 9 omet « la souris et les ports » et « la section C » (`PLAN.md:361-362`) ; la boucle d'`exec386`
    fait environ 170 lignes (`Cpu/386.cs:342-512`), pas une centaine. — *documenté*.

---

## 3. Ajouté

1. **Les réponses sur .NET 10 que le rapport laisse implicites.**
   - *Pliage d'un `static readonly bool` lu après l'initialisation* : oui, à quatre conditions. Release : en Debug, `Optimize` est
     coupé, et « Optimize also tells the common language runtime to optimize code at run time » (learn, *C# compiler options —
     code generation*, Optimize) : aucun pliage en Debug, donc pas sous un lancement Debug de Rider. Pas de MinOpts. La garde lit le
     champ directement et l'IL enchaîne `brtrue`/`brfalse` ou une comparaison à une constante, pliés à l'importation
     (`importer.cpp` 7705-7811) ; la branche morte n'est alors pas importée. À travers une locale, une propriété ou une petite
     méthode, l'importeur voit une locale ou un `GT_RET_EXPR`, IMPORTE la branche morte (budget d'inlining, temporaires) et ne la
     retire que plus tard. D'où une règle : `if (materiel.pb_nn)` sur le champ, rien d'autre — cohérente avec « Des champs, pas des
     propriétés, dans `exec386` et `execx86` » (`statique-instance-et-ecart-oracle.md:103`). — *déduit* ; *documenté* (Optimize).
   - *`beforefieldinit` contre constructeur statique explicite* : après l'initialisation, aucune différence pour le pliage
     (`getStaticFieldContent` ne regarde que l'état initialisé et `InitOnly`). Avant : (i) sans constructeur explicite, appeler une
     méthode statique de la classe ne lance pas l'initialiseur (`initClass`, cas `pFD == NULL` et `IsBeforeFieldInit()`,
     `jitinterface.cpp` ≈ 3817-3823) : `materiel.figer()` doit lire un champ ou appeler `RuntimeHelpers.RunClassConstructor` ; avec
     un constructeur explicite, tout membre statique référencé le lance (learn, *Static Constructors*) ; (ii) pour une classe
     `beforefieldinit`, l'appel d'initialisation reçoit `GTF_CALL_HOISTABLE` (`flowgraph.cpp`, `fgGetStaticsCCtorHelper`, 747-752)
     et peut sortir d'une boucle : dans une méthode compilée avant le gel, l'initialiseur peut courir avant la garde. Learn note que
     le constructeur explicite « limits runtime optimization » ; le code montre que cela ne joue qu'avant l'initialisation.
     Recommandation : constructeur statique EXPLICITE, classe qui ne contient que les champs figés, initialiseur qui refuse de courir
     hors de `figer()`. Un initialiseur qui lève laisse le type « uninitialized for the lifetime of the application domain »
     (learn, *Static Constructors*) : le refus bruyant se fait dans `figer()` ou dans la demande, pas en levant dans l'initialiseur.
     — *déduit* ; *documenté*.
   - *Classe statique imbriquée* : un type à part, un état d'initialisation à part (`getStaticFieldContent` prend
     `GetEnclosingMethodTable()` du champ) ; initialiser l'englobante n'initialise pas l'imbriquée : `figer()` les initialise une à
     une. — *déduit*.
   - *`static readonly bool[]`* : jamais plié. La référence ne devient constante que si l'objet est dans un segment gelé
     (`getStaticObjRefContent`, `jitinterface.cpp:12099-12117` ; `ignoreMovableObjects = true` par défaut, `corinfo.h:3265`) —
     un `new bool[n]` est mobile ; les éléments ne se lisent comme constantes que sur un objet immuable (`isObjectImmutable`,
     `jitinterface.cpp:5970-6002` : chaînes, `RuntimeType`, tableaux vides, délégués ; `GetImmutableDataFromAddress`,
     `valuenum.cpp:12316-12330`). Coût : lecture du champ, contrôle de bornes, lecture de l'élément, test — PLUS cher que A.
     — *déduit*.
   - *Méthode inlinée dans une méthode compilée plus tôt* : le corps inliné est importé dans la compilation de la racine ; seul
     compte l'état au moment de compiler la RACINE ; racine compilée avant le gel : appel d'initialisation et lecture, pour toujours
     (aucune recompilation). — *déduit*.
   - *Délégués `OpFn`* : la cible est compilée à sa première invocation, par le prestub (`src/coreclr/vm/prestub.cpp`,
     `PreStubWorker` 1894, `MethodDesc::DoPrestub` 2134) ; créer le délégué ne compile rien. Donc après le gel si la première
     invocation l'est. `Is486Banc` compile le gestionnaire dans `[GlobalSetup]` (`VerifierEquivalence`), après `Reset386`/`Reset486` :
     compatible avec un gel dans les remises. — *déduit*.
   - *Seuils de MinOpts* : confirmés ; ils comptent l'IL des branches mortes ; ajouter le budget de locales (Corrigé n° 9).
     — *déduit*.
2. **Les fils d'exécution : aujourd'hui, un seul.** `SdlAudio` est en mode PUSH, appelé du fil d'émulation (« un callback audio
   SDL3 […] serait le premier thread du projet », `Host/SdlAudio.cs:14-20`) ; le fil du CD est omis, son corps appelé depuis
   `sound_poll` (`TRANSCRIPTION.md:195`) ; l'accélérateur S3 est synchrone (`:183`) ; le seul rappel étranger, celui du dialogue de
   fichiers SDL, ne pose qu'un drapeau (`Host/SdlMenu.cs:1034-1058`). Ni A ni B n'ont de course aujourd'hui ; B reste sûr si G16 ou
   G17 ajoutent des fils (état immuable, initialiseur appelé une fois sous verrou, learn *Static Constructors*) ; A demanderait alors
   une règle de publication. — *documenté*.
3. **Plusieurs machines par processus : définir la clé absente.** Les `r9-*` enchaînent `loadconfig` et `initpc` (`R9Awe.cs:126`,
   `:196`, `:243` ; `R9Sb16.cs:81`, `:133` ; `R9CdCfg.cs:96`, `:137` ; `R9JoyCfg.cs:65` ; `R9SbCfg.cs:119` ; `R9Aha.cs:197`) ;
   `--menu-check` monte cinq machines (`SdlMenu.cs:1777-1797`) ; `bench` réamorce (`Video/video.cs:655`). Si une clé absente vaut 0,
   la deuxième machine d'un `r9-*` lancé en mode matériel « redemande » PCem après le gel et serait refusée. À écrire : la clé ne
   pose la demande que si elle est présente (comme les surcharges « non demandé = null », `ligne-de-commande.md:26-27`), et seul un
   CHANGEMENT de valeur après le gel est refusé. — *documenté* ; *déduit*.
4. **Les verbes d'`iXtal26` sont à classer aussi**, pas seulement ceux d'`iXtal26.Diff` : `--joystick-check` ASSERTE le
   comportement reproduit de PB-104 (« PB-104, correspondances du chapeau par défaut : (0, 0) », puis 135° et 315°,
   `SdlJoystick.cs:343-351`) et l'exécute sans `initpc` (`:412`) ; `--menu-check` mesure la fréquence de ligne carte par carte
   (`SdlMenu.cs:1760-1797`), que PB-97, 99 et 102 peuvent déplacer ; `--setup-check`, `--speaker-check`, `--fat-check`, `--boot`,
   `--timer-check` ; `--make-nvr` ne monte rien (`NvrImage.Make` lit le `.cfg` lui-même, `Host/NvrImage.cs:208-260`) et ne dépend
   pas du mode. — *documenté*.
5. **La section B contient des entrées sans « cas qui discrimine » possible.** PB-10 (« sans conséquence dans iXtal26 »,
   `PCEM_BUGS.md:1813-1814`) et PB-18 (« aucun observé », `:1852`) n'ont aucun effet ; PB-08 (« en C#, exception. Inatteignable »,
   `:1793-1794`), PB-16 (« `Seek` sur un flux fermé lève », `:1828-1829`) et PB-17 (« non reproductible — `fread` sur un `Span`
   borné lève en C# », `:1841`) remplacent un comportement indéfini du C par une exception : il n'y a pas de « valeur de la
   documentation ». La doctrine du § 6.2 (« pour chaque PB… ») doit les exclure ou les verser ailleurs (nettoyage, R9). PB-07,
   lui, se corrigerait (rebouclage à 1 Mo d'une lecture mot du 8088) dans `readmemw` du 808x : un site chaud. — *documenté* ;
   *déduit*.
6. **Des écarts de PCem déjà mesurés par SST n'ont pas d'entrée PB, donc aucune correction prévue.** 8088 : DAA/DAS, REP LODSW,
   SETMO, SHL par CL (OF), DIV (débordement de quotient) — « Les tirets de la colonne « Défaut » sont des trous, pas des
   acquittements » (`VERIFICATION.md:689-700`) ; 386 : LOCK illégal (E1, 125 formes), contrôles de limite (E2, E3), « pas encore
   instruites » (`VERIFICATION.md:3895-3907`). Le mode borné aux 110 PB laissera ces formes rouges contre le silicium, et le verdict
   « passe(matériel) ≥ passe(PCem) » les masque. Décision pour Julien : les instruire en PB (section A) pendant G13, ou les déclarer
   hors du mode. S'y ajoutent les marqueurs orphelins de la M24 (Corrigé n° 2). — *documenté*.
7. **Des PB demandent un ÉTAT neuf, pas seulement un chemin gardé.** PB-55 et 56 (80 bits, alors que « ST est un `double[]` »,
   entrée PB-56), PB-62 (FIP/FDP jamais mémorisés), PB-03 (les cycles de rafraîchissement), sans doute PB-69 (la NMI). Cet état
   doit rester hors des comparaisons à l'oracle (`h_state`, sondes) en mode PCem, et « à côté » peut exiger une représentation
   parallèle (des registres 80 bits ombres) que la règle n° 4 du § 4.3 (une aide pure sur des `double`) n'exprime pas. Décision de
   conception à prendre avant l'étape du x87. — *documenté* (registre) ; *déduit*.
8. **R5, R6, R1(c) et la Portée manquent au § 5.** R5 : « jamais plus de 300 lignes neuves sans `dotnet build` et exécution du
   vérificateur » (`TRANSCRIPTION.md:60-61`) — le mécanisme est estimé à 350–480 lignes : G13.0 aura au moins un point de contrôle
   intermédiaire. R6 : « le harnais différentiel est vert » et « un chemin non implémenté est `fatal(…)` » (`:62-64`) contredisent
   les `*.Materiel.cs` (sans oracle) et l'« aucun arrêt » de R10 : exemption à écrire. R1(c) exige une provenance `// pcem: …` par
   fonction (`:20`) : « le marqueur tient lieu de provenance » doit y figurer. La Portée (`:81-90`) doit dire quelles conventions
   valent pour un `*.Materiel.cs` rangé dans `Cpu/`. Budget : la R10 proposée fait ≈ 1 060 caractères, 11 à 12 lignes à la largeur
   du fichier (lignes de 105 caractères au plus) ; avec R1, R2, R6, R8 et la Portée, ≈ 235 lignes sur 240, sans marge pour G14 à
   G17 hors relèvement inscrit (R3). — *documenté* ; *déduit*.
9. **À amender avec l'en-tête de `PCEM_BUGS.md`** : ses lignes 12-13 parlent encore d'un « plafond de 200 lignes de R3 » (240
   aujourd'hui) ; `oracle.tsv:8` énumère les statuts (`transcribed | deviated | partial | host`) et doit recevoir `materiel`.
   — *documenté*.
10. **L'hygiène de la preuve par listings (M0, M2).** Release seulement (Ajouté n° 1). Une liste fixe d'une dizaine de méthodes
    manque les appelants qui inlinent une méthode gardée et toute méthode gardée qu'aucun scénario ne compile (un amorçage DOS ne
    compile pas les aides m80 du x87) : la comparaison serait vide de sens. Prendre `DOTNET_JitDisasm=*` par scénario, découper par
    méthode, et exiger que chaque méthode gardée paraisse dans `JitDisasmSummary` d'au moins un scénario ; tourner sans fenêtre (la
    sortie du JIT n'est pas synchronisée entre fils, `viewing-jit-dumps.md:267-269`). P10 reste ouvert — `TieredCompilation=false`
    « à re-mesurer » sur la charge Windows 3.1 (`audit-performance-2026-09-26.md:159-165`) : si les paliers reviennent, B devient
    plus sûr (le Tier1 compile après le gel), mais M0 et M2 devront viser les listings du Tier1. — *documenté* ; *déduit*.
11. **Le banc n'est pas au dépôt.** `tools/perfbanc/` n'est pas suivi, et l'`InternalsVisibleTo` qui l'ouvre est une modification
    non commitée d'`iXtal26.csproj` (`git status`, `git diff`). `PLAN.md:561-562` : « Rien n'est installé avant G15 » ;
    BenchmarkDotNet l'a été le 04/10 avec l'accord de Julien (commentaire d'`iXtal26.PerfBanc.csproj`). Se servir de M1 en G13
    suppose de commiter le banc en G13.0 : décision de Julien. M1 doit aussi mesurer la forme « B gelé trop tard » (méthode compilée
    avant l'initialiseur), seul risque que le rapport dit se payer « en vitesse ». — *documenté* ; *déduit*.
12. **Le prix des portes SST.** 10 min 20 s pour le 8088 à `--limit 2000` (`VERIFICATION.md:892`) ; `sst-probe` et `sst386-probe`
    dans les deux modes allongent chaque série, et une porte qui exige des vecteurs absents la rend rouge (§ 6.4 du rapport) : le
    téléchargement précède l'entrée de la porte dans `series.sh`. — *documenté* ; *déduit*.
13. **Le masque par PB : dépendances et contrôles négatifs.** Des corrections ne valent qu'ensemble (PB-55/56 et la disposition de
    FSAVE de PB-62 ; FXAM, PB-63, et les dénormaux de PB-55) : « chacun seul » peut fabriquer un état qui n'est ni PCem ni le
    matériel ; la table PB → domaine doit porter des groupes. En retour, le masque rend les contrôles négatifs gratuits (une
    correction coupée, sans construction à part, cf. `PLAN-G12.md:236`), comme les pannes que le dépôt injecte déjà à l'exécution
    (`IXTAL26_FAUTE_*`, `808x.State.cs:113-121`, `Fuzzer.cs:1106`). — *déduit* ; *documenté*.
14. **Des précédents non cités.** La forme B existe déjà dans le harnais : `static readonly bool CarteCourte`, `VerifRaz`
    (`808x.State.cs:112`, `:114`), `FnvRef` (`BootDiff.cs:1361`), lus d'une source fixée au lancement. Le compteur par site de la
    « sonde du mode » a son précédent dans `Diag/R9.cs` (`Dictionary<string, int>`), qu'on ne peut recopier sur un site chaud (PB-51 à
    chaque lecture d'instruction) : un `int[]`, comme le dit le rapport. — *documenté*.
15. **Où vivent les champs, sous B.** Un champ posé dans une classe existante (`x87_c`, `_386`, `pc`…) se figerait à la première
    initialisation de cette classe, souvent avant la ligne de commande : raison de plus pour une classe centrale, qui ne contient
    RIEN d'autre que les champs figés. — *déduit*.
16. **G14 et G15 renommeront** `materiel`, `pb_nn` et les `*.Materiel.cs` (`Ics/`, PascalCase : `PLAN.md:479-486`, `:497-499`) ;
    les marqueurs `// pcem bug … PB-nn` sont conservés (`PLAN.md:501`), donc `fixed in hardware mode` survit. Écrire la R10 sans
    nommer la classe. — *documenté*.
