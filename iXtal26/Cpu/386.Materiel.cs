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

using System.Reflection;

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
        if (verrouillable(deux, op, modrm))
                return false;
        ModeMateriel.Sonde[182]++;
        return ILLEGAL_ON(true);
    }

    // La liste de LOCK, sur la mémoire seulement (forme registre exclue).
    private static bool verrouillable(bool deux, uint8_t op, uint8_t modrm)
    {
        var reg = (modrm >> 3) & 7;
        var liste = deux
            ? op is 0xab or 0xb3 or 0xbb || (op == 0xba && reg >= 5) || (is486 != 0 && op is 0xb0 or 0xb1 or 0xc0 or 0xc1)
            : (op < 0x40 && (op & 7) < 2 && (op & 0x38) != 0x38) || (op is >= 0x80 and <= 0x83 && reg != 7) ||
              op is 0x86 or 0x87 || (op is 0xf6 or 0xf7 && reg is 2 or 3) || (op is 0xfe or 0xff && reg < 2);
        return liste && (modrm & 0xc0) != 0xc0;
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

    // pcem bug, fixed in hardware mode: PB-43 — MOV vers et depuis CRx, DRx et TRx : le champ mod est ignoré, rm est
    //   toujours un registre (SDM vol. 2, page MOV, registres de contrôle et de débogage : « The 2 bits in the mod field
    //   are ignored »). Ni déplacement ni SIB : l'instruction a trois octets, préfixes à part. Corrigé, PB-44 (une forme
    //   a32 décodée en 16 bits) ne s'observe plus.
    internal static void modrm_registre_materiel(uint32_t fetchdat)
    {
        cpu_state.pc++;
        cpu_mod = 3;
        cpu_reg = (int8_t)((fetchdat >> 3) & 7);
        cpu_rm = (int8_t)(fetchdat & 7);
        if ((fetchdat & 0xc0) != 0xc0)
                ModeMateriel.Sonde[43]++;
    }

    // pcem bug, fixed in hardware mode: PB-78 — le 486 n'a plus de LOADALL : 0F 07 lève #UD (R. Collins, « The
    //   LOADALL Instruction » ; l'opcode est absent de la carte du 486). Le geste d'ILLEGAL : pc sur l'instruction,
    //   puis l'exception.
    internal static bool loadall486_materiel()
    {
        if (is486 == 0)
                return false;
        cpu_state.pc = cpu_state.oldpc;
        x86illegal();
        ModeMateriel.Sonde[78]++;
        return true;
    }

    // pcem bug, fixed in hardware mode: PB-50 — une instruction de plus de 15 octets lève #GP(0) sur le 386 et le 486
    //   (386 PRM § 14.7, point 6), de plus de 10 sur le 286 (errata Intel « 80286 ARPL and Overlength Instructions »,
    //   1984 ; mesuré : SST 286, 239 cas d'onze octets et plus, l'exception 13, aucune écriture, l'IP du premier préfixe
    //   empilé). Le 386EX mesuré exécute ses instructions de 15 octets (SST 386, neuf cas).
    // pcem bug, fixed in hardware mode: PB-51 — la lecture d'une instruction contrôle la limite de CS : un octet au-delà
    //   lève #GP(0), avant toute exécution (386 PRM § 14.7, point 8 ; pages CALL et JMP du SDM ; mesuré : SST 386, 284
    //   instructions à cheval sur FFFFh, l'exception 13, l'IP de l'instruction empilé). Le 286 en mode réel : OS/2
    //   Museum, son corpus n'ayant aucune instruction à cheval.
    //
    // Les deux bornes se lisent par le même décodeur de longueur, en tête d'instruction, aux quatre sites qui lisent un
    // opcode (exec386 et l'ombre de SS) : PB-50 et PB-51 vont ensemble (ModeMateriel.Groupes). Le chemin court ne décode
    // rien : quinze octets au moins avant la limite, et moins de quatre préfixes en tête, l'instruction tient (onze
    // octets au plus sans préfixe sur le 386, six sur le 286). Sinon le décodeur compte les octets que l'émulateur va
    // consommer, préfixes, opcode, ModRM, SIB, déplacement et immédiat ; un opcode qui tombe sur ILLEGAL s'arrête à
    // lui. Rend le premier mot de l'instruction, comme `fastreadl(cs + pc)` ; sur une faute, abrt est posé, et trap
    // effacé (la boucle d'exec386 ne le relit que pour une instruction lue). Deux écarts connus, sans effet hors du bord
    // de la limite : l'ordre des fautes — le décodeur compte le ModRM et les opérandes d'instructions qui lèveraient
    // d'abord #UD ou #NM (CMPXCHG et XADD sur le 386, ESC sans coprocesseur, ARPL en mode réel, C6 et C7 hors de /0) ; et,
    // sur le 286, F2 ou F3 suivi de 66h ou 67h, que PCem exécute par sa table REP (trois octets) et que le décodeur
    // arrête à 66h, ILLEGAL du 286.
    internal static uint32_t lire_instruction_materiel()
    {
        var pc = cpu_state.pc;
        var limite = cpu_state.seg_cs.limit;
        if (pc <= limite && limite - pc >= 14)
        {
                var dat = fastreadl(x86.cs + pc);
                if (cpu_state.abrt != 0 || !prefixe((uint8_t)dat) || !prefixe((uint8_t)(dat >> 8)) ||
                    !prefixe((uint8_t)(dat >> 16)) || !prefixe((uint8_t)(dat >> 24)))
                        return dat;
        }
        if (!longueur_materiel(pc, limite))
        {
                trap = 0;               // aucune instruction n'a fini : pas de pas-à-pas après la faute
                return 0;
        }
        return fastreadl(x86.cs + pc);
    }

    private static bool prefixe(uint8_t b) =>
        b is 0x26 or 0x2e or 0x36 or 0x3e or 0xf0 or 0xf2 or 0xf3 || (is386 != 0 && b is >= 0x64 and <= 0x67);

    // Les opérandes d'un opcode d'un octet, par ligne de 16 : N aucun, M ModRM, m ModRM et imm8, W ModRM et imm16/32,
    // b imm8, v imm16/32, w imm16, E imm16 et imm8 (ENTER), P sélecteur et offset (CALL et JMP far), O offset d'adresse
    // (MOV A0-A3), F ModRM et l'immédiat de TEST sous /0 et /1 (F6, F7), X l'échappement 0F. Les préfixes sont lus
    // avant.
    private const string Un =
        "MMMMbvNNMMMMbvNX" + "MMMMbvNNMMMMbvNN" + "MMMMbvNNMMMMbvNN" + "MMMMbvNNMMMMbvNN" +
        "NNNNNNNNNNNNNNNN" + "NNNNNNNNNNNNNNNN" + "NNMMNNNNvWbmNNNN" + "bbbbbbbbbbbbbbbb" +
        "mWmmMMMMMMMMMMMM" + "NNNNNNNNNNPNNNNN" + "OOOONNNNbvNNNNNN" + "bbbbbbbbvvvvvvvv" +
        "mmwNMMmWENwNNbNN" + "MMMMbbNNMMMMMMMM" + "bbbbbbbbvvPbNNNN" + "NNNNNNFFNNNNNNMM";

    // Le second octet après 0F (386, 486) : C les registres de contrôle, de débogage et de test (0F 20 à 0F 26). Le
    // 286 n'a que 0F 00 à 0F 03 (ModRM), 0F 05 et 0F 06 ; ses autres entrées sont ILLEGAL.
    private const string Deux =
        "MMMMNNNNNNNNNNNN" + "NNNNNNNNNNNNNNNN" + "CCCCCCCNNNNNNNNN" + "NNNNNNNNNNNNNNNN" +
        "NNNNNNNNNNNNNNNN" + "NNNNNNNNNNNNNNNN" + "NNNNNNNNNNNNNNNN" + "NNNNNNNNNNNNNNNN" +
        "vvvvvvvvvvvvvvvv" + "MMMMMMMMMMMMMMMM" + "NNNMmMMMNNNMmMMM" + "MMMMMMMMNNmMMMMM" +
        "MMNNNNNNNNNNNNNN" + "NNNNNNNNNNNNNNNN" + "NNNNNNNNNNNNNNNN" + "NNNNNNNNNNNNNNNN";

    private static readonly MethodInfo illegal =
        typeof(_386).GetMethod("ILLEGAL", BindingFlags.NonPublic | BindingFlags.Static) ??
        throw new InvalidOperationException("_386.ILLEGAL introuvable : le décodeur de longueur ne reconnaîtrait plus les opcodes invalides");

    // Le décodeur. Rend faux sur une faute (#GP posé, ou l'abandon d'une lecture).
    private static bool longueur_materiel(uint32_t pc, uint32_t limite)
    {
        var max = is386 != 0 ? 15 : 10;
        var o32 = (use32 & 0x100) != 0;
        var a32 = (use32 & 0x200) != 0;
        var n = 0;
        var verrou = false;
        uint8_t b;

        bool octet(int i, out uint8_t v)
        {
                v = 0;
                if (!borne(i))
                        return false;
                v = fastreadb(x86.cs + pc + (uint32_t)i);
                return cpu_state.abrt == 0;
        }
        bool borne(int i)
        {
                if (materiel.pb_50 && i >= max)
                {
                        ModeMateriel.Sonde[50]++;
                        x86seg_c.x86gpf(null!, 0);
                        return false;
                }
                if (pc > limite || (uint32_t)i > limite - pc)
                {
                        ModeMateriel.Sonde[51]++;
                        x86seg_c.x86gpf(null!, 0);
                        return false;
                }
                return true;
        }

        while (true)
        {
                if (!octet(n++, out b))
                        return false;
                if (!prefixe(b))
                        break;
                if (b == 0xf0)
                        verrou = true;
                else if (b == 0x66)
                        o32 = (use32 & 0x100) == 0;
                else if (b == 0x67)
                        a32 = (use32 & 0x200) == 0;
        }
        var quadrant = (o32 ? 0x100 : 0) | (a32 ? 0x200 : 0);
        char genre;
        var deux = b == 0x0f;
        if (deux)
        {
                if (!octet(n++, out b))
                        return false;
                if (_386.x86_opcodes_0f![is386 != 0 ? b | quadrant : b].Method == illegal)
                        return true;
                genre = Deux[b];
        }
        else
        {
                if (_386.x86_opcodes![b | quadrant].Method == illegal)
                        return true;
                genre = Un[b];
        }

        // LOCK devant ce qui ne se verrouille pas : #UD (PB-182) se décide à l'opcode et au ModRM, avant la longueur et la
        // limite (mesuré : SST 386, `676681.7`, LOCK CMP de seize et dix-sept octets, #UD et non #GP). Le handler lève.
        var refus = verrou && materiel.pb_182 && is386 != 0;
        if (refus && genre is not ('M' or 'm' or 'W' or 'F'))
                return true;
        var imm = 0;
        var v = o32 ? 4 : 2;
        switch (genre)
        {
        case 'N':
                return true;
        case 'b':
                return borne(n);
        case 'v':
                return borne(n + v - 1);
        case 'w':
                return borne(n + 1);
        case 'E':
                return borne(n + 2);
        case 'P':
                return borne(n + v + 1);
        case 'O':
                return borne(n + (a32 ? 3 : 1));
        case 'm':
                imm = 1;
                break;
        case 'W':
                imm = v;
                break;
        }

        if (!octet(n++, out var modrm))
                return false;
        if (refus && !verrouillable(deux, b, modrm))
                return true;
        var mod = modrm >> 6;
        var rm = modrm & 7;
        if (genre == 'F' && ((modrm >> 3) & 7) < 2)
                imm = b == 0xf6 ? 1 : v;
        if (genre == 'C')
        {
                if (materiel.pb_43)
                        return true;            // le mod ignoré : ni déplacement ni SIB
                if (b is 0x23 or 0x26)
                        a32 = false;            // PB-44 : fetch_ea_16 sous la forme a32
        }
        var depl = 0;
        if (mod != 3)
        {
                if (a32)
                {
                        if (rm == 4)
                        {
                                if (!octet(n++, out var sib))
                                        return false;
                                if (mod == 0 && (sib & 7) == 5)
                                        depl = 4;
                        }
                        else if (mod == 0 && rm == 5)
                                depl = 4;
                        if (mod == 1)
                                depl = 1;
                        else if (mod == 2)
                                depl = 4;
                }
                else
                        depl = mod == 1 ? 1 : mod == 2 || rm == 6 ? 2 : 0;
        }
        return depl + imm == 0 || borne(n + depl + imm - 1);
    }

    // ===== Le mode protégé (G13.5c) =====
    //
    // Les exceptions sont celles du manuel de chaque processeur (décision de Julien, le 10/10) : sur le 386 et le 486,
    // #TS pour les contrôles d'une tâche par CALL ou par INT, #GP par JMP (386 PRM et i486 PRM, pages CALL, INT et JMP,
    // TASK-GATE et TASK-STATE-SEGMENT) ; sur le 286, #GP partout (80286 PRM, mêmes pages) ; #NP pour la présence, partout.
    // Le SDM, plus tardif, écrit #GP aussi pour CALL et INT : il ne décrit pas ces processeurs-là.

    /// <summary>La faute d'un contrôle de tâche : #TS sur le 386 et le 486 pour CALL et INT (`ts`), #GP sinon.</summary>
    private static void faute_tache(uint16_t code, bool ts)
    {
        if (ts && is386 != 0)
                x86seg_c.x86ts(null!, code);
        else
                x86seg_c.x86gpf(null!, code);
    }

    // pcem bug, fixed in hardware mode: PB-32 — un vecteur hors de l'IDT lève #GP(n × 8 + 2 + EXT) (386 PRM, page INT ;
    //   § 9.7, « Error Code ») ; PCem rend 0, par une précédence d'opérateurs.
    // pcem bug, fixed in hardware mode: PB-192 — la porte doit tenir tout entière dans l'IDT : le SDM (page INT n) compare
    //   `(vector_number « 3) + 7` à la limite ; PCem, le premier octet. Avec PB-32 (ModeMateriel.Groupes). Rend vrai si la
    //   porte dépasse : la faute, ou la double faute pour #GP, ou la triple faute pour #DF, comme la branche de PCem.
    internal static bool idt_limite_materiel(int num, int soft)
    {
        var addr = (uint32_t)(num << 3);
        if (addr + 7 <= idt.limit)
                return false;
        if (addr < idt.limit)
                ModeMateriel.Sonde[192]++;
        if (num == 8)
                _808x.softresetx86();
        else if (num == 0xD)
                x86seg_c.pmodeint(8, 0);
        else
        {
                x86seg_c.x86gpf(null!, (uint16_t)(addr + 2 + (soft != 0 ? 0 : 1)));
                ModeMateriel.Sonde[32]++;
        }
        return true;
    }

    // pcem bug, fixed in hardware mode: PB-192 — une porte de type nul lève #GP(n × 8 + 2 + EXT) : EXT pour un événement
    //   externe au programme, une interruption matérielle ou une exception (386 PRM § 9.7). Un INT n garde le code de
    //   PCem, sans EXT.
    internal static bool porte_nulle_materiel(int num, int soft)
    {
        if (soft != 0)
                return false;
        var code = (uint16_t)((num * 8) + 3);
        if ((cpu_state.eflags & VM_FLAG) != 0)
                x86seg_c.x86gpf_expected(null!, code);
        else
                x86seg_c.x86gpf(null!, code);
        ModeMateriel.Sonde[192]++;
        return true;
    }

    /// <summary>Une TSS disponible : type 1 (286), ou 9 sur le 386 et le 486 ; le mot 2 d'un descripteur, S compris.</summary>
    private static bool tss_disponible(uint16_t mot2) =>
        (mot2 & 0x1F00) == 0x0100 || ((mot2 & 0x1F00) == 0x0900 && is386 != 0);

    /// <summary>Le sélecteur d'une TSS : dans la GDT (TI = 0), ses huit octets sous la limite (386 PRM, pages CALL, JMP
    /// et INT, « Must specify global in the local/global bit », « Index must be within GDT limits »).</summary>
    private static bool selecteur_tss(uint16_t sel) => (sel & 4) == 0 && (uint32_t)(sel | 7) <= gdt.limit;

    // pcem bug, fixed in hardware mode: PB-39 — CALL et JMP sur une porte de tâche (type 5) changent de tâche (386 PRM
    //   § 7.5 et pages CALL et JMP, TASK-GATE) : le DPL de la porte au moins CPL et RPL, la porte présente ; la TSS
    //   qu'elle désigne dans la GDT, disponible et présente ; puis la commutation, avec le lien arrière pour CALL, sans
    //   lui pour JMP, dont NT sort effacé (386 PRM, Table 7-2), comme sur la voie TSS. Rend faux pour un autre type, que
    //   le `default` de PCem traite.
    internal static bool porte_tache_materiel(uint16_t seg, uint16_t[] segdat, uint32_t old_pc, int op)
    {
        if ((segdat[2] & 0x1F00) != 0x0500)
                return false;
        ModeMateriel.Sonde[39]++;
        var dpl = (segdat[2] >> 13) & 3;
        if (dpl < CPL || dpl < (seg & 3))
        {
                faute_tache((uint16_t)(seg & 0xFFFC), op == CALL);
                return true;
        }
        if ((segdat[2] & 0x8000) == 0)
        {
                x86seg_c.x86np(null!, (uint16_t)(seg & 0xFFFC));
                return true;
        }
        var tss = segdat[1];
        if (!selecteur_tss(tss))
        {
                faute_tache((uint16_t)(tss & 0xFFFC), op == CALL);
                return true;
        }
        var addr = gdt.@base + (uint32_t)(tss & ~7);
        var d = new uint16_t[4];
        cpl_override = 1;
        d[0] = readmemw(0, addr);
        d[1] = readmemw(0, addr + 2);
        d[2] = readmemw(0, addr + 4);
        d[3] = readmemw(0, addr + 6);
        cpl_override = 0;
        if (cpu_state.abrt != 0)
                return true;
        if (!tss_disponible(d[2]))
        {
                faute_tache((uint16_t)(tss & 0xFFFC), op == CALL);
                return true;
        }
        if ((d[2] & 0x8000) == 0)
        {
                x86seg_c.x86np(null!, (uint16_t)(tss & 0xFFFC));
                return true;
        }
        cpu_state.pc = old_pc;
        optype = op;
        cpl_override = 1;
        x86seg_c.taskswitch286(tss, d, d[2] & 0x800);
        if (op == JMP)
                cpu_state.flags &= unchecked((uint16_t)~NT_FLAG);
        cpl_override = 0;
        if (op == CALL && materiel.pb_263)
                ip_tache_materiel();
        if (op == CALL && materiel.pb_40)
                commutation = cpu_state.abrt == 0;
        return true;
    }

    // pcem bug, fixed in hardware mode: PB-191 — CALL et JMP directement sur une TSS : la TSS dans la GDT, son DPL au
    //   moins CPL et RPL, présente (386 PRM, pages CALL et JMP, TASK-STATE-SEGMENT ; § 7.2, « TSS descriptors may reside
    //   only in the GDT »), disponible (une TSS 386, type 9, n'existe pas sur le 286 : #GP, 80286 PRM). Rend vrai sur
    //   une faute.
    internal static bool tss_directe_materiel(uint16_t seg, uint16_t[] segdat, int op)
    {
        var dpl = (segdat[2] >> 13) & 3;
        if ((seg & 4) != 0 || dpl < CPL || dpl < (seg & 3) || !tss_disponible(segdat[2]))
                faute_tache((uint16_t)(seg & 0xFFFC), op == CALL);
        else if ((segdat[2] & 0x8000) == 0)
                x86seg_c.x86np(null!, (uint16_t)(seg & 0xFFFC));
        else
                return false;
        ModeMateriel.Sonde[191]++;
        return true;
    }

    // pcem bug, fixed in hardware mode: PB-191 — CALL et JMP sur une TSS occupée (type 3, ou Bh sur le 386 et le 486) :
    //   #TS(sélecteur) pour CALL sur le 386 et le 486, #GP(sélecteur) sinon (pages CALL et JMP, « TSS descriptor AR byte
    //   must specify available TSS ») ; PCem lève #GP(sélecteur) pour CALL, #GP(0) pour JMP. Rend faux pour un autre type,
    //   que le `default` de PCem traite.
    internal static bool tss_occupee_materiel(uint16_t seg, uint16_t[] segdat, int op)
    {
        var type = segdat[2] & 0x1F00;
        if (type != 0x0300 && !(type == 0x0B00 && is386 != 0))
                return false;
        faute_tache((uint16_t)(seg & 0xFFFC), op == CALL);
        ModeMateriel.Sonde[191]++;
        return true;
    }

    // pcem bug, fixed in hardware mode: PB-191 — JMP sur une TSS, contrôlé avant le test de présence que loadcsjmp fait
    //   pour tout descripteur système : DPL, GDT et type d'abord, #GP, puis la présence, #NP (page JMP). Rend faux pour
    //   un autre type.
    internal static bool tss_jmp_materiel(uint16_t seg, uint16_t[] segdat)
    {
        var type = segdat[2] & 0x1F00;
        if (type is 0x0100 or 0x0900)
                return tss_directe_materiel(seg, segdat, JMP);
        return tss_occupee_materiel(seg, segdat, JMP);
    }

    // pcem bug, fixed in hardware mode: PB-191 — la porte de tâche de l'IDT désigne une TSS de la GDT, disponible (386
    //   PRM, page INT, TASK-GATE : « AR byte must specify available TSS ») : #TS(sélecteur) sur le 386 et le 486, #GP sur
    //   le 286. Le code porte EXT pour une exception ou une interruption matérielle (386 PRM § 9.7, comme PB-192). La
    //   présence reste au test de PCem, qui suit. Rend vrai sur une faute.
    internal static bool tss_int_materiel(uint16_t seg, int soft)
    {
        if (selecteur_tss(seg))
        {
                cpl_override = 1;
                var mot2 = readmemw(0, gdt.@base + (uint32_t)(seg & ~7) + 4);
                cpl_override = 0;
                if (cpu_state.abrt != 0)
                        return true;
                if (tss_disponible(mot2))
                        return false;
        }
        faute_tache((uint16_t)((seg & 0xFFFC) | (soft != 0 ? 0 : 1)), true);
        ModeMateriel.Sonde[191]++;
        return true;
    }

    // pcem bug, fixed in hardware mode: PB-263 — après la commutation d'un CALL, l'IP doit tenir dans la limite du nouveau
    //   CS, sinon #TS(0) sur le 386 et le 486 (386 PRM et i486 PRM, page CALL : « IP must be in code segment limit ELSE
    //   #TS(0) ») ; la faute se livre dans la nouvelle tâche, à son IP (x86_doabrt reprend oldpc). Sur le 286, et pour JMP
    //   et INT, le manuel écrit #GP(0), que la lecture de l'instruction lève déjà (PB-51).
    internal static void ip_tache_materiel()
    {
        if (is386 == 0 || cpu_state.abrt != 0 || cpu_state.pc <= cpu_state.seg_cs.limit)
                return;
        cpu_state.oldpc = cpu_state.pc;
        x86seg_c.x86ts(null!, 0);
        ModeMateriel.Sonde[263]++;
    }

    /// <summary>PB-40 : un CALL vient de changer de tâche. Posé par la voie TSS de loadcscall et par la porte de tâche
    /// (PB-39), lu et effacé par CALL_FAR_w et CALL_FAR_l.</summary>
    internal static bool commutation;

    // pcem bug, fixed in hardware mode: PB-40 — la voie TSS de loadcscall a changé de tâche, ou non.
    internal static void tache_appelee_materiel() => commutation = cpu_state.abrt == 0;

    // pcem bug, fixed in hardware mode: PB-40 — un CALL qui change de tâche n'empile rien : le lien arrière de la nouvelle
    //   TSS tient lieu de retour, et IRET le suit (386 PRM § 7.6, Table 7-2 ; page CALL, TASK-GATE et
    //   TASK-STATE-SEGMENT). Rend vrai si CALL_FAR doit s'arrêter là.
    internal static bool appel_de_tache_materiel()
    {
        if (!commutation)
                return false;
        commutation = false;
        ModeMateriel.Sonde[40]++;
        return true;
    }

    // pcem bug, fixed in hardware mode: PB-190 — LTR : le sélecteur dans la GDT et sous sa limite, une TSS disponible,
    //   sinon #GP(sélecteur) ; présente, sinon #NP(sélecteur) (386 PRM, page LTR ; § 7.2). Le sélecteur nul désigne le
    //   descripteur nul, qui n'est pas une TSS : #GP(0). Rend vrai sur une faute.
    internal static bool ltr_materiel(uint16_t sel)
    {
        var code = (uint16_t)(sel & 0xFFFC);
        if (selecteur_tss(sel))
        {
                var mot2 = readmemw(0, gdt.@base + (uint32_t)(sel & ~7) + 4);
                if (cpu_state.abrt != 0)
                        return true;
                if (!tss_disponible(mot2))
                        x86seg_c.x86gpf(null!, code);
                else if ((mot2 & 0x8000) == 0)
                        x86seg_c.x86np(null!, code);
                else
                        return false;
        }
        else
                x86seg_c.x86gpf(null!, code);
        ModeMateriel.Sonde[190]++;
        return true;
    }

    // pcem bug, fixed in hardware mode: PB-193 — LOADALL386 hors du niveau 0, en mode protégé, lève l'exception 13 (R.
    //   Collins, « The LOADALL Instruction ») ; la garde d'opLOADALL, celui du 286. Rend vrai sur la faute.
    internal static bool loadall386_privilege_materiel()
    {
        if (CPL == 0 || (cr0 & 1) == 0)
                return false;
        x86seg_c.x86gpf(null!, 0);
        ModeMateriel.Sonde[193]++;
        return true;
    }
}
