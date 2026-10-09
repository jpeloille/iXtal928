// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel du 8042 et de ses files (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans keyboard_at.c, appelées par les gardes de keyboard_at.cs et de
// mouse_ps2.cs (`if (materiel.pb_nn)`), seules sur leur ligne. La source, le cas qui discrimine et la panne qui le
// rougit sont dans l'entrée PB de PCEM_BUGS.md.
//
// PB-254. Le clavier de l'AT garde seize codes dans son tampon, remplace le dix-septième par le code de débordement
// (00h, que le 8042 traduit en FFh) et perd les suivants (AT TR p. 4-3) : ce tampon est ici, en octets bruts, avant la
// traduction ; il livre à la file de PCem un octet à la fois, quand elle est vide, et l'octet livré compte encore dans
// les seize, puisqu'il attend que le 8042 le prenne. Le contrôleur et la souris, eux, ne perdent rien : le 8042 tient
// la ligne « clock » de la souris tant que son tampon de sortie est plein (PS/2 HITR 84F9735, p. 14-15), et ses propres
// réponses attendent leur tour (déduit). Ce qui ne tient plus dans la file de PCem, seize places dont quinze utiles,
// attend ici, dans l'ordre ; au-delà de RESERVE octets, l'émulateur borne, pour qu'un invité qui empile des commandes
// sans jamais lire ne fasse pas grandir l'hôte (R9).

namespace iXtal26.Keyboard;

internal static partial class keyboard_at
{
    private const int RESERVE = 64;

    // L'état que PCem n'a pas, dans une classe à part : des champs statiques de plus dans keyboard_at changeraient son
    // constructeur statique, et le code que le JIT produit pour les méthodes qui le lisent, en mode PCem aussi.
    private static class Etat8042
    {
        /// <summary>Le tampon du clavier : seize octets bruts et le code de débordement.</summary>
        internal static readonly uint8_t[] clavier = new uint8_t[17];
        internal static int nClavier;

        /// <summary>Ce que la file du contrôleur et celle de la souris ne tiennent plus.</summary>
        internal static readonly uint8_t[] controleur = new uint8_t[RESERVE], souris = new uint8_t[RESERVE];
        internal static int nControleur, nSouris;
    }

    private static int compte(int start, int end) => (end - start) & 0xf;

    private static uint8_t retirer(uint8_t[] f, ref int n)
    {
        var v = f[0];
        Array.Copy(f, 1, f, 0, --n);
        return v;
    }

    // pcem bug, fixed in hardware mode: PB-254 — au keyboard_at_init, tout vide, comme à la mise sous tension : les
    //   trois tampons d'ici et les trois files de PCem, qu'il garde d'un amorçage à l'autre (hors de la structure que
    //   son memset efface).
    private static void keyboard_at_init_materiel()
    {
        Etat8042.nClavier = Etat8042.nControleur = Etat8042.nSouris = 0;
        key_queue_start = key_queue_end = key_ctrl_queue_start = key_ctrl_queue_end = 0;
        mouse_queue_start = mouse_queue_end = 0;
    }

    // pcem bug, fixed in hardware mode: PB-254 — un octet du clavier entre dans son tampon : seize, puis le code de
    //   débordement, puis rien.
    private static void keyboard_at_clavier_materiel(uint8_t val)
    {
        var n = Etat8042.nClavier + compte(key_queue_start, key_queue_end);
        if (n > 16)
        {
                ModeMateriel.Sonde[254]++;
                return;
        }
        if (n == 16)
        {
                val = 0x00;
                ModeMateriel.Sonde[254]++;
        }
        Etat8042.clavier[Etat8042.nClavier++] = val;
    }

    // pcem bug, fixed in hardware mode: PB-254 — la commande FFh (reset) du clavier vide son tampon.
    private static void keyboard_at_clavier_vider_materiel()
    {
        Etat8042.nClavier = 0;
    }

    // pcem bug, fixed in hardware mode: PB-254 — la réponse du contrôleur, mise en réserve si la file est pleine ou si
    //   la réserve attend déjà : l'ordre se garde. Puis, comme keyboard_at_adddata, l'octet du clavier qui attendait
    //   le tampon de sortie passe après.
    private static bool keyboard_at_controleur_materiel(uint8_t val)
    {
        if (Etat8042.nControleur == 0 && compte(key_ctrl_queue_start, key_ctrl_queue_end) < 15)
                return false;
        if (Etat8042.nControleur < RESERVE)
                Etat8042.controleur[Etat8042.nControleur++] = val;
        ModeMateriel.Sonde[254]++;
        if ((keyboard_at_.out_new & 0x300) == 0)
        {
                keyboard_at_.out_delayed = keyboard_at_.out_new;
                keyboard_at_.out_new = -1;
        }
        return true;
    }

    // pcem bug, fixed in hardware mode: PB-254 — l'octet de la souris, de même.
    private static bool keyboard_at_souris_materiel(uint8_t val)
    {
        if (Etat8042.nSouris == 0 && compte(mouse_queue_start, mouse_queue_end) < 15)
                return false;
        if (Etat8042.nSouris < RESERVE)
                Etat8042.souris[Etat8042.nSouris++] = val;
        ModeMateriel.Sonde[254]++;
        return true;
    }

    // pcem bug, fixed in hardware mode: PB-254 — le reset de la souris (FFh) vide aussi sa réserve.
    internal static void keyboard_at_souris_vider_materiel()
    {
        Etat8042.nSouris = 0;
    }

    // pcem bug, fixed in hardware mode: PB-254 — le self-test du contrôleur (AAh), quand il vide sa file, vide aussi sa
    //   réserve.
    private static void keyboard_at_controleur_vider_materiel()
    {
        Etat8042.nControleur = 0;
    }

    // pcem bug, fixed in hardware mode: PB-254 — en tête du poll : les réserves passent dans les files de PCem tant
    //   qu'elles y tiennent ; le tampon du clavier livre quand la sienne est vide, et le 8042 traduit ce qu'il reçoit
    //   (keyboard_at_adddata_keyboard). Un F0h traduit n'occupe pas la file : l'octet qui le suit passe au même poll.
    private static void keyboard_at_livrer_materiel()
    {
        while (Etat8042.nControleur != 0 && compte(key_ctrl_queue_start, key_ctrl_queue_end) < 15)
        {
                key_ctrl_queue[key_ctrl_queue_end] = retirer(Etat8042.controleur, ref Etat8042.nControleur);
                key_ctrl_queue_end = (key_ctrl_queue_end + 1) & 0xf;
        }
        while (Etat8042.nSouris != 0 && compte(mouse_queue_start, mouse_queue_end) < 15)
        {
                mouse_queue[mouse_queue_end] = retirer(Etat8042.souris, ref Etat8042.nSouris);
                mouse_queue_end = (mouse_queue_end + 1) & 0xf;
        }
        while (Etat8042.nClavier != 0 && key_queue_start == key_queue_end)
                keyboard_at_traduire_materiel(retirer(Etat8042.clavier, ref Etat8042.nClavier));
    }

    // La traduction de keyboard_at_adddata_keyboard (keyboard_at.c:218-231), le jeu 2 vers le jeu 1 sous mem[0] bit 6,
    //   et l'ajout à la file : le même code, appelé à la livraison plutôt qu'à l'arrivée.
    private static void keyboard_at_traduire_materiel(uint8_t val)
    {
        if (keyboard_at_.translate != 0)
        {
                if (val == 0xf0)
                {
                        keyboard_at_.next_is_release = 1;
                        return;
                }
                val = at_translation[val];
                if (keyboard_at_.next_is_release != 0)
                        val |= 0x80;
                keyboard_at_.next_is_release = 0;
        }
        key_queue[key_queue_end] = val;
        key_queue_end = (key_queue_end + 1) & 0xf;
    }
}
