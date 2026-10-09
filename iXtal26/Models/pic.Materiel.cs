// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel du 8259A (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans pic.c, appelées par les gardes de pic.cs (`if (materiel.pb_nn)`), seules
// sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans l'entrée PB de PCEM_BUGS.md ;
// la fiche est celle du 8259A d'Intel (231468-003). Chaque correction ne vaut que si on la demande : sans PB-248, la
// priorité reste fixe (IR0 la plus haute) et le masque de service est celui de pic_update_mask, comme dans PCem.

using iXtal26.Diag;
using static iXtal26.Cpu.x86;

namespace iXtal26.Models;

internal static partial class pic
{
    // pcem bug, fixed in hardware mode: PB-248 — l'état du 8259A que PCem ne tient pas, par contrôleur : le niveau de
    //   plus basse priorité (7 après ICW1 : fiche p. 10, point c), le masque spécial (OCW3, bits 6-5), la rotation en
    //   fin d'interruption automatique (OCW2 80h et 00h), un poll en attente (OCW3, bit 2). Dans une classe à part :
    //   un champ initialisé de plus dans pic change son constructeur statique, et le JIT n'y intègre plus en constante
    //   l'adresse de pic_current (picintc, mesuré contre M0) ; le mode PCem doit rester à l'octet près.
    private static class Etat8259
    {
        internal static int bas1 = 7, bas2 = 7;
        internal static bool smm1, smm2, rotationAuto1, rotationAuto2, poll1, poll2;
    }

    private static ref int bas_materiel(PIC p) => ref p == pic_ ? ref Etat8259.bas1 : ref Etat8259.bas2;

    private static ref bool smm_materiel(PIC p) => ref p == pic_ ? ref Etat8259.smm1 : ref Etat8259.smm2;

    private static ref bool rotation_auto_materiel(PIC p) =>
        ref p == pic_ ? ref Etat8259.rotationAuto1 : ref Etat8259.rotationAuto2;

    private static ref bool poll_attendu_materiel(PIC p) => ref p == pic_ ? ref Etat8259.poll1 : ref Etat8259.poll2;

    /// <summary>Le niveau de rang `i` (0, le plus prioritaire) : IR0 d'abord sans PB-248 ; avec, le niveau qui suit
    /// celui de plus basse priorité (fiche p. 15, « Automatic Rotation », « Specific Rotation »).</summary>
    private static int niveau_materiel(PIC p, int i) => materiel.pb_248 ? (bas_materiel(p) + 1 + i) & 7 : i;

    /// <summary>Le masque de service : les niveaux de priorité égale ou moindre que le plus prioritaire en service (fiche
    /// p. 15, « While the IS bit is set, all further interrupts of the same or lower priority are inhibited ») ; aucun en
    /// masque spécial (p. 16, « Special Mask Mode »). Sans PB-248, celui de PCem.</summary>
    private static void masque_service_materiel(PIC p)
    {
        if (!materiel.pb_248)
        {
                pic_update_mask(ref p.mask2, p.ins);
                return;
        }
        p.mask2 = 0;
        if (smm_materiel(p))
                return;
        for (var i = 0; i < 8; i++)
        {
                if ((p.ins & (1 << niveau_materiel(p, i))) != 0)
                {
                        for (var j = i; j < 8; j++)
                                p.mask2 |= (uint8_t)(1 << niveau_materiel(p, j));
                        return;
                }
        }
    }

    // pcem bug, fixed in hardware mode: PB-05, PB-246 et PB-247 — l'acquittement selon la fiche, chaque correction
    //   seulement si on la demande. PB-246 : la cascade (IR2 du maître, sur l'AT) se sert à son rang, après IR0 et IR1
    //   (p. 15, « Fully Nested Mode » ; AT TR p. 1-10). PB-247 : le masque de service retient les niveaux de priorité
    //   égale ou moindre, au maître comme à l'esclave (p. 15 ; p. 18, « a slave is masked out when its request is in
    //   service »). PB-05 : servir l'esclave ne touche à l'IRR du maître que par la cascade, que pic_updatepending
    //   recalcule (p. 7, « Interrupt Sequence »). Avec PB-248, l'ordre des niveaux tourne et la fin automatique peut
    //   faire tourner les priorités.
    private static uint8_t picinterrupt_materiel()
    {
        uint8_t temp = (uint8_t)(pic_.pend & ~pic_.mask);
        if (materiel.pb_247)
        {
                ModeMateriel.Sonde[247]++;
                temp &= (uint8_t)~pic_.mask2;
        }
        if (materiel.pb_246)
                ModeMateriel.Sonde[246]++;
        for (var i = 0; i < 8; i++)
        {
                var c = niveau_materiel(pic_, i);
                if (AT != 0 && (temp & (1 << 2)) != 0 && (!materiel.pb_246 || c == 2))
                {
                        uint8_t temp2 = (uint8_t)(pic2.pend & ~pic2.mask);
                        if (materiel.pb_247)
                                temp2 &= (uint8_t)~pic2.mask2;
                        for (var j = 0; j < 8; j++)
                        {
                                var d = niveau_materiel(pic2, j);
                                if ((temp2 & (1 << d)) != 0)
                                {
                                        if ((pic2.level_sensitive & (1 << d)) == 0)
                                                pic2.pend &= (uint8_t)~(1 << d);
                                        pic2.ins |= (uint8_t)(1 << d);
                                        masque_service_materiel(pic2);
                                        if (!materiel.pb_05)
                                        {
                                                if ((pic2.level_sensitive & (1 << d)) == 0)
                                                        pic_.pend &= (uint8_t)~(1 << d);
                                        }
                                        else
                                                ModeMateriel.Sonde[5]++;
                                        pic_.ins |= 1 << 2;
                                        masque_service_materiel(pic_);
                                        pic_updatepending();
                                        if ((pic2.icw4 & 0x02) != 0)
                                                fin_non_specifique_materiel(pic2, rotation_auto_materiel(pic2), false);
                                        return (uint8_t)(d + pic2.vector);
                                }
                        }
                        // PCem sort ici de la boucle, son indice épuisé ; la fiche passe au niveau suivant.
                        if (!materiel.pb_246)
                                return 0xFF;
                }
                else if ((temp & (1 << c)) != 0)
                {
                        if ((pic_.level_sensitive & (1 << c)) == 0)
                                pic_.pend &= (uint8_t)~(1 << c);
                        pic_.ins |= (uint8_t)(1 << c);
                        masque_service_materiel(pic_);
                        pic_updatepending();
                        if ((pic_.icw4 & 0x02) != 0)
                                fin_non_specifique_materiel(pic_, rotation_auto_materiel(pic_), false);
                        return (uint8_t)(c + pic_.vector);
                }
        }
        return 0xFF;
    }

    /// <summary>La fin d'interruption non spécifique : le bit de l'ISR le plus prioritaire s'efface (p. 15) ; avec
    /// `rotation`, son niveau devient le moins prioritaire. Les effets de bord de PCem restent : la cascade remise en
    /// attente, et, à l'OCW2 seulement (`ocw2`), l'IRQ 1 libérée pour le clavier de l'hôte ; pas à la fin automatique,
    /// où pic_autoeoi ne le fait pas.</summary>
    private static void fin_non_specifique_materiel(PIC p, bool rotation, bool ocw2)
    {
        for (var i = 0; i < 8; i++)
        {
                var c = niveau_materiel(p, i);
                if ((p.ins & (1 << c)) != 0)
                {
                        fin_specifique_materiel(p, c);
                        if (ocw2 && p == pic_ && c == 1 && keywaiting != 0)
                                intclear &= ~1;
                        if (rotation)
                        {
                                bas_materiel(p) = c;
                                masque_service_materiel(p);
                                pic_updatepending();
                        }
                        return;
                }
        }
    }

    /// <summary>La fin d'interruption du niveau `c` ; au maître, la cascade (IR2) se remet en attente si l'esclave
    /// demande encore — PB-13 : PCem testait `val == 2`, jamais vrai.</summary>
    private static void fin_specifique_materiel(PIC p, int c)
    {
        p.ins &= (uint8_t)~(1 << c);
        masque_service_materiel(p);
        if (p == pic_ && AT != 0 && c == 2 && ((pic2.pend & ~pic2.mask) & ~pic2.mask2) != 0)
                pic_.pend |= 1 << 2;
        pic_updatepending();
    }

    // pcem bug, fixed in hardware mode: PB-248 — l'OCW2 selon la fiche (p. 13, figure 8 ; p. 15-16) : R, SL et EOI
    //   choisissent entre la fin non spécifique (20h), la fin spécifique (60h + n), « sans opération » (40h), la
    //   rotation en fin automatique, posée (80h) ou levée (00h), la rotation à la fin non spécifique (A0h) ou
    //   spécifique (E0h + n), la priorité posée (C0h + n : n devient le moins prioritaire).
    private static void pic_ocw2_materiel(PIC p, uint8_t val)
    {
        ModeMateriel.Sonde[248]++;
        var n = val & 7;
        switch (val >> 5)
        {
        case 0:
                rotation_auto_materiel(p) = false;
                break;
        case 1:
                fin_non_specifique_materiel(p, false, true);
                break;
        case 2:
                break;
        case 3:
                fin_specifique_materiel(p, n);
                break;
        case 4:
                rotation_auto_materiel(p) = true;
                break;
        case 5:
                fin_non_specifique_materiel(p, true, true);
                break;
        case 6:
                bas_materiel(p) = n;
                masque_service_materiel(p);
                pic_updatepending();
                break;
        case 7:
                fin_specifique_materiel(p, n);
                bas_materiel(p) = n;
                masque_service_materiel(p);
                pic_updatepending();
                break;
        }
    }

    // pcem bug, fixed in hardware mode: PB-248 — l'OCW3 selon la fiche (p. 14, figure 8 ; p. 16-17) : RR et RIS
    //   choisissent la lecture (comme PCem), P demande un poll, ESMM et SMM posent (11) ou lèvent (10) le masque spécial.
    private static void pic_ocw3_materiel(PIC p, uint8_t val)
    {
        ModeMateriel.Sonde[248]++;
        if ((val & 2) != 0)
                p.read = val & 1;
        if ((val & 4) != 0)
                poll_attendu_materiel(p) = true;
        if ((val & 0x40) != 0)
        {
                smm_materiel(p) = (val & 0x20) != 0;
                masque_service_materiel(p);
                pic_updatepending();
        }
    }

    // pcem bug, fixed in hardware mode: PB-248 — la lecture qui suit un poll (p. 16, « Poll Command ») vaut
    //   acquittement : le bit de l'ISR du niveau le plus prioritaire qui demande se pose, et le mot rend 80h + ce niveau ;
    //   aucune demande, 00h (les bits W2-W0 sont alors indéfinis, à zéro ici). La lecture suivante revient à l'IRR ou à
    //   l'ISR.
    private static uint8_t pic_poll_lire_materiel(PIC p)
    {
        ModeMateriel.Sonde[248]++;
        poll_attendu_materiel(p) = false;
        var temp = p.pend & ~p.mask & ~p.mask2;
        for (var i = 0; i < 8; i++)
        {
                var c = niveau_materiel(p, i);
                if ((temp & (1 << c)) != 0)
                {
                        if ((p.level_sensitive & (1 << c)) == 0)
                                p.pend &= (uint8_t)~(1 << c);
                        p.ins |= (uint8_t)(1 << c);
                        masque_service_materiel(p);
                        pic_updatepending();
                        return (uint8_t)(0x80 | c);
                }
        }
        return 0;
    }

    // pcem bug, fixed in hardware mode: PB-248 — ICW1 remet IR7 au plus bas, lève le masque spécial (fiche p. 10, points
    //   c et e) ; la rotation en fin automatique et le poll en attente tombent avec.
    private static void pic_icw1_248_materiel(PIC p)
    {
        ModeMateriel.Sonde[248]++;
        bas_materiel(p) = 7;
        smm_materiel(p) = false;
        rotation_auto_materiel(p) = false;
        poll_attendu_materiel(p) = false;
    }

    // pcem bug, fixed in hardware mode: PB-255 — après ICW1, la lecture de l'adresse paire rend l'IRR (fiche p. 10,
    //   point e : « Status Read is set to IRR » ; p. 17).
    private static void pic_icw1_255_materiel(PIC p)
    {
        ModeMateriel.Sonde[255]++;
        p.read = 0;
    }

    // pcem bug, fixed in hardware mode: PB-248 et PB-255 — la mise sous tension : le 8259A n'a pas de broche de remise
    //   à zéro (fiche p. 2, table 1), seul ICW1 l'initialise ; on pose l'état qu'ICW1 laisserait, aux deux contrôleurs.
    //   PB-255 : la lecture sur l'IRR, l'esclave compris, que PCem ne remet jamais. PB-248 : IR7 au plus bas, ni masque
    //   spécial, ni rotation, ni poll ; le masque de service recalculé, ce qui remet aussi celui de l'esclave (PB-06).
    private static void pic_reset_255_materiel()
    {
        pic_icw1_255_materiel(pic_);
        pic_icw1_255_materiel(pic2);
    }

    private static void pic_reset_248_materiel()
    {
        pic_icw1_248_materiel(pic_);
        pic_icw1_248_materiel(pic2);
        masque_service_materiel(pic_);
        masque_service_materiel(pic2);
    }
}
