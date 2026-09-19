// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/flash/rom.c + includes/private/flash/rom.h
// STATUS: transcribed — rom.c en entier. Les shims stdio (fopen, fread) vivent
//         ici faute de fichier hôte qui les porte.

// CS8981 : `rom` et `rom_t` n'ont que des minuscules ASCII. Le .editorconfig
// neutralise l'avertissement pour Cpu/, Memory/, Models/, Keyboard/, Video/,
// PluginApi/ et Diag/ — Flash/ n'y figure pas. Le #pragma tient lieu d'entrée
// manquante et disparaîtra quand Flash/ sera ajouté au glob.
#pragma warning disable CS8981

using iXtal26.Memory;
using static iXtal26.PluginApi.paths;

namespace iXtal26.Flash;

// pcem: rom.h:7-11 — classe et non struct : &rom->mapping est chaînée par
// mem_mapping_add, et `rom` lui-même est passé au handler comme void *p.
internal sealed class rom_t
{
    internal uint8_t[] rom = [];
    internal uint32_t mask;
    internal mem_mapping_t mapping = new();
}

internal static partial class rom
{
    // DEVIATION: pclog() appartient à ibm.h / plugin-api/logging.c, pas encore
    //   transcrits. Shim local au fichier, comme fatal() à mem.cs:169.
    private static void pclog(string s) => Console.Error.Write(s);

    // DEVIATION: stdio de la libc. `FILE *` devient FileStream ; fseek, fclose et
    //   getc se lisent directement sur le flux. Seuls fopen et fread, dont la
    //   valeur de retour porte du sens (NULL, nombre d'éléments lus), gardent un
    //   nom — mem_bios.c consomme le second.
    private static FileStream? fopen(string s, string mode)
    {
            try { return new FileStream(s, FileMode.Open, FileAccess.Read); }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            catch (ArgumentException) { return null; }
    }

    internal static int fread(uint8_t[] buf, int buf_offset, int size, int count, FileStream fp)
            => fp.ReadAtLeast(buf.AsSpan(buf_offset, size * count), size * count, false) / size;

    // pcem: rom.c:10-24
    internal static FileStream? romfopen(string fn, string mode)
    {
            FileStream? f;
            string s;
            int i;

            for (i = 0; i < num_roms_paths; ++i)
            {
                    get_roms_path(i, out s, 511);
                    s = put_backslash(s);
                    s += fn;
                    f = fopen(s, mode);
                    if (f != null)
                            return f;
            }
            return null;
    }

    // pcem: rom.c:26-34
    internal static int rom_present(string fn)
    {
            FileStream? f;
            f = romfopen(fn, "rb");
            if (f != null)
            {
                    f.Close();
                    return 1;
            }
            return 0;
    }

    // pcem: rom.c:36-40
    internal static uint8_t rom_read(uint32_t addr, object? p)
    {
            rom_t rom = (rom_t)p!;
            return rom.rom[addr & rom.mask];
    }

    // pcem: rom.c:41-45
    // DEVIATION: `*(uint16_t *)&rom->rom[...]` est une lecture non alignée par cast
    //   de pointeur, reconstruite ici octet par octet en petit-boutien. Comme en C,
    //   le masque ne s'applique qu'au premier octet : rom_init sur-alloue de quatre
    //   octets pour que le débordement lise du zéro au lieu de lever (cf. mem.cs:660).
    internal static uint16_t rom_readw(uint32_t addr, object? p)
    {
            rom_t rom = (rom_t)p!;
            return (uint16_t)(rom.rom[addr & rom.mask] | (rom.rom[(addr & rom.mask) + 1] << 8));
    }

    // pcem: rom.c:46-50
    internal static uint32_t rom_readl(uint32_t addr, object? p)
    {
            rom_t rom = (rom_t)p!;
            return (uint32_t)(rom.rom[addr & rom.mask] | (rom.rom[(addr & rom.mask) + 1] << 8)
                            | (rom.rom[(addr & rom.mask) + 2] << 16) | (rom.rom[(addr & rom.mask) + 3] << 24));
    }

    // pcem: rom.c:52-71
    internal static int rom_init(rom_t rom, string fn, uint32_t address, int size, int mask, int file_offset, uint32_t flags)
    {
            FileStream? f = romfopen(fn, "rb");

            if (f == null)
            {
                    pclog($"ROM image not found : {fn}\n");
                    return -1;
            }

            // DEVIATION: malloc(size) -> size + 4 octets, cf. rom_readw ci-dessus et
            //   la marge de `ram` à mem.cs:660.
            rom.rom = new uint8_t[size + 4];
            f.Seek(file_offset, SeekOrigin.Begin);
            fread(rom.rom, 0, size, 1, f);
            f.Close();

            rom.mask = (uint32_t)mask;

            mem.mem_mapping_add(rom.mapping, address, (uint32_t)size, rom_read, rom_readw, rom_readl,
                                mem.mem_write_null, mem.mem_write_nullw, mem.mem_write_nulll,
                                rom.rom, 0, flags | mem.MEM_MAPPING_ROM, rom);

            return 0;
    }

    // pcem: rom.c:73-107
    internal static int rom_init_interleaved(rom_t rom, string fn_low, string fn_high, uint32_t address, int size, int mask,
                                             int file_offset, uint32_t flags)
    {
            FileStream? f_low = romfopen(fn_low, "rb");
            FileStream? f_high = romfopen(fn_high, "rb");
            int c;

            if (f_low == null || f_high == null)
            {
                    if (f_low == null)
                            pclog($"ROM image not found : {fn_low}\n");
                    else
                            f_low.Close();
                    if (f_high == null)
                            pclog($"ROM image not found : {fn_high}\n");
                    else
                            f_high.Close();
                    return -1;
            }

            rom.rom = new uint8_t[size + 4];
            f_low.Seek(file_offset, SeekOrigin.Begin);
            f_high.Seek(file_offset, SeekOrigin.Begin);
            for (c = 0; c < size; c += 2)
            {
                    rom.rom[c] = (uint8_t)f_low.ReadByte();
                    rom.rom[c + 1] = (uint8_t)f_high.ReadByte();
            }
            f_high.Close();
            f_low.Close();

            rom.mask = (uint32_t)mask;

            mem.mem_mapping_add(rom.mapping, address, (uint32_t)size, rom_read, rom_readw, rom_readl,
                                mem.mem_write_null, mem.mem_write_nullw, mem.mem_write_nulll,
                                rom.rom, 0, flags | mem.MEM_MAPPING_ROM, rom);

            return 0;
    }

    // pcem: rom.c:109-114
    internal static void rom_deinit(rom_t rom)
    {
            // omitted: assert(rom) — rom_t est une classe, un `rom` nul lève au
            //   déréférencement suivant.
            mem.mem_mapping_remove(rom.mapping);
    }
}
