// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x86_ops_rep.h  (REP_OPS :3-518,
//         REP_OPS_CMPS_SCAS :521-733, opREPNE et opREPE :741-764)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — A11 : les deux emplacements F2 et F3, et les dix-huit
//         handlers de la table a16 vers lesquels ils aiguillent. Les formes
//         `_a32` et `L` restent dehors — mais VOIR L'AVERTISSEMENT ci-dessous :
//         elles ne sont pas inatteignables comme ailleurs dans ce jalon.
//
// UN CHEMIN ATTEINT LES FORMES 32 BITS, ET IL EST LAISSE OUVERT SCIEMMENT.
//
// ops_REPE et ops_REPNE ne sont PAS des tables du 286 : elles s'appellent
// `ops_REPE`, pas `ops_REPE_286`, et PCem les partage entre toutes les
// generations. Elles portent donc op_66_REPE et op_67_REPE, les prefixes de
// taille du 386, la ou ops_286 met ILLEGAL aux memes emplacements.
//
// Consequence mesuree : `F2 66 AD` pose op32 = 0x100 et atteint
// ops_REPNE[0x1AD] = opREP_LODSL_a16 — la forme 32 bits.
//   cycles consommes : oracle 112, C# 22
//
// CE CHEMIN N'EXISTE SUR AUCUN 286 REEL, ou 0x66 et 0x67 sont des opcodes
// invalides : c'est un artefact du partage de table. Les formes `_l` et `_a32`
// arriveront avec le jalon 386, qui en a besoin pour de vrai. D'ici la, le
// fuzzeur n'enchaine pas un prefixe de repetition sur 66 ni 67, et la raison
// est ecrite a cote du code qui l'evite (Fuzzer.cs).
//
// LA BOUCLE EST DANS LE HANDLER, et c'est le seul groupe de la table où ce soit
// le cas. Une opération de chaîne répétée ne rend pas la main entre deux tours :
// elle boucle jusqu'à ce que CX tombe à zéro OU que son budget de cycles soit
// épuisé, puis — si CX n'est pas nul — REMET pc SUR ELLE-MÊME et rend 1.
// L'instruction se ré-exécute donc, et le processeur reste interruptible entre
// deux tranches. C'est ce que `cpu_state.pc = cpu_state.oldpc` fait.
//
// LE BUDGET EST DE 100 CYCLES PAR TRANCHE : `cycles_end = cycles - ((is386 &&
// cpu_use_dynarec) ? 1000 : 100)`. Sur un 286 is386 est nul, donc toujours 100.
//
// ET LE PIÈGE PAS-À-PAS LE RAMÈNE À UN SEUL TOUR : `if (trap) cycles_end =
// cycles + 1`, ce qui fait sortir la boucle dès le premier passage. Sans cela,
// un débogueur ne verrait jamais l'intérieur d'un REP MOVSB.
//
// `ins++` PUIS `ins--` ENCADRENT LA BOUCLE, et ce n'est pas décoratif : chaque
// tour compte comme une instruction pour le compteur du cœur, mais la boucle en
// compte un de trop puisque exec386 en comptera un aussi à la sortie. `ins` EST
// dans le vecteur comparé depuis le palier (a) — se tromper ici rougirait le
// fuzzeur sans rapport avec la chaîne.
//
// QUATRE FAMILLES SANS BOUCLE. INS et OUTS répétés ne font QU'UN tour par
// appel — `if (CNT_REG > 0)` au lieu de `while` — et rendent 1 tant que CX
// n'est pas nul. Une E/S ne se met pas en rafale : chaque accès doit pouvoir
// être vu par le reste de la machine.
//
// CMPS ET SCAS RÉPÉTÉS TESTENT DEUX CONDITIONS. `FV` vaut 1 pour REPE et 0 pour
// REPNE, et la boucle continue tant que `ZF_SET() == FV`. D'où QUATRE tables
// distinctes là où les autres n'en ont qu'une — et c'est pourquoi CMPS et SCAS
// ont leur propre macro.
//
// DEVIATION: CHECK_WRITE_REP (386_common.h:88-92) fait `break` — il sort de la
//   boucle de l'appelant. Une méthode C# ne peut pas ; le test est donc écrit
//   en place dans chaque boucle qui le porte.

using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;
using static iXtal26.Cpu.x86_flags;

namespace iXtal26.Cpu;

internal static partial class _386
{
    // pcem: x86_ops_rep.h:741-752 — opREPNE, et :753-764 — opREPE.
    //
    // CE SONT DES PRÉFIXES, comme op_seg à A11 : ils vont chercher l'opcode
    // suivant et l'aiguillent — mais vers UNE AUTRE TABLE. Si l'opcode n'a pas
    // d'entrée répétée, ils retombent sur la table normale ; c'est ce que fait
    // le `if (x86_opcodes_REPNE[...])`, et il est VRAIMENT utile ici,
    // contrairement à op_seg où les deux tables sont la même.
    private static OpFn PrefixeRep(OpFn?[] table) => fetchdat =>
    {
        fetchdat = fastreadl(x86.cs + cpu_state.pc);
        if (cpu_state.abrt != 0)
                return 1;
        cpu_state.pc++;

        CLOCK_CYCLES(2);
        PREFETCH_PREFIX();
        if (table[(fetchdat & 0xff) | cpu_state.op32] != null)
                return table[(fetchdat & 0xff) | cpu_state.op32]!(fetchdat >> 8);
        return x86_opcodes![(fetchdat & 0xff) | cpu_state.op32](fetchdat >> 8);
    };

    internal static readonly OpFn?[] ops_REPE = new OpFn?[1024];
    internal static readonly OpFn?[] ops_REPNE = new OpFn?[1024];

    /// <summary>pcem: x86_ops_prefix.h:70-83 — op_seg(CS_REPE, …) et ses cinq
    /// sœurs, puis :84-97 pour REPNE.
    ///
    /// UN PRÉFIXE DE SEGMENT DOIT GARDER LE CONTEXTE DE RÉPÉTITION. `REP ES:
    /// MOVSB` est du code courant ; si le 26 retombait dans la table normale, le
    /// MOVSB qui suit ne serait plus répété. D'où ces variantes, qui aiguillent
    /// vers la table REPE ou REPNE au lieu de x86_opcodes — et ici le test
    /// `if (opcode_table[...])` sert VRAIMENT, contrairement à op_seg simple où
    /// les deux tables passées sont la même.
    ///
    /// Je les avais omises en écrivant « les tables REPE/REPNE sont posées par
    /// le groupe rep » — ce qui ne disait rien de leur CONTENU. Trouvé par le
    /// fuzzeur, en lisant ops_REPE[] dans la .so : 0x26, 0x2E, 0x36, 0x3E y
    /// nomment opES_REPE_w_a16 et ses sœurs.</summary>
    private static OpFn PrefixeSegmentRep(x86seg seg, OpFn?[] table) => fetchdat =>
    {
        fetchdat = fastreadl(x86.cs + cpu_state.pc);
        if (cpu_state.abrt != 0)
                return 1;
        cpu_state.pc++;

        cpu_state.ea_seg = seg;
        cpu_state.ssegs = 1;
        CLOCK_CYCLES(4);
        PREFETCH_PREFIX();

        if (table[fetchdat & 0xff] != null)
                return table[fetchdat & 0xff]!(fetchdat >> 8);
        return x86_opcodes![fetchdat & 0xff](fetchdat >> 8);
    };

    /// <summary>pcem: x86_ops_prefix.h:112-140 — op_66_REPE / op_67_REPE et
    /// leurs pendants REPNE.
    ///
    /// LES TABLES ops_REPE ET ops_REPNE NE SONT PAS PROPRES AU 286 : elles sont
    /// PARTAGÉES entre toutes les générations — `ops_REPE`, pas `ops_REPE_286`.
    /// Elles portent donc les préfixes 66 et 67 du 386, là où ops_286 met
    /// ILLEGAL aux mêmes emplacements. Conséquence mesurable : `67 xx` est un
    /// opcode invalide sur un 286, mais `F2 67 xx` ne l'est PAS — il pose op32
    /// et aiguille dans le quadrant 32 bits.
    ///
    /// C'est ce qui a rendu la recopie des quatre quadrants d'ops_286
    /// nécessaire (386.cs) : sans elle, l'index sortait de la table posée.
    ///
    /// `masque` vaut 0x100 pour le préfixe de DONNÉE (66) et 0x200 pour celui
    /// d'ADRESSE (67) ; l'autre bit est conservé tel quel.</summary>
    private static OpFn PrefixeTaille(uint32_t masque, OpFn?[] table) => fetchdat =>
    {
        fetchdat = fastreadl(x86.cs + cpu_state.pc);
        if (cpu_state.abrt != 0)
                return 1;
        cpu_state.pc++;

        cpu_state.op32 = ((use32 & masque) ^ masque) | (cpu_state.op32 & (masque == 0x100 ? 0x200u : 0x100u));
        CLOCK_CYCLES(2);
        PREFETCH_PREFIX();
        if (table[(fetchdat & 0xff) | cpu_state.op32] != null)
                return table[(fetchdat & 0xff) | cpu_state.op32]!(fetchdat >> 8);
        return x86_opcodes![(fetchdat & 0xff) | cpu_state.op32](fetchdat >> 8);
    };

    // pcem: x86_ops_rep.h — opREP_INSB_a16. UN SEUL TOUR par appel.
    private static int opREP_INSB_a16(uint32_t fetchdat)
    {
        int reads = 0, writes = 0, total_cycles = 0;

        if (CX > 0)
        {
                uint8_t temp;

                if (SEG_CHECK_WRITE(cpu_state.seg_es)) return 1;
        if (check_io_perm(DX)) return 1;
                temp = (uint8_t)io.inb(DX);
                writememb(es, DI, temp);
                if (cpu_state.abrt != 0)
                        return 1;

                if ((cpu_state.flags & D_FLAG) != 0)
                        DI -= 1;
                else
                        DI += 1;
                CX--;
                cycles -= 15;
                reads++;
                writes++;
                total_cycles += 15;
        }
        PREFETCH_RUN(total_cycles, 1, -1, reads, 0, writes, 0, 0);
        if (CX > 0)
        {
                CPU_BLOCK_END();
                cpu_state.pc = cpu_state.oldpc;
                return 1;
        }
        return cpu_state.abrt;
    }

    // pcem: x86_ops_rep.h — opREP_INSW_a16. UN SEUL TOUR par appel.
    private static int opREP_INSW_a16(uint32_t fetchdat)
    {
        int reads = 0, writes = 0, total_cycles = 0;

        if (CX > 0)
        {
                uint16_t temp;

                if (SEG_CHECK_WRITE(cpu_state.seg_es)) return 1;
        if (check_io_perm(DX)) return 1;
        if (check_io_perm((uint16_t)(DX + 1))) return 1;
                temp = (uint16_t)io.inw(DX);
                writememw(es, DI, temp);
                if (cpu_state.abrt != 0)
                        return 1;

                if ((cpu_state.flags & D_FLAG) != 0)
                        DI -= 2;
                else
                        DI += 2;
                CX--;
                cycles -= 15;
                reads++;
                writes++;
                total_cycles += 15;
        }
        PREFETCH_RUN(total_cycles, 1, -1, reads, 0, writes, 0, 0);
        if (CX > 0)
        {
                CPU_BLOCK_END();
                cpu_state.pc = cpu_state.oldpc;
                return 1;
        }
        return cpu_state.abrt;
    }

    // pcem: x86_ops_rep.h — opREP_OUTSB_a16. UN SEUL TOUR par appel.
    private static int opREP_OUTSB_a16(uint32_t fetchdat)
    {
        int reads = 0, writes = 0, total_cycles = 0;

        if (CX > 0)
        {
                uint8_t temp;
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                temp = readmemb(cpu_state.ea_seg!.@base, SI);
                if (cpu_state.abrt != 0)
                        return 1;
                if (check_io_perm(DX)) return 1;
                io.outb(DX, temp);
                if ((cpu_state.flags & D_FLAG) != 0)
                        SI -= 1;
                else
                        SI += 1;
                CX--;
                cycles -= 14;
                reads++;
                writes++;
                total_cycles += 14;
        }
        PREFETCH_RUN(total_cycles, 1, -1, reads, 0, writes, 0, 0);
        if (CX > 0)
        {
                CPU_BLOCK_END();
                cpu_state.pc = cpu_state.oldpc;
                return 1;
        }
        return cpu_state.abrt;
    }

    // pcem: x86_ops_rep.h — opREP_OUTSW_a16. UN SEUL TOUR par appel.
    private static int opREP_OUTSW_a16(uint32_t fetchdat)
    {
        int reads = 0, writes = 0, total_cycles = 0;

        if (CX > 0)
        {
                uint16_t temp;
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                temp = readmemw(cpu_state.ea_seg!.@base, SI);
                if (cpu_state.abrt != 0)
                        return 1;
                if (check_io_perm(DX)) return 1;
                if (check_io_perm((uint16_t)(DX + 1))) return 1;
                io.outw(DX, temp);
                if ((cpu_state.flags & D_FLAG) != 0)
                        SI -= 2;
                else
                        SI += 2;
                CX--;
                cycles -= 14;
                reads++;
                writes++;
                total_cycles += 14;
        }
        PREFETCH_RUN(total_cycles, 1, -1, reads, 0, writes, 0, 0);
        if (CX > 0)
        {
                CPU_BLOCK_END();
                cpu_state.pc = cpu_state.oldpc;
                return 1;
        }
        return cpu_state.abrt;
    }
    // pcem: x86_ops_rep.h — opREP_MOVSB_a16
    private static int opREP_MOVSB_a16(uint32_t fetchdat)
    {
        int reads = 0, writes = 0, total_cycles = 0;
        int cycles_end = cycles - ((is386 != 0 && cpu_c.cpu_use_dynarec != 0) ? 1000 : 100);
        if (trap != 0)
                cycles_end = cycles + 1; /*Force the instruction to end after only one iteration when trap flag set*/
        if (CX > 0)
        {
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                if (SEG_CHECK_WRITE(cpu_state.seg_es)) return 1;
        }
        while (CX > 0)
        {
                uint8_t temp;

                // CHECK_WRITE_REP, deplie : le macro fait `break`.
                if (DI < cpu_state.seg_es.limit_low || DI > cpu_state.seg_es.limit_high)
                {
                        x86seg_c.x86gpf("Limit check", 0);
                        break;
                }
                temp = readmemb(cpu_state.ea_seg!.@base, SI);
                if (cpu_state.abrt != 0)
                        return 1;
                writememb(es, DI, temp);
                if (cpu_state.abrt != 0)
                        return 1;

                if ((cpu_state.flags & D_FLAG) != 0)
                {
                        DI -= 1;
                        SI -= 1;
                }
                else
                {
                        DI += 1;
                        SI += 1;
                }
                CX--;
                cycles -= is486 != 0 ? 3 : 4;
                _808x.ins++;
                reads++;
                writes++;
                total_cycles += is486 != 0 ? 3 : 4;
                if (cycles < cycles_end)
                        break;
        }
        _808x.ins--;
        PREFETCH_RUN(total_cycles, 1, -1, reads, 0, writes, 0, 0);
        if (CX > 0)
        {
                CPU_BLOCK_END();
                cpu_state.pc = cpu_state.oldpc;
                return 1;
        }
        return cpu_state.abrt;
    }

    // pcem: x86_ops_rep.h — opREP_MOVSW_a16
    private static int opREP_MOVSW_a16(uint32_t fetchdat)
    {
        int reads = 0, writes = 0, total_cycles = 0;
        int cycles_end = cycles - ((is386 != 0 && cpu_c.cpu_use_dynarec != 0) ? 1000 : 100);
        if (trap != 0)
                cycles_end = cycles + 1; /*Force the instruction to end after only one iteration when trap flag set*/
        if (CX > 0)
        {
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                if (SEG_CHECK_WRITE(cpu_state.seg_es)) return 1;
        }
        while (CX > 0)
        {
                uint16_t temp;

                // CHECK_WRITE_REP, deplie : le macro fait `break`.
                if (DI < cpu_state.seg_es.limit_low || DI > cpu_state.seg_es.limit_high)
                {
                        x86seg_c.x86gpf("Limit check", 0);
                        break;
                }
                temp = readmemw(cpu_state.ea_seg!.@base, SI);
                if (cpu_state.abrt != 0)
                        return 1;
                writememw(es, DI, temp);
                if (cpu_state.abrt != 0)
                        return 1;

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
                CX--;
                cycles -= is486 != 0 ? 3 : 4;
                _808x.ins++;
                reads++;
                writes++;
                total_cycles += is486 != 0 ? 3 : 4;
                if (cycles < cycles_end)
                        break;
        }
        _808x.ins--;
        PREFETCH_RUN(total_cycles, 1, -1, reads, 0, writes, 0, 0);
        if (CX > 0)
        {
                CPU_BLOCK_END();
                cpu_state.pc = cpu_state.oldpc;
                return 1;
        }
        return cpu_state.abrt;
    }

    // pcem: x86_ops_rep.h — opREP_STOSB_a16
    private static int opREP_STOSB_a16(uint32_t fetchdat)
    {
        int writes = 0, total_cycles = 0;
        int cycles_end = cycles - ((is386 != 0 && cpu_c.cpu_use_dynarec != 0) ? 1000 : 100);
        if (trap != 0)
                cycles_end = cycles + 1; /*Force the instruction to end after only one iteration when trap flag set*/
        if (CX > 0)
                if (SEG_CHECK_WRITE(cpu_state.seg_es)) return 1;
        while (CX > 0)
        {
                // CHECK_WRITE_REP borne a `DI` — et MOVSW, qui ecrit AUSSI deux
                // octets, se borne a DI tout court (x86_ops_rep.h:249 contre :368).
                // Incoherence de PCem, observable a une limite de segment.
                if (DI < cpu_state.seg_es.limit_low || DI > cpu_state.seg_es.limit_high)
                {
                        x86seg_c.x86gpf("Limit check", 0);
                        break;
                }
                writememb(es, DI, AL);
                if (cpu_state.abrt != 0)
                        return 1;
                if ((cpu_state.flags & D_FLAG) != 0)
                        DI -= 1;
                else
                        DI += 1;
                CX--;
                cycles -= is486 != 0 ? 4 : 5;
                writes++;
                total_cycles += is486 != 0 ? 4 : 5;
                _808x.ins++;
                if (cycles < cycles_end)
                        break;
        }
        PREFETCH_RUN(total_cycles, 1, -1, 0, 0, writes, 0, 0);
        if (CX > 0)
        {
                CPU_BLOCK_END();
                cpu_state.pc = cpu_state.oldpc;
                return 1;
        }
        return cpu_state.abrt;
    }

    // pcem: x86_ops_rep.h — opREP_STOSW_a16
    private static int opREP_STOSW_a16(uint32_t fetchdat)
    {
        int writes = 0, total_cycles = 0;
        int cycles_end = cycles - ((is386 != 0 && cpu_c.cpu_use_dynarec != 0) ? 1000 : 100);
        if (trap != 0)
                cycles_end = cycles + 1; /*Force the instruction to end after only one iteration when trap flag set*/
        if (CX > 0)
                if (SEG_CHECK_WRITE(cpu_state.seg_es)) return 1;
        while (CX > 0)
        {
                // CHECK_WRITE_REP borne a `DI + 1` — et MOVSW, qui ecrit AUSSI deux
                // octets, se borne a DI tout court (x86_ops_rep.h:249 contre :368).
                // Incoherence de PCem, observable a une limite de segment.
                if (DI < cpu_state.seg_es.limit_low || DI + 1 > cpu_state.seg_es.limit_high)
                {
                        x86seg_c.x86gpf("Limit check", 0);
                        break;
                }
                writememw(es, DI, AX);
                if (cpu_state.abrt != 0)
                        return 1;
                if ((cpu_state.flags & D_FLAG) != 0)
                        DI -= 2;
                else
                        DI += 2;
                CX--;
                cycles -= is486 != 0 ? 4 : 5;
                writes++;
                total_cycles += is486 != 0 ? 4 : 5;
                _808x.ins++;
                if (cycles < cycles_end)
                        break;
        }
        PREFETCH_RUN(total_cycles, 1, -1, 0, 0, writes, 0, 0);
        if (CX > 0)
        {
                CPU_BLOCK_END();
                cpu_state.pc = cpu_state.oldpc;
                return 1;
        }
        return cpu_state.abrt;
    }

    // pcem: x86_ops_rep.h — opREP_LODSB_a16. PAS de ins-- final : PCem ne le
    // met que sur MOVS, CMPS et SCAS. Transcrit tel quel.
    private static int opREP_LODSB_a16(uint32_t fetchdat)
    {
        int reads = 0, total_cycles = 0;
        int cycles_end = cycles - ((is386 != 0 && cpu_c.cpu_use_dynarec != 0) ? 1000 : 100);
        if (trap != 0)
                cycles_end = cycles + 1; /*Force the instruction to end after only one iteration when trap flag set*/
        if (CX > 0)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        while (CX > 0)
        {
                AL = readmemb(cpu_state.ea_seg!.@base, SI);
                if (cpu_state.abrt != 0)
                        return 1;
                if ((cpu_state.flags & D_FLAG) != 0)
                        SI -= 1;
                else
                        SI += 1;
                CX--;
                cycles -= is486 != 0 ? 4 : 5;
                reads++;
                total_cycles += is486 != 0 ? 4 : 5;
                _808x.ins++;
                if (cycles < cycles_end)
                        break;
        }
        PREFETCH_RUN(total_cycles, 1, -1, reads, 0, 0, 0, 0);
        if (CX > 0)
        {
                CPU_BLOCK_END();
                cpu_state.pc = cpu_state.oldpc;
                return 1;
        }
        return cpu_state.abrt;
    }

    // pcem: x86_ops_rep.h — opREP_LODSW_a16. PAS de ins-- final : PCem ne le
    // met que sur MOVS, CMPS et SCAS. Transcrit tel quel.
    private static int opREP_LODSW_a16(uint32_t fetchdat)
    {
        int reads = 0, total_cycles = 0;
        int cycles_end = cycles - ((is386 != 0 && cpu_c.cpu_use_dynarec != 0) ? 1000 : 100);
        if (trap != 0)
                cycles_end = cycles + 1; /*Force the instruction to end after only one iteration when trap flag set*/
        if (CX > 0)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        while (CX > 0)
        {
                AX = readmemw(cpu_state.ea_seg!.@base, SI);
                if (cpu_state.abrt != 0)
                        return 1;
                if ((cpu_state.flags & D_FLAG) != 0)
                        SI -= 2;
                else
                        SI += 2;
                CX--;
                cycles -= is486 != 0 ? 4 : 5;
                reads++;
                total_cycles += is486 != 0 ? 4 : 5;
                _808x.ins++;
                if (cycles < cycles_end)
                        break;
        }
        PREFETCH_RUN(total_cycles, 1, -1, reads, 0, 0, 0, 0);
        if (CX > 0)
        {
                CPU_BLOCK_END();
                cpu_state.pc = cpu_state.oldpc;
                return 1;
        }
        return cpu_state.abrt;
    }
    // pcem: x86_ops_rep.h — opREP_CMPSB_a16_NE. UN SEUL TOUR par appel, comme
    // INS et OUTS — CMPS repete n'a pas de boucle interne dans PCem.
    private static int opREP_CMPSB_a16_NE(uint32_t fetchdat)
    {
        int reads = 0, total_cycles = 0, tempz;

        tempz = 0;
        if ((CX > 0) && (0 == tempz))
        {
                uint8_t temp, temp2;
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                if (SEG_CHECK_READ(cpu_state.seg_es)) return 1;
                temp = readmemb(cpu_state.ea_seg!.@base, SI);
                temp2 = readmemb(es, DI);
                if (cpu_state.abrt != 0)
                        return 1;

                if ((cpu_state.flags & D_FLAG) != 0)
                {
                        DI -= 1;
                        SI -= 1;
                }
                else
                {
                        DI += 1;
                        SI += 1;
                }
                CX--;
                cycles -= is486 != 0 ? 7 : 9;
                reads += 2;
                total_cycles += is486 != 0 ? 7 : 9;
                setsub8(temp, temp2);
                tempz = (ZF_SET() != 0) ? 1 : 0;
        }
        PREFETCH_RUN(total_cycles, 1, -1, reads, 0, 0, 0, 0);
        if ((CX > 0) && (0 == tempz))
        {
                CPU_BLOCK_END();
                cpu_state.pc = cpu_state.oldpc;
                return 1;
        }
        return cpu_state.abrt;
    }

    // pcem: x86_ops_rep.h — opREP_CMPSB_a16_E. UN SEUL TOUR par appel, comme
    // INS et OUTS — CMPS repete n'a pas de boucle interne dans PCem.
    private static int opREP_CMPSB_a16_E(uint32_t fetchdat)
    {
        int reads = 0, total_cycles = 0, tempz;

        tempz = 1;
        if ((CX > 0) && (1 == tempz))
        {
                uint8_t temp, temp2;
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                if (SEG_CHECK_READ(cpu_state.seg_es)) return 1;
                temp = readmemb(cpu_state.ea_seg!.@base, SI);
                temp2 = readmemb(es, DI);
                if (cpu_state.abrt != 0)
                        return 1;

                if ((cpu_state.flags & D_FLAG) != 0)
                {
                        DI -= 1;
                        SI -= 1;
                }
                else
                {
                        DI += 1;
                        SI += 1;
                }
                CX--;
                cycles -= is486 != 0 ? 7 : 9;
                reads += 2;
                total_cycles += is486 != 0 ? 7 : 9;
                setsub8(temp, temp2);
                tempz = (ZF_SET() != 0) ? 1 : 0;
        }
        PREFETCH_RUN(total_cycles, 1, -1, reads, 0, 0, 0, 0);
        if ((CX > 0) && (1 == tempz))
        {
                CPU_BLOCK_END();
                cpu_state.pc = cpu_state.oldpc;
                return 1;
        }
        return cpu_state.abrt;
    }

    // pcem: x86_ops_rep.h — opREP_CMPSW_a16_NE. UN SEUL TOUR par appel, comme
    // INS et OUTS — CMPS repete n'a pas de boucle interne dans PCem.
    private static int opREP_CMPSW_a16_NE(uint32_t fetchdat)
    {
        int reads = 0, total_cycles = 0, tempz;

        tempz = 0;
        if ((CX > 0) && (0 == tempz))
        {
                uint16_t temp, temp2;
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                if (SEG_CHECK_READ(cpu_state.seg_es)) return 1;
                temp = readmemw(cpu_state.ea_seg!.@base, SI);
                temp2 = readmemw(es, DI);
                if (cpu_state.abrt != 0)
                        return 1;

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
                CX--;
                cycles -= is486 != 0 ? 7 : 9;
                reads += 2;
                total_cycles += is486 != 0 ? 7 : 9;
                setsub16(temp, temp2);
                tempz = (ZF_SET() != 0) ? 1 : 0;
        }
        PREFETCH_RUN(total_cycles, 1, -1, reads, 0, 0, 0, 0);
        if ((CX > 0) && (0 == tempz))
        {
                CPU_BLOCK_END();
                cpu_state.pc = cpu_state.oldpc;
                return 1;
        }
        return cpu_state.abrt;
    }

    // pcem: x86_ops_rep.h — opREP_CMPSW_a16_E. UN SEUL TOUR par appel, comme
    // INS et OUTS — CMPS repete n'a pas de boucle interne dans PCem.
    private static int opREP_CMPSW_a16_E(uint32_t fetchdat)
    {
        int reads = 0, total_cycles = 0, tempz;

        tempz = 1;
        if ((CX > 0) && (1 == tempz))
        {
                uint16_t temp, temp2;
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                if (SEG_CHECK_READ(cpu_state.seg_es)) return 1;
                temp = readmemw(cpu_state.ea_seg!.@base, SI);
                temp2 = readmemw(es, DI);
                if (cpu_state.abrt != 0)
                        return 1;

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
                CX--;
                cycles -= is486 != 0 ? 7 : 9;
                reads += 2;
                total_cycles += is486 != 0 ? 7 : 9;
                setsub16(temp, temp2);
                tempz = (ZF_SET() != 0) ? 1 : 0;
        }
        PREFETCH_RUN(total_cycles, 1, -1, reads, 0, 0, 0, 0);
        if ((CX > 0) && (1 == tempz))
        {
                CPU_BLOCK_END();
                cpu_state.pc = cpu_state.oldpc;
                return 1;
        }
        return cpu_state.abrt;
    }

    // pcem: x86_ops_rep.h — opREP_SCASB_a16_NE. AVEC boucle, contrairement a CMPS.
    private static int opREP_SCASB_a16_NE(uint32_t fetchdat)
    {
        int reads = 0, total_cycles = 0, tempz;
        int cycles_end = cycles - ((is386 != 0 && cpu_c.cpu_use_dynarec != 0) ? 1000 : 100);
        if (trap != 0)
                cycles_end = cycles + 1; /*Force the instruction to end after only one iteration when trap flag set*/
        tempz = 0;
        if ((CX > 0) && (0 == tempz))
                if (SEG_CHECK_READ(cpu_state.seg_es)) return 1;
        while ((CX > 0) && (0 == tempz))
        {
                uint8_t temp = readmemb(es, DI);
                if (cpu_state.abrt != 0)
                        break;
                setsub8(AL, temp);
                tempz = (ZF_SET() != 0) ? 1 : 0;
                if ((cpu_state.flags & D_FLAG) != 0)
                        DI -= 1;
                else
                        DI += 1;
                CX--;
                cycles -= is486 != 0 ? 5 : 8;
                reads++;
                total_cycles += is486 != 0 ? 5 : 8;
                _808x.ins++;
                if (cycles < cycles_end)
                        break;
        }
        _808x.ins--;
        PREFETCH_RUN(total_cycles, 1, -1, reads, 0, 0, 0, 0);
        if ((CX > 0) && (0 == tempz))
        {
                CPU_BLOCK_END();
                cpu_state.pc = cpu_state.oldpc;
                return 1;
        }
        return cpu_state.abrt;
    }

    // pcem: x86_ops_rep.h — opREP_SCASB_a16_E. AVEC boucle, contrairement a CMPS.
    private static int opREP_SCASB_a16_E(uint32_t fetchdat)
    {
        int reads = 0, total_cycles = 0, tempz;
        int cycles_end = cycles - ((is386 != 0 && cpu_c.cpu_use_dynarec != 0) ? 1000 : 100);
        if (trap != 0)
                cycles_end = cycles + 1; /*Force the instruction to end after only one iteration when trap flag set*/
        tempz = 1;
        if ((CX > 0) && (1 == tempz))
                if (SEG_CHECK_READ(cpu_state.seg_es)) return 1;
        while ((CX > 0) && (1 == tempz))
        {
                uint8_t temp = readmemb(es, DI);
                if (cpu_state.abrt != 0)
                        break;
                setsub8(AL, temp);
                tempz = (ZF_SET() != 0) ? 1 : 0;
                if ((cpu_state.flags & D_FLAG) != 0)
                        DI -= 1;
                else
                        DI += 1;
                CX--;
                cycles -= is486 != 0 ? 5 : 8;
                reads++;
                total_cycles += is486 != 0 ? 5 : 8;
                _808x.ins++;
                if (cycles < cycles_end)
                        break;
        }
        _808x.ins--;
        PREFETCH_RUN(total_cycles, 1, -1, reads, 0, 0, 0, 0);
        if ((CX > 0) && (1 == tempz))
        {
                CPU_BLOCK_END();
                cpu_state.pc = cpu_state.oldpc;
                return 1;
        }
        return cpu_state.abrt;
    }

    // pcem: x86_ops_rep.h — opREP_SCASW_a16_NE. AVEC boucle, contrairement a CMPS.
    private static int opREP_SCASW_a16_NE(uint32_t fetchdat)
    {
        int reads = 0, total_cycles = 0, tempz;
        int cycles_end = cycles - ((is386 != 0 && cpu_c.cpu_use_dynarec != 0) ? 1000 : 100);
        if (trap != 0)
                cycles_end = cycles + 1; /*Force the instruction to end after only one iteration when trap flag set*/
        tempz = 0;
        if ((CX > 0) && (0 == tempz))
                if (SEG_CHECK_READ(cpu_state.seg_es)) return 1;
        while ((CX > 0) && (0 == tempz))
        {
                uint16_t temp = readmemw(es, DI);
                if (cpu_state.abrt != 0)
                        break;
                setsub16(AX, temp);
                tempz = (ZF_SET() != 0) ? 1 : 0;
                if ((cpu_state.flags & D_FLAG) != 0)
                        DI -= 2;
                else
                        DI += 2;
                CX--;
                cycles -= is486 != 0 ? 5 : 8;
                reads++;
                total_cycles += is486 != 0 ? 5 : 8;
                _808x.ins++;
                if (cycles < cycles_end)
                        break;
        }
        _808x.ins--;
        PREFETCH_RUN(total_cycles, 1, -1, reads, 0, 0, 0, 0);
        if ((CX > 0) && (0 == tempz))
        {
                CPU_BLOCK_END();
                cpu_state.pc = cpu_state.oldpc;
                return 1;
        }
        return cpu_state.abrt;
    }

    // pcem: x86_ops_rep.h — opREP_SCASW_a16_E. AVEC boucle, contrairement a CMPS.
    private static int opREP_SCASW_a16_E(uint32_t fetchdat)
    {
        int reads = 0, total_cycles = 0, tempz;
        int cycles_end = cycles - ((is386 != 0 && cpu_c.cpu_use_dynarec != 0) ? 1000 : 100);
        if (trap != 0)
                cycles_end = cycles + 1; /*Force the instruction to end after only one iteration when trap flag set*/
        tempz = 1;
        if ((CX > 0) && (1 == tempz))
                if (SEG_CHECK_READ(cpu_state.seg_es)) return 1;
        while ((CX > 0) && (1 == tempz))
        {
                uint16_t temp = readmemw(es, DI);
                if (cpu_state.abrt != 0)
                        break;
                setsub16(AX, temp);
                tempz = (ZF_SET() != 0) ? 1 : 0;
                if ((cpu_state.flags & D_FLAG) != 0)
                        DI -= 2;
                else
                        DI += 2;
                CX--;
                cycles -= is486 != 0 ? 5 : 8;
                reads++;
                total_cycles += is486 != 0 ? 5 : 8;
                _808x.ins++;
                if (cycles < cycles_end)
                        break;
        }
        _808x.ins--;
        PREFETCH_RUN(total_cycles, 1, -1, reads, 0, 0, 0, 0);
        if ((CX > 0) && (1 == tempz))
        {
                CPU_BLOCK_END();
                cpu_state.pc = cpu_state.oldpc;
                return 1;
        }
        return cpu_state.abrt;
    }

    // omitted: toutes les formes `_a32` et `L` (INSL, OUTSL, MOVSL, STOSL,
    //   LODSL, CMPSL, SCASL) — op32 nul sur un 286.

    /// <summary>pcem: cpu.c:232-233 — `x86_opcodes_REPE = ops_REPE` et son
    /// pendant REPNE, plus les emplacements F2 et F3 de ops_286.
    ///
    /// LES DEUX TABLES NE DIFFERENT QUE PAR CMPS ET SCAS : partout ailleurs
    /// REPE et REPNE font la meme chose, et c'est pourquoi PCem les remplit avec
    /// les memes handlers sauf sur A6, A7, AE, AF.</summary>
    private static void PoserGroupeRep()
    {
        foreach (var t in new[] { ops_REPE, ops_REPNE })
        {
                t[0x6C] = opREP_INSB_a16;
                t[0x6D] = opREP_INSW_a16;
                t[0x6E] = opREP_OUTSB_a16;
                t[0x6F] = opREP_OUTSW_a16;
                t[0xA4] = opREP_MOVSB_a16;
                t[0xA5] = opREP_MOVSW_a16;
                t[0xAA] = opREP_STOSB_a16;
                t[0xAB] = opREP_STOSW_a16;
                t[0xAC] = opREP_LODSB_a16;
                t[0xAD] = opREP_LODSW_a16;
        }

        ops_REPE[0xA6] = opREP_CMPSB_a16_E;
        ops_REPE[0xA7] = opREP_CMPSW_a16_E;
        ops_REPE[0xAE] = opREP_SCASB_a16_E;
        ops_REPE[0xAF] = opREP_SCASW_a16_E;

        ops_REPNE[0xA6] = opREP_CMPSB_a16_NE;
        ops_REPNE[0xA7] = opREP_CMPSW_a16_NE;
        ops_REPNE[0xAE] = opREP_SCASB_a16_NE;
        ops_REPNE[0xAF] = opREP_SCASW_a16_NE;

        // Les prefixes, dans CHAQUE table, pointant vers ELLE-MEME.
        foreach (var (t, nom) in new[] { (ops_REPE, "E"), (ops_REPNE, "NE") })
        {
                t[0x26] = PrefixeSegmentRep(cpu_state.seg_es, t);
                t[0x2E] = PrefixeSegmentRep(cpu_state.seg_cs, t);
                t[0x36] = PrefixeSegmentRep(cpu_state.seg_ss, t);
                t[0x3E] = PrefixeSegmentRep(cpu_state.seg_ds, t);
                t[0x64] = PrefixeSegmentRep(cpu_state.seg_fs, t);
                t[0x65] = PrefixeSegmentRep(cpu_state.seg_gs, t);
                t[0x66] = PrefixeTaille(0x100, t);
                t[0x67] = PrefixeTaille(0x200, t);
        }

        ops_286[0xF2] = PrefixeRep(ops_REPNE);
        ops_286[0xF3] = PrefixeRep(ops_REPE);
    }
}
