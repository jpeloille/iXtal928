// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel du cœur 286/386/486 (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans les en-têtes x86_*.h, appelées par les gardes des handlers (`if
// (materiel.pb_nn)`), seules sur leur ligne. Chaque fonction ouvre sur son marqueur ; la source, le cas qui discrimine
// et la panne qui le rougit sont dans l'entrée PB de PCEM_BUGS.md. R2 ne s'applique pas : ce code n'a pas de C en
// regard. Il incrémente la sonde (ModeMateriel.Sonde), qui dit qu'il a servi.
//
// Le 286 partage ces handlers. Une correction ne vaut pour lui que si le corpus SST du 286 (Harris N80C286-12) la
// confirme ; sinon elle s'arrête sur is386 et le 286 garde le comportement de PCem (PLAN-G13.md, décision n° 14). Le
// corpus confirme l'AF d'ADC, AAA, AAS, AAD, AAM, AAM 0 et DAS ; il dément LOCK, que le 286 accepte devant tout.
// BT, BTS, BTR, BTC et MOVSX n'existent pas sur le 286.

using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;
using static iXtal26.Cpu.x86_flags;

namespace iXtal26.Cpu;

// Une classe à part, et non une partie de _386 : des membres de plus dans _386, même dans une classe imbriquée,
// décaleraient les numéros que le compilateur donne à ses lambdas et à ses classes de fermeture, et les listings du JIT
// changeraient de noms, en mode PCem aussi.
internal static class _386_materiel
{
    // pcem bug, fixed in hardware mode: PB-181 — AF est la retenue du bit 3, retenue entrante comprise : le bit 4 de
    //   op1 ^ op2 ^ résultat (SDM vol. 1, § 3.4.3.1).
    internal static int af_adc_materiel()
    {
        ModeMateriel.Sonde[181]++;
        return (int)((cpu_state.flags_res ^ cpu_state.flags_op1 ^ cpu_state.flags_op2) & 0x10);
    }

    // pcem bug, fixed in hardware mode: PB-185 — AAA : AX + 106h (SDM). PCem a ajouté 6 à AL et 1 à AH ; la retenue
    //   d'AL + 6, qu'un AL de FAh à FFh produit, passe aussi dans AH.
    internal static void aaa_materiel()
    {
        if (AL >= 6)
                return;
        AH++;
        ModeMateriel.Sonde[185]++;
    }

    // pcem bug, fixed in hardware mode: PB-185 — AAS : AX − 6, puis AH − 1 (SDM). PCem a retiré 6 d'AL et 1 d'AH ;
    //   l'emprunt d'AL − 6, qu'un AL de 00h à 05h produit, passe aussi dans AH.
    internal static void aas_materiel()
    {
        if (AL < 0xFA)
                return;
        AH--;
        ModeMateriel.Sonde[185]++;
    }

    // pcem bug, fixed in hardware mode: PB-186 — SF, ZF et PF d'après AL (SDM, pages AAD et AAM).
    internal static void aad_aam_materiel()
    {
        setznp8(AL);
        ModeMateriel.Sonde[186]++;
    }

    // pcem bug, fixed in hardware mode: PB-187 — AAM 0 lève #DE, l'adresse de l'AAM empilée (SDM, page AAM ; 386 PRM
    //   § 14.7, point 2) : x86_int remet pc sur l'instruction.
    internal static int aam0_materiel()
    {
        x86_int(0);
        ModeMateriel.Sonde[187]++;
        return 1;
    }

    // pcem bug, fixed in hardware mode: PB-188 — DAS selon le SDM : le second test porte sur l'AL et le CF d'origine.
    //   Les drapeaux sont déjà reconstruits (flags_rebuild en tête d'opDAS).
    internal static void das_materiel()
    {
        uint8_t al = AL;
        var cf = (cpu_state.flags & C_FLAG) != 0;
        uint16_t ca = 0;

        if ((cpu_state.flags & A_FLAG) != 0 || ((AL & 0xf) > 9))
        {
                if (AL < 6)
                        ca |= C_FLAG;
                AL -= 6;
                ca |= A_FLAG;
        }
        if (cf || al > 0x99)
        {
                AL -= 0x60;
                ca |= C_FLAG;
        }
        setznp8(AL);
        _386.flags_rebuild();
        cpu_state.flags = (uint16_t)((cpu_state.flags & ~(C_FLAG | A_FLAG)) | ca);
        ModeMateriel.Sonde[188]++;
    }

    // pcem bug, fixed in hardware mode: PB-183 — le décalage de BT, BTS, BTR et BTC est signé : un registre négatif
    //   adresse en arrière (386 PRM § 17.2 ; SDM vol. 2, Table 3-2). PCem a ajouté (r / 16) × 2 ou (r / 32) × 4 non
    //   signés ; pour un registre négatif, la différence avec le décalage signé est fixe : 2000h de moins en 16 bits,
    //   20000000h de moins (E0000000h de plus, modulo 2^32) en 32 bits. Et sous une adresse de 16 bits, l'adresse du
    //   mot, déplacement compris, replie dans les 64 Ko du segment comme toute adresse effective de 16 bits (mesuré :
    //   SST 386, formes 0FA3, 0FAB, 0FB3, 0FBB) ; PCem la laisse déborder au-delà de FFFFh.
    internal static void bt_decalage_materiel(bool l, bool a32)
    {
        if (cpu_mod == 3)
                return;                 // la forme registre n'a pas d'adresse
        var avant = cpu_state.eaaddr;
        if (l ? (cpu_state.regs[cpu_reg].l & 0x80000000) != 0 : (cpu_state.regs[cpu_reg].w & 0x8000) != 0)
                cpu_state.eaaddr += l ? 0xE0000000 : unchecked((uint32_t)(-0x2000));
        if (!a32)
                cpu_state.eaaddr &= 0xffff;
        if (cpu_state.eaaddr != avant)
                ModeMateriel.Sonde[183]++;
    }

    // pcem bug, fixed in hardware mode: PB-262 — le décalage immédiat d'un BT, BTS, BTR ou BTC 16 bits se prend modulo
    //   16 (SDM vol. 2, page BT : « the immediate bit offset ... modulo 16 » ; 386 PRM, page BT : les 4 bits bas).
    internal static int bt_immediat_materiel(int count)
    {
        if ((count & 0x10) == 0)
                return count;           // `1 << count` se réduit modulo 32 : PCem tombe juste
        ModeMateriel.Sonde[262]++;
        return count & 15;
    }

    // pcem bug, fixed in hardware mode: PB-182 — LOCK lève #UD devant toute instruction hors de sa liste, et devant la
    //   forme registre d'une instruction de la liste (386 PRM § 14.7, point 9, et page LOCK ; SDM, page LOCK) : ADD,
    //   ADC, AND, BTS, BTR, BTC, OR, SBB, SUB, XOR, XCHG, NOT, NEG, INC, DEC, et sur le 486 CMPXCHG et XADD, sur la
    //   mémoire. Les préfixes qui suivent LOCK sont sautés jusqu'à l'opcode. Rend vrai si l'exception est levée. Le
    //   286 n'en fait rien : il exécute LOCK devant tout, forme registre comprise (SST 286, une quarantaine de cas par
    //   forme).
    internal static bool lock_materiel()
    {
        if (is386 == 0)
                return false;
        var a = x86.cs + cpu_state.pc - 1;      // opLOCK a déjà passé l'octet qui suit LOCK
        uint8_t op;
        var n = 0;
        do
        {
                op = fastreadb(a + (uint32_t)n++);
                if (cpu_state.abrt != 0)
                        return false;
        } while (n < 15 && op is 0x26 or 0x2e or 0x36 or 0x3e or 0x64 or 0x65 or 0x66 or 0x67 or 0xf0 or 0xf2 or 0xf3);
        var deux = op == 0x0f;
        if (deux)
                op = fastreadb(a + (uint32_t)n++);
        var modrm = fastreadb(a + (uint32_t)n);
        if (cpu_state.abrt != 0)
                return false;
        var reg = (modrm >> 3) & 7;
        var liste = deux
            ? op is 0xab or 0xb3 or 0xbb || (op == 0xba && reg >= 5) || (is486 != 0 && op is 0xb0 or 0xb1 or 0xc0 or 0xc1)
            : (op < 0x40 && (op & 7) < 2 && (op & 0x38) != 0x38) || (op is >= 0x80 and <= 0x83 && reg != 7) ||
              op is 0x86 or 0x87 || (op is 0xf6 or 0xf7 && reg is 2 or 3) || (op is 0xfe or 0xff && reg < 2);
        if (liste && (modrm & 0xc0) != 0xc0)
                return false;
        ModeMateriel.Sonde[182]++;
        return ILLEGAL_ON(true);
    }

    // pcem bug, fixed in hardware mode: PB-184 — la table 0F du mode : MOVSX r16,r/m16 (0BFh, 2BFh) copie le mot,
    //   comme MOVZX r16,r/m16, un mot étendu vers 16 bits n'ayant rien à étendre. Posée par cpu_set, après le gel ;
    //   la table de PCem reste intacte, en mode PCem comme pour la construction des tables.
    private static OpFn[]? ops_386_0f_materiel;

    internal static OpFn[] table_0f_materiel()
    {
        if (ops_386_0f_materiel is null)
        {
                ops_386_0f_materiel = (OpFn[])_386.ops_386_0f.Clone();
                ops_386_0f_materiel[0x0BF] = opMOVSX_w_w_a16_materiel;
                ops_386_0f_materiel[0x2BF] = opMOVSX_w_w_a32_materiel;
        }
        return ops_386_0f_materiel;
    }

    internal static int opMOVSX_w_w_a16_materiel(uint32_t fetchdat)
    {
        ModeMateriel.Sonde[184]++;
        return _386.ops_386_0f[0x0B7](fetchdat);     // MOVZX r16,r/m16, adresse de 16 bits
    }

    internal static int opMOVSX_w_w_a32_materiel(uint32_t fetchdat)
    {
        ModeMateriel.Sonde[184]++;
        return _386.ops_386_0f[0x2B7](fetchdat);     // MOVZX r16,r/m16, adresse de 32 bits
    }
}
