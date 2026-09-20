// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// OÙ PASSENT LES CYCLES DE L'AMORÇAGE, MESURÉS ET NON MODÉLISÉS.
//
// Un agent a PRÉDIT le coût du test mémoire du POST en transcrivant à la main le
// modèle de temps de 808x.c (charges par opcode + file de préfetch) et a trouvé
// 49,0 s contre 52,5 s mesurées, soit -6,6 %. Cette prédiction ne dit pas OÙ le
// résidu vit. Ce fichier le mesure : il avance instruction par instruction sur
// tout l'amorçage et impute les cycles CONSOMMÉS à l'adresse linéaire de
// l'instruction. Le profil par adresse tranche alors sans modèle :
//   - combien de cycles la boucle F000:E02E consomme-t-elle réellement,
//   - combien d'itérations elle exécute (donc combien de blocs sont testés),
//   - et ce que le POST fait du reste du temps.
//
// Attention : on avance ici par Step() (budget d'un cycle), pas par runpc()
// (budget de 47 727). Le découpage change la frontière de tranche, donc le
// compte de cycles peut différer de quelques pour mille de celui de runpc().
// Le récapitulatif imprime les deux pour que l'écart reste visible.

using iXtal26;
using iXtal26.Cpu;
using iXtal26.Memory;

namespace iXtal26.Diff;

public static class BootProfile
{
    // Adresse linéaire de la boucle interne du test mémoire, BIOS 27/10/82 :
    // AC | 32 C4 | 75 25 | 8A C2 | AA | E2 F6 à F000:E02E.
    private const uint LoopLin = 0xFE02E;
    private const uint LoopEnd = 0xFE038; // exclu : premier octet après E2 F6

    public static int Run(string romsPath, int instructions)
    {
        _808x.ResetDiagState();
        if (!pc.initpc(romsPath))
            return 1;

        // Imputation par adresse linéaire. 1 Mio d'entrées longues = 8 Mio :
        // c'est le prix d'un profil sans échantillonnage ni hypothèse.
        var cyclesAt = new long[0x100000];
        var hitsAt = new long[0x100000];

        // Datation : à quel instant (en cycles depuis le reset) chaque adresse
        // est exécutée pour la première et la dernière fois. Sans cela le profil
        // dit COMBIEN mais pas QUAND, et l'on ne peut pas dire si une phase
        // tombe dans la fenêtre « tranches 150-5400 » ou après elle.
        var firstAt = new long[0x100000];
        var lastAt = new long[0x100000];

        long consumed = 0;
        long n = 0;

        for (; n < instructions; n++)
        {
            var lin = (uint)((x86.cs + _386_common.cpu_state.pc) & 0xFFFFF);
            var c = _808x.Step();
            consumed += c;
            cyclesAt[lin] += c;
            if (hitsAt[lin] == 0)
                firstAt[lin] = consumed;
            lastAt[lin] = consumed;
            hitsAt[lin]++;

            // On s'arrête net à l'invite BASIC : F000:E84D est le point d'arrêt
            // relevé par --boot roms 6000. Sans cela on profilerait la boucle
            // d'attente clavier, qui n'appartient pas à l'amorçage.
            if (consumed > 100_000_000 && lin == 0xFE84D)
                break;
        }

        const double CpuHz = 4772728.0;
        const int SliceBudget = 4772728 / 100;

        Console.WriteLine("=== PROFIL DE L'AMORÇAGE PAR ADRESSE — cycles CONSOMMÉS ===\n");
        Console.WriteLine($"  instructions          : {n,15:N0}");
        Console.WriteLine($"  cycles consommés      : {consumed,15:N0}");
        Console.WriteLine($"  soit                  : {consumed / CpuHz,15:F3} s à 4 772 728 Hz");
        Console.WriteLine($"  soit                  : {consumed / (double)SliceBudget,15:F1} tranches de runpc()");
        Console.WriteLine($"  cycles par instruction: {consumed / (double)n,15:F3}");
        Console.WriteLine();

        // --- la boucle du test mémoire, mesurée -------------------------------
        long loopCycles = 0, loopHits = 0;
        for (var a = LoopLin; a < LoopEnd; a++)
        {
            loopCycles += cyclesAt[a];
            loopHits += hitsAt[a];
        }

        var iters = hitsAt[LoopLin]; // le LODSB : une exécution = une itération
        Console.WriteLine("--- boucle interne du test mémoire, F000:E02E ---");
        Console.WriteLine($"  itérations (exécutions du LODSB en E02E) : {iters,15:N0}");
        Console.WriteLine($"  instructions dans la boucle              : {loopHits,15:N0}" +
                          $"  ({100.0 * loopHits / n:F2} % de l'amorçage)");
        Console.WriteLine($"  cycles dans la boucle                    : {loopCycles,15:N0}" +
                          $"  ({100.0 * loopCycles / consumed:F2} % de l'amorçage)");
        Console.WriteLine($"  cycles par itération                     : {loopCycles / (double)iters,15:F3}");
        Console.WriteLine($"  durée de la boucle seule                 : {loopCycles / CpuHz,15:F3} s");
        Console.WriteLine();
        Console.WriteLine("  détail par instruction de la boucle :");
        string[] mn = { "AC LODSB", "32 C4 XOR AL,AH", "", "75 25 JNZ", "", "8A C2 MOV AL,DL", "", "AA STOSB", "E2 F6 LOOP", "" };
        for (var a = LoopLin; a < LoopEnd; a++)
        {
            if (hitsAt[a] == 0)
                continue;

            Console.WriteLine($"    F000:{a - 0xF0000:X4}  {mn[a - LoopLin],-16} " +
                              $"exéc {hitsAt[a],12:N0}  cycles {cyclesAt[a],14:N0}  " +
                              $"{cyclesAt[a] / (double)hitsAt[a],7:F3} /exéc");
        }

        // Nombre de blocs de 16 Kio déduit du compte d'itérations : cinq passes
        // par bloc, 16 384 octets par passe.
        Console.WriteLine();
        Console.WriteLine($"  blocs de 16 Kio impliqués si 5 passes : {iters / (5.0 * 16384),8:F3}");
        Console.WriteLine($"  octets testés (itérations / 5)        : {iters / 5,15:N0}" +
                          $"  = {iters / 5 / 1024.0,8:F1} Kio");
        Console.WriteLine();

        // --- le reste de l'amorçage, par adresse ------------------------------
        var rest = consumed - loopCycles;
        Console.WriteLine($"--- hors boucle E02E : {rest:N0} cycles = {rest / CpuHz:F3} s ---");
        Console.WriteLine("  vingt adresses les plus coûteuses :");

        var idx = new int[0x100000];
        for (var i = 0; i < idx.Length; i++) idx[i] = i;
        Array.Sort(idx, (a, b) => cyclesAt[b].CompareTo(cyclesAt[a]));

        var shown = 0;
        for (var k = 0; k < idx.Length && shown < 20; k++)
        {
            var a = (uint)idx[k];
            if (a >= LoopLin && a < LoopEnd)
                continue;
            if (cyclesAt[a] == 0)
                break;

            var seg = a >= 0xF0000 ? $"F000:{a - 0xF0000:X4}" : $"lin {a:X5}";
            Console.WriteLine($"    {seg,-12} exéc {hitsAt[a],12:N0}  cycles {cyclesAt[a],13:N0}  " +
                              $"{cyclesAt[a] / CpuHz,8:F3} s  {100.0 * cyclesAt[a] / consumed,6:F2} %  " +
                              $"tranches {firstAt[a] / SliceBudget,5:N0}→{lastAt[a] / SliceBudget,5:N0}");
            shown++;
        }

        Console.WriteLine();
        Console.WriteLine("--- datation des phases (en tranches runpc de 10 ms depuis le reset) ---");
        Console.WriteLine($"  boucle E02E du test mémoire : tranches {firstAt[LoopLin] / SliceBudget,5:N0} → " +
                          $"{lastAt[LoopLin] / SliceBudget,5:N0}");
        Console.WriteLine($"  amorçage terminé (E84D)     : tranche  {consumed / SliceBudget,5:N0}");

        return 0;
    }

    /// <summary>D'OÙ VIENNENT LES CYCLES QUI N'ARRIVENT JAMAIS AU TSC.
    ///
    /// Hypothèse à départager : clockhardware() (808x.c:893-904) banque d'abord
    /// « tsc += tsc_frac >> 32 », PUIS appelle timer_process(). Sur XT, celui-ci
    /// redescend par pit_refresh_timer_xt → dma_channel_read(0) → refreshread()
    /// → FETCHCOMPLETE(), qui fait « cycles -= (4 - (fetchcycles &amp; 3)) ». Ce
    /// retrait a lieu APRÈS que le diff a été pris ; l'instruction suivante
    /// refait « cycdiff = cycles » sur la valeur déjà amputée, et ces cycles ne
    /// figurent dans aucun diff — ni en mode Step, ni en mode runpc.
    ///
    /// Le test ne touche pas au cœur : il compare, instruction par instruction,
    /// la perte de conversion au NOMBRE d'appels à timer_process survenus
    /// pendant cette instruction (compteur Diag déjà présent). Si l'hypothèse
    /// est juste, les instructions à zéro appel ne perdent rien.</summary>
    public static int Refresh(string romsPath, long skipCycles, int window)
    {
        _808x.ResetDiagState();
        if (!pc.initpc(romsPath))
            return 1;

        var st = Diag.HState.Create();
        long consumed = 0;

        while (consumed < skipCycles)
            consumed += _808x.Step();

        long lostWith = 0, lostWithout = 0;
        long nWith = 0, nWithout = 0, calls = 0;
        long cycWith = 0, cycWithout = 0;

        for (var i = 0; i < window; i++)
        {
            _808x.GetState(ref st);
            var t0 = ((UInt128)st.tsc << 32) + st.tsc_frac;
            var p0 = Diag.Counters.n_timer_process;

            var c = _808x.Step();

            _808x.GetState(ref st);
            var t1 = ((UInt128)st.tsc << 32) + st.tsc_frac;
            var dp = (long)(Diag.Counters.n_timer_process - p0);

            // tsc et tsc_frac forment un 32:32 : il faut les deux, sinon on
            // confond « rien converti » et « converti moins d'une unité ».
            var converted = (long)((t1 - t0) / _808x.xt_cpu_multi);
            var lost = c - converted;

            calls += dp;
            if (dp > 0) { nWith++; cycWith += c; lostWith += lost; }
            else { nWithout++; cycWithout += c; lostWithout += lost; }
        }

        Console.WriteLine("=== LE MANQUE DE COMPTABILITÉ EST-IL LE RAFRAÎCHISSEMENT DRAM ? ===\n");
        Console.WriteLine($"  fenêtre : {window:N0} instructions à partir du cycle {skipCycles:N0}\n");
        Console.WriteLine($"  instructions SANS appel à timer_process : {nWithout,12:N0}  " +
                          $"cycles {cycWithout,12:N0}  PERDUS {lostWithout,10:N0}");
        Console.WriteLine($"  instructions AVEC appel à timer_process : {nWith,12:N0}  " +
                          $"cycles {cycWith,12:N0}  PERDUS {lostWith,10:N0}");
        Console.WriteLine($"  appels à timer_process                 : {calls,12:N0}");
        Console.WriteLine();
        Console.WriteLine($"  perte totale                           : {lostWith + lostWithout,12:N0} cycles " +
                          $"({100.0 * (lostWith + lostWithout) / (cycWith + cycWithout):F3} % des cycles consommés)");
        Console.WriteLine($"  part imputable aux instructions AVEC   : " +
                          $"{100.0 * lostWith / (lostWith + lostWithout),12:F3} %");
        Console.WriteLine($"  perte par appel à timer_process        : {(lostWith + lostWithout) / (double)calls,12:F3} " +
                          "cycles  (FETCHCOMPLETE facture 4 − (fetchcycles & 3), donc 1 à 3)");
        Console.WriteLine($"  un cycle CPU sur                       : {(cycWith + cycWithout) / (double)calls,12:F1} " +
                          "déclenche un timer_process (canal 1 programmé à 18 ⟹ 72 cycles)");

        return 0;
    }
}
