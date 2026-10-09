// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel de la souris PS/2 (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans mouse_ps2.c, appelées par les gardes de mouse_ps2.cs (`if (materiel.pb_nn)`),
// seules sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans l'entrée PB de
// PCEM_BUGS.md. Les commandes et leurs réponses sont celles de Chapweske (« The PS/2 Mouse Interface », 2001) et de
// Brouwer (« The PS/2 mouse »), sources secondaires ; l'octet d'état est celui d'IBM (15F0306, p. 2-97).

using static iXtal26.Keyboard.keyboard_at;
using static iXtal26.Mouse.mouse;

namespace iXtal26.Mouse;

internal static partial class mouse_ps2
{
    // L'état que PCem n'a pas, hors de mouse_ps2_t : un champ de plus y changerait la disposition de la classe, et le
    // code que le JIT produit pour les méthodes qui la lisent, en mode PCem aussi. Une seule souris par machine.
    private static class EtatSouris
    {
        /// <summary>Le mode d'avant l'écho (EEh), que ECh rétablit.</summary>
        internal static int modeAvant = MOUSE_STREAM;

        /// <summary>Les commandes invalides d'affilée : la première reçoit FEh, les suivantes FCh.</summary>
        internal static int invalides;
    }

    // pcem bug, fixed in hardware mode: PB-94 — à l'init, la souris neuve : en flux, aucune commande invalide, ses
    //   valeurs par défaut.
    private static void mouse_ps2_init_materiel(mouse_ps2_t mouse)
    {
        EtatSouris.modeAvant = MOUSE_STREAM;
        EtatSouris.invalides = 0;
        mouse_ps2_defauts_materiel(mouse);
    }

    // pcem bug, fixed in hardware mode: PB-94 — à la mise sous tension et au reset (FFh), la souris prend ses valeurs
    //   par défaut, celles de F6h : 100 points par seconde, 4 points par mm (Chapweske). PCem les laisse à zéro, ou à ce
    //   qu'elles valaient.
    private static void mouse_ps2_defauts_materiel(mouse_ps2_t mouse)
    {
        mouse.sample_rate = 100;
        mouse.resolution = 2;
        ModeMateriel.Sonde[94]++;
    }

    // pcem bug, fixed in hardware mode: PB-261 — après le paquet d'EBh, la souris remet ses compteurs de mouvement à
    //   zéro (Chapweske, « Read Data »).
    private static void mouse_ps2_lu_materiel(mouse_ps2_t mouse)
    {
        mouse.x = mouse.y = mouse.z = 0;
        ModeMateriel.Sonde[261]++;
    }

    // pcem bug, fixed in hardware mode: PB-94 — avant le switch : en mode écho (EEh), la souris renvoie tout octet
    //   reçu, sauf ECh (fin de l'écho) et FFh (reset), qu'elle exécute. Hors de l'écho, une commande valide remet à zéro
    //   le compte des invalides. Un octet de donnée (après E8h ou F3h) n'est pas une commande.
    private static bool mouse_ps2_entree_materiel(mouse_ps2_t mouse, uint8_t val)
    {
        if (mouse.cd != 0)
                return false;
        if (mouse.mode == MOUSE_ECHO && val != 0xec && val != 0xff)
        {
                keyboard_at_adddata_mouse(val);
                ModeMateriel.Sonde[94]++;
                return true;
        }
        if (val is 0xe6 or 0xe7 or 0xe8 or 0xe9 or 0xea or 0xeb or 0xec or 0xee or 0xf0 or 0xf2 or 0xf3 or 0xf4 or 0xf5
            or 0xf6 or 0xfe or 0xff)
                EtatSouris.invalides = 0;
        return false;
    }

    // pcem bug, fixed in hardware mode: PB-94 — les commandes que le switch de PCem ignore. FAh à chacune : F6h, les
    //   valeurs par défaut (100 points par seconde, 4 points par mm, échelle 1:1, flux coupé) ; EAh, le mode flux ;
    //   F0h, le mode distant ; ces trois remettent les compteurs de mouvement à zéro. EEh, l'écho ; ECh, sa fin, le mode
    //   d'avant. Une commande inconnue reçoit FEh, et FCh si elle suit une autre inconnue. FEh (renvoyer le dernier
    //   paquet) n'est pas modélisé : il reste sans réponse, comme chez PCem ; ni le refus (FEh) d'une donnée hors de sa plage
    //   après E8h ou F3h.
    private static void mouse_ps2_autre_materiel(mouse_ps2_t mouse, uint8_t val)
    {
        ModeMateriel.Sonde[94]++;
        switch (val)
        {
        case 0xf6:
                mouse_ps2_defauts_materiel(mouse);
                mouse.flags &= unchecked((uint8_t)~(MOUSE_ENABLE | MOUSE_SCALE));
                mouse.x = mouse.y = mouse.z = 0;
                keyboard_at_adddata_mouse(0xfa);
                break;
        case 0xea:
        case 0xf0:
                mouse.mode = val == 0xea ? MOUSE_STREAM : MOUSE_REMOTE;
                mouse.x = mouse.y = mouse.z = 0;
                keyboard_at_adddata_mouse(0xfa);
                break;
        case 0xee:
                EtatSouris.modeAvant = mouse.mode;
                mouse.mode = MOUSE_ECHO;
                keyboard_at_adddata_mouse(0xfa);
                break;
        case 0xec:
                if (mouse.mode == MOUSE_ECHO)
                        mouse.mode = EtatSouris.modeAvant;
                keyboard_at_adddata_mouse(0xfa);
                break;
        case 0xfe:
                break;
        default:
                keyboard_at_adddata_mouse(EtatSouris.invalides++ == 0 ? (uint8_t)0xfe : (uint8_t)0xfc);
                break;
        }
    }

    // pcem bug, fixed in hardware mode: PB-95 — l'octet d'état d'E9h selon IBM (15F0306, p. 2-97) : bit 6 le mode
    //   distant, 5 le flux activé, 4 l'échelle 2:1, 2 le bouton gauche, 0 le droit ; le bit 1, réservé chez IBM, porte
    //   le bouton du milieu d'une souris à trois boutons (Chapweske), comme le paquet. Puis la résolution et la cadence.
    private static bool mouse_ps2_etat_materiel(mouse_ps2_t mouse)
    {
        var temp = (uint8_t)(mouse.flags & (MOUSE_ENABLE | MOUSE_SCALE));
        if (mouse.mode == MOUSE_REMOTE)
                temp |= 0x40;
        if ((mouse_buttons & 1) != 0)
                temp |= 4;
        if ((mouse_buttons & 2) != 0)
                temp |= 1;
        if ((mouse_buttons & 4) != 0 && (mouse_get_type(mouse_type) & MOUSE_TYPE_3BUTTON) != 0)
                temp |= 2;
        keyboard_at_adddata_mouse(temp);
        keyboard_at_adddata_mouse(mouse.resolution);
        keyboard_at_adddata_mouse(mouse.sample_rate);
        ModeMateriel.Sonde[95]++;
        return true;
    }
}
