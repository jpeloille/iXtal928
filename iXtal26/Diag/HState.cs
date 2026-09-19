// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: tools/oracle/harness.h (struct h_state)
// STATUS: host — infrastructure de diagnostic, pas de code PCem transcrit.
//         R2 (parité de lignes) ne s'applique pas.
//
// Le vecteur d'état comparé entre le C# et l'oracle C, après CHAQUE instruction.
//
// Il vit dans l'assembly du cœur, pas dans l'outillage : c'est le cœur qui le
// remplit, et l'outillage qui marshale l'oracle dedans. La dépendance va donc
// outillage -> cœur, jamais l'inverse.
//
// La disposition doit rester binairement identique à h_state côté C.
// Oracle.CheckAbi() confronte Marshal.SizeOf<HState>() à sizeof(h_state) : un
// champ ajouté d'un seul côté ne lèverait rien, il fausserait silencieusement
// toutes les comparaisons. Tout champ ajouté ici doit l'être aussi là-bas, et
// entrer dans la comparaison — un champ non comparé est un champ où la dérive
// se cache.

using System.Runtime.InteropServices;

namespace iXtal26.Diag;

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

[StructLayout(LayoutKind.Sequential)]
public struct HState
{
    // --- état architectural ---
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

    // --- comptabilité de cycles : les statiques de 808x.c ---
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

    // --- état d'interruption ---
    public int noint;
    public int inhlt;
    public int takeint;

    // --- compteurs d'appels des stubs ---
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

    /// <summary>Alloue les tableaux de longueur fixe. Nécessaire avant remplissage
    /// côté C# ; le marshaling depuis le C les alloue lui-même.</summary>
    public static HState Create() => new()
    {
        regs = new uint[8],
        seg_base = new uint[(int)Seg.COUNT],
        seg_sel = new ushort[(int)Seg.COUNT],
        prefetchqueue = new byte[6],
        _pad = new byte[2],
    };
}
