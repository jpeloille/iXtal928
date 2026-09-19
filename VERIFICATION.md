# Journal de vérification

Le registre des **mesures**. TRANSCRIPTION.md porte les règles et les conventions — un
document borné, qui ne bouge que quand une règle change. Celui-ci porte ce qu'on a
constaté : résultats de portes, divergences, injections de panne. Il grandit à chaque
jalon, et c'est normal.

La séparation n'est pas cosmétique : le plafond de 200 lignes de TRANSCRIPTION.md est
l'anticorps contre la prose de conception qui a tué le projet précédent. Une mesure n'est
pas de la prose, et elle ne doit pas être rationnée.

---

## Où PCem s'écarte du silicium

Chiffres dans **`sst-baseline.tsv`**, généré par
`iXtal26.Diff sst-probe --baseline` : c'est de la donnée, pas de la prose, et elle grandira
à chaque forme sondée. **Le critère n'est pas « 100 % de SST » mais « le C# reproduit la
colonne `passe` à l'identique ».**

Sondé sur un échantillon *volontairement adverse* (2 000 cas × 19 formes, les opcodes les
plus retors) : **100 % sur tout ce qui est ordinaire** — ADD, MOV, NOP, MOVSB, CALL, INC,
MUL, SHL 1, AAA/AAS. Les masques de `metadata.json` sont indispensables : `37` passe de
117/2000 à 2000/2000 une fois appliqué.

Cinq écarts réels, à transcrire tels quels et à marquer `// pcem bug, reproduced:` —
`F6.6`/`F7.6` DIV (**débordement de quotient non détecté** : vérifié à la main sur
`F6.6[2]`, AX=36562 ÷ 12 = 3046 > 255, le 8088 lève INT 0, PCem tronque à 0xE6 et
poursuit) · `D0.6` SETMO (opcode non documenté, non implémenté) · `AD` REP LODSW ·
`D2.4` SHL par CL (un seul bit : **OF**) · `27`/`2F` DAA/DAS (AL, ~1–2 % des cas).

Performance relevée : 38 000 cas en 61 s, dominés par `h_reset()` qui remet à `-1` les 16 Mo
de `readlookup2`/`writelookup2` à chaque cas — ~85 min pour le corpus complet contre 2 min
visées. Le harnais devra réinitialiser ces tables paresseusement.

---

## Résultats des portes M0

Mesurés le 2026-09-19, .NET 10.0.112, x86-64 Linux.

| Porte | Résultat |
|---|---|
| **G1** propriété `ref` | **Verte.** `cycles -= 3;`, passage `ref`, `tsc += 0x1_0000_0000UL` mutent bien le champ référencé. |
| **G2** switch géant + `goto` | **Verte.** **180 106 octets d'IL** pour 256 cas / ~13 900 lignes ; JIT propre en Debug, Release, et sous `DOTNET_TieredCompilation=0` (pas de bailout). Le switch de `808x.c` fait ~2 600 lignes, soit ~1/5 : **marge 5×**, la contingence « découper par quartet » est inutile. |
| **G3** union explicite | **Verte.** Aliasing `l`/`w`/`b.h`/`b.l`, écriture de demi-octet préservant le reste, `getr8`/`setr8` indexés avec le pliage haut/bas sur le bit 2, et absence de mutation à travers une copie de struct. |
| **G4** SDL3-CS sous net10 | **Verte.** Restaure et compile ; `dotnet run -- --frames 5` ouvre une fenêtre, rend 5 images, sort 0. |

### Le harnais attrape-t-il vraiment ? (M1.0b)

Un fuzzer vert ne prouve rien tant qu'on n'a pas vérifié qu'il sait rougir. Trois
pannes injectées dans `808x.cs`, puis révoquées — **les trois sorties sur la première
instruction**, en nommant le bon champ :

| Panne injectée | Détectée |
|---|---|
| `cycles -= 8` → `-= 9` (coût de l'opcode indéfini) | `cycles consommés : oracle 16, C# 17` |
| `fetchcycles = 4` → `= 5` (rechargement de la file) | `cycles consommés : oracle 16, C# 15` |
| suppression de `fetchclocks += …` dans `FETCH` | `fetchcycles : oracle 12, C# 16` |

Passe verte de référence : **200 000 instructions** (2 000 rondes x 100), 32 champs
comparés, zéro divergence, 17 s. Le hachage de la RAM se fait en fin de ronde et non par
instruction — 1 Mo par côté et par instruction représentait des centaines de Go et rendait
la passe interminable ; `--ram-per-instr` le rétablit pour localiser une divergence.

**Point aveugle mesuré, à lever en M1.1.** La quatrième panne — supprimer la garde
`if (a != (cs + cpu_state.pc))` sur `memcycs += 4` — **n'est PAS détectée** : 900
instructions vertes. `0xCE` ne passe que par `readmembf`, qui n'a ni garde ni
comptabilisation ; `readmemb` n'est jamais atteinte. C'est exactement l'accord vide que
les compteurs de stubs traquent ailleurs. **Le premier opcode à opérande mémoire de M1.1
doit rejouer cette injection avant d'être déclaré fini.**

L'échafaudage (`M0Gates.cs`, `M0Gates.Generated.cs`, le drapeau `--gates`) est supprimé
une fois ces résultats consignés. Il est regénérable depuis l'historique git si une
régression de runtime remet G2 en question.

---

## M4.0 — Diff de traces d'amorçage : oracle contre C#, depuis le reset

Le fuzzer compare une instruction sur un état fabriqué ; SST compare une instruction
contre du silicium. Ni l'un ni l'autre ne fait tourner une **machine**. Le troisième
oracle est le diff de traces : les deux cœurs amorcent la même ROM d'IBM PC 5150 et on
compare instruction par instruction.

Deux phases, comme prévu au plan. Phase 1 : un hachage FNV-1a de 8 octets par instruction
(CS, pc, les huit registres, DS/ES/SS, flags, tsc) — assez léger pour tracer 700 000
instructions des deux côtés. Phase 2 : rejeu en pas à pas jusqu'à l'index fautif, avec le
vecteur d'état complet des 32 champs.

**Ce que la phase 2 a dû apprendre à ne pas comparer.** Les deux premières exécutions se
sont arrêtées sur `n_inb` puis sur `ins` — des compteurs de diagnostic, absents du
hachage de phase 1, donc *incapables* d'être la divergence cherchée. Ils divergeaient
parce que `h_boot()` et `initpc()` ne les remettaient pas à zéro au même moment, et le
diff s'arrêtait sur son propre bruit avant d'atteindre la vraie panne. Corrigé des deux
côtés (`h_stub_counters_reset` dans `h_boot`, `_808x.ResetDiagState()` symétrique) et
`CompareStates(counters: false)` pour le boot.

### Ce que le diff a trouvé : le PIT ne tournait pas du tout

Première divergence architecturale à l'instruction **24 664**, `F000:E0E8`, `IN AL, 0x41` :
oracle `0x00`, iXtal26 `0xFB`. Une sonde ajoutée sur les trois canaux du PIT (`h_pit_probe`
lit la globale `pit` de `pit.c` — le C n'est pas instrumenté, il est lu) a reporté le
premier écart **24 598 instructions plus tôt**, à l'instruction 66, `F000:E0C1`,
`OUT 0x61, 0xFC` : `pit.count[2]`, oracle 0, C# 65535.

État d'entrée identique des deux côtés. Les deux exécutent la même branche de
`pit_set_gate_no_timer`, et `pit.timer[2].ts` finit identique. Seul `timer_enable()`
diverge — parce que le C commence par `if (!timer_valid(timer)) return;` et que le C#
n'avait pas ce test.

Deux défauts distincts, tous deux confirmés par la source :

1. **`timer.cs` omettait la couche de validité** — `magic`, `all_timers[]`, `num_timers`,
   `timer_valid()`. Je l'avais classée « détection d'use-after-free, inatteignable sous
   GC ». C'est faux : `timer_reset()` remet `magic` à zéro sur **tous** les chronomètres
   enregistrés (`timer.c:162`), et `timer_valid` les rend alors inertes jusqu'au prochain
   `timer_add()`. C'est du contrôle de flux vivant, pas du durcissement.

2. **`pc_reset()` appelait `timer_reset()`** — que `pc.c:178` porte **en commentaire**.
   Combiné au défaut n°1, il invalidait tous les chronomètres que `model_init()` venait
   d'enregistrer : la machine tournait sans PIT, donc sans IRQ0 et sans rafraîchissement
   DRAM. Symétriquement, `setpitclock(14318184.0)` appartient à `pc_reset()`
   (`pc.c:184-187`), donc **après** `pit_init()`, et non à `initpc()` où je l'avais mis.

Le second défaut seul faisait disparaître le symptôme. Le premier est une lacune de
transcription : sans lui, la divergence serait revenue plus loin, au prochain
`resetpchard()`.

### Mesure

| | |
|---|---|
| Amorçage 20 tranches (0,2 s émulée) | **88 459 instructions, identiques** |
| Amorçage 200 tranches (2 s émulée) | **714 879 instructions, identiques** |
| Champs comparés par instruction (phase 2) | 32, plus 19 champs de sonde PIT par canal |

Le nombre d'instructions par tranche a changé avec le correctif (87 611 → 88 459 sur 20
tranches) : c'est la signature du PIT qui se met enfin à tourner.

**Ce que cette mesure ne prouve pas.** Le fuzzer n'a jamais pu attraper ce défaut et ne le
pourra jamais : il passe par `FlatMap()`/`Reset()`, qui n'appelle `timer_add` qu'une fois
par processus et jamais `timer_reset()` après enregistrement. SST ne touche pas aux
chronomètres. Un fuzzer vert après ce correctif ne dit donc rien sur ce correctif — seule
la longueur du préfixe d'amorçage identique le mesure.
