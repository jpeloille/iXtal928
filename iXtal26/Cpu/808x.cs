// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/cpu/808x.c
//         (36-260, 340-455, 456-520, 662-702, 748-886, 886-906, 1222-1340, 3902-3996)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — M1.1 : accesseurs, préfetch, EA, tables et helpers de flags,
//         resetx86, boucle execx86, opcodes 0x00-0x7F (prefixes de segment
//         compris).
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

// CS1717 : `ds = ss = ss;` (808x.c:1737, préfixe SS:) et `ds = ss = ds;`
// (808x.c:1794, préfixe DS:) affectent une variable à elle-même. C'est
// intentionnel dans PCem — la chaîne d'affectation sauvegarde le segment courant
// dans les DEUX alias avant l'override — et la forme est conservée telle quelle.
#pragma warning disable CS1717

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

    // pcem: 808x.c:33 — nmi_auto_clear. DÉ-OMISSION : le chemin d'interruption de
    // exec386 le lit (386.c:245-248), et il n'était pas là. Personne ne le pose à 1
    // dans l'arbre lié — `grep -rn 'nmi_auto_clear = 1' pcem-dev/src` est vide — donc
    // sa branche est morte ; transcrite quand même, parce que la structure du C est ce
    // qui se lit, et qu'une branche absente se remarque moins qu'une branche morte.
    internal static int nmi_auto_clear;
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

    internal static uint16_t Mod1Add(int which, int rm)
    {
        var idx = mod1add[which, rm];
        return idx == MOD1_ZERO ? (uint16_t)0 : cpu_state.regs[idx].w;
    }

    private static uint32_t Mod1Seg(int rm) => mod1seg[rm] == MOD1_DS ? ds : ss;

    /// <summary>`mod1seg[rm] == &ss` du C (386_dynarec.c:104). Le C compare des
    /// ADRESSES de segment ; ici on compare l'index, qui est ce que la table stocke.
    /// Ouvert en A2.2d : fetch_ea_16_long en a besoin, et en C `mod1add`/`mod1seg`
    /// sont des globaux NON statiques de 808x.c déclarés dans x86.h (x86.h:213-214),
    /// donc partagés avec 386_dynarec.c. Les ouvrir RESTAURE la parité.</summary>
    internal static bool Mod1IsSS(int rm) => mod1seg[rm] == MOD1_SS;

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

    // ===== rep() : instructions de chaîne (808x.c:908-1215) =====
    // pcem: 808x.c:55
    private static bool IRQTEST => (cpu_state.flags & I_FLAG) != 0 && (pic.pic_.pend & ~pic.pic_.mask) != 0 && noint == 0;

    // pcem: 808x.c:908
    internal static int firstrepcycle = 1;

// CS0162 : les trois `break;` qui suivent un `goto startrep;` (808x.c:933, 940, 947)
// sont inatteignables. Ils sont conservés — R6(a) veut une contrepartie par ligne de
// C — et l'avertissement est neutralisé sur la seule rep().
#pragma warning disable CS0162

    // pcem: 808x.c:910-1215
    private static void rep(int fv)
    {
        uint8_t temp;
        int c = CX;
        uint8_t temp2;
        uint16_t tempw, tempw2;
        uint16_t ipc = (uint16_t)cpu_state.oldpc;
        int changeds = 0;
        uint32_t oldds = ds;
startrep:
        temp = FETCH();

        switch (temp)
        {
        case 0x08:
                cpu_state.pc = (uint32_t)(ipc + 1);
                cycles -= 2;
                FETCHCLEAR();
                break;
        case 0x26: /*ES:*/
                oldds = ds;
                ds = es;
                changeds = 1;
                cycles -= 2;
                goto startrep;
                break;
        case 0x2E: /*CS:*/
                oldds = ds;
                ds = cs;
                changeds = 1;
                cycles -= 2;
                goto startrep;
                break;
        case 0x36: /*SS:*/
                oldds = ds;
                ds = ss;
                changeds = 1;
                cycles -= 2;
                goto startrep;
                break;
        case 0x6E: /*REP OUTSB*/
                if (c > 0)
                {
                        temp2 = readmemb(ds + SI);
                        io.outb(DX, temp2);
                        if ((cpu_state.flags & D_FLAG) != 0)
                                SI--;
                        else
                                SI++;
                        c--;
                        cycles -= 5;
                }
                if (c > 0)
                {
                        firstrepcycle = 0;
                        cpu_state.pc = ipc;
                        if (cpu_state.ssegs != 0)
                                cpu_state.ssegs++;
                        FETCHCLEAR();
                }
                else
                        firstrepcycle = 1;
                break;
        case 0xA4: /*REP MOVSB*/
                // pcem bug, reproduced: pas de `memcycs = 0;` en tête de boucle, à la
                // différence de 0xA5/0xA6/0xA7/0xAA/0xAB — le FETCHADD ci-dessous
                // consomme donc un memcycs jamais remis à zéro.
                while (c > 0 && !IRQTEST)
                {
                        temp2 = readmemb(ds + SI);
                        writememb(es + DI, temp2);
                        if ((cpu_state.flags & D_FLAG) != 0)
                        {
                                DI--;
                                SI--;
                        }
                        else
                        {
                                DI++;
                                SI++;
                        }
                        c--;
                        cycles -= 17;
                        clockhardware();
                        FETCHADD(17 - memcycs);
                }
                if (IRQTEST && c > 0)
                        cpu_state.pc = ipc;
                break;
        case 0xA5: /*REP MOVSW*/
                while (c > 0 && !IRQTEST)
                {
                        memcycs = 0;
                        tempw = readmemw(ds, SI);
                        writememw(es, DI, tempw);
                        if ((cpu_state.flags & D_FLAG) != 0)
                        {
                                DI -= 2;
                                SI -= 2;
                        }
                        else
                        {
                                DI += 2;
                                SI += 2;
                        }
                        c--;
                        cycles -= 17;
                        clockhardware();
                        FETCHADD(17 - memcycs);
                }
                if (IRQTEST && c > 0)
                        cpu_state.pc = ipc;
                break;
        case 0xA6: /*REP CMPSB*/
                if (fv != 0)
                        cpu_state.flags |= Z_FLAG;
                else
                        cpu_state.flags &= unchecked((uint16_t)~Z_FLAG);
                while ((c > 0) && (fv == (((cpu_state.flags & Z_FLAG) != 0) ? 1 : 0)) && !IRQTEST)
                {
                        memcycs = 0;
                        temp = readmemb(ds + SI);
                        temp2 = readmemb(es + DI);
                        if ((cpu_state.flags & D_FLAG) != 0)
                        {
                                DI--;
                                SI--;
                        }
                        else
                        {
                                DI++;
                                SI++;
                        }
                        c--;
                        cycles -= 30;
                        setsub8(temp, temp2);
                        clockhardware();
                        FETCHADD(30 - memcycs);
                }
                if (IRQTEST && c > 0 && (fv == (((cpu_state.flags & Z_FLAG) != 0) ? 1 : 0)))
                        cpu_state.pc = ipc;
                break;
        case 0xA7: /*REP CMPSW*/
                if (fv != 0)
                        cpu_state.flags |= Z_FLAG;
                else
                        cpu_state.flags &= unchecked((uint16_t)~Z_FLAG);
                while ((c > 0) && (fv == (((cpu_state.flags & Z_FLAG) != 0) ? 1 : 0)) && !IRQTEST)
                {
                        memcycs = 0;
                        tempw = readmemw(ds, SI);
                        tempw2 = readmemw(es, DI);
                        if ((cpu_state.flags & D_FLAG) != 0)
                        {
                                DI -= 2;
                                SI -= 2;
                        }
                        else
                        {
                                DI += 2;
                                SI += 2;
                        }
                        c--;
                        cycles -= 30;
                        setsub16(tempw, tempw2);
                        clockhardware();
                        FETCHADD(30 - memcycs);
                }
                if (IRQTEST && c > 0 && (fv == (((cpu_state.flags & Z_FLAG) != 0) ? 1 : 0)))
                        cpu_state.pc = ipc;
                break;
        case 0xAA: /*REP STOSB*/
                while (c > 0 && !IRQTEST)
                {
                        memcycs = 0;
                        writememb(es + DI, AL);
                        if ((cpu_state.flags & D_FLAG) != 0)
                                DI--;
                        else
                                DI++;
                        c--;
                        cycles -= 10;
                        clockhardware();
                        FETCHADD(10 - memcycs);
                }
                if (IRQTEST && c > 0)
                        cpu_state.pc = ipc;
                break;
        case 0xAB: /*REP STOSW*/
                while (c > 0 && !IRQTEST)
                {
                        memcycs = 0;
                        writememw(es, DI, AX);
                        if ((cpu_state.flags & D_FLAG) != 0)
                                DI -= 2;
                        else
                                DI += 2;
                        c--;
                        cycles -= 10;
                        clockhardware();
                        FETCHADD(10 - memcycs);
                }
                if (IRQTEST && c > 0)
                        cpu_state.pc = ipc;
                break;
        case 0xAC: /*REP LODSB*/
                if (c > 0)
                {
                        temp2 = readmemb(ds + SI);
                        if ((cpu_state.flags & D_FLAG) != 0)
                                SI--;
                        else
                                SI++;
                        c--;
                        cycles -= 4;
                }
                if (c > 0)
                {
                        firstrepcycle = 0;
                        cpu_state.pc = ipc;
                        if (cpu_state.ssegs != 0)
                                cpu_state.ssegs++;
                        FETCHCLEAR();
                }
                else
                        firstrepcycle = 1;
                break;
        case 0xAD: /*REP LODSW*/
                if (c > 0)
                {
                        tempw2 = readmemw(ds, SI);
                        if ((cpu_state.flags & D_FLAG) != 0)
                                SI -= 2;
                        else
                                SI += 2;
                        c--;
                        cycles -= 4;
                }
                if (c > 0)
                {
                        firstrepcycle = 0;
                        cpu_state.pc = ipc;
                        if (cpu_state.ssegs != 0)
                                cpu_state.ssegs++;
                        FETCHCLEAR();
                }
                else
                        firstrepcycle = 1;
                break;
        case 0xAE: /*REP SCASB*/
                if (fv != 0)
                        cpu_state.flags |= Z_FLAG;
                else
                        cpu_state.flags &= unchecked((uint16_t)~Z_FLAG);
                if ((c > 0) && (fv == (((cpu_state.flags & Z_FLAG) != 0) ? 1 : 0)))
                {
                        temp2 = readmemb(es + DI);
                        setsub8(AL, temp2);
                        if ((cpu_state.flags & D_FLAG) != 0)
                                DI--;
                        else
                                DI++;
                        c--;
                        cycles -= 15;
                }
                if ((c > 0) && (fv == (((cpu_state.flags & Z_FLAG) != 0) ? 1 : 0)))
                {
                        cpu_state.pc = ipc;
                        firstrepcycle = 0;
                        if (cpu_state.ssegs != 0)
                                cpu_state.ssegs++;
                        FETCHCLEAR();
                }
                else
                        firstrepcycle = 1;
                break;
        case 0xAF: /*REP SCASW*/
                if (fv != 0)
                        cpu_state.flags |= Z_FLAG;
                else
                        cpu_state.flags &= unchecked((uint16_t)~Z_FLAG);
                if ((c > 0) && (fv == (((cpu_state.flags & Z_FLAG) != 0) ? 1 : 0)))
                {
                        tempw = readmemw(es, DI);
                        setsub16(AX, tempw);
                        if ((cpu_state.flags & D_FLAG) != 0)
                                DI -= 2;
                        else
                                DI += 2;
                        c--;
                        cycles -= 15;
                }
                if ((c > 0) && (fv == (((cpu_state.flags & Z_FLAG) != 0) ? 1 : 0)))
                {
                        cpu_state.pc = ipc;
                        firstrepcycle = 0;
                        if (cpu_state.ssegs != 0)
                                cpu_state.ssegs++;
                        FETCHCLEAR();
                }
                else
                        firstrepcycle = 1;
                break;
        default:
                cpu_state.pc = (uint32_t)(ipc + 1);
                cycles -= 20;
                FETCHCLEAR();
                // CS8070: en C, `default:` sort du switch par sa fin ; C# l'interdit.
                break;
        }
        CX = (uint16_t)c;
        if (changeds != 0)
                ds = oldds;
        if (IRQTEST)
                takeint = 1;
    }
#pragma warning restore CS0162

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
        // pcem: 808x.c:675-676 — depuis M16 (étape 6). Omis jusque-là parce que l'oracle
        // l'enveloppait à vide, `cpus` y étant NUL ; cpu_set() tourne désormais des deux
        // côtés. Au-delà de 8 MHz c'est lui qui remet le préfetch au coût de la RAM à
        // chaque reset — le coût de la ROM, rspeed / 1e6, n'y survit plus.
        cpu_c.cpu_cache_int_enabled = 0;
        cpu_c.cpu_update_waitstates();
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
        // omitted: x87_reset() et codegen_reset() — 8087 et dynarec.
        // omitted: cpu_set_edx() (:698) — inerte ICI : EDX vient d'être mis à zéro, et
        //   edx_reset vaut 0 dans les trois tables du dépôt (cpu_tables.cs).
        mem.mmu_perm = 4;
        x86seg_c.x86seg_reset();
        x86_was_reset = 1;
        cpu_state.smbase = 0x30000;
    }

    // pcem: 808x.c:706-746 — LE RESET DOUX, ET C'EST LA SEULE SORTIE DU MODE PROTÉGÉ.
    //
    // Un 286 ne sait pas revenir en mode réel : LMSW ne peut pas effacer le bit qu'il
    // a posé (386_ops_0f.cs). La seule issue est la ligne de reset, et sur un AT c'est
    // le 8042 qui la tient — commande 0xFE en 0x64. Le BIOS s'en sert après avoir
    // dimensionné la mémoire haute, et il retrouve son état par LOADALL.
    //
    // CE QU'IL NE FAIT PAS, ET LA DIFFÉRENCE EST POUR LE 286 : il ne vide les registres
    // généraux QUE si is386. Sur un 286 EAX..ESP TRAVERSENT le reset — c'est ainsi que
    // le BIOS se souvient d'où il en était. resetx86(), lui, les vide sans condition.
    // Il ne refait pas non plus makeznptable, makemod1table, resetreadlookup, mmu_perm
    // ni smbase : ces cinq-là sont du reset DUR.
    internal static void softresetx86()
    {
        use32 = 0;
        stack32 = 0;
        cpu_cur_status = 0;
        msw = 0;
        if (is486 != 0)
                cr0 = 1 << 30;
        else
                cr0 = 0;
        // pcem: 808x.c:719-720 — voir resetx86. C'est ce reset-ci que le 8042 déclenche
        // (port 0x64, 0xFE) pour sortir du mode protégé.
        cpu_c.cpu_cache_int_enabled = 0;
        cpu_c.cpu_update_waitstates();
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
        cpu_state.flags = 2;
        idt.@base = 0;
        if (is386 != 0)
        {
                idt.limit = 0x03FF;
                EAX = EBX = ECX = EDX = ESI = EDI = EBP = ESP = 0;
        }
        else
                idt.limit = 0xFFFF;
        x86seg_c.x86seg_reset();
        mem.flushmmucache();
        x86_was_reset = 1;
        FETCHCLEAR();
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
        // DEVIATION: alias de prologue. RyuJIT refuse tout inlining dans cette méthode
        //   (fginline.cpp : lvaCount >= 0,9 x JitMaxLocalsToTrack, franchi par les
        //   temporaires du switch avant fgInline) : chaque `cycles -= n` devenait un
        //   `call get_cycles`, 898 appels au total. Ces ref locales masquent les
        //   propriétés ref de x86.cs pour la durée de la méthode — même référent, même
        //   mémoire, aucune ligne du switch ne change. VERIFICATION.md § M5.1.
        ref int cycles = ref x86.cycles;
        ref int8_t cpu_mod = ref x86.cpu_mod;
        ref int8_t cpu_reg = ref x86.cpu_reg;
        ref uint32_t cs = ref x86.cs, ds = ref x86.ds, es = ref x86.es, ss = ref x86.ss;
        ref uint16_t CS = ref x86.CS, DS = ref x86.DS, ES = ref x86.ES, SS = ref x86.SS;
        ref uint16_t AX = ref x86.AX, CX = ref x86.CX, DX = ref x86.DX, BX = ref x86.BX;
        ref uint16_t SP = ref x86.SP, SI = ref x86.SI, DI = ref x86.DI;
        ref uint8_t AL = ref x86.AL, AH = ref x86.AH, CL = ref x86.CL, CH = ref x86.CH;
        ref uint8_t DL = ref x86.DL, DH = ref x86.DH, BL = ref x86.BL, BH = ref x86.BH;

        uint8_t temp, temp2;
        uint16_t addr, tempw, tempw2;
        int8_t offset;
        int tempi, trap, tempws, c;
        uint16_t tempw3, tempw4;
        uint32_t templ;

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

                case 0x20: /*AND 8,reg*/
                        fetchea();
                        temp = geteab();
                        temp &= getr8(cpu_reg);
                        setznp8(temp);
                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG | A_FLAG));
                        seteab(temp);
                        cycles -= ((cpu_mod == 3) ? 3 : 24);
                        break;
                case 0x21: /*AND 16,reg*/
                        fetchea();
                        tempw = geteaw();
                        tempw &= cpu_state.regs[cpu_reg].w;
                        setznp16(tempw);
                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG | A_FLAG));
                        seteaw(tempw);
                        cycles -= ((cpu_mod == 3) ? 3 : 24);
                        break;
                case 0x22: /*AND cpu_reg,8*/
                        fetchea();
                        temp = geteab();
                        temp &= getr8(cpu_reg);
                        setznp8(temp);
                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG | A_FLAG));
                        setr8(cpu_reg, temp);
                        cycles -= ((cpu_mod == 3) ? 3 : 13);
                        break;
                case 0x23: /*AND cpu_reg,16*/
                        fetchea();
                        tempw = geteaw();
                        tempw &= cpu_state.regs[cpu_reg].w;
                        setznp16(tempw);
                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG | A_FLAG));
                        cpu_state.regs[cpu_reg].w = tempw;
                        cycles -= ((cpu_mod == 3) ? 3 : 13);
                        break;
                case 0x24: /*AND AL,#8*/
                        AL &= FETCH();
                        setznp8(AL);
                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG | A_FLAG));
                        cycles -= 4;
                        break;
                case 0x25: /*AND AX,#16*/
                        AX &= getword();
                        setznp16(AX);
                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG | A_FLAG));
                        cycles -= 4;
                        break;

                case 0x26: /*ES:*/
                        oldss = ss;
                        oldds = ds;
                        ds = ss = es;
                        cpu_state.ssegs = 2;
                        cycles -= 4;
                        goto opcodestart;

                case 0x27: /*DAA*/
                        if ((cpu_state.flags & A_FLAG) != 0 || ((AL & 0xF) > 9))
                        {
                                tempi = ((uint16_t)AL) + 6;
                                AL += 6;
                                cpu_state.flags |= A_FLAG;
                                if ((tempi & 0x100) != 0)
                                        cpu_state.flags |= C_FLAG;
                        }
                        if ((cpu_state.flags & C_FLAG) != 0 || (AL > 0x9F))
                        {
                                AL += 0x60;
                                cpu_state.flags |= C_FLAG;
                        }
                        setznp8(AL);
                        cycles -= 4;
                        break;

                case 0x28: /*SUB 8,reg*/
                        fetchea();
                        temp = geteab();
                        setsub8(temp, getr8(cpu_reg));
                        temp -= getr8(cpu_reg);
                        seteab(temp);
                        cycles -= ((cpu_mod == 3) ? 3 : 24);
                        break;
                case 0x29: /*SUB 16,reg*/
                        fetchea();
                        tempw = geteaw();
                        setsub16(tempw, cpu_state.regs[cpu_reg].w);
                        tempw -= cpu_state.regs[cpu_reg].w;
                        seteaw(tempw);
                        cycles -= ((cpu_mod == 3) ? 3 : 24);
                        break;
                case 0x2A: /*SUB cpu_reg,8*/
                        fetchea();
                        temp = geteab();
                        setsub8(getr8(cpu_reg), temp);
                        setr8(cpu_reg, (uint8_t)(getr8(cpu_reg) - temp));
                        cycles -= ((cpu_mod == 3) ? 3 : 13);
                        break;
                case 0x2B: /*SUB cpu_reg,16*/
                        fetchea();
                        tempw = geteaw();
                        setsub16(cpu_state.regs[cpu_reg].w, tempw);
                        cpu_state.regs[cpu_reg].w -= tempw;
                        cycles -= ((cpu_mod == 3) ? 3 : 13);
                        break;
                case 0x2C: /*SUB AL,#8*/
                        temp = FETCH();
                        setsub8(AL, temp);
                        AL -= temp;
                        cycles -= 4;
                        break;
                case 0x2D: /*SUB AX,#16*/
                        tempw = getword();
                        setsub16(AX, tempw);
                        AX -= tempw;
                        cycles -= 4;
                        break;

                case 0x2E: /*CS:*/
                        oldss = ss;
                        oldds = ds;
                        ds = ss = cs;
                        cpu_state.ssegs = 2;
                        cycles -= 4;
                        goto opcodestart;

                case 0x2F: /*DAS*/
                        if ((cpu_state.flags & A_FLAG) != 0 || ((AL & 0xF) > 9))
                        {
                                tempi = ((uint16_t)AL) - 6;
                                AL -= 6;
                                cpu_state.flags |= A_FLAG;
                                if ((tempi & 0x100) != 0)
                                        cpu_state.flags |= C_FLAG;
                        }
                        if ((cpu_state.flags & C_FLAG) != 0 || (AL > 0x9F))
                        {
                                AL -= 0x60;
                                cpu_state.flags |= C_FLAG;
                        }
                        setznp8(AL);
                        cycles -= 4;
                        break;

                case 0x30: /*XOR 8,reg*/
                        fetchea();
                        temp = geteab();
                        temp ^= getr8(cpu_reg);
                        setznp8(temp);
                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG | A_FLAG));
                        seteab(temp);
                        cycles -= ((cpu_mod == 3) ? 3 : 24);
                        break;
                case 0x31: /*XOR 16,reg*/
                        fetchea();
                        tempw = geteaw();
                        tempw ^= cpu_state.regs[cpu_reg].w;
                        setznp16(tempw);
                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG | A_FLAG));
                        seteaw(tempw);
                        cycles -= ((cpu_mod == 3) ? 3 : 24);
                        break;
                case 0x32: /*XOR cpu_reg,8*/
                        fetchea();
                        temp = geteab();
                        temp ^= getr8(cpu_reg);
                        setznp8(temp);
                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG | A_FLAG));
                        setr8(cpu_reg, temp);
                        cycles -= ((cpu_mod == 3) ? 3 : 13);
                        break;
                case 0x33: /*XOR cpu_reg,16*/
                        fetchea();
                        tempw = geteaw();
                        tempw ^= cpu_state.regs[cpu_reg].w;
                        setznp16(tempw);
                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG | A_FLAG));
                        cpu_state.regs[cpu_reg].w = tempw;
                        cycles -= ((cpu_mod == 3) ? 3 : 13);
                        break;
                case 0x34: /*XOR AL,#8*/
                        AL ^= FETCH();
                        setznp8(AL);
                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG | A_FLAG));
                        cycles -= 4;
                        break;
                case 0x35: /*XOR AX,#16*/
                        AX ^= getword();
                        setznp16(AX);
                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG | A_FLAG));
                        cycles -= 4;
                        break;

                case 0x36: /*SS:*/
                        oldss = ss;
                        oldds = ds;
                        ds = ss = ss;
                        cpu_state.ssegs = 2;
                        cycles -= 4;
                        goto opcodestart;

                case 0x37: /*AAA*/
                        if ((cpu_state.flags & A_FLAG) != 0 || ((AL & 0xF) > 9))
                        {
                                AL += 6;
                                AH++;
                                cpu_state.flags |= (A_FLAG | C_FLAG);
                        }
                        else
                                cpu_state.flags &= unchecked((uint16_t)~(A_FLAG | C_FLAG));
                        AL &= 0xF;
                        cycles -= 8;
                        break;

                case 0x38: /*CMP 8,reg*/
                        fetchea();
                        temp = geteab();
                        setsub8(temp, getr8(cpu_reg));
                        cycles -= ((cpu_mod == 3) ? 3 : 13);
                        break;
                case 0x39: /*CMP 16,reg*/
                        fetchea();
                        tempw = geteaw();
                        setsub16(tempw, cpu_state.regs[cpu_reg].w);
                        cycles -= ((cpu_mod == 3) ? 3 : 13);
                        break;
                case 0x3A: /*CMP cpu_reg,8*/
                        fetchea();
                        temp = geteab();
                        setsub8(getr8(cpu_reg), temp);
                        cycles -= ((cpu_mod == 3) ? 3 : 13);
                        break;
                case 0x3B: /*CMP cpu_reg,16*/
                        fetchea();
                        tempw = geteaw();
                        setsub16(cpu_state.regs[cpu_reg].w, tempw);
                        cycles -= ((cpu_mod == 3) ? 3 : 13);
                        break;
                case 0x3C: /*CMP AL,#8*/
                        temp = FETCH();
                        setsub8(AL, temp);
                        cycles -= 4;
                        break;
                case 0x3D: /*CMP AX,#16*/
                        tempw = getword();
                        setsub16(AX, tempw);
                        cycles -= 4;
                        break;

                case 0x3E: /*DS:*/
                        oldss = ss;
                        oldds = ds;
                        ds = ss = ds;
                        cpu_state.ssegs = 2;
                        cycles -= 4;
                        goto opcodestart;

                case 0x3F: /*AAS*/
                        if ((cpu_state.flags & A_FLAG) != 0 || ((AL & 0xF) > 9))
                        {
                                AL -= 6;
                                AH--;
                                cpu_state.flags |= (A_FLAG | C_FLAG);
                        }
                        else
                                cpu_state.flags &= unchecked((uint16_t)~(A_FLAG | C_FLAG));
                        AL &= 0xF;
                        cycles -= 8;
                        break;

                case 0x40:
                case 0x41:
                case 0x42:
                case 0x43: /*INC r16*/
                case 0x44:
                case 0x45:
                case 0x46:
                case 0x47:
                        setadd16nc(cpu_state.regs[opcode & 7].w, 1);
                        cpu_state.regs[opcode & 7].w++;
                        cycles -= 3;
                        break;
                case 0x48:
                case 0x49:
                case 0x4A:
                case 0x4B: /*DEC r16*/
                case 0x4C:
                case 0x4D:
                case 0x4E:
                case 0x4F:
                        setsub16nc(cpu_state.regs[opcode & 7].w, 1);
                        cpu_state.regs[opcode & 7].w--;
                        cycles -= 3;
                        break;

                case 0x50:
                case 0x51:
                case 0x52:
                case 0x53: /*PUSH r16*/
                case 0x54:
                case 0x55:
                case 0x56:
                case 0x57:
                        if (cpu_state.ssegs != 0)
                                ss = oldss;
                        SP -= 2;
                        writememw(ss, SP, cpu_state.regs[opcode & 7].w);
                        cycles -= 15;
                        break;

                case 0x58:
                case 0x59:
                case 0x5A:
                case 0x5B: /*POP r16*/
                case 0x5C:
                case 0x5D:
                case 0x5E:
                case 0x5F:
                        if (cpu_state.ssegs != 0)
                                ss = oldss;
                        SP += 2;
                        cpu_state.regs[opcode & 7].w = readmemw(ss, (uint16_t)((SP - 2) & 0xFFFF));
                        cycles -= 12;
                        break;

                case 0x60: /*JO alias*/
                case 0x70: /*JO*/
                        offset = (int8_t)FETCH();
                        if ((cpu_state.flags & V_FLAG) != 0)
                        {
                                cpu_state.pc = (uint32_t)(cpu_state.pc + offset);
                                cycles -= 12;
                                FETCHCLEAR();
                        }
                        cycles -= 4;
                        break;
                case 0x61: /*JNO alias*/
                case 0x71: /*JNO*/
                        offset = (int8_t)FETCH();
                        if ((cpu_state.flags & V_FLAG) == 0)
                        {
                                cpu_state.pc = (uint32_t)(cpu_state.pc + offset);
                                cycles -= 12;
                                FETCHCLEAR();
                        }
                        cycles -= 4;
                        break;
                case 0x62: /*JB alias*/
                case 0x72: /*JB*/
                        offset = (int8_t)FETCH();
                        if ((cpu_state.flags & C_FLAG) != 0)
                        {
                                cpu_state.pc = (uint32_t)(cpu_state.pc + offset);
                                cycles -= 12;
                                FETCHCLEAR();
                        }
                        cycles -= 4;
                        break;
                case 0x63: /*JNB alias*/
                case 0x73: /*JNB*/
                        offset = (int8_t)FETCH();
                        if ((cpu_state.flags & C_FLAG) == 0)
                        {
                                cpu_state.pc = (uint32_t)(cpu_state.pc + offset);
                                cycles -= 12;
                                FETCHCLEAR();
                        }
                        cycles -= 4;
                        break;
                case 0x64: /*JE alias*/
                case 0x74: /*JE*/
                        offset = (int8_t)FETCH();
                        if ((cpu_state.flags & Z_FLAG) != 0)
                        {
                                cpu_state.pc = (uint32_t)(cpu_state.pc + offset);
                                cycles -= 12;
                                FETCHCLEAR();
                        }
                        cycles -= 4;
                        break;
                case 0x65: /*JNE alias*/
                case 0x75: /*JNE*/
                        offset = (int8_t)FETCH();
                        cycles -= 4;
                        if ((cpu_state.flags & Z_FLAG) == 0)
                        {
                                cpu_state.pc = (uint32_t)(cpu_state.pc + offset);
                                cycles -= 12;
                                FETCHCLEAR();
                        }
                        break;
                case 0x66: /*JBE alias*/
                case 0x76: /*JBE*/
                        offset = (int8_t)FETCH();
                        if ((cpu_state.flags & (C_FLAG | Z_FLAG)) != 0)
                        {
                                cpu_state.pc = (uint32_t)(cpu_state.pc + offset);
                                cycles -= 12;
                                FETCHCLEAR();
                        }
                        cycles -= 4;
                        break;
                case 0x67: /*JNBE alias*/
                case 0x77: /*JNBE*/
                        offset = (int8_t)FETCH();
                        if ((cpu_state.flags & (C_FLAG | Z_FLAG)) == 0)
                        {
                                cpu_state.pc = (uint32_t)(cpu_state.pc + offset);
                                cycles -= 12;
                                FETCHCLEAR();
                        }
                        cycles -= 4;
                        break;
                case 0x68: /*JS alias*/
                case 0x78: /*JS*/
                        offset = (int8_t)FETCH();
                        if ((cpu_state.flags & N_FLAG) != 0)
                        {
                                cpu_state.pc = (uint32_t)(cpu_state.pc + offset);
                                cycles -= 12;
                                FETCHCLEAR();
                        }
                        cycles -= 4;
                        break;
                case 0x69: /*JNS alias*/
                case 0x79: /*JNS*/
                        offset = (int8_t)FETCH();
                        if ((cpu_state.flags & N_FLAG) == 0)
                        {
                                cpu_state.pc = (uint32_t)(cpu_state.pc + offset);
                                cycles -= 12;
                                FETCHCLEAR();
                        }
                        cycles -= 4;
                        break;
                case 0x6A: /*JP alias*/
                case 0x7A: /*JP*/
                        offset = (int8_t)FETCH();
                        if ((cpu_state.flags & P_FLAG) != 0)
                        {
                                cpu_state.pc = (uint32_t)(cpu_state.pc + offset);
                                cycles -= 12;
                                FETCHCLEAR();
                        }
                        cycles -= 4;
                        break;
                case 0x6B: /*JNP alias*/
                case 0x7B: /*JNP*/
                        offset = (int8_t)FETCH();
                        if ((cpu_state.flags & P_FLAG) == 0)
                        {
                                cpu_state.pc = (uint32_t)(cpu_state.pc + offset);
                                cycles -= 12;
                                FETCHCLEAR();
                        }
                        cycles -= 4;
                        break;
                case 0x6C: /*JL alias*/
                case 0x7C: /*JL*/
                        offset = (int8_t)FETCH();
                        temp = (uint8_t)(((cpu_state.flags & N_FLAG) != 0) ? 1 : 0);
                        temp2 = (uint8_t)(((cpu_state.flags & V_FLAG) != 0) ? 1 : 0);
                        if (temp != temp2)
                        {
                                cpu_state.pc = (uint32_t)(cpu_state.pc + offset);
                                cycles -= 12;
                                FETCHCLEAR();
                        }
                        cycles -= 4;
                        break;
                case 0x6D: /*JNL alias*/
                case 0x7D: /*JNL*/
                        offset = (int8_t)FETCH();
                        temp = (uint8_t)(((cpu_state.flags & N_FLAG) != 0) ? 1 : 0);
                        temp2 = (uint8_t)(((cpu_state.flags & V_FLAG) != 0) ? 1 : 0);
                        if (temp == temp2)
                        {
                                cpu_state.pc = (uint32_t)(cpu_state.pc + offset);
                                cycles -= 12;
                                FETCHCLEAR();
                        }
                        cycles -= 4;
                        break;
                case 0x6E: /*JLE alias*/
                case 0x7E: /*JLE*/
                        offset = (int8_t)FETCH();
                        temp = (uint8_t)(((cpu_state.flags & N_FLAG) != 0) ? 1 : 0);
                        temp2 = (uint8_t)(((cpu_state.flags & V_FLAG) != 0) ? 1 : 0);
                        if ((cpu_state.flags & Z_FLAG) != 0 || (temp != temp2))
                        {
                                cpu_state.pc = (uint32_t)(cpu_state.pc + offset);
                                cycles -= 12;
                                FETCHCLEAR();
                        }
                        cycles -= 4;
                        break;
                case 0x6F: /*JNLE alias*/
                case 0x7F: /*JNLE*/
                        offset = (int8_t)FETCH();
                        temp = (uint8_t)(((cpu_state.flags & N_FLAG) != 0) ? 1 : 0);
                        temp2 = (uint8_t)(((cpu_state.flags & V_FLAG) != 0) ? 1 : 0);
                        if (!((cpu_state.flags & Z_FLAG) != 0 || (temp != temp2)))
                        {
                                cpu_state.pc = (uint32_t)(cpu_state.pc + offset);
                                cycles -= 12;
                                FETCHCLEAR();
                        }
                        cycles -= 4;
                        break;

                case 0x80:
                case 0x82:
                        fetchea();
                        temp = geteab();
                        temp2 = FETCH();
                        switch (rmdat & 0x38)
                        {
                        case 0x00: /*ADD b,#8*/
                                setadd8(temp, temp2);
                                seteab((uint8_t)(temp + temp2));
                                cycles -= ((cpu_mod == 3) ? 4 : 23);
                                break;
                        case 0x08: /*OR b,#8*/
                                temp |= temp2;
                                setznp8(temp);
                                cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG | A_FLAG));
                                seteab(temp);
                                cycles -= ((cpu_mod == 3) ? 4 : 23);
                                break;
                        case 0x10: /*ADC b,#8*/
                                setadc8(temp, temp2);
                                seteab((uint8_t)(temp + temp2 + tempc));
                                cycles -= ((cpu_mod == 3) ? 4 : 23);
                                break;
                        case 0x18: /*SBB b,#8*/
                                setsbc8(temp, temp2);
                                seteab((uint8_t)(temp - (temp2 + tempc)));
                                cycles -= ((cpu_mod == 3) ? 4 : 23);
                                break;
                        case 0x20: /*AND b,#8*/
                                temp &= temp2;
                                setznp8(temp);
                                cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG | A_FLAG));
                                seteab(temp);
                                cycles -= ((cpu_mod == 3) ? 4 : 23);
                                break;
                        case 0x28: /*SUB b,#8*/
                                setsub8(temp, temp2);
                                seteab((uint8_t)(temp - temp2));
                                cycles -= ((cpu_mod == 3) ? 4 : 23);
                                break;
                        case 0x30: /*XOR b,#8*/
                                temp ^= temp2;
                                setznp8(temp);
                                cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG | A_FLAG));
                                seteab(temp);
                                cycles -= ((cpu_mod == 3) ? 4 : 23);
                                break;
                        case 0x38: /*CMP b,#8*/
                                setsub8(temp, temp2);
                                cycles -= ((cpu_mod == 3) ? 4 : 14);
                                break;
                        }
                        break;

                case 0x81:
                        fetchea();
                        tempw = geteaw();
                        tempw2 = getword();
                        switch (rmdat & 0x38)
                        {
                        case 0x00: /*ADD w,#16*/
                                setadd16(tempw, tempw2);
                                tempw += tempw2;
                                seteaw(tempw);
                                cycles -= ((cpu_mod == 3) ? 4 : 23);
                                break;
                        case 0x08: /*OR w,#16*/
                                tempw |= tempw2;
                                setznp16(tempw);
                                cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG | A_FLAG));
                                seteaw(tempw);
                                cycles -= ((cpu_mod == 3) ? 4 : 23);
                                break;
                        case 0x10: /*ADC w,#16*/
                                setadc16(tempw, tempw2);
                                tempw += (uint16_t)(tempw2 + tempc);
                                seteaw(tempw);
                                cycles -= ((cpu_mod == 3) ? 4 : 23);
                                break;
                        case 0x20: /*AND w,#16*/
                                tempw &= tempw2;
                                setznp16(tempw);
                                cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG | A_FLAG));
                                seteaw(tempw);
                                cycles -= ((cpu_mod == 3) ? 4 : 23);
                                break;
                        case 0x18: /*SBB w,#16*/
                                setsbc16(tempw, tempw2);
                                seteaw((uint16_t)(tempw - (tempw2 + tempc)));
                                cycles -= ((cpu_mod == 3) ? 4 : 23);
                                break;
                        case 0x28: /*SUB w,#16*/
                                setsub16(tempw, tempw2);
                                tempw -= tempw2;
                                seteaw(tempw);
                                cycles -= ((cpu_mod == 3) ? 4 : 23);
                                break;
                        case 0x30: /*XOR w,#16*/
                                tempw ^= tempw2;
                                setznp16(tempw);
                                cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG | A_FLAG));
                                seteaw(tempw);
                                cycles -= ((cpu_mod == 3) ? 4 : 23);
                                break;
                        case 0x38: /*CMP w,#16*/
                                setsub16(tempw, tempw2);
                                cycles -= ((cpu_mod == 3) ? 4 : 14);
                                break;
                        }
                        break;

                case 0x83:
                        fetchea();
                        tempw = geteaw();
                        tempw2 = FETCH();
                        if ((tempw2 & 0x80) != 0)
                                tempw2 |= 0xFF00;
                        switch (rmdat & 0x38)
                        {
                        case 0x00: /*ADD w,#8*/
                                setadd16(tempw, tempw2);
                                tempw += tempw2;
                                seteaw(tempw);
                                cycles -= ((cpu_mod == 3) ? 4 : 23);
                                break;
                        case 0x08: /*OR w,#8*/
                                tempw |= tempw2;
                                setznp16(tempw);
                                seteaw(tempw);
                                cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | A_FLAG | V_FLAG));
                                cycles -= ((cpu_mod == 3) ? 4 : 23);
                                break;
                        case 0x10: /*ADC w,#8*/
                                setadc16(tempw, tempw2);
                                tempw += (uint16_t)(tempw2 + tempc);
                                seteaw(tempw);
                                cycles -= ((cpu_mod == 3) ? 4 : 23);
                                break;
                        case 0x18: /*SBB w,#8*/
                                setsbc16(tempw, tempw2);
                                tempw -= (uint16_t)(tempw2 + tempc);
                                seteaw(tempw);
                                cycles -= ((cpu_mod == 3) ? 4 : 23);
                                break;
                        case 0x20: /*AND w,#8*/
                                tempw &= tempw2;
                                setznp16(tempw);
                                seteaw(tempw);
                                cycles -= ((cpu_mod == 3) ? 4 : 23);
                                cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | A_FLAG | V_FLAG));
                                break;
                        case 0x28: /*SUB w,#8*/
                                setsub16(tempw, tempw2);
                                tempw -= tempw2;
                                seteaw(tempw);
                                cycles -= ((cpu_mod == 3) ? 4 : 23);
                                break;
                        case 0x30: /*XOR w,#8*/
                                tempw ^= tempw2;
                                setznp16(tempw);
                                seteaw(tempw);
                                cycles -= ((cpu_mod == 3) ? 4 : 23);
                                cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | A_FLAG | V_FLAG));
                                break;
                        case 0x38: /*CMP w,#8*/
                                setsub16(tempw, tempw2);
                                cycles -= ((cpu_mod == 3) ? 4 : 14);
                                break;
                        }
                        break;

                case 0x84: /*TEST b,reg*/
                        fetchea();
                        temp = geteab();
                        temp2 = getr8(cpu_reg);
                        setznp8((uint8_t)(temp & temp2));
                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG | A_FLAG));
                        cycles -= ((cpu_mod == 3) ? 3 : 13);
                        break;
                case 0x85: /*TEST w,reg*/
                        fetchea();
                        tempw = geteaw();
                        tempw2 = cpu_state.regs[cpu_reg].w;
                        setznp16((uint16_t)(tempw & tempw2));
                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG | A_FLAG));
                        cycles -= ((cpu_mod == 3) ? 3 : 13);
                        break;
                case 0x86: /*XCHG b,reg*/
                        fetchea();
                        temp = geteab();
                        seteab(getr8(cpu_reg));
                        setr8(cpu_reg, temp);
                        cycles -= ((cpu_mod == 3) ? 4 : 25);
                        break;
                case 0x87: /*XCHG w,reg*/
                        fetchea();
                        tempw = geteaw();
                        seteaw(cpu_state.regs[cpu_reg].w);
                        cpu_state.regs[cpu_reg].w = tempw;
                        cycles -= ((cpu_mod == 3) ? 4 : 25);
                        break;

                case 0x88: /*MOV b,reg*/
                        fetchea();
                        seteab(getr8(cpu_reg));
                        cycles -= ((cpu_mod == 3) ? 2 : 13);
                        break;
                case 0x89: /*MOV w,reg*/
                        fetchea();
                        seteaw(cpu_state.regs[cpu_reg].w);
                        cycles -= ((cpu_mod == 3) ? 2 : 13);
                        break;
                case 0x8A: /*MOV cpu_reg,b*/
                        fetchea();
                        temp = geteab();
                        setr8(cpu_reg, temp);
                        cycles -= ((cpu_mod == 3) ? 2 : 12);
                        break;
                case 0x8B: /*MOV cpu_reg,w*/
                        fetchea();
                        tempw = geteaw();
                        cpu_state.regs[cpu_reg].w = tempw;
                        cycles -= ((cpu_mod == 3) ? 2 : 12);
                        break;

                case 0x8C: /*MOV w,sreg*/
                        fetchea();
                        switch (rmdat & 0x38)
                        {
                        case 0x00: /*ES*/
                                seteaw(ES);
                                break;
                        case 0x08: /*CS*/
                                seteaw(CS);
                                break;
                        case 0x18: /*DS*/
                                if (cpu_state.ssegs != 0)
                                        ds = oldds;
                                seteaw(DS);
                                break;
                        case 0x10: /*SS*/
                                if (cpu_state.ssegs != 0)
                                        ss = oldss;
                                seteaw(SS);
                                break;
                        }
                        cycles -= ((cpu_mod == 3) ? 2 : 13);
                        break;

                case 0x8D: /*LEA*/
                        fetchea();
                        cpu_state.regs[cpu_reg].w = (uint16_t)cpu_state.eaaddr;
                        cycles -= 2;
                        break;

                case 0x8E: /*MOV sreg,w*/
                        fetchea();
                        switch (rmdat & 0x38)
                        {
                        case 0x00: /*ES*/
                                tempw = geteaw();
                                x86seg_c.loadseg(tempw, cpu_state.seg_es);
                                break;
                        case 0x08: /*CS - 8088/8086 only*/
                                tempw = geteaw();
                                x86seg_c.loadseg(tempw, cpu_state.seg_cs);
                                break;
                        case 0x18: /*DS*/
                                tempw = geteaw();
                                x86seg_c.loadseg(tempw, cpu_state.seg_ds);
                                if (cpu_state.ssegs != 0)
                                        oldds = ds;
                                break;
                        case 0x10: /*SS*/
                                tempw = geteaw();
                                x86seg_c.loadseg(tempw, cpu_state.seg_ss);
                                if (cpu_state.ssegs != 0)
                                        oldss = ss;
                                break;
                        }
                        cycles -= ((cpu_mod == 3) ? 2 : 12);
                        // omitted: skipnextprint = 1 (808x.c:2339) — le bloc de trace if (output) n'est pas transcrit.
                        noint = 1;
                        break;

                case 0x8F: /*POPW*/
                        fetchea();
                        if (cpu_state.ssegs != 0)
                                ss = oldss;
                        tempw = readmemw(ss, SP);
                        SP += 2;
                        seteaw(tempw);
                        cycles -= 25;
                        break;

                case 0x90: /*NOP*/
                        cycles -= 3;
                        break;

                case 0x91:
                case 0x92:
                case 0x93: /*XCHG AX*/
                case 0x94:
                case 0x95:
                case 0x96:
                case 0x97:
                        tempw = AX;
                        AX = cpu_state.regs[opcode & 7].w;
                        cpu_state.regs[opcode & 7].w = tempw;
                        cycles -= 3;
                        break;

                case 0x98: /*CBW*/
                        AH = (uint8_t)(((AL & 0x80) != 0) ? 0xFF : 0);
                        cycles -= 2;
                        break;
                case 0x99: /*CWD*/
                        DX = (uint16_t)(((AX & 0x8000) != 0) ? 0xFFFF : 0);
                        cycles -= 5;
                        break;
                case 0x9A: /*CALL FAR*/
                        tempw = getword();
                        tempw2 = getword();
                        tempw3 = CS;
                        tempw4 = (uint16_t)cpu_state.pc;
                        if (cpu_state.ssegs != 0)
                                ss = oldss;
                        cpu_state.pc = tempw;
                        x86seg_c.loadcs(tempw2);
                        writememw(ss, (uint32_t)((SP - 2) & 0xFFFF), tempw3);
                        writememw(ss, (uint32_t)((SP - 4) & 0xFFFF), tempw4);
                        SP -= 4;
                        cycles -= 36;
                        FETCHCLEAR();
                        break;
                case 0x9B: /*WAIT*/
                        cycles -= 4;
                        break;
                case 0x9C: /*PUSHF*/
                        if (cpu_state.ssegs != 0)
                                ss = oldss;
                        writememw(ss, (uint32_t)((SP - 2) & 0xFFFF), (uint16_t)(cpu_state.flags | 0xF000));
                        SP -= 2;
                        cycles -= 14;
                        break;
                case 0x9D: /*POPF*/
                        if (cpu_state.ssegs != 0)
                                ss = oldss;
                        cpu_state.flags = (uint16_t)(readmemw(ss, SP) & 0xFFF);
                        SP += 2;
                        cycles -= 12;
                        break;
                case 0x9E: /*SAHF*/
                        cpu_state.flags = (uint16_t)((cpu_state.flags & 0xFF00) | AH);
                        cycles -= 4;
                        break;
                case 0x9F: /*LAHF*/
                        AH = (uint8_t)(cpu_state.flags & 0xFF);
                        cycles -= 4;
                        break;

                case 0xA0: /*MOV AL,(w)*/
                        addr = getword();
                        AL = readmemb(ds + addr);
                        cycles -= 14;
                        break;
                case 0xA1: /*MOV AX,(w)*/
                        addr = getword();
                        AX = readmemw(ds, addr);
                        cycles -= 14;
                        break;
                case 0xA2: /*MOV (w),AL*/
                        addr = getword();
                        writememb(ds + addr, AL);
                        cycles -= 14;
                        break;
                case 0xA3: /*MOV (w),AX*/
                        addr = getword();
                        writememw(ds, addr, AX);
                        cycles -= 14;
                        break;

                case 0xA4: /*MOVSB*/
                        temp = readmemb(ds + SI);
                        writememb(es + DI, temp);
                        if ((cpu_state.flags & D_FLAG) != 0)
                        {
                                DI--;
                                SI--;
                        }
                        else
                        {
                                DI++;
                                SI++;
                        }
                        cycles -= 18;
                        break;
                case 0xA5: /*MOVSW*/
                        tempw = readmemw(ds, SI);
                        writememw(es, DI, tempw);
                        if ((cpu_state.flags & D_FLAG) != 0)
                        {
                                DI -= 2;
                                SI -= 2;
                        }
                        else
                        {
                                DI += 2;
                                SI += 2;
                        }
                        cycles -= 18;
                        break;
                case 0xA6: /*CMPSB*/
                        temp = readmemb(ds + SI);
                        temp2 = readmemb(es + DI);
                        setsub8(temp, temp2);
                        if ((cpu_state.flags & D_FLAG) != 0)
                        {
                                DI--;
                                SI--;
                        }
                        else
                        {
                                DI++;
                                SI++;
                        }
                        cycles -= 30;
                        break;
                case 0xA7: /*CMPSW*/
                        tempw = readmemw(ds, SI);
                        tempw2 = readmemw(es, DI);
                        setsub16(tempw, tempw2);
                        if ((cpu_state.flags & D_FLAG) != 0)
                        {
                                DI -= 2;
                                SI -= 2;
                        }
                        else
                        {
                                DI += 2;
                                SI += 2;
                        }
                        cycles -= 30;
                        break;
                case 0xA8: /*TEST AL,#8*/
                        temp = FETCH();
                        setznp8((uint8_t)(AL & temp));
                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG | A_FLAG));
                        cycles -= 5;
                        break;
                case 0xA9: /*TEST AX,#16*/
                        tempw = getword();
                        setznp16((uint16_t)(AX & tempw));
                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG | A_FLAG));
                        cycles -= 5;
                        break;
                case 0xAA: /*STOSB*/
                        writememb(es + DI, AL);
                        if ((cpu_state.flags & D_FLAG) != 0)
                                DI--;
                        else
                                DI++;
                        cycles -= 11;
                        break;
                case 0xAB: /*STOSW*/
                        writememw(es, DI, AX);
                        if ((cpu_state.flags & D_FLAG) != 0)
                                DI -= 2;
                        else
                                DI += 2;
                        cycles -= 11;
                        break;
                case 0xAC: /*LODSB*/
                        AL = readmemb(ds + SI);
                        if ((cpu_state.flags & D_FLAG) != 0)
                                SI--;
                        else
                                SI++;
                        cycles -= 16;
                        break;
                case 0xAD: /*LODSW*/
                        AX = readmemw(ds, SI);
                        if ((cpu_state.flags & D_FLAG) != 0)
                                SI -= 2;
                        else
                                SI += 2;
                        cycles -= 16;
                        break;
                case 0xAE: /*SCASB*/
                        temp = readmemb(es + DI);
                        setsub8(AL, temp);
                        if ((cpu_state.flags & D_FLAG) != 0)
                                DI--;
                        else
                                DI++;
                        cycles -= 19;
                        break;
                case 0xAF: /*SCASW*/
                        tempw = readmemw(es, DI);
                        setsub16(AX, tempw);
                        if ((cpu_state.flags & D_FLAG) != 0)
                                DI -= 2;
                        else
                                DI += 2;
                        cycles -= 19;
                        break;

                case 0xB0: /*MOV AL,#8*/
                        AL = FETCH();
                        cycles -= 4;
                        break;
                case 0xB1: /*MOV CL,#8*/
                        CL = FETCH();
                        cycles -= 4;
                        break;
                case 0xB2: /*MOV DL,#8*/
                        DL = FETCH();
                        cycles -= 4;
                        break;
                case 0xB3: /*MOV BL,#8*/
                        BL = FETCH();
                        cycles -= 4;
                        break;
                case 0xB4: /*MOV AH,#8*/
                        AH = FETCH();
                        cycles -= 4;
                        break;
                case 0xB5: /*MOV CH,#8*/
                        CH = FETCH();
                        cycles -= 4;
                        break;
                case 0xB6: /*MOV DH,#8*/
                        DH = FETCH();
                        cycles -= 4;
                        break;
                case 0xB7: /*MOV BH,#8*/
                        BH = FETCH();
                        cycles -= 4;
                        break;
                case 0xB8:
                case 0xB9:
                case 0xBA:
                case 0xBB: /*MOV cpu_reg,#16*/
                case 0xBC:
                case 0xBD:
                case 0xBE:
                case 0xBF:
                        cpu_state.regs[opcode & 7].w = getword();
                        cycles -= 4;
                        break;

                case 0xC0: /*RET alias*/
                case 0xC2: /*RET*/
                        tempw = getword();
                        if (cpu_state.ssegs != 0)
                                ss = oldss;
                        cpu_state.pc = readmemw(ss, SP);
                        SP += (uint16_t)(2 + tempw);
                        cycles -= 24;
                        FETCHCLEAR();
                        break;
                case 0xC1: /*RET alias*/
                case 0xC3: /*RET*/
                        if (cpu_state.ssegs != 0)
                                ss = oldss;
                        cpu_state.pc = readmemw(ss, SP);
                        SP += 2;
                        cycles -= 20;
                        FETCHCLEAR();
                        break;
                case 0xC4: /*LES*/
                        fetchea();
                        cpu_state.regs[cpu_reg].w = readmemw(easeg, (uint16_t)cpu_state.eaaddr);
                        tempw = readmemw(easeg, (uint16_t)((cpu_state.eaaddr + 2) & 0xFFFF));
                        x86seg_c.loadseg(tempw, cpu_state.seg_es);
                        cycles -= 24;
                        break;
                case 0xC5: /*LDS*/
                        fetchea();
                        cpu_state.regs[cpu_reg].w = readmemw(easeg, (uint16_t)cpu_state.eaaddr);
                        tempw = readmemw(easeg, (uint16_t)((cpu_state.eaaddr + 2) & 0xFFFF));
                        x86seg_c.loadseg(tempw, cpu_state.seg_ds);
                        if (cpu_state.ssegs != 0)
                                oldds = ds;
                        cycles -= 24;
                        break;
                case 0xC6: /*MOV b,#8*/
                        fetchea();
                        temp = FETCH();
                        seteab(temp);
                        cycles -= ((cpu_mod == 3) ? 4 : 14);
                        break;
                case 0xC7: /*MOV w,#16*/
                        fetchea();
                        tempw = getword();
                        seteaw(tempw);
                        cycles -= ((cpu_mod == 3) ? 4 : 14);
                        break;

                case 0xC8: /*RETF alias*/
                case 0xCA: /*RETF*/
                        tempw = getword();
                        if (cpu_state.ssegs != 0)
                                ss = oldss;
                        cpu_state.pc = readmemw(ss, SP);
                        x86seg_c.loadcs(readmemw(ss, (uint16_t)(SP + 2)));
                        SP += 4;
                        SP += tempw;
                        cycles -= 33;
                        FETCHCLEAR();
                        break;
                case 0xC9: /*RETF alias*/
                case 0xCB: /*RETF*/
                        if (cpu_state.ssegs != 0)
                                ss = oldss;
                        cpu_state.pc = readmemw(ss, SP);
                        x86seg_c.loadcs(readmemw(ss, (uint16_t)(SP + 2)));
                        SP += 4;
                        cycles -= 34;
                        FETCHCLEAR();
                        break;
                case 0xCC: /*INT 3*/
                        if (cpu_state.ssegs != 0)
                                ss = oldss;
                        writememw(ss, (uint32_t)((SP - 2) & 0xFFFF), (uint16_t)(cpu_state.flags | 0xF000));
                        writememw(ss, (uint32_t)((SP - 4) & 0xFFFF), CS);
                        writememw(ss, (uint32_t)((SP - 6) & 0xFFFF), (uint16_t)cpu_state.pc);
                        SP -= 6;
                        addr = 3 << 2;
                        cpu_state.flags &= unchecked((uint16_t)~I_FLAG);
                        cpu_state.flags &= unchecked((uint16_t)~T_FLAG);
                        cpu_state.pc = readmemw(0, addr);
                        x86seg_c.loadcs(readmemw(0, (uint16_t)(addr + 2)));
                        FETCHCLEAR();
                        cycles -= 72;
                        break;
                case 0xCD: /*INT*/
                        temp = FETCH();

                        if (cpu_state.ssegs != 0)
                                ss = oldss;
                        writememw(ss, (uint32_t)((SP - 2) & 0xFFFF), (uint16_t)(cpu_state.flags | 0xF000));
                        writememw(ss, (uint32_t)((SP - 4) & 0xFFFF), CS);
                        writememw(ss, (uint32_t)((SP - 6) & 0xFFFF), (uint16_t)cpu_state.pc);
                        cpu_state.flags &= unchecked((uint16_t)~T_FLAG);
                        SP -= 6;
                        addr = (uint16_t)(temp << 2);
                        cpu_state.pc = readmemw(0, addr);

                        x86seg_c.loadcs(readmemw(0, (uint16_t)(addr + 2)));
                        FETCHCLEAR();

                        cycles -= 71;
                        break;
                case 0xCF: /*IRET*/
                        if (cpu_state.ssegs != 0)
                                ss = oldss;
                        tempw = CS;
                        tempw2 = (uint16_t)cpu_state.pc;
                        cpu_state.pc = readmemw(ss, SP);
                        x86seg_c.loadcs(readmemw(ss, (uint16_t)((SP + 2) & 0xFFFF)));
                        cpu_state.flags = (uint16_t)(readmemw(ss, (uint16_t)((SP + 4) & 0xFFFF)) & 0xFFF);
                        SP += 6;
                        cycles -= 44;
                        FETCHCLEAR();
                        nmi_enable = 1;
                        break;

                case 0xD0:
                        fetchea();
                        temp = geteab();
                        switch (rmdat & 0x38)
                        {
                        case 0x00: /*ROL b,1*/
                                if ((temp & 0x80) != 0)
                                        cpu_state.flags |= C_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
                                temp <<= 1;
                                if ((cpu_state.flags & C_FLAG) != 0)
                                        temp |= 1;
                                seteab(temp);
                                if (((cpu_state.flags & C_FLAG) ^ (temp >> 7)) != 0)
                                        cpu_state.flags |= V_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~V_FLAG);
                                cycles -= ((cpu_mod == 3) ? 2 : 23);
                                break;
                        case 0x08: /*ROR b,1*/
                                if ((temp & 1) != 0)
                                        cpu_state.flags |= C_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
                                temp >>= 1;
                                if ((cpu_state.flags & C_FLAG) != 0)
                                        temp |= 0x80;
                                seteab(temp);
                                if (((temp ^ (temp >> 1)) & 0x40) != 0)
                                        cpu_state.flags |= V_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~V_FLAG);
                                cycles -= ((cpu_mod == 3) ? 2 : 23);
                                break;
                        case 0x10: /*RCL b,1*/
                                temp2 = (uint8_t)(cpu_state.flags & C_FLAG);
                                if ((temp & 0x80) != 0)
                                        cpu_state.flags |= C_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
                                temp <<= 1;
                                if (temp2 != 0)
                                        temp |= 1;
                                seteab(temp);
                                if (((cpu_state.flags & C_FLAG) ^ (temp >> 7)) != 0)
                                        cpu_state.flags |= V_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~V_FLAG);
                                cycles -= ((cpu_mod == 3) ? 2 : 23);
                                break;
                        case 0x18: /*RCR b,1*/
                                temp2 = (uint8_t)(cpu_state.flags & C_FLAG);
                                if ((temp & 1) != 0)
                                        cpu_state.flags |= C_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
                                temp >>= 1;
                                if (temp2 != 0)
                                        temp |= 0x80;
                                seteab(temp);
                                if (((temp ^ (temp >> 1)) & 0x40) != 0)
                                        cpu_state.flags |= V_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~V_FLAG);
                                cycles -= ((cpu_mod == 3) ? 2 : 23);
                                break;
                        case 0x20:
                        case 0x30: /*SHL b,1*/
                                if ((temp & 0x80) != 0)
                                        cpu_state.flags |= C_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
                                if (((temp ^ (temp << 1)) & 0x80) != 0)
                                        cpu_state.flags |= V_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~V_FLAG);
                                temp <<= 1;
                                seteab(temp);
                                setznp8(temp);
                                cycles -= ((cpu_mod == 3) ? 2 : 23);
                                cpu_state.flags |= A_FLAG;
                                break;
                        case 0x28: /*SHR b,1*/
                                if ((temp & 1) != 0)
                                        cpu_state.flags |= C_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
                                if ((temp & 0x80) != 0)
                                        cpu_state.flags |= V_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~V_FLAG);
                                temp >>= 1;
                                seteab(temp);
                                setznp8(temp);
                                cycles -= ((cpu_mod == 3) ? 2 : 23);
                                cpu_state.flags |= A_FLAG;
                                break;
                        case 0x38: /*SAR b,1*/
                                if ((temp & 1) != 0)
                                        cpu_state.flags |= C_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
                                temp >>= 1;
                                if ((temp & 0x40) != 0)
                                        temp |= 0x80;
                                seteab(temp);
                                setznp8(temp);
                                cycles -= ((cpu_mod == 3) ? 2 : 23);
                                cpu_state.flags |= A_FLAG;
                                cpu_state.flags &= unchecked((uint16_t)~V_FLAG);
                                break;
                        }
                        break;

                case 0xD1:
                        fetchea();
                        tempw = geteaw();
                        switch (rmdat & 0x38)
                        {
                        case 0x00: /*ROL w,1*/
                                if ((tempw & 0x8000) != 0)
                                        cpu_state.flags |= C_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
                                tempw <<= 1;
                                if ((cpu_state.flags & C_FLAG) != 0)
                                        tempw |= 1;
                                seteaw(tempw);
                                if (((cpu_state.flags & C_FLAG) ^ (tempw >> 15)) != 0)
                                        cpu_state.flags |= V_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~V_FLAG);
                                cycles -= ((cpu_mod == 3) ? 2 : 23);
                                break;
                        case 0x08: /*ROR w,1*/
                                if ((tempw & 1) != 0)
                                        cpu_state.flags |= C_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
                                tempw >>= 1;
                                if ((cpu_state.flags & C_FLAG) != 0)
                                        tempw |= 0x8000;
                                seteaw(tempw);
                                if (((tempw ^ (tempw >> 1)) & 0x4000) != 0)
                                        cpu_state.flags |= V_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~V_FLAG);
                                cycles -= ((cpu_mod == 3) ? 2 : 23);
                                break;
                        case 0x10: /*RCL w,1*/
                                temp2 = (uint8_t)(cpu_state.flags & C_FLAG);
                                if ((tempw & 0x8000) != 0)
                                        cpu_state.flags |= C_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
                                tempw <<= 1;
                                if (temp2 != 0)
                                        tempw |= 1;
                                seteaw(tempw);
                                if (((cpu_state.flags & C_FLAG) ^ (tempw >> 15)) != 0)
                                        cpu_state.flags |= V_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~V_FLAG);
                                cycles -= ((cpu_mod == 3) ? 2 : 23);
                                break;
                        case 0x18: /*RCR w,1*/
                                temp2 = (uint8_t)(cpu_state.flags & C_FLAG);
                                if ((tempw & 1) != 0)
                                        cpu_state.flags |= C_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
                                tempw >>= 1;
                                if (temp2 != 0)
                                        tempw |= 0x8000;
                                seteaw(tempw);
                                if (((tempw ^ (tempw >> 1)) & 0x4000) != 0)
                                        cpu_state.flags |= V_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~V_FLAG);
                                cycles -= ((cpu_mod == 3) ? 2 : 23);
                                break;
                        case 0x20:
                        case 0x30: /*SHL w,1*/
                                if ((tempw & 0x8000) != 0)
                                        cpu_state.flags |= C_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
                                if (((tempw ^ (tempw << 1)) & 0x8000) != 0)
                                        cpu_state.flags |= V_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~V_FLAG);
                                tempw <<= 1;
                                seteaw(tempw);
                                setznp16(tempw);
                                cycles -= ((cpu_mod == 3) ? 2 : 23);
                                cpu_state.flags |= A_FLAG;
                                break;
                        case 0x28: /*SHR w,1*/
                                if ((tempw & 1) != 0)
                                        cpu_state.flags |= C_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
                                if ((tempw & 0x8000) != 0)
                                        cpu_state.flags |= V_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~V_FLAG);
                                tempw >>= 1;
                                seteaw(tempw);
                                setznp16(tempw);
                                cycles -= ((cpu_mod == 3) ? 2 : 23);
                                cpu_state.flags |= A_FLAG;
                                break;

                        case 0x38: /*SAR w,1*/
                                if ((tempw & 1) != 0)
                                        cpu_state.flags |= C_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
                                tempw >>= 1;
                                if ((tempw & 0x4000) != 0)
                                        tempw |= 0x8000;
                                seteaw(tempw);
                                setznp16(tempw);
                                cycles -= ((cpu_mod == 3) ? 2 : 23);
                                cpu_state.flags |= A_FLAG;
                                cpu_state.flags &= unchecked((uint16_t)~V_FLAG);
                                break;
                        }
                        break;

                case 0xD2:
                        fetchea();
                        temp = geteab();
                        c = CL;
                        if (c == 0)
                                break;
                        // CS0165: temp2 n'est écrit que dans les boucles `while (c > 0)`
                        //   ci-dessous. c >= 1 est acquis ici, elles s'exécutent donc
                        //   toujours au moins une fois et cette initialisation est inerte.
                        temp2 = 0;
                        switch (rmdat & 0x38)
                        {
                        case 0x00: /*ROL b,CL*/
                                while (c > 0)
                                {
                                        temp2 = (uint8_t)(((temp & 0x80) != 0) ? 1 : 0);
                                        temp = (uint8_t)((temp << 1) | temp2);
                                        c--;
                                        cycles -= 4;
                                }
                                if (temp2 != 0)
                                        cpu_state.flags |= C_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
                                seteab(temp);
                                if (((cpu_state.flags & C_FLAG) ^ (temp >> 7)) != 0)
                                        cpu_state.flags |= V_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~V_FLAG);
                                cycles -= ((cpu_mod == 3) ? 8 : 28);
                                break;
                        case 0x08: /*ROR b,CL*/
                                while (c > 0)
                                {
                                        temp2 = (uint8_t)(temp & 1);
                                        temp >>= 1;
                                        if (temp2 != 0)
                                                temp |= 0x80;
                                        c--;
                                        cycles -= 4;
                                }
                                if (temp2 != 0)
                                        cpu_state.flags |= C_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
                                seteab(temp);
                                if (((temp ^ (temp >> 1)) & 0x40) != 0)
                                        cpu_state.flags |= V_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~V_FLAG);
                                cycles -= ((cpu_mod == 3) ? 8 : 28);
                                break;
                        case 0x10: /*RCL b,CL*/
                                while (c > 0)
                                {
                                        templ = (uint32_t)(cpu_state.flags & C_FLAG);
                                        temp2 = (uint8_t)(temp & 0x80);
                                        temp <<= 1;
                                        if (temp2 != 0)
                                                cpu_state.flags |= C_FLAG;
                                        else
                                                cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
                                        if (templ != 0)
                                                temp |= 1;
                                        c--;
                                        cycles -= 4;
                                }
                                seteab(temp);
                                if (((cpu_state.flags & C_FLAG) ^ (temp >> 7)) != 0)
                                        cpu_state.flags |= V_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~V_FLAG);
                                cycles -= ((cpu_mod == 3) ? 8 : 28);
                                break;
                        case 0x18: /*RCR b,CL*/
                                while (c > 0)
                                {
                                        templ = (uint32_t)(cpu_state.flags & C_FLAG);
                                        temp2 = (uint8_t)(temp & 1);
                                        temp >>= 1;
                                        if (temp2 != 0)
                                                cpu_state.flags |= C_FLAG;
                                        else
                                                cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
                                        if (templ != 0)
                                                temp |= 0x80;
                                        c--;
                                        cycles -= 4;
                                }
                                seteab(temp);
                                if (((temp ^ (temp >> 1)) & 0x40) != 0)
                                        cpu_state.flags |= V_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~V_FLAG);
                                cycles -= ((cpu_mod == 3) ? 8 : 28);
                                break;
                        case 0x20:
                        case 0x30: /*SHL b,CL*/
                                if (c > 8)
                                {
                                        temp = 0;
                                        cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
                                }
                                else
                                {
                                        if (((temp << (c - 1)) & 0x80) != 0)
                                                cpu_state.flags |= C_FLAG;
                                        else
                                                cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
                                        temp <<= c;
                                }
                                seteab(temp);
                                setznp8(temp);
                                cycles -= (c * 4);
                                cycles -= ((cpu_mod == 3) ? 8 : 28);
                                cpu_state.flags |= A_FLAG;
                                break;
                        case 0x28: /*SHR b,CL*/
                                if (c > 8)
                                {
                                        temp = 0;
                                        cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
                                }
                                else
                                {
                                        if (((temp >> (c - 1)) & 1) != 0)
                                                cpu_state.flags |= C_FLAG;
                                        else
                                                cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
                                        temp >>= c;
                                }
                                seteab(temp);
                                setznp8(temp);
                                cycles -= (c * 4);
                                cycles -= ((cpu_mod == 3) ? 8 : 28);
                                cpu_state.flags |= A_FLAG;
                                break;
                        case 0x38: /*SAR b,CL*/
                                if (((temp >> (c - 1)) & 1) != 0)
                                        cpu_state.flags |= C_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
                                while (c > 0)
                                {
                                        temp >>= 1;
                                        if ((temp & 0x40) != 0)
                                                temp |= 0x80;
                                        c--;
                                        cycles -= 4;
                                }
                                seteab(temp);
                                setznp8(temp);
                                cycles -= ((cpu_mod == 3) ? 8 : 28);
                                cpu_state.flags |= A_FLAG;
                                break;
                        }
                        break;

                case 0xD3:
                        fetchea();
                        tempw = geteaw();
                        c = CL;
                        if (c == 0)
                                break;
                        // CS0165: temp, tempw2 et templ ne sont écrits que dans les boucles
                        //   `while (c > 0)` ci-dessous. c >= 1 est acquis ici, elles
                        //   s'exécutent toujours et ces initialisations sont inertes.
                        temp = 0;
                        tempw2 = 0;
                        templ = 0;
                        switch (rmdat & 0x38)
                        {
                        case 0x00: /*ROL w,CL*/
                                while (c > 0)
                                {
                                        temp = (uint8_t)(((tempw & 0x8000) != 0) ? 1 : 0);
                                        tempw = (uint16_t)((tempw << 1) | temp);
                                        c--;
                                        cycles -= 4;
                                }
                                if (temp != 0)
                                        cpu_state.flags |= C_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
                                seteaw(tempw);
                                if (((cpu_state.flags & C_FLAG) ^ (tempw >> 15)) != 0)
                                        cpu_state.flags |= V_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~V_FLAG);
                                cycles -= ((cpu_mod == 3) ? 8 : 28);
                                break;
                        case 0x08: /*ROR w,CL*/
                                while (c > 0)
                                {
                                        tempw2 = (uint16_t)(((tempw & 1) != 0) ? 0x8000 : 0);
                                        tempw = (uint16_t)((tempw >> 1) | tempw2);
                                        c--;
                                        cycles -= 4;
                                }
                                if (tempw2 != 0)
                                        cpu_state.flags |= C_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
                                seteaw(tempw);
                                if (((tempw ^ (tempw >> 1)) & 0x4000) != 0)
                                        cpu_state.flags |= V_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~V_FLAG);
                                cycles -= ((cpu_mod == 3) ? 8 : 28);
                                break;
                        case 0x10: /*RCL w,CL*/
                                while (c > 0)
                                {
                                        templ = (uint32_t)(cpu_state.flags & C_FLAG);
                                        if ((tempw & 0x8000) != 0)
                                                cpu_state.flags |= C_FLAG;
                                        else
                                                cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
                                        tempw = (uint16_t)((uint32_t)(tempw << 1) | templ);
                                        c--;
                                        cycles -= 4;
                                }
                                // pcem bug, reproduced: `templ` porte la retenue ENTRÉE à la
                                //   dernière itération, pas celle qui en est sortie ; ce bloc
                                //   écrase donc le C_FLAG correct posé dans la boucle. Le
                                //   pendant octet, RCL b,CL (0xD2/0x10), ne l'a pas.
                                if (templ != 0)
                                        cpu_state.flags |= C_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
                                seteaw(tempw);
                                if (((cpu_state.flags & C_FLAG) ^ (tempw >> 15)) != 0)
                                        cpu_state.flags |= V_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~V_FLAG);
                                cycles -= ((cpu_mod == 3) ? 8 : 28);
                                break;
                        case 0x18: /*RCR w,CL*/
                                while (c > 0)
                                {
                                        templ = (uint32_t)(cpu_state.flags & C_FLAG);
                                        tempw2 = (uint16_t)(((templ & 1) != 0) ? 0x8000 : 0);
                                        if ((tempw & 1) != 0)
                                                cpu_state.flags |= C_FLAG;
                                        else
                                                cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
                                        tempw = (uint16_t)((tempw >> 1) | tempw2);
                                        c--;
                                        cycles -= 4;
                                }
                                // pcem bug, reproduced: `tempw2` porte le bit ENTRÉ (l'ancienne
                                //   retenue), pas le bit sorti ; ce bloc écrase le C_FLAG
                                //   correct posé dans la boucle. Le pendant octet, RCR b,CL,
                                //   a exactement ces quatre lignes commentées (808x.c:3059-3060).
                                if (tempw2 != 0)
                                        cpu_state.flags |= C_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
                                seteaw(tempw);
                                if (((tempw ^ (tempw >> 1)) & 0x4000) != 0)
                                        cpu_state.flags |= V_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~V_FLAG);
                                cycles -= ((cpu_mod == 3) ? 8 : 28);
                                break;

                        case 0x20:
                        case 0x30: /*SHL w,CL*/
                                if (c > 16)
                                {
                                        tempw = 0;
                                        cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
                                }
                                else
                                {
                                        if (((tempw << (c - 1)) & 0x8000) != 0)
                                                cpu_state.flags |= C_FLAG;
                                        else
                                                cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
                                        tempw <<= c;
                                }
                                seteaw(tempw);
                                setznp16(tempw);
                                cycles -= (c * 4);
                                cycles -= ((cpu_mod == 3) ? 8 : 28);
                                cpu_state.flags |= A_FLAG;
                                break;

                        case 0x28: /*SHR w,CL*/
                                if (c > 16)
                                {
                                        tempw = 0;
                                        cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
                                }
                                else
                                {
                                        if (((tempw >> (c - 1)) & 1) != 0)
                                                cpu_state.flags |= C_FLAG;
                                        else
                                                cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
                                        tempw >>= c;
                                }
                                seteaw(tempw);
                                setznp16(tempw);
                                cycles -= (c * 4);
                                cycles -= ((cpu_mod == 3) ? 8 : 28);
                                cpu_state.flags |= A_FLAG;
                                break;

                        case 0x38: /*SAR w,CL*/
                                tempw2 = (uint16_t)(tempw & 0x8000);
                                if (((tempw >> (c - 1)) & 1) != 0)
                                        cpu_state.flags |= C_FLAG;
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
                                while (c > 0)
                                {
                                        tempw = (uint16_t)((tempw >> 1) | tempw2);
                                        c--;
                                        cycles -= 4;
                                }
                                seteaw(tempw);
                                setznp16(tempw);
                                cycles -= ((cpu_mod == 3) ? 8 : 28);
                                cpu_state.flags |= A_FLAG;
                                break;
                        }
                        break;

                case 0xD4: /*AAM*/
                        tempws = FETCH();
                        // pcem bug, not reproduced: PB-46 — AAM 0 divise par zéro : le C tombe
                        //   (SIGFPE, 808x.c:3282, mesuré rc 136) et le C# levait une
                        //   DivideByZeroException qui abattait l'hôte (rc 134). Un 8088 lève
                        //   INT 0 — SingleStepTests/8088, forme D4 : SP − 6, IP poussé APRÈS
                        //   l'instruction, AX inchangé. Garde vers le chemin de l'erreur de
                        //   division, le même que DIV par zéro juste au-dessus (F6 /6).
                        //
                        // DEVIATION: l'oracle n'a pas de comportement à reproduire ici — il
                        //   meurt. Le coût, 83 cycles, est celui d'AAM ; aucun oracle ne le
                        //   départage (SST ne compare pas les cycles).
                        if (tempws == 0)
                        {
                                writememw(ss, (uint32_t)((SP - 2) & 0xFFFF), (uint16_t)(cpu_state.flags | 0xF000));
                                writememw(ss, (uint32_t)((SP - 4) & 0xFFFF), CS);
                                writememw(ss, (uint32_t)((SP - 6) & 0xFFFF), (uint16_t)cpu_state.pc);
                                SP -= 6;
                                cpu_state.flags &= unchecked((uint16_t)~I_FLAG);
                                cpu_state.flags &= unchecked((uint16_t)~T_FLAG);
                                cpu_state.pc = readmemw(0, 0);
                                x86seg_c.loadcs(readmemw(0, 2));
                                FETCHCLEAR();
                                cycles -= 83;
                                break;
                        }
                        AH = (uint8_t)(AL / tempws);
                        AL %= (uint8_t)tempws;
                        setznp16(AX);
                        cycles -= 83;
                        break;
                case 0xD5: /*AAD*/
                        tempws = FETCH();
                        AL = (uint8_t)((AH * tempws) + AL);
                        AH = 0;
                        setznp16(AX);
                        cycles -= 60;
                        break;
                case 0xD6: /*SETALC*/
                        AL = (uint8_t)(((cpu_state.flags & C_FLAG) != 0) ? 0xff : 0);
                        cycles -= 4;
                        break;
                case 0xD7: /*XLAT*/
                        addr = (uint16_t)(BX + AL);
                        AL = readmemb(ds + addr);
                        cycles -= 11;
                        break;

                case 0xd8:
                        fetchea();
                        // omitted: le bloc `if (hasfpu)` — ops_808x_fpu_d8_a16[rmdat >> 3](rmdat),
                        //   encadré d'une sauvegarde/restauration de cpu_state.pc. L'état 8087
                        //   est hors portage (registre des omissions) et hasfpu vaut 0 sur XT :
                        //   le bloc ne s'exécute jamais. Idem 0xd9 à 0xdf.
                        break;
                case 0xd9:
                        fetchea();
                        // omitted: bloc `if (hasfpu)` — ops_808x_fpu_d9_a16[rmdat](rmdat).
                        break;
                case 0xda:
                        fetchea();
                        // omitted: bloc `if (hasfpu)` — ops_808x_fpu_da_a16[rmdat](rmdat).
                        break;
                case 0xdb:
                        fetchea();
                        // omitted: bloc `if (hasfpu)` — ops_808x_fpu_db_a16[rmdat](rmdat).
                        break;
                case 0xdc:
                        fetchea();
                        // omitted: bloc `if (hasfpu)` — ops_808x_fpu_dc_a16[rmdat >> 3](rmdat).
                        break;
                case 0xdd:
                        fetchea();
                        // omitted: bloc `if (hasfpu)` — ops_808x_fpu_dd_a16[rmdat](rmdat).
                        break;
                case 0xde:
                        fetchea();
                        // omitted: bloc `if (hasfpu)` — ops_808x_fpu_de_a16[rmdat](rmdat).
                        break;
                case 0xdf:
                        fetchea();
                        // omitted: bloc `if (hasfpu)` — ops_808x_fpu_df_a16[rmdat](rmdat).
                        break;

                case 0xE0: /*LOOPNE*/
                        offset = (int8_t)FETCH();
                        CX--;
                        if (CX != 0 && (cpu_state.flags & Z_FLAG) == 0)
                        {
                                cpu_state.pc = (uint32_t)(cpu_state.pc + offset);
                                cycles -= 12;
                                FETCHCLEAR();
                        }
                        cycles -= 6;
                        break;
                case 0xE1: /*LOOPE*/
                        offset = (int8_t)FETCH();
                        CX--;
                        if (CX != 0 && (cpu_state.flags & Z_FLAG) != 0)
                        {
                                cpu_state.pc = (uint32_t)(cpu_state.pc + offset);
                                cycles -= 12;
                                FETCHCLEAR();
                        }
                        cycles -= 6;
                        break;
                case 0xE2: /*LOOP*/
                        offset = (int8_t)FETCH();
                        CX--;
                        if (CX != 0)
                        {
                                cpu_state.pc = (uint32_t)(cpu_state.pc + offset);
                                cycles -= 12;
                                FETCHCLEAR();
                        }
                        cycles -= 5;
                        break;
                case 0xE3: /*JCXZ*/
                        offset = (int8_t)FETCH();
                        if (CX == 0)
                        {
                                cpu_state.pc = (uint32_t)(cpu_state.pc + offset);
                                cycles -= 12;
                                FETCHCLEAR();
                        }
                        cycles -= 6;
                        break;

                case 0xE4: /*IN AL*/
                        temp = FETCH();
                        AL = io.inb(temp);
                        cycles -= 14;
                        break;
                case 0xE5: /*IN AX*/
                        temp = FETCH();
                        AL = io.inb(temp);
                        AH = io.inb((uint16_t)(temp + 1));
                        cycles -= 14;
                        break;
                case 0xE6: /*OUT AL*/
                        temp = FETCH();
                        io.outb(temp, AL);
                        cycles -= 14;
                        break;
                case 0xE7: /*OUT AX*/
                        temp = FETCH();
                        io.outb(temp, AL);
                        io.outb((uint16_t)(temp + 1), AH);
                        cycles -= 14;
                        break;

                case 0xE8: /*CALL rel 16*/
                        tempw = getword();
                        if (cpu_state.ssegs != 0)
                                ss = oldss;
                        writememw(ss, ((uint32_t)(SP - 2) & 0xFFFF), (uint16_t)cpu_state.pc);
                        SP -= 2;
                        cpu_state.pc += tempw;
                        cycles -= 23;
                        FETCHCLEAR();
                        break;
                case 0xE9: /*JMP rel 16*/
                        tempw = getword();
                        cpu_state.pc += tempw;
                        cycles -= 15;
                        FETCHCLEAR();
                        break;
                case 0xEA: /*JMP far*/
                        addr = getword();
                        tempw = getword();
                        cpu_state.pc = addr;
                        x86seg_c.loadcs(tempw);
                        cycles -= 15;
                        FETCHCLEAR();
                        break;
                case 0xEB: /*JMP rel*/
                        offset = (int8_t)FETCH();
                        cpu_state.pc = (uint32_t)(cpu_state.pc + offset);
                        cycles -= 15;
                        FETCHCLEAR();
                        break;
                case 0xEC: /*IN AL,DX*/
                        AL = io.inb(DX);
                        cycles -= 12;
                        break;
                case 0xED: /*IN AX,DX*/
                        AL = io.inb(DX);
                        AH = io.inb((uint16_t)(DX + 1));
                        cycles -= 12;
                        break;
                case 0xEE: /*OUT DX,AL*/
                        io.outb(DX, AL);
                        cycles -= 12;
                        break;
                case 0xEF: /*OUT DX,AX*/
                        io.outb(DX, AL);
                        io.outb((uint16_t)(DX + 1), AH);
                        cycles -= 12;
                        break;

                case 0xF0: /*LOCK*/
                case 0xF1: /*LOCK alias*/
                        cycles -= 4;
                        break;

                case 0xF2: /*REPNE*/
                        rep(0);
                        break;
                case 0xF3: /*REPE*/
                        rep(1);
                        break;

                case 0xF4: /*HLT*/
                        inhlt = 1;
                        cpu_state.pc--;
                        FETCHCLEAR();
                        cycles -= 2;
                        break;
                case 0xF5: /*CMC*/
                        cpu_state.flags ^= C_FLAG;
                        cycles -= 2;
                        break;

                case 0xF6:
                        fetchea();
                        temp = geteab();
                        switch (rmdat & 0x38)
                        {
                        case 0x00: /*TEST b,#8*/
                        case 0x08:
                                temp2 = FETCH();
                                temp &= temp2;
                                setznp8(temp);
                                cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG | A_FLAG));
                                cycles -= ((cpu_mod == 3) ? 5 : 11);
                                break;
                        case 0x10: /*NOT b*/
                                temp = (uint8_t)~temp;
                                seteab(temp);
                                cycles -= ((cpu_mod == 3) ? 3 : 24);
                                break;
                        case 0x18: /*NEG b*/
                                setsub8(0, temp);
                                temp = (uint8_t)(0 - temp);
                                seteab(temp);
                                cycles -= ((cpu_mod == 3) ? 3 : 24);
                                break;
                        case 0x20: /*MUL AL,b*/
                                setznp8(AL);
                                AX = (uint16_t)(AL * temp);
                                if (AX != 0)
                                        cpu_state.flags &= unchecked((uint16_t)~Z_FLAG);
                                else
                                        cpu_state.flags |= Z_FLAG;
                                if (AH != 0)
                                        cpu_state.flags |= (C_FLAG | V_FLAG);
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG));
                                cycles -= 70;
                                break;
                        case 0x28: /*IMUL AL,b*/
                                setznp8(AL);
                                tempws = (int)((int8_t)AL) * (int)((int8_t)temp);
                                AX = (uint16_t)(tempws & 0xFFFF);
                                if (AX != 0)
                                        cpu_state.flags &= unchecked((uint16_t)~Z_FLAG);
                                else
                                        cpu_state.flags |= Z_FLAG;
                                if (AH != 0)
                                        cpu_state.flags |= (C_FLAG | V_FLAG);
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG));
                                cycles -= 80;
                                break;
                        case 0x30: /*DIV AL,b*/
                                tempw = AX;
                                if (temp != 0)
                                {
                                        tempw2 = (uint16_t)(tempw % temp);
                                        AH = (uint8_t)tempw2;
                                        tempw /= temp;
                                        AL = (uint8_t)(tempw & 0xFF);
                                }
                                else
                                {
                                        // omitted: printf("DIVb BY 0 %04X:%04X\n", cs >> 4, cpu_state.pc) — sortie pure.
                                        writememw(ss, (uint32_t)((SP - 2) & 0xFFFF), (uint16_t)(cpu_state.flags | 0xF000));
                                        writememw(ss, (uint32_t)((SP - 4) & 0xFFFF), CS);
                                        writememw(ss, (uint32_t)((SP - 6) & 0xFFFF), (uint16_t)cpu_state.pc);
                                        SP -= 6;
                                        cpu_state.flags &= unchecked((uint16_t)~I_FLAG);
                                        cpu_state.flags &= unchecked((uint16_t)~T_FLAG);
                                        cpu_state.pc = readmemw(0, 0);
                                        x86seg_c.loadcs(readmemw(0, 2));
                                        FETCHCLEAR();
                                }
                                cycles -= 80;
                                break;
                        case 0x38: /*IDIV AL,b*/
                                // pcem bug, reproduced: PB-45 — `(int)AX` étend AX par des ZÉROS
                                //   (808x.c:3614) : un dividende négatif est lu comme un grand
                                //   positif. La forme mot, elle, signe (DX << 16 | AX).
                                tempws = (int)AX;
                                if (temp != 0)
                                {
                                        tempw2 = (uint16_t)(tempws % (int)((int8_t)temp));
                                        AH = (uint8_t)(tempw2 & 0xFF);
                                        tempws /= (int)((int8_t)temp);
                                        AL = (uint8_t)(tempws & 0xFF);
                                }
                                else
                                {
                                        // omitted: printf("IDIVb BY 0 %04X:%04X\n", cs >> 4, cpu_state.pc) — sortie pure.
                                        writememw(ss, (uint32_t)((SP - 2) & 0xFFFF), (uint16_t)(cpu_state.flags | 0xF000));
                                        writememw(ss, (uint32_t)((SP - 4) & 0xFFFF), CS);
                                        writememw(ss, (uint32_t)((SP - 6) & 0xFFFF), (uint16_t)cpu_state.pc);
                                        SP -= 6;
                                        cpu_state.flags &= unchecked((uint16_t)~I_FLAG);
                                        cpu_state.flags &= unchecked((uint16_t)~T_FLAG);
                                        cpu_state.pc = readmemw(0, 0);
                                        x86seg_c.loadcs(readmemw(0, 2));
                                        FETCHCLEAR();
                                }
                                cycles -= 101;
                                break;
                        }
                        break;

                case 0xF7:
                        fetchea();
                        tempw = geteaw();
                        switch (rmdat & 0x38)
                        {
                        case 0x00: /*TEST w*/
                        case 0x08:
                                tempw2 = getword();
                                setznp16((uint16_t)(tempw & tempw2));
                                cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG | A_FLAG));
                                cycles -= ((cpu_mod == 3) ? 5 : 11);
                                break;
                        case 0x10: /*NOT w*/
                                seteaw((uint16_t)~tempw);
                                cycles -= ((cpu_mod == 3) ? 3 : 24);
                                break;
                        case 0x18: /*NEG w*/
                                setsub16(0, tempw);
                                tempw = (uint16_t)(0 - tempw);
                                seteaw(tempw);
                                cycles -= ((cpu_mod == 3) ? 3 : 24);
                                break;
                        case 0x20: /*MUL AX,w*/
                                setznp16(AX);
                                templ = (uint32_t)(AX * tempw);
                                AX = (uint16_t)(templ & 0xFFFF);
                                DX = (uint16_t)(templ >> 16);
                                if ((AX | DX) != 0)
                                        cpu_state.flags &= unchecked((uint16_t)~Z_FLAG);
                                else
                                        cpu_state.flags |= Z_FLAG;
                                if (DX != 0)
                                        cpu_state.flags |= (C_FLAG | V_FLAG);
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG));
                                cycles -= 118;
                                break;
                        case 0x28: /*IMUL AX,w*/
                                setznp16(AX);
                                tempws = (int)((int16_t)AX) * (int)((int16_t)tempw);
                                if ((tempws >> 15) != 0 && ((tempws >> 15) != -1))
                                        cpu_state.flags |= (C_FLAG | V_FLAG);
                                else
                                        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG));
                                AX = (uint16_t)(tempws & 0xFFFF);
                                tempws = (uint16_t)(tempws >> 16);
                                DX = (uint16_t)(tempws & 0xFFFF);
                                if ((AX | DX) != 0)
                                        cpu_state.flags &= unchecked((uint16_t)~Z_FLAG);
                                else
                                        cpu_state.flags |= Z_FLAG;
                                cycles -= 128;
                                break;
                        case 0x30: /*DIV AX,w*/
                                templ = (uint32_t)((DX << 16) | AX);
                                if (tempw != 0)
                                {
                                        tempw2 = (uint16_t)(templ % tempw);
                                        DX = tempw2;
                                        templ /= tempw;
                                        AX = (uint16_t)(templ & 0xFFFF);
                                }
                                else
                                {
                                        // omitted: printf("DIVw BY 0 %04X:%04X\n", cs >> 4, cpu_state.pc) — sortie pure.
                                        writememw(ss, (uint32_t)((SP - 2) & 0xFFFF), (uint16_t)(cpu_state.flags | 0xF000));
                                        writememw(ss, (uint32_t)((SP - 4) & 0xFFFF), CS);
                                        writememw(ss, (uint32_t)((SP - 6) & 0xFFFF), (uint16_t)cpu_state.pc);
                                        SP -= 6;
                                        cpu_state.flags &= unchecked((uint16_t)~I_FLAG);
                                        cpu_state.flags &= unchecked((uint16_t)~T_FLAG);
                                        cpu_state.pc = readmemw(0, 0);
                                        x86seg_c.loadcs(readmemw(0, 2));
                                        FETCHCLEAR();
                                }
                                cycles -= 144;
                                break;
                        case 0x38: /*IDIV AX,w*/
                                tempws = (int)((DX << 16) | AX);
                                if (tempw != 0)
                                {
                                        tempw2 = (uint16_t)(tempws % (int)((int16_t)tempw));
                                        DX = tempw2;
                                        tempws /= (int)((int16_t)tempw);
                                        AX = (uint16_t)(tempws & 0xFFFF);
                                }
                                else
                                {
                                        // omitted: printf("IDIVw BY 0 %04X:%04X\n", cs >> 4, cpu_state.pc) — sortie pure.
                                        writememw(ss, (uint32_t)((SP - 2) & 0xFFFF), (uint16_t)(cpu_state.flags | 0xF000));
                                        writememw(ss, (uint32_t)((SP - 4) & 0xFFFF), CS);
                                        writememw(ss, (uint32_t)((SP - 6) & 0xFFFF), (uint16_t)cpu_state.pc);
                                        SP -= 6;
                                        cpu_state.flags &= unchecked((uint16_t)~I_FLAG);
                                        cpu_state.flags &= unchecked((uint16_t)~T_FLAG);
                                        cpu_state.pc = readmemw(0, 0);
                                        x86seg_c.loadcs(readmemw(0, 2));
                                        FETCHCLEAR();
                                }
                                cycles -= 165;
                                break;
                        }
                        break;

                case 0xF8: /*CLC*/
                        cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
                        cycles -= 2;
                        break;
                case 0xF9: /*STC*/
                        cpu_state.flags |= C_FLAG;
                        cycles -= 2;
                        break;
                case 0xFA: /*CLI*/
                        cpu_state.flags &= unchecked((uint16_t)~I_FLAG);
                        cycles -= 3;
                        break;
                case 0xFB: /*STI*/
                        cpu_state.flags |= I_FLAG;
                        cycles -= 2;
                        break;
                case 0xFC: /*CLD*/
                        cpu_state.flags &= unchecked((uint16_t)~D_FLAG);
                        cycles -= 2;
                        break;
                case 0xFD: /*STD*/
                        cpu_state.flags |= D_FLAG;
                        cycles -= 2;
                        break;

                case 0xFE: /*INC/DEC b*/
                        fetchea();
                        temp = geteab();
                        cpu_state.flags &= unchecked((uint16_t)~V_FLAG);
                        if ((rmdat & 0x38) != 0)
                        {
                                setsub8nc(temp, 1);
                                temp2 = (uint8_t)(temp - 1);
                                if ((temp & 0x80) != 0 && (temp2 & 0x80) == 0)
                                        cpu_state.flags |= V_FLAG;
                        }
                        else
                        {
                                setadd8nc(temp, 1);
                                temp2 = (uint8_t)(temp + 1);
                                if ((temp2 & 0x80) != 0 && (temp & 0x80) == 0)
                                        cpu_state.flags |= V_FLAG;
                        }
                        seteab(temp2);
                        cycles -= ((cpu_mod == 3) ? 3 : 23);
                        break;

                case 0xFF:
                        fetchea();
                        switch (rmdat & 0x38)
                        {
                        case 0x00: /*INC w*/
                                tempw = geteaw();
                                setadd16nc(tempw, 1);
                                seteaw((uint16_t)(tempw + 1));
                                cycles -= ((cpu_mod == 3) ? 3 : 23);
                                break;
                        case 0x08: /*DEC w*/
                                tempw = geteaw();
                                setsub16nc(tempw, 1);
                                seteaw((uint16_t)(tempw - 1));
                                cycles -= ((cpu_mod == 3) ? 3 : 23);
                                break;
                        case 0x10: /*CALL*/
                                tempw = geteaw();
                                if (cpu_state.ssegs != 0)
                                        ss = oldss;
                                writememw(ss, (uint32_t)((SP - 2) & 0xFFFF), (uint16_t)cpu_state.pc);
                                SP -= 2;
                                cpu_state.pc = tempw;
                                cycles -= ((cpu_mod == 3) ? 20 : 29);
                                FETCHCLEAR();
                                break;
                        case 0x18: /*CALL far*/
                                tempw = readmemw(easeg, (uint16_t)cpu_state.eaaddr);
                                tempw2 = readmemw(easeg, (uint16_t)((cpu_state.eaaddr + 2) & 0xFFFF));
                                tempw3 = CS;
                                tempw4 = (uint16_t)cpu_state.pc;
                                if (cpu_state.ssegs != 0)
                                        ss = oldss;
                                cpu_state.pc = tempw;
                                x86seg_c.loadcs(tempw2);
                                writememw(ss, (uint32_t)((SP - 2) & 0xFFFF), tempw3);
                                writememw(ss, ((uint32_t)((SP - 4) & 0xFFFF)), tempw4);
                                SP -= 4;
                                cycles -= 53;
                                FETCHCLEAR();
                                break;
                        case 0x20: /*JMP*/
                                cpu_state.pc = geteaw();
                                cycles -= ((cpu_mod == 3) ? 11 : 18);
                                FETCHCLEAR();
                                break;
                        case 0x28: /*JMP far*/
                                cpu_state.pc = readmemw(easeg, (uint16_t)cpu_state.eaaddr);
                                x86seg_c.loadcs(readmemw(easeg, (uint16_t)((cpu_state.eaaddr + 2) & 0xFFFF)));
                                cycles -= 24;
                                FETCHCLEAR();
                                break;
                        case 0x30: /*PUSH w*/
                                tempw = geteaw();
                                if (cpu_state.ssegs != 0)
                                        ss = oldss;
                                writememw(ss, ((uint32_t)((SP - 2) & 0xFFFF)), tempw);
                                SP -= 2;
                                cycles -= ((cpu_mod == 3) ? 15 : 24);
                                break;
                        }
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
