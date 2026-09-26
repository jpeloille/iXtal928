// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: -
// STATUS: host — outillage, pas de code PCem transcrit.
//
// G2, D0.5 — la sonde SingleStepTests/80386 : le SEUL oracle silicium du cœur 386.
//
// Même doctrine que sst-probe pour le 8088. On fait passer le corpus à l'oracle C (PCem)
// pour figer, forme par forme, la ligne de base de ce que PCem réussit et rate face au
// silicium ; puis au C#, qui doit reproduire cette colonne À L'IDENTIQUE — pas la
// maximiser. Transcrire PCem, même quand il a tort.
//
// CE QUE LE CORPUS NE DIT PAS, et que la sonde ne prétend pas vérifier :
//   - les cycles : ceux d'un 386EX, bus 16 bits, SMM compris ; seul l'état est comparé ;
//   - le mode protégé, la pagination, le V86 : v1_ex_real_mode seulement ;
//   - les cas dont la RAM dépasse 1 Mo : la carte plate du harnais s'arrête là, des deux
//     côtés. Ils sont comptés « hors carte » et non joués, pas comptés comme réussis.
//
// Un pas = une instruction : exec386 exécute préfixes et instruction dans la même
// itération, exception livrée comprise. Le corpus capture l'état APRÈS le HLT qui clôt
// chaque cas, donc eip au-delà de lui ; PCem, lui, laisse pc SUR le HLT (il boucle
// dessus). Le HLT n'est donc pas exécuté, et l'eip attendu est pris moins un — mesuré :
// tous les cas rendaient eip attendu − 1, NOP compris.

using iXtal26.Cpu;
using iXtal26.Diag;
using iXtal26.Memory;

namespace iXtal26.Diff;

internal static class Sst386Probe
{
    private const uint CarteMax = 0x100000;

    internal static int Run(string vectors, List<string> forms, int limit, bool csharp, string? baseline)
    {
        Oracle.CheckAbi();
        var dir = Path.Combine(vectors, "v1_ex_real_mode");
        if (!Directory.Exists(dir))
        {
            Console.Error.WriteLine($"{dir} absent : ./tools/fetch-sst386.sh --all (ou nommer des formes).");
            return 2;
        }

        var revoked = new HashSet<string>(
            File.Exists(Path.Combine(vectors, "revocation_list.txt"))
                ? File.ReadAllLines(Path.Combine(vectors, "revocation_list.txt"))
                      .Select(l => l.Trim().ToLowerInvariant()).Where(l => l.Length > 0)
                : []);

        var files = forms.Count > 0
            ? forms.Select(f => Path.Combine(dir, f + ".MOO.gz")).ToList()
            : Directory.GetFiles(dir, "*.MOO.gz").OrderBy(f => f, StringComparer.Ordinal).ToList();

        Console.WriteLine($"Sonde SST 80386 (mode réel) — cible {(csharp ? "C#" : "oracle")}, " +
                          $"{files.Count} forme(s), {revoked.Count} empreinte(s) révoquée(s)\n");

        var rows = new List<(string form, int pass, int played, int horsCarte, string firstWhy)>();
        foreach (var path in files)
        {
            var form = Path.GetFileName(path)[..^".MOO.gz".Length];
            if (!File.Exists(path))
            {
                Console.WriteLine($"  {form,-10} ABSENT");
                continue;
            }

            var moo = MooFile.Load(path);
            int pass = 0, played = 0, horsCarte = 0;
            var firstWhy = "";
            foreach (var t in moo.Tests)
            {
                if (limit > 0 && played >= limit)
                    break;
                if (revoked.Contains(t.Hash))
                    continue;
                if (t.InitRam.Any(e => e.addr >= CarteMax) || t.FinalRam.Any(e => e.addr >= CarteMax))
                {
                    horsCarte++;
                    continue;
                }

                played++;
                string? why;
                try
                {
                    why = RunCase(moo, t, csharp);
                }
                catch (Exception e)
                {
                    // Un arrêt du cœur est un RÉSULTAT : il nomme ce qui manque.
                    why = "arrêt : " + e.Message.Trim().Split('\n')[0];
                }

                if (why is null)
                    pass++;
                else if (firstWhy.Length == 0)
                    firstWhy = $"#{t.Index} « {t.Name} » : {why}";
            }

            rows.Add((form, pass, played, horsCarte, firstWhy));
            var rate = played == 0 ? 0 : 100.0 * pass / played;
            Console.WriteLine($"  {form,-10} {moo.Mnemonic,-8} {pass,5}/{played,-5} {rate,6:F2} %" +
                              (horsCarte > 0 ? $"  ({horsCarte} hors carte)" : "") +
                              (firstWhy.Length > 0 ? $"\n             {firstWhy}" : ""));
        }

        if (baseline is not null)
        {
            using var w = new StreamWriter(baseline);
            w.WriteLine("# Généré par : iXtal26.Diff sst386-probe --baseline");
            w.WriteLine($"# Cible : {(csharp ? "C#" : "oracle")} — ABI {Oracle.h_abi_version()}");
            w.WriteLine("# Le C# doit reproduire la colonne 'passe' de l'oracle à l'identique, pas la maximiser.");
            w.WriteLine("forme\tpasse\tjoues\thors_carte");
            foreach (var r in rows)
                w.WriteLine($"{r.form}\t{r.pass}\t{r.played}\t{r.horsCarte}");
            Console.WriteLine($"\nLigne de base écrite : {baseline}");
        }

        var total = rows.Sum(r => r.played);
        var ok = rows.Sum(r => r.pass);
        Console.WriteLine($"\n{ok} / {total} cas réussis sur {rows.Count} forme(s) ; " +
                          $"{rows.Sum(r => r.horsCarte)} hors carte. Comparer forme par forme, pas ce total.");
        return 0;
    }

    /// <summary>Rend null si l'état final concorde avec le silicium, sinon la raison.</summary>
    private static string? RunCase(MooFile moo, MooTest t, bool csharp)
    {
        var r = t.Init;
        uint V(int i) => r[i] ?? 0;

        // Le vecteur 16 bits de h_setregs (ordre H_R_*), puis ce qu'un 386 a de plus.
        var regs16 = new ushort[(int)Diag.R.COUNT];
        regs16[(int)Diag.R.AX] = (ushort)V(2);
        regs16[(int)Diag.R.BX] = (ushort)V(3);
        regs16[(int)Diag.R.CX] = (ushort)V(4);
        regs16[(int)Diag.R.DX] = (ushort)V(5);
        regs16[(int)Diag.R.SI] = (ushort)V(6);
        regs16[(int)Diag.R.DI] = (ushort)V(7);
        regs16[(int)Diag.R.BP] = (ushort)V(8);
        regs16[(int)Diag.R.SP] = (ushort)V(9);
        regs16[(int)Diag.R.CS] = (ushort)V(10);
        regs16[(int)Diag.R.DS] = (ushort)V(11);
        regs16[(int)Diag.R.ES] = (ushort)V(12);
        regs16[(int)Diag.R.SS] = (ushort)V(15);
        regs16[(int)Diag.R.IP] = (ushort)V(16);
        regs16[(int)Diag.R.FLAGS] = (ushort)V(17);

        // Moitiés hautes dans l'ordre de cpu_state.regs : EAX ECX EDX EBX ESP EBP ESI EDI.
        ushort[] hi =
        [
            (ushort)(V(2) >> 16), (ushort)(V(4) >> 16), (ushort)(V(5) >> 16), (ushort)(V(3) >> 16),
            (ushort)(V(9) >> 16), (ushort)(V(8) >> 16), (ushort)(V(6) >> 16), (ushort)(V(7) >> 16),
        ];
        var eflagsHi = (ushort)(V(17) >> 16);
        var fs = (ushort)V(13);
        var gs = (ushort)V(14);

        HState got;
        Func<uint, byte> readRam;
        if (csharp)
        {
            _386.Reset386();
            mem.fill_ram(0x90);
            foreach (var (addr, val) in t.InitRam)
                mem.ram[addr & mem.rammask] = val;
            _808x.SetRegs(regs16);
            _808x.SetRegs386(hi, eflagsHi, fs, gs);
            x86.cr0 = V(0);
            x86.cr3 = V(1);
            x86.dr[6] = V(18);
            x86.dr[7] = V(19);
            _386.Step286();
            _386.flags_rebuild();             // le corpus donne EFLAGS en clair
            got = HState.Create();
            _808x.GetState(ref got);
            readRam = a => mem.ram[a & mem.rammask];
        }
        else
        {
            Oracle.h_set_core(Oracle.Core386);
            Oracle.h_reset();
            Oracle.h_fill_ram(0x90);
            foreach (var (addr, val) in t.InitRam)
                Oracle.WriteByte(addr, val);
            Oracle.h_setregs(regs16);
            Oracle.h_setregs386(hi, eflagsHi, fs, gs);
            Oracle.h_setsys386(V(0), V(1), V(18), V(19));
            Oracle.h_step();
            Oracle.h_flags_rebuild();
            Oracle.h_getstate(out got);
            readRam = Oracle.ReadByte;
        }

        // L'état obtenu, dans l'ordre de RG32.
        var seg = got.seg_sel;
        uint[] actual =
        [
            got.cr0, got.cr3,
            got.regs[0], got.regs[3], got.regs[1], got.regs[2], got.regs[6], got.regs[7], got.regs[5], got.regs[4],
            seg[(int)Seg.CS], seg[(int)Seg.DS], seg[(int)Seg.ES], seg[(int)Seg.FS], seg[(int)Seg.GS], seg[(int)Seg.SS],
            got.pc, (uint)got.flags | ((uint)got.eflags << 16), got.dr[6], got.dr[7],
        ];

        for (var i = 0; i < MooFile.RegCount; i++)
        {
            // Un registre absent de l'état final est INCHANGÉ : il doit valoir l'initial.
            var expect = t.Final[i] ?? t.Init[i];
            if (expect is null)
                continue;
            if (i == 16)
                expect = expect.Value - 1;    // eip : voir l'en-tête, le HLT
            var mask = (t.FinalMask[i] ?? 0xFFFFFFFF) & (moo.FileMask[i] ?? 0xFFFFFFFF);
            if (i >= 10 && i <= 15)
                mask &= 0xFFFF;               // sélecteurs : les deux octets hauts ne comptent pas
            if ((actual[i] & mask) != (expect.Value & mask))
                return $"{MooFile.RegNames[i]} attendu {expect.Value:X8}, obtenu {actual[i]:X8}";
        }

        foreach (var (addr, val) in t.FinalRam)
        {
            var b = readRam(addr);
            if (b != val)
                return $"RAM {addr:X6} attendu {val:X2}, obtenu {b:X2}";
        }
        return null;
    }
}
