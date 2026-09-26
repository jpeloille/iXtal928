// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x86_ops_mov_seg.h  (lignes 3-160)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — A11 : les quatre emplacements qu'un 286 atteint, 8C, 8E,
//         C4 et C5. opLES vient de la macro opLsel du MEME en-tete, pas de
//         x86_ops_mov.h comme le laissait croire son voisinage de table.
//         Les formes `_a32`, `_l` et les LFS/LGS/LSS du 386 restent dehors.
//
// CHARGER UN SEGMENT N'EST PAS COPIER SEIZE BITS.
//
// En mode réel ça y ressemble — loadseg pose base = seg << 4 et s'arrête. En
// mode protégé c'est une lecture de descripteur avec quatre contrôles de
// privilège. C'est la même fonction des deux côtés, et c'est pourquoi le 286
// peut basculer sans changer un seul de ces handlers.
//
// MOV SS, r/m EXÉCUTE L'INSTRUCTION SUIVANTE, comme POP SS à A4 : l'ombre
// d'interruption. Deuxième et dernier endroit de la table où un handler
// aiguille lui-même un second opcode sans être un préfixe.
//
// LES CAS FS ET GS SONT LA, ET C'EST UNE CORRECTION. Je les avais omis en
// ecrivant « le 286 n'a ni FS ni GS » — vrai de l'ARCHITECTURE, faux du CODE :
// cpu_state porte seg_fs et seg_gs depuis le palier (a), les emplacements FS et
// GS sont dans le vecteur d'etat depuis A1a, et PCem ne garde PAS l'opcode. Un
// `MOV CX, FS` (8C E1) ecrit donc 0 dans CX sur un 286, au lieu de ne rien
// faire. Trouve par le fuzzeur des le premier passage sur les 256 :
//   opcode 0x8C, octets 8C E1 — regs[1] : oracle 0x00000000, C# 0x00007FFF
// « Inatteignable » se verifie sur le CODE, pas sur la fiche technique. Meme
// lecon qu'a A6 pour PUSH_L.
//
// ET LE CAS 0x08 N'EXISTE QUE DANS UN SENS. `MOV r/m, CS` est légal et lit CS ;
// `MOV CS, r/m` ne l'est pas, et opMOV_seg_w_a16 n'a tout simplement PAS de
// `case 0x08` — le switch tombe à travers, aucun segment n'est chargé, et
// l'instruction ne fait rien qu'avancer. Ce n'est pas un ILLEGAL_ON : c'est un
// trou silencieux, et PCem le laisse tel quel.

using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;

namespace iXtal26.Cpu;

internal static partial class _386
{
    // pcem: x86_ops_mov_seg.h:3-31 — opMOV_w_seg_a16. Lire un segment, lui,
    // est toujours légal — CS compris.
    private static int opMOV_w_seg_a16(uint32_t fetchdat)
    {
        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;

        switch (fetchdat & 0x38)
        {
        case 0x00: /*ES*/
                seteaw(ES);
                break;
        case 0x08: /*CS*/
                seteaw(CS);
                break;
        case 0x18: /*DS*/
                seteaw(DS);
                break;
        case 0x10: /*SS*/
                seteaw(SS);
                break;
        case 0x20: /*FS*/
                seteaw(FS);
                break;
        case 0x28: /*GS*/
                seteaw(GS);
                break;
        }

        CLOCK_CYCLES((cpu_mod == 3) ? 2 : 3);
        PREFETCH_RUN((cpu_mod == 3) ? 2 : 3, 2, (int)fetchdat, 0,
                     (cpu_mod == 3) ? 0 : 1, 0, 0, 0);
        return cpu_state.abrt;
    }

    // pcem: x86_ops_mov_seg.h:62-102 — opMOV_seg_w_a16.
    private static int opMOV_seg_w_a16(uint32_t fetchdat)
    {
        uint16_t new_seg;

        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        new_seg = geteaw();
        if (cpu_state.abrt != 0)
                return 1;

        switch (fetchdat & 0x38)
        {
        case 0x00: /*ES*/
                x86seg_c.loadseg(new_seg, cpu_state.seg_es);
                break;
        case 0x18: /*DS*/
                x86seg_c.loadseg(new_seg, cpu_state.seg_ds);
                break;
        case 0x20: /*FS*/
                x86seg_c.loadseg(new_seg, cpu_state.seg_fs);
                break;
        case 0x28: /*GS*/
                x86seg_c.loadseg(new_seg, cpu_state.seg_gs);
                break;
        case 0x10: /*SS*/
                x86seg_c.loadseg(new_seg, cpu_state.seg_ss);
                if (cpu_state.abrt != 0)
                        return 1;
                cpu_state.oldpc = cpu_state.pc;
                cpu_state.op32 = use32;
                cpu_state.ssegs = 0;
                cpu_state.ea_seg = cpu_state.seg_ds;
                fetchdat = fastreadl(x86.cs + cpu_state.pc);
                cpu_state.pc++;
                if (cpu_state.abrt != 0)
                        return 1;
                x86_opcodes![(fetchdat & 0xff) | cpu_state.op32](fetchdat >> 8);
                return 1;
        // PAS de `case 0x08` : MOV CS, r/m n'existe pas, le switch tombe a
        // travers en silence. Voir l'en-tete.
        }

        CLOCK_CYCLES((cpu_mod == 3) ? 2 : 5);
        PREFETCH_RUN((cpu_mod == 3) ? 2 : 5, 2, (int)fetchdat, 0,
                     (cpu_mod == 3) ? 0 : 1, 0, 0, 0);
        return cpu_state.abrt;
    }

    // pcem: x86_ops_mov_seg.h — opLDS_w_a16. Charge DS ET un registre d'un coup,
    // depuis un pointeur lointain en mémoire. ILLEGAL_ON(mod == 3) : il n'y a
    // pas de pointeur lointain dans un registre.
    private static int opLDS_w_a16(uint32_t fetchdat)
    {
        uint16_t addr, seg;

        if (fetch_ea_16(fetchdat)) return 1;
        if (ILLEGAL_ON(cpu_mod == 3)) return 0;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        addr = readmemw(easeg, cpu_state.eaaddr);
        seg = readmemw(easeg, cpu_state.eaaddr + 2);
        if (cpu_state.abrt != 0)
                return 1;
        x86seg_c.loadseg(seg, cpu_state.seg_ds);
        if (cpu_state.abrt != 0)
                return 1;
        cpu_state.regs[cpu_reg].w = addr;

        CLOCK_CYCLES(7);
        PREFETCH_RUN(7, 2, (int)fetchdat, 2, 0, 0, 0, 0);
        return 0;
    }

    /// <summary>pcem: x86_ops_mov_seg.h:406-425 — la macro opLsel(name, sel),
    /// forme `_w_a16`, instanciée pour ES seul sur un 286.
    ///
    /// LE MÊME CORPS QUE opLDS, À L'ORDRE PRÈS : opLsel fait SEG_CHECK_READ puis
    /// ILLEGAL_ON, opLDS fait ILLEGAL_ON puis SEG_CHECK_READ. Sur un `LES
    /// reg, reg` (mod == 3) avec un segment non chargeable, les deux ne lèvent
    /// donc pas la même exception. C'est une incohérence de PCem, pas de ce
    /// port ; transcrite telle quelle.</summary>
    private static int opLES_w_a16(uint32_t fetchdat)
    {
        uint16_t addr, seg;

        if (fetch_ea_16(fetchdat)) return 1;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (ILLEGAL_ON(cpu_mod == 3)) return 0;
        addr = readmemw(easeg, cpu_state.eaaddr);
        seg = readmemw(easeg, cpu_state.eaaddr + 2);
        if (cpu_state.abrt != 0)
                return 1;
        x86seg_c.loadseg(seg, cpu_state.seg_es);
        if (cpu_state.abrt != 0)
                return 1;
        cpu_state.regs[cpu_reg].w = addr;

        CLOCK_CYCLES(7);
        PREFETCH_RUN(7, 2, (int)fetchdat, 2, 0, 0, 0, 0);
        return 0;
    }

    // omitted: opLsel(FS, …) et opLsel(GS, …) — pas de FS ni GS sur un 286.
    // omitted: opLSS_* — LSS est une instruction 386.

    // omitted: opMOV_l_seg_a16/a32, opMOV_w_seg_a32, opMOV_seg_w_a32,
    //   opLDS_w_a32, opLDS_l_*, et LFS/LGS/LSS avec leurs variantes — op32 nul
    //   sur un 286, et FS/GS n'existent pas.

    /// <summary>pcem: 8C, 8E, C4, C5 — relevés sur ops_286[] par gdb.</summary>
    private static void PoserGroupeMovSeg()
    {
        ops_286[0x8C] = opMOV_w_seg_a16;
        ops_286[0x8E] = opMOV_seg_w_a16;
        ops_286[0xC4] = opLES_w_a16;
        ops_286[0xC5] = opLDS_w_a16;
    }

    /// <summary>G2, D2 — la macro opLsel(name, sel) (x86_ops_mov_seg.h:406-485) en
    /// ses quatre formes : `l` pour 32 bits d'offset, `a32` pour l'adresse effective.
    /// Instanciée pour ES, FS et GS (:486) ; LES_w_a16 garde sa méthode du 286.</summary>
    private static OpFn OpLsel(x86seg sel, bool l, bool a32) => fetchdat =>
    {
        if (a32 ? fetch_ea_32(fetchdat) : fetch_ea_16(fetchdat)) return 1;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        if (ILLEGAL_ON(cpu_mod == 3)) return 0;
        uint32_t addr = l ? readmeml(easeg, cpu_state.eaaddr) : readmemw(easeg, cpu_state.eaaddr);
        uint16_t seg = readmemw(easeg, cpu_state.eaaddr + (l ? 4u : 2u));
        if (cpu_state.abrt != 0)
                return 1;
        x86seg_c.loadseg(seg, sel);
        if (cpu_state.abrt != 0)
                return 1;
        if (l)
                cpu_state.regs[cpu_reg].l = addr;
        else
                cpu_state.regs[cpu_reg].w = (uint16_t)addr;
        CLOCK_CYCLES(7);
        if (l)
                PREFETCH_RUN(7, 2, (int)fetchdat, 1, 1, 0, 0, a32 ? 1 : 0);
        else
                PREFETCH_RUN(7, 2, (int)fetchdat, 2, 0, 0, 0, a32 ? 1 : 0);
        return 0;
    };

    private static void PoserLsel386()
    {
        ops_386[0x1C4] = OpLsel(cpu_state.seg_es, true, false);
        ops_386[0x2C4] = OpLsel(cpu_state.seg_es, false, true);
        ops_386[0x3C4] = OpLsel(cpu_state.seg_es, true, true);
        foreach (var (op, sel) in new[] { (0xB4, cpu_state.seg_fs), (0xB5, cpu_state.seg_gs) })
        {
                ops_386_0f[op] = OpLsel(sel, false, false);
                ops_386_0f[0x100 | op] = OpLsel(sel, true, false);
                ops_386_0f[0x200 | op] = OpLsel(sel, false, true);
                ops_386_0f[0x300 | op] = OpLsel(sel, true, true);
        }
    }

    // ---- G2, D2 : les formes 32 bits (_l, _a32) de x86_ops_mov_seg.h ----

    // pcem: x86_ops_mov_seg.h:286
    private static int opLDS_l_a16(uint32_t fetchdat)
    {
        uint32_t addr;
        uint16_t seg;

        if (fetch_ea_16(fetchdat)) return 1;
        if (ILLEGAL_ON(cpu_mod == 3)) return 0;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        addr = readmeml(easeg, cpu_state.eaaddr);
        seg = readmemw(easeg, (uint32_t)(cpu_state.eaaddr + 4));
        if (cpu_state.abrt != 0)
                return 1;
        x86seg_c.loadseg(seg, cpu_state.seg_ds);
        if (cpu_state.abrt != 0)
                return 1;
        cpu_state.regs[cpu_reg].l = addr;

        CLOCK_CYCLES(7);
        PREFETCH_RUN(7, 2, (int)fetchdat, 1, 1, 0, 0, 0);
        return 0;
    }

    // pcem: x86_ops_mov_seg.h:306
    private static int opLDS_l_a32(uint32_t fetchdat)
    {
        uint32_t addr;
        uint16_t seg;

        if (fetch_ea_32(fetchdat)) return 1;
        if (ILLEGAL_ON(cpu_mod == 3)) return 0;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        addr = readmeml(easeg, cpu_state.eaaddr);
        seg = readmemw(easeg, (uint32_t)(cpu_state.eaaddr + 4));
        if (cpu_state.abrt != 0)
                return 1;
        x86seg_c.loadseg(seg, cpu_state.seg_ds);
        if (cpu_state.abrt != 0)
                return 1;
        cpu_state.regs[cpu_reg].l = addr;

        CLOCK_CYCLES(7);
        PREFETCH_RUN(7, 2, (int)fetchdat, 1, 1, 0, 0, 1);
        return 0;
    }

    // pcem: x86_ops_mov_seg.h:267
    private static int opLDS_w_a32(uint32_t fetchdat)
    {
        uint16_t addr, seg;

        if (fetch_ea_32(fetchdat)) return 1;
        if (ILLEGAL_ON(cpu_mod == 3)) return 0;
        if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        addr = readmemw(easeg, cpu_state.eaaddr);
        seg = readmemw(easeg, (uint32_t)(cpu_state.eaaddr + 2));
        if (cpu_state.abrt != 0)
                return 1;
        x86seg_c.loadseg(seg, cpu_state.seg_ds);
        if (cpu_state.abrt != 0)
                return 1;
        cpu_state.regs[cpu_reg].w = addr;

        CLOCK_CYCLES(7);
        PREFETCH_RUN(7, 2, (int)fetchdat, 2, 0, 0, 0, 1);
        return 0;
    }

    // pcem: x86_ops_mov_seg.h:64
    private static int opMOV_l_seg_a16(uint32_t fetchdat)
    {
        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;

        switch (fetchdat & 0x38) {
        case 0x00: /*ES*/
                if (cpu_mod == 3)
                        cpu_state.regs[cpu_rm].l = ES;
                else
                        seteaw((uint16_t)(ES));
                break;
        case 0x08: /*CS*/
                if (cpu_mod == 3)
                        cpu_state.regs[cpu_rm].l = CS;
                else
                        seteaw((uint16_t)(CS));
                break;
        case 0x18: /*DS*/
                if (cpu_mod == 3)
                        cpu_state.regs[cpu_rm].l = DS;
                else
                        seteaw((uint16_t)(DS));
                break;
        case 0x10: /*SS*/
                if (cpu_mod == 3)
                        cpu_state.regs[cpu_rm].l = SS;
                else
                        seteaw((uint16_t)(SS));
                break;
        case 0x20: /*FS*/
                if (cpu_mod == 3)
                        cpu_state.regs[cpu_rm].l = FS;
                else
                        seteaw((uint16_t)(FS));
                break;
        case 0x28: /*GS*/
                if (cpu_mod == 3)
                        cpu_state.regs[cpu_rm].l = GS;
                else
                        seteaw((uint16_t)(GS));
                break;
        }

        CLOCK_CYCLES((cpu_mod == 3) ? 2 : 3);
        PREFETCH_RUN((cpu_mod == 3) ? 2 : 3, 2, (int)fetchdat, 0, (cpu_mod == 3) ? 0 : 1, 0, 0, 0);
        return cpu_state.abrt;
    }

    // pcem: x86_ops_mov_seg.h:112
    private static int opMOV_l_seg_a32(uint32_t fetchdat)
    {
        if (fetch_ea_32(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;

        switch (fetchdat & 0x38) {
        case 0x00: /*ES*/
                if (cpu_mod == 3)
                        cpu_state.regs[cpu_rm].l = ES;
                else
                        seteaw((uint16_t)(ES));
                break;
        case 0x08: /*CS*/
                if (cpu_mod == 3)
                        cpu_state.regs[cpu_rm].l = CS;
                else
                        seteaw((uint16_t)(CS));
                break;
        case 0x18: /*DS*/
                if (cpu_mod == 3)
                        cpu_state.regs[cpu_rm].l = DS;
                else
                        seteaw((uint16_t)(DS));
                break;
        case 0x10: /*SS*/
                if (cpu_mod == 3)
                        cpu_state.regs[cpu_rm].l = SS;
                else
                        seteaw((uint16_t)(SS));
                break;
        case 0x20: /*FS*/
                if (cpu_mod == 3)
                        cpu_state.regs[cpu_rm].l = FS;
                else
                        seteaw((uint16_t)(FS));
                break;
        case 0x28: /*GS*/
                if (cpu_mod == 3)
                        cpu_state.regs[cpu_rm].l = GS;
                else
                        seteaw((uint16_t)(GS));
                break;
        }

        CLOCK_CYCLES((cpu_mod == 3) ? 2 : 3);
        PREFETCH_RUN((cpu_mod == 3) ? 2 : 3, 2, (int)fetchdat, 0, (cpu_mod == 3) ? 0 : 1, 0, 0, 1);
        return cpu_state.abrt;
    }

    // pcem: x86_ops_mov_seg.h:204
    private static int opMOV_seg_w_a32(uint32_t fetchdat)
    {
        uint16_t new_seg;

        if (fetch_ea_32(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_READ(cpu_state.ea_seg!)) return 1;
        new_seg = geteaw();
        if (cpu_state.abrt != 0)
                return 1;

        switch (fetchdat & 0x38) {
        case 0x00: /*ES*/
                x86seg_c.loadseg(new_seg, cpu_state.seg_es);
                break;
        case 0x18: /*DS*/
                x86seg_c.loadseg(new_seg, cpu_state.seg_ds);
                break;
        case 0x10: /*SS*/
                x86seg_c.loadseg(new_seg, cpu_state.seg_ss);
                if (cpu_state.abrt != 0)
                        return 1;
                cpu_state.oldpc = cpu_state.pc;
                cpu_state.op32 = use32;
                cpu_state.ssegs = 0;
                cpu_state.ea_seg = cpu_state.seg_ds;
                fetchdat = fastreadl(x86.cs + cpu_state.pc);
                cpu_state.pc++;
                if (cpu_state.abrt != 0)
                        return 1;
                x86_opcodes![(fetchdat & 0xff) | cpu_state.op32](fetchdat >> 8);
                return 1;
        case 0x20: /*FS*/
                x86seg_c.loadseg(new_seg, cpu_state.seg_fs);
                break;
        case 0x28: /*GS*/
                x86seg_c.loadseg(new_seg, cpu_state.seg_gs);
                break;
        }

        CLOCK_CYCLES((cpu_mod == 3) ? 2 : 5);
        PREFETCH_RUN((cpu_mod == 3) ? 2 : 5, 2, (int)fetchdat, 0, (cpu_mod == 3) ? 0 : 1, 0, 0, 1);
        return cpu_state.abrt;
    }

    // pcem: x86_ops_mov_seg.h:33
    private static int opMOV_w_seg_a32(uint32_t fetchdat)
    {
        if (fetch_ea_32(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;

        switch (fetchdat & 0x38) {
        case 0x00: /*ES*/
                seteaw((uint16_t)(ES));
                break;
        case 0x08: /*CS*/
                seteaw((uint16_t)(CS));
                break;
        case 0x18: /*DS*/
                seteaw((uint16_t)(DS));
                break;
        case 0x10: /*SS*/
                seteaw((uint16_t)(SS));
                break;
        case 0x20: /*FS*/
                seteaw((uint16_t)(FS));
                break;
        case 0x28: /*GS*/
                seteaw((uint16_t)(GS));
                break;
        }

        CLOCK_CYCLES((cpu_mod == 3) ? 2 : 3);
        PREFETCH_RUN((cpu_mod == 3) ? 2 : 3, 2, (int)fetchdat, 0, (cpu_mod == 3) ? 0 : 1, 0, 0, 1);
        return cpu_state.abrt;
    }

    // pcem: 386_ops.h — les emplacements de ces handlers dans OP_TABLE(386) et (386_0f).
    private static void PoserGroupeMovSeg386()
    {
        ops_386[0x18C] = opMOV_l_seg_a16;
        ops_386[0x1C5] = opLDS_l_a16;
        ops_386[0x28C] = opMOV_w_seg_a32;
        ops_386[0x28E] = opMOV_seg_w_a32;
        ops_386[0x2C5] = opLDS_w_a32;
        ops_386[0x38C] = opMOV_l_seg_a32;
        ops_386[0x38E] = opMOV_seg_w_a32;
        ops_386[0x3C5] = opLDS_l_a32;
    }
}
