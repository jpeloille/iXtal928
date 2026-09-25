// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/video/vid_tkd8001_ramdac.c + includes/private/video/vid_tkd8001_ramdac.h
// STATUS: transcribed — tkd8001_ramdac_t, tkd8001_ramdac_out, tkd8001_ramdac_in.

using static iXtal26.Video.vid_svga;

namespace iXtal26.Video;

// pcem: vid_tkd8001_ramdac.h:3-6
// Classe et non struct : son adresse est prise (&tvga->ramdac, vid_tvga.c:88).
internal sealed class tkd8001_ramdac_t
{
    internal int state;
    internal uint8_t ctrl;
}

/*Trident TKD8001 RAMDAC emulation*/
internal static partial class vid_tkd8001_ramdac
{
    // pcem: vid_tkd8001_ramdac.c:8-43
    internal static void tkd8001_ramdac_out(uint16_t addr, uint8_t val, tkd8001_ramdac_t ramdac, svga_t svga)
    {
        switch (addr)
        {
        case 0x3C6:
                if (ramdac.state == 4)
                {
                        ramdac.state = 0;
                        ramdac.ctrl = val;
                        switch (val >> 5)
                        {
                        case 0:
                        case 1:
                        case 2:
                        case 3:
                                svga.bpp = 8;
                                break;
                        case 5:
                                svga.bpp = 15;
                                break;
                        case 6:
                                svga.bpp = 24;
                                break;
                        case 7:
                                svga.bpp = 16;
                                break;
                        }
                        return;
                }
                break;
        case 0x3C7:
        case 0x3C8:
        case 0x3C9:
                ramdac.state = 0;
                break;
        }
        svga_out(addr, val, svga);
    }

    // pcem: vid_tkd8001_ramdac.c:45-62
    internal static uint8_t tkd8001_ramdac_in(uint16_t addr, tkd8001_ramdac_t ramdac, svga_t svga)
    {
        switch (addr)
        {
        case 0x3C6:
                if (ramdac.state == 4)
                {
                        return ramdac.ctrl;
                }
                ramdac.state++;
                break;
        case 0x3C7:
        case 0x3C8:
        case 0x3C9:
                ramdac.state = 0;
                break;
        }
        return svga_in(addr, svga);
    }
}
