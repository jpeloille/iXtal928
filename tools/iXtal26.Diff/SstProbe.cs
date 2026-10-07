// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de mesure.
//
// SONDE SingleStepTests — porte de M0.
//
// La question, et c'est la seule que cette sonde pose : le cœur 8088 de PCem
// passe-t-il les vecteurs SingleStepTests ? PCem précède ce corpus d'une
// quinzaine d'années et n'a jamais été validé contre lui.
//
//   - Taux élevé  -> le harnais xunit complet vaut ses deux semaines : SST
//                    devient un oracle matériel indépendant, et le critère
//                    pour le C# sera « reproduire la ligne de base à l'identique ».
//   - Taux faible -> SST redescend au rang d'audit ponctuel de là où PCem est
//                    faux, le diff différentiel reste l'unique oracle par
//                    instruction, et les deux semaines repartent vers les
//                    programmes synthétiques.
//
// Volontairement minimale : System.Text.Json直, pas de bake, pas de mmap, pas de
// format pack. On mesure avant de construire.
//
// Ce qui est comparé : registres, flags (masqués), mémoire. PAS les cycles ni la
// file de préfetch — le modèle de temps de PCem lui est propre et ne coïncide pas
// avec les traces SST (cf. TRANSCRIPTION.md).
//
// G13.2 — `--cpu 8086` : le corpus SingleStepTests/8086 (v1, Intel P80C86A-2), même format,
// joué sur le MÊME execx86 avec is8086, des deux côtés (h_set_core(Core8086), Reset8086). Le
// corpus dit son processeur (metadata.json, « cpu ») : jouer l'un sur le cœur de l'autre est
// refusé, au lieu d'écrire en silence une ligne de base qui ne mesure rien.

using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using iXtal26.Cpu;
using iXtal26.Diag;
using iXtal26.Memory;

namespace iXtal26.Diff;

public sealed record SstRegs
{
    public ushort? ax { get; init; }
    public ushort? bx { get; init; }
    public ushort? cx { get; init; }
    public ushort? dx { get; init; }
    public ushort? cs { get; init; }
    public ushort? ss { get; init; }
    public ushort? ds { get; init; }
    public ushort? es { get; init; }
    public ushort? sp { get; init; }
    public ushort? bp { get; init; }
    public ushort? si { get; init; }
    public ushort? di { get; init; }
    public ushort? ip { get; init; }
    public ushort? flags { get; init; }
}

public sealed record SstFrame
{
    public SstRegs regs { get; init; } = new();
    public uint[][]? ram { get; init; }
    public int[]? queue { get; init; }
}

public sealed record SstCase
{
    public string name { get; init; } = "";
    public int[] bytes { get; init; } = [];
    public SstFrame initial { get; init; } = new();
    public SstFrame final { get; init; } = new();
    public JsonElement cycles { get; init; }
    public string? hash { get; init; }
    public int idx { get; init; }

    // Le corpus 8086 v1 garde les noms d'avant la v1.2.0 du 8088 : test_num pour idx, test_hash pour
    // hash (CHANGELOG du 8088). Sans eux, chaque premier échec du 8086 serait étiqueté [0].
    public int? test_num { get; init; }
    public string? test_hash { get; init; }

    /// <summary>Le numéro du cas dans son fichier, sous l'un ou l'autre nom.</summary>
    internal int Index => test_num ?? idx;
}

public static class SstProbe
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    /// <param name="cpu8086">G13.2 — le cœur 8086 (is8086, sur l'Olivetti M24) au lieu du 8088 : le
    /// corpus SingleStepTests/8086.</param>
    /// <param name="attenduPath">G13.2 — la ligne de base à reproduire à l'identique, forme par forme : la porte rend 1
    /// sur le moindre écart, et sur une forme dont les vecteurs manquent.</param>
    public static int Run(string vectorsDir, string[] opcodes, int limit, string? baselinePath = null,
                          bool target808xCs = false, bool cpu8086 = false, string? attenduPath = null)
    {
        // G13.2 — en mode matériel, le C# corrige : la ligne de base de l'oracle (sst-baseline.tsv, ce que PCem fait
        // passer) ne s'écrit pas, celle du mode va dans un fichier à part (sst-baseline-materiel.tsv).
        if (ModeMateriel.Actif && baselinePath is not null && !Path.GetFileName(baselinePath).Contains("materiel"))
        {
            Console.Error.WriteLine($"sst-probe : en mode matériel, la ligne de base va dans un fichier « …-materiel.tsv » ; " +
                                    $"{baselinePath} est celle de l'oracle, refusé.");
            return 2;
        }
        Oracle.CheckAbi();
        Console.WriteLine($"Sonde SST — cible : {(target808xCs ? "cœur C#" : "oracle C")} " +
                          $"(ABI {Oracle.h_abi_version()}, h_state {Oracle.h_state_size()} o)" +
                          (cpu8086 ? ", cœur 8086" : "") + "\n");

        var metadata = Path.Combine(vectorsDir, "metadata.json");
        var cpu = cpu8086 ? "8086" : "8088";
        if (ReadCorpusCpu(metadata) is { } corpusCpu && corpusCpu != cpu)
        {
            Console.Error.WriteLine($"Corpus du {corpusCpu} ({metadata}), sonde sur le cœur {cpu} : refusé" +
                                    (corpusCpu == "8086" ? " — il manque --cpu 8086." : "."));
            return 2;
        }

        var masks = LoadFlagMasks(metadata);
        var grand = (total: 0, passMasked: 0, passRaw: 0);
        var baseline = new List<string>();

        foreach (var op in opcodes)
        {
            var path = Path.Combine(vectorsDir, $"{op}.json.gz");
            if (!File.Exists(path))
            {
                Console.WriteLine($"  {op} : absent ({path})");
                continue;
            }

            var cases = Load(path);
            var n = limit > 0 ? Math.Min(limit, cases.Count) : cases.Count;
            ushort mask = masks.GetValueOrDefault(op, (ushort)0xFFFF);

            int passMasked = 0, passRaw = 0, skippedPrefix = 0, skippedAam0 = 0, run = 0;
            var firstFailures = new List<string>();

            for (var i = 0; i < n; i++)
            {
                // Les fichiers SST d'un opcode contiennent aussi ses formes
                // PRÉFIXÉES. Tant qu'un préfixe n'est pas transcrit, ces cas
                // mesurent son absence et non la justesse de l'opcode : on les
                // compte à part au lieu de les laisser passer pour des échecs.
                // Les overrides de segment sont tombés en M1.2 ; il ne reste que
                // REPNE/REPE, qui attendent rep().
                // Filtre appliqué aux DEUX cibles : sans quoi les dénominateurs diffèrent
                // et la comparaison « le C# reproduit-il la ligne de base ? » n'a pas de sens.
                if (cases[i].bytes.Length > 0 && IsUnimplementedPrefix((byte)cases[i].bytes[0]))
                {
                    skippedPrefix++;
                    continue;
                }
                // G13.2 — AAM 0 (D4 00) tue l'oracle d'un SIGFPE (PB-46 : la division par zéro du C) ; le
                // C# prend l'INT 0 de son chemin R9. Écarté des deux cibles, pour la même raison que REP :
                // ces cas ne se jouent qu'en C# seul (PB-180).
                if (IsAam0(cases[i].bytes))
                {
                    skippedAam0++;
                    continue;
                }
                run++;

                var (okMasked, okRaw, why) = RunCase(cases[i], mask, target808xCs, cpu8086);
                if (okMasked) passMasked++;
                if (okRaw) passRaw++;
                if (!okMasked && firstFailures.Count < 3)
                    firstFailures.Add($"      [{cases[i].Index}] {cases[i].name}: {why}");
            }

            var denom = run > 0 ? run : n;
            var pct = 100.0 * passMasked / denom;
            Console.WriteLine($"  {op} : {passMasked}/{denom} ({pct:F2} %) avec masque 0x{mask:X4}" +
                              (mask != 0xFFFF ? $" ; {passRaw}/{denom} sans masque" : "") +
                              (skippedPrefix > 0 ? $" — {skippedPrefix} cas REP écartés (rep() en M1.9)" : "") +
                              (skippedAam0 > 0 ? $" — {skippedAam0} cas AAM 0 écartés (PB-46)" : ""));
            foreach (var f in firstFailures)
                Console.WriteLine(f);

            baseline.Add(string.Join('\t', op, denom, passMasked, $"0x{mask:X4}",
                firstFailures.Count > 0 ? firstFailures[0].Trim() : ""));

            grand.total += denom;
            grand.passMasked += passMasked;
            grand.passRaw += passRaw;
        }

        if (baselinePath is not null)
        {
            // Ligne de base : ce que l'oracle (donc PCem) fait réellement passer.
            // Le critère pour le C# n'est pas « 100 % de SST » mais « reproduire ce
            // fichier à l'identique ». Généré, jamais édité à la main.
            using var w = new StreamWriter(baselinePath);
            if (ModeMateriel.Actif)
            {
                // G13.2 — la ligne de base du mode matériel : le C# seul, les corrections demandées.
                w.WriteLine($"# Généré par : iXtal26.Diff --hardware-mode {ModeMateriel.ListeDemandee} sst-probe" +
                            $"{(cpu8086 ? " --cpu 8086" : "")} --target csharp --baseline");
                w.WriteLine($"# Mode matériel, {ModeMateriel.ListeDemandee} : le C# seul, sans oracle (R10).");
                w.WriteLine("# Contre la ligne de base de l'oracle, seules les formes que les corrections visent montent.");
            }
            else
            {
                w.WriteLine(cpu8086
                    ? "# Généré par : iXtal26.Diff sst-probe --cpu 8086 --baseline"
                    : "# Généré par : iXtal26.Diff sst-probe --baseline");
                w.WriteLine(cpu8086
                    ? $"# Oracle : PCem v18, 808x.c, is8086 (Olivetti M24) — ABI {Oracle.h_abi_version()}"
                    : $"# Oracle : PCem v18, 808x.c — ABI {Oracle.h_abi_version()}");
                w.WriteLine("# Le C# doit reproduire la colonne 'passe' à l'identique, pas la maximiser.");
            }
            w.WriteLine("forme\tcas\tpasse\tmasque\tpremier_echec");
            foreach (var line in baseline)
                w.WriteLine(line);
            Console.WriteLine($"\nLigne de base écrite : {baselinePath} ({baseline.Count} formes)");
        }

        if (grand.total == 0)
        {
            Console.WriteLine("\nAucun vecteur. Récupérer au moins un fichier v2/<op>.json.gz.");
            return 2;
        }

        var rate = 100.0 * grand.passMasked / grand.total;
        Console.WriteLine($"\nTotal : {grand.passMasked}/{grand.total} ({rate:F2} %)");
        if (attenduPath is not null)
            return Comparer(attenduPath, baseline);
        Console.WriteLine(rate switch
        {
            >= 99.0 => "Verdict : PCem suit le silicium. Le harnais SST complet vaut l'investissement.",
            >= 80.0 => "Verdict : PCem s'écarte par endroits. Harnais utile, mais il faudra figer une\n"
                       + "          ligne de base des cas que PCem échoue, et non viser 100 %.",
            _ => "Verdict : écart important. SST ne peut pas servir d'oracle direct pour une\n"
                 + "          transcription de PCem — le rétrograder en audit ponctuel et s'appuyer\n"
                 + "          sur le diff différentiel."
        });
        return 0;
    }

    /// <summary>G13.2 — la porte : les lignes rendues contre celles du fichier attendu, forme par forme.</summary>
    private static int Comparer(string attenduPath, List<string> rendu)
    {
        if (!File.Exists(attenduPath))
        {
            Console.WriteLine($"\n{attenduPath} introuvable.");
            return 2;
        }
        var attendu = File.ReadLines(attenduPath).Where(l => l.Length > 0 && !l.StartsWith('#') && !l.StartsWith("forme\t"))
                          .ToDictionary(l => l.Split('\t')[0]);
        var obtenu = rendu.ToDictionary(l => l.Split('\t')[0]);
        var ecarts = 0;
        foreach (var (forme, ligne) in attendu)
        {
            if (!obtenu.TryGetValue(forme, out var o))
            {
                ecarts++;
                Console.WriteLine($"  {forme} : attendue, pas jouée (vecteurs absents ?)");
            }
            else if (o != ligne)
            {
                ecarts++;
                Console.WriteLine($"  {forme} : attendu « {ligne} »\n      {new string(' ', forme.Length)}   rendu « {o} »");
            }
        }
        foreach (var forme in obtenu.Keys.Where(f => !attendu.ContainsKey(f)))
        {
            ecarts++;
            Console.WriteLine($"  {forme} : jouée, absente de {attenduPath}");
        }
        Console.WriteLine(ecarts == 0
            ? $"\nVert : {Path.GetFileName(attenduPath)} reproduite à l'identique, {attendu.Count} formes."
            : $"\n{ecarts} écart(s) contre {Path.GetFileName(attenduPath)}.");
        return ecarts == 0 ? 0 : 1;
    }

    private static (bool okMasked, bool okRaw, string why) RunCase(SstCase c, ushort mask, bool csharp,
                                                                   bool cpu8086)
    {
        if (csharp)
                return RunCaseCsharp(c, mask, cpu8086);

        // Le cœur, posé avant h_reset : c'est h_reset qui en tire la machine (le 5150 ou la M24).
        Oracle.h_set_core(cpu8086 ? Oracle.Core8086 : Oracle.Core8088);
        Oracle.h_reset();

        // SST : « all bytes fetched after the initial instruction bytes are set
        // to 0x90 ». Sans ce remplissage, tout opérande lu hors des octets listés
        // dans initial.ram (un diviseur, une source de chaîne) vaudrait 0x00 et on
        // mesurerait les divergences du harnais, pas celles de PCem.
        Oracle.h_fill_ram(0x90);

        // La RAM initiale contient déjà les octets d'instruction à cs:ip, donc on
        // ignore `queue` : il est redondant avec elle, et FETCHCLEAR reconstruit la
        // file depuis la mémoire.
        if (c.initial.ram is not null)
            foreach (var pair in c.initial.ram)
                Oracle.WriteByte(pair[0], (byte)pair[1]);

        var init = ToVector(c.initial.regs, null);
        Oracle.h_setregs(init);

        Oracle.h_step();

        var got = new ushort[(int)R.COUNT];
        Oracle.h_getregs(got);

        // `final` est un delta : seuls les registres modifiés y figurent.
        var want = ToVector(c.final.regs, init);

        for (var i = 0; i < (int)R.COUNT; i++)
        {
            if (i == (int)R.FLAGS) continue;
            if (got[i] != want[i])
                return (false, false, $"{(R)i} = 0x{got[i]:X4}, attendu 0x{want[i]:X4}");
        }

        var okRawFlags = got[(int)R.FLAGS] == want[(int)R.FLAGS];
        var okMaskedFlags = (got[(int)R.FLAGS] & mask) == (want[(int)R.FLAGS] & mask);

        if (c.final.ram is not null)
            foreach (var pair in c.final.ram)
            {
                var actual = Oracle.ReadByte(pair[0]);
                if (actual != (byte)pair[1])
                    return (false, false,
                        $"mem[0x{pair[0]:X5}] = 0x{actual:X2}, attendu 0x{pair[1]:X2}");
            }

        var why = okMaskedFlags
            ? ""
            : $"flags = 0x{got[(int)R.FLAGS]:X4}, attendu 0x{want[(int)R.FLAGS]:X4} " +
              $"(diff masqué 0x{(got[(int)R.FLAGS] ^ want[(int)R.FLAGS]) & mask:X4})";

        return (okMaskedFlags, okMaskedFlags && okRawFlags, why);
    }

    /// <summary>
    /// Même cas SST, joué par les DEUX cœurs, état complet comparé. Sert quand
    /// le fuzzer et la sonde SST se contredisent : si le C# diverge de l'oracle
    /// ici mais pas sous le fuzzer, c'est la MISE EN PLACE qui diffère, pas le
    /// cœur.
    /// </summary>
    internal static string? DiffCase(SstCase c, bool cpu8086 = false)
    {
        var a = HState.Create();
        var b = HState.Create();
        var init = ToVector(c.initial.regs, null);

        Oracle.h_set_core(cpu8086 ? Oracle.Core8086 : Oracle.Core8088);
        Oracle.h_reset();
        Oracle.h_fill_ram(0x90);
        if (c.initial.ram is not null)
                foreach (var pair in c.initial.ram)
                        Oracle.WriteByte(pair[0], (byte)pair[1]);
        Oracle.h_setregs(init);
        var cycC = Oracle.h_step();
        Oracle.h_getstate(out a);

        if (cpu8086)
                _808x.Reset8086();
        else
                _808x.Reset();
        mem.fill_ram(0x90);
        if (c.initial.ram is not null)
                foreach (var pair in c.initial.ram)
                        mem.ram[pair[0] & mem.rammask] = (byte)pair[1];
        _808x.SetRegs(init);
        var cycS = _808x.Step();
        _808x.GetState(ref b);

        return Fuzzer.CompareStates(a, b, cycC, cycS);
    }

    /// <summary>
    /// Même cas, exécuté par le cœur C# au lieu de l'oracle. C'est LE critère du
    /// plan : le C# ne doit pas « maximiser SST », il doit reproduire à
    /// l'identique la colonne `passe` de sst-baseline.tsv. Un écart dans un sens
    /// comme dans l'autre est une divergence de transcription.
    /// </summary>
    private static (bool okMasked, bool okRaw, string why) RunCaseCsharp(SstCase c, ushort mask, bool cpu8086)
    {
        if (cpu8086)
                _808x.Reset8086();
        else
                _808x.Reset();
        mem.fill_ram(0x90);

        if (c.initial.ram is not null)
                foreach (var pair in c.initial.ram)
                        mem.ram[pair[0] & mem.rammask] = (byte)pair[1];

        var init = ToVector(c.initial.regs, null);
        _808x.SetRegs(init);

        _808x.Step();

        var got = new ushort[(int)R.COUNT];
        _808x.GetRegs(got);

        var want = ToVector(c.final.regs, init);

        for (var i = 0; i < (int)R.COUNT; i++)
        {
                if (i == (int)R.FLAGS) continue;
                if (got[i] != want[i])
                        return (false, false, $"{(R)i} = 0x{got[i]:X4}, attendu 0x{want[i]:X4}");
        }

        var okRawFlags = got[(int)R.FLAGS] == want[(int)R.FLAGS];
        var okMaskedFlags = (got[(int)R.FLAGS] & mask) == (want[(int)R.FLAGS] & mask);

        if (c.final.ram is not null)
                foreach (var pair in c.final.ram)
                {
                        var actual = mem.ram[pair[0] & mem.rammask];
                        if (actual != (byte)pair[1])
                                return (false, false,
                                    $"mem[0x{pair[0]:X5}] = 0x{actual:X2}, attendu 0x{pair[1]:X2}");
                }

        // Même libellé que la branche oracle, suffixe « diff masqué » compris : le
        // critère du plan est que les deux lignes de base soient identiques OCTET
        // POUR OCTET, et premier_echec est une de leurs colonnes. Un suffixe absent
        // d'un seul côté fait diverger 13 formes sans qu'aucun cœur ne diverge.
        var why = okMaskedFlags
            ? ""
            : $"flags = 0x{got[(int)R.FLAGS]:X4}, attendu 0x{want[(int)R.FLAGS]:X4} " +
              $"(diff masqué 0x{(got[(int)R.FLAGS] ^ want[(int)R.FLAGS]) & mask:X4})";

        return (okMaskedFlags, okMaskedFlags && okRawFlags, why);
    }

    /// <summary>Préfixes que le cœur C# ne transcrit pas encore. Les overrides
    /// de segment (26/2E/36/3E) sont transcrits depuis M1.2 ; restent REPNE et
    /// REPE, qui dépendent de rep() — M1.9.</summary>
    private static bool IsUnimplementedPrefix(byte b) => b is 0xF2 or 0xF3;

    /// <summary>AAM d'immédiat nul, derrière ses préfixes éventuels (segment, LOCK, REP).</summary>
    internal static bool IsAam0(int[] bytes)
    {
        var i = 0;
        while (i < bytes.Length && bytes[i] is 0x26 or 0x2E or 0x36 or 0x3E or 0xF0 or 0xF1 or 0xF2 or 0xF3)
            i++;
        return i + 1 < bytes.Length && bytes[i] == 0xD4 && bytes[i + 1] == 0;
    }

    /// <summary>Convertit un objet regs SST en vecteur de 14, en retombant sur
    /// <paramref name="base_"/> pour les champs absents (final est un delta).</summary>
    private static ushort[] ToVector(SstRegs r, ushort[]? base_)
    {
        var v = base_ is null ? new ushort[(int)R.COUNT] : (ushort[])base_.Clone();
        if (r.ax.HasValue) v[(int)R.AX] = r.ax.Value;
        if (r.bx.HasValue) v[(int)R.BX] = r.bx.Value;
        if (r.cx.HasValue) v[(int)R.CX] = r.cx.Value;
        if (r.dx.HasValue) v[(int)R.DX] = r.dx.Value;
        if (r.cs.HasValue) v[(int)R.CS] = r.cs.Value;
        if (r.ss.HasValue) v[(int)R.SS] = r.ss.Value;
        if (r.ds.HasValue) v[(int)R.DS] = r.ds.Value;
        if (r.es.HasValue) v[(int)R.ES] = r.es.Value;
        if (r.sp.HasValue) v[(int)R.SP] = r.sp.Value;
        if (r.bp.HasValue) v[(int)R.BP] = r.bp.Value;
        if (r.si.HasValue) v[(int)R.SI] = r.si.Value;
        if (r.di.HasValue) v[(int)R.DI] = r.di.Value;
        if (r.ip.HasValue) v[(int)R.IP] = r.ip.Value;
        if (r.flags.HasValue) v[(int)R.FLAGS] = r.flags.Value;
        return v;
    }

    internal static List<SstCase> LoadPublic(string gzPath) => Load(gzPath);

    /// <summary>Le processeur que le corpus déclare (metadata.json, « cpu » : 8088 ou 8086), ou null.</summary>
    private static string? ReadCorpusCpu(string metadataPath)
    {
        if (!File.Exists(metadataPath))
            return null;
        using var doc = JsonDocument.Parse(File.ReadAllText(metadataPath));
        return doc.RootElement.TryGetProperty("cpu", out var c) && c.ValueKind == JsonValueKind.String
            ? c.GetString()
            : null;
    }

    private static List<SstCase> Load(string gzPath)
    {
        using var fs = File.OpenRead(gzPath);
        using var gz = new GZipStream(fs, CompressionMode.Decompress);
        using var ms = new MemoryStream();
        gz.CopyTo(ms);
        ms.Position = 0;
        return JsonSerializer.Deserialize<List<SstCase>>(ms, JsonOpts) ?? [];
    }

    /// <summary>
    /// Masques de bits de flags indéfinis, depuis metadata.json. Sans eux, les
    /// flags eager de PCem (znptable8/16) divergeront du silicium sur tout
    /// l'espace décalages + MUL/DIV, et on chassera des fantômes.
    /// </summary>
    private static Dictionary<string, ushort> LoadFlagMasks(string metadataPath)
    {
        var result = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(metadataPath))
            return result;

        using var doc = JsonDocument.Parse(File.ReadAllText(metadataPath));
        if (!doc.RootElement.TryGetProperty("opcodes", out var ops))
            return result;

        foreach (var op in ops.EnumerateObject())
        {
            if (op.Value.TryGetProperty("flags-mask", out var m))
                result[op.Name] = (ushort)m.GetUInt32();

            // Opcodes de groupe : le masque vit sous reg/<n>, et le fichier de
            // vecteurs correspondant s'appelle "D0.6.json.gz". On indexe sous la
            // même clé que le nom de fichier.
            if (op.Value.TryGetProperty("reg", out var regs))
                foreach (var sub in regs.EnumerateObject())
                    if (sub.Value.TryGetProperty("flags-mask", out var sm))
                        result[$"{op.Name}.{sub.Name}"] = (ushort)sm.GetUInt32();
        }

        return result;
    }
}
