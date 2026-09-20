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
}

public static class SstProbe
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public static int Run(string vectorsDir, string[] opcodes, int limit, string? baselinePath = null,
                          bool target808xCs = false)
    {
        Oracle.CheckAbi();
        Console.WriteLine($"Sonde SST — cible : {(target808xCs ? "cœur C#" : "oracle C")} " +
                          $"(ABI {Oracle.h_abi_version()}, h_state {Oracle.h_state_size()} o)\n");

        var masks = LoadFlagMasks(Path.Combine(vectorsDir, "metadata.json"));
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

            int passMasked = 0, passRaw = 0, skippedPrefix = 0, run = 0;
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
                run++;

                var (okMasked, okRaw, why) = RunCase(cases[i], mask, target808xCs);
                if (okMasked) passMasked++;
                if (okRaw) passRaw++;
                if (!okMasked && firstFailures.Count < 3)
                    firstFailures.Add($"      [{cases[i].idx}] {cases[i].name}: {why}");
            }

            var denom = run > 0 ? run : n;
            var pct = 100.0 * passMasked / denom;
            Console.WriteLine($"  {op} : {passMasked}/{denom} ({pct:F2} %) avec masque 0x{mask:X4}" +
                              (mask != 0xFFFF ? $" ; {passRaw}/{denom} sans masque" : "") +
                              (skippedPrefix > 0 ? $" — {skippedPrefix} cas REP écartés (rep() en M1.9)" : ""));
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
            w.WriteLine("# Généré par : iXtal26.Diff sst-probe --baseline");
            w.WriteLine($"# Oracle : PCem v18, 808x.c — ABI {Oracle.h_abi_version()}");
            w.WriteLine("# Le C# doit reproduire la colonne 'passe' à l'identique, pas la maximiser.");
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

    private static (bool okMasked, bool okRaw, string why) RunCase(SstCase c, ushort mask, bool csharp)
    {
        if (csharp)
                return RunCaseCsharp(c, mask);

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
    internal static string? DiffCase(SstCase c)
    {
        var a = HState.Create();
        var b = HState.Create();
        var init = ToVector(c.initial.regs, null);

        Oracle.h_reset();
        Oracle.h_fill_ram(0x90);
        if (c.initial.ram is not null)
                foreach (var pair in c.initial.ram)
                        Oracle.WriteByte(pair[0], (byte)pair[1]);
        Oracle.h_setregs(init);
        var cycC = Oracle.h_step();
        Oracle.h_getstate(out a);

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
    private static (bool okMasked, bool okRaw, string why) RunCaseCsharp(SstCase c, ushort mask)
    {
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
