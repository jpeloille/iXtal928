// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// x87-couverture — la couverture comptée du noyau de 80 bits du x87 (G13.6, le noyau, N8b ; PLAN-G13.md : « la couverture
// comptée (gestionnaires × classes × RC × PC) »). Chaque fonction de la couche du noyau (_x87_noyau) que les gestionnaires
// appellent est jouée, en C# seul, sur une grille : vingt-trois valeurs de 80 bits, de toutes les classes du format (les
// zéros, des normaux, les bords de la plage, le dénormal et le pseudo-dénormal, le non normal et le pseudo-zéro, les
// infinis, le QNaN, le SNaN, l'indéfini, le pseudo-infini et le pseudo-NaN), et un registre vide, en un ou deux
// opérandes ; les quatre RC ; les trois PC ; les exceptions toutes masquées ou toutes démasquées ; les cinq
// coprocesseurs (8087, 287, 287XL, 387, 486). Sans oracle, chaque combinaison est jugée sur trois invariants : aucune
// exception de l'hôte ; l'étiquette de chaque registre non vide est celle de son contenu (etiquette_de) ; ES est posé si
// et seulement si une exception démasquée est levée. Vert : aucune combinaison fautive ; le compte, par fonction, est
// imprimé.

using iXtal26.Cpu;
using iXtal26.SoftFloat;
using static iXtal26.Cpu._386_common;

namespace iXtal26.Diff;

internal static class X87Couverture
{
    private static readonly (string nom, ushort se, ulong sig)[] Valeurs =
    [
        ("+0", 0x0000, 0), ("−0", 0x8000, 0), ("1", 0x3FFF, 0x8000000000000000), ("−1,5", 0xBFFF, 0xC000000000000000),
        ("0,5", 0x3FFE, 0x8000000000000000), ("π", 0x4000, 0xC90FDAA22168C235), ("10", 0x4002, 0xA000000000000000),
        ("2^63", 0x403E, 0x8000000000000000), ("2^64", 0x403F, 0x8000000000000000),
        ("2^−16382", 0x0001, 0x8000000000000000), ("max", 0x7FFE, 0xFFFFFFFFFFFFFFFF), ("−max", 0xFFFE, 0xFFFFFFFFFFFFFFFF),
        ("dénormal", 0x0000, 0x0000000000000001), ("dénormal haut", 0x8000, 0x7FFFFFFFFFFFFFFF),
        ("pseudo-dénormal", 0x0000, 0x8000000000000001), ("non normal", 0x3FFF, 0x4000000000000000),
        ("pseudo-zéro", 0x3FFF, 0), ("+∞", 0x7FFF, 0x8000000000000000), ("−∞", 0xFFFF, 0x8000000000000000),
        ("QNaN", 0x7FFF, 0xC000000000000001), ("SNaN", 0x7FFF, 0x8000000000000001), ("pseudo-infini", 0x7FFF, 0),
        ("pseudo-NaN", 0x7FFF, 0x4000000000000001),
    ];

    private static readonly int[] Fpus = [cpu_c.FPU_8087, cpu_c.FPU_287, cpu_c.FPU_287XL, cpu_c.FPU_387, cpu_c.FPU_BUILTIN];

    private static readonly (string nom, int operandes, Action op)[] Fonctions =
    [
        ("FADD ST, ST(1)", 2, () => _x87_noyau.arith_st0(_x87_noyau.ADD, false, 1)),
        ("FSUBR ST, ST(1)", 2, () => _x87_noyau.arith_st0(_x87_noyau.SUB, true, 1)),
        ("FMUL ST, ST(1)", 2, () => _x87_noyau.arith_st0(_x87_noyau.MUL, false, 1)),
        ("FDIV ST, ST(1)", 2, () => _x87_noyau.arith_st0(_x87_noyau.DIV, false, 1)),
        ("FDIVRP ST(1), ST", 2, () => _x87_noyau.arith_sti(_x87_noyau.DIV, true, 1, true)),
        ("FCOM ST(1)", 2, () => _x87_noyau.fcom_st(1, true)),
        ("FUCOM ST(1)", 2, () => _x87_noyau.fcom_st(1, false)),
        ("FSCALE", 2, _x87_noyau.fscale),
        ("FPREM", 2, () => _x87_noyau.fprem(false)),
        ("FPREM1", 2, () => _x87_noyau.fprem(true)),
        ("FYL2X", 2, () => _x87_noyau.fyl2x(false)),
        ("FYL2XP1", 2, () => _x87_noyau.fyl2x(true)),
        ("FPATAN", 2, _x87_noyau.fpatan),
        ("FXCH ST(1)", 2, () => _x87_noyau.fxch(1)),
        ("FSQRT", 1, _x87_noyau.fsqrt),
        ("FRNDINT", 1, _x87_noyau.frndint),
        ("FXTRACT", 1, _x87_noyau.fxtract),
        ("F2XM1", 1, _x87_noyau.f2xm1),
        ("FSIN", 1, () => _x87_noyau.fsin_cos(0)),
        ("FCOS", 1, () => _x87_noyau.fsin_cos(1)),
        ("FPTAN", 1, () => _x87_noyau.fptan(false)),
        ("FSINCOS", 1, () => _x87_noyau.fptan(true)),
        ("FTST", 1, _x87_noyau.ftst),
        ("FXAM", 1, _x87_noyau.fxam),
        ("FCHS", 1, () => _x87_noyau.signe(false)),
        ("FST ST(1)", 1, () => _x87_noyau.fst_st(1)),
        ("FST m32", 1, () => _x87_noyau.fst_f32(out _)),
        ("FST m64", 1, () => _x87_noyau.fst_f64(out _)),
        ("FSTP m80", 1, () => _x87_noyau.fst_f80(out _)),
        ("FIST m16", 1, () => _x87_noyau.fist(16, out _)),
        ("FIST m32", 1, () => _x87_noyau.fist(32, out _)),
        ("FIST m64", 1, () => _x87_noyau.fist(64, out _)),
        ("FBSTP", 1, () => _x87_noyau.fbstp(out _, out _)),
        ("FLD m80", 1, () => { var v = _x87_noyau.R[_x87_noyau.phys(0)]; _x87_noyau.fld_f80(v.signif, v.signExp); }),
        ("FLD ST(0) et constantes", 1, () => _x87_noyau.constante(3)),
    ];

    internal static int Run(string[] args)
    {
        var fpu = cpu_c.fpu_type;
        var fautes = 0;
        var montrees = 0;
        long total = 0;
        try
        {
            foreach (var (nom, n, op) in Fonctions)
            {
                long jouees = 0;
                var mauvaises = 0;
                // Un opérande : chaque valeur, et ST(0) vide ; deux : chaque couple, et ST(1) vide.
                var gauches = Valeurs.Length + 1;
                var droites = n == 2 ? Valeurs.Length + 1 : 1;
                foreach (var f in Fpus)
                for (var rc = 0; rc < 4; rc++)
                foreach (var pc in (int[])[0, 2, 3])
                foreach (var masque in (int[])[0x3F, 0x00])
                for (var i = 0; i < gauches; i++)
                for (var j = 0; j < droites; j++)
                {
                    cpu_c.fpu_type = f;
                    _x87_noyau.mise_sous_tension();
                    cpu_state.npxc = (ushort)(0x0040 | masque | pc << 8 | rc << 10);
                    cpu_state.npxs = 0;
                    if (n == 2 && j < Valeurs.Length)
                        Poser(Valeurs[j]);
                    else if (n == 2)
                        _x87_noyau.empiler(default);
                    if (i < Valeurs.Length)
                        Poser(Valeurs[i]);
                    else if (n == 2)
                        _x87_noyau.empiler(default);
                    if (n == 2 && j == Valeurs.Length)
                        _x87_noyau.etiqueter((cpu_state.TOP + 1) & 7, _x87_noyau.VIDE);
                    if (i == Valeurs.Length)
                        _x87_noyau.etiqueter(cpu_state.TOP & 7, _x87_noyau.VIDE);
                    jouees++;
                    string? faute = null;
                    try
                    {
                        op();
                        faute = Invariants();
                    }
                    catch (Exception e)
                    {
                        faute = $"exception de l'hôte : {e.GetType().Name} ({e.Message})";
                    }
                    if (faute is null)
                        continue;
                    mauvaises++;
                    if (montrees++ < 30)
                        Console.WriteLine($"FAUTE {nom} (fpu {f}, RC {rc}, PC {pc}, masques {masque:X2}) " +
                                          $"{(i < Valeurs.Length ? Valeurs[i].nom : "vide")}" +
                                          $"{(n == 2 ? ", " + (j < Valeurs.Length ? Valeurs[j].nom : "vide") : "")} : {faute}");
                }
                Console.WriteLine($"  {nom,-26} {jouees,8} combinaisons{(mauvaises > 0 ? $", {mauvaises} fautive(s)" : "")}");
                total += jouees;
                fautes += mauvaises;
            }
        }
        finally
        {
            cpu_c.fpu_type = fpu;
            _x87_noyau.mise_sous_tension();
        }
        Console.WriteLine($"\n{Fonctions.Length} fonctions, {Valeurs.Length} valeurs et le vide, 4 RC × 3 PC × 2 masques × 5 " +
                          $"coprocesseurs : {total} combinaisons.");
        if (fautes != 0)
        {
            Console.WriteLine($"\nROUGE : {fautes} combinaison(s) fautive(s).");
            return 1;
        }
        Console.WriteLine("\nVert : aucune exception de l'hôte, les étiquettes conformes aux registres, ES posé exactement " +
                          "pour une exception démasquée.");
        return 0;
    }

    private static void Poser((string nom, ushort se, ulong sig) v) =>
        _x87_noyau.empiler(new extFloat80_t { signExp = v.se, signif = v.sig });

    private static string? Invariants()
    {
        for (var p = 0; p < 8; p++)
        {
            var e = _x87_noyau.etiquette(p);
            if (e != _x87_noyau.VIDE && e != _x87_noyau.etiquette_de(_x87_noyau.R[p]))
                return $"R[{p}] {_x87_noyau.R[p].signExp:X4} {_x87_noyau.R[p].signif:X16} étiqueté {e}, son contenu " +
                       $"{_x87_noyau.etiquette_de(_x87_noyau.R[p])}";
        }
        var demasquees = cpu_state.npxs & ~cpu_state.npxc & 0x3F;
        var es = (cpu_state.npxs & 0x80) != 0;
        if (es != (demasquees != 0))
            return $"ES {(es ? "posé" : "effacé")} pour le mot d'état {cpu_state.npxs:X4} sous le mot de contrôle {cpu_state.npxc:X4}";
        return null;
    }
}
