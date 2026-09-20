// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
// STATUS: host
//
// CONTRÔLE DE FRÉQUENCE ABSOLUE.
//
// Le diff par instruction répond à « iXtal26 = PCem ». Il ne répond pas à
// « PCem = vrai 5150 » : une dérive systématique du modèle de temps est
// identique des deux côtés et le diff ne la voit pas. Ce contrôle-ci la voit,
// parce qu'il compare un compteur que la MACHINE tient elle-même — le compteur
// de tops de la BDA, incrémenté par l'INT 8 du BIOS — à l'échelle de temps que
// l'émulateur PROMET : une tranche pc.runpc() = 10 ms.
//
// Aucun programme assembleur n'est nécessaire : le BIOS du 5150 programme le
// canal 0 du 8253 au diviseur 0 (= 65536), démasque l'IRQ 0, et son INT 8
// incrémente le dword 0040:006C (dépassement 24 h à 0040:0070).
//
// Les trois rapports imprimés ne testent PAS la même chose, et c'est le point :
//
//   1. Δtsc / (tranches/100) vs 14 318 184  — teste la conversion cycles CPU →
//      tsc : clockhardware() et xt_cpu_multi.
//   2. Δtops / (Δtsc/14 318 184) vs 18,2065 — teste PITCONST, le 8253, l'IRQ 0
//      et l'INT 8. QUASI-TAUTOLOGIQUE : 1 193 182 × 12 = 14 318 184 et
//      4 772 728 × 3 = 14 318 184 exactement, donc PITCONST = 12 × 2^32 et
//      xt_cpu_multi = 3 × 2^32 sans reste. Un top vaut exactement 786 432 tsc.
//      Ce rapport ne peut que retomber sur 18,2065 si PITCONST vaut 12.
//   3. Δtops / (tranches/100) vs 18,2065   — LA mesure de la tâche : la chaîne
//      entière, du budget de cycles de pc.runpc() jusqu'au compteur de la BDA.

using iXtal26.Cpu;
using iXtal26.Memory;
using iXtal26.Models;

namespace iXtal26;

internal static class TimerCheck
{
    // BDA : compteur de tops (dword) et drapeau de dépassement 24 h (octet).
    private const uint32_t BdaTicks = 0x0046C;
    private const uint32_t BdaRollover = 0x00470;

    // 1 193 182 / 65 536 — la fréquence attendue de l'IRQ 0, diviseur par défaut.
    private const double ExpectedHz = 1193182.0 / 65536.0;

    // L'oscillateur MAÎTRE du XT. C'est lui que setpitclock() reçoit (pc.c:184).
    private const double MasterHz = 14318184.0;

    private const int CpuHz = 4772728;

    // pc.c:473 — `int cycles_to_run = cpu_get_speed() / 100;`. DIVISION ENTIÈRE :
    // 4 772 728 / 100 = 47 727, pas 47 727,28. Une « seconde émulée » de cent
    // tranches n'accorde donc que 4 772 700 cycles, 5,87 ppm sous les
    // 4 772 728 Hz nominaux. Ce n'est pas une approximation de ma part : c'est
    // ce que PCem fait, et la constante est écrite ici sous la même forme pour
    // que la troncature reste lisible.
    private const int CyclesPerSlice = CpuHz / 100;

    private const double TwoPow32 = 4294967296.0;

    // Nombre de tranches d'amorçage avant toute mesure. Mesuré : à 6 000, la
    // machine est à l'invite de Cassette BASIC, le PIT est programmé depuis
    // longtemps et l'IRQ 0 est démasquée.
    private const int BootSlices = 6000;

    private static long _slices;

    private static void RunSlices(int n)
    {
        for (var i = 0; i < n; i++)
        {
                pc.runpc();
                _slices++;
        }
    }

    /// <summary>Le dword 0040:006C, octet par octet par le chemin physique — le
    /// même que DumpTextScreen emploie pour la VRAM, sans passer par le cache
    /// de traduction du CPU ni facturer de cycles.</summary>
    private static uint32_t ReadTicks()
        => mem.mem_readb_phys(BdaTicks)
           | ((uint32_t)mem.mem_readb_phys(BdaTicks + 1) << 8)
           | ((uint32_t)mem.mem_readb_phys(BdaTicks + 2) << 16)
           | ((uint32_t)mem.mem_readb_phys(BdaTicks + 3) << 24);

    /// <summary>Cycles CPU réellement consommés depuis le départ.
    ///
    /// execx86 fait `cycles += cycs; while (cycles > 0)` : le budget non dépensé
    /// — toujours négatif ou nul en sortie — se reporte. Le total consommé est
    /// donc le total accordé moins ce qu'il reste au compteur.</summary>
    private static long ConsumedCycles() => _slices * CyclesPerSlice - x86.cycles;

    internal static int Run(string roms, int seconds)
    {
        if (!pc.initpc(roms))
                return 1;

        Console.WriteLine("=== CONTRÔLE DE FRÉQUENCE ABSOLUE — IBM PC 5150 ===\n");

        Console.WriteLine("--- domaine d'horloge (posé par setpitclock(14318184), pc.c:184) ---");
        Console.WriteLine($"  PITCONST      = {pit.PITCONST,20}  = {pit.PITCONST / TwoPow32:F12} x 2^32");
        Console.WriteLine($"  xt_cpu_multi  = {_808x.xt_cpu_multi,20}  = {_808x.xt_cpu_multi / TwoPow32:F12} x 2^32");
        Console.WriteLine($"  TIMER_USEC    = {timer.TIMER_USEC,20}  = {timer.TIMER_USEC / TwoPow32:F12} x 2^32");
        Console.WriteLine($"  CGACONST      = {pit.CGACONST,20}");
        Console.WriteLine($"  budget de cycles par tranche (pc.c:473) = {CyclesPerSlice} " +
                          $"(= {CpuHz} / 100, division ENTIÈRE ; 100 tranches = {100L * CyclesPerSlice} cycles)");
        Console.WriteLine();

        // ---------------------------------------------------------------
        // Amorçage. On relève Δtsc tranche par tranche : si la conversion
        // cycles → tsc était exacte, chaque tranche vaudrait exactement
        // 3 × 47 727 = 143 181 tsc. Tout écart se voit ici, et se date.
        // ---------------------------------------------------------------
        // On relève AUSSI le manque cumulé « cycles consommés moins cycles
        // portés au tsc » tranche par tranche. Sans lui, un Δtsc bas est
        // ambigu : il peut venir d'une DETTE de cycles (une instruction longue
        // a dépassé le budget, les tranches suivantes la remboursent) ou d'une
        // vraie perte de comptabilité. Les deux se distinguent ici.
        var perSlice = new long[BootSlices];
        var perSliceGap = new long[BootSlices];
        var prevTsc = (long)timer.tsc;
        for (var i = 0; i < BootSlices; i++)
        {
                pc.runpc();
                _slices++;
                var now = (long)timer.tsc;
                perSlice[i] = now - prevTsc;
                prevTsc = now;
                perSliceGap[i] = ConsumedCycles() - now / 3;
        }

        const long NominalPerSlice = 3L * CyclesPerSlice;

        Console.WriteLine($"--- amorçage : {BootSlices} tranches ({BootSlices / 100.0:F0} s émulées) ---");
        Console.WriteLine($"  CS:IP = {x86.CS:X4}:{_386_common.cpu_state.pc:X4}, ins = {_808x.ins}, tsc = {timer.tsc}");
        Console.WriteLine($"  BDA equipement 0040:0010 = {mem.readmemwl(0x410):X4}, " +
                          $"taille memoire 0040:0013 = {mem.readmemwl(0x413)} Ko");
        Console.WriteLine($"  Δtsc nominal par tranche si la conversion était exacte : {NominalPerSlice}");

        var firstOff = -1;
        for (var i = 0; i < BootSlices && firstOff < 0; i++)
                if (perSlice[i] != NominalPerSlice)
                        firstOff = i;

        Console.WriteLine($"  première tranche dont Δtsc ≠ {NominalPerSlice} : " +
                          (firstOff < 0 ? "aucune" : $"{firstOff} (Δtsc = {perSlice[firstOff]})"));
        Console.WriteLine("  vingt premières tranches : " +
                          string.Join(" ", perSlice.Take(20)));
        // Bornes MESURÉES par boot-profile (tools/iXtal26.Diff/BootProfile.cs), pas
        // devinées : la boucle F000:E02E court des tranches 9 à 4864, et la ROM BASIC
        // est atteinte vers 5729. Des bornes codées en dur et fausses ont déjà coûté
        // cher — un libellé « 150-5400 » lu comme une mesure a fait conclure à tort
        // à 6,6 % d'écart entre le coût prédit du test mémoire et son coût réel,
        // qui est en fait de 0,16 %.
        ProfileRange(perSlice, 0, 9, "POST avant le test mémoire");
        ProfileRange(perSlice, 9, 4865, "test mémoire (LODSB/XOR/STOSB/LOOP à F000:E02E)");
        ProfileRange(perSlice, 4865, BootSlices, "fin de POST, attente disquette, invite BASIC");
        Console.WriteLine($"  manque cumulé « consommé − porté au tsc », en cycles, aux tranches " +
                          "0 / 150 / 1000 / 3000 / 5400 / 5999 :");
        Console.WriteLine($"    {perSliceGap[0]} / {perSliceGap[149]} / {perSliceGap[999]} / " +
                          $"{perSliceGap[2999]} / {perSliceGap[5399]} / {perSliceGap[BootSlices - 1]}");
        Console.WriteLine($"  budget restant au compteur (x86.cycles) en fin d'amorçage : {x86.cycles} " +
                          "— une dette, pas une perte, et elle est ici négligeable");
        Console.WriteLine();

        // ---------------------------------------------------------------
        // État du matériel AVANT de mesurer. Si le compteur ne bouge pas,
        // c'est ici — et pas dans la conclusion — qu'on saura pourquoi.
        // ---------------------------------------------------------------
        ReportHardware();

        // ---------------------------------------------------------------
        // Garde-fou exigé par le protocole : vérifier que le compteur BOUGE
        // DÉJÀ avant d'ouvrir la fenêtre de mesure. Une fenêtre où l'ISR ne
        // tourne pas encore mesurerait zéro et n'apprendrait rien.
        // ---------------------------------------------------------------
        var probe0 = ReadTicks();
        RunSlices(100);
        var probe1 = ReadTicks();
        Console.WriteLine($"--- amorce du compteur ---");
        Console.WriteLine($"  0040:006C avant 100 tranches = {probe0} (0x{probe0:X8}), après = {probe1} (0x{probe1:X8})");
        Console.WriteLine($"  0040:0070 (dépassement 24 h) = {mem.mem_readb_phys(BdaRollover)}");

        if (probe1 == probe0)
        {
                Console.Error.WriteLine("\n*** LE COMPTEUR DE TOPS NE BOUGE PAS. ***");
                Console.Error.WriteLine("Ne pas conclure que le timer est cassé : l'état matériel est imprimé");
                Console.Error.WriteLine("ci-dessus. Regarder d'abord IF, le masque du PIC et le mode du canal 0.");
                return 1;
        }

        Console.WriteLine($"  le compteur avance : +{probe1 - probe0} tops en 1 s émulée. Mesure autorisée.\n");

        // ---------------------------------------------------------------
        // La fenêtre. On l'aligne sur un FRONT du compteur aux deux bouts :
        // sans cela l'incertitude est de ±1 top sur le compte de tops ; avec,
        // le compte de tops est EXACT et l'incertitude passe sur la durée,
        // bornée par une tranche (10 ms) à chaque bout.
        // ---------------------------------------------------------------
        if (!AlignToTick(out var t0))
                return 1;

        var slices0 = _slices;
        var tsc0 = (long)timer.tsc;
        var consumed0 = ConsumedCycles();
        var ins0 = _808x.ins;

        RunSlices(seconds * 100);

        if (!AlignToTick(out var t1))
                return 1;

        var slices1 = _slices;
        var tsc1 = (long)timer.tsc;
        var consumed1 = ConsumedCycles();
        var ins1 = _808x.ins;

        var ticks = (long)(t1 - t0);
        var windowSlices = slices1 - slices0;
        var dtsc = tsc1 - tsc0;
        var dcons = consumed1 - consumed0;
        var granted = windowSlices * CyclesPerSlice;

        // Le temps émulé selon le CONTRAT de l'émulateur : une tranche = 10 ms.
        var emuSeconds = windowSlices / 100.0;

        Console.WriteLine("--- FENÊTRE DE MESURE (alignée sur un front du compteur aux deux bouts) ---");
        Console.WriteLine($"  tops        : {t0} → {t1}, soit {ticks} tops (compte EXACT : les deux bouts sont des fronts)");
        Console.WriteLine($"  tranches    : {windowSlices}  ⟹ {emuSeconds:F2} s émulées (contrat pc.runpc : 10 ms/tranche)");
        Console.WriteLine($"  tsc         : {tsc0} → {tsc1}, Δ = {dtsc}");
        Console.WriteLine($"  cycles CPU  : accordés {granted}, consommés {dcons}, reste au compteur {x86.cycles}");
        Console.WriteLine($"  instructions: {ins0} → {ins1}, Δ = {ins1 - ins0}");
        Console.WriteLine($"  0040:0070 (dépassement 24 h) = {mem.mem_readb_phys(BdaRollover)}");
        Console.WriteLine();

        // Incertitude : chaque alignement s'arrête au premier relevé POSTÉRIEUR
        // au front, donc dans [0, 1 tranche) après lui. La durée vraie de la
        // fenêtre est donc dans (windowSlices − 1, windowSlices + 1) tranches.
        var loSeconds = (windowSlices - 1) / 100.0;
        var hiSeconds = (windowSlices + 1) / 100.0;

        Console.WriteLine("--- 1. Δtsc par seconde émulée ---");
        Console.WriteLine("    (teste la conversion cycles CPU → tsc : clockhardware() + xt_cpu_multi)");
        var tscPerSec = dtsc / emuSeconds;
        Report(tscPerSec, MasterHz, "Hz (oscillateur maître)");
        Console.WriteLine();

        Console.WriteLine("--- 2. tops par seconde de tsc ---");
        Console.WriteLine("    (teste PITCONST + 8253 + IRQ 0 + INT 8 ; QUASI-TAUTOLOGIQUE, voir l'en-tête)");
        var fTsc = ticks * MasterHz / dtsc;
        Report(fTsc, ExpectedHz, "Hz");
        Console.WriteLine($"    tsc par top mesuré = {(double)dtsc / ticks:F3}, attendu 65536 × 12 = 786432");
        Console.WriteLine();

        Console.WriteLine("--- 3. tops par seconde ÉMULÉE — LA MESURE ---");
        Console.WriteLine("    (la chaîne entière : budget de cycles, clockhardware, PITCONST, PIT, IRQ 0, INT 8)");
        var fSlice = ticks / emuSeconds;
        Report(fSlice, ExpectedHz, "Hz");
        Console.WriteLine($"    encadrement par la quantification de l'alignement (±1 tranche) : " +
                          $"[{ticks / hiSeconds:F6} ; {ticks / loSeconds:F6}] Hz");
        Console.WriteLine($"    soit ±{1e6 / windowSlices:F1} ppm de largeur d'incertitude sur la fenêtre.");
        Console.WriteLine($"    sans alignement, la quantification à ±1 top aurait valu " +
                          $"±{1e6 / ticks:F1} ppm.");
        Console.WriteLine();

        Console.WriteLine("--- 4. cycles CPU par seconde émulée ---");
        Console.WriteLine($"    accordés par pc.runpc()  : {granted / emuSeconds:F3} /s " +
                          $"(attendu {CpuHz}, écart {Signed(Ppm(granted / emuSeconds, CpuHz), 2)} ppm " +
                          "— c'est la division entière de pc.c:473)");
        Console.WriteLine($"    réellement consommés     : {dcons / emuSeconds:F3} /s " +
                          $"(écart {Signed(Ppm(dcons / emuSeconds, CpuHz), 2)} ppm)");
        Console.WriteLine($"    comptés par clockhardware: {dtsc / 3.0 / emuSeconds:F3} /s " +
                          $"(écart {Signed(Ppm(dtsc / 3.0 / emuSeconds, CpuHz), 2)} ppm)");
        Console.WriteLine($"    cycles consommés mais JAMAIS portés au tsc : {dcons - dtsc / 3} " +
                          $"sur {dcons} ({100.0 * (dcons - dtsc / 3.0) / dcons:F4} %)");
        Console.WriteLine();

        return 0;
    }

    /// <summary>Avance tranche par tranche jusqu'à ce que le compteur de tops
    /// change, et rend sa valeur juste après le front. Borné : au diviseur par
    /// défaut un top tombe toutes les 5,5 tranches, donc 100 tranches sans front
    /// signifient que l'ISR s'est arrêtée.</summary>
    private static bool AlignToTick(out uint32_t value)
    {
        var start = ReadTicks();
        for (var i = 0; i < 100; i++)
        {
                pc.runpc();
                _slices++;
                var now = ReadTicks();
                if (now != start)
                {
                        value = now;
                        return true;
                }
        }

        value = start;
        Console.Error.WriteLine("*** alignement impossible : 100 tranches sans aucun top. ***");
        ReportHardware();
        return false;
    }

    private static void ReportHardware()
    {
        var flags = _386_common.cpu_state.flags;
        Console.WriteLine("--- état du matériel au moment de la mesure ---");
        Console.WriteLine($"  drapeaux CPU = {flags:X4} ; IF (0x0200) = {((flags & x86.I_FLAG) != 0 ? "1 — interruptions AUTORISÉES" : "0 — interruptions INHIBÉES")}");
        Console.WriteLine($"  PIC maître : mask = {pic.pic_.mask:X2} (bit 0 = IRQ 0 : " +
                          $"{((pic.pic_.mask & 1) != 0 ? "MASQUÉE" : "démasquée")}), " +
                          $"pend = {pic.pic_.pend:X2}, ins = {pic.pic_.ins:X2}, vecteur de base = {pic.pic_.vector:X2}");
        Console.WriteLine($"  PIT canal 0 : mode m[0] = {pit.pit_.m[0]}, diviseur l[0] = {pit.pit_.l[0]} " +
                          $"(0 écrit ⟹ 65536), ctrl = {pit.pit_.ctrl:X2}, " +
                          $"enabled = {pit.pit_.enabled[0]}, gate = {pit.pit_.gate[0]}, " +
                          $"using_timer = {pit.pit_.using_timer[0]}, out = {pit.pit_.@out[0]}");
        Console.WriteLine($"  vecteur INT 8 (0000:0020) = {mem.readmemwl(0x22):X4}:{mem.readmemwl(0x20):X4}");
        Console.WriteLine();
    }

    private static void ProfileRange(long[] d, int from, int to, string label)
    {
        if (from >= to || to > d.Length)
                return;

        long min = long.MaxValue, max = long.MinValue, sum = 0;
        for (var i = from; i < to; i++)
        {
                if (d[i] < min) min = d[i];
                if (d[i] > max) max = d[i];
                sum += d[i];
        }

        var n = to - from;
        Console.WriteLine($"  tranches [{from,5};{to,5}) {label,-50} " +
                          $"Δtsc min {min,7} max {max,7} moyen {(double)sum / n,10:F1}");
    }

    private static double Ppm(double measured, double expected)
        => (measured - expected) / expected * 1e6;

    /// <summary>Signe toujours explicite. Les sections « +0.00;-0.00 » d'un
    /// format .NET produisent « -+0.0000 » sur une valeur qui arrondit à zéro par
    /// en dessous ; un écart de -0,04 ppm est exactement le cas intéressant.</summary>
    private static string Signed(double v, int digits)
        => (v >= 0 ? "+" : "-") + Math.Abs(v).ToString("F" + digits);

    private static void Report(double measured, double expected, string unit)
    {
        var ppm = Ppm(measured, expected);
        Console.WriteLine($"    mesuré  = {measured:F6} {unit}");
        Console.WriteLine($"    attendu = {expected:F6} {unit}");
        Console.WriteLine($"    écart   = {Signed(ppm, 2)} ppm ({Signed(ppm / 1e4, 5)} %)");
    }
}
