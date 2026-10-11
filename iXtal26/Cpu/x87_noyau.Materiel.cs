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
// sans rien ranger ni dépiler (une exception démasquée qui précède le calcul ; OE ou UE démasquée quand la destination
// est la mémoire), faux quand elle continue, réponse masquée comprise, après une OE ou une UE démasquée dont le
// résultat se range dans un registre, et après une PE démasquée, où qu'il se range (indicateurs).
//
// Le mot de contrôle, le mot d'état et TOP restent ceux de cpu_state (npxc, npxs, TOP), que le mode PCem tient aussi :
// FNSTSW et FNSTCW les lisent. Les registres, les étiquettes et les pointeurs sont ici ; cpu_state.ST et cpu_state.tag
// ne servent plus.

using System.Numerics;
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

    /// <summary>Les pointeurs de la dernière instruction qui n'est pas de contrôle (FIP, FCS, FOP) et de son opérande
    /// (FDP, FDS) : l'offset et le sélecteur, ou, relus d'une image du mode réel ou pris par le 8087, l'adresse de 20 ou
    /// 32 bits, le sélecteur à zéro (pointeurs, fldenv).</summary>
    internal static uint32_t fip, fdp;
    internal static uint16_t fcs, fds, fop;

    /// <summary>Le mode protégé du 287 et du 287XL, posé par FSETPM, effacé par le seul RESET (287 PRM, FSETPM) : il
    /// choisit la disposition de leurs images (disposition).</summary>
    internal static bool fsetpm;

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
    /// infini, un dénormal et les formats non supportés, 00 sinon. Le 8087 et le 287 étiquettent valides (00) le non
    /// normal et le pseudo-zéro (387 PRM, annexe C § C.2 : « The encoding for pseudozero and unnormal is "valid" » ;
    /// PB-208).</summary>
    internal static int etiquette_de(in extFloat80_t v) => classe(v) switch
    {
        Classe.Zero => ZERO,
        Classe.Normal => VALIDE,
        Classe.NonNormal when seize_bits() => VALIDE,
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
    /// dénormal (DE). Le 287XL, un cœur de 387 : déduit. Le 8087 et le 287 calculent sur un non normal (non_normal_seize).</summary>
    internal static bool non_supporte(Classe c) =>
        cpu_c.fpu_type >= cpu_c.FPU_287XL && c is Classe.NonNormal or Classe.PseudoInfini or Classe.PseudoNaN;

    /// <summary>Le 8087 et le 287, les coprocesseurs de 16 bits, d'avant la norme IEEE 754 (387 PRM, annexe C,
    /// « Compatibility between the 80387 and the 80287/8087 ») ; le 287XL, un cœur de 387, n'en est pas (déduit).</summary>
    internal static bool seize_bits() => cpu_c.fpu_type < cpu_c.FPU_287XL;

    /// <summary>Un NaN pour le 8087 et le 287 : l'exposant maximal et tout significande sauf 1,0…0, celui de l'infini
    /// (287 PRM, « NaN ») ; le pseudo-infini et le pseudo-NaN du 387 en sont.</summary>
    private static bool nan_seize(Classe c) => c is Classe.QNaN or Classe.SNaN or Classe.PseudoNaN or Classe.PseudoInfini;

    // ===== Les exceptions =====

    /// <summary>Vrai si toutes les exceptions de `e` sont masquées.</summary>
    internal static bool masquee(uint16_t e) => (cpu_state.npxc & e & 0x3F) == (e & 0x3F);

    /// <summary>Pose les drapeaux d'exception `e` (IE à PE). Masquées, l'instruction continue avec sa réponse masquée et
    /// rend faux. Démasquée l'une d'elles, ES et B sont posés (387 PRM, mot d'état ; 287 PRM p. 1-10) : vrai. Ce que
    /// l'instruction laisse (N5) : avant le calcul (IS, I, D, Z), rien, ni la destination ni TOP (SDM vol. 1, 2003, §
    /// 8.5.1 à 8.5.3) ; après (O, U, P), le résultat rangé, voir indicateurs. L'exception ne part vers la carte (la NMI
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
        softfloat.softfloat_unboundedExp = (byte)(((cpu_state.npxc & OE) == 0 ? softfloat.softfloat_flag_overflow : 0) |
                                                  ((cpu_state.npxc & UE) == 0 ? softfloat.softfloat_flag_underflow : 0));
        // La petitesse : après l'arrondi (SDM vol. 1, 2003, § 4.9.1.5), avant sur le 8087 et le 287 (PB-59, seize_arrondi).
        softfloat.softfloat_detectTininess = seize_bits() ? softfloat.softfloat_tininess_beforeRounding
                                                          : softfloat.softfloat_tininess_afterRounding;
    }

    // pcem bug, fixed in hardware mode: PB-59 — après un calcul : les indicateurs de SoftFloat vers le mot d'état
    //   (inexact → PE, sous-dépassement → UE, dépassement → OE, infini → ZE, invalide → IE) ; C1, l'arrondi vers le
    //   haut, sur le 387 et le 486 (et le 287XL : déduit ; 8087 et 287 : indéfini, laissé). DE vient de la classe des
    //   opérandes, que SoftFloat ne signale pas. La petitesse se juge après l'arrondi, comme SoftFloat le fait pour le
    //   8086 (SDM vol. 1, 2003, § 4.9.1.5 : « whenever the rounded result of an instruction is tiny »). Après une faute
    //   de pile masquée, l'opérande est l'indéfini, un QNaN : le calcul est exact et C1 retombe à 0, comme la faute le
    //   veut d'un sous-débordement ; un débordement de pile pose son C1 après (faute_de_pile). Démasquées (N5) : IE et ZE
    //   arrêtent l'instruction avant qu'elle range (vrai) ; OE et UE, la destination un registre, laissent ranger le
    //   résultat à l'exposant décalé de 24 576 (SoftFloat, softfloat_unboundedExp, que pose arrondi ; UE levée pour un
    //   résultat petit, exact ou non : 387 PRM § 3.2.5.2), C1 et PE compris, puis lèvent (SDM vol. 1, 2003, § 8.5.4 et §
    //   8.5.5 : « After the result is stored, the OE flag is set and a software exception handler is invoked ») : faux ;
    //   une PE démasquée, de même (387 PRM § 3.2.6 : « delivers the rounded [...] result to the destination, regardless
    //   of whether a trap occurs »). L'instruction qui range se termine, dépilement compris (486 PRM § 19.2.5 : « the
    //   register stack and memory appear as if the instruction has run to completion »). La destination en mémoire
    //   (`memoire` : FST et FSTP m32 et m64),
    //   OE ou UE démasquée arrête l'instruction, rien n'est rangé ni dépilé, PE n'est pas signalée et C1 est mis à zéro
    //   (SDM vol. 1, 2003, § 8.5.6 : « the inexact-result condition is not reported and the C1 flag is cleared » ; 486
    //   PRM § 19.2.5 : « handled like a before exception; memory is not updated and the stack is not popped ») ; UE y
    //   est levée pour un résultat petit et exact par l'appelant (petit). La petitesse après l'arrondi n'est écrite que
    //   par le SDM (le 387 PRM et le 486 PRM disent « tiny » sans plus). Le 8087 et le 287 : seize_arrondi et
    //   seize_depassement.
    internal static bool indicateurs(bool memoire = false)
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
        var demasquees = (uint16_t)(e & ~cpu_state.npxc & 0x3F);
        if (memoire && (demasquees & (OE | UE)) != 0)
        {
                ModeMateriel.Sonde[59]++;
                if (cpu_c.fpu_type >= cpu_c.FPU_287XL)
                        cpu_state.npxs &= unchecked((uint16_t)~C1);
                return lever((uint16_t)(e & ~PE));
        }
        if (cpu_c.fpu_type >= cpu_c.FPU_287XL)
                cpu_state.npxs = (uint16_t)((cpu_state.npxs & ~C1) | (softfloat.softfloat_roundedUp ? C1 : 0));
        if ((demasquees & (OE | UE)) != 0)
                ModeMateriel.Sonde[59]++;
        if (e == 0 || !lever(e))
                return false;
        return (demasquees & (IE | ZE)) != 0;
    }

    /// <summary>Vrai si `v`, fini et non nul, est petit pour un format de `bits` de significande et d'exposant minimal
    /// `emin` (sans biais) : arrondi selon RC à `bits` sur un exposant non borné, il reste sous 2^emin (après l'arrondi,
    /// comme indicateurs ; un dénormal de 80 bits l'est toujours ; le 8087 et le 287 la jugent avant l'arrondi, sur la
    /// valeur vraie, PB-59). Les indicateurs de SoftFloat et C1 sont gardés.</summary>
    private static bool petit(in extFloat80_t v, byte bits, int emin)
    {
        var c = classe(v);
        if (c is Classe.Zero or Classe.Infini or Classe.QNaN or Classe.SNaN || non_supporte(c) || seize_bits() && nan_seize(c))
                return false;
        var (s, e, m) = normaliser(v);
        if (e >= emin)
                return false;
        if (seize_bits())
                return true;
        var f = softfloat.softfloat_exceptionFlags;
        var h = softfloat.softfloat_roundedUp;
        var u = softfloat.softfloat_unboundedExp;
        softfloat.softfloat_unboundedExp = 0;
        var r = softfloat.softfloat_roundPackToExtF80(s, e + 0x3FFF, m, 0, bits);
        softfloat.softfloat_exceptionFlags = f;
        softfloat.softfloat_roundedUp = h;
        softfloat.softfloat_unboundedExp = u;
        return (r.signExp & 0x7FFF) < emin + 0x3FFF;
    }

    // ===== Le 8087 et le 287 : les exceptions (N7a) =====

    // pcem bug, fixed in hardware mode: PB-59 — le dépassement et la petitesse du 8087 et du 287 (387 PRM, annexe C §
    //   C.2). OE ou UE démasquée, la destination un registre : « the significand is not rounded but rather is left as
    //   is », et PE n'est pas levée ; le noyau tronque le significande à 64 bits (déduit) et garde l'exposant décalé de
    //   N5. `seize_arrondi` refait l'arrondi de (s, exp, sig, extra) vers zéro ; les opérations qui n'arrondissent pas
    //   par softfloat_roundPackToExtF80 refont leur calcul (operation). La petitesse se juge avant l'arrondi, sur le
    //   résultat vrai (287 PRM, table 1-17 : « exponent of true result < −16,382 (true) » ; arrondi).
    internal static extFloat80_t seize_arrondi(bool s, int exp, uint64_t sig, uint64_t extra, byte bits)
    {
        var r = softfloat.softfloat_roundPackToExtF80(s, exp, sig, extra, bits);
        if (!brut())
                return r;
        var mode = softfloat.softfloat_roundingMode;
        var f = softfloat.softfloat_exceptionFlags;
        softfloat.softfloat_roundingMode = softfloat.softfloat_round_minMag;
        softfloat.softfloat_exceptionFlags = 0;
        r = softfloat.softfloat_roundPackToExtF80(s, exp, sig, extra, 80);
        softfloat.softfloat_exceptionFlags = (byte)((f | softfloat.softfloat_exceptionFlags) & ~softfloat.softfloat_flag_inexact);
        softfloat.softfloat_roundingMode = mode;
        ModeMateriel.Sonde[59]++;
        return r;
    }

    /// <summary>Vrai sur le 8087 et le 287 quand OE ou UE démasquée vient d'être levée par l'arrondi : le résultat doit
    /// être refait sans arrondi (seize_arrondi).</summary>
    internal static bool brut() =>
        seize_bits() && (softfloat.softfloat_exceptionFlags & softfloat.softfloat_unboundedExp &
                         (softfloat.softfloat_flag_overflow | softfloat.softfloat_flag_underflow)) != 0;

    // pcem bug, fixed in hardware mode: PB-59 — les réponses masquées du 8087 et du 287 (387 PRM, annexe C § C.2 ; 287
    //   PRM, tables 1-11 et 1-17), après l'arrondi d'un résultat `r` : OE masquée, le dépassement rend l'infini au plus
    //   près et vers zéro (« If rounding is set to chop (toward zero), the result is positive or negative infinity »), et
    //   OE n'est levée que si la réponse est l'infini (« does not signal the overflow exception when the masked
    //   response is not infinity ») : vers +∞ ou −∞, le plus grand fini, PE seule ; UE masquée et l'arrondi vers zéro,
    //   UE est levée sur la seule petitesse (« raised on tininess, regardless of loss of accuracy ») : un résultat
    //   dénormal exact la lève aussi. `exp`, `frac` : les bits de l'exposant et de la fraction du format (15 et 64, le
    //   bit entier compris, pour un registre ; 8 et 23, 11 et 52, pour FST m32 et m64).
    internal static void seize_depassement(ref uint64_t bits, ref uint16_t signExp, int exp, int frac)
    {
        if (!seize_bits())
                return;
        var f = softfloat.softfloat_exceptionFlags;
        var registre = exp == 15;
        var max = (1 << exp) - 1;
        var e = registre ? signExp & 0x7FFF : (int)(bits >> frac) & max;
        var fraction = registre ? bits : bits & ((1UL << frac) - 1);
        var negatif = registre ? (signExp & 0x8000) != 0 : (bits >> (frac + exp)) != 0;
        var chop = softfloat.softfloat_roundingMode == softfloat.softfloat_round_minMag;
        if ((f & softfloat.softfloat_flag_overflow) != 0 && masquee(OE))
        {
                ModeMateriel.Sonde[59]++;
                if (chop)
                {
                        if (registre)
                        {
                                signExp = (uint16_t)(negatif ? 0xFFFF : 0x7FFF);
                                bits = 0x8000000000000000;
                        }
                        else
                                bits = (negatif ? 1UL << (frac + exp) : 0) | (uint64_t)max << frac;
                }
                else if (e != max)
                        softfloat.softfloat_exceptionFlags &= unchecked((byte)~softfloat.softfloat_flag_overflow);
        }
        if (chop && masquee(UE) && e == 0 && fraction != 0 && (f & softfloat.softfloat_flag_underflow) == 0)
        {
                ModeMateriel.Sonde[59]++;
                softfloat.softfloat_exceptionFlags |= softfloat.softfloat_flag_underflow;
        }
    }

    /// <summary>seize_depassement d'un résultat de 80 bits.</summary>
    internal static void seize_depassement(ref extFloat80_t r) => seize_depassement(ref r.signif, ref r.signExp, 15, 64);

    // pcem bug, fixed in hardware mode: PB-60 — le 8087 et le 287 lèvent IE pour tout NaN opérande, et non pour le seul
    //   SNaN (387 PRM, annexe C § C.2 : « raises an invalid-operation exception upon encountering any kind of NaN »),
    //   et rendent le NaN de plus grande valeur absolue, signes ignorés (287 PRM, table 1-17 ; Numerics Supplement,
    //   1980, « NANs »), tel quel : ils ne connaissent qu'un genre de NaN et ne le rendent pas silencieux (déduit). Deux
    //   significandes égaux : le positif, la règle du 387 (inconnu).
    private static extFloat80_t nan_de_seize(in extFloat80_t a, in extFloat80_t b)
    {
        ModeMateriel.Sonde[60]++;
        softfloat.softfloat_exceptionFlags |= softfloat.softfloat_flag_invalid;
        bool na = nan_seize(classe(a)), nb = nan_seize(classe(b));
        if (!nb)
                return a;
        if (!na)
                return b;
        if (a.signif != b.signif)
                return a.signif > b.signif ? a : b;
        return a.signExp <= b.signExp ? a : b;
    }

    // pcem bug, fixed in hardware mode: PB-200 — DF E0 sur le 8087, qui n'a pas FNSTSW AX (287 PRM, FSTSW AX : « a
    //   special 80287 instruction » ; Numerics Supplement, 1980 : le 8087 n'échange avec l'UC que par la mémoire) : AX
    //   reste tel quel (déduit) ; ce que le 8087 fait de l'opcode est inconnu, le noyau n'en fait rien.
    internal static void dfe0_8087() => ModeMateriel.Sonde[200]++;

    // pcem bug, fixed in hardware mode: PB-59 — sur le 8087 et le 287, DE démasquée passe avant toute autre exception, sauf
    //   la faute de pile (287 PRM, table 3-2, « Denormalized operand (if unmasked) » en tête, et annexe C, point 4 :
    //   « a Denormal Operand exception takes precedence over a nonstack Invalid operation exception » ; 387 PRM, annexe
    //   C § C.2). Vrai si l'instruction s'arrête.
    private static bool de_d_abord(bool den)
    {
        if (!seize_bits() || !den || masquee(DE))
                return false;
        ModeMateriel.Sonde[59]++;
        return lever(DE);
    }

    // ===== Le 8087 et le 287 : les non normaux (N7b) =====
    //
    // Le 8087 et le 287 calculent sur un non normal (un exposant ni nul ni maximal, J = 0 ; le pseudo-zéro, son
    // significande nul, en est un) au lieu de le refuser, et convertissent un dénormal en son non normal équivalent,
    // l'exposant de 1 (−16382) et le même significande (287 PRM, « Unnormals—Descendents of Denormal Operands », tables
    // 1-8, 1-9 et 1-17) : « unnormal operands generate unnormal results, so long as their unnormality has a significant
    // effect on the result ». Leur chemin de données n'est pas décrit au bit près : le noyau calcule la valeur exacte de
    // l'opération et la range à l'exposant que la table 1-9 lui donne, sans la normaliser (ranger_seize) ; l'alignement
    // réel des opérandes et ses bits de garde sont inconnus.

    /// <summary>Sur le 8087 et le 287, un opérande de l'arithmétique des non normaux : un non normal (le pseudo-zéro
    /// compris) ou un dénormal (l'exposant nul, J compris : 287 PRM, annexe C, point 1).</summary>
    private static bool non_normal_seize(Classe c) =>
        seize_bits() && c is Classe.NonNormal or Classe.Denormal or Classe.PseudoDenormal;

    /// <summary>Un fini tel qu'il est : le signe, l'exposant sans biais, le significande sans normalisation ; un exposant
    /// nul vaut 1, le non normal équivalent du dénormal (287 PRM, table 1-8).</summary>
    private static (bool s, int e, uint64_t m) brut80(in extFloat80_t v)
    {
        var f = v.signExp & 0x7FFF;
        return ((v.signExp & 0x8000) != 0, (f == 0 ? 1 : f) - 0x3FFF, v.signif);
    }

    /// <summary>La valeur d'un non normal ou d'un dénormal, normalisée exactement (le plus possible : un dénormal au
    /// besoin) ; un pseudo-zéro devient le zéro de son signe. « Normalize as much as possible » (287 PRM, table 1-9).</summary>
    private static extFloat80_t normal80(in extFloat80_t v)
    {
        var (s, e, m) = brut80(v);
        var signe = (uint16_t)(s ? 0x8000 : 0);
        if (m == 0)
                return new extFloat80_t { signExp = signe };
        var d = Math.Min(System.Numerics.BitOperations.LeadingZeroCount(m), e + 16382);
        m <<= d;
        e -= d;
        return new extFloat80_t { signExp = (uint16_t)(signe | ((m >> 63) != 0 ? e + 0x3FFF : 0)), signif = m };
    }

    /// <summary>Un opérande du 8087 et du 287 tel que SoftFloat le prend : normalisé s'il est non normal ou dénormal.</summary>
    private static extFloat80_t pour_softfloat(in extFloat80_t v) => non_normal_seize(classe(v)) ? normal80(v) : v;

    // pcem bug, fixed in hardware mode: PB-59 — ranger_seize : la valeur exacte s × num / den × 2^k (num > 0), rangée à
    //   l'exposant de travail `er` imposé (le significande, ses bits au-dessus de 2^(er − 63), sans normaliser : un non
    //   normal reste non normal), arrondi selon RC et PC (24, 53 ou 64 bits comptés depuis le bit 63), une retenue
    //   montant er d'un. Petit, er sous −16382 (« exponent of true result < −16,382 », table 1-17) : masquée, la valeur
    //   dénormalisée jusqu'à −16382 et arrondie, UE si inexacte ou vers zéro (seize_depassement) ; démasquée, tronquée,
    //   l'exposant décalé de 24 576, sans PE (387 PRM, annexe C § C.2). Au-delà de 16383 : masquée, l'infini au plus près
    //   et vers zéro, OE et PE ; dirigée, l'infini du côté de l'arrondi, OE et PE, sinon le plus grand exposant avec le
    //   significande du résultat, tronqué, normal ou non, PE sans OE (287 PRM, table 1-11) ; démasquée, tronquée, décalée,
    //   sans PE. Un significande arrondi à zéro, hors de la petitesse : un pseudo-zéro (287 PRM : « A pseudo zero result
    //   will be produced if two unnormals [...] are multiplied together »). Les indicateurs vont à SoftFloat ; C1 n'est
    //   pas posé (indéfini sur le 8087 et le 287).
    private static extFloat80_t ranger_seize(bool s, BigInteger num, BigInteger den, int k, int er, bool selonPc)
    {
        ModeMateriel.Sonde[59]++;
        var p = !selonPc ? 64 : softfloat.extF80_roundingPrecision switch { 32 => 24, 64 => 53, _ => 64 };
        var signe = (uint16_t)(s ? 0x8000 : 0);
        var mode = softfloat.softfloat_roundingMode;
        var petitVrai = er < -16382;
        if (petitVrai && masquee(UE))
        {
                var (m, e, inexact) = arrondir_a(s, num, den, k, -16382, p, mode);
                var f = softfloat.softfloat_exceptionFlags;
                if (inexact || mode == softfloat.softfloat_round_minMag)
                        f |= softfloat.softfloat_flag_underflow;
                if (inexact)
                        f |= softfloat.softfloat_flag_inexact;
                softfloat.softfloat_exceptionFlags = f;
                if (m == 0)
                        return new extFloat80_t { signExp = signe };
                return new extFloat80_t { signExp = (uint16_t)(signe | ((m >> 63) != 0 ? e + 0x3FFF : 0)), signif = m };
        }
        if (petitVrai)
        {
                var (m, e, _) = arrondir_a(s, num, den, k, er, p, softfloat.softfloat_round_minMag);
                softfloat.softfloat_exceptionFlags |= softfloat.softfloat_flag_underflow;
                e += 0x6000;
                if (e + 0x3FFF <= 0)
                {
                        softfloat.softfloat_exceptionFlags |= softfloat.softfloat_flag_inexact;
                        return new extFloat80_t { signExp = signe };
                }
                return new extFloat80_t { signExp = (uint16_t)(signe | (e + 0x3FFF)), signif = m };
        }
        var (mr, er2, inex) = arrondir_a(s, num, den, k, er, p, mode);
        if (er2 > 16383)
        {
                if (!masquee(OE))
                {
                        var (m, e, _) = arrondir_a(s, num, den, k, er, p, softfloat.softfloat_round_minMag);
                        softfloat.softfloat_exceptionFlags |= softfloat.softfloat_flag_overflow;
                        e -= 0x6000;
                        if (e > 16383)
                        {
                                softfloat.softfloat_exceptionFlags |= softfloat.softfloat_flag_inexact;
                                return infini(s);
                        }
                        return new extFloat80_t { signExp = (uint16_t)(signe | (e + 0x3FFF)), signif = m };
                }
                softfloat.softfloat_exceptionFlags |= softfloat.softfloat_flag_inexact;
                var versInfini = mode == softfloat.softfloat_round_near_even || mode == softfloat.softfloat_round_minMag ||
                                 mode == (s ? softfloat.softfloat_round_min : softfloat.softfloat_round_max);
                if (versInfini)
                {
                        softfloat.softfloat_exceptionFlags |= softfloat.softfloat_flag_overflow;
                        return infini(s);
                }
                var (mt, _, _) = arrondir_a(s, num, den, k, er, p, softfloat.softfloat_round_minMag);
                var masque = ~((1UL << (64 - p)) - 1);
                var sig = (mt >> 63) != 0 ? masque : mt;
                return new extFloat80_t { signExp = (uint16_t)(signe | 0x7FFE), signif = sig };
        }
        if (inex)
                softfloat.softfloat_exceptionFlags |= softfloat.softfloat_flag_inexact;
        return new extFloat80_t { signExp = (uint16_t)(signe | (er2 + 0x3FFF)), signif = mr };
    }

    /// <summary>num / den × 2^k en unités de 2^(er − 63), arrondi selon `mode` à p bits comptés depuis le bit 63 ; une
    /// retenue au-delà du bit 63 décale d'un et monte er. Rend le significande, l'exposant et l'inexactitude.</summary>
    private static (uint64_t m, int e, bool inexact) arrondir_a(bool s, BigInteger num, BigInteger den, int k, int er, int p,
                                                              uint8_t mode)
    {
        var sh = k - er + 63;
        BigInteger n = num, d = den;
        if (sh >= 0)
                n <<= sh;
        else
                d <<= -sh;
        var du = d << (64 - p);
        var q = BigInteger.DivRem(n, du, out var reste);
        var inexact = !reste.IsZero;
        var plus = mode switch
        {
                softfloat.softfloat_round_near_even => reste * 2 > du || reste * 2 == du && !q.IsEven,
                softfloat.softfloat_round_min => inexact && s,
                softfloat.softfloat_round_max => inexact && !s,
                _ => false,
        };
        if (plus)
                q += 1;
        var m = q << (64 - p);
        if (m >= BigInteger.One << 64)
        {
                m >>= 1;
                er++;
        }
        return ((uint64_t)m, er, inexact);
    }

    private static BigInteger grand(uint64_t m) => new(m);

    // pcem bug, fixed in hardware mode: PB-59 — les quatre opérations du 8087 et du 287 sur un non normal ou un dénormal
    //   (287 PRM, tables 1-8, 1-9 et 1-17), `a op b`, aucun NaN ni infini : la division par un non normal ou un dénormal
    //   (le pseudo-zéro compris), IE et l'indéfini ; l'addition et la soustraction, rangées à l'exposant le plus grand
    //   des opérandes non nuls (un de plus sur une retenue), si l'opérande de plus grande valeur absolue est non normal,
    //   et normalisées, l'opération de SoftFloat sur les valeurs, s'il est normal (« Normalization of operand with
    //   larger absolute value determines normalization of result ») ; la multiplication, non normale (« If either operand
    //   is unnormal, result is unnormal ») : l'exposant de la somme des exposants, un de plus si le produit des
    //   significandes dépasse 2^127 ; la division d'un dividende non normal (le diviseur normal) : l'exposant de la
    //   différence, sans le décalage d'une division normalisée, le quotient gardant ses zéros de tête (« Result is
    //   unnormal »). Un zéro vrai rend le zéro de l'opération ; un résultat nul d'un
    //   pseudo-zéro, un pseudo-zéro (un pseudo-zéro divisé par un normal aussi : le 287 PRM, ambigu, pourrait vouloir un
    //   zéro vrai) ; une somme exactement nulle, le zéro vrai du signe d'IEEE 754 (287 PRM, table 1-10). La division par
    //   un zéro vrai et les infinis prennent le chemin ordinaire (chemin_seize). Le dépassement et la petitesse se
    //   jugent sur l'exposant de travail, celui du registre (« exponent of true result ») ; un arrondi à PC sous 64 bits
    //   peut rendre un pseudo-zéro (non décrit).
    //   DE pour un dénormal, l'opération faite. Rend vrai si l'instruction s'arrête.
    private static bool arith_seize(int k, in extFloat80_t a, in extFloat80_t b, bool den, out extFloat80_t r)
    {
        Classe ca = classe(a), cb = classe(b);
        r = Indefini;
        if (k == DIV && non_normal_seize(cb))
        {
                ModeMateriel.Sonde[59]++;
                softfloat.softfloat_exceptionFlags = softfloat.softfloat_flag_invalid;
                return indicateurs();
        }
        var (sa, ea, ma) = brut80(a);
        var (sb, eb, mb) = brut80(b);
        if (k == SUB)
                sb = !sb;
        if (k is ADD or SUB)
        {
                var lo = Math.Min(ca == Classe.Zero ? eb : ea, cb == Classe.Zero ? ea : eb);
                var va = grand(ma) << (ea - lo);
                var vb = grand(mb) << (eb - lo);
                var somme = (sa ? -va : va) + (sb ? -vb : vb);
                var er = Math.Max(ca == Classe.Zero ? int.MinValue : ea, cb == Classe.Zero ? int.MinValue : eb);
                if (somme.IsZero)
                {
                        var neg = sa == sb ? sa : softfloat.softfloat_roundingMode == softfloat.softfloat_round_min;
                        r = ma == 0 && mb == 0 ? pseudo_zero(neg, er) : zero(neg);
                }
                else
                {
                        var neg = somme.Sign < 0;
                        var abs = BigInteger.Abs(somme);
                        if (abs >= BigInteger.One << (er - lo + 64))
                                er++;
                        r = ranger_seize(neg, abs, BigInteger.One, lo - 63, er, true);
                }
        }
        else if (ca == Classe.Zero || cb == Classe.Zero)
                r = zero(sa != sb);
        else if (k == MUL)
        {
                var prod = grand(ma) * grand(mb);
                var er = ea + eb + (prod >= BigInteger.One << 127 ? 1 : 0);
                r = prod.IsZero ? pseudo_zero(sa != sb, er) : ranger_seize(sa != sb, prod, BigInteger.One, ea + eb - 126, er, true);
        }
        else
        {
                var er = ea - eb;
                r = ma == 0 ? pseudo_zero(sa != sb, er) : ranger_seize(sa != sb, grand(ma), grand(mb), ea - eb, er, true);
        }
        if ((den || denormal(ca) || denormal(cb)) && lever(DE))
                return true;
        return indicateurs();
    }

    /// <summary>Le pseudo-zéro d'exposant `er` (sans biais), le zéro vrai si l'exposant sort de la plage.</summary>
    private static extFloat80_t pseudo_zero(bool neg, int er)
    {
        var f = er + 0x3FFF;
        return new extFloat80_t { signExp = (uint16_t)((neg ? 0x8000 : 0) | (f is > 0 and < 0x7FFF ? f : 0)) };
    }

    /// <summary>Vrai si `a op b` prend le chemin des non normaux (arith_seize) : un opérande non normal ou dénormal sur le
    /// 8087 ou le 287, aucun NaN ni infini, et, pour l'addition et la soustraction, l'opérande de plus grande valeur
    /// absolue non normal (sinon, les valeurs normalisées par le chemin ordinaire) ; la division par un non normal,
    /// toujours (IE).</summary>
    private static bool chemin_seize(int k, in extFloat80_t a, in extFloat80_t b)
    {
        Classe ca = classe(a), cb = classe(b);
        if (!non_normal_seize(ca) && !non_normal_seize(cb))
                return false;
        if (k == DIV && non_normal_seize(cb))
                return true;
        // Un infini, ou la division par un zéro vrai (ZE et ±∞ ; IE pour un pseudo-zéro, 0 ÷ 0) : le chemin ordinaire.
        if (ca == Classe.Infini || cb == Classe.Infini || k == DIV && cb == Classe.Zero)
                return false;
        if (k is MUL or DIV)
                return true;
        if (ca == Classe.Zero && cb == Classe.Zero)
                return false;
        var (_, ea, ma) = brut80(a);
        var (_, eb, mb) = brut80(b);
        var lo = Math.Min(ca == Classe.Zero ? eb : ea, cb == Classe.Zero ? ea : eb);
        var va = grand(ma) << (ea - lo);
        var vb = grand(mb) << (eb - lo);
        // À égalité de valeur absolue, le chemin ne dépend pas de l'ordre : deux valeurs nulles (un zéro vrai et un
        // pseudo-zéro, ou deux pseudo-zéros) le prennent (le pseudo-zéro « behaves like unnormals », 287 PRM, « Zeros and
        // pseudo zeros ») ; sinon, un normal l'emporte (inconnu).
        if (va == vb)
                return va.IsZero || non_normal_seize(ca) && non_normal_seize(cb);
        return non_normal_seize(va > vb ? ca : cb);
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
    //   « #IA Source operand is an SNaN ») ; un dénormal lève DE et se charge normalisé, exact, que DE soit masquée ou
    //   non (SDM vol. 2, FLD : « an exception is flagged but the value is still pushed onto the x87 stack » ; le 387
    //   PRM § 3.2.4, le SDM vol. 1 § 8.5.2 et le 486 PRM § 19.2.5 disent, en général, la pile inchangée après une
    //   exception de dénormal démasquée : les sources se contredisent, la note propre à FLD est suivie ; inconnu). Le
    //   8087 et le 287 ne lèvent rien sur un SNaN (SDM vol. 3, 2016, § 22.18.7.11) ; la valeur qu'ils chargent est
    //   inconnue : ses bits, étendus sans le rendre silencieux. DE démasquée les arrête avant toute écriture, pile comprise
    //   (287 PRM, « Simultaneous Exception Response » et table 3-2 ; manuel de l'iAPX 86/88, 1981 : une exception
    //   « detected before an operation begins » laisse la pile et la mémoire ; PB-59, N7a). Leurs dénormaux se chargent en
    //   non normaux (non_normal_f32, N7b).
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
        var r = non_normal_f32(v) ?? softfloat.f32_to_extF80(new float32_t { v = v });
        uint16_t e = 0;
        if ((softfloat.softfloat_exceptionFlags & softfloat.softfloat_flag_invalid) != 0)
        {
                e |= IE;
                ModeMateriel.Sonde[206]++;
        }
        if ((v & 0x7F800000) == 0 && (v & 0x007FFFFF) != 0)
                e |= DE;
        if (e != 0 && lever(e) && (e != DE || seize_bits()))
                return;
        empiler(r);
    }

    // pcem bug, fixed in hardware mode: PB-212 — FLD m64 : un SNaN lève IE et se charge silencieux (SDM vol. 2, FLD) ;
    //   un dénormal lève DE, et s'empile, DE démasquée comprise. Le 8087 et le 287 : comme FLD m32 (PB-206).
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
        var r = non_normal_f64(v) ?? softfloat.f64_to_extF80(new float64_t { v = v });
        uint16_t e = 0;
        if ((softfloat.softfloat_exceptionFlags & softfloat.softfloat_flag_invalid) != 0)
        {
                e |= IE;
                ModeMateriel.Sonde[212]++;
        }
        if ((v & 0x7FF0000000000000) == 0 && (v & 0x000FFFFFFFFFFFFF) != 0)
                e |= DE;
        if (e != 0 && lever(e) && (e != DE || seize_bits()))
                return;
        empiler(r);
    }

    // pcem bug, fixed in hardware mode: PB-55 — FLD m80 : les 80 bits tels quels, sans conversion ni autre exception que
    //   la faute de pile (SDM vol. 2, FLD ; 387 PRM, annexe B) ; un format non supporté se charge, et lève IE quand une opération le lit.
    //   PB-59 : le 8087 et le 287 lèvent DE pour un dénormal (387 PRM, annexe C § C.4 : « Reports denormal
    //   exception » ; 287 PRM, annexe C, point 2 : de la mémoire seulement), l'exposant nul, J compris (point 1) ;
    //   masquée, « load as usual » (287 PRM, table 1-17) ; démasquée, rien n'est empilé (fld_f32).
    internal static void fld_f80(uint64_t signif, uint16_t signExp)
    {
        ModeMateriel.Sonde[55]++;
        c1_zero();
        if (seize_bits() && !plein() && (signExp & 0x7FFF) == 0 && signif != 0)
        {
                ModeMateriel.Sonde[59]++;
                if (lever(DE))
                        return;
        }
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
    //   compris) ; C1, le signe du registre, vide compris. Le 8087 et le 287 ont seize codes (287 PRM, table 2-8 ;
    //   Numerics Supplement, 1980, table S-13) : ceux du 387, et 000 pour un non normal, le pseudo-zéro compris (un non
    //   normal au significande nul : 287 PRM, « Zeros and pseudo zeros »), le signe dans C1 ; le pseudo-infini et le
    //   pseudo-NaN sont des NaN (nan_seize), 001. Le vide : quatre codes possibles, C3 = C0 = 1, C2 et C1 quelconques
    //   (287 PRM, FXAM : « bits C3 and C0 of the condition code are both 1 in all encodings ») ; le noyau rend celui du 387
    //   (inconnu).
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
                        Classe.PseudoInfini or Classe.PseudoNaN when seize_bits() => x87_c.C0,
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

    /// <summary>FST et FSTP m32 et m64 : arrondis selon RC (PC n'y joue pas), DE pour une source dénormale (pas sur le
    /// 8087 et le 287 : st_non_normal), puis O, U, P
    /// et C1 (l'arrondi vers le haut) ; un SNaN, IE et le QNaN. Rend vrai si l'instruction s'arrête, sans rien
    /// ranger : IE, DE, et OE ou UE démasquées (indicateurs, la destination en mémoire ; UE démasquée pour un résultat
    /// petit, même exact : 387 PRM § 3.2.5.2).</summary>
    internal static bool fst_f32(out uint32_t r)
    {
        r = 0;
        if (source(out var v, out var ns))
                return true;
        if (!seize_bits() && classe(v) is Classe.Denormal or Classe.PseudoDenormal && lever(DE))
                return true;
        arrondi(false);
        if (st_non_normal(ref v, -126))
        {
                r = 0xFFC00000;
                return indicateurs(true);
        }
        r = softfloat.extF80_to_f32(v).v;
        if (ns)
                softfloat.softfloat_exceptionFlags = 0;
        else
        {
                if (!masquee(UE) && petit(v, 32, -126))
                        softfloat.softfloat_exceptionFlags |= softfloat.softfloat_flag_underflow;
                uint64_t b = r;
                uint16_t se = 0;
                seize_memoire(v, ref b, ref se, 8, 23);
                r = (uint32_t)b;
        }
        return indicateurs(true);
    }

    internal static bool fst_f64(out uint64_t r)
    {
        r = 0;
        if (source(out var v, out var ns))
                return true;
        if (!seize_bits() && classe(v) is Classe.Denormal or Classe.PseudoDenormal && lever(DE))
                return true;
        arrondi(false);
        if (st_non_normal(ref v, -1022))
        {
                r = 0xFFF8000000000000;
                return indicateurs(true);
        }
        r = softfloat.extF80_to_f64(v).v;
        if (ns)
                softfloat.softfloat_exceptionFlags = 0;
        else
        {
                if (!masquee(UE) && petit(v, 64, -1022))
                        softfloat.softfloat_exceptionFlags |= softfloat.softfloat_flag_underflow;
                uint16_t se = 0;
                seize_memoire(v, ref r, ref se, 11, 52);
        }
        return indicateurs(true);
    }

    // pcem bug, fixed in hardware mode: PB-59 — FST et FSTP m32 et m64 d'un non normal sur le 8087 et le 287 (287 PRM, table
    //   1-9 : « If value is above destination's underflow boundary, then signal invalid operation; else signal
    //   underflow » ; table 1-17 : « Store real indefinite ») : au-dessus de 2^emin du format, IE, et l'indéfini (vrai, la
    //   destination s'en charge) ; au-dessous, la valeur normalisée, convertie, UE levée (le pseudo-zéro, de valeur
    //   nulle, compris : son zéro). Un dénormal du registre n'y lève pas DE (les tables 1-8 et 1-17 ne la nomment pas
    //   pour FST : déduit), et se convertit comme le 387 le ferait. Après arrondi.
    private static bool st_non_normal(ref extFloat80_t v, int emin)
    {
        if (!seize_bits() || classe(v) != Classe.NonNormal)
                return false;
        ModeMateriel.Sonde[59]++;
        var (_, e, m) = brut80(v);
        if (m != 0 && e - System.Numerics.BitOperations.LeadingZeroCount(m) >= emin)
        {
                softfloat.softfloat_exceptionFlags |= softfloat.softfloat_flag_invalid;
                return true;
        }
        v = normal80(v);
        softfloat.softfloat_exceptionFlags |= softfloat.softfloat_flag_underflow;
        return false;
    }

    /// <summary>FST m32 et m64 sur le 8087 et le 287 : IE pour un NaN (nan_de_seize, PB-60 ; la valeur rangée, celle de
    /// la conversion de SoftFloat, inconnue), puis les réponses masquées du dépassement et de la petitesse
    /// (seize_depassement, PB-59).</summary>
    private static void seize_memoire(in extFloat80_t v, ref uint64_t r, ref uint16_t se, int exp, int frac)
    {
        if (!seize_bits())
                return;
        if (nan_seize(classe(v)))
        {
                ModeMateriel.Sonde[60]++;
                softfloat.softfloat_exceptionFlags |= softfloat.softfloat_flag_invalid;
                return;
        }
        seize_depassement(ref r, ref se, exp, frac);
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
    //   annexe C ; N7b).
    internal static bool fist(int bits, out int64_t r)
    {
        r = 0;
        if (source(out var v, out _))
                return true;
        arrondi(false);
        if (non_normal_seize(classe(v)))
        {
                // Le 8087 et le 287 : FIST et FISTP d'un dénormal ou d'un non normal, IE et l'indéfini entier (287 PRM,
                // tables 1-9 et 1-17 ; 387 PRM, annexe C § C.4 ; PB-59).
                ModeMateriel.Sonde[59]++;
                softfloat.softfloat_exceptionFlags = softfloat.softfloat_flag_invalid;
                r = bits == 16 ? short.MinValue : bits == 32 ? int.MinValue : long.MinValue;
                return indicateurs();
        }
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
    //   son signe (déduit, comme FIST : 387 PRM § 4.3.6). Le 8087 et le 287, sur un dénormal ou un non normal : comme FIST (N7b). Rend vrai
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
        // Le 8087 et le 287 : FBSTP d'un dénormal ou d'un non normal, IE et l'indéfini BCD (287 PRM, table 1-17 ; PB-59).
        if (ns || c is Classe.QNaN or Classe.SNaN or Classe.Infini || non_normal_seize(c))
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

    // ===== Les pointeurs, les limites, l'environnement (N3b) =====

    // pcem bug, fixed in hardware mode: PB-62 — chaque instruction qui n'est pas de contrôle mémorise son adresse, son
    //   opcode et l'adresse de son opérande (387 PRM § 2.1.5 ; SDM vol. 1, 2003, § 8.1.7 et § 8.1.8). Les instructions
    //   de contrôle les laissent : FINIT, FCLEX, FLDCW, FSTCW, FSTSW, FSTENV, FLDENV, FSAVE, FRSTOR et WAIT (la liste du
    //   SDM ; le 387 PRM la donne pour le pointeur d'opérande), et FSETPM, FENI et FDISI, sans effet sur le 387 (387
    //   PRM § 4.8 : « no internal states are affected » ; le 486 PRM dit FSETPM « treated as FNOP », et ailleurs sans
    //   fonction : le noyau le tient pour sans effet) ; FNOP, FFREE, FINCSTP et FDECSTP, des instructions de contrôle
    //   pour le 387 PRM (table 4-12) mais absentes des listes qui laissent les pointeurs, les posent (déduit). L'adresse est
    //   celle du premier préfixe (387 PRM § 2.1.5, « as in the 80287 »), CS:oldpc ; l'opcode, les trois bits bas de
    //   l'ESC et l'octet ModRM. Une forme de registre laisse le pointeur d'opérande, « undefined » (387 PRM ; le 387 ne
    //   le prend que « if present » : déduit). Les pointeurs se prennent après les contrôles de l'adresse : une
    //   instruction qui faute sur son opérande ne les touche pas (inconnu).
    internal static void pointeurs(int esc, uint32_t fetchdat)
    {
        fip = cpu_state.oldpc;
        fcs = x86.CS;
        fop = (uint16_t)((uint32_t)((esc & 7) << 8) | (fetchdat & 0xFF));
    }

    internal static void pointeurs_m(int esc, uint32_t fetchdat)
    {
        pointeurs(esc, fetchdat);
        fdp = cpu_state.eaaddr;
        fds = cpu_state.ea_seg!.seg;
    }

    // pcem bug, fixed in hardware mode: PB-62 — le 8087 prend l'adresse de l'ESC lui-même, sans ses préfixes (387 PRM
    //   § 2.1.5 : « the instruction address points only to the ESC instruction opcode »), et celle de l'opérande, en
    //   adresses physiques de 20 bits (287 PRM, fig. 1-7, « formatted like the 8087 »). L'ESC est avant l'octet ModRM
    //   et son déplacement, que le 8088 a lus (fetchea). Quelles instructions de contrôle laissent ses pointeurs :
    //   celles du 287 (déduit).
    internal static void pointeurs_8087(int esc, uint32_t fetchdat, bool memoire)
    {
        var mod = (fetchdat >> 6) & 3;
        var dep = mod == 1 ? 1u : mod == 2 || (mod == 0 && (fetchdat & 7) == 6) ? 2u : 0u;
        fip = ((uint32_t)(x86.CS << 4) + ((cpu_state.pc - 2 - dep) & 0xFFFF)) & 0xFFFFF;
        fcs = 0;
        fop = (uint16_t)((uint32_t)((esc & 7) << 8) | (fetchdat & 0xFF));
        if (!memoire)
                return;
        fdp = (x86.easeg + (cpu_state.eaaddr & 0xFFFF)) & 0xFFFFF;
        fds = 0;
    }

    // pcem bug, fixed in hardware mode: PB-54 — chaque accès du x87 à la mémoire contrôle l'opérande contre la limite de
    //   son segment, et, en mode protégé, ses droits. Les droits : écrire dans un segment de code ou de données en
    //   lecture seule lève #GP(0) (SDM vol. 2, FST, « #GP(0) If the destination is located in a non-writable segment ») ;
    //   lire un segment de code en exécution seule aussi, la règle de tout accès en lecture du mode protégé (déduit
    //   pour le x87). La limite : l'exception 13 si l'opérande commence hors de la limite (387 PRM, annexe D,
    //   item 7 ; 287 PRM, table 2-4 : « Word memory reference with offset = FFFF(H) », le premier mot). Un mot suivant
    //   qui en sort lève l'INT 9 du 287 et du 387 selon leurs manuels (287 PRM § 9.6.3 ; 387 PRM, annexe D, item 7),
    //   l'exception 13 selon le 386 PRM (§ 14.7, point 7) : sur le 386 et le 486, les sources se contredisent et
    //   l'exception 13 est retenue pour l'opérande entier ; sur le 286, le 287 PRM seul fait foi, et l'INT 9 n'est pas
    //   modélisée : le premier mot est contrôlé, les suivants passent, comme dans PCem. Le 286 ne replie pas ici
    //   l'offset de chaque mot comme pour ses propres opérandes (PB-189) : le canal du 287 incrémente l'adresse (287 PRM §
    //   9.6.3, « the limit test performed on each transfer »). #SS(0) pour la pile, comme PB-189. Le contrôle précède
    //   tout ce que l'instruction modifie ; le 8087 n'a pas de limite.
    internal static bool limite(uint32_t taille, bool ecriture)
    {
        var s = cpu_state.ea_seg!;
        var bas = cpu_state.eaaddr;
        var pm = (x86.msw & 1) != 0 && (cpu_state.eflags & x86.VM_FLAG) == 0;
        var interdit = pm && (ecriture ? (s.access & 0x0A) != 0x02 : (s.access & 0x0A) == 0x08);
        if (x86.is386 == 0 && taille > 2)
                taille = 2;
        if (!interdit && bas >= s.limit_low && (uint64_t)bas + taille - 1 <= s.limit_high)
                return false;
        ModeMateriel.Sonde[54]++;
        if (!interdit && ReferenceEquals(s, cpu_state.seg_ss))
                x86seg_c.x86ss(null!, 0);
        else
                x86seg_c.x86gpf(null!, 0);
        return true;
    }

    /// <summary>Les mots d'une image à écrire ou à lire : `n` paires, l'offset dans l'image et le mot. Les champs
    /// réservés n'y sont pas : ni écrits, ni lus.</summary>
    internal static readonly uint32_t[] off = new uint32_t[64];
    internal static readonly uint16_t[] mot = new uint16_t[64];
    internal static int n;

    private static readonly uint16_t[] image = new uint16_t[54];

    private static void ajouter(uint32_t o, uint32_t v)
    {
        off[n] = o;
        mot[n] = (uint16_t)v;
        n++;
    }

    // pcem bug, fixed in hardware mode: PB-62 — la disposition de l'image (387 PRM § 2.1.5, fig. 2-5 à 2-8 ; 486 PRM §
    //   15.1.5) : bit 0, le mode protégé ; bit 1, l'opérande de 32 bits. Le 387 et le 486 suivent PE, le mode virtuel
    //   8086 prenant celle du mode réel ; le 287 et le 287XL, qui ne voient pas le mode du processeur, suivent FSETPM
    //   (287 PRM, FSETPM ; le 287XL, au brochage du 287 : déduit) ; le 8087, la disposition réelle de 16 bits.
    internal static int disposition()
    {
        if (cpu_c.fpu_type == cpu_c.FPU_8087)
                return 0;
        var pm = cpu_c.fpu_type is cpu_c.FPU_287 or cpu_c.FPU_287XL
                ? fsetpm
                : (x86.msw & 1) != 0 && (cpu_state.eflags & x86.VM_FLAG) == 0;
        return (pm ? 1 : 0) | ((cpu_state.op32 & 0x100) != 0 ? 2 : 0);
    }

    internal static uint32_t taille_env(int d) => (d & 2) != 0 ? 28u : 14u;

    // pcem bug, fixed in hardware mode: PB-62 — l'environnement rangé, dans les quatre dispositions : le mot de
    //   contrôle, le mot d'état (TOP compris), les étiquettes et les pointeurs. En mode réel, les adresses de 20 bits (16
    //   bits) ou de 32 bits (32 bits), le sélecteur décalé de quatre bits ajouté à l'offset ; en mode protégé, l'offset
    //   et le sélecteur. L'opcode : dans les deux dispositions réelles, et dans celle de 32 bits protégée du 387 (fig.
    //   2-5 ; son annexe D, item 5, dit au contraire l'opcode « not saved in protected mode » : la figure est suivie),
    //   pas dans celle du 486 (486 PRM, fig. 15-5 : réservé) ni dans celle de 16 bits protégée. Les champs
    //   réservés (les moitiés hautes de la disposition de 32 bits) ne sont pas écrits : leur valeur sur le silicium est
    //   inconnue, et PCem les laisse ; les bits que les figures disent nuls le sont.
    private static void env_ranger(int d)
    {
        n = 0;
        ModeMateriel.Sonde[62]++;
        var sw = (cpu_state.npxs & 0xC7FF) | ((cpu_state.TOP & 7) << 11);
        uint32_t ip = (uint32_t)(fcs << 4) + fip, op = (uint32_t)(fds << 4) + fdp;
        if ((d & 2) == 0)
        {
                ajouter(0, cpu_state.npxc);
                ajouter(2, (uint32_t)sw);
                ajouter(4, etiquettes);
                if (d == 0)
                {
                        ajouter(6, ip);
                        ajouter(8, ((ip >> 16) & 0xF) << 12 | (uint32_t)(fop & 0x7FF));
                        ajouter(10, op);
                        ajouter(12, ((op >> 16) & 0xF) << 12);
                }
                else
                {
                        ajouter(6, fip);
                        ajouter(8, fcs);
                        ajouter(10, fdp);
                        ajouter(12, fds);
                }
        }
        else
        {
                ajouter(0, cpu_state.npxc);
                ajouter(4, (uint32_t)sw);
                ajouter(8, etiquettes);
                if (d == 2)
                {
                        ajouter(12, ip);
                        var x = ((ip >> 16) & 0xFFFF) << 12 | (uint32_t)(fop & 0x7FF);
                        ajouter(16, x);
                        ajouter(18, x >> 16);
                        ajouter(20, op);
                        var y = ((op >> 16) & 0xFFFF) << 12;
                        ajouter(24, y);
                        ajouter(26, y >> 16);
                }
                else
                {
                        ajouter(12, fip);
                        ajouter(14, fip >> 16);
                        ajouter(16, fcs);
                        if (cpu_c.fpu_type != cpu_c.FPU_BUILTIN)
                                ajouter(18, (uint32_t)(fop & 0x7FF));
                        ajouter(20, fdp);
                        ajouter(22, fdp >> 16);
                        ajouter(24, fds);
                }
        }
        // PB-208 : les étiquettes rangées sont celles du contenu (etiquette_de, à chaque écriture d'un registre).
        for (var p = 0; p < 8; p++)
                if (etiquette(p) == SPECIAL)
                {
                        ModeMateriel.Sonde[208]++;
                        break;
                }
    }

    // pcem bug, fixed in hardware mode: PB-199 — FSAVE range, après l'environnement, ST(0) à ST(7), chacun en réel de 80
    //   bits, quel que soit son contenu (SDM vol. 1, 2003, § 8.1.9 ; l'ordre de pile : 387 PRM, fig. 4-1 et 4-2).
    private static void registres_ranger(int d)
    {
        ModeMateriel.Sonde[199]++;
        var b = taille_env(d);
        for (var i = 0; i < 8; i++, b += 10)
        {
                var v = R[phys(i)];
                ajouter(b, (uint32_t)v.signif);
                ajouter(b + 2, (uint32_t)(v.signif >> 16));
                ajouter(b + 4, (uint32_t)(v.signif >> 32));
                ajouter(b + 6, (uint32_t)(v.signif >> 48));
                ajouter(b + 8, v.signExp);
        }
    }

    /// <summary>FSTENV : les mots de l'environnement, à écrire (off, mot, n).</summary>
    internal static void fstenv(int d) => env_ranger(d);

    // PB-207 (corrigé en G13.6b, en double) : après avoir rangé l'environnement, FSTENV masque les six exceptions (SDM
    //   vol. 2, FSTENV ; le 8087 et le 287 : déduit).
    internal static void fstenv_fin() => cpu_state.npxc |= 0x3F;

    /// <summary>FSAVE : l'environnement et les huit registres, à écrire.</summary>
    internal static void fsave(int d)
    {
        env_ranger(d);
        registres_ranger(d);
    }

    /// <summary>Après FSAVE, l'état de FNINIT (SDM vol. 2, FSAVE) : finit, qui garde les pointeurs sur le 387 et les
    /// efface sur le 486, et C3-C0 sur le 8087 et le 287 (PB-202 ; FSAVE du 8087 : 86Box, ticket 4518, seconde main ; du
    /// 287 : déduit).</summary>
    internal static void fsave_fin() => finit();

    /// <summary>FLDENV et FRSTOR : les offsets des mots à lire, ceux qu'env_ranger et registres_ranger écrivent, sans les
    /// champs réservés.</summary>
    internal static void a_lire(int d, bool registres)
    {
        n = 0;
        if ((d & 2) == 0)
                for (uint32_t o = 0; o < 14; o += 2)
                        ajouter(o, 0);
        else if (d == 2)
                foreach (var o in (ReadOnlySpan<uint32_t>)[0, 4, 8, 12, 16, 18, 20, 24, 26])
                        ajouter(o, 0);
        else
        {
                foreach (var o in (ReadOnlySpan<uint32_t>)[0, 4, 8, 12, 14, 16, 20, 22, 24])
                        ajouter(o, 0);
                if (cpu_c.fpu_type != cpu_c.FPU_BUILTIN)
                        ajouter(18, 0);
        }
        if (!registres)
                return;
        var b = taille_env(d);
        for (uint32_t k = 0; k < 80; k += 2)
                ajouter(b + k, 0);
    }

    private static uint32_t L(uint32_t o) => image[o >> 1];

    private static uint32_t LL(uint32_t o) => image[o >> 1] | (uint32_t)image[(o >> 1) + 1] << 16;

    // pcem bug, fixed in hardware mode: PB-62 — FLDENV et FRSTOR relisent les pointeurs de l'image, dans sa
    //   disposition : les adresses du mode réel, sélecteur à zéro ; l'offset et le sélecteur du mode protégé ; l'opcode
    //   là où l'image le porte, gardé sinon (déduit). Le mot d'état donne TOP. Démasquer ainsi une exception en
    //   attente : N5.
    internal static void fldenv(int d, bool registres)
    {
        for (var k = 0; k < n; k++)
                image[off[k] >> 1] = mot[k];
        cpu_state.npxc = (uint16_t)L(0);
        var sw = (uint16_t)L((d & 2) != 0 ? 4u : 2u);
        var tw = (uint16_t)L((d & 2) != 0 ? 8u : 4u);
        cpu_state.npxs = sw;
        cpu_state.TOP = (sw >> 11) & 7;
        switch (d)
        {
        case 0:
                fip = L(6) | (L(8) >> 12) << 16;
                fop = (uint16_t)(L(8) & 0x7FF);
                fdp = L(10) | (L(12) >> 12) << 16;
                fcs = fds = 0;
                break;
        case 1:
                fip = L(6);
                fcs = (uint16_t)L(8);
                fdp = L(10);
                fds = (uint16_t)L(12);
                break;
        case 2:
                fip = L(12) | ((LL(16) >> 12) & 0xFFFF) << 16;
                fop = (uint16_t)(L(16) & 0x7FF);
                fdp = L(20) | ((LL(24) >> 12) & 0xFFFF) << 16;
                fcs = fds = 0;
                break;
        default:
                fip = LL(12);
                fcs = (uint16_t)L(16);
                if (cpu_c.fpu_type != cpu_c.FPU_BUILTIN)
                        fop = (uint16_t)(L(18) & 0x7FF);
                fdp = LL(20);
                fds = (uint16_t)L(24);
                break;
        }
        if (registres)
        {
                var b = taille_env(d);
                for (var i = 0; i < 8; i++, b += 10)
                        R[phys(i)] = new extFloat80_t { signif = LL(b) | (uint64_t)LL(b + 4) << 32, signExp = (uint16_t)L(b + 8) };
        }
        etiquettes_chargees(tw);
    }

    // pcem bug, fixed in hardware mode: PB-208 — le 387 et le 486 ne retiennent d'une étiquette chargée que « vide » ou
    //   « non vide », et recalculent celle d'un registre non vide de son contenu (SDM vol. 1, 2003, § 8.1.6 : « the x87
    //   FPU reads the actual value in the data register and sets the tag for the register accordingly » ; le 287XL :
    //   déduit). Le 8087 et le 287 gardent les étiquettes de l'image (387 PRM, annexe C § C.3 : « the 80287/8087, in
    //   this case, honors the tag and does not examine the register »). Qu'ils s'y fient ensuite pour classer l'opérande
    //   de chaque instruction est documenté, mais pas ce qu'ils font d'un contenu que l'étiquette contredit (quelle
    //   classe, par exemple, pour « spécial » sur un normal ?) : inconnu, le noyau examine le contenu (c).
    private static void etiquettes_chargees(uint16_t tw)
    {
        if (cpu_c.fpu_type < cpu_c.FPU_287XL)
        {
                etiquettes = tw;
                return;
        }
        for (var p = 0; p < 8; p++)
        {
                var e = (tw >> (p * 2)) & 3;
                var c = e == VIDE ? VIDE : etiquette_de(R[p]);
                if (c != e)
                        ModeMateriel.Sonde[208]++;
                etiqueter(p, c);
        }
    }

    // ===== L'arithmétique et les comparaisons (N4a) =====
    //
    // Les exceptions de chaque instruction (387 PRM, annexe B) : FADD, FSUB, FSUBR, FMUL et leurs formes entières, IS, I,
    // D, O, U et P ; FDIV et FDIVR, Z en plus ; FSQRT, IS, I, D et P ; FCOM, FCOMP, FCOMPP, FICOM, FICOMP, FTST, FUCOM,
    // FUCOMP et FUCOMPP, IS, I et D. Leur ordre (SDM vol. 1, 2003, § 4.9.2) : la faute de pile, un format non supporté,
    // un SNaN ; un QNaN, qui n'est pas une exception mais passe avant les suivantes (« a QNaN divided by zero results in
    // a QNaN ») ; une autre opération invalide, la division par zéro ; le dénormal, qui, masqué, laisse les suivantes se
    // produire ; le dépassement, par excès ou par défaut ; la précision. Les premières sont « detected before a
    // floating-point operation begins » : démasquées, la destination reste intacte. RC et PC règlent l'arrondi
    // (arrondi(true)) : PB-48 et la précision de PB-56. Le 8087 et le 287 : DE démasquée avant les autres, IE pour tout
    // NaN, leurs dépassements (N7a, plus haut) ; leurs non normaux (arith_seize, N7b) ; l'infini projectif dans
    // l'addition et la soustraction (PB-70).

    internal const int ADD = 0, MUL = 1, SUB = 2, DIV = 3;

    /// <summary>Un opérande m32 en 80 bits, exact. Un NaN garde son bit de silence : le calcul applique la règle des NaN
    /// (SDM vol. 1, 2003, table 4-7 : un SNaN et un QNaN, le QNaN) à l'opérande tel qu'il est en mémoire. `den` : un
    /// dénormal, que la conversion normalise.</summary>
    internal static extFloat80_t de_f32(uint32_t v, out bool den)
    {
        den = (v & 0x7F800000) == 0 && (v & 0x007FFFFF) != 0;
        if ((v & 0x7F800000) == 0x7F800000 && (v & 0x007FFFFF) != 0)
                return new extFloat80_t
                {
                        signExp = (uint16_t)((v >> 16 & 0x8000) | 0x7FFF),
                        signif = 0x8000000000000000 | (uint64_t)(v & 0x007FFFFF) << 40,
                };
        return non_normal_f32(v) ?? softfloat.f32_to_extF80(new float32_t { v = v });
    }

    /// <summary>Un opérande m64 en 80 bits, comme de_f32.</summary>
    internal static extFloat80_t de_f64(uint64_t v, out bool den)
    {
        den = (v & 0x7FF0000000000000) == 0 && (v & 0x000FFFFFFFFFFFFF) != 0;
        if ((v & 0x7FF0000000000000) == 0x7FF0000000000000 && (v & 0x000FFFFFFFFFFFFF) != 0)
                return new extFloat80_t
                {
                        signExp = (uint16_t)((v >> 48 & 0x8000) | 0x7FFF),
                        signif = 0x8000000000000000 | (v & 0x000FFFFFFFFFFFFF) << 11,
                };
        return non_normal_f64(v) ?? softfloat.f64_to_extF80(new float64_t { v = v });
    }

    // pcem bug, fixed in hardware mode: PB-59 — le 8087 et le 287 chargent un dénormal de 32 ou de 64 bits en son non normal
    //   équivalent (387 PRM, annexe C § C.4 : « Loading a denormal causes the number to be converted to an unnormal » ;
    //   287 PRM, table 1-8 : « Load as equivalent unnormal »), en FLD comme en opérande d'une opération : l'exposant
    //   minimal du format (−126, −1022), le significande décalé tel quel, J = 0 ; null ailleurs.
    private static extFloat80_t? non_normal_f32(uint32_t v)
    {
        if (!seize_bits() || (v & 0x7F800000) != 0 || (v & 0x007FFFFF) == 0)
                return null;
        ModeMateriel.Sonde[59]++;
        return new extFloat80_t { signExp = (uint16_t)((v >> 16 & 0x8000) | (0x3FFF - 126)), signif = (uint64_t)(v & 0x007FFFFF) << 40 };
    }

    private static extFloat80_t? non_normal_f64(uint64_t v)
    {
        if (!seize_bits() || (v & 0x7FF0000000000000) != 0 || (v & 0x000FFFFFFFFFFFFF) == 0)
                return null;
        ModeMateriel.Sonde[59]++;
        return new extFloat80_t { signExp = (uint16_t)((v >> 48 & 0x8000) | (0x3FFF - 1022)), signif = (v & 0x000FFFFFFFFFFFFF) << 11 };
    }

    /// <summary>Un entier m16 ou m32 en 80 bits, exact (FIADD et ses voisines, FICOM).</summary>
    internal static extFloat80_t de_i(int32_t v) => softfloat.i32_to_extF80(v);

    private static bool nan(Classe c) => c is Classe.QNaN or Classe.SNaN;

    private static bool denormal(Classe c) => c is Classe.Denormal or Classe.PseudoDenormal;

    // pcem bug, fixed in hardware mode: PB-48 PB-60 — les quatre opérations sur 80 bits, `a op b` : arrondies selon RC
    //   et PC (387 PRM § 2.2.4 ; SDM vol. 1, 2003, § 8.1.4.2), qu'elles soient en mémoire ou de registre, l'opérande en
    //   mémoire d'abord étendu à 80 bits ; le NaN qui survit est celui de la règle du silicium (SDM vol. 1, 2003, table
    //   4-7 : un SNaN et un QNaN, le QNaN ; deux NaN du même genre, le plus grand significande ; rendu silencieux), que
    //   SoftFloat applique dans sa spécialisation du 8086, pas celui de l'ordre des opérandes. Deux significandes égaux
    //   de signes contraires : SoftFloat rend le positif (inconnu sur le silicium). Le format non supporté (387, 486) :
    //   IE et l'indéfini ; un dénormal, DE, sauf si un NaN, une autre opération invalide ou la division par zéro passe
    //   avant (SDM vol. 1, 2003, § 4.9.2). Rend vrai si l'instruction s'arrête, sans rien ranger. Le 8087 et le 287 :
    //   DE démasquée d'abord (de_d_abord), IE pour tout NaN (nan_de_seize), et leurs réponses au dépassement et à la
    //   petitesse (seize_depassement ; démasquées, le calcul refait sans arrondi, seize_arrondi).
    internal static bool operation(int k, in extFloat80_t a, in extFloat80_t b, bool den, out extFloat80_t r)
    {
        Classe ca = classe(a), cb = classe(b);
        arrondi(true);
        if (non_supporte(ca) || non_supporte(cb))
        {
                r = Indefini;
                softfloat.softfloat_exceptionFlags = softfloat.softfloat_flag_invalid;
                return indicateurs();
        }
        r = Indefini;
        if (de_d_abord(den || denormal(ca) || denormal(cb)))
                return true;
        if (seize_bits() && (nan_seize(ca) || nan_seize(cb)))
        {
                r = nan_de_seize(a, b);
                return indicateurs();
        }
        if (projectif() && k is ADD or SUB && ca == Classe.Infini && cb == Classe.Infini)
        {
                // PB-70 — en projectif, ∞ ± ∞, quels que soient les signes : IE et l'indéfini (287 PRM, table 1-12).
                ModeMateriel.Sonde[70]++;
                softfloat.softfloat_exceptionFlags = softfloat.softfloat_flag_invalid;
                return indicateurs();
        }
        if (chemin_seize(k, a, b))
                return arith_seize(k, a, b, den, out r);
        // Le 8087 et le 287, l'opérande le plus grand normal, ou un infini : les valeurs, normalisées, au chemin ordinaire.
        extFloat80_t x = a, y = b;
        if (seize_bits())
        {
                x = pour_softfloat(a);
                y = pour_softfloat(b);
        }
        r = calcul(k, x, y);
        ModeMateriel.Sonde[48]++;
        if (nan(ca) || nan(cb))
                ModeMateriel.Sonde[60]++;
        else if ((softfloat.softfloat_exceptionFlags & (softfloat.softfloat_flag_invalid | softfloat.softfloat_flag_infinite)) == 0 &&
                 (den || denormal(ca) || denormal(cb)) && lever(DE))
                return true;
        if (brut())
        {
                // Le 8087 et le 287, OE ou UE démasquée : le calcul refait vers zéro, à 64 bits, sans PE (seize_arrondi).
                var mode = softfloat.softfloat_roundingMode;
                var bits = softfloat.extF80_roundingPrecision;
                var f = softfloat.softfloat_exceptionFlags;
                softfloat.softfloat_roundingMode = softfloat.softfloat_round_minMag;
                softfloat.extF80_roundingPrecision = 80;
                softfloat.softfloat_exceptionFlags = 0;
                r = calcul(k, x, y);
                softfloat.softfloat_exceptionFlags = (byte)((f | softfloat.softfloat_exceptionFlags) & ~softfloat.softfloat_flag_inexact);
                softfloat.softfloat_roundingMode = mode;
                softfloat.extF80_roundingPrecision = bits;
                ModeMateriel.Sonde[59]++;
        }
        seize_depassement(ref r);
        return indicateurs();
    }

    private static extFloat80_t calcul(int k, in extFloat80_t a, in extFloat80_t b) => k switch
    {
        ADD => softfloat.extF80_add(a, b),
        MUL => softfloat.extF80_mul(a, b),
        SUB => softfloat.extF80_sub(a, b),
        _ => softfloat.extF80_div(a, b),
    };

    // Une faute de pile masquée range l'indéfini, sans calculer (SDM vol. 1, 2003, § 8.5.1.1 : « the x87 FPU returns the
    // floating point, integer, or packed decimal integer indefinite value to the destination operand ») : pas le NaN
    // qu'une propagation choisirait.

    /// <summary>ST(0) ← ST(0) op m, ou m op ST(0) (`inverse`, FSUBR et FDIVR) : les formes en mémoire, l'opérande déjà
    /// étendu (de_f32, de_f64, ou l'entier, exact).</summary>
    internal static void arith_m(int k, bool inverse, in extFloat80_t m, bool den)
    {
        var faute = vide(0);
        if (lire(0, out var x))
                return;
        var r = Indefini;
        if (!faute && (inverse ? operation(k, m, x, den, out r) : operation(k, x, m, den, out r)))
                return;
        ecrire(0, r);
    }

    /// <summary>D8 de registre : ST(0) ← ST(0) op ST(i), ou ST(i) op ST(0) (`inverse`).</summary>
    internal static void arith_st0(int k, bool inverse, int i)
    {
        var faute = vide(0) || vide(i);
        if (lire(0, out var x) || lire(i, out var y))
                return;
        var r = Indefini;
        if (!faute && (inverse ? operation(k, y, x, false, out r) : operation(k, x, y, false, out r)))
                return;
        ecrire(0, r);
    }

    /// <summary>DC et DE de registre : ST(i) ← ST(i) op ST(0), ou ST(0) op ST(i) (`inverse` : DC E0+i et DE E0+i,
    /// FSUBR ; DC F0+i et DE F0+i, FDIVR ; SDM vol. 2, FSUB/FSUBP/FISUB et FSUBR/FSUBRP/FISUBR) ; `pop`, puis dépiler.
    /// Rien n'est rangé ni dépilé si l'instruction s'arrête.</summary>
    internal static void arith_sti(int k, bool inverse, int i, bool pop)
    {
        var faute = vide(0) || vide(i);
        if (lire(0, out var y) || lire(i, out var x))
                return;
        var r = Indefini;
        if (!faute && (inverse ? operation(k, y, x, false, out r) : operation(k, x, y, false, out r)))
                return;
        ecrire(i, r);
        if (pop)
                depiler();
    }

    /// <summary>FSQRT : la racine de ST(0), arrondie selon RC et PC ; un négatif non nul, −∞ compris, IE et
    /// l'indéfini ; −0 rend −0 (SDM vol. 2, FSQRT). Le 8087 et le 287 : DE démasquée d'abord, IE pour tout NaN (N7a) ;
    /// un non normal, un dénormal et +∞ en projectif, IE (N7b).</summary>
    internal static void fsqrt()
    {
        var faute = vide(0);
        if (lire(0, out var x))
                return;
        if (faute)
        {
                ecrire(0, Indefini);
                return;
        }
        var c = classe(x);
        arrondi(true);
        extFloat80_t r;
        if (non_supporte(c))
        {
                r = Indefini;
                softfloat.softfloat_exceptionFlags = softfloat.softfloat_flag_invalid;
        }
        else if (de_d_abord(denormal(c)))
                return;
        else if (seize_bits() && nan_seize(c))
                r = nan_de_seize(x, x);
        else if (non_normal_seize(c) || projectif() && c == Classe.Infini && !negatif(x))
        {
                // Le 8087 et le 287 : la racine d'un non normal ou d'un dénormal, et de +∞ en projectif, IE et l'indéfini
                // (287 PRM, tables 1-9, 1-12 et 1-17 ; PB-59, PB-70).
                ModeMateriel.Sonde[non_normal_seize(c) ? 59 : 70]++;
                r = Indefini;
                softfloat.softfloat_exceptionFlags = softfloat.softfloat_flag_invalid;
        }
        else
        {
                r = softfloat.extF80_sqrt(x);
                if (!nan(c) && (softfloat.softfloat_exceptionFlags & softfloat.softfloat_flag_invalid) == 0 &&
                    denormal(c) && lever(DE))
                        return;
        }
        if (!indicateurs())
                ecrire(0, r);
    }

    /// <summary>Vrai si l'infini est projectif : le 8087 et le 287, IC (bit 12) nul (287 PRM, fig. 1-10 ; Numerics
    /// Supplement, 1980, p. S-18). Le 287XL, le 387 et le 486 n'ont que l'affine (387 PRM, annexe C ; le 287XL : déduit).</summary>
    private static bool projectif() =>
        cpu_c.fpu_type is cpu_c.FPU_8087 or cpu_c.FPU_287 && (cpu_state.npxc & 0x1000) == 0;

    // PB-57, PB-58, PB-64, PB-70, PB-213 (corrigés en G13.6a et 6b, en double) : la comparaison sur 80 bits (SDM vol. 2,
    //   FCOM/FCOMP/FCOMPP, FUCOM/FUCOMP/FUCOMPP, FICOM/FICOMP et FTST ; 387 PRM § 4.5) : C3 C2 C0 = 000 pour a > b, 001
    //   pour a < b, 100 pour a = b (−0 et +0 égaux), 111 non ordonné ; C1 à zéro sur le 287XL, le 387 et le 486, « X »
    //   sur le 8087 et le 287, laissé. Non ordonné : un NaN, IE pour FCOM et ses voisines, pour un SNaN seul avec FUCOM
    //   (`signale` faux) ; un format non supporté, IE. En projectif, ±∞ contre ±∞ est égal, et un infini contre un fini
    //   non comparable, avec IE (287 PRM, table 1-12, « ±∞ : ±∞ A=B » ; Numerics Supplement, 1980, table S-27 et son
    //   texte, « infinity cannot be compared with any value except infinity »). Chacun de ces deux manuels se contredit :
    //   le 287 PRM par sa table 1-17 et le texte de FCOM, le Numerics Supplement par sa table des réponses masquées,
    //   qui comptent ±∞ contre ±∞ invalide, non comparable (C3 C2 C0 = 111 et IE). L'égalité de G13.6a est gardée (C3
    //   seul, sans IE ; inconnu) ; les deux lectures posent C3, que la détection du 287 lit. Un dénormal, DE, si rien ne
    //   passe avant ; démasquée sur le 8087 et le 287, avant tout (de_d_abord). Leur pseudo-infini et leur pseudo-NaN
    //   sont des NaN (nan_seize) ; FUCOM, qu'ils n'ont pas, compare comme celui du 387 (PB-209). Une exception
    //   démasquée arrête l'instruction, les codes laissés (inconnu) ; elle ne dépile pas. `den` : l'opérande en mémoire
    //   était un dénormal (de_f32, de_f64).
    internal static bool comparer(in extFloat80_t a, in extFloat80_t b, bool signale, bool den = false)
    {
        Classe ca = classe(a), cb = classe(b);
        uint16_t cc;
        uint16_t e = 0;
        // Le 8087 et le 287 lèvent DE pour un non normal comparé, le pseudo-zéro excepté (287 PRM, annexe C, point 3 ;
        // table 1-17), et le comparent normalisé (table 1-9 : « Normalize as much as possible before making comparison »).
        var nn = seize_bits() && (ca == Classe.NonNormal && a.signif != 0 || cb == Classe.NonNormal && b.signif != 0);
        if (de_d_abord(den || denormal(ca) || denormal(cb) || nn))
                return true;
        if (non_supporte(ca) || non_supporte(cb))
        {
                e = IE;
                cc = x87_c.C3 | x87_c.C2 | x87_c.C0;
        }
        else if (nan(ca) || nan(cb) || seize_bits() && (nan_seize(ca) || nan_seize(cb)))
        {
                if (signale || ca == Classe.SNaN || cb == Classe.SNaN)
                        e = IE;
                cc = x87_c.C3 | x87_c.C2 | x87_c.C0;
        }
        else if (projectif() && (ca == Classe.Infini || cb == Classe.Infini))
        {
                ModeMateriel.Sonde[70]++;
                if (ca == cb)
                        cc = x87_c.C3;
                else
                {
                        e = IE;
                        cc = x87_c.C3 | x87_c.C2 | x87_c.C0;
                }
        }
        else
        {
                if (den || denormal(ca) || denormal(cb) || nn)
                        e = DE;
                var ac = canonique(pour_softfloat(a));
                var bc = canonique(pour_softfloat(b));
                cc = softfloat.extF80_eq(ac, bc) ? x87_c.C3 : softfloat.extF80_lt_quiet(ac, bc) ? x87_c.C0 : (uint16_t)0;
        }
        if (e != 0 && lever(e))
                return true;
        c1_zero();
        cpu_state.npxs = (uint16_t)((cpu_state.npxs & ~(x87_c.C0 | x87_c.C2 | x87_c.C3)) | cc);
        return false;
    }

    /// <summary>Le pseudo-dénormal (exposant nul, J = 1) vaut le normal d'exposant 1 (387 PRM § 3.1 ; SDM vol. 1, 2003,
    /// § 8.2.2) : la comparaison de SoftFloat, qui lit les bits, le reçoit sous cette forme.</summary>
    private static extFloat80_t canonique(extFloat80_t v)
    {
        if ((v.signExp & 0x7FFF) == 0 && (v.signif >> 63) != 0)
                v.signExp |= 1;
        return v;
    }

    /// <summary>FCOM et FCOMP m : ST(0) contre l'opérande étendu. Rend vrai si l'instruction s'arrête.</summary>
    internal static bool fcom_m(in extFloat80_t m, bool den)
    {
        if (lire(0, out var x))
                return true;
        return comparer(x, m, true, den);
    }

    /// <summary>FCOM, FCOMP, FUCOM, FUCOMP ST(i), FCOMPP et FUCOMPP (i = 1) : ST(0) contre ST(i). Rend vrai si
    /// l'instruction s'arrête.</summary>
    internal static bool fcom_st(int i, bool signale)
    {
        if (lire(0, out var x) || lire(i, out var y))
                return true;
        return comparer(x, y, signale);
    }

    // PB-64 (corrigé en G13.6a, en double) : FTST compare ST(0) à +0, comme FCOM (SDM vol. 2, FTST ; Numerics
    //   Supplement, 1980, table S-27 : en projectif, un infini, non comparable et IE). FTST de −0 : le 387 PRM, table
    //   3-3, donne C1 = 1 (« C3 = C1 = 1 ») ; le SDM, « C1 Set to 0 » ; le SDM est suivi, comme pour PB-213 (inconnu).
    internal static void ftst()
    {
        if (!lire(0, out var x))
                comparer(x, new extFloat80_t(), true);
    }

    // ===== FRNDINT, FSCALE, FXTRACT, FPREM et FPREM1 (N4b) =====
    //
    // Les exceptions (387 PRM, annexe B) : FRNDINT, IS, I, D et P ; FSCALE, IS, I, D, O, U et P ; FXTRACT, IS, I, D et Z ;
    // FPREM et FPREM1, IS, I, D et U — U masquée jamais : le reste est exact, et un reste dénormal exact ne lève pas UE
    // masquée ; démasquée, il la lève et se range à l'exposant décalé (indicateurs, N5). Ni PC ni, pour FPREM et FPREM1, RC n'y jouent (387 PRM § 4.4.9 et § 4.4.10 : « the
    // 80387 produces an exact result »). Les cas des opérandes spéciaux : 387 PRM, tables 3-3 et 3-4. Le 8087 et le 287 :
    // les fonctions le disent (N7a, N7b).

    /// <summary>Un opérande fini non nul, normalisé : le signe, l'exposant sans biais, le significande (bit 63 à 1) ;
    /// un dénormal ou un pseudo-dénormal (exposant nul) vaut 2^−16382 × 0,f.</summary>
    private static (bool s, int e, uint64_t m) normaliser(in extFloat80_t v)
    {
        var e = v.signExp & 0x7FFF;
        if (e != 0)
                return ((v.signExp & 0x8000) != 0, e - 0x3FFF, v.signif);
        // L'exposant d'un dénormal est celui de 1 (2^−16382) ; SoftFloat rend le décalage de la normalisation.
        var n = softfloat.softfloat_normSubnormalExtF80Sig(v.signif);
        return ((v.signExp & 0x8000) != 0, 1 + (int)n.exp - 0x3FFF, n.sig);
    }

    /// <summary>Le NaN de deux opérandes, par la règle de la table 4-7 (SoftFloat, la spécialisation du 8086) : IE pour un
    /// SNaN.</summary>
    private static extFloat80_t nan_de(in extFloat80_t a, in extFloat80_t b)
    {
        var z = softfloat.softfloat_propagateNaNExtF80UI(a.signExp, a.signif, b.signExp, b.signif);
        return new extFloat80_t { signExp = (uint16_t)z.v64, signif = z.v0 };
    }

    // pcem bug, fixed in hardware mode: PB-197 PB-211 PB-265 — FRNDINT : ST(0) arrondi à l'entier selon RC (387 PRM §
    //   4.4.11), au plus près sans l'arrondi de b + 1,0 de PCem, PE et C1 si inexact ; une valeur déjà entière, |x| ≥ 2^63 compris, et un infini restent tels quels ; un zéro, et
    //   un résultat nul, gardent le signe de l'opérande (IEEE 754-1985 § 6.3 ; 387 PRM, table 3-3) ; un SNaN, IE et le
    //   QNaN ; un format non supporté, IE et l'indéfini ; un dénormal, DE (SoftFloat, extF80_roundToInt). Le 8087 et le
    //   287 : DE démasquée d'abord, IE pour tout NaN (N7a).
    internal static void frndint()
    {
        var faute = vide(0);
        if (lire(0, out var x))
                return;
        if (faute)
        {
                ecrire(0, Indefini);
                return;
        }
        var c = classe(x);
        arrondi(false);
        extFloat80_t r;
        if (non_supporte(c))
        {
                r = Indefini;
                softfloat.softfloat_exceptionFlags = softfloat.softfloat_flag_invalid;
        }
        else if (de_d_abord(denormal(c)))
                return;
        else if (seize_bits() && nan_seize(c))
                r = nan_de_seize(x, x);
        else
        {
                // Le 8087 et le 287 normalisent un non normal le plus possible avant d'arrondir, un pseudo-zéro rend son zéro
                // (287 PRM, table 1-9 et « Zeros and pseudo zeros »).
                if (non_normal_seize(c))
                        x = normal80(x);
                r = softfloat.extF80_roundToInt(x, softfloat.softfloat_roundingMode, true);
                if (denormal(c) && lever(DE))
                        return;
                if ((x.signExp & 0x7FFF) >= 0x3FFF + 63)
                        ModeMateriel.Sonde[197]++;
                if (r.signExp == 0x8000 && r.signif == 0)
                        ModeMateriel.Sonde[211]++;
                if (softfloat.softfloat_roundingMode == softfloat.softfloat_round_near_even && (x.signExp & 0x7FFF) < 0x3FFF + 63)
                        ModeMateriel.Sonde[265]++;
        }
        if (!indicateurs())
                ecrire(0, r);
    }

    // pcem bug, fixed in hardware mode: PB-198 — FSCALE : ST(0) × 2^n, n la valeur de ST(1) tronquée vers zéro, sans
    //   limite (387 PRM § 4.4.8) ; arrondi selon RC, à 64 bits (PC n'y joue pas : SDM vol. 1, 2003, § 8.1.4.2), O, U,
    //   P et C1. Les spéciaux (387 PRM, tables 3-3 et 3-4) : ST(1) = −∞ rend ±0 d'un fini, IE d'un infini ; ST(1) = +∞
    //   rend ±∞ d'un fini non nul, IE d'un zéro ; un infini ou un zéro en ST(0), sinon, reste tel quel ; un NaN, la règle
    //   des NaN ; un format non supporté, IE. Un dénormal, DE. Le 8087 et le 287 (287 PRM, table 1-12) : par ±∞, ±0 rend
    //   ±0, tout autre opérande IE (∞ par ∞ compris) ; DE démasquée d'abord, IE pour tout NaN, leurs dépassements (N7a).
    //   Hors de leur domaine (−2^15 ≤ ST(1) < 2^15, et 0 < |ST(1)| < 1), « undefined » sans exception (287 PRM, FSCALE ;
    //   387 PRM, annexe C § C.4) : le noyau rend la valeur du 387 (un choix ; la valeur du silicium, inconnue).
    internal static void fscale()
    {
        var faute = vide(0) || vide(1);
        if (lire(0, out var x) || lire(1, out var y))
                return;
        if (faute)
        {
                ecrire(0, Indefini);
                return;
        }
        Classe cx = classe(x), cy = classe(y);
        arrondi(false);
        extFloat80_t r;
        if (non_supporte(cx) || non_supporte(cy))
        {
                r = Indefini;
                softfloat.softfloat_exceptionFlags = softfloat.softfloat_flag_invalid;
        }
        else if (de_d_abord(denormal(cx) || denormal(cy)))
                return;
        else if (seize_bits() && (nan_seize(cx) || nan_seize(cy)))
                r = nan_de_seize(x, y);
        else if (nan(cx) || nan(cy))
        {
                ModeMateriel.Sonde[198] += nan(cy) ? 1 : 0;
                r = nan_de(x, y);
        }
        else if (cy == Classe.Infini && seize_bits())
        {
                ModeMateriel.Sonde[198]++;
                if (cx == Classe.Zero)
                        r = x;
                else
                {
                        r = Indefini;
                        softfloat.softfloat_exceptionFlags = softfloat.softfloat_flag_invalid;
                }
        }
        else if (cy == Classe.Infini)
        {
                ModeMateriel.Sonde[198]++;
                var moins = (y.signExp & 0x8000) != 0;
                if (moins ? cx == Classe.Infini : cx == Classe.Zero)
                {
                        r = Indefini;
                        softfloat.softfloat_exceptionFlags = softfloat.softfloat_flag_invalid;
                }
                else if (denormal(cx) && lever(DE))
                        return;
                else
                        r = cx is Classe.Infini or Classe.Zero ? x
                          : new extFloat80_t { signExp = (uint16_t)((x.signExp & 0x8000) | (moins ? 0 : 0x7FFF)), signif = moins ? 0 : 0x8000000000000000 };
        }
        else
        {
                if ((denormal(cx) || denormal(cy)) && lever(DE))
                        return;
                if (cx is Classe.Infini or Classe.Zero)
                        r = x;
                else if (non_normal_seize(cx))
                {
                        // Le 8087 et le 287 ajoutent l'échelle à l'exposant, le significande gardé, non normal compris (287 PRM,
                        // FSCALE : « adds this value to the exponent » ; déduit ; PB-198).
                        var (s, e, m) = brut80(x);
                        var n = tronque(y);
                        r = m == 0 ? pseudo_zero(s, e + n) : ranger_seize(s, grand(m), BigInteger.One, e + n - 63, e + n, false);
                }
                else
                {
                        var (s, e, m) = normaliser(x);
                        r = seize_arrondi(s, e + 0x3FFF + tronque(y), m, 0, 80);
                        seize_depassement(ref r);
                }
        }
        if (!indicateurs())
                ecrire(0, r);
    }

    /// <summary>ST(1) tronqué vers zéro, borné à ±2^20 : au-delà, le résultat déborde de toute façon, par excès ou par
    /// défaut (l'exposant de 80 bits tient en 15 bits, un dénormal en 64 de plus).</summary>
    private static int tronque(in extFloat80_t y)
    {
        if (classe(y) == Classe.Zero)
                return 0;
        var (s, e, m) = normaliser(y);
        // L'exposant du bit de tête : celui d'un non normal du 8087 et du 287 est sous son exposant.
        var tete = m == 0 ? -1 : e - System.Numerics.BitOperations.LeadingZeroCount(m);
        if (tete < 0)
                return 0;
        var n = tete >= 20 ? 1 << 20 : (int)(e >= 63 ? m << (e - 63) : m >> (63 - e));
        return s ? -n : n;
    }

    // pcem bug, fixed in hardware mode: PB-194 — FXTRACT (387 PRM § 4.4.12 ; SDM vol. 2, FXTRACT) : l'exposant sans biais
    //   de ST(0), en réel, remplace ST(0), puis son significande, l'exposant mis à 3FFFh et le signe gardé, est empilé.
    //   ST(7) doit être vide. Un zéro : ZE, ST(1) = −∞ et ST(0) le zéro ; un infini : ST(1) = +∞ et ST(0) l'infini, sans
    //   exception (387 PRM, tables 3-3 et 3-4) ; un NaN, rendu silencieux, aux deux ; un format non supporté, IE et
    //   l'indéfini aux deux ; un dénormal, DE, normalisé. Une faute de pile masquée : l'indéfini aux deux (inconnu). Le
    //   8087 et le 287 (387 PRM, annexe C § C.2 et § C.4 ; 287 PRM, FXTRACT et table 1-12) : un zéro rend deux zéros
    //   du signe de l'opérande, sans exception ; un infini, IE et l'indéfini aux deux ; un NaN, IE (nan_de_seize) ; ni
    //   DE ni ZE ; le dénormal et le non normal, leur exposant et leur significande tels quels (N7b).
    internal static void fxtract()
    {
        c1_zero();
        if (vide(0) || plein())
        {
                if (faute_de_pile(!vide(0)))
                        return;
                ecrire(0, Indefini);
                empiler(Indefini);
                return;
        }
        var x = R[phys(0)];
        ModeMateriel.Sonde[194]++;
        var c = classe(x);
        arrondi(false);
        extFloat80_t ex, sig;
        if (seize_bits() && nan_seize(c))
                ex = sig = nan_de_seize(x, x);
        else if (non_supporte(c) || nan(c))
        {
                softfloat.softfloat_exceptionFlags = non_supporte(c) || c == Classe.SNaN ? softfloat.softfloat_flag_invalid : (byte)0;
                ex = sig = non_supporte(c) ? Indefini : nan_de(x, x);
        }
        else if (c == Classe.Zero && seize_bits())
                ex = sig = x;
        else if (c == Classe.Zero)
        {
                if (lever(ZE))
                        return;
                ex = new extFloat80_t { signExp = 0xFFFF, signif = 0x8000000000000000 };
                sig = x;
        }
        else if (c == Classe.Infini && seize_bits())
        {
                softfloat.softfloat_exceptionFlags = softfloat.softfloat_flag_invalid;
                ex = sig = Indefini;
        }
        else if (c == Classe.Infini)
        {
                ex = new extFloat80_t { signExp = 0x7FFF, signif = 0x8000000000000000 };
                sig = x;
        }
        else if (non_normal_seize(c))
        {
                // Le 8087 et le 287 : l'exposant et le significande tels quels, le dénormal pris pour son non normal
                // équivalent (287 PRM, FXTRACT : « its significand is identical to the original operand's » ; déduit pour
                // le non normal et le dénormal ; PB-194).
                ModeMateriel.Sonde[194]++;
                var (s, e, m) = brut80(x);
                ex = softfloat.i32_to_extF80(e);
                sig = new extFloat80_t { signExp = (uint16_t)((s ? 0x8000 : 0) | 0x3FFF), signif = m };
        }
        else
        {
                if (denormal(c) && !seize_bits() && lever(DE))
                        return;
                var (s, e, m) = normaliser(x);
                ex = softfloat.i32_to_extF80(e);
                sig = new extFloat80_t { signExp = (uint16_t)((s ? 0x8000 : 0) | 0x3FFF), signif = m };
        }
        if (indicateurs())
                return;
        ecrire(0, ex);
        empiler(sig);
    }

    // pcem bug, fixed in hardware mode: PB-65 — FPREM et FPREM1 (387 PRM § 4.4.9 et § 4.4.10 ; SDM vol. 2, FPREM et
    //   FPREM1) : le reste exact de ST(0) par ST(1), le quotient tronqué vers zéro (FPREM) ou au plus près, au pair
    //   (FPREM1) ; le reste a le signe du dividende (FPREM), ou celui que le quotient au plus près lui donne (FPREM1), et
    //   un reste nul celui du dividende (table 3-3). Si les exposants diffèrent de 64 ou plus, une réduction partielle :
    //   ST(0) − ST(1) × Q × 2^(D − N), Q tronqué, C2 = 1, et C0, C3, C1 à zéro (SDM vol. 3, 2016, § 22.18.2.1 ; le 8087
    //   et le 287 les laissent, 387 PRM, annexe C § C.3). N, « an implementation-dependent number between 32 and 63 »
    //   (SDM vol. 2) : 32 + (D mod 32), un choix (inconnu pour le 387 et le 486). Complète, C2 = 0 et les trois bits bas
    //   du quotient en C0 (Q2), C3 (Q1) et C1 (Q0) (387 PRM, table 4-4). Le 8087 et le 287 : les mêmes bits. Leurs sources
    //   se contredisent sur un défaut de ces bits — le 287 PRM, p. 2-8 : « If the quotient is less than 4, then C0 will be
    //   the value of C3 before FPREM was executed », que sa table 2-4 contredit ; le 387 PRM, annexe C § C.4 : « incorrect
    //   when performing a reduction of 64N + M when N ≥ 1 and M = 1 or M = 2 » — : faute de silicium, les bits justes,
    //   ceux de PCem, sont gardés (c). Un
    //   diviseur nul ou un dividende infini : IE, l'indéfini et C2 (387 PRM, table 3-10 ; le 8087 et le 287, C2 effacé,
    //   « set condition code = complete remainder », 287 PRM, table 1-17, les autres codes laissés : inconnu, et de même
    //   pour un NaN) ; un diviseur infini : le dividende, Q = 0 ; un dividende nul : lui-même ; un NaN, la règle des NaN
    //   (IE pour tout NaN sur le 8087 et le 287) ; un format non supporté, IE ; un dénormal, DE (démasquée sur le 8087 et
    //   le 287, avant tout). Ni PE ni RC. FPREM1 du 8087 et du 287, qu'ils n'ont pas : celui du 387 (PB-209).
    internal static void fprem(bool ieee)
    {
        var faute = vide(0) || vide(1);
        if (lire(0, out var x) || lire(1, out var y))
                return;
        ModeMateriel.Sonde[65]++;
        const uint16_t CODES = x87_c.C0 | x87_c.C1 | x87_c.C2 | x87_c.C3;
        uint16_t cc = 0;
        var invalide = false;
        extFloat80_t r;
        Classe cx = classe(x), cy = classe(y);
        arrondi(false);
        if (faute)
                r = Indefini;
        else if (de_d_abord(denormal(cx) || denormal(cy)))
                return;
        else if (seize_bits() && (nan_seize(cx) || nan_seize(cy)))
        {
                r = nan_de_seize(x, y);
                invalide = true;
        }
        else if (non_normal_seize(cy) && cx != Classe.Infini)
        {
                // Le 8087 et le 287 : un modulo non normal ou dénormal, IE, l'indéfini, « complete remainder » (287 PRM,
                // tables 1-9 et 1-17 ; PB-59).
                ModeMateriel.Sonde[59]++;
                r = Indefini;
                invalide = true;
                softfloat.softfloat_exceptionFlags = softfloat.softfloat_flag_invalid;
        }
        // Un pseudo-zéro diviseur (exposant non nul, significande nul : le 8087 et le 287 le prennent) compte pour un zéro.
        else if (non_supporte(cx) || non_supporte(cy) ||
                 !nan(cx) && !nan(cy) && (cx == Classe.Infini || cy == Classe.Zero || cy != Classe.Infini && y.signif == 0))
        {
                r = Indefini;
                cc = x87_c.C2;
                invalide = true;
                softfloat.softfloat_exceptionFlags = softfloat.softfloat_flag_invalid;
        }
        else if (nan(cx) || nan(cy))
                r = nan_de(x, y);
        else
        {
                if ((denormal(cx) || denormal(cy)) && lever(DE))
                        return;
                // Le 8087 et le 287 : un dividende non normal ou dénormal rend un reste normalisé (287 PRM, table 1-9).
                if (non_normal_seize(cx))
                {
                        x = normal80(x);
                        cx = classe(x);
                }
                if (cx == Classe.Zero || x.signif == 0 || cy == Classe.Infini)
                        r = cx == Classe.Denormal ? exact(x) : x;
                else
                        r = reste(x, y, ieee, out cc);
        }
        if (indicateurs())
                return;
        if (!seize_bits())
                cpu_state.npxs = (uint16_t)((cpu_state.npxs & ~CODES) | cc);
        else if (invalide)
                cpu_state.npxs &= unchecked((uint16_t)~x87_c.C2);
        else if ((cc & x87_c.C2) != 0)
                cpu_state.npxs |= x87_c.C2;
        else
                cpu_state.npxs = (uint16_t)((cpu_state.npxs & ~CODES) | cc);
        ecrire(0, r);
    }

    /// <summary>Un dividende dénormal rendu tel quel (FPREM et FPREM1, Q = 0) : repassé par l'arrondi, qui le rend
    /// identique, exact, mais lève UE démasquée et le range décalé, comme tout résultat petit (indicateurs).</summary>
    private static extFloat80_t exact(in extFloat80_t x)
    {
        var (s, e, m) = normaliser(x);
        return softfloat.softfloat_roundPackToExtF80(s, e + 0x3FFF, m, 0, 80);
    }

    /// <summary>Le reste de deux finis non nuls, exact (FPREM, FPREM1) ; `cc`, C2 et les bits du quotient.</summary>
    private static extFloat80_t reste(in extFloat80_t x, in extFloat80_t y, bool ieee, out uint16_t cc)
    {
        var (sx, ex, mx) = normaliser(x);
        var (_, ey, my) = normaliser(y);
        var d = ex - ey;
        var partielle = d >= 64;
        if (partielle)
        {
                var n = 32 + d % 32;
                ey += d - n;
                d = n;
        }
        // |x| < |y| / 2 : Q = 0 pour les deux, le dividende rendu.
        if (d < -1)
        {
                cc = 0;
                return classe(x) == Classe.Denormal ? exact(x) : x;
        }
        // x = num × 2^(ey − 64), y = den × 2^(ey − 64) : un bit de plus, pour D = −1 (FPREM1, |x| ≥ |y| / 2).
        UInt128 num = (UInt128)mx << (d + 1), den = (UInt128)my << 1;
        UInt128 q = num / den, rr = num % den;
        var signe = sx;
        if (ieee && !partielle && (rr << 1 > den || rr << 1 == den && (q & 1) != 0))
        {
                q++;
                rr = den - rr;
                signe = !sx;
        }
        cc = partielle ? x87_c.C2
           : (uint16_t)(((q & 4) != 0 ? x87_c.C0 : 0) | ((q & 2) != 0 ? x87_c.C3 : 0) | ((q & 1) != 0 ? x87_c.C1 : 0));
        if (rr == 0)
                return new extFloat80_t { signExp = (uint16_t)(sx ? 0x8000 : 0) };
        // rr < 2^65 : son bit de tête p, la valeur 1,f × 2^(p + ey − 64).
        var p = 127 - (int)UInt128.LeadingZeroCount(rr);
        var m = p >= 63 ? (uint64_t)(rr >> (p - 63)) : (uint64_t)(rr << (63 - p));
        return softfloat.softfloat_roundPackToExtF80(signe, p + ey - 64 + 0x3FFF, m, 0, 80);
    }

    // ===== Les transcendantes (N6 ; le calcul : x87_transcendantes.Materiel.cs) =====

    private static extFloat80_t infini(bool neg) => new() { signExp = (uint16_t)(neg ? 0xFFFF : 0x7FFF), signif = 0x8000000000000000 };

    private static extFloat80_t zero(bool neg) => new() { signExp = (uint16_t)(neg ? 0x8000 : 0) };

    private static bool negatif(in extFloat80_t v) => (v.signExp & 0x8000) != 0;

    /// <summary>IE, et l'indéfini.</summary>
    private static extFloat80_t invalide()
    {
        softfloat.softfloat_exceptionFlags |= softfloat.softfloat_flag_invalid;
        return Indefini;
    }

    // pcem bug, fixed in hardware mode: PB-68 — le 8087 et le 287 n'examinent pas les opérandes de leurs transcendantes
    //   (287 PRM, « Transcendental Instructions » : « If a transcendental operand is invalid or out-of-range, the
    //   instruction will produce an undefined result without signalling an exception » ; ses pages d'instructions :
    //   FPTAN, I et P ; FPATAN et F2XM1, U et P ; FYL2X et FYL2XP1, P ; « operands not checked » ; 387 PRM, annexe C §
    //   C.2 : pas de DE) : ni IE, hors de la faute de pile, ni ZE, ni DE. La valeur rendue, indéfinie sur le silicium, est
    //   celle du 387 (un choix), et de même hors de leurs domaines étroits (F2XM1, 0 ≤ x ≤ 0,5 ; FYL2X, 0 < x < ∞ ;
    //   FYL2XP1, 0 ≤ |x| < 1 − √2/2 ; FPTAN, 0 ≤ x ≤ π/4 ; FPATAN, 0 ≤ y < x < ∞ : 287 PRM, table 2-9 et ses pages). Avant
    //   indicateurs.
    private static void non_examines()
    {
        if (!seize_bits())
                return;
        const byte IZ = softfloat.softfloat_flag_invalid | softfloat.softfloat_flag_infinite;
        if ((softfloat.softfloat_exceptionFlags & IZ) != 0)
                ModeMateriel.Sonde[68]++;
        softfloat.softfloat_exceptionFlags &= unchecked((byte)~IZ);
    }

    // pcem bug, fixed in hardware mode: PB-68 — F2XM1 : 2^ST(0) − 1 sur 80 bits, arrondi une fois selon RC (PC n'y joue
    //   pas : SDM vol. 1, 2003, § 8.1.4.2), au lieu de pow(2, x) − 1 en double, qui perd toute précision relative pour x
    //   petit. ±0 rend ±0 (SDM vol. 2, 2016, table 3-16) ; un SNaN, IE et le QNaN ; un format non supporté, IE et
    //   l'indéfini ; un dénormal, DE, puis le calcul (x ln 2, petit : UE masquée et PE). Hors de [−1, 1], le résultat du
    //   silicium est indéfini (SDM vol. 2, F2XM1) : le noyau rend la même valeur mathématique, +∞ de +∞ et −1 de −∞
    //   (un choix). Le 8087 et le 287 : non_examines.
    internal static void f2xm1()
    {
        var faute = vide(0);
        if (lire(0, out var x))
                return;
        if (faute)
        {
                ecrire(0, Indefini);
                return;
        }
        var c = classe(x);
        arrondi(false);
        extFloat80_t r;
        if (non_supporte(c))
                r = invalide();
        else if (nan(c))
                r = nan_de(x, x);
        else if (c == Classe.Zero)
                r = x;
        else if (c == Classe.Infini)
                r = negatif(x) ? new extFloat80_t { signExp = 0xBFFF, signif = 0x8000000000000000 } : x;
        else
        {
                if (denormal(c) && !seize_bits() && lever(DE))
                        return;
                ModeMateriel.Sonde[68]++;
                var v = _x87_transcendantes.exp2m1(x, out var exact);
                r = _x87_transcendantes.arrondir(v, !exact);
        }
        non_examines();
        if (!indicateurs())
                ecrire(0, r);
    }

    // pcem bug, fixed in hardware mode: PB-68 — FYL2X, ST(1) × log₂ ST(0), et FYL2XP1, ST(1) × log₂(ST(0) + 1) (`p1`),
    //   rangés dans ST(1), puis la pile dépilée : sur 80 bits, le produit arrondi une seule fois selon RC (PC n'y joue
    //   pas), au lieu de log(x) / log(2) en double, et de log(x + 1,0), qui perd tout x petit. Les spéciaux sont ceux des
    //   tables 3-48 et 3-49 du SDM vol. 2, 2016 : FYL2X d'un x négatif (−0 excepté), IE ; de ±0, ZE et ∓∞ par un y fini
    //   non nul, ±∞ par ∓∞, IE par ±0 ; de +∞, IE par ±0 ; de +1, IE par ±∞ ; sinon, un zéro ou un infini de y prend le
    //   signe de y fois celui de log₂ x. FYL2XP1 de ±0, le zéro du produit, IE par ±∞. Un NaN, la règle des NaN ; un
    //   format non supporté, IE ; un dénormal, DE, sauf si IE ou ZE passe avant (SDM vol. 1, 2003, § 4.9.2, comme les
    //   quatre opérations). FYL2XP1 hors de |x| < 1 − √2/2 : le résultat du silicium est indéfini (SDM vol. 2,
    //   FYL2XP1 : « software should not rely on an exception being generated ») ; le noyau rend la même valeur
    //   mathématique, et, pour x ≤ −1, ce que FYL2X rend de 1 + x (x = −1 : le cas de ±0 ; x < −1, IE), un choix. Le
    //   8087 et le 287 : non_examines.
    internal static void fyl2x(bool p1)
    {
        var faute = vide(0) || vide(1);
        if (lire(0, out var x) || lire(1, out var y))
                return;
        var r = Indefini;
        if (!faute && logarithme(p1, x, y, out r))
                return;
        ecrire(1, r);
        depiler();
    }

    /// <summary>y × log₂ x, ou y × log₂(1 + x) (`p1`) ; vrai si l'instruction s'arrête sans rien ranger.</summary>
    private static bool logarithme(bool p1, in extFloat80_t x, in extFloat80_t y, out extFloat80_t r)
    {
        Classe cx = classe(x), cy = classe(y);
        bool sx = negatif(x), sy = negatif(y);
        arrondi(false);
        // x = −1 pour FYL2XP1 : 1 + x vaut ±0, le cas de FYL2X d'un zéro.
        var moinsUn = p1 && x.signExp == 0xBFFF && x.signif == 0x8000000000000000;
        if (non_supporte(cx) || non_supporte(cy))
                r = invalide();
        else if (nan(cx) || nan(cy))
                r = nan_de(x, y);
        else if (cx == Classe.Zero && !p1 || moinsUn)
        {
                if (cy == Classe.Zero)
                        r = invalide();
                else
                {
                        r = infini(!sy);
                        if (cy != Classe.Infini)
                                softfloat.softfloat_exceptionFlags |= softfloat.softfloat_flag_infinite;
                }
        }
        else if (cx == Classe.Zero)
                r = cy == Classe.Infini ? invalide() : zero(sx != sy);
        else if (sx && (!p1 || cx == Classe.Infini || (x.signExp & 0x7FFF) > 0x3FFF ||
                        (x.signExp & 0x7FFF) == 0x3FFF && x.signif > 0x8000000000000000))
                r = invalide();
        else if (cx == Classe.Infini)
                r = cy == Classe.Zero ? invalide() : infini(sy);
        else
        {
                ModeMateriel.Sonde[68]++;
                var l = p1 ? _x87_transcendantes.log2_1p(x, out var exact) : _x87_transcendantes.log2(_x87_transcendantes.de(x), out exact);
                if (l.m == 0)
                        r = cy == Classe.Infini ? invalide() : zero(sy);
                else if (cy == Classe.Infini)
                        r = infini(sy != l.s);
                else if (cy == Classe.Zero)
                        r = zero(sy != l.s);
                else
                        r = _x87_transcendantes.arrondir(_x87_transcendantes.mul(_x87_transcendantes.de(y), l), !exact);
        }
        if ((softfloat.softfloat_exceptionFlags & (softfloat.softfloat_flag_invalid | softfloat.softfloat_flag_infinite)) == 0 &&
            !nan(cx) && !nan(cy) && (denormal(cx) || denormal(cy)) && !seize_bits() && lever(DE))
                return true;
        non_examines();
        return indicateurs();
    }

    private static readonly extFloat80_t Un = new() { signExp = 0x3FFF, signif = 0x8000000000000000 };

    /// <summary>|x| ≥ 2^63 (x fini, normal) : hors de la borne de FSIN, FCOS, FSINCOS et FPTAN.</summary>
    private static bool hors_borne(in extFloat80_t x) => (x.signExp & 0x7FFF) >= 0x3FFF + 63;

    // pcem bug, fixed in hardware mode: PB-68 — FSIN et FCOS (`f` : 0, 1) : |x| ≥ 2^63, C2 = 1 et ST(0) inchangé, sans
    //   exception (SDM vol. 2, 2016, FSIN et FCOS ; 387 PRM § 4.6.1 et § 4.6.2) ; sinon C2 = 0 et le résultat sur 80 bits,
    //   l'argument réduit exactement par le Pi du silicium (x87_transcendantes.reduire), arrondi une fois selon RC (PC n'y
    //   joue pas), au lieu de sin et cos de la libm en double, qui réduisent tout argument par le vrai π. ±0 : sin ±0,
    //   cos +1 ; ±∞, IE, l'indéfini et C2 = 1 (387 PRM, table 3-10 ; 486 PRM, la même table : « set C2 » ; le SDM vol. 1,
    //   2016, dit C2 effacé : le noyau suit les manuels du 387 et du 486, comme pour FPREM, N4b) ; un SNaN, IE et le QNaN ;
    //   un format non supporté, IE ; un dénormal, DE, puis le calcul ; un résultat petit, UE (387 PRM § 3.2.5.2 compte
    //   FSIN, FCOS et FSINCOS parmi ceux qui la lèvent). FSIN, FCOS et FSINCOS n'existent ni sur le 8087 ni sur le 287
    //   (387 PRM, annexe C) : ce que le silicium fait de leurs octets est inconnu, et ils calculent comme sur le 387
    //   (PB-266, reproduit ; décision n° 14).
    internal static void fsin_cos(int f)
    {
        var faute = vide(0);
        if (lire(0, out var x))
                return;
        cpu_state.npxs &= unchecked((uint16_t)~x87_c.C2);
        if (faute)
        {
                ecrire(0, Indefini);
                return;
        }
        var c = classe(x);
        arrondi(false);
        extFloat80_t r;
        if (non_supporte(c))
                r = invalide();
        else if (c == Classe.Infini)
        {
                r = invalide();
                cpu_state.npxs |= x87_c.C2;
        }
        else if (nan(c))
                r = nan_de(x, x);
        else if (c == Classe.Zero)
                r = f == 0 ? x : Un;
        else if (hors_borne(x))
        {
                ModeMateriel.Sonde[68]++;
                cpu_state.npxs |= x87_c.C2;
                indicateurs();
                return;
        }
        else
        {
                if (denormal(c) && lever(DE))
                        return;
                ModeMateriel.Sonde[68]++;
                r = _x87_transcendantes.arrondir(_x87_transcendantes.trigo(f, x), true);
        }
        if (!indicateurs())
                ecrire(0, r);
    }

    // pcem bug, fixed in hardware mode: PB-68 — FPTAN (`sincos` faux) : tan ST(0) rangé dans ST(0), puis 1,0 empilé
    //   (SDM vol. 2, 2016, FPTAN) ; FSINCOS (`sincos`) : sin ST(0) rangé dans ST(0), puis cos ST(0) empilé (FSINCOS). |x|
    //   ≥ 2^63 : C2 = 1, rien de rangé ni d'empilé ; sinon C2 = 0, le calcul comme FSIN (réduction exacte par Pi, un
    //   arrondi selon RC) ; C1, celui du cosinus pour FSINCOS (inconnu : deux résultats, un C1). ±0 : ±0 et 1,0. ±∞, IE
    //   et C2 = 1, comme FSIN ; un format non supporté, IE ; un NaN, rendu silencieux : l'indéfini, ou le NaN, aux deux
    //   places (inconnu, comme FXTRACT). ST(7) doit être vide : la faute de pile masquée range l'indéfini aux deux places,
    //   avant tout examen de x (387 PRM, annexe C, FPTAN : « both ST and ST(1) contain quiet NaNs » ; FSINCOS, par
    //   analogie). Le 8087 et le 287 : FPTAN rend un rapport Y / X (287 PRM, FPTAN : « The result of the operation is a
    //   ratio ») ; le noyau garde tan x et 1,0, un rapport juste (les Y et X du silicium : inconnus) ; C2 indéfini,
    //   laissé ; après un débordement de pile masqué, « the original operand remains unchanged, but is pushed to ST(1) »
    //   (387 PRM, annexe C § C.4), l'indéfini en ST(0) ; ni DE ni examen des opérandes (non_examines). FSINCOS, qu'ils
    //   n'ont pas, calcule comme celui du 387 (PB-266, reproduit).
    internal static void fptan(bool sincos)
    {
        c1_zero();
        // Le 8087 et le 287 : C2 indéfini pour FPTAN (387 PRM, annexe C § C.3), laissé.
        var seize = seize_bits() && !sincos;
        var c2 = (uint16_t)(cpu_state.npxs & x87_c.C2);
        if (vide(0) || plein())
        {
                if (faute_de_pile(!vide(0)))
                        return;
                if (seize && !vide(0))
                {
                        // L'opérande gardé, poussé en ST(1) ; l'indéfini en ST(0).
                        ModeMateriel.Sonde[68]++;
                        var p = (cpu_state.TOP - 1) & 7;
                        cpu_state.TOP = p;
                        R[p] = Indefini;
                        etiqueter(p, SPECIAL);
                        return;
                }
                if (!seize)
                        cpu_state.npxs &= unchecked((uint16_t)~x87_c.C2);
                ecrire(0, Indefini);
                empiler(Indefini);
                return;
        }
        cpu_state.npxs &= unchecked((uint16_t)~x87_c.C2);
        var x = R[phys(0)];
        var c = classe(x);
        arrondi(false);
        extFloat80_t r0, r1;
        if (non_supporte(c))
                r0 = r1 = invalide();
        else if (c == Classe.Infini)
        {
                r0 = r1 = invalide();
                cpu_state.npxs |= x87_c.C2;
        }
        else if (nan(c))
                r0 = r1 = nan_de(x, x);
        else if (c == Classe.Zero)
        {
                r0 = x;
                r1 = Un;
        }
        else if (hors_borne(x))
        {
                ModeMateriel.Sonde[68]++;
                cpu_state.npxs = (uint16_t)((cpu_state.npxs & ~x87_c.C2) | (seize ? c2 : x87_c.C2));
                indicateurs();
                return;
        }
        else
        {
                if (denormal(c) && !seize && lever(DE))
                        return;
                ModeMateriel.Sonde[68]++;
                if (sincos)
                {
                        r0 = _x87_transcendantes.arrondir(_x87_transcendantes.trigo(0, x), true);
                        r1 = _x87_transcendantes.arrondir(_x87_transcendantes.trigo(1, x), true);
                }
                else
                {
                        r0 = _x87_transcendantes.arrondir(_x87_transcendantes.trigo(2, x), true);
                        r1 = Un;
                }
        }
        if (seize)
        {
                non_examines();
                cpu_state.npxs = (uint16_t)((cpu_state.npxs & ~x87_c.C2) | c2);
        }
        if (indicateurs())
                return;
        ecrire(0, r0);
        empiler(r1);
    }

    // pcem bug, fixed in hardware mode: PB-68 — FPATAN : l'angle du point (ST(0), ST(1)), de −π à π, du signe de ST(1),
    //   rangé dans ST(1), puis la pile dépilée (SDM vol. 2, 2016, FPATAN), sur 80 bits, arrondi une fois selon RC (PC n'y
    //   joue pas), au lieu d'atan2 de la libm en double. Les spéciaux de la table 3-30 : ±∞ en ST(1), ±π/2, ou ±π/4 et
    //   ±3π/4 contre +∞ et −∞ ; ±0 en ST(1), ±0 contre un ST(0) positif (+0 compris), ±π contre un négatif (−0
    //   compris) ; un fini en ST(1), ±0 contre +∞, ±π contre −∞, ±π/2 contre ±0 — sans IE pour 0/0 ni ∞/∞ (la note de la
    //   table). Un NaN, la règle des NaN ; un format non supporté, IE ; un dénormal, DE. Le 8087 et le 287 :
    //   non_examines.
    internal static void fpatan()
    {
        var faute = vide(0) || vide(1);
        if (lire(0, out var x) || lire(1, out var y))
                return;
        var r = Indefini;
        if (!faute && arctangente(x, y, out r))
                return;
        ecrire(1, r);
        depiler();
    }

    /// <summary>atan(y / x), x = ST(0), y = ST(1) ; vrai si l'instruction s'arrête sans rien ranger.</summary>
    private static bool arctangente(in extFloat80_t x, in extFloat80_t y, out extFloat80_t r)
    {
        Classe cx = classe(x), cy = classe(y);
        bool sx = negatif(x), sy = negatif(y);
        arrondi(false);
        // Le 8087 et le 287, qui n'examinent pas leurs opérandes, prennent un pseudo-zéro pour un zéro, et le pseudo-infini
        // et le pseudo-NaN pour des NaN (nan_seize) : la division exacte d'arctan ne reçoit jamais un significande nul.
        if (seize_bits())
        {
                if (cx == Classe.NonNormal && x.signif == 0)
                        cx = Classe.Zero;
                if (cy == Classe.NonNormal && y.signif == 0)
                        cy = Classe.Zero;
        }
        if (non_supporte(cx) || non_supporte(cy))
                r = invalide();
        else if (seize_bits() && (nan_seize(cx) || nan_seize(cy)))
                r = nan_de_seize(x, y);
        else if (nan(cx) || nan(cy))
                r = nan_de(x, y);
        else if (cy == Classe.Infini)
                r = _x87_transcendantes.arrondir(_x87_transcendantes.angle(cx != Classe.Infini ? 2 : sx ? 3 : 1, sy), true);
        else if (cy == Classe.Zero || cx == Classe.Infini)
                r = sx ? _x87_transcendantes.arrondir(_x87_transcendantes.angle(4, sy), true) : zero(sy);
        else if (cx == Classe.Zero)
                r = _x87_transcendantes.arrondir(_x87_transcendantes.angle(2, sy), true);
        else
        {
                ModeMateriel.Sonde[68]++;
                r = _x87_transcendantes.arrondir(_x87_transcendantes.arctan(y, x), true);
        }
        if ((softfloat.softfloat_exceptionFlags & softfloat.softfloat_flag_invalid) == 0 && !nan(cx) && !nan(cy) &&
            (denormal(cx) || denormal(cy)) && !seize_bits() && lever(DE))
                return true;
        non_examines();
        return indicateurs();
    }

    // ===== FNINIT, la mise sous tension, le RESET =====

    /// <summary>L'état de FNINIT (387 PRM, table 6-1 ; 287 PRM, table 3-1 ; 486 PRM, table 19-1) : le mot de contrôle
    /// 037Fh (03FFh sur le 8087, IEM posé, comme PCem), le mot d'état à zéro, TOP à zéro, les huit étiquettes vides ;
    /// les registres gardés ; les pointeurs gardés, sauf sur le 486, qui les efface. Le 287 : sa table 3-1 dit IEM à 1
    /// (03FFh), sa figure 1-5 fait du bit 7 un bit réservé ; la source se contredit, 037Fh, la valeur de PCem, est
    /// gardée (inconnu).</summary>
    // pcem bug, fixed in hardware mode: PB-202 — le 8087 et le 287 gardent C3-C0 après FINIT, et donc après FSAVE et le
    //   RESET (387 PRM, annexe C § C.3 : « After FINIT, incomplete FPREM, and hardware reset, the 80287/8087 leaves
    //   these bits intact » ; SDM vol. 3, 2016, § 22.18.2.1) : le mot d'état passe par le masque 4700h. Le 287XL, un cœur
    //   de 387, les efface (déduit).
    internal static void finit()
    {
        cpu_state.npxc = cpu_c.fpu_type == cpu_c.FPU_8087 ? (uint16_t)0x3FF : (uint16_t)0x37F;
        if (seize_bits() && (cpu_state.npxs & 0x4700) != 0)
                ModeMateriel.Sonde[202]++;
        cpu_state.npxs = seize_bits() ? (uint16_t)(cpu_state.npxs & 0x4700) : (uint16_t)0;
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
        fsetpm = false;
        cpu_state.npxs = 0;
        finit();
    }

    // pcem bug, fixed in hardware mode: PB-203 — au RESET, le coprocesseur prend l'état initial de son type. Le 8087,
    //   le 287 et le 287XL : celui de FNINIT (Numerics Supplement, table S-7 ; 287 PRM, table 3-1 ; le 287XL : inconnu,
    //   celui du 287 qu'il remplace), C3-C0 gardés par le 8087 et le 287 (PB-202), et le mode réel, que FSETPM ne quitte que jusqu'au RESET (287 PRM, FSETPM). Le
    //   387 : celui de FNINIT, IM à 0, IE, ES et B posés, ce qui active ERROR# et
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
                fsetpm = false;
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
