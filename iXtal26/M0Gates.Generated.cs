// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — echafaudage temporaire M0, GENERE. Ne pas editer a la main.
//
// Porte G2 : approxime la forme de src/cpu/808x.c:1271-3905 — un switch(opcode)
// de 256 cas, des sous-switch sur (rmdat & 0x38), de la comptabilite de cycles,
// des acces memoire, et quatre `goto opcodestart` depuis les prefixes de segment
// vers un label du corps de boucle englobant (808x.c:1589/1664/1739/1798).
//
// Ce qu'on mesure : est-ce que RyuJIT avale un corps de methode de cette taille
// sans bailout, en Debug comme en Release.
//
// FICHIER JETABLE — supprime avec M0Gates.cs des que les portes sont vertes.

namespace iXtal26;

internal static partial class M0Gates
{
    private static long ExecScratch(uint8_t opcode, uint8_t rmdat)
    {
        long result = 0;
        int prefix_guard = 0;
        uint32_t ea = 0;
        uint8_t temp = 0, temp2 = 0;
        uint16_t tempw = 0, tempw2 = 0;
        int c = 0;

        while (cpu_state._cycles > 0)
        {
            cpu_mod = (rmdat >> 6) & 3;
            cpu_reg = (rmdat >> 3) & 7;
            cpu_rm = rmdat & 7;
        opcodestart:
            switch (opcode)
            {
                case 0x00:
                    cycles -= (cpu_mod == 3) ? 3 : 20;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x07);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x3039);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x0B);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x07;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x07);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x3039);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x0B);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x07;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x07) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x0B);
                    result += scratch_ram[(ea + 0x07) & 0xFFFFF];
                    break;
                case 0x01:
                    cycles -= (cpu_mod == 3) ? 4 : 21;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x14);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x3488);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x30);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x14;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x14);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x3488);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x30);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x14;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x14) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x30);
                    result += scratch_ram[(ea + 0x14) & 0xFFFFF];
                    break;
                case 0x02:
                    cycles -= (cpu_mod == 3) ? 5 : 22;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x21);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x38D7);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x55);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x21;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x21);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x38D7);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x55);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x21;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x21) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x55);
                    result += scratch_ram[(ea + 0x21) & 0xFFFFF];
                    break;
                case 0x03:
                    cycles -= (cpu_mod == 3) ? 6 : 23;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x2E);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x3D26);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x7A);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x2E;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x2E);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x3D26);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x7A);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x2E;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x2E) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x7A);
                    result += scratch_ram[(ea + 0x2E) & 0xFFFFF];
                    break;
                case 0x04:
                    cycles -= (cpu_mod == 3) ? 7 : 24;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x3B);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x4175);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x9F);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x3B;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x3B);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x4175);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x9F);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x3B;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x3B) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x9F);
                    result += scratch_ram[(ea + 0x3B) & 0xFFFFF];
                    break;
                case 0x05:
                    cycles -= (cpu_mod == 3) ? 8 : 25;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x48);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x45C4);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xC4);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x48;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x48);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x45C4);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xC4);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x48;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x48) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xC4);
                    result += scratch_ram[(ea + 0x48) & 0xFFFFF];
                    break;
                case 0x06:
                    cycles -= (cpu_mod == 3) ? 9 : 26;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x55);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x4A13);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xE9);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x55;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x55);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x4A13);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xE9);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x55;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x55) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xE9);
                    result += scratch_ram[(ea + 0x55) & 0xFFFFF];
                    break;
                case 0x07:
                    cycles -= (cpu_mod == 3) ? 10 : 27;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x62);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x4E62);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x0E);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x62;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x62);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x4E62);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x0E);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x62;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x62) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x0E);
                    result += scratch_ram[(ea + 0x62) & 0xFFFFF];
                    break;
                case 0x08:
                    cycles -= (cpu_mod == 3) ? 11 : 28;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x6F);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x52B1);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x33);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x6F;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x6F);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x52B1);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x33);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x6F;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x6F) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x33);
                    result += scratch_ram[(ea + 0x6F) & 0xFFFFF];
                    break;
                case 0x09:
                    cycles -= (cpu_mod == 3) ? 12 : 29;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x7C);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x5700);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x58);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x7C;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x7C);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x5700);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x58);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x7C;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x7C) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x58);
                    result += scratch_ram[(ea + 0x7C) & 0xFFFFF];
                    break;
                case 0x0A:
                    cycles -= (cpu_mod == 3) ? 13 : 30;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x89);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x5B4F);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x7D);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x89;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x89);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x5B4F);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x7D);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x89;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x89) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x7D);
                    result += scratch_ram[(ea + 0x89) & 0xFFFFF];
                    break;
                case 0x0B:
                    cycles -= (cpu_mod == 3) ? 14 : 31;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x96);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x5F9E);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xA2);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x96;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x96);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x5F9E);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xA2);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x96;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x96) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xA2);
                    result += scratch_ram[(ea + 0x96) & 0xFFFFF];
                    break;
                case 0x0C:
                    cycles -= (cpu_mod == 3) ? 15 : 32;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xA3);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x63ED);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xC7);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xA3;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xA3);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x63ED);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xC7);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xA3;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xA3) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xC7);
                    result += scratch_ram[(ea + 0xA3) & 0xFFFFF];
                    break;
                case 0x0D:
                    cycles -= (cpu_mod == 3) ? 16 : 33;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xB0);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x683C);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xEC);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xB0;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xB0);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x683C);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xEC);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xB0;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xB0) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xEC);
                    result += scratch_ram[(ea + 0xB0) & 0xFFFFF];
                    break;
                case 0x0E:
                    cycles -= (cpu_mod == 3) ? 17 : 34;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xBD);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x6C8B);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x11);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xBD;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xBD);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x6C8B);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x11);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xBD;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xBD) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x11);
                    result += scratch_ram[(ea + 0xBD) & 0xFFFFF];
                    break;
                case 0x0F:
                    cycles -= (cpu_mod == 3) ? 18 : 35;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xCA);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x70DA);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x36);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xCA;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xCA);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x70DA);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x36);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xCA;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xCA) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x36);
                    result += scratch_ram[(ea + 0xCA) & 0xFFFFF];
                    break;
                case 0x10:
                    cycles -= (cpu_mod == 3) ? 19 : 36;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xD7);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x7529);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x5B);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xD7;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xD7);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x7529);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x5B);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xD7;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xD7) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x5B);
                    result += scratch_ram[(ea + 0xD7) & 0xFFFFF];
                    break;
                case 0x11:
                    cycles -= (cpu_mod == 3) ? 20 : 37;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xE4);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x7978);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x80);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xE4;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xE4);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x7978);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x80);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xE4;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xE4) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x80);
                    result += scratch_ram[(ea + 0xE4) & 0xFFFFF];
                    break;
                case 0x12:
                    cycles -= (cpu_mod == 3) ? 21 : 38;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xF1);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x7DC7);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xA5);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xF1;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xF1);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x7DC7);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xA5);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xF1;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xF1) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xA5);
                    result += scratch_ram[(ea + 0xF1) & 0xFFFFF];
                    break;
                case 0x13:
                    cycles -= (cpu_mod == 3) ? 22 : 39;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xFE);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x8216);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xCA);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xFE;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xFE);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x8216);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xCA);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xFE;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xFE) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xCA);
                    result += scratch_ram[(ea + 0xFE) & 0xFFFFF];
                    break;
                case 0x14:
                    cycles -= (cpu_mod == 3) ? 23 : 40;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x0B);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x8665);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xEF);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x0B;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x0B);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x8665);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xEF);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x0B;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x0B) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xEF);
                    result += scratch_ram[(ea + 0x0B) & 0xFFFFF];
                    break;
                case 0x15:
                    cycles -= (cpu_mod == 3) ? 3 : 20;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x18);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x8AB4);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x14);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x18;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x18);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x8AB4);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x14);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x18;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x18) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x14);
                    result += scratch_ram[(ea + 0x18) & 0xFFFFF];
                    break;
                case 0x16:
                    cycles -= (cpu_mod == 3) ? 4 : 21;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x25);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x8F03);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x39);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x25;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x25);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x8F03);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x39);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x25;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x25) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x39);
                    result += scratch_ram[(ea + 0x25) & 0xFFFFF];
                    break;
                case 0x17:
                    cycles -= (cpu_mod == 3) ? 5 : 22;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x32);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x9352);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x5E);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x32;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x32);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x9352);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x5E);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x32;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x32) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x5E);
                    result += scratch_ram[(ea + 0x32) & 0xFFFFF];
                    break;
                case 0x18:
                    cycles -= (cpu_mod == 3) ? 6 : 23;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x3F);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x97A1);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x83);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x3F;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x3F);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x97A1);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x83);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x3F;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x3F) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x83);
                    result += scratch_ram[(ea + 0x3F) & 0xFFFFF];
                    break;
                case 0x19:
                    cycles -= (cpu_mod == 3) ? 7 : 24;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x4C);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x9BF0);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xA8);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x4C;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x4C);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x9BF0);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xA8);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x4C;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x4C) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xA8);
                    result += scratch_ram[(ea + 0x4C) & 0xFFFFF];
                    break;
                case 0x1A:
                    cycles -= (cpu_mod == 3) ? 8 : 25;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x59);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xA03F);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xCD);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x59;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x59);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xA03F);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xCD);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x59;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x59) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xCD);
                    result += scratch_ram[(ea + 0x59) & 0xFFFFF];
                    break;
                case 0x1B:
                    cycles -= (cpu_mod == 3) ? 9 : 26;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x66);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xA48E);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xF2);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x66;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x66);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xA48E);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xF2);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x66;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x66) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xF2);
                    result += scratch_ram[(ea + 0x66) & 0xFFFFF];
                    break;
                case 0x1C:
                    cycles -= (cpu_mod == 3) ? 10 : 27;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x73);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xA8DD);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x17);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x73;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x73);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xA8DD);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x17);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x73;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x73) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x17);
                    result += scratch_ram[(ea + 0x73) & 0xFFFFF];
                    break;
                case 0x1D:
                    cycles -= (cpu_mod == 3) ? 11 : 28;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x80);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xAD2C);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x3C);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x80;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x80);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xAD2C);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x3C);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x80;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x80) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x3C);
                    result += scratch_ram[(ea + 0x80) & 0xFFFFF];
                    break;
                case 0x1E:
                    cycles -= (cpu_mod == 3) ? 12 : 29;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x8D);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xB17B);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x61);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x8D;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x8D);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xB17B);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x61);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x8D;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x8D) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x61);
                    result += scratch_ram[(ea + 0x8D) & 0xFFFFF];
                    break;
                case 0x1F:
                    cycles -= (cpu_mod == 3) ? 13 : 30;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x9A);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xB5CA);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x86);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x9A;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x9A);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xB5CA);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x86);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x9A;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x9A) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x86);
                    result += scratch_ram[(ea + 0x9A) & 0xFFFFF];
                    break;
                case 0x20:
                    cycles -= (cpu_mod == 3) ? 14 : 31;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xA7);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xBA19);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xAB);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xA7;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xA7);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xBA19);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xAB);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xA7;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xA7) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xAB);
                    result += scratch_ram[(ea + 0xA7) & 0xFFFFF];
                    break;
                case 0x21:
                    cycles -= (cpu_mod == 3) ? 15 : 32;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xB4);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xBE68);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xD0);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xB4;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xB4);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xBE68);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xD0);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xB4;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xB4) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xD0);
                    result += scratch_ram[(ea + 0xB4) & 0xFFFFF];
                    break;
                case 0x22:
                    cycles -= (cpu_mod == 3) ? 16 : 33;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xC1);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xC2B7);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xF5);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xC1;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xC1);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xC2B7);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xF5);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xC1;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xC1) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xF5);
                    result += scratch_ram[(ea + 0xC1) & 0xFFFFF];
                    break;
                case 0x23:
                    cycles -= (cpu_mod == 3) ? 17 : 34;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xCE);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xC706);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x1A);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xCE;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xCE);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xC706);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x1A);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xCE;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xCE) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x1A);
                    result += scratch_ram[(ea + 0xCE) & 0xFFFFF];
                    break;
                case 0x24:
                    cycles -= (cpu_mod == 3) ? 18 : 35;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xDB);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xCB55);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x3F);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xDB;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xDB);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xCB55);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x3F);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xDB;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xDB) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x3F);
                    result += scratch_ram[(ea + 0xDB) & 0xFFFFF];
                    break;
                case 0x25:
                    cycles -= (cpu_mod == 3) ? 19 : 36;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xE8);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xCFA4);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x64);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xE8;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xE8);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xCFA4);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x64);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xE8;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xE8) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x64);
                    result += scratch_ram[(ea + 0xE8) & 0xFFFFF];
                    break;
                case 0x26: /*ES:*/
                    ssegs = 1;
                    ea_seg_base = 0x0000;
                    cycles -= 4;
                    if (prefix_guard++ < 1)
                    {
                        goto_taken++;
                        opcode = 0x90;
                        goto opcodestart;
                    }
                    result += ssegs + ea_seg_base;
                    break;
                case 0x27:
                    cycles -= (cpu_mod == 3) ? 21 : 38;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x02);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xD842);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xAE);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x02;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x02);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xD842);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xAE);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x02;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x02) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xAE);
                    result += scratch_ram[(ea + 0x02) & 0xFFFFF];
                    break;
                case 0x28:
                    cycles -= (cpu_mod == 3) ? 22 : 39;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x0F);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xDC91);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xD3);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x0F;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x0F);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xDC91);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xD3);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x0F;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x0F) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xD3);
                    result += scratch_ram[(ea + 0x0F) & 0xFFFFF];
                    break;
                case 0x29:
                    cycles -= (cpu_mod == 3) ? 23 : 40;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x1C);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xE0E0);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xF8);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x1C;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x1C);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xE0E0);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xF8);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x1C;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x1C) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xF8);
                    result += scratch_ram[(ea + 0x1C) & 0xFFFFF];
                    break;
                case 0x2A:
                    cycles -= (cpu_mod == 3) ? 3 : 20;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x29);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xE52F);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x1D);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x29;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x29);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xE52F);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x1D);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x29;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x29) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x1D);
                    result += scratch_ram[(ea + 0x29) & 0xFFFFF];
                    break;
                case 0x2B:
                    cycles -= (cpu_mod == 3) ? 4 : 21;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x36);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xE97E);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x42);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x36;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x36);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xE97E);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x42);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x36;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x36) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x42);
                    result += scratch_ram[(ea + 0x36) & 0xFFFFF];
                    break;
                case 0x2C:
                    cycles -= (cpu_mod == 3) ? 5 : 22;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x43);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xEDCD);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x67);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x43;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x43);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xEDCD);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x67);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x43;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x43) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x67);
                    result += scratch_ram[(ea + 0x43) & 0xFFFFF];
                    break;
                case 0x2D:
                    cycles -= (cpu_mod == 3) ? 6 : 23;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x50);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xF21C);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x8C);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x50;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x50);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xF21C);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x8C);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x50;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x50) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x8C);
                    result += scratch_ram[(ea + 0x50) & 0xFFFFF];
                    break;
                case 0x2E: /*CS:*/
                    ssegs = 1;
                    ea_seg_base = 0x0080;
                    cycles -= 4;
                    if (prefix_guard++ < 1)
                    {
                        goto_taken++;
                        opcode = 0x90;
                        goto opcodestart;
                    }
                    result += ssegs + ea_seg_base;
                    break;
                case 0x2F:
                    cycles -= (cpu_mod == 3) ? 8 : 25;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x6A);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xFABA);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xD6);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x6A;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x6A);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xFABA);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xD6);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x6A;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x6A) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xD6);
                    result += scratch_ram[(ea + 0x6A) & 0xFFFFF];
                    break;
                case 0x30:
                    cycles -= (cpu_mod == 3) ? 9 : 26;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x77);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xFF09);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xFB);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x77;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x77);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xFF09);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xFB);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x77;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x77) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xFB);
                    result += scratch_ram[(ea + 0x77) & 0xFFFFF];
                    break;
                case 0x31:
                    cycles -= (cpu_mod == 3) ? 10 : 27;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x84);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x0358);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x20);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x84;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x84);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x0358);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x20);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x84;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x84) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x20);
                    result += scratch_ram[(ea + 0x84) & 0xFFFFF];
                    break;
                case 0x32:
                    cycles -= (cpu_mod == 3) ? 11 : 28;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x91);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x07A7);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x45);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x91;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x91);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x07A7);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x45);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x91;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x91) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x45);
                    result += scratch_ram[(ea + 0x91) & 0xFFFFF];
                    break;
                case 0x33:
                    cycles -= (cpu_mod == 3) ? 12 : 29;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x9E);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x0BF6);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x6A);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x9E;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x9E);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x0BF6);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x6A);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x9E;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x9E) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x6A);
                    result += scratch_ram[(ea + 0x9E) & 0xFFFFF];
                    break;
                case 0x34:
                    cycles -= (cpu_mod == 3) ? 13 : 30;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xAB);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x1045);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x8F);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xAB;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xAB);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x1045);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x8F);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xAB;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xAB) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x8F);
                    result += scratch_ram[(ea + 0xAB) & 0xFFFFF];
                    break;
                case 0x35:
                    cycles -= (cpu_mod == 3) ? 14 : 31;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xB8);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x1494);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xB4);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xB8;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xB8);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x1494);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xB4);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xB8;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xB8) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xB4);
                    result += scratch_ram[(ea + 0xB8) & 0xFFFFF];
                    break;
                case 0x36: /*SS:*/
                    ssegs = 1;
                    ea_seg_base = 0x0100;
                    cycles -= 4;
                    if (prefix_guard++ < 1)
                    {
                        goto_taken++;
                        opcode = 0x90;
                        goto opcodestart;
                    }
                    result += ssegs + ea_seg_base;
                    break;
                case 0x37:
                    cycles -= (cpu_mod == 3) ? 16 : 33;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xD2);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x1D32);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xFE);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xD2;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xD2);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x1D32);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xFE);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xD2;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xD2) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xFE);
                    result += scratch_ram[(ea + 0xD2) & 0xFFFFF];
                    break;
                case 0x38:
                    cycles -= (cpu_mod == 3) ? 17 : 34;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xDF);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x2181);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x23);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xDF;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xDF);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x2181);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x23);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xDF;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xDF) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x23);
                    result += scratch_ram[(ea + 0xDF) & 0xFFFFF];
                    break;
                case 0x39:
                    cycles -= (cpu_mod == 3) ? 18 : 35;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xEC);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x25D0);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x48);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xEC;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xEC);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x25D0);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x48);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xEC;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xEC) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x48);
                    result += scratch_ram[(ea + 0xEC) & 0xFFFFF];
                    break;
                case 0x3A:
                    cycles -= (cpu_mod == 3) ? 19 : 36;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xF9);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x2A1F);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x6D);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xF9;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xF9);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x2A1F);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x6D);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xF9;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xF9) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x6D);
                    result += scratch_ram[(ea + 0xF9) & 0xFFFFF];
                    break;
                case 0x3B:
                    cycles -= (cpu_mod == 3) ? 20 : 37;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x06);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x2E6E);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x92);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x06;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x06);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x2E6E);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x92);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x06;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x06) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x92);
                    result += scratch_ram[(ea + 0x06) & 0xFFFFF];
                    break;
                case 0x3C:
                    cycles -= (cpu_mod == 3) ? 21 : 38;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x13);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x32BD);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xB7);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x13;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x13);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x32BD);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xB7);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x13;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x13) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xB7);
                    result += scratch_ram[(ea + 0x13) & 0xFFFFF];
                    break;
                case 0x3D:
                    cycles -= (cpu_mod == 3) ? 22 : 39;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x20);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x370C);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xDC);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x20;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x20);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x370C);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xDC);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x20;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x20) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xDC);
                    result += scratch_ram[(ea + 0x20) & 0xFFFFF];
                    break;
                case 0x3E: /*DS:*/
                    ssegs = 1;
                    ea_seg_base = 0x0180;
                    cycles -= 4;
                    if (prefix_guard++ < 1)
                    {
                        goto_taken++;
                        opcode = 0x90;
                        goto opcodestart;
                    }
                    result += ssegs + ea_seg_base;
                    break;
                case 0x3F:
                    cycles -= (cpu_mod == 3) ? 3 : 20;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x3A);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x3FAA);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x26);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x3A;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x3A);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x3FAA);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x26);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x3A;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x3A) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x26);
                    result += scratch_ram[(ea + 0x3A) & 0xFFFFF];
                    break;
                case 0x40:
                    cycles -= (cpu_mod == 3) ? 4 : 21;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x47);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x43F9);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x4B);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x47;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x47);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x43F9);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x4B);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x47;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x47) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x4B);
                    result += scratch_ram[(ea + 0x47) & 0xFFFFF];
                    break;
                case 0x41:
                    cycles -= (cpu_mod == 3) ? 5 : 22;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x54);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x4848);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x70);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x54;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x54);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x4848);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x70);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x54;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x54) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x70);
                    result += scratch_ram[(ea + 0x54) & 0xFFFFF];
                    break;
                case 0x42:
                    cycles -= (cpu_mod == 3) ? 6 : 23;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x61);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x4C97);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x95);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x61;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x61);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x4C97);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x95);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x61;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x61) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x95);
                    result += scratch_ram[(ea + 0x61) & 0xFFFFF];
                    break;
                case 0x43:
                    cycles -= (cpu_mod == 3) ? 7 : 24;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x6E);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x50E6);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xBA);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x6E;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x6E);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x50E6);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xBA);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x6E;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x6E) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xBA);
                    result += scratch_ram[(ea + 0x6E) & 0xFFFFF];
                    break;
                case 0x44:
                    cycles -= (cpu_mod == 3) ? 8 : 25;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x7B);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x5535);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xDF);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x7B;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x7B);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x5535);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xDF);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x7B;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x7B) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xDF);
                    result += scratch_ram[(ea + 0x7B) & 0xFFFFF];
                    break;
                case 0x45:
                    cycles -= (cpu_mod == 3) ? 9 : 26;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x88);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x5984);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x04);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x88;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x88);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x5984);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x04);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x88;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x88) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x04);
                    result += scratch_ram[(ea + 0x88) & 0xFFFFF];
                    break;
                case 0x46:
                    cycles -= (cpu_mod == 3) ? 10 : 27;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x95);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x5DD3);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x29);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x95;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x95);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x5DD3);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x29);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x95;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x95) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x29);
                    result += scratch_ram[(ea + 0x95) & 0xFFFFF];
                    break;
                case 0x47:
                    cycles -= (cpu_mod == 3) ? 11 : 28;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xA2);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x6222);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x4E);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xA2;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xA2);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x6222);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x4E);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xA2;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xA2) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x4E);
                    result += scratch_ram[(ea + 0xA2) & 0xFFFFF];
                    break;
                case 0x48:
                    cycles -= (cpu_mod == 3) ? 12 : 29;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xAF);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x6671);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x73);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xAF;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xAF);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x6671);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x73);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xAF;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xAF) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x73);
                    result += scratch_ram[(ea + 0xAF) & 0xFFFFF];
                    break;
                case 0x49:
                    cycles -= (cpu_mod == 3) ? 13 : 30;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xBC);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x6AC0);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x98);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xBC;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xBC);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x6AC0);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x98);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xBC;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xBC) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x98);
                    result += scratch_ram[(ea + 0xBC) & 0xFFFFF];
                    break;
                case 0x4A:
                    cycles -= (cpu_mod == 3) ? 14 : 31;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xC9);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x6F0F);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xBD);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xC9;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xC9);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x6F0F);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xBD);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xC9;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xC9) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xBD);
                    result += scratch_ram[(ea + 0xC9) & 0xFFFFF];
                    break;
                case 0x4B:
                    cycles -= (cpu_mod == 3) ? 15 : 32;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xD6);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x735E);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xE2);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xD6;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xD6);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x735E);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xE2);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xD6;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xD6) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xE2);
                    result += scratch_ram[(ea + 0xD6) & 0xFFFFF];
                    break;
                case 0x4C:
                    cycles -= (cpu_mod == 3) ? 16 : 33;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xE3);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x77AD);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x07);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xE3;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xE3);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x77AD);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x07);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xE3;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xE3) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x07);
                    result += scratch_ram[(ea + 0xE3) & 0xFFFFF];
                    break;
                case 0x4D:
                    cycles -= (cpu_mod == 3) ? 17 : 34;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xF0);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x7BFC);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x2C);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xF0;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xF0);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x7BFC);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x2C);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xF0;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xF0) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x2C);
                    result += scratch_ram[(ea + 0xF0) & 0xFFFFF];
                    break;
                case 0x4E:
                    cycles -= (cpu_mod == 3) ? 18 : 35;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xFD);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x804B);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x51);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xFD;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xFD);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x804B);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x51);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xFD;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xFD) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x51);
                    result += scratch_ram[(ea + 0xFD) & 0xFFFFF];
                    break;
                case 0x4F:
                    cycles -= (cpu_mod == 3) ? 19 : 36;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x0A);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x849A);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x76);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x0A;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x0A);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x849A);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x76);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x0A;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x0A) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x76);
                    result += scratch_ram[(ea + 0x0A) & 0xFFFFF];
                    break;
                case 0x50:
                    cycles -= (cpu_mod == 3) ? 20 : 37;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x17);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x88E9);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x9B);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x17;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x17);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x88E9);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x9B);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x17;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x17) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x9B);
                    result += scratch_ram[(ea + 0x17) & 0xFFFFF];
                    break;
                case 0x51:
                    cycles -= (cpu_mod == 3) ? 21 : 38;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x24);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x8D38);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xC0);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x24;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x24);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x8D38);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xC0);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x24;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x24) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xC0);
                    result += scratch_ram[(ea + 0x24) & 0xFFFFF];
                    break;
                case 0x52:
                    cycles -= (cpu_mod == 3) ? 22 : 39;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x31);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x9187);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xE5);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x31;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x31);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x9187);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xE5);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x31;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x31) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xE5);
                    result += scratch_ram[(ea + 0x31) & 0xFFFFF];
                    break;
                case 0x53:
                    cycles -= (cpu_mod == 3) ? 23 : 40;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x3E);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x95D6);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x0A);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x3E;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x3E);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x95D6);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x0A);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x3E;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x3E) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x0A);
                    result += scratch_ram[(ea + 0x3E) & 0xFFFFF];
                    break;
                case 0x54:
                    cycles -= (cpu_mod == 3) ? 3 : 20;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x4B);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x9A25);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x2F);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x4B;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x4B);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x9A25);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x2F);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x4B;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x4B) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x2F);
                    result += scratch_ram[(ea + 0x4B) & 0xFFFFF];
                    break;
                case 0x55:
                    cycles -= (cpu_mod == 3) ? 4 : 21;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x58);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x9E74);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x54);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x58;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x58);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x9E74);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x54);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x58;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x58) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x54);
                    result += scratch_ram[(ea + 0x58) & 0xFFFFF];
                    break;
                case 0x56:
                    cycles -= (cpu_mod == 3) ? 5 : 22;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x65);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xA2C3);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x79);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x65;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x65);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xA2C3);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x79);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x65;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x65) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x79);
                    result += scratch_ram[(ea + 0x65) & 0xFFFFF];
                    break;
                case 0x57:
                    cycles -= (cpu_mod == 3) ? 6 : 23;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x72);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xA712);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x9E);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x72;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x72);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xA712);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x9E);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x72;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x72) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x9E);
                    result += scratch_ram[(ea + 0x72) & 0xFFFFF];
                    break;
                case 0x58:
                    cycles -= (cpu_mod == 3) ? 7 : 24;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x7F);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xAB61);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xC3);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x7F;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x7F);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xAB61);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xC3);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x7F;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x7F) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xC3);
                    result += scratch_ram[(ea + 0x7F) & 0xFFFFF];
                    break;
                case 0x59:
                    cycles -= (cpu_mod == 3) ? 8 : 25;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x8C);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xAFB0);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xE8);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x8C;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x8C);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xAFB0);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xE8);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x8C;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x8C) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xE8);
                    result += scratch_ram[(ea + 0x8C) & 0xFFFFF];
                    break;
                case 0x5A:
                    cycles -= (cpu_mod == 3) ? 9 : 26;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x99);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xB3FF);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x0D);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x99;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x99);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xB3FF);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x0D);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x99;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x99) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x0D);
                    result += scratch_ram[(ea + 0x99) & 0xFFFFF];
                    break;
                case 0x5B:
                    cycles -= (cpu_mod == 3) ? 10 : 27;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xA6);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xB84E);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x32);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xA6;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xA6);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xB84E);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x32);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xA6;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xA6) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x32);
                    result += scratch_ram[(ea + 0xA6) & 0xFFFFF];
                    break;
                case 0x5C:
                    cycles -= (cpu_mod == 3) ? 11 : 28;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xB3);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xBC9D);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x57);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xB3;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xB3);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xBC9D);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x57);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xB3;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xB3) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x57);
                    result += scratch_ram[(ea + 0xB3) & 0xFFFFF];
                    break;
                case 0x5D:
                    cycles -= (cpu_mod == 3) ? 12 : 29;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xC0);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xC0EC);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x7C);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xC0;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xC0);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xC0EC);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x7C);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xC0;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xC0) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x7C);
                    result += scratch_ram[(ea + 0xC0) & 0xFFFFF];
                    break;
                case 0x5E:
                    cycles -= (cpu_mod == 3) ? 13 : 30;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xCD);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xC53B);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xA1);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xCD;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xCD);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xC53B);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xA1);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xCD;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xCD) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xA1);
                    result += scratch_ram[(ea + 0xCD) & 0xFFFFF];
                    break;
                case 0x5F:
                    cycles -= (cpu_mod == 3) ? 14 : 31;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xDA);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xC98A);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xC6);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xDA;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xDA);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xC98A);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xC6);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xDA;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xDA) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xC6);
                    result += scratch_ram[(ea + 0xDA) & 0xFFFFF];
                    break;
                case 0x60:
                    cycles -= (cpu_mod == 3) ? 15 : 32;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xE7);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xCDD9);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xEB);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xE7;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xE7);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xCDD9);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xEB);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xE7;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xE7) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xEB);
                    result += scratch_ram[(ea + 0xE7) & 0xFFFFF];
                    break;
                case 0x61:
                    cycles -= (cpu_mod == 3) ? 16 : 33;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xF4);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xD228);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x10);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xF4;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xF4);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xD228);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x10);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xF4;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xF4) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x10);
                    result += scratch_ram[(ea + 0xF4) & 0xFFFFF];
                    break;
                case 0x62:
                    cycles -= (cpu_mod == 3) ? 17 : 34;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x01);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xD677);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x35);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x01;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x01);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xD677);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x35);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x01;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x01) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x35);
                    result += scratch_ram[(ea + 0x01) & 0xFFFFF];
                    break;
                case 0x63:
                    cycles -= (cpu_mod == 3) ? 18 : 35;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x0E);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xDAC6);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x5A);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x0E;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x0E);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xDAC6);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x5A);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x0E;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x0E) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x5A);
                    result += scratch_ram[(ea + 0x0E) & 0xFFFFF];
                    break;
                case 0x64:
                    cycles -= (cpu_mod == 3) ? 19 : 36;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x1B);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xDF15);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x7F);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x1B;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x1B);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xDF15);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x7F);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x1B;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x1B) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x7F);
                    result += scratch_ram[(ea + 0x1B) & 0xFFFFF];
                    break;
                case 0x65:
                    cycles -= (cpu_mod == 3) ? 20 : 37;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x28);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xE364);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xA4);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x28;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x28);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xE364);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xA4);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x28;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x28) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xA4);
                    result += scratch_ram[(ea + 0x28) & 0xFFFFF];
                    break;
                case 0x66:
                    cycles -= (cpu_mod == 3) ? 21 : 38;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x35);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xE7B3);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xC9);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x35;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x35);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xE7B3);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xC9);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x35;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x35) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xC9);
                    result += scratch_ram[(ea + 0x35) & 0xFFFFF];
                    break;
                case 0x67:
                    cycles -= (cpu_mod == 3) ? 22 : 39;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x42);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xEC02);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xEE);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x42;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x42);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xEC02);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xEE);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x42;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x42) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xEE);
                    result += scratch_ram[(ea + 0x42) & 0xFFFFF];
                    break;
                case 0x68:
                    cycles -= (cpu_mod == 3) ? 23 : 40;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x4F);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xF051);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x13);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x4F;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x4F);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xF051);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x13);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x4F;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x4F) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x13);
                    result += scratch_ram[(ea + 0x4F) & 0xFFFFF];
                    break;
                case 0x69:
                    cycles -= (cpu_mod == 3) ? 3 : 20;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x5C);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xF4A0);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x38);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x5C;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x5C);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xF4A0);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x38);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x5C;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x5C) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x38);
                    result += scratch_ram[(ea + 0x5C) & 0xFFFFF];
                    break;
                case 0x6A:
                    cycles -= (cpu_mod == 3) ? 4 : 21;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x69);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xF8EF);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x5D);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x69;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x69);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xF8EF);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x5D);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x69;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x69) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x5D);
                    result += scratch_ram[(ea + 0x69) & 0xFFFFF];
                    break;
                case 0x6B:
                    cycles -= (cpu_mod == 3) ? 5 : 22;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x76);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xFD3E);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x82);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x76;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x76);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xFD3E);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x82);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x76;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x76) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x82);
                    result += scratch_ram[(ea + 0x76) & 0xFFFFF];
                    break;
                case 0x6C:
                    cycles -= (cpu_mod == 3) ? 6 : 23;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x83);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x018D);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xA7);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x83;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x83);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x018D);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xA7);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x83;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x83) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xA7);
                    result += scratch_ram[(ea + 0x83) & 0xFFFFF];
                    break;
                case 0x6D:
                    cycles -= (cpu_mod == 3) ? 7 : 24;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x90);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x05DC);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xCC);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x90;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x90);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x05DC);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xCC);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x90;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x90) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xCC);
                    result += scratch_ram[(ea + 0x90) & 0xFFFFF];
                    break;
                case 0x6E:
                    cycles -= (cpu_mod == 3) ? 8 : 25;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x9D);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x0A2B);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xF1);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x9D;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x9D);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x0A2B);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xF1);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x9D;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x9D) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xF1);
                    result += scratch_ram[(ea + 0x9D) & 0xFFFFF];
                    break;
                case 0x6F:
                    cycles -= (cpu_mod == 3) ? 9 : 26;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xAA);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x0E7A);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x16);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xAA;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xAA);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x0E7A);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x16);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xAA;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xAA) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x16);
                    result += scratch_ram[(ea + 0xAA) & 0xFFFFF];
                    break;
                case 0x70:
                    cycles -= (cpu_mod == 3) ? 10 : 27;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xB7);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x12C9);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x3B);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xB7;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xB7);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x12C9);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x3B);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xB7;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xB7) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x3B);
                    result += scratch_ram[(ea + 0xB7) & 0xFFFFF];
                    break;
                case 0x71:
                    cycles -= (cpu_mod == 3) ? 11 : 28;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xC4);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x1718);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x60);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xC4;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xC4);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x1718);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x60);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xC4;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xC4) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x60);
                    result += scratch_ram[(ea + 0xC4) & 0xFFFFF];
                    break;
                case 0x72:
                    cycles -= (cpu_mod == 3) ? 12 : 29;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xD1);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x1B67);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x85);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xD1;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xD1);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x1B67);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x85);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xD1;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xD1) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x85);
                    result += scratch_ram[(ea + 0xD1) & 0xFFFFF];
                    break;
                case 0x73:
                    cycles -= (cpu_mod == 3) ? 13 : 30;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xDE);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x1FB6);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xAA);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xDE;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xDE);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x1FB6);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xAA);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xDE;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xDE) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xAA);
                    result += scratch_ram[(ea + 0xDE) & 0xFFFFF];
                    break;
                case 0x74:
                    cycles -= (cpu_mod == 3) ? 14 : 31;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xEB);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x2405);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xCF);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xEB;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xEB);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x2405);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xCF);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xEB;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xEB) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xCF);
                    result += scratch_ram[(ea + 0xEB) & 0xFFFFF];
                    break;
                case 0x75:
                    cycles -= (cpu_mod == 3) ? 15 : 32;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xF8);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x2854);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xF4);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xF8;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xF8);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x2854);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xF4);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xF8;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xF8) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xF4);
                    result += scratch_ram[(ea + 0xF8) & 0xFFFFF];
                    break;
                case 0x76:
                    cycles -= (cpu_mod == 3) ? 16 : 33;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x05);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x2CA3);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x19);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x05;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x05);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x2CA3);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x19);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x05;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x05) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x19);
                    result += scratch_ram[(ea + 0x05) & 0xFFFFF];
                    break;
                case 0x77:
                    cycles -= (cpu_mod == 3) ? 17 : 34;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x12);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x30F2);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x3E);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x12;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x12);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x30F2);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x3E);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x12;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x12) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x3E);
                    result += scratch_ram[(ea + 0x12) & 0xFFFFF];
                    break;
                case 0x78:
                    cycles -= (cpu_mod == 3) ? 18 : 35;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x1F);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x3541);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x63);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x1F;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x1F);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x3541);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x63);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x1F;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x1F) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x63);
                    result += scratch_ram[(ea + 0x1F) & 0xFFFFF];
                    break;
                case 0x79:
                    cycles -= (cpu_mod == 3) ? 19 : 36;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x2C);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x3990);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x88);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x2C;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x2C);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x3990);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x88);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x2C;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x2C) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x88);
                    result += scratch_ram[(ea + 0x2C) & 0xFFFFF];
                    break;
                case 0x7A:
                    cycles -= (cpu_mod == 3) ? 20 : 37;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x39);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x3DDF);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xAD);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x39;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x39);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x3DDF);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xAD);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x39;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x39) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xAD);
                    result += scratch_ram[(ea + 0x39) & 0xFFFFF];
                    break;
                case 0x7B:
                    cycles -= (cpu_mod == 3) ? 21 : 38;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x46);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x422E);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xD2);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x46;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x46);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x422E);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xD2);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x46;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x46) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xD2);
                    result += scratch_ram[(ea + 0x46) & 0xFFFFF];
                    break;
                case 0x7C:
                    cycles -= (cpu_mod == 3) ? 22 : 39;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x53);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x467D);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xF7);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x53;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x53);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x467D);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xF7);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x53;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x53) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xF7);
                    result += scratch_ram[(ea + 0x53) & 0xFFFFF];
                    break;
                case 0x7D:
                    cycles -= (cpu_mod == 3) ? 23 : 40;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x60);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x4ACC);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x1C);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x60;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x60);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x4ACC);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x1C);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x60;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x60) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x1C);
                    result += scratch_ram[(ea + 0x60) & 0xFFFFF];
                    break;
                case 0x7E:
                    cycles -= (cpu_mod == 3) ? 3 : 20;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x6D);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x4F1B);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x41);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x6D;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x6D);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x4F1B);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x41);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x6D;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x6D) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x41);
                    result += scratch_ram[(ea + 0x6D) & 0xFFFFF];
                    break;
                case 0x7F:
                    cycles -= (cpu_mod == 3) ? 4 : 21;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x7A);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x536A);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x66);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x7A;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x7A);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x536A);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x66);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x7A;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x7A) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x66);
                    result += scratch_ram[(ea + 0x7A) & 0xFFFFF];
                    break;
                case 0x80:
                    cycles -= (cpu_mod == 3) ? 5 : 22;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x87);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x57B9);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x8B);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x87;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x87);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x57B9);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x8B);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x87;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x87) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x8B);
                    result += scratch_ram[(ea + 0x87) & 0xFFFFF];
                    break;
                case 0x81:
                    cycles -= (cpu_mod == 3) ? 6 : 23;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x94);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x5C08);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xB0);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x94;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x94);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x5C08);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xB0);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x94;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x94) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xB0);
                    result += scratch_ram[(ea + 0x94) & 0xFFFFF];
                    break;
                case 0x82:
                    cycles -= (cpu_mod == 3) ? 7 : 24;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xA1);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x6057);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xD5);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xA1;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xA1);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x6057);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xD5);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xA1;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xA1) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xD5);
                    result += scratch_ram[(ea + 0xA1) & 0xFFFFF];
                    break;
                case 0x83:
                    cycles -= (cpu_mod == 3) ? 8 : 25;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xAE);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x64A6);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xFA);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xAE;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xAE);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x64A6);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xFA);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xAE;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xAE) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xFA);
                    result += scratch_ram[(ea + 0xAE) & 0xFFFFF];
                    break;
                case 0x84:
                    cycles -= (cpu_mod == 3) ? 9 : 26;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xBB);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x68F5);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x1F);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xBB;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xBB);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x68F5);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x1F);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xBB;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xBB) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x1F);
                    result += scratch_ram[(ea + 0xBB) & 0xFFFFF];
                    break;
                case 0x85:
                    cycles -= (cpu_mod == 3) ? 10 : 27;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xC8);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x6D44);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x44);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xC8;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xC8);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x6D44);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x44);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xC8;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xC8) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x44);
                    result += scratch_ram[(ea + 0xC8) & 0xFFFFF];
                    break;
                case 0x86:
                    cycles -= (cpu_mod == 3) ? 11 : 28;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xD5);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x7193);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x69);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xD5;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xD5);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x7193);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x69);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xD5;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xD5) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x69);
                    result += scratch_ram[(ea + 0xD5) & 0xFFFFF];
                    break;
                case 0x87:
                    cycles -= (cpu_mod == 3) ? 12 : 29;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xE2);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x75E2);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x8E);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xE2;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xE2);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x75E2);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x8E);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xE2;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xE2) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x8E);
                    result += scratch_ram[(ea + 0xE2) & 0xFFFFF];
                    break;
                case 0x88:
                    cycles -= (cpu_mod == 3) ? 13 : 30;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xEF);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x7A31);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xB3);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xEF;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xEF);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x7A31);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xB3);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xEF;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xEF) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xB3);
                    result += scratch_ram[(ea + 0xEF) & 0xFFFFF];
                    break;
                case 0x89:
                    cycles -= (cpu_mod == 3) ? 14 : 31;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xFC);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x7E80);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xD8);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xFC;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xFC);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x7E80);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xD8);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xFC;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xFC) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xD8);
                    result += scratch_ram[(ea + 0xFC) & 0xFFFFF];
                    break;
                case 0x8A:
                    cycles -= (cpu_mod == 3) ? 15 : 32;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x09);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x82CF);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xFD);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x09;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x09);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x82CF);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xFD);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x09;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x09) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xFD);
                    result += scratch_ram[(ea + 0x09) & 0xFFFFF];
                    break;
                case 0x8B:
                    cycles -= (cpu_mod == 3) ? 16 : 33;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x16);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x871E);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x22);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x16;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x16);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x871E);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x22);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x16;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x16) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x22);
                    result += scratch_ram[(ea + 0x16) & 0xFFFFF];
                    break;
                case 0x8C:
                    cycles -= (cpu_mod == 3) ? 17 : 34;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x23);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x8B6D);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x47);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x23;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x23);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x8B6D);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x47);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x23;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x23) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x47);
                    result += scratch_ram[(ea + 0x23) & 0xFFFFF];
                    break;
                case 0x8D:
                    cycles -= (cpu_mod == 3) ? 18 : 35;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x30);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x8FBC);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x6C);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x30;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x30);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x8FBC);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x6C);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x30;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x30) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x6C);
                    result += scratch_ram[(ea + 0x30) & 0xFFFFF];
                    break;
                case 0x8E:
                    cycles -= (cpu_mod == 3) ? 19 : 36;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x3D);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x940B);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x91);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x3D;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x3D);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x940B);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x91);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x3D;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x3D) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x91);
                    result += scratch_ram[(ea + 0x3D) & 0xFFFFF];
                    break;
                case 0x8F:
                    cycles -= (cpu_mod == 3) ? 20 : 37;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x4A);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x985A);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xB6);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x4A;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x4A);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x985A);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xB6);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x4A;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x4A) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xB6);
                    result += scratch_ram[(ea + 0x4A) & 0xFFFFF];
                    break;
                case 0x90:
                    cycles -= (cpu_mod == 3) ? 21 : 38;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x57);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x9CA9);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xDB);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x57;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x57);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x9CA9);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xDB);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x57;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x57) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xDB);
                    result += scratch_ram[(ea + 0x57) & 0xFFFFF];
                    break;
                case 0x91:
                    cycles -= (cpu_mod == 3) ? 22 : 39;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x64);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xA0F8);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x00);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x64;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x64);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xA0F8);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x00);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x64;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x64) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x00);
                    result += scratch_ram[(ea + 0x64) & 0xFFFFF];
                    break;
                case 0x92:
                    cycles -= (cpu_mod == 3) ? 23 : 40;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x71);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xA547);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x25);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x71;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x71);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xA547);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x25);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x71;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x71) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x25);
                    result += scratch_ram[(ea + 0x71) & 0xFFFFF];
                    break;
                case 0x93:
                    cycles -= (cpu_mod == 3) ? 3 : 20;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x7E);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xA996);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x4A);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x7E;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x7E);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xA996);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x4A);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x7E;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x7E) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x4A);
                    result += scratch_ram[(ea + 0x7E) & 0xFFFFF];
                    break;
                case 0x94:
                    cycles -= (cpu_mod == 3) ? 4 : 21;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x8B);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xADE5);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x6F);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x8B;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x8B);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xADE5);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x6F);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x8B;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x8B) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x6F);
                    result += scratch_ram[(ea + 0x8B) & 0xFFFFF];
                    break;
                case 0x95:
                    cycles -= (cpu_mod == 3) ? 5 : 22;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x98);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xB234);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x94);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x98;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x98);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xB234);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x94);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x98;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x98) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x94);
                    result += scratch_ram[(ea + 0x98) & 0xFFFFF];
                    break;
                case 0x96:
                    cycles -= (cpu_mod == 3) ? 6 : 23;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xA5);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xB683);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xB9);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xA5;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xA5);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xB683);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xB9);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xA5;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xA5) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xB9);
                    result += scratch_ram[(ea + 0xA5) & 0xFFFFF];
                    break;
                case 0x97:
                    cycles -= (cpu_mod == 3) ? 7 : 24;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xB2);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xBAD2);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xDE);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xB2;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xB2);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xBAD2);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xDE);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xB2;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xB2) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xDE);
                    result += scratch_ram[(ea + 0xB2) & 0xFFFFF];
                    break;
                case 0x98:
                    cycles -= (cpu_mod == 3) ? 8 : 25;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xBF);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xBF21);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x03);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xBF;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xBF);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xBF21);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x03);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xBF;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xBF) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x03);
                    result += scratch_ram[(ea + 0xBF) & 0xFFFFF];
                    break;
                case 0x99:
                    cycles -= (cpu_mod == 3) ? 9 : 26;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xCC);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xC370);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x28);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xCC;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xCC);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xC370);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x28);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xCC;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xCC) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x28);
                    result += scratch_ram[(ea + 0xCC) & 0xFFFFF];
                    break;
                case 0x9A:
                    cycles -= (cpu_mod == 3) ? 10 : 27;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xD9);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xC7BF);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x4D);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xD9;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xD9);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xC7BF);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x4D);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xD9;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xD9) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x4D);
                    result += scratch_ram[(ea + 0xD9) & 0xFFFFF];
                    break;
                case 0x9B:
                    cycles -= (cpu_mod == 3) ? 11 : 28;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xE6);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xCC0E);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x72);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xE6;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xE6);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xCC0E);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x72);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xE6;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xE6) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x72);
                    result += scratch_ram[(ea + 0xE6) & 0xFFFFF];
                    break;
                case 0x9C:
                    cycles -= (cpu_mod == 3) ? 12 : 29;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xF3);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xD05D);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x97);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xF3;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xF3);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xD05D);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x97);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xF3;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xF3) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x97);
                    result += scratch_ram[(ea + 0xF3) & 0xFFFFF];
                    break;
                case 0x9D:
                    cycles -= (cpu_mod == 3) ? 13 : 30;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x00);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xD4AC);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xBC);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x00;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x00);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xD4AC);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xBC);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x00;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x00) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xBC);
                    result += scratch_ram[(ea + 0x00) & 0xFFFFF];
                    break;
                case 0x9E:
                    cycles -= (cpu_mod == 3) ? 14 : 31;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x0D);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xD8FB);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xE1);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x0D;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x0D);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xD8FB);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xE1);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x0D;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x0D) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xE1);
                    result += scratch_ram[(ea + 0x0D) & 0xFFFFF];
                    break;
                case 0x9F:
                    cycles -= (cpu_mod == 3) ? 15 : 32;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x1A);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xDD4A);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x06);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x1A;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x1A);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xDD4A);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x06);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x1A;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x1A) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x06);
                    result += scratch_ram[(ea + 0x1A) & 0xFFFFF];
                    break;
                case 0xA0:
                    cycles -= (cpu_mod == 3) ? 16 : 33;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x27);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xE199);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x2B);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x27;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x27);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xE199);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x2B);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x27;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x27) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x2B);
                    result += scratch_ram[(ea + 0x27) & 0xFFFFF];
                    break;
                case 0xA1:
                    cycles -= (cpu_mod == 3) ? 17 : 34;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x34);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xE5E8);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x50);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x34;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x34);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xE5E8);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x50);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x34;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x34) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x50);
                    result += scratch_ram[(ea + 0x34) & 0xFFFFF];
                    break;
                case 0xA2:
                    cycles -= (cpu_mod == 3) ? 18 : 35;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x41);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xEA37);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x75);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x41;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x41);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xEA37);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x75);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x41;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x41) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x75);
                    result += scratch_ram[(ea + 0x41) & 0xFFFFF];
                    break;
                case 0xA3:
                    cycles -= (cpu_mod == 3) ? 19 : 36;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x4E);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xEE86);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x9A);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x4E;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x4E);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xEE86);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x9A);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x4E;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x4E) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x9A);
                    result += scratch_ram[(ea + 0x4E) & 0xFFFFF];
                    break;
                case 0xA4:
                    cycles -= (cpu_mod == 3) ? 20 : 37;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x5B);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xF2D5);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xBF);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x5B;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x5B);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xF2D5);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xBF);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x5B;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x5B) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xBF);
                    result += scratch_ram[(ea + 0x5B) & 0xFFFFF];
                    break;
                case 0xA5:
                    cycles -= (cpu_mod == 3) ? 21 : 38;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x68);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xF724);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xE4);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x68;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x68);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xF724);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xE4);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x68;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x68) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xE4);
                    result += scratch_ram[(ea + 0x68) & 0xFFFFF];
                    break;
                case 0xA6:
                    cycles -= (cpu_mod == 3) ? 22 : 39;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x75);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xFB73);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x09);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x75;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x75);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xFB73);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x09);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x75;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x75) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x09);
                    result += scratch_ram[(ea + 0x75) & 0xFFFFF];
                    break;
                case 0xA7:
                    cycles -= (cpu_mod == 3) ? 23 : 40;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x82);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xFFC2);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x2E);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x82;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x82);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xFFC2);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x2E);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x82;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x82) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x2E);
                    result += scratch_ram[(ea + 0x82) & 0xFFFFF];
                    break;
                case 0xA8:
                    cycles -= (cpu_mod == 3) ? 3 : 20;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x8F);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x0411);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x53);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x8F;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x8F);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x0411);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x53);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x8F;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x8F) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x53);
                    result += scratch_ram[(ea + 0x8F) & 0xFFFFF];
                    break;
                case 0xA9:
                    cycles -= (cpu_mod == 3) ? 4 : 21;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x9C);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x0860);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x78);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x9C;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x9C);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x0860);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x78);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x9C;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x9C) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x78);
                    result += scratch_ram[(ea + 0x9C) & 0xFFFFF];
                    break;
                case 0xAA:
                    cycles -= (cpu_mod == 3) ? 5 : 22;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xA9);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x0CAF);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x9D);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xA9;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xA9);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x0CAF);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x9D);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xA9;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xA9) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x9D);
                    result += scratch_ram[(ea + 0xA9) & 0xFFFFF];
                    break;
                case 0xAB:
                    cycles -= (cpu_mod == 3) ? 6 : 23;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xB6);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x10FE);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xC2);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xB6;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xB6);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x10FE);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xC2);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xB6;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xB6) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xC2);
                    result += scratch_ram[(ea + 0xB6) & 0xFFFFF];
                    break;
                case 0xAC:
                    cycles -= (cpu_mod == 3) ? 7 : 24;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xC3);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x154D);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xE7);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xC3;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xC3);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x154D);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xE7);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xC3;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xC3) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xE7);
                    result += scratch_ram[(ea + 0xC3) & 0xFFFFF];
                    break;
                case 0xAD:
                    cycles -= (cpu_mod == 3) ? 8 : 25;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xD0);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x199C);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x0C);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xD0;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xD0);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x199C);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x0C);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xD0;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xD0) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x0C);
                    result += scratch_ram[(ea + 0xD0) & 0xFFFFF];
                    break;
                case 0xAE:
                    cycles -= (cpu_mod == 3) ? 9 : 26;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xDD);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x1DEB);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x31);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xDD;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xDD);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x1DEB);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x31);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xDD;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xDD) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x31);
                    result += scratch_ram[(ea + 0xDD) & 0xFFFFF];
                    break;
                case 0xAF:
                    cycles -= (cpu_mod == 3) ? 10 : 27;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xEA);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x223A);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x56);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xEA;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xEA);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x223A);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x56);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xEA;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xEA) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x56);
                    result += scratch_ram[(ea + 0xEA) & 0xFFFFF];
                    break;
                case 0xB0:
                    cycles -= (cpu_mod == 3) ? 11 : 28;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xF7);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x2689);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x7B);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xF7;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xF7);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x2689);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x7B);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xF7;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xF7) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x7B);
                    result += scratch_ram[(ea + 0xF7) & 0xFFFFF];
                    break;
                case 0xB1:
                    cycles -= (cpu_mod == 3) ? 12 : 29;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x04);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x2AD8);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xA0);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x04;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x04);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x2AD8);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xA0);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x04;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x04) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xA0);
                    result += scratch_ram[(ea + 0x04) & 0xFFFFF];
                    break;
                case 0xB2:
                    cycles -= (cpu_mod == 3) ? 13 : 30;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x11);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x2F27);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xC5);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x11;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x11);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x2F27);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xC5);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x11;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x11) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xC5);
                    result += scratch_ram[(ea + 0x11) & 0xFFFFF];
                    break;
                case 0xB3:
                    cycles -= (cpu_mod == 3) ? 14 : 31;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x1E);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x3376);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xEA);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x1E;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x1E);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x3376);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xEA);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x1E;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x1E) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xEA);
                    result += scratch_ram[(ea + 0x1E) & 0xFFFFF];
                    break;
                case 0xB4:
                    cycles -= (cpu_mod == 3) ? 15 : 32;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x2B);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x37C5);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x0F);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x2B;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x2B);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x37C5);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x0F);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x2B;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x2B) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x0F);
                    result += scratch_ram[(ea + 0x2B) & 0xFFFFF];
                    break;
                case 0xB5:
                    cycles -= (cpu_mod == 3) ? 16 : 33;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x38);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x3C14);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x34);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x38;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x38);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x3C14);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x34);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x38;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x38) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x34);
                    result += scratch_ram[(ea + 0x38) & 0xFFFFF];
                    break;
                case 0xB6:
                    cycles -= (cpu_mod == 3) ? 17 : 34;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x45);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x4063);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x59);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x45;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x45);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x4063);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x59);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x45;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x45) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x59);
                    result += scratch_ram[(ea + 0x45) & 0xFFFFF];
                    break;
                case 0xB7:
                    cycles -= (cpu_mod == 3) ? 18 : 35;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x52);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x44B2);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x7E);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x52;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x52);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x44B2);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x7E);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x52;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x52) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x7E);
                    result += scratch_ram[(ea + 0x52) & 0xFFFFF];
                    break;
                case 0xB8:
                    cycles -= (cpu_mod == 3) ? 19 : 36;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x5F);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x4901);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xA3);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x5F;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x5F);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x4901);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xA3);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x5F;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x5F) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xA3);
                    result += scratch_ram[(ea + 0x5F) & 0xFFFFF];
                    break;
                case 0xB9:
                    cycles -= (cpu_mod == 3) ? 20 : 37;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x6C);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x4D50);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xC8);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x6C;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x6C);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x4D50);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xC8);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x6C;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x6C) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xC8);
                    result += scratch_ram[(ea + 0x6C) & 0xFFFFF];
                    break;
                case 0xBA:
                    cycles -= (cpu_mod == 3) ? 21 : 38;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x79);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x519F);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xED);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x79;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x79);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x519F);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xED);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x79;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x79) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xED);
                    result += scratch_ram[(ea + 0x79) & 0xFFFFF];
                    break;
                case 0xBB:
                    cycles -= (cpu_mod == 3) ? 22 : 39;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x86);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x55EE);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x12);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x86;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x86);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x55EE);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x12);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x86;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x86) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x12);
                    result += scratch_ram[(ea + 0x86) & 0xFFFFF];
                    break;
                case 0xBC:
                    cycles -= (cpu_mod == 3) ? 23 : 40;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x93);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x5A3D);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x37);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x93;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x93);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x5A3D);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x37);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x93;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x93) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x37);
                    result += scratch_ram[(ea + 0x93) & 0xFFFFF];
                    break;
                case 0xBD:
                    cycles -= (cpu_mod == 3) ? 3 : 20;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xA0);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x5E8C);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x5C);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xA0;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xA0);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x5E8C);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x5C);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xA0;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xA0) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x5C);
                    result += scratch_ram[(ea + 0xA0) & 0xFFFFF];
                    break;
                case 0xBE:
                    cycles -= (cpu_mod == 3) ? 4 : 21;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xAD);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x62DB);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x81);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xAD;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xAD);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x62DB);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x81);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xAD;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xAD) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x81);
                    result += scratch_ram[(ea + 0xAD) & 0xFFFFF];
                    break;
                case 0xBF:
                    cycles -= (cpu_mod == 3) ? 5 : 22;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xBA);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x672A);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xA6);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xBA;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xBA);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x672A);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xA6);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xBA;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xBA) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xA6);
                    result += scratch_ram[(ea + 0xBA) & 0xFFFFF];
                    break;
                case 0xC0:
                    cycles -= (cpu_mod == 3) ? 6 : 23;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xC7);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x6B79);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xCB);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xC7;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xC7);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x6B79);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xCB);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xC7;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xC7) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xCB);
                    result += scratch_ram[(ea + 0xC7) & 0xFFFFF];
                    break;
                case 0xC1:
                    cycles -= (cpu_mod == 3) ? 7 : 24;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xD4);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x6FC8);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xF0);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xD4;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xD4);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x6FC8);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xF0);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xD4;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xD4) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xF0);
                    result += scratch_ram[(ea + 0xD4) & 0xFFFFF];
                    break;
                case 0xC2:
                    cycles -= (cpu_mod == 3) ? 8 : 25;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xE1);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x7417);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x15);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xE1;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xE1);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x7417);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x15);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xE1;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xE1) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x15);
                    result += scratch_ram[(ea + 0xE1) & 0xFFFFF];
                    break;
                case 0xC3:
                    cycles -= (cpu_mod == 3) ? 9 : 26;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xEE);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x7866);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x3A);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xEE;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xEE);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x7866);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x3A);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xEE;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xEE) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x3A);
                    result += scratch_ram[(ea + 0xEE) & 0xFFFFF];
                    break;
                case 0xC4:
                    cycles -= (cpu_mod == 3) ? 10 : 27;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xFB);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x7CB5);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x5F);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xFB;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xFB);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x7CB5);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x5F);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xFB;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xFB) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x5F);
                    result += scratch_ram[(ea + 0xFB) & 0xFFFFF];
                    break;
                case 0xC5:
                    cycles -= (cpu_mod == 3) ? 11 : 28;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x08);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x8104);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x84);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x08;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x08);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x8104);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x84);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x08;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x08) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x84);
                    result += scratch_ram[(ea + 0x08) & 0xFFFFF];
                    break;
                case 0xC6:
                    cycles -= (cpu_mod == 3) ? 12 : 29;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x15);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x8553);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xA9);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x15;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x15);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x8553);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xA9);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x15;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x15) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xA9);
                    result += scratch_ram[(ea + 0x15) & 0xFFFFF];
                    break;
                case 0xC7:
                    cycles -= (cpu_mod == 3) ? 13 : 30;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x22);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x89A2);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xCE);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x22;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x22);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x89A2);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xCE);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x22;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x22) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xCE);
                    result += scratch_ram[(ea + 0x22) & 0xFFFFF];
                    break;
                case 0xC8:
                    cycles -= (cpu_mod == 3) ? 14 : 31;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x2F);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x8DF1);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xF3);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x2F;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x2F);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x8DF1);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xF3);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x2F;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x2F) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xF3);
                    result += scratch_ram[(ea + 0x2F) & 0xFFFFF];
                    break;
                case 0xC9:
                    cycles -= (cpu_mod == 3) ? 15 : 32;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x3C);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x9240);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x18);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x3C;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x3C);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x9240);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x18);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x3C;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x3C) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x18);
                    result += scratch_ram[(ea + 0x3C) & 0xFFFFF];
                    break;
                case 0xCA:
                    cycles -= (cpu_mod == 3) ? 16 : 33;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x49);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x968F);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x3D);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x49;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x49);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x968F);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x3D);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x49;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x49) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x3D);
                    result += scratch_ram[(ea + 0x49) & 0xFFFFF];
                    break;
                case 0xCB:
                    cycles -= (cpu_mod == 3) ? 17 : 34;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x56);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x9ADE);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x62);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x56;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x56);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x9ADE);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x62);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x56;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x56) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x62);
                    result += scratch_ram[(ea + 0x56) & 0xFFFFF];
                    break;
                case 0xCC:
                    cycles -= (cpu_mod == 3) ? 18 : 35;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x63);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x9F2D);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x87);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x63;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x63);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x9F2D);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x87);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x63;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x63) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x87);
                    result += scratch_ram[(ea + 0x63) & 0xFFFFF];
                    break;
                case 0xCD:
                    cycles -= (cpu_mod == 3) ? 19 : 36;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x70);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xA37C);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xAC);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x70;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x70);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xA37C);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xAC);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x70;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x70) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xAC);
                    result += scratch_ram[(ea + 0x70) & 0xFFFFF];
                    break;
                case 0xCE:
                    cycles -= (cpu_mod == 3) ? 20 : 37;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x7D);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xA7CB);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xD1);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x7D;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x7D);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xA7CB);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xD1);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x7D;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x7D) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xD1);
                    result += scratch_ram[(ea + 0x7D) & 0xFFFFF];
                    break;
                case 0xCF:
                    cycles -= (cpu_mod == 3) ? 21 : 38;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x8A);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xAC1A);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xF6);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x8A;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x8A);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xAC1A);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xF6);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x8A;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x8A) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xF6);
                    result += scratch_ram[(ea + 0x8A) & 0xFFFFF];
                    break;
                case 0xD0:
                    cycles -= (cpu_mod == 3) ? 22 : 39;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x97);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xB069);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x1B);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x97;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x97);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xB069);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x1B);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x97;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x97) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x1B);
                    result += scratch_ram[(ea + 0x97) & 0xFFFFF];
                    break;
                case 0xD1:
                    cycles -= (cpu_mod == 3) ? 23 : 40;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xA4);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xB4B8);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x40);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xA4;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xA4);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xB4B8);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x40);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xA4;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xA4) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x40);
                    result += scratch_ram[(ea + 0xA4) & 0xFFFFF];
                    break;
                case 0xD2:
                    cycles -= (cpu_mod == 3) ? 3 : 20;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xB1);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xB907);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x65);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xB1;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xB1);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xB907);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x65);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xB1;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xB1) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x65);
                    result += scratch_ram[(ea + 0xB1) & 0xFFFFF];
                    break;
                case 0xD3:
                    cycles -= (cpu_mod == 3) ? 4 : 21;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xBE);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xBD56);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x8A);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xBE;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xBE);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xBD56);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x8A);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xBE;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xBE) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x8A);
                    result += scratch_ram[(ea + 0xBE) & 0xFFFFF];
                    break;
                case 0xD4:
                    cycles -= (cpu_mod == 3) ? 5 : 22;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xCB);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xC1A5);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xAF);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xCB;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xCB);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xC1A5);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xAF);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xCB;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xCB) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xAF);
                    result += scratch_ram[(ea + 0xCB) & 0xFFFFF];
                    break;
                case 0xD5:
                    cycles -= (cpu_mod == 3) ? 6 : 23;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xD8);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xC5F4);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xD4);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xD8;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xD8);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xC5F4);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xD4);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xD8;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xD8) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xD4);
                    result += scratch_ram[(ea + 0xD8) & 0xFFFFF];
                    break;
                case 0xD6:
                    cycles -= (cpu_mod == 3) ? 7 : 24;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xE5);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xCA43);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xF9);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xE5;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xE5);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xCA43);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xF9);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xE5;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xE5) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xF9);
                    result += scratch_ram[(ea + 0xE5) & 0xFFFFF];
                    break;
                case 0xD7:
                    cycles -= (cpu_mod == 3) ? 8 : 25;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xF2);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xCE92);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x1E);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xF2;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xF2);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xCE92);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x1E);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xF2;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xF2) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x1E);
                    result += scratch_ram[(ea + 0xF2) & 0xFFFFF];
                    break;
                case 0xD8:
                    cycles -= (cpu_mod == 3) ? 9 : 26;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xFF);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xD2E1);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x43);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xFF;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xFF);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xD2E1);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x43);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xFF;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xFF) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x43);
                    result += scratch_ram[(ea + 0xFF) & 0xFFFFF];
                    break;
                case 0xD9:
                    cycles -= (cpu_mod == 3) ? 10 : 27;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x0C);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xD730);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x68);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x0C;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x0C);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xD730);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x68);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x0C;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x0C) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x68);
                    result += scratch_ram[(ea + 0x0C) & 0xFFFFF];
                    break;
                case 0xDA:
                    cycles -= (cpu_mod == 3) ? 11 : 28;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x19);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xDB7F);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x8D);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x19;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x19);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xDB7F);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x8D);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x19;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x19) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x8D);
                    result += scratch_ram[(ea + 0x19) & 0xFFFFF];
                    break;
                case 0xDB:
                    cycles -= (cpu_mod == 3) ? 12 : 29;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x26);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xDFCE);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xB2);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x26;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x26);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xDFCE);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xB2);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x26;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x26) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xB2);
                    result += scratch_ram[(ea + 0x26) & 0xFFFFF];
                    break;
                case 0xDC:
                    cycles -= (cpu_mod == 3) ? 13 : 30;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x33);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xE41D);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xD7);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x33;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x33);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xE41D);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xD7);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x33;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x33) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xD7);
                    result += scratch_ram[(ea + 0x33) & 0xFFFFF];
                    break;
                case 0xDD:
                    cycles -= (cpu_mod == 3) ? 14 : 31;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x40);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xE86C);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xFC);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x40;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x40);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xE86C);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xFC);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x40;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x40) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xFC);
                    result += scratch_ram[(ea + 0x40) & 0xFFFFF];
                    break;
                case 0xDE:
                    cycles -= (cpu_mod == 3) ? 15 : 32;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x4D);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xECBB);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x21);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x4D;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x4D);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xECBB);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x21);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x4D;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x4D) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x21);
                    result += scratch_ram[(ea + 0x4D) & 0xFFFFF];
                    break;
                case 0xDF:
                    cycles -= (cpu_mod == 3) ? 16 : 33;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x5A);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xF10A);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x46);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x5A;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x5A);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xF10A);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x46);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x5A;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x5A) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x46);
                    result += scratch_ram[(ea + 0x5A) & 0xFFFFF];
                    break;
                case 0xE0:
                    cycles -= (cpu_mod == 3) ? 17 : 34;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x67);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xF559);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x6B);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x67;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x67);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xF559);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x6B);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x67;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x67) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x6B);
                    result += scratch_ram[(ea + 0x67) & 0xFFFFF];
                    break;
                case 0xE1:
                    cycles -= (cpu_mod == 3) ? 18 : 35;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x74);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xF9A8);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x90);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x74;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x74);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xF9A8);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x90);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x74;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x74) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x90);
                    result += scratch_ram[(ea + 0x74) & 0xFFFFF];
                    break;
                case 0xE2:
                    cycles -= (cpu_mod == 3) ? 19 : 36;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x81);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xFDF7);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xB5);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x81;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x81);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0xFDF7);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xB5);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x81;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x81) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xB5);
                    result += scratch_ram[(ea + 0x81) & 0xFFFFF];
                    break;
                case 0xE3:
                    cycles -= (cpu_mod == 3) ? 20 : 37;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x8E);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x0246);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xDA);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x8E;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x8E);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x0246);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xDA);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x8E;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x8E) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xDA);
                    result += scratch_ram[(ea + 0x8E) & 0xFFFFF];
                    break;
                case 0xE4:
                    cycles -= (cpu_mod == 3) ? 21 : 38;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x9B);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x0695);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xFF);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x9B;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x9B);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x0695);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xFF);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x9B;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x9B) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xFF);
                    result += scratch_ram[(ea + 0x9B) & 0xFFFFF];
                    break;
                case 0xE5:
                    cycles -= (cpu_mod == 3) ? 22 : 39;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xA8);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x0AE4);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x24);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xA8;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xA8);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x0AE4);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x24);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xA8;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xA8) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x24);
                    result += scratch_ram[(ea + 0xA8) & 0xFFFFF];
                    break;
                case 0xE6:
                    cycles -= (cpu_mod == 3) ? 23 : 40;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xB5);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x0F33);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x49);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xB5;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xB5);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x0F33);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x49);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xB5;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xB5) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x49);
                    result += scratch_ram[(ea + 0xB5) & 0xFFFFF];
                    break;
                case 0xE7:
                    cycles -= (cpu_mod == 3) ? 3 : 20;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xC2);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x1382);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x6E);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xC2;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xC2);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x1382);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x6E);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xC2;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xC2) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x6E);
                    result += scratch_ram[(ea + 0xC2) & 0xFFFFF];
                    break;
                case 0xE8:
                    cycles -= (cpu_mod == 3) ? 4 : 21;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xCF);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x17D1);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x93);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xCF;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xCF);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x17D1);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x93);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xCF;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xCF) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x93);
                    result += scratch_ram[(ea + 0xCF) & 0xFFFFF];
                    break;
                case 0xE9:
                    cycles -= (cpu_mod == 3) ? 5 : 22;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xDC);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x1C20);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xB8);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xDC;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xDC);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x1C20);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xB8);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xDC;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xDC) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xB8);
                    result += scratch_ram[(ea + 0xDC) & 0xFFFFF];
                    break;
                case 0xEA:
                    cycles -= (cpu_mod == 3) ? 6 : 23;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xE9);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x206F);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xDD);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xE9;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xE9);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x206F);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xDD);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xE9;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xE9) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xDD);
                    result += scratch_ram[(ea + 0xE9) & 0xFFFFF];
                    break;
                case 0xEB:
                    cycles -= (cpu_mod == 3) ? 7 : 24;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xF6);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x24BE);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x02);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xF6;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xF6);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x24BE);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x02);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xF6;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xF6) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x02);
                    result += scratch_ram[(ea + 0xF6) & 0xFFFFF];
                    break;
                case 0xEC:
                    cycles -= (cpu_mod == 3) ? 8 : 25;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x03);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x290D);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x27);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x03;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x03);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x290D);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x27);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x03;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x03) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x27);
                    result += scratch_ram[(ea + 0x03) & 0xFFFFF];
                    break;
                case 0xED:
                    cycles -= (cpu_mod == 3) ? 9 : 26;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x10);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x2D5C);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x4C);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x10;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x10);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x2D5C);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x4C);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x10;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x10) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x4C);
                    result += scratch_ram[(ea + 0x10) & 0xFFFFF];
                    break;
                case 0xEE:
                    cycles -= (cpu_mod == 3) ? 10 : 27;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x1D);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x31AB);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x71);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x1D;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x1D);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x31AB);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x71);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x1D;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x1D) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x71);
                    result += scratch_ram[(ea + 0x1D) & 0xFFFFF];
                    break;
                case 0xEF:
                    cycles -= (cpu_mod == 3) ? 11 : 28;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x2A);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x35FA);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x96);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x2A;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x2A);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x35FA);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x96);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x2A;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x2A) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x96);
                    result += scratch_ram[(ea + 0x2A) & 0xFFFFF];
                    break;
                case 0xF0:
                    cycles -= (cpu_mod == 3) ? 12 : 29;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x37);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x3A49);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xBB);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x37;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x37);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x3A49);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xBB);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x37;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x37) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xBB);
                    result += scratch_ram[(ea + 0x37) & 0xFFFFF];
                    break;
                case 0xF1:
                    cycles -= (cpu_mod == 3) ? 13 : 30;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x44);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x3E98);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xE0);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x44;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x44);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x3E98);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xE0);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x44;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x44) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xE0);
                    result += scratch_ram[(ea + 0x44) & 0xFFFFF];
                    break;
                case 0xF2:
                    cycles -= (cpu_mod == 3) ? 14 : 31;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x51);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x42E7);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x05);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x51;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x51);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x42E7);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x05);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x51;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x51) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x05);
                    result += scratch_ram[(ea + 0x51) & 0xFFFFF];
                    break;
                case 0xF3:
                    cycles -= (cpu_mod == 3) ? 15 : 32;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x5E);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x4736);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x2A);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x5E;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x5E);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x4736);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x2A);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x5E;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x5E) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x2A);
                    result += scratch_ram[(ea + 0x5E) & 0xFFFFF];
                    break;
                case 0xF4:
                    cycles -= (cpu_mod == 3) ? 16 : 33;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x6B);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x4B85);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x4F);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x6B;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x6B);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x4B85);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x4F);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x6B;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x6B) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x4F);
                    result += scratch_ram[(ea + 0x6B) & 0xFFFFF];
                    break;
                case 0xF5:
                    cycles -= (cpu_mod == 3) ? 17 : 34;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x78);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x4FD4);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x74);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x78;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x78);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x4FD4);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x74);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x78;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x78) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x74);
                    result += scratch_ram[(ea + 0x78) & 0xFFFFF];
                    break;
                case 0xF6:
                    cycles -= (cpu_mod == 3) ? 18 : 35;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x85);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x5423);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x99);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x85;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x85);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x5423);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x99);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x85;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x85) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x99);
                    result += scratch_ram[(ea + 0x85) & 0xFFFFF];
                    break;
                case 0xF7:
                    cycles -= (cpu_mod == 3) ? 19 : 36;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x92);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x5872);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xBE);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x92;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x92);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x5872);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xBE);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x92;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x92) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xBE);
                    result += scratch_ram[(ea + 0x92) & 0xFFFFF];
                    break;
                case 0xF8:
                    cycles -= (cpu_mod == 3) ? 20 : 37;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x9F);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x5CC1);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xE3);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x9F;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0x9F);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x5CC1);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xE3);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0x9F;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0x9F) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xE3);
                    result += scratch_ram[(ea + 0x9F) & 0xFFFFF];
                    break;
                case 0xF9:
                    cycles -= (cpu_mod == 3) ? 21 : 38;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xAC);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x6110);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x08);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xAC;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xAC);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x6110);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x08);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xAC;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xAC) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x08);
                    result += scratch_ram[(ea + 0xAC) & 0xFFFFF];
                    break;
                case 0xFA:
                    cycles -= (cpu_mod == 3) ? 22 : 39;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xB9);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x655F);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x2D);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xB9;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xB9);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x655F);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x2D);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xB9;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xB9) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x2D);
                    result += scratch_ram[(ea + 0xB9) & 0xFFFFF];
                    break;
                case 0xFB:
                    cycles -= (cpu_mod == 3) ? 23 : 40;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xC6);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x69AE);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x52);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xC6;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xC6);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x69AE);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x52);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xC6;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xC6) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x52);
                    result += scratch_ram[(ea + 0xC6) & 0xFFFFF];
                    break;
                case 0xFC:
                    cycles -= (cpu_mod == 3) ? 3 : 20;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xD3);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x6DFD);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x77);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xD3;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xD3);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x6DFD);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x77);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xD3;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xD3) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x77);
                    result += scratch_ram[(ea + 0xD3) & 0xFFFFF];
                    break;
                case 0xFD:
                    cycles -= (cpu_mod == 3) ? 4 : 21;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xE0);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x724C);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x9C);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xE0;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xE0);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x724C);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0x9C);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xE0;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xE0) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0x9C);
                    result += scratch_ram[(ea + 0xE0) & 0xFFFFF];
                    break;
                case 0xFE:
                    cycles -= (cpu_mod == 3) ? 5 : 22;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xED);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x769B);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xC1);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xED;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xED);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x769B);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xC1);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xED;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xED) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xC1);
                    result += scratch_ram[(ea + 0xED) & 0xFFFFF];
                    break;
                case 0xFF:
                    cycles -= (cpu_mod == 3) ? 6 : 23;
                    switch (rmdat & 0x38)
                    {
                        case 0x00:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xFA);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x08:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x7AEA);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x10:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x18:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xE6);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xFA;
                            break;
                        case 0x20:
                            temp = getr8(cpu_rm);
                            temp2 = (uint8_t)(temp + 0xFA);
                            setr8(cpu_reg, temp2);
                            result += temp2;
                            break;
                        case 0x28:
                            tempw = (uint16_t)(regs[cpu_rm & 3].w ^ 0x7AEA);
                            regs[cpu_reg & 3].w = tempw;
                            result += tempw;
                            break;
                        case 0x30:
                            c = (rmdat & 7) + 1;
                            tempw2 = regs[cpu_rm & 3].w;
                            while (c > 0) { tempw2 = (uint16_t)((tempw2 << 1) | (tempw2 >> 15)); c--; cycles -= 4; }
                            regs[cpu_rm & 3].w = tempw2;
                            result += tempw2;
                            break;
                        case 0x38:
                            temp = (uint8_t)(getr8(cpu_reg) - 0xE6);
                            setr8(cpu_rm, temp);
                            result += temp ^ 0xFA;
                            break;
                    }
                    ea = (uint32_t)((ea_seg_base + tempw + 0xFA) & 0xFFFFF);
                    scratch_ram[ea] = (uint8_t)(result + 0xE6);
                    result += scratch_ram[(ea + 0xFA) & 0xFFFFF];
                    break;
            }

            result += cpu_mod + cpu_reg + cpu_rm;
            break;
        }

        return result;
    }
}
