// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/lpt/lpt.c + includes/private/lpt/lpt.h
//         + includes/public/pcem/devices.h:99-118 (lpt_device_t, LPT_DEVICE)
//         + src/plugin-api/device.c:21, :240-252 (lpt_devices, lpt_count, pcem_add_lpt)
// STATUS: transcribed — lpt1_device_name, lpt1_current, l_none/l_dss/l_lpt_dac/l_lpt_dac_stereo,
//         lpt_device_get_name, lpt_device_get_internal_name, lpt_device_has_config,
//         lpt_get_device, lpt_device_get_from_internal_name, lpt1_device_init/close,
//         lpt1_write/read, lpt2_write/read, lpt_init, lpt1_init, lpt1_remove, lpt2_init,
//         lpt2_remove, lpt2_remove_ams, lpt_init_builtin. Omis : l'Epson LX-810, derrière
//         USE_EXPERIMENTAL_PRINTER (lpt.c:11-14), éteint.
//
// G10.0 (PLAN-G10.md). Jusqu'ici, seuls lpt1_write/read existaient, sans périphérique, pour
// l'Amstrad ; common_init n'appelait pas lpt_init. Désormais LPT1 (378h) et LPT2 (278h) sont
// posés sur toutes les machines, comme chez PCem, et LPT1 peut porter un périphérique : la
// Disney Sound Source (lpt_dss.cs), le Covox (lpt_dac.cs) ou le Covox stéréo.

// CS8981 : `lpt` n'a que des minuscules ASCII — le nom du fichier C, comme rom.cs.
#pragma warning disable CS8981

using static iXtal26.io;

namespace iXtal26.Lpt;

internal delegate object lpt_init_fn();
internal delegate void lpt_close_fn(object p);
internal delegate void lpt_write_data_fn(uint8_t val, object p);
internal delegate void lpt_write_ctrl_fn(uint8_t val, object p);
internal delegate uint8_t lpt_read_status_fn(object p);

// pcem: devices.h:99-112
// omitted: available, speed_changed, force_redraw, add_status_info, config (:103-107) — NULL pour
//   les trois périphériques du registre ; lpt_device_has_config les lit (rend 0).
internal sealed class lpt_device_t
{
    internal string name = "";
    internal uint32_t flags;
    internal lpt_init_fn? init;
    internal lpt_close_fn? close;
    internal object? config;
    internal lpt_write_data_fn? write_data;
    internal lpt_write_ctrl_fn? write_ctrl;
    internal lpt_read_status_fn? read_status;
}

// pcem: devices.h:114-118
internal sealed class LPT_DEVICE
{
    internal string name = "";
    internal string internal_name = "";
    internal lpt_device_t? device;
}

internal static class lpt
{
    // pcem: devices.h — LPT_MAX.
    internal const int LPT_MAX = 64;

    // pcem: lpt.c:16-18
    internal static string lpt1_device_name = "";
    internal static int lpt1_current = 0;

    // pcem: device.c:21 — le registre, rempli par lpt_init_builtin (pcem_add_lpt).
    internal static readonly LPT_DEVICE?[] lpt_devices = new LPT_DEVICE?[LPT_MAX];

    // pcem: lpt.c:22-25
    internal static readonly LPT_DEVICE l_none = new() { name = "None", internal_name = "none", device = null };
    internal static readonly LPT_DEVICE l_dss = new() { name = "Disney Sound Source", internal_name = "dss", device = lpt_dss.dss_device };
    internal static readonly LPT_DEVICE l_lpt_dac = new() { name = "LPT DAC / Covox Speech Thing", internal_name = "lpt_dac", device = lpt_dac.lpt_dac_device };
    internal static readonly LPT_DEVICE l_lpt_dac_stereo = new() { name = "Stereo LPT DAC", internal_name = "lpt_dac_stereo", device = lpt_dac.lpt_dac_stereo_device };

    // pcem: lpt.c:27-31
    internal static string? lpt_device_get_name(int id)
    {
        if (lpt_devices[id] == null || lpt_devices[id]!.name.Length == 0)
                return null;
        return lpt_devices[id]!.name;
    }

    // pcem: lpt.c:32-36
    internal static string? lpt_device_get_internal_name(int id)
    {
        if (lpt_devices[id] == null || lpt_devices[id]!.internal_name.Length == 0)
                return null;
        return lpt_devices[id]!.internal_name;
    }

    // pcem: lpt.c:38-39
    private static lpt_device_t? lpt1_device;
    private static object? lpt1_device_p;

    // pcem: lpt.c:41-55
    internal static void lpt1_device_init()
    {
        int c = 0;

        while (lpt_devices[c] != null && string.CompareOrdinal(lpt_devices[c]!.internal_name, lpt1_device_name) != 0 &&
               lpt_devices[c]!.internal_name.Length != 0)
                c++;

        if (lpt_devices[c] == null || lpt_devices[c]!.internal_name.Length == 0)
                lpt1_device = null;
        else if (lpt_devices[c] != null)
        {
                lpt1_device = lpt_devices[c]!.device;
                if (lpt1_device != null)
                        lpt1_device_p = lpt1_device.init!();
        }
    }

    // pcem: lpt.c:57-61
    internal static void lpt1_device_close()
    {
        if (lpt1_device != null)
                lpt1_device.close!(lpt1_device_p!);
        lpt1_device = null;
    }

    // pcem: lpt.c:63-64
    private static uint8_t lpt1_dat, lpt2_dat;
    private static uint8_t lpt1_ctrl, lpt2_ctrl;

    // pcem: lpt.c:66-79
    internal static void lpt1_write(uint16_t port, uint8_t val, object? priv)
    {
        switch (port & 3)
        {
        case 0:
                if (lpt1_device != null)
                        lpt1_device.write_data!(val, lpt1_device_p!);
                lpt1_dat = val;
                break;
        case 2:
                if (lpt1_device != null)
                        lpt1_device.write_ctrl!(val, lpt1_device_p!);
                lpt1_ctrl = val;
                break;
        }
    }

    // pcem: lpt.c:80-84
    internal static int lpt_device_has_config(int devId)
    {
        if (lpt_devices[devId] == null || lpt_devices[devId]!.device == null)
                return 0;
        return lpt_devices[devId]!.device!.config != null ? 1 : 0;
    }

    // pcem: lpt.c:86-91
    internal static lpt_device_t? lpt_get_device(int devId)
    {
        if (lpt_devices[devId] == null || lpt_devices[devId]!.device == null)
                return null;

        return lpt_devices[devId]!.device;
    }

    // pcem: lpt.c:93-104
    internal static int lpt_device_get_from_internal_name(string s)
    {
        int c = 0;

        while (lpt_devices[c] != null && lpt_devices[c]!.internal_name.Length != 0)
        {
                if (string.CompareOrdinal(lpt_devices[c]!.internal_name, s) == 0)
                        return c;
                c++;
        }

        return 0;
    }

    // pcem: lpt.c:106-118
    internal static uint8_t lpt1_read(uint16_t port, object? priv)
    {
        switch (port & 3)
        {
        case 0:
                return lpt1_dat;
        case 1:
                if (lpt1_device != null)
                        return lpt1_device.read_status!(lpt1_device_p!);
                return 0;
        case 2:
                return lpt1_ctrl;
        }
        return 0xff;
    }

    // pcem: lpt.c:120-129
    internal static void lpt2_write(uint16_t port, uint8_t val, object? priv)
    {
        switch (port & 3)
        {
        case 0:
                lpt2_dat = val;
                break;
        case 2:
                lpt2_ctrl = val;
                break;
        }
    }

    // pcem: lpt.c:130-140
    internal static uint8_t lpt2_read(uint16_t port, object? priv)
    {
        switch (port & 3)
        {
        case 0:
                return lpt2_dat;
        case 1:
                return 0;
        case 2:
                return lpt2_ctrl;
        }
        return 0xff;
    }

    // pcem: lpt.c:142-145
    internal static void lpt_init()
    {
        io_sethandler(0x0378, 0x0003, lpt1_read, null, null, lpt1_write, null, null, null);
        io_sethandler(0x0278, 0x0003, lpt2_read, null, null, lpt2_write, null, null, null);
    }

    // pcem: lpt.c:147-150
    internal static void lpt1_init(uint16_t port)
    {
        if (port != 0)
                io_sethandler(port, 0x0003, lpt1_read, null, null, lpt1_write, null, null, null);
    }

    // pcem: lpt.c:151-155
    internal static void lpt1_remove()
    {
        io_removehandler(0x0278, 0x0003, lpt1_read, null, null, lpt1_write, null, null, null);
        io_removehandler(0x0378, 0x0003, lpt1_read, null, null, lpt1_write, null, null, null);
        io_removehandler(0x03bc, 0x0003, lpt1_read, null, null, lpt1_write, null, null, null);
    }

    // pcem: lpt.c:156-159
    internal static void lpt2_init(uint16_t port)
    {
        if (port != 0)
                io_sethandler(port, 0x0003, lpt2_read, null, null, lpt2_write, null, null, null);
    }

    // pcem: lpt.c:160-164
    internal static void lpt2_remove()
    {
        io_removehandler(0x0278, 0x0003, lpt2_read, null, null, lpt2_write, null, null, null);
        io_removehandler(0x0378, 0x0003, lpt2_read, null, null, lpt2_write, null, null, null);
        io_removehandler(0x03bc, 0x0003, lpt2_read, null, null, lpt2_write, null, null, null);
    }

    // pcem: lpt.c:166
    // pcem bug, fixed in hardware mode: PB-101 — les gestionnaires de lpt2 sont à 278h, pas à 379h : ce
    //   retrait ne trouve rien.
    internal static void lpt2_remove_ams() { io_removehandler(0x0379, 0x0002, lpt2_read, null, null, lpt2_write, null, null, null); }

    // pcem: device.c:240-252 — lpt_count, pcem_add_lpt.
    private static int lpt_count()
    {
        int ret = 0;
        while (lpt_devices[ret] != null && ret < LPT_MAX)
                ret++;
        return ret;
    }

    internal static void pcem_add_lpt(LPT_DEVICE lpt) { lpt_devices[lpt_count()] = lpt; }

    // pcem: lpt.c:168-172
    internal static void lpt_init_builtin()
    {
        pcem_add_lpt(l_none);
        pcem_add_lpt(l_dss);
        pcem_add_lpt(l_lpt_dac);
        pcem_add_lpt(l_lpt_dac_stereo);
    }

    // iXtal26 — le registre est rempli une fois par processus, comme pcem_add_lpt à l'amorçage de
    // l'application (wx-main, plugin), et non à chaque initpc.
    static lpt() { lpt_init_builtin(); }
}
