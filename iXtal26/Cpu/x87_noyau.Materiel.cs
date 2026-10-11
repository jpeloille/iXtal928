// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le noyau de 80 bits du x87, sa couche commune (G13.6, le noyau ; PLAN-G13.md ; R10 de
//         TRANSCRIPTION.md).
// STATUS: materiel
//
// Le mode PCem calcule en `double` (PCem, l'oracle). Le noyau calcule comme le silicium : huit registres de 80 bits et
// SoftFloat 3e (iXtal26.SoftFloat/, réécrit et vérifié au point de décision n° 10). Ce fichier porte ce que les
// gestionnaires du noyau partagent, ceux du 286, du 386 et du 486 comme ceux du 8087 : l'état, le RESET et FNINIT par
// type de coprocesseur, la classe d'un opérande, la pile et son débordement, les exceptions et leur réponse masquée,
// le mot de contrôle vers SoftFloat et ses indicateurs vers le mot d'état. Les gestionnaires eux-mêmes sont générés
// d'une seule description (tools/x87gen/genmat.py : x87_noyau_386.Materiel.cs, x87_noyau_808x.Materiel.cs).
//
// EN CHANTIER (ModeMateriel.EnChantier) : le noyau ne se demande que par les numéros de son groupe, jusqu'à la fin de
// N7 ; ni « tout » ni le domaine x87 ne le prennent. Ses tables remplacent alors toutes celles du x87 : chaque
// instruction déjà écrite y a son gestionnaire, les autres s'arrêtent bruyamment (a_ecrire), au lieu de calculer en
// `double` sur des registres que le noyau ne lit pas. Demandé avec une correction de G13.6a ou 6b (PB-57, 58, 61, 63,
// 64, 66, 67, 70, 207, 213), le noyau la remplace : leurs gestionnaires calculent en `double`, les siens sur 80 bits,
// et ils disparaîtront en N8. L'acheminement de G13.6c (PB-59, 69, 204), lui, reste celui de la carte : le noyau ne
// le prend que s'il est demandé (lever, int_retombe), et PB-204 enveloppe ses tables comme celles du mode.
//
// Le contrat de la couche : une fonction qui peut lever une exception rend VRAI quand l'instruction doit s'arrêter
// (une exception démasquée), faux quand elle continue, réponse masquée comprise.
//
// Le mot de contrôle, le mot d'état et TOP restent ceux de cpu_state (npxc, npxs, TOP), que le mode PCem tient aussi :
// FNSTSW et FNSTCW les lisent. Les registres, les étiquettes et les pointeurs sont ici ; cpu_state.ST et cpu_state.tag
// ne servent plus.

using iXtal26.SoftFloat;
using static iXtal26.Cpu._386_common;

namespace iXtal26.Cpu;

internal static class _x87_noyau
{
    // ===== L'état =====

    /// <summary>Les huit registres physiques, de 80 bits ; ST(i) est R[(TOP + i) &amp; 7].</summary>
    internal static readonly extFloat80_t[] R = new extFloat80_t[8];

    /// <summary>Le mot d'étiquettes de l'architecture : deux bits par registre physique (00 valide, 01 zéro, 10
    /// spécial, 11 vide).</summary>
    internal static uint16_t etiquettes = 0xFFFF;

    /// <summary>Les pointeurs de la dernière instruction (FIP, FCS, FOP) et de son opérande (FDP, FDS) ; N3 les
    /// tient.</summary>
    internal static uint32_t fip, fdp;
    internal static uint16_t fcs, fds, fop;

    internal const int VALIDE = 0, ZERO = 1, SPECIAL = 2, VIDE = 3;

    internal const uint16_t IE = 0x01, DE = 0x02, ZE = 0x04, OE = 0x08, UE = 0x10, PE = 0x20, SF = 0x40, ES = 0x80;
    internal const uint16_t C1 = 0x200, B = 0x8000;

    /// <summary>L'indéfini réel : FFFF C000000000000000h, un QNaN négatif (387 PRM § 3.1.5 et table 3-9 ; SDM vol. 1 § 4.8.3.7).
    /// La réponse masquée d'une opération invalide qui rend un réel.</summary>
    internal static readonly extFloat80_t Indefini = new() { signExp = 0xFFFF, signif = 0xC000000000000000 };

    internal static int phys(int i) => (cpu_state.TOP + i) & 7;

    internal static int etiquette(int p) => (etiquettes >> (p * 2)) & 3;

    internal static void etiqueter(int p, int e) =>
        etiquettes = (uint16_t)((etiquettes & ~(3 << (p * 2))) | (e << (p * 2)));

    internal static bool vide(int i) => etiquette(phys(i)) == VIDE;

    /// <summary>L'étiquette d'un contenu, comme le 387 et le 486 la calculent : 01 pour un zéro, 10 pour un NaN, un
    /// infini, un dénormal et les formats non supportés, 00 sinon. Celle du 8087 et du 287 pour un non normal : N7.</summary>
    internal static int etiquette_de(in extFloat80_t v) => classe(v) switch
    {
        Classe.Zero => ZERO,
        Classe.Normal => VALIDE,
        _ => SPECIAL,
    };

    // ===== La classe d'un opérande =====

    /// <summary>Les classes du format de 80 bits (387 PRM § 3.1 et table 3-9 ; SDM vol. 1 § 8.2.2). Le bit
    /// entier J est explicite : un exposant nul avec J = 1 est un pseudo-dénormal ; un exposant ni nul ni maximal avec
    /// J = 0, un non normal (le pseudo-zéro compris) ; l'exposant maximal avec J = 0, un pseudo-infini ou un
    /// pseudo-NaN.</summary>
    internal enum Classe : byte
    {
        Zero, Normal, Denormal, PseudoDenormal, NonNormal, PseudoInfini, PseudoNaN, Infini, QNaN, SNaN,
    }

    internal static Classe classe(in extFloat80_t v)
    {
        var exp = v.signExp & 0x7FFF;
        var j = (v.signif >> 63) != 0;
        var frac = v.signif & 0x7FFFFFFFFFFFFFFF;
        if (exp == 0)
                return v.signif == 0 ? Classe.Zero : j ? Classe.PseudoDenormal : Classe.Denormal;
        if (exp == 0x7FFF)
        {
                if (!j)
                        return frac == 0 ? Classe.PseudoInfini : Classe.PseudoNaN;
                if (frac == 0)
                        return Classe.Infini;
                return (frac >> 62) != 0 ? Classe.QNaN : Classe.SNaN;
        }
        return j ? Classe.Normal : Classe.NonNormal;
    }

    /// <summary>Un format que le 387 et le 486 ne supportent plus (387 PRM § 3.1.7 : « pseudozeros, pseudo-NaNs,
    /// pseudoinfinities, and unnormal numbers ») : un opérande invalide (IE). Le pseudo-dénormal reste un opérande,
    /// dénormal (DE). Le 287XL, un cœur de 387 : déduit. Le 8087 et le 287 calculent sur un non normal : N7.</summary>
    internal static bool non_supporte(Classe c) =>
        cpu_c.fpu_type >= cpu_c.FPU_287XL && c is Classe.NonNormal or Classe.PseudoInfini or Classe.PseudoNaN;

    // ===== Les exceptions =====

    /// <summary>Vrai si toutes les exceptions de `e` sont masquées.</summary>
    internal static bool masquee(uint16_t e) => (cpu_state.npxc & e & 0x3F) == (e & 0x3F);

    /// <summary>Pose les drapeaux d'exception `e` (IE à PE). Masquées, l'instruction continue avec sa réponse masquée et
    /// rend faux. Démasquée l'une d'elles, ES et B sont posés (387 PRM, mot d'état ; 287 PRM p. 1-10) et l'instruction
    /// s'arrête : vrai (ce qu'elle laisse, instruction par instruction : N5). L'exception ne part vers la carte (la NMI
    /// du 8087, IRQ13 et le verrou de l'AT, #MF du 486 : x87.Materiel.cs, exception_demasquee) que si l'acheminement est
    /// demandé, PB-59 et son groupe.</summary>
    internal static bool lever(uint16_t e)
    {
        cpu_state.npxs |= e;
        if (masquee(e))
                return false;
        if (materiel.pb_59)
                _x87_materiel.exception_demasquee();
        else
                cpu_state.npxs |= ES | B;
        return true;
    }

    /// <summary>FNINIT et FNCLEX du 8087 effacent IR : sa sortie INT retombe, si l'acheminement de PB-69 est demandé
    /// (x87.Materiel.cs, int_8087_retombe).</summary>
    internal static void int_retombe()
    {
        if (materiel.pb_69)
                _x87_materiel.int_8087_retombe();
    }

    // ===== La pile =====

    // pcem bug, fixed in hardware mode: PB-264 — empiler sur un registre plein ou lire un registre vide est une faute de
    //   pile (SDM vol. 1 § 8.5.1.1, « #IS » ; 387 PRM) : IE ; sur le 387 et le 486, SF, et C1 à 1 pour un débordement, à
    //   0 pour un sous-débordement (le 287XL, un cœur de 387 : déduit). Le 8087 et le 287 n'ont pas SF (287 PRM ;
    //   Numerics Supplement) : IE seul, C1 laissé. Rend vrai si IE est démasquée : l'instruction s'arrête. C1 posé ici
    //   l'est en dernier : une instruction qui calcule puis empile (FPTAN, FXTRACT, FSINCOS) appelle indicateurs avant.
    internal static bool faute_de_pile(bool debordement)
    {
        ModeMateriel.Sonde[264]++;
        if (cpu_c.fpu_type >= cpu_c.FPU_287XL)
                cpu_state.npxs = (uint16_t)((cpu_state.npxs & ~C1) | SF | (debordement ? C1 : 0));
        return lever(IE);
    }

    // pcem bug, fixed in hardware mode: PB-264 — empiler sur ST(7) plein : la faute de pile ; masquée, TOP descend et
    //   l'indéfini est rangé (SDM vol. 1 § 8.5.1.1). Rend vrai si l'instruction s'arrête (IE démasquée), sans rien
    //   empiler.
    internal static bool empiler(extFloat80_t v)
    {
        var p = (cpu_state.TOP - 1) & 7;
        if (etiquette(p) != VIDE)
        {
                if (faute_de_pile(true))
                        return true;
                v = Indefini;
        }
        cpu_state.TOP = p;
        R[p] = v;
        etiqueter(p, etiquette_de(v));
        return false;
    }

    /// <summary>Dépile : ST(0) vide, TOP monte. Dépiler un registre vide n'est pas une faute ; l'avoir lu l'était.</summary>
    internal static void depiler()
    {
        etiqueter(cpu_state.TOP & 7, VIDE);
        cpu_state.TOP = (cpu_state.TOP + 1) & 7;
    }

    // pcem bug, fixed in hardware mode: PB-264 — lire ST(i) vide : la faute de pile ; masquée, l'opérande devient
    //   l'indéfini, un QNaN, qui donne à chaque instruction sa réponse masquée (l'indéfini rangé, non ordonné pour une
    //   comparaison). Rend vrai si l'instruction s'arrête (IE démasquée).
    internal static bool lire(int i, out extFloat80_t v)
    {
        if (!vide(i))
        {
                v = R[phys(i)];
                return false;
        }
        v = Indefini;
        return faute_de_pile(false);
    }

    /// <summary>Range `v` dans ST(i) et son étiquette.</summary>
    internal static void ecrire(int i, in extFloat80_t v)
    {
        var p = phys(i);
        R[p] = v;
        etiqueter(p, etiquette_de(v));
    }

    // ===== Le mot de contrôle vers SoftFloat, ses indicateurs vers le mot d'état =====

    /// <summary>Avant un calcul : RC vers le mode d'arrondi de SoftFloat ; PC vers sa précision, si `selon_pc` (387 PRM
    /// § 2.2.4 ; SDM vol. 1, 2003, § 8.1.4.2 : FADD, FSUB, FSUBR, FMUL, FDIV, FDIVR, leurs formes entières et FSQRT ;
    /// les autres arrondissent à 64 bits de significande) ; les indicateurs et l'arrondi vers le haut à zéro. PC = 01
    /// est réservé : 64 bits de significande, comme 11 (inconnu).</summary>
    internal static void arrondi(bool selon_pc)
    {
        softfloat.softfloat_roundingMode = ((cpu_state.npxc >> 10) & 3) switch
        {
                0 => softfloat.softfloat_round_near_even,
                1 => softfloat.softfloat_round_min,
                2 => softfloat.softfloat_round_max,
                _ => softfloat.softfloat_round_minMag,
        };
        softfloat.extF80_roundingPrecision = !selon_pc ? (byte)80 : ((cpu_state.npxc >> 8) & 3) switch
        {
                0 => (byte)32,
                2 => (byte)64,
                _ => (byte)80,
        };
        softfloat.softfloat_exceptionFlags = 0;
        softfloat.softfloat_roundedUp = false;
    }

    /// <summary>Après un calcul : les indicateurs de SoftFloat vers le mot d'état (inexact → PE, sous-dépassement → UE,
    /// dépassement → OE, infini → ZE, invalide → IE) ; C1, l'arrondi vers le haut, sur le 387 et le 486 (et le 287XL :
    /// déduit ; 8087 et 287 : indéfini, laissé). DE vient de la classe des opérandes, que SoftFloat ne signale pas. Rend
    /// vrai si une exception démasquée arrête l'instruction : IE, ZE, OE ou UE ; une PE démasquée laisse ranger le
    /// résultat arrondi, puis lève (387 PRM § 3.2.6 : « delivers the rounded [...] result to the destination, regardless
    /// of whether a trap occurs ») : faux. OE et UE démasquées, l'exposant décalé, et UE démasquée sur un résultat
    /// petit mais exact : N5. La petitesse se juge après l'arrondi, comme SoftFloat le fait pour le 8086 (SDM vol. 1,
    /// 2003, § 4.9.1.5 : « whenever the rounded result of an instruction is tiny »). Après une faute de pile masquée,
    /// l'opérande est l'indéfini, un QNaN : le calcul est exact et C1 retombe à 0, comme la faute le veut d'un
    /// sous-débordement ; un débordement de pile pose son C1 après (faute_de_pile).</summary>
    internal static bool indicateurs()
    {
        var f = softfloat.softfloat_exceptionFlags;
        uint16_t e = 0;
        if ((f & softfloat.softfloat_flag_invalid) != 0)
                e |= IE;
        if ((f & softfloat.softfloat_flag_infinite) != 0)
                e |= ZE;
        if ((f & softfloat.softfloat_flag_overflow) != 0)
                e |= OE;
        if ((f & softfloat.softfloat_flag_underflow) != 0)
                e |= UE;
        if ((f & softfloat.softfloat_flag_inexact) != 0)
                e |= PE;
        if (cpu_c.fpu_type >= cpu_c.FPU_287XL)
                cpu_state.npxs = (uint16_t)((cpu_state.npxs & ~C1) | (softfloat.softfloat_roundedUp ? C1 : 0));
        if (e == 0 || !lever(e))
                return false;
        return (e & ~PE & ~cpu_state.npxc & 0x3F) != 0;
    }

    // ===== Les mouvements de données (N3a) =====
    //
    // Chaque fonction est le cœur d'une instruction : le gestionnaire généré lit ou écrit la mémoire, elle fait le reste.
    // Les exceptions de chacune, celles du 387 PRM, annexe B : FLD m32 et m64, IS, I et D ; FLD m80 et de registre,
    // FILD, FBLD, les constantes, FXCH, FCHS, FABS, IS ; FST m32 et m64, IS, I, D, O, U et P ; FST m80 et de registre,
    // IS ; FIST et FBSTP, IS, I et P ; FXAM, aucune. Une exception qui précède le calcul (IS, I, D), démasquée, arrête
    // l'instruction avant qu'elle range quoi que ce soit.

    /// <summary>C1 à zéro (387 et 486 ; le 287XL : déduit), au début d'une instruction dont C1 vaut « zéro, ou O/U# »
    /// (486 PRM, table 15-1 : FLD, FILD, FBLD, les constantes, FXCH, FCHS, FABS, FSTP m80) ; une faute de pile le pose
    /// ensuite. Le 8087 et le 287 : indéfini, laissé.</summary>
    internal static void c1_zero()
    {
        if (cpu_c.fpu_type >= cpu_c.FPU_287XL)
                cpu_state.npxs &= unchecked((uint16_t)~C1);
    }

    /// <summary>Vrai si l'empilement suivant déborderait : ST(7) n'est pas vide.</summary>
    internal static bool plein() => etiquette((cpu_state.TOP - 1) & 7) != VIDE;

    // pcem bug, fixed in hardware mode: PB-206 — FLD m32 : un SNaN lève IE et se charge silencieux (SDM vol. 2, FLD,
    //   « #IA Source operand is an SNaN ») ; un dénormal lève DE et se charge normalisé, exact. Le 8087 et le 287 ne
    //   lèvent rien sur un SNaN (SDM vol. 3, 2016, § 22.18.7.11) ; la valeur qu'ils chargent est inconnue : ses bits,
    //   étendus sans le rendre silencieux. Leurs dénormaux chargés en non normaux : N7.
    internal static void fld_f32(uint32_t v)
    {
        c1_zero();
        if (plein())
        {
                empiler(Indefini);
                return;
        }
        var nan = (v & 0x7F800000) == 0x7F800000 && (v & 0x007FFFFF) != 0;
        if (nan && cpu_c.fpu_type < cpu_c.FPU_287XL)
        {
                empiler(new extFloat80_t
                {
                        signExp = (uint16_t)((v >> 16 & 0x8000) | 0x7FFF),
                        signif = 0x8000000000000000 | (uint64_t)(v & 0x007FFFFF) << 40,
                });
                return;
        }
        softfloat.softfloat_exceptionFlags = 0;
        var r = softfloat.f32_to_extF80(new float32_t { v = v });
        uint16_t e = 0;
        if ((softfloat.softfloat_exceptionFlags & softfloat.softfloat_flag_invalid) != 0)
        {
                e |= IE;
                ModeMateriel.Sonde[206]++;
        }
        if ((v & 0x7F800000) == 0 && (v & 0x007FFFFF) != 0)
                e |= DE;
        if (e != 0 && lever(e))
                return;
        empiler(r);
    }

    // pcem bug, fixed in hardware mode: PB-212 — FLD m64 : un SNaN lève IE et se charge silencieux (SDM vol. 2, FLD) ;
    //   un dénormal lève DE. Le 8087 et le 287 : comme FLD m32 (PB-206).
    internal static void fld_f64(uint64_t v)
    {
        c1_zero();
        if (plein())
        {
                empiler(Indefini);
                return;
        }
        var nan = (v & 0x7FF0000000000000) == 0x7FF0000000000000 && (v & 0x000FFFFFFFFFFFFF) != 0;
        if (nan && cpu_c.fpu_type < cpu_c.FPU_287XL)
        {
                empiler(new extFloat80_t
                {
                        signExp = (uint16_t)((v >> 48 & 0x8000) | 0x7FFF),
                        signif = 0x8000000000000000 | (v & 0x000FFFFFFFFFFFFF) << 11,
                });
                return;
        }
        softfloat.softfloat_exceptionFlags = 0;
        var r = softfloat.f64_to_extF80(new float64_t { v = v });
        uint16_t e = 0;
        if ((softfloat.softfloat_exceptionFlags & softfloat.softfloat_flag_invalid) != 0)
        {
                e |= IE;
                ModeMateriel.Sonde[212]++;
        }
        if ((v & 0x7FF0000000000000) == 0 && (v & 0x000FFFFFFFFFFFFF) != 0)
                e |= DE;
        if (e != 0 && lever(e))
                return;
        empiler(r);
    }

    // pcem bug, fixed in hardware mode: PB-55 — FLD m80 : les 80 bits tels quels, sans conversion ni autre exception que
    //   la faute de pile (SDM vol. 2, FLD ; 387 PRM, annexe B) ; un format non supporté se charge, et lève IE quand une opération le lit.
    internal static void fld_f80(uint64_t signif, uint16_t signExp)
    {
        ModeMateriel.Sonde[55]++;
        c1_zero();
        empiler(new extFloat80_t { signExp = signExp, signif = signif });
    }

    /// <summary>FILD m16, m32 et m64 : l'entier, exact sur 64 bits de significande.</summary>
    internal static void fild(int64_t v)
    {
        c1_zero();
        empiler(softfloat.i64_to_extF80(v));
    }

    // pcem bug, fixed in hardware mode: PB-52 — FBLD : dix-huit chiffres décimaux et un signe, convertis exactement et
    //   empilés, −0 gardé (SDM vol. 2, FBLD ; 387 PRM § 4.3.8). Un chiffre de A à F : « undefined » ; il compte ici pour sa
    //   valeur (inconnu).
    internal static void fbld(uint64_t bas, uint16_t haut)
    {
        ModeMateriel.Sonde[52]++;
        c1_zero();
        uint64_t n = 0;
        n = n * 10 + (uint64_t)((haut >> 4) & 0xF);
        n = n * 10 + (uint64_t)(haut & 0xF);
        for (var k = 15; k >= 0; k--)
                n = n * 10 + ((bas >> (4 * k)) & 0xF);
        var r = softfloat.i64_to_extF80((int64_t)n);
        if ((haut & 0x8000) != 0)
                r.signExp |= 0x8000;
        empiler(r);
    }

    // PB-66 (corrigé en G13.6a, en double) : les constantes de 80 bits. Le 287XL, le 387 et le 486 arrondissent la
    //   valeur exacte selon RC (387 PRM § 4.7 ; SDM vol. 1, 2003, § 8.3.4 ; § 8.3.8 donne les 66 bits de π) ; le 8087 et le 287 la rendent au plus près.
    //   Les significandes, calculés de la valeur exacte à deux cents chiffres : au plus près, vers le bas (vers −∞ et
    //   vers zéro, les constantes étant positives), vers le haut.
    private static readonly (uint16_t se, uint64_t pres, uint64_t bas, uint64_t haut)[] Constantes =
    [
        (0x3FFF, 0x8000000000000000, 0x8000000000000000, 0x8000000000000000),   // 1
        (0x4000, 0xD49A784BCD1B8AFE, 0xD49A784BCD1B8AFE, 0xD49A784BCD1B8AFF),   // log₂ 10
        (0x3FFF, 0xB8AA3B295C17F0BC, 0xB8AA3B295C17F0BB, 0xB8AA3B295C17F0BC),   // log₂ e
        (0x4000, 0xC90FDAA22168C235, 0xC90FDAA22168C234, 0xC90FDAA22168C235),   // π
        (0x3FFD, 0x9A209A84FBCFF799, 0x9A209A84FBCFF798, 0x9A209A84FBCFF799),   // log₁₀ 2
        (0x3FFE, 0xB17217F7D1CF79AC, 0xB17217F7D1CF79AB, 0xB17217F7D1CF79AC),   // ln 2
        (0x0000, 0x0000000000000000, 0x0000000000000000, 0x0000000000000000),   // +0
    ];

    /// <summary>D9 E8 + k : FLD1, FLDL2T, FLDL2E, FLDPI, FLDLG2, FLDLN2, FLDZ.</summary>
    internal static void constante(int k)
    {
        c1_zero();
        var c = Constantes[k];
        var sig = cpu_c.fpu_type < cpu_c.FPU_287XL ? c.pres : ((cpu_state.npxc >> 10) & 3) switch
        {
                1 or 3 => c.bas,
                2 => c.haut,
                _ => c.pres,
        };
        empiler(new extFloat80_t { signExp = c.se, signif = sig });
    }

    /// <summary>FLD ST(i) : lire ST(i), puis l'empiler (SDM vol. 2, FLD) ; vide, l'indéfini (masquée).</summary>
    internal static void fld_st(int i)
    {
        c1_zero();
        if (lire(i, out var v))
                return;
        empiler(v);
    }

    /// <summary>FXCH ST(i) : un registre vide est une faute de pile ; masquée, chaque registre vide reçoit l'indéfini,
    /// puis l'échange (387 PRM, table 3-10 : « Change empty registers to the QNaN indefinite and then perform
    /// exchange »).</summary>
    internal static void fxch(int i)
    {
        c1_zero();
        if (vide(0) || vide(i))
        {
                if (faute_de_pile(false))
                        return;
                if (vide(0))
                        ecrire(0, Indefini);
                if (vide(i))
                        ecrire(i, Indefini);
        }
        int p0 = phys(0), pi = phys(i);
        (R[p0], R[pi]) = (R[pi], R[p0]);
        int e0 = etiquette(p0), ei = etiquette(pi);
        etiqueter(p0, ei);
        etiqueter(pi, e0);
    }

    /// <summary>FST et FSTP ST(i) : ST(0) copié tel quel, 80 bits (C1 à zéro : rien n'est arrondi) ; vide, l'indéfini.
    /// Rend vrai si l'instruction s'arrête (le gestionnaire ne dépile pas).</summary>
    internal static bool fst_st(int i)
    {
        c1_zero();
        if (lire(0, out var v))
                return true;
        ecrire(i, v);
        return false;
    }

    /// <summary>FCHS et FABS : le signe de ST(0) inversé, ou effacé, sans autre exception que la pile ; vide, masquée,
    /// l'indéfini.</summary>
    internal static void signe(bool abs)
    {
        c1_zero();
        if (vide(0))
        {
                if (!faute_de_pile(false))
                        ecrire(0, Indefini);
                return;
        }
        var p = phys(0);
        R[p].signExp = (uint16_t)(abs ? R[p].signExp & 0x7FFF : R[p].signExp ^ 0x8000);
    }

    // PB-63 (corrigé en G13.6a, en double) : FXAM sur 80 bits (387 PRM § 4.5.10 et table 4-8 ; SDM vol. 2, FXAM) : non
    //   supporté C3 C2 C0 = 000, NaN 001, normal 010, infini 011, zéro 100, vide 101, dénormal 110 (le pseudo-dénormal
    //   compris) ; C1, le signe du registre, vide compris. Les seize codes du 8087 et du 287 (le non normal, ses signes) :
    //   N7 ; ceux qu'il partage avec le 387 sont les mêmes.
    internal static void fxam()
    {
        var v = R[phys(0)];
        uint16_t c;
        if (vide(0))
                c = x87_c.C3 | x87_c.C0;
        else
                c = classe(v) switch
                {
                        Classe.Zero => x87_c.C3,
                        Classe.Normal => x87_c.C2,
                        Classe.Denormal or Classe.PseudoDenormal => (uint16_t)(x87_c.C3 | x87_c.C2),
                        Classe.Infini => (uint16_t)(x87_c.C2 | x87_c.C0),
                        Classe.QNaN or Classe.SNaN => x87_c.C0,
                        _ => 0,
                };
        if ((v.signExp & 0x8000) != 0)
                c |= x87_c.C1;
        cpu_state.npxs = (uint16_t)((cpu_state.npxs & ~(x87_c.C0 | x87_c.C1 | x87_c.C2 | x87_c.C3)) | c);
    }

    /// <summary>Lit ST(0) pour le ranger en mémoire : vrai si l'instruction s'arrête (pile vide, IE démasquée) ;
    /// masquée, l'indéfini. Un format non supporté (387, 486) lève IE et rend l'indéfini : `nonSupporte`.</summary>
    private static bool source(out extFloat80_t v, out bool nonSupporte)
    {
        nonSupporte = false;
        if (lire(0, out v))
                return true;
        if (non_supporte(classe(v)))
        {
                nonSupporte = true;
                if (lever(IE))
                        return true;
                v = Indefini;
        }
        return false;
    }

    /// <summary>FST et FSTP m32 et m64 : arrondis selon RC (PC n'y joue pas), DE pour une source dénormale, puis O, U, P
    /// et C1 (l'arrondi vers le haut) ; un SNaN, IE et le QNaN. Rend vrai si l'instruction s'arrête, sans rien
    /// ranger.</summary>
    internal static bool fst_f32(out uint32_t r)
    {
        r = 0;
        if (source(out var v, out var ns))
                return true;
        if (classe(v) is Classe.Denormal or Classe.PseudoDenormal && lever(DE))
                return true;
        arrondi(false);
        r = softfloat.extF80_to_f32(v).v;
        if (ns)
                softfloat.softfloat_exceptionFlags = 0;
        return indicateurs();
    }

    internal static bool fst_f64(out uint64_t r)
    {
        r = 0;
        if (source(out var v, out var ns))
                return true;
        if (classe(v) is Classe.Denormal or Classe.PseudoDenormal && lever(DE))
                return true;
        arrondi(false);
        r = softfloat.extF80_to_f64(v).v;
        if (ns)
                softfloat.softfloat_exceptionFlags = 0;
        return indicateurs();
    }

    // pcem bug, fixed in hardware mode: PB-56 — FSTP m80 : les 80 bits tels quels (SDM vol. 2, FST ; 387 PRM, annexe B :
    //   IS seule) ; vide, l'indéfini.
    internal static bool fst_f80(out extFloat80_t r)
    {
        ModeMateriel.Sonde[56]++;
        c1_zero();
        return lire(0, out r);
    }

    // pcem bug, fixed in hardware mode: PB-196 — FIST et FISTP : arrondi selon RC ; hors des bornes de la destination, un
    //   NaN ou un infini : IE, et masquée l'indéfini entier, 8000h, 80000000h ou 8000000000000000h (SDM vol. 2,
    //   FIST/FISTP ; 387 PRM § 3.1.4, table 3-6) ; inexact, PE et C1 (l'arrondi vers le haut). `bits` : 16, 32 ou 64.
    //   Le 8087 et le 287 rangent l'indéfini, IE, pour un dénormal ou un non normal (287 PRM, table 1-17 ; 387 PRM,
    //   annexe C) : N7.
    internal static bool fist(int bits, out int64_t r)
    {
        r = 0;
        if (source(out var v, out _))
                return true;
        arrondi(false);
        var mode = softfloat.softfloat_roundingMode;
        if (bits == 64)
                r = softfloat.extF80_to_i64(v, mode, true);
        else
        {
                r = softfloat.extF80_to_i32(v, mode, true);
                var max = bits == 16 ? 0x7FFF : 0x7FFFFFFF;
                if ((softfloat.softfloat_exceptionFlags & softfloat.softfloat_flag_invalid) != 0 || r > max || r < -max - 1)
                {
                        softfloat.softfloat_exceptionFlags = softfloat.softfloat_flag_invalid;
                        softfloat.softfloat_roundedUp = false;
                        r = -max - 1;
                        ModeMateriel.Sonde[196]++;
                }
        }
        return indicateurs();
    }

    // pcem bug, fixed in hardware mode: PB-195 — FBSTP : ST(0) arrondi à l'entier selon RC (PE, C1), puis dix-huit
    //   chiffres et le signe ; au-delà de 10^18 − 1, un NaN ou un infini : IE, et masquée l'indéfini BCD, FFFF C000 0000
    //   0000 0000h (SDM vol. 1, 2003, § 4.7 ; le 387 PRM, table 3-7, n'en fixe que les deux octets hauts). Un zéro garde
    //   son signe (déduit, comme FIST : 387 PRM § 4.3.6). Le 8087 et le 287, sur un dénormal : comme FIST, N7. Rend vrai
    //   si l'instruction s'arrête, sans rien ranger.
    internal static bool fbstp(out uint64_t bas, out uint16_t haut)
    {
        bas = 0xC000000000000000;
        haut = 0xFFFF;
        ModeMateriel.Sonde[195]++;
        if (source(out var v, out var ns))
                return true;
        arrondi(false);
        var c = classe(v);
        if (ns || c is Classe.QNaN or Classe.SNaN or Classe.Infini)
        {
                softfloat.softfloat_exceptionFlags = ns ? (byte)0 : softfloat.softfloat_flag_invalid;
                return indicateurs();
        }
        var z = softfloat.extF80_roundToInt(v, softfloat.softfloat_roundingMode, true);
        var exp = z.signExp & 0x7FFF;
        uint64_t n = exp < 0x3FFF ? 0 : exp > 0x3FFF + 63 ? uint64_t.MaxValue : z.signif >> (0x3FFF + 63 - exp);
        if (n > 999999999999999999)
        {
                softfloat.softfloat_exceptionFlags = softfloat.softfloat_flag_invalid;
                softfloat.softfloat_roundedUp = false;
                return indicateurs();
        }
        if (indicateurs())
                return true;
        bas = 0;
        for (var k = 0; k < 16; k++, n /= 10)
                bas |= (n % 10) << (4 * k);
        haut = (uint16_t)((n % 10) | (n / 10 % 10) << 4 | ((uint64_t)z.signExp & 0x8000));
        return false;
    }

    // ===== FNINIT, la mise sous tension, le RESET =====

    /// <summary>L'état de FNINIT (387 PRM, table 6-1 ; 287 PRM, table 3-1 ; 486 PRM, table 19-1) : le mot de contrôle
    /// 037Fh (03FFh sur le 8087, IEM posé, comme PCem), le mot d'état à zéro, TOP à zéro, les huit étiquettes vides ;
    /// les registres gardés ; les pointeurs gardés, sauf sur le 486, qui les efface. Le 287 : sa table 3-1 dit IEM à 1
    /// (03FFh), sa figure 1-5 fait du bit 7 un bit réservé ; la source se contredit, 037Fh, la valeur de PCem, est
    /// gardée (inconnu). C3-C0 du 8087 et du 287 (gardés par le silicium, PB-202) : N7.</summary>
    internal static void finit()
    {
        cpu_state.npxc = cpu_c.fpu_type == cpu_c.FPU_8087 ? (uint16_t)0x3FF : (uint16_t)0x37F;
        cpu_state.npxs = 0;
        cpu_state.TOP = 0;
        etiquettes = 0xFFFF;
        if (cpu_c.fpu_type == cpu_c.FPU_BUILTIN)
                fip = fdp = fcs = fds = fop = 0;
    }

    /// <summary>La mise sous tension (cpu_set) : des registres et des pointeurs à zéro, l'état de FNINIT. Le RESET qui
    /// suit (resetx86) donne à chaque type le sien. Le 486, que le RESET ne touche pas sans auto-test, garde celui-ci :
    /// un choix, son état n'étant pas défini (486 PRM, table 10-1, note 4 : « not initialized unless the Built-In
    /// Self-Test is invoked ») ; les valeurs sont celles de la table (037Fh, 0000h, FFFFh).</summary>
    internal static void mise_sous_tension()
    {
        Array.Clear(R);
        fip = fdp = fcs = fds = fop = 0;
        finit();
    }

    // pcem bug, fixed in hardware mode: PB-203 — au RESET, le coprocesseur prend l'état initial de son type. Le 8087,
    //   le 287 et le 287XL : celui de FNINIT (Numerics Supplement, table S-7 ; 287 PRM, table 3-1 ; le 287XL : inconnu,
    //   celui du 287 qu'il remplace). Le 387 : celui de FNINIT, IM à 0, IE, ES et B posés, ce qui active ERROR# et
    //   signale sa présence au 386 (387 PRM § 6.2.5 et annexe C, « RESET, FINIT, and ERROR# pin »). Le 486 : rien, sans
    //   l'auto-test que le RESET d'un PC ne demande pas (486 PRM § 19.2.3 : « When the Built-In Self-Test is not
    //   requested, a hardware RESET leaves the FPU state unchanged »).
    internal static void reset()
    {
        switch (cpu_c.fpu_type)
        {
        case cpu_c.FPU_8087:
        case cpu_c.FPU_287:
        case cpu_c.FPU_287XL:
                finit();
                break;
        case cpu_c.FPU_387:
                finit();
                cpu_state.npxc &= unchecked((uint16_t)~0x0001);   // IM
                cpu_state.npxs |= IE | ES | B;
                break;
        default:
                return;
        }
        ModeMateriel.Sonde[203]++;
    }

    // ===== Les tables du noyau =====

    /// <summary>Pose les tables du noyau (cpu_set, sous la garde de PB-264) : l'état de la mise sous tension, les seize
    /// tables du 286, du 386 et du 486, et, sur le 8087, ses huit tables, remplacées en place (execx86 les indexe en
    /// dur).</summary>
    internal static void poser()
    {
        mise_sous_tension();
        _386_noyau.poser_tables();
        if (cpu_c.fpu_type == cpu_c.FPU_8087)
                _808x_noyau.poser_tables();
    }

    /// <summary>Une table du noyau, de l'échappement `esc` : chaque entrée s'arrête (a_ecrire) jusqu'à ce que son
    /// instruction soit écrite.</summary>
    internal static OpFn[] table(int n, int esc)
    {
        var t = new OpFn[n];
        remplir(t, esc);
        return t;
    }

    internal static void remplir(OpFn[] t, int esc) => Array.Fill(t, a_ecrire(esc));

    /// <summary>Pose `f` aux entrées de mémoire de la ligne `reg` : dans une table de 256 (indexée par l'octet ModRM),
    /// les trois valeurs de mod autres que 3 ; dans une table de 32 (D8 et DC, indexées par mod et reg), les entrées
    /// reg, 8 + reg et 16 + reg.</summary>
    internal static void memoire(OpFn[] t, int reg, OpFn f)
    {
        if (t.Length == 32)
        {
                for (var mod = 0; mod < 3; mod++)
                        t[mod * 8 + reg] = f;
                return;
        }
        for (var i = 0; i < 0xC0; i++)
                if (((i >> 3) & 7) == reg)
                        t[i] = f;
    }

    /// <summary>Une instruction que le noyau n'a pas encore : l'arrêt, plutôt qu'un calcul faux (le noyau en
    /// chantier).</summary>
    private static OpFn a_ecrire(int esc) => fetchdat =>
    {
        pc.fatal($"le noyau du x87, en chantier : {esc:X2} {fetchdat & 0xff:X2} n'est pas encore écrite (IP " +
                 $"{cpu_state.oldpc:X4}).\n");
        return 1;
    };
}
