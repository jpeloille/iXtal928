// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/models/dma.c
// STATUS: partial — LES DEUX 8237 : celui du 5150 (canaux 0-3, ports 0x00-0x0f,
//         pages 0x80-0x87) et, depuis B3, celui de l'AT (canaux 4-7, ports
//         0xc0-0xdf, pages 0x88-0x8f) — dma16_read, dma16_write, dma16_init et
//         les trois cases 16 bits de dma_page_write. Les DÉCLARATIONS d'état du C
//         sont transcrites en entier (dma16_wp, dma16_command, dma_ps2,
//         dma_t.ps2_mode). N'est plus omis que le PS/2, marqué sur place.
//         Cette ligne disait « seuls les HANDLERS 16 bits et PS/2 sont omis ».

using iXtal26.Cpu;
using iXtal26.Memory;
using static iXtal26.Cpu.x86;

namespace iXtal26.Models;

// pcem: ibm.h:117-131
internal sealed class dma_t
{
    internal uint32_t ab, ac;
    internal uint16_t cb;
    internal int cc;
    internal int wp;
    internal uint8_t m, mode;
    internal uint8_t page;
    internal uint8_t stat, stat_rq;
    internal uint8_t command;
    internal int size;

    internal uint8_t ps2_mode;
    internal uint8_t arb_level;
    internal uint16_t io_addr;
}

internal static partial class dma
{
    // CS0414 RETIRÉ À B3. Cette classe portait `#pragma warning disable CS0414` parce
    // que dma16_wp était écrit sans jamais être relu : le 5150 n'a que le DMA 8 bits.
    // dma16_read et dma16_write sont transcrits maintenant, donc il a un lecteur, et le
    // build est propre SANS le pragma — vérifié en le retirant.
    // pcem: dma.h:8-9
    internal const int DMA_NODATA = -1;
    internal const int DMA_OVER = 0x10000;

    // pcem: dma.c:11
    internal static dma_t[] dma_ = { new(), new(), new(), new(), new(), new(), new(), new() };

    // pcem: dma.c:13-21
    private static uint8_t[] dmaregs = new uint8_t[16];
    // pcem: dma.c:14 — DÉ-OMISSION : dma16_read et dma16_write sont transcrits depuis
    // B3, et ce tableau est leur seul état propre. Il porte le dernier octet écrit dans
    // chaque registre, et dma16_read le rend TEL QUEL pour les emplacements que son
    // switch ne couvre pas — c'est son `return dma16regs[addr & 0xf]` final.
    private static readonly uint8_t[] dma16regs = new uint8_t[16];
    private static uint8_t[] dmapages = new uint8_t[16];

    private static int dma_wp, dma16_wp;
    private static uint8_t dma_m;
    private static uint8_t dma_stat;
    private static uint8_t dma_stat_rq;
    private static uint8_t dma_command, dma16_command;

    // pcem: dma.c:23-28
    private struct dma_ps2_t
    {
        internal int xfr_command, xfr_channel;
        internal int byte_ptr;

        internal int is_ps2;
    }

    private static dma_ps2_t dma_ps2;

    // omitted: DMA_PS2_IOA, DMA_PS2_XFER_MEM_TO_IO, DMA_PS2_XFER_IO_TO_MEM,
    //          DMA_PS2_XFER_MASK, DMA_PS2_DEC2, DMA_PS2_SIZE16 (dma.c:30-35) —
    //          protocole PS/2, lus par les seuls handlers omis
    // omitted: dma_ps2_run (dma.c:37, 637-718) — PS/2

    // pcem: dma.c:39-55
    internal static void dma_reset()
    {
        int c;

        dma_wp = dma16_wp = 0;
        dma_m = 0;

        for (c = 0; c < 16; c++)
                dmaregs[c] = 0;
        for (c = 0; c < 8; c++)
        {
                dma_[c].mode = 0;
                dma_[c].ac = 0;
                dma_[c].cc = 0;
                dma_[c].ab = 0;
                dma_[c].cb = 0;
                dma_[c].size = (c & 4) != 0 ? 1 : 0;
        }
    }

    // pcem: dma.c:57-92
    internal static uint8_t dma_read(uint16_t addr, object priv)
    {
        int channel = (addr >> 1) & 3;
        uint8_t temp;
        switch (addr & 0xf)
        {
        case 0:
        case 2:
        case 4:
        case 6: /*Address registers*/
                dma_wp ^= 1;
                if (dma_wp != 0)
                        return (uint8_t)(dma_[channel].ac & 0xff);
                return (uint8_t)((dma_[channel].ac >> 8) & 0xff);

        case 1:
        case 3:
        case 5:
        case 7: /*Count registers*/
                dma_wp ^= 1;
                if (dma_wp != 0)
                        temp = (uint8_t)(dma_[channel].cc & 0xff);
                else
                        temp = (uint8_t)(dma_[channel].cc >> 8);
                return temp;

        case 8: /*Status register*/
                temp = (uint8_t)(dma_stat & 0xf);
                dma_stat &= unchecked((uint8_t)~0xf);
                return temp;

        case 0xd:
                return 0;
        }
        return dmaregs[addr & 0xf];
    }

    // pcem: dma.c:94-162
    internal static void dma_write(uint16_t addr, uint8_t val, object priv)
    {
        int channel = (addr >> 1) & 3;
        dmaregs[addr & 0xf] = val;
        switch (addr & 0xf)
        {
        case 0:
        case 2:
        case 4:
        case 6: /*Address registers*/
                dma_wp ^= 1;
                if (dma_wp != 0)
                        dma_[channel].ab = (dma_[channel].ab & 0xffff00) | val;
                else
                        dma_[channel].ab = (dma_[channel].ab & 0xff00ff) | ((uint32_t)val << 8);
                dma_[channel].ac = dma_[channel].ab;
                return;

        case 1:
        case 3:
        case 5:
        case 7: /*Count registers*/
                dma_wp ^= 1;
                if (dma_wp != 0)
                        dma_[channel].cb = (uint16_t)((dma_[channel].cb & 0xff00) | val);
                else
                        dma_[channel].cb = (uint16_t)((dma_[channel].cb & 0x00ff) | (val << 8));
                dma_[channel].cc = dma_[channel].cb;
                return;

        case 8: /*Control register*/
                dma_command = val;
                return;

        case 0xa: /*Mask*/
                if ((val & 4) != 0)
                        dma_m |= (uint8_t)(1 << (val & 3));
                else
                        dma_m &= unchecked((uint8_t)~(1 << (val & 3)));
                return;

        case 0xb: /*Mode*/
                channel = (val & 3);
                dma_[channel].mode = val;
                if (dma_ps2.is_ps2 != 0)
                {
                        dma_[channel].ps2_mode &= unchecked((uint8_t)~0x1c);
                        if ((val & 0x20) != 0)
                                dma_[channel].ps2_mode |= 0x10;
                        if ((val & 0xc) == 8)
                                dma_[channel].ps2_mode |= 4;
                        else if ((val & 0xc) == 4)
                                dma_[channel].ps2_mode |= 0xc;
                }
                return;

        case 0xc: /*Clear FF*/
                dma_wp = 0;
                return;

        case 0xd: /*Master clear*/
                dma_wp = 0;
                dma_m |= 0xf;
                return;

        case 0xf: /*Mask write*/
                dma_m = (uint8_t)((dma_m & 0xf0) | (val & 0xf));
                return;
        }
    }

    // omitted: dma_ps2_read (dma.c:164-224), dma_ps2_write (dma.c:226-311) —
    //          ports 0x18/0x1a du DMA PS/2

    // LE SECOND 8237, CELUI DES TRANSFERTS 16 BITS, ET L'AT L'EXERCE POUR DE VRAI.
    //
    // MESURE, pas déduction : une sonde dlsym sur la globale `dma` (exportée, `nm -D`
    // rend « B dma ») lit zéro partout après h_boot, puis après ~12 millions
    // d'instructions de POST :
    //     dma[4].mode = C0   dma[5].mode = 41   dma[6].mode = 42   dma[7].mode = 43
    //     dma[5].page = 08   dma[6].page = 06   dma[7].page = 06
    // Or dma[4..7].mode n'est écrit QUE par dma16_write (case 0xb) et dma[5..7].page
    // QUE par les trois cases de dma_page_write ci-dessous. Les deux plages de ports
    // ne sont décodées que par dma16_init.
    //
    // SES ADRESSES SONT EN MOTS, PAS EN OCTETS, et c'est tout l'écart avec le premier
    // 8237 : `(ac >> 1) & 0xff` à la lecture, `(val << 1)` à l'écriture. Un canal
    // 16 bits compte des mots, donc son registre d'adresse est décalé d'un bit — et
    // c'est pourquoi la page est masquée à 0xfe et l'adresse à 0x1ffff au lieu de
    // 0xffff. Confondre les deux donnerait des transferts au bon endroit divisé par
    // deux.
    //
    // dma16_wp EST UN BASCULEUR PARTAGÉ par les registres d'adresse ET de compte : un
    // `^= 1` à chaque accès, et c'est lui qui distingue l'octet bas du haut. Les cases
    // 0xc et 0xd le remettent à zéro. Il était déclaré depuis M4 sans lecteur, d'où le
    // CS0414 que l'en-tête mentionnait ; il en a un maintenant, et le pragma est parti.

    // pcem: dma.c:313-350
    internal static uint8_t dma16_read(uint16_t addr, object priv)
    {
        int channel = ((addr >> 2) & 3) + 4;
        uint8_t temp;
        // omitted: printf de trace (dma.c:316) — sortie pure.
        addr >>= 1;
        switch (addr & 0xf)
        {
        case 0:
        case 2:
        case 4:
        case 6: /*Address registers*/
                dma16_wp ^= 1;
                if (dma_ps2.is_ps2 != 0)
                {
                        if (dma16_wp != 0)
                                return (uint8_t)dma_[channel].ac;
                        return (uint8_t)((dma_[channel].ac >> 8) & 0xff);
                }
                if (dma16_wp != 0)
                        return (uint8_t)((dma_[channel].ac >> 1) & 0xff);
                return (uint8_t)((dma_[channel].ac >> 9) & 0xff);

        case 1:
        case 3:
        case 5:
        case 7: /*Count registers*/
                dma16_wp ^= 1;
                if (dma16_wp != 0)
                        temp = (uint8_t)(dma_[channel].cc & 0xff);
                else
                        temp = (uint8_t)(dma_[channel].cc >> 8);
                return temp;

        case 8: /*Status register*/
                temp = (uint8_t)(dma_stat >> 4);
                dma_stat &= unchecked((uint8_t)~0xf0);
                return temp;
        }
        return dma16regs[addr & 0xf];
    }

    // pcem: dma.c:352-426
    internal static void dma16_write(uint16_t addr, uint8_t val, object priv)
    {
        int channel = ((addr >> 2) & 3) + 4;
        // omitted: printf de trace (dma.c:354) — sortie pure.
        addr >>= 1;
        dma16regs[addr & 0xf] = val;
        switch (addr & 0xf)
        {
        case 0:
        case 2:
        case 4:
        case 6: /*Address registers*/
                dma16_wp ^= 1;
                if (dma_ps2.is_ps2 != 0)
                {
                        if (dma16_wp != 0)
                                dma_[channel].ab = (dma_[channel].ab & 0xffff00) | val;
                        else
                                dma_[channel].ab = (dma_[channel].ab & 0xff00ff) | ((uint32_t)val << 8);
                }
                else
                {
                        if (dma16_wp != 0)
                                dma_[channel].ab = (dma_[channel].ab & 0xfffe00) | ((uint32_t)val << 1);
                        else
                                dma_[channel].ab = (dma_[channel].ab & 0xfe01ff) | ((uint32_t)val << 9);
                }
                dma_[channel].ac = dma_[channel].ab;
                return;

        case 1:
        case 3:
        case 5:
        case 7: /*Count registers*/
                dma16_wp ^= 1;
                if (dma16_wp != 0)
                        dma_[channel].cb = (uint16_t)((dma_[channel].cb & 0xff00) | val);
                else
                        dma_[channel].cb = (uint16_t)((dma_[channel].cb & 0x00ff) | (val << 8));
                dma_[channel].cc = dma_[channel].cb;
                return;

        case 8: /*Control register*/
                return;

        case 0xa: /*Mask*/
                if ((val & 4) != 0)
                        dma_m |= (uint8_t)(0x10 << (val & 3));
                else
                        dma_m &= unchecked((uint8_t)~(0x10 << (val & 3)));
                return;

        case 0xb: /*Mode*/
                channel = (val & 3) + 4;
                dma_[channel].mode = val;
                if (dma_ps2.is_ps2 != 0)
                {
                        dma_[channel].ps2_mode &= unchecked((uint8_t)~0x1c);
                        if ((val & 0x20) != 0)
                                dma_[channel].ps2_mode |= 0x10;
                        if ((val & 0xc) == 8)
                                dma_[channel].ps2_mode |= 4;
                        else if ((val & 0xc) == 4)
                                dma_[channel].ps2_mode |= 0xc;
                }
                return;

        case 0xc: /*Clear FF*/
                dma16_wp = 0;
                return;

        case 0xd: /*Master clear*/
                dma16_wp = 0;
                dma_m |= 0xf0;
                return;

        case 0xf: /*Mask write*/
                dma_m = (uint8_t)((dma_m & 0x0f) | ((val & 0xf) << 4));
                return;
        }
    }

    // pcem: dma.c:428-467
    internal static void dma_page_write(uint16_t addr, uint8_t val, object priv)
    {
        dmapages[addr & 0xf] = val;
        switch (addr & 0xf)
        {
        case 1:
                dma_[2].page = (AT != 0) ? val : (uint8_t)(val & 0xf);
                dma_[2].ab = (dma_[2].ab & 0xffff) | ((uint32_t)dma_[2].page << 16);
                dma_[2].ac = (dma_[2].ac & 0xffff) | ((uint32_t)dma_[2].page << 16);
                break;
        case 2:
                dma_[3].page = (AT != 0) ? val : (uint8_t)(val & 0xf);
                dma_[3].ab = (dma_[3].ab & 0xffff) | ((uint32_t)dma_[3].page << 16);
                dma_[3].ac = (dma_[3].ac & 0xffff) | ((uint32_t)dma_[3].page << 16);
                break;
        case 3:
                dma_[1].page = (AT != 0) ? val : (uint8_t)(val & 0xf);
                dma_[1].ab = (dma_[1].ab & 0xffff) | ((uint32_t)dma_[1].page << 16);
                dma_[1].ac = (dma_[1].ac & 0xffff) | ((uint32_t)dma_[1].page << 16);
                break;
        case 7:
                dma_[0].page = (AT != 0) ? val : (uint8_t)(val & 0xf);
                dma_[0].ab = (dma_[0].ab & 0xffff) | ((uint32_t)dma_[0].page << 16);
                dma_[0].ac = (dma_[0].ac & 0xffff) | ((uint32_t)dma_[0].page << 16);
                break;
        // LES TROIS PAGES DES CANAUX 16 BITS. Masque 0xfe et non 0xf, et l'adresse
        // conservée sur 17 bits et non 16 : le bit 16 appartient à l'adresse en mots,
        // pas à la page. Mesuré posées par le POST (voir dma16_write ci-dessus).
        case 0x9:
                dma_[6].page = (uint8_t)(val & 0xfe);
                dma_[6].ab = (dma_[6].ab & 0x1ffff) | ((uint32_t)dma_[6].page << 16);
                dma_[6].ac = (dma_[6].ac & 0x1ffff) | ((uint32_t)dma_[6].page << 16);
                break;
        case 0xa:
                dma_[7].page = (uint8_t)(val & 0xfe);
                dma_[7].ab = (dma_[7].ab & 0x1ffff) | ((uint32_t)dma_[7].page << 16);
                dma_[7].ac = (dma_[7].ac & 0x1ffff) | ((uint32_t)dma_[7].page << 16);
                break;
        case 0xb:
                dma_[5].page = (uint8_t)(val & 0xfe);
                dma_[5].ab = (dma_[5].ab & 0x1ffff) | ((uint32_t)dma_[5].page << 16);
                dma_[5].ac = (dma_[5].ac & 0x1ffff) | ((uint32_t)dma_[5].page << 16);
                break;
        }
    }

    // pcem: dma.c:469
    internal static uint8_t dma_page_read(uint16_t addr, object priv) { return dmapages[addr & 0xf]; }

    // pcem: dma.c:471-475
    internal static void dma_init()
    {
        io.io_sethandler(0x0000, 0x0010, dma_read, null, null, dma_write, null, null, null);
        io.io_sethandler(0x0080, 0x0008, dma_page_read, null, null, dma_page_write, null, null, null);
        dma_ps2.is_ps2 = 0;
    }

    // pcem: dma.c:477-480 — DEUX plages, et la seconde est un ALIAS : dma_page_read et
    // dma_page_write sont les MÊMES handlers qu'en 0x80-0x87, ce qui donne à un port
    // de 0x88-0x8f l'indice `addr & 0xf` de 8 à 15 — d'où les cases 0x9, 0xa et 0xb.
    internal static void dma16_init()
    {
        io.io_sethandler(0x00C0, 0x0020, dma16_read, null, null, dma16_write, null, null, null);
        io.io_sethandler(0x0088, 0x0008, dma_page_read, null, null, dma_page_write, null, null, null);
    }

    // omitted: ps2_dma_init (dma.c:482-486) — ports 0x18/0x1a, pose is_ps2 = 1

    // pcem: dma.c:488-491
    internal static uint8_t _dma_read(uint32_t addr)
    {
        uint8_t temp = mem.mem_readb_phys(addr);
        return temp;
    }

    // pcem: dma.c:493-496
    internal static void _dma_write(uint32_t addr, uint8_t val)
    {
        mem.mem_writeb_phys(addr, val);
        // omitted: mem_invalidate_range(addr, addr) (dma.c:495) — n'écrit que
        //          pages[], chemin dynarec/pagination du registre des omissions
    }

    // pcem: dma.c:498-566
    internal static int dma_channel_read(int channel)
    {
        dma_t dma_c = dma_[channel];
        uint16_t temp;
        int tc = 0;

        if (channel < 4)
        {
                if ((dma_command & 0x04) != 0)
                        return DMA_NODATA;
        }
        else
        {
                if ((dma16_command & 0x04) != 0)
                        return DMA_NODATA;
        }

        if (AT == 0)
                _808x.refreshread();

        if ((dma_m & (1 << channel)) != 0)
                return DMA_NODATA;
        if ((dma_c.mode & 0xC) != 8)
                return DMA_NODATA;

        if (dma_c.size == 0)
        {
                temp = _dma_read(dma_c.ac);

                if ((dma_c.mode & 0x20) != 0)
                {
                        if (dma_ps2.is_ps2 != 0)
                                dma_c.ac--;
                        else
                                dma_c.ac = (dma_c.ac & 0xff0000) | ((dma_c.ac - 1) & 0xffff);
                }
                else
                {
                        if (dma_ps2.is_ps2 != 0)
                                dma_c.ac++;
                        else
                                dma_c.ac = (dma_c.ac & 0xff0000) | ((dma_c.ac + 1) & 0xffff);
                }
        }
        else
        {
                temp = (uint16_t)(_dma_read(dma_c.ac) | (_dma_read(dma_c.ac + 1) << 8));

                if ((dma_c.mode & 0x20) != 0)
                {
                        if (dma_ps2.is_ps2 != 0)
                                dma_c.ac -= 2;
                        else
                                dma_c.ac = (dma_c.ac & 0xfe0000) | ((dma_c.ac - 2) & 0x1ffff);
                }
                else
                {
                        if (dma_ps2.is_ps2 != 0)
                                dma_c.ac += 2;
                        else
                                dma_c.ac = (dma_c.ac & 0xfe0000) | ((dma_c.ac + 2) & 0x1ffff);
                }
        }

        dma_stat_rq |= (uint8_t)(1 << channel);

        dma_c.cc--;
        if (dma_c.cc < 0)
        {
                tc = 1;
                if ((dma_c.mode & 0x10) != 0) /*Auto-init*/
                {
                        dma_c.cc = dma_c.cb;
                        dma_c.ac = dma_c.ab;
                }
                else
                        dma_m |= (uint8_t)(1 << channel);
                dma_stat |= (uint8_t)(1 << channel);
        }

        if (tc != 0)
                return temp | DMA_OVER;
        return temp;
    }

    // pcem: dma.c:568-635
    internal static int dma_channel_write(int channel, uint16_t val)
    {
        dma_t dma_c = dma_[channel];

        if (channel < 4)
        {
                if ((dma_command & 0x04) != 0)
                        return DMA_NODATA;
        }
        else
        {
                if ((dma16_command & 0x04) != 0)
                        return DMA_NODATA;
        }

        if (AT == 0)
                _808x.refreshread();

        if ((dma_m & (1 << channel)) != 0)
                return DMA_NODATA;
        if ((dma_c.mode & 0xC) != 4)
                return DMA_NODATA;

        if (dma_c.size == 0)
        {
                _dma_write(dma_c.ac, (uint8_t)val);

                if ((dma_c.mode & 0x20) != 0)
                {
                        if (dma_ps2.is_ps2 != 0)
                                dma_c.ac--;
                        else
                                dma_c.ac = (dma_c.ac & 0xff0000) | ((dma_c.ac - 1) & 0xffff);
                }
                else
                {
                        if (dma_ps2.is_ps2 != 0)
                                dma_c.ac++;
                        else
                                dma_c.ac = (dma_c.ac & 0xff0000) | ((dma_c.ac + 1) & 0xffff);
                }
        }
        else
        {
                _dma_write(dma_c.ac, (uint8_t)val);
                _dma_write(dma_c.ac + 1, (uint8_t)(val >> 8));

                if ((dma_c.mode & 0x20) != 0)
                {
                        if (dma_ps2.is_ps2 != 0)
                                dma_c.ac -= 2;
                        else
                                dma_c.ac = (dma_c.ac & 0xfe0000) | ((dma_c.ac - 2) & 0x1ffff);
                }
                else
                {
                        if (dma_ps2.is_ps2 != 0)
                                dma_c.ac += 2;
                        else
                                dma_c.ac = (dma_c.ac & 0xfe0000) | ((dma_c.ac + 2) & 0x1ffff);
                }
        }

        dma_stat_rq |= (uint8_t)(1 << channel);

        dma_c.cc--;
        if (dma_c.cc < 0)
        {
                if ((dma_c.mode & 0x10) != 0) /*Auto-init*/
                {
                        dma_c.cc = dma_c.cb;
                        dma_c.ac = dma_c.ab;
                }
                else
                        dma_m |= (uint8_t)(1 << channel);
                dma_stat |= (uint8_t)(1 << channel);
        }

        if ((dma_m & (1 << channel)) != 0)
                return DMA_OVER;

        return 0;
    }
}
