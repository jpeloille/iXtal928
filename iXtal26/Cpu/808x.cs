// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/cpu/808x.c
//         (36-260, 340-455, 456-520, 662-702, 748-886, 886-906, 1222-1340, 3902-3996)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — M1.1 : accesseurs, préfetch, EA, tables et helpers de flags,
//         resetx86, boucle execx86, bande d'opcodes 0x00-0x1F.
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
    // La garde `if (a != (cs + cpu_state.pc))` ne mord que sur les accès
    // AUTO-RÉFÉRENTIELS — quand l'opérande tombe exactement sur le pointeur
    // d'instruction. C'est rare, et c'est précisément ce qui la rend dangereuse :
    // des opérandes aléatoires ne l'atteignent jamais (1 sur 65 536), si bien
    // qu'on peut la supprimer sans qu'aucun test ne bronche. Le fuzzer fabrique
    // donc ces cas exprès (voir Fuzzer.RunSingle). readmembf, la variante de
    // préfetch, ne facture rien du tout.
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

    // -----------------------------------------------------------------------
    // Calcul d'adresse effective (pcem: 808x.c:340-455)
    // -----------------------------------------------------------------------

    // pcem: 808x.c:344-348
    //
    // DEVIATION: en C ce sont des tableaux de POINTEURS vers des champs —
    //   uint16_t *mod1add[2][8];  uint32_t *mod1seg[8];
    // le C# sûr n'a pas de pointeur de donnée. On stocke donc des INDEX (de
    // registre pour mod1add, avec -1 pour le `zero` de PCem ; de segment pour
    // mod1seg) et on déréférence au site d'usage. Même table, même résultat,
    // indirection exprimée autrement.
    private static readonly int[,] mod1add = new int[2, 8];
    private static readonly int[] mod1seg = new int[8];
    internal static readonly int[] slowrm = new int[8];

    private const int MOD1_ZERO = -1;
    private const int MOD1_DS = 0, MOD1_SS = 1;

    // pcem: 808x.c:350-378
    internal static void makemod1table()
    {
        mod1add[0, 0] = 3; // &BX
        mod1add[0, 1] = 3; // &BX
        mod1add[0, 2] = 5; // &BP
        mod1add[0, 3] = 5; // &BP
        mod1add[0, 4] = 6; // &SI
        mod1add[0, 5] = 7; // &DI
        mod1add[0, 6] = 5; // &BP
        mod1add[0, 7] = 3; // &BX
        mod1add[1, 0] = 6; // &SI
        mod1add[1, 1] = 7; // &DI
        mod1add[1, 2] = 6; // &SI
        mod1add[1, 3] = 7; // &DI
        mod1add[1, 4] = MOD1_ZERO;
        mod1add[1, 5] = MOD1_ZERO;
        mod1add[1, 6] = MOD1_ZERO;
        mod1add[1, 7] = MOD1_ZERO;
        slowrm[0] = 0;
        slowrm[1] = 1;
        slowrm[2] = 1;
        slowrm[3] = 0;
        mod1seg[0] = MOD1_DS;
        mod1seg[1] = MOD1_DS;
        mod1seg[2] = MOD1_SS;
        mod1seg[3] = MOD1_SS;
        mod1seg[4] = MOD1_DS;
        mod1seg[5] = MOD1_DS;
        mod1seg[6] = MOD1_SS;
        mod1seg[7] = MOD1_DS;
    }

    private static uint16_t Mod1Add(int which, int rm)
    {
        var idx = mod1add[which, rm];
        return idx == MOD1_ZERO ? (uint16_t)0 : cpu_state.regs[idx].w;
    }

    private static uint32_t Mod1Seg(int rm) => mod1seg[rm] == MOD1_DS ? ds : ss;

    // pcem: 808x.c:381-414 — les coûts en cycles du calcul d'EA sont ici, dans
    // les FETCHADD : c'est la table de timings d'EA du 8086.
    private static void fetcheal()
    {
        if (cpu_mod == 0 && cpu_rm == 6)
        {
                cpu_state.eaaddr = getword();
                easeg = ds;
                FETCHADD(6);
        }
        else
        {
                switch (cpu_mod)
                {
                case 0:
                        cpu_state.eaaddr = 0;
                        if ((cpu_rm & 4) != 0)
                                FETCHADD(5);
                        else
                                FETCHADD(7 + slowrm[cpu_rm]);
                        break;
                case 1:
                        cpu_state.eaaddr = (uint16_t)(int8_t)FETCH();
                        if ((cpu_rm & 4) != 0)
                                FETCHADD(9);
                        else
                                FETCHADD(11 + slowrm[cpu_rm]);
                        break;
                case 2:
                        cpu_state.eaaddr = getword();
                        if ((cpu_rm & 4) != 0)
                                FETCHADD(9);
                        else
                                FETCHADD(11 + slowrm[cpu_rm]);
                        break;
                }
                cpu_state.eaaddr += (uint32_t)(Mod1Add(0, cpu_rm) + Mod1Add(1, cpu_rm));
                easeg = Mod1Seg(cpu_rm);
                cpu_state.eaaddr &= 0xFFFF;
        }
    }

    // pcem: 808x.c:416-455
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint8_t geteab()
    {
        if (cpu_mod == 3)
                return (cpu_rm & 4) != 0 ? cpu_state.regs[cpu_rm & 3].b.h : cpu_state.regs[cpu_rm & 3].b.l;
        return readmemb(easeg + cpu_state.eaaddr);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint16_t geteaw()
    {
        if (cpu_mod == 3)
                return cpu_state.regs[cpu_rm].w;
        return readmemw(easeg, (uint16_t)cpu_state.eaaddr);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint16_t geteaw2()
    {
        if (cpu_mod == 3)
                return cpu_state.regs[cpu_rm].w;
        return readmemw(easeg, (uint16_t)((cpu_state.eaaddr + 2) & 0xFFFF));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void seteab(uint8_t val)
    {
        if (cpu_mod == 3)
        {
                if ((cpu_rm & 4) != 0)
                        cpu_state.regs[cpu_rm & 3].b.h = val;
                else
                        cpu_state.regs[cpu_rm & 3].b.l = val;
        }
        else
        {
                writememb(easeg + cpu_state.eaaddr, val);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void seteaw(uint16_t val)
    {
        if (cpu_mod == 3)
                cpu_state.regs[cpu_rm].w = val;
        else
        {
                writememw(easeg, cpu_state.eaaddr, val);
        }
    }

    // pcem: 808x.c:86-95 — la macro fetchea()
    private static void fetchea()
    {
        rmdat = FETCH();
        cpu_reg = (int8_t)((rmdat >> 3) & 7);
        cpu_mod = (int8_t)((rmdat >> 6) & 3);
        cpu_rm = (int8_t)(rmdat & 7);
        if (cpu_mod != 3)
                fetcheal();
    }

    // -----------------------------------------------------------------------
    // Flags (pcem: 808x.c:456-520, 748-886)
    //
    // Le 8088 de PCem calcule ses flags EN DIRECT, sans la moindre évaluation
    // paresseuse : deux tables donnent Z, N et P, le reste est calculé au site.
    //
    // RÈGLE DE TRANSCRIPTION, la plus dangereuse du projet : tout intermédiaire
    // arithmétique est `int` ou `uint`, jamais `byte`/`ushort`. En C,
    // `((a & 0xF) - (b & 0xF)) & 0x10` est de l'arithmétique int qui donne -1
    // puis 0x10, donc AF posé. Typer l'intermédiaire en `byte` « pour coller aux
    // types C » ferait disparaître l'emprunt SILENCIEUSEMENT — et AF n'est
    // observable qu'à travers DAA/DAS/AAA/AAS.
    // -----------------------------------------------------------------------

    internal static readonly uint8_t[] znptable8 = new uint8_t[256];
    private static readonly uint16_t[] znptable16 = new uint16_t[65536];

    // pcem: 808x.c:460-520
    // Note : la boucle 16 bits ne compte la parité que sur les 8 bits BAS
    // (les tests vont de `c & 1` à `c & 128`), ce qui est le comportement x86 —
    // PF ne regarde que l'octet de poids faible.
    // omitted: les deux pclog de débogage (`znp8 b1`, `znp16 65b1`), sortie pure.
    internal static void makeznptable()
    {
        int c, d;
        for (c = 0; c < 256; c++)
        {
                d = 0;
                if ((c & 1) != 0) d++;
                if ((c & 2) != 0) d++;
                if ((c & 4) != 0) d++;
                if ((c & 8) != 0) d++;
                if ((c & 16) != 0) d++;
                if ((c & 32) != 0) d++;
                if ((c & 64) != 0) d++;
                if ((c & 128) != 0) d++;
                if ((d & 1) != 0)
                        znptable8[c] = 0;
                else
                        znptable8[c] = (uint8_t)P_FLAG;
                if (c == 0)
                        znptable8[c] |= (uint8_t)Z_FLAG;
                if ((c & 0x80) != 0)
                        znptable8[c] |= (uint8_t)N_FLAG;
        }
        for (c = 0; c < 65536; c++)
        {
                d = 0;
                if ((c & 1) != 0) d++;
                if ((c & 2) != 0) d++;
                if ((c & 4) != 0) d++;
                if ((c & 8) != 0) d++;
                if ((c & 16) != 0) d++;
                if ((c & 32) != 0) d++;
                if ((c & 64) != 0) d++;
                if ((c & 128) != 0) d++;
                if ((d & 1) != 0)
                        znptable16[c] = 0;
                else
                        znptable16[c] = P_FLAG;
                if (c == 0)
                        znptable16[c] |= Z_FLAG;
                if ((c & 0x8000) != 0)
                        znptable16[c] |= N_FLAG;
        }
    }

    // pcem: 808x.c:748-756
    private static void setznp8(uint8_t val)
    {
        cpu_state.flags &= unchecked((uint16_t)~0xC4);
        cpu_state.flags |= znptable8[val];
    }

    private static void setznp16(uint16_t val)
    {
        cpu_state.flags &= unchecked((uint16_t)~0xC4);
        cpu_state.flags |= znptable16[val];
    }

    // pcem: 808x.c:758-819
    private static void setadd8(uint8_t a, uint8_t b)
    {
        uint16_t c = (uint16_t)(a + b);
        cpu_state.flags &= unchecked((uint16_t)~0x8D5);
        cpu_state.flags |= znptable8[c & 0xFF];
        if ((c & 0x100) != 0)
                cpu_state.flags |= C_FLAG;
        if (((a ^ b) & 0x80) == 0 && ((a ^ c) & 0x80) != 0)
                cpu_state.flags |= V_FLAG;
        if ((((a & 0xF) + (b & 0xF)) & 0x10) != 0)
                cpu_state.flags |= A_FLAG;
    }

    private static void setadd8nc(uint8_t a, uint8_t b)
    {
        uint16_t c = (uint16_t)(a + b);
        cpu_state.flags &= unchecked((uint16_t)~0x8D4);
        cpu_state.flags |= znptable8[c & 0xFF];
        if (((a ^ b) & 0x80) == 0 && ((a ^ c) & 0x80) != 0)
                cpu_state.flags |= V_FLAG;
        if ((((a & 0xF) + (b & 0xF)) & 0x10) != 0)
                cpu_state.flags |= A_FLAG;
    }

    private static void setadc8(uint8_t a, uint8_t b)
    {
        uint16_t c = (uint16_t)(a + b + tempc);
        cpu_state.flags &= unchecked((uint16_t)~0x8D5);
        cpu_state.flags |= znptable8[c & 0xFF];
        if ((c & 0x100) != 0)
                cpu_state.flags |= C_FLAG;
        if (((a ^ b) & 0x80) == 0 && ((a ^ c) & 0x80) != 0)
                cpu_state.flags |= V_FLAG;
        if ((((a & 0xF) + (b & 0xF)) & 0x10) != 0)
                cpu_state.flags |= A_FLAG;
    }

    private static void setadd16(uint16_t a, uint16_t b)
    {
        uint32_t c = (uint32_t)a + (uint32_t)b;
        cpu_state.flags &= unchecked((uint16_t)~0x8D5);
        cpu_state.flags |= znptable16[c & 0xFFFF];
        if ((c & 0x10000) != 0)
                cpu_state.flags |= C_FLAG;
        if (((a ^ b) & 0x8000) == 0 && ((a ^ c) & 0x8000) != 0)
                cpu_state.flags |= V_FLAG;
        if ((((a & 0xF) + (b & 0xF)) & 0x10) != 0)
                cpu_state.flags |= A_FLAG;
    }

    private static void setadd16nc(uint16_t a, uint16_t b)
    {
        uint32_t c = (uint32_t)a + (uint32_t)b;
        cpu_state.flags &= unchecked((uint16_t)~0x8D4);
        cpu_state.flags |= znptable16[c & 0xFFFF];
        if (((a ^ b) & 0x8000) == 0 && ((a ^ c) & 0x8000) != 0)
                cpu_state.flags |= V_FLAG;
        if ((((a & 0xF) + (b & 0xF)) & 0x10) != 0)
                cpu_state.flags |= A_FLAG;
    }

    private static void setadc16(uint16_t a, uint16_t b)
    {
        uint32_t c = (uint32_t)a + (uint32_t)b + (uint32_t)tempc;
        cpu_state.flags &= unchecked((uint16_t)~0x8D5);
        cpu_state.flags |= znptable16[c & 0xFFFF];
        if ((c & 0x10000) != 0)
                cpu_state.flags |= C_FLAG;
        if (((a ^ b) & 0x8000) == 0 && ((a ^ c) & 0x8000) != 0)
                cpu_state.flags |= V_FLAG;
        if ((((a & 0xF) + (b & 0xF)) & 0x10) != 0)
                cpu_state.flags |= A_FLAG;
    }

    // pcem: 808x.c:821-884
    private static void setsub8(uint8_t a, uint8_t b)
    {
        uint16_t c = (uint16_t)(a - b);
        cpu_state.flags &= unchecked((uint16_t)~0x8D5);
        cpu_state.flags |= znptable8[c & 0xFF];
        if ((c & 0x100) != 0)
                cpu_state.flags |= C_FLAG;
        if (((a ^ b) & (a ^ c) & 0x80) != 0)
                cpu_state.flags |= V_FLAG;
        if ((((a & 0xF) - (b & 0xF)) & 0x10) != 0)
                cpu_state.flags |= A_FLAG;
    }

    private static void setsub8nc(uint8_t a, uint8_t b)
    {
        uint16_t c = (uint16_t)(a - b);
        cpu_state.flags &= unchecked((uint16_t)~0x8D4);
        cpu_state.flags |= znptable8[c & 0xFF];
        if (((a ^ b) & (a ^ c) & 0x80) != 0)
                cpu_state.flags |= V_FLAG;
        if ((((a & 0xF) - (b & 0xF)) & 0x10) != 0)
                cpu_state.flags |= A_FLAG;
    }

    private static void setsbc8(uint8_t a, uint8_t b)
    {
        uint16_t c = (uint16_t)(a - (b + tempc));
        cpu_state.flags &= unchecked((uint16_t)~0x8D5);
        cpu_state.flags |= znptable8[c & 0xFF];
        if ((c & 0x100) != 0)
                cpu_state.flags |= C_FLAG;
        if (((a ^ b) & (a ^ c) & 0x80) != 0)
                cpu_state.flags |= V_FLAG;
        if ((((a & 0xF) - (b & 0xF)) & 0x10) != 0)
                cpu_state.flags |= A_FLAG;
    }

    private static void setsub16(uint16_t a, uint16_t b)
    {
        uint32_t c = (uint32_t)a - (uint32_t)b;
        cpu_state.flags &= unchecked((uint16_t)~0x8D5);
        cpu_state.flags |= znptable16[c & 0xFFFF];
        if ((c & 0x10000) != 0)
                cpu_state.flags |= C_FLAG;
        if (((a ^ b) & (a ^ c) & 0x8000) != 0)
                cpu_state.flags |= V_FLAG;
        if ((((a & 0xF) - (b & 0xF)) & 0x10) != 0)
                cpu_state.flags |= A_FLAG;
    }

    private static void setsub16nc(uint16_t a, uint16_t b)
    {
        uint32_t c = (uint32_t)a - (uint32_t)b;
        cpu_state.flags &= unchecked((uint16_t)~0x8D4);
        cpu_state.flags |= (uint16_t)(znptable16[c & 0xFFFF] & ~4);
        cpu_state.flags |= (uint16_t)(znptable8[c & 0xFF] & 4);
        if (((a ^ b) & (a ^ c) & 0x8000) != 0)
                cpu_state.flags |= V_FLAG;
        if ((((a & 0xF) - (b & 0xF)) & 0x10) != 0)
                cpu_state.flags |= A_FLAG;
    }

    private static void setsbc16(uint16_t a, uint16_t b)
    {
        uint32_t c = (uint32_t)a - ((uint32_t)b + (uint32_t)tempc);
        cpu_state.flags &= unchecked((uint16_t)~0x8D5);
        cpu_state.flags |= (uint16_t)(znptable16[c & 0xFFFF] & ~4);
        cpu_state.flags |= (uint16_t)(znptable8[c & 0xFF] & 4);
        if ((c & 0x10000) != 0)
                cpu_state.flags |= C_FLAG;
        if (((a ^ b) & (a ^ c) & 0x8000) != 0)
                cpu_state.flags |= V_FLAG;
        if ((((a & 0xF) - (b & 0xF)) & 0x10) != 0)
                cpu_state.flags |= A_FLAG;
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
        makeznptable();
        makemod1table();
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
        uint8_t temp, temp2;
        uint16_t addr, tempw, tempw2;
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
                case 0x00: /*ADD 8,reg*/
                        fetchea();
                        temp = geteab();
                        setadd8(temp, getr8(cpu_reg));
                        temp += getr8(cpu_reg);
                        seteab(temp);
                        cycles -= ((cpu_mod == 3) ? 3 : 24);
                        break;
                case 0x01: /*ADD 16,reg*/
                        fetchea();
                        tempw = geteaw();
                        setadd16(tempw, cpu_state.regs[cpu_reg].w);
                        tempw += cpu_state.regs[cpu_reg].w;
                        seteaw(tempw);
                        cycles -= ((cpu_mod == 3) ? 3 : 24);
                        break;
                case 0x02: /*ADD cpu_reg,8*/
                        fetchea();
                        temp = geteab();
                        setadd8(getr8(cpu_reg), temp);
                        setr8(cpu_reg, (uint8_t)(getr8(cpu_reg) + temp));
                        cycles -= ((cpu_mod == 3) ? 3 : 13);
                        break;
                case 0x03: /*ADD cpu_reg,16*/
                        fetchea();
                        tempw = geteaw();
                        setadd16(cpu_state.regs[cpu_reg].w, tempw);
                        cpu_state.regs[cpu_reg].w += tempw;
                        cycles -= ((cpu_mod == 3) ? 3 : 13);
                        break;
                case 0x04: /*ADD AL,#8*/
                        temp = FETCH();
                        setadd8(AL, temp);
                        AL += temp;
                        cycles -= 4;
                        break;
                case 0x05: /*ADD AX,#16*/
                        tempw = getword();
                        setadd16(AX, tempw);
                        AX += tempw;
                        cycles -= 4;
                        break;

                case 0x06: /*PUSH ES*/
                        if (cpu_state.ssegs != 0)
                                ss = oldss;
                        writememw(ss, ((uint32_t)(SP - 2) & 0xFFFF), ES);
                        SP -= 2;
                        cycles -= 14;
                        break;
                case 0x07: /*POP ES*/
                        if (cpu_state.ssegs != 0)
                                ss = oldss;
                        tempw = readmemw(ss, SP);
                        x86seg_c.loadseg(tempw, cpu_state.seg_es);
                        SP += 2;
                        cycles -= 12;
                        break;

                case 0x08: /*OR 8,reg*/
                        fetchea();
                        temp = geteab();
                        temp |= getr8(cpu_reg);
                        setznp8(temp);
                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG | A_FLAG));
                        seteab(temp);
                        cycles -= ((cpu_mod == 3) ? 3 : 24);
                        break;
                case 0x09: /*OR 16,reg*/
                        fetchea();
                        tempw = geteaw();
                        tempw |= cpu_state.regs[cpu_reg].w;
                        setznp16(tempw);
                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG | A_FLAG));
                        seteaw(tempw);
                        cycles -= ((cpu_mod == 3) ? 3 : 24);
                        break;
                case 0x0A: /*OR cpu_reg,8*/
                        fetchea();
                        temp = geteab();
                        temp |= getr8(cpu_reg);
                        setznp8(temp);
                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG | A_FLAG));
                        setr8(cpu_reg, temp);
                        cycles -= ((cpu_mod == 3) ? 3 : 13);
                        break;
                case 0x0B: /*OR cpu_reg,16*/
                        fetchea();
                        tempw = geteaw();
                        tempw |= cpu_state.regs[cpu_reg].w;
                        setznp16(tempw);
                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG | A_FLAG));
                        cpu_state.regs[cpu_reg].w = tempw;
                        cycles -= ((cpu_mod == 3) ? 3 : 13);
                        break;
                case 0x0C: /*OR AL,#8*/
                        AL |= FETCH();
                        setznp8(AL);
                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG | A_FLAG));
                        cycles -= 4;
                        break;
                case 0x0D: /*OR AX,#16*/
                        AX |= getword();
                        setznp16(AX);
                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG | A_FLAG));
                        cycles -= 4;
                        break;

                case 0x0E: /*PUSH CS*/
                        if (cpu_state.ssegs != 0)
                                ss = oldss;
                        writememw(ss, ((uint32_t)(SP - 2) & 0xFFFF), CS);
                        SP -= 2;
                        cycles -= 14;
                        break;
                case 0x0F: /*POP CS - 8088/8086 only*/
                        if (cpu_state.ssegs != 0)
                                ss = oldss;
                        tempw = readmemw(ss, SP);
                        x86seg_c.loadseg(tempw, cpu_state.seg_cs);
                        SP += 2;
                        cycles -= 12;
                        break;

                case 0x10: /*ADC 8,reg*/
                        fetchea();
                        temp = geteab();
                        temp2 = getr8(cpu_reg);
                        setadc8(temp, temp2);
                        temp += (uint8_t)(temp2 + tempc);
                        seteab(temp);
                        cycles -= ((cpu_mod == 3) ? 3 : 24);
                        break;
                case 0x11: /*ADC 16,reg*/
                        fetchea();
                        tempw = geteaw();
                        tempw2 = cpu_state.regs[cpu_reg].w;
                        setadc16(tempw, tempw2);
                        tempw += (uint16_t)(tempw2 + tempc);
                        seteaw(tempw);
                        cycles -= ((cpu_mod == 3) ? 3 : 24);
                        break;
                case 0x12: /*ADC cpu_reg,8*/
                        fetchea();
                        temp = geteab();
                        setadc8(getr8(cpu_reg), temp);
                        setr8(cpu_reg, (uint8_t)(getr8(cpu_reg) + temp + tempc));
                        cycles -= ((cpu_mod == 3) ? 3 : 13);
                        break;
                case 0x13: /*ADC cpu_reg,16*/
                        fetchea();
                        tempw = geteaw();
                        setadc16(cpu_state.regs[cpu_reg].w, tempw);
                        cpu_state.regs[cpu_reg].w += (uint16_t)(tempw + tempc);
                        cycles -= ((cpu_mod == 3) ? 3 : 13);
                        break;
                case 0x14: /*ADC AL,#8*/
                        tempw = FETCH();
                        setadc8(AL, (uint8_t)tempw);
                        AL += (uint8_t)(tempw + tempc);
                        cycles -= 4;
                        break;
                case 0x15: /*ADC AX,#16*/
                        tempw = getword();
                        setadc16(AX, tempw);
                        AX += (uint16_t)(tempw + tempc);
                        cycles -= 4;
                        break;

                case 0x16: /*PUSH SS*/
                        if (cpu_state.ssegs != 0)
                                ss = oldss;
                        writememw(ss, ((uint32_t)(SP - 2) & 0xFFFF), SS);
                        SP -= 2;
                        cycles -= 14;
                        break;
                case 0x17: /*POP SS*/
                        if (cpu_state.ssegs != 0)
                                ss = oldss;
                        tempw = readmemw(ss, SP);
                        x86seg_c.loadseg(tempw, cpu_state.seg_ss);
                        SP += 2;
                        noint = 1;
                        cycles -= 12;
                        break;

                case 0x18: /*SBB 8,reg*/
                        fetchea();
                        temp = geteab();
                        temp2 = getr8(cpu_reg);
                        setsbc8(temp, temp2);
                        temp -= (uint8_t)(temp2 + tempc);
                        seteab(temp);
                        cycles -= ((cpu_mod == 3) ? 3 : 24);
                        break;
                case 0x19: /*SBB 16,reg*/
                        fetchea();
                        tempw = geteaw();
                        tempw2 = cpu_state.regs[cpu_reg].w;
                        setsbc16(tempw, tempw2);
                        tempw -= (uint16_t)(tempw2 + tempc);
                        seteaw(tempw);
                        cycles -= ((cpu_mod == 3) ? 3 : 24);
                        break;
                case 0x1A: /*SBB cpu_reg,8*/
                        fetchea();
                        temp = geteab();
                        setsbc8(getr8(cpu_reg), temp);
                        setr8(cpu_reg, (uint8_t)(getr8(cpu_reg) - (temp + tempc)));
                        cycles -= ((cpu_mod == 3) ? 3 : 13);
                        break;
                case 0x1B: /*SBB cpu_reg,16*/
                        fetchea();
                        tempw = geteaw();
                        tempw2 = cpu_state.regs[cpu_reg].w;
                        setsbc16(tempw2, tempw);
                        tempw2 -= (uint16_t)(tempw + tempc);
                        cpu_state.regs[cpu_reg].w = tempw2;
                        cycles -= ((cpu_mod == 3) ? 3 : 13);
                        break;
                case 0x1C: /*SBB AL,#8*/
                        temp = FETCH();
                        setsbc8(AL, temp);
                        AL -= (uint8_t)(temp + tempc);
                        cycles -= 4;
                        break;
                case 0x1D: /*SBB AX,#16*/
                        tempw = getword();
                        setsbc16(AX, tempw);
                        AX -= (uint16_t)(tempw + tempc);
                        cycles -= 4;
                        break;

                case 0x1E: /*PUSH DS*/
                        if (cpu_state.ssegs != 0)
                                ss = oldss;
                        writememw(ss, ((uint32_t)(SP - 2) & 0xFFFF), DS);
                        SP -= 2;
                        cycles -= 14;
                        break;
                case 0x1F: /*POP DS*/
                        if (cpu_state.ssegs != 0)
                                ss = oldss;
                        tempw = readmemw(ss, SP);
                        x86seg_c.loadseg(tempw, cpu_state.seg_ds);
                        if (cpu_state.ssegs != 0)
                                oldds = ds;
                        SP += 2;
                        cycles -= 12;
                        break;

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
