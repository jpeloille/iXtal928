// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — couche d'interopérabilité, pas de contrepartie C à transcrire.
//
// P/Invoke vers tools/oracle/libixtal26oracle.so : le vrai cœur 8088 de PCem.
// C'est par ici que passe tout le diff différentiel — la seule source de vérité
// pour la comptabilité de cycles, que SingleStepTests ne peut pas valider.

using System.Runtime.InteropServices;

namespace iXtal26.Diff;

/// <summary>Index des registres dans le vecteur de 14 (calqué sur harness.h).</summary>
public enum R
{
    AX = 0, BX, CX, DX, CS, SS, DS, ES, SP, BP, SI, DI, IP, FLAGS, COUNT
}

/// <summary>Index des segments (calqué sur harness.h).</summary>
public enum Seg
{
    CS = 0, DS, ES, SS, FS, GS, COUNT
}

/// <summary>
/// Image de <c>h_state</c> (tools/oracle/harness.h). L'ordre et les types des
/// champs doivent correspondre exactement : un décalage ne lèverait aucune
/// exception, il produirait des comparaisons silencieusement fausses. C'est
/// pourquoi <see cref="Oracle.CheckAbi"/> assène Marshal.SizeOf contre le
/// sizeof que le compilateur C rapporte.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct HState
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public uint[] regs;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public uint[] seg_base;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public ushort[] seg_sel;
    public ushort flags;
    public ushort eflags;
    public ushort prefetchpc;
    public uint pc;
    public uint oldpc;
    public uint eaaddr;
    public int ea_seg_idx;
    public int ssegs;
    public int abrt;

    public int cycles;
    public ulong tsc;
    public ulong tsc_frac;
    public int memcycs;
    public int fetchcycles;
    public int fetchclocks;
    public int nextcyc;
    public int cycdiff;
    public int current_diff;
    public int prefetchw;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public byte[] prefetchqueue;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)] public byte[] _pad;

    public int noint;
    public int inhlt;
    public int takeint;

    public ulong n_inb;
    public ulong n_outb;
    public ulong n_picint;
    public ulong n_picinterrupt;
    public ulong n_timer_process;
    public ulong n_readmembl;
    public ulong n_writemembl;
    public ulong n_readmemwl;
    public ulong n_writememwl;
    public ulong n_fatal;

    public ulong ins;
}

public static class Oracle
{
    private const string Lib = "ixtal26oracle";
    public const int AbiVersion = 1;

    static Oracle()
    {
        // La .so est construite par tools/oracle/Makefile et n'est pas installée
        // sur le système. On la résout par chemin plutôt que d'imposer un
        // LD_LIBRARY_PATH à l'appelant.
        NativeLibrary.SetDllImportResolver(typeof(Oracle).Assembly, (name, asm, path) =>
        {
            if (name != Lib)
                return IntPtr.Zero;

            foreach (var candidate in Candidates())
                if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out var h))
                    return h;

            throw new DllNotFoundException(
                $"libixtal26oracle.so introuvable. Construire l'oracle : (cd tools/oracle && make)\n" +
                $"Cherché dans :\n  {string.Join("\n  ", Candidates())}");
        });
    }

    private static IEnumerable<string> Candidates()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && dir is not null; i++)
        {
            yield return Path.Combine(dir, "libixtal26oracle.so");
            yield return Path.Combine(dir, "tools", "oracle", "libixtal26oracle.so");
            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
        }
    }

    [DllImport(Lib)] public static extern uint h_abi_version();
    [DllImport(Lib)] public static extern uint h_state_size();
    [DllImport(Lib)] public static extern void h_reset();
    [DllImport(Lib)] public static extern void h_load(uint addr, byte[] buf, uint len);
    [DllImport(Lib)] public static extern void h_read(uint addr, byte[] buf, uint len);
    [DllImport(Lib)] public static extern void h_fill_ram(byte value);
    [DllImport(Lib)] public static extern void h_set_cs_ip(ushort cs, ushort ip);
    [DllImport(Lib)] public static extern void h_setregs(ushort[] r);
    [DllImport(Lib)] public static extern void h_getregs(ushort[] r);
    [DllImport(Lib)] public static extern int h_step();
    [DllImport(Lib)] public static extern int h_run(int cycs);
    [DllImport(Lib)] public static extern void h_getstate(out HState s);
    [DllImport(Lib)] public static extern ulong h_ram_hash();

    /// <summary>
    /// Vérifie que le contrat binaire tient. À appeler avant toute utilisation :
    /// une .so périmée ou un champ ajouté d'un seul côté doit échouer ici,
    /// bruyamment, et pas trois heures plus tard sous forme de divergence.
    /// </summary>
    public static void CheckAbi()
    {
        var v = h_abi_version();
        if (v != AbiVersion)
            throw new InvalidOperationException(
                $"Version d'ABI de l'oracle : {v}, attendu {AbiVersion}. Reconstruire tools/oracle.");

        var native = h_state_size();
        var managed = Marshal.SizeOf<HState>();
        if (native != managed)
            throw new InvalidOperationException(
                $"Taille de h_state : {native} octets côté C, {managed} côté C#. " +
                "Les champs ont divergé — toute comparaison serait silencieusement fausse.");
    }

    /// <summary>Lit un octet de la RAM de l'oracle.</summary>
    public static byte ReadByte(uint addr)
    {
        var b = new byte[1];
        h_read(addr, b, 1);
        return b[0];
    }

    /// <summary>Écrit un octet dans la RAM de l'oracle.</summary>
    public static void WriteByte(uint addr, byte value) => h_load(addr, [value], 1);
}
