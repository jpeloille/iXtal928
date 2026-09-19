// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/memory/mem_bios.c
// STATUS: partial — romfread, mem_load_basic et loadbios réduit au seul cas
//         ROM_IBMPC. Les 101 autres romsets sont omis.

using static iXtal26.Flash.rom;
using static iXtal26.Memory.mem;
using static iXtal26.Video.fontformat_t;
using static iXtal26.Video.video;
using static iXtal26.pc;

namespace iXtal26.Memory;

internal static partial class mem_bios
{
    // DEVIATION: pclog() et printf() — shims locaux au fichier, comme fatal() à
    //   mem.cs:169. Un échec de chargement de ROM doit rester audible.
    private static void pclog(string s) => Console.Error.Write(s);
    private static void printf(string s) => Console.Write(s);

    // pcem: mem_bios.c:9-14
    // DEVIATION: `uint8_t *buf` -> (tableau, offset), comme mem_mapping_t.exec.
    //   `result` compte des ÉLÉMENTS de `size` octets, pas des octets.
    private static void romfread(uint8_t[] buf, int buf_offset, int size, int count, FileStream fp)
    {
            int result = fread(buf, buf_offset, size, count, fp);
            if (result < count)
                    pclog($"ROM read failed: Expected {count}, read {result}\n");
    }

    // pcem: mem_bios.c:16-53
    private static int mem_load_basic(string path)
    {
            string s;
            FileStream? f;

            s = $"{path}/ibm-basic-1.10.rom";
            f = romfopen(s, "rb");
            if (f == null)
            {
                    s = $"{path}/basicc11.f6";
                    f = romfopen(s, "rb");
                    if (f == null)
                            return 1; /*I don't really care if BASIC is there or not*/
                    romfread(rom, 0x6000, 8192, 1, f);
                    f.Close();
                    s = $"{path}/basicc11.f8";
                    f = romfopen(s, "rb");
                    if (f == null)
                            return 0; /*But if some of it is there, then all of it must be*/
                    romfread(rom, 0x8000, 8192, 1, f);
                    f.Close();
                    s = $"{path}/basicc11.fa";
                    f = romfopen(s, "rb");
                    if (f == null)
                            return 0;
                    romfread(rom, 0xA000, 8192, 1, f);
                    f.Close();
                    s = $"{path}/basicc11.fc";
                    f = romfopen(s, "rb");
                    if (f == null)
                            return 0;
                    romfread(rom, 0xC000, 8192, 1, f);
                    f.Close();
            }
            else
            {
                    romfread(rom, 0x6000, 32768, 1, f);
                    f.Close();
            }

            return 1;
    }

    // pcem: mem_bios.c:55-72, 543-552, 1278-1284
    internal static int loadbios()
    {
            FileStream? f = null;
            // omitted: `FILE *ff` et `int c` (mem_bios.c:56-57) — leurs seuls usages
            //   sont dans les romsets omis.

            loadfont("mda.rom", FONT_MDA);
            loadfont("wy700.rom", FONT_WY700);
            loadfont("8x12.bin", FONT_MDSI);
            loadfont("im1024font.bin", FONT_IM1024);

            biosmask = 0xffff;

            // omitted: `if (!rom) rom = malloc(0x40000)` (mem_bios.c:66-67) — rom est
            //   alloué à sa déclaration (mem.cs:108) et mem_add_bios en a déjà pris la
            //   référence comme `exec` de quatre mappages : réallouer les orphelinerait.
            Array.Fill(romext, (uint8_t)0x63, 0, 0x4000);
            Array.Fill(rom, (uint8_t)0xff, 0, 0x20000);

            // omitted: pclog("Starting with romset %i\n", romset) — sortie pure.

            switch (romset)
            {
            case ROM_IBMPC:
                    f = romfopen("ibmpc/pc102782.bin", "rb");
                    if (f == null)
                            break;
                    romfread(rom, 0xE000, 8192, 1, f);
                    f.Close();
                    if (mem_load_basic("ibmpc") == 0)
                            break;
                    return 1;

            // omitted: les 101 autres cas de `romset` (mem_bios.c:74-542, 553-1277),
            //   de ROM_PC1512 à ROM_GA686BX — machines hors cible IBM PC 5150.
            }
            printf("Failed to load ROM!\n");
            // pcem bug, reproduced: `f` n'est pas remis à NULL par le fclose de la
            //   ligne 549 ; ce fclose-ci est donc un double fclose. Stream.Close()
            //   étant idempotent, la faute n'a pas de conséquence ici.
            if (f != null)
                    f.Close();
            // omitted: `if (ff) fclose(ff)` (mem_bios.c:1282-1283) — ff n'est écrit
            //   que par les romsets omis.
            return 0;
    }
}
