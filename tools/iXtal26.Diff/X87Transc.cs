// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// x87-transc FICHIER | --vecteurs N GRAINE — les transcendantes du noyau de 80 bits (G13.6, le noyau, N6 ;
// PLAN-G13.md) contre leur référence hors machine : les vecteurs de tools/x87ref/transcendantes.py (précision
// arbitraire, un seul arrondi à 64 bits selon RC), une ligne par cas, lus dans FICHIER, ou tirés à l'appel (N cas par
// instruction, de la graine GRAINE : python3, depuis la racine du dépôt, la porte). Chaque cas est joué sur le 387, en C# seul, par la couche du noyau
// (_x87_noyau.f2xm1, fyl2x, fpatan, fsin_cos, fptan), la pile posée par FNINIT puis ST(1) et ST(0) empilés, le mot de
// contrôle 037Fh avec RC ; il compare au bit près le nouvel ST(0), le nouvel ST(1) (FPTAN et FSINCOS), les six
// drapeaux, C1 et C2 (quand l'instruction le définit). Vert : aucun écart, et chaque instruction jouée. Les lignes
// « # difficile », que la référence n'a pas tranchées à sa marge, sont comptées, non jouées.

using iXtal26.Cpu;
using iXtal26.SoftFloat;
using static iXtal26.Cpu._386_common;

namespace iXtal26.Diff;

internal static class X87Transc
{
    private static extFloat80_t Lire(string h) =>
        new() { signExp = Convert.ToUInt16(h[..4], 16), signif = Convert.ToUInt64(h[4..], 16) };

    private static string Hexa(in extFloat80_t v) => $"{v.signExp:X4}{v.signif:X16}";

    private static string Drapeaux(int sw)
    {
        var s = "";
        foreach (var (b, c) in new[] { (1, 'I'), (4, 'Z'), (2, 'D'), (8, 'O'), (16, 'U'), (32, 'P') })
            if ((sw & b) != 0)
                s += c;
        return s.Length == 0 ? "-" : s;
    }

    internal static int Run(string[] args)
    {
        IEnumerable<string> lignes;
        if (args.Length == 1)
            lignes = File.ReadLines(args[0]);
        else if (args.Length == 3 && args[0] == "--vecteurs")
        {
            var psi = new System.Diagnostics.ProcessStartInfo("python3", ["-I", "tools/x87ref/transcendantes.py", "vecteurs", args[1], args[2]])
            {
                RedirectStandardOutput = true,
            };
            using var p = System.Diagnostics.Process.Start(psi)!;
            var tout = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            if (p.ExitCode != 0)
            {
                Console.WriteLine($"ROUGE : la référence a échoué (python3, code {p.ExitCode}).");
                return 1;
            }
            lignes = tout.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }
        else
        {
            Console.Error.WriteLine("usage : x87-transc FICHIER | --vecteurs N GRAINE (tools/x87ref/transcendantes.py)");
            return 2;
        }
        var fpu = cpu_c.fpu_type;
        cpu_c.fpu_type = cpu_c.FPU_387;
        var cas = new SortedDictionary<string, (int n, int bad)>();
        int difficiles = 0, montres = 0;
        try
        {
            foreach (var ligne in lignes)
            {
                if (ligne.StartsWith('#'))
                {
                    difficiles++;
                    continue;
                }
                var c = ligne.Split('\t');
                var (f, rc, x, y) = (c[0], int.Parse(c[1]), Lire(c[2]), c[3] == "-" ? (extFloat80_t?)null : Lire(c[3]));
                _x87_noyau.finit();
                cpu_state.npxc = (ushort)(0x037F | rc << 10);
                if (y is { } yy)
                    _x87_noyau.empiler(yy);
                _x87_noyau.empiler(x);
                switch (f)
                {
                    case "F2XM1": _x87_noyau.f2xm1(); break;
                    case "FYL2X": _x87_noyau.fyl2x(false); break;
                    case "FYL2XP1": _x87_noyau.fyl2x(true); break;
                    case "FPATAN": _x87_noyau.fpatan(); break;
                    case "FSIN": _x87_noyau.fsin_cos(0); break;
                    case "FCOS": _x87_noyau.fsin_cos(1); break;
                    case "FPTAN": _x87_noyau.fptan(false); break;
                    case "FSINCOS": _x87_noyau.fptan(true); break;
                    default: throw new InvalidDataException($"x87-transc : instruction inconnue, {f}");
                }
                var sw = cpu_state.npxs;
                var r0 = Hexa(_x87_noyau.R[_x87_noyau.phys(0)]);
                var r1 = c[5] == "-" ? "-" : Hexa(_x87_noyau.R[_x87_noyau.phys(1)]);
                var obtenu = $"{r0}\t{r1}\t{Drapeaux(sw)}\t{((sw & 0x200) != 0 ? 1 : 0)}\t" +
                             (c[8] == "-" ? "-" : (sw & 0x400) != 0 ? "1" : "0");
                var attendu = string.Join('\t', c[4..9]);
                var (n, bad) = cas.GetValueOrDefault(f);
                var faux = obtenu != attendu;
                cas[f] = (n + 1, bad + (faux ? 1 : 0));
                if (faux && montres++ < 20)
                    Console.WriteLine($"ÉCART {f} RC={rc} x={c[2]} y={c[3]}\n  attendu {attendu}\n  obtenu  {obtenu}");
            }
        }
        finally
        {
            cpu_c.fpu_type = fpu;
            _x87_noyau.finit();
        }
        var total = 0;
        var ecarts = 0;
        foreach (var (f, (n, bad)) in cas)
        {
            Console.WriteLine($"{f,-8} {n,8} cas, {bad} écart(s)");
            total += n;
            ecarts += bad;
        }
        Console.WriteLine($"{difficiles} cas difficiles écartés par la référence.");
        if (cas.Count == 0 || ecarts != 0)
        {
            Console.WriteLine($"\nROUGE : {ecarts} écart(s) sur {total} cas.");
            return 1;
        }
        Console.WriteLine($"\nVert : {total} cas, {cas.Count} instructions, au bit près de la référence.");
        return 0;
    }
}
