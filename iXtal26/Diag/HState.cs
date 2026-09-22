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

/// <summary>Index des quatre descripteurs système (calqué sur harness.h).
/// Séparés des segments : gdt et idt n'ont pas de sélecteur, aucun des quatre n'est
/// chargeable par un préfixe, et ea_seg ne peut pas les viser.</summary>
public enum Sys
{
    GDT = 0, LDT, IDT, TR, COUNT
}

[StructLayout(LayoutKind.Sequential)]
public struct HState
{
    // --- état architectural ---
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public uint[] regs;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public uint[] seg_base;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public ushort[] seg_sel;

    // Le CACHE DESCRIPTEUR des six segments, ajouté au jalon 286. Les deux côtés le
    // portaient déjà — x86seg a huit champs — mais le vecteur n'en comparait que
    // `base` et `seg`. Sur un 8088 les six autres ne bougent jamais ; en mode protégé
    // ils portent tout. Ils entrent AVANT d'être nécessaires, pour vérifier le câblage
    // pendant qu'il est encore trivial.
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public uint[] seg_limit;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public uint[] seg_limit_raw;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public uint[] seg_limit_low;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public uint[] seg_limit_high;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public int[] seg_checked;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public byte[] seg_access;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public byte[] seg_access2;

    // Les QUATRE DESCRIPTEURS SYSTÈME et les registres de contrôle, ajoutés juste après
    // le cache descripteur et pour la même raison : sur un 8088 ils sont constants, donc
    // le câblage se vérifie pendant qu'il est encore trivial. LGDT, LIDT, LLDT, LTR et
    // LMSW n'écrivent QUE là-dedans ; un vecteur aveugle à ces champs laisserait passer
    // tout l'amorçage du mode protégé sans rien voir.
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public uint[] sys_base;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public uint[] sys_limit;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public uint[] sys_limit_raw;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public uint[] sys_limit_low;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public uint[] sys_limit_high;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public int[] sys_checked;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public ushort[] sys_sel;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public byte[] sys_access;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public byte[] sys_access2;

    // cr0 est cpu_state.CR0 ; msw en est les 16 bits bas, donc pas de champ séparé.
    // cr4 est omis : 486 et au-delà. cpl_override n'est pas un registre mais
    // l'interrupteur qui fait sauter les contrôles de privilège.
    public uint cr0;
    public uint cr2;
    public uint cr3;
    public uint use32;
    public int stack32;
    public int cpl_override;

    // Les DRAPEAUX PARESSEUX (x86.h:65-68). exec386 ne matérialise pas `flags` : entre
    // deux flags_rebuild() il est périmé, donc le comparer seul serait un accord vide.
    // On compare la représentation paresseuse elle-même plutôt que de la matérialiser
    // avant chaque capture — sans quoi flags_op vaudrait FLAGS_UNKNOWN à toutes les
    // captures par construction, et ne serait jamais comparé.
    public int flags_op;
    public uint flags_res;
    public uint flags_op1;
    public uint flags_op2;

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
        seg_limit = new uint[(int)Seg.COUNT],
        seg_limit_raw = new uint[(int)Seg.COUNT],
        seg_limit_low = new uint[(int)Seg.COUNT],
        seg_limit_high = new uint[(int)Seg.COUNT],
        seg_checked = new int[(int)Seg.COUNT],
        seg_access = new byte[(int)Seg.COUNT],
        seg_access2 = new byte[(int)Seg.COUNT],
        sys_base = new uint[(int)Sys.COUNT],
        sys_limit = new uint[(int)Sys.COUNT],
        sys_limit_raw = new uint[(int)Sys.COUNT],
        sys_limit_low = new uint[(int)Sys.COUNT],
        sys_limit_high = new uint[(int)Sys.COUNT],
        sys_checked = new int[(int)Sys.COUNT],
        sys_sel = new ushort[(int)Sys.COUNT],
        sys_access = new byte[(int)Sys.COUNT],
        sys_access2 = new byte[(int)Sys.COUNT],
        prefetchqueue = new byte[6],
        _pad = new byte[2],
    };
}
