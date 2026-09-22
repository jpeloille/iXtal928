// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de diff, pas de code PCem transcrit.
//
// A2.2a — LA PORTE DU CHEMIN DE FETCH.
//
// exec386 fait `fastreadl(cs + pc)` à CHAQUE instruction (386.c:176). Ce chemin
// traverse getpccache, le cache de page pccache/pccache2, et une arithmétique de
// BIAIS : le C rend un pointeur décalé que l'appelant relit avec l'adresse virtuelle
// COMPLÈTE. Le C# transcrit ce biais en porteur + offset.
//
// POURQUOI CETTE SONDE EXISTE. Une erreur d'une page dans ce biais est parfaitement
// silencieuse : elle rend des octets plausibles, pris au mauvais endroit. Elle ne
// deviendrait observable qu'une fois exec386 transcrit — mêlée à cent autres
// changements, donc illisible. On la met sous oracle AVANT d'écrire la boucle.
//
// On AMORCE les deux côtés plutôt que d'utiliser la carte plate du fuzzeur : il faut
// que ROM et RAM soient toutes deux cartographiées, sans quoi la branche
// MEM_MAPPING_ROM de getpccache — celle qui bascule cpu_prefetch_cycles — n'est
// jamais prise.

using iXtal26.Cpu;

namespace iXtal26.Diff;

public static class FetchProbe
{
    public static int Run(string romsPath)
    {
        Oracle.CheckAbi();

        Console.WriteLine("Sonde du chemin de fetch — oracle contre C#.\n");

        if (Oracle.h_boot(romsPath) == 0)
        {
            Console.Error.WriteLine($"L'oracle n'a pas pu charger le BIOS depuis « {romsPath} ».");
            return 1;
        }

        _808x.ResetDiagState();
        if (!pc.initpc(romsPath))
        {
            Console.Error.WriteLine("Le cœur C# n'a pas pu s'initialiser.");
            return 1;
        }

        var bad = 0;
        var tested = 0;

        // Les quatre régions qui comptent, et pourquoi chacune :
        //   RAM basse      — le cas ordinaire, porteur = ram[]
        //   trou vidéo     — cartographié ailleurs ; peut n'avoir aucun exec
        //   ROM du BIOS    — porteur ≠ ram[], et la branche ROM du coût de préfetch
        //   sommet         — le vecteur de reset, là où le 8088 et le 286 démarrent
        (string nom, uint debut, uint fin, uint pas)[] regions =
        [
            ("RAM basse",    0x00000000, 0x00010000, 0x40),
            ("trou video",   0x000A0000, 0x000C0000, 0x40),
            ("ROM du BIOS",  0x000F0000, 0x00100000, 0x40),
            ("sommet",       0x000FFF00, 0x00100000, 1),
        ];

        foreach (var (nom, debut, fin, pas) in regions)
        {
            var erreursIci = 0;
            for (var a = debut; a < fin; a += pas)
            {
                // L'ordre compte : chaque appel peut DÉPLACER le cache de page, donc
                // les deux côtés doivent recevoir exactement la même séquence.
                var o = Oracle.h_fastreadl(a);
                var c = _386_common.fastreadl(a);
                var op = Oracle.h_pccache();
                var cp = _386_common.pccache;
                tested++;

                if (o != c || op != cp)
                {
                    if (erreursIci < 4)
                        Console.WriteLine(o != c
                            ? $"  {nom} {a:X8} : oracle {o:X8}, C# {c:X8}"
                            : $"  {nom} {a:X8} : page oracle {op:X8}, C# {cp:X8} (octets d'accord)");
                    erreursIci++;
                    bad++;
                }
            }
            Console.WriteLine($"  {nom,-14} {(fin - debut) / pas,6} adresses  " +
                              (erreursIci == 0 ? "identiques" : $"*** {erreursIci} ECARTS ***"));
        }

        // Les accès à cheval sur une page : c'est là que fastreadw/l se dédoublent en
        // deux getpccache, et que le biais doit être recalculé au milieu de la lecture.
        Console.WriteLine("\n  Accès à cheval sur une frontière de page :");
        var chevalBad = 0;
        for (uint page = 0x1000; page <= 0xFF000; page += 0x1000)
            for (uint d = 0xFFC; d <= 0xFFF; d++)
            {
                var a = page - 0x1000 + d;
                var o = Oracle.h_fastreadl(a);
                var c = _386_common.fastreadl(a);
                var o2 = Oracle.h_fastreadw(a);
                var c2 = _386_common.fastreadw(a);
                tested += 2;
                if (o != c || o2 != c2)
                {
                    if (chevalBad < 4)
                        Console.WriteLine($"    {a:X8} : l oracle {o:X8} C# {c:X8} | w oracle {o2:X4} C# {c2:X4}");
                    chevalBad++;
                    bad++;
                }
            }
        Console.WriteLine($"    {(0xFF000 / 0x1000) * 4 * 2,6} lectures  " +
                          (chevalBad == 0 ? "identiques" : $"*** {chevalBad} ECARTS ***"));

        Oracle.h_closepc();
        pc.closepc();

        Console.WriteLine();
        if (bad == 0)
        {
            Console.WriteLine($"Vert : {tested} lectures, octets ET état du cache de page identiques.");
            return 0;
        }
        Console.WriteLine($"ROUGE : {bad} écarts sur {tested} lectures.");
        return 1;
    }
}
