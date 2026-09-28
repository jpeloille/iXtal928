# Statique ou instance, et l'écart avec l'oracle : ce qui a été mesuré

> Session du 26 septembre 2026, au commit `a0cc3de`. Aucun fichier de code n'a été modifié ni
> commité : les variantes ont été construites dans des worktrees jetables, supprimés depuis.
> Les leviers de performance, eux, sont dans `audit-performance-2026-09-26.md`. Cette note ne
> les répète pas, elle garde les mesures, le protocole et les réponses de principe.

## 1. Le protocole de mesure sur l'AT 286 sous Windows

Le banc `iXtal26.Diff bench` est un banc de 8088 par construction : budget de 4,77 MHz en
littéral, trajectoire du 5150 face à l'oracle C. Il ne passe jamais par `exec386` ni par la
Trident. Pour cette charge, on fait donc une mesure A/B côté C# seul.

- **Répertoire jetable** `/tmp/ixbench-run` : `roms` en lien symbolique, copie de `nvr/`, de
  `ixtal26-286.cfg` et de `os/vierge-hdd-type46.img` (qui contient Windows) sous le nom
  `os/golden.img`.
- **Disque neuf à chaque passe** : on recopie `golden.img` avant chaque lancement, sinon la
  passe B démarre sur un disque que la passe A a déjà modifié.
- **La charge** : le DOS, puis `WIN` et 2 minutes de Windows émulées.

  ```
  taskset -c 0-3 <binaire> --boot roms 3000 --config ixtal26-286.cfg --settle 12000 --type ZIN
  ```

- **La frappe passe par le clavier AZERTY** : `--type WIN` arrive comme `ZIN`, donc on tape
  `ZIN` pour obtenir `WIN`.
- **Binaires Release** compilés avec `dotnet build -c Release iXtal26 -o /tmp/ixbench-X`, et non
  `dotnet run`, qui compterait la compilation.
- **5 passes alternées** (A puis B, puis B puis A…), on retient le minimum et la médiane. Rider
  était ouvert : le bruit mesuré est d'environ ±0,5 %.
- **La trajectoire n'est pas déterministe.** À partir de la tranche ~2400, deux passes A
  diffèrent de quelques instructions sur 40 millions. La cause probable est l'horloge RTC lue
  sur l'hôte, non vérifiée. La charge reste comparable, mais ce n'est pas une trajectoire
  vérifiée comme celle du banc 8088.

## 2. Statique contre instance : les deux mesures

| Variante | Ce qui change | min | médiane |
|---|---|---|---|
| A | code actuel | 10,35–10,40 s | 10,39–10,43 s |
| B | la SVGA passe par un `static readonly svga_t svga_s`, et non plus par `(svga_t)p` | 10,38 s | 10,46 s |
| C | `cpu_state` perd son `readonly` (`Cpu/386_common.cs:23`) | 10,62 s | 10,71 s |

- **B, la vidéo : aucun écart mesurable.** `svga_t` est déjà une instance, dans PCem comme en
  C#. Le chemin vidéo ne pèse que 3,6 % du temps : même un gain réel s'y noierait.
- **C, le cœur : +3 % au total, sans recouvrement.** Toutes les passes C sont plus lentes que
  toutes les passes A. Le CPU faisant environ 65 % du temps, le cœur seul ralentit d'environ
  4 à 5 %.
- **Ce que C mesure exactement** : une référence statique relue à chaque accès. Ce n'est pas
  le cas d'un objet reçu en paramètre ou via `this` dans une méthode d'instance, qui n'a pas
  été mesuré.

Les patchs sont gardés dans `/tmp/ixbench-run/variante-B.patch` et `variante-C.patch`.

## 3. Le profil de la même charge

`dotnet-trace`, installé en outil global, échantillonne sans sudo, contrairement à `perf`, qui
exige `perf_event_paranoid`. Le dotTrace de Rider produit un `.dtp` qui ne s'ouvre que dans
l'interface.

```
dotnet-trace collect --profile dotnet-sampled-thread-time --format Speedscope -o A.nettrace -- <binaire> …
```

Dans le JSON, le temps propre est porté par un cadre `CPU_TIME` : on l'attribue au cadre géré
parent pour agréger.

| Poste | Part du temps |
|---|---|
| `exec386` | 40 % |
| `prefetch_run` | 10 % |
| Timers (`timer_process`, `timer_enable`, `timer_advance_u64`, `pit_over`) | ~9 % |
| Accès mémoire (`readmemw`, `writememw`, `fetch_ea_16`, `fastreadl`) | ~8 % |
| Vidéo en propre | 3,6 % |
| `svga_poll` inclusif, rendu compris | 7,4 % |
| `svga_write` / `svga_read` | ~0 % (Windows au repos) |
| `Delegate.InternalEqualMethodHandles` | 2,5 %, attribué à `svga_poll` par l'audit (P1) |

## 4. Écrire lisible avec des `static readonly`

Le gain ne vient pas du fait d'écrire des fonctions statiques. Il vient d'une référence
`static readonly` dont le JIT connaît l'adresse. On peut donc garder de vrais objets, avec des
méthodes et des noms explicites :

```csharp
internal sealed class Intel8259A
{
    internal byte Mask, InService, Pending;
    internal void Raise(int irq) { … }
    internal void Reset() { … }
}

internal static class Ics
{
    internal static readonly Intel8259A MasterPic = new();
    internal static readonly Intel8259A SlavePic  = new();
}
```

- **Ne jamais réaffecter le champ.** On remet à zéro par `Reset()`, pas par `= new()` : c'est
  la variante C.
- **`sealed`, sans interface ni `virtual`** sur le chemin chaud.
- **Des champs, pas des propriétés**, dans `exec386` et `execx86` : au-delà d'environ 922
  locales, RyuJIT n'inline plus rien.
- **`using static`** là où PCem écrit des fonctions libres.
- **La lisibilité passe par les noms** : un type par puce, un champ nommé par exemplaire, un
  vocabulaire de fiche technique. Le lien avec PCem reste assuré par `// pcem:` et `// noms:`.

C'est la forme à retenir pour la section « Après G8 — nommer les puces » de `PLAN.md`. Si on
y passe à des méthodes d'instance, il faudra d'abord la même mesure A/B sur le PIT, qui est
sur le chemin chaud.

## 5. La liste de conseils pour interpréteur, confrontée au code

| Conseil | Chez nous | Verdict |
|---|---|---|
| Lazy flags | `flags_op`, `flags_rebuild` (`Cpu/x86.cs:101`), repris de PCem | ✅ |
| Dispatch par `switch` | Le 8088 a un `switch` monolithique (`execx86`), le 286/386 une table de délégués `x86_opcodes[]` comme PCem | ⚠ voir ci-dessous |
| Pré-décodage / cache ModRM | Non, PCem en mode interprété non plus | ⏸ seulement si G6 ne tient pas le temps réel |
| Registres en champs nommés | `regs[8]`, un tableau de `x86reg` (`Cpu/x86.cs:58`) | ⚠ voir ci-dessous |
| `AggressiveInlining` | 17 usages dans `Cpu/` et `Memory/` | ✅ partiel |
| Pointeur brut sur la RAM | `mem.ram[readlookup2[addr >> 12] + addr]`, deux tableaux et deux bornes par accès | ❌ mais `unsafe` est exclu par le csproj (audit § 3), voir P4 |
| Pas d'appel virtuel, timers par lots | Pas d'interface. Timers testés par une comparaison à chaque instruction (`Cpu/386.cs:449`), comme PCem | ✅ |
| Zéro allocation dans la boucle | Voir audit P8 (mode protégé) | ❓ |

Deux affirmations de cette liste sont fausses dans notre cas :

- **« Le `switch` est strictement plus rapide qu'une table de délégués ».** C'est vrai pour un
  petit `switch`. Un `switch` de 256 cas aux corps volumineux fait passer la méthode au-delà du
  seuil d'environ 922 locales de RyuJIT, et plus rien n'y est inliné : c'est le piège de
  `execx86`. Pour le 286/386, la table est défendable. Le coût réel est dans les handlers bâtis
  en fermetures (audit P2).
- **« Des champs nommés, et un `switch` pour le ModRM ».** Remplacer `regs[r]` par un
  `switch` à 8 branches échange une vérification de bornes contre un saut indirect. Si un
  remède est nécessaire, c'est un tableau sans vérification de bornes, qui garde l'indexation
  de PCem (audit P5).

## 6. L'écart avec l'oracle

Ce qui est connu vient de M5.1, sur le 8088 en CGA (`VERIFICATION.md` § M5.1). Le C# est à
**1,39× l'oracle instrumenté** et à **2,12× le C de production**. Le cœur CPU est à parité :
`execx86` fait 0,32 s des deux côtés.

| Poste | C# | C | Surcoût | Levier |
|---|---|---|---|---|
| `cga_poll` | 1,55 s | 1,12 s | +0,43 s | vérifications de bornes sur ~31 M accès/s (`Buffer32`, `cgapal`) |
| Chauffe du JIT | 0,37 s | — | +0,37 s | coût fixe : ReadyToRun, réglages du tiering (audit P10) |
| Runtime (barrières d'écriture, `__tls_get_addr`, thunks de délégués) | 0,38 s | — | +0,38 s | à détailler |
| Chemin lent mémoire | 0,39 s | 0,21 s | +0,18 s | levier B : miroir exact de `--wrap`, refusé à M5.1 |
| `execx86` monolithique | — | — | ~5 % | le découper : refusé à M5.1 |

- **Estimation, non mesurée** : face à l'oracle instrumenté, on pourrait descendre vers
  1,1 à 1,2×. Face au C de production, la parité est peu probable : l'instrumentation
  `--wrap` rend l'oracle 50 % plus lent, et une partie de ce qui reste est la différence entre
  GCC `-O2` et RyuJIT.
- **Ces chiffres ne valent que pour le 8088.** La machine cible passe par `exec386` et la
  Trident. Aucune comparaison avec le C n'existe sur le 286 : il faudrait d'abord étendre le
  banc, l'oracle connaissant le 286 (pm-check s'en sert).
- **L'objectif décide.** Le 286 à 16 MHz a une marge de 27× sur le temps réel. Le gain
  deviendra peut-être nécessaire pour le 486 DX2-66, en G6.

**Première étape proposée, sans toucher au code** : mesurer la chauffe du JIT par variables
d'environnement (`DOTNET_TieredPGO`, `DOTNET_TieredCompilation=0`, `DOTNET_ReadyToRun`) sur la
charge Windows du § 1. Chaque levier de code reste soumis au chiffre et à l'accord de Julien.
