// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/models/ali1429.c
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: transcribed — G6.3, le chipset de l'AMI 486. Seul le pclog commenté est omis.
//
// LE CHIPSET ALi 1429 DE L'AMI 486, le plus petit des quatre chipsets du dépôt : un port
// d'index (0x22) et un port de données (0x23), 256 registres dont DEUX font quelque chose.
// 0x13 désigne, bit par bit, lesquels des huit blocs de 32 Ko de C0000h-FFFFFh sont ombrés ;
// 0x14, bits 1-0, dit comment : lecture et écriture vers la ROM (0), lecture en RAM (1),
// écriture en RAM (2), les deux (3). Les autres registres sont mémorisés et relus.
//
// LE RESET NE RECALCULE RIEN : ali1429_reset remplit les registres à 0xFF et s'arrête là —
// l'ombrage ne change qu'à la prochaine écriture de 0x13 ou 0x14. PCem l'appelle trois fois,
// sur TOUTE machine (pc.c:191, :317, :403) ; ses ports ne sont posés que par ali1429_init.

using static iXtal26.io;
using static iXtal26.Memory.mem;

namespace iXtal26.Models;

internal static class ali1429
{
    // pcem: ali1429.c:9-10
    private static int ali1429_index;
    private static readonly uint8_t[] ali1429_regs = new uint8_t[256];

    // pcem: ali1429.c:12-36
    private static void ali1429_recalc()
    {
        int c;

        for (c = 0; c < 8; c++)
        {
                uint32_t @base = (uint32_t)(0xc0000 + (c << 15));
                if ((ali1429_regs[0x13] & (1 << c)) != 0)
                {
                        switch (ali1429_regs[0x14] & 3)
                        {
                        case 0:
                                mem_set_mem_state(@base, 0x8000, MEM_READ_EXTERNAL | MEM_WRITE_EXTERNAL);
                                break;
                        case 1:
                                mem_set_mem_state(@base, 0x8000, MEM_READ_INTERNAL | MEM_WRITE_EXTERNAL);
                                break;
                        case 2:
                                mem_set_mem_state(@base, 0x8000, MEM_READ_EXTERNAL | MEM_WRITE_INTERNAL);
                                break;
                        case 3:
                                mem_set_mem_state(@base, 0x8000, MEM_READ_INTERNAL | MEM_WRITE_INTERNAL);
                                break;
                        }
                }
                else
                        mem_set_mem_state(@base, 0x8000, MEM_READ_EXTERNAL | MEM_WRITE_EXTERNAL);
        }

        flushmmucache();
    }

    // pcem: ali1429.c:38-53
    private static void ali1429_write(uint16_t port, uint8_t val, object? priv)
    {
        if ((port & 1) == 0)
                ali1429_index = val;
        else
        {
                ali1429_regs[ali1429_index] = val;
                // omitted: pclog commenté (:43).
                switch (ali1429_index)
                {
                case 0x13:
                        ali1429_recalc();
                        break;
                case 0x14:
                        ali1429_recalc();
                        break;
                }
        }
    }

    // pcem: ali1429.c:55-61
    private static uint8_t ali1429_read(uint16_t port, object? priv)
    {
        if ((port & 1) == 0)
                return (uint8_t)ali1429_index;
        if ((ali1429_index >= 0xc0 || ali1429_index == 0x20) && Cpu.cpu_c.cpu_iscyrix != 0)
                return 0xff; /*Don't conflict with Cyrix config registers*/
        return ali1429_regs[ali1429_index];
    }

    // pcem: ali1429.c:63
    internal static void ali1429_reset() { Array.Fill(ali1429_regs, (uint8_t)0xff); }

    private static readonly inb_fn rd = ali1429_read;
    private static readonly outb_fn wr = ali1429_write;

    // pcem: ali1429.c:65
    internal static void ali1429_init() { io_sethandler(0x0022, 0x0002, rd, null, null, wr, null, null, null); }
}
