# Faits vérifiés qui contredisent l'intuition

Déplacés de TRANSCRIPTION.md le 1er octobre 2026 (R3) ; inchangés.

Chacun a été vérifié dans l'arbre, pas déduit.

1. **`808x.c` n'utilise ni `_mem_exec` ni `getpccache`** (`grep -c` → 0). Il passe par
   `readlookup2`/`writelookup2`, granularité **4 Ko**, sentinelle `-1`. Et
   `addreadlookup` facture **`cycles -= 9`** (`mem.c:378`).
2. **`memcycs` n'est pas uniforme.** Lecture : `if (a != (cs + cpu_state.pc)) memcycs += 4;`
   (`:62`). `readmembf`, la variante de préfetch, ne facture **rien** (`:69-75`). Écriture :
   inconditionnel (`:98`). Mots : `+= (8 >> is8086)`, même garde.
3. **`execx86` n'est pas borné par `timer_target`** (contrairement à `exec386`, `386.c:163`).
   C'est `cycles += cycs; while (cycles > 0)` (`:1222`), avec `clockhardware()` appelé par
   instruction (`:3939`) et six fois dans `rep()`. TSC en virgule fixe 32:32 :
   `tsc_frac += (uint64_t)diff * xt_cpu_multi` (`:893-904`).
4. **`setpitclock` écrit onze globales**, pas neuf (`pit.c:37-59`), puis diffuse
   `video_updatetiming()` et `device_speed_changed()`. C'est le domaine d'horloge partagé,
   et c'est ce qui interdit un refactor « struct d'instance ».
5. **Les décalages par CL sont des boucles par comptage** (`:2984, 3138`), pas des
   décalages larges — donc pas de divergence C-UB / masquage C#. Ça reviendra au palier (b).
6. **La sémantique multi-thread de PCem sous Linux n'existe pas.** `thread_reset_event`
   est vide (`thread-pthread.c:46`), `thread_wait_event` n'a pas de boucle de prédicat.
   Le handshake de `video.c:1132-1144` dégénère en attente active sur un `int` non
   atomique. Il n'y a pas de fidélité à préserver : iXtal26 est mono-thread.
7. **Une propriété `ref` n'est un `#define` qu'en deçà de ~922 locales par méthode.**
   RyuJIT refuse *tout* inlining — `AggressiveInlining` compris — dès que
   `lvaCount ≥ 0,9 × JitMaxLocalsToTrack` (`fginline.cpp`), et les temporaires du switch
   de `execx86` (un par `x -= n` sur un byref) y arrivent avant le premier candidat :
   1 507 `call` dans le Tier1, dont 898 vers des accesseurs d'une ligne. D'où la 7e
   entrée de R4. `JitDisasmSummary` ne le montre pas (« Tier1 »), seul `JitDisasm` le
   montre ; la porte G2 n'avait vérifié que la compilation. VERIFICATION.md § M5.
