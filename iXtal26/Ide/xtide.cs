// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/ide/xtide.c  (xtide.h : includes/private/ide/xtide.h)
// STATUS: partial — xtide_t, xtide_write, xtide_read, xtide_init, xtide_close, xtide_available,
//         xtide_device. Omis : xtide_at_device et xtide_ps1_device (xtide_at_init,
//         xtide_ps1_init, leurs *_available) — la variante XT seule (PLAN.md, PLAN-G10.md).
//
// G10.2 (PLAN-G10.md). Le XTIDE, version XT : une carte 8 bits qui porte l'IDE de G5 (ide.c) sur un
// bus ISA d'XT. Sa ROM (ide_xt.bin, le XTIDE Universal BIOS v2.0.0 β3, en C8000) donne l'INT 13h du
// disque dur aux machines 8088 et 8086 dont le BIOS n'en a pas. Les registres de la tâche ATA sont
// en 300h-307h, le contrôle en 30Eh ; le mot de données passe en deux octets, l'octet haut par le
// verrou de 308h — écrit AVANT l'octet bas, lu APRÈS. Les ports IDE standard (1F0h, 170h) sont
// retirés : le XTIDE est le seul chemin vers les lecteurs.

#pragma warning disable CS8600, CS8602
// CS8981 : `xtide` n'a que des minuscules ASCII — le nom de l'unité C, comme ide.cs.
#pragma warning disable CS8981

using iXtal26.Flash;
using iXtal26.PluginApi;
using static iXtal26.io;

namespace iXtal26.Ide;

// pcem: xtide.c:11-14
internal sealed class xtide_t
{
    internal uint8_t data_high;
    internal rom_t bios_rom = new();
}

internal static class xtide
{
    // pcem: xtide.c:16-42
    private static void xtide_write(uint16_t port, uint8_t val, object p)
    {
        xtide_t xtide = (xtide_t)p;

        switch (port & 0xf)
        {
        case 0x0:
                ide.writeidew(0, (uint16_t)(val | (xtide.data_high << 8)));
                return;

        case 0x1:
        case 0x2:
        case 0x3:
        case 0x4:
        case 0x5:
        case 0x6:
        case 0x7:
                ide.writeide(0, (uint16_t)((port & 0xf) | 0x1f0), val);
                return;

        case 0x8:
                xtide.data_high = val;
                return;

        case 0xe:
                ide.writeide(0, 0x3f6, val);
                return;
        }
    }

    // pcem: xtide.c:44-71
    private static uint8_t xtide_read(uint16_t port, object p)
    {
        xtide_t xtide = (xtide_t)p;
        uint16_t tempw;

        switch (port & 0xf)
        {
        case 0x0:
                tempw = ide.readidew(0);
                xtide.data_high = (uint8_t)(tempw >> 8);
                return (uint8_t)(tempw & 0xff);

        case 0x1:
        case 0x2:
        case 0x3:
        case 0x4:
        case 0x5:
        case 0x6:
        case 0x7:
                return ide.readide(0, (uint16_t)((port & 0xf) | 0x1f0));

        case 0x8:
                return xtide.data_high;

        case 0xe:
                return ide.readide(0, 0x3f6);
        }

        return 0xff;
    }

    // pcem: xtide.c:73-84
    private static object? xtide_init()
    {
        xtide_t xtide = new();
        // pcem: :74-75 — malloc + memset ; `new` zéro-initialise.

        rom.rom_init(xtide.bios_rom, "ide_xt.bin", 0xc8000, 0x4000, 0x3fff, 0, Memory.mem.MEM_MAPPING_EXTERNAL);
        device.device_add(ide.ide_device);
        ide.ide_pri_disable();
        ide.ide_sec_disable();
        io_sethandler(0x0300, 0x0010, xtide_read, null, null, xtide_write, null, null, xtide);

        return xtide;
    }

    // pcem: xtide.c:106-110
    // pcem bug, reproduced: PB-98 — free sans rom_deinit : la projection de la ROM reste dans la liste
    //   de mem.c jusqu'au mem_alloc de l'amorçage suivant (le Xebec, lui, la retire,
    //   mfm_xebec.c:766-774). Sans effet observable ; l'objet reste vivant sous GC.
    private static void xtide_close(object? p)
    {
        // omitted: free(xtide) — le GC s'en charge.
    }

    // pcem: xtide.c:112
    private static int xtide_available() => rom.rom_present("ide_xt.bin");

    // pcem: xtide.c:118
    internal static device_t xtide_device = new device_t("XTIDE", 0, xtide_init, xtide_close, xtide_available,
                                                         null, null, null, null);
}
