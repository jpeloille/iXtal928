// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/cpu/x86_ops_shift.h  (lignes 3-208 pour les
//         deux macros, 312-544 pour les six handlers d'un 286)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — A9 : les six emplacements qu'un 286 atteint, C0, C1, D0,
//         D1, D2, D3. OP_SHIFT_l et toutes les formes `_a32` restent dehors,
//         déclarées au registre des omissions.
//
// HUIT OPÉRATIONS PAR EMPLACEMENT, et trois façons de fournir le compte.
//
// C0/C1 prennent un immédiat, D0/D1 sous-entendent 1, D2/D3 lisent CL. Les six
// convergent ensuite vers la même macro, qui aiguille sur `rmdat & 0x38` :
// ROL, ROR, RCL, RCR, SHL, SHR, SAR — et SHL une seconde fois, en 0x30.
//
// `tempc` EST UNE VARIABLE LOCALE ICI, et c'est le piège du groupe. Chaque
// handler déclare `int tempc;` (x86_ops_shift.h:314 par exemple), qui MASQUE le
// `tempc` global de x86_flags.h — celui que ADC et SBB utilisent comme retenue
// entrante. Une rotation ne touche donc PAS la retenue d'un ADC qui suivrait.
// Utiliser le global ici serait invisible au fuzzeur en mode simple, où rien
// n'enchaîne jamais deux instructions.
//
// `if (!c) return 0;` AVANT TOUT LE RESTE : un décalage de zéro ne fait rien,
// pas même poser un drapeau. C'est conforme au 286 et ça se perd facilement.
//
// flags_rebuild() EN TÊTE, parce que RCL et RCR LISENT `cpu_state.flags &
// C_FLAG` et que les huit branches écrivent C et V directement. Sans lui, la
// retenue entrante d'un RCL serait celle d'un champ périmé. C'est le deuxième
// groupe, après A7, à mettre la fonction sur le chemin chaud — et le premier à
// la LIRE plutôt qu'à seulement matérialiser avant d'écrire.
//
// DEVIATION: les macros OP_SHIFT_b et OP_SHIFT_w se paramètrent par des
//   LARGEURS collées par ## — même situation qu'ARITH_MULTI à A3d, même
//   décision : on écrit les deux instanciations atteignables, et substituer
//   b<->w, 8<->16, 0x80<->0x8000, 7<->15 dans l'une doit rendre l'autre.
//   Le macro fait `return 0` et `return 1` depuis son milieu ; la méthode rend
//   `true` pour « l'appelant doit rendre 1 », et `false` dans les deux autres
//   cas — le `if (!c)` comme la sortie normale mènent au même `return 0`.

using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;
using static iXtal26.Cpu.x86_flags;

namespace iXtal26.Cpu;

internal static partial class _386
{
    // pcem: x86_ops_shift.h:3-104 — OP_SHIFT_b(c, ea32), avec ea32 = 0.
    private static bool OP_SHIFT_b(uint32_t rmdat, int c, uint8_t temp)
    {
        uint8_t temp_orig = temp;
        uint8_t temp2;
        int tempc;
        if (c == 0)
                return false;
        flags_rebuild();
        switch (rmdat & 0x38)
        {
        case 0x00: /*ROL b, c*/
                temp = (uint8_t)((temp << (c & 7)) | (temp >> (8 - (c & 7))));
                seteab(temp);
                if (cpu_state.abrt != 0)
                        return true;
                set_flags_rotate(FLAGS_ROL8, temp);
                CLOCK_CYCLES((cpu_mod == 3) ? 3 : 7);
                PREFETCH_RUN((cpu_mod == 3) ? 3 : 7, 2, (int)rmdat,
                             (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0, 0);
                break;
        case 0x08: /*ROR b,CL*/
                temp = (uint8_t)((temp >> (c & 7)) | (temp << (8 - (c & 7))));
                seteab(temp);
                if (cpu_state.abrt != 0)
                        return true;
                set_flags_rotate(FLAGS_ROR8, temp);
                CLOCK_CYCLES((cpu_mod == 3) ? 3 : 7);
                PREFETCH_RUN((cpu_mod == 3) ? 3 : 7, 2, (int)rmdat,
                             (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0, 0);
                break;
        case 0x10: /*RCL b,CL*/
                temp2 = (uint8_t)(cpu_state.flags & C_FLAG);
                if (is486 != 0)
                        CLOCK_CYCLES_ALWAYS(c);
                while (c > 0)
                {
                        tempc = temp2 != 0 ? 1 : 0;
                        temp2 = (uint8_t)(temp & 0x80);
                        temp = (uint8_t)((temp << 1) | tempc);
                        c--;
                }
                seteab(temp);
                if (cpu_state.abrt != 0)
                        return true;
                cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG));
                if (temp2 != 0)
                        cpu_state.flags |= C_FLAG;
                if (((cpu_state.flags & C_FLAG) ^ (temp >> 7)) != 0)
                        cpu_state.flags |= V_FLAG;
                CLOCK_CYCLES((cpu_mod == 3) ? 9 : 10);
                PREFETCH_RUN((cpu_mod == 3) ? 9 : 10, 2, (int)rmdat,
                             (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0, 0);
                break;
        case 0x18: /*RCR b,CL*/
                temp2 = (uint8_t)(cpu_state.flags & C_FLAG);
                if (is486 != 0)
                        CLOCK_CYCLES_ALWAYS(c);
                while (c > 0)
                {
                        tempc = temp2 != 0 ? 0x80 : 0;
                        temp2 = (uint8_t)(temp & 1);
                        temp = (uint8_t)((temp >> 1) | tempc);
                        c--;
                }
                seteab(temp);
                if (cpu_state.abrt != 0)
                        return true;
                cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG));
                if (temp2 != 0)
                        cpu_state.flags |= C_FLAG;
                if (((temp ^ (temp >> 1)) & 0x40) != 0)
                        cpu_state.flags |= V_FLAG;
                CLOCK_CYCLES((cpu_mod == 3) ? 9 : 10);
                PREFETCH_RUN((cpu_mod == 3) ? 9 : 10, 2, (int)rmdat,
                             (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0, 0);
                break;
        case 0x20:
        case 0x30: /*SHL b,CL*/
                seteab((uint8_t)(temp << c));
                if (cpu_state.abrt != 0)
                        return true;
                set_flags_shift(FLAGS_SHL8, temp_orig, c, (uint32_t)((temp << c) & 0xff));
                CLOCK_CYCLES((cpu_mod == 3) ? 3 : 7);
                PREFETCH_RUN((cpu_mod == 3) ? 3 : 7, 2, (int)rmdat,
                             (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0, 0);
                break;
        case 0x28: /*SHR b,CL*/
                seteab((uint8_t)(temp >> c));
                if (cpu_state.abrt != 0)
                        return true;
                set_flags_shift(FLAGS_SHR8, temp_orig, c, (uint32_t)(temp >> c));
                CLOCK_CYCLES((cpu_mod == 3) ? 3 : 7);
                PREFETCH_RUN((cpu_mod == 3) ? 3 : 7, 2, (int)rmdat,
                             (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0, 0);
                break;
        case 0x38: /*SAR b,CL*/
                temp = (uint8_t)((int8_t)temp >> c);
                seteab(temp);
                if (cpu_state.abrt != 0)
                        return true;
                set_flags_shift(FLAGS_SAR8, temp_orig, c, temp);
                CLOCK_CYCLES((cpu_mod == 3) ? 3 : 7);
                PREFETCH_RUN((cpu_mod == 3) ? 3 : 7, 2, (int)rmdat,
                             (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0, 0);
                break;
        }
        return false;
    }

    // pcem: x86_ops_shift.h:106-207 — OP_SHIFT_w(c, ea32), avec ea32 = 0.
    private static bool OP_SHIFT_w(uint32_t rmdat, int c, uint16_t temp)
    {
        uint16_t temp_orig = temp;
        uint16_t temp2;
        int tempc;
        if (c == 0)
                return false;
        flags_rebuild();
        switch (rmdat & 0x38)
        {
        case 0x00: /*ROL w, c*/
                temp = (uint16_t)((temp << (c & 15)) | (temp >> (16 - (c & 15))));
                seteaw(temp);
                if (cpu_state.abrt != 0)
                        return true;
                set_flags_rotate(FLAGS_ROL16, temp);
                CLOCK_CYCLES((cpu_mod == 3) ? 3 : 7);
                PREFETCH_RUN((cpu_mod == 3) ? 3 : 7, 2, (int)rmdat,
                             (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0, 0);
                break;
        case 0x08: /*ROR w,CL*/
                temp = (uint16_t)((temp >> (c & 15)) | (temp << (16 - (c & 15))));
                seteaw(temp);
                if (cpu_state.abrt != 0)
                        return true;
                set_flags_rotate(FLAGS_ROR16, temp);
                CLOCK_CYCLES((cpu_mod == 3) ? 3 : 7);
                PREFETCH_RUN((cpu_mod == 3) ? 3 : 7, 2, (int)rmdat,
                             (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0, 0);
                break;
        case 0x10: /*RCL w, c*/
                temp2 = (uint16_t)(cpu_state.flags & C_FLAG);
                if (is486 != 0)
                        CLOCK_CYCLES_ALWAYS(c);
                while (c > 0)
                {
                        tempc = temp2 != 0 ? 1 : 0;
                        temp2 = (uint16_t)(temp & 0x8000);
                        temp = (uint16_t)((temp << 1) | tempc);
                        c--;
                }
                seteaw(temp);
                if (cpu_state.abrt != 0)
                        return true;
                cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG));
                if (temp2 != 0)
                        cpu_state.flags |= C_FLAG;
                if (((cpu_state.flags & C_FLAG) ^ (temp >> 15)) != 0)
                        cpu_state.flags |= V_FLAG;
                CLOCK_CYCLES((cpu_mod == 3) ? 9 : 10);
                PREFETCH_RUN((cpu_mod == 3) ? 9 : 10, 2, (int)rmdat,
                             (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0, 0);
                break;
        case 0x18: /*RCR w, c*/
                temp2 = (uint16_t)(cpu_state.flags & C_FLAG);
                if (is486 != 0)
                        CLOCK_CYCLES_ALWAYS(c);
                while (c > 0)
                {
                        tempc = temp2 != 0 ? 0x8000 : 0;
                        temp2 = (uint16_t)(temp & 1);
                        temp = (uint16_t)((temp >> 1) | tempc);
                        c--;
                }
                seteaw(temp);
                if (cpu_state.abrt != 0)
                        return true;
                cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | V_FLAG));
                if (temp2 != 0)
                        cpu_state.flags |= C_FLAG;
                if (((temp ^ (temp >> 1)) & 0x4000) != 0)
                        cpu_state.flags |= V_FLAG;
                CLOCK_CYCLES((cpu_mod == 3) ? 9 : 10);
                PREFETCH_RUN((cpu_mod == 3) ? 9 : 10, 2, (int)rmdat,
                             (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0, 0);
                break;
        case 0x20:
        case 0x30: /*SHL w, c*/
                seteaw((uint16_t)(temp << c));
                if (cpu_state.abrt != 0)
                        return true;
                set_flags_shift(FLAGS_SHL16, temp_orig, c, (uint32_t)((temp << c) & 0xffff));
                CLOCK_CYCLES((cpu_mod == 3) ? 3 : 7);
                PREFETCH_RUN((cpu_mod == 3) ? 3 : 7, 2, (int)rmdat,
                             (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0, 0);
                break;
        case 0x28: /*SHR w, c*/
                seteaw((uint16_t)(temp >> c));
                if (cpu_state.abrt != 0)
                        return true;
                set_flags_shift(FLAGS_SHR16, temp_orig, c, (uint32_t)(temp >> c));
                CLOCK_CYCLES((cpu_mod == 3) ? 3 : 7);
                PREFETCH_RUN((cpu_mod == 3) ? 3 : 7, 2, (int)rmdat,
                             (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0, 0);
                break;
        case 0x38: /*SAR w, c*/
                temp = (uint16_t)((int16_t)temp >> c);
                seteaw(temp);
                if (cpu_state.abrt != 0)
                        return true;
                set_flags_shift(FLAGS_SAR16, temp_orig, c, temp);
                CLOCK_CYCLES((cpu_mod == 3) ? 3 : 7);
                PREFETCH_RUN((cpu_mod == 3) ? 3 : 7, 2, (int)rmdat,
                             (cpu_mod == 3) ? 0 : 1, 0, (cpu_mod == 3) ? 0 : 1, 0, 0);
                break;
        }
        return false;
    }

    // pcem: x86_ops_shift.h:312-328 — opC0_a16. Le compte vient d'un IMMÉDIAT,
    // masqué à 31 : un `SHL AL, 40` décale donc de 8, pas de 40.
    //
    // PREFETCH_PREFIX() APRÈS la lecture de l'immédiat, et il n'est là que dans
    // les deux formes C0/C1 : c'est l'octet supplémentaire qu'elles consomment.
    private static int opC0_a16(uint32_t fetchdat)
    {
        int c;
        uint8_t temp;

        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        c = readmemb(x86.cs, cpu_state.pc) & 31;
        cpu_state.pc++;
        PREFETCH_PREFIX();
        temp = geteab();
        if (cpu_state.abrt != 0)
                return 1;
        if (OP_SHIFT_b(fetchdat, c, temp)) return 1;
        return 0;
    }

    // pcem: x86_ops_shift.h:346-362 — opC1_w_a16
    private static int opC1_w_a16(uint32_t fetchdat)
    {
        int c;
        uint16_t temp;

        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        c = readmemb(x86.cs, cpu_state.pc) & 31;
        cpu_state.pc++;
        PREFETCH_PREFIX();
        temp = geteaw();
        if (cpu_state.abrt != 0)
                return 1;
        if (OP_SHIFT_w(fetchdat, c, temp)) return 1;
        return 0;
    }

    // pcem: x86_ops_shift.h:415-428 — opD0_a16. Le compte est 1, sous-entendu.
    private static int opD0_a16(uint32_t fetchdat)
    {
        int c = 1;
        uint8_t temp;

        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        temp = geteab();
        if (cpu_state.abrt != 0)
                return 1;
        if (OP_SHIFT_b(fetchdat, c, temp)) return 1;
        return 0;
    }

    // pcem: x86_ops_shift.h:443-456 — opD1_w_a16
    private static int opD1_w_a16(uint32_t fetchdat)
    {
        int c = 1;
        uint16_t temp;

        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        temp = geteaw();
        if (cpu_state.abrt != 0)
                return 1;
        if (OP_SHIFT_w(fetchdat, c, temp)) return 1;
        return 0;
    }

    // pcem: x86_ops_shift.h:500-514 — opD2_a16. Le compte vient de CL, masqué à
    // 31 comme l'immédiat — et PAS de PREFETCH_PREFIX : aucun octet de plus.
    private static int opD2_a16(uint32_t fetchdat)
    {
        int c;
        uint8_t temp;

        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        c = CL & 31;
        temp = geteab();
        if (cpu_state.abrt != 0)
                return 1;
        if (OP_SHIFT_b(fetchdat, c, temp)) return 1;
        return 0;
    }

    // pcem: x86_ops_shift.h:530-544 — opD3_w_a16
    private static int opD3_w_a16(uint32_t fetchdat)
    {
        int c;
        uint16_t temp;

        if (fetch_ea_16(fetchdat)) return 1;
        if (cpu_mod != 3)
                if (SEG_CHECK_WRITE(cpu_state.ea_seg!)) return 1;
        c = CL & 31;
        temp = geteaw();
        if (cpu_state.abrt != 0)
                return 1;
        if (OP_SHIFT_w(fetchdat, c, temp)) return 1;
        return 0;
    }

    // omitted: OP_SHIFT_l et les douze handlers `_l` / `_a32` — op32 nul sur un
    //   286. Les formes SHLD/SHRD du 386 vivent dans un autre en-tête.

    /// <summary>pcem: C0, C1, D0, D1, D2, D3 — relevés sur ops_286[] par gdb.
    /// Le couple C0/C1 est immédiat, D0/D1 sous-entend 1, D2/D3 lit CL ; dans
    /// chaque couple le premier est l'octet et le second le mot.</summary>
    private static void PoserGroupeDecalages()
    {
        ops_286[0xC0] = opC0_a16;
        ops_286[0xC1] = opC1_w_a16;
        ops_286[0xD0] = opD0_a16;
        ops_286[0xD1] = opD1_w_a16;
        ops_286[0xD2] = opD2_a16;
        ops_286[0xD3] = opD3_w_a16;
    }
}
