// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/hdd/hdd.c:149-164 (le registre hdd_controllers[])
//         + wx-config.c:237-242 (le filtre DEVICE_AT contre MODEL_AT)
// STATUS: host
//
// LES CONTRÔLEURS DE DISQUE DUR QU'ON PEUT CHOISIR, et sur quelle machine.
//
// Une seule liste pour l'écran de construction, --hdd-controller et le contrôleur posé
// d'office par --hdd : trois listes finiraient par diverger, et c'est ce qui était
// arrivé — celle de l'écran s'était arrêtée aux deux cartes du XT, si bien que mfm_at,
// monté par pc.cs depuis le 286, ne se choisissait que dans un .cfg.
//
// Le filtre est celui de PCem : une carte marquée DEVICE_AT (mfm_at_device,
// mfm_at.c:533) n'est proposée que sur une machine MODEL_AT. Le Xebec et le DTC, cartes
// ISA 8 bits sans drapeau, sont proposés partout — un AT peut les recevoir.
//
// omitted: les treize autres entrées du registre (ESDI, IDE, XTIDE, SCSI) — aucune n'est
//   transcrite.

namespace iXtal26.Host;

internal static class HardDiskControllers
{
    internal readonly record struct Controller(string InternalName, string Label, bool RequiresAtMachine);

    internal static readonly Controller[] All =
    [
        new("mfm_xebec", "IBM Fixed Disk Adapter", false),
        new("dtc5150x", "DTC 5150X", false),
        new("mfm_at", "IBM AT Fixed Disk Adapter", true),
    ];

    internal static bool CurrentMachineIsAt =>
        (Models.model_c.models[Models.model_c.model].flags & Models.model_c.MODEL_AT) != 0;

    internal static bool IsAvailable(Controller controller) =>
        !controller.RequiresAtMachine || CurrentMachineIsAt;

    /// <summary>« » (aucun) est toujours disponible ; un nom inconnu ne l'est jamais.</summary>
    internal static bool IsAvailable(string internalName)
    {
        if (internalName.Length == 0)
            return true;

        foreach (var controller in All)
        {
            if (controller.InternalName == internalName)
                return IsAvailable(controller);
        }

        return false;
    }

    /// <summary>
    /// Le contrôleur posé quand une image de disque dur est montée sans qu'on en ait
    /// nommé un. Sur un AT, c'est mfm_at : le BIOS de l'AT a son INT 13h pour disque dur
    /// et attend la carte en 0x1F0 ; le Xebec y tournerait, mais ce n'est pas la machine.
    /// </summary>
    internal static string DefaultForCurrentMachine => CurrentMachineIsAt ? "mfm_at" : "mfm_xebec";

    internal static string AvailableNames()
    {
        var names = new List<string>();

        foreach (var controller in All)
        {
            if (IsAvailable(controller))
                names.Add(controller.InternalName);
        }

        return string.Join(", ", names);
    }
}
