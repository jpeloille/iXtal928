// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: -
// STATUS: host — outillage, pas de code PCem transcrit.
//
// G2, D0.5 — le lecteur du format MOO de SingleStepTests/80386.
//
// Spécification : dbalsom/moo, doc/moo_format_v1.md (v1.1). Un fichier est un chunk
// `MOO ` suivi de chunks `TEST`, chacun portant ses sous-chunks ; tout est petit-boutiste
// et chaque chunk se saute par sa longueur. On suit la règle de la spécification : un
// chunk inconnu se SAUTE, on ne suppose jamais que le suivant le touche.
//
// Ne sont lus que ce que la sonde compare : registres 32 bits (RG32) et leurs masques
// (RM32, au niveau du fichier ou de l'état final), RAM, octets, exception, empreinte.
// Les cycles (CYCL) sont ignorés, et c'est voulu : ce sont ceux d'un 386EX à bus 16
// bits, SMM compris — ils ne valent pas pour le modèle de temps de PCem.

using System.IO.Compression;

namespace iXtal26.Diff;

internal sealed class MooTest
{
    internal int Index;
    internal string Name = "";
    internal byte[] Bytes = [];
    internal readonly uint?[] Init = new uint?[MooFile.RegCount];
    internal readonly uint?[] Final = new uint?[MooFile.RegCount];
    internal readonly uint?[] FinalMask = new uint?[MooFile.RegCount];
    internal List<(uint addr, byte val)> InitRam = [];
    internal List<(uint addr, byte val)> FinalRam = [];
    internal int? Exception;
    internal uint ExceptionFlagAddr;
    internal string Hash = "";
}

internal sealed class MooFile
{
    // L'ordre des bits de RG32 et RM32 (moo_format_v1.md, « RG32 »).
    internal const int RegCount = 20;
    internal static readonly string[] RegNames =
    [
        "cr0", "cr3", "eax", "ebx", "ecx", "edx", "esi", "edi", "ebp", "esp",
        "cs", "ds", "es", "fs", "gs", "ss", "eip", "eflags", "dr6", "dr7",
    ];

    internal string CpuId = "";
    internal string Mnemonic = "";
    internal int CpuMode;
    internal readonly uint?[] FileMask = new uint?[RegCount];
    internal readonly List<MooTest> Tests = [];

    internal static MooFile Load(string path)
    {
        byte[] data;
        using (var fs = File.OpenRead(path))
        using (Stream s = path.EndsWith(".gz", StringComparison.Ordinal) ? new GZipStream(fs, CompressionMode.Decompress) : fs)
        using (var ms = new MemoryStream())
        {
            s.CopyTo(ms);
            data = ms.ToArray();
        }

        var f = new MooFile();
        var pos = 0;
        while (pos + 8 <= data.Length)
        {
            var (id, len, body) = Chunk(data, ref pos);
            switch (id)
            {
                case "MOO ":
                    f.CpuId = System.Text.Encoding.ASCII.GetString(data, body + 8, 4);
                    break;
                case "META":
                    f.Mnemonic = System.Text.Encoding.ASCII.GetString(data, body + 7, 8).TrimEnd();
                    f.CpuMode = data[body + 27];
                    break;
                case "RM32":
                    ReadRegs(data, body, f.FileMask);
                    break;
                case "TEST":
                    f.Tests.Add(ReadTest(data, body, len));
                    break;
            }
        }
        return f;
    }

    private static MooTest ReadTest(byte[] d, int start, int len)
    {
        var t = new MooTest { Index = (int)U32(d, start) };
        var pos = start + 4;
        var end = start + len;
        while (pos + 8 <= end)
        {
            var (id, clen, body) = Chunk(d, ref pos);
            switch (id)
            {
                case "NAME":
                    t.Name = System.Text.Encoding.ASCII.GetString(d, body + 4, (int)U32(d, body));
                    break;
                case "BYTS":
                    t.Bytes = d.AsSpan(body + 4, (int)U32(d, body)).ToArray();
                    break;
                case "INIT":
                    ReadState(d, body, clen, t.Init, null, t.InitRam);
                    break;
                case "FINA":
                    ReadState(d, body, clen, t.Final, t.FinalMask, t.FinalRam);
                    break;
                case "EXCP":
                    t.Exception = d[body];
                    t.ExceptionFlagAddr = U32(d, body + 1);
                    break;
                case "HASH":
                    t.Hash = Convert.ToHexString(d, body, 20).ToLowerInvariant();
                    break;
            }
        }
        return t;
    }

    private static void ReadState(byte[] d, int start, int len, uint?[] regs, uint?[]? mask,
                                  List<(uint, byte)> ram)
    {
        var pos = start;
        var end = start + len;
        while (pos + 8 <= end)
        {
            var (id, _, body) = Chunk(d, ref pos);
            switch (id)
            {
                case "RG32":
                    ReadRegs(d, body, regs);
                    break;
                case "RM32" when mask is not null:
                    ReadRegs(d, body, mask);
                    break;
                case "RAM ":
                    var n = (int)U32(d, body);
                    for (var i = 0; i < n; i++)
                        ram.Add((U32(d, body + 4 + i * 5), d[body + 8 + i * 5]));
                    break;
                case "REGS":
                    throw new InvalidDataException("REGS (16 bits) dans un fichier 386 : format inattendu");
            }
        }
    }

    private static void ReadRegs(byte[] d, int body, uint?[] into)
    {
        var bits = U32(d, body);
        var p = body + 4;
        for (var i = 0; i < RegCount; i++)
            if ((bits & (1u << i)) != 0)
            {
                into[i] = U32(d, p);
                p += 4;
            }
    }

    private static (string id, int len, int body) Chunk(byte[] d, ref int pos)
    {
        var id = System.Text.Encoding.ASCII.GetString(d, pos, 4);
        var len = (int)U32(d, pos + 4);
        var body = pos + 8;
        pos = body + len;
        return (id, len, body);
    }

    private static uint U32(byte[] d, int p) => BitConverter.ToUInt32(d, p);
}
