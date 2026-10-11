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
// EN CHANTIER (ModeMateriel.EnChantier) : le noyau ne se demande que par ses numéros, PB-203 et PB-264, jusqu'à la fin
// de N7 ; ni « tout » ni le domaine x87 ne le prennent. Ses tables remplacent alors toutes celles du x87 : chaque
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
    /// vrai si une exception démasquée arrête l'instruction (OE et UE démasquées : N5). Après une faute de pile masquée,
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
        return e != 0 && lever(e);
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
