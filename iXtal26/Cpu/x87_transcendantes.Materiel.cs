// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — les transcendantes du noyau de 80 bits du x87 (G13.6, le noyau, N6 ; PLAN-G13.md ; R10 de
//         TRANSCRIPTION.md).
// STATUS: materiel
//
// Le code du mode est déterministe, sans fonction de l'hôte à résultat variable (R10) : ni Math.Exp, ni Math.Log, ni leurs
// voisins. Les transcendantes s'écrivent ici sur un réel de travail de 128 bits de significande (Reel) : chaque calcul
// rend une valeur à quelques unités de 2^−127 près, que le noyau arrondit une seule fois à 64 bits selon RC
// (softfloat_roundPackToExtF80, comme ses autres calculs : la petitesse, UE, OE, PE et C1 compris). Le résultat est
// donc le résultat exact correctement arrondi, sauf si la valeur vraie tombe à moins de 2^−120 environ d'une frontière
// d'arrondi ; l'écart est alors d'une unité au plus. Le silicium vise moins d'une unité (SDM vol. 1, 2016, § 8.3.10,
// pour le Pentium et ses suivants) ; ses bits exacts, sur le 387 et le 486, sont inconnus (décision n° 14 : (c)).
//
// La référence est hors machine : tools/x87ref/transcendantes.py calcule en précision arbitraire (decimal et fractions
// de Python) et arrondit comme SoftFloat ; il rend aussi les constantes de ce fichier, que le banc compare (x87-transc).
//
// Les séries : e^y − 1 par ses coefficients de Taylor, sur |y| ≤ ln 2 / 2 ; ln m par 2 atanh((m − 1) / (m + 1)), m
// ramené à [√2 / 2, √2[ ; sin r et cos r, |r| ≤ π / 4, par Taylor ; atan u, |u| ≤ 1/16, par Taylor, après atan t =
// atan(k/8) + atan((t − k/8) / (1 + t k/8)) ; chacune évaluée par Horner sur le réel de travail, chaque produit et
// chaque somme tronqués à 128 bits, le reste replié dans le bit de poids faible (le collant de SoftFloat), qui garde
// le sens de l'arrondi d'un petit terme ajouté ; un résidu retranché sous le dernier bit coûte, lui, une unité de
// 2^−127 : une valeur vraie à moins de 2^−127 environ d'une frontière d'arrondi peut tomber d'un côté ou de l'autre
// (la contre-lecture de N6 en a trouvé sur des entrées structurées : t = 1 / (2^64 − 1) pour FPATAN, y = ln 2 et x =
// 1 − 2^−k pour FYL2X). La réduction de FSIN, FCOS, FSINCOS et FPTAN est exacte, en entiers, par le Pi de 66 bits
// significatifs du silicium (SDM vol. 1, 2016, § 8.3.8 ; 387 PRM § 4.4.9 : « an internally stored π/4 divisor that
// has 67 significant bits », la même valeur, le 67e bit de π étant nul ; pour π/4 < |x| < 2^63 ; pour le 486 :
// déduit) : |x| = q · Pi/2 + r, |r| ≤ Pi/4, puis la fonction vraie de r selon q mod 4. Le SDM résume l'effet en
// « sin(x · π / Pi) » ; les deux diffèrent d'un facteur (π − Pi) / Pi sur r, sous 2^−69 relatif, qui change le dernier
// bit de quelques pour cent des résultats réduits ; pour |x| ≤ Pi/4, où rien n'est réduit (387 PRM, ibid.), le modèle
// retenu rend sin x lui-même, sans facteur (un choix ; les bits exacts du silicium sont inconnus).

using iXtal26.SoftFloat;

namespace iXtal26.Cpu;

/// <summary>Un réel de travail : (−1)^s · m · 2^(e − 127), m normalisé (bit 127 posé) ou nul, l'exposant non
/// borné.</summary>
internal readonly struct Reel(bool s, int e, UInt128 m)
{
    internal readonly bool s = s;
    internal readonly int e = e;
    internal readonly UInt128 m = m;
}

internal static class _x87_transcendantes
{
    // ===== L'arithmétique du réel de travail =====

    private static readonly Reel Un = new(false, 0, (UInt128)1 << 127);
    private static readonly Reel Deux = new(false, 1, (UInt128)1 << 127);

    /// <summary>Le registre de 80 bits, exact (un dénormal normalisé) ; un zéro rend le réel nul.</summary>
    internal static Reel de(in extFloat80_t v)
    {
        var e = v.signExp & 0x7FFF;
        var s = (v.signExp & 0x8000) != 0;
        if (v.signif == 0)
                return new Reel(s, 0, 0);
        if (e == 0)
                e = 1;
        var z = System.Numerics.BitOperations.LeadingZeroCount(v.signif);
        return new Reel(s, e - 0x3FFF - z, (UInt128)(v.signif << z) << 64);
    }

    /// <summary>L'entier k, exact.</summary>
    internal static Reel entier(int k)
    {
        if (k == 0)
                return new Reel(false, 0, 0);
        var a = (ulong)Math.Abs((long)k);
        var z = System.Numerics.BitOperations.LeadingZeroCount(a);
        return new Reel(k < 0, 63 - z, (UInt128)(a << z) << 64);
    }

    internal static Reel oppose(in Reel a) => new(!a.s, a.e, a.m);

    internal static Reel echelle(in Reel a, int n) => new(a.s, a.e + n, a.m);

    /// <summary>m décalé de d vers la droite, le reste replié dans le bit de poids faible.</summary>
    private static UInt128 jam(UInt128 m, int d) =>
        d == 0 ? m : d >= 128 ? (m != 0 ? UInt128.One : UInt128.Zero) : (m >> d) | ((m << (128 - d)) != 0 ? UInt128.One : UInt128.Zero);

    internal static Reel mul(in Reel a, in Reel b)
    {
        if (a.m == 0 || b.m == 0)
                return new Reel(a.s ^ b.s, 0, 0);
        ulong ah = (ulong)(a.m >> 64), al = (ulong)a.m, bh = (ulong)(b.m >> 64), bl = (ulong)b.m;
        UInt128 hh = (UInt128)ah * bh, hl = (UInt128)ah * bl, lh = (UInt128)al * bh, ll = (UInt128)al * bl;
        var mid = (UInt128)(ulong)hl + (ulong)lh + (ll >> 64);
        var hi = hh + (hl >> 64) + (lh >> 64) + (mid >> 64);
        var lo = (UInt128)(ulong)mid << 64 | (ulong)ll;
        var e = a.e + b.e;
        if (hi >> 127 == 0)
        {
                hi = hi << 1 | lo >> 127;
                lo <<= 1;
        }
        else
                e++;
        return new Reel(a.s ^ b.s, e, hi | (lo != 0 ? UInt128.One : UInt128.Zero));
    }

    internal static Reel add(in Reel a, in Reel b)
    {
        if (b.m == 0)
                return a;
        if (a.m == 0)
                return b;
        var grand = a.e > b.e || a.e == b.e && a.m >= b.m;
        Reel x = grand ? a : b, y = grand ? b : a;
        var ym = jam(y.m, (int)Math.Min((long)x.e - y.e, 128));
        if (x.s == y.s)
        {
                var m = x.m + ym;
                if (m >= x.m)
                        return new Reel(x.s, x.e, m);
                return new Reel(x.s, x.e + 1, (m >> 1) | (m & 1) | (UInt128.One << 127));
        }
        var d = x.m - ym;
        if (d == 0)
                return new Reel(false, 0, 0);
        var z = (int)UInt128.LeadingZeroCount(d);
        return new Reel(x.s, x.e - z, d << z);
    }

    internal static Reel sub(in Reel a, in Reel b) => add(a, oppose(b));

    /// <summary>1 / b, b non nul : une amorce de 64 bits (2^127 divisé par les 64 bits hauts), puis deux pas de Newton,
    /// r ← r (2 − b r) ; à quelques unités de 2^−127 près.</summary>
    internal static Reel inverse(in Reel b)
    {
        var q = (UInt128.One << 127) / (ulong)(b.m >> 64);
        var z = (int)UInt128.LeadingZeroCount(q);
        var r = new Reel(b.s, 63 - z - b.e, q << z);
        for (var k = 0; k < 2; k++)
                r = mul(r, sub(Deux, mul(b, r)));
        return r;
    }

    internal static Reel div(in Reel a, in Reel b) => mul(a, inverse(b));

    /// <summary>a / b, a et b de 64 bits de significande (des registres) : la division longue, exacte à 127 bits, le reste
    /// replié dans le collant. Un quotient exact (y = x · 2^k) le reste, au lieu des quelques unités de 2^−127 de
    /// div.</summary>
    internal static Reel quotient(in Reel a, in Reel b)
    {
        ulong A = (ulong)(a.m >> 64), B = (ulong)(b.m >> 64);
        var n = (UInt128)A << 63;
        UInt128 q1 = n / B, r1 = n % B;
        UInt128 q2 = (r1 << 64) / B, r2 = (r1 << 64) % B;
        var q = q1 << 64 | q2;
        var z = (int)UInt128.LeadingZeroCount(q);
        return new Reel(a.s ^ b.s, a.e - b.e - z, q << z | (r2 != 0 ? UInt128.One : UInt128.Zero));
    }

    /// <summary>Σ c[k] · t^k, par Horner.</summary>
    private static Reel horner(Reel[] c, in Reel t)
    {
        var p = c[^1];
        for (var k = c.Length - 2; k >= 0; k--)
                p = add(c[k], mul(t, p));
        return p;
    }

    /// <summary>Le réel arrondi une fois au registre de 80 bits, selon RC, à 64 bits de significande
    /// (softfloat_roundPackToExtF80 : les indicateurs, C1, et l'exposant non borné d'OE et d'UE démasquées).
    /// `inexact` : la valeur vraie n'est pas celle du réel (une transcendante), qui doit donc lever PE même si ses bits
    /// bas sont nuls.</summary>
    internal static extFloat80_t arrondir(in Reel r, bool inexact)
    {
        var bas = (ulong)r.m | (inexact ? 1UL : 0UL);
        return softfloat.softfloat_roundPackToExtF80(r.s, r.e + 0x3FFF, (ulong)(r.m >> 64), bas, 80);
    }

    // ===== Les constantes (tools/x87ref/transcendantes.py constantes ; au plus près, 128 bits) =====

    // CONSTANTES-DEBUT
    // ln 2
    private static readonly Reel Ln2 = new(false, -1, new UInt128(0xB17217F7D1CF79AB, 0xC9E3B39803F2F6AF));
    // log₂ e = 1 / ln 2
    private static readonly Reel Log2e = new(false, 0, new UInt128(0xB8AA3B295C17F0BB, 0xBE87FED0691D3E89));
    // √2
    private static readonly Reel Racine2 = new(false, 0, new UInt128(0xB504F333F9DE6484, 0x597D89B3754ABE9F));
    // π
    private static readonly Reel Pi = new(false, 1, new UInt128(0xC90FDAA22168C234, 0xC4C6628B80DC1CD1));
    // π / 2
    private static readonly Reel PiSur2 = new(false, 0, new UInt128(0xC90FDAA22168C234, 0xC4C6628B80DC1CD1));
    // π / 4
    private static readonly Reel PiSur4 = new(false, -1, new UInt128(0xC90FDAA22168C234, 0xC4C6628B80DC1CD1));
    // 3π / 4
    private static readonly Reel TroisPiSur4 = new(false, 1, new UInt128(0x96CBE3F9990E91A7, 0x9394C9E8A0A5159D));
    // 1 / (n + 1)!, n de 0 à 26 : (e^y − 1) / y
    private static readonly Reel[] Expm1 =
    [
        new(false, 0, new UInt128(0x8000000000000000, 0x0000000000000000)),
        new(false, -1, new UInt128(0x8000000000000000, 0x0000000000000000)),
        new(false, -3, new UInt128(0xAAAAAAAAAAAAAAAA, 0xAAAAAAAAAAAAAAAB)),
        new(false, -5, new UInt128(0xAAAAAAAAAAAAAAAA, 0xAAAAAAAAAAAAAAAB)),
        new(false, -7, new UInt128(0x8888888888888888, 0x8888888888888889)),
        new(false, -10, new UInt128(0xB60B60B60B60B60B, 0x60B60B60B60B60B6)),
        new(false, -13, new UInt128(0xD00D00D00D00D00D, 0x00D00D00D00D00D0)),
        new(false, -16, new UInt128(0xD00D00D00D00D00D, 0x00D00D00D00D00D0)),
        new(false, -19, new UInt128(0xB8EF1D2AB6399C7D, 0x560E4472800B8EF2)),
        new(false, -22, new UInt128(0x93F27DBBC4FAE397, 0x780B69F5333C725B)),
        new(false, -26, new UInt128(0xD7322B3FAA271C7F, 0x3A3F25C1BEE38F10)),
        new(false, -29, new UInt128(0x8F76C77FC6C4BDAA, 0x26D4C3D67F425F60)),
        new(false, -33, new UInt128(0xB092309D43684BE5, 0x1C198E91D7B4269E)),
        new(false, -37, new UInt128(0xC9CBA54603E4E905, 0xD6F8A2EFD1F27546)),
        new(false, -41, new UInt128(0xD73F9F399DC0F88E, 0xC32B58774657F48F)),
        new(false, -45, new UInt128(0xD73F9F399DC0F88E, 0xC32B58774657F48F)),
        new(false, -49, new UInt128(0xCA963B81856A5359, 0x3028CBBB8D7FF53C)),
        new(false, -53, new UInt128(0xB413C31DCBECBBDD, 0x8024435161554BC3)),
        new(false, -57, new UInt128(0x97A4DA340A0AB926, 0x50F61DBDCB3A5ABF)),
        new(false, -62, new UInt128(0xF2A15D201011283D, 0x4E5695FC785D5DFF)),
        new(false, -66, new UInt128(0xB8DC77B6E7AB8C5F, 0x78A37E77372290C2)),
        new(false, -70, new UInt128(0x8671CB6DBFC294A2, 0x86485BF99C763ABC)),
        new(false, -75, new UInt128(0xBB0DA098B1C0CECB, 0xDC3826EBFB13CC27)),
        new(false, -80, new UInt128(0xF96780CB97ABBE65, 0x25A033E54EC51034)),
        new(false, -84, new UInt128(0x9F9E66E8B2FD46A7, 0x22520CBBB7885C4A)),
        new(false, -89, new UInt128(0xC4742FE35272CD1C, 0x790285D3580A4A34)),
        new(false, -94, new UInt128(0xE8D58E16E6751905, 0x4D0C78AEA13B9A50)),
    ];
    // 1 / (2k + 1), k de 0 à 25 : atanh(u) / u
    private static readonly Reel[] Atanh =
    [
        new(false, 0, new UInt128(0x8000000000000000, 0x0000000000000000)),
        new(false, -2, new UInt128(0xAAAAAAAAAAAAAAAA, 0xAAAAAAAAAAAAAAAB)),
        new(false, -3, new UInt128(0xCCCCCCCCCCCCCCCC, 0xCCCCCCCCCCCCCCCD)),
        new(false, -3, new UInt128(0x9249249249249249, 0x2492492492492492)),
        new(false, -4, new UInt128(0xE38E38E38E38E38E, 0x38E38E38E38E38E4)),
        new(false, -4, new UInt128(0xBA2E8BA2E8BA2E8B, 0xA2E8BA2E8BA2E8BA)),
        new(false, -4, new UInt128(0x9D89D89D89D89D89, 0xD89D89D89D89D89E)),
        new(false, -4, new UInt128(0x8888888888888888, 0x8888888888888889)),
        new(false, -5, new UInt128(0xF0F0F0F0F0F0F0F0, 0xF0F0F0F0F0F0F0F1)),
        new(false, -5, new UInt128(0xD79435E50D79435E, 0x50D79435E50D7943)),
        new(false, -5, new UInt128(0xC30C30C30C30C30C, 0x30C30C30C30C30C3)),
        new(false, -5, new UInt128(0xB21642C8590B2164, 0x2C8590B21642C859)),
        new(false, -5, new UInt128(0xA3D70A3D70A3D70A, 0x3D70A3D70A3D70A4)),
        new(false, -5, new UInt128(0x97B425ED097B425E, 0xD097B425ED097B42)),
        new(false, -5, new UInt128(0x8D3DCB08D3DCB08D, 0x3DCB08D3DCB08D3E)),
        new(false, -5, new UInt128(0x8421084210842108, 0x4210842108421084)),
        new(false, -6, new UInt128(0xF83E0F83E0F83E0F, 0x83E0F83E0F83E0F8)),
        new(false, -6, new UInt128(0xEA0EA0EA0EA0EA0E, 0xA0EA0EA0EA0EA0EA)),
        new(false, -6, new UInt128(0xDD67C8A60DD67C8A, 0x60DD67C8A60DD67D)),
        new(false, -6, new UInt128(0xD20D20D20D20D20D, 0x20D20D20D20D20D2)),
        new(false, -6, new UInt128(0xC7CE0C7CE0C7CE0C, 0x7CE0C7CE0C7CE0C8)),
        new(false, -6, new UInt128(0xBE82FA0BE82FA0BE, 0x82FA0BE82FA0BE83)),
        new(false, -6, new UInt128(0xB60B60B60B60B60B, 0x60B60B60B60B60B6)),
        new(false, -6, new UInt128(0xAE4C415C9882B931, 0x0572620AE4C415CA)),
        new(false, -6, new UInt128(0xA72F05397829CBC1, 0x4E5E0A72F0539783)),
        new(false, -6, new UInt128(0xA0A0A0A0A0A0A0A0, 0xA0A0A0A0A0A0A0A1)),
    ];
    // (−1)^k / (2k + 1)!, k de 0 à 15 : sin(r) / r
    private static readonly Reel[] Sin =
    [
        new(false, 0, new UInt128(0x8000000000000000, 0x0000000000000000)),
        new(true, -3, new UInt128(0xAAAAAAAAAAAAAAAA, 0xAAAAAAAAAAAAAAAB)),
        new(false, -7, new UInt128(0x8888888888888888, 0x8888888888888889)),
        new(true, -13, new UInt128(0xD00D00D00D00D00D, 0x00D00D00D00D00D0)),
        new(false, -19, new UInt128(0xB8EF1D2AB6399C7D, 0x560E4472800B8EF2)),
        new(true, -26, new UInt128(0xD7322B3FAA271C7F, 0x3A3F25C1BEE38F10)),
        new(false, -33, new UInt128(0xB092309D43684BE5, 0x1C198E91D7B4269E)),
        new(true, -41, new UInt128(0xD73F9F399DC0F88E, 0xC32B58774657F48F)),
        new(false, -49, new UInt128(0xCA963B81856A5359, 0x3028CBBB8D7FF53C)),
        new(true, -57, new UInt128(0x97A4DA340A0AB926, 0x50F61DBDCB3A5ABF)),
        new(false, -66, new UInt128(0xB8DC77B6E7AB8C5F, 0x78A37E77372290C2)),
        new(true, -75, new UInt128(0xBB0DA098B1C0CECB, 0xDC3826EBFB13CC27)),
        new(false, -84, new UInt128(0x9F9E66E8B2FD46A7, 0x22520CBBB7885C4A)),
        new(true, -94, new UInt128(0xE8D58E16E6751905, 0x4D0C78AEA13B9A50)),
        new(false, -103, new UInt128(0x92CFCC5A1AC56BD5, 0xF1873BB378948EB3)),
        new(true, -113, new UInt128(0xA1A6973C1FADE217, 0x0F7237D35FE1C89E)),
    ];
    // (−1)^k / (2k)!, k de 0 à 16 : cos(r)
    private static readonly Reel[] Cos =
    [
        new(false, 0, new UInt128(0x8000000000000000, 0x0000000000000000)),
        new(true, -1, new UInt128(0x8000000000000000, 0x0000000000000000)),
        new(false, -5, new UInt128(0xAAAAAAAAAAAAAAAA, 0xAAAAAAAAAAAAAAAB)),
        new(true, -10, new UInt128(0xB60B60B60B60B60B, 0x60B60B60B60B60B6)),
        new(false, -16, new UInt128(0xD00D00D00D00D00D, 0x00D00D00D00D00D0)),
        new(true, -22, new UInt128(0x93F27DBBC4FAE397, 0x780B69F5333C725B)),
        new(false, -29, new UInt128(0x8F76C77FC6C4BDAA, 0x26D4C3D67F425F60)),
        new(true, -37, new UInt128(0xC9CBA54603E4E905, 0xD6F8A2EFD1F27546)),
        new(false, -45, new UInt128(0xD73F9F399DC0F88E, 0xC32B58774657F48F)),
        new(true, -53, new UInt128(0xB413C31DCBECBBDD, 0x8024435161554BC3)),
        new(false, -62, new UInt128(0xF2A15D201011283D, 0x4E5695FC785D5DFF)),
        new(true, -70, new UInt128(0x8671CB6DBFC294A2, 0x86485BF99C763ABC)),
        new(false, -80, new UInt128(0xF96780CB97ABBE65, 0x25A033E54EC51034)),
        new(true, -89, new UInt128(0xC4742FE35272CD1C, 0x790285D3580A4A34)),
        new(false, -98, new UInt128(0x850C5131A842E9B9, 0xE2E28E1AA546A152)),
        new(true, -108, new UInt128(0x9C9962823EB07306, 0x56F6A614C4E2BA59)),
        new(false, -118, new UInt128(0xA1A6973C1FADE217, 0x0F7237D35FE1C89E)),
    ];
    // (−1)^k / (2k + 1), k de 0 à 16 : atan(u) / u
    private static readonly Reel[] Atan =
    [
        new(false, 0, new UInt128(0x8000000000000000, 0x0000000000000000)),
        new(true, -2, new UInt128(0xAAAAAAAAAAAAAAAA, 0xAAAAAAAAAAAAAAAB)),
        new(false, -3, new UInt128(0xCCCCCCCCCCCCCCCC, 0xCCCCCCCCCCCCCCCD)),
        new(true, -3, new UInt128(0x9249249249249249, 0x2492492492492492)),
        new(false, -4, new UInt128(0xE38E38E38E38E38E, 0x38E38E38E38E38E4)),
        new(true, -4, new UInt128(0xBA2E8BA2E8BA2E8B, 0xA2E8BA2E8BA2E8BA)),
        new(false, -4, new UInt128(0x9D89D89D89D89D89, 0xD89D89D89D89D89E)),
        new(true, -4, new UInt128(0x8888888888888888, 0x8888888888888889)),
        new(false, -5, new UInt128(0xF0F0F0F0F0F0F0F0, 0xF0F0F0F0F0F0F0F1)),
        new(true, -5, new UInt128(0xD79435E50D79435E, 0x50D79435E50D7943)),
        new(false, -5, new UInt128(0xC30C30C30C30C30C, 0x30C30C30C30C30C3)),
        new(true, -5, new UInt128(0xB21642C8590B2164, 0x2C8590B21642C859)),
        new(false, -5, new UInt128(0xA3D70A3D70A3D70A, 0x3D70A3D70A3D70A4)),
        new(true, -5, new UInt128(0x97B425ED097B425E, 0xD097B425ED097B42)),
        new(false, -5, new UInt128(0x8D3DCB08D3DCB08D, 0x3DCB08D3DCB08D3E)),
        new(true, -5, new UInt128(0x8421084210842108, 0x4210842108421084)),
        new(false, -6, new UInt128(0xF83E0F83E0F83E0F, 0x83E0F83E0F83E0F8)),
    ];
    // atan(k / 8), k de 1 à 8
    private static readonly Reel[] AtanK =
    [
        new(false, -4, new UInt128(0xFEADD4D5617B6E32, 0xC897989F3E888EF8)),
        new(false, -3, new UInt128(0xFADBAFC96406EB15, 0x6DC79EF5F7A217E6)),
        new(false, -2, new UInt128(0xB7B0CA0F26F78473, 0x8AA32122DCFE4483)),
        new(false, -2, new UInt128(0xED63382B0DDA7B45, 0x6FE445ECBC3A8D03)),
        new(false, -1, new UInt128(0x8F005D5EF7F59F9B, 0x5C835E1665C43748)),
        new(false, -1, new UInt128(0xA4BC7D1934F70924, 0x19A87F2A457DAC9F)),
        new(false, -1, new UInt128(0xB8053E2BC2319E73, 0xCB2DA55210A4443D)),
        new(false, -1, new UInt128(0xC90FDAA22168C234, 0xC4C6628B80DC1CD1)),
    ];
    // CONSTANTES-FIN

    // ===== Les fonctions =====

    /// <summary>e^y − 1, |y| ≤ ln 2 / 2, sans perte relative pour y petit : y · Σ y^n / (n + 1)!.</summary>
    private static Reel expm1(in Reel y) => mul(y, horner(Expm1, y));

    /// <summary>2 atanh(u) = ln((1 + u) / (1 − u)), |u| ≤ 3 − 2√2 : 2u · Σ u^2k / (2k + 1).</summary>
    private static Reel ln_atanh(in Reel u) => echelle(mul(u, horner(Atanh, mul(u, u))), 1);

    /// <summary>2^x − 1, x fini et non nul : x = n + f, n l'entier le plus proche, |f| ≤ 1/2 ; 2^n (1 + (e^(f ln 2) − 1))
    /// − 1, ou e^(f ln 2) − 1 seul si n = 0. `exact` : f = 0 (x entier), le résultat 2^n − 1, que l'arrondi juge. Hors
    /// de [−1, 1] (le résultat du silicium y est indéfini : SDM vol. 2, F2XM1), la même valeur, un choix ; |x| ≥ 2^15
    /// donne n = ±2^20, dont le résultat déborde ou vaut −1 au collant près.</summary>
    internal static Reel exp2m1(in extFloat80_t v, out bool exact)
    {
        var x = de(v);
        int n;
        if (x.e < -1)
                n = 0;
        else if (x.e >= 15)
                n = x.s ? -(1 << 20) : 1 << 20;
        else
        {
                // |x| < 2^15 : la partie entière et le premier bit de la partie fractionnaire.
                var k = 127 - x.e;
                var ent = k >= 128 ? 0 : (int)(x.m >> k);
                var demi = (x.m >> (k - 1) & 1) != 0;
                n = ent + (demi ? 1 : 0);
                if (x.s)
                        n = -n;
        }
        var f = x.e >= 15 ? new Reel(false, 0, 0) : sub(x, entier(n));
        exact = f.m == 0;
        var e = exact ? new Reel(false, 0, 0) : expm1(mul(f, Ln2));
        if (n == 0)
                return e;
        return sub(echelle(add(Un, e), n), Un);
    }

    /// <summary>log₂ x, x > 0 fini (réel de travail) : x = 2^k m, m dans [√2 / 2, √2[ ; k + ln m · log₂ e. `exact` :
    /// x est une puissance de 2, le résultat k.</summary>
    internal static Reel log2(in Reel x, out bool exact)
    {
        var k = x.e;
        var m = new Reel(false, 0, x.m);
        if (m.m > Racine2.m)
        {
                m = echelle(m, -1);
                k++;
        }
        exact = x.m == UInt128.One << 127;
        if (exact)
                return entier(k);
        var l = mul(ln_atanh(div(sub(m, Un), add(m, Un))), Log2e);
        return add(entier(k), l);
    }

    // ===== La trigonométrie =====

    /// <summary>Pi, le π du silicium : 0.C90FDAA2 2168C234 C × 2^2, 66 bits significatifs (SDM vol. 1, 2016, § 8.3.8 ;
    /// 387 PRM § 4.4.9, π/4 sur 67 bits, la même valeur ; pour le 486 : déduit) ; Pi = P66 · 2^−66.</summary>
    private static readonly UInt128 P66 = new(0xC, 0x90FDAA22168C234C);

    /// <summary>|x| (0 &lt; |x| &lt; 2^63, positif) réduit par Pi/2, exactement : |x| = q · Pi/2 + r, q le plus proche,
    /// |r| ≤ Pi/4 ; rend r et q mod 4. Sous 1/2, rien n'est réduit. Au-delà, |x| = X · 2^(e − 63) = N · 2^−67 et Pi/2 =
    /// P66 · 2^−67 : r = (N mod P66) · 2^−67, par décalages et soustractions (N < 2^130, le reste < 2^69). r n'est
    /// jamais nul : P66 / 4 est impair et de 66 bits, il ne divise aucun X de 64.</summary>
    private static Reel reduire(in Reel ax, out int quadrant)
    {
        quadrant = 0;
        if (ax.e < -1)
                return ax;
        var a = (UInt128)(ulong)(ax.m >> 64);
        ulong q = 0;
        for (var i = 0; i < ax.e + 4; i++)
        {
                a <<= 1;
                q <<= 1;
                if (a >= P66)
                {
                        a -= P66;
                        q |= 1;
                }
        }
        var neg = false;
        if (a << 1 > P66)
        {
                a = P66 - a;
                neg = true;
                q++;
        }
        quadrant = (int)(q & 3);
        var z = (int)UInt128.LeadingZeroCount(a);
        return new Reel(neg, 60 - z, a << z);
    }

    private static Reel sinus(in Reel r) => mul(r, horner(Sin, mul(r, r)));

    private static Reel cosinus(in Reel r) => horner(Cos, mul(r, r));

    /// <summary>sin x, cos x ou tan x (`f` : 0, 1, 2), x fini, 0 &lt; |x| &lt; 2^63 : la fonction vraie de l'argument
    /// réduit par Pi/2, selon le quadrant ; tan x, sin r / cos r, ou − cos r / sin r d'un quadrant impair.</summary>
    internal static Reel trigo(int f, in extFloat80_t v)
    {
        var x = de(v);
        var r = reduire(new Reel(false, x.e, x.m), out var k);
        Reel y;
        if (f == 2 && r.e < -32)
        {
                // |r| < 2^−32 : tan r = r (1 + r²/3 + …), et − cot r = − (1/r − r/3 − …) ; le quotient sin r / cos r
                // perdrait le sens de r²/3, sous le bruit de div. 1/r, exact si r est une puissance de 2.
                var tiers = mul(mul(r, r), Atanh[1]);
                if (k % 2 == 0)
                        y = mul(r, add(Un, tiers));
                else
                {
                        var inv = r.m == UInt128.One << 127 ? new Reel(r.s, -r.e, r.m) : inverse(r);
                        y = oppose(mul(inv, sub(Un, tiers)));
                }
        }
        else if (f == 2)
                y = k % 2 == 0 ? div(sinus(r), cosinus(r)) : oppose(div(cosinus(r), sinus(r)));
        else
        {
                // sin(q π/2 + r) et cos(q π/2 + r) : sin, cos, −sin, −cos, en décalant d'un quadrant pour le cosinus.
                var j = (k + f) & 3;
                y = (j & 1) == 0 ? sinus(r) : cosinus(r);
                if ((j & 2) != 0)
                        y = oppose(y);
        }
        return f != 1 && x.s ? oppose(y) : y;
    }

    /// <summary>atan t, t dans [0, 1] à quelques unités de 2^−127 près (un quotient) : k = t · 8 au plus près ;
    /// atan(k/8) + atan(u), u = (t − k/8) / (1 + t k/8), |u| ≤ 1/16 ; k = 0, la série de t elle-même, sans perte
    /// relative.</summary>
    private static Reel atan01(in Reel t)
    {
        var k = 0;
        if (t.e >= -4)
        {
                var d = 124 - t.e;
                k = (d >= 128 ? 0 : (int)(t.m >> d)) + (int)(t.m >> (d - 1) & 1);
        }
        if (k == 0)
                return mul(t, horner(Atan, mul(t, t)));
        var c = echelle(entier(k), -3);
        var u = div(sub(t, c), add(Un, mul(t, c)));
        return add(AtanK[k - 1], mul(u, horner(Atan, mul(u, u))));
    }

    /// <summary>atan(y / x), y et x finis et non nuls : l'angle du point (x, y), de −π à π, du signe de y (SDM vol. 2,
    /// 2016, FPATAN) ; |y| ≤ |x|, atan(|y| / |x|), sinon π/2 − atan(|x| / |y|) ; x négatif, π moins l'angle.</summary>
    internal static Reel arctan(in extFloat80_t vy, in extFloat80_t vx)
    {
        Reel y = de(vy), x = de(vx);
        Reel a = new(false, y.e, y.m), b = new(false, x.e, x.m);
        var inverse = a.e > b.e || a.e == b.e && a.m > b.m;
        var th = inverse ? sub(PiSur2, atan01(quotient(b, a))) : atan01(quotient(a, b));
        if (x.s)
                th = sub(Pi, th);
        return y.s ? oppose(th) : th;
    }

    /// <summary>±π, ±π/2, ±π/4 ou ±3π/4 (k : 4, 2, 1, 3, en quarts de π), du signe `neg`.</summary>
    internal static Reel angle(int k, bool neg)
    {
        var a = k switch { 4 => Pi, 2 => PiSur2, 1 => PiSur4, _ => TroisPiSur4 };
        return neg ? oppose(a) : a;
    }

    /// <summary>log₂(1 + x), x > −1 fini et non nul : pour |x| < 1/4, 2 atanh(x / (2 + x)) · log₂ e, sans perte
    /// relative pour x petit ; au-delà, log₂ de 1 + x, exact sur le réel de travail (hors du domaine du silicium,
    /// |x| < 1 − √2 / 2, au-delà de 0,29 : un choix, la même valeur). `exact` : 1 + x est une puissance de 2.</summary>
    internal static Reel log2_1p(in extFloat80_t v, out bool exact)
    {
        var x = de(v);
        if (x.e < -2)
        {
                exact = false;
                return mul(ln_atanh(div(x, add(Deux, x))), Log2e);
        }
        return log2(add(Un, x), out exact);
    }
}
