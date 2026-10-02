// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/memory/mem_bios.c
// STATUS: partial — romfread, mem_load_basic et loadbios réduit aux cas
//         ROM_IBMPC, ROM_IBMXT et, depuis B3, ROM_IBMAT — le seul dont les deux
//         ROM sont ENTRELACÉES octet par octet, u27 les pairs et u47 les impairs,
//         parce qu'un AT a un bus de données de seize bits. Les 99 autres romsets
//         sont omis.

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
            FileStream? ff;
            // omitted: `int c` (mem_bios.c:57) — ses seuls usages sont dans les
            //   romsets omis. `ff` (mem_bios.c:56) est vivant depuis ROM_IBMXT :
            //   la variante deux puces du XT lit deux fichiers à la fois.

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

            // pcem: mem_bios.c:396-403 — L'AMI 286, ET C'EST LE CHARGEMENT LE PLUS
            // SIMPLE DU DÉPÔT : un seul fichier de 64 Ko, lu d'un trait.
            //
            // À COMPARER AVEC L'IBM AT juste en dessous, dont les deux ROM sont
            // entrelacées octet par octet. La différence n'est pas d'époque mais de
            // conception : IBM a câblé son bus de seize bits avec deux boîtiers de
            // huit, les clones ont attendu qu'un boîtier de seize existe.
            //
            // ET C'EST CE BIOS QUI DÉBLOQUE LE « 161-System Options Not Set » : un AMI
            // porte son SETUP en ROM, là où celui de l'IBM AT est sur la disquette de
            // diagnostics — que ce dépôt n'a pas. Le message « (Run SETUP) » de l'AT
            // désigne donc un programme introuvable.
            case ROM_AMI286:
                    f = romfopen("ami286/amic206.bin", "rb");
                    if (f == null)
                            break;
                    romfread(rom, 0, 65536, 1, f);
                    f.Close();
                    // omitted: `memset(romext, 0x63, 0x8000)` (mem_bios.c:402) — PCem
                    //   l'a mis en commentaire lui-même.
                    return 1;

            // pcem: mem_bios.c:350-357 — L'AMI 386SX (G3.1) : une ROM de 64 Ko d'un seul
            // tenant, comme l'AMI 286.
            case ROM_AMI386SX:
                    // omitted: `//f=romfopen("at386/at386.bin","rb");` (:351) — en commentaire
                    //   dans PCem.
                    f = romfopen("ami386/ami386.bin", "rb");
                    if (f == null)
                            break;
                    romfread(rom, 0, 65536, 1, f);
                    f.Close();
                    return 1;

            // pcem: mem_bios.c:359-365 — L'AMI 386DX (G3.2), chipset OPTi 82C495.
            case ROM_AMI386DX_OPTI495: /*This uses the OPTi 82C495 chipset*/
                    f = romfopen("ami386dx/opt495sx.ami", "rb");
                    if (f == null)
                            break;
                    romfread(rom, 0, 65536, 1, f);
                    f.Close();
                    return 1;

            // pcem: mem_bios.c:569-576 — G6.3, l'AMI 486 (ALi 1429). Le `is486=1` y est commenté.
            case pc.ROM_AMI486:
                    f = romfopen("ami486/ami486.bin", "rb");
                    if (f == null)
                            break;
                    romfread(rom, 0, 65536, 1, f);
                    f.Close();
                    // is486=1;
                    return 1;

            // pcem: mem_bios.c:288-304 — L'IBM AT, ET SES DEUX ROM SONT ENTRELACÉES.
            //
            // Un AT a un bus de DONNÉES de seize bits, et IBM l'a câblé avec deux
            // boîtiers de huit : u27 porte tous les octets PAIRS, u47 tous les IMPAIRS.
            // D'où la boucle `c += 2` et non un romfread par moitié — c'est le seul
            // chargement de ce dépôt qui ne peut pas passer par romfread.
            //
            // getc() OCTET PAR OCTET, 65 536 fois par fichier, est ce que fait le C. Le
            // pendant fidèle est ReadByte() et non un buffer : romfread ne convient pas,
            // et lire les deux fichiers en entier pour les fusionner serait plus rapide
            // mais ne serait plus la même suite d'appels. Ici le coût est nul — un seul
            // chargement au démarrage.
            //
            // omitted: le bloc commenté amic206.bin de `case ROM_IBMAT` (:289-293) —
            //   PCem l'a désactivé lui-même ; le case tombe DANS ROM_IBMAT386, et c'est
            //   ce qui rend les deux machines identiques de ce point de vue.
            case ROM_IBMAT:
                    f = romfopen("ibmat/62x0820.u27", "rb");
                    ff = romfopen("ibmat/62x0821.u47", "rb");
                    // DEVIATION: le C fait `if (!f || !ff) break;` et FUIT celui des deux
                    //   qui s'était ouvert. Ici on le ferme. Sans effet observable sur
                    //   l'émulation — aucun oracle ne peut voir un descripteur — mais un
                    //   FileStream abandonné garde le fichier verrouillé jusqu'au passage
                    //   du ramasse-miettes, ce que le C ne fait pas. Corrigé plutôt que
                    //   reproduit, et marqué parce que ce n'est pas le geste du C.
                    if (f == null || ff == null)
                    {
                            f?.Close();
                            ff?.Close();
                            break;
                    }
                    for (var c = 0x0000; c < 0x10000; c += 2)
                    {
                            rom[c] = (uint8_t)f.ReadByte();
                            rom[c + 1] = (uint8_t)ff.ReadByte();
                    }
                    ff.Close();
                    f.Close();
                    return 1;

            // pcem: mem_bios.c:166-181
            case ROM_IBMXT:
                    f = romfopen("ibmxt/xt.rom", "rb");
                    if (f == null)
                    {
                            f = romfopen("ibmxt/5000027.u19", "rb");
                            ff = romfopen("ibmxt/1501512.u18", "rb");
                            if (f == null || ff == null)
                                    break;
                            romfread(rom, 0, 0x8000, 1, f);
                            romfread(rom, 0x8000, 0x8000, 1, ff);
                            ff.Close();
                            f.Close();
                            return 1;
                    }
                    else
                    {
                            romfread(rom, 0, 65536, 1, f);
                            f.Close();
                            return 1;
                    }
                    // omitted: le `break;` de mem_bios.c:181 — inatteignable, les deux
                    //   branches du if/else rendent. C# en fait une erreur (CS0162).
                    //
                    // Pas de mem_load_basic ici, contrairement à ROM_IBMPC : le BASIC
                    // du XT est DANS xt.rom, qui couvre les 64 Ko de F000:0000 d'un
                    // bloc. biosmask vaut 0xffff pour les deux machines.

            // pcem: mem_bios.c:74-86 — G1.2 : l'Amstrad PC1512, deux ROM de 8 Ko entrelacées dans
            // les 16 derniers Ko, et sa police propre (40078.ic127), chargée au format CGA.
            case ROM_PC1512:
                    f = romfopen("pc1512/40043.v1", "rb");
                    ff = romfopen("pc1512/40044.v1", "rb");
                    // DEVIATION: le descripteur ouvert seul est fermé, comme pour ROM_IBMAT.
                    if (f == null || ff == null)
                    {
                            f?.Close();
                            ff?.Close();
                            break;
                    }
                    for (var c = 0xC000; c < 0x10000; c += 2)
                    {
                            rom[c] = (uint8_t)f.ReadByte();
                            rom[c + 1] = (uint8_t)ff.ReadByte();
                    }
                    ff.Close();
                    f.Close();
                    loadfont("pc1512/40078.ic127", FONT_CGA);
                    return 1;

            // pcem: mem_bios.c:234-245 — G1.1 : l'Olivetti M24, deux ROM de 8 Ko entrelacées dans
            // les 16 derniers Ko. Sa police est celle du MDA (mda.rom), chargée plus haut.
            case ROM_OLIM24:
                    f = romfopen("olivetti_m24/olivetti_m24_version_1.43_low.bin", "rb");
                    ff = romfopen("olivetti_m24/olivetti_m24_version_1.43_high.bin", "rb");
                    // DEVIATION: le descripteur ouvert seul est fermé, comme pour ROM_IBMAT.
                    if (f == null || ff == null)
                    {
                            f?.Close();
                            ff?.Close();
                            break;
                    }
                    for (var c = 0x0000; c < 0x4000; c += 2)
                    {
                            rom[c + 0xc000] = (uint8_t)f.ReadByte();
                            rom[c + 0xc001] = (uint8_t)ff.ReadByte();
                    }
                    ff.Close();
                    f.Close();
                    return 1;

            // omitted: les 98 autres cas de `romset` (mem_bios.c:87-165, 182-233, 246-542,
            //   553-1277), de ROM_PC1640 à ROM_GA686BX — machines hors cible.
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
