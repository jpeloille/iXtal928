// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de mesure.
//
// LA MACHINE ENTIÈRE, POUR LA RÉFÉRENCE DE PERFORMANCE (G14.0a ; PLAN.md § G15 : « IPS de chaque machine » ;
// PLAN-G14.md, décision n° 6).
//
// Le seul banc qui fasse passer les IRQ, le PIT, le PIC et le DMA : une fenêtre fixe de pc.runpc depuis la mise sous
// tension, BIOS compris, sans disque, de 1 000 tranches de 10 ms (10 s émulées), 1 720 (17,2 s) sur l'ami286 et le
// PC1512. Mesuré seconde par seconde en G14.0a (VERIFICATION.md § G14.0a) : sur 5 s, le PC1512 n'a encore ni son
// rafraîchissement ni l'IRQ 0 (à 6 s), la M24 ni l'IRQ 0 (à 7 s) ni le PIC et le DMA de la fin de son POST (à 9 s) ;
// et la première IRQ SERVIE (picinterrupt) n'arrive qu'à 14 s sur l'ami286 (le PIC masqué, FFh, puis le test mémoire
// en mode protégé, IF = 0) et à 16 s sur le PC1512 (son test mémoire, IF = 0). La préparation exige au moins une IRQ
// servie dans la fenêtre de chaque modèle. Les neuf modèles de models[] (model.cs), dans les deux modes. Chaque
// modèle est monté comme son profil de la racine, sans ses disques ; ibmat et ami386, qui n'en ont pas, comme les
// portes bd-ibmat et bd-ami386 (la configuration par défaut : la CGA, 640 Ko, le premier processeur de la table). Le
// CMOS des machines AT est lu dans une COPIE de nvr/default/, posée dans un répertoire temporaire qui devient le
// répertoire courant (resolve_roms_path le cherche là d'abord) : le nvr/ du dépôt n'est ni lu ni écrit ; rien
// n'appelle savenvr.
//
// LE MÊME ÉTAT À CHAQUE INVOCATION. Avant chaque invocation ([IterationSetup], hors de la mesure), la machine est
// remontée : les remises de l'outillage que BootDiff fait avant chaque amorçage (ResetDiagState, prefetch_reset,
// ClearSegResidue), puis pc.initpc. Ce n'est pas tout à fait la mise sous tension, PCem n'amorçant qu'une fois par
// processus (mesuré en G14.0a) :
// - sur l'ami286 et l'ami386dx, la première fenêtre d'un processus n'est pas celle des suivantes, la CGA à la place de
//   la Trident aussi. Les registres du NEAT et de l'OPTi 495 sont des statiques que ni neat_init ni opti495_init ne
//   remettent (neat.cs:37, opti495.cs:36), et le POST de la fenêtre précédente les a programmés (déduit) ;
// - sur la M24 en mode « tout », les deux premières fenêtres diffèrent entre elles et des suivantes (de 3 instructions
//   la deuxième, à cycles égaux) ; ce qui passe d'une fenêtre à l'autre n'est pas identifié ;
// - sur le PC1512, une fenêtre trop longue alterne d'une remontée à l'autre entre deux empreintes, sans régime : à
//   1 750 tranches en « aucun », à 1 760 et 1 800 en « tout » ; de 1 680 à 1 740, toutes égales dans les deux modes.
//   L'état que la fin d'une fenêtre laisse à la suivante n'est pas identifié : amstrad_dead (amstrad.cs:34) vaut 0
//   avant chaque remontée, et remettre à 0 la piste des têtes de disquette (fdd.cs:67), qui passe bien d'une fenêtre
//   à l'autre, ne supprime pas l'alternance. D'où 1 720 tranches.
// La préparation joue donc des fenêtres jetées jusqu'à ce que deux de suite aient la même empreinte (au plus cinq) :
// le régime, où chaque fenêtre suit une fenêtre identique. L'invocation joue sa fenêtre et compare son empreinte
// (instructions, cycles consommés, TSC, IRQ levées et servies) à celle de la préparation : un écart d'une instruction
// fait échouer le cas. Une invocation par itération, sans déroulage (InvocationCount, UnrollFactor) ; après la
// remontée, un GC.Collect, hors de la mesure, ramasse ce que initpc a alloué.
//
// CE QUI EST EXERCÉ, mesuré par la préparation sur une fenêtre à part (une remontée de plus, avec des compteurs posés
// sur les ports et sur les sorties du PIT, que la remontée suivante efface) : les accès de l'invité aux ports du PIC
// (20h-21h, A0h-A1h), du PIT (40h-43h) et du DMA (00h-0Fh, 80h-8Fh, C0h-DFh) ; les fronts des trois sorties du PIT (la
// sortie 0 est l'IRQ 0, la sortie 1 le rafraîchissement, par le canal 0 du DMA sur le PC et l'XT) ; les appels de
// timer_process ; les IRQ levées (picint) et servies (picinterrupt, l'acquittement par le processeur), que le PIC
// compte lui-même (Diag/Counters.cs, pic.cs:399 et :472) ; les corrections que la sonde du mode matériel a vues
// servir, aucune en mode PCem. L'empreinte de cette fenêtre est celle des autres : les compteurs ne changent rien à la
// machine.
//
// LES ALLOCATIONS de la fenêtre (de 0 à quelques dizaines de Ko) viennent de l'exécution du processeur, ni des ports
// ni des minuteries : les `new uint16_t[4]` de x86seg.cs (loadseg en mode protégé, loadcscall, pmoderetf, pmodeint,
// pmodeiret, taskswitch286, loadcsjmp), 32 octets chacun, aux passages du POST en mode protégé ; aucune sur les 8088
// et 8086 (mesuré en G14.0a, pas à pas).

using BenchmarkDotNet.Attributes;
using iXtal26.Cpu;
using iXtal26.Diag;
using iXtal26.Memory;
using iXtal26.Models;

namespace iXtal26.PerfBanc;

[InvocationCount(1, 1)]
public class Machine
{
    [Params("ibmpc", "ibmxt", "ami286", "ibmat", "ami386", "ami386dx", "ami486", "pc1512", "olivetti_m24")]
    public string Modele = "";

    [Params("aucun", "tout")] public string Mode = "";

    [GlobalSetup]
    public void Preparer() => MachineBanc.Preparer(Mode, Modele);

    [IterationSetup]
    public void Remonter()
    {
        MachineBanc.Remonter();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    [Benchmark]
    public long Fenetre() => MachineBanc.Fenetre();

    [GlobalCleanup]
    public void Conclure() => MachineBanc.Conclure();
}

internal static class MachineBanc
{
    /// <summary>La fenêtre de chaque modèle, en tranches : 10 s, et 18 s là où la première IRQ servie vient plus
    /// tard.</summary>
    private static int Tranches(string modele) => modele is "ami286" or "pc1512" ? 1720 : 1000;

    /// <summary>La configuration de chaque modèle : celle de son profil de la racine, sans disque.</summary>
    private static string Configuration(string modele) => modele switch
    {
        "ibmpc" or "ibmxt" => $"model = {modele}\nmem_size = 640\n",
        "ami286" => "model = ami286\ncpu = 4\nfpu = 287\nmem_size = 4096\ngfxcard = tvga8900d\n",
        "ami386dx" => "model = ami386dx\ncpu = 3\nfpu = 387\nmem_size = 4096\ngfxcard = tvga9000b\nsndcard = sbprov2\n",
        "ami486" =>
            "model = ami486\ncpu = 10\nfpu = builtin\nmem_size = 4096\ngfxcard = cl_gd5429\nsndcard = sbprov2\n",
        "pc1512" or "olivetti_m24" => $"model = {modele}\ncpu = 0\nmem_size = 640\n",
        "ibmat" or "ami386" => $"model = {modele}\n",
        _ => throw new InvalidOperationException($"MachineBanc : modèle inconnu : {modele}"),
    };

    private readonly record struct Empreinte(int Instructions, long Cycles, ulong Tsc, ulong Levees, ulong Servies);

    private static string _roms = "", _temp = "", _courant = "", _cas = "";
    private static int _tranches;
    private static Empreinte _attendue;
    private static int _invocations;

    /// <summary>Les remises de l'outillage, puis pc.initpc : la machine remontée (pas tout à fait la mise sous
    /// tension : voir l'en-tête).</summary>
    internal static void Remonter()
    {
        _808x.ResetDiagState();
        _386.prefetch_reset();
        _386.ClearSegResidue();
        Floppy.fdd_c.discfns[0] = Floppy.fdd_c.discfns[1] = "";
        for (var u = 0; u < 4; u++)
            Disc.hdd_c.ide_fn[u] = "";
        if (!pc.initpc(_roms))
            throw new InvalidOperationException($"MachineBanc ({_cas}) : la machine ne se remonte pas");
    }

    private static Empreinte Jouer()
    {
        var (ins, cycles, tsc) = (_808x.ins, x86.cycles, timer.tsc);
        var (levees, servies) = (Counters.n_picint, Counters.n_picinterrupt);
        for (var k = 0; k < _tranches; k++)
            pc.runpc();
        var budget = (long)_tranches * (cpu_c.cpu_get_speed() / 100);
        return new Empreinte(_808x.ins - ins, budget + cycles - x86.cycles, timer.tsc - tsc,
                             Counters.n_picint - levees, Counters.n_picinterrupt - servies);
    }

    /// <summary>Pose le mode avant le gel, la copie du CMOS, la configuration ; des fenêtres jetées jusqu'au régime,
    /// une fenêtre comptée, une fenêtre de contrôle, chacune sur une machine remontée ; vérifie le mode et l'état, puis
    /// l'imprime. Sur un refus, la copie du CMOS est effacée et le répertoire courant rendu.</summary>
    internal static void Preparer(string mode, string modele)
    {
        Reference.Demander(mode, "MachineBanc");
        _cas = $"{modele}, {mode}";
        _tranches = Tranches(modele);
        var depot = Reference.Depot();
        _roms = Path.Combine(depot, "roms");
        _temp = Path.Combine(Path.GetTempPath(), $"machinebanc-{Environment.ProcessId}");
        _courant = Environment.CurrentDirectory;
        try
        {
            Directory.CreateDirectory(Path.Combine(_temp, "nvr", "default"));
            foreach (var f in Directory.GetFiles(Path.Combine(depot, "nvr", "default"), "*.nvr"))
                File.Copy(f, Path.Combine(_temp, "nvr", "default", Path.GetFileName(f)), overwrite: true);
            Environment.CurrentDirectory = _temp;
            Relever(mode, modele);
        }
        catch
        {
            Environment.CurrentDirectory = _courant;
            if (Directory.Exists(_temp))
                Directory.Delete(_temp, recursive: true);
            throw;
        }
    }

    private static void Relever(string mode, string modele)
    {
        var cfg = Path.Combine(_temp, "machine.cfg");
        File.WriteAllText(cfg, Configuration(modele));
        if (!pc.loadconfig(cfg))
            throw new InvalidOperationException($"MachineBanc ({_cas}) : la configuration ne se lit pas");
        Remonter();
        Reference.VerifierMode(mode, "MachineBanc");
        var nvr = Path.GetFullPath(PluginApi.paths.nvr_path);
        if (!nvr.StartsWith(_temp, StringComparison.Ordinal))
            throw new InvalidOperationException($"MachineBanc ({_cas}) : le CMOS serait lu dans {nvr}, " +
                                                "pas dans la copie");
        var precedente = Jouer();
        var jetees = 1;
        while (true)
        {
            Remonter();
            var e = Jouer();
            jetees++;
            if (e == precedente)
                break;
            if (jetees == 5)
                throw new InvalidOperationException($"MachineBanc ({_cas}) : cinq fenêtres, aucun régime : " +
                                                    $"la dernière {e}, l'avant-dernière {precedente}");
            precedente = e;
        }

        Remonter();
        var (exerce, comptee) = Compter();
        Remonter();
        _attendue = Jouer();
        if (comptee != _attendue)
            throw new InvalidOperationException($"MachineBanc ({_cas}) : deux fenêtres, deux empreintes : {comptee} " +
                                                $"puis {_attendue}");
        if (_attendue.Servies == 0)
            throw new InvalidOperationException($"MachineBanc ({_cas}) : aucune IRQ servie dans la fenêtre " +
                                                $"({_attendue.Levees} levées) : elle ne fait pas passer les IRQ");
        var corrections = Enumerable.Range(0, ModeMateriel.Sonde.Length).Where(n => ModeMateriel.Sonde[n] != 0)
                                    .Select(n => $"PB-{n:00}").ToList();
        if ((mode == "tout") != (corrections.Count > 0))
            throw new InvalidOperationException($"MachineBanc ({_cas}) : le mode demandé est « {mode} », et la sonde " +
                                                $"a vu servir {corrections.Count} corrections : " +
                                                "le mode n'est pas posé");
        var m = model_c.models[model_c.model];
        var processeur = m.cpu[cpu_c.cpu_manufacturer].cpus![cpu_c.cpu].name;
        var carte = pc.CarteVideo.Integree
            ? "vidéo intégrée"
            : Array.Find(Video.video.video_cards, k => k.legacy_id == pc.gfxcard)?.internal_name
              ?? $"carte {pc.gfxcard}";
        var son = Sound.sound.sound_card_get_internal_name(Sound.sound.sound_card_current);
        Reference.Verifier("MachineBanc", $"{modele}, {processeur}, {carte}, son {son}, {mem.mem_size} Ko", mode,
                           $"le régime après {jetees} fenêtres jetées ; {_tranches} tranches, " +
                           $"{_attendue.Instructions} instructions, {_attendue.Cycles} cycles, " +
                           $"TSC {_attendue.Tsc}, la RAM {_808x.RamHash():X16} ; exercé : {exerce}, " +
                           $"IRQ levées {_attendue.Levees}, servies {_attendue.Servies} ; " +
                           "corrections servies : " +
                           (corrections.Count == 0 ? "aucune" : string.Join(",", corrections)));
        _invocations = 0;
    }

    /// <summary>Une fenêtre comptée : les ports du PIC, du PIT et du DMA, et les sorties du PIT, enveloppés par des
    /// compteurs ; la remontée suivante les efface (io_init, pit_reset).</summary>
    private static (string, Empreinte) Compter()
    {
        var ports = new long[0x10000];
        void Envelopper(int premier, int dernier)
        {
            for (var p = premier; p <= dernier; p++)
                for (var k = 0; k < 2; k++)
                {
                    var (lire, ecrire, port) = (io.port_inb[p, k], io.port_outb[p, k], p);
                    if (lire != null)
                        io.port_inb[p, k] = (a, priv) => { ports[port]++; return lire(a, priv); };
                    if (ecrire != null)
                        io.port_outb[p, k] = (a, v, priv) => { ports[port]++; ecrire(a, v, priv); };
                }
        }
        long Somme(params (int Premier, int Dernier)[] plages)
        {
            long s = 0;
            foreach (var (premier, dernier) in plages)
                for (var p = premier; p <= dernier; p++)
                    s += ports[p];
            return s;
        }
        Envelopper(0x00, 0x0f);
        Envelopper(0x20, 0x21);
        Envelopper(0x40, 0x43);
        Envelopper(0x80, 0x8f);
        Envelopper(0xa0, 0xa1);
        Envelopper(0xc0, 0xdf);
        var fronts = new long[3];
        for (var t = 0; t < 3; t++)
        {
            var (f, canal) = (pit.pit_.set_out_funcs[t], t);
            if (f != null)
                pit.pit_.set_out_funcs[t] = (n, o) => { fronts[canal]++; f(n, o); };
        }
        Array.Clear(ModeMateriel.Sonde);
        var tp = Counters.n_timer_process;
        var e = Jouer();
        var exerce = $"PIC {Somme((0x20, 0x21), (0xa0, 0xa1))} accès, PIT {Somme((0x40, 0x43))} accès, " +
                     $"DMA {Somme((0x00, 0x0f), (0x80, 0x8f), (0xc0, 0xdf))} accès, " +
                     $"fronts du PIT {fronts[0]}/{fronts[1]}/{fronts[2]}, " +
                     $"timer_process {Counters.n_timer_process - tp}";
        return (exerce, e);
    }

    /// <summary>La fenêtre mesurée ; son empreinte doit être celle de la préparation.</summary>
    internal static long Fenetre()
    {
        var e = Jouer();
        if (e != _attendue)
            throw new InvalidOperationException($"MachineBanc ({_cas}) : l'invocation {_invocations + 1} n'est pas " +
                                                $"partie du même état : {e} au lieu de {_attendue}");
        _invocations++;
        return e.Cycles;
    }

    /// <summary>Le compte des invocations, toutes à la même empreinte ; le répertoire de la copie du CMOS
    /// effacé.</summary>
    internal static void Conclure()
    {
        Console.WriteLine($"// MachineBanc ({ModeMateriel.Description}, {_cas}) : {_invocations} invocations, toutes " +
                          $"à l'empreinte {_attendue.Instructions} instructions, {_attendue.Cycles} cycles, " +
                          $"TSC {_attendue.Tsc}, IRQ servies {_attendue.Servies}");
        Environment.CurrentDirectory = _courant;
        Directory.Delete(_temp, recursive: true);
    }
}
