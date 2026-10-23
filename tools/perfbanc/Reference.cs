// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de mesure.
//
// CE QUE PARTAGENT LES BANCS DE LA RÉFÉRENCE (G14.0a ; PLAN-G14.md, § G14.0a, décision n° 6).
//
// Les bancs de G14.0a mesurent ce que PLAN.md § G15 énumère, dans les deux modes : « aucun » (les portes, l'oracle) et
// « tout » (l'utilisateur). Chacun pose son mode avant le gel, puis vérifie trois choses avant la mesure, et refuse
// sinon :
// - le mode figé est celui demandé (un banc qui oublierait de le poser mesurerait le mode PCem sous le nom « tout ») ;
// - son état après la préparation est celui que ce fichier attend (les registres, les sommes, l'empreinte) : une
//   préparation qui diverge, une boucle changée, un émulateur qui ne fait plus la même chose, sont refusés ;
// - l'état est imprimé, en une ligne « // Banc (…) : … », que le comparateur de G14.0b lira dans le journal.
// L'attendu vaut pour les deux modes quand le mode n'y change rien ; sinon il est donné mode par mode.

using System.Runtime.CompilerServices;

namespace iXtal26.PerfBanc;

internal static class Reference
{
    /// <summary>Les états attendus, par cas : « Banc paramètres » pour les deux modes, ou « Banc paramètres mode »
    /// quand le mode change l'état. Relevés au premier lancement (G14.0a) ; un écart est un refus, jamais une mise à
    /// jour silencieuse.</summary>
    private static readonly Dictionary<string, string> Attendus = new()
    {
        ["Exec8088 ModRM mémoire, ES:, REP MOVSB"] =
            "après un appel, 4711 instructions, AX = 9090, CX = FDF4, SI = 0800, DI = 0900, IP = 010C, 0900h = 2021",
        ["Exec286 286/16, memoire"] =
            "après un appel, 9676 instructions, BX = 0000, CX = F366, IP = 0102",
        ["Exec286 286/16, prefixe"] =
            "après un appel, 9078 instructions, BX = C620, CX = F42D, IP = 0100",
        ["Exec286 286/16, sans-prefixe"] =
            "après un appel, 11112 instructions, BX = 6060, CX = F187, IP = 0100",
        ["Exec486 i486DX2/66, memoire"] =
            "après un appel, 15787 instructions, BX = 0000, CX = EB71, IP = 0102",
        ["Exec486 i486DX2/66, prefixe"] =
            "après un appel, 14266 instructions, BX = 22B0, CX = ED6C, IP = 0103",
        ["Exec486 i486DX2/66, sans-prefixe"] =
            "après un appel, 17647 instructions, BX = 3EC8, CX = E905, IP = 0103",
        ["MemoireLente ami486, i486DX2/66, cl_gd5429"] =
            "après un appel, 17500 instructions, 2500 lectures et 2500 écritures par le chemin lent, CX = F63B, SI = " +
            "09C4, DI = 09C4, la mémoire vidéo B93A0C83CE3B6325",
        ["MemoireLente ibmxt, 8088/4.77, cga"] =
            "après un appel, 8234 instructions, 1178 lectures et 1177 écritures par le chemin lent, CX = FB67, SI = " +
            "0498, DI = 0498, la mémoire vidéo 47781D1547D53B58",
        ["RenduCga ibmxt, cga, texte 80 colonnes"] =
            "une trame de 262 lignes, mode 29, displine 38, l'image 7C24875FDC9EA3CE",
        ["Rendu8bpp cl_gd5429"] =
            "une trame de 525 lignes, 640 × 480 points, svga_render_8bpp_highres, 8 bpp, l'image 7AF794A9FF195325",
        ["Rendu8bpp tvga8900d"] =
            "une trame de 525 lignes, 640 × 480 points, svga_render_8bpp_highres, 8 bpp, l'image F5E326ADCDB03155",
        ["SonOplPro sbprov2, OPL3, 18 voix aucun"] =
            "un tampon de 2400 tics, la somme du troisième 1024940, de -12595 à 12722",
        ["SonOplPro sbprov2, OPL3, 18 voix tout"] =
            "un tampon de 2400 tics, la somme du troisième 61350, de -795 à 802",
        ["MachineBanc ibmpc, 8088/4.77, cga, son none, 640 Ko aucun"] =
            "le régime après 3 fenêtres jetées ; 1000 tranches, 3588242 instructions, 47727000 cycles, TSC 140994687," +
            " la RAM 6E43FC33CF056796 ; exercé : PIC 19 accès, PIT 31 accès, DMA 75 accès, fronts du PIT " +
            "242/1290696/0, timer_process 2119986, IRQ levées 122, servies 3 ; corrections servies : aucune",
        ["MachineBanc ibmpc, 8088/4.77, cga, son none, 640 Ko tout"] =
            "le régime après 3 fenêtres jetées ; 1000 tranches, 3606827 instructions, 47727003 cycles, TSC 143181009," +
            " la RAM F4791F0A77989234 ; exercé : PIC 19 accès, PIT 31 accès, DMA 75 accès, fronts du PIT " +
            "246/1310940/0, timer_process 2128964, IRQ levées 124, servies 3 ; corrections servies : PB-03,PB-04," +
            "PB-21,PB-174,PB-222,PB-223,PB-246,PB-247,PB-248,PB-251,PB-252,PB-255",
        ["MachineBanc ibmxt, 8088/4.77, cga, son none, 640 Ko aucun"] =
            "le régime après 2 fenêtres jetées ; 1000 tranches, 3386010 instructions, 47727000 cycles, TSC 141214092," +
            " la RAM A4E0A2EACE20E4DF ; exercé : PIC 29 accès, PIT 31 accès, DMA 76 accès, fronts du PIT " +
            "195/1240162/1, timer_process 2164584, IRQ levées 98, servies 3 ; corrections servies : aucune",
        ["MachineBanc ibmxt, 8088/4.77, cga, son none, 640 Ko tout"] =
            "le régime après 2 fenêtres jetées ; 1000 tranches, 3354684 instructions, 47727015 cycles, TSC 143181045," +
            " la RAM 3DF53FBCE7224923 ; exercé : PIC 29 accès, PIT 31 accès, DMA 76 accès, fronts du PIT " +
            "197/1258374/1, timer_process 2139070, IRQ levées 99, servies 3 ; corrections servies : PB-03,PB-04," +
            "PB-21,PB-174,PB-222,PB-223,PB-246,PB-247,PB-248,PB-251,PB-252,PB-255",
        ["MachineBanc ami286, 286/16, tvga8900d, son none, 4096 Ko aucun"] =
            "le régime après 3 fenêtres jetées ; 1720 tranches, 24675316 instructions, 275200077 cycles, TSC " +
            "275199232, la RAM 6DCC2500B8C478C8 ; exercé : PIC 726 accès, PIT 692995 accès, DMA 463 accès, fronts du " +
            "PIT 594/2010792/6350, timer_process 2435875, IRQ levées 373, servies 46 ; corrections servies : aucune",
        ["MachineBanc ami286, 286/16, tvga8900d, son none, 4096 Ko tout"] =
            "le régime après 3 fenêtres jetées ; 1720 tranches, 24674906 instructions, 275200006 cycles, TSC " +
            "275199161, la RAM 6DB0DA1375C10E06 ; exercé : PIC 726 accès, PIT 692995 accès, DMA 463 accès, fronts du " +
            "PIT 594/2010792/6350, timer_process 2440142, IRQ levées 373, servies 46 ; corrections servies : PB-21," +
            "PB-157,PB-181,PB-203,PB-204,PB-220,PB-246,PB-247,PB-248,PB-251,PB-252,PB-255,PB-293",
        ["MachineBanc ibmat, 286/6, cga, son none, 640 Ko aucun"] =
            "le régime après 2 fenêtres jetées ; 1000 tranches, 8170432 instructions, 60000056 cycles, TSC 60000043, " +
            "la RAM 20A79E4F7D61777F ; exercé : PIC 45 accès, PIT 36 accès, DMA 7845 accès, fronts du PIT " +
            "292/1295272/2, timer_process 1787742, IRQ levées 146, servies 2 ; corrections servies : aucune",
        ["MachineBanc ibmat, 286/6, cga, son none, 640 Ko tout"] =
            "le régime après 2 fenêtres jetées ; 1000 tranches, 8170432 instructions, 60000056 cycles, TSC 60000043, " +
            "la RAM 20A79E4F7D61777F ; exercé : PIC 45 accès, PIT 36 accès, DMA 7845 accès, fronts du PIT " +
            "292/1295272/2, timer_process 1787742, IRQ levées 146, servies 2 ; corrections servies : PB-04,PB-21," +
            "PB-157,PB-204,PB-222,PB-223,PB-246,PB-247,PB-248,PB-251,PB-252,PB-255",
        ["MachineBanc ami386, i386SX/16, cga, son none, 640 Ko aucun"] =
            "le régime après 2 fenêtres jetées ; 1000 tranches, 8692779 instructions, 160000010 cycles, TSC " +
            "160000010, la RAM 3AC7676F13C3F7F6 ; exercé : PIC 277 accès, PIT 457 accès, DMA 674 accès, fronts du " +
            "PIT 351/1294368/742, timer_process 1578533, IRQ levées 179, servies 143 ; corrections servies : aucune",
        ["MachineBanc ami386, i386SX/16, cga, son none, 640 Ko tout"] =
            "le régime après 2 fenêtres jetées ; 1000 tranches, 8692779 instructions, 160000010 cycles, TSC " +
            "160000010, la RAM 3AC7676F13C3F7F6 ; exercé : PIC 277 accès, PIT 457 accès, DMA 674 accès, fronts du " +
            "PIT 351/1294368/742, timer_process 1578533, IRQ levées 179, servies 143 ; corrections servies : PB-04," +
            "PB-21,PB-157,PB-204,PB-222,PB-223,PB-246,PB-247,PB-248,PB-251,PB-252,PB-255,PB-293",
        ["MachineBanc ami386dx, i386DX/33, tvga9000b, son sbprov2, 4096 Ko aucun"] =
            "le régime après 3 fenêtres jetées ; 1000 tranches, 16009312 instructions, 333333172 cycles, TSC " +
            "333333172, la RAM 03C224416D1B4DF1 ; exercé : PIC 707 accès, PIT 1324138 accès, DMA 670 accès, fronts " +
            "du PIT 355/1305250/7033, timer_process 1694071, IRQ levées 181, servies 83 ; corrections servies : " +
            "aucune",
        ["MachineBanc ami386dx, i386DX/33, tvga9000b, son sbprov2, 4096 Ko tout"] =
            "le régime après 3 fenêtres jetées ; 1000 tranches, 16009314 instructions, 333333246 cycles, TSC " +
            "333333246, la RAM 03C224416D1B4DF1 ; exercé : PIC 707 accès, PIT 1324138 accès, DMA 670 accès, fronts " +
            "du PIT 355/1305252/7033, timer_process 1699383, IRQ levées 181, servies 83 ; corrections servies : " +
            "PB-21,PB-148,PB-157,PB-181,PB-203,PB-204,PB-220,PB-236,PB-237,PB-246,PB-247,PB-248,PB-251,PB-252,PB-255," +
            "PB-291,PB-293",
        ["MachineBanc ami486, i486DX2/66, cl_gd5429, son sbprov2, 4096 Ko aucun"] =
            "le régime après 2 fenêtres jetées ; 1000 tranches, 106669349 instructions, 666666436 cycles, TSC " +
            "666666436, la RAM 10FF055124DF7514 ; exercé : PIC 789 accès, PIT 1011 accès, DMA 2023 accès, fronts du " +
            "PIT 361/1312594/1132, timer_process 1813222, IRQ levées 186, servies 165 ; corrections servies : aucune",
        ["MachineBanc ami486, i486DX2/66, cl_gd5429, son sbprov2, 4096 Ko tout"] =
            "le régime après 2 fenêtres jetées ; 1000 tranches, 106663912 instructions, 666666449 cycles, TSC " +
            "666666449, la RAM 21A7830CA10A60C7 ; exercé : PIC 789 accès, PIT 1011 accès, DMA 2023 accès, fronts du " +
            "PIT 361/1312594/1132, timer_process 1819545, IRQ levées 186, servies 165 ; corrections servies : PB-21," +
            "PB-80,PB-148,PB-157,PB-181,PB-204,PB-220,PB-236,PB-237,PB-246,PB-247,PB-248,PB-251,PB-252,PB-255,PB-291," +
            "PB-293",
        ["MachineBanc pc1512, 8086/8, vidéo intégrée, son none, 640 Ko aucun"] =
            "le régime après 2 fenêtres jetées ; 1720 tranches, 14501158 instructions, 137600018 cycles, TSC " +
            "246272797, la RAM C0F1C1AC27AB4C97 ; exercé : PIC 60 accès, PIT 2376 accès, DMA 92 accès, fronts du PIT " +
            "410/1485548/169, timer_process 1996717, IRQ levées 211, servies 15 ; corrections servies : aucune",
        ["MachineBanc pc1512, 8086/8, vidéo intégrée, son none, 640 Ko tout"] =
            "le régime après 2 fenêtres jetées ; 1720 tranches, 14458384 instructions, 137600008 cycles, TSC " +
            "246272779, la RAM B74B506B6AB89D54 ; exercé : PIC 59 accès, PIT 2376 accès, DMA 92 accès, fronts du PIT " +
            "410/1485548/49, timer_process 1992482, IRQ levées 211, servies 14 ; corrections servies : PB-01,PB-03," +
            "PB-21,PB-101,PB-170,PB-174,PB-175,PB-246,PB-247,PB-248,PB-252,PB-255,PB-256,PB-257",
        ["MachineBanc olivetti_m24, 8086/7.16, vidéo intégrée, son none, 640 Ko aucun"] =
            "le régime après 2 fenêtres jetées ; 1000 tranches, 7599162 instructions, 71590002 cycles, TSC 143180004," +
            " la RAM 0D1969B82B81A45E ; exercé : PIC 49 accès, PIT 182 accès, DMA 125 accès, fronts du PIT " +
            "82/1245562/337, timer_process 1881021, IRQ levées 54, servies 31 ; corrections servies : aucune",
        ["MachineBanc olivetti_m24, 8086/7.16, vidéo intégrée, son none, 640 Ko tout"] =
            "le régime après 4 fenêtres jetées ; 1000 tranches, 7560178 instructions, 71590013 cycles, TSC 143180026," +
            " la RAM 4F0FC1EE9295F82D ; exercé : PIC 56 accès, PIT 176 accès, DMA 143 accès, fronts du PIT " +
            "81/1245562/337, timer_process 1899694, IRQ levées 55, servies 32 ; corrections servies : PB-03,PB-21," +
            "PB-174,PB-175,PB-246,PB-247,PB-248,PB-251,PB-252,PB-255,PB-256",
    };

    /// <summary>Pose le mode demandé, avant le gel ; refuse un nom que ModeMateriel ne lit pas.</summary>
    internal static void Demander(string mode, string banc)
    {
        if (mode is not ("aucun" or "tout"))
            throw new InvalidOperationException($"{banc} : le mode « {mode} » n'est ni « aucun » ni « tout »");
        if (!ModeMateriel.Demander(mode, banc))
            throw new InvalidOperationException($"{banc} : le mode « {mode} » est refusé");
    }

    /// <summary>Après le gel : le mode figé doit être celui demandé. Sans lui, un cas « tout » mesurerait le mode PCem
    /// en silence.</summary>
    internal static void VerifierMode(string mode, string banc)
    {
        var attendu = mode == "tout" ? "materiel (tout)" : "PCem";
        if (!ModeMateriel.Fige || ModeMateriel.Description != attendu)
        {
            var fige = ModeMateriel.Fige ? ModeMateriel.Description : "mode non figé";
            throw new InvalidOperationException($"{banc} : le mode demandé est « {mode} », la machine tourne en " +
                                                $"{fige} : le mode n'a pas été posé avant le gel");
        }
    }

    /// <summary>Compare l'état à l'attendu du cas, puis l'imprime. Un cas sans attendu est refusé aussi.</summary>
    internal static void Verifier(string banc, string cas, string mode, string etat)
    {
        if (!Attendus.TryGetValue($"{banc} {cas} {mode}", out var attendu) &&
            !Attendus.TryGetValue($"{banc} {cas}", out attendu))
            throw new InvalidOperationException($"{banc} ({cas}, {mode}) : aucun état attendu ; " +
                                                $"l'état obtenu : {etat}");
        if (etat != attendu)
            throw new InvalidOperationException($"{banc} ({cas}, {mode}) : l'état diffère de l'attendu.\n" +
                                                $"  attendu : {attendu}\n  obtenu  : {etat}");
        Console.WriteLine($"// {banc} ({ModeMateriel.Description}, {cas}) : {etat}");
    }

    /// <summary>La racine du dépôt, au-dessus du banc (roms/ et iXtal26.sln) : BenchmarkDotNet lance ses processus
    /// depuis un répertoire de build sous tools/perfbanc.</summary>
    internal static string Depot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "iXtal26.sln")) &&
                Directory.Exists(Path.Combine(d.FullName, "roms")))
                return d.FullName;
        throw new InvalidOperationException("le dépôt (roms/, iXtal26.sln) est introuvable au-dessus du banc");
    }

    /// <summary>Monte une machine sans BIOS exécuté (pc.initpc), d'après une configuration écrite dans un fichier
    /// temporaire ; aucun disque.</summary>
    internal static void Monter(string configuration, string banc)
    {
        var cfg = Path.Combine(Path.GetTempPath(), $"{banc.ToLowerInvariant()}-{Environment.ProcessId}.cfg");
        bool lu;
        try
        {
            File.WriteAllText(cfg, configuration);
            lu = pc.loadconfig(cfg);
        }
        finally
        {
            File.Delete(cfg);
        }
        if (!lu)
            throw new InvalidOperationException($"{banc} : la configuration ne se lit pas");
        Floppy.fdd_c.discfns[0] = Floppy.fdd_c.discfns[1] = "";
        for (var u = 0; u < 4; u++)
            Disc.hdd_c.ide_fn[u] = "";
        if (!pc.initpc(Path.Combine(Depot(), "roms")))
            throw new InvalidOperationException($"{banc} : la machine ne se monte pas");
    }

    /// <summary>FNV-1a sur 64 bits, pour résumer une mémoire ou une image en une valeur imprimable.</summary>
    internal static ulong Fnv<T>(ReadOnlySpan<T> donnees) where T : unmanaged
    {
        var h = 0xcbf29ce484222325UL;
        foreach (var o in System.Runtime.InteropServices.MemoryMarshal.AsBytes(donnees))
            h = (h ^ o) * 0x100000001b3UL;
        return h;
    }

    /// <summary>Le nom d'un délégué, pour dire quel rendu ou quel gestionnaire la carte a choisi.</summary>
    internal static string Nom(Delegate? d, [CallerArgumentExpression(nameof(d))] string expr = "") =>
        d?.Method.Name ?? $"({expr} nul)";
}
