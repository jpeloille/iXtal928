# G4 — Le x87 : le plan, sur reconnaissance

> Écrit le 29 septembre 2026, au commit `3723463` (G3 fait). Bloc G4 de `PLAN.md`.
> Chaque constat cite la ligne de C qui le fonde, sur `pcem-dev/` tel que vendoré.
> Les décisions de Julien sont tranchées en fin de fichier.

## Où on en est

| | C# | oracle |
|---|---|---|
| `hasfpu` | 0, posé par `cpu_set` depuis `fpu_type = FPU_NONE` (`cpu.cs:111`, `:215`) ; la clé `fpu` n'est pas lue | 0, zéro implicite |
| ESCAPE D8-DF, 286/386 | `op_nofpu_a16/a32` seuls (`386_ops_fpu.cs`) ; `cpu.cs:261` : `fatal` si `hasfpu` | tables `ops_fpu_*` **déjà compilées** : `386.c`, `x87.c`, `x87_timings.c` liés (`tools/oracle/Makefile:49-52`) |
| ESCAPE 8088 | blocs `if (hasfpu)` omis (`808x.cs:3223-3260`) | `8087.h` compilé par `808x.c`, inclus dans `harness.c` |
| État FPU | omis (`x86.cs:105` : ST/TOP/tag/npxs/npxc/MM) | présent, **non exposé** par `h_state` |
| `fpus_*`, `fpu_get_type` | non portés (`cpu_tables.cs:16`) | présents |

**Le constat qui change tout : PCem n'émule pas un x87 à 80 bits.** `cpu_state.ST[8]` est un
tableau de `double` (`x86.h:93`) ; le format 80 bits n'existe qu'aux frontières mémoire
(`x87_ld80` / `x87_st80`, `x87_ops.h:86-150`). Un `double` C# reproduit donc l'oracle au bit
près, sans type 80 bits à écrire. Le risque se déplace sur trois points mesurables (§ Risques) :
l'arrondi dirigé, la libm, les conversions double → entier.

## Ce qu'il faut transcrire

| Source | Lignes | Contenu |
|---|---:|---|
| `includes/private/cpu/x87_ops.h` | 1 116 | aides (push/pop, `x87_fround`, ld80/st80, compare, fsave/frstor), `FP_ENTER` (#NM si CR0.EM/TS, `:264-270`), tables D8-DF a16/a32 (`:310-1040`), `nofpu` |
| `x87_ops_loadstore.h` | 576 | 30 handlers : FILD/FIST(P) 16/32/64, FLD/FST(P) s/d/t, FBLD/FBSTP |
| `x87_ops_arith.h` | 452 | 27 handlers : macro `opFPU` × (s, d, iw, il), FADD…FDIVR, FCOM(P), `fesetround` (`:12-16`) |
| `x87_ops_misc.h` | 927 | 51 handlers : constantes, FXCH, FCHS/FABS, FSTSW/FSTCW/FLDCW, FNINIT/FNCLEX, FSTENV/FLDENV, FSAVE/FRSTOR, FSCALE, FPREM, F2XM1, FYL2X(P1), FPTAN, FPATAN, FSQRT, FSINCOS, FSIN, FCOS |
| `x87.c` + `x87.h` | 97 + 39 | gettag/settag, `x87_reset` (vide) |
| `x87_timings.c` + `.h` | 297 + 59 | données : 8087, 287, 387, 486 |
| `8087.h` | 89 | la colle du 8088 : `OP_TABLE` → `ops_808x_*` |
| `cpu.c` | ~80 | `fpu_get_type` (`:128-137`), `hasfpu` (`:184`), tables interprétées (`:276-300`), `switch (fpu_type)` (`:1132-1152`) |
| `cpu_tables.c:25-29`, `pc.c:656` | ~10 | `fpus_*`, clé `fpu` |

**≈ 3 750 lignes.** Hors portée : les tables `fpu_686_*` (FCMOV, FCOMI), les
`dynarec_ops_fpu_*`, MMX et 3DNow — sauf les champs d'état que FSAVE/FRSTOR touchent
(`MM_w4`, `TAG_UINT64`).

## Périmètre

| Coprocesseur | Machines | Table PCem |
|---|---|---|
| 8087 | 5150, XT (SW1 bit 1, `keyboard_xt.cs:153`, `:204`) | `fpus_8088` |
| 287, 287XL | ibmat, ami286 | `fpus_80286` |
| 387 | ami386, ami386dx | `fpus_80386` |
| intégré | 486, en G6 | `fpus_builtin` |

Réglé par la clé `fpu` de la configuration, comme PCem. Défaut `none` : décision n° 1.

## Les étapes

### G4.0 — L'outillage, avant toute ligne de x87  ✅ *fait (d774bc2), VERIFICATION.md § G4.0*

1. `h_state` expose `ST[8]` en **bits bruts** (uint64 : NaN, signes, zéros négatifs), `tag[8]`,
   `TOP`, `npxs`, `npxc`, `x87_pc/op_off/seg` (ABI 27). `h_set_fpu(type)` pose `fpu_type` et
   `hasfpu` **explicitement** des deux côtés — la fin du zéro implicite (`PLAN-286.md` § 5).
2. Le fuzzeur tire un état FPU : piles pleines, vides, partielles ; valeurs spéciales ;
   `npxc` avec ses quatre modes d'arrondi et ses masques.
3. **Trois mesures de parité, avant d'écrire** : la libm C contre `Math.*` .NET, ~10⁷ tirages
   par fonction ; l'effet réel de `fesetround` dans l'oracle compilé en -O2 sans
   `-frounding-math` ; `(int64_t)` et `(uint64_t)` sur NaN, ±∞ et hors bornes.

Porte : `check-oracle.sh` à zéro dérive, tous les boot-diffs inchangés à l'unité.

### G4.1 — L'état et la plomberie (`fpu=none` : rien ne bouge)  ✅ *fait, VERIFICATION.md § G4.1*

État dans `x86.cs`, `x87.cs` (tags), `x87_timings.cs`, `fpus_*`, `fpu_get_type`, clé `fpu`,
`cpu_set` (tables et temps). Porte : boot-diffs à l'unité en `fpu=none` ; empreinte CPU
identique en `fpu=287` et `fpu=387`.

### G4.2 — Chargements et stockages  ✅ *fait, VERIFICATION.md § G4.2*

`x87_ops_loadstore.h`, `x87_ld80` / `x87_st80`, `x87_fround`, FBLD/FBSTP (`fmod`, `floor`).
Porte : fuzzeur 386 en mode réel, `fpu=387`, opcodes D9/DB/DD/DF mémoire.

### G4.3 — L'arithmétique  ✅ *fait, VERIFICATION.md § G4.3*

`x87_ops_arith.h`, et l'arrondi dirigé TEL QUE PCem l'applique (décision n° 3, PB-48) : la
sémantique de `fesetround` réécrite en C# sur les **seuls** `opFADD` mémoire (m32, m64,
m16int, m32int, `x87_ops_arith.h:12-16`), au plus près partout ailleurs — FSUB, FSUBR, FMUL,
FDIV, FDIVR et les formes registre (`:122-152`, `:239-397`). Elle remplace côté C# le
`fesetround` C, qui reste tel quel dans l'oracle. Porte : fuzzeur D8/DA/DC/DE, les quatre
modes d'arrondi tirés.

### G4.4 — Le reste, transcendantes exceptées

`x87_ops_misc.h` : pile, contrôle et état, FNINIT, FSTENV/FLDENV, **FSAVE/FRSTOR en 16 et
32 bits, mode réel et protégé** (`TAG_UINT64`, `MM_w4`), FXCH, FPREM, FSCALE, FSQRT ; #NM par
CR0.EM/TS (`FP_ENTER`) sous `pm-check`.

### G4.5 — Les transcendantes

F2XM1, FYL2X, FYL2XP1, FPTAN, FPATAN, FSIN, FCOS, FSINCOS. Porte : fuzzeur au bit près, avec
la libm retenue par la mesure de G4.0 (décision n° 4).

### G4.6 — Le 8087

`8087.h`, les huit blocs `if (hasfpu)` de `808x.c:3304-3366` (sauvegarde et restitution de
`pc`), SW1 du XT. Porte : fuzzeur 8088 en `fpu=8087`, puis `boot-diff --model xt` avec 8087.

### G4.7 — Les machines et les témoins

`fpu` dans les configurations et les deux profils Rider (décision n° 1) ; octet CMOS 0x14
bit 1 posé par `--make-nvr`. Boot-diffs ibmat et ami286 en 287, ami386 et ami386dx en 387.
Témoins `--boot` (décision n° 6) : POST et `INT 11h` bit 1 ; MSD de DOS 5, « Math
Coprocessor » ; QBASIC `PRINT SIN(1), ATN(1)*4, SQR(2)` ; Windows 3.1, Calculatrice
scientifique sous WIN87EM.

## La vérification

- **Oracle** : PCem seul. SingleStepTests n'a pas de jeu x87 — vérifié en G4.0 : vingt-deux
  dépôts, aucun x87 ni 8087, et aucune forme D8-DF dans nos deux lignes de base ; un corpus
  silicium ne départagerait de toute façon rien contre un modèle à 53 bits.
- **Fuzzeur** : état FPU bit pour bit, cycles compris (`x87_timings`).
- **pm-check** : #NM, FSAVE/FRSTOR en mode protégé.
- **Boot-diffs** : avec et sans coprocesseur, à l'unité.
- **L'angle mort** : ce que PCem ne modélise pas (précision 64 bits, exceptions autres que ZE)
  n'a pas d'oracle. Cela s'inscrit au registre ; cela ne se mesure pas.

## Les risques

1. **L'arrondi dirigé.** PCem n'encadre de `fesetround` que les FADD mémoire
   (`x87_ops_arith.h:12-16`, PB-48) ; .NET n'a pas d'équivalent. Mesuré en G4.0
   (`x87-parity`) : GCC -O2 sans `-frounding-math` honore le mode, et le vrai handler
   l'applique à FADD seul.
2. **La libm.** Mesuré en G4.0 contre la .so de l'oracle : `Math.*` rend les bits de la glibc
   sur 10⁷ tirages par fonction, expressions de `x87_ops_misc.h` verbatim comprises. Parité
   liée à la glibc de l'hôte : à remesurer si l'oracle change de machine.
3. **Les conversions.** Depuis .NET 9, `(long)double` **sature** ; `cvttsd2si` rend
   `0x8000000000000000` sur NaN et hors bornes. Mesuré en G4.0 contre `h_conv` : le cast
   .NET s'écarte ~1,2 M fois sur 10⁷, les aides de `X87Parity.cs` (`CvtI64`, `CvtU64` — test
   `>=`, un NaN prend la branche directe —, `CvtI32`) jamais. Elles entrent dans le cœur en
   G4.2, marquées `// DEVIATION:`.
4. **Les défauts de PCem à reproduire (R8).** PB-48 (l'arrondi, ci-dessus) est inscrit.
   Relevés en reconnaissance, à lire à la ligne puis consigner (PB-52 à PB-56 inscrits en
   G4.2 : FBLD, tempc, CHECK_WRITE, x87_ld80, x87_st80) :
   - précision de 53 bits au lieu de 64 ;
   - `x87_ld80` tronque l'exposant (`& 0x3ff`, `:101-102`) et écrase les dénormaux ;
   - `x87_st80` et le zéro (commentaires « Elvira », « Ca-cyber ») ;
   - ZE seule levée (`x87_checkexceptions` vide, `:32`) ;
   - IRQ13 par `picint(1 << 13)` **même sur un XT**, sans second PIC — le 8087 y passe par NMI ;
   - `x87_reset` vide (`x87.c:97`) : le mot de contrôle au reset n'est pas 0x037F ;
   - ports F0h/F1h de l'AT (effacer l'occupation, reset du coprocesseur) non émulés.
5. **Le coût.** `exec386` ne change pas en `fpu=none` ; avec coprocesseur, un appel indirect
   par ESC, comme aujourd'hui.

## Les décisions — tranchées par Julien le 29 septembre 2026

1. **`fpu=none` par défaut partout.** Les lignes de base ne bougent pas. Un **287** sur le
   profil Rider 286 / 8900D, un **387** sur le profil Rider 386DX/33.
2. **Le 8087 est inclus**, en G4.6.
3. **Les modes d'arrondi sont émulés en C#, dès G4.3, COMME PCem LES APPLIQUE** (précisé le
   29/09 après la mesure de G4.0 : « on reproduit comme PCem pour le moment, et on trace
   les bugs ») : arrondi dirigé sur les seuls `opFADD` mémoire m32/m64/m16int/m32int, au plus
   près ailleurs — PB-48. La sémantique de `fesetround` est réécrite de C vers C# (TwoSum/FMA
   puis `Math.BitIncrement` / `BitDecrement`), marquée `// DEVIATION:`. **Aucun P/Invoke de
   `fesetround`, à aucun moment** — il toucherait le MXCSR que le JIT partage. Tout autre
   défaut de PCem relevé en G4 : même traitement, reproduit et inscrit (PB-nn).
4. **Si G4.0 mesure un écart de libm** : P/Invoke direct vers la libm, marqué
   `// DEVIATION:`, pour garder la parité avec l'oracle. **Mesuré : aucun écart** — `Math.*`
   suffit, pas de P/Invoke.
5. **Le 287XL est inclus** (temps du 387, `cpu.c:1144-1145`).
6. **Les témoins** : QBASIC et MSD (DOS 5), Windows 3.1 (Calculatrice, WIN87EM). **Le banc**
   (CHECKIT, Landmark ou une fractale) reste **ouvert** : Julien le fournira.
7. **Ce fichier** : `PLAN-G4.md`.

## Ce que je ne sais pas encore

- ~~Si `Math.*` et la glibc rendent les mêmes bits~~ : **oui**, mesuré en G4.0.
- ~~Si `fesetround` agit vraiment dans l'oracle compilé en -O2~~ : **oui**, mais PCem ne
  l'appelle que pour FADD mémoire (PB-48).
- Combien de handlers et de sites `fesetround` la macro `opFPU` engendre : à compter après
  expansion (`gcc -E`), comme `ops386-table.py` pour G2.
- Si un BIOS AMI 286 ou 386 sonde le coprocesseur par F0h/F1h — ce qui le ferait diverger
  du silicium sans diverger de l'oracle.
