// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// G2, D6 — PAGE-CHECK, LA SONDE DE LA PAGINATION, écrite AVANT le code (PLAN-386.md,
// « Le point que l'on sous-estime »).
//
// Le fuzzeur et SST ne voient pas la pagination : les bits A (accédé) et D (modifié) des
// entrées de page s'écrivent en RAM PENDANT une traduction, par mmu_writel, directement
// dans _mem_exec — pas par writemembl, donc hors du journal d'écritures. Ici, chaque appel
// de mmutranslatereal (mem.c:220-317) est confronté des deux côtés :
//   - l'adresse physique rendue, abrt, abrt_error, cr2 et mmu_perm ;
//   - le PDE et le PTE RELUS en RAM après l'appel, des deux côtés ;
//   - la RAM entière, hachée, à intervalles.
//
// Les tables sont tirées : un répertoire de 1024 PDE pointant vers une réserve de seize
// tables de pages, toutes EN RAM — mmu_readl lit _mem_exec[addr >> 14], NUL hors de la RAM,
// et l'oracle tombe sur un cr3 ou un PDE qui en sort. Les cadres FINAUX (PTE) sont libres,
// jusqu'à 0xFFFFF : la traduction ne les lit pas, elle les rend. Un sur soixante-quatre vaut
// 0xFFFFF, pour qu'une adresse physique réelle 0xFFFFFFFF croise la sentinelle de faute.
//
// Contexte tiré par appel : écriture ou lecture, CPL 0 à 3, cpl_override, CR0.WP (PCem le
// teste même sur un 386), et une fois sur soixante-quatre un abrt DÉJÀ posé — le retour
// anticipé de la première ligne, qui ne doit toucher ni cr2 ni les tables.
//
// La branche des pages de 4 Mo (`cr4 & CR4_PSE`) est morte sur un 386, où CR4 est un
// opcode invalide : cr4 reste nul et le bit 7 des PDE, tiré, est ignoré des deux côtés.

using iXtal26.Cpu;
using iXtal26.Diag;
using iXtal26.Memory;

namespace iXtal26.Diff;

public static class PageCheck
{
    private const int Pool = 16;

    public static int Run(int iterations, ulong seed, int renew, bool oracleOnly)
    {
        Oracle.CheckAbi();
        Console.WriteLine($"page-check — {iterations} traductions, graine {seed}, tables renouvelées toutes les {renew}" +
                          (oracleOnly ? ", ORACLE SEUL" : ""));
        var st = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
        uint Next() { st ^= st << 13; st ^= st >> 7; st ^= st << 17; return (uint)(st >> 32); }

        Oracle.h_set_core(Oracle.Core386);
        Oracle.h_reset();
        Oracle.h_fill_ram(0x90);
        Oracle.h_seg_clear_residue();
        _386.Reset386();
        mem.fill_ram(0x90);
        _386.ClearSegResidue();

        uint cr3 = 0;
        int fautes = 0, abrtEntree = 0, collisions = 0, ecritures = 0, lectures = 0, bitsAD = 0;
        var cpls = new int[4];

        void Poke(uint addr, uint v)
        {
            byte[] b = [(byte)v, (byte)(v >> 8), (byte)(v >> 16), (byte)(v >> 24)];
            Oracle.h_load(addr, b, 4);
            if (!oracleOnly)
                for (var i = 0; i < 4; i++) mem.ram[(addr + (uint)i) & mem.rammask] = b[i];
        }
        uint LireO(uint addr) => (uint)(Oracle.ReadByte(addr) | (Oracle.ReadByte(addr + 1) << 8)
                                        | (Oracle.ReadByte(addr + 2) << 16) | (Oracle.ReadByte(addr + 3) << 24));
        uint LireS(uint addr) => BitConverter.ToUInt32(mem.ram, (int)(addr & mem.rammask));

        void Tables()
        {
            // Cadres de 4 Ko entre 1 Mo et 15 Mo, tous distincts : le répertoire et la réserve.
            var cadres = new HashSet<uint>();
            while (cadres.Count < Pool + 1)
                cadres.Add(0x100 + Next() % 0xE00);
            var l = cadres.ToArray();
            cr3 = (l[0] << 12) | (Next() & 0xFFF);             // les bits bas de cr3 sont ignorés
            for (uint i = 0; i < 1024; i++)
            {
                var r = Next();
                var pde = (l[1 + r % Pool] << 12) | (Next() & 0xFFF);
                if ((r >> 16) % 4 != 0) pde |= 6;               // U/S et R/W, trois fois sur quatre
                if ((r >> 8) % 8 == 0) pde &= ~1u;             // absent, une fois sur huit
                else pde |= 1;
                Poke((cr3 & ~0xFFFu) + 4 * i, pde);
            }
            for (var t = 0; t < Pool; t++)
                for (uint i = 0; i < 1024; i++)
                {
                    var r = Next();
                    var cadre = (r & 63) == 0 ? 0xFFFFFu : Next() & 0xFFFFF;
                    var pte = (cadre << 12) | (Next() & 0xFFF);
                    if ((r >> 16) % 4 != 0) pte |= 6;
                    if ((r >> 8) % 8 == 0) pte &= ~1u;
                    else pte |= 1;
                    Poke((l[1 + t] << 12) + 4 * i, pte);
                }
        }

        for (var it = 0; it < iterations; it++)
        {
            if (it % renew == 0)
                Tables();

            var addr = Next();
            var ctx = Next();
            if ((ctx >> 13 & 31) == 0) addr |= 0xFFF;          // la dernière adresse d'une page
            var rw = (int)(ctx & 1);
            var cpl = (int)((ctx >> 1) & 3);
            var ovr = (ctx >> 3 & 7) == 0 ? 1 : 0;
            var wp = (ctx >> 6) & 1;
            var abrtIn = (ctx >> 7 & 63) == 0 ? 1 : 0;
            var cr0 = 0x11u | (wp << 16);
            cpls[cpl]++;
            if (abrtIn != 0) abrtEntree++;
            if (rw != 0) ecritures++; else lectures++;

            Oracle.h_setsys386(cr0, cr3, 0, 0);
            var pdeAvantAppel = LireO((cr3 & ~0xFFFu) + ((addr >> 20) & 0xFFC));
            var pteAvantAppel = (pdeAvantAppel & 1) != 0
                ? LireO((pdeAvantAppel & ~0xFFFu) + ((addr >> 10) & 0xFFC)) : 0;
            var rO = Oracle.h_mmutranslate(addr, rw, cpl, ovr, abrtIn);
            var permO = Oracle.h_mmu_perm();
            Oracle.h_getstate(out var a);
            var pdeAddr = (cr3 & ~0xFFFu) + ((addr >> 20) & 0xFFC);
            var pdeO = LireO(pdeAddr);
            var pteAddr = (pdeO & ~0xFFFu) + ((addr >> 10) & 0xFFC);
            var pteO = (pdeO & 1) != 0 ? LireO(pteAddr) : 0;
            if (a.abrt != 0 && abrtIn == 0) fautes++;
            if (pdeO != pdeAvantAppel || pteO != pteAvantAppel) bitsAD++;
            if (rO == 0xFFFFFFFF && a.abrt == 0) collisions++;

            // L'abrt posé ou levé ne doit pas survivre à l'appel : il détournerait le suivant.
            Oracle.h_seg_clear_residue();

            if (oracleOnly)
                continue;

            x86.cr0 = cr0;
            x86.cr3 = cr3;
            _386_common.cpu_state.seg_cs.access = (byte)((_386_common.cpu_state.seg_cs.access & ~0x60) | (cpl << 5));
            x86.cpl_override = ovr;
            _386_common.cpu_state.abrt = (sbyte)abrtIn;
            x86.abrt_error = 0;
            uint rS;
            try
            {
                rS = mem.mmutranslatereal(addr, rw);
            }
            catch (Exception e)
            {
                Console.WriteLine($"\nARRÊT C# à la traduction {it} : {e.Message.Trim()}");
                Resume();
                return 1;
            }
            x86.cpl_override = 0;
            var permS = mem.mmu_perm;
            var d = Diff("adresse physique", rO, rS)
                    ?? Diff("abrt", (uint)a.abrt, (uint)_386_common.cpu_state.abrt)
                    ?? Diff("abrt_error", a.abrt_error, x86.abrt_error)
                    ?? Diff("cr2", a.cr2, x86.cr2)
                    ?? Diff("mmu_perm", (uint)permO, (uint)permS)
                    ?? Diff("PDE relu", pdeO, LireS(pdeAddr))
                    ?? ((pdeO & 1) != 0 ? Diff("PTE relu", pteO, LireS(pteAddr)) : null);
            if (d is null && it % 4096 == 4095)
                d = Fuzzer.CmpRam();
            _386.ClearSegResidue();
            if (d is null)
                continue;

            Console.WriteLine($"\nDIVERGENCE à la traduction {it} : {d}");
            Console.WriteLine($"  linéaire {addr:X8}, {(rw != 0 ? "écriture" : "lecture")}, CPL {cpl}, cpl_override {ovr}, " +
                              $"WP {wp}, abrt en entrée {abrtIn}, cr3 {cr3:X8}, PDE {pdeO:X8}, PTE {pteO:X8}");
            Console.WriteLine($"\n  Rejouer : page-check --seed {seed} --iter {it + 1} --renew {renew}");
            return 1;
        }

        Resume();
        Console.WriteLine(oracleOnly ? "\nOracle seul : la sonde tourne, rien n'est comparé."
                                     : $"\nVert : {iterations} traductions, zéro divergence.");
        return 0;

        void Resume() =>
            Console.WriteLine($"  lectures {lectures}, écritures {ecritures} ; CPL 0/1/2/3 : {cpls[0]}/{cpls[1]}/{cpls[2]}/{cpls[3]} ; " +
                              $"fautes {fautes} ; A/D posés {bitsAD} ; abrt en entrée {abrtEntree} ; physique 0xFFFFFFFF sans faute {collisions}");
    }

    private static string? Diff(string champ, uint o, uint s) =>
        o == s ? null : $"{champ} : oracle 0x{o:X8}, C# 0x{s:X8}";
}
