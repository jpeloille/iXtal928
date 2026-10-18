// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel du Fixed Disk and Diskette Drive Adapter de l'AT (G13 ; R10 de
// TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans mfm_at.c, appelées par les gardes de mfm_at.cs (`if (materiel.pb_nn)`),
// seules sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans l'entrée PB de
// PCEM_BUGS.md.

using iXtal26.PluginApi;
using static iXtal26.io;
using static iXtal26.PluginApi.device;

namespace iXtal26.Mfm;

internal static partial class mfm_at
{
    // pcem bug, fixed in hardware mode: PB-25 — les têtes se numérotent depuis 0 (Drive/Head, 1F6h, bits 0-3) : la tête
    //   égale au nombre de têtes, celui de SET PARAMETERS ou celui de l'image, n'existe pas ; aucun champ ID ne s'y
    //   présente, et mfm_at rend IDNF (10h) pour tout refus d'adresse (déduit, PCEM_BUGS.md). PCem la laisse passer (`>`)
    //   et lit la tête 0 du cylindre suivant.
    private static bool mfm_tete_materiel(mfm_t mfm, mfm_drive_t drive)
    {
        if (mfm.head < drive.cfg_hpc && mfm.head < drive.hdd_file.hpc)
                return false;
        ModeMateriel.Sonde[25]++;
        return true;
    }

    // ===== G13.12b — le cavalier du disque =====

    // La table que PCem n'a pas (`config = null`), dans une classe à part : un champ statique de plus dans mfm_at
    // changerait son constructeur statique.
    private static class CavalierAt
    {
        /// <summary>Le task file en primaire, 1F0h-1F7h, ou en secondaire, 170h-177h (p. 2) ; l'IRQ 14 dans les deux
        /// (p. 36).</summary>
        internal static readonly device_config_t[] config =
        [
            new device_config_t { name = "addr", type = CONFIG_SELECTION, default_int = 0x1f0,
                selection = [new() { description = "1F0h", value = 0x1f0 },
                             new() { description = "170h", value = 0x170 }] },
            new device_config_t { type = -1 },
        ];
    }

    // G13.12b (Q25, Q26, Q31) — addr = 170h, dans les deux modes : la carte répond en 170h-177h et 376h, les ports
    //   de 1F0h-1F7h et 3F6h décalés de 80h vers les gestionnaires de PCem, qui distinguent les registres par leur
    //   port. Le BIOS de l'AT n'adresse que 1F0h (sa ROM charge 1F0h, 1F7h et 3F6h, jamais 170h, 177h ni 376h) :
    //   averti.
    private static void mfm_at_secondaire(mfm_t mfm)
    {
        Console.Error.WriteLine($"iXtal26 : [{mfm_at_device.name}] addr = 170h : la carte répond en 170h-177h et " +
                                "376h, que le BIOS de l'AT n'adresse pas (il ne connaît que 1F0h) ; il ne trouvera " +
                                "pas le disque.");
        io_sethandler(0x0170, 0x0001, (a, p) => mfm_read((uint16_t)(a + 0x80), p),
                      (a, p) => mfm_readw((uint16_t)(a + 0x80), p), null,
                      (a, v, p) => mfm_write((uint16_t)(a + 0x80), v, p),
                      (a, v, p) => mfm_writew((uint16_t)(a + 0x80), v, p), null, mfm);
        io_sethandler(0x0171, 0x0007, (a, p) => mfm_read((uint16_t)(a + 0x80), p), null, null,
                      (a, v, p) => mfm_write((uint16_t)(a + 0x80), v, p), null, null, mfm);
        io_sethandler(0x0376, 0x0001, null, null, null, (a, v, p) => mfm_write((uint16_t)(a + 0x80), v, p), null, null,
                      mfm);
    }
}
