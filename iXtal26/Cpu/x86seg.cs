// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/cpu/x86seg.c  (lignes 50-74, 419-448, 452-566)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — branches MODE RÉEL uniquement (~80 lignes sur 3 187).
//
// x86seg.c est partagé entre le cœur 8088 et le cœur 386 : 808x.c l'appelle pour
// loadcs/loadseg, et sur un XT `msw & 1` vaut toujours 0, donc seules les
// branches `else` sont atteintes. Tout le mode protégé — descripteurs, portes,
// TSS, gates — est hors palier (a) et constitue son propre lot de travail.
//
// Le nom de conteneur est x86seg_c : le fichier x86seg.c et le typedef x86seg se
// disputent le nom, et c'est le type qui le garde (cf. TRANSCRIPTION.md).

using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;

namespace iXtal26.Cpu;

internal static partial class x86seg_c
{
    // pcem: x86seg.c:50-65
    private static void seg_reset(x86seg s)
    {
        s.access = (0 << 5) | 2;
        s.access2 = 0;
        s.limit = 0xFFFF;
        s.limit_low = 0;
        s.limit_high = 0xffff;
        if (s == cpu_state.seg_cs)
        {
                // TODO - When the PC is reset, initialization of the CS descriptor must be like the annotated line below.
                // s->base = AT ? (cpu_16bitbus ? 0xFF0000 : 0xFFFF0000) : 0xFFFF0;
                s.@base = AT != 0 ? 0xF0000u : 0xFFFF0u;
                s.seg = AT != 0 ? (uint16_t)0xF000 : (uint16_t)0xFFFF;
        }
        else
        {
                s.@base = 0;
                s.seg = 0;
        }
    }

    // pcem: x86seg.c:67-74
    internal static void x86seg_reset()
    {
        seg_reset(cpu_state.seg_cs);
        seg_reset(cpu_state.seg_ds);
        seg_reset(cpu_state.seg_es);
        seg_reset(cpu_state.seg_fs);
        seg_reset(cpu_state.seg_gs);
        seg_reset(cpu_state.seg_ss);
    }

    // pcem: x86seg.c:410-448 — branche `else` (mode réel) de loadseg().
    // omitted: toute la branche mode protégé (msw & 1), ~350 lignes.
    internal static int loadseg(uint16_t seg, x86seg s)
    {
        s.access = (3 << 5) | 2;
        s.access2 = 0;
        s.@base = (uint32_t)(seg << 4);
        s.seg = seg;
        s.@checked = 1;
        if (s == cpu_state.seg_ds)
                codegen_flat_ds = 0;
        if (s == cpu_state.seg_ss)
                codegen_flat_ss = 0;
        // omitted: `if (s == seg_ss && (eflags & VM_FLAG)) set_stack32(0);`
        //          VM_FLAG est inatteignable sur un XT (mode virtuel 8086 = 386+).

        if (s == cpu_state.seg_ds)
        {
                if (s.@base == 0 && s.limit_low == 0 && s.limit_high == 0xffffffff)
                        cpu_cur_status &= unchecked((uint16_t)~CPU_STATUS_NOTFLATDS);
                else
                        cpu_cur_status |= CPU_STATUS_NOTFLATDS;
        }
        if (s == cpu_state.seg_ss)
        {
                if (s.@base == 0 && s.limit_low == 0 && s.limit_high == 0xffffffff)
                        cpu_cur_status &= unchecked((uint16_t)~CPU_STATUS_NOTFLATSS);
                else
                        cpu_cur_status |= CPU_STATUS_NOTFLATSS;
        }

        return cpu_state.abrt;
    }

    // pcem: x86seg.c:452-566 — branche `else` (mode réel) de loadcs().
    // omitted: toute la branche mode protégé, ~90 lignes.
    internal static void loadcs(uint16_t seg)
    {
        cpu_state.seg_cs.@base = (uint32_t)(seg << 4);
        cpu_state.seg_cs.limit = 0xFFFF;
        cpu_state.seg_cs.limit_low = 0;
        cpu_state.seg_cs.limit_high = 0xffff;
        CS = seg;
        if ((cpu_state.eflags & VM_FLAG) != 0)
                cpu_state.seg_cs.access = (3 << 5) | 2;
        else
                cpu_state.seg_cs.access = (0 << 5) | 2;
        if (CPL == 3 && oldcpl != 3)
                Memory.mem.flushmmucache_cr3();
        oldcpl = CPL;
    }

    // pcem: x86seg.c:135-139 — LA LEVÉE D'EXCEPTION, réduite à ce qu'elle est :
    // poser la cause et le code d'erreur. C'est exec386 qui, voyant `abrt` non nul,
    // appellera x86_doabrt. Le message ne sert qu'au pclog de PCem, omis ici.
    internal static void x86gpf(string s, uint16_t error)
    {
        cpu_state.abrt = (int8_t)ABRT_GPF;
        abrt_error = error;
    }

    // pcem: x86seg.c:145-149
    internal static void x86ss(string s, uint16_t error)
    {
        cpu_state.abrt = (int8_t)ABRT_SS;
        abrt_error = error;
    }
}
