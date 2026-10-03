// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/video/vid_unk_ramdac.c + includes/private/video/vid_unk_ramdac.h
// STATUS: transcribed — unk_ramdac_t, unk_ramdac_out, unk_ramdac_in.
//
// G9.3 (PLAN-G9.md). Le RAMDAC de la Tseng ET4000AX, un Sierra SC1502x HiColor : quatre lectures
// de 3C6h arment le registre de commande, la lecture suivante le rend, l'écriture suivante choisit
// la profondeur.

using static iXtal26.Video.vid_svga;

namespace iXtal26.Video;

// pcem: vid_unk_ramdac.h:3-6
internal sealed class unk_ramdac_t
{
    internal int state;
    internal uint8_t ctrl;
}

/*It is unknown exactly what RAMDAC this is
  It is possibly a Sierra 1502x
  It's addressed by the TLIVESA1 driver for ET4000*/
/* Note by Tenshi: Not possibly, this *IS* a Sierra 1502x. */
internal static class vid_unk_ramdac
{
    // pcem: vid_unk_ramdac.c:14-76
    internal static void unk_ramdac_out(uint16_t addr, uint8_t val, unk_ramdac_t ramdac, svga_t svga)
    {
        // pclog("OUT RAMDAC %04X %02X\n",addr,val);
        int oldbpp = 0;
        switch (addr)
        {
        case 0x3C6:
                if (ramdac.state == 4)
                {
                        ramdac.state = 0;
                        // pcem bug, reproduced: PB-100 — FFh écrit une fois armé ne touche pas le
                        //   registre de commande et tombe dans svga_out (le masque des pixels) ;
                        //   et le décodage ci-dessous rend 32 bits, que le SC1502x n'a pas.
                        if (val == 0xFF) break;
                        ramdac.ctrl = val;
                        oldbpp = svga.bpp;
                        switch ((val & 1) | ((val & 0xC0) >> 5))
                        {
                        case 0:
                                svga.bpp = 8;
                                break;
                        case 2:
                        case 3:
                                switch (val & 0x20)
                                {
                                case 0x00: svga.bpp = 32; break;
                                case 0x20: svga.bpp = 24; break;
                                }
                                break;
                        case 4:
                        case 5:
                                svga.bpp = 15;
                                break;
                        case 6:
                                svga.bpp = 16;
                                break;
                        case 7:
                                switch (val & 4)
                                {
                                case 4:
                                        switch (val & 0x20)
                                        {
                                        case 0x00: svga.bpp = 32; break;
                                        case 0x20: svga.bpp = 24; break;
                                        }
                                        break;
                                case 0:
                                default:
                                        svga.bpp = 16;
                                        break;
                                }
                                // pcem: :61-62 — le `case 7` tombe dans `case 1: default: break;`,
                                //   sans effet.
                                break;
                        case 1:
                        default:
                                break;
                        }
                        if (oldbpp != svga.bpp)
                        {
                                svga_recalctimings(svga);
                        }
                        // pclog("unk_ramdac: set to %02X (b5 = %i) [%02X], %i bpp\n", (val&1)|((val&0xC0)>>5), val & 0x20 ? 1 : 0, val, svga->bpp);
                        return;
                }
                ramdac.state = 0;
                break;
        case 0x3C7:
        case 0x3C8:
        case 0x3C9:
                ramdac.state = 0;
                break;
        }
        svga_out(addr, val, svga);
    }

    // pcem: vid_unk_ramdac.c:78-96
    internal static uint8_t unk_ramdac_in(uint16_t addr, unk_ramdac_t ramdac, svga_t svga)
    {
        // pclog("IN RAMDAC %04X\n",addr);
        switch (addr)
        {
        case 0x3C6:
                if (ramdac.state == 4)
                {
                        ramdac.state = 0;
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
