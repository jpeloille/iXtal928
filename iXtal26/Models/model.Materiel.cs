// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel des machines (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans model.c, appelées par les gardes de model.cs (`if (materiel.pb_nn)`),
// seules sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans l'entrée PB de
// PCEM_BUGS.md.

namespace iXtal26.Models;

internal static partial class model_c
{
    // pcem bug, fixed in hardware mode: PB-256 — la M24 et le PC1512 rafraîchissent leur mémoire comme le 5150 et
    //   l'XT : la sortie 1 du 8253 demande le canal 0 du DMA (Amstrad PC1512 Technical Reference Manual, section 1,
    //   § 1.5, 1.5.1 et 1.7.2, toutes les 15,13 µs ; la ROM 1.43 de la M24 pose le canal 0 en 58h et le compteur 1
    //   à 13h, F000:DC41-DC5A). Le branchement de xt_init (model.c:205) ; le coût d'un cycle, celui de PCem pour
    //   l'XT (refreshread : quatre cycles et l'achèvement du fetch), n'est pas documenté pour ces deux UC — un temps
    //   déduit.
    private static void model_rafraichissement_materiel()
    {
        pit.pit_set_out_func(pit.pit_, 1, pit.pit_refresh_timer_xt);
        ModeMateriel.Sonde[256]++;
    }

    // ===== G13.13b — les cartes de COM et de LPT =====

    // Les clés que PCem n'a pas, dans une classe à part : un champ statique de plus dans model_c changerait son
    // constructeur statique. Seul loadconfig les pose (model_ports_lire), à chaque chargement ; une valeur illisible
    // rend le défaut (sscanf_i), la carte présente.
    private static class PortsCarteMere
    {
        /// <summary>COM1 (3F8h, l'IRQ 4), COM2 (2F8h, l'IRQ 3), LPT1 (378h) et LPT2 (278h) : présents sans clé, les
        /// quatre ports de common_init.</summary>
        internal static bool com1 = true, com2 = true, lpt1 = true, lpt2 = true;
    }

    // G13.13b (Q25, Q26 ; décisions n° 14 et 17) — serial1, serial2, parallel1 et parallel2, dans les deux modes : 1,
    //   la carte présente à son adresse (le défaut, les ports de PCem), 0, la carte retirée ; une autre valeur est
    //   refusée, 1 à la place. Lues par loadconfig après la souris et le périphérique de LPT1 : COM1 retiré sous la
    //   souris série (elle suit serial1, mouse_serial.cs), LPT1 retiré sous un périphérique de LPT1 sont avertis. Le
    //   PC1512 porte COM1 et LPT1 sur sa carte mère (sa ROM pose COM1 en 3F8h sans le sonder, F000:C8A3-C8A8 ; LPT1,
    //   PB-101) : ils restent, avec un avertissement.
    internal static void model_ports_lire()
    {
        PortsCarteMere.com1 = model_port_present("serial1", "COM1");
        PortsCarteMere.com2 = model_port_present("serial2", "COM2");
        PortsCarteMere.lpt1 = model_port_present("parallel1", "LPT1");
        PortsCarteMere.lpt2 = model_port_present("parallel2", "LPT2");
        int souris = Mouse.mouse.mouse_get_type(Mouse.mouse.mouse_type) & Mouse.mouse.MOUSE_TYPE_IF_MASK;
        if (!PortsCarteMere.com1 && souris == Mouse.mouse.MOUSE_TYPE_SERIAL)
                Console.Error.WriteLine($"serial1 = 0 : COM1 retiré ; la souris série (mouse_type = " +
                                        $"{Mouse.mouse.mouse_type}), qui s'y branche, ne répondra pas.");
        if (!PortsCarteMere.lpt1 && Lpt.lpt.lpt1_current != 0)
                Console.Error.WriteLine($"parallel1 = 0 : LPT1 retiré ; lpt1_device = « {Lpt.lpt.lpt1_device_name} »" +
                                        ", qui s'y branche, ne recevra rien.");
    }

    private static bool model_port_present(string cle, string port)
    {
        int v = PluginApi.config.config_get_int(PluginApi.config.CFG_MACHINE, null, cle, 1);
        if (v != 0 && v != 1)
        {
                Console.Error.WriteLine($"{cle} = {v} : hors de la liste (0 : {port} retiré, 1 : présent) ; 1 à la " +
                                        "place.");
                return true;
        }
        if (v == 0 && (port is "COM1" or "LPT1") && model_get_internal_name() == "pc1512")
        {
                Console.Error.WriteLine($"{cle} = 0 : le PC1512 porte {port} sur sa carte mère ; {port} reste.");
                return true;
        }
        return v == 1;
    }

    // G13.13b — à la fin de common_init : retire les cartes que les clés disent absentes (model_ports_lire). Le LPT
    //   suit lpt_jeu_hors_service : sans LPT, rien à retirer.
    private static void model_ports_retirer()
    {
        if (!PortsCarteMere.com1)
                serial.serial1_remove();
        if (!PortsCarteMere.com2)
                serial.serial2_remove();
        if (lpt_jeu_hors_service)
                return;
        if (!PortsCarteMere.lpt1)
                Lpt.lpt.lpt1_remove();
        if (!PortsCarteMere.lpt2)
                Lpt.lpt.lpt2_remove();
    }
}
