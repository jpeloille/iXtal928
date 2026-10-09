// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel du 8088 et du 8086 (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans 808x.c, appelées par les gardes de 808x.cs (`if (materiel.pb_nn)`), seules
// sur leur ligne. Chaque fonction ouvre sur son marqueur ; la source, le cas qui discrimine et la panne qui le rougit
// sont dans l'entrée PB de PCEM_BUGS.md. R2 ne s'applique pas : ce code n'a pas de C en regard. Il incrémente la sonde
// (ModeMateriel.Sonde), qui dit qu'il a servi.

using iXtal26.Memory;
using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;

namespace iXtal26.Cpu;

internal static partial class _808x
{
    // pcem bug, fixed in hardware mode: PB-01 — AF est la retenue (l'emprunt) du bit 3, retenue entrante comprise : le
    //   bit 4 de a ^ b ^ résultat, pour ADC comme pour SBB.
    private static void af_materiel(uint32_t a, uint32_t b, uint32_t c)
    {
        ModeMateriel.Sonde[1]++;
        if (((a ^ b ^ c) & 0x10) != 0)
                cpu_state.flags |= A_FLAG;
    }

    // pcem bug, fixed in hardware mode: PB-02 — RCL et RCR mot par CL : le CF que la boucle a posé, le dernier bit sorti,
    //   reste ; le bloc de PCem qui l'écrasait est sauté. La garde suffit : rien d'autre à faire que compter.
    private static void rcl_rcr_materiel()
    {
        ModeMateriel.Sonde[2]++;
    }

    // pcem bug, fixed in hardware mode: PB-03 — les cycles que timer_process() vient de débiter (le rafraîchissement par
    //   DMA du 5150 et de l'XT : refreshread → FETCHCOMPLETE) entrent au TSC, comme ceux de l'instruction : un cycle volé
    //   par le rafraîchissement s'écoule aussi pour le PIT (IBM PC TR 6025008, p. 2-3 et 2-22). timer_process, puis le
    //   même compte que clockhardware, sans relancer timer_process : la prochaine échéance attend l'instruction suivante.
    private static void clockhardware_materiel()
    {
        timer.timer_process();
        int diff = cycdiff - cycles - current_diff;

        if (diff == 0)
                return;
        ModeMateriel.Sonde[3]++;
        current_diff += diff;
        tsc_frac += (uint64_t)diff * xt_cpu_multi;
        timer.tsc += (tsc_frac >> 32);
        tsc_frac &= 0xffffffff;
    }

    // pcem bug, fixed in hardware mode: PB-07 et PB-179 — un mot se lit octet par octet, chacun par sa propre page :
    //   l'octet haut d'un mot en fin de page (PB-07 : au sommet de la RAM, au repli de 1 Mo) vient de la page suivante,
    //   et celui d'un mot à l'offset FFFFh vient de l'offset 0 du même segment (PB-179 ; 386 PRM § 14.7, points 7 et
    //   18). readmembf passe par le cache de pages ou par readmembl, qui applique rammask : le repli de 1 Mo du 8088.
    //   Les cycles du mot ont déjà été comptés par readmemw.
    private static uint16_t readmemw_materiel(int pb, uint32_t bas, uint32_t haut)
    {
        ModeMateriel.Sonde[pb]++;
        return (uint16_t)(readmembf(bas) | (readmembf(haut) << 8));
    }

    private static void writememw_materiel(int pb, uint32_t bas, uint32_t haut, uint16_t v)
    {
        ModeMateriel.Sonde[pb]++;
        writemembf_materiel(bas, (uint8_t)v);
        writemembf_materiel(haut, (uint8_t)(v >> 8));
    }

    private static void writemembf_materiel(uint32_t a, uint8_t v)
    {
        if (mem.writelookup2[a >> 12] == -1)
                mem.writemembl(a, v);
        else
                mem.ram[mem.writelookup2[a >> 12] + a] = v;
    }

    // pcem bug, fixed in hardware mode: PB-87 — la lecture d'instruction, file vide : au repli de l'IP, l'octet vient de
    //   l'offset 0 du segment (386 PRM § 14.7, point 8). `pc` n'est masqué qu'en fin d'instruction ; on lit à
    //   `cs + (pc & 0xFFFF)`.
    // DEVIATION: R9 — une instruction de plus de 64 Kio (une chaîne de préfixes qui remplit son segment) ne finirait
    //   jamais sur le silicium, qui n'accepte aucune interruption entre deux préfixes : l'émulateur ne rendrait plus la
    //   main. Au-delà, la lecture redevient celle de PCem (`cs + pc`), qui sort du segment et finit par lire un
    //   non-préfixe.
    private static uint8_t fetch_materiel(uint32_t pc)
    {
        if (pc > 0xFFFF && pc <= 0x1FFFF)
        {
                ModeMateriel.Sonde[87]++;
                return readmembf(cs + (pc & 0xFFFF));
        }
        return readmembf(cs + pc);
    }

    // pcem bug, fixed in hardware mode: PB-45 et PB-169 — DIV et IDIV : le dividende d'IDIV octet est AX signé (PB-45) ;
    //   un quotient hors capacité lève l'interruption 0, comme le diviseur nul (PB-169 ; 386 PRM, pages DIV et IDIV),
    //   et sur le 8086 et le 8088 un quotient de 80h (8000h) aussi (386 PRM § 14.7, point 11). L'adresse empilée est
    //   celle de l'instruction suivante (§ 14.7, point 2). Les drapeaux empilés restent ceux d'avant l'instruction :
    //   PB-180, dont la règle n'est pas connue, reste reproduit.
    private static void div8_materiel(uint8_t d)
    {
        if (d == 0 || AX / d > 0xFF)
        {
                if (d != 0)
                        ModeMateriel.Sonde[169]++;
                int0_materiel();
                return;
        }
        var q = AX / d;
        AH = (uint8_t)(AX % d);
        AL = (uint8_t)q;
    }

    private static void idiv8_materiel(uint8_t d)
    {
        int dividende = (int16_t)AX, diviseur = (int8_t)d;
        if (dividende < 0)
                ModeMateriel.Sonde[45]++;
        if (diviseur == 0 || dividende / diviseur is > 127 or < -127)
        {
                if (diviseur != 0)
                        ModeMateriel.Sonde[169]++;
                int0_materiel();
                return;
        }
        AH = (uint8_t)(dividende % diviseur);
        AL = (uint8_t)(rep_idiv_materiel() ? -(dividende / diviseur) : dividende / diviseur);
    }

    private static void div16_materiel(uint16_t d)
    {
        var dividende = ((uint32_t)DX << 16) | AX;
        if (d == 0 || dividende / d > 0xFFFF)
        {
                if (d != 0)
                        ModeMateriel.Sonde[169]++;
                int0_materiel();
                return;
        }
        DX = (uint16_t)(dividende % d);
        AX = (uint16_t)(dividende / d);
    }

    private static void idiv16_materiel(uint16_t d)
    {
        long dividende = (int32_t)(((uint32_t)DX << 16) | AX), diviseur = (int16_t)d;
        if (diviseur == 0 || dividende / diviseur is > 32767 or < -32767)
        {
                if (diviseur != 0)
                        ModeMateriel.Sonde[169]++;
                int0_materiel();
                return;
        }
        DX = (uint16_t)(dividende % diviseur);
        AX = (uint16_t)(rep_idiv_materiel() ? -(dividende / diviseur) : dividende / diviseur);
    }

    /// <summary>L'erreur de division, le chemin de PCem pour le diviseur nul (808x.c:3595).</summary>
    // pcem bug, fixed in hardware mode: PB-258 — l'interruption empile dans SS, préfixe de segment ou non : un préfixe a
    //   posé `ss` sur son segment (`ds = ss = …`), et PCem empile avant de le remettre ; les PUSH, eux, le remettent.
    private static void int0_materiel()
    {
        if (cpu_state.ssegs != 0)
        {
                ModeMateriel.Sonde[258]++;
                ss = oldss;
        }
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

    // pcem bug, fixed in hardware mode: PB-170 — DAA du 8088 mesuré (SST 8088 v2, AMD D8088, forme 27 ; et SST 8086,
    //   Intel P80C86A-2 : 10 000 et 2 000 cas sur autant) : le premier pas ajoute 6 si le quartet bas dépasse 9 ou si AF
    //   valait 1, et pose AF, sinon l'efface ; le second ajoute 60h et pose CF si l'AL d'ORIGINE dépasse 99h — 9Fh
    //   quand AF valait 1 — ou si CF valait 1, sinon efface CF. SF, ZF et PF d'après AL ; OF n'est pas touché (le corpus
    //   le masque).
    private static void daa_materiel()
    {
        ModeMateriel.Sonde[170]++;
        var avant = AL;
        var af = (cpu_state.flags & A_FLAG) != 0;
        var cf = (cpu_state.flags & C_FLAG) != 0;
        cpu_state.flags &= unchecked((uint16_t)~(A_FLAG | C_FLAG));
        if (af || (avant & 0xF) > 9)
        {
                AL += 6;
                cpu_state.flags |= A_FLAG;
        }
        if (cf || avant > (af ? 0x9F : 0x99))
        {
                AL += 0x60;
                cpu_state.flags |= C_FLAG;
        }
        setznp8(AL);
    }

    // pcem bug, fixed in hardware mode: PB-171 — DAS, la même règle (SDM vol. 2, page DAS : `old_AL`, `old_CF` ; le
    //   seuil de 9Fh quand AF valait 1, mesuré : SST 8088 v2 et 8086, forme 2F, 10 000 et 2 000 cas sur autant). L'emprunt
    //   du premier pas ne pose pas CF : seul le second le décide.
    private static void das_materiel()
    {
        ModeMateriel.Sonde[171]++;
        var avant = AL;
        var af = (cpu_state.flags & A_FLAG) != 0;
        var cf = (cpu_state.flags & C_FLAG) != 0;
        cpu_state.flags &= unchecked((uint16_t)~(A_FLAG | C_FLAG));
        if (af || (avant & 0xF) > 9)
        {
                AL -= 6;
                cpu_state.flags |= A_FLAG;
        }
        if (cf || avant > (af ? 0x9F : 0x99))
        {
                AL -= 0x60;
                cpu_state.flags |= C_FLAG;
        }
        setznp8(AL);
    }

    // pcem bug, fixed in hardware mode: PB-172 — LODS charge AL (AX) à chaque répétition (Intel, page LODS).
    private static void lodsb_materiel(uint8_t v)
    {
        ModeMateriel.Sonde[172]++;
        AL = v;
    }

    private static void lodsw_materiel(uint16_t v)
    {
        ModeMateriel.Sonde[172]++;
        AX = v;
    }

    // pcem bug, fixed in hardware mode: PB-173 — /6 de D0 à D3 : SETMO (par 1) et SETMOC (par CL, CL ≠ 0, que 808x.cs a
    //   déjà testé) mettent l'opérande à FFh (FFFFh). Les drapeaux sont ceux que le 8088 laisse, mesurés (SST 8088 v2,
    //   D0.6 à D3.6 : sur tous les cas, l'octet bas vaut 84h) : SF et PF posés, ZF, AF, CF et OF effacés. Le temps est
    //   celui de SHL, que PCem lui donne. Rend faux pour /4 (SHL), que 808x.cs exécute.
    private static bool setmo8_materiel(int reg, int memoire)
    {
        if ((rmdat & 0x38) != 0x30)
                return false;
        ModeMateriel.Sonde[173]++;
        seteab(0xFF);
        setmo_drapeaux_materiel();
        cycles -= (cpu_mod == 3) ? reg : memoire;
        return true;
    }

    private static bool setmo16_materiel(int reg, int memoire)
    {
        if ((rmdat & 0x38) != 0x30)
                return false;
        ModeMateriel.Sonde[173]++;
        seteaw(0xFFFF);
        setmo_drapeaux_materiel();
        cycles -= (cpu_mod == 3) ? reg : memoire;
        return true;
    }

    private static void setmo_drapeaux_materiel()
    {
        cpu_state.flags &= unchecked((uint16_t)~(C_FLAG | P_FLAG | A_FLAG | Z_FLAG | N_FLAG | V_FLAG));
        cpu_state.flags |= N_FLAG | P_FLAG;
    }

    // pcem bug, fixed in hardware mode: PB-174 — l'OF d'un décalage par CL est celui de son dernier pas d'un bit (règle
    //   tirée de SST 8088 v2, D2.4, D2.5, D2.7 : 14 566 cas sur autant ; Intel le dit indéfini) : SHL, le bit de poids
    //   fort du résultat XOR CF ; SHR, le bit de poids fort de l'opérande avant le dernier pas, soit le bit 6 (14) du
    //   résultat ; SAR, 0.
    private static void of_shl8_materiel(uint8_t r)
    {
        ModeMateriel.Sonde[174]++;
        of_materiel(((r >> 7) ^ (cpu_state.flags & C_FLAG)) != 0);
    }

    private static void of_shl16_materiel(uint16_t r)
    {
        ModeMateriel.Sonde[174]++;
        of_materiel(((r >> 15) ^ (cpu_state.flags & C_FLAG)) != 0);
    }

    private static void of_shr8_materiel(uint8_t r)
    {
        ModeMateriel.Sonde[174]++;
        of_materiel((r & 0x40) != 0);
    }

    private static void of_shr16_materiel(uint16_t r)
    {
        ModeMateriel.Sonde[174]++;
        of_materiel((r & 0x4000) != 0);
    }

    private static void of_sar_materiel()
    {
        ModeMateriel.Sonde[174]++;
        of_materiel(false);
    }

    private static void of_materiel(bool of)
    {
        if (of)
                cpu_state.flags |= V_FLAG;
        else
                cpu_state.flags &= unchecked((uint16_t)~V_FLAG);
    }

    // pcem bug, fixed in hardware mode: PB-176 — SAR par CL : le dernier bit sorti est une copie du signe dès que le
    //   compte atteint la largeur ; le 8088 ne masque pas le compte (386 PRM § 14.7, point 5).
    private static void cf_sar8_materiel(uint8_t v, int c)
    {
        ModeMateriel.Sonde[176]++;
        if ((((int8_t)v >> Math.Min(c - 1, 7)) & 1) != 0)
                cpu_state.flags |= C_FLAG;
        else
                cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
    }

    private static void cf_sar16_materiel(uint16_t v, int c)
    {
        ModeMateriel.Sonde[176]++;
        if ((((int16_t)v >> Math.Min(c - 1, 15)) & 1) != 0)
                cpu_state.flags |= C_FLAG;
        else
                cpu_state.flags &= unchecked((uint16_t)~C_FLAG);
    }

    // pcem bug, fixed in hardware mode: PB-175 — AAM et AAD : SF, ZF et PF d'après AL (SDM vol. 2, pages AAD et AAM).
    private static void znp_al_materiel()
    {
        ModeMateriel.Sonde[175]++;
        setznp8(AL);
    }

    // pcem bug, fixed in hardware mode: PB-177 — rep() : un REP devant autre chose qu'une chaîne n'a pas d'effet, et
    //   l'instruction qui le suit s'exécute DANS LA MÊME instruction, avec les préfixes de segment placés avant le REP
    //   (un préfixe vaut pour l'instruction qui le suit, dans n'importe quel ordre) ; 6Eh est l'alias de JLE, OUTS
    //   n'existe pas avant le 186 (386 PRM § 14.7, point 3) ; REP DS: répète la chaîne. PCem relance à `ipc + 1`, le
    //   début de toute l'instruction plus un, en fin d'instruction : un préfixe placé avant le REP y est perdu. Ici, le
    //   REP regarde, sans le lire, le premier octet qui suit ses préfixes de segment (la file, puis la mémoire) : devant
    //   une chaîne, rep() suit son cours ; devant autre chose, le REP n'est qu'un préfixe, et execx86 repart à
    //   opcodestart sans finir l'instruction, la file intacte. Son prix, REP_PRIX_MATERIEL, est celui d'un préfixe de
    //   segment du cœur (`case 0x26` d'execx86), et non les 20 cycles et la file vidée de PCem : le silicium paie le
    //   REP 7,6 cycles devant IDIV, file de départ vide, le cœur 7,8 (sst-rep-temps, VERIFICATION.md § G13.3, le temps
    //   du REP). Au-delà de quinze préfixes de segment, la borne de rep_idiv_materiel, rep() suit son cours. IDIV ainsi
    //   relancé rend l'opposé de son quotient, le reste inchangé (README SST 8088 ; mesuré sur les cas à registre des
    //   formes F6.7 et F7.7, au 8088 et au 8086 : tous) ; la capacité, symétrique, ne change pas. Sans état :
    //   rep_idiv_materiel relit les préfixes de l'instruction, depuis son début (oldpc), en mode matériel avec PB-177
    //   seulement — sans lui, IDIV n'est pas dans la même instruction que son REP.
    private const int REP_PRIX_MATERIEL = 4;
    private static bool repRelance;

    private static bool rep_prefixe_materiel()
    {
        for (uint32_t k = 0; k < 16; k++)
        {
                var b = k < prefetchw ? prefetchqueue[k] : readmembf(cs + ((cpu_state.pc + k) & 0xFFFF));
                if (b is 0x26 or 0x2E or 0x36 or 0x3E)
                        continue;
                if (b is (>= 0xA4 and <= 0xA7) or (>= 0xAA and <= 0xAF))
                        return false;
                ModeMateriel.Sonde[177]++;
                cycles -= REP_PRIX_MATERIEL;
                repRelance = true;
                return true;
        }
        return false;
    }

    private static uint32_t rep_ds_materiel()
    {
        ModeMateriel.Sonde[177]++;
        return (uint32_t)DS << 4;
    }

    private static bool rep_idiv_materiel()
    {
        if (!materiel.pb_177)
                return false;
        for (uint32_t k = 0; k < 16; k++)
        {
                var b = readmembf(cs + ((cpu_state.oldpc + k) & 0xFFFF));
                if (b is 0xF2 or 0xF3)
                {
                        ModeMateriel.Sonde[177]++;
                        return true;
                }
                if (b is not (0x26 or 0x2E or 0x36 or 0x3E or 0xF0 or 0xF1))
                        return false;
        }
        return false;
    }

    private static bool rep_relance_materiel()
    {
        if (!repRelance)
                return false;
        repRelance = false;
        return true;
    }
}
