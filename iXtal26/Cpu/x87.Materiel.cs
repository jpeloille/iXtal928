// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel du x87 (G13.6 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les gestionnaires du x87 sont générés (tools/x87gen) et aiguillés par table : aucune garde ne s'y écrit (PLAN-G13.md,
// § Le mécanisme). En mode matériel, cpu_set pose donc des tables du mode : des copies de celles de PCem, où chaque
// correction demandée enveloppe ses gestionnaires (`poser`, sous la garde de son PB, dans cpu_set). Le mode PCem garde
// les tables de PCem, intactes, sans copie. Ne restent ici que l'acheminement de l'exception (PB-59, 69, 204), qui vaut
// avec les tables de PCem comme avec celles du noyau : les corrections en `double` de G13.6a et 6b (PB-57, 58, 61, 63,
// 64, 66, 67, 70, 207, 213), que le noyau de 80 bits a reprises (N7c), ont quitté le dépôt (G13.6, le noyau, N8a ;
// leurs cas restent, joués sur le noyau). Le 8087, que execx86 indexe en dur (ops_808x_fpu_*), a ses entrées
// remplacées en place (808x.Materiel.cs).
//
// Chaque gestionnaire ouvre sur son marqueur et incrémente la sonde (ModeMateriel.Sonde) ; la source, le cas qui
// discrimine et la panne qui le rougit sont dans l'entrée PB de PCEM_BUGS.md.

using static iXtal26.Cpu._386_common;
using static iXtal26.Cpu.x86;

namespace iXtal26.Cpu;

// Une classe à part, comme _386_materiel : des membres de plus dans _386 renuméroteraient ses lambdas.
internal static class _x87_materiel
{
    /// <summary>Pose dans les tables du mode les gestionnaires de la correction `pb` (sans coprocesseur, rien).</summary>
    internal static void poser(int pb)
    {
        // La carte se remet avec le système, coprocesseur ou non : ni verrou, ni IGNNE#, ni la NMI du 8087.
        if (pb == 204)
                verrou = ignne = false;
        if (pb == 69)
                _808x.nmi = 0;
        if (cpu_c.hasfpu == 0)
                return;
        switch (pb)
        {
        case 59:
                partout(h => h.Method.Name == "opFCLEX" ? efface_b(h) : null);
                if (cpu_c.fpu_type == cpu_c.FPU_8087)
                        _808x.poser_8087_materiel(59);
                break;
        case 69:
                if (cpu_c.fpu_type == cpu_c.FPU_8087)
                        _808x.poser_8087_materiel(69);
                break;
        case 204:
                if (cpu_c.fpu_type != cpu_c.FPU_8087)
                        attente();
                break;
        }
    }

    /// <summary>Remplace, dans les seize tables du mode, chaque gestionnaire que `f` désigne (par son nom) par celui que
    /// `f` rend ; une table n'est copiée que si elle change. PB-204, qui enveloppe par position, se pose après toutes les
    /// poses par nom.</summary>
    private static void partout(Func<OpFn, OpFn?> f)
    {
        _386.x86_opcodes_d8_a16 = sur(_386.x86_opcodes_d8_a16!, _386.ops_fpu_d8_a16, f);
        _386.x86_opcodes_d8_a32 = sur(_386.x86_opcodes_d8_a32!, _386.ops_fpu_d8_a32, f);
        _386.x86_opcodes_d9_a16 = sur(_386.x86_opcodes_d9_a16!, _386.ops_fpu_d9_a16, f);
        _386.x86_opcodes_d9_a32 = sur(_386.x86_opcodes_d9_a32!, _386.ops_fpu_d9_a32, f);
        _386.x86_opcodes_da_a16 = sur(_386.x86_opcodes_da_a16!, _386.ops_fpu_da_a16, f);
        _386.x86_opcodes_da_a32 = sur(_386.x86_opcodes_da_a32!, _386.ops_fpu_da_a32, f);
        _386.x86_opcodes_db_a16 = sur(_386.x86_opcodes_db_a16!, _386.ops_fpu_db_a16, f);
        _386.x86_opcodes_db_a32 = sur(_386.x86_opcodes_db_a32!, _386.ops_fpu_db_a32, f);
        _386.x86_opcodes_dc_a16 = sur(_386.x86_opcodes_dc_a16!, _386.ops_fpu_dc_a16, f);
        _386.x86_opcodes_dc_a32 = sur(_386.x86_opcodes_dc_a32!, _386.ops_fpu_dc_a32, f);
        _386.x86_opcodes_dd_a16 = sur(_386.x86_opcodes_dd_a16!, _386.ops_fpu_dd_a16, f);
        _386.x86_opcodes_dd_a32 = sur(_386.x86_opcodes_dd_a32!, _386.ops_fpu_dd_a32, f);
        _386.x86_opcodes_de_a16 = sur(_386.x86_opcodes_de_a16!, _386.ops_fpu_de_a16, f);
        _386.x86_opcodes_de_a32 = sur(_386.x86_opcodes_de_a32!, _386.ops_fpu_de_a32, f);
        _386.x86_opcodes_df_a16 = sur(_386.x86_opcodes_df_a16!, _386.ops_fpu_df_a16, f);
        _386.x86_opcodes_df_a32 = sur(_386.x86_opcodes_df_a32!, _386.ops_fpu_df_a32, f);
    }

    private static OpFn[] sur(OpFn[] posee, OpFn[] pcem, Func<OpFn, OpFn?> f)
    {
        var t = posee;
        for (var i = 0; i < posee.Length; i++)
                if (posee[i] is { } h && f(h) is { } n)
                {
                        t = copie(t, pcem);
                        t[i] = n;
                }
        return t;
    }

    /// <summary>La table du mode : une copie de celle de PCem à la première correction qui la touche, puis la même.</summary>
    private static OpFn[] copie(OpFn[] posee, OpFn[] pcem) => ReferenceEquals(posee, pcem) ? (OpFn[])pcem.Clone() : posee;

    // ===== L'exception démasquée et son acheminement : PB-59, PB-69, PB-204 (un groupe, ModeMateriel.Groupes) =====
    //
    // ZE seule : x87_div est le seul site où PCem lève une exception ; les autres viendront avec le noyau de 80 bits
    // (point de décision n° 10). Le 8087 signale par sa sortie INT, que le PC et le XT mènent à la NMI (AP-578 § 2.1) ;
    // le 287 et le 387 par ERROR#, que la carte AT mène à IRQ13 en verrouillant BUSY# jusqu'à une écriture au port F0h
    // (387 PRM, annexe F ; AP-578 § 2.2.1) ; le 486 par FERR#, mené à IRQ13, et il prend lui-même #MF si NE (bit 5 de
    // CR0) est posé, sinon il s'arrête devant l'instruction suivante jusqu'à IGNNE#, que la carte pose à l'écriture au
    // port F0h (486 PRM § 16.2.1.2). Chaque signal part sur un front : une exception de plus, ES déjà posé, n'en lève
    // pas d'autre.

    private const uint16_t ES = 0x80, B = 0x8000;

    /// <summary>Le verrou de la carte AT : ERROR# du 287 ou du 387 tient BUSY# actif jusqu'à une écriture au port F0h ou
    /// F1h.</summary>
    internal static bool verrou;

    /// <summary>IGNNE# du 486 : posé par une écriture au port F0h tant que FERR# est actif, il retombe avec lui.</summary>
    internal static bool ignne;

    // pcem bug, fixed in hardware mode: PB-59 — une exception démasquée pose ES (IR sur le 8087) et B (387 PRM, mot
    //   d'état ; 287 PRM p. 1-10 ; le 8087 : déduit, comme le 287) ; la destination et la pile restent intactes, comme
    //   le gestionnaire de PCem les laisse ; ses cycles aussi (déduit). PB-69 : sur le 8087, la sortie INT, si IEM (bit
    //   7 de npxc) est nul, lève la NMI (AP-578 § 2.1). PB-204 : sur le 287 et le 387, IRQ13 et le verrou ; sur le 486,
    //   IRQ13, que NE soit posé ou non (déduit : la carte mène FERR# à IRQ13 ; le 486 l'active dans les deux cas).
    internal static bool exception_demasquee()
    {
        var front = (cpu_state.npxs & ES) == 0;
        cpu_state.npxs |= ES | B;
        ModeMateriel.Sonde[59]++;
        if (!front)
                return true;
        if (cpu_c.fpu_type == cpu_c.FPU_8087)
        {
                if ((cpu_state.npxc & 0x80) == 0)
                {
                        _808x.nmi = 1;
                        ModeMateriel.Sonde[69]++;
                }
                return true;
        }
        Models.pic.picint(1 << 13);
        if (cpu_c.fpu_type == cpu_c.FPU_BUILTIN)
                ignne = false;
        else
        {
                verrou = true;
                ModeMateriel.Sonde[204]++;
        }
        return true;
    }

    // pcem bug, fixed in hardware mode: PB-59 — FCLEX efface les exceptions, ES et B (287 PRM, FCLEX ; 486 PRM,
    //   FCLEX) ; celui de PCem (`npxs &= FF00h`) garde B.
    private static OpFn efface_b(OpFn pcem) => fetchdat =>
    {
        var r = pcem(fetchdat);
        if (r == 0)
                effacer_b();
        return r;
    };

    internal static void effacer_b()
    {
        if ((cpu_state.npxs & B) == 0)
                return;
        cpu_state.npxs &= unchecked((uint16_t)~B);
        ModeMateriel.Sonde[59]++;
    }

    // pcem bug, fixed in hardware mode: PB-69 — FCLEX et FINIT du 8087 effacent IR : la sortie INT retombe, et une NMI
    //   que le port A0h retenait ne viendra plus. Que FLDCW, FENI ou FDISI changent IEM sous une exception en attente
    //   n'est pas modélisé.
    internal static void int_8087_retombe()
    {
        if (_808x.nmi == 0)
                return;
        _808x.nmi = 0;
        ModeMateriel.Sonde[69]++;
    }

    /// <summary>Enveloppe, dans les seize tables du mode, chaque instruction qui attend : toutes, sauf les sept que le
    /// processeur lance sans tester BUSY# ni ERROR#.</summary>
    private static void attente()
    {
        _386.x86_opcodes_d8_a16 = attente(_386.x86_opcodes_d8_a16!, _386.ops_fpu_d8_a16, 0xD8);
        _386.x86_opcodes_d8_a32 = attente(_386.x86_opcodes_d8_a32!, _386.ops_fpu_d8_a32, 0xD8);
        _386.x86_opcodes_d9_a16 = attente(_386.x86_opcodes_d9_a16!, _386.ops_fpu_d9_a16, 0xD9);
        _386.x86_opcodes_d9_a32 = attente(_386.x86_opcodes_d9_a32!, _386.ops_fpu_d9_a32, 0xD9);
        _386.x86_opcodes_da_a16 = attente(_386.x86_opcodes_da_a16!, _386.ops_fpu_da_a16, 0xDA);
        _386.x86_opcodes_da_a32 = attente(_386.x86_opcodes_da_a32!, _386.ops_fpu_da_a32, 0xDA);
        _386.x86_opcodes_db_a16 = attente(_386.x86_opcodes_db_a16!, _386.ops_fpu_db_a16, 0xDB);
        _386.x86_opcodes_db_a32 = attente(_386.x86_opcodes_db_a32!, _386.ops_fpu_db_a32, 0xDB);
        _386.x86_opcodes_dc_a16 = attente(_386.x86_opcodes_dc_a16!, _386.ops_fpu_dc_a16, 0xDC);
        _386.x86_opcodes_dc_a32 = attente(_386.x86_opcodes_dc_a32!, _386.ops_fpu_dc_a32, 0xDC);
        _386.x86_opcodes_dd_a16 = attente(_386.x86_opcodes_dd_a16!, _386.ops_fpu_dd_a16, 0xDD);
        _386.x86_opcodes_dd_a32 = attente(_386.x86_opcodes_dd_a32!, _386.ops_fpu_dd_a32, 0xDD);
        _386.x86_opcodes_de_a16 = attente(_386.x86_opcodes_de_a16!, _386.ops_fpu_de_a16, 0xDE);
        _386.x86_opcodes_de_a32 = attente(_386.x86_opcodes_de_a32!, _386.ops_fpu_de_a32, 0xDE);
        _386.x86_opcodes_df_a16 = attente(_386.x86_opcodes_df_a16!, _386.ops_fpu_df_a16, 0xDF);
        _386.x86_opcodes_df_a32 = attente(_386.x86_opcodes_df_a32!, _386.ops_fpu_df_a32, 0xDF);
    }

    private static OpFn[] attente(OpFn[] posee, OpFn[] pcem, int esc)
    {
        var t = copie(posee, pcem);
        for (var i = 0; i < t.Length; i++)
                if (t[i] is { } h && !sans_attente(esc, i))
                        t[i] = attend(h);
        return t;
    }

    // pcem bug, fixed in hardware mode: PB-204 — les instructions qui n'attendent pas : FNCLEX, FNINIT, FSETPM, FNSTCW,
    //   FNSTSW, FNSAVE et FNSTENV, que le 286 lance sans tester ERROR# (286 PRM, #MF), que le verrou de l'AT laisse
    //   passer (FINIT, FSETPM et FCLEX : 387 PRM, annexe F ; FNSTSW, FNSTSW AX, FNSTENV et FNSAVE : 287 PRM p. 1-10 ;
    //   FNSTCW : déduit), et les formes « no-wait » du 486 (486 PRM, table 17-8). L'indice est l'octet ModRM (D9, DB,
    //   DD, DF) ; D8 et DC, indexées par mod et reg, n'ont que des instructions qui attendent.
    private static bool sans_attente(int esc, int i) => esc switch
    {
        0xD9 or 0xDD => i < 0xC0 && ((i >> 3) & 7) is 6 or 7,
        0xDB => i is 0xE2 or 0xE3 or 0xE4,
        0xDF => i == 0xE0,
        _ => false,
    };

    // FP_ENTER d'abord : #NM (EM ou TS) précède l'attente, et le gestionnaire de PCem le lève.
    private static OpFn attend(OpFn pcem) => fetchdat => (cr0 & 0xc) == 0 && bloque() ? 1 : pcem(fetchdat);

    // pcem bug, fixed in hardware mode: PB-204 — ni WAIT ni une instruction x87 n'attendent rien en mode PCem. Sur le
    //   286 et le 386 de l'AT, le verrou tient BUSY# : le processeur s'arrête devant l'instruction, une interruption
    //   peut le prendre, et il la reprend (286 HRM, « Execution of ESC Instructions »). Sur le 486, une exception en
    //   attente lève #MF si NE est posé ; sinon le processeur s'arrête, sauf sous IGNNE# (486 PRM § 16.2.1.2). L'arrêt
    //   compte cent cycles par tour, comme HLT : le pas auquel une interruption peut le prendre (déduit).
    internal static bool bloque()
    {
        if (cpu_c.fpu_type == cpu_c.FPU_BUILTIN)
        {
                if ((cpu_state.npxs & ES) == 0)
                        return false;
                if ((cr0 & 0x20) != 0)
                {
                        ModeMateriel.Sonde[59]++;
                        x86_int(16);
                        return true;
                }
                if (ignne)
                        return false;
        }
        else if (!verrou)
                return false;
        ModeMateriel.Sonde[204]++;
        cpu_state.pc = cpu_state.oldpc;
        _386.CLOCK_CYCLES(100);
        return true;
    }

    // pcem bug, fixed in hardware mode: PB-204 — les ports F0h et F1h de l'AT (AP-578 § 2.2.1 ; IBM PC AT Technical
    //   Reference, 1985) : une écriture à F0h efface le verrou (le 486 : pose IGNNE# tant que FERR# est actif) ; à F1h,
    //   elle efface aussi le verrou et remet à zéro le 287 ou le 387, dans l'état de FNINIT (opFINIT), ou, avec le
    //   noyau, dans l'état du RESET de son type (PB-203 : le 387 en sort IE, ES et B posés). Le 486 et F1h : inconnu,
    //   rien.
    internal static void ports_at() => io.io_sethandler(0x00f0, 0x0002, null, null, null, ecrire_f0_f1, null, null, null);

    internal static void ecrire_f0_f1(uint16_t port, uint8_t val, object p)
    {
        ModeMateriel.Sonde[204]++;
        verrou = false;
        if (cpu_c.fpu_type == cpu_c.FPU_BUILTIN)
        {
                if (port == 0xF0)
                        ignne = (cpu_state.npxs & ES) != 0;
                return;
        }
        if (port == 0xF1 && cpu_c.fpu_type is cpu_c.FPU_287 or cpu_c.FPU_287XL or cpu_c.FPU_387)
        {
                // Le noyau : le RESET de son type (PB-203) ; le 387 en sort ES posé, sans front acheminé.
                if (materiel.pb_264)
                {
                        _x87_noyau.reset();
                        return;
                }
                cpu_state.npxc = 0x37f;
                cpu_state.npxs = 0;
                Array.Clear(cpu_state.tag);
                cpu_state.TOP = 0;
                cpu_state.ismmx = 0;
        }
    }
}
