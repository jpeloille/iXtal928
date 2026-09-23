// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x86.h  (lignes 1-245)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — palier (a) seulement. Les champs 8087/MMX/SMM/32 bits sont
//         omis, cf. le registre des omissions de TRANSCRIPTION.md.
//
// Types du CPU et couche de macros. x86.h aplatit cpu_state en #define pour que
// 4 000 lignes puissent écrire `cycles -= 3;` ou `AL = 0x42;`. On reproduit cet
// aplatissement avec des propriétés ref-returning : une propriété ref EST une
// variable, donc l'affectation composée compile et mute le référent. Résoudre
// les macros à la main reviendrait à réécrire chacune de ces 4 000 lignes.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using static iXtal26.Cpu._386_common;

namespace iXtal26.Cpu;

// pcem: x86.h:29-35
[StructLayout(LayoutKind.Explicit, Size = 4)]
internal struct x86reg
{
    [FieldOffset(0)] internal uint32_t l;
    [FieldOffset(0)] internal uint16_t w;
    [FieldOffset(0)] internal x86reg_b b;
}

// L'union b tient les deux moitiés 8 bits. Petit-boutien assumé, exactement
// comme le C : getr8 (x86.h:227) indexe regs[r & 3] et choisit la moitié sur le
// bit 2 de r, ce qui n'a de sens qu'avec cette disposition.
[StructLayout(LayoutKind.Explicit, Size = 2)]
internal struct x86reg_b
{
    [FieldOffset(0)] internal uint8_t l;
    [FieldOffset(1)] internal uint8_t h;
}

// pcem: x86.h:37-45
// Classe et non struct : cpu_state.ea_seg est un x86seg* qui aliase l'un des six
// segments. Un type valeur ferait une copie et l'écriture serait perdue sans
// diagnostic.
internal sealed class x86seg
{
    internal uint32_t @base;      // `base` est un mot-clé C#
    internal uint32_t limit, limit_raw;
    internal uint8_t access, access2;
    internal uint16_t seg;
    internal uint32_t limit_low, limit_high;
    internal int @checked;        // `checked` est un mot-clé C#
}

// pcem: x86.h:57-118
internal sealed class cpu_state_t
{
    internal readonly x86reg[] regs = new x86reg[8];

    internal x86seg? ea_seg;
    internal uint32_t eaaddr;

    internal uint32_t pc;
    internal uint32_t oldpc;

    // pcem: x86.h:76-81 — l'union rm_data. Le palier (a) ne lit jamais la forme
    // agrégée rm_mod_reg_data, seulement les trois champs.
    internal int8_t rm, mod, reg;

    internal int8_t ssegs;
    internal int8_t abrt;

    internal int _cycles;

    internal readonly x86seg seg_cs = new(), seg_ds = new(), seg_es = new();
    internal readonly x86seg seg_ss = new(), seg_fs = new(), seg_gs = new();

    internal uint32_t CR0;

    internal uint16_t flags, eflags;

    // pcem: x86.h:71 — le quadrant d'opérandes courant, `cpu_state.op32 = use32`
    // au sommet de la boucle de exec386 (386.c:172). Il indexe la table d'opcodes :
    // `x86_opcodes[(opcode | op32) & 0x3ff]`. Nul sur un 286, qui n'a pas de
    // variantes 32 bits — d'où les 256 seules entrées atteignables sur 1024.
    internal uint32_t op32;

    internal uint32_t smbase;

    // pcem: x86.h:65-68 — LES DRAPEAUX PARESSEUX. Dé-omis en A2.1.
    //
    // exec386 ne matérialise pas `flags` : il retient l'OPÉRATION et ses opérandes, et
    // ne reconstruit les six bits arithmétiques qu'au moment où quelqu'un les lit
    // (flags_rebuild, x86_flags.h:420). Entre deux reconstructions `flags` est PÉRIMÉ,
    // donc le comparer seul serait comparer un champ mort des deux côtés.
    //
    // Le 8088 ne les touche toujours pas — 0 occurrence dans 808x.c, et les six appels
    // de x86seg.c sont en mode protégé ou SMM, inatteignables ici. Ils entrent avant
    // d'être nécessaires, pour que le vecteur d'état vérifie le câblage des deux côtés
    // pendant qu'ils ne varient pas encore.
    internal int flags_op;
    internal uint32_t flags_res;
    internal uint32_t flags_op1, flags_op2;

    // omitted: ST/TOP/tag/npxs/npxc/MM/MM_w4/ismmx — 8087 et MMX.
    // omitted: smi_pending, cpu_recomp_ins, old_fp_control & co.
}

internal static partial class x86
{
    // cpu_state vit dans Cpu/386_common.cs, comme en C (386_common.c:6), et
    // arrive ici par `using static`. Global de PCem, global ici : voir
    // TRANSCRIPTION.md sur pourquoi il n'y a pas de struct d'instance.

    // ---- pcem: x86.h:122-143 — l'aplatissement en macros ------------------
    // Chacune est une propriété ref : `cycles -= 3;` compile et mute le champ.

    internal static ref int cycles { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ref cpu_state._cycles; }
    internal static ref uint32_t cr0 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ref cpu_state.CR0; }

    // msw est les 16 bits bas de CR0 (union w/l en C). Une propriété ref ne peut
    // pas rendre une demi-variable : seul site du fichier où la macro se résout.
    internal static uint16_t msw
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)] get => (uint16_t)cpu_state.CR0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] set => cpu_state.CR0 = (cpu_state.CR0 & 0xFFFF0000u) | value;
    }

    /*Segments -
      _cs,_ds,_es,_ss are the segment structures
      CS,DS,ES,SS is the 16-bit data
      cs,ds,es,ss are defines to the bases*/
    internal static ref uint16_t CS { get => ref cpu_state.seg_cs.seg; }
    internal static ref uint16_t DS { get => ref cpu_state.seg_ds.seg; }
    internal static ref uint16_t ES { get => ref cpu_state.seg_es.seg; }
    internal static ref uint16_t SS { get => ref cpu_state.seg_ss.seg; }
    internal static ref uint16_t FS { get => ref cpu_state.seg_fs.seg; }
    internal static ref uint16_t GS { get => ref cpu_state.seg_gs.seg; }
    internal static ref uint32_t cs { get => ref cpu_state.seg_cs.@base; }
    internal static ref uint32_t ds { get => ref cpu_state.seg_ds.@base; }
    internal static ref uint32_t es { get => ref cpu_state.seg_es.@base; }
    internal static ref uint32_t ss { get => ref cpu_state.seg_ss.@base; }
    internal static ref uint32_t gs { get => ref cpu_state.seg_gs.@base; }

    // pcem: x86.h:4-27 — registres généraux
    internal static ref uint32_t EAX { get => ref cpu_state.regs[0].l; }
    internal static ref uint32_t ECX { get => ref cpu_state.regs[1].l; }
    internal static ref uint32_t EDX { get => ref cpu_state.regs[2].l; }
    internal static ref uint32_t EBX { get => ref cpu_state.regs[3].l; }
    internal static ref uint32_t ESP { get => ref cpu_state.regs[4].l; }
    internal static ref uint32_t EBP { get => ref cpu_state.regs[5].l; }
    internal static ref uint32_t ESI { get => ref cpu_state.regs[6].l; }
    internal static ref uint32_t EDI { get => ref cpu_state.regs[7].l; }
    internal static ref uint16_t AX { get => ref cpu_state.regs[0].w; }
    internal static ref uint16_t CX { get => ref cpu_state.regs[1].w; }
    internal static ref uint16_t DX { get => ref cpu_state.regs[2].w; }
    internal static ref uint16_t BX { get => ref cpu_state.regs[3].w; }
    internal static ref uint16_t SP { get => ref cpu_state.regs[4].w; }
    internal static ref uint16_t BP { get => ref cpu_state.regs[5].w; }
    internal static ref uint16_t SI { get => ref cpu_state.regs[6].w; }
    internal static ref uint16_t DI { get => ref cpu_state.regs[7].w; }
    internal static ref uint8_t AL { get => ref cpu_state.regs[0].b.l; }
    internal static ref uint8_t AH { get => ref cpu_state.regs[0].b.h; }
    internal static ref uint8_t CL { get => ref cpu_state.regs[1].b.l; }
    internal static ref uint8_t CH { get => ref cpu_state.regs[1].b.h; }
    internal static ref uint8_t DL { get => ref cpu_state.regs[2].b.l; }
    internal static ref uint8_t DH { get => ref cpu_state.regs[2].b.h; }
    internal static ref uint8_t BL { get => ref cpu_state.regs[3].b.l; }
    internal static ref uint8_t BH { get => ref cpu_state.regs[3].b.h; }

    // pcem: x86.h:76-81 — les trois champs de rm_data, vus comme cpu_mod/reg/rm
    internal static ref int8_t cpu_mod { get => ref cpu_state.mod; }
    internal static ref int8_t cpu_reg { get => ref cpu_state.reg; }
    internal static ref int8_t cpu_rm { get => ref cpu_state.rm; }

    // pcem: x86.h:227-235
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint8_t getr8(int r) => (r & 4) != 0 ? cpu_state.regs[r & 3].b.h : cpu_state.regs[r & 3].b.l;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void setr8(int r, uint8_t v)
    {
        if ((r & 4) != 0) cpu_state.regs[r & 3].b.h = v;
        else cpu_state.regs[r & 3].b.l = v;
    }

    // pcem: x86.h:145
    internal static int CPL => (cpu_state.seg_cs.access >> 5) & 3;

    // pcem: x86.h:145-172 — drapeaux
    internal const uint16_t C_FLAG = 0x0001;
    internal const uint16_t P_FLAG = 0x0004;
    internal const uint16_t A_FLAG = 0x0010;
    internal const uint16_t Z_FLAG = 0x0040;
    internal const uint16_t N_FLAG = 0x0080;
    internal const uint16_t T_FLAG = 0x0100;
    internal const uint16_t I_FLAG = 0x0200;
    internal const uint16_t D_FLAG = 0x0400;
    internal const uint16_t V_FLAG = 0x0800;
    internal const uint16_t NT_FLAG = 0x4000;
    internal const uint16_t VM_FLAG = 0x0002;  /*In EFLAGS*/
    // pcem: x86.h:158-159 — les drapeaux d'interruption VIRTUELS, du Pentium.
    // Portés parce que opCLI, opSTI et opPUSHF les testent ; leurs branches ne
    // sont prises que si cr4 porte VME ou PVI, ce qu'un 286 ne fait jamais.
    internal const uint16_t VIF_FLAG = 0x0008;  /*In EFLAGS*/
    internal const uint16_t VIP_FLAG = 0x0010;  /*In EFLAGS*/

    // pcem: x86.h:163-164
    internal const uint32_t CR4_VME = 1 << 0;
    internal const uint32_t CR4_PVI = 1 << 1;

    // pcem: x86.h:169 — `#define IOPLp ((!(msw & 1)) || (CPL <= IOPL))`.
    // En mode réel il vaut TOUJOURS vrai : c'est ce qui fait que CLI et STI y
    // posent simplement le drapeau sans jamais lever de faute.
    internal static bool IOPLp => (msw & 1) == 0 || CPL <= IOPL;

    // pcem: x86.h:185-193
    // pcem: x86.h:185 — pose par LMSW quand le bit 0 du mot d'etat machine
    // passe a un. Porte depuis la seconde table ; lu par le recompilateur seul.
    internal const uint16_t CPU_STATUS_PMODE = 1 << 2;
    internal const uint16_t CPU_STATUS_NOTFLATDS = 1 << 8;
    internal const uint16_t CPU_STATUS_NOTFLATSS = 1 << 9;

    // pcem: x86.h:196-210 — globaux hors cpu_state
    internal static x86seg gdt = new(), ldt = new(), idt = new(), tr = new();
    internal static uint32_t rmdat;
    internal static uint32_t easeg;
    internal static int oldcpl;
    internal static uint32_t oldss;
    internal static int trap;
    internal static uint32_t use32;
    internal static int stack32;
    internal static uint16_t cpu_cur_status;
    internal static uint32_t cr2, cr3, cr4;
    // pcem: x86.h:255-267 — les causes d'abandon. ABRT_MASK vaut 0x7F et non 7 :
    // le bit haut porte ABRT_EXPECTED, une distinction du recompilateur.
    internal const int ABRT_NONE = 0;
    internal const int ABRT_GEN = 1;
    internal const int ABRT_TS = 0xA;
    internal const int ABRT_NP = 0xB;
    internal const int ABRT_SS = 0xC;
    internal const int ABRT_GPF = 0xD;
    internal const int ABRT_PF = 0xE;
    internal const int ABRT_MASK = 0x7f;
    // pcem: x86.h:267 — `#define ABRT_EXPECTED ((int8_t)0x80)`. Porté depuis A10,
    // où x86gpf_expected en a besoin.
    internal const int ABRT_EXPECTED = unchecked((int)0xffffff80);

    // pcem: x86.h:268 — le code d'erreur que le gestionnaire empile en mode
    // protégé. En mode réel il est posé puis ignoré ; on le porte quand même,
    // parce que le ne pas porter ferait diverger la transcription de x86gpf.
    internal static uint32_t abrt_error;

    // pcem: x86.h:216 — `extern int cgate16, cgate32;`. Posés par loadcscall
    // (mode protégé) pour dire à CALL_FAR quelle LARGEUR empiler : une porte
    // d'appel 16 bits empile deux mots, une porte 32 bits deux longs. En mode
    // réel les deux restent nuls, et CALL_FAR_w prend donc sa branche `else`,
    // celle qui empile en 32 bits — un détail qui n'a l'air de rien et qui est
    // observable au premier CALL far.
    internal static int cgate16;
    internal static int cgate32;

    // pcem: x86.h:250-254. `optype` dit à x86_doabrt et aux routines de mode
    // protégé quelle FAMILLE d'instruction a levé l'exception : un abandon en
    // plein CALL ne se rattrape pas comme un abandon en plein IRET. Posé autour
    // des appels au mode protégé, remis à zéro juste après.
    internal static int optype;
    internal const int JMP = 1;
    internal const int CALL = 2;
    internal const int IRET = 3;
    internal const int OPTYPE_INT = 4;

    // pcem: x86.h:167 — `#define IOPL ((cpu_state.flags >> 12) & 3)`
    internal static int IOPL => (cpu_state.flags >> 12) & 3;

    // pcem: x86.h:217 — fait sauter les contrôles de privilège le temps d'un
    // chargement de descripteur. Jamais posé sur un 8088 ; entre au jalon 286 avec
    // le reste des registres système, pour que le vecteur d'état le compare dès
    // maintenant à la valeur qu'il ne fait pas varier.
    internal static int cpl_override;
    // pcem: 386_dynarec.c:22 — `#define CPU_BLOCK_END() cpu_block_end = 1`.
    //
    // INERTE ICI, ET IL FAUT SAVOIR POURQUOI. Les handlers sont instanciés par
    // 386_dynarec.c (c'est lui qui fait `#include "386_ops.h"`), donc c'est SA
    // définition de CPU_BLOCK_END qu'ils portent — celle qui pose ce drapeau.
    // Mais 386.c, dont vient exec386, redéfinit la macro À VIDE (386.c:17-18) et
    // ne lit jamais la variable : seul exec_interpreter du dynarec la consulte,
    // et il ne tourne pas ici. On la pose donc, sans la lire — le contraire
    // ferait diverger le C# du C le jour où un autre lecteur apparaîtrait.
    internal static int cpu_block_end;

    // pcem: 386_dynarec.c:233 et le codegen. INERTES ICI, pour la même raison que
    // cpu_block_end juste au-dessus : les handlers les POSENT parce qu'ils sont
    // compilés dans 386_dynarec.c, mais exec386 vient de 386.c, qui ne lit ni
    // l'un ni l'autre — vérifié, zéro occurrence de cpu_end_block_after_ins dans
    // 386.c. On les porte sans les lire ; ne pas les porter ferait diverger la
    // transcription de opSTI et de opPOPF_286.
    internal static int cpu_end_block_after_ins;
    internal static int codegen_flags_changed;

    internal static int x86_was_reset;

    // pcem: ibm.h:160 — classe de machine. 0 sur un XT.
    internal static int AT, AMSTRAD, PCI, TANDY, MCA;
    internal static int is386, is486, cpu_16bitbus;

    // pcem: codegen — drapeaux « segment plat », écrits par loadseg. Le dynarec
    // n'est pas porté, mais les écritures le sont : les retirer changerait
    // loadseg, pas seulement le codegen.
    internal static int codegen_flat_ds = 1, codegen_flat_ss = 1;
}
