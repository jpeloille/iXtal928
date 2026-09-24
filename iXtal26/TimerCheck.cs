// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
// STATUS: host
//
// CONTRÔLE DE FRÉQUENCE ABSOLUE, et CADENCE du processeur émulé.
//
// Le diff par instruction répond à « iXtal26 = PCem ». Il ne répond pas à
// « PCem = vraie machine » : une dérive systématique du modèle de temps est
// identique des deux côtés et le diff ne la voit pas. Ce contrôle-ci la voit,
// parce qu'il compare un compteur que la MACHINE tient elle-même — le compteur
// de tops de la BDA, incrémenté par l'INT 8 du BIOS — à l'échelle de temps que
// l'émulateur PROMET : une tranche pc.runpc() = 10 ms.
//
// Aucun programme assembleur n'est nécessaire : le BIOS programme le canal 0 du
// 8253 au diviseur 0 (= 65536), démasque l'IRQ 0, et son INT 8 incrémente le dword
// 0040:006C (dépassement 24 h à 0040:0070). C'est vrai du 5150 comme de l'AT.
//
// DEUX DOMAINES DE TSC, et c'est ce qui rend l'outil valable sur les deux cœurs.
// Le tsc compte TOUJOURS dans l'unité de l'horloge que setpitclock() a reçue
// (pit.cpuclock) : TIMER_USEC, PITCONST et RTCCONST en dérivent tous.
//   - 808x : setpitclock(14 318 184), l'oscillateur maître ; clockhardware()
//     convertit un cycle CPU en xt_cpu_multi / 2^32 tsc — exactement 3 à 4,77 MHz.
//   - 286  : setpitclock(vitesse du CPU) ; exec386 ajoute les cycles bruts au tsc
//     (386.cs, `timer.tsc += ins_cycles`) — 1 tsc par cycle.
// Si le budget de cycles de runpc() ne correspond pas à l'horloge posée par
// setpitclock(), le temps de l'invité ne court plus à la vitesse du temps mural.
// C'est exactement ce que les rapports 1, 3 et 6 mesurent.
//
// Les rapports imprimés ne testent PAS la même chose, et c'est le point :
//
//   1. Δtsc / (tranches/100) vs cpuclock — teste le budget de runpc() contre le
//      domaine d'horloge : cycles accordés × tsc par cycle = horloge ?
//   2. Δtops / (Δtsc/cpuclock) vs 18,2065 — teste PITCONST, le 8253, l'IRQ 0 et
//      l'INT 8. QUASI-TAUTOLOGIQUE : PITCONST et le tsc dérivent de la même
//      horloge ; ce rapport ne peut que retomber sur 18,2065.
//   3. Δtops / (tranches/100) vs 18,2065 — la chaîne entière, du budget de
//      cycles de pc.runpc() jusqu'au compteur de la BDA.
//   4. cycles accordés, consommés, et portés au tsc.
//   5. FRÉQUENCE VUE PAR L'INVITÉ — cycles consommés par seconde de BDA. C'est ce
//      qu'un programme qui chronomètre une boucle avec le PIT mesurerait.
//   6. TEMPS INVITÉ / TEMPS CONTRACTUEL — secondes de BDA par seconde de tranches.
//      Sous le frein de l'hôte, une tranche dure 10 ms murales : ce rapport est
//      alors la vitesse du temps de l'invité rapportée au temps réel.
//   7. HÔTE, NON DÉTERMINISTE — marge, capacité, MIPS, CPI. Les seuls chiffres de
//      cet outil qui dépendent de la machine qui l'exécute.

using System.Diagnostics;
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

    private const double TwoPow32 = 4294967296.0;

    // Nombre de tranches d'amorçage avant toute mesure, par défaut. Mesuré sur le
    // 5150 à 640 Ko : à 6 000, la machine est à l'invite de Cassette BASIC, le PIT
    // est programmé depuis longtemps et l'IRQ 0 est démasquée. --boot-slices N le
    // change ; le garde-fou « le compteur avance » vérifie de toute façon.
    internal const int DefaultBootSlices = 6000;

    private static long _slices;

    // Posés après initpc, parce qu'ils dépendent de la machine choisie.
    //
    // pc.c:473 — `int cycles_to_run = cpu_get_speed() / 100;`. DIVISION ENTIÈRE :
    // 4 772 728 / 100 = 47 727, pas 47 727,28. Une « seconde émulée » de cent
    // tranches n'accorde donc que 4 772 700 cycles, 5,87 ppm sous les 4 772 728 Hz
    // nominaux. C'est ce que PCem fait, et le budget est calculé ici sous la même
    // forme, avec la MÊME fonction que runpc, pour que la troncature reste lisible
    // et qu'une divergence entre les deux soit impossible.
    private static int _speed;
    private static int _cyclesPerSlice;
    private static double _clock;
    private static double _tscPerCycle;

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
    /// execx86 et exec386 font tous deux `cycles += cycs; while (cycles > 0)` : le
    /// budget non dépensé — toujours négatif ou nul en sortie — se reporte. Le total
    /// consommé est donc le total accordé moins ce qu'il reste au compteur, et
    /// x86.cycles est le même compteur pour les deux cœurs.</summary>
    private static long ConsumedCycles() => _slices * _cyclesPerSlice - x86.cycles;

    /// <summary>Tsc ramené en cycles CPU. Division entière quand le rapport est
    /// entier (3 sur le 8088 à 4,77 MHz, 1 sur le 286) : c'est la forme qu'avait
    /// l'outil avant d'être généralisé, et ses chiffres de référence en dépendent.</summary>
    private static long TscToCycles(long tsc)
    {
        var k = (long)_tscPerCycle;
        return k == _tscPerCycle ? tsc / k : (long)Math.Floor(tsc / _tscPerCycle);
    }

    /// <param name="ramLoad">« --charge ram » : la fenêtre mesure une boucle écrite en RAM
    /// plutôt que ce que la machine faisait à la fin de l'amorçage. Voir InstallRamLoop.</param>
    internal static int Run(string roms, int seconds, int bootSlices, bool ramLoad = false)
    {
        if (!pc.initpc(roms))
                return 1;

        _speed = cpu_c.cpu_get_speed();
        _cyclesPerSlice = _speed / 100;
        _clock = pit.cpuclock;
        _tscPerCycle = x86.AT != 0 ? 1.0 : _808x.xt_cpu_multi / TwoPow32;

        var machine = model_c.models[model_c.model];
        var is5150 = machine.id == pc.ROM_IBMPC;

        Console.WriteLine($"=== CONTRÔLE DE FRÉQUENCE ABSOLUE — {machine.name} ===\n");

        Console.WriteLine($"--- domaine d'horloge (posé par setpitclock({_clock:F0}), pc.c:184-187) ---");
        Console.WriteLine($"  cœur          = {(x86.AT != 0 ? "286 (exec386) : 1 tsc par cycle CPU" : "808x (execx86) : xt_cpu_multi tsc par cycle CPU")}");
        Console.WriteLine($"  PITCONST      = {pit.PITCONST,20}  = {pit.PITCONST / TwoPow32:F12} x 2^32");
        Console.WriteLine($"  xt_cpu_multi  = {_808x.xt_cpu_multi,20}  = {_808x.xt_cpu_multi / TwoPow32:F12} x 2^32");
        Console.WriteLine($"  TIMER_USEC    = {timer.TIMER_USEC,20}  = {timer.TIMER_USEC / TwoPow32:F12} x 2^32");
        Console.WriteLine($"  CGACONST      = {pit.CGACONST,20}");
        Console.WriteLine($"  tsc par cycle = {_tscPerCycle:F12}");
        Console.WriteLine($"  budget de cycles par tranche (pc.c:473) = {_cyclesPerSlice} " +
                          $"(= {_speed} / 100, division ENTIÈRE ; 100 tranches = {100L * _cyclesPerSlice} cycles)");
        Console.WriteLine($"  rapport a priori budget × tsc par cycle / horloge = " +
                          $"{100.0 * _cyclesPerSlice * _tscPerCycle / _clock:F6} " +
                          "(1 : le temps de l'invité suit les tranches)");
        Console.WriteLine();

        // ---------------------------------------------------------------
        // Amorçage. On relève Δtsc tranche par tranche : si la conversion
        // cycles → tsc était exacte, chaque tranche vaudrait exactement
        // budget × tsc par cycle (3 × 47 727 = 143 181 sur le 5150). Tout écart
        // se voit ici, et se date.
        // ---------------------------------------------------------------
        // On relève AUSSI le manque cumulé « cycles consommés moins cycles
        // portés au tsc » tranche par tranche. Sans lui, un Δtsc bas est
        // ambigu : il peut venir d'une DETTE de cycles (une instruction longue
        // a dépassé le budget, les tranches suivantes la remboursent) ou d'une
        // vraie perte de comptabilité. Les deux se distinguent ici.
        var perSlice = new long[bootSlices];
        var perSliceGap = new long[bootSlices];
        var prevTsc = (long)timer.tsc;
        for (var i = 0; i < bootSlices; i++)
        {
                pc.runpc();
                _slices++;
                var now = (long)timer.tsc;
                perSlice[i] = now - prevTsc;
                prevTsc = now;
                perSliceGap[i] = ConsumedCycles() - TscToCycles(now);
        }

        var nominalPerSlice = (long)Math.Round(_tscPerCycle * _cyclesPerSlice);

        Console.WriteLine($"--- amorçage : {bootSlices} tranches ({bootSlices / 100.0:F0} s contractuelles) ---");
        Console.WriteLine($"  CS:IP = {x86.CS:X4}:{_386_common.cpu_state.pc:X4}, ins = {_808x.ins}, tsc = {timer.tsc}");
        Console.WriteLine($"  BDA equipement 0040:0010 = {mem.readmemwl(0x410):X4}, " +
                          $"taille memoire 0040:0013 = {mem.readmemwl(0x413)} Ko");
        Console.WriteLine($"  Δtsc nominal par tranche si la conversion était exacte : {nominalPerSlice}");

        var firstOff = -1;
        for (var i = 0; i < bootSlices && firstOff < 0; i++)
                if (perSlice[i] != nominalPerSlice)
                        firstOff = i;

        Console.WriteLine($"  première tranche dont Δtsc ≠ {nominalPerSlice} : " +
                          (firstOff < 0 ? "aucune" : $"{firstOff} (Δtsc = {perSlice[firstOff]})"));
        Console.WriteLine("  vingt premières tranches : " +
                          string.Join(" ", perSlice.Take(20)));

        // Bornes MESURÉES par boot-profile (tools/iXtal26.Diff/BootProfile.cs), pas
        // devinées, et mesurées sur le 5150 à 640 Ko en 6 000 tranches : la boucle
        // F000:E02E court des tranches 9 à 4864, et la ROM BASIC est atteinte vers
        // 5729. Des bornes codées en dur et fausses ont déjà coûté cher — un libellé
        // « 150-5400 » lu comme une mesure a fait conclure à tort à 6,6 % d'écart
        // entre le coût prédit du test mémoire et son coût réel, qui est en fait de
        // 0,16 %. Hors de cette machine et de cette durée, elles ne veulent rien dire :
        // on ne les imprime pas.
        if (is5150 && bootSlices == DefaultBootSlices)
        {
                ProfileRange(perSlice, 0, 9, "POST avant le test mémoire");
                ProfileRange(perSlice, 9, 4865, "test mémoire (LODSB/XOR/STOSB/LOOP à F000:E02E)");
                ProfileRange(perSlice, 4865, bootSlices, "fin de POST, attente disquette, invite BASIC");
        }
        else
        {
                ProfileRange(perSlice, 0, bootSlices, "amorçage entier (profil par phases : 5150 seul)");
        }

        // Libellés et indices tels que l'outil les imprimait sur le 5150 : « 150 » est
        // l'indice 149, la fin de la 150e tranche.
        int[] labels = [0, 150, 1000, 3000, 5400, bootSlices - 1];
        int[] indices = [0, 149, 999, 2999, 5399, bootSlices - 1];
        var shown = Enumerable.Range(0, labels.Length)
                .Where(k => indices[k] < bootSlices && (k == labels.Length - 1 || indices[k] < bootSlices - 1))
                .ToArray();
        Console.WriteLine($"  manque cumulé « consommé − porté au tsc », en cycles, aux tranches " +
                          string.Join(" / ", shown.Select(k => labels[k])) + " :");
        Console.WriteLine("    " + string.Join(" / ", shown.Select(k => perSliceGap[indices[k]])));
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

        Console.WriteLine($"  le compteur avance : +{probe1 - probe0} tops en 1 s contractuelle. Mesure autorisée.\n");

        if (ramLoad && !InstallRamLoop())
                return 1;

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
        var insc0 = _808x.insc;
        var rollover0 = mem.mem_readb_phys(BdaRollover);

        // Le chronomètre de l'hôte ne couvre que la fenêtre elle-même, alignement de
        // fin compris : ce sont les tranches dont on mesure le temps émulé.
        var host = Stopwatch.StartNew();
        RunSlices(seconds * 100);

        if (!AlignToTick(out var t1))
                return 1;

        host.Stop();

        var slices1 = _slices;
        var tsc1 = (long)timer.tsc;
        var consumed1 = ConsumedCycles();
        var ins1 = _808x.ins;
        var insc1 = _808x.insc;
        var rollover1 = mem.mem_readb_phys(BdaRollover);

        // Un passage de minuit remet le compteur à zéro : la différence n'a plus de sens.
        if (rollover1 != rollover0 || t1 < t0)
        {
                Console.Error.WriteLine("*** le compteur de tops a franchi minuit pendant la fenêtre : mesure refusée. ***");
                return 1;
        }

        var ticks = (long)(t1 - t0);
        var windowSlices = slices1 - slices0;
        var dtsc = tsc1 - tsc0;
        var dcons = consumed1 - consumed0;
        var granted = windowSlices * _cyclesPerSlice;
        // ins et insc sont des int de PCem : ils débordent sur une longue fenêtre à
        // 25 MHz. La différence modulo 2^32 reste juste tant qu'elle tient sur 32 bits.
        var dins = (long)unchecked((uint)(ins1 - ins0));
        var dinsc = (long)unchecked((uint)(insc1 - insc0));

        // Le temps émulé selon le CONTRAT de l'émulateur : une tranche = 10 ms.
        var emuSeconds = windowSlices / 100.0;

        // Le temps émulé selon l'INVITÉ : ce que son compteur de tops a vu passer.
        var guestSeconds = ticks / ExpectedHz;

        Console.WriteLine("--- FENÊTRE DE MESURE (alignée sur un front du compteur aux deux bouts) ---");
        Console.WriteLine($"  tops        : {t0} → {t1}, soit {ticks} tops (compte EXACT : les deux bouts sont des fronts)");
        Console.WriteLine($"  tranches    : {windowSlices}  ⟹ {emuSeconds:F2} s contractuelles (pc.runpc : 10 ms/tranche)");
        Console.WriteLine($"  tsc         : {tsc0} → {tsc1}, Δ = {dtsc}");
        Console.WriteLine($"  cycles CPU  : accordés {granted}, consommés {dcons}, reste au compteur {x86.cycles}");
        Console.WriteLine($"  instructions: {ins0} → {ins1}, Δ = {dins} (insc : Δ = {dinsc})");
        Console.WriteLine($"  0040:0070 (dépassement 24 h) = {rollover1}");
        Console.WriteLine();

        // Incertitude : chaque alignement s'arrête au premier relevé POSTÉRIEUR
        // au front, donc dans [0, 1 tranche) après lui. La durée vraie de la
        // fenêtre est donc dans (windowSlices − 1, windowSlices + 1) tranches.
        var loSeconds = (windowSlices - 1) / 100.0;
        var hiSeconds = (windowSlices + 1) / 100.0;

        Console.WriteLine("--- 1. Δtsc par seconde contractuelle ---");
        Console.WriteLine("    (teste le budget de runpc contre le domaine d'horloge : cycles accordés × tsc par cycle)");
        var tscPerSec = dtsc / emuSeconds;
        Report(tscPerSec, _clock, "Hz (horloge de setpitclock)");
        Console.WriteLine();

        Console.WriteLine("--- 2. tops par seconde de tsc ---");
        Console.WriteLine("    (teste PITCONST + 8253 + IRQ 0 + INT 8 ; QUASI-TAUTOLOGIQUE, voir l'en-tête)");
        var fTsc = ticks * _clock / dtsc;
        Report(fTsc, ExpectedHz, "Hz");
        Console.WriteLine($"    tsc par top mesuré = {(double)dtsc / ticks:F3}, attendu 65536 × {_clock:F0} / 1193182 = " +
                          $"{65536.0 * _clock / 1193182.0:F3}");
        Console.WriteLine();

        Console.WriteLine("--- 3. tops par seconde CONTRACTUELLE ---");
        Console.WriteLine("    (la chaîne entière : budget de cycles, conversion en tsc, PITCONST, PIT, IRQ 0, INT 8)");
        var fSlice = ticks / emuSeconds;
        Report(fSlice, ExpectedHz, "Hz");
        Console.WriteLine($"    encadrement par la quantification de l'alignement (±1 tranche) : " +
                          $"[{ticks / hiSeconds:F6} ; {ticks / loSeconds:F6}] Hz");
        Console.WriteLine($"    soit ±{1e6 / windowSlices:F1} ppm de largeur d'incertitude sur la fenêtre.");
        Console.WriteLine($"    sans alignement, la quantification à ±1 top aurait valu " +
                          $"±{1e6 / ticks:F1} ppm.");
        Console.WriteLine();

        var dtscCycles = TscToCycles(dtsc);
        Console.WriteLine("--- 4. cycles CPU par seconde contractuelle ---");
        Console.WriteLine($"    accordés par pc.runpc()  : {granted / emuSeconds:F3} /s " +
                          $"(attendu {_speed}, écart {Signed(Ppm(granted / emuSeconds, _speed), 2)} ppm " +
                          "— c'est la division entière de pc.c:473)");
        Console.WriteLine($"    réellement consommés     : {dcons / emuSeconds:F3} /s " +
                          $"(écart {Signed(Ppm(dcons / emuSeconds, _speed), 2)} ppm)");
        Console.WriteLine($"    portés au tsc            : {dtscCycles / emuSeconds:F3} /s " +
                          $"(écart {Signed(Ppm(dtscCycles / emuSeconds, _speed), 2)} ppm)");
        Console.WriteLine($"    cycles consommés mais JAMAIS portés au tsc : {dcons - dtscCycles} " +
                          $"sur {dcons} ({100.0 * (dcons - dtscCycles) / dcons:F4} %)");
        Console.WriteLine();

        Console.WriteLine("--- 5. FRÉQUENCE DU PROCESSEUR VUE PAR L'INVITÉ ---");
        Console.WriteLine("    (cycles consommés par seconde du compteur de tops : ce qu'un programme qui");
        Console.WriteLine("     chronomètre une boucle avec le PIT mesurerait)");
        var guestHz = dcons / guestSeconds;
        Report(guestHz, _clock / _tscPerCycle, "Hz (horloge / tsc par cycle)");
        Console.WriteLine($"    soit {guestHz / 1e6:F3} MHz");
        Console.WriteLine();

        Console.WriteLine("--- 6. TEMPS DE L'INVITÉ / TEMPS CONTRACTUEL ---");
        Console.WriteLine("    (sous le frein de l'hôte, une tranche dure 10 ms murales : c'est alors la");
        Console.WriteLine("     vitesse à laquelle le temps de l'invité s'écoule, rapportée au temps réel)");
        var timeRatio = guestSeconds / emuSeconds;
        Console.WriteLine($"    rapport = {timeRatio:F6}  ({guestSeconds:F3} s invitées pour {emuSeconds:F2} s contractuelles)");
        Console.WriteLine($"    cadence FOURNIE à 100 % des tranches : {dcons / emuSeconds / 1e6:F3} MHz par seconde réelle");
        Console.WriteLine();

        // Le seul bloc non déterministe. Il ne sert qu'à savoir si l'hôte TIENT la
        // cadence : la marge est le temps de l'invité divisé par le temps que l'hôte a
        // passé à le produire. Sous 1, la machine ne peut pas tourner en temps réel.
        var hostSeconds = host.Elapsed.TotalSeconds;
        Console.WriteLine("--- 7. HÔTE (NON DÉTERMINISTE : dépend de la machine qui exécute l'outil) ---");
        Console.WriteLine($"    temps hôte de la fenêtre : {hostSeconds:F3} s");
        Console.WriteLine($"    marge   = {guestSeconds / hostSeconds:F3} (s invitées par s hôte ; < 1 : pas de temps réel)");
        Console.WriteLine($"    capacité = {dcons / hostSeconds / 1e6:F3} MHz de cycles émulés par seconde hôte");
        Console.WriteLine($"    MIPS invité = {dinsc / guestSeconds / 1e6:F3} (insc, comme pc.c:501) ; " +
                          $"CPI = {(double)dcons / dinsc:F3}");
        Console.WriteLine();

        Console.WriteLine($"EMPREINTE: tops={ticks} tranches={windowSlices} tsc={dtsc} cons={dcons} ins={dins} insc={dinsc}");

        return 0;
    }

    // MOV CX,FFFF / DEC CX / JNZ -3 / JMP -8 : une boucle sur registres, qui ne touche pas
    // la mémoire hors de son propre préfetch.
    private static readonly byte[] RamLoop = [0xB9, 0xFF, 0xFF, 0x49, 0x75, 0xFD, 0xEB, 0xF8];
    private const uint32_t RamLoopAddr = 0x600;

    /// <summary>
    /// LA CHARGE « RAM », pour mesurer la marge de l'hôte là où elle se décide. À la fin de
    /// l'amorçage, un BIOS attend une touche ou un disque dans une boucle en ROM ; au-delà de
    /// 8 MHz, chaque mot lu en ROM coûte rspeed / 1e6 cycles (20 à 286/20), donc très peu
    /// d'instructions par seconde invitée, donc un hôte peu chargé et une marge flatteuse.
    /// Un programme qui tourne en RAM en exécute cinq fois plus. On écrit donc en 0000:0600
    /// une boucle sur registres, et le 286 y saute, interruptions AUTORISÉES : l'INT 8 du
    /// BIOS continue de compter les tops, et la mesure reste celle du reste de l'outil.
    ///
    /// Outil hôte, pas un comportement de la machine : la RAM et CS:IP sont écrits de
    /// l'extérieur, comme un débogueur le ferait. Réservé au 286 en mode réel — le 8088
    /// porte sa propre file de préfetch, et un sélecteur de mode protégé ne se pose pas ainsi.
    /// </summary>
    private static bool InstallRamLoop()
    {
        if (x86.AT == 0 || (x86.msw & 1) != 0)
        {
                Console.Error.WriteLine("--charge ram : réservée au 286 en mode réel " +
                                        $"(AT = {x86.AT}, MSW.PE = {x86.msw & 1}).");
                return false;
        }

        for (var i = 0; i < RamLoop.Length; i++)
                mem.ram[RamLoopAddr + i] = RamLoop[i];

        x86seg_c.loadcs(0);
        _386_common.cpu_state.pc = RamLoopAddr;
        _386_common.cpu_state.flags |= (uint16_t)x86.I_FLAG;
        mem.flushmmucache();
        _386.prefetch_reset();

        Console.WriteLine($"--- charge « ram » : boucle MOV CX,FFFF / DEC CX / JNZ / JMP en 0000:{RamLoopAddr:X4}, IF = 1 ---\n");
        return true;
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
