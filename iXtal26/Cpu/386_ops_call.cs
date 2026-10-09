// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x86_ops_call.h  (lignes 3-251)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — A6 : les deux emplacements qu'un 286 atteint, 9A et FF.
//         Les formes `_l` et `_a32` restent dehors, déclarées au registre des
//         omissions.
//
// L'APPEL LOINTAIN, ET LE GROUPE FF.
//
// Cet en-tête ne porte QUE les formes lointaines — CALL far et l'aiguillage FF.
// CALL et RET PROCHES vivent dans x86_ops_jump.h et ont été posés à A5 : le
// découpage de PCem suit la mécanique, pas la mnémonique.
//
// FF N'EST PAS UN OPCODE, C'EST SEPT. Comme le groupe immédiat 80/81/83, il
// aiguille sur les trois bits `reg` du ModRM — mais là où ARITH_MULTI enchaîne
// huit variantes de la même opération, FF réunit des instructions sans rapport :
// INC et DEC vers adresse effective, CALL proche indirect, CALL lointain
// indirect, JMP proche indirect, JMP lointain indirect, PUSH vers adresse
// effective. Et son `default:` est un opcode ILLÉGAL, pas un huitième cas.

using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;
using static iXtal26.Cpu.x86_flags;

namespace iXtal26.Cpu;

internal static partial class _386
{
    /// <summary>pcem: x86_ops_call.h:3-96 — la macro CALL_FAR_w.
    ///
    /// DEVIATION: le macro fait `return 1` depuis son milieu ; une méthode C# ne
    ///   peut pas rendre la main pour son appelant. Même idiome que fetch_ea_16
    ///   et ARITH_MULTI : elle rend `true` pour « l'appelant doit rendre 1 ».
    ///
    /// CGATE32 EST NUL EN MODE RÉEL, donc l'empilement passe par la branche
    /// `else` : PUSH_W deux fois, seize bits. La branche PUSH_L n'est atteinte
    /// qu'à travers une porte d'appel 32 bits, que seul loadcscall pose — et
    /// loadcscall échoue en se nommant tant que le mode protégé n'est pas
    /// transcrit. On la porte quand même : c'est elle qui rendra le bloc C
    /// additif, et l'omettre ferait diverger la transcription.
    ///
    /// `cgate16` n'est PAS lu par cette macro — seulement par les PREFETCH_RUN
    /// de ses deux appelants, qui s'en servent pour décider s'ils comptent deux
    /// écritures de mot ou deux de long. La macro, elle, ne teste que cgate32.</summary>
    private static bool CALL_FAR_w(uint16_t new_seg, uint16_t new_pc,
                                   out uint16_t old_cs, out uint32_t old_pc)
    {
        old_cs = CS;
        old_pc = cpu_state.pc;
        cpu_state.pc = new_pc;
        optype = CALL;
        cgate16 = cgate32 = 0;
        if ((msw & 1) != 0)
                // pcem bug, fixed in hardware mode: PB-40 — les empilements du retour suivent loadcscall :
                //   sur une TSS, ils atterrissent sur la pile de la NOUVELLE tâche.
                x86seg_c.loadcscall(new_seg, old_pc);
        else
        {
                x86seg_c.loadcs(new_seg);
                cycles -= cpu_c.timing_call_rm;
        }
        optype = 0;
        if (cpu_state.abrt != 0)
        {
                cgate16 = cgate32 = 0;
                return true;
        }
        oldss = ss;
        if (materiel.pb_40)
                if (_386_materiel.appel_de_tache_materiel()) return false;
        if (cgate32 != 0)
        {
                uint32_t old_esp = ESP;
                PUSH_L(old_cs);
                if (cpu_state.abrt != 0)
                {
                        CS = old_cs;
                        cgate16 = cgate32 = 0;
                        return true;
                }
                PUSH_L(old_pc);
                if (cpu_state.abrt != 0)
                {
                        CS = old_cs;
                        ESP = old_esp;
                        return true;
                }
        }
        else
        {
                uint32_t old_esp = ESP;
                PUSH_W(old_cs);
                if (cpu_state.abrt != 0)
                {
                        CS = old_cs;
                        cgate16 = cgate32 = 0;
                        return true;
                }
                PUSH_W((uint16_t)old_pc);
                if (cpu_state.abrt != 0)
                {
                        CS = old_cs;
                        ESP = old_esp;
                        return true;
                }
        }
        return false;
    }

    /// <summary>pcem: x86_ops_call.h:51-97 — CALL_FAR_l (G2, D2). Même corps que
    /// CALL_FAR_w, test inversé : empile en MOTS seulement si cgate16.</summary>
    private static bool CALL_FAR_l(uint16_t new_seg, uint32_t new_pc,
                                   out uint16_t old_cs, out uint32_t old_pc)
    {
        old_cs = CS;
        old_pc = cpu_state.pc;
        cpu_state.pc = new_pc;
        optype = CALL;
        cgate16 = cgate32 = 0;
        if ((msw & 1) != 0)
                // pcem bug, fixed in hardware mode: PB-40 — les empilements du retour suivent loadcscall :
                //   sur une TSS, ils atterrissent sur la pile de la NOUVELLE tâche.
                x86seg_c.loadcscall(new_seg, old_pc);
        else
        {
                x86seg_c.loadcs(new_seg);
                cycles -= cpu_c.timing_call_rm;
        }
        optype = 0;
        if (cpu_state.abrt != 0)
        {
                cgate16 = cgate32 = 0;
                return true;
        }
        oldss = ss;
        if (materiel.pb_40)
                if (_386_materiel.appel_de_tache_materiel()) return false;
        if (cgate16 != 0)
        {
                uint32_t old_esp = ESP;
                PUSH_W(old_cs);
                if (cpu_state.abrt != 0)
                {
                        CS = old_cs;
                        cgate16 = cgate32 = 0;
                        return true;
                }
                PUSH_W((uint16_t)old_pc);
                if (cpu_state.abrt != 0)
                {
                        CS = old_cs;
                        ESP = old_esp;
                        return true;
                }
        }
        else
        {
                uint32_t old_esp = ESP;
                PUSH_L(old_cs);
                if (cpu_state.abrt != 0)
                {
                        CS = old_cs;
                        cgate16 = cgate32 = 0;
                        return true;
                }
                PUSH_L(old_pc);
                if (cpu_state.abrt != 0)
                {
                        CS = old_cs;
                        ESP = old_esp;
                        return true;
                }
        }
        return false;
    }

    // pcem: x86_ops_call.h:99-116 — opCALL_far_w
    private static int opCALL_far_w(uint32_t fetchdat)
    {
        uint16_t new_cs, new_pc;
        int cycles_old = cycles;

        new_pc = (uint16_t)fetchdat; cpu_state.pc += 2;   // getwordf()
        new_cs = getword();
        if (cpu_state.abrt != 0)
                return 1;

        if (CALL_FAR_w(new_cs, (uint16_t)new_pc, out _, out _)) return 1;
        CPU_BLOCK_END();
        PREFETCH_RUN(cycles_old - cycles, 5, -1, 0, 0, cgate16 != 0 ? 2 : 0,
                     cgate16 != 0 ? 0 : 2, 0);
        PREFETCH_FLUSH();

        return 0;
    }

    // pcem: x86_ops_call.h:136-251 — opFF_w_a16.
    //
    // LES CYCLES DE INC ET DEC PASSENT PAR timing_mm ET NON timing_mr, alors que
    // l'opération écrit un seul opérande mémoire. C'est PCem ; sur un 286 les
    // deux valent 7, donc le fuzzeur ne peut pas les distinguer — d'où le diff
    // mécanique de jetons.
    private static int opFF_w_a16(uint32_t fetchdat)
    {
        uint16_t old_cs, new_cs;
        uint32_t old_pc, new_pc;
        int cycles_old = cycles;

        uint16_t temp;

        if (fetch_ea_16(fetchdat)) return 1;

        switch (fetchdat & 0x38)
        {
        case 0x00: /*INC w*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
                temp = geteaw();
                if (cpu_state.abrt != 0)
                        return 1;
                seteaw((uint16_t)(temp + 1));
                if (cpu_state.abrt != 0)
                        return 1;
                setadd16nc(temp, 1);
                CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm);
                PREFETCH_RUN((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm, 2, (int)fetchdat,
                             (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0, 0);
                break;
        case 0x08: /*DEC w*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
                temp = geteaw();
                if (cpu_state.abrt != 0)
                        return 1;
                seteaw((uint16_t)(temp - 1));
                if (cpu_state.abrt != 0)
                        return 1;
                setsub16nc(temp, 1);
                CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm);
                PREFETCH_RUN((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm, 2, (int)fetchdat,
                             (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0, 0);
                break;
        case 0x10: /*CALL*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                new_pc = geteaw();
                if (cpu_state.abrt != 0)
                        return 1;
                PUSH_W((uint16_t)cpu_state.pc);
                cpu_state.pc = new_pc;
                CPU_BLOCK_END();
                if (is486 != 0)
                        CLOCK_CYCLES(5);
                else
                        CLOCK_CYCLES((cpu_mod == 3) ? 7 : 10);
                PREFETCH_RUN((cpu_mod == 3) ? 7 : 10, 2, (int)fetchdat,
                             (cpu_mod == 3) ? 0 : 1, 0, 1, 0, 0);
                PREFETCH_FLUSH();
                break;
        case 0x18: /*CALL far*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                new_pc = readmemw(easeg, cpu_state.eaaddr);
                new_cs = readmemw(easeg, (cpu_state.eaaddr + 2));
                if (cpu_state.abrt != 0)
                        return 1;

                if (CALL_FAR_w(new_cs, (uint16_t)new_pc, out old_cs, out old_pc)) return 1;
                CPU_BLOCK_END();
                PREFETCH_RUN(cycles_old - cycles, 2, (int)fetchdat, 2, 0,
                             cgate16 != 0 ? 2 : 0, cgate16 != 0 ? 0 : 2, 0);
                PREFETCH_FLUSH();
                break;
        case 0x20: /*JMP*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                new_pc = geteaw();
                if (cpu_state.abrt != 0)
                        return 1;
                cpu_state.pc = new_pc;
                CPU_BLOCK_END();
                if (is486 != 0)
                        CLOCK_CYCLES(5);
                else
                        CLOCK_CYCLES((cpu_mod == 3) ? 7 : 10);
                PREFETCH_RUN((cpu_mod == 3) ? 7 : 10, 2, (int)fetchdat,
                             (cpu_mod == 3) ? 0 : 1, 0, 0, 0, 0);
                PREFETCH_FLUSH();
                break;
        case 0x28: /*JMP far*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                old_pc = cpu_state.pc;
                new_pc = readmemw(easeg, cpu_state.eaaddr);
                new_cs = readmemw(easeg, cpu_state.eaaddr + 2);
                if (cpu_state.abrt != 0)
                        return 1;
                cpu_state.pc = new_pc;
                x86seg_c.loadcsjmp(new_cs, old_pc);
                if (cpu_state.abrt != 0)
                        return 1;
                CPU_BLOCK_END();
                PREFETCH_RUN(cycles_old - cycles, 2, (int)fetchdat, 2, 0, 0, 0, 0);
                PREFETCH_FLUSH();
                break;
        case 0x30: /*PUSH w*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                temp = geteaw();
                if (cpu_state.abrt != 0)
                        return 1;
                PUSH_W(temp);
                CLOCK_CYCLES((cpu_mod == 3) ? 2 : 5);
                PREFETCH_RUN((cpu_mod == 3) ? 2 : 5, 2, (int)fetchdat,
                             (cpu_mod == 3) ? 0 : 1, 0, 1, 0, 0);
                break;

        default:
                x86illegal();
                break;
        }
        return cpu_state.abrt;
    }

    // omitted: opCALL_far_l, opFF_w_a32, opFF_l_a16, opFF_l_a32 et la macro
    //   CALL_FAR_l — inatteignables tant qu'op32 est nul.

    /// <summary>pcem: 9A et FF — relevés sur ops_286[] par gdb.</summary>
    private static void PoserGroupeAppel()
    {
        ops_286[0x9A] = opCALL_far_w;
        ops_286[0xFF] = opFF_w_a16;
    }

    // ---- G2, D2 : les formes 32 bits (_l, _a32) de x86_ops_call.h ----

    // pcem: x86_ops_call.h:117
    private static int opCALL_far_l(uint32_t fetchdat)
    {
        uint32_t new_cs, new_pc;
        int cycles_old = cycles;

        new_pc = getlong();
        new_cs = getword();
        if (cpu_state.abrt != 0)
                return 1;

        if (CALL_FAR_l((uint16_t)new_cs, new_pc, out _, out _)) return 1;
        CPU_BLOCK_END();
        PREFETCH_RUN(cycles_old - cycles, 7, -1, 0, 0, cgate16 != 0 ? 2 : 0, cgate16 != 0 ? 0 : 2, 0);
        PREFETCH_FLUSH();

        return 0;
    }

    // pcem: x86_ops_call.h:369
    private static int opFF_l_a16(uint32_t fetchdat)
    {
        uint16_t new_cs;
        uint32_t old_pc, new_pc;
        int cycles_old = cycles;

        uint32_t temp;

        if (fetch_ea_16(fetchdat)) return 1;

        switch (fetchdat & 0x38) {
        case 0x00: /*INC l*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
                temp = geteal();
                if (cpu_state.abrt != 0)
                        return 1;
                seteal(temp + 1);
                if (cpu_state.abrt != 0)
                        return 1;
                setadd32nc(temp, 1);
                CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm);
                PREFETCH_RUN((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm, 2, (int)fetchdat, 0, (cpu_mod == 3) ? 0 : 1, 0,
                             (cpu_mod == 3) ? 0 : 1, 0);
                break;
        case 0x08: /*DEC l*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
                temp = geteal();
                if (cpu_state.abrt != 0)
                        return 1;
                seteal(temp - 1);
                if (cpu_state.abrt != 0)
                        return 1;
                setsub32nc(temp, 1);
                CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm);
                PREFETCH_RUN((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm, 2, (int)fetchdat, 0, (cpu_mod == 3) ? 0 : 1, 0,
                             (cpu_mod == 3) ? 0 : 1, 0);
                break;
        case 0x10: /*CALL*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                new_pc = geteal();
                if (cpu_state.abrt != 0)
                        return 1;
                PUSH_L(cpu_state.pc);
                cpu_state.pc = new_pc;
                CPU_BLOCK_END();
                if (is486 != 0)
                        CLOCK_CYCLES(5);
                else
                        CLOCK_CYCLES((cpu_mod == 3) ? 7 : 10);
                PREFETCH_RUN((cpu_mod == 3) ? 7 : 10, 2, (int)fetchdat, 0, (cpu_mod == 3) ? 0 : 1, 0, 1, 0);
                PREFETCH_FLUSH();
                break;
        case 0x18: /*CALL far*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                new_pc = readmeml(easeg, cpu_state.eaaddr);
                new_cs = readmemw(easeg, (uint32_t)((cpu_state.eaaddr + 4)));
                if (cpu_state.abrt != 0)
                        return 1;

                if (CALL_FAR_l((uint16_t)new_cs, new_pc, out _, out _)) return 1;
                CPU_BLOCK_END();
                PREFETCH_RUN(cycles_old - cycles, 2, (int)fetchdat, 1, 1, cgate16 != 0 ? 2 : 0, cgate16 != 0 ? 0 : 2, 0);
                PREFETCH_FLUSH();
                break;
        case 0x20: /*JMP*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                new_pc = geteal();
                if (cpu_state.abrt != 0)
                        return 1;
                cpu_state.pc = new_pc;
                CPU_BLOCK_END();
                if (is486 != 0)
                        CLOCK_CYCLES(5);
                else
                        CLOCK_CYCLES((cpu_mod == 3) ? 7 : 10);
                PREFETCH_RUN(cycles_old - cycles, 2, (int)fetchdat, 0, 1, 0, 0, 0);
                PREFETCH_FLUSH();
                break;
        case 0x28: /*JMP far*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                old_pc = cpu_state.pc;
                new_pc = readmeml(easeg, cpu_state.eaaddr);
                new_cs = readmemw(easeg, (uint32_t)(cpu_state.eaaddr + 4));
                if (cpu_state.abrt != 0)
                        return 1;
                cpu_state.pc = new_pc;
                x86seg_c.loadcsjmp(new_cs, old_pc);
                if (cpu_state.abrt != 0)
                        return 1;
                CPU_BLOCK_END();
                PREFETCH_RUN(cycles_old - cycles, 2, (int)fetchdat, 1, 1, 0, 0, 0);
                PREFETCH_FLUSH();
                break;
        case 0x30: /*PUSH l*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                temp = geteal();
                if (cpu_state.abrt != 0)
                        return 1;
                PUSH_L(temp);
                CLOCK_CYCLES((cpu_mod == 3) ? 2 : 5);
                PREFETCH_RUN((cpu_mod == 3) ? 2 : 5, 2, (int)fetchdat, 0, (cpu_mod == 3) ? 0 : 1, 0, 1, 0);
                break;

        default:
                //                fatal("Bad FF opcode %02X\n",fetchdat&0x38);
                x86illegal();
                break;
        }
        return cpu_state.abrt;
    }

    // pcem: x86_ops_call.h:485
    private static int opFF_l_a32(uint32_t fetchdat)
    {
        uint16_t new_cs;
        uint32_t old_pc, new_pc;
        int cycles_old = cycles;

        uint32_t temp;

        if (fetch_ea_32(fetchdat)) return 1;

        switch (fetchdat & 0x38) {
        case 0x00: /*INC l*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
                temp = geteal();
                if (cpu_state.abrt != 0)
                        return 1;
                seteal(temp + 1);
                if (cpu_state.abrt != 0)
                        return 1;
                setadd32nc(temp, 1);
                CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm);
                PREFETCH_RUN((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm, 2, (int)fetchdat, 0, (cpu_mod == 3) ? 0 : 1, 0,
                             (cpu_mod == 3) ? 0 : 1, 1);
                break;
        case 0x08: /*DEC l*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
                temp = geteal();
                if (cpu_state.abrt != 0)
                        return 1;
                seteal(temp - 1);
                if (cpu_state.abrt != 0)
                        return 1;
                setsub32nc(temp, 1);
                CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm);
                PREFETCH_RUN((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm, 2, (int)fetchdat, 0, (cpu_mod == 3) ? 0 : 1, 0,
                             (cpu_mod == 3) ? 0 : 1, 1);
                break;
        case 0x10: /*CALL*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                new_pc = geteal();
                if (cpu_state.abrt != 0)
                        return 1;
                PUSH_L(cpu_state.pc);
                if (cpu_state.abrt != 0)
                        return 1;
                cpu_state.pc = new_pc;
                CPU_BLOCK_END();
                if (is486 != 0)
                        CLOCK_CYCLES(5);
                else
                        CLOCK_CYCLES((cpu_mod == 3) ? 7 : 10);
                PREFETCH_RUN((cpu_mod == 3) ? 7 : 10, 2, (int)fetchdat, 0, (cpu_mod == 3) ? 0 : 1, 0, 1, 1);
                PREFETCH_FLUSH();
                break;
        case 0x18: /*CALL far*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                new_pc = readmeml(easeg, cpu_state.eaaddr);
                new_cs = readmemw(easeg, (uint32_t)((cpu_state.eaaddr + 4)));
                if (cpu_state.abrt != 0)
                        return 1;

                if (CALL_FAR_l((uint16_t)new_cs, new_pc, out _, out _)) return 1;
                CPU_BLOCK_END();
                PREFETCH_RUN(cycles_old - cycles, 2, (int)fetchdat, 1, 1, cgate16 != 0 ? 2 : 0, cgate16 != 0 ? 0 : 2, 1);
                PREFETCH_FLUSH();
                break;
        case 0x20: /*JMP*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                new_pc = geteal();
                if (cpu_state.abrt != 0)
                        return 1;
                cpu_state.pc = new_pc;
                CPU_BLOCK_END();
                if (is486 != 0)
                        CLOCK_CYCLES(5);
                else
                        CLOCK_CYCLES((cpu_mod == 3) ? 7 : 10);
                PREFETCH_RUN(cycles_old - cycles, 2, (int)fetchdat, 1, 1, 0, 0, 1);
                PREFETCH_FLUSH();
                break;
        case 0x28: /*JMP far*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                old_pc = cpu_state.pc;
                new_pc = readmeml(easeg, cpu_state.eaaddr);
                new_cs = readmemw(easeg, (uint32_t)(cpu_state.eaaddr + 4));
                if (cpu_state.abrt != 0)
                        return 1;
                cpu_state.pc = new_pc;
                x86seg_c.loadcsjmp(new_cs, old_pc);
                if (cpu_state.abrt != 0)
                        return 1;
                CPU_BLOCK_END();
                PREFETCH_RUN(cycles_old - cycles, 2, (int)fetchdat, 1, 1, 0, 0, 1);
                PREFETCH_FLUSH();
                break;
        case 0x30: /*PUSH l*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                temp = geteal();
                if (cpu_state.abrt != 0)
                        return 1;
                PUSH_L(temp);
                PREFETCH_RUN((cpu_mod == 3) ? 2 : 5, 2, (int)fetchdat, 0, (cpu_mod == 3) ? 0 : 1, 0, 1, 1);
                break;

        default:
                //                fatal("Bad FF opcode %02X\n",fetchdat&0x38);
                x86illegal();
                break;
        }
        return cpu_state.abrt;
    }

    // pcem: x86_ops_call.h:252
    private static int opFF_w_a32(uint32_t fetchdat)
    {
        uint16_t new_cs;
        uint32_t old_pc, new_pc;
        int cycles_old = cycles;

        uint16_t temp;

        if (fetch_ea_32(fetchdat)) return 1;

        switch (fetchdat & 0x38) {
        case 0x00: /*INC w*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
                temp = geteaw();
                if (cpu_state.abrt != 0)
                        return 1;
                seteaw((uint16_t)(temp + 1));
                if (cpu_state.abrt != 0)
                        return 1;
                setadd16nc(temp, 1);
                CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm);
                PREFETCH_RUN((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1,
                             0, 1);
                break;
        case 0x08: /*DEC w*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
                temp = geteaw();
                if (cpu_state.abrt != 0)
                        return 1;
                seteaw((uint16_t)(temp - 1));
                if (cpu_state.abrt != 0)
                        return 1;
                setsub16nc(temp, 1);
                CLOCK_CYCLES((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm);
                PREFETCH_RUN((cpu_mod == 3) ? cpu_c.timing_rr : cpu_c.timing_mm, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1,
                             0, 1);
                break;
        case 0x10: /*CALL*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                new_pc = geteaw();
                if (cpu_state.abrt != 0)
                        return 1;
                PUSH_W((uint16_t)(cpu_state.pc));
                cpu_state.pc = new_pc;
                CPU_BLOCK_END();
                if (is486 != 0)
                        CLOCK_CYCLES(5);
                else
                        CLOCK_CYCLES((cpu_mod == 3) ? 7 : 10);
                PREFETCH_RUN((cpu_mod == 3) ? 7 : 10, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 1, 0, 1);
                PREFETCH_FLUSH();
                break;
        case 0x18: /*CALL far*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                new_pc = readmemw(easeg, cpu_state.eaaddr);
                new_cs = readmemw(easeg, (uint32_t)((cpu_state.eaaddr + 2)));
                if (cpu_state.abrt != 0)
                        return 1;

                if (CALL_FAR_w(new_cs, (uint16_t)new_pc, out _, out _)) return 1;
                CPU_BLOCK_END();
                PREFETCH_RUN(cycles_old - cycles, 2, (int)fetchdat, 2, 0, cgate16 != 0 ? 2 : 0, cgate16 != 0 ? 0 : 2, 1);
                PREFETCH_FLUSH();
                break;
        case 0x20: /*JMP*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                new_pc = geteaw();
                if (cpu_state.abrt != 0)
                        return 1;
                cpu_state.pc = new_pc;
                CPU_BLOCK_END();
                if (is486 != 0)
                        CLOCK_CYCLES(5);
                else
                        CLOCK_CYCLES((cpu_mod == 3) ? 7 : 10);
                PREFETCH_RUN(cycles_old - cycles, 2, (int)fetchdat, 1, 0, 0, 0, 1);
                PREFETCH_FLUSH();
                break;
        case 0x28: /*JMP far*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                old_pc = cpu_state.pc;
                new_pc = readmemw(easeg, cpu_state.eaaddr);
                new_cs = readmemw(easeg, (uint32_t)(cpu_state.eaaddr + 2));
                if (cpu_state.abrt != 0)
                        return 1;
                cpu_state.pc = new_pc;
                x86seg_c.loadcsjmp(new_cs, old_pc);
                if (cpu_state.abrt != 0)
                        return 1;
                CPU_BLOCK_END();
                PREFETCH_RUN(cycles_old - cycles, 2, (int)fetchdat, 2, 0, 0, 0, 1);
                PREFETCH_FLUSH();
                break;
        case 0x30: /*PUSH w*/
                if (cpu_mod != 3)
                        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
                temp = geteaw();
                if (cpu_state.abrt != 0)
                        return 1;
                PUSH_W((uint16_t)(temp));
                CLOCK_CYCLES((cpu_mod == 3) ? 2 : 5);
                PREFETCH_RUN((cpu_mod == 3) ? 2 : 5, 2, (int)fetchdat, (cpu_mod == 3) ? 0 : 1, 0, 1, 0, 1);
                break;

        default:
                //                fatal("Bad FF opcode %02X\n",fetchdat&0x38);
                x86illegal();
                break;
        }
        return cpu_state.abrt;
    }

    // pcem: 386_ops.h — les emplacements de ces handlers dans OP_TABLE(386) et (386_0f).
    private static void PoserGroupe_call_386()
    {
        ops_386[0x19A] = opCALL_far_l;
        ops_386[0x1FF] = opFF_l_a16;
        ops_386[0x2FF] = opFF_w_a32;
        ops_386[0x39A] = opCALL_far_l;
        ops_386[0x3FF] = opFF_l_a32;
    }
}
