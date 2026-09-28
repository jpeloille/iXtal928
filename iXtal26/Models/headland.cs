// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/models/headland.c
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: transcribed — G3.1, le chipset de l'ami386. Le port 92 (AMA-932J seul) est
//         transcrit mais n'est pas posé sur l'ami386 ; son cpu_set_edx s'arrête en se
//         nommant.
//
// LE CHIPSET HEADLAND HT18 DE L'AMI 386SX. Comme le NEAT de l'ami286, deux ports
// d'index et de données (0x22/0x23) pour ses registres de configuration ; mais il fait
// bien plus : il REMPLACE les trois mappages de la RAM (ram_low, ram_mid, ram_high) par
// les siens, et TOUT accès à la RAM passe par get_headland_addr.
//
// CE QUE get_headland_addr FAIT, dans l'ordre :
//   - une page EMS active (CR0 bit 1, registre de page bit 0x200) : l'adresse est
//     recomposée depuis le registre de page, selon la configuration mémoire (CR1, CR4,
//     CR6) ;
//   - sinon, sans remappage (CR0 bit 2 nul) et au-delà du Mo : `addr -= 0x60000`. Les
//     384 Ko entre 640 Ko et 1 Mo, cachés sous la vidéo et la ROM, sont rendus au-dessus
//     du Mo — c'est le « remap » que les BIOS d'époque affichent.
//
// LES GESTIONNAIRES NE REMPLISSENT PAS LE CACHE readlookup2 : mem_read_headland* lisent
// ram[] directement, sans addreadlookup. Les accès de DONNÉES à la RAM de l'ami386
// prennent donc le chemin lent de readmembl — c'est PCem, et c'est mesurable en vitesse
// hôte. Les lectures d'instructions, elles, passent par `exec` (headland.c:469, 473,
// 481, 488, 497), que les mappages portent sur la RAM.
//
// LA CONFIGURATION MÉMOIRE EST CODÉE EN DUR, et le commentaire de PCem le dit (:21-22) :
// headland_mem_conf_cr0/cr1 donnent les bits de taille selon mem_size, par tranches de
// 512 Ko.

using iXtal26.Memory;
using static iXtal26.io;
using static iXtal26.Memory.mem;

namespace iXtal26.Models;

internal static class headland
{
    // pcem: headland.c:9-19
    private static int headland_index;
    private static readonly uint8_t[] headland_regs = new uint8_t[256];
    private static uint8_t headland_port_92 = 0xFC, headland_ems_mar = 0, headland_cri = 0;
    private static readonly uint8_t[] headland_regs_cr = new uint8_t[8];
    private static readonly uint16_t[] headland_ems_mr = new uint16_t[64];

    private static readonly mem_mapping_t headland_low_mapping = new();
    private static readonly mem_mapping_t[] headland_ems_mapping = Mappages(64);
    private static readonly mem_mapping_t headland_mid_mapping = new();
    private static readonly mem_mapping_t headland_high_mapping = new();
    private static readonly mem_mapping_t[] headland_4000_9FFF_mapping = Mappages(24);

    private static mem_mapping_t[] Mappages(int n)
    {
        var t = new mem_mapping_t[n];
        for (var i = 0; i < n; i++)
                t[i] = new mem_mapping_t();
        return t;
    }

    // pcem: headland.c:21-28 — « TODO - Headland chipset's memory address mapping emulation
    // isn't fully implemented yet, so memory configuration is hardcoded now. »
    private static readonly int[] headland_mem_conf_cr0 =
    [
        0x00, 0x00, 0x20, 0x40, 0x60, 0xA0, 0x40, 0xE0, 0xA0, 0xC0, 0xE0, 0xE0, 0xC0, 0xE0,
        0xE0, 0xE0, 0xE0, 0x20, 0x40, 0x40, 0xA0, 0xC0, 0xE0, 0xE0, 0xC0, 0xE0, 0xE0, 0xE0,
        0xE0, 0xE0, 0xE0, 0xE0, 0x20, 0x40, 0x60, 0x60, 0xC0, 0xE0, 0xE0, 0xE0, 0xE0,
    ];
    private static readonly int[] headland_mem_conf_cr1 =
    [
        0x00, 0x40, 0x00, 0x00, 0x00, 0x40, 0x40, 0x40, 0x00, 0x40, 0x40, 0x40, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x40, 0x40, 0x40, 0x00, 0x00, 0x00, 0x00, 0x40, 0x40, 0x40, 0x40,
        0x40, 0x40, 0x40, 0x40, 0x00, 0x00, 0x40, 0x40, 0x00, 0x00, 0x00, 0x00, 0x40,
    ];

    /// <summary>pcem: headland.c:30-67 — get_headland_addr.
    ///
    /// DEVIATION: le C reçoit `uint16_t *mr`, un pointeur DANS headland_ems_mr[] (ou NULL).
    ///   Ici l'indice de ce registre, -1 pour NULL : c'est ce que porte le `priv` des
    ///   mappages EMS (headland_init). `*mr` devient headland_ems_mr[mr].</summary>
    private static uint32_t get_headland_addr(uint32_t addr, int mr)
    {
        if (mr >= 0 && (headland_regs_cr[0] & 2) != 0 && (headland_ems_mr[mr] & 0x200) != 0)
        {
                uint32_t m = headland_ems_mr[mr];
                addr = (addr & 0x3fff) | ((m & 0x1F) << 14);

                if ((headland_regs_cr[1] & 0x40) != 0)
                {
                        if ((headland_regs_cr[4] & 0x80) != 0 && (headland_regs_cr[6] & 1) != 0)
                        {
                                if ((headland_regs_cr[0] & 0x80) != 0)
                                {
                                        addr |= (m & 0x60) << 14;
                                        if ((m & 0x100) != 0)
                                                addr += ((m & 0xC00) << 13) + (((m & 0x80) + 0x80) << 15);
                                        else
                                                addr += (m & 0x80) << 14;
                                }
                                else if ((m & 0x100) != 0)
                                        addr += ((m & 0xC00) << 13) + (((m & 0x80) + 0x20) << 15);
                                else
                                        addr += (m & 0x80) << 12;
                        }
                        else if ((headland_regs_cr[0] & 0x80) != 0)
                                addr |= (m & 0x100) != 0 ? ((m & 0x80) + 0x400) << 12 : (m & 0xE0) << 14;
                        else
                                addr |= (m & 0x100) != 0 ? ((m & 0xE0) + 0x40) << 14 : (m & 0x80) << 12;
                }
                else
                {
                        if ((headland_regs_cr[4] & 0x80) != 0 && (headland_regs_cr[6] & 1) != 0)
                        {
                                if ((headland_regs_cr[0] & 0x80) != 0)
                                {
                                        addr |= ((m & 0x60) << 14);
                                        if ((m & 0x180) != 0)
                                                addr += ((m & 0xC00) << 13) + (((m & 0x180) - 0x60) << 16);
                                }
                                else
                                        addr |= ((m & 0x60) << 14) | ((m & 0x180) << 16) | ((m & 0xC00) << 13);
                        }
                        else if ((headland_regs_cr[0] & 0x80) != 0)
                                addr |= (m & 0x1E0) << 14;
                        else
                                addr |= (m & 0x180) << 12;
                }
        }
        else if (mr < 0 && (headland_regs_cr[0] & 4) == 0 && mem_size >= 1024 && addr >= 0x100000)
                addr -= 0x60000;

        return addr;
    }

    /// <summary>DEVIATION: `ram + off` en C, un pointeur ; ici le porteur et son décalage,
    /// ou null quand le C passe NULL. Même idiome que mem_mapping_t.exec.</summary>
    private static void SetExecRam(mem_mapping_t m, uint32_t off, bool present)
    {
        if (present)
                mem_mapping_set_exec(m, ram, (int)off);
        else
                mem_mapping_set_exec(m, null, 0);
    }

    // pcem: headland.c:69-97
    private static void headland_set_global_EMS_state(int state)
    {
        int i;
        uint32_t base_addr, virt_addr;

        for (i = 0; i < 32; i++)
        {
                base_addr = (uint32_t)((i + 16) << 14);
                if (i >= 24)
                        base_addr += 0x20000;
                if ((state & 2) != 0 && (headland_ems_mr[((state & 1) << 5) | i] & 0x200) != 0)
                {
                        virt_addr = get_headland_addr(base_addr, ((state & 1) << 5) | i);
                        if (i < 24)
                                mem_mapping_disable(headland_4000_9FFF_mapping[i]);
                        mem_mapping_disable(headland_ems_mapping[(((state ^ 1) & 1) << 5) | i]);
                        mem_mapping_enable(headland_ems_mapping[((state & 1) << 5) | i]);
                        SetExecRam(headland_ems_mapping[((state & 1) << 5) | i], virt_addr,
                                   virt_addr < (uint32_t)(mem_size << 10));
                }
                else
                {
                        SetExecRam(headland_ems_mapping[((state & 1) << 5) | i], base_addr, true);
                        mem_mapping_disable(headland_ems_mapping[(((state ^ 1) & 1) << 5) | i]);
                        mem_mapping_disable(headland_ems_mapping[((state & 1) << 5) | i]);

                        if (i < 24)
                                mem_mapping_enable(headland_4000_9FFF_mapping[i]);
                }
        }
        flushmmucache();
    }

    // pcem: headland.c:99-130
    private static void headland_memmap_state_update()
    {
        int i;
        uint32_t addr;

        for (i = 0; i < 24; i++)
        {
                addr = get_headland_addr((uint32_t)(0x40000 + (i << 14)), -1);
                SetExecRam(headland_4000_9FFF_mapping[i], addr, addr < (uint32_t)(mem_size << 10));
                // omitted: pclog("headland_memmap_state_update …") commenté dans le C.
        }

        mem_set_mem_state(0xA0000, 0x40000, MEM_READ_EXTERNAL | MEM_WRITE_EXTERNAL);

        if (mem_size > 640)
        {
                if ((headland_regs_cr[0] & 4) == 0)
                {
                        mem_mapping_set_addr(headland_mid_mapping, 0x100000,
                                             mem_size > 1024 ? 0x60000u : (uint32_t)((mem_size - 640) << 10));
                        SetExecRam(headland_mid_mapping, 0xA0000, true);
                        if (mem_size > 1024)
                        {
                                mem_mapping_set_addr(headland_high_mapping, 0x160000, (uint32_t)((mem_size - 1024) << 10));
                                SetExecRam(headland_high_mapping, 0x100000, true);
                        }
                }
                else
                {
                        mem_mapping_set_addr(headland_mid_mapping, 0xA0000,
                                             mem_size > 1024 ? 0x60000u : (uint32_t)((mem_size - 640) << 10));
                        SetExecRam(headland_mid_mapping, 0xA0000, true);
                        if (mem_size > 1024)
                        {
                                mem_mapping_set_addr(headland_high_mapping, 0x100000, (uint32_t)((mem_size - 1024) << 10));
                                SetExecRam(headland_high_mapping, 0x100000, true);
                        }
                }
        }

        headland_set_global_EMS_state(headland_regs_cr[0] & 3);
    }

    // pcem: headland.c:132-256
    internal static void headland_write(uint16_t addr, uint8_t val, object? priv)
    {
        uint8_t old_val, index;
        uint32_t base_addr, virt_addr;

        switch (addr)
        {
        case 0x22:
                headland_index = val;
                break;

        case 0x23:
                old_val = headland_regs[headland_index];

                if (headland_index == 0xc1 && Cpu.x86.is486 == 0)
                        val = 0;
                headland_regs[headland_index] = val;
                if (headland_index == 0x82)
                {
                        if ((val & 0x10) != 0)
                                mem_set_mem_state(0xf0000, 0x10000, MEM_READ_INTERNAL | MEM_WRITE_DISABLED);
                        else
                                mem_set_mem_state(0xf0000, 0x10000, MEM_READ_EXTERNAL | MEM_WRITE_INTERNAL);
                }
                else if (headland_index == 0x87)
                {
                        if ((val & 1) != 0 && (old_val & 1) == 0)
                                Cpu._808x.softresetx86();
                }
                break;

        case 0x92:
                if (((mem_a20_alt ^ val) & 2) != 0)
                {
                        mem_a20_alt = val & 2;
                        mem_a20_recalc();
                }
                if ((~headland_port_92 & val & 1) != 0)
                {
                        Cpu._808x.softresetx86();
                        // omitted: cpu_set_edx() (cpu.c) n'est pas transcrit. Le port 92 n'est
                        //   posé que pour l'AMA-932J (headland.c:449-452) : branche morte sur
                        //   l'ami386, qui s'arrête en se nommant si elle cessait de l'être.
                        pc.fatal("not implemented: cpu_set_edx (port 92 de Headland, AMA-932J)\n");
                }
                headland_port_92 = (uint8_t)(val | 0xFC);
                break;

        case 0x1EC:
                headland_ems_mr[headland_ems_mar & 0x3F] = (uint16_t)(val | 0xFF00);
                index = (uint8_t)(headland_ems_mar & 0x1F);
                base_addr = (uint32_t)((index + 16) << 14);
                if (index >= 24)
                        base_addr += 0x20000;
                if ((headland_regs_cr[0] & 2) != 0 && (headland_regs_cr[0] & 1) == ((headland_ems_mar & 0x20) >> 5))
                {
                        virt_addr = get_headland_addr(base_addr, headland_ems_mar & 0x3F);
                        if (index < 24)
                                mem_mapping_disable(headland_4000_9FFF_mapping[index]);
                        SetExecRam(headland_ems_mapping[headland_ems_mar & 0x3F], virt_addr,
                                   virt_addr < (uint32_t)(mem_size << 10));
                        mem_mapping_enable(headland_ems_mapping[headland_ems_mar & 0x3F]);
                        // omitted: pclog("Map page %d …") — sortie pure.
                        flushmmucache();
                }
                if ((headland_ems_mar & 0x80) != 0)
                        headland_ems_mar++;
                break;

        case 0x1ED:
                headland_cri = val;
                break;

        case 0x1EE:
                headland_ems_mar = val;
                break;

        case 0x1EF:
                old_val = headland_regs_cr[headland_cri];
                switch (headland_cri)
                {
                case 0:
                        headland_regs_cr[0] =
                                (uint8_t)((val & 0x1F) | headland_mem_conf_cr0[(mem_size > 640 ? mem_size : mem_size - 128) >> 9]);
                        mem_set_mem_state(0xE0000, 0x10000,
                                          ((val & 8) != 0 ? MEM_READ_INTERNAL : MEM_READ_EXTERNAL) | MEM_WRITE_DISABLED);
                        mem_set_mem_state(0xF0000, 0x10000,
                                          ((val & 0x10) != 0 ? MEM_READ_INTERNAL : MEM_READ_EXTERNAL) | MEM_WRITE_DISABLED);
                        headland_memmap_state_update();
                        break;
                case 1:
                        headland_regs_cr[1] =
                                (uint8_t)((val & 0xBF) | headland_mem_conf_cr1[(mem_size > 640 ? mem_size : mem_size - 128) >> 9]);
                        headland_memmap_state_update();
                        break;
                case 2:
                case 3:
                case 5:
                        headland_regs_cr[headland_cri] = val;
                        headland_memmap_state_update();
                        break;
                case 4:
                        headland_regs_cr[4] = (uint8_t)((headland_regs_cr[4] & 0xF0) | (val & 0x0F));
                        if ((val & 1) != 0)
                        {
                                mem_mapping_disable(bios_mapping[0]);
                                mem_mapping_disable(bios_mapping[1]);
                                mem_mapping_disable(bios_mapping[2]);
                                mem_mapping_disable(bios_mapping[3]);
                        }
                        else
                        {
                                mem_mapping_enable(bios_mapping[0]);
                                mem_mapping_enable(bios_mapping[1]);
                                mem_mapping_enable(bios_mapping[2]);
                                mem_mapping_enable(bios_mapping[3]);
                        }
                        break;
                case 6:
                        if ((headland_regs_cr[4] & 0x80) != 0)
                        {
                                headland_regs_cr[headland_cri] = (uint8_t)((val & 0xFE) | (mem_size > 8192 ? 1 : 0));
                                headland_memmap_state_update();
                        }
                        break;
                default:
                        break;
                }
                break;

        default:
                break;
        }
    }

    // pcem: headland.c:258-298
    internal static void headland_writew(uint16_t addr, uint16_t val, object? priv)
    {
        uint8_t index;
        uint32_t base_addr, virt_addr;

        switch (addr)
        {
        case 0x1EC:
                headland_ems_mr[headland_ems_mar & 0x3F] = val;
                index = (uint8_t)(headland_ems_mar & 0x1F);
                base_addr = (uint32_t)((index + 16) << 14);
                if (index >= 24)
                        base_addr += 0x20000;
                if ((headland_regs_cr[0] & 2) != 0 && (headland_regs_cr[0] & 1) == ((headland_ems_mar & 0x20) >> 5))
                {
                        if ((val & 0x200) != 0)
                        {
                                virt_addr = get_headland_addr(base_addr, headland_ems_mar & 0x3F);
                                if (index < 24)
                                        mem_mapping_disable(headland_4000_9FFF_mapping[index]);
                                SetExecRam(headland_ems_mapping[headland_ems_mar & 0x3F], virt_addr,
                                           virt_addr < (uint32_t)(mem_size << 10));
                                mem_mapping_enable(headland_ems_mapping[headland_ems_mar & 0x3F]);
                        }
                        else
                        {
                                SetExecRam(headland_ems_mapping[headland_ems_mar & 0x3F], base_addr, true);
                                mem_mapping_disable(headland_ems_mapping[headland_ems_mar & 0x3F]);
                                if (index < 24)
                                        mem_mapping_enable(headland_4000_9FFF_mapping[index]);
                        }
                        flushmmucache();
                }
                if ((headland_ems_mar & 0x80) != 0)
                        headland_ems_mar++;
                break;

        default:
                break;
        }
    }

    // pcem: headland.c:300-361
    internal static uint8_t headland_read(uint16_t addr, object? priv)
    {
        uint8_t val;

        switch (addr)
        {
        case 0x22:
                val = (uint8_t)headland_index;
                break;

        case 0x23:
                if ((headland_index >= 0xc0 || headland_index == 0x20) && Cpu.cpu_c.cpu_iscyrix != 0)
                        val = 0xff; /*Don't conflict with Cyrix config registers*/
                else
                        val = headland_regs[headland_index];
                break;

        case 0x92:
                val = (uint8_t)(headland_port_92 | 0xFC);
                break;

        case 0x1EC:
                val = (uint8_t)headland_ems_mr[headland_ems_mar & 0x3F];
                if ((headland_ems_mar & 0x80) != 0)
                        headland_ems_mar++;
                break;

        case 0x1ED:
                val = headland_cri;
                break;

        case 0x1EE:
                val = headland_ems_mar;
                break;

        case 0x1EF:
                switch (headland_cri)
                {
                case 0:
                        val = (uint8_t)((headland_regs_cr[0] & 0x1F) |
                                        headland_mem_conf_cr0[(mem_size > 640 ? mem_size : mem_size - 128) >> 9]);
                        break;
                case 1:
                        val = (uint8_t)((headland_regs_cr[1] & 0xBF) |
                                        headland_mem_conf_cr1[(mem_size > 640 ? mem_size : mem_size - 128) >> 9]);
                        break;
                case 6:
                        if ((headland_regs_cr[4] & 0x80) != 0)
                                val = (uint8_t)((headland_regs_cr[6] & 0xFE) | (mem_size > 8192 ? 1 : 0));
                        else
                                val = 0;
                        break;
                default:
                        val = headland_regs_cr[headland_cri];
                        break;
                }
                break;

        default:
                val = 0xFF;
                break;
        }

        return val;
    }

    // pcem: headland.c:363-379
    internal static uint16_t headland_readw(uint16_t addr, object? priv)
    {
        uint16_t val;

        switch (addr)
        {
        case 0x1EC:
                val = (uint16_t)(headland_ems_mr[headland_ems_mar & 0x3F] | ((headland_regs_cr[4] & 0x80) != 0 ? 0xF000 : 0xFC00));
                if ((headland_ems_mar & 0x80) != 0)
                        headland_ems_mar++;
                break;

        default:
                val = 0xFFFF;
                break;
        }

        return val;
    }

    // Le `priv` des mappages : l'indice du registre de page (boxé), ou null.
    private static int Mr(object? priv) => priv is int i ? i : -1;

    // pcem: headland.c:381-435 — les six gestionnaires de la RAM.
    //
    // DEVIATION: en C, `*(uint16_t *)&ram[addr]` à `addr = (mem_size << 10) - 1` lit un à
    //   trois octets au-delà du malloc exact de mem_alloc (mem.c:1344) : comportement
    //   indéfini. Ici ram[] porte quatre octets de marge (mem.cs:1234), lus à zéro.
    private static uint8_t mem_read_headlandb(uint32_t addr, object? priv)
    {
        uint8_t val = 0xff;

        addr = get_headland_addr(addr, Mr(priv));
        if (addr < (uint32_t)(mem_size << 10))
                val = ram[addr];

        return val;
    }

    private static void mem_write_headlandb(uint32_t addr, uint8_t val, object? priv)
    {
        addr = get_headland_addr(addr, Mr(priv));
        if (addr < (uint32_t)(mem_size << 10))
                ram[addr] = val;
    }

    private static uint16_t mem_read_headlandw(uint32_t addr, object? priv)
    {
        uint16_t val = 0xffff;

        // omitted: pclog("mem_read_headlandw …") à cheval sur 16 Ko — sortie pure.
        addr = get_headland_addr(addr, Mr(priv));
        if (addr < (uint32_t)(mem_size << 10))
                val = (uint16_t)(ram[addr] | (ram[addr + 1] << 8));

        return val;
    }

    private static void mem_write_headlandw(uint32_t addr, uint16_t val, object? priv)
    {
        // omitted: pclog("mem_write_headlandw …") — sortie pure.
        addr = get_headland_addr(addr, Mr(priv));
        if (addr < (uint32_t)(mem_size << 10))
        {
                ram[addr] = (uint8_t)val;
                ram[addr + 1] = (uint8_t)(val >> 8);
        }
    }

    private static uint32_t mem_read_headlandl(uint32_t addr, object? priv)
    {
        uint32_t val = 0xffffffff;

        // omitted: pclog("mem_read_headlandl …") — sortie pure.
        addr = get_headland_addr(addr, Mr(priv));
        if (addr < (uint32_t)(mem_size << 10))
                val = (uint32_t)(ram[addr] | (ram[addr + 1] << 8) | (ram[addr + 2] << 16) | (ram[addr + 3] << 24));

        return val;
    }

    private static void mem_write_headlandl(uint32_t addr, uint32_t val, object? priv)
    {
        // omitted: pclog("mem_write_headland …") — sortie pure.
        addr = get_headland_addr(addr, Mr(priv));
        if (addr < (uint32_t)(mem_size << 10))
        {
                ram[addr] = (uint8_t)val;
                ram[addr + 1] = (uint8_t)(val >> 8);
                ram[addr + 2] = (uint8_t)(val >> 16);
                ram[addr + 3] = (uint8_t)(val >> 24);
        }
    }

    // pcem: headland.c:437-503 — headland_init.
    internal static void headland_init()
    {
        int i;

        for (i = 0; i < 8; i++)
                headland_regs_cr[i] = 0;
        headland_regs_cr[0] = 4;

        switch (pc.romset)
        {
        case pc.ROM_AMI386SX:
                // Remark - Previously distributed AMI386 BIOS doesn't seem to be for the Headland chipset.
                io_sethandler(0x0022, 0x0002, headland_read, null, null, headland_write, null, null, null);
                break;
        // omitted: case ROM_AMA932J (:449-452) — machine non portée ; c'est elle qui pose le
        //   port 92 et headland_regs_cr[4] = 0x20.
        default:
                headland_regs_cr[4] = 0;
                break;
        }

        io_sethandler(0x01EC, 0x0001, headland_read, headland_readw, null, headland_write, headland_writew, null, null);
        io_sethandler(0x01ED, 0x0003, headland_read, null, null, headland_write, null, null, null);

        for (i = 0; i < 64; i++)
                headland_ems_mr[i] = 0;

        mem_mapping_disable(ram_low_mapping);
        mem_mapping_disable(ram_mid_mapping);
        mem_mapping_disable(ram_high_mapping);

        mem_mapping_add(headland_low_mapping, 0, 0x40000, mem_read_headlandb, mem_read_headlandw, mem_read_headlandl,
                        mem_write_headlandb, mem_write_headlandw, mem_write_headlandl, ram, 0, MEM_MAPPING_INTERNAL, null);

        if (mem_size > 640)
        {
                mem_mapping_add(headland_mid_mapping, 0xA0000, 0x60000, mem_read_headlandb, mem_read_headlandw,
                                mem_read_headlandl, mem_write_headlandb, mem_write_headlandw, mem_write_headlandl, ram, 0xA0000,
                                MEM_MAPPING_INTERNAL, null);
                mem_mapping_enable(headland_mid_mapping);
        }

        if (mem_size > 1024)
        {
                mem_mapping_add(headland_high_mapping, 0x100000, (uint32_t)((mem_size - 1024) * 1024), mem_read_headlandb,
                                mem_read_headlandw, mem_read_headlandl, mem_write_headlandb, mem_write_headlandw,
                                mem_write_headlandl, ram, 0x100000, MEM_MAPPING_INTERNAL, null);
                mem_mapping_enable(headland_high_mapping);
        }

        for (i = 0; i < 24; i++)
        {
                var present = mem_size > 256 + (i << 4);
                mem_mapping_add(headland_4000_9FFF_mapping[i], (uint32_t)(0x40000 + (i << 14)), 0x4000, mem_read_headlandb,
                                mem_read_headlandw, mem_read_headlandl, mem_write_headlandb, mem_write_headlandw,
                                mem_write_headlandl, present ? ram : null, present ? 0x40000 + (i << 14) : 0,
                                MEM_MAPPING_INTERNAL, null);
                mem_mapping_enable(headland_4000_9FFF_mapping[i]);
        }

        for (i = 0; i < 64; i++)
        {
                headland_ems_mr[i] = 0;
                var @base = ((i & 31) + ((i & 31) >= 24 ? 24 : 16)) << 14;
                mem_mapping_add(headland_ems_mapping[i], (uint32_t)@base, 0x04000,
                                mem_read_headlandb, mem_read_headlandw, mem_read_headlandl, mem_write_headlandb,
                                mem_write_headlandw, mem_write_headlandl, ram, @base,
                                0, i);
                mem_mapping_disable(headland_ems_mapping[i]);
        }

        headland_memmap_state_update();
    }
}
