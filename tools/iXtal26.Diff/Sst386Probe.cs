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
//   - les cas dont la RAM dépasse la carte plate du harnais (16 Mo pour le 386) : ils sont
//     comptés « hors carte » et non joués, pas comptés comme réussis.
//
// Un pas = une instruction : exec386 exécute préfixes et instruction dans la même
// itération, exception livrée comprise. Le corpus capture l'état APRÈS le HLT qui clôt
// chaque cas, donc eip au-delà de lui ; PCem, lui, laisse pc SUR le HLT (il boucle
// dessus). Le HLT n'est donc pas exécuté, et l'eip attendu est pris moins un — mesuré :
// tous les cas rendaient eip attendu − 1, NOP compris.
//
// G13.2 — LES DRAPEAUX INDÉFINIS. Le corpus les donne de deux façons, appliquées ensemble (un
// drapeau n'est comparé que si aucune des deux ne le dit indéfini) :
//   - dans le fichier MOO, un masque RM32 (RMSK) de niveau fichier ou de l'état final, que la
//     sonde lisait déjà ; au 386, il manque aux 60 formes de BT*, BSF, BSR, SHLD, SHRD et de
//     l'IMUL de 0F AF, et 80386.csv le donne à l'identique partout ailleurs ;
//   - dans la table des opcodes : la colonne `f_umask` de 80386.csv (README du 80386,
//     « 80386.csv ») ; le « flags-mask » de metadata.json pour le 286, comme pour le 8088. Au
//     286, les deux sources se contredisent sur 55 formes (OR, AND, XOR, TEST, IMUL, les
//     décalages) : leur union, faute de mieux.
// Le masque vaut pour EFLAGS, et pour les drapeaux qu'une exception empile : EXCP donne leur
// adresse « to assist in masking the flag value to handle undefined flags in instructions
// such as DIV » (README du 80386 et du 80286, « Exceptions »).
//
// G13.2 — LE 286 (`sst286-probe`) : le corpus SingleStepTests/80286 (Harris 80C286, mode réel,
// MOO à registres 16 bits), joué sur le cœur 286 des deux côtés (h_set_core(Core286),
// Reset286), sur la carte de 16 Mo depuis G13.5 (h_set_carte286, _808x.Carte286.Ko) : le mode réel du 286 atteint
// 10FFEFh, et 17 % du corpus tombait hors de la carte de 1 Mo des autres portes du 286.

using System.Text.Json;
using iXtal26.Cpu;
using iXtal26.Diag;
using iXtal26.Memory;

namespace iXtal26.Diff;

internal static class Sst386Probe
{
    // La carte du cœur 386 fait 16 Mo depuis D2 : tout le corpus (bus 24 bits) y tient. Celle du cœur 286 aussi, pour
    // la sonde seule, depuis G13.5 (FlatMap286 sous Carte286.Ko, h_flat_map sous h_carte286).
    private const uint CarteMax = 0x1000000;

    internal static int Run(string vectors, List<string> forms, int limit, bool csharp, string? baseline,
                            bool cpu286 = false, string? attendu = null)
    {
        Oracle.CheckAbi();
        var sub = cpu286 ? "v1_real_mode" : "v1_ex_real_mode";
        var dir = Path.Combine(vectors, sub);
        var fetch = cpu286 ? "./tools/fetch-sst286.sh" : "./tools/fetch-sst386.sh";
        if (!Directory.Exists(dir))
        {
            Console.Error.WriteLine($"{dir} absent : {fetch} --all (ou nommer des formes).");
            return 2;
        }

        // Le masque des drapeaux indéfinis, forme par forme. Sans sa table, la sonde comparerait des
        // drapeaux que le corpus dit lui-même indéfinis : refusé, plutôt qu'une ligne de base fausse.
        var table = cpu286 ? Path.Combine(dir, "metadata.json") : Path.Combine(vectors, "80386.csv");
        if (!File.Exists(table))
        {
            Console.Error.WriteLine($"{table} absent : {fetch} le récupère avec les vecteurs.");
            return 2;
        }
        var umasks = cpu286 ? LoadFlagsMask286(table) : LoadUmask386(table);

        var revoked = new HashSet<string>(
            File.Exists(Path.Combine(vectors, "revocation_list.txt"))
                ? File.ReadAllLines(Path.Combine(vectors, "revocation_list.txt"))
                      .Select(l => l.Trim().ToLowerInvariant()).Where(l => l.Length > 0 && l[0] != '#')
                : []);

        var files = forms.Count > 0
            ? forms.Select(f => Path.Combine(dir, f + ".MOO.gz")).ToList()
            : Directory.GetFiles(dir, "*.MOO.gz").OrderBy(f => f, StringComparer.Ordinal).ToList();

        var cpu = cpu286 ? "80286 (mode réel), cœur 286" : "80386 (mode réel)";
        Console.WriteLine($"Sonde SST {cpu} — cible {(csharp ? "C#" : "oracle")}, " +
                          $"{files.Count} forme(s), {revoked.Count} empreinte(s) révoquée(s)\n");

        var carteMax = CarteMax;
        if (cpu286)
        {
            _808x.Carte286.Ko = 16384;
            Oracle.h_set_carte286(1);
        }
        try
        {
            return Jouer(files, cpu286, csharp, limit, revoked, umasks, carteMax, baseline, attendu);
        }
        finally
        {
            _808x.Carte286.Ko = 1024;
            Oracle.h_set_carte286(0);
        }
    }

    private static int Jouer(List<string> files, bool cpu286, bool csharp, int limit, HashSet<string> revoked,
                             Dictionary<string, ushort> umasks, uint carteMax, string? baseline, string? attendu)
    {
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
            if (moo.Regs16 != cpu286)
            {
                Console.Error.WriteLine($"{path} : registres de {(moo.Regs16 ? 16 : 32)} bits, " +
                                        $"inattendus pour le corpus du {(cpu286 ? "286" : "386")}.");
                return 2;
            }
            // Le mot bas d'EFLAGS seul : la table ne masque que les drapeaux du 8086.
            var flagsMask = 0xFFFF0000u | umasks.GetValueOrDefault(OpcodeOf(form), (ushort)0xFFFF);
            int pass = 0, played = 0, horsCarte = 0;
            var firstWhy = "";
            foreach (var t in moo.Tests)
            {
                if (limit > 0 && played >= limit)
                    break;
                if (revoked.Contains(t.Hash))
                    continue;
                if (t.InitRam.Any(e => e.addr >= carteMax) || t.FinalRam.Any(e => e.addr >= carteMax))
                {
                    horsCarte++;
                    continue;
                }

                played++;
                string? why;
                try
                {
                    why = RunCase(moo, t, csharp, cpu286, flagsMask);
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
            var commande = cpu286 ? "sst286-probe" : "sst386-probe";
            var source = cpu286 ? "flags-mask de metadata.json, RMSK" : "f_umask de 80386.csv, RM32";
            w.WriteLine($"# Généré par : iXtal26.Diff {commande} --baseline " +
                        $"(drapeaux indéfinis masqués : {source})");
            w.WriteLine($"# Cible : {(csharp ? "C#" : "oracle")} — ABI {Oracle.h_abi_version()}" +
                        (cpu286 ? ", cœur 286" : ""));
            w.WriteLine("# Le C# doit reproduire la colonne 'passe' de l'oracle à l'identique, pas la maximiser.");
            w.WriteLine("forme\tpasse\tjoues\thors_carte");
            foreach (var r in rows)
                w.WriteLine($"{r.form}\t{r.pass}\t{r.played}\t{r.horsCarte}");
            Console.WriteLine($"\nLigne de base écrite : {baseline}");
        }

        if (attendu is not null)
            return SstProbe.Comparer(attendu, rows.Select(r => $"{r.form}\t{r.pass}\t{r.played}\t{r.horsCarte}").ToList());

        var total = rows.Sum(r => r.played);
        var ok = rows.Sum(r => r.pass);
        Console.WriteLine($"\n{ok} / {total} cas réussis sur {rows.Count} forme(s) ; " +
                          $"{rows.Sum(r => r.horsCarte)} hors carte. Comparer forme par forme, pas ce total.");
        return 0;
    }

    /// <summary>Rend null si l'état final concorde avec le silicium, sinon la raison.</summary>
    private static string? RunCase(MooFile moo, MooTest t, bool csharp, bool cpu286, uint flagsMask)
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
            if (cpu286)
                _386.Reset286();
            else
                _386.Reset386();
            mem.fill_ram(0x90);
            foreach (var (addr, val) in t.InitRam)
                mem.ram[addr & mem.rammask] = val;
            _808x.SetRegs(regs16);
            if (!cpu286)
            {
                _808x.SetRegs386(hi, eflagsHi, fs, gs);
                x86.cr0 = V(0);
                x86.cr3 = V(1);
                x86.dr[6] = V(18);
                x86.dr[7] = V(19);
            }
            _386.Step286();
            _386.flags_rebuild();             // le corpus donne EFLAGS en clair
            got = HState.Create();
            _808x.GetState(ref got);
            readRam = a => mem.ram[a & mem.rammask];
        }
        else
        {
            Oracle.h_set_core(cpu286 ? Oracle.Core286 : Oracle.Core386);
            Oracle.h_reset();
            Oracle.h_fill_ram(0x90);
            foreach (var (addr, val) in t.InitRam)
                Oracle.WriteByte(addr, val);
            Oracle.h_setregs(regs16);
            if (!cpu286)
            {
                Oracle.h_setregs386(hi, eflagsHi, fs, gs);
                Oracle.h_setsys386(V(0), V(1), V(18), V(19));
            }
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
            if (i == 17)
                mask &= flagsMask;            // les drapeaux indéfinis de la table : voir l'en-tête
            if ((i >= 10 && i <= 15) || moo.Regs16)
                mask &= 0xFFFF;               // sélecteurs, et registres REGS : 16 bits seulement
            if ((actual[i] & mask) != (expect.Value & mask))
                return $"{MooFile.RegNames[i]} attendu {expect.Value:X8}, obtenu {actual[i]:X8}";
        }

        // Les drapeaux qu'une exception a empilés, sous le même masque qu'EFLAGS.
        var pile = (t.FinalMask[17] ?? 0xFFFFFFFF) & (moo.FileMask[17] ?? 0xFFFFFFFF) & flagsMask;
        foreach (var (addr, val) in t.FinalRam)
        {
            var m = t.Exception is null ? 0xFF
                : addr == t.ExceptionFlagAddr ? (int)(pile & 0xFF)
                : addr == t.ExceptionFlagAddr + 1 ? (int)((pile >> 8) & 0xFF)
                : 0xFF;
            var b = readRam(addr);
            if ((b & m) != (val & m))
                return $"RAM {addr:X6} attendu {val:X2}, obtenu {b:X2}";
        }
        return null;
    }

    /// <summary>La forme sans ses préfixes de taille : « 6766C1.4 » donne « C1.4 », la clé des tables
    /// d'opcodes (80386.csv : op et ex ; metadata.json : l'opcode et son reg).</summary>
    private static string OpcodeOf(string form)
    {
        var s = form;
        while (s.Length > 2 && s[0] == '6' && s[1] is '6' or '7')
            s = s[2..];
        return s;
    }

    /// <summary>La colonne f_umask de 80386.csv, par « op » ou « op.ex ». Le fichier a des champs
    /// entre guillemets qui contiennent des virgules (« "JNAE,JC" ») : lu champ par champ.</summary>
    private static Dictionary<string, ushort> LoadUmask386(string csvPath)
    {
        var result = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
        var lines = File.ReadAllLines(csvPath);
        var head = SplitCsv(lines[0]);
        int iOp = head.IndexOf("op"), iEx = head.IndexOf("ex"), iMask = head.IndexOf("f_umask");
        if (iOp < 0 || iEx < 0 || iMask < 0)
            throw new InvalidDataException($"{csvPath} : colonnes op, ex ou f_umask absentes");
        foreach (var line in lines.Skip(1))
        {
            var f = SplitCsv(line);
            if (f.Count <= iMask || f[iMask].Trim().Length == 0)
                continue;
            var key = f[iOp].Trim() + (f[iEx].Trim().Length > 0 ? "." + f[iEx].Trim() : "");
            result[key] = Convert.ToUInt16(f[iMask].Trim(), 16);
        }
        return result;
    }

    private static List<string> SplitCsv(string line)
    {
        var fields = new List<string>();
        var cur = new System.Text.StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                    cur.Append(line[++i]);
                else if (c == '"')
                    quoted = false;
                else
                    cur.Append(c);
            }
            else if (c == '"')
                quoted = true;
            else if (c == ',')
            {
                fields.Add(cur.ToString());
                cur.Clear();
            }
            else
                cur.Append(c);
        }
        fields.Add(cur.ToString());
        return fields;
    }

    /// <summary>Le « flags-mask » de metadata.json (286), par opcode ou « opcode.reg » — la forme du
    /// 8088 (SstProbe.LoadFlagMasks).</summary>
    private static Dictionary<string, ushort> LoadFlagsMask286(string metadataPath)
    {
        var result = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
        using var doc = JsonDocument.Parse(File.ReadAllText(metadataPath));
        if (!doc.RootElement.TryGetProperty("opcodes", out var ops))
            throw new InvalidDataException($"{metadataPath} : pas de clé « opcodes »");
        foreach (var op in ops.EnumerateObject())
        {
            if (op.Value.TryGetProperty("flags-mask", out var m))
                result[op.Name] = (ushort)m.GetUInt32();
            if (op.Value.TryGetProperty("reg", out var regs))
                foreach (var sub in regs.EnumerateObject())
                    if (sub.Value.TryGetProperty("flags-mask", out var sm))
                        result[$"{op.Name}.{sub.Name}"] = (ushort)sm.GetUInt32();
        }
        return result;
    }
}
