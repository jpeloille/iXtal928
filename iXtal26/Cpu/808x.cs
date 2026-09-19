// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/cpu/808x.c  (lignes 36-260, 886-906, 1222-1276, 3902-3996)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — M1.0b : accesseurs, file de préfetch, clockhardware, et la
//         coquille d'execx86 avec un seul `default:`. Aucun opcode réel encore.
//
// Le modèle de temps du 8088. C'est la partie la plus fidèle de PCem et la plus
// fragile à transcrire : la réconciliation entre cycdiff, cycles, memcycs,
// fetchclocks et nextcyc n'a AUCUN invariant qui échoue bruyamment. Se tromper
// ici ne plante rien, ne fausse aucun registre, et se découvre trois jalons plus
// tard sous forme d'un CGA à 57 Hz au lieu de 59,92.
//
// D'où la règle : on transcrit les dix variables avec les noms de PCem, on ne
// consolide rien, on n'extrait aucune table, et le diff par instruction compare
// chacune d'elles dès la première passe.

// CS0164 : le label `opcodestart` n'est pas encore référencé. Ses quatre seuls
// utilisateurs sont les préfixes de segment (808x.c:1589/1664/1739/1798), qui
// arrivent en M1.9 — délibérément en dernier, ce sont les seuls `goto` du
// fichier. À retirer dès qu'ils sont transcrits.
#pragma warning disable CS0164

using System.Runtime.CompilerServices;
using iXtal26.Diag;
using iXtal26.Memory;
using iXtal26.Models;
using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;

namespace iXtal26.Cpu;

internal static partial class _808x
{
    // pcem: 808x.c:36-52
    internal static int tempc;
    private static int noint = 0;

    internal static int output = 0;
    internal static int ins = 0;
    internal static int insc = 0;

    internal static int is8086 = 0;

    private static uint32_t oldds;
    internal static uint32_t oldss;

    private static int nextcyc = 0;
    private static int memcycs;

    private static int cycdiff;

    // pcem: 808x.c:886-891
    internal static int current_diff = 0;
    private static uint64_t tsc_frac = 0;

    // pcem: 808x.c:906, 1217
    private static int takeint = 0;
    private static int inhlt = 0;

    // pcem: cpu.h:160 — tops de l'oscillateur maître par cycle CPU, en 32:32.
    // Posé par setpitclock() (pit.c:52) à M3 ; en M1 c'est h_reset qui le fixe.
    internal static uint64_t xt_cpu_multi;

    internal static int nmi, nmi_mask;

    // -----------------------------------------------------------------------
    // Accès mémoire privés du 8088 (pcem: 808x.c:57-110)
    //
    // Ils comptabilisent `memcycs`, les cycles de bus consommés par les DONNÉES,
    // que la réconciliation de fin d'instruction retranchera du budget.
    //
    // La garde `if (a != (cs + cpu_state.pc))` est chargée de sens : elle
    // distingue une lecture d'opérande d'une relecture du flux d'instruction, et
    // l'oublier rend lourde de quelques cycles chaque instruction qui lit son
    // propre flux — sans que rien ne plante. readmembf, la variante de préfetch,
    // ne facture rien du tout.
    // -----------------------------------------------------------------------

    private static uint8_t readmemb(uint32_t a)
    {
        if (a != (cs + cpu_state.pc))
                memcycs += 4;
        if (mem.readlookup2[a >> 12] == -1)
                return mem.readmembl(a);
        else
                return mem.ram[mem.readlookup2[a >> 12] + a];
    }

    private static uint8_t readmembf(uint32_t a)
    {
        if (mem.readlookup2[a >> 12] == -1)
                return mem.readmembl(a);
        else
                return mem.ram[mem.readlookup2[a >> 12] + a];
    }

    private static uint16_t readmemw(uint32_t s, uint16_t a)
    {
        // pcem bug, reproduced: `a` est l'offset 16 bits, `cs + pc` une adresse
        // linéaire 20 bits — la garde est donc vraie en pratique toujours, là où
        // readmemb compare bien deux adresses linéaires. On transcrit tel quel.
        if (a != (cs + cpu_state.pc))
                memcycs += (8 >> is8086);
        if (mem.readlookup2[(s + a) >> 12] == -1 || s == 0xFFFFFFFF)
                return mem.readmemwl(s + a);
        else
                return (uint16_t)(mem.ram[mem.readlookup2[(s + a) >> 12] + s + a]
                                | (mem.ram[mem.readlookup2[(s + a) >> 12] + s + a + 1] << 8));
    }

    internal static void refreshread()
    {
        FETCHCOMPLETE();
        memcycs += 4;
    }

    private static void writememb(uint32_t a, uint8_t v)
    {
        memcycs += 4;
        if (mem.writelookup2[a >> 12] == -1)
                mem.writemembl(a, v);
        else
                mem.ram[mem.writelookup2[a >> 12] + a] = v;
    }

    private static void writememw(uint32_t s, uint32_t a, uint16_t v)
    {
        memcycs += (8 >> is8086);
        if (mem.writelookup2[(s + a) >> 12] == -1 || s == 0xFFFFFFFF)
                mem.writememwl(s + a, v);
        else
        {
                mem.ram[mem.writelookup2[(s + a) >> 12] + s + a] = (uint8_t)v;
                mem.ram[mem.writelookup2[(s + a) >> 12] + s + a + 1] = (uint8_t)(v >> 8);
        }
    }

    // -----------------------------------------------------------------------
    // La file de préfetch (pcem: 808x.c:114-260)
    //
    // 4 octets sur 8088, 6 sur 8086 — is8086 sélectionne partout. C'EST
    // l'émulation, pas une optimisation : supprimer la file donnerait un CPU aux
    // registres justes et au temps faux.
    // -----------------------------------------------------------------------

    private static int fetchcycles = 0, fetchclocks;

    private static readonly uint8_t[] prefetchqueue = new uint8_t[6];
    private static uint16_t prefetchpc;
    private static int prefetchw = 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint8_t FETCH()
    {
        uint8_t temp;

        if (prefetchw == 0) //(fetchcycles<4)
        {
                cycles -= (4 - (fetchcycles & 3));
                fetchclocks += (4 - (fetchcycles & 3));
                fetchcycles = 4;
                temp = readmembf(cs + cpu_state.pc);
                cpu_state.pc = cpu_state.pc + 1;
                prefetchpc = (uint16_t)cpu_state.pc;
                if (is8086 != 0 && (cpu_state.pc & 1) != 0)
                {
                        prefetchqueue[0] = readmembf(cs + prefetchpc);
                        prefetchpc++;
                        prefetchw++;
                }
        }
        else
        {
                temp = prefetchqueue[0];
                prefetchqueue[0] = prefetchqueue[1];
                prefetchqueue[1] = prefetchqueue[2];
                prefetchqueue[2] = prefetchqueue[3];
                prefetchqueue[3] = prefetchqueue[4];
                prefetchqueue[4] = prefetchqueue[5];
                prefetchw--;
                fetchcycles -= 4;
                cpu_state.pc++;
        }
        return temp;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FETCHADD(int c)
    {
        int d;
        if (c < 0)
                return;
        if (prefetchw > ((is8086 != 0) ? 4 : 3))
                return;
        d = c + (fetchcycles & 3);
        while (d > 3 && prefetchw < ((is8086 != 0) ? 6 : 4))
        {
                d -= 4;
                if (is8086 != 0 && (prefetchpc & 1) == 0)
                {
                        prefetchqueue[prefetchw] = readmembf(cs + prefetchpc);
                        prefetchpc++;
                        prefetchw++;
                }
                if (prefetchw < 6)
                {
                        prefetchqueue[prefetchw] = readmembf(cs + prefetchpc);
                        prefetchpc++;
                        prefetchw++;
                }
        }
        fetchcycles += c;
        if (fetchcycles > 16)
                fetchcycles = 16;
    }

    private static void FETCHCOMPLETE()
    {
        if ((fetchcycles & 3) == 0)
                return;
        if (prefetchw > ((is8086 != 0) ? 4 : 3))
                return;
        if (prefetchw == 0)
                nextcyc = (4 - (fetchcycles & 3));
        cycles -= (4 - (fetchcycles & 3));
        fetchclocks += (4 - (fetchcycles & 3));
        if (is8086 != 0 && (prefetchpc & 1) == 0)
        {
                prefetchqueue[prefetchw] = readmembf(cs + prefetchpc);
                prefetchpc++;
                prefetchw++;
        }
        if (prefetchw < 6)
        {
                prefetchqueue[prefetchw] = readmembf(cs + prefetchpc);
                prefetchpc++;
                prefetchw++;
        }
        fetchcycles += (4 - (fetchcycles & 3));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FETCHCLEAR()
    {
        prefetchpc = (uint16_t)cpu_state.pc;
        prefetchw = 0;
        memcycs = cycdiff - cycles;
        fetchclocks = 0;
    }

    private static uint16_t getword()
    {
        uint8_t temp = FETCH();
        return (uint16_t)(temp | (FETCH() << 8));
    }

    // -----------------------------------------------------------------------
    // pcem: 808x.c:893-904
    //
    // Les systèmes XT cadencent leurs timers sur l'oscillateur maître à
    // 14,318 MHz, pas sur l'horloge CPU. Comme il n'y a pas de rapport entier
    // entre les deux, la conversion du TSC se fait en virgule fixe 32:32.
    // -----------------------------------------------------------------------
    private static void clockhardware()
    {
        int diff = cycdiff - cycles - current_diff;

        current_diff += diff;

        tsc_frac += (uint64_t)diff * xt_cpu_multi;

        timer.tsc += (tsc_frac >> 32);
        tsc_frac &= 0xffffffff;
        if (timer.TIMER_VAL_LESS_THAN_VAL(timer.timer_target, (uint32_t)timer.tsc))
                timer.timer_process();
    }

    // pcem: 808x.c:662-702
    internal static void resetx86()
    {
        ins = 0;
        use32 = 0;
        cpu_cur_status = 0;
        stack32 = 0;
        msw = 0;
        if (is486 != 0)
                cr0 = 1 << 30;
        else
                cr0 = 0;
        cr4 = 0;
        cpu_state.eflags = 0;
        cgate32 = 0;
        if (AT != 0)
        {
                x86seg_c.loadcs(0xF000);
                cpu_state.pc = 0xFFF0;
                mem.rammask = cpu_16bitbus != 0 ? 0xFFFFFF : 0xFFFFFFFF;
        }
        else
        {
                x86seg_c.loadcs(0xFFFF);
                cpu_state.pc = 0;
                mem.rammask = 0xfffff;
        }
        idt.@base = 0;
        idt.limit = is386 != 0 ? 0x03FFu : 0xFFFFu;
        cpu_state.flags = 2;
        EAX = EBX = ECX = EDX = ESI = EDI = EBP = ESP = 0;
        // omitted (M1.0c) : makeznptable() et makemod1table(). Les tables de
        // flags et d'EA ne servent qu'aux vrais opcodes ; le `default:` de M1.0b
        // ne touche ni l'un ni l'autre.
        mem.resetreadlookup();
        FETCHCLEAR();
        // omitted: x87_reset(), cpu_set_edx(), codegen_reset() — 8087 et dynarec.
        mem.mmu_perm = 4;
        x86seg_c.x86seg_reset();
        x86_was_reset = 1;
        cpu_state.smbase = 0x30000;
    }

    // -----------------------------------------------------------------------
    // pcem: 808x.c:1222-... — la boucle d'exécution
    //
    // Contrairement à exec386 (386.c:163), elle n'est PAS bornée par
    // timer_target : c'est un simple `cycles += cycs; while (cycles > 0)`, et la
    // synchronisation des timers se fait par instruction via clockhardware().
    // -----------------------------------------------------------------------
    internal static void execx86(int cycs)
    {
        uint8_t temp;
        uint16_t addr;
        int trap;

        cycles += cycs;
        while (cycles > 0)
        {
                uint8_t opcode;

                cycdiff = cycles;
                current_diff = 0;
                cycles -= nextcyc;
                nextcyc = 0;
                fetchclocks = 0;
                cpu_state.oldpc = cpu_state.pc;
        opcodestart:
                opcode = FETCH();
                tempc = cpu_state.flags & C_FLAG;
                trap = cpu_state.flags & T_FLAG;
                cpu_state.pc--;
                cpu_state.pc++;
                inhlt = 0;
                switch (opcode)
                {
                default:
                        FETCH();
                        cycles -= 8;
                        break;
                }
                cpu_state.pc &= 0xFFFF;

                if (cpu_state.ssegs != 0)
                {
                        ds = oldds;
                        ss = oldss;
                        cpu_state.ssegs = 0;
                }

                FETCHADD(((cycdiff - cycles) - memcycs) - fetchclocks);
                if ((cycdiff - cycles) < memcycs)
                        cycles -= (memcycs - (cycdiff - cycles));
                memcycs = 0;

                insc++;
                clockhardware();

                if (trap != 0 && (cpu_state.flags & T_FLAG) != 0 && noint == 0)
                {
                        writememw(ss, (uint32_t)((SP - 2) & 0xFFFF), (uint16_t)(cpu_state.flags | 0xF000));
                        writememw(ss, (uint32_t)((SP - 4) & 0xFFFF), CS);
                        writememw(ss, (uint32_t)((SP - 6) & 0xFFFF), (uint16_t)cpu_state.pc);
                        SP -= 6;
                        addr = 1 << 2;
                        cpu_state.flags &= unchecked((uint16_t)~I_FLAG);
                        cpu_state.flags &= unchecked((uint16_t)~T_FLAG);
                        cpu_state.pc = readmemw(0, addr);
                        x86seg_c.loadcs(readmemw(0, (uint16_t)(addr + 2)));
                        FETCHCLEAR();
                }
                else if (nmi != 0 && nmi_enable != 0 && nmi_mask != 0)
                {
                        writememw(ss, (uint32_t)((SP - 2) & 0xFFFF), (uint16_t)(cpu_state.flags | 0xF000));
                        writememw(ss, (uint32_t)((SP - 4) & 0xFFFF), CS);
                        writememw(ss, (uint32_t)((SP - 6) & 0xFFFF), (uint16_t)cpu_state.pc);
                        SP -= 6;
                        addr = 2 << 2;
                        cpu_state.flags &= unchecked((uint16_t)~I_FLAG);
                        cpu_state.flags &= unchecked((uint16_t)~T_FLAG);
                        cpu_state.pc = readmemw(0, addr);
                        x86seg_c.loadcs(readmemw(0, (uint16_t)(addr + 2)));
                        FETCHCLEAR();
                        nmi_enable = 0;
                }
                else if (takeint != 0 && cpu_state.ssegs == 0 && noint == 0)
                {
                        temp = pic.picinterrupt();
                        if (temp != 0xFF)
                        {
                                if (inhlt != 0)
                                        cpu_state.pc++;
                                writememw(ss, (uint32_t)((SP - 2) & 0xFFFF), (uint16_t)(cpu_state.flags | 0xF000));
                                writememw(ss, (uint32_t)((SP - 4) & 0xFFFF), CS);
                                writememw(ss, (uint32_t)((SP - 6) & 0xFFFF), (uint16_t)cpu_state.pc);
                                SP -= 6;
                                addr = (uint16_t)(temp << 2);
                                cpu_state.flags &= unchecked((uint16_t)~I_FLAG);
                                cpu_state.flags &= unchecked((uint16_t)~T_FLAG);
                                cpu_state.pc = readmemw(0, addr);
                                x86seg_c.loadcs(readmemw(0, (uint16_t)(addr + 2)));
                                FETCHCLEAR();
                        }
                }
                takeint = ((cpu_state.flags & I_FLAG) != 0 && (pic.pic_.pend & ~pic.pic_.mask) != 0) ? 1 : 0;

                if (noint != 0)
                        noint = 0;
                ins++;
        }
    }
}
