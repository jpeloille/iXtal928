// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel du x87 (G13.6 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les gestionnaires du x87 sont générés (tools/x87gen) et aiguillés par table : aucune garde ne s'y écrit (PLAN-G13.md,
// § Le mécanisme). En mode matériel, cpu_set pose donc des tables du mode : des copies de celles de PCem, où chaque
// correction demandée remplace ses gestionnaires (`poser`, sous la garde de son PB, dans cpu_set). Le mode PCem garde
// les tables de PCem, intactes, sans copie. Le 8087, que execx86 indexe en dur (ops_808x_fpu_*), verra ses entrées
// remplacées en place, au même endroit, pour les corrections qui le concernent (ses gestionnaires, dans
// 808x.Materiel.cs, appellent les mêmes aides que ceux du 286, du 386 et du 486).
//
// Chaque gestionnaire ouvre sur son marqueur et incrémente la sonde (ModeMateriel.Sonde) ; la source, le cas qui
// discrimine et la panne qui le rougit sont dans l'entrée PB de PCEM_BUGS.md.

using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;

namespace iXtal26.Cpu;

// Une classe à part, comme _386_materiel : des membres de plus dans _386 renuméroteraient ses lambdas.
internal static class _x87_materiel
{
    /// <summary>Pose dans les tables du mode les gestionnaires de la correction `pb` (sans coprocesseur, rien).</summary>
    internal static void poser(int pb)
    {
        // La carte se remet avec le système, coprocesseur ou non : ni verrou, ni IGNNE#, ni la NMI du 8087.
        if (pb == 204)
                verrou = ignne = false;
        if (pb == 69)
                _808x.nmi = 0;
        if (cpu_c.hasfpu == 0)
                return;
        switch (pb)
        {
        case 61:
                df(0xE0, opFSTSW_AX_materiel);
                break;
        case 57:
                d8_dc(0x1A, opFCOM_materiel);
                d8_dc(0x1B, opFCOMP_materiel);
                de(0xD9, opFCOMPP_materiel);
                if (cpu_c.fpu_type == cpu_c.FPU_8087)
                        _808x.poser_8087_materiel(57);
                break;
        case 64:
                d9(0xE4, opFTST_materiel);
                if (cpu_c.fpu_type == cpu_c.FPU_8087)
                        _808x.poser_8087_materiel(64);
                break;
        case 63:
                d9(0xE5, opFXAM_materiel);
                if (cpu_c.fpu_type == cpu_c.FPU_8087)
                        _808x.poser_8087_materiel(63);
                break;
        case 66:
                for (var k = 0; k < 5; k++)
                        d9(0xE9 + k, Constante(k));
                if (cpu_c.fpu_type == cpu_c.FPU_8087)
                        _808x.poser_8087_materiel(66);
                break;
        case 67:
                for (var i = 0; i < 8; i++)
                {
                        dd(0xD0 + i, opFST_materiel);
                        dd(0xD8 + i, opFSTP_materiel);
                        d9(0xD8 + i, opFSTP_materiel);
                }
                if (cpu_c.fpu_type == cpu_c.FPU_8087)
                        _808x.poser_8087_materiel(67);
                break;
        case 207:
                partout(h => h.Method.Name is "opFSTENV_a16" or "opFSTENV_a32" ? masque_fstenv(h) : null);
                if (cpu_c.fpu_type == cpu_c.FPU_8087)
                        _808x.poser_8087_materiel(207);
                break;
        case 213:
                // Le 287XL (un cœur de 387 : déduit, comme pour PB-66), le 387 et le 486 : après une comparaison, le C1 du
                // 8087 et du 287 est indéfini.
                if (cpu_c.fpu_type >= cpu_c.FPU_287XL)
                        partout(h => h.Method.Name.StartsWith("opFCOM", StringComparison.Ordinal)
                                     || h.Method.Name.StartsWith("opFUCOM", StringComparison.Ordinal)
                                     || h.Method.Name.StartsWith("opFTST", StringComparison.Ordinal)
                                ? efface_c1(h) : null);
                break;
        case 59:
                partout(h => h.Method.Name == "opFCLEX" ? efface_b(h) : null);
                if (cpu_c.fpu_type == cpu_c.FPU_8087)
                        _808x.poser_8087_materiel(59);
                break;
        case 69:
                if (cpu_c.fpu_type == cpu_c.FPU_8087)
                        _808x.poser_8087_materiel(69);
                break;
        case 204:
                if (cpu_c.fpu_type != cpu_c.FPU_8087)
                        attente();
                break;
        }
    }

    /// <summary>Remplace, dans les seize tables du mode, chaque gestionnaire que `f` désigne (par son nom) par celui que
    /// `f` rend ; une table n'est copiée que si elle change. Les gestionnaires déjà posés (FCOM de registre, PB-57) sont
    /// enveloppés comme ceux de PCem : `cpu_set` pose PB-213 après eux, et PB-204, qui enveloppe par
    /// position, après toutes les poses par nom.</summary>
    private static void partout(Func<OpFn, OpFn?> f)
    {
        _386.x86_opcodes_d8_a16 = sur(_386.x86_opcodes_d8_a16!, _386.ops_fpu_d8_a16, f);
        _386.x86_opcodes_d8_a32 = sur(_386.x86_opcodes_d8_a32!, _386.ops_fpu_d8_a32, f);
        _386.x86_opcodes_d9_a16 = sur(_386.x86_opcodes_d9_a16!, _386.ops_fpu_d9_a16, f);
        _386.x86_opcodes_d9_a32 = sur(_386.x86_opcodes_d9_a32!, _386.ops_fpu_d9_a32, f);
        _386.x86_opcodes_da_a16 = sur(_386.x86_opcodes_da_a16!, _386.ops_fpu_da_a16, f);
        _386.x86_opcodes_da_a32 = sur(_386.x86_opcodes_da_a32!, _386.ops_fpu_da_a32, f);
        _386.x86_opcodes_db_a16 = sur(_386.x86_opcodes_db_a16!, _386.ops_fpu_db_a16, f);
        _386.x86_opcodes_db_a32 = sur(_386.x86_opcodes_db_a32!, _386.ops_fpu_db_a32, f);
        _386.x86_opcodes_dc_a16 = sur(_386.x86_opcodes_dc_a16!, _386.ops_fpu_dc_a16, f);
        _386.x86_opcodes_dc_a32 = sur(_386.x86_opcodes_dc_a32!, _386.ops_fpu_dc_a32, f);
        _386.x86_opcodes_dd_a16 = sur(_386.x86_opcodes_dd_a16!, _386.ops_fpu_dd_a16, f);
        _386.x86_opcodes_dd_a32 = sur(_386.x86_opcodes_dd_a32!, _386.ops_fpu_dd_a32, f);
        _386.x86_opcodes_de_a16 = sur(_386.x86_opcodes_de_a16!, _386.ops_fpu_de_a16, f);
        _386.x86_opcodes_de_a32 = sur(_386.x86_opcodes_de_a32!, _386.ops_fpu_de_a32, f);
        _386.x86_opcodes_df_a16 = sur(_386.x86_opcodes_df_a16!, _386.ops_fpu_df_a16, f);
        _386.x86_opcodes_df_a32 = sur(_386.x86_opcodes_df_a32!, _386.ops_fpu_df_a32, f);
    }

    private static OpFn[] sur(OpFn[] posee, OpFn[] pcem, Func<OpFn, OpFn?> f)
    {
        var t = posee;
        for (var i = 0; i < posee.Length; i++)
                if (posee[i] is { } h && f(h) is { } n)
                {
                        t = copie(t, pcem);
                        t[i] = n;
                }
        return t;
    }

    private static void d8_dc(int i, OpFn f)
    {
        _386.x86_opcodes_d8_a16 = copie(_386.x86_opcodes_d8_a16!, _386.ops_fpu_d8_a16);
        _386.x86_opcodes_d8_a32 = copie(_386.x86_opcodes_d8_a32!, _386.ops_fpu_d8_a32);
        _386.x86_opcodes_dc_a16 = copie(_386.x86_opcodes_dc_a16!, _386.ops_fpu_dc_a16);
        _386.x86_opcodes_dc_a32 = copie(_386.x86_opcodes_dc_a32!, _386.ops_fpu_dc_a32);
        _386.x86_opcodes_d8_a16[i] = _386.x86_opcodes_d8_a32[i] = _386.x86_opcodes_dc_a16[i] = _386.x86_opcodes_dc_a32[i] = f;
    }

    private static void d9(int i, OpFn f)
    {
        _386.x86_opcodes_d9_a16 = copie(_386.x86_opcodes_d9_a16!, _386.ops_fpu_d9_a16);
        _386.x86_opcodes_d9_a32 = copie(_386.x86_opcodes_d9_a32!, _386.ops_fpu_d9_a32);
        _386.x86_opcodes_d9_a16[i] = _386.x86_opcodes_d9_a32[i] = f;
    }

    private static void dd(int i, OpFn f)
    {
        _386.x86_opcodes_dd_a16 = copie(_386.x86_opcodes_dd_a16!, _386.ops_fpu_dd_a16);
        _386.x86_opcodes_dd_a32 = copie(_386.x86_opcodes_dd_a32!, _386.ops_fpu_dd_a32);
        _386.x86_opcodes_dd_a16[i] = _386.x86_opcodes_dd_a32[i] = f;
    }

    private static void de(int i, OpFn f)
    {
        _386.x86_opcodes_de_a16 = copie(_386.x86_opcodes_de_a16!, _386.ops_fpu_de_a16);
        _386.x86_opcodes_de_a32 = copie(_386.x86_opcodes_de_a32!, _386.ops_fpu_de_a32);
        _386.x86_opcodes_de_a16[i] = _386.x86_opcodes_de_a32[i] = f;
    }

    private static void df(int i, OpFn f)
    {
        _386.x86_opcodes_df_a16 = copie(_386.x86_opcodes_df_a16!, _386.ops_fpu_df_a16);
        _386.x86_opcodes_df_a32 = copie(_386.x86_opcodes_df_a32!, _386.ops_fpu_df_a32);
        _386.x86_opcodes_df_a16[i] = _386.x86_opcodes_df_a32[i] = f;
    }

    /// <summary>La table du mode : une copie de celle de PCem à la première correction qui la touche, puis la même.</summary>
    private static OpFn[] copie(OpFn[] posee, OpFn[] pcem) => ReferenceEquals(posee, pcem) ? (OpFn[])pcem.Clone() : posee;

    // pcem bug, fixed in hardware mode: PB-61 — FNSTSW AX rend le mot d'état entier, TOP compris (SDM vol. 2, FSTSW ;
    //   287 PRM), comme la forme mémoire (opFSTSW_a16) le compose. Le 287, le 387 et le 486 ; le 8087 n'a pas DF E0
    //   (PB-200).
    internal static int opFSTSW_AX_materiel(uint32_t fetchdat)
    {
        if (_386.FP_ENTER())
                return 1;
        cpu_state.pc++;
        AX = (uint16_t)((cpu_state.npxs & 0xC7FF) | ((cpu_state.TOP & 7) << 11));
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fstcw_sw);
        ModeMateriel.Sonde[61]++;
        return 0;
    }

    // ===== Les comparaisons : PB-57, PB-64, PB-70 (un groupe, ModeMateriel.Groupes) =====
    //
    // Les codes de condition seulement : IE, que le silicium pose pour un opérande non comparable, reste à poser avec
    // les autres exceptions (PB-59, le noyau). C1 : PB-213, qui enveloppe ces gestionnaires comme ceux de PCem.

    /// <summary>Le 8087 et le 287 comparent en projectif tant que IC (bit 12 de npxc) est nul : +∞ = −∞, et un infini
    /// n'est pas comparable à un fini. Le 287XL, le 387 et le 486 n'ont que l'affine.</summary>
    private static bool projectif() =>
        (cpu_c.fpu_type == cpu_c.FPU_8087 || cpu_c.fpu_type == cpu_c.FPU_287) && (cpu_state.npxc & 0x1000) == 0;

    // pcem bug, fixed in hardware mode: PB-70 — la comparaison du silicium : un NaN, non ordonné (C3 C2 C0 = 111) ; en
    //   projectif (le 8087 et le 287, IC nul), deux infinis égaux (C3), un infini contre un fini non ordonné (Numerics
    //   Supplement, 1980, p. S-18 et table S-27 ; 287 PRM fig. 1-10) ; sinon, l'ordre.
    internal static uint16_t compare_materiel(double a, double b)
    {
        if (double.IsNaN(a) || double.IsNaN(b))
                return (uint16_t)(x87_c.C0 | x87_c.C2 | x87_c.C3);
        if (projectif() && (double.IsInfinity(a) || double.IsInfinity(b)))
        {
                ModeMateriel.Sonde[70]++;
                return double.IsInfinity(a) && double.IsInfinity(b) ? x87_c.C3 : (uint16_t)(x87_c.C0 | x87_c.C2 | x87_c.C3);
        }
        if (a < b)
                return x87_c.C0;
        if (a == b)
                return x87_c.C3;
        return 0;
    }

    // pcem bug, fixed in hardware mode: PB-57 — FCOM ST(i) compare comme les autres formes (un NaN, non ordonné : SDM
    //   vol. 2, FCOM ; 387 PRM annexe C), et FCOM, FCOMP et FCOMPP comptent le temps de fcom, pas celui de fadd.
    internal static int opFCOM_materiel(uint32_t fetchdat)
    {
        if (_386.FP_ENTER())
                return 1;
        cpu_state.pc++;
        cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C2 | x87_c.C3));
        cpu_state.npxs |= compare_materiel(_386.ST(0), _386.ST((int)(fetchdat & 7)));
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fcom);
        ModeMateriel.Sonde[57]++;
        return 0;
    }

    internal static int opFCOMP_materiel(uint32_t fetchdat)
    {
        if (_386.FP_ENTER())
                return 1;
        cpu_state.pc++;
        cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C2 | x87_c.C3));
        cpu_state.npxs |= compare_materiel(_386.ST(0), _386.ST((int)(fetchdat & 7)));
        _386.x87_pop();
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fcom);
        ModeMateriel.Sonde[57]++;
        return 0;
    }

    /// <summary>FCOMPP : la comparaison du silicium, sans le contournement de PCem pour −0 contre +0 (PB-58, du même
    /// groupe ; le test de `fcompp` sur materiel.pb_58 ne fait que le dire).</summary>
    internal static int opFCOMPP_materiel(uint32_t fetchdat)
    {
        if (_386.FP_ENTER())
                return 1;
        cpu_state.pc++;
        cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C2 | x87_c.C3));
        cpu_state.npxs |= fcompp(_386.ST(0), _386.ST(1));
        _386.x87_pop();
        _386.x87_pop();
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fcom);
        ModeMateriel.Sonde[57]++;
        return 0;
    }

    // pcem bug, fixed in hardware mode: PB-58 — −0 contre +0 sont égaux (IEEE 754-1985 § 5.7 ; SDM vol. 2, FCOM) : le
    //   contournement de PCem pour la détection du 387 n'est plus pris quand PB-58 est demandé.
    internal static uint16_t fcompp(double a, double b)
    {
        if (!materiel.pb_58 && BitConverter.DoubleToUInt64Bits(a) == (1UL << 63) && BitConverter.DoubleToUInt64Bits(b) == 0)
                return x87_c.C0;
        if (materiel.pb_58 && BitConverter.DoubleToUInt64Bits(a) == (1UL << 63) && BitConverter.DoubleToUInt64Bits(b) == 0)
                ModeMateriel.Sonde[58]++;
        return compare_materiel(a, b);
    }

    // pcem bug, fixed in hardware mode: PB-64 — FTST compare ST(0) à +0 comme FCOM : un NaN, non ordonné (SDM vol. 2,
    //   FTST ; Numerics Supplement table S-27) ; en projectif, un infini aussi.
    internal static int opFTST_materiel(uint32_t fetchdat)
    {
        if (_386.FP_ENTER())
                return 1;
        cpu_state.pc++;
        cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C2 | x87_c.C3));
        cpu_state.npxs |= compare_materiel(_386.ST(0), 0.0);
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.ftst);
        ModeMateriel.Sonde[64]++;
        return 0;
    }

    // pcem bug, fixed in hardware mode: PB-63 — FXAM rend la classe de ST(0) (SDM vol. 2, FXAM ; Numerics Supplement, 1980,
    //   table S-13 ; 387 PRM annexe C) : vide C3 C0, NaN C0, infini C2 C0, zéro C3, normal C2, et le signe dans C1, −0
    //   et un NaN négatif compris. Un dénormal de 64 bits est normal dans le format de 80 bits que le registre tient :
    //   la classe « dénormal » n'apparaît pas. Le vide garde le C1 de PCem (le code du 8087 et du 287 : inconnu).
    internal static uint16_t fxam_materiel()
    {
        uint16_t c;
        var v = _386.ST(0);
        if (cpu_state.tag[cpu_state.TOP & 7] == x87_c.TAG_EMPTY)
                return (uint16_t)(x87_c.C0 | x87_c.C3 | (v < 0.0 ? x87_c.C1 : 0));
        if (double.IsNaN(v))
                c = x87_c.C0;
        else if (double.IsInfinity(v))
                c = (uint16_t)(x87_c.C2 | x87_c.C0);
        else if (v == 0.0)
                c = x87_c.C3;
        else
                c = x87_c.C2;
        if ((BitConverter.DoubleToUInt64Bits(v) >> 63) != 0)
                c |= x87_c.C1;
        return c;
    }

    internal static int opFXAM_materiel(uint32_t fetchdat)
    {
        if (_386.FP_ENTER())
                return 1;
        cpu_state.pc++;
        cpu_state.npxs &= unchecked((uint16_t)~(x87_c.C0 | x87_c.C1 | x87_c.C2 | x87_c.C3));
        cpu_state.npxs |= fxam_materiel();
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fxam);
        ModeMateriel.Sonde[63]++;
        return 0;
    }

    // pcem bug, fixed in hardware mode: PB-66 — les constantes de D9 E9 à ED (log₂ 10, log₂ e, π, log₁₀ 2, ln 2), au double
    //   le plus proche de leur valeur exacte sur le 8087 et le 287 ; selon RC sur le 287XL, le 387 et le 486, qui
    //   arrondissent leurs constantes au mode courant (387 PRM § 4.7 ; i486 PRM § 17.6 ; SDM vol. 2, FLD1…FLDZ). PCem
    //   pousse ln 2 d'un ulp trop haut, et ignore RC. Les doubles, calculés de la valeur exacte à 60 chiffres : le plus
    //   proche, celui du dessous (vers −∞ et vers zéro, les constantes étant positives), celui du dessus (vers +∞).
    private static readonly ulong[][] Constantes =
    [
        [0x400A934F0979A371, 0x400A934F0979A371, 0x400A934F0979A372],   // log₂ 10
        [0x3FF71547652B82FE, 0x3FF71547652B82FE, 0x3FF71547652B82FF],   // log₂ e
        [0x400921FB54442D18, 0x400921FB54442D18, 0x400921FB54442D19],   // π
        [0x3FD34413509F79FF, 0x3FD34413509F79FE, 0x3FD34413509F79FF],   // log₁₀ 2
        [0x3FE62E42FEFA39EF, 0x3FE62E42FEFA39EF, 0x3FE62E42FEFA39F0],   // ln 2
    ];

    /// <summary>La constante `k` (0 : log₂ 10 … 4 : ln 2), arrondie comme le coprocesseur l'arrondit.</summary>
    internal static ulong constante_materiel(int k)
    {
        var c = Constantes[k];
        if (cpu_c.fpu_type < cpu_c.FPU_287XL)
                return c[0];
        return ((cpu_state.npxc >> 10) & 3) switch { 1 or 3 => c[1], 2 => c[2], _ => c[0] };
    }

    private static OpFn Constante(int k) => fetchdat =>
    {
        if (_386.FP_ENTER())
                return 1;
        cpu_state.pc++;
        _386.x87_push_u64(constante_materiel(k));
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fld_const);
        ModeMateriel.Sonde[66]++;
        return 0;
    };

    // pcem bug, fixed in hardware mode: PB-67 — FST et FSTP ST(i) copient aussi l'entier exact que TAG_UINT64 annonce
    //   (MM[].q), comme FLD ST(i) et FXCH le font : un registre de 80 bits tient tout entier de 64 bits (SDM vol. 1
    //   § 8.1.2 ; SDM vol. 2, FST).
    internal static void fst_materiel(uint32_t fetchdat)
    {
        _386.ST((int)(fetchdat & 7)) = _386.ST(0);
        cpu_state.tag[(cpu_state.TOP + (int)fetchdat) & 7] = cpu_state.tag[cpu_state.TOP & 7];
        cpu_state.MM[(cpu_state.TOP + (int)fetchdat) & 7].q = cpu_state.MM[cpu_state.TOP & 7].q;
        ModeMateriel.Sonde[67]++;
    }

    internal static int opFST_materiel(uint32_t fetchdat)
    {
        if (_386.FP_ENTER())
                return 1;
        cpu_state.pc++;
        fst_materiel(fetchdat);
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fst);
        return 0;
    }

    internal static int opFSTP_materiel(uint32_t fetchdat)
    {
        if (_386.FP_ENTER())
                return 1;
        cpu_state.pc++;
        fst_materiel(fetchdat);
        _386.x87_pop();
        _386.CLOCK_CYCLES(x87_timings_c.x87_timings.fst);
        return 0;
    }

    // pcem bug, fixed in hardware mode: PB-207 — FSTENV range l'environnement, puis masque les six exceptions (SDM vol. 2,
    //   FSTENV/FNSTENV ; 287 PRM : déduit). Le gestionnaire de PCem, puis les masques, s'il a abouti.
    private static OpFn masque_fstenv(OpFn pcem) => fetchdat =>
    {
        var r = pcem(fetchdat);
        if (r == 0)
                masquer_exceptions();
        return r;
    };

    internal static void masquer_exceptions()
    {
        if ((cpu_state.npxc & 0x3F) != 0x3F)
                ModeMateriel.Sonde[207]++;
        cpu_state.npxc |= 0x3F;
    }

    // pcem bug, fixed in hardware mode: PB-213 — FCOM, FCOMP, FCOMPP, FICOM, FICOMP, FUCOM, FUCOMP, FUCOMPP et FTST
    //   remettent C1 à zéro sur le 387 et le 486 (SDM vol. 2, « C1 Set to 0 ») ; le 287XL : déduit. La comparaison ne
    //   touche pas C1 : l'effacer après le gestionnaire de PCem, s'il a abouti, revient à l'effacer avec C0, C2 et C3.
    private static OpFn efface_c1(OpFn pcem) => fetchdat =>
    {
        var r = pcem(fetchdat);
        if (r == 0 && (cpu_state.npxs & x87_c.C1) != 0)
        {
                cpu_state.npxs &= unchecked((uint16_t)~x87_c.C1);
                ModeMateriel.Sonde[213]++;
        }
        return r;
    };

    // ===== L'exception démasquée et son acheminement : PB-59, PB-69, PB-204 (un groupe, ModeMateriel.Groupes) =====
    //
    // ZE seule : x87_div est le seul site où PCem lève une exception ; les autres viendront avec le noyau de 80 bits
    // (point de décision n° 10). Le 8087 signale par sa sortie INT, que le PC et le XT mènent à la NMI (AP-578 § 2.1) ;
    // le 287 et le 387 par ERROR#, que la carte AT mène à IRQ13 en verrouillant BUSY# jusqu'à une écriture au port F0h
    // (387 PRM, annexe F ; AP-578 § 2.2.1) ; le 486 par FERR#, mené à IRQ13, et il prend lui-même #MF si NE (bit 5 de
    // CR0) est posé, sinon il s'arrête devant l'instruction suivante jusqu'à IGNNE#, que la carte pose à l'écriture au
    // port F0h (486 PRM § 16.2.1.2). Chaque signal part sur un front : une exception de plus, ES déjà posé, n'en lève
    // pas d'autre.

    private const uint16_t ES = 0x80, B = 0x8000;

    /// <summary>Le verrou de la carte AT : ERROR# du 287 ou du 387 tient BUSY# actif jusqu'à une écriture au port F0h ou
    /// F1h.</summary>
    internal static bool verrou;

    /// <summary>IGNNE# du 486 : posé par une écriture au port F0h tant que FERR# est actif, il retombe avec lui.</summary>
    internal static bool ignne;

    // pcem bug, fixed in hardware mode: PB-59 — une exception démasquée pose ES (IR sur le 8087) et B (387 PRM, mot
    //   d'état ; 287 PRM p. 1-10 ; le 8087 : déduit, comme le 287) ; la destination et la pile restent intactes, comme
    //   le gestionnaire de PCem les laisse ; ses cycles aussi (déduit). PB-69 : sur le 8087, la sortie INT, si IEM (bit
    //   7 de npxc) est nul, lève la NMI (AP-578 § 2.1). PB-204 : sur le 287 et le 387, IRQ13 et le verrou ; sur le 486,
    //   IRQ13, que NE soit posé ou non (déduit : la carte mène FERR# à IRQ13 ; le 486 l'active dans les deux cas).
    internal static bool exception_demasquee()
    {
        var front = (cpu_state.npxs & ES) == 0;
        cpu_state.npxs |= ES | B;
        ModeMateriel.Sonde[59]++;
        if (!front)
                return true;
        if (cpu_c.fpu_type == cpu_c.FPU_8087)
        {
                if ((cpu_state.npxc & 0x80) == 0)
                {
                        _808x.nmi = 1;
                        ModeMateriel.Sonde[69]++;
                }
                return true;
        }
        Models.pic.picint(1 << 13);
        if (cpu_c.fpu_type == cpu_c.FPU_BUILTIN)
                ignne = false;
        else
        {
                verrou = true;
                ModeMateriel.Sonde[204]++;
        }
        return true;
    }

    // pcem bug, fixed in hardware mode: PB-59 — FCLEX efface les exceptions, ES et B (287 PRM, FCLEX ; 486 PRM,
    //   FCLEX) ; celui de PCem (`npxs &= FF00h`) garde B.
    private static OpFn efface_b(OpFn pcem) => fetchdat =>
    {
        var r = pcem(fetchdat);
        if (r == 0)
                effacer_b();
        return r;
    };

    internal static void effacer_b()
    {
        if ((cpu_state.npxs & B) == 0)
                return;
        cpu_state.npxs &= unchecked((uint16_t)~B);
        ModeMateriel.Sonde[59]++;
    }

    // pcem bug, fixed in hardware mode: PB-69 — FCLEX et FINIT du 8087 effacent IR : la sortie INT retombe, et une NMI
    //   que le port A0h retenait ne viendra plus. Que FLDCW, FENI ou FDISI changent IEM sous une exception en attente
    //   n'est pas modélisé.
    internal static void int_8087_retombe()
    {
        if (_808x.nmi == 0)
                return;
        _808x.nmi = 0;
        ModeMateriel.Sonde[69]++;
    }

    /// <summary>Enveloppe, dans les seize tables du mode, chaque instruction qui attend : toutes, sauf les sept que le
    /// processeur lance sans tester BUSY# ni ERROR#.</summary>
    private static void attente()
    {
        _386.x86_opcodes_d8_a16 = attente(_386.x86_opcodes_d8_a16!, _386.ops_fpu_d8_a16, 0xD8);
        _386.x86_opcodes_d8_a32 = attente(_386.x86_opcodes_d8_a32!, _386.ops_fpu_d8_a32, 0xD8);
        _386.x86_opcodes_d9_a16 = attente(_386.x86_opcodes_d9_a16!, _386.ops_fpu_d9_a16, 0xD9);
        _386.x86_opcodes_d9_a32 = attente(_386.x86_opcodes_d9_a32!, _386.ops_fpu_d9_a32, 0xD9);
        _386.x86_opcodes_da_a16 = attente(_386.x86_opcodes_da_a16!, _386.ops_fpu_da_a16, 0xDA);
        _386.x86_opcodes_da_a32 = attente(_386.x86_opcodes_da_a32!, _386.ops_fpu_da_a32, 0xDA);
        _386.x86_opcodes_db_a16 = attente(_386.x86_opcodes_db_a16!, _386.ops_fpu_db_a16, 0xDB);
        _386.x86_opcodes_db_a32 = attente(_386.x86_opcodes_db_a32!, _386.ops_fpu_db_a32, 0xDB);
        _386.x86_opcodes_dc_a16 = attente(_386.x86_opcodes_dc_a16!, _386.ops_fpu_dc_a16, 0xDC);
        _386.x86_opcodes_dc_a32 = attente(_386.x86_opcodes_dc_a32!, _386.ops_fpu_dc_a32, 0xDC);
        _386.x86_opcodes_dd_a16 = attente(_386.x86_opcodes_dd_a16!, _386.ops_fpu_dd_a16, 0xDD);
        _386.x86_opcodes_dd_a32 = attente(_386.x86_opcodes_dd_a32!, _386.ops_fpu_dd_a32, 0xDD);
        _386.x86_opcodes_de_a16 = attente(_386.x86_opcodes_de_a16!, _386.ops_fpu_de_a16, 0xDE);
        _386.x86_opcodes_de_a32 = attente(_386.x86_opcodes_de_a32!, _386.ops_fpu_de_a32, 0xDE);
        _386.x86_opcodes_df_a16 = attente(_386.x86_opcodes_df_a16!, _386.ops_fpu_df_a16, 0xDF);
        _386.x86_opcodes_df_a32 = attente(_386.x86_opcodes_df_a32!, _386.ops_fpu_df_a32, 0xDF);
    }

    private static OpFn[] attente(OpFn[] posee, OpFn[] pcem, int esc)
    {
        var t = copie(posee, pcem);
        for (var i = 0; i < t.Length; i++)
                if (t[i] is { } h && !sans_attente(esc, i))
                        t[i] = attend(h);
        return t;
    }

    // pcem bug, fixed in hardware mode: PB-204 — les instructions qui n'attendent pas : FNCLEX, FNINIT, FSETPM, FNSTCW,
    //   FNSTSW, FNSAVE et FNSTENV, que le 286 lance sans tester ERROR# (286 PRM, #MF), que le verrou de l'AT laisse
    //   passer (FINIT, FSETPM et FCLEX : 387 PRM, annexe F ; FNSTSW, FNSTSW AX, FNSTENV et FNSAVE : 287 PRM p. 1-10 ;
    //   FNSTCW : déduit), et les formes « no-wait » du 486 (486 PRM, table 17-8). L'indice est l'octet ModRM (D9, DB,
    //   DD, DF) ; D8 et DC, indexées par mod et reg, n'ont que des instructions qui attendent.
    private static bool sans_attente(int esc, int i) => esc switch
    {
        0xD9 or 0xDD => i < 0xC0 && ((i >> 3) & 7) is 6 or 7,
        0xDB => i is 0xE2 or 0xE3 or 0xE4,
        0xDF => i == 0xE0,
        _ => false,
    };

    // FP_ENTER d'abord : #NM (EM ou TS) précède l'attente, et le gestionnaire de PCem le lève.
    private static OpFn attend(OpFn pcem) => fetchdat => (cr0 & 0xc) == 0 && bloque() ? 1 : pcem(fetchdat);

    // pcem bug, fixed in hardware mode: PB-204 — ni WAIT ni une instruction x87 n'attendent rien en mode PCem. Sur le
    //   286 et le 386 de l'AT, le verrou tient BUSY# : le processeur s'arrête devant l'instruction, une interruption
    //   peut le prendre, et il la reprend (286 HRM, « Execution of ESC Instructions »). Sur le 486, une exception en
    //   attente lève #MF si NE est posé ; sinon le processeur s'arrête, sauf sous IGNNE# (486 PRM § 16.2.1.2). L'arrêt
    //   compte cent cycles par tour, comme HLT : le pas auquel une interruption peut le prendre (déduit).
    internal static bool bloque()
    {
        if (cpu_c.fpu_type == cpu_c.FPU_BUILTIN)
        {
                if ((cpu_state.npxs & ES) == 0)
                        return false;
                if ((cr0 & 0x20) != 0)
                {
                        ModeMateriel.Sonde[59]++;
                        x86_int(16);
                        return true;
                }
                if (ignne)
                        return false;
        }
        else if (!verrou)
                return false;
        ModeMateriel.Sonde[204]++;
        cpu_state.pc = cpu_state.oldpc;
        _386.CLOCK_CYCLES(100);
        return true;
    }

    // pcem bug, fixed in hardware mode: PB-204 — les ports F0h et F1h de l'AT (AP-578 § 2.2.1 ; IBM PC AT Technical
    //   Reference, 1985) : une écriture à F0h efface le verrou (le 486 : pose IGNNE# tant que FERR# est actif) ; à F1h,
    //   elle efface aussi le verrou et remet à zéro le 287 ou le 387, dans l'état de FNINIT (opFINIT). Le 486 et F1h :
    //   inconnu, rien.
    internal static void ports_at() => io.io_sethandler(0x00f0, 0x0002, null, null, null, ecrire_f0_f1, null, null, null);

    internal static void ecrire_f0_f1(uint16_t port, uint8_t val, object p)
    {
        ModeMateriel.Sonde[204]++;
        verrou = false;
        if (cpu_c.fpu_type == cpu_c.FPU_BUILTIN)
        {
                if (port == 0xF0)
                        ignne = (cpu_state.npxs & ES) != 0;
                return;
        }
        if (port == 0xF1 && cpu_c.fpu_type is cpu_c.FPU_287 or cpu_c.FPU_287XL or cpu_c.FPU_387)
        {
                cpu_state.npxc = 0x37f;
                cpu_state.npxs = 0;
                Array.Clear(cpu_state.tag);
                cpu_state.TOP = 0;
                cpu_state.ismmx = 0;
        }
    }
}
