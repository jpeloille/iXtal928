// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// materiel-cas PB-nn [--attendu pcem|materiel] — le cas qui discrimine une correction du mode matériel (G13 ;
// PLAN-G13.md, § La vérification), en C# seul. Il rend la valeur de PCem en mode PCem et celle du vrai matériel en mode
// matériel, la source à côté de l'attendu, et il dit par la sonde qu'il est passé par la correction : une correction
// qu'aucun cas n'atteint ne prouve rien. Sans --attendu, l'attendu suit le mode figé de la correction. --attendu
// materiel en mode PCem est la panne injectée (la correction coupée par le masque) : le cas doit rougir.
//
// Les cas du processeur jouent quelques instructions sur le cœur du harnais (_808x.Reset, la RAM plate de 1 Mo), le code
// en 0000:0100 sur un fond de NOP ; ceux de PB-03 et de PB-257 montent une machine, le 5150. Ceux du 8259 (PB-05,
// PB-246 à PB-248, PB-255) écrivent et lisent ses ports et l'acquittent comme le ferait le processeur, après
// l'initialisation du BIOS de l'AT (maître en 08h, esclave en 70h sur IR2) ou de l'XT (un seul 8259, en 08h). Ceux du
// 8237 (PB-157, PB-249 à PB-253) écrivent ses ports et tirent un transfert comme le ferait un périphérique
// (dma_channel_write), après un reset du DMA. Ceux du 8042 et de la souris PS/2 (PB-254, PB-94, PB-95) appellent le
// contrôleur, la souris et le poll comme le feraient le processeur et le chronomètre ; celui de PB-101 monte les ports
// du PC1512 ; celui de PB-103 pose l'état de la manette et lit la CH et la TM. Ceux du mode protégé (PB-32, PB-39,
// PB-40, PB-190 à PB-193, PB-263) jouent les cas de pm-check qui les discriminent, au 386 et au 286, en C# seul
// (PmCheck386.CasMateriel, PmCheck.CasMateriel). Ceux du stockage (PB-14, PB-19, PB-22, PB-23, PB-25, PB-217) sont dans
// MaterielCasStockage.cs.

using System.Reflection;
using iXtal26.Cpu;
using iXtal26.Diag;
using iXtal26.Joystick;
using iXtal26.Keyboard;
using iXtal26.Memory;
using iXtal26.Models;
using iXtal26.Mouse;
using iXtal26.SoftFloat;

namespace iXtal26.Diff;

internal static partial class MaterielCas
{
    private static readonly SortedDictionary<int, Func<bool, int>> Cas = new()
    {
        [1] = Pb01, [2] = Pb02, [3] = Pb03, [5] = Pb05, [7] = Pb07, [45] = Pb45, [87] = Pb87, [169] = Pb169, [170] = Pb170,
        [171] = Pb171, [172] = Pb172, [173] = Pb173, [174] = Pb174, [175] = Pb175, [176] = Pb176, [177] = Pb177,
        [179] = Pb179, [157] = Pb157, [246] = Pb246, [247] = Pb247, [248] = Pb248, [249] = Pb249, [250] = Pb250,
        [251] = Pb251, [252] = Pb252, [253] = Pb253, [255] = Pb255, [257] = Pb257, [258] = Pb258, [94] = Pb94,
        [95] = Pb95, [101] = Pb101, [103] = Pb103, [254] = Pb254, [261] = Pb261, [181] = Pb181, [182] = Pb182,
        [183] = Pb183, [184] = Pb184, [185] = Pb185, [186] = Pb186, [187] = Pb187, [188] = Pb188, [262] = Pb262,
        [43] = Pb43, [50] = Pb50, [51] = Pb51, [78] = Pb78, [189] = Pb189, [61] = Pb61, [57] = Pb57, [64] = Pb64, [70] = Pb70, [63] = Pb63, [66] = Pb66, [67] = Pb67, [58] = Pb58, [213] = Pb213, [207] = Pb207, [59] = Pb59, [69] = Pb69, [204] = Pb204, [203] = Pb203, [264] = Pb264, [52] = Pb52, [55] = Pb55, [56] = Pb56, [195] = Pb195,
        [196] = Pb196, [48] = Pb48, [60] = Pb60, [65] = Pb65, [194] = Pb194, [197] = Pb197, [198] = Pb198, [68] = Pb68, [200] = Pb200, [201] = Pb201, [202] = Pb202, [211] = Pb211, [265] = Pb265, [206] = Pb206, [212] = Pb212, [54] = Pb54, [62] = Pb62, [199] = Pb199, [208] = Pb208, [32] = Pm(32), [39] = Pm(39), [40] = Pm(40),
        [190] = Pm(190), [191] = Pm(191), [192] = Pm(192), [193] = Pm(193), [263] = Pm(263),
        [14] = Pb14, [19] = Pb19, [22] = Pb22, [23] = Pb23, [25] = Pb25, [217] = Pb217,
    };

    /// <summary>G13.5c — les corrections du mode protégé : les cas de pm-check --core 386 et du banc du 286 qui les
    /// discriminent, en C# seul (PmCheck386.CasMateriel, PmCheck.CasMateriel), chacun avec l'attente de PCem et celle
    /// du mode matériel.</summary>
    private static Func<bool, int> Pm(int pb) => hw =>
    {
        var bad386 = PmCheck386.CasMateriel(pb, hw);
        var bad286 = PmCheck.CasMateriel(pb, hw);
        return bad386 < 0 && bad286 < 0 ? 1 : Math.Max(bad386, 0) + Math.Max(bad286, 0);
    };

    internal static int Run(string[] args)
    {
        var pb = args.FirstOrDefault(a => a.StartsWith("PB-", StringComparison.OrdinalIgnoreCase));
        var i = Array.IndexOf(args, "--attendu");
        var attendu = i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        if (attendu is not (null or "pcem" or "materiel"))
        {
            Console.Error.WriteLine("materiel-cas : --attendu pcem ou --attendu materiel.");
            return 2;
        }
        if (pb is null || !int.TryParse(pb[3..], out var n) || !Cas.TryGetValue(n, out var cas))
        {
            Console.Error.WriteLine($"materiel-cas PB-nn : une correction du mode matériel " +
                                    $"({string.Join(", ", Cas.Keys.Select(k => $"PB-{k:00}"))}).");
            return 2;
        }
        var corrige = ModeMateriel.Demande(n);
        var materielAttendu = (attendu ?? (corrige ? "materiel" : "pcem")) == "materiel";
        var sonde = ModeMateriel.Sonde[n];
        var bad = cas(materielAttendu);
        var passages = ModeMateriel.Sonde[n] - sonde;
        Console.WriteLine($"  la sonde : PB-{n:00} a servi {passages} fois");
        if (corrige && passages == 0)
        {
            bad++;
            Console.WriteLine("  la correction n'a pas servi : le cas ne prouve rien");
        }
        Console.WriteLine(bad == 0
            ? $"\nVert : PB-{n:00}, chaque cas rend la valeur {(materielAttendu ? "du matériel (mode matériel)" : "de PCem (mode PCem)")}."
            : $"\n{bad} échec(s) : PB-{n:00} ne rend pas la valeur {(materielAttendu ? "du matériel" : "de PCem")}.");
        return bad == 0 ? 0 : 1;
    }

    // ===== Le cœur du harnais =====

    /// <summary>Joue `pas` instructions : le code en 0000:0100 (ou à `lin`, avec CS:IP), sur un fond de NOP, SP = FFFEh,
    /// les drapeaux F002h ; `regs` et `ram` posent le reste. Rend les registres d'après.</summary>
    private static ushort[] Jouer(byte[] code, Action<ushort[]> regs, Action? ram = null, int pas = 1,
                                  bool cpu8086 = false, uint lin = 0x100, ushort cs = 0, ushort ip = 0x100,
                                  Func<ushort[], bool>? jusqua = null)
    {
        if (cpu8086)
            _808x.Reset8086();
        else
            _808x.Reset();
        mem.fill_ram(0x90);
        for (var k = 0; k < code.Length; k++)
            mem.ram[lin + k] = code[k];
        ram?.Invoke();
        var r = new ushort[(int)R.COUNT];
        r[(int)R.SP] = 0xFFFE;
        r[(int)R.CS] = cs;
        r[(int)R.IP] = ip;
        r[(int)R.FLAGS] = 0xF002;
        regs(r);
        _808x.SetRegs(r);
        for (var k = 0; k < pas; k++)
        {
            _808x.Step();
            if (jusqua is not null)
            {
                _808x.GetRegs(r);
                if (jusqua(r))
                    break;
            }
        }
        _808x.GetRegs(r);
        return r;
    }

    private static int Voir(string nom, bool ok, string vu, string attendu, bool materielAttendu)
    {
        Console.WriteLine($"  [{(ok ? "ok" : "ECHEC")}] {nom} : {vu} (attendu {(materielAttendu ? "du matériel" : "de PCem")} : " +
                          $"{attendu})");
        return ok ? 0 : 1;
    }

    private static int Drapeau(ushort[] r, ushort bit) => (r[(int)R.FLAGS] & bit) != 0 ? 1 : 0;

    /// <summary>Un cas sur un registre de 16 bits et un drapeau : la valeur et le drapeau, de PCem ou du matériel.</summary>
    private static int Reg(string nom, ushort[] r, R reg, ushort drapeau, string nomDrapeau, (int v, int f) pcem,
                           (int v, int f) materiel, bool hw)
    {
        var (v, f) = hw ? materiel : pcem;
        var vu = (r[(int)reg], Drapeau(r, drapeau));
        return Voir(nom, vu == (v, f), $"{reg} = {vu.Item1:X4}, {nomDrapeau} = {vu.Item2}", $"{reg} = {v:X4}, {nomDrapeau} = {f}", hw);
    }

    // ===== Les cas =====

    // PB-01 — l'AF d'ADC et de SBB du 8088. AF est la retenue (l'emprunt) du bit 3, retenue entrante comprise (SDM
    // vol. 1, § 3.4.3.1 ; mesuré par SST 8088 v2, formes 10 à 15 et 18 à 1D) ; PCem l'oublie quand les quartets bas
    // ne font la retenue qu'avec elle. Un cas par fonction de 808x.cs : setadc8, setadc16, setsbc8, setsbc16.
    private static int Pb01(bool hw)
    {
        var bad = 0;
        // 09h + 06h + 1 = 10h : PCem AF = 0, et DAA rend 10h ; le 8088 AF = 1, et DAA rend 16h.
        bad += Pb01Cas("ADC AL,06h puis DAA, CF = 1, AL = 09h", [0x14, 0x06, 0x27], 0x0009, 2,
                       hw ? (true, 0x0016) : (false, 0x0010), hw);
        bad += Pb01Cas("ADC AX,0006h, CF = 1, AX = 0009h", [0x15, 0x06, 0x00], 0x0009, 1,
                       hw ? (true, 0x0010) : (false, 0x0010), hw);
        // 15h - 05h - 1 = 0Fh : l'emprunt du bit 3 ne vient que de la retenue entrante.
        bad += Pb01Cas("SBB AL,05h, CF = 1, AL = 15h", [0x1C, 0x05], 0x0015, 1,
                       hw ? (true, 0x000F) : (false, 0x000F), hw);
        bad += Pb01Cas("SBB AX,0005h, CF = 1, AX = 0015h", [0x1D, 0x05, 0x00], 0x0015, 1,
                       hw ? (true, 0x000F) : (false, 0x000F), hw);
        return bad;
    }

    // AF lu après la première instruction, AX après la dernière.
    private static int Pb01Cas(string nom, byte[] code, ushort ax, int pas, (bool af, int ax) attendu, bool hw)
    {
        var r = Jouer(code, r => { r[(int)R.AX] = ax; r[(int)R.FLAGS] = 0xF003; });
        var af = Drapeau(r, 0x10) != 0;
        r = Jouer(code, r => { r[(int)R.AX] = ax; r[(int)R.FLAGS] = 0xF003; }, pas: pas);
        return Voir(nom, af == attendu.af && r[(int)R.AX] == attendu.ax, $"AF = {(af ? 1 : 0)}, AX = {r[(int)R.AX]:X4}",
                    $"AF = {(attendu.af ? 1 : 0)}, AX = {attendu.ax:X4}", hw);
    }

    // PB-02 — RCL et RCR mot par CL : CF reçoit le dernier bit sorti (386 PRM, page RCL/RCR/ROL/ROR) ; PCem le remplace
    // par le bit entré à la dernière itération.
    private static int Pb02(bool hw)
    {
        var bad = 0;
        var r = Jouer([0xD3, 0xD0], r => { r[(int)R.AX] = 0x8000; r[(int)R.CX] = 1; });
        bad += Reg("RCL AX,CL, AX = 8000h, CL = 1, CF = 0", r, R.AX, 0x0001, "CF", (0x0000, 0), (0x0000, 1), hw);
        r = Jouer([0xD3, 0xD8], r => { r[(int)R.AX] = 0x0001; r[(int)R.CX] = 1; });
        bad += Reg("RCR AX,CL, AX = 0001h, CL = 1, CF = 0", r, R.AX, 0x0001, "CF", (0x0000, 0), (0x0000, 1), hw);
        return bad;
    }

    // PB-03 — les cycles du rafraîchissement par DMA (refreshread → FETCHCOMPLETE, appelés par timer_process) arrivent au
    // TSC : un cycle volé s'écoule aussi pour le PIT (IBM PC TR 6025008, p. 2-3 et 2-22). Le contrôle de fréquence du
    // 5150, cinq secondes à l'invite de BASIC : PCem perd des cycles (le rapport 4 de --timer-check) ; le matériel aucun,
    // à l'arrondi de la conversion en cycles près.
    private static int Pb03(bool hw)
    {
        TimerCheck.Manque = long.MinValue;
        if (TimerCheck.Run("roms", 5, TimerCheck.DefaultBootSlices) != 0)
            return Voir("--timer-check, 5150", false, "le contrôle a échoué", "un contrôle mené", hw);
        var m = TimerCheck.Manque;
        return Voir("--timer-check, 5150, cinq secondes à l'invite de BASIC", hw ? Math.Abs(m) <= 1 : m > 100,
                    $"{m} cycles consommés jamais portés au TSC", hw ? "aucun, à un près" : "plus de cent", hw);
    }

    // PB-07 — un mot à cheval sur une fin de page lit son octet haut par la page suivante : au sommet de la RAM, au
    // repli de 1 Mo du 8088 (IBM PC TR 6025008 p. 2-3 ; 386 PRM § 14.7, point 18). Sur la RAM plate du harnais, la page
    // FFh dans le cache, un mot en FFFFFh : PCem lit la marge de mem_alloc (nulle), le 8088 l'octet en 00000h.
    private static int Pb07(bool hw)
    {
        var bad = 0;
        var r = Jouer([0x59], r => { r[(int)R.SS] = 0xFFFF; r[(int)R.SP] = 0x000F; },
                      () => { mem.ram[0xFFFFF] = 0x77; mem.ram[0] = 0x5A; Amorcer(); });
        bad += Voir("POP CX, SS:SP = FFFF:000F", r[(int)R.CX] == (hw ? 0x5A77 : 0x0077), $"CX = {r[(int)R.CX]:X4}",
                    $"CX = {(hw ? 0x5A77 : 0x0077):X4}", hw);
        Jouer([0xA3, 0x0F, 0x00], r => { r[(int)R.DS] = 0xFFFF; r[(int)R.AX] = 0x1234; },
              () => { mem.ram[0] = 0x90; Amorcer(); });
        bad += Voir("MOV [000Fh],AX, DS = FFFFh, AX = 1234h", mem.ram[0xFFFFF] == 0x34 && mem.ram[0] == (hw ? 0x12 : 0x90),
                    $"FFFFFh = {mem.ram[0xFFFFF]:X2}, 00000h = {mem.ram[0]:X2}", $"34h et {(hw ? 0x12 : 0x90):X2}h", hw);
        return bad;
    }

    /// <summary>Le cache de pages de la page FFh rempli, comme après tout accès d'octet : sans lui, le mot passe par le
    /// chemin lent (readmemwl), qui découpe déjà un mot à cheval sur deux pages.</summary>
    private static void Amorcer()
    {
        mem.readmembl(0xFFFF0);
        mem.writemembl(0xFFFF0, 0x90);
    }

    // PB-45 — le dividende d'IDIV octet est AX, signé (386 PRM, page IDIV) : AX = FFF7h (-9), BL = 2, le quotient -4 et
    // le reste -1. PCem divise 65 527 : AL = FBh, AH = 01h.
    private static int Pb45(bool hw)
    {
        var r = Jouer([0xF6, 0xFB], r => { r[(int)R.AX] = 0xFFF7; r[(int)R.BX] = 0x0002; });
        return Voir("IDIV BL, AX = FFF7h, BL = 02h", r[(int)R.AX] == (hw ? 0xFFFC : 0x01FB), $"AX = {r[(int)R.AX]:X4}",
                    $"AX = {(hw ? 0xFFFC : 0x01FB):X4}", hw);
    }

    // PB-87 — au repli de l'IP, la lecture d'instruction se fait à l'offset 0 du segment (386 PRM § 14.7, point 8) :
    // MOV AL,imm8 en 1000:FFFF, son immédiat en 1000:0000 (11h) ; PCem le lit 64 Ko plus loin (22h). Le 8088, et le
    // 8086, dont la seconde lecture (808x.c:150) tombe aussi au-delà.
    private static int Pb87(bool hw)
    {
        var bad = 0;
        foreach (var cpu8086 in new[] { false, true })
        {
            var r = Jouer([], r => { r[(int)R.CS] = 0x1000; r[(int)R.IP] = 0xFFFF; },
                          () => { mem.ram[0x1FFFF] = 0xB0; mem.ram[0x10000] = 0x11; mem.ram[0x20000] = 0x22; },
                          cpu8086: cpu8086);
            bad += Voir($"MOV AL,imm8 en 1000:FFFF, le {(cpu8086 ? "8086" : "8088")}",
                        (r[(int)R.AX] & 0xFF) == (hw ? 0x11 : 0x22) && r[(int)R.IP] == 0x0001,
                        $"AL = {r[(int)R.AX] & 0xFF:X2}, IP = {r[(int)R.IP]:X4}", $"AL = {(hw ? 0x11 : 0x22):X2}, IP = 0001", hw);
        }
        return bad;
    }

    // PB-169 — un quotient hors capacité lève l'interruption 0 (386 PRM, pages DIV et IDIV), 80h (8000h) compris sur le
    // 8086 et le 8088 (§ 14.7, point 11), l'adresse de l'instruction suivante empilée (point 2). Le vecteur 0 en
    // 0000:0400. PCem tronque le quotient.
    private static int Pb169(bool hw)
    {
        var bad = 0;
        bad += Div("DIV BL, AX = 1000h, BL = 02h", [0xF6, 0xF3], r => { r[(int)R.AX] = 0x1000; r[(int)R.BX] = 2; },
                   r => r[(int)R.AX] == 0x0000, "AX = 0000", hw);
        bad += Div("IDIV BL, AX = FF00h, BL = 02h (quotient 80h)", [0xF6, 0xFB],
                   r => { r[(int)R.AX] = 0xFF00; r[(int)R.BX] = 2; }, r => r[(int)R.AX] == 0x0080, "AX = 0080", hw);
        bad += Div("DIV BX, DX:AX = 0002:0000, BX = 0002h", [0xF7, 0xF3],
                   r => { r[(int)R.DX] = 2; r[(int)R.BX] = 2; }, r => r[(int)R.AX] == 0 && r[(int)R.DX] == 0,
                   "DX:AX = 0000:0000", hw);
        bad += Div("IDIV BX, DX:AX = FFFF:8000, BX = 0001h (quotient 8000h)", [0xF7, 0xFB],
                   r => { r[(int)R.DX] = 0xFFFF; r[(int)R.AX] = 0x8000; r[(int)R.BX] = 1; },
                   r => r[(int)R.AX] == 0x8000 && r[(int)R.DX] == 0, "DX:AX = 0000:8000", hw);
        return bad;
    }

    private static int Div(string nom, byte[] code, Action<ushort[]> regs, Func<ushort[], bool> pcem, string vuPcem, bool hw)
    {
        var r = Jouer(code, regs, () => { mem.ram[0] = 0x00; mem.ram[1] = 0x04; mem.ram[2] = 0; mem.ram[3] = 0; });
        var empile = mem.ram[0xFFF8] | (mem.ram[0xFFF9] << 8);
        var vu = $"CS:IP = {r[(int)R.CS]:X4}:{r[(int)R.IP]:X4}, SP = {r[(int)R.SP]:X4}, AX = {r[(int)R.AX]:X4}, " +
                 $"DX = {r[(int)R.DX]:X4}, IP empilé = {empile:X4}";
        var ok = hw
            ? r[(int)R.CS] == 0 && r[(int)R.IP] == 0x0400 && r[(int)R.SP] == 0xFFF8 && empile == 0x0102
            : r[(int)R.IP] == 0x0102 && r[(int)R.SP] == 0xFFFE && pcem(r);
        return Voir(nom, ok, vu, hw ? "INT 0 : CS:IP = 0000:0400, SP = FFF8, IP empilé = 0102" : $"IP = 0102, {vuPcem}", hw);
    }

    // PB-170 — DAA du 8088 mesuré (SST 8088 v2 et 8086, forme 27) : AL = 9Eh, AF = 1, CF = 0 ; le seuil est 9Fh quand AF
    // valait 1 : le 8088 n'ajoute que 6 (A4h, CF = 0) ; PCem, au pseudo-code d'Intel, ajoute 66h (04h, CF = 1).
    private static int Pb170(bool hw)
    {
        var r = Jouer([0x27], r => { r[(int)R.AX] = 0x009E; r[(int)R.FLAGS] = 0xF012; });
        return Reg("DAA, AL = 9Eh, AF = 1, CF = 0", r, R.AX, 0x0001, "CF", (0x0004, 1), (0x00A4, 0), hw);
    }

    // PB-171 — DAS teste l'AL et le CF d'origine (SDM vol. 2, page DAS) : AL = 01h, AF = 1, CF = 0 rend FBh, CF = 0 ;
    // PCem reprend l'emprunt du premier pas et rend 9Bh, CF = 1.
    private static int Pb171(bool hw)
    {
        var r = Jouer([0x2F], r => { r[(int)R.AX] = 0x0001; r[(int)R.FLAGS] = 0xF012; });
        return Reg("DAS, AL = 01h, AF = 1, CF = 0", r, R.AX, 0x0001, "CF", (0x009B, 1), (0x00FB, 0), hw);
    }

    // PB-172 — LODS charge AL (AX) à chaque répétition (Intel, page LODS) ; PCem garde l'octet lu pour lui.
    private static int Pb172(bool hw)
    {
        var bad = 0;
        var r = Jouer([0xF3, 0xAC], r => { r[(int)R.CX] = 1; r[(int)R.SI] = 0x200; }, () => mem.ram[0x200] = 0x5A);
        bad += Voir("REP LODSB, CX = 1, DS:SI sur 5Ah", r[(int)R.AX] == (hw ? 0x005A : 0) && r[(int)R.CX] == 0,
                    $"AX = {r[(int)R.AX]:X4}, CX = {r[(int)R.CX]:X4}", $"AX = {(hw ? 0x5A : 0):X4}, CX = 0000", hw);
        r = Jouer([0xF3, 0xAD], r => { r[(int)R.CX] = 1; r[(int)R.SI] = 0x200; },
                  () => { mem.ram[0x200] = 0x34; mem.ram[0x201] = 0x12; });
        bad += Voir("REP LODSW, CX = 1, DS:SI sur 1234h", r[(int)R.AX] == (hw ? 0x1234 : 0) && r[(int)R.CX] == 0,
                    $"AX = {r[(int)R.AX]:X4}, CX = {r[(int)R.CX]:X4}", $"AX = {(hw ? 0x1234 : 0):X4}, CX = 0000", hw);
        return bad;
    }

    // PB-173 — SETMO et SETMOC (D0 à D3 /6) mettent l'opérande à FFh (FFFFh), SETMOC si CL ≠ 0 ; les drapeaux que le
    // 8088 laisse, SF et PF (SST 8088 v2, D0.6 à D3.6, mesuré). PCem les exécute comme SHL.
    private static int Pb173(bool hw)
    {
        var bad = 0;
        var r = Jouer([0xD0, 0xF0], r => r[(int)R.AX] = 0x002A);
        bad += Voir("SETMO AL (D0 F0), AL = 2Ah", r[(int)R.AX] == (hw ? 0x00FF : 0x0054) && (!hw || (r[(int)R.FLAGS] & 0x8D5) == 0x84),
                    $"AX = {r[(int)R.AX]:X4}, drapeaux & 8D5h = {r[(int)R.FLAGS] & 0x8D5:X3}",
                    hw ? "AX = 00FF, 084" : "AX = 0054", hw);
        r = Jouer([0xD1, 0xF0], r => r[(int)R.AX] = 0x1234);
        bad += Voir("SETMO AX (D1 F0), AX = 1234h", r[(int)R.AX] == (hw ? 0xFFFF : 0x2468), $"AX = {r[(int)R.AX]:X4}",
                    $"AX = {(hw ? 0xFFFF : 0x2468):X4}", hw);
        r = Jouer([0xD2, 0xF0], r => { r[(int)R.AX] = 0x002A; r[(int)R.CX] = 3; });
        bad += Voir("SETMOC AL (D2 F0), AL = 2Ah, CL = 3", r[(int)R.AX] == (hw ? 0x00FF : 0x0050), $"AX = {r[(int)R.AX]:X4}",
                    $"AX = {(hw ? 0xFF : 0x50):X4}", hw);
        r = Jouer([0xD3, 0xF0], r => { r[(int)R.AX] = 0x1234; r[(int)R.CX] = 0; });
        bad += Voir("SETMOC AX (D3 F0), CL = 0 : l'opérande reste", r[(int)R.AX] == 0x1234, $"AX = {r[(int)R.AX]:X4}",
                    "AX = 1234", hw);
        return bad;
    }

    // PB-174 — les décalages par CL posent OF, celui du dernier pas d'un bit (mesuré, SST 8088 v2, D2.4, D2.5, D2.7) ;
    // PCem garde l'OF d'avant.
    private static int Pb174(bool hw)
    {
        var bad = 0;
        var r = Jouer([0xD2, 0xE0], r => { r[(int)R.AX] = 0x0001; r[(int)R.CX] = 2; r[(int)R.FLAGS] = 0xF802; });
        bad += Reg("SHL AL,CL, AL = 01h, CL = 2, OF = 1", r, R.AX, 0x0800, "OF", (0x0004, 1), (0x0004, 0), hw);
        r = Jouer([0xD2, 0xE8], r => { r[(int)R.AX] = 0x0080; r[(int)R.CX] = 1; });
        bad += Reg("SHR AL,CL, AL = 80h, CL = 1, OF = 0", r, R.AX, 0x0800, "OF", (0x0040, 0), (0x0040, 1), hw);
        r = Jouer([0xD3, 0xF8], r => { r[(int)R.AX] = 0x8000; r[(int)R.CX] = 3; r[(int)R.FLAGS] = 0xF802; });
        bad += Reg("SAR AX,CL, AX = 8000h, CL = 3, OF = 1", r, R.AX, 0x0800, "OF", (0xF000, 1), (0xF000, 0), hw);
        return bad;
    }

    // PB-175 — AAM et AAD posent SF, ZF et PF d'après AL (SDM vol. 2, pages AAD et AAM) ; PCem d'après AX.
    private static int Pb175(bool hw)
    {
        var bad = 0;
        var r = Jouer([0xD4, 0x0A], r => r[(int)R.AX] = 0x0014);
        bad += Reg("AAM 0Ah, AL = 14h", r, R.AX, 0x0040, "ZF", (0x0200, 0), (0x0200, 1), hw);
        r = Jouer([0xD5, 0x0A], r => r[(int)R.AX] = 0x0178);
        bad += Reg("AAD 0Ah, AH = 01h, AL = 78h", r, R.AX, 0x0080, "SF", (0x0082, 0), (0x0082, 1), hw);
        return bad;
    }

    // PB-176 — SAR par CL : le dernier bit sorti est une copie du signe dès que le compte atteint la largeur (386 PRM,
    // page SAL/SAR/SHL/SHR ; le 8088 ne masque pas le compte, § 14.7, point 5) ; PCem étend par des zéros.
    private static int Pb176(bool hw)
    {
        var bad = 0;
        var r = Jouer([0xD2, 0xF8], r => { r[(int)R.AX] = 0x0080; r[(int)R.CX] = 9; });
        bad += Reg("SAR AL,CL, AL = 80h, CL = 9", r, R.AX, 0x0001, "CF", (0x00FF, 0), (0x00FF, 1), hw);
        r = Jouer([0xD3, 0xF8], r => { r[(int)R.AX] = 0x8000; r[(int)R.CX] = 17; });
        bad += Reg("SAR AX,CL, AX = 8000h, CL = 17", r, R.AX, 0x0001, "CF", (0xFFFF, 0), (0xFFFF, 1), hw);
        return bad;
    }

    // PB-177 — rep() : 6Eh est l'alias de JLE (OUTS naît avec le 186), REP DS: répète la chaîne, un préfixe placé avant
    // REP vaut pour l'instruction (déduit : un préfixe vaut pour l'instruction qui le suit ; 386 PRM § 14.7, point 3).
    private static int Pb177(bool hw)
    {
        var bad = 0;
        // F3 6E 10, ZF = 1 : le 8088 prend le saut (IP = 0113h), CX intact ; PCem fait un OUTSB et reprend en 0100h.
        var r = Jouer([0xF3, 0x6E, 0x10], r => { r[(int)R.CX] = 2; r[(int)R.DX] = 0x2F0; r[(int)R.FLAGS] = 0xF042; });
        bad += Voir("REP 6Eh (JLE), CX = 2, ZF = 1", hw ? r[(int)R.IP] == 0x0113 && r[(int)R.CX] == 2
                                                         : r[(int)R.IP] == 0x0100 && r[(int)R.CX] == 1,
                    $"IP = {r[(int)R.IP]:X4}, CX = {r[(int)R.CX]:X4}", hw ? "IP = 0113, CX = 0002" : "IP = 0100, CX = 0001", hw);
        // F3 3E A4, CX = 3 : le 8088 copie les trois octets ; PCem relance sur DS: MOVSB, qui en copie un seul plus tard.
        r = Jouer([0xF3, 0x3E, 0xA4], r => { r[(int)R.CX] = 3; r[(int)R.SI] = 0x200; r[(int)R.DI] = 0x300; },
                  () => { mem.ram[0x200] = 1; mem.ram[0x201] = 2; mem.ram[0x202] = 3; });
        bad += Voir("REP DS: MOVSB, CX = 3", hw ? r[(int)R.CX] == 0 && r[(int)R.IP] == 0x0103 && mem.ram[0x302] == 3
                                                : r[(int)R.CX] == 3 && r[(int)R.IP] == 0x0101 && mem.ram[0x300] == 0x90,
                    $"CX = {r[(int)R.CX]:X4}, IP = {r[(int)R.IP]:X4}, 0302h = {mem.ram[0x302]:X2}",
                    hw ? "CX = 0000, IP = 0103, 0302h = 03" : "CX = 0003, IP = 0101, rien de copié", hw);
        // 26 F3 A1 00 00 : MOV AX,[0000] sous ES:, le REP entre les deux. Le 8088 lit dans ES ; PCem perd ES: et lit DS.
        r = Jouer([0x26, 0xF3, 0xA1, 0x00, 0x00], r => { r[(int)R.ES] = 0x2000; r[(int)R.DS] = 0x3000; },
                  () => { mem.ram[0x20000] = 0x11; mem.ram[0x20001] = 0x11; mem.ram[0x30000] = 0x22; mem.ram[0x30001] = 0x22; },
                  pas: 4, jusqua: r => r[(int)R.IP] >= 0x105);
        bad += Voir("ES: REP MOV AX,[0000]", r[(int)R.AX] == (hw ? 0x1111 : 0x2222) && r[(int)R.IP] == 0x105,
                    $"AX = {r[(int)R.AX]:X4}, IP = {r[(int)R.IP]:X4}", $"AX = {(hw ? 0x1111 : 0x2222):X4}, IP = 0105", hw);
        // 26 F3 F6 FB : ES: REP IDIV BL, AX = 16, BL = 2. Le 8088 rend l'opposé du quotient (README SST 8088, mesuré) :
        // AL = F8h ; PCem, qui relance IDIV seul, 08h. L'inversion est dans la correction d'IDIV (PB-45) et ne vaut que
        // si PB-177 garde IDIV dans l'instruction de son REP : la porte materiel-cas-pb177-idiv demande les deux.
        var neg = hw && materiel.pb_45;
        r = Jouer([0x26, 0xF3, 0xF6, 0xFB], r => { r[(int)R.AX] = 0x0010; r[(int)R.BX] = 2; },
                  pas: 4, jusqua: r => r[(int)R.IP] >= 0x104);
        bad += Voir($"ES: REP IDIV BL, AX = 0010h, BL = 02h{(hw && !neg ? " (PB-45 non demandé)" : "")}",
                    r[(int)R.AX] == (neg ? 0x00F8 : 0x0008), $"AX = {r[(int)R.AX]:X4}",
                    $"AX = {(neg ? 0x00F8 : 0x0008):X4}", hw);
        return bad;
    }

    // PB-179 — un mot à l'offset FFFFh replie à l'offset 0 du segment (386 PRM § 14.7, point 7) : DS = 1000h, AAh en
    // 1FFFFh, 11h en 10000h, 22h en 20000h. PCem prend l'octet haut en 20000h.
    private static int Pb179(bool hw)
    {
        var bad = 0;
        var r = Jouer([0xA1, 0xFF, 0xFF], r => r[(int)R.DS] = 0x1000,
                      () => { mem.ram[0x1FFFF] = 0xAA; mem.ram[0x10000] = 0x11; mem.ram[0x20000] = 0x22; });
        bad += Voir("MOV AX,[FFFFh], DS = 1000h", r[(int)R.AX] == (hw ? 0x11AA : 0x22AA), $"AX = {r[(int)R.AX]:X4}",
                    $"AX = {(hw ? 0x11AA : 0x22AA):X4}", hw);
        Jouer([0xA3, 0xFF, 0xFF], r => { r[(int)R.DS] = 0x1000; r[(int)R.AX] = 0x1234; });
        bad += Voir("MOV [FFFFh],AX, DS = 1000h, AX = 1234h",
                    mem.ram[0x1FFFF] == 0x34 && (hw ? mem.ram[0x10000] == 0x12 && mem.ram[0x20000] == 0x90
                                                    : mem.ram[0x20000] == 0x12 && mem.ram[0x10000] == 0x90),
                    $"1FFFFh = {mem.ram[0x1FFFF]:X2}, 10000h = {mem.ram[0x10000]:X2}, 20000h = {mem.ram[0x20000]:X2}",
                    hw ? "34h, 12h, 90h" : "34h, 90h, 12h", hw);
        return bad;
    }

    // PB-258 — l'erreur de division empile dans SS, préfixe de segment ou non (Intel : l'interruption pousse les
    // drapeaux, CS et IP sur la pile, SS:SP) ; sous DS:, PCem empile dans DS. DS: DIV BL, BL = 0, DS = 3000h, SS = 0,
    // SP = FFFEh : l'IP empilé (0103h) en 0FFF8h sur le 8088, en 3FFF8h chez PCem.
    private static int Pb258(bool hw)
    {
        var r = Jouer([0x3E, 0xF6, 0xF3], r => { r[(int)R.AX] = 0x1000; r[(int)R.DS] = 0x3000; },
                      () => { mem.ram[0] = 0x00; mem.ram[1] = 0x04; mem.ram[2] = 0; mem.ram[3] = 0; });
        var pile = mem.ram[0x0FFF8] | (mem.ram[0x0FFF9] << 8);
        var ds = mem.ram[0x3FFF8] | (mem.ram[0x3FFF9] << 8);
        return Voir("DS: DIV BL, BL = 0, DS = 3000h, SS:SP = 0000:FFFE",
                    r[(int)R.IP] == 0x0400 && r[(int)R.SP] == 0xFFF8 && (hw ? pile == 0x0103 : ds == 0x0103),
                    $"IP = {r[(int)R.IP]:X4}, SP = {r[(int)R.SP]:X4}, 0FFF8h = {pile:X4}, 3FFF8h = {ds:X4}",
                    hw ? "INT 0, l'IP 0103 empilé en 0FFF8h" : "INT 0, l'IP 0103 empilé en 3FFF8h", hw);
    }

    // PB-257 — un canal masqué ne fait aucune requête au 8237A (fiche 8237A, p. 8) : pas de cycle de bus. Sur un PC (AT
    // nul), dma_channel_write du canal 2 masqué : PCem facture quatre cycles de bus (refreshread) ; le 8237A aucun. Un
    // transfert accepté coûte ses quatre cycles dans les deux modes.
    private static int Pb257(bool hw)
    {
        var memcycs = typeof(_808x).GetField("memcycs", BindingFlags.NonPublic | BindingFlags.Static)!;
        var bad = 0;
        _808x.Reset();
        dma.dma_reset();
        dma.dma_write(0x0A, 0x06, null!);                 // masque le canal 2
        memcycs.SetValue(null, 0);
        dma.dma_channel_write(2, 0x55);
        var refuse = (int)memcycs.GetValue(null)!;
        bad += Voir("canal 2 masqué, dma_channel_write", refuse == (hw ? 0 : 4), $"{refuse} cycles de bus facturés",
                    $"{(hw ? 0 : 4)}", hw);
        dma.dma_write(0x0B, 0x46, null!);                 // canal 2 : écriture en mémoire, mode simple
        dma.dma_write(0x0A, 0x02, null!);                 // démasque le canal 2
        memcycs.SetValue(null, 0);
        dma.dma_channel_write(2, 0x55);
        var accepte = (int)memcycs.GetValue(null)!;
        bad += Voir("canal 2 démasqué, en écriture, dma_channel_write", accepte == 4, $"{accepte} cycles de bus facturés",
                    "4", hw);
        return bad;
    }

    // ===== Le cœur 286/386/486 =====

    /// <summary>Joue une instruction (ou <paramref name="pas"/>) sur le cœur 386 (ou 286, 486) en mode réel : le code en
    /// 0000:0100 (ou <paramref name="ip"/>), sur un fond de NOP, SP = FFFEh, les drapeaux F002h, et les vecteurs 0 (#DE),
    /// 6 (#UD) et 0Dh (#GP) en 0000:0400, 0000:0600 et 0000:0700, pour qu'une exception se lise à l'IP d'après. Rend les
    /// registres de 16 bits d'après.</summary>
    /// <summary>Les cycles du dernier pas de Jouer386 (G13.6a : le temps d'une instruction x87).</summary>
    private static int DernierPas;

    private static ushort[] Jouer386(byte[] code, Action<ushort[]> regs, Action? ram = null, int coeur = 386,
                                     int ip = 0x100, int pas = 1)
    {
        if (coeur == 286)
            _386.Reset286();
        else if (coeur == 486)
            _386.Reset486();
        else
            _386.Reset386();
        mem.fill_ram(0x90);
        mem.ram[0x02] = 0x00; mem.ram[0x00] = 0x00; mem.ram[0x01] = 0x04; mem.ram[0x03] = 0x00;
        mem.ram[0x18] = 0x00; mem.ram[0x19] = 0x06; mem.ram[0x1A] = 0x00; mem.ram[0x1B] = 0x00;
        mem.ram[0x34] = 0x00; mem.ram[0x35] = 0x07; mem.ram[0x36] = 0x00; mem.ram[0x37] = 0x00;
        for (var k = 0; k < code.Length; k++)
            mem.ram[ip + k] = code[k];
        ram?.Invoke();
        var r = new ushort[(int)R.COUNT];
        r[(int)R.SP] = 0xFFFE;
        r[(int)R.IP] = (ushort)ip;
        r[(int)R.FLAGS] = 0xF002;
        regs(r);
        _808x.SetRegs(r);
        for (var k = 0; k < pas; k++)
            DernierPas = _386.Step286();
        _386.flags_rebuild();
        _808x.GetRegs(r);
        return r;
    }

    // PB-181 — l'AF d'ADC est la retenue du bit 3, retenue entrante comprise (SDM vol. 1, § 3.4.3.1) : CF = 1, AL = 00h,
    // ADC AL,0Fh rend 10h, une retenue sortie du bit 3 ; PCem ne la voit que si op2 vaut FFh.
    private static int Pb181(bool hw)
    {
        var r = Jouer386([0x14, 0x0F], r => r[(int)R.FLAGS] = 0xF003);
        return Reg("ADC AL,0Fh, CF = 1, AL = 00h (386)", r, R.AX, 0x0010, "AF", (0x0010, 0), (0x0010, 1), hw);
    }

    // PB-182 — LOCK devant la forme registre d'ADD lève #UD (386 PRM § 14.7, point 9) : l'IP d'après est celui du vecteur
    // 6, AX intact. LOCK ADD [BX],AX, la forme mémoire, s'exécute dans les deux modes.
    private static int Pb182(bool hw)
    {
        var bad = 0;
        var r = Jouer386([0xF0, 0x01, 0xC0], r => r[(int)R.AX] = 0x0101);
        bad += Voir("LOCK ADD AX,AX (386)", (r[(int)R.AX], r[(int)R.IP]) == (hw ? (0x0101, 0x0600) : (0x0202, 0x0103)),
                    $"AX = {r[(int)R.AX]:X4}, IP = {r[(int)R.IP]:X4}", hw ? "AX = 0101, IP = 0600 (#UD)" : "AX = 0202, IP = 0103", hw);
        r = Jouer386([0xF0, 0x01, 0x07], r => { r[(int)R.AX] = 0x0101; r[(int)R.BX] = 0x2000; });
        bad += Voir("LOCK ADD [BX],AX (386), témoin", mem.ram[0x2000] == 0x91 && r[(int)R.IP] == 0x0103,
                    $"[2000h] = {mem.ram[0x2000]:X2}, IP = {r[(int)R.IP]:X4}", "[2000h] = 91, IP = 0103", hw);
        return bad;
    }

    // PB-183 — le décalage de BT est signé (386 PRM § 17.2) : AX = FFFFh, BX = 0100h, BT [BX],AX lit le bit 15 du mot en
    // DS:00FEh ; PCem en DS:20FEh. Et sous une adresse de 16 bits, l'adresse replie : BX = 0000h, BT [BX],AX lit le mot en
    // DS:FFFEh, PCem en DS:1FFEh.
    private static int Pb183(bool hw)
    {
        var bad = 0;
        var r = Jouer386([0x0F, 0xA3, 0x07], r => { r[(int)R.AX] = 0xFFFF; r[(int)R.BX] = 0x0100; },
                         () => { mem.ram[0x00FF] = 0x80; mem.ram[0x20FF] = 0x00; });
        bad += Reg("BT [BX],AX, AX = FFFFh, BX = 0100h (386)", r, R.AX, 0x0001, "CF", (0xFFFF, 0), (0xFFFF, 1), hw);
        r = Jouer386([0x0F, 0xA3, 0x07], r => { r[(int)R.AX] = 0xFFFF; r[(int)R.BX] = 0x0000; },
                     () => { mem.ram[0xFFFF] = 0x80; mem.ram[0x1FFF] = 0x00; });
        bad += Reg("BT [BX],AX, AX = FFFFh, BX = 0000h (386)", r, R.AX, 0x0001, "CF", (0xFFFF, 0), (0xFFFF, 1), hw);
        return bad;
    }

    // PB-184 — MOVSX AX,BX (0F BF C3) copie le mot sur le 386EX mesuré (SST, forme 0FBF) ; PCem lève #UD.
    private static int Pb184(bool hw)
    {
        var r = Jouer386([0x0F, 0xBF, 0xC3], r => { r[(int)R.AX] = 0x1234; r[(int)R.BX] = 0x8001; });
        return Voir("MOVSX AX,BX, BX = 8001h (386)", (r[(int)R.AX], r[(int)R.IP]) == (hw ? (0x8001, 0x0103) : (0x1234, 0x0600)),
                    $"AX = {r[(int)R.AX]:X4}, IP = {r[(int)R.IP]:X4}", hw ? "AX = 8001, IP = 0103" : "AX = 1234, IP = 0600 (#UD)", hw);
    }

    // PB-185 — AAA : AX + 106h ; AAS : AX − 6, puis AH − 1 (SDM vol. 2). PCem ne passe pas la retenue d'AL à AH.
    private static int Pb185(bool hw)
    {
        var bad = 0;
        var r = Jouer386([0x37], r => { r[(int)R.AX] = 0x00FA; r[(int)R.FLAGS] = 0xF012; });
        bad += Reg("AAA, AX = 00FAh, AF = 1 (386)", r, R.AX, 0x0001, "CF", (0x0100, 1), (0x0200, 1), hw);
        r = Jouer386([0x3F], r => { r[(int)R.AX] = 0x0102; r[(int)R.FLAGS] = 0xF012; });
        bad += Reg("AAS, AX = 0102h, AF = 1 (386)", r, R.AX, 0x0001, "CF", (0x000C, 1), (0xFF0C, 1), hw);
        return bad;
    }

    // PB-186 — AAD et AAM posent SF, ZF et PF d'après AL (SDM vol. 2) ; PCem d'après AX.
    private static int Pb186(bool hw)
    {
        var bad = 0;
        var r = Jouer386([0xD4, 0x0A], r => r[(int)R.AX] = 0x0014);
        bad += Reg("AAM 0Ah, AL = 14h (386)", r, R.AX, 0x0040, "ZF", (0x0200, 0), (0x0200, 1), hw);
        r = Jouer386([0xD5, 0x0A], r => r[(int)R.AX] = 0x0178);
        bad += Reg("AAD 0Ah, AH = 01h, AL = 78h (386)", r, R.AX, 0x0080, "SF", (0x0082, 0), (0x0082, 1), hw);
        return bad;
    }

    // PB-187 — AAM 0 lève #DE, l'adresse de l'AAM empilée (SDM, page AAM) ; PCem divise par 10.
    private static int Pb187(bool hw)
    {
        var r = Jouer386([0xD4, 0x00], r => r[(int)R.AX] = 0x002A);
        var pile = (ushort)(mem.ram[0xFFF8] | mem.ram[0xFFF9] << 8);    // FLAGS, CS, puis IP
        return Voir("AAM 0, AL = 2Ah (386)",
                    hw ? (r[(int)R.AX], r[(int)R.IP], pile) == (0x002A, 0x0400, 0x0100) : (r[(int)R.AX], r[(int)R.IP]) == (0x0402, 0x0102),
                    $"AX = {r[(int)R.AX]:X4}, IP = {r[(int)R.IP]:X4}, IP empilé = {pile:X4}",
                    hw ? "AX = 002A, IP = 0400 (#DE), IP empilé = 0100" : "AX = 0402, IP = 0102", hw);
    }

    // PB-188 — DAS teste l'AL et le CF d'origine (SDM vol. 2) : AL = 01h, AF = 1, CF = 0 rend FBh ; PCem 9Bh.
    private static int Pb188(bool hw)
    {
        var r = Jouer386([0x2F], r => { r[(int)R.AX] = 0x0001; r[(int)R.FLAGS] = 0xF012; });
        return Reg("DAS, AL = 01h, AF = 1, CF = 0 (386)", r, R.AX, 0x0010, "AF", (0x009B, 1), (0x00FB, 1), hw);
    }

    // PB-262 — le décalage immédiat d'un BTC 16 bits se prend modulo 16 (SDM, page BT) : DX = 6D16h, BTC DX,53h bascule
    // le bit 3 ; PCem vise le bit 83, hors du mot, et DX ne bouge pas.
    private static int Pb262(bool hw)
    {
        var r = Jouer386([0x0F, 0xBA, 0xFA, 0x53], r => r[(int)R.DX] = 0x6D16);
        return Reg("BTC DX,53h, DX = 6D16h (386)", r, R.DX, 0x0001, "CF", (0x6D16, 0), (0x6D1E, 0), hw);
    }

    // PB-43 — MOV CRx, DRx et TRx ignorent le champ mod (SDM vol. 2) : 0F 20 06 (MOV ESI,CR0, mod 0, rm 6) fait trois
    // octets ; PCem lit un déplacement de 16 bits et en fait cinq. Et 67 0F 23 85 (MOV DR0,EBP, mod 2) en fait quatre ;
    // PCem six, la forme a32 décodée en 16 bits (PB-44), qui ne s'observe plus une fois PB-43 corrigé.
    private static int Pb43(bool hw)
    {
        var bad = 0;
        var r = Jouer386([0x0F, 0x20, 0x06, 0x40, 0x40], _ => { });
        bad += Voir("MOV ESI,CR0, ModRM 06h (386)", r[(int)R.IP] == (hw ? 0x0103 : 0x0105), $"IP = {r[(int)R.IP]:X4}",
                    hw ? "IP = 0103" : "IP = 0105", hw);
        r = Jouer386([0x67, 0x0F, 0x23, 0x85], _ => { });
        bad += Voir("MOV DR0,EBP, 67h, ModRM 85h (386)", r[(int)R.IP] == (hw ? 0x0104 : 0x0106), $"IP = {r[(int)R.IP]:X4}",
                    hw ? "IP = 0104" : "IP = 0106", hw);
        return bad;
    }

    // PB-50 — au-delà de 15 octets (10 sur le 286), #GP(0), l'IP du premier préfixe empilé (386 PRM § 14.7, point 6 ;
    // SST 286) : quinze 26h et un NOP, seize octets ; PCem exécute le NOP. Quatorze 26h et le NOP passent dans les deux
    // modes. Sur le 286, dix 26h et le NOP, onze octets.
    private static int Pb50(bool hw)
    {
        var bad = 0;
        byte[] Prefixes(int n) => [.. Enumerable.Repeat((byte)0x26, n), 0x90];
        ushort Pile() => (ushort)(mem.ram[0xFFF8] | mem.ram[0xFFF9] << 8);
        var r = Jouer386(Prefixes(15), _ => { });
        bad += Voir("quinze ES: et NOP, seize octets (386)",
                    hw ? (r[(int)R.IP], Pile()) == (0x0700, 0x0100) : r[(int)R.IP] == 0x0110,
                    $"IP = {r[(int)R.IP]:X4}, IP empilé = {Pile():X4}", hw ? "IP = 0700 (#GP), IP empilé = 0100" : "IP = 0110", hw);
        r = Jouer386(Prefixes(14), _ => { });
        bad += Voir("quatorze ES: et NOP, quinze octets (386), témoin", r[(int)R.IP] == 0x010F, $"IP = {r[(int)R.IP]:X4}",
                    "IP = 010F", hw);
        r = Jouer386(Prefixes(10), _ => { }, coeur: 286);
        bad += Voir("dix ES: et NOP, onze octets (286)",
                    hw ? (r[(int)R.IP], Pile()) == (0x0700, 0x0100) : r[(int)R.IP] == 0x010B,
                    $"IP = {r[(int)R.IP]:X4}, IP empilé = {Pile():X4}", hw ? "IP = 0700 (#GP), IP empilé = 0100" : "IP = 010B", hw);
        r = Jouer386(Prefixes(9), _ => { }, coeur: 286);
        bad += Voir("neuf ES: et NOP, dix octets (286), témoin", r[(int)R.IP] == 0x010A, $"IP = {r[(int)R.IP]:X4}",
                    "IP = 010A", hw);
        return bad;
    }

    // PB-51 — un octet d'instruction au-delà de la limite de CS lève #GP(0), avant l'exécution (386 PRM § 14.7, point 8 ;
    // SST 386) : MOV AX,1234h en 0000:FFFEh, à cheval, ne charge rien et empile FFFEh ; PCem lit 12h en 1:0000h. Un NOP
    // en FFFFh s'exécute, et l'instruction d'après, en 10000h, lève #GP, IP empilé 0000h ; PCem l'exécute. La pile en
    // 0000:2000h, loin du code.
    private static int Pb51(bool hw)
    {
        var bad = 0;
        ushort Pile() => (ushort)(mem.ram[0x1FFA] | mem.ram[0x1FFB] << 8);
        foreach (var coeur in new[] { 386, 286 })
        {
                var r = Jouer386([0xB8, 0x34, 0x12], r => r[(int)R.SP] = 0x2000, coeur: coeur, ip: 0xFFFE);
                bad += Voir($"MOV AX,1234h en 0000:FFFEh ({coeur})",
                            hw ? (r[(int)R.AX], r[(int)R.IP], Pile()) == (0, 0x0700, 0xFFFE) : (r[(int)R.AX], r[(int)R.IP]) == (0x1234, 0x0001),
                            $"AX = {r[(int)R.AX]:X4}, IP = {r[(int)R.IP]:X4}, IP empilé = {Pile():X4}",
                            hw ? "AX = 0000, IP = 0700 (#GP), IP empilé = FFFE" : "AX = 1234, IP = 0001 (de 10001h)", hw);
        }
        var n = Jouer386([0x90], r => r[(int)R.SP] = 0x2000, ip: 0xFFFF, pas: 2);
        bad += Voir("NOP en 0000:FFFFh, puis l'instruction d'après (386)",
                    hw ? (n[(int)R.IP], Pile()) == (0x0700, 0x0000) : n[(int)R.IP] == 0x0001,
                    $"IP = {n[(int)R.IP]:X4}, IP empilé = {Pile():X4}", hw ? "IP = 0700 (#GP), IP empilé = 0000" : "IP = 0001 (de 10001h)", hw);
        return bad;
    }

    // PB-78 — le 486 n'a pas de LOADALL : 0F 07 lève #UD (R. Collins). Le bloc en 0000:3000h charge un mode réel, EIP =
    // 0200h, EAX = 5A5Ah ; PCem le charge. Sur le 386, LOADALL le charge dans les deux modes.
    private static int Pb78(bool hw)
    {
        var bad = 0;
        void Bloc()
        {
            void Bd(int off, uint v) { for (var k = 0; k < 4; k++) mem.ram[0x3000 + off + k] = (byte)(v >> (8 * k)); }
            void Seg(int off, uint limit, byte access) { Bd(off, (uint)access << 8); Bd(off + 4, 0); Bd(off + 8, limit); }
            for (var k = 0; k < 0xCC; k++) mem.ram[0x3000 + k] = 0;
            Bd(0x00, 0x00000010);                               // CR0 : ET, mode réel
            Bd(0x04, 0x0002);                                   // FLAGS
            Bd(0x08, 0x0200);                                   // EIP
            Bd(0x18, 0xFFFE);                                   // ESP
            Bd(0x28, 0x5A5A);                                   // EAX
            Seg(0x54, 0xFFFF, 0x89); Seg(0x60, 0x3FF, 0); Seg(0x6C, 0, 0); Seg(0x78, 0xFFFF, 0x82);
            foreach (var off in new[] { 0x84, 0x90, 0x9C, 0xA8, 0xC0 })
                Seg(off, 0xFFFF, 0x93);
            Seg(0xB4, 0xFFFF, 0x9B);                            // CS
        }
        var r = Jouer386([0x0F, 0x07], r => r[(int)R.DI] = 0x3000, Bloc, coeur: 486);
        bad += Voir("LOADALL386, ES:EDI = 0000:3000h (486)",
                    (r[(int)R.AX], r[(int)R.IP]) == (hw ? (0, 0x0600) : (0x5A5A, 0x0200)),
                    $"AX = {r[(int)R.AX]:X4}, IP = {r[(int)R.IP]:X4}", hw ? "AX = 0000, IP = 0600 (#UD)" : "AX = 5A5A, IP = 0200", hw);
        r = Jouer386([0x0F, 0x07], r => r[(int)R.DI] = 0x3000, Bloc);
        bad += Voir("LOADALL386, ES:EDI = 0000:3000h (386), témoin", (r[(int)R.AX], r[(int)R.IP]) == (0x5A5A, 0x0200),
                    $"AX = {r[(int)R.AX]:X4}, IP = {r[(int)R.IP]:X4}", "AX = 5A5A, IP = 0200", hw);
        return bad;
    }

    // PB-189 — un opérande en mémoire doit tenir entier dans la limite de son segment (386 PRM § 14.7, point 7 ;
    // mesuré : SST 386 et 286) : l'exception 13, ou 12 pour la pile. Le vecteur 0Ch posé en 0000:0800, pour #SS. Au 386,
    // l'opérande entier ; au 286, chaque mot à son offset replié (BOUND et LES en FFFEh lisent leur second mot en
    // 0000h, sans faute). Un RETF en SS:FFFEh lit CS en SS:0000h, sans faute (PCem le lit en SS:10000h).
    private static int Pb189(bool hw)
    {
        var bad = 0;
        void Ss() { mem.ram[0x30] = 0x00; mem.ram[0x31] = 0x08; mem.ram[0x32] = 0x00; mem.ram[0x33] = 0x00; }
        int Cas(string nom, int coeur, byte[] code, Action<ushort[]> regs, Func<ushort[], bool> pcem, string vuPcem,
                Func<ushort[], bool> materiel, string vuMateriel)
        {
            var r = Jouer386(code, regs, Ss, coeur: coeur);
            return Voir($"{nom} ({coeur})", hw ? materiel(r) : pcem(r),
                        $"IP = {r[(int)R.IP]:X4}, CS = {r[(int)R.CS]:X4}, AX = {r[(int)R.AX]:X4}, SP = {r[(int)R.SP]:X4}",
                        hw ? vuMateriel : vuPcem, hw);
        }
        // Une faute en mode réel empile FLAGS, CS et IP : SP − 6 ; l'IP du vecteur, 0700h (#GP) ou 0800h (#SS) ; le
        // registre de destination intact (AX = 1234h).
        bool Gp(ushort[] r, int sp) => r[(int)R.IP] == 0x0700 && r[(int)R.SP] == sp - 6 && r[(int)R.AX] == 0x1234;
        const string VuGp = "IP = 0700 (#GP), AX = 1234 intact, SP − 6";
        bad += Cas("ADD [BX],AX, BX = FFFFh, un mot à cheval", 386, [0x01, 0x07], r => { r[(int)R.BX] = 0xFFFF; r[(int)R.AX] = 0x1234; },
                   r => r[(int)R.IP] == 0x0102, "IP = 0102, le mot écrit en FFFFh et 10000h", r => Gp(r, 0xFFFE), VuGp);
        bad += Cas("67 ADD [00010000h],AX, l'adresse de 32 bits au-delà de FFFFh", 386, [0x67, 0x01, 0x05, 0x00, 0x00, 0x01, 0x00],
                   r => r[(int)R.AX] = 0x1234, r => r[(int)R.IP] == 0x0107, "IP = 0107", r => Gp(r, 0xFFFE), VuGp);
        bad += Cas("POP AX, SP = FFFFh", 386, [0x58], r => { r[(int)R.SP] = 0xFFFF; r[(int)R.AX] = 0x1234; },
                   r => r[(int)R.IP] == 0x0101 && r[(int)R.SP] == 0x0001, "IP = 0101, SP = 0001",
                   r => r[(int)R.IP] == 0x0800 && r[(int)R.SP] == 0xFFF9 && r[(int)R.AX] == 0x1234, "IP = 0800 (#SS), AX = 1234 intact, SP = FFF9");
        bad += Cas("LODSW, SI = FFFFh", 386, [0xAD], r => { r[(int)R.SI] = 0xFFFF; r[(int)R.AX] = 0x1234; },
                   r => r[(int)R.IP] == 0x0101, "IP = 0101", r => Gp(r, 0xFFFE) && r[(int)R.SI] == 0xFFFF, VuGp + ", SI intact");
        bad += Cas("LES AX,[BX], BX = FFFEh, le pointeur à cheval", 386, [0xC4, 0x07], r => { r[(int)R.BX] = 0xFFFE; r[(int)R.AX] = 0x1234; },
                   r => r[(int)R.IP] == 0x0102, "IP = 0102", r => Gp(r, 0xFFFE), VuGp);
        bad += Cas("ADD [BX],AX, BX = FFFFh", 286, [0x01, 0x07], r => { r[(int)R.BX] = 0xFFFF; r[(int)R.AX] = 0x1234; },
                   r => r[(int)R.IP] == 0x0102, "IP = 0102", r => Gp(r, 0xFFFE), VuGp);
        bad += Cas("LES AX,[BX], BX = FFFDh, le second mot à cheval", 286, [0xC4, 0x07], r => { r[(int)R.BX] = 0xFFFD; r[(int)R.AX] = 0x1234; },
                   r => r[(int)R.IP] == 0x0102, "IP = 0102", r => Gp(r, 0xFFFE), VuGp);
        bad += Cas("LES AX,[BX], BX = FFFEh, deux mots entiers (témoin)", 286, [0xC4, 0x07], r => { r[(int)R.BX] = 0xFFFE; r[(int)R.AX] = 0x1234; },
                   r => r[(int)R.IP] == 0x0102, "IP = 0102", r => r[(int)R.IP] == 0x0102, "IP = 0102, aucune faute");
        // RETF en SS:FFFEh : IP en FFFEh (9090h, le fond), CS en SS:0000h, le mot 0400h du vecteur 0 (silicium) ; PCem le
        // lit en SS:10000h, le fond (9090h).
        bad += Cas("RETF, SP = FFFEh, CS lu en SS:0000h", 386, [0xCB], r => r[(int)R.SP] = 0xFFFE,
                   r => r[(int)R.SP] == 0x0002 && r[(int)R.CS] == 0x9090, "SP = 0002, CS = 9090 (lu en SS:10000h)",
                   r => r[(int)R.SP] == 0x0002 && r[(int)R.CS] == 0x0400, "SP = 0002, CS = 0400 (lu en SS:0000h), aucune faute");
        return bad;
    }

    // ===== Le x87 (G13.6) =====

    /// <summary>Joue `pas` instructions x87 sur le cœur 286 (avec un 287) ou 386 (avec un 387), comme Jouer386 ; rend les
    /// registres et l'état x87 d'après.</summary>
    private static (ushort[] r, HState s) JouerX87(byte[] code, int coeur, int pas, Action<ushort[]>? regs = null)
    {
        _386.FuzzFpu = coeur == 286 ? 2 : 4;
        try
        {
            var r = Jouer386(code, regs ?? (_ => { }), coeur: coeur, pas: pas);
            var s = HState.Create();
            _808x.GetState(ref s);
            return (r, s);
        }
        finally
        {
            _386.FuzzFpu = 0;
        }
    }

    // PB-61 — FNSTSW AX rend le mot d'état entier, TOP compris (SDM vol. 2, FSTSW ; 287 PRM) : FNINIT ; FLD1 ×3 (TOP = 5) ;
    // FNSTSW AX → 2800h ; PCem 0000h. Sur le 287 (cœur 286) et le 387 (cœur 386).
    private static int Pb61(bool hw)
    {
        var bad = 0;
        foreach (var coeur in new[] { 286, 386 })
        {
            var (r, _) = JouerX87([0xDB, 0xE3, 0xD9, 0xE8, 0xD9, 0xE8, 0xD9, 0xE8, 0xDF, 0xE0], coeur, 5);
            var attendu = hw ? 0x2800 : 0x0000;
            bad += Voir($"FNINIT ; FLD1 ×3 ; FNSTSW AX ({(coeur == 286 ? "287" : "387")})", r[(int)R.AX] == attendu,
                        $"AX = {r[(int)R.AX]:X4}", $"AX = {attendu:X4}", hw);
        }
        return bad;
    }

    /// <summary>Joue sur le 8087 (cœur 8088), le 287 (cœur 286) ou le 387 (cœur 386) : un NaN de 64 bits en DS:0200h
    /// (7FF8000000000000), BX = 0200h. Rend AX (FNSTSW AX en fin de code) ; sur le 8087, qui n'a pas FNSTSW AX (PB-200 :
    /// le noyau laisse AX), le DF E0 final devient FNSTSW [BX+40h] (DD 7F 40), et le mot d'état est lu en mémoire.</summary>
    private static ushort X87(int fpu, byte[] code, int pas)
    {
        void Nan()
        {
            byte[] q = [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF8, 0x7F];
            for (var k = 0; k < 8; k++) mem.ram[0x200 + k] = q[k];
        }
        if (fpu == 1)
        {
            _386.FuzzFpu = 1;
            try
            {
                if (code is [.., 0xDF, 0xE0])
                {
                    Jouer([.. code[..^2], 0xDD, 0x7F, 0x40], r => r[(int)R.BX] = 0x200, Nan, pas);
                    return W(0x240);
                }
                return Jouer(code, r => r[(int)R.BX] = 0x200, Nan, pas)[(int)R.AX];
            }
            finally
            {
                _386.FuzzFpu = 0;
            }
        }
        _386.FuzzFpu = fpu;
        try
        {
            return Jouer386(code, r => r[(int)R.BX] = 0x200, Nan, coeur: Coeur(fpu), pas: pas)[(int)R.AX];
        }
        finally
        {
            _386.FuzzFpu = 0;
        }
    }

    private static readonly (int fpu, string nom)[] Fpus = [(1, "8087"), (2, "287"), (4, "387"), (5, "486")];
    private static int Coeur(int fpu) => fpu switch { 2 or 3 => 286, 5 => 486, _ => 386 };
    private static string Cc(ushort ax) => $"C3 C2 C0 = {(ax >> 14) & 1}{(ax >> 10) & 1}{(ax >> 8) & 1}";

    // PB-57 — FCOM ST(1) de registre, ST(0) = NaN et ST(1) = 1 : non ordonné, C3 C2 C0 = 111 (SDM vol. 2, FCOM ; 387 PRM
    // annexe C) ; PCem, 000. FNINIT ; FLD1 ; FLD m64 NaN ; FCOM ST(1) ; FNSTSW AX.
    private static int Pb57(bool hw)
    {
        var bad = 0;
        foreach (var (fpu, nom) in Fpus)
        {
            var ax = X87(fpu, [0xDB, 0xE3, 0xD9, 0xE8, 0xDD, 0x07, 0xD8, 0xD1, 0xDF, 0xE0], 5);
            var attendu = hw ? "C3 C2 C0 = 111" : "C3 C2 C0 = 000";
            bad += Voir($"FCOM ST(1), NaN contre 1 ({nom})", Cc(ax) == attendu, Cc(ax), attendu, hw);
        }
        // FCOMP ST(1) (D8 D9) et FCOMPP (DE D9), NaN contre 1 : non ordonné dans les deux modes (ils appellent
        // x87_compare chez PCem aussi) ; leurs entrées dans les tables du mode, que seul le temps distingue.
        foreach (var (fpu, nom) in Fpus)
        foreach (var (o1, o2, n) in new (byte, byte, string)[] { (0xD8, 0xD9, "FCOMP ST(1)"), (0xDE, 0xD9, "FCOMPP") })
        {
            var ax = X87(fpu, [0xDB, 0xE3, 0xD9, 0xE8, 0xDD, 0x07, o1, o2, 0xDF, 0xE0], 5);
            bad += Voir($"{n}, NaN contre 1 ({nom})", Cc(ax) == "C3 C2 C0 = 111", Cc(ax), "C3 C2 C0 = 111", hw);
        }
        // Le temps : FCOM, FCOMP et FCOMPP de registre comptent `fcom` (SDM ; 387 PRM annexe C), PCem `fadd` ; le 387 :
        // FNINIT ; FLD1 ; FLD1 ; puis l'instruction, son pas mesuré.
        foreach (var (o1, o2, n) in new (byte, byte, string)[] { (0xD8, 0xD1, "FCOM ST(1)"), (0xD8, 0xD9, "FCOMP ST(1)"), (0xDE, 0xD9, "FCOMPP") })
        {
            X87(4, [0xDB, 0xE3, 0xD9, 0xE8, 0xD9, 0xE8, o1, o2], 4);
            var t = x87_timings_c.x87_timings;
            var attendu = hw ? t.fcom : t.fadd;
            bad += Voir($"{n}, son temps (387)", DernierPas == attendu, $"{DernierPas} cycles",
                        $"{attendu} ({(hw ? "fcom" : "fadd")})", hw);
        }
        return bad;
    }

    // PB-64 — FTST de NaN : non ordonné, 111 (SDM vol. 2, FTST) ; PCem 000. FNINIT ; FLD m64 NaN ; FTST ; FNSTSW AX.
    private static int Pb64(bool hw)
    {
        var bad = 0;
        foreach (var (fpu, nom) in Fpus)
        {
            var ax = X87(fpu, [0xDB, 0xE3, 0xDD, 0x07, 0xD9, 0xE4, 0xDF, 0xE0], 4);
            var attendu = hw ? "C3 C2 C0 = 111" : "C3 C2 C0 = 000";
            bad += Voir($"FTST, NaN ({nom})", Cc(ax) == attendu, Cc(ax), attendu, hw);
        }
        // FTST de +∞ (1/0) : non comparable en projectif (le 8087 et le 287, IC nul ; Numerics Supplement table S-27) ;
        // plus grand que 0 en affine, et chez PCem.
        foreach (var (fpu, nom) in Fpus)
        {
            var ax = X87(fpu, [0xDB, 0xE3, 0xD9, 0xE8, 0xD9, 0xEE, 0xDE, 0xF9, 0xD9, 0xE4, 0xDF, 0xE0], 6);
            var attendu = hw && fpu < 4 ? "C3 C2 C0 = 111" : "C3 C2 C0 = 000";
            bad += Voir($"FTST, +∞ ({nom}){(fpu >= 4 ? ", témoin" : "")}", Cc(ax) == attendu, Cc(ax), attendu, hw);
        }
        return bad;
    }

    // PB-70 — la détection classique : FNINIT ; FLD1 ; FLDZ ; FDIVP (+∞) ; FLD ST(0) ; FCHS ; FCOMPP ; FNSTSW AX. En
    // projectif (le 8087 et le 287, IC nul après FNINIT), −∞ = +∞ : C3 (Numerics Supplement, table S-27) ; PCem, −∞ < +∞ :
    // C0. Le 387 n'a que l'affine : C0 dans les deux modes (le témoin).
    private static int Pb70(bool hw)
    {
        var bad = 0;
        foreach (var (fpu, nom) in Fpus)
        {
            var ax = X87(fpu, [0xDB, 0xE3, 0xD9, 0xE8, 0xD9, 0xEE, 0xDE, 0xF9, 0xD9, 0xC0, 0xD9, 0xE0, 0xDE, 0xD9, 0xDF, 0xE0], 8);
            var attendu = hw && fpu < 4 ? "C3 C2 C0 = 100" : "C3 C2 C0 = 001";
            bad += Voir($"FCOMPP, −∞ contre +∞ ({nom}){(fpu >= 4 ? ", témoin" : "")}", Cc(ax) == attendu, Cc(ax), attendu, hw);
        }
        return bad;
    }

    // PB-63 — FXAM (SDM vol. 2 ; Numerics Supplement table S-13), C3 C2 C1 C0 : +∞ 0101 (PCem 0100) ; NaN 0001 (PCem 0100) ;
    // −0 1010 (PCem 1000). Le résultat lu par FNSTSW AX (sur le 8087, DF E0, que PCem exécute : PB-200).
    private static int Pb63(bool hw)
    {
        var bad = 0;
        string Code(ushort ax) => $"C3 C2 C1 C0 = {(ax >> 14) & 1}{(ax >> 10) & 1}{(ax >> 9) & 1}{(ax >> 8) & 1}";
        (string nom, byte[] charge, string pcem, string materiel)[] cas =
        [
            ("+∞", [0xD9, 0xE8, 0xD9, 0xEE, 0xDE, 0xF9], "0100", "0101"),
            ("NaN", [0xDD, 0x07], "0100", "0001"),
            ("−0", [0xD9, 0xEE, 0xD9, 0xE0], "1000", "1010"),
        ];
        foreach (var (fpu, nomFpu) in Fpus)
        foreach (var (nom, charge, pcem, materiel) in cas)
        {
            byte[] code = [0xDB, 0xE3, .. charge, 0xD9, 0xE5, 0xDF, 0xE0];
            var ax = X87(fpu, code, 1 + charge.Length / 2 + 2);
            var attendu = "C3 C2 C1 C0 = " + (hw ? materiel : pcem);
            bad += Voir($"FXAM {nom} ({nomFpu})", Code(ax) == attendu, Code(ax), attendu, hw);
        }
        return bad;
    }

    /// <summary>Comme X87, le mot `init` en DS:0200h (un mot de contrôle pour FLDCW) ; rend les huit octets de DS:0200h
    /// d'après (un FSTP m64 en fin de code).</summary>
    private static ulong X87Mem(int fpu, byte[] code, int pas, ushort init)
    {
        void Mot() { mem.ram[0x200] = (byte)init; mem.ram[0x201] = (byte)(init >> 8); }
        _386.FuzzFpu = fpu;
        try
        {
            if (fpu == 1)
                Jouer(code, r => r[(int)R.BX] = 0x200, Mot, pas);
            else
                Jouer386(code, r => r[(int)R.BX] = 0x200, Mot, coeur: Coeur(fpu), pas: pas);
            ulong v = 0;
            for (var k = 7; k >= 0; k--)
                v = (v << 8) | mem.ram[0x200 + k];
            return v;
        }
        finally
        {
            _386.FuzzFpu = 0;
        }
    }

    // PB-66 — FNINIT ; FLDCW [BX] ; FLDLN2 (ou FLDLG2) ; FSTP m64 [BX]. ln 2 au plus près : 3FE62E42FEFA39EF (PCem …39F0),
    // sur les trois coprocesseurs. log₁₀ 2 sous RC vers −∞ (077Fh) : 3FD34413509F79FE sur le 387, qui arrondit ses
    // constantes au mode (387 PRM § 4.7) ; PCem …79FF. Le 8087 et le 287 la prennent au plus près, mais le noyau (N7c)
    // range la constante de 80 bits et FSTP m64 l'arrondit selon RC, comme le silicium : la même valeur que le 387 ; leur
    // arrondi propre se voit en m80 (materiel-cas PB-264, les constantes de N3a). En G13.6a, en double, ils rendaient …79FF.
    private static int Pb66(bool hw)
    {
        var bad = 0;
        foreach (var (fpu, nom) in Fpus)
        {
            var v = X87Mem(fpu, [0xDB, 0xE3, 0xD9, 0x2F, 0xD9, 0xED, 0xDD, 0x1F], 4, 0x037F);
            var attendu = hw ? 0x3FE62E42FEFA39EFUL : 0x3FE62E42FEFA39F0UL;
            bad += Voir($"FLDLN2 ; FSTP m64 ({nom})", v == attendu, $"{v:X16}", $"{attendu:X16}", hw);
        }
        foreach (var (fpu, nom) in Fpus)
        {
            var v = X87Mem(fpu, [0xDB, 0xE3, 0xD9, 0x2F, 0xD9, 0xEC, 0xDD, 0x1F], 4, 0x077F);
            var attendu = hw ? 0x3FD34413509F79FEUL : 0x3FD34413509F79FFUL;
            bad += Voir($"RC vers −∞ ; FLDLG2 ; FSTP m64 ({nom})", v == attendu, $"{v:X16}",
                        $"{attendu:X16}", hw);
        }
        (byte op, string nom, ulong pres, ulong haut)[] hauts =
        [(0xE9, "FLDL2T", 0x400A934F0979A371, 0x400A934F0979A372), (0xEA, "FLDL2E", 0x3FF71547652B82FE, 0x3FF71547652B82FF),
         (0xEB, "FLDPI", 0x400921FB54442D18, 0x400921FB54442D19)];
        foreach (var (fpu, nom) in Fpus)
        foreach (var (op, nomC, pres, haut) in hauts)
        {
            var v = X87Mem(fpu, [0xDB, 0xE3, 0xD9, 0x2F, 0xD9, op, 0xDD, 0x1F], 4, 0x0B7F);
            var attendu = hw ? haut : pres;
            bad += Voir($"RC vers +∞ ; {nomC} ; FSTP m64 ({nom})", v == attendu, $"{v:X16}",
                        $"{attendu:X16}", hw);
        }
        return bad;
    }

    // PB-67 — FNINIT ; FILD m64 = 5 ; FILD m64 = 2^53 + 1 ; FST ST(1) ; FINCSTP ; FISTP m64 → 0020000000000001 (le registre
    // de 80 bits tient l'entier : SDM vol. 1 § 8.1.2) ; PCem 0000000000000005, l'entier que MM[] gardait. Les deux entiers
    // en DS:0200h et 0208h, le résultat en 0210h.
    private static int Pb67(bool hw)
    {
        var bad = 0;
        foreach (var (fpu, nom) in Fpus)
        {
            _386.FuzzFpu = fpu;
            ulong v = 0;
            try
            {
                void Entiers()
                {
                    for (var k = 0; k < 8; k++)
                    {
                        mem.ram[0x200 + k] = (byte)(5UL >> (8 * k));
                        mem.ram[0x208 + k] = (byte)(0x0020000000000001UL >> (8 * k));
                    }
                }
                byte[] code = [0xDB, 0xE3, 0xDF, 0x2F, 0xDF, 0x6F, 0x08, 0xDD, 0xD1, 0xD9, 0xF7, 0xDF, 0x7F, 0x10];
                if (fpu == 1)
                    Jouer(code, r => r[(int)R.BX] = 0x200, Entiers, 6);
                else
                    Jouer386(code, r => r[(int)R.BX] = 0x200, Entiers, coeur: Coeur(fpu), pas: 6);
                for (var k = 7; k >= 0; k--)
                    v = (v << 8) | mem.ram[0x210 + k];
            }
            finally
            {
                _386.FuzzFpu = 0;
            }
            var attendu = hw ? 0x0020000000000001UL : 5UL;
            bad += Voir($"FILD ; FILD ; FST ST(1) ; FINCSTP ; FISTP m64 ({nom})", v == attendu, $"{v:X16}", $"{attendu:X16}", hw);
        }
        // FSTP ST(1), par DD D9 et par son alias D9 D9 : FILD ; FILD ; FSTP ST(1) ; FISTP m64, sans FINCSTP.
        foreach (var (fpu, nom) in Fpus)
        foreach (var (o1, o2) in new (byte, byte)[] { (0xDD, 0xD9), (0xD9, 0xD9) })
        {
            _386.FuzzFpu = fpu;
            ulong v = 0;
            try
            {
                void Entiers()
                {
                    for (var k = 0; k < 8; k++)
                    {
                        mem.ram[0x200 + k] = (byte)(5UL >> (8 * k));
                        mem.ram[0x208 + k] = (byte)(0x0020000000000001UL >> (8 * k));
                    }
                }
                byte[] code = [0xDB, 0xE3, 0xDF, 0x2F, 0xDF, 0x6F, 0x08, o1, o2, 0xDF, 0x7F, 0x10];
                if (fpu == 1)
                    Jouer(code, r => r[(int)R.BX] = 0x200, Entiers, 5);
                else
                    Jouer386(code, r => r[(int)R.BX] = 0x200, Entiers, coeur: Coeur(fpu), pas: 5);
                for (var k = 7; k >= 0; k--)
                    v = (v << 8) | mem.ram[0x210 + k];
            }
            finally
            {
                _386.FuzzFpu = 0;
            }
            var attendu = hw ? 0x0020000000000001UL : 5UL;
            bad += Voir($"FILD ; FILD ; FSTP ST(1) ({o1:X2} {o2:X2}) ; FISTP m64 ({nom})", v == attendu, $"{v:X16}", $"{attendu:X16}", hw);
        }
        return bad;
    }

    // PB-213 — les comparaisons remettent C1 à zéro sur le 387 et le 486 (SDM vol. 2, « C1 Set to 0 ») ; PCem le laisse.
    // FNINIT ; FLD1 ; FLD1 ; FCHS ; FXAM (−1 : C1 = 1) ; la comparaison ; FNSTSW AX. Ses formes de registre et de mémoire
    // (BX = 0200h ; a32 : [EDI]), FUCOM et FTST, sur le 387, le 486 et le 287XL (déduit) ; le 8087 et le 287, où C1 est
    // indéfini, gardent celui de PCem (témoins).
    private static int Pb213(bool hw)
    {
        var bad = 0;
        (byte[] op, string nom)[] formes =
        [
            ([0xD8, 0xD1], "FCOM ST(1)"), ([0xD8, 0xD9], "FCOMP ST(1)"), ([0xDE, 0xD9], "FCOMPP"),
            ([0xD8, 0x17], "FCOM m32"), ([0xDC, 0x1F], "FCOMP m64"), ([0x67, 0xDC, 0x17], "FCOM m64 (a32)"),
            ([0xDE, 0x17], "FICOM m16"), ([0xDE, 0x1F], "FICOMP m16"), ([0xDA, 0x17], "FICOM m32"),
            ([0xDA, 0x1F], "FICOMP m32"), ([0xDD, 0xE1], "FUCOM ST(1)"), ([0xDD, 0xE9], "FUCOMP ST(1)"),
            ([0xDA, 0xE9], "FUCOMPP"), ([0xD9, 0xE4], "FTST"),
        ];
        foreach (var (fpu, nom) in Fpus.Append((3, "287XL")))
        foreach (var (op, n) in formes)
        {
            if (fpu < 3 && n != "FCOM ST(1)" || fpu == 3 && op[0] == 0x67) // le 287XL, sur le 286 : pas de 67h
                continue;
            byte[] code = [0xDB, 0xE3, 0xD9, 0xE8, 0xD9, 0xE8, 0xD9, 0xE0, 0xD9, 0xE5, .. op, 0xDF, 0xE0];
            var c1 = (X87(fpu, code, 7) >> 9) & 1;
            var attendu = hw && fpu >= 3 ? 0 : 1;
            bad += Voir($"FXAM (C1 = 1) ; {n} ({nom}){(fpu < 3 ? ", témoin" : "")}", c1 == attendu, $"C1 = {c1}",
                        $"C1 = {attendu}", hw);
        }
        return bad;
    }

    // PB-207 — FSTENV range l'environnement, puis masque les six exceptions (SDM vol. 2, FSTENV ; 8087 et 287 : déduit).
    // FNINIT ; FLDCW [BX] (037Bh, ZE démasquée) ; FNSTENV [BX+10h] ; FNSTCW [BX] → 037Fh ; PCem 037Bh. L'environnement
    // rangé garde 037Bh dans les deux modes. Le 387 et le 486 aussi en a32 (FNSTENV [EBX+10h]).
    private static int Pb207(bool hw)
    {
        var bad = 0;
        foreach (var (fpu, nom) in Fpus)
        foreach (var a32 in fpu >= 4 ? new[] { false, true } : new[] { false })
        {
            byte[] env = a32 ? [0x67, 0xD9, 0x73, 0x10] : [0xD9, 0x77, 0x10];
            var v = X87Mem(fpu, [0xDB, 0xE3, 0xD9, 0x2F, .. env, 0xD9, 0x3F], 4, 0x037B);
            var range = mem.ram[0x210] | (mem.ram[0x211] << 8);
            var cw = (int)(v & 0xFFFF);
            var attendu = hw ? 0x037F : 0x037B;
            var f = a32 ? "FNSTENV [EBX+10h]" : "FNSTENV [BX+10h]";
            bad += Voir($"FLDCW 037Bh ; {f} ; FNSTCW ({nom})", cw == attendu, $"{cw:X4}", $"{attendu:X4}", hw);
            bad += Voir($"{f}, le mot de contrôle rangé ({nom})", range == 0x037B, $"{range:X4}", "037B", hw);
        }
        // La trace de l'IRQ13 (G13.6c) : FNINIT ; FLDCW [BX] ; FNSTENV [BX+10h] ; FLD1 ; FLDZ ; FDIVP ; FNSTSW [BX] → ZE
        // masquée, +∞ et le dépilement, 3804h, sans IRQ13 ; PCem 3004h, l'IRQ13 en attente au second 8259.
        return Avec8259(() =>
        {
            foreach (var (fpu, nom) in new[] { (2, "287"), (4, "387"), (5, "486") })
            {
                Pic(true);
                var (_, m) = Acheminer(fpu, [0xDB, 0xE3, 0xD9, 0x2F, 0xD9, 0x77, 0x10, 0xD9, 0xE8, 0xD9, 0xEE, 0xDE, 0xF9,
                                             0xDD, 0x3F, 0xF4], 9);
                var irq13 = (pic.pic2.pend & 0x20) != 0;
                var attendu = hw ? (0x3804, false) : (0x3004, true);
                bad += Voir($"FNSTENV ; FDIVP 1/0 ; FNSTSW m16, et IRQ13 ({nom})", (m, irq13) == attendu,
                            $"{m:X4}, IRQ13 {(irq13 ? "en attente" : "non")}",
                            $"{attendu.Item1:X4}, IRQ13 {(attendu.Item2 ? "en attente" : "non")}", hw);
            }
            return bad;
        });
    }

    // ===== L'exception démasquée du x87 et son acheminement : PB-59, PB-69, PB-204 (un groupe) =====

    /// <summary>FNINIT ; FLDCW [BX] ; FLD1 ; FLDZ ; FDIVP : 1 / 0, ZE démasquée par le mot de contrôle 037Bh ; dix octets,
    /// l'instruction suivante en 010Ah.</summary>
    private static readonly byte[] Zero = [0xDB, 0xE3, 0xD9, 0x2F, 0xD9, 0xE8, 0xD9, 0xEE, 0xDE, 0xF9];

    /// <summary>Joue sur le x87 `fpu` (le 8088 pour le 8087, sinon le cœur de Coeur), comme X87Mem : le mot `cw` en
    /// DS:0200h, BX = 0200h ; le vecteur 2 (NMI) en 0000:0500h, `nmi` (un HLT par défaut), et le vecteur 10h (#MF) en
    /// 0000:0800h, un HLT ; la NMI réarmée (aucun reset ne le fait). `avant` court après le reset. Un code dont on lit
    /// l'IP finit sur un HLT : l'IP d'arrivée ne dépend pas du nombre de pas (en mode PCem, FDIVP démasquée ne compte
    /// aucun cycle, et le pas enchaîne l'instruction suivante). Rend l'IP d'après et le mot de DS:0200h ; les registres
    /// d'après restent dans <see cref="Registres"/>.</summary>
    private static (ushort ip, ushort mot) Acheminer(int fpu, byte[] code, int pas, ushort cw = 0x037B, Action? avant = null,
                                                     byte[]? nmi = null)
    {
        void Ram()
        {
            mem.ram[0x200] = (byte)cw; mem.ram[0x201] = (byte)(cw >> 8);
            mem.ram[0x08] = 0x00; mem.ram[0x09] = 0x05; mem.ram[0x0A] = 0x00; mem.ram[0x0B] = 0x00;
            mem.ram[0x40] = 0x00; mem.ram[0x41] = 0x08; mem.ram[0x42] = 0x00; mem.ram[0x43] = 0x00;
            var h = nmi ?? [0xF4];
            for (var k = 0; k < h.Length; k++)
                mem.ram[0x500 + k] = h[k];
            mem.ram[0x800] = 0xF4;
            _386_common.nmi_enable = 1;
            avant?.Invoke();
        }
        _386.FuzzFpu = fpu;
        try
        {
            Registres = fpu == 1 ? Jouer(code, r => r[(int)R.BX] = 0x200, Ram, pas)
                                 : Jouer386(code, r => r[(int)R.BX] = 0x200, Ram, coeur: Coeur(fpu), pas: pas);
            return (Registres[(int)R.IP], (ushort)(mem.ram[0x200] | mem.ram[0x201] << 8));
        }
        finally
        {
            _386.FuzzFpu = 0;
            _808x.nmi = 0;
            _808x.nmi_mask = 0;
        }
    }

    private static ushort[] Registres = [];

    private static void Ne() => x86.cr0 |= 0x20;

    // PB-59 — une division par zéro démasquée pose ES et B (387 PRM, mot d'état ; 287 PRM p. 1-10 ; le 8087 : déduit) :
    // FNINIT ; FLDCW [BX] (037Bh) ; FLD1 ; FLDZ ; FDIVP ; FNSTSW [BX] → B084h ; PCem 3004h (ni ES ni B). FNSTSW
    // n'attend pas : le verrou de l'AT et l'exception en attente du 486 la laissent passer. FNCLEX avant FNSTSW : 3000h
    // dans les deux modes, B effacé. Le 486, NE posé : FNSTSW passe sans #MF ; FLD1, ou WAIT, lève #MF (le vecteur 10h,
    // 0000:0800h), l'adresse de retour celle de FLD1 ; PCem l'exécute. Les sept qui n'attendent pas (FNSTSW AX, FNSTCW,
    // FNSTENV, FSETPM, FNSAVE ; a32 sur le 387 et le 486) passent après l'exception, sur le 287, le 287XL, le 387 et le
    // 486.
    private static int Pb59(bool hw)
    {
        var bad = 0;
        foreach (var (fpu, nom) in Fpus)
        {
            var (_, m) = Acheminer(fpu, [.. Zero, 0xDD, 0x3F], 6);
            var attendu = hw ? 0xB084 : 0x3004;
            bad += Voir($"FDIVP 1/0, ZE démasquée ; FNSTSW m16 ({nom})", m == attendu, $"{m:X4}", $"{attendu:X4}", hw);
            (_, m) = Acheminer(fpu, [.. Zero, 0xDB, 0xE2, 0xDD, 0x3F], 7);
            bad += Voir($"… ; FNCLEX ; FNSTSW m16 ({nom})", m == 0x3000, $"{m:X4}", "3000", hw);
        }
        var (ip, mot) = Acheminer(5, [.. Zero, 0xDD, 0x3F, 0xF4], 8, avant: Ne);
        var att = hw ? (0x010C, 0xB084) : (0x010C, 0x3004);
        bad += Voir("NE posé ; FDIVP 1/0 ; FNSTSW m16 (486)", (ip, mot) == att, $"IP = {ip:X4}, {mot:X4}",
                    $"IP = {att.Item1:X4}, {att.Item2:X4}", hw);
        (ip, _) = Acheminer(5, [.. Zero, 0xD9, 0xE8, 0xF4], 8, avant: Ne);
        bad += Voir("NE posé ; FDIVP 1/0 ; FLD1 (486)", ip == (hw ? 0x0800 : 0x010C), $"IP = {ip:X4}",
                    hw ? "IP = 0800 (#MF)" : "IP = 010C", hw);
        // #MF est une faute : l'adresse de retour empilée est celle de FLD1, rejouée au retour.
        var sp = Registres[(int)R.SP];
        var retour = mem.ram[sp] | (mem.ram[sp + 1] << 8);
        if (hw)
            bad += Voir("NE posé ; #MF, l'adresse de retour (486)", (sp, retour) == (0xFFF8, 0x010A),
                        $"SP = {sp:X4}, [SP] = {retour:X4}", "SP = FFF8, [SP] = 010A", hw);
        (ip, _) = Acheminer(5, [.. Zero, 0x9B, 0xF4], 8, avant: Ne);
        bad += Voir("NE posé ; FDIVP 1/0 ; WAIT (486)", ip == (hw ? 0x0800 : 0x010B), $"IP = {ip:X4}",
                    hw ? "IP = 0800 (#MF)" : "IP = 010B", hw);
        // Les sept qui n'attendent pas, après l'exception : FNSTSW AX ; FNSTCW [BX+2] ; FNSTENV [BX+10h] ; FSETPM ;
        // FNSAVE [BX+30h] ; HLT en 0117h. Puis en a32 (387 et 486) : FNSTCW [EBX+2] ; FNSTENV [EBX+10h] ; FNSTSW [EBX+4] ;
        // FNSAVE [EBX+30h] ; HLT en 011Ah. Aucune n'est arrêtée (ni #MF sur le 486, NE posé ou non).
        byte[] a16 = [.. Zero, 0xDF, 0xE0, 0xD9, 0x7F, 0x02, 0xD9, 0x77, 0x10, 0xDB, 0xE4, 0xDD, 0x77, 0x30, 0xF4];
        byte[] a32 = [.. Zero, 0x67, 0xD9, 0x7B, 0x02, 0x67, 0xD9, 0x73, 0x10, 0x67, 0xDD, 0x7B, 0x04, 0x67, 0xDD, 0x73, 0x30,
                      0xF4];
        foreach (var (fpu, nom, ne) in new[] { (2, "287", false), (3, "287XL", false), (4, "387", false), (5, "486", false),
                                              (5, "486, NE posé", true) })
        {
            (ip, _) = Acheminer(fpu, a16, 14, avant: ne ? Ne : null);
            bad += Voir($"FDIVP 1/0 ; les sept sans attente ({nom})", ip == 0x0117, $"IP = {ip:X4}", "IP = 0117", hw);
            if (fpu < 4)
                continue;
            (ip, _) = Acheminer(fpu, a32, 13, avant: ne ? Ne : null);
            bad += Voir($"FDIVP 1/0 ; les quatre a32 sans attente ({nom})", ip == 0x011A, $"IP = {ip:X4}", "IP = 011A", hw);
        }
        return bad;
    }

    // PB-69 — sur un PC ou un XT, la sortie INT du 8087 va à la NMI, que le port A0h masque (AP-578 § 2.1) : MOV
    // AL,80h ; OUT A0h,AL ; FNINIT ; FLDCW [BX] (037Bh, IEM nul) ; FLD1 ; FLDZ ; FDIVP ; HLT → la NMI, IP = 0500h ;
    // PCem 010Eh. IEM posé (03FBh) ou A0h nul : 010Eh dans les deux modes. La NMI qui rend la main par IRET : 010Eh,
    // prise sur un front, une fois (en niveau, elle reviendrait à chaque IRET). A0h ouvert après l'exception : la NMI
    // vient alors ; FNCLEX ou FNINIT entre les deux : elle ne vient plus. Deux exceptions, ES encore posé à la seconde :
    // une seule NMI (le front).
    private static int Pb69(bool hw)
    {
        var bad = 0;
        byte[] A0(byte v) => [0xB0, v, 0xE6, 0xA0];
        nmi.nmi_init();
        try
        {
            byte[] hlt = [0xF4], iret = [0x46, 0xCF];
            (string nom, byte[] code, ushort cw, byte[] nmi, int pcem, int materiel)[] cas =
            [
                ("A0h = 80h ; FDIVP 1/0, IEM nul", [.. A0(0x80), .. Zero, 0xF4], 0x037B, hlt, 0x010E, 0x0500),
                ("A0h = 80h ; FDIVP 1/0, IEM posé", [.. A0(0x80), .. Zero, 0xF4], 0x03FB, hlt, 0x010E, 0x010E),
                ("A0h = 00h ; FDIVP 1/0, IEM nul", [.. A0(0x00), .. Zero, 0xF4], 0x037B, hlt, 0x010E, 0x010E),
                ("A0h = 80h ; FDIVP 1/0, la NMI rend la main (IRET)", [.. A0(0x80), .. Zero, 0xF4], 0x037B, iret, 0x010E,
                 0x010E),
                ("FDIVP 1/0 ; A0h = 80h", [.. Zero, .. A0(0x80), 0xF4], 0x037B, hlt, 0x010E, 0x0500),
                ("FDIVP 1/0 ; FNCLEX ; A0h = 80h", [.. Zero, 0xDB, 0xE2, .. A0(0x80), 0xF4], 0x037B, hlt, 0x0110, 0x0110),
                ("FDIVP 1/0 ; FNINIT ; A0h = 80h", [.. Zero, 0xDB, 0xE3, .. A0(0x80), 0xF4], 0x037B, hlt, 0x0110, 0x0110),
            ];
            foreach (var (nom, code, cw, nmi, pcem, materiel) in cas)
            {
                var (ip, _) = Acheminer(1, code, 10, cw, nmi: nmi);
                var attendu = hw ? materiel : pcem;
                bad += Voir($"{nom} (8087)", ip == attendu, $"IP = {ip:X4}", $"IP = {attendu:X4}", hw);
            }
            // Le front : la NMI (INC SI ; IRET) une fois pour deux exceptions, ES encore posé à la seconde ; PCem, aucune.
            Acheminer(1, [.. A0(0x80), .. Zero, 0xD9, 0xE8, 0xD9, 0xEE, 0xDE, 0xF9, 0xF4], 16, nmi: iret);
            var si = Registres[(int)R.SI];
            bad += Voir("A0h = 80h ; deux FDIVP 1/0, ES encore posé ; les NMI prises (8087)", si == (hw ? 1 : 0),
                        $"{si}", hw ? "1" : "0", hw);
        }
        finally
        {
            io.io_removehandler(0x00a0, 0x0001, null, null, null, nmi.nmi_write, null, null, null);
        }
        return bad;
    }

    // PB-204 — l'AT verrouille BUSY# sur ERROR# jusqu'à une écriture au port F0h (387 PRM, annexe F ; AP-578 § 2.2.1),
    // et F1h remet le coprocesseur à zéro. Le 287 et le 387 : après FDIVP 1/0, FLD1 (ou WAIT) attend, IP = 010Ah ; PCem
    // l'exécute. OUT F0h,AL ; FLD1 ; FNSTSW [BX] → A884h (TOP = 5, ZE, ES, B) ; PCem 2804h. FNINIT passe le verrou
    // (0000h dans les deux modes). FNINIT ; FLD1 ; OUT F1h,AL ; FNSTSW [BX] → 0000h ; PCem 3800h. Le 486, NE nul : FLD1
    // (ou WAIT) attend IGNNE#, que pose OUT F0h. IRQ13 en attente au second 8259 après l'exception : dans les deux
    // modes (le témoin). Une seconde exception, ES encore posé, n'a pas de front : FLD1 passe (dans les deux modes).
    private static int Pb204(bool hw)
    {
        var bad = 0;
        return Avec8259(() =>
        {
            model_c.at_coprocesseur_init();
            try
            {
                foreach (var (fpu, nom) in new[] { (2, "287"), (3, "287XL"), (4, "387"), (5, "486") })
                {
                    Pic(true);
                    var (ip, _) = Acheminer(fpu, [.. Zero, 0xD9, 0xE8, 0xF4], 8);
                    bad += Voir($"FDIVP 1/0 ; FLD1 ({nom})", ip == (hw ? 0x010A : 0x010C), $"IP = {ip:X4}",
                                hw ? "IP = 010A (attend)" : "IP = 010C", hw);
                    var irq13 = (pic.pic2.pend & 0x20) != 0;
                    bad += Voir($"FDIVP 1/0, IRQ13 en attente ({nom}, témoin)", irq13, $"{irq13}", "True", hw);
                    (ip, _) = Acheminer(fpu, [.. Zero, 0x9B, 0xF4], 8);
                    bad += Voir($"FDIVP 1/0 ; WAIT ({nom})", ip == (hw ? 0x010A : 0x010B), $"IP = {ip:X4}",
                                hw ? "IP = 010A (attend)" : "IP = 010B", hw);
                    var (_, m) = Acheminer(fpu, [.. Zero, 0xE6, 0xF0, 0xD9, 0xE8, 0xDD, 0x3F], 8);
                    bad += Voir($"FDIVP 1/0 ; OUT F0h ; FLD1 ; FNSTSW m16 ({nom})", m == (hw ? 0xA884 : 0x2804), $"{m:X4}",
                                hw ? "A884" : "2804", hw);
                    (ip, _) = Acheminer(fpu, [.. Zero, 0xE6, 0xF0, 0xD9, 0xE8, 0xD9, 0xEE, 0xDE, 0xF9, 0xD9, 0xE8, 0xF4], 13);
                    bad += Voir($"FDIVP 1/0 ; OUT F0h ; FDIVP 1/0, ES encore posé ; FLD1 ({nom})", ip == 0x0114,
                                $"IP = {ip:X4}", "IP = 0114 (sans front, ni verrou ni arrêt)", hw);
                    if (fpu == 5)
                        continue;
                    (_, m) = Acheminer(fpu, [.. Zero, 0xDB, 0xE3, 0xDD, 0x3F], 7);
                    bad += Voir($"FDIVP 1/0 ; FNINIT ; FNSTSW m16 ({nom})", m == 0x0000, $"{m:X4}", "0000", hw);
                    // Avec le noyau (demandé par « x87 » ou « tout »), F1h fait le RESET de PB-203 : le 387 en sort IE, ES et B
                    // posés (8081h).
                    var r387 = hw && fpu == 4 && materiel.pb_264;
                    (_, m) = Acheminer(fpu, [0xDB, 0xE3, 0xD9, 0xE8, 0xE6, 0xF1, 0xDD, 0x3F], 4);
                    var a = !hw ? 0x3800 : r387 ? 0x8081 : 0x0000;
                    bad += Voir($"FNINIT ; FLD1 ; OUT F1h ; FNSTSW m16 ({nom})", m == a, $"{m:X4}", $"{a:X4}", hw);
                    (_, m) = Acheminer(fpu, [.. Zero, 0xE6, 0xF1, 0xD9, 0xE8, 0xDD, 0x3F], 8);
                    a = !hw ? 0x2804 : r387 ? 0xB881 : 0x3800;
                    bad += Voir($"FDIVP 1/0 ; OUT F1h ; FLD1 ; FNSTSW m16 ({nom})", m == a, $"{m:X4}", $"{a:X4}", hw);
                }
            }
            finally
            {
                io.io_removehandler(0x00f0, 0x0002, null, null, null, _x87_materiel.ecrire_f0_f1, null, null, null);
            }
            return bad;
        });
    }

    // ===== Le noyau de 80 bits (G13.6, le noyau ; PB-203, PB-264 et leur groupe) =====
    //
    // Les premiers cas (N2) jouent la pile par la couche (x87_noyau.Materiel.cs) en mode matériel, par les instructions en
    // mode PCem. Le chemin suit le mode FIGÉ, et l'attendu --attendu : la panne injectée (l'attendu de l'autre mode)
    // rougit donc dans les deux sens.

    private static readonly (int fpu, string nom)[] FpusXL = [(1, "8087"), (2, "287"), (3, "287XL"), (4, "387"), (5, "486")];

    private static readonly extFloat80_t Un = new() { signExp = 0x3FFF, signif = 0x8000000000000000 };

    /// <summary>Joue `code` sur le coprocesseur `fpu` (le cœur du 8088, du 286, du 386 ou du 486), `avant` après le RESET
    /// du harnais, BX = 0200h ; rend les mots en DS:0200h et DS:0202h (FNSTSW [BX], FNSTCW [BX+2]).</summary>
    private static (ushort a, ushort b) Mots(int fpu, byte[] code, int pas, Action? avant = null)
    {
        Jouer87(fpu, code, pas, avant);
        return (W(0x200), W(0x202));
    }

    /// <summary>Joue `code` sur le coprocesseur `fpu` (le cœur du 8088, du 286, du 386 ou du 486), `avant` après le RESET
    /// du harnais, BX = 0200h ; rend les registres d'après. C3-C0 sont effacés après le RESET, comme à la mise sous
    /// tension : le 8087 et le 287 les gardent à travers le RESET et FNINIT (PB-202), et un cas hériterait sinon de ceux
    /// du précédent.</summary>
    private static ushort[] Jouer87(int fpu, byte[] code, int pas, Action? avant = null)
    {
        _386.FuzzFpu = fpu;
        var apres = () =>
        {
            _386_common.cpu_state.npxs &= unchecked((ushort)~0x4700);
            avant?.Invoke();
        };
        try
        {
            if (fpu == 1)
                return Jouer(code, r => r[(int)R.BX] = 0x200, apres, pas);
            return Jouer386(code, r => r[(int)R.BX] = 0x200, apres, coeur: Coeur(fpu), pas: pas);
        }
        finally
        {
            _386.FuzzFpu = 0;
        }
    }

    private static ushort W(int a) => (ushort)(mem.ram[a] | mem.ram[a + 1] << 8);

    private static ulong Q(int a) => (ulong)W(a) | (ulong)W(a + 2) << 16 | (ulong)W(a + 4) << 32 | (ulong)W(a + 6) << 48;

    /// <summary>Pose `octets` en DS:0300h (l'opérande des cas de N3a).</summary>
    private static Action Op(params byte[] octets) => () =>
    {
        for (var k = 0; k < octets.Length; k++)
            mem.ram[0x300 + k] = octets[k];
    };

    private static byte[] Le(ulong q) => BitConverter.GetBytes(q);

    private static byte[] Le80(ushort se, ulong sig) => [.. BitConverter.GetBytes(sig), .. BitConverter.GetBytes(se)];

    // ===== Le noyau, N3a : les mouvements de données =====
    //
    // Le code en 0000:0100, BX = 0200h, l'opérande en DS:0300h. FLD m64 [0300h] (DD 06 00 03), FLD m32 (D9 06 00 03), FLD
    // m80 (DB 2E 00 03), FBLD (DF 26 00 03) ; FSTP m64 [BX] (DD 1F), FSTP m80 [BX] (DB 3F), FBSTP [BX] (DF 37), FISTP
    // m16 et m32 [BX] (DF 1F, DB 1F) ; FNSTSW [BX+0Ah] (DD 7F 0A) et FNSTSW [BX] (DD 3F) ; FSTP m64 [BX+2] (DD 5F 02).

    private static readonly byte[] SwA = [0xDD, 0x7F, 0x0A];

    // PB-52 — FBLD (SDM vol. 2, FBLD) : FNINIT ; FLDZ ; FBLD = −12 345 ; FSTP m64 → C0C81C8000000000h ; PCem, FBLD
    // ILLEGAL, le zéro de FLDZ, 0. −0 : 8000000000000000h. En m80, exacts : 999 999 999 999 999 999 (403Ah
    // DE0B6B3A763FFFF0h) et 123 456 789 012 345 678, chiffres tous distincts (4037h DB4DA5D31879A700h).
    private static int Pb52(bool hw)
    {
        var bad = 0;
        foreach (var (fpu, nom) in FpusXL)
        {
            foreach (var (bcd, att, n) in new (byte[], ulong, string)[]
                     {
                         ([0x45, 0x23, 0x01, 0, 0, 0, 0, 0, 0, 0x80], 0xC0C81C8000000000, "−12 345"),
                         ([0, 0, 0, 0, 0, 0, 0, 0, 0, 0x80], 0x8000000000000000, "−0"),
                     })
            {
                Jouer87(fpu, [0xDB, 0xE3, 0xD9, 0xEE, 0xDF, 0x26, 0x00, 0x03, 0xDD, 0x1F], 4, Op(bcd));
                var a = hw ? att : 0UL;
                bad += Voir($"FLDZ ; FBLD {n} ; FSTP m64 ({nom})", Q(0x200) == a, $"{Q(0x200):X16}", $"{a:X16}", hw);
            }
            foreach (var (bcd, hse, hsig, n) in new (byte[], ushort, ulong, string)[]
                     {
                         ([0x99, 0x99, 0x99, 0x99, 0x99, 0x99, 0x99, 0x99, 0x99, 0x00], 0x403A, 0xDE0B6B3A763FFFF0, "10^18 − 1"),
                         ([0x78, 0x56, 0x34, 0x12, 0x90, 0x78, 0x56, 0x34, 0x12, 0x00], 0x4037, 0xDB4DA5D31879A700,
                          "123 456 789 012 345 678"),
                     })
            {
                Jouer87(fpu, [0xDB, 0xE3, 0xD9, 0xEE, 0xDF, 0x26, 0x00, 0x03, 0xDB, 0x3F], 4, Op(bcd));
                var (se, sig) = hw ? (hse, hsig) : ((ushort)0, 0UL);
                bad += Voir($"FLDZ ; FBLD {n} ; FSTP m80 ({nom})", W(0x208) == se && Q(0x200) == sig,
                            $"{W(0x208):X4} {Q(0x200):X16}", $"{se:X4} {sig:X16}", hw);
            }
        }
        return bad;
    }

    // PB-55 — FLD m80 charge les 80 bits tels quels (SDM vol. 2, FLD) : 2^1024 (43FFh 8000000000000000h) ; FSTP m80 → le
    // même ; PCem, l'exposant replié, 3FFFh 8000000000000000h. Un non normal (3FFFh 4000000000000000h) : le même ; PCem,
    // le bit entier ignoré, 1,5 : 3FFFh C000000000000000h.
    private static int Pb55(bool hw)
    {
        var bad = 0;
        foreach (var (fpu, nom) in FpusXL)
        foreach (var (se, sig, pse, psig) in new (ushort, ulong, ushort, ulong)[]
                 {
                     (0x43FF, 0x8000000000000000, 0x3FFF, 0x8000000000000000),
                     (0x3FFF, 0x4000000000000000, 0x3FFF, 0xC000000000000000),
                 })
        {
            Jouer87(fpu, [0xDB, 0xE3, 0xDB, 0x2E, 0x00, 0x03, 0xDB, 0x3F], 3, Op(Le80(se, sig)));
            var (ase, asig) = hw ? (se, sig) : (pse, psig);
            bad += Voir($"FLD m80 {se:X4} {sig:X16} ; FSTP m80 ({nom})", W(0x208) == ase && Q(0x200) == asig,
                        $"{W(0x208):X4} {Q(0x200):X16}", $"{ase:X4} {asig:X16}", hw);
        }
        return bad;
    }

    // PB-56 — FSTP m80 range les 80 bits (SDM vol. 1 § 8.1.2) : FLD m64 = 2^−1074 (le plus petit dénormal, chargé
    // normalisé, DE) ; FSTP m80 → 3BCDh 8000000000000000h, et le mot d'état 0002h (DE) ; PCem 3C00h 8000000000000800h,
    // 0000h. Le 8087 et le 287 le chargent en son non normal équivalent, 3C01h 0000000000000800h (N7b). La précision (N4a) : FNINIT ; FLD1 ; FDIV m64 3,0 ; FSTP m80 → 3FFDh AAAAAAAAAAAAAAABh, 64 bits arrondis au
    // plus près ; PCem, 53 bits, 3FFDh AAAAAAAAAAAAA800h.
    private static int Pb56(bool hw)
    {
        var bad = 0;
        foreach (var (fpu, nom) in FpusXL)
        {
            Jouer87(fpu, [0xDB, 0xE3, 0xDD, 0x06, 0x00, 0x03, 0xDB, 0x3F, .. SwA], 4, Op(Le(1)));
            var (se, sig, sw) = !hw ? ((ushort)0x3C00, 0x8000000000000800, (ushort)0)
                              : fpu < 3 ? ((ushort)0x3C01, 0x0000000000000800, (ushort)0x0002)
                              : ((ushort)0x3BCD, 0x8000000000000000, (ushort)0x0002);
            bad += Voir($"FLD m64 2^−1074 ; FSTP m80 ; FNSTSW ({nom})", W(0x208) == se && Q(0x200) == sig && W(0x20A) == sw,
                        $"{W(0x208):X4} {Q(0x200):X16}, {W(0x20A):X4}", $"{se:X4} {sig:X16}, {sw:X4}", hw);
            Jouer87(fpu, [0xDB, 0xE3, 0xD9, 0xE8, 0xDC, 0x36, 0x08, 0x03, 0xDB, 0x3F], 4, Ops((0x308, Trois)));
            var s = hw ? 0xAAAAAAAAAAAAAAAB : 0xAAAAAAAAAAAAA800;
            bad += Voir($"FLD1 ; FDIV m64 3 ; FSTP m80 ({nom})", W(0x208) == 0x3FFD && Q(0x200) == s,
                        $"{W(0x208):X4} {Q(0x200):X16}", $"3FFD {s:X16}", hw);
        }
        return bad;
    }

    private static readonly byte[] Trois = Le(0x4008000000000000);

    /// <summary>Pose chaque opérande à son adresse de DS (0300h, 0308h ; 0310h, le mot de contrôle de FLDCW ; 0320h, un
    /// second m80).</summary>
    private static Action Ops(params (int adr, byte[] octets)[] ops) => () =>
    {
        foreach (var (adr, octets) in ops)
            for (var k = 0; k < octets.Length; k++)
                mem.ram[adr + k] = octets[k];
    };

    private static byte[] Cw(ushort cw) => [(byte)cw, (byte)(cw >> 8)];

    // ===== Le noyau, N4a : l'arithmétique et les comparaisons =====
    //
    // FLDCW [0310h] (D9 2E 10 03), FDIV m64 [0308h] (DC 36 08 03), FADD m64 [0308h] (DC 06 08 03), FLD m64 [0308h] (DD 06
    // 08 03), FLD m80 [0320h] (DB 2E 20 03).

    // PB-48 — chaque opération suit RC (SDM vol. 1, 2003, § 8.1.4.3 ; IEEE 754-1985 § 4), et PC la précision des quatre
    // opérations (§ 8.1.4.2). Sous 0A7Fh (53 bits, vers +∞) : FLD1 ; FDIV m64 3 ; FSTP m64 → 3FD5555555555556h ; PCem, au
    // plus près, 3FD5555555555555h. De registre, FLD1 ; FLD m64 3 ; FDIVP ST(1), ST(0) (DE F9) : de même. Sous 077Fh (64
    // bits, vers −∞) : FLD1 ; FDIV m64 3 ; FST m32 → 3EAAAAAAh ; PCem 3EAAAAABh. Sous 0F7Fh (64 bits, vers zéro) : FLD m64
    // 2^−70 ; FLD1 ; FSUB ST(0), ST(1) (D8 E1) ; FSTP m80 → 3FFEh FFFFFFFFFFFFFFFFh ; PCem, en double, 1 : 3FFFh
    // 8000000000000000h. Le 8087 et le 287 aussi (Numerics Supplement, 1980, « Rounding Control »).
    private static int Pb48(bool hw)
    {
        var bad = 0;
        foreach (var (fpu, nom) in FpusXL)
        {
            foreach (var (code, pas, n) in new (byte[], int, string)[]
                     {
                         ([0xD9, 0xE8, 0xDC, 0x36, 0x08, 0x03], 2, "FLD1 ; FDIV m64 3"),
                         ([0xD9, 0xE8, 0xDD, 0x06, 0x08, 0x03, 0xDE, 0xF9], 3, "FLD1 ; FLD m64 3 ; FDIVP ST(1), ST(0)"),
                     })
            {
                Jouer87(fpu, [0xDB, 0xE3, 0xD9, 0x2E, 0x10, 0x03, .. code, 0xDD, 0x1F], pas + 3, Ops((0x308, Trois), (0x310, Cw(0x0A7F))));
                var a = hw ? 0x3FD5555555555556UL : 0x3FD5555555555555UL;
                bad += Voir($"CW 0A7Fh ; {n} ; FSTP m64 ({nom})", Q(0x200) == a, $"{Q(0x200):X16}", $"{a:X16}", hw);
            }
            Jouer87(fpu, [0xDB, 0xE3, 0xD9, 0x2E, 0x10, 0x03, 0xD9, 0xE8, 0xDC, 0x36, 0x08, 0x03, 0xD9, 0x17], 5,
                    Ops((0x308, Trois), (0x310, Cw(0x077F))));
            var f = (uint)(W(0x200) | W(0x202) << 16);
            var af = hw ? 0x3EAAAAAAu : 0x3EAAAAABu;
            bad += Voir($"CW 077Fh ; FLD1 ; FDIV m64 3 ; FST m32 ({nom})", f == af, $"{f:X8}", $"{af:X8}", hw);
            Jouer87(fpu, [0xDB, 0xE3, 0xD9, 0x2E, 0x10, 0x03, 0xDD, 0x06, 0x08, 0x03, 0xD9, 0xE8, 0xD8, 0xE1, 0xDB, 0x3F], 6,
                    Ops((0x308, Le(0x3B90000000000000)), (0x310, Cw(0x0F7F))));
            var (se, sig) = hw ? ((ushort)0x3FFE, 0xFFFFFFFFFFFFFFFF) : ((ushort)0x3FFF, 0x8000000000000000);
            bad += Voir($"CW 0F7Fh ; FLD m64 2^−70 ; FLD1 ; FSUB ST(0), ST(1) ; FSTP m80 ({nom})", W(0x208) == se && Q(0x200) == sig,
                        $"{W(0x208):X4} {Q(0x200):X16}", $"{se:X4} {sig:X16}", hw);
        }
        return bad;
    }

    // PB-60 — deux NaN : le plus grand significande survit (SDM vol. 1, table 4-7 ; Numerics Supplement, « NANs »), quel
    // que soit l'ordre des opérandes ; PCem suit celui que GCC a choisi. A = 7FF8000000000001h, B = 7FF8000000000002h.
    // FLD m64 B ; FLD m64 A × 5 ; FADD ST(5), ST(0) (DC C5) ; FLD ST(5) ; FSTP m64 → B ; PCem A. FLD m64 B ; FADD m64 A ;
    // FSTP m64 → B ; PCem, la mémoire, A. FLD m64 A ; FLD m64 B ; FMUL ST(0), ST(1) (D8 C9) ; FSTP m64 → B ; PCem, ST(1), A.
    private static int Pb60(bool hw)
    {
        var bad = 0;
        var a = Le(0x7FF8000000000001);
        var b = Le(0x7FF8000000000002);
        foreach (var (fpu, nom) in FpusXL)
        {
            foreach (var (code, pas, op, n) in new (byte[], int, Action, string)[]
                     {
                         ([0xDD, 0x06, 0x00, 0x03, .. Enumerable.Repeat<byte[]>([0xDD, 0x06, 0x08, 0x03], 5).SelectMany(x => x),
                           0xDC, 0xC5, 0xD9, 0xC5], 8, Ops((0x300, b), (0x308, a)), "FLD B ; FLD A × 5 ; FADD ST(5), ST(0) ; FLD ST(5)"),
                         ([0xDD, 0x06, 0x00, 0x03, 0xDC, 0x06, 0x08, 0x03], 2, Ops((0x300, b), (0x308, a)), "FLD B ; FADD m64 A"),
                         ([0xDD, 0x06, 0x00, 0x03, 0xDD, 0x06, 0x08, 0x03, 0xD8, 0xC9], 3, Ops((0x300, a), (0x308, b)),
                          "FLD A ; FLD B ; FMUL ST(0), ST(1)"),
                     })
            {
                Jouer87(fpu, [0xDB, 0xE3, .. code, 0xDD, 0x1F], pas + 2, op);
                var att = hw ? 0x7FF8000000000002UL : 0x7FF8000000000001UL;
                bad += Voir($"{n} ; FSTP m64 ({nom})", Q(0x200) == att, $"{Q(0x200):X16}", $"{att:X16}", hw);
            }
        }
        return bad;
    }

    // PB-195 — FBSTP (SDM vol. 2, FBSTP ; vol. 1 § 4.7) : 12,7 au plus près → 13, PE (et C1 sur le 387, le 486 et le
    // 287XL : 0220h ; 0020h) ; PCem 12 et 0000h. 10^18 → l'indéfini BCD FFFF C000000000000000h et IE (0001h) ; PCem 00 × 9
    // puis 01. −13,5 vers zéro (RC = 11) → −13 (au plus près : −14 ; PE : 0020h ; PCem tronque aussi, 0000h). En mode
    // matériel, depuis un m80 exact : 123 456 789 012 345 678 → ses chiffres, et 10^18 − 1, la borne, → 99 × 9 ; ni PE ni
    // IE.
    private static int Pb195(bool hw)
    {
        var bad = 0;
        if (materiel.pb_195 && hw)
            foreach (var (fpu, nom) in FpusXL)
            foreach (var (se, sig, bas, haut) in new (ushort, ulong, ulong, ushort)[]
                     {
                         (0x4037, 0xDB4DA5D31879A700, 0x3456789012345678, 0x0012),
                         (0x403A, 0xDE0B6B3A763FFFF0, 0x9999999999999999, 0x0099),
                     })
            {
                Jouer87(fpu, [0xDB, 0xE3, 0xDB, 0x2E, 0x00, 0x03, 0xDF, 0x37, .. SwA], 4, Op(Le80(se, sig)));
                bad += Voir($"FLD m80 {se:X4} {sig:X16} ; FBSTP ({nom})", Q(0x200) == bas && W(0x208) == haut && W(0x20A) == 0,
                            $"{W(0x208):X4} {Q(0x200):X16}, {W(0x20A):X4}", $"{haut:X4} {bas:X16}, 0000", hw);
            }
        foreach (var (fpu, nom) in FpusXL)
        {
            var c1 = fpu >= 3 ? 0x0200 : 0;
            foreach (var (v, cw, hse, hsig, hsw, pse, psig) in new (ulong, ushort, ushort, ulong, int, ushort, ulong)[]
                     {
                         (0x4029666666666666, 0x037F, 0x0000, 0x13, 0x0020 | c1, 0x0000, 0x12),
                         (0x43ABC16D674EC800, 0x037F, 0xFFFF, 0xC000000000000000, 0x0001, 0x0100, 0x0),
                         (0xC02B000000000000, 0x0F7F, 0x8000, 0x13, 0x0020, 0x8000, 0x13),
                     })
            {
                Jouer87(fpu, [0xDB, 0xE3, 0xD9, 0x2E, 0x10, 0x03, 0xDD, 0x06, 0x00, 0x03, 0xDF, 0x37, .. SwA], 5,
                        () => { Op(Le(v))(); mem.ram[0x310] = (byte)cw; mem.ram[0x311] = (byte)(cw >> 8); });
                var (se, sig, sw) = hw ? (hse, hsig, hsw) : (pse, psig, 0);
                bad += Voir($"CW {cw:X4} ; FLD m64 {v:X16} ; FBSTP ; FNSTSW ({nom})",
                            W(0x208) == se && Q(0x200) == sig && W(0x20A) == sw,
                            $"{W(0x208):X4} {Q(0x200):X16}, {W(0x20A):X4}", $"{se:X4} {sig:X16}, {sw:X4}", hw);
            }
        }
        return bad;
    }

    // PB-196 — FIST hors bornes (SDM vol. 2, FIST/FISTP) : 40 000 ; FISTP m16 → 8000h et IE ; PCem 9C40h. 3·10^9 ; FISTP
    // m32 → 80000000h et IE ; PCem B2D05E00h. Les bornes : −32 768 → 8000h, sans IE (témoin) ; 32 767,5, au plus près
    // 32 768 → 8000h et IE (PCem, sans IE) ; 32 767,4 → 7FFFh et PE (PCem, sans PE).
    private static int Pb196(bool hw)
    {
        var bad = 0;
        foreach (var (fpu, nom) in FpusXL)
        foreach (var (v, op, hv, hsw, pv, n) in new (ulong, byte, uint, int, uint, string)[]
                 {
                     (0x40E3880000000000, 0xDF, 0x8000, 0x0001, 0x9C40, "40 000 ; FISTP m16"),
                     (0x41E65A0BC0000000, 0xDB, 0x80000000, 0x0001, 0xB2D05E00, "3·10^9 ; FISTP m32"),
                     (0xC0E0000000000000, 0xDF, 0x8000, 0x0000, 0x8000, "−32 768, la borne ; FISTP m16"),
                     (0x40DFFFE000000000, 0xDF, 0x8000, 0x0001, 0x8000, "32 767,5, arrondi à 32 768 ; FISTP m16"),
                     (0x40DFFFD99999999A, 0xDF, 0x7FFF, 0x0020, 0x7FFF, "32 767,4 ; FISTP m16"),
                 })
        {
            Jouer87(fpu, [0xDB, 0xE3, 0xDD, 0x06, 0x00, 0x03, op, 0x1F, .. SwA], 4, Op(Le(v)));
            var r = op == 0xDF ? W(0x200) : (uint)(W(0x200) | W(0x202) << 16);
            var (a, sw) = hw ? (hv, hsw) : (pv, 0);
            bad += Voir($"{n} ; FNSTSW ({nom})", r == a && W(0x20A) == sw, $"{r:X}, {W(0x20A):X4}", $"{a:X}, {sw:X4}", hw);
        }
        return bad;
    }

    // PB-206 et PB-212 — FLD m32 et m64 d'un SNaN (SDM vol. 2, FLD) : sur le 387, le 486 et le 287XL, IE (le mot d'état
    // 3801h, TOP = 7), et le QNaN : FSTP m64 → 7FF8000020000000h (m32 7F800001h) et 7FF8000000000001h (m64
    // 7FF0000000000001h) ; le 8087 et le 287 ne lèvent rien au chargement (3800h) et gardent les bits signalants
    // (inconnu). Le registre est relu par FSTP m80, sans conversion : 7FFFh C000010000000000h (m32) et C000000000000800h
    // (m64) ; le 8087 et le 287, 8000010000000000h et 8000000000000800h ; PCem 3800h, C000010000000000h (cvtss2sd rend le
    // NaN silencieux) et 8000000000000800h (gardé signalant).
    private static int Pb206(bool hw) =>
        Snan(hw, [0xD9, 0x06, 0x00, 0x03], Le(0x7F800001)[..4], 0xC000010000000000, 0x8000010000000000, 0xC000010000000000);

    private static int Pb212(bool hw) =>
        Snan(hw, [0xDD, 0x06, 0x00, 0x03], Le(0x7FF0000000000001), 0xC000000000000800, 0x8000000000000800, 0x8000000000000800);

    /// <summary>FNINIT ; FLD ; FNSTSW [BX] ; FSTP m80 [BX+2] : le registre chargé, lu sans conversion. `h387` : le QNaN du
    /// 387, du 486 et du 287XL ; `h16` : les bits étendus du 8087 et du 287 ; `p` : PCem.</summary>
    private static int Snan(bool hw, byte[] fld, byte[] op, ulong h387, ulong h16, ulong p)
    {
        var bad = 0;
        foreach (var (fpu, nom) in FpusXL)
        {
            Jouer87(fpu, [0xDB, 0xE3, .. fld, 0xDD, 0x3F, 0xDB, 0x7F, 0x02], 4, Op(op));
            var sw = hw && fpu >= 3 ? 0x3801 : 0x3800;
            var q = !hw ? p : fpu >= 3 ? h387 : h16;
            bad += Voir($"FLD d'un SNaN ; FNSTSW ; FSTP m80 ({nom})", W(0x200) == sw && W(0x20A) == 0x7FFF && Q(0x202) == q,
                        $"{W(0x200):X4}, {W(0x20A):X4} {Q(0x202):X16}", $"{sw:X4}, 7FFF {q:X16}", hw);
        }
        return bad;
    }

    // PB-203 — au RESET, l'état initial du type (387 PRM § 6.2.5 et annexe C ; 287 PRM, table 3-1 ; Numerics Supplement,
    // table S-7 ; 486 PRM § 19.2.3 et table 10-1). Le RESET du harnais (resetx86), puis FNSTSW [BX] ; FNSTCW [BX+2] : le
    // 8087 03FFh et 0000h, le 287 et le 287XL 037Fh et 0000h, le 387 037Eh et 8081h (IM à 0 ; IE, ES et B), le 486
    // 037Fh et 0000h (la mise sous tension) ; PCem 0000h et 0000h, le zéro de sa mise sous tension. Puis un second RESET
    // (resetx86) sur un état posé (le mot de contrôle 0040h, PE) : les mêmes mots, sauf le 486, que le RESET ne touche
    // pas (0040h et 0020h, comme PCem). C3-C0 restent hors du cas : le 8087 et le 287 les gardent (PB-202, N7). Avec
    // l'acheminement (PB-204), le port F1h de l'AT remet le 287 et le 387 dans leur état de RESET (le cas de PB-264).
    private static int Pb203(bool hw)
    {
        var bad = 0;
        byte[] code = [0xDD, 0x3F, 0xD9, 0x7F, 0x02];
        foreach (var (fpu, nom) in FpusXL)
        {
            var (sw, cw) = Mots(fpu, code, 2);
            var (acw, asw) = !hw ? (0x0000, 0x0000) : fpu switch
            {
                1 => (0x03FF, 0x0000),
                4 => (0x037E, 0x8081),
                _ => (0x037F, 0x0000),
            };
            bad += Voir($"RESET ; FNSTSW ; FNSTCW ({nom})", (cw, sw) == (acw, asw), $"{cw:X4} et {sw:X4}",
                        $"{acw:X4} et {asw:X4}", hw);
            (sw, cw) = Mots(fpu, code, 2, () =>
            {
                _386_common.cpu_state.npxc = 0x0040;
                _386_common.cpu_state.npxs = 0x0020;
                _808x.resetx86();
            });
            if (!hw || fpu == 5)
                (acw, asw) = (0x0040, 0x0020);
            bad += Voir($"CW 0040h, PE ; RESET ; FNSTSW ; FNSTCW ({nom})", (cw, sw) == (acw, asw), $"{cw:X4} et {sw:X4}",
                        $"{acw:X4} et {asw:X4}", hw);
        }
        return bad;
    }

    // PB-264 — la faute de pile (SDM vol. 1 § 8.5.1.1 ; 387 PRM ; 287 PRM). Le débordement : FNINIT, neuf empilements de
    // 1, FNSTSW [BX] → le 387, le 486 et le 287XL 3A41h (TOP = 7 ; IE, SF, C1), le 8087 et le 287 3801h (IE) ; ST(0)
    // l'indéfini ; PCem (neuf FLD1) 3800h, ST(0) = 1. Le sous-débordement, C1 posé : ST(1) lu vide → 0041h (IE, SF ; C1
    // à 0), le 8087 et le 287 0201h ; l'opérande lu, l'indéfini ; PCem (FADD ST,ST(1)) 0200h. Démasquée (IM à 0) : la
    // lecture s'arrête, ES et B posés (8081h, ou 80C1h avec SF) ; PCem 0000h ; sans l'acheminement, rien ne part vers la
    // carte (le 8087 compris), avec lui (--hardware-mode PB-204,PB-264), le cas Acheminement. Puis la couche seule (le
    // mode matériel) : les classes d'un opérande, le mot de contrôle vers SoftFloat, ses indicateurs vers le mot d'état.
    private static int Pb264(bool hw)
    {
        var bad = 0;
        var noyau = materiel.pb_264;
        foreach (var (fpu, nom) in FpusXL)
        {
            var sf = fpu >= 3;
            ushort sw;
            bool indefini;
            if (noyau)
            {
                (sw, _) = Mots(fpu, [0xDD, 0x3F], 1, () =>
                {
                    _x87_noyau.finit();
                    for (var k = 0; k < 9; k++)
                        _x87_noyau.empiler(Un);
                });
                indefini = Egal(_x87_noyau.R[_x87_noyau.phys(0)], _x87_noyau.Indefini);
            }
            else
            {
                (sw, _) = Mots(fpu, [0xDB, 0xE3, .. Enumerable.Repeat<byte[]>([0xD9, 0xE8], 9).SelectMany(b => b), 0xDD, 0x3F], 11);
                indefini = double.IsNaN(_386.ST(0));
            }
            var att = !hw ? 0x3800 : sf ? 0x3A41 : 0x3801;
            bad += Voir($"neuf empilements ; FNSTSW ({nom})", sw == att && indefini == hw, $"{sw:X4}, ST(0) {(indefini ? "indéfini" : "non")}",
                        $"{att:X4}, ST(0) {(hw ? "indéfini" : "non")}", hw);

            if (noyau)
            {
                var lu = Un;
                (sw, _) = Mots(fpu, [0xDD, 0x3F], 1, () =>
                {
                    _x87_noyau.finit();
                    _386_common.cpu_state.npxs = 0x0200;
                    _x87_noyau.lire(1, out lu);
                });
                indefini = Egal(lu, _x87_noyau.Indefini);
            }
            else
            {
                (sw, _) = Mots(fpu, [0xD8, 0xC1, 0xDD, 0x3F], 2, () => _386_common.cpu_state.npxs = 0x0200);
                indefini = false;
            }
            att = !hw ? 0x0200 : sf ? 0x0041 : 0x0201;
            bad += Voir($"C1 ; ST(1) lu vide ; FNSTSW ({nom})", sw == att && indefini == hw, $"{sw:X4}, l'opérande {(indefini ? "indéfini" : "non")}",
                        $"{att:X4}, l'opérande {(hw ? "indéfini" : "non")}", hw);

            if (fpu == 1 && materiel.pb_59)
                continue;
            var arrete = false;
            bad += Avec8259(() =>
            {
                Pic(true);
                if (noyau)
                    (sw, _) = Mots(fpu, [0xDD, 0x3F], 1, () =>
                    {
                        _x87_noyau.finit();
                        _386_common.cpu_state.npxc = 0x037E;
                        arrete = _x87_noyau.lire(1, out _);
                    });
                else
                    (sw, _) = Mots(fpu, [0xDB, 0xE3, 0xD9, 0x2F, 0xD8, 0xC1, 0xDD, 0x3F], 4,
                                   () => { mem.ram[0x200] = 0x7E; mem.ram[0x201] = 0x03; });
                _x87_materiel.verrou = _x87_materiel.ignne = false;
                att = !hw ? 0x0000 : sf ? 0x80C1 : 0x8081;
                return Voir($"IM à 0 ; ST(1) lu vide ; FNSTSW ({nom})", sw == att && arrete == hw,
                            $"{sw:X4}, {(arrete ? "arrêtée" : "continue")}", $"{att:X4}, {(hw ? "arrêtée" : "continue")}", hw);
            });
        }
        bad += Controle(hw, noyau);
        if (noyau && hw)
            bad += Avec8259(() =>
            {
                Pic(true);
                var b = Couche();
                _x87_materiel.verrou = _x87_materiel.ignne = false;
                return b;
            });
        if (noyau && materiel.pb_204)
            bad += Acheminement(hw);
        if (noyau && hw)
            bad += Mouvements();
        if (noyau && hw)
            bad += Arithmetique();
        if (noyau && hw)
            bad += Reductions();
        if (noyau && hw)
            bad += Demasquees();
        if (noyau && hw)
            bad += Transcendantes();
        if (noyau && hw)
            bad += SeizeBits();
        if (noyau && hw)
            bad += NonNormaux();
        return bad;
    }

    // ===== Le noyau, N4b : FRNDINT, FSCALE, FXTRACT, FPREM et FPREM1 =====
    //
    // FLD m64 [0308h] (DD 06 08 03) charge ST(1) ; puis FLD1 ou FLD m64 [0300h] (DD 06 00 03) ST(0) ; FSTP m64 [BX] (DD
    // 1F), FSTP m64 [BX+8] (DD 5F 08).

    /// <summary>FNINIT ; FLD m64 b (ST(1)) ; FLD m64 a (ST(0)) ; `op` ; FNSTSW [BX+0Ah] ; FSTP m64 [BX] : le mot d'état et
    /// le résultat.</summary>
    private static (ushort sw, ulong r) Deux(int fpu, ulong a, ulong b, byte op)
    {
        Jouer87(fpu, [0xDB, 0xE3, 0xDD, 0x06, 0x08, 0x03, 0xDD, 0x06, 0x00, 0x03, 0xD9, op, .. SwA, 0xDD, 0x1F], 6,
                Ops((0x300, Le(a)), (0x308, Le(b))));
        return (W(0x20A), Q(0x200));
    }

    // PB-65 — FPREM et FPREM1 (387 PRM § 4.4.9, § 4.4.10 et table 4-4). FPREM de 1 par 0,1 (3FB999999999999Ah) : Q = 9,
    // le reste exact, 3FB9999999999996h, C3 C1 C0 = 010 (3200h, TOP = 6) ; PCem, Q par un double arrondi, 10, 0 et C3
    // (7000h). FPREM1 du même couple : Q = 10, le reste −2^−54 (BC90000000000000h), C3 (7000h) ; PCem, FPREM, 0. FPREM1 de
    // 3 par 2 : 1,5, au pair, Q = 2, −1 (BFF0000000000000h, 7000h) ; PCem, FPREM, 1 et C1 (3200h). FPREM de 2^100 par 3 :
    // une réduction partielle, C2 (3400h) ; D = 99, N = 32 + 3 (un choix : PB-65) : 2^100 − 3 × 2^64 × ⌊2^36 / 3⌋ = 2^64
    // (43F0000000000000h) ; PCem, C2 jamais posé.
    private static int Pb65(bool hw)
    {
        var bad = 0;
        foreach (var (fpu, nom) in FpusXL)
        foreach (var (a, b, op, hsw, hr, psw, pr, n) in new (ulong, ulong, byte, ushort, ulong, ushort, ulong, string)[]
                 {
                     (0x3FF0000000000000, 0x3FB999999999999A, 0xF8, 0x3200, 0x3FB9999999999996, 0x7000, 0, "FPREM 1 par 0,1"),
                     (0x3FF0000000000000, 0x3FB999999999999A, 0xF5, 0x7000, 0xBC90000000000000, 0x7000, 0, "FPREM1 1 par 0,1"),
                     (0x4008000000000000, 0x4000000000000000, 0xF5, 0x7000, 0xBFF0000000000000, 0x3200, 0x3FF0000000000000,
                      "FPREM1 3 par 2"),
                     (0x4630000000000000, 0x4008000000000000, 0xF8, 0x3400, 0x43F0000000000000, 0x3000, 0xFFFFFFFFFFFFFFFF,
                      "FPREM 2^100 par 3"),
                 })
        {
            var (sw, r) = Deux(fpu, a, b, op);
            var (asw, ar) = hw ? (hsw, hr) : (psw, pr);
            var ok = sw == asw && (ar == 0xFFFFFFFFFFFFFFFF || r == ar);
            bad += Voir($"{n} ; FNSTSW ; FSTP m64 ({nom})", ok, $"{sw:X4}, {r:X16}", $"{asw:X4}, {(ar == 0xFFFFFFFFFFFFFFFF ? "—" : ar.ToString("X16"))}", hw);
        }
        return bad;
    }

    // PB-194 — FXTRACT (387 PRM § 4.4.12) : FLDZ ; FLD m64 8 ; FXTRACT ; FSTP m64 ; FSTP m64 → le significande 1
    // (3FF0000000000000h), puis l'exposant 3 (4008000000000000h) ; PCem, D9 F4 ILLEGAL : 8 puis le zéro de FLDZ.
    private static int Pb194(bool hw)
    {
        var bad = 0;
        foreach (var (fpu, nom) in FpusXL)
        {
            Jouer87(fpu, [0xDB, 0xE3, 0xD9, 0xEE, 0xDD, 0x06, 0x00, 0x03, 0xD9, 0xF4, 0xDD, 0x1F, 0xDD, 0x5F, 0x08], 6,
                    Ops((0x300, Le(0x4020000000000000))));
            var (a, b) = hw ? (0x3FF0000000000000UL, 0x4008000000000000UL) : (0x4020000000000000UL, 0UL);
            bad += Voir($"FLDZ ; FLD m64 8 ; FXTRACT ; FSTP m64 × 2 ({nom})", Q(0x200) == a && Q(0x208) == b,
                        $"{Q(0x200):X16} {Q(0x208):X16}", $"{a:X16} {b:X16}", hw);
        }
        return bad;
    }

    // PB-197 — FRNDINT de 2^63 (43E0000000000000h) et de +∞ : eux-mêmes (SDM vol. 2, FRNDINT) ; PCem C3E0000000000000h.
    private static int Pb197(bool hw) => Rndint(hw, [(0x43E0000000000000, "2^63"), (0x7FF0000000000000, "+∞")], 0xC3E0000000000000);

    // PB-211 — FRNDINT de −0,3 (BFD3333333333333h) et de −0 : −0 (IEEE 754-1985 § 6.3) ; PCem +0.
    private static int Pb211(bool hw) => Rndint(hw, [(0xBFD3333333333333, "−0,3"), (0x8000000000000000, "−0")], 0, 0x8000000000000000);

    /// <summary>FNINIT ; FLD m64 v ; FRNDINT ; FSTP m64 : en mode matériel `materiel` (ou v), chez PCem `pcem`.</summary>
    private static int Rndint(bool hw, (ulong v, string n)[] cas, ulong pcem, ulong? materiel = null)
    {
        var bad = 0;
        foreach (var (fpu, nom) in FpusXL)
        foreach (var (v, n) in cas)
        {
            Jouer87(fpu, [0xDB, 0xE3, 0xDD, 0x06, 0x00, 0x03, 0xD9, 0xFC, 0xDD, 0x1F], 4, Ops((0x300, Le(v))));
            var a = hw ? materiel ?? v : pcem;
            bad += Voir($"FLD m64 {n} ; FRNDINT ; FSTP m64 ({nom})", Q(0x200) == a, $"{Q(0x200):X16}", $"{a:X16}", hw);
        }
        return bad;
    }

    // PB-265 — l'arrondi au plus près de PCem arrondit b + 1,0 : FRNDINT de −2^−60 (BC30000000000000h) → −0 ; PCem +1
    // (3FF0000000000000h). FRNDINT de 2 − 2^−52 (3FFFFFFFFFFFFFFFh) → 2 ; PCem 1. FISTP m16 du même → 2 ; PCem 1.
    private static int Pb265(bool hw)
    {
        var bad = Rndint(hw, [(0xBC30000000000000, "−2^−60")], 0x3FF0000000000000, 0x8000000000000000)
                + Rndint(hw, [(0x3FFFFFFFFFFFFFFF, "2 − 2^−52")], 0x3FF0000000000000, 0x4000000000000000);
        foreach (var (fpu, nom) in FpusXL)
        {
            Jouer87(fpu, [0xDB, 0xE3, 0xDD, 0x06, 0x00, 0x03, 0xDF, 0x1F], 3, Ops((0x300, Le(0x3FFFFFFFFFFFFFFF))));
            var a = hw ? 2 : 1;
            bad += Voir($"FLD m64 2 − 2^−52 ; FISTP m16 ({nom})", W(0x200) == a, $"{W(0x200)}", $"{a}", hw);
        }
        return bad;
    }

    // PB-198 — FSCALE de 1 par ST(1) = NaN (7FF8000000000000h) : le NaN ; par +∞ : +∞ (387 PRM, table 3-4), l'indéfini
    // sur le 8087 et le 287 (287 PRM, table 1-12 : « ± Y scaled by ± ∞ : Invalid operation ») ; PCem 0.
    private static int Pb198(bool hw)
    {
        var bad = 0;
        foreach (var (fpu, nom) in FpusXL)
        foreach (var (b, n) in new (ulong, string)[] { (0x7FF8000000000000, "NaN"), (0x7FF0000000000000, "+∞") })
        {
            var (_, r) = Deux(fpu, 0x3FF0000000000000, b, 0xFD);
            var a = !hw ? 0UL : fpu < 3 && n == "+∞" ? 0xFFF8000000000000 : b;
            bad += Voir($"FSCALE 1 par {n} ; FSTP m64 ({nom})", r == a, $"{r:X16}", $"{a:X16}", hw);
        }
        return bad;
    }

    /// <summary>Les transcendantes du noyau (N6), sur le 387, aux attentes des sources (SDM vol. 2, 2016, F2XM1, FYL2X,
    /// FPTAN, FPATAN, FSIN, FCOS, FSINCOS et leurs tables) et de la référence (tools/x87ref/transcendantes.py) : la pile
    /// (FPTAN et FSINCOS empilent, FYL2X et FPATAN dépilent), l'ordre de FSINCOS, C2, C1, les spéciaux exacts. FNINIT ;
    /// FLDCW [0310h] ; FLDZ ; FLD m64 [0308h] (ST(1)) ; FLD m64 [0300h] (ST(0)) ; D9 op ; FNSTSW [BX+0Ah] ; FSTP m80 [BX] ;
    /// FSTP m80 [BX+0Ch]. TOP : 5 avant, 4 après un empilement, 6 après un dépilement.</summary>
    private static int Transcendantes()
    {
        var bad = 0;
        foreach (var (a, b, op, sw, s0, g0, s1, g1, n) in new (ulong, ulong, byte, ushort, ushort, ulong, ushort, ulong, string)[]
                 {
                     (0x3FE0000000000000, 0, 0xF2, 0x2220, 0x3FFF, 0x8000000000000000, 0x3FFE, 0x8BDA7ADF9A3A5219,
                      "FPTAN 0,5 (1,0 empilé sur tan 0,5 ; PE, C1)"),
                     (0x3FF0000000000000, 0, 0xFB, 0x2220, 0x3FFE, 0x8A51407DA8345C92, 0x3FFE, 0xD76AA47848677021,
                      "FSINCOS 1 (cos 1 empilé sur sin 1 ; PE, C1 du cosinus)"),
                     (0x3FF0000000000000, 0x3FF0000000000000, 0xF3, 0x3220, 0x3FFE, 0xC90FDAA22168C235, 0, 0,
                      "FPATAN 1 / 1 (π/4, dépilé ; PE, C1)"),
                     (0x8000000000000000, 0, 0xF3, 0x3220, 0x4000, 0xC90FDAA22168C235, 0, 0, "FPATAN +0 / −0 (+π, sans IE)"),
                     (0xFFF0000000000000, 0x7FF0000000000000, 0xF3, 0x3220, 0x4000, 0x96CBE3F9990E91A8, 0, 0,
                      "FPATAN +∞ / −∞ (+3π/4, sans IE)"),
                     (0x4020000000000000, 0x4008000000000000, 0xF1, 0x3000, 0x4002, 0x9000000000000000, 0, 0,
                      "FYL2X 3 × log₂ 8 (9, exact)"),
                     (0, 0x4000000000000000, 0xF1, 0x3004, 0xFFFF, 0x8000000000000000, 0, 0, "FYL2X 2 × log₂ +0 (ZE, −∞)"),
                     (0x43F0000000000000, 0, 0xFE, 0x2C00, 0x403F, 0x8000000000000000, 0, 0, "FSIN 2^64 (C2, inchangé)"),
                     (0x43E0000000000000, 0, 0xF2, 0x2C00, 0x403E, 0x8000000000000000, 0, 0, "FPTAN 2^63 (C2, rien d'empilé)"),
                     (0x8000000000000000, 0, 0xFF, 0x2800, 0x3FFF, 0x8000000000000000, 0, 0, "FCOS −0 (+1, exact)"),
                     (0x7FF0000000000000, 0, 0xFE, 0x2C01, 0xFFFF, 0xC000000000000000, 0, 0,
                      "FSIN +∞ (IE, l'indéfini, C2 : 387 PRM, table 3-10)"),
                     (0xFFF0000000000000, 0, 0xF2, 0x2401, 0xFFFF, 0xC000000000000000, 0xFFFF, 0xC000000000000000,
                      "FPTAN −∞ (IE, l'indéfini aux deux places, C2)"),
                     (0xBFF0000000000000, 0, 0xF0, 0x2800, 0xBFFE, 0x8000000000000000, 0, 0, "F2XM1 −1 (−0,5, exact)"),
                 })
        {
            Jouer87(4, [0xDB, 0xE3, 0xD9, 0x2E, 0x10, 0x03, 0xD9, 0xEE, 0xDD, 0x06, 0x08, 0x03, 0xDD, 0x06, 0x00, 0x03, 0xD9, op,
                        .. SwA, 0xDB, 0x3F, 0xDB, 0x7F, 0x0C], 9, Ops((0x300, Le(a)), (0x308, Le(b)), (0x310, Cw(0x037F))));
            bad += Voir($"{n} ; FNSTSW ; FSTP m80 × 2 (387)",
                        W(0x20A) == sw && W(0x208) == s0 && Q(0x200) == g0 && W(0x214) == s1 && Q(0x20C) == g1,
                        $"{W(0x20A):X4}, {W(0x208):X4} {Q(0x200):X16}, {W(0x214):X4} {Q(0x20C):X16}",
                        $"{sw:X4}, {s0:X4} {g0:X16}, {s1:X4} {g1:X16}", true);
        }
        return bad;
    }

    /// <summary>FNINIT ; FLD m64 a ; D9 op ; FNSTSW [BX+0Ah] ; FSTP m64 [BX] : le mot d'état et le nouvel ST(0).</summary>
    private static (ushort sw, ulong r) Un87(int fpu, ulong a, byte op)
    {
        Jouer87(fpu, [0xDB, 0xE3, 0xDD, 0x06, 0x00, 0x03, 0xD9, op, .. SwA, 0xDD, 0x1F], 5, Ops((0x300, Le(a))));
        return (W(0x20A), Q(0x200));
    }

    // PB-68 — les transcendantes. Sur le 387 et le 486, |x| ≥ 2^63 laisse ST(0) et pose C2 (387 PRM § 4.6.4 ; SDM vol. 2,
    // FSIN, FCOS, FPTAN) : FSIN de 2^64, FCOS et FPTAN de 2^63 (FPTAN n'empile rien) ; PCem calcule par la libm et efface
    // C2 (sin 2^64 = 3F982A353118793Dh, cos 2^63 = 3F882AA375B3C33Eh, FPTAN empile 1,0). Sur les cinq coprocesseurs, dans
    // leurs domaines (387 PRM § 4.6 ; 287 PRM, F2XM1 et FYL2XP1) : F2XM1 de 10^−20, 2^x − 1 = x ln 2 + …, 6,93·10^−21
    // (3BC05DD14F56AA62h, FSTP m64 de l'arrondi à 64 bits, tools/x87ref/transcendantes.py), PCem pow(2, x) − 1 = 0 ;
    // FYL2XP1 de 2^−70 par 1, log₂(1 + 2^−70) (3B971547652B82FEh), PCem log(x + 1,0) / log 2 = 0. Sur le 387 et le 486,
    // FYL2X de −1 lève IE (SDM vol. 2, table 3-48) ; PCem rend le NaN de log(−1), sans IE.
    private static int Pb68(bool hw)
    {
        var bad = 0;
        const ushort C2 = 0x400;
        foreach (var (fpu, nom) in FpusXL.Where(f => f.fpu >= 4))
        {
            foreach (var (x, op, n, pcem) in new (ulong, byte, string, ulong)[]
                     {
                         (0x43F0000000000000, 0xFE, "FSIN de 2^64", 0x3F982A353118793D),
                         (0x43E0000000000000, 0xFF, "FCOS de 2^63", 0x3F882AA375B3C33E),
                         (0x43E0000000000000, 0xF2, "FPTAN de 2^63", 0x3FF0000000000000),
                     })
            {
                var (sw, r) = Un87(fpu, x, op);
                var (a, c2) = hw ? (x, C2) : (pcem, 0);
                bad += Voir($"{n} ; FSTP m64, C2 ({nom})", r == a && (sw & C2) == c2, $"{r:X16}, C2 = {(sw & C2) >> 10}",
                            $"{a:X16}, C2 = {c2 >> 10}", hw);
            }
            var (sw2, _) = Deux(fpu, 0xBFF0000000000000, 0x3FF0000000000000, 0xF1);
            var ie = hw ? 1 : 0;
            bad += Voir($"FYL2X de −1 par 1 : IE ({nom})", (sw2 & 1) == ie, $"IE = {sw2 & 1}", $"IE = {ie}", hw);
        }
        foreach (var (fpu, nom) in FpusXL)
        {
            var (_, r) = Un87(fpu, 0x3BC79CA10C924223, 0xF0);
            var a = hw ? 0x3BC05DD14F56AA62UL : 0UL;
            bad += Voir($"F2XM1 de 10^−20 ; FSTP m64 ({nom})", r == a, $"{r:X16}", $"{a:X16}", hw);
            (_, r) = Deux(fpu, 0x3B90000000000000, 0x3FF0000000000000, 0xF9);
            a = hw ? 0x3B971547652B82FEUL : 0UL;
            bad += Voir($"FYL2XP1 de 2^−70 par 1 ; FSTP m64 ({nom})", r == a, $"{r:X16}", $"{a:X16}", hw);
        }
        return bad;
    }

    // ===== Le noyau, N7a : le 8087 et le 287, hors des non normaux =====
    //
    // Chaque cas sur les cinq coprocesseurs ; le 8087 et le 287 (seize) contre le 287XL, le 387 et le 486, le témoin. Le
    // code en 0000:0100, BX = 0200h ; les opérandes en DS:0300h et suivants, le mot de contrôle en DS:0310h (FLDCW [0310h],
    // D9 2E 10 03).

    private static readonly byte[] Cw310 = [0xD9, 0x2E, 0x10, 0x03];
    private static readonly byte[] M80a = [0xDB, 0x2E, 0x00, 0x03];
    private static readonly byte[] M80b = [0xDB, 0x2E, 0x20, 0x03];

    /// <summary>FNINIT ; FLDCW cw ; FLD m80 a (0300h) ; FLD m80 b (0320h) ; DE op (ST(1) ← ST(1) op ST(0), dépilé) ; FNSTSW [BX+0Ah] ;
    /// FSTP m80 [BX] : le mot d'état et le résultat.</summary>
    private static (ushort sw, ushort se, ulong sig) Deux80(int fpu, ushort cw, (ushort se, ulong sig) a, (ushort se, ulong sig) b, byte op)
    {
        Jouer87(fpu, [0xDB, 0xE3, .. Cw310, .. M80a, .. M80b, 0xDE, op, .. SwA, 0xDB, 0x3F], 7,
                Ops((0x300, Le80(a.se, a.sig)), (0x320, Le80(b.se, b.sig)), (0x310, Cw(cw))));
        return (W(0x20A), W(0x208), Q(0x200));
    }

    private static int SeizeBits()
    {
        var bad = 0;
        foreach (var (fpu, nom) in FpusXL)
        {
            var seize = fpu < 3;

            // PB-60 — un NaN opérande : IE sur le 8087 et le 287, quel qu'il soit, et le NaN tel quel (387 PRM, annexe C §
            // C.2 ; 287 PRM, table 1-17). FLD m64 7FF0000000000001 (un SNaN pour le 387, qui le rend silencieux au
            // chargement, avec IE) ; FLD1 ; FADD ST(0), ST(1) ; FNSTSW ; FSTP m80 → 287 7FFF 8000000000000800h, 387 7FFF
            // C000000000000800h, IE des deux côtés. Un QNaN (7FF8000000000001) : IE sur le 287 seul.
            foreach (var (nan, s287, s387, n) in new (ulong, ulong, ushort, string)[]
                     {
                         (0x7FF0000000000001, 0x8000000000000800, 0x3001, "SNaN"),
                         (0x7FF8000000000001, 0xC000000000000800, 0x3000, "QNaN"),
                     })
            {
                Jouer87(fpu, [0xDB, 0xE3, 0xDD, 0x06, 0x00, 0x03, 0xD9, 0xE8, 0xD8, 0xC1, .. SwA, 0xDB, 0x3F], 6, Ops((0x300, Le(nan))));
                var (asw, asig) = seize ? ((ushort)0x3001, s287) : (s387, 0xC000000000000800UL);
                bad += Voir($"FLD m64 {n} ; FLD1 ; FADD ; FNSTSW ; FSTP m80 ({nom})", W(0x20A) == asw && Q(0x200) == asig && W(0x208) == 0x7FFF,
                            $"{W(0x20A):X4}, {W(0x208):X4} {Q(0x200):X16}", $"{asw:X4}, 7FFF {asig:X16}", true);
            }

            // PB-59 — le dépassement masqué (387 PRM, annexe C § C.2) : (2 − 2^−63) × 2^16383 au carré. Vers zéro (0F7Fh), le
            // 8087 et le 287 rendent +∞, le 387 le plus grand fini, OE et PE des deux côtés ; vers −∞ (077Fh), le plus
            // grand fini, sans OE sur le 8087 et le 287. FMULP (DE C9).
            var max = ((ushort)0x7FFE, 0xFFFFFFFFFFFFFFFFUL);
            {
                var (sw, se, sig) = Deux80(fpu, 0x0F7F, max, max, 0xC9);
                var (ase, asig) = seize ? ((ushort)0x7FFF, 0x8000000000000000UL) : max;
                bad += Voir($"vers zéro, max × max ; FNSTSW ; FSTP m80 ({nom})", sw == 0x3828 && se == ase && sig == asig,
                            $"{sw:X4}, {se:X4} {sig:X16}", $"3828, {ase:X4} {asig:X16}", true);
                (sw, se, sig) = Deux80(fpu, 0x077F, max, max, 0xC9);
                var asw = seize ? (ushort)0x3820 : (ushort)0x3828;
                bad += Voir($"vers −∞, max × max ; FNSTSW ; FSTP m80 ({nom})", sw == asw && se == 0x7FFE && sig == 0xFFFFFFFFFFFFFFFF,
                            $"{sw:X4}, {se:X4} {sig:X16}", $"{asw:X4}, 7FFE FFFFFFFFFFFFFFFF", true);
            }

            // PB-59 — la petitesse (387 PRM, annexe C § C.2 ; 287 PRM, table 1-17). Vers zéro, 2^−16382 × 0,5 = 2^−16383,
            // un dénormal exact (0000 4000000000000000h) : UE sur le 8087 et le 287 (« on tininess, regardless of loss of
            // accuracy »), rien sur le 387. Au plus près, (2 − 2^−62) × 2^−16382 × 0,5 × (1 + 2^−63) = (1 − 2^−126) ×
            // 2^−16382, petit avant l'arrondi, 2^−16382 après, même sur un exposant non borné (0001 8000000000000000h) : UE
            // et PE sur le 8087 et le 287, PE et C1 sur le 387.
            {
                var (sw, se, sig) = Deux80(fpu, 0x0F7F, (0x0001, 0x8000000000000000), (0x3FFE, 0x8000000000000000), 0xC9);
                var asw = seize ? (ushort)0x3810 : (ushort)0x3800;
                bad += Voir($"vers zéro, 2^−16382 × 0,5 ; FNSTSW ; FSTP m80 ({nom})", sw == asw && se == 0 && sig == 0x4000000000000000,
                            $"{sw:X4}, {se:X4} {sig:X16}", $"{asw:X4}, 0000 4000000000000000", true);
                (sw, se, sig) = Deux80(fpu, 0x037F, (0x0001, 0xFFFFFFFFFFFFFFFE), (0x3FFE, 0x8000000000000001), 0xC9);
                asw = seize ? (ushort)0x3830 : (ushort)0x3A20;
                bad += Voir($"au plus près, (1 − 2^−126) × 2^−16382 ; FNSTSW ; FSTP m80 ({nom})", sw == asw && se == 1 && sig == 0x8000000000000000,
                            $"{sw:X4}, {se:X4} {sig:X16}", $"{asw:X4}, 0001 8000000000000000", true);
            }

            // PB-59 — OE démasquée (0377h), la destination un registre : (1 + 2^−63) × 2^16383 × 3. Le 387 arrondit (au
            // pair, C000000000000002h), PE et C1 ; le 8087 et le 287 laissent le significande non arrondi
            // (C000000000000001h), sans PE (387 PRM, annexe C § C.2) ; l'exposant décalé de 24 576 (1FFFh), ES et B. Sous
            // l'acheminement (PB-204), FSTP m80 attend le verrou de l'AT : la mémoire intacte, comme pour Demasquees.
            {
                var (sw, se, sig) = Deux80(fpu, 0x0377, (0x7FFE, 0x8000000000000001), (0x4000, 0xC000000000000000), 0xC9);
                var (asw, asig) = seize ? ((ushort)0xB888, 0xC000000000000001UL) : ((ushort)0xBAA8, 0xC000000000000002UL);
                var ase = (ushort)0x1FFF;
                if (materiel.pb_204 && fpu > 1)
                    (ase, asig) = (0x9090, 0x9090909090909090);
                bad += Voir($"OE démasquée, (1 + 2^−63) × 2^16383 × 3 ; FNSTSW ; FSTP m80 ({nom})", sw == asw && se == ase && sig == asig,
                            $"{sw:X4}, {se:X4} {sig:X16}", $"{asw:X4}, {ase:X4} {asig:X16}", true);
            }

            // PB-59 — UE démasquée (036Fh), au plus près, (1 − 2^−126) × 2^−16382 (le produit plus haut) : petit avant
            // l'arrondi seulement. Le 8087 et le 287 lèvent UE, et rangent le significande non arrondi
            // (FFFFFFFFFFFFFFFFh), à l'exposant décalé (0 + 24 576 : 6000h), sans PE, ES et B ; le 387, ni petit ni UE, rend
            // 2^−16382, PE et C1. Sous l'acheminement, FSTP m80 attend (le 287).
            {
                var (sw, se, sig) = Deux80(fpu, 0x036F, (0x0001, 0xFFFFFFFFFFFFFFFE), (0x3FFE, 0x8000000000000001), 0xC9);
                var (asw, ase, asig) = seize ? ((ushort)0xB890, (ushort)0x6000, 0xFFFFFFFFFFFFFFFFUL)
                                             : ((ushort)0x3A20, (ushort)0x0001, 0x8000000000000000UL);
                if (materiel.pb_204 && fpu == 2)
                    (ase, asig) = (0x9090, 0x9090909090909090);
                bad += Voir($"UE démasquée, (1 − 2^−126) × 2^−16382 ; FNSTSW ; FSTP m80 ({nom})", sw == asw && se == ase && sig == asig,
                            $"{sw:X4}, {se:X4} {sig:X16}", $"{asw:X4}, {ase:X4} {asig:X16}", true);
            }

            // PB-59 et PB-60 — FST m32 et m64 (seize_memoire) : vers zéro (0F7Fh), FLD m80 2^200 ; FST m32 [BX] → +∞
            // (7F800000h) sur le 8087 et le 287, le plus grand fini (7F7FFFFFh) sur le 387, OE et PE. FLD m64 d'un QNaN ;
            // FST m64 [BX] : IE sur le 8087 et le 287 seuls.
            {
                Jouer87(fpu, [0xDB, 0xE3, .. Cw310, .. M80a, 0xD9, 0x17, .. SwA], 5, Ops((0x300, Le80(0x40C7, 0x8000000000000000)), (0x310, Cw(0x0F7F))));
                var a32 = seize ? 0x7F800000u : 0x7F7FFFFFu;
                var l32 = (uint)(W(0x200) | W(0x202) << 16);
                bad += Voir($"vers zéro, FLD m80 2^200 ; FST m32 ; FNSTSW ({nom})", l32 == a32 && W(0x20A) == 0x3828,
                            $"{l32:X8}, {W(0x20A):X4}", $"{a32:X8}, 3828", true);
                Jouer87(fpu, [0xDB, 0xE3, 0xDD, 0x06, 0x00, 0x03, 0xDD, 0x17, .. SwA], 4, Ops((0x300, Le(0x7FF8000000000001))));
                var asw = seize ? (ushort)0x3801 : (ushort)0x3800;
                bad += Voir($"FLD m64 QNaN ; FST m64 ; FNSTSW ({nom})", W(0x20A) == asw && Q(0x200) == 0x7FF8000000000001,
                            $"{W(0x20A):X4}, {Q(0x200):X16}", $"{asw:X4}, 7FF8000000000001", true);
            }

            // PB-59 — DE démasquée d'abord sur le 8087 et le 287 (287 PRM, table 3-2) : un dénormal divisé par +0, DM à 0
            // (037Dh) ; le 387 lève ZE, masquée, range −∞… +∞ et dépile ; le 8087 et le 287 lèvent DE et s'arrêtent, sans
            // ranger ni dépiler. FNINIT ; FLD m80 dénormal ; FLDZ ; FNCLEX ; FLDCW ; FDIVP (DE F9) ; FNSTSW.
            {
                Jouer87(fpu, [0xDB, 0xE3, .. M80a, 0xD9, 0xEE, 0xDB, 0xE2, .. Cw310, 0xDE, 0xF9, .. SwA], 7,
                        Ops((0x300, Le80(0, 1)), (0x310, Cw(0x037D))));
                var asw = seize ? (ushort)0xB082 : (ushort)0x3804;
                bad += Voir($"DM à 0, dénormal / +0 ; FNSTSW ({nom})", W(0x20A) == asw, $"{W(0x20A):X4}", $"{asw:X4}", true);
            }

            // PB-59 — FLD m80 d'un dénormal : DE sur le 8087 et le 287 (387 PRM, annexe C § C.4), rien sur le 387. DM à 0
            // (037Dh) : le 8087 et le 287 s'arrêtent avant d'empiler (287 PRM, table 3-2 : TOP = 0, 8082h) ; le 387, FLD m80
            // sans DE (3800h) ; FLD m64 2^−1074, le 387 empile quand même (SDM vol. 2, FLD : B882h), le 8087 et le 287 non.
            {
                Jouer87(fpu, [0xDB, 0xE3, .. M80a, .. SwA], 3, Ops((0x300, Le80(0, 1))));
                var asw = seize ? (ushort)0x3802 : (ushort)0x3800;
                bad += Voir($"FLD m80 dénormal ; FNSTSW ({nom})", W(0x20A) == asw, $"{W(0x20A):X4}", $"{asw:X4}", true);
                Jouer87(fpu, [0xDB, 0xE3, .. Cw310, .. M80a, .. SwA], 4, Ops((0x300, Le80(0, 1)), (0x310, Cw(0x037D))));
                asw = seize ? (ushort)0x8082 : (ushort)0x3800;
                bad += Voir($"DM à 0 ; FLD m80 dénormal ; FNSTSW ({nom})", W(0x20A) == asw, $"{W(0x20A):X4}", $"{asw:X4}", true);
                Jouer87(fpu, [0xDB, 0xE3, .. Cw310, 0xDD, 0x06, 0x00, 0x03, .. SwA], 4, Ops((0x300, Le(1)), (0x310, Cw(0x037D))));
                asw = seize ? (ushort)0x8082 : (ushort)0xB882;
                bad += Voir($"DM à 0 ; FLD m64 2^−1074 ; FNSTSW ({nom})", W(0x20A) == asw, $"{W(0x20A):X4}", $"{asw:X4}", true);
            }

            // PB-68 — FPATAN d'un pseudo-zéro en ST(0) (3FFF 0000000000000000h) par 0,5 : le 8087 et le 287 le prennent pour
            // un zéro (π/2, 3FF921FB54442D18h, sans IE) ; sans cela, la division exacte d'arctan le prendrait pour diviseur
            // (son exposant de travail, 0, passe celui de 0,5) et planterait ; le 387, un format non supporté, IE et
            // l'indéfini.
            {
                Jouer87(fpu, [0xDB, 0xE3, 0xDD, 0x06, 0x30, 0x03, .. M80a, 0xD9, 0xF3, .. SwA, 0xDD, 0x1F], 6,
                        Ops((0x300, Le80(0x3FFF, 0)), (0x330, Le(0x3FE0000000000000))));
                var (asw, ar) = seize ? ((ushort)0x3820, 0x3FF921FB54442D18UL) : ((ushort)0x3801, 0xFFF8000000000000UL);
                bad += Voir($"FLD m64 0,5 ; FLD m80 pseudo-zéro ; FPATAN ; FNSTSW, C1 ignoré ; FSTP m64 ({nom})",
                            (W(0x20A) & ~0x200) == asw && Q(0x200) == ar, $"{W(0x20A):X4}, {Q(0x200):X16}", $"{asw:X4}, {ar:X16}", true);
            }

            // PB-194 — FXTRACT (387 PRM, annexe C § C.4 ; 287 PRM, FXTRACT et table 1-12). De −0 : deux −0 sur le 8087 et
            // le 287, sans exception ; le 387, ZE, −∞ en ST(1). De +∞ : IE et l'indéfini aux deux sur le 8087 et le 287 ;
            // +∞ aux deux sur le 387. FNSTSW ; FSTP m64 [BX] ; FSTP m64 [BX+10h].
            foreach (var (charge, n, s16, a16, s87, a87) in new (byte[], string, ushort, (ulong, ulong), ushort, (ulong, ulong))[]
                     {
                         ([0xD9, 0xEE, 0xD9, 0xE0], "FLDZ ; FCHS", 0x3000, (0x8000000000000000, 0x8000000000000000), 0x3004,
                          (0x8000000000000000, 0xFFF0000000000000)),
                         ([0xDD, 0x06, 0x00, 0x03], "FLD m64 +∞", 0x3001, (0xFFF8000000000000, 0xFFF8000000000000), 0x3000,
                          (0x7FF0000000000000, 0x7FF0000000000000)),
                     })
            {
                Jouer87(fpu, [0xDB, 0xE3, .. charge, 0xD9, 0xF4, .. SwA, 0xDD, 0x1F, 0xDD, 0x5F, 0x10], charge.Length == 4 && charge[0] == 0xDD ? 6 : 7,
                        Ops((0x300, Le(0x7FF0000000000000))));
                var (asw, (a0, a1)) = seize ? (s16, a16) : (s87, a87);
                bad += Voir($"{n} ; FXTRACT ; FNSTSW ; FSTP m64 × 2 ({nom})", W(0x20A) == asw && Q(0x200) == a0 && Q(0x210) == a1,
                            $"{W(0x20A):X4}, {Q(0x200):X16} {Q(0x210):X16}", $"{asw:X4}, {a0:X16} {a1:X16}", true);
            }

            // PB-198 — FSCALE par +∞ (287 PRM, table 1-12) : 1 rend IE et l'indéfini sur le 8087 et le 287, +∞ sur le 387 ;
            // +0 rend +0 sur le 8087 et le 287, IE et l'indéfini sur le 387.
            foreach (var (a, n, s16, r16, s87, r87) in new (ulong, string, ushort, ulong, ushort, ulong)[]
                     {
                         (0x3FF0000000000000, "1", 0x3001, 0xFFF8000000000000, 0x3000, 0x7FF0000000000000),
                         (0, "+0", 0x3000, 0, 0x3001, 0xFFF8000000000000),
                     })
            {
                var (sw, r) = Deux(fpu, a, 0x7FF0000000000000, 0xFD);
                var (asw, ar) = seize ? (s16, r16) : (s87, r87);
                bad += Voir($"FSCALE {n} par +∞ ; FNSTSW ; FSTP m64 ({nom})", sw == asw && r == ar, $"{sw:X4}, {r:X16}", $"{asw:X4}, {ar:X16}", true);
            }

            // PB-65 — les bits du quotient de FPREM sur le 8087 et le 287 : leurs sources se contredisent sur un défaut (287
            // PRM, p. 2-8, et sa table 2-4 ; 387 PRM, annexe C § C.4) ; les bits justes, ceux de PCem et du 387, sont gardés
            // (c). FXAM de −0 pose C3 et C1 ; FPREM de 1 par 3 (Q = 0) : 3000h ; de 7 par 2 (Q = 3) : C3 et C1, 7200h.
            foreach (var (b, a, n, asw) in new (ulong, ulong, string, ushort)[]
                     {
                         (0x4008000000000000, 0x3FF0000000000000, "1 par 3", 0x3000),
                         (0x4000000000000000, 0x401C000000000000, "7 par 2", 0x7200),
                     })
            {
                Jouer87(fpu, [0xDB, 0xE3, 0xD9, 0xEE, 0xD9, 0xE0, 0xD9, 0xE5, 0xDD, 0xD8, 0xDD, 0x06, 0x00, 0x03, 0xDD, 0x06, 0x08, 0x03,
                              0xD9, 0xF8, .. SwA], 9, Ops((0x300, Le(b)), (0x308, Le(a))));
                bad += Voir($"FXAM −0 ; FPREM {n} ; FNSTSW ({nom})", W(0x20A) == asw, $"{W(0x20A):X4}", $"{asw:X4}", true);
            }

            // PB-68 — FPTAN sur le 8087 et le 287 : C2 indéfini, laissé (387 PRM, annexe C § C.3) ; FXAM de 0,5 le pose,
            // FPTAN de 0,5 le garde (3420h : C2, PE, TOP = 6) ; le 387 l'efface (3020h, C1 ignoré). Pile pleine (huit FLD1) :
            // l'opérande gardé, poussé en ST(1), l'indéfini en ST(0), IE seule (3801h) ; le 387, l'indéfini aux deux, SF et
            // C1 (3A41h).
            {
                Jouer87(fpu, [0xDB, 0xE3, 0xDD, 0x06, 0x00, 0x03, 0xD9, 0xE5, 0xD9, 0xF2, .. SwA], 5, Ops((0x300, Le(0x3FE0000000000000))));
                var asw = seize ? (ushort)0x3420 : (ushort)0x3020;
                bad += Voir($"FLD m64 0,5 ; FXAM ; FPTAN ; FNSTSW, C1 ignoré ({nom})", (W(0x20A) & ~0x200) == asw, $"{W(0x20A):X4}", $"{asw:X4}", true);
                Jouer87(fpu, [0xDB, 0xE3, .. Enumerable.Repeat<byte[]>([0xD9, 0xE8], 8).SelectMany(o => o), 0xD9, 0xF2, .. SwA, 0xDD, 0x1F,
                              0xDD, 0x5F, 0x10], 13);
                var (s, a1) = seize ? ((ushort)0x3801, 0x3FF0000000000000UL) : ((ushort)0x3A41, 0xFFF8000000000000UL);
                bad += Voir($"huit FLD1 ; FPTAN ; FNSTSW ; FSTP m64 × 2 ({nom})", W(0x20A) == s && Q(0x200) == 0xFFF8000000000000 && Q(0x210) == a1,
                            $"{W(0x20A):X4}, {Q(0x200):X16} {Q(0x210):X16}", $"{s:X4}, FFF8000000000000 {a1:X16}", true);
            }

            // PB-68 — le 8087 et le 287 n'examinent pas les opérandes de leurs transcendantes (287 PRM, « operands not
            // checked ») : FYL2X de −1 par 1, l'indéfini sans IE (3800h ; le 387, 3801h) ; de +0 par 1, −∞ sans ZE
            // (3800h ; le 387, 3804h).
            foreach (var (a, n, r, s87) in new (ulong, string, ulong, ushort)[]
                     {
                         (0xBFF0000000000000, "−1", 0xFFF8000000000000, 0x3801),
                         (0, "+0", 0xFFF0000000000000, 0x3804),
                     })
            {
                var (sw, rr) = Deux(fpu, a, 0x3FF0000000000000, 0xF1);
                var asw = seize ? (ushort)0x3800 : s87;
                bad += Voir($"FYL2X {n} par 1 ; FNSTSW ; FSTP m64 ({nom})", sw == asw && rr == r, $"{sw:X4}, {rr:X16}", $"{asw:X4}, {r:X16}", true);
            }

            // PB-63 — FXAM d'un pseudo-infini (7FFF 0000000000000000h) : un NaN pour le 8087 et le 287 (C0, 3900h) ; un format
            // non supporté pour le 387 (000, 3800h). PB-208 — l'étiquette d'un non normal (3FFF 4000000000000000h), valide
            // sur le 8087 et le 287 (FNSTENV : 3FFFh), spéciale sur le 387 (BFFFh).
            {
                Jouer87(fpu, [0xDB, 0xE3, .. M80a, 0xD9, 0xE5, .. SwA], 4, Ops((0x300, Le80(0x7FFF, 0))));
                var asw = seize ? (ushort)0x3900 : (ushort)0x3800;
                bad += Voir($"FLD m80 pseudo-infini ; FXAM ; FNSTSW ({nom})", W(0x20A) == asw, $"{W(0x20A):X4}", $"{asw:X4}", true);
                Jouer87(fpu, [0xDB, 0xE3, .. M80a, 0xD9, 0x37], 3, Ops((0x300, Le80(0x3FFF, 0x4000000000000000))));
                var tw = seize ? (ushort)0x3FFF : (ushort)0xBFFF;
                bad += Voir($"FLD m80 non normal ; FNSTENV, les étiquettes ({nom})", W(0x204) == tw, $"{W(0x204):X4}", $"{tw:X4}", true);
            }

            // PB-210 — les entrées que PCem tient pour illégales gardent son gestionnaire sous le noyau (N7c) : DD C9 (FXCH4,
            // un alias non documenté), sans effet ; FLD1 ; FLDZ ; DD C9 ; FNSTSW ; FSTP m64 → 0 (rien d'échangé), TOP = 6.
            {
                Jouer87(fpu, [0xDB, 0xE3, 0xD9, 0xE8, 0xD9, 0xEE, 0xDD, 0xC9, .. SwA, 0xDD, 0x1F], 6);
                bad += Voir($"FLD1 ; FLDZ ; DD C9 (FXCH4, illégal chez PCem) ; FNSTSW ; FSTP m64 ({nom})", W(0x20A) == 0x3000 && Q(0x200) == 0,
                            $"{W(0x20A):X4}, {Q(0x200):X16}", "3000, 0000000000000000", true);
            }

            // FENI et FDISI : IEM (bit 7) sur le 8087 (FNINIT, 03FFh ; FENI, 037Fh ; FDISI, 03FFh), sans effet ailleurs
            // (037Fh) ; DB E5, sans effet. FNSTCW [BX+2] (D9 7F 02).
            {
                Jouer87(fpu, [0xDB, 0xE3, 0xDB, 0xE0, 0xD9, 0x7F, 0x02, 0xDB, 0xE1, 0xDB, 0xE5, 0xD9, 0x7F, 0x04, .. SwA], 7);
                var (eni, disi) = fpu == 1 ? ((ushort)0x037F, (ushort)0x03FF) : ((ushort)0x037F, (ushort)0x037F);
                bad += Voir($"FNINIT ; FENI ; FNSTCW ; FDISI ; DB E5 ; FNSTCW ; FNSTSW ({nom})", W(0x202) == eni && W(0x204) == disi && W(0x20A) == 0,
                            $"{W(0x202):X4} {W(0x204):X4}, {W(0x20A):X4}", $"{eni:X4} {disi:X4}, 0000", true);
            }
        }
        return bad;
    }

    // ===== Le noyau, N7b : les non normaux et l'infini projectif du 8087 et du 287 =====
    //
    // Les opérandes : un m80 en 0300h (M80a) et en 0320h (M80b), un m64 en 0330h (DD 06 30 03). U, le non normal 3FFF
    // 4000000000000000h (0,5, exposant 0, J = 0) ; le dénormal 0000 0000000000000001h (2^−16445). Le 287XL, le 387 et le
    // 486 (le témoin) refusent U (IE, un format non supporté). C1 ignoré.

    private static readonly byte[] M64c = [0xDD, 0x06, 0x30, 0x03];

    private static int NonNormaux()
    {
        var bad = 0;
        const ulong J = 0x8000000000000000, IND = 0xC000000000000000;
        (ushort, ulong) u = (0x3FFF, 0x4000000000000000), den = (0, 1);
        foreach (var (fpu, nom) in FpusXL)
        {
            var seize = fpu < 3;
            // (nom, le code entre FNINIT et FNSTSW ; FSTP m80 [BX], le nombre de ses instructions, les opérandes,
            //  l'attendu du 8087 et du 287, puis du 387 : mot d'état, exposant, significande)
            foreach (var (n, code, pas, ops, a16, a87) in new (string, byte[], int, Action, (ushort, ushort, ulong), (ushort, ushort, ulong))[]
                     {
                         ("FLD m32 2^−149 (le non normal 3F81h)", [0xD9, 0x06, 0x00, 0x03], 1, Ops((0x300, [1, 0, 0, 0])),
                          (0x3802, 0x3F81, 0x0000010000000000), (0x3802, 0x3F6A, J)),
                         ("FLD m64 2^−1074 (le non normal 3C01h)", [0xDD, 0x06, 0x00, 0x03], 1, Ops((0x300, Le(1))),
                          (0x3802, 0x3C01, 0x800), (0x3802, 0x3BCD, J)),
                         ("FLD m32 2^−149 ; FXAM (non normal, 0000)", [0xD9, 0x06, 0x00, 0x03, 0xD9, 0xE5], 2, Ops((0x300, [1, 0, 0, 0])),
                          (0x3802, 0x3F81, 0x0000010000000000), (0x3C02, 0x3F6A, J)),
                         ("0,25 + U, FADDP (rangé à l'exposant de U : 3FFF 6000000000000000h)", [.. M80b, .. M80a, 0xDE, 0xC1], 3,
                          Ops((0x300, Le80(u.Item1, u.Item2)), (0x320, Le80(0x3FFD, J))), (0x3800, 0x3FFF, 0x6000000000000000),
                          (0x3801, 0xFFFF, IND)),
                         ("1 + 0,5 non normal d'exposant 3 (4002 0800000000000000h), FADDP (1 le plus grand, normal : normalisé, 3FFF C000000000000000h)",
                          [.. M80b, .. M80a, 0xDE, 0xC1], 3, Ops((0x300, Le80(0x4002, 0x0800000000000000)), (0x320, Le80(0x3FFF, J))),
                          (0x3800, 0x3FFF, 0xC000000000000000), (0x3801, 0xFFFF, IND)),
                         ("3 × U, FMULP (non normal : 4000 6000000000000000h)", [.. M80b, .. M80a, 0xDE, 0xC9], 3,
                          Ops((0x300, Le80(u.Item1, u.Item2)), (0x320, Le80(0x4000, 0xC000000000000000))),
                          (0x3800, 0x4000, 0x6000000000000000), (0x3801, 0xFFFF, IND)),
                         ("U / 2, FDIVP (non normal : 3FFE 4000000000000000h)", [.. M80a, .. M80b, 0xDE, 0xF9], 3,
                          Ops((0x300, Le80(u.Item1, u.Item2)), (0x320, Le80(0x4000, J))), (0x3800, 0x3FFE, 0x4000000000000000),
                          (0x3801, 0xFFFF, IND)),
                         ("U × U, deux non normaux de 63 zéros de tête, FMULP (un pseudo-zéro, PE)", [.. M80b, .. M80a, 0xDE, 0xC9], 3,
                          Ops((0x300, Le80(0x3FFF, 1)), (0x320, Le80(0x3FFF, 1))), (0x3820, 0x3FFF, 0), (0x3801, 0xFFFF, IND)),
                         ("1 / dénormal, FDIV ST(0), ST(1) (IE ; le 387, DE, OE, PE, +∞)", [.. M80a, 0xD9, 0xE8, 0xD8, 0xF1], 3,
                          Ops((0x300, Le80(den.Item1, den.Item2))), (0x3003, 0xFFFF, IND), (0x302A, 0x7FFF, J)),
                         ("FSQRT d'un dénormal (IE ; le 387, DE et PE)", [.. M80a, 0xD9, 0xFA], 2, Ops((0x300, Le80(den.Item1, den.Item2))),
                          (0x3803, 0xFFFF, IND), (0x3822, 0x1FE0, 0xB504F333F9DE6484)),
                         ("FSCALE de U par 2 (4001 4000000000000000h)", [.. M80b, .. M80a, 0xD9, 0xFD], 3,
                          Ops((0x300, Le80(u.Item1, u.Item2)), (0x320, Le80(0x4000, J))), (0x3000, 0x4001, 0x4000000000000000),
                          (0x3001, 0xFFFF, IND)),
                         ("FRNDINT de 1,5 non normal (4000 6000000000000000h : 2, PE)", [.. M80a, 0xD9, 0xFC], 2,
                          Ops((0x300, Le80(0x4000, 0x6000000000000000))), (0x3820, 0x4000, J), (0x3801, 0xFFFF, IND)),
                         ("vers +∞, FRNDINT de 0,5 non normal (4000 2000000000000000h : 1, normalisé, 3FFF 8000000000000000h)",
                          [.. Cw310, .. M80a, 0xD9, 0xFC], 3, Ops((0x300, Le80(0x4000, 0x2000000000000000)), (0x310, Cw(0x0B7F))),
                          (0x3820, 0x3FFF, J), (0x3801, 0xFFFF, IND)),
                         ("FXTRACT d'un dénormal (le significande tel quel, l'exposant −16382, C00C FFF8000000000000h en ST(1))",
                          [.. M80a, 0xD9, 0xF4], 2, Ops((0x300, Le80(den.Item1, den.Item2))), (0x3002, 0x3FFF, 1), (0x3002, 0x3FFF, J)),
                         ("FSCALE d'un dénormal par 1 (0002 0000000000000001h, le significande gardé)", [.. M80b, .. M80a, 0xD9, 0xFD], 3,
                          Ops((0x300, Le80(den.Item1, den.Item2)), (0x320, Le80(0x3FFF, J))), (0x3002, 0x0002, 1), (0x3002, 0x0000, 2)),
                         ("FLD1 ; FPREM par U (un modulo non normal : IE, C2 effacé ; le 387, IE et C2)", [.. M80b, 0xD9, 0xE8, 0xD9, 0xF8], 3,
                          Ops((0x320, Le80(u.Item1, u.Item2))), (0x3001, 0xFFFF, IND), (0x3401, 0xFFFF, IND)),
                         ("U / +0, FDIVP (ZE et +∞)", [.. M80a, 0xD9, 0xEE, 0xDE, 0xF9], 3, Ops((0x300, Le80(u.Item1, u.Item2))),
                          (0x3804, 0x7FFF, J), (0x3801, 0xFFFF, IND)),
                         ("pseudo-zéro / +0, FDIVP (0 ÷ 0 : IE)", [.. M80a, 0xD9, 0xEE, 0xDE, 0xF9], 3, Ops((0x300, Le80(0x3FFF, 0))),
                          (0x3801, 0xFFFF, IND), (0x3801, 0xFFFF, IND)),
                         ("+0 + pseudo-zéro, FADDP (le pseudo-zéro 3FFFh)", [0xD9, 0xEE, .. M80a, 0xDE, 0xC1], 3, Ops((0x300, Le80(0x3FFF, 0))),
                          (0x3800, 0x3FFF, 0), (0x3801, 0xFFFF, IND)),
                         ("pseudo-zéro + +0, FADDP (le même, l'ordre inversé)", [.. M80a, 0xD9, 0xEE, 0xDE, 0xC1], 3, Ops((0x300, Le80(0x3FFF, 0))),
                          (0x3800, 0x3FFF, 0), (0x3801, 0xFFFF, IND)),
                         ("FSCALE de 1 par 4040 0000000000000001h (4, non normal : 16, 4003 8000000000000000h)", [.. M80a, 0xD9, 0xE8, 0xD9, 0xFD], 3,
                          Ops((0x300, Le80(0x4040, 1))), (0x3000, 0x4003, J), (0x3001, 0xFFFF, IND)),
                         ("FTST d'un pseudo-zéro (C3, sans DE ; gardé tel quel)", [.. M80a, 0xD9, 0xE4], 2, Ops((0x300, Le80(0x3FFF, 0))),
                          (0x7800, 0x3FFF, 0), (0x7D01, 0x3FFF, 0)),
                         ("2^−16383 non normal × 0,5, FMULP (petit : dénormalisé, 0000 2000000000000000h, exact)", [.. M80b, .. M80a, 0xDE, 0xC9], 3,
                          Ops((0x300, Le80(0x0001, 0x4000000000000000)), (0x320, Le80(0x3FFE, J))), (0x3800, 0x0000, 0x2000000000000000),
                          (0x3801, 0xFFFF, IND)),
                         ("vers −∞, 2^16382 non normal × 4, FMULP (table 1-11 : 7FFE 4000000000000000h, PE sans OE)",
                          [.. Cw310, .. M80b, .. M80a, 0xDE, 0xC9], 4,
                          Ops((0x300, Le80(0x7FFE, 0x4000000000000000)), (0x320, Le80(0x4001, J)), (0x310, Cw(0x077F))),
                          (0x3820, 0x7FFE, 0x4000000000000000), (0x3801, 0xFFFF, IND)),
                         ("+∞ + +∞, FADDP (projectif : IE)", [.. M64c, .. M64c, 0xDE, 0xC1], 3, Ops((0x330, Le(0x7FF0000000000000))),
                          (0x3801, 0xFFFF, IND), (0x3800, 0x7FFF, J)),
                         ("FSQRT de +∞ (projectif : IE)", [.. M64c, 0xD9, 0xFA], 2, Ops((0x330, Le(0x7FF0000000000000))),
                          (0x3801, 0xFFFF, IND), (0x3800, 0x7FFF, J)),
                     })
            {
                Jouer87(fpu, [0xDB, 0xE3, .. code, .. SwA, 0xDB, 0x3F], pas + 3, ops);
                var (asw, ase, asig) = seize ? a16 : a87;
                bad += Voir($"{n} ; FNSTSW ; FSTP m80 ({nom})", (W(0x20A) & ~0x200) == asw && W(0x208) == ase && Q(0x200) == asig,
                            $"{W(0x20A):X4}, {W(0x208):X4} {Q(0x200):X16}", $"{asw:X4}, {ase:X4} {asig:X16}", true);
            }

            // FCOMPP de U contre 0,5 : le 8087 et le 287, DE et l'égalité (C3, 4002h) ; le 387, IE et non ordonné (4501h).
            Jouer87(fpu, [0xDB, 0xE3, .. M64c, .. M80a, 0xDE, 0xD9, .. SwA], 5,
                    Ops((0x300, Le80(u.Item1, u.Item2)), (0x330, Le(0x3FE0000000000000))));
            var sw = seize ? (ushort)0x4002 : (ushort)0x4501;
            bad += Voir($"FLD m64 0,5 ; FLD m80 U ; FCOMPP ; FNSTSW ({nom})", W(0x20A) == sw, $"{W(0x20A):X4}", $"{sw:X4}", true);

            // FST m32 [BX+40h] d'un non normal sous 2^−126 (3F00 4000000000000000h, 2^−256) : le 8087 et le 287, la valeur
            // convertie (+0), UE et PE (3830h) ; le 387, IE et l'indéfini (FFC00000h).
            Jouer87(fpu, [0xDB, 0xE3, .. M80a, 0xD9, 0x57, 0x40, .. SwA], 4, Ops((0x300, Le80(0x3F00, 0x4000000000000000))));
            var (s32, v32) = seize ? ((ushort)0x3830, 0u) : ((ushort)0x3801, 0xFFC00000u);
            var l32 = (uint)(W(0x240) | W(0x242) << 16);
            bad += Voir($"FLD m80 non normal 2^−256 ; FST m32 ; FNSTSW ({nom})", (W(0x20A) & ~0x200) == s32 && l32 == v32,
                        $"{W(0x20A):X4}, {l32:X8}", $"{s32:X4}, {v32:X8}", true);
            // FST m32 de U (0,5, au-dessus de 2^−126) : IE et l'indéfini, sur les cinq (le 387, un format non supporté).
            Jouer87(fpu, [0xDB, 0xE3, .. M80a, 0xD9, 0x57, 0x40, .. SwA], 4, Ops((0x300, Le80(u.Item1, u.Item2))));
            l32 = (uint)(W(0x240) | W(0x242) << 16);
            bad += Voir($"FLD m80 U ; FST m32 ; FNSTSW ({nom})", (W(0x20A) & ~0x200) == 0x3801 && l32 == 0xFFC00000,
                        $"{W(0x20A):X4}, {l32:X8}", "3801, FFC00000", true);

            // FISTP m32 [BX+40h] d'un dénormal : le 8087 et le 287, IE et l'indéfini entier (80000000h ; DE du chargement) ;
            // le 387, 0 et PE.
            Jouer87(fpu, [0xDB, 0xE3, .. M80a, 0xDB, 0x5F, 0x40, .. SwA], 4, Ops((0x300, Le80(den.Item1, den.Item2))));
            (s32, v32) = seize ? ((ushort)0x0003, 0x80000000u) : ((ushort)0x0020, 0u);
            l32 = (uint)(W(0x240) | W(0x242) << 16);
            bad += Voir($"FLD m80 dénormal ; FISTP m32 ; FNSTSW ({nom})", (W(0x20A) & ~0x200) == s32 && l32 == v32,
                        $"{W(0x20A):X4}, {l32:X8}", $"{s32:X4}, {v32:X8}", true);

            // FXTRACT de 4005 4000000000000000h (32, non normal) : le 8087 et le 287, le significande tel quel (3FFF
            // 4000000000000000h) et l'exposant 6 (4001 C000000000000000h) ; le 387, IE et l'indéfini aux deux.
            Jouer87(fpu, [0xDB, 0xE3, .. M80a, 0xD9, 0xF4, .. SwA, 0xDB, 0x3F, 0xDB, 0x7F, 0x0C], 6,
                    Ops((0x300, Le80(0x4005, 0x4000000000000000))));
            var (xs, s0, g0, s1, g1) = seize ? ((ushort)0x3000, (ushort)0x3FFF, 0x4000000000000000UL, (ushort)0x4001, 0xC000000000000000UL)
                                             : ((ushort)0x3001, (ushort)0xFFFF, IND, (ushort)0xFFFF, IND);
            bad += Voir($"FLD m80 32 non normal ; FXTRACT ; FNSTSW ; FSTP m80 × 2 ({nom})",
                        W(0x20A) == xs && W(0x208) == s0 && Q(0x200) == g0 && W(0x214) == s1 && Q(0x20C) == g1,
                        $"{W(0x20A):X4}, {W(0x208):X4} {Q(0x200):X16}, {W(0x214):X4} {Q(0x20C):X16}",
                        $"{xs:X4}, {s0:X4} {g0:X16}, {s1:X4} {g1:X16}", true);
        }
        return bad;
    }

    // PB-200 — DF E0 sur le 8088 et le 8087 : MOV AX, 1234h ; FNINIT ; FNSTSW AX → AX gardé (déduit : le 8087 n'a pas
    // FNSTSW AX, 287 PRM) ; PCem, 0000h. Le 287 (le témoin) : 0000h des deux côtés.
    private static int Pb200(bool hw)
    {
        var bad = 0;
        foreach (var (fpu, nom) in new[] { (1, "8087"), (2, "287") })
        {
            var r = Jouer87(fpu, [0xB8, 0x34, 0x12, 0xDB, 0xE3, 0xDF, 0xE0], 3);
            var ax = fpu == 1 && hw ? (ushort)0x1234 : (ushort)0;
            bad += Voir($"MOV AX, 1234h ; FNINIT ; FNSTSW AX ({nom})", r[(int)R.AX] == ax, $"AX = {r[(int)R.AX]:X4}", $"{ax:X4}", hw);
        }
        return bad;
    }

    // PB-201 — l'adressage du 8087 (Numerics Supplement, 1980) : DS = 0, FLD m64 [FFFCh] lit ses quatre premiers octets en
    // FFFCh, les quatre suivants en 10000h, l'adresse physique incrémentée ; PCem boucle dans le segment et les lit en
    // 0000h. En FFFCh, 0 ; en 10000h, 3FF00000h (1,0) ; en 0000h, 40000000h (2,0). FNINIT ; FLD m64 [FFFCh] (DD 06 FC FF) ;
    // FSTP m64 [BX].
    private static int Pb201(bool hw)
    {
        Jouer87(1, [0xDB, 0xE3, 0xDD, 0x06, 0xFC, 0xFF, 0xDD, 0x1F], 3,
                Ops((0xFFFC, [0, 0, 0, 0]), (0x10000, [0, 0, 0xF0, 0x3F]), (0, [0, 0, 0, 0x40])));
        var a = hw ? 0x3FF0000000000000UL : 0x4000000000000000UL;
        return Voir("DS = 0 ; FNINIT ; FLD m64 [FFFCh] ; FSTP m64 (8087)", Q(0x200) == a, $"{Q(0x200):X16}", $"{a:X16}", hw);
    }

    // PB-202 — FINIT et FSAVE gardent C3-C0 sur le 8087 et le 287 (387 PRM, annexe C § C.3) : FNINIT ; FLDZ ; FTST (C3) ;
    // FNINIT, ou FNSAVE [BX] ; FNSTSW [BX+70h] → 4000h ; PCem, 0000h ; le 287XL, le 387 et le 486, 0000h des deux côtés.
    private static int Pb202(bool hw)
    {
        var bad = 0;
        foreach (var (fpu, nom) in FpusXL)
        foreach (var (op, n) in new (byte[], string)[] { ([0xDB, 0xE3], "FNINIT"), ([0xDD, 0x37], "FNSAVE") })
        {
            Jouer87(fpu, [0xDB, 0xE3, 0xD9, 0xEE, 0xD9, 0xE4, .. op, 0xDD, 0x7F, 0x70], 5);
            var sw = hw && fpu < 3 ? (ushort)0x4000 : (ushort)0;
            bad += Voir($"FLDZ ; FTST ; {n} ; FNSTSW ({nom})", W(0x270) == sw, $"{W(0x270):X4}", $"{sw:X4}", hw);
        }
        return bad;
    }

    /// <summary>FRNDINT, FSCALE, FXTRACT, FPREM et FPREM1 du noyau (N4b), en mode matériel, aux attentes des sources (387
    /// PRM § 4.4.8 à 4.4.12, tables 3-3, 3-4, 3-10 et 4-4 ; SDM vol. 2). FNINIT ; FLDCW [0310h] ; FLD m64 [0308h] ; FLD m64
    /// [0300h] ; le code ; FNSTSW [BX+0Ah] (TOP = 6) ; FSTP m80 [BX].</summary>
    private static int Reductions()
    {
        var bad = 0;
        foreach (var (a, b, cw, op, sw, se, sig, n) in new (ulong, ulong, ushort, byte, ushort, ushort, ulong, string)[]
                 {
                     (0x4004000000000000, 0, 0x037F, 0xFC, 0x3020, 0x4000, 0x8000000000000000, "FRNDINT 2,5 (au pair : 2, PE)"),
                     (0x400C000000000000, 0, 0x037F, 0xFC, 0x3220, 0x4001, 0x8000000000000000, "FRNDINT 3,5 (4, PE, C1)"),
                     (0xC004000000000000, 0, 0x0B7F, 0xFC, 0x3020, 0xC000, 0x8000000000000000, "FRNDINT −2,5 vers +∞ (−2, PE)"),
                     (0x3FF0000000000000, 0x40D3880000000000, 0x037F, 0xFD, 0x3228, 0x7FFF, 0x8000000000000000,
                      "FSCALE 1 par 20 000 (+∞ : OE, PE, C1)"),
                     (0x3FF0000000000000, 0x4010CCCCCCCCCCCD, 0x037F, 0xFD, 0x3000, 0x4003, 0x8000000000000000,
                      "FSCALE 1 par 4,2 (tronqué : 16)"),
                     (0x3FF0000000000000, 0xC0D3880000000000, 0x037F, 0xFD, 0x3030, 0x0000, 0, "FSCALE 1 par −20 000 (UE, PE, 0)"),
                     (0x7FF0000000000000, 0xFFF0000000000000, 0x037F, 0xFD, 0x3001, 0xFFFF, 0xC000000000000000,
                      "FSCALE +∞ par −∞ (IE, l'indéfini)"),
                     (0x0000000000000000, 0x7FF0000000000000, 0x037F, 0xFD, 0x3001, 0xFFFF, 0xC000000000000000,
                      "FSCALE 0 par +∞ (IE, l'indéfini)"),
                     (0x4008000000000000, 0, 0x037F, 0xF8, 0x3401, 0xFFFF, 0xC000000000000000, "FPREM 3 par 0 (IE, l'indéfini, C2)"),
                     (0x7FF0000000000000, 0x3FF0000000000000, 0x037F, 0xF8, 0x3401, 0xFFFF, 0xC000000000000000,
                      "FPREM +∞ par 1 (IE, C2)"),
                     (0x4008000000000000, 0x7FF0000000000000, 0x037F, 0xF8, 0x3000, 0x4000, 0xC000000000000000,
                      "FPREM 3 par +∞ (3, Q = 0)"),
                     (0xC01C000000000000, 0x4000000000000000, 0x037F, 0xF8, 0x7200, 0xBFFF, 0x8000000000000000,
                      "FPREM −7 par 2 (−1, Q = 3 : C3 C1)"),
                     (0xC018000000000000, 0x4008000000000000, 0x037F, 0xF8, 0x7000, 0x8000, 0, "FPREM −6 par 3 (−0, Q = 2 : C3)"),
                     (0x3FF0000000000000, 0x4000000000000000, 0x037F, 0xF5, 0x3000, 0x3FFF, 0x8000000000000000,
                      "FPREM1 1 par 2 (0,5 : au pair, Q = 0, 1)"),
                     (0x3FF0000000000000, 0xC0D0040000000000, 0x037F, 0xFD, 0x3000, 0x0000, 0x0000200000000000,
                      "FSCALE 1 par −16 400 (2^−16400, dénormal exact : ni UE ni PE)"),
                 })
        {
            Jouer87(4, [0xDB, 0xE3, 0xD9, 0x2E, 0x10, 0x03, 0xDD, 0x06, 0x08, 0x03, 0xDD, 0x06, 0x00, 0x03, 0xD9, op, .. SwA, 0xDB, 0x3F], 7,
                    Ops((0x300, Le(a)), (0x308, Le(b)), (0x310, Cw(cw))));
            bad += Voir($"{n} ; FNSTSW ; FSTP m80 (387)", W(0x20A) == sw && W(0x208) == se && Q(0x200) == sig,
                        $"{W(0x20A):X4}, {W(0x208):X4} {Q(0x200):X16}", $"{sw:X4}, {se:X4} {sig:X16}", true);
        }
        // FPREM sur le 287 d'un diviseur pseudo-zéro (3FFFh 0000000000000000h, que le 287 prend) : un diviseur nul, IE,
        // l'indéfini, C2 effacé (287 PRM, table 1-17, « complete remainder » : N7a) (et pas l'arrêt de l'hôte : la
        // division entière par zéro de `reste`).
        Jouer87(2, [0xDB, 0xE3, 0xDB, 0x2E, 0x20, 0x03, 0xD9, 0xE8, 0xD9, 0xF8, .. SwA, 0xDB, 0x3F], 6,
                Ops((0x320, Le80(0x3FFF, 0))));
        bad += Voir("FLD m80 pseudo-zéro ; FLD1 ; FPREM ; FNSTSW ; FSTP m80 (287)",
                    W(0x20A) == 0x3001 && W(0x208) == 0xFFFF && Q(0x200) == 0xC000000000000000,
                    $"{W(0x20A):X4}, {W(0x208):X4} {Q(0x200):X16}", "3001, FFFF C000000000000000", true);
        // FXTRACT : FNINIT ; FLD m80 [0320h] ; FXTRACT ; FNSTSW ; FSTP m80 [BX] (le significande) ; FSTP m80 [BX+0Ch]
        // (l'exposant).
        foreach (var (se0, sig0, sw, s1, g1, s2, g2, n) in new (ushort, ulong, ushort, ushort, ulong, ushort, ulong, string)[]
                 {
                     (0x8000, 0, 0x3004, 0x8000, 0, 0xFFFF, 0x8000000000000000, "−0 (ZE ; −0, −∞)"),
                     (0x7FFF, 0x8000000000000000, 0x3000, 0x7FFF, 0x8000000000000000, 0x7FFF, 0x8000000000000000, "+∞ (+∞, +∞)"),
                     (0x0000, 0x0000000000000001, 0x3002, 0x3FFF, 0x8000000000000000, 0xC00D, 0x807A000000000000,
                      "le plus petit dénormal (DE ; 1, −16 445)"),
                     (0xC005, 0xC000000000000000, 0x3000, 0xBFFF, 0xC000000000000000, 0x4001, 0xC000000000000000, "−96 (−1,5, 6)"),
                 })
        {
            Jouer87(4, [0xDB, 0xE3, 0xDB, 0x2E, 0x20, 0x03, 0xD9, 0xF4, .. SwA, 0xDB, 0x3F, 0xDB, 0x7F, 0x0C], 6,
                    Ops((0x320, Le80(se0, sig0))));
            bad += Voir($"FXTRACT {n} ; FNSTSW ; FSTP m80 × 2 (387)",
                        W(0x20A) == sw && W(0x208) == s1 && Q(0x200) == g1 && W(0x214) == s2 && Q(0x20C) == g2,
                        $"{W(0x20A):X4}, {W(0x208):X4} {Q(0x200):X16}, {W(0x214):X4} {Q(0x20C):X16}",
                        $"{sw:X4}, {s1:X4} {g1:X16}, {s2:X4} {g2:X16}", true);
        }
        return bad;
    }

    /// <summary>L'arithmétique et les comparaisons du noyau (N4a), en mode matériel, aux attentes des sources (387 PRM,
    /// annexe B et § 4.5 ; SDM vol. 1, 2003, § 4.9.2 et table 4-7 ; SDM vol. 2 ; 287 PRM, table 1-17 ; Numerics
    /// Supplement, 1980, table S-27) : les drapeaux masqués de chaque exception (la part masquée de PB-59), la faute de
    /// pile qui rend l'indéfini, PC, les formes renversées, les comparaisons ordonnées, non ordonnées, en projectif.
    /// FNINIT ; le code ; FNSTSW [BX+0Ah] ; FSTP m80 [BX].</summary>
    private static int Arithmetique()
    {
        var bad = 0;
        var m2 = Le(0x4000000000000000);
        var dix = Le(0x4024000000000000);
        foreach (var (fpu, code, pas, op, sw, se, sig, n) in new (int, byte[], int, Action?, ushort, ushort, ulong, string)[]
                 {
                     (4, [0xDD, 0x06, 0x08, 0x03, 0xD9, 0xFA], 2, Ops((0x308, Le(0xBFF0000000000000))), 0x3801, 0xFFFF,
                      0xC000000000000000, "FLD m64 −1 ; FSQRT (IE, l'indéfini)"),
                     (4, [0xD9, 0xEE, 0xD9, 0xE0, 0xD9, 0xFA], 3, null, 0x3800, 0x8000, 0, "FLDZ ; FCHS ; FSQRT (−0)"),
                     (4, [0xDD, 0x06, 0x08, 0x03, 0xD9, 0xFA], 2, Ops((0x308, m2)), 0x3820, 0x3FFF, 0xB504F333F9DE6484,
                      "FLD m64 2 ; FSQRT (PE)"),
                     (4, [0xD9, 0xE8, 0xD9, 0xEE, 0xDE, 0xF9], 3, null, 0x3804, 0x7FFF, 0x8000000000000000,
                      "FLD1 ; FLDZ ; FDIVP (ZE, +∞)"),
                     (4, [0xD9, 0xEE, 0xD8, 0xF0], 2, null, 0x3801, 0xFFFF, 0xC000000000000000, "FLDZ ; FDIV ST(0), ST(0) (0/0, IE)"),
                     (4, [0xD9, 0xE8, 0xD9, 0xEE, 0xDE, 0xF9, 0xD9, 0xC0, 0xDE, 0xE9], 5, null, 0x3805, 0xFFFF, 0xC000000000000000,
                      "FLD1 ; FLDZ ; FDIVP ; FLD ST(0) ; FSUBP (∞ − ∞, IE)"),
                     (4, [0xDB, 0x2E, 0x20, 0x03, 0xD8, 0xC8], 2, Ops((0x320, Le80(0x7E7F, 0x8000000000000000))), 0x3A28, 0x7FFF,
                      0x8000000000000000, "FLD m80 2^16000 ; FMUL ST(0), ST(0) (OE, PE, C1, +∞)"),
                     (4, [0xDB, 0x2E, 0x20, 0x03, 0xD8, 0xC8], 2, Ops((0x320, Le80(0x017F, 0x8000000000000000))), 0x3830, 0x0000,
                      0, "FLD m80 2^−16000 ; FMUL ST(0), ST(0) (UE, PE, 0)"),
                     (4, [0xDB, 0x2E, 0x20, 0x03, 0xD9, 0xE8, 0xD8, 0xC9], 3, Ops((0x320, Le80(0x0000, 1))), 0x3002, 0x0000, 1,
                      "FLD m80 dénormal ; FLD1 ; FMUL ST(0), ST(1) (DE, exact)"),
                     (4, [0xD9, 0x2E, 0x10, 0x03, 0xD9, 0xE8, 0xDC, 0x36, 0x08, 0x03], 3, Ops((0x308, Trois), (0x310, Cw(0x007F))),
                      0x3A20, 0x3FFD, 0xAAAAAB0000000000, "CW 007Fh (24 bits) ; FLD1 ; FDIV m64 3 (PE, C1)"),
                     (4, [0xD9, 0xE8, 0xD8, 0xC3], 2, null, 0x3841, 0xFFFF, 0xC000000000000000,
                      "FLD1 ; FADD ST(0), ST(3) vide (IE, SF, l'indéfini)"),
                     (4, [0xDB, 0x2E, 0x20, 0x03, 0xD8, 0xC3], 2, Ops((0x320, Le80(0x7FFF, 0xFFFFFFFFFFFFFFFF))), 0x3841, 0xFFFF,
                      0xC000000000000000, "FLD m80 QNaN ; FADD ST(0), ST(3) vide (l'indéfini, pas le QNaN)"),
                     (1, [0xD9, 0xE8, 0xD8, 0xC3], 2, null, 0x3801, 0xFFFF, 0xC000000000000000,
                      "FLD1 ; FADD ST(0), ST(3) vide (le 8087 : IE seule)"),
                     (4, [0xD9, 0xE8, 0xDB, 0x2E, 0x20, 0x03, 0xD8, 0xC1], 3, Ops((0x320, Le80(0x7FFF, 0xA000000000000001))), 0x3001,
                      0x7FFF, 0xE000000000000001, "FLD1 ; FLD m80 SNaN ; FADD ST(0), ST(1) (IE, rendu silencieux)"),
                     (4, [0xDB, 0x2E, 0x20, 0x03, 0xD9, 0xE8, 0xD8, 0xC1], 3, Ops((0x320, Le80(0x3FFF, 0x4000000000000000))), 0x3001,
                      0xFFFF, 0xC000000000000000, "FLD m80 non normal ; FLD1 ; FADD ST(0), ST(1) (IE, l'indéfini)"),
                     (4, [0xDD, 0x06, 0x08, 0x03, 0xD9, 0xE8, 0xD8, 0xE9], 3, Ops((0x308, dix)), 0x3000, 0x4002, 0x9000000000000000,
                      "FLD m64 10 ; FLD1 ; FSUBR ST(0), ST(1) (10 − 1)"),
                     (4, [0xDD, 0x06, 0x08, 0x03, 0xD9, 0xE8, 0xDC, 0xE1, 0xDD, 0xD8], 4, Ops((0x308, dix)), 0x3800, 0xC002,
                      0x9000000000000000, "FLD m64 10 ; FLD1 ; FSUBR ST(1), ST(0) (DC E1 : 1 − 10) ; FSTP ST(0)"),
                     (4, [0xDD, 0x06, 0x08, 0x03, 0xD9, 0xE8, 0xDC, 0xE9, 0xDD, 0xD8], 4, Ops((0x308, dix)), 0x3800, 0x4002,
                      0x9000000000000000, "FLD m64 10 ; FLD1 ; FSUB ST(1), ST(0) (DC E9 : 10 − 1) ; FSTP ST(0)"),
                     (4, [0xDD, 0x06, 0x08, 0x03, 0xD9, 0xE8, 0xDE, 0xF1], 3, Ops((0x308, Le(0x4010000000000000))), 0x3800, 0x3FFD,
                      0x8000000000000000, "FLD m64 4 ; FLD1 ; FDIVRP ST(1), ST(0) (DE F1 : 1/4)"),
                     (4, [0xDD, 0x06, 0x08, 0x03, 0xD9, 0xE8, 0xDE, 0xF9], 3, Ops((0x308, Le(0x4010000000000000))), 0x3800, 0x4001,
                      0x8000000000000000, "FLD m64 4 ; FLD1 ; FDIVP ST(1), ST(0) (DE F9 : 4/1)"),
                     (4, [0xDB, 0x2E, 0x20, 0x03, 0xDD, 0x06, 0x08, 0x03, 0xD8, 0xC1], 3,
                      Ops((0x320, Le80(0x0000, 1)), (0x308, Le(0x7FF8000000000000))), 0x3000, 0x7FFF, 0xC000000000000000,
                      "FLD m80 dénormal ; FLD m64 QNaN ; FADD ST(0), ST(1) (le QNaN, sans DE)"),
                     (4, [0xD9, 0xEE, 0xDB, 0x2E, 0x20, 0x03, 0xD8, 0xF1], 3, Ops((0x320, Le80(0x0000, 1))), 0x3004, 0x7FFF,
                      0x8000000000000000, "FLDZ ; FLD m80 dénormal ; FDIV ST(0), ST(1) (ZE, sans DE)"),
                     (4, [0xDB, 0x2E, 0x20, 0x03, 0xD9, 0xE8, 0xD8, 0xC9], 3, Ops((0x320, Le80(0x0000, 0x8000000000000000))), 0x3002,
                      0x0001, 0x8000000000000000, "FLD m80 pseudo-dénormal ; FLD1 ; FMUL ST(0), ST(1) (DE, 2^−16382)"),
                     (4, [0xD9, 0x2E, 0x10, 0x03, 0xDD, 0x06, 0x08, 0x03, 0xD9, 0xFA], 3,
                      Ops((0x308, Le(0xBFF0000000000000)), (0x310, Cw(0x037E))), 0xB881, 0xBFFF, 0x8000000000000000,
                      "IM à 0 ; FLD m64 −1 ; FSQRT (arrêtée : −1 gardé ; IE, ES, B)"),
                     (4, [0xD9, 0x2E, 0x10, 0x03, 0xD9, 0xEE, 0xD9, 0xEE, 0xDE, 0xF9], 4, Ops((0x310, Cw(0x037E))), 0xB081, 0x0000, 0,
                      "IM à 0 ; FLDZ ; FLDZ ; FDIVP 0/0 (arrêtée, sans dépiler : TOP = 6)"),
                     (4, [0xDD, 0x06, 0x08, 0x03, 0xDE, 0x2E, 0x00, 0x03], 2, Ops((0x308, Le(0x4004000000000000)), (0x300, [10, 0])),
                      0x3800, 0x4001, 0xF000000000000000, "FLD m64 2,5 ; FISUBR m16 10 (7,5)"),
                     (5, [0xD9, 0xE8, 0xDA, 0x0E, 0x00, 0x03], 2, Ops((0x300, [0xFD, 0xFF, 0xFF, 0xFF])), 0x3800, 0xC000,
                      0xC000000000000000, "FLD1 ; FIMUL m32 −3 (486)"),
                     (4, [0xD9, 0xE8, 0xD8, 0x06, 0x08, 0x03], 2, Ops((0x308, Le(0x7F800001)[..4])), 0x3801, 0x7FFF,
                      0xC000010000000000, "FLD1 ; FADD m32 SNaN (IE, rendu silencieux au calcul)"),
                 })
        {
            Jouer87(fpu, [0xDB, 0xE3, .. code, .. SwA, 0xDB, 0x3F], pas + 3, op);
            // Une exception démasquée, avec l'acheminement de G13.6c (PB-204) : le verrou de l'AT retient FSTP m80, qui
            // attend ; rien n'est rangé.
            var (ase, asig) = (sw & 0x8000) != 0 && materiel.pb_204 ? ((ushort)0x9090, 0x9090909090909090) : (se, sig);
            bad += Voir($"{n} ; FNSTSW ; FSTP m80 (FPU {fpu})", W(0x20A) == sw && W(0x208) == ase && Q(0x200) == asig,
                        $"{W(0x20A):X4}, {W(0x208):X4} {Q(0x200):X16}", $"{sw:X4}, {ase:X4} {asig:X16}", true);
        }
        // Les comparaisons : FNINIT ; le code ; FNSTSW [BX+0Ah] (C3 C2 C0 = 4500h, non ordonné).
        foreach (var (fpu, code, pas, op, sw, n) in new (int, byte[], int, Action?, ushort, string)[]
                 {
                     (4, [0xD9, 0xE8, 0xDD, 0x06, 0x08, 0x03, 0xDD, 0xE1], 3, Ops((0x308, Le(0x7FF8000000000000))), 0x7500,
                      "FLD1 ; FLD m64 QNaN ; FUCOM ST(1) (non ordonné, sans IE)"),
                     (4, [0xD9, 0xE8, 0xDD, 0x06, 0x08, 0x03, 0xD8, 0xD1], 3, Ops((0x308, Le(0x7FF8000000000000))), 0x7501,
                      "FLD1 ; FLD m64 QNaN ; FCOM ST(1) (IE)"),
                     (4, [0xD9, 0xE8, 0xDB, 0x2E, 0x20, 0x03, 0xDD, 0xE1], 3, Ops((0x320, Le80(0x7FFF, 0xA000000000000000))), 0x7501,
                      "FLD1 ; FLD m80 SNaN ; FUCOM ST(1) (IE)"),
                     (4, [0xDD, 0x06, 0x00, 0x03, 0xDC, 0x16, 0x08, 0x03], 2, Ops((0x300, m2), (0x308, Le(1))), 0x3802,
                      "FLD m64 2 ; FCOM m64 2^−1074 (plus grand, DE)"),
                     (4, [0xD9, 0xE8, 0xD8, 0x16, 0x08, 0x03], 2, Ops((0x308, Le(0x7F800001)[..4])), 0x7D01,
                      "FLD1 ; FCOM m32 SNaN (IE)"),
                     (4, [0xD9, 0xEE, 0xD9, 0xEE, 0xD9, 0xE0, 0xDE, 0xD9], 4, null, 0x4000, "FLDZ ; FLDZ ; FCHS ; FCOMPP (égaux)"),
                     (4, [0xDF, 0x06, 0x08, 0x03, 0xDE, 0x16, 0x08, 0x03], 2, Ops((0x308, [0xFB, 0xFF])), 0x7800,
                      "FILD m16 −5 ; FICOM m16 −5 (égaux)"),
                     (4, [0xD9, 0xE8, 0xD9, 0xE0, 0xD9, 0xE4], 3, null, 0x3900, "FLD1 ; FCHS ; FTST (plus petit)"),
                     (4, [0xD9, 0xE8, 0xD9, 0xE0, 0xD9, 0xE5, 0xD9, 0xE4], 4, null, 0x3900,
                      "FLD1 ; FCHS ; FXAM (C1) ; FTST (C1 à zéro)"),
                     (4, [0xD8, 0xD9], 1, null, 0x4D41, "FCOMP ST(1), la pile vide (IE, SF, non ordonné, dépilé)"),
                     (4, [0xD9, 0xE8, 0xD9, 0xEE, 0xDE, 0xF9, 0xD9, 0xE8, 0xD8, 0xD1], 5, null, 0x3104,
                      "FLD1 ; FLDZ ; FDIVP ; FLD1 ; FCOM ST(1) (1 < +∞, affine)"),
                     (2, [0xD9, 0xE8, 0xD9, 0xEE, 0xDE, 0xF9, 0xD9, 0xE8, 0xD8, 0xD1], 5, null, 0x7505,
                      "FLD1 ; FLDZ ; FDIVP ; FLD1 ; FCOM ST(1) (le 287, projectif : IE, non comparable)"),
                     (2, [0xD9, 0xE8, 0xD9, 0xEE, 0xDE, 0xF9, 0xD9, 0xE4], 4, null, 0x7D05, "FLD1 ; FLDZ ; FDIVP ; FTST (le 287, projectif)"),
                     (1, [0xD9, 0xE8, 0xD9, 0xEE, 0xDE, 0xF9, 0xD9, 0xC0, 0xD9, 0xE0, 0xDE, 0xD9], 6, null, 0x4004,
                      "FLD1 ; FLDZ ; FDIVP ; FLD ST(0) ; FCHS ; FCOMPP (le 8087, projectif : −∞ = +∞)"),
                 })
        {
            Jouer87(fpu, [0xDB, 0xE3, .. code, .. SwA], pas + 2, op);
            bad += Voir($"{n} ; FNSTSW (FPU {fpu})", W(0x20A) == sw, $"{W(0x20A):X4}", $"{sw:X4}", true);
        }
        return bad;
    }

    /// <summary>Les exceptions démasquées du noyau (N5 ; PB-59), sur le 387, aux attentes des sources : avant le calcul (IS,
    /// I, D, Z), l'instruction s'arrête, rien n'est rangé ni dépilé (SDM vol. 1, 2003, § 8.5.1 à 8.5.3) ; OE et UE, la
    /// destination un registre, le résultat rangé à l'exposant décalé de 24 576, arrondi selon PC et RC, C1 compris, UE
    /// levée pour un résultat petit même exact, et FSCALE au-delà du décalage, ±∞ ou ±0 (387 PRM § 3.2.5.1 et § 3.2.5.2 ;
    /// SDM vol. 1, 2003, § 8.5.4 et § 8.5.5) ; la destination en mémoire, rien n'est rangé, la pile intacte, ni PE ni C1
    /// (§ 8.5.6) ; PE, le résultat rangé (387 PRM § 3.2.6) ; FLD d'un dénormal, DE démasquée, empile quand même (SDM vol.
    /// 2, FLD). FNINIT ; FLDCW [0310h] ; le code ; FNSTSW [BX+0Ah] ; FSTP m80 [BX] (retenu par le verrou de l'AT sous
    /// l'acheminement, PB-204 : la mémoire intacte).</summary>
    private static int Demasquees()
    {
        var bad = 0;
        const byte FLDCW = 0xD9, FLD80 = 0xDB;
        byte[] pre = [FLDCW, 0x2E, 0x10, 0x03];
        byte[] m80 = [FLD80, 0x2E, 0x20, 0x03];
        byte[] deux = [0xDD, 0x06, 0x08, 0x03, 0xDD, 0x06, 0x00, 0x03];
        const ulong J = 0x8000000000000000;
        foreach (var (cw, code, pas, op, sw, se, sig, n) in new (ushort, byte[], int, Action?, ushort, ushort, ulong, string)[]
                 {
                     (0x0377, [.. m80, 0xD8, 0xC8], 2, Ops((0x320, Le80(0x7E7F, J))), 0xB888, 0x5CFF, J,
                      "OM à 0 ; FLD m80 2^16000 ; FMUL ST(0), ST(0) (2^32000 décalé : 2^7424 ; OE seule, exact)"),
                     (0x036F, [.. m80, 0xD8, 0xC8], 2, Ops((0x320, Le80(0x017F, J))), 0xB890, 0x22FF, J,
                      "UM à 0 ; FLD m80 2^−16000 ; FMUL ST(0), ST(0) (2^−32000 décalé : 2^−7424 ; UE)"),
                     (0x036F, [.. m80, 0xD9, 0xE8, 0xD8, 0xC9], 3, Ops((0x320, Le80(0x0000, 1))), 0xB092, 0x5FC2, J,
                      "UM à 0 ; FLD m80 dénormal ; FLD1 ; FMUL ST(0), ST(1) (2^−16445 exact, petit : UE et DE, décalé)"),
                     (0x0077, [.. m80, 0xD8, 0xC8], 2, Ops((0x320, Le80(0x7E7F, 0xFFFFFFFFFFFFFFFF))), 0xBAA8, 0x5D01, J,
                      "OM à 0, PC 24 bits ; FLD m80 2^16000 × (2 − 2^−63) ; FMUL ST(0), ST(0) (arrondi à 24 bits : 2^32002 décalé ; PE, C1)"),
                     (0x0377, [.. deux, 0xD9, 0xFD], 3, Ops((0x300, Le(0x3FF0000000000000)), (0x308, Le(0x40D3880000000000))), 0xB088,
                      0x2E1F, J, "OM à 0 ; FSCALE 1 par 20 000 (2^20000 décalé : 2^−4576)"),
                     (0x0377, [.. deux, 0xD9, 0xFD], 3, Ops((0x300, Le(0x3FF0000000000000)), (0x308, Le(0x40ED4C0000000000))), 0xB2A8,
                      0x7FFF, J, "OM à 0 ; FSCALE 1 par 60 000 (au-delà du décalage : +∞, OE, PE, C1)"),
                     (0x036F, [.. deux, 0xD9, 0xFD], 3, Ops((0x300, Le(0x3FF0000000000000)), (0x308, Le(0xC0ED4C0000000000))), 0xB0B0,
                      0x0000, 0, "UM à 0 ; FSCALE 1 par −60 000 (au-delà du décalage : +0, UE, PE)"),
                     (0x0377, [.. m80, 0xD9, 0xC0, 0xDE, 0xC1], 3, Ops((0x320, Le80(0x7FFE, J))), 0xB888, 0x1FFF, J,
                      "OM à 0 ; FLD m80 2^16383 ; FLD ST(0) ; FADDP (2^16384 décalé : 2^−8192 ; rangé et dépilé, TOP = 7)"),
                     (0x036F, [FLD80, 0x2E, 0x30, 0x03, .. m80, 0xD9, 0xF8], 3,
                      Ops((0x330, Le80(0x0002, J)), (0x320, Le80(0x0002, 0x8040000000000000))), 0xB290, 0x5FF9, J,
                      "UM à 0 ; FPREM (2^−16381 + 2^−16390) par 2^−16381 (le reste 2^−16390, exact et petit : UE, décalé ; Q = 1, C1)"),
                     (0x037F, [FLD80, 0x2E, 0x30, 0x03, .. m80, 0xD9, 0xF8], 3,
                      Ops((0x330, Le80(0x0002, J)), (0x320, Le80(0x0002, 0x8040000000000000))), 0x3200, 0x0000, 0x0080000000000000,
                      "masquées ; le même FPREM (le reste dénormal, exact : ni UE ni PE)"),
                     (0x037D, [.. m80, 0xD9, 0xE8, 0xD8, 0xC9], 3, Ops((0x320, Le80(0x0000, 1))), 0xB082, 0x3FFF, J,
                      "DM à 0 ; FLD m80 dénormal ; FLD1 ; FMUL ST(0), ST(1) (arrêtée : 1 gardé, TOP = 6)"),
                     (0x037B, [0xD9, 0xEE, 0xD9, 0xF4], 2, null, 0xB884, 0x0000, 0,
                      "ZM à 0 ; FLDZ ; FXTRACT (arrêtée : 0 gardé, rien d'empilé)"),
                     (0x037D, [0xD9, 0x06, 0x00, 0x03], 1, Ops((0x300, Le(1)[..4])), 0xB882, 0x3F6A, J,
                      "DM à 0 ; FLD m32 2^−149 (DE, empilé quand même)"),
                     (0x037D, [0xDD, 0x06, 0x00, 0x03], 1, Ops((0x300, Le(1))), 0xB882, 0x3BCD, J,
                      "DM à 0 ; FLD m64 2^−1074 (DE, empilé quand même)"),
                     (0x035F, [0xD9, 0xE8, 0xDC, 0x36, 0x08, 0x03], 2, Ops((0x308, Trois)), 0xBAA0, 0x3FFD, 0xAAAAAAAAAAAAAAAB,
                      "PM à 0 ; FLD1 ; FDIV m64 3 (rangé, arrondi vers le haut : PE, C1)"),
                     (0x0777, [.. m80, 0xDC, 0x36, 0x08, 0x03], 2, Ops((0x320, Le80(0x7E7F, J)), (0x308, Le(0x0188000000000000))),
                      0xB8A8, 0x2265, 0xAAAAAAAAAAAAAAAA,
                      "OM à 0, vers −∞ ; FLD m80 2^16000 ; FDIV m64 3 × 2^−1000 (4/3 × 2^16998 décalé, tronqué : OE, PE, sans C1)"),
                     (0x036F, [0xD9, 0xE8, .. m80, 0xD9, 0xF8], 3, Ops((0x320, Le80(0x0000, 1))), 0xB092, 0x5FC2, J,
                      "UM à 0 ; FLD1 ; FLD m80 dénormal ; FPREM (Q = 0, le dividende rendu, petit : UE et DE, décalé)"),
                 })
        {
            Jouer87(4, [0xDB, 0xE3, .. pre, .. code, .. SwA, 0xDB, 0x3F], pas + 4,
                    () => { op?.Invoke(); mem.ram[0x310] = (byte)cw; mem.ram[0x311] = (byte)(cw >> 8); });
            var (ase, asig) = (sw & 0x8000) != 0 && materiel.pb_204 ? ((ushort)0x9090, 0x9090909090909090) : (se, sig);
            bad += Voir($"{n} ; FNSTSW ; FSTP m80 (387)", W(0x20A) == sw && W(0x208) == ase && Q(0x200) == asig,
                        $"{W(0x20A):X4}, {W(0x208):X4} {Q(0x200):X16}", $"{sw:X4}, {ase:X4} {asig:X16}", true);
        }
        // La destination en mémoire : FST m32 [BX] (D9 17) ou FST m64 [BX] (DD 17) ; FNSTSW [BX+0Ah] ; FSTP m80 [BX+0Ch]
        // (DB 7F 0C), la pile.
        // FSTP (modrm 1Fh) : rien de rangé, la pile intacte, sous OE ou UE démasquée ; rangé et dépilé sous PE démasquée
        // (FSTP m80 lit alors la pile vide : l'indéfini).
        foreach (var (cw, st, modrm, se0, sig0, mem8, sw, pse, psig, n) in new (ushort, byte, byte, ushort, ulong, ulong, ushort, ushort, ulong, string)[]
                 {
                     (0x0377, 0xD9, 0x17, 0x40C7, J, 0x9090909090909090, 0xB888, 0x40C7, J,
                      "OM à 0 ; FLD m80 2^200 ; FST m32 (rien de rangé ; OE seule, ni PE ni C1)"),
                     (0x036F, 0xD9, 0x17, 0x3F73, J, 0x9090909090909090, 0xB890, 0x3F73, J,
                      "UM à 0 ; FLD m80 2^−140 ; FST m32 (exact mais petit : UE, rien de rangé)"),
                     (0x037F, 0xD9, 0x17, 0x3F73, J, 0x9090909000000200, 0x3800, 0x3F73, J,
                      "masquées ; FLD m80 2^−140 ; FST m32 (le dénormal exact, sans UE)"),
                     (0x036F, 0xDD, 0x17, 0x0000, 1, 0x9090909090909090, 0xB892, 0x0000, 1,
                      "UM à 0 ; FLD m80 dénormal ; FST m64 (DE, puis UE : rien de rangé)"),
                     (0x0377, 0xD9, 0x1F, 0x40C7, J, 0x9090909090909090, 0xB888, 0x40C7, J,
                      "OM à 0 ; FLD m80 2^200 ; FSTP m32 (rien de rangé ni dépilé : TOP = 7)"),
                     (0x035F, 0xDD, 0x1F, 0x3FFF, J | 1, 0x3FF0000000000000, 0x80A0, 0xFFFF, 0xC000000000000000,
                      "PM à 0 ; FLD m80 1 + 2^−63 ; FSTP m64 (1 rangé et dépilé, TOP = 0 ; PE, sans C1)"),
                 })
        {
            Jouer87(4, [0xDB, 0xE3, .. pre, .. m80, st, modrm, .. SwA, 0xDB, 0x7F, 0x0C], 6,
                    () => { Ops((0x320, Le80(se0, sig0)))(); mem.ram[0x310] = (byte)cw; mem.ram[0x311] = (byte)(cw >> 8); });
            var (ase, asig) = (sw & 0x8000) != 0 && materiel.pb_204 ? ((ushort)0x9090, 0x9090909090909090) : (pse, psig);
            bad += Voir($"{n} ; FNSTSW ; FSTP m80 (387)", Q(0x200) == mem8 && W(0x20A) == sw && W(0x214) == ase && Q(0x20C) == asig,
                        $"[BX] {Q(0x200):X16}, {W(0x20A):X4}, {W(0x214):X4} {Q(0x20C):X16}",
                        $"[BX] {mem8:X16}, {sw:X4}, {ase:X4} {asig:X16}", true);
        }
        return bad;
    }

    /// <summary>Les instructions de N3a sans PB propre, en mode matériel, aux attentes des sources (387 PRM, annexe B et
    /// tables 3-6 à 3-10 ; SDM vol. 2 ; 486 PRM, table 15-1) : les constantes selon RC (le 287, au plus près) ; FXCH et
    /// FLD ST(i) sur un registre vide ; FCHS, FABS ; FXAM sur chaque classe ; FST m32 en dépassement, d'un format non
    /// supporté, d'un dénormal ; FISTP m64 selon RC, C1 compris ; FILD m64 exact ; FST ST(i). Le mot d'état lu par FNSTSW
    /// [BX+0Ah].</summary>
    private static int Mouvements()
    {
        var bad = 0;
        // Les constantes : FNINIT ; FLDCW [0310h] ; FLDPI ou FLDLG2 ; FSTP m80.
        foreach (var (fpu, cw, op, sig) in new (int, ushort, byte, ulong)[]
                 {
                     (4, 0x037F, 0xEB, 0xC90FDAA22168C235), (4, 0x077F, 0xEB, 0xC90FDAA22168C234),
                     (4, 0x0B7F, 0xEB, 0xC90FDAA22168C235), (4, 0x037F, 0xEC, 0x9A209A84FBCFF799),
                     (4, 0x0F7F, 0xEC, 0x9A209A84FBCFF798), (2, 0x077F, 0xEB, 0xC90FDAA22168C235),
                     (5, 0x0F7F, 0xED, 0xB17217F7D1CF79AB), (4, 0x0B7F, 0xE9, 0xD49A784BCD1B8AFF),
                     (4, 0x037F, 0xE9, 0xD49A784BCD1B8AFE), (4, 0x037F, 0xEA, 0xB8AA3B295C17F0BC),
                     (4, 0x077F, 0xEA, 0xB8AA3B295C17F0BB),
                 })
        {
            Jouer87(fpu, [0xDB, 0xE3, 0xD9, 0x2E, 0x10, 0x03, 0xD9, op, 0xDB, 0x3F], 4,
                    () => { mem.ram[0x310] = (byte)cw; mem.ram[0x311] = (byte)(cw >> 8); });
            bad += Voir($"CW {cw:X4} ; D9 {op:X2} ; FSTP m80 (FPU {fpu})", Q(0x200) == sig, $"{Q(0x200):X16}", $"{sig:X16}", true);
        }
        // Les opérations de registre, sur le 387 : le mot d'état, puis ST(0) rangé en m80.
        foreach (var (code, pas, op, sw, se, sig, n) in new (byte[], int, byte[], ushort, ushort, ulong, string)[]
                 {
                     ([0xDB, 0xE3, 0xD9, 0xE8, 0xD9, 0xC9, 0xD9, 0xE5], 4, [], 0x3B41, 0xFFFF, 0xC000000000000000,
                      "FLD1 ; FXCH ST(1) vide ; FXAM (l'indéfini, pas le vide)"),
                     ([0xDB, 0xE3, 0xD9, 0xC3], 2, [], 0x3841, 0xFFFF, 0xC000000000000000, "FLD ST(3) vide"),
                     ([0xDB, 0xE3, 0xD9, 0xE8, 0xD9, 0xE0], 3, [], 0x3800, 0xBFFF, 0x8000000000000000, "FLD1 ; FCHS"),
                     ([0xDB, 0xE3, 0xDD, 0x06, 0x00, 0x03, 0xD9, 0xE1], 3, Le(0xC000000000000000), 0x3800, 0x4000,
                      0x8000000000000000, "FLD m64 −2 ; FABS"),
                     ([0xDB, 0xE3, 0xDF, 0x2E, 0x00, 0x03], 2, Le(0x7FFFFFFFFFFFFFFF), 0x3800, 0x403D, 0xFFFFFFFFFFFFFFFE,
                      "FILD m64 2^63 − 1"),
                     ([0xDB, 0xE3, 0xD9, 0xE8, 0xD9, 0xEE, 0xDD, 0xD1, 0xDD, 0xD8], 5, [], 0x3800, 0x0000, 0, "FLD1 ; FLDZ ; FST ST(1) ; FSTP ST(0)"),
                     ([0xDB, 0xE3, 0xD9, 0xE8, 0xD9, 0xE0, 0xD9, 0xE5, 0xD9, 0xE8], 5, [], 0x3400, 0x3FFF, 0x8000000000000000,
                      "FLD1 ; FCHS ; FXAM (C1, C2) ; FLD1 (C1 à zéro)"),
                     ([0xDB, 0xE3, .. Enumerable.Repeat<byte[]>([0xD9, 0xE8], 8).SelectMany(b => b), 0xD9, 0x06, 0x00, 0x03], 10,
                      Le(0x3F800000)[..4], 0x3A41, 0xFFFF, 0xC000000000000000, "FLD1 × 8 ; FLD m32 (débordement, C1)"),
                     ([0xDB, 0xE3, 0xDF, 0x06, 0x00, 0x03], 2, [0xFB, 0xFF], 0x3800, 0xC001, 0xA000000000000000, "FILD m16 −5"),
                     ([0xDB, 0xE3, 0xD9, 0x06, 0x00, 0x03], 2, Le(1)[..4], 0x3802, 0x3F6A, 0x8000000000000000,
                      "FLD m32 2^−149 (DE, normalisé)"),
                     ([0xDB, 0xE3, 0xD9, 0xC9], 2, [], 0x0041, 0xFFFF, 0xC000000000000000, "FXCH ST(1), les deux vides"),
                 })
        {
            Jouer87(4, [.. code, .. SwA, 0xDB, 0x3F], pas + 2, Op(op));
            bad += Voir($"{n} ; FNSTSW ; FSTP m80 (387)", W(0x20A) == sw && W(0x208) == se && Q(0x200) == sig,
                        $"{W(0x20A):X4}, {W(0x208):X4} {Q(0x200):X16}", $"{sw:X4}, {se:X4} {sig:X16}", true);
        }
        // FXAM : FLD m80 ; FXAM ; FNSTSW [BX+0Ah] (TOP = 7) ; et la pile vide.
        foreach (var (se, sig, sw, n) in new (ushort, ulong, ushort, string)[]
                 {
                     (0x0000, 0x0000000000000001, 0x7C00, "dénormal"), (0x0000, 0x8000000000000000, 0x7C00, "pseudo-dénormal"),
                     (0x3FFF, 0x4000000000000000, 0x3800, "non normal (non supporté)"), (0x7FFF, 0x8000000000000000, 0x3D00, "+∞"),
                     (0x8000, 0x0000000000000000, 0x7A00, "−0"), (0xFFFF, 0xC000000000000000, 0x3B00, "l'indéfini, −QNaN"),
                     (0x3FFF, 0x8000000000000000, 0x3C00, "1"),
                 })
        {
            Jouer87(4, [0xDB, 0xE3, 0xDB, 0x2E, 0x00, 0x03, 0xD9, 0xE5, .. SwA], 4, Op(Le80(se, sig)));
            bad += Voir($"FXAM, {n} (387)", W(0x20A) == sw, $"{W(0x20A):X4}", $"{sw:X4}", true);
        }
        Jouer87(4, [0xDB, 0xE3, 0xD9, 0xE5, .. SwA], 3);
        bad += Voir("FXAM, la pile vide (387)", W(0x20A) == 0x4100, $"{W(0x20A):X4}", "4100", true);
        // FST m32 [BX] (D9 17) depuis un m80 : 2^200, l'infini, OE, PE et C1 ; un non normal, IE et l'indéfini ; le plus
        // petit dénormal de 80 bits, DE, UE et PE, zéro.
        foreach (var (se, sig, r, sw, n) in new (ushort, ulong, uint, ushort, string)[]
                 {
                     (0x40C7, 0x8000000000000000, 0x7F800000, 0x3A28, "2^200"),
                     (0x3FFF, 0x4000000000000000, 0xFFC00000, 0x3801, "un non normal"),
                     (0x0000, 0x0000000000000001, 0x00000000, 0x3832, "un dénormal de 80 bits"),
                 })
        {
            Jouer87(4, [0xDB, 0xE3, 0xDB, 0x2E, 0x00, 0x03, 0xD9, 0x17, .. SwA], 4, Op(Le80(se, sig)));
            var v = (uint)(W(0x200) | W(0x202) << 16);
            bad += Voir($"FLD m80 {n} ; FST m32 (387)", v == r && W(0x20A) == sw, $"{v:X8}, {W(0x20A):X4}", $"{r:X8}, {sw:X4}", true);
        }
        // FST m64 [BX] (DD 17) d'un SNaN de 80 bits (7FFFh A000000000000000h) : IE, le QNaN 7FFC000000000000h.
        Jouer87(4, [0xDB, 0xE3, 0xDB, 0x2E, 0x00, 0x03, 0xDD, 0x17, .. SwA], 4, Op(Le80(0x7FFF, 0xA000000000000000)));
        bad += Voir("FLD m80 SNaN ; FST m64 (387)", Q(0x200) == 0x7FFC000000000000 && W(0x20A) == 0x3801,
                    $"{Q(0x200):X16}, {W(0x20A):X4}", "7FFC000000000000, 3801", true);
        // FSTP m80 [BX] de la pile vide : l'indéfini, IE et SF, TOP = 1 après le dépilement.
        Jouer87(4, [0xDB, 0xE3, 0xDB, 0x3F, .. SwA], 3);
        bad += Voir("FSTP m80, la pile vide (387)", W(0x208) == 0xFFFF && Q(0x200) == 0xC000000000000000 && W(0x20A) == 0x0841,
                    $"{W(0x208):X4} {Q(0x200):X16}, {W(0x20A):X4}", "FFFF C000000000000000, 0841", true);
        // Démasquées (FLDCW [0310h]) : IM à 0, FLD m32 d'un SNaN n'empile rien (TOP = 0 ; IE, ES et B : 8081h) ; IM à 0,
        // FISTP m16 de 40 000 ne range ni ne dépile (le mot en [BX] reste 9090h ; TOP = 7 : B881h) ; PM à 0, FISTP m32
        // de 2,5 range 2 et dépile, puis lève (PE, ES et B : 80A0h).
        foreach (var (cw, code, op, mot, sw, n) in new (ushort, byte[], byte[], uint, ushort, string)[]
                 {
                     (0x037E, [0xD9, 0x06, 0x00, 0x03], Le(0x7F800001)[..4], 0x90909090, 0x8081, "IM à 0 ; FLD m32 SNaN"),
                     (0x037E, [0xDD, 0x06, 0x00, 0x03, 0xDF, 0x1F], Le(0x40E3880000000000), 0x90909090, 0xB881,
                      "IM à 0 ; FLD m64 40 000 ; FISTP m16"),
                     (0x035F, [0xDD, 0x06, 0x00, 0x03, 0xDB, 0x1F], Le(0x4004000000000000), 0x00000002, 0x80A0,
                      "PM à 0 ; FLD m64 2,5 ; FISTP m32"),
                 })
        {
            Jouer87(4, [0xDB, 0xE3, 0xD9, 0x2E, 0x10, 0x03, .. code, .. SwA], code.Length == 4 ? 4 : 5,
                    () => { Op(op)(); mem.ram[0x310] = (byte)cw; mem.ram[0x311] = (byte)(cw >> 8); });
            var m = (uint)(W(0x200) | W(0x202) << 16);
            bad += Voir($"{n} ; FNSTSW (387)", m == mot && W(0x20A) == sw, $"[BX] {m:X8}, {W(0x20A):X4}", $"[BX] {mot:X8}, {sw:X4}", true);
        }
        // FISTP m64 [BX] (DF 3F) : 2,5 au plus près → 2, PE ; 3,5 → 4, PE et C1 ; 2,1 vers +∞ → 3, PE et C1.
        foreach (var (v, cw, r, sw) in new (ulong, ushort, ulong, ushort)[]
                 {
                     (0x4004000000000000, 0x037F, 2, 0x0020), (0x400C000000000000, 0x037F, 4, 0x0220),
                     (0x4000CCCCCCCCCCCD, 0x0B7F, 3, 0x0220),
                 })
        {
            Jouer87(4, [0xDB, 0xE3, 0xD9, 0x2E, 0x10, 0x03, 0xDD, 0x06, 0x00, 0x03, 0xDF, 0x3F, .. SwA], 5,
                    () => { Op(Le(v))(); mem.ram[0x310] = (byte)cw; mem.ram[0x311] = (byte)(cw >> 8); });
            bad += Voir($"CW {cw:X4} ; FLD m64 {v:X16} ; FISTP m64 (387)", Q(0x200) == r && W(0x20A) == sw,
                        $"{Q(0x200)}, {W(0x20A):X4}", $"{r}, {sw:X4}", true);
        }
        return bad;
    }

    /// <summary>Le noyau avec l'acheminement de G13.6c (--hardware-mode PB-204,PB-264). Le port F1h de l'AT, le mot de
    /// contrôle 0040h et PE posés : le 287 et le 287XL sortent en 037Fh et 0000h, le 387 en 037Eh et 8081h (leur RESET,
    /// PB-203) ; le 486, que F1h ne touche pas, en 0040h et 0020h. Une lecture de pile démasquée (IM à 0), puis FNOP, qui
    /// attend : le verrou de l'AT (287, 387) ou l'exception en attente du 486 (NE nul) l'arrêtent, IP = 0100h.</summary>
    private static int Acheminement(bool hw)
    {
        var bad = 0;
        model_c.at_coprocesseur_init();
        try
        {
            foreach (var (fpu, nom) in FpusXL.Where(f => f.fpu >= 2))
            {
                var (sw, cw) = Mots(fpu, [0xE6, 0xF1, 0xDD, 0x3F, 0xD9, 0x7F, 0x02], 3, () =>
                {
                    _386_common.cpu_state.npxc = 0x0040;
                    _386_common.cpu_state.npxs = 0x0020;
                });
                var (acw, asw) = !hw || fpu == 5 ? (0x0040, 0x0020) : fpu == 4 ? (0x037E, 0x8081) : (0x037F, 0x0000);
                bad += Voir($"CW 0040h, PE ; OUT F1h ; FNSTSW ; FNSTCW ({nom})", (cw, sw) == (acw, asw), $"{cw:X4} et {sw:X4}",
                            $"{acw:X4} et {asw:X4}", hw);
                bad += Avec8259(() =>
                {
                    Pic(true);
                    _386.FuzzFpu = fpu;
                    try
                    {
                        var r = Jouer386([0xD9, 0xD0], _ => { }, () =>
                        {
                            _x87_noyau.finit();
                            _386_common.cpu_state.npxc = 0x037E;
                            _x87_noyau.lire(1, out _);
                        }, coeur: Coeur(fpu));
                        _x87_materiel.verrou = _x87_materiel.ignne = false;
                        var ip = r[(int)R.IP];
                        var aip = hw ? 0x0100 : 0x0102;
                        return Voir($"IM à 0 ; ST(1) lu vide ; FNOP ({nom})", ip == aip, $"IP = {ip:X4}", $"IP = {aip:X4}", hw);
                    }
                    finally
                    {
                        _386.FuzzFpu = 0;
                    }
                });
            }
        }
        finally
        {
            io.io_removehandler(0x00f0, 0x0002, null, null, null, _x87_materiel.ecrire_f0_f1, null, null, null);
        }
        return bad;
    }

    /// <summary>Les instructions de contrôle du noyau (N2), sur chaque coprocesseur, par les instructions dans les deux
    /// modes. FNINIT ; FLDCW [BX+4] = 0B7Fh ; FNOP ; FNSTCW [BX+2] ; FNSTSW [BX] → 0B7Fh et 0000h (témoin). C1 posé ;
    /// FINCSTP (ou FDECSTP) ; FNSTSW → TOP = 1 (ou 7) et, sur le 387, le 486 et le 287XL, C1 à zéro (486 PRM, table
    /// 15-1) : 0800h (3800h) ; le 8087 et le 287 (C1 indéfini, laissé) et PCem 0A00h (3A00h). FNCLEX sous B, C3-C0 et les six exceptions : 4700h ;
    /// PCem garde B, C700h (PB-59, que le noyau ne reproduit pas). FFREE ST(1) après deux empilements : ST(1) vide, ST(0)
    /// non (témoin), lu dans le mot d'étiquettes, celui de l'architecture : DFFFh (FLD1, FLDZ). TOP = 7 ; FINCSTP : TOP = 0
    /// (témoin du repli). FNSTSW AX, TOP = 5 (287 et après) : 2800h ; PCem 0000h (PB-61, de même). Les formes a32 de FLDCW,
    /// FNSTCW et FNSTSW (386, 486 ; témoin).</summary>
    private static int Controle(bool hw, bool noyau)
    {
        var bad = 0;
        foreach (var (fpu, nom) in FpusXL)
        {
            var (sw, cw) = Mots(fpu, [0xDB, 0xE3, 0xD9, 0x6F, 0x04, 0xD9, 0xD0, 0xD9, 0x7F, 0x02, 0xDD, 0x3F], 5,
                                () => { mem.ram[0x204] = 0x7F; mem.ram[0x205] = 0x0B; });
            bad += Voir($"FNINIT ; FLDCW ; FNOP ; FNSTCW ; FNSTSW ({nom}, témoin)", (cw, sw) == (0x0B7F, 0x0000),
                        $"{cw:X4} et {sw:X4}", "0B7F et 0000", hw);

            int att;
            foreach (var (op, n, top) in new (byte, string, int)[] { (0xF7, "FINCSTP", 1), (0xF6, "FDECSTP", 7), (0xF7, "TOP = 7 ; FINCSTP", 0) })
            {
                (sw, _) = Mots(fpu, [0xD9, op, 0xDD, 0x3F], 2, () =>
                {
                    _386_common.cpu_state.npxs = 0x0200;
                    _386_common.cpu_state.TOP = top == 0 ? 7 : 0;
                });
                att = top << 11 | (hw && fpu >= 3 ? 0 : 0x0200);
                bad += Voir($"C1 ; {n} ; FNSTSW ({nom})", sw == att, $"{sw:X4}", $"{att:X4}", hw);
            }

            (sw, _) = Mots(fpu, [0xDB, 0xE2, 0xDD, 0x3F], 2, () => _386_common.cpu_state.npxs = 0xC7FF);
            att = hw ? 0x4700 : 0xC700;
            bad += Voir($"B, C3-C0, ES, SF et les exceptions ; FNCLEX ; FNSTSW ({nom})", sw == att, $"{sw:X4}", $"{att:X4}", hw);

            ushort tw;
            if (noyau)
            {
                Mots(fpu, [0xDD, 0xC1], 1, () =>
                {
                    _x87_noyau.finit();
                    _x87_noyau.empiler(Un);
                    _x87_noyau.empiler(new extFloat80_t());
                });
                tw = _x87_noyau.etiquettes;
            }
            else
            {
                Mots(fpu, [0xDB, 0xE3, 0xD9, 0xE8, 0xD9, 0xEE, 0xDD, 0xC1], 4);
                tw = x87_c.x87_gettag();
            }
            bad += Voir($"1 et 0 empilés ; FFREE ST(1) ; le mot d'étiquettes ({nom}, témoin)", tw == 0xDFFF, $"{tw:X4}",
                        "DFFF (ST(0) = R6 zéro, R7 libéré)", hw);

            if (fpu == 1)
                continue;
            var ax = X87(fpu, [0xDB, 0xE3, 0xD9, 0xF6, 0xD9, 0xF6, 0xD9, 0xF6, 0xDF, 0xE0], 5);
            att = hw ? 0x2800 : 0x0000;
            bad += Voir($"FNINIT ; FDECSTP ×3 ; FNSTSW AX ({nom})", ax == att, $"{ax:X4}", $"{att:X4}", hw);

            if (fpu < 4)
                continue;
            (sw, cw) = Mots(fpu, [0xDB, 0xE3, 0x67, 0xD9, 0x6B, 0x04, 0x67, 0xD9, 0x7B, 0x02, 0x67, 0xDD, 0x3B], 4,
                            () => { mem.ram[0x204] = 0x7F; mem.ram[0x205] = 0x0B; });
            bad += Voir($"FNINIT ; FLDCW, FNSTCW et FNSTSW en a32 ({nom}, témoin)", (cw, sw) == (0x0B7F, 0x0000),
                        $"{cw:X4} et {sw:X4}", "0B7F et 0000", hw);
        }
        return bad;
    }

    private static bool Egal(in extFloat80_t a, in extFloat80_t b) => a.signExp == b.signExp && a.signif == b.signif;

    /// <summary>La couche du noyau, hors des instructions : les classes d'un opérande (387 PRM § 3.1 et table 3-9), les
    /// formats que le 387 ne supporte plus (§ 3.1.7), le mot de contrôle vers SoftFloat et ses indicateurs vers le mot
    /// d'état, C1 compris (SDM vol. 1, 2016, § 8.1.3.2 ; 2003, § 8.1.2.2).</summary>
    private static int Couche()
    {
        var bad = 0;
        foreach (var (se, sig, c) in new (ushort, ulong, _x87_noyau.Classe)[]
                 {
                     (0x0000, 0, _x87_noyau.Classe.Zero), (0x8000, 0, _x87_noyau.Classe.Zero),
                     (0x3FFF, 0x8000000000000000, _x87_noyau.Classe.Normal),
                     (0x0000, 0x0000000000000001, _x87_noyau.Classe.Denormal),
                     (0x0000, 0x8000000000000000, _x87_noyau.Classe.PseudoDenormal),
                     (0x3FFF, 0x4000000000000000, _x87_noyau.Classe.NonNormal),
                     (0x3FFF, 0x0000000000000000, _x87_noyau.Classe.NonNormal),
                     (0x7FFF, 0x8000000000000000, _x87_noyau.Classe.Infini),
                     (0x7FFF, 0x0000000000000000, _x87_noyau.Classe.PseudoInfini),
                     (0x7FFF, 0x4000000000000000, _x87_noyau.Classe.PseudoNaN),
                     (0x7FFF, 0xC000000000000000, _x87_noyau.Classe.QNaN),
                     (0xFFFF, 0xC000000000000000, _x87_noyau.Classe.QNaN),
                     (0x7FFF, 0xA000000000000000, _x87_noyau.Classe.SNaN),
                 })
        {
            var v = _x87_noyau.classe(new extFloat80_t { signExp = se, signif = sig });
            bad += Voir($"la classe de {se:X4} {sig:X16}", v == c, $"{v}", $"{c}", true);
        }
        var fpu = cpu_c.fpu_type;
        try
        {
            foreach (var (t, nom) in FpusXL)
            {
                cpu_c.fpu_type = t;
                var ns = _x87_noyau.non_supporte(_x87_noyau.Classe.NonNormal) && _x87_noyau.non_supporte(_x87_noyau.Classe.PseudoNaN)
                         && _x87_noyau.non_supporte(_x87_noyau.Classe.PseudoInfini)
                         && !_x87_noyau.non_supporte(_x87_noyau.Classe.PseudoDenormal);
                bad += Voir($"non normal, pseudo-infini et pseudo-NaN non supportés, pseudo-dénormal supporté ({nom})", ns == t >= 3, $"{ns}",
                            $"{t >= 3}", true);
            }
            // 1 ÷ 3 (387) : au plus près, AAAAAAAAAAAAAAABh, PE et C1 (l'arrondi vers le haut) ; vers −∞ et vers zéro,
            // ...AAAAh, PE seul ; vers +∞, ...AAABh, PE et C1 ; PC à 24 bits, AAAAAB0000000000h, PE et C1 ; à 53 bits,
            // AAAAAAAAAAAAA800h, PE ; PC à 24 bits sans `selon_pc`, les 64 bits. 1 ÷ 1, C1 posé avant : exact, rien, C1
            // à 0. Le plus grand fini ÷ le plus petit normal : OE, PE et C1 (l'infini). 1 ÷ 0 : ZE et l'infini ; 0 ÷ 0 :
            // IE et l'indéfini ; IM à 0, l'instruction s'arrête, ES et B posés.
            cpu_c.fpu_type = cpu_c.FPU_387;
            var trois = new extFloat80_t { signExp = 0x4000, signif = 0xC000000000000000 };
            var zero = new extFloat80_t();
            var max = new extFloat80_t { signExp = 0x7FFE, signif = 0xFFFFFFFFFFFFFFFF };
            var min = new extFloat80_t { signExp = 0x0001, signif = 0x8000000000000000 };
            foreach (var (cw, pc, a, b, sig, sw, stop) in new (ushort, bool, extFloat80_t, extFloat80_t, ulong, ushort, bool)[]
                     {
                         (0x037F, true, Un, trois, 0xAAAAAAAAAAAAAAAB, 0x0220, false),
                         (0x077F, true, Un, trois, 0xAAAAAAAAAAAAAAAA, 0x0020, false),
                         (0x0B7F, true, Un, trois, 0xAAAAAAAAAAAAAAAB, 0x0220, false),
                         (0x0F7F, true, Un, trois, 0xAAAAAAAAAAAAAAAA, 0x0020, false),
                         (0x007F, true, Un, trois, 0xAAAAAB0000000000, 0x0220, false),
                         (0x027F, true, Un, trois, 0xAAAAAAAAAAAAA800, 0x0020, false),
                         (0x007F, false, Un, trois, 0xAAAAAAAAAAAAAAAB, 0x0220, false),
                         (0x037F, true, Un, Un, 0x8000000000000000, 0x0000, false),
                         (0x037F, true, max, min, 0x8000000000000000, 0x0228, false),
                         (0x037F, true, Un, zero, 0x8000000000000000, 0x0004, false),
                         (0x037F, true, zero, zero, 0xC000000000000000, 0x0001, false),
                         (0x037E, true, zero, zero, 0xC000000000000000, 0x8081, true),
                     })
            {
                _386_common.cpu_state.npxc = cw;
                _386_common.cpu_state.npxs = 0x0200;
                _x87_noyau.arrondi(selon_pc: pc);
                var z = softfloat.extF80_div(a, b);
                var arrete = _x87_noyau.indicateurs();
                bad += Voir($"CW {cw:X4}{(pc ? "" : " (sans PC)")}, {a.signExp:X4} {a.signif:X16} ÷ {b.signExp:X4} {b.signif:X16}",
                            z.signif == sig && _386_common.cpu_state.npxs == sw && arrete == stop,
                            $"{z.signif:X16}, {_386_common.cpu_state.npxs:X4}{(arrete ? ", arrêtée" : "")}",
                            $"{sig:X16}, {sw:X4}{(stop ? ", arrêtée" : "")}", true);
            }
        }
        finally
        {
            cpu_c.fpu_type = fpu;
        }
        return bad;
    }

    // PB-58 — FLDZ ; FLDZ ; FCHS (ST(0) = −0, ST(1) = +0) ; FCOMPP ; FNSTSW AX → égaux, C3 (IEEE 754-1985 § 5.7) ; PCem,
    // par son contournement de détection du 387, C0. L'hypothèse A6 instruite : les POST du PC (8087), de l'AT (287), de
    // l'ami386dx (387) et de l'ami486 détectent leur coprocesseur sans le contournement (VERIFICATION.md § G13.6a).
    private static int Pb58(bool hw)
    {
        var bad = 0;
        foreach (var (fpu, nom) in Fpus)
        {
            var ax = X87(fpu, [0xDB, 0xE3, 0xD9, 0xEE, 0xD9, 0xEE, 0xD9, 0xE0, 0xDE, 0xD9, 0xDF, 0xE0], 6);
            var attendu = hw ? "C3 C2 C0 = 100" : "C3 C2 C0 = 001";
            bad += Voir($"FCOMPP, −0 contre +0 ({nom})", Cc(ax) == attendu, Cc(ax), attendu, hw);
        }
        return bad;
    }

    // ===== Le noyau, N3b : l'environnement, les pointeurs, les étiquettes, les limites =====
    //
    // Le code en 0000:0100, BX = 0200h, l'opérande en DS:0300h ; FNSTENV [BX] (D9 37), FLDENV [BX] (D9 27), FNSAVE [BX]
    // (DD 37), FRSTOR [BX] (DD 27) ; la mémoire est remplie de 90h : un champ que l'image n'écrit pas reste 9090h.

    private static readonly byte[] Un64 = Le(0x3FF0000000000000);

    private static string Mots4(int a) => $"{W(a):X4} {W(a + 2):X4} {W(a + 4):X4} {W(a + 6):X4}";

    // PB-62 — les pointeurs (387 PRM § 2.1.5, fig. 2-8 ; 287 PRM, fig. 1-7) : FNINIT ; FLD m64 [0300h] (en 0102h) ;
    // FNSTENV [BX] → la disposition réelle de 16 bits, +6 l'adresse 00102h, +8 l'opcode DD 06 (506h), +0Ah 0300h, +0Ch
    // 0000h ; PCem 0000h, 9090h (non écrit), 0000h, 9090h. Sous un préfixe DS: (3E), l'adresse est celle du préfixe,
    // 00102h, sur le 287, le 387 et le 486, celle de l'ESC, 00103h, sur le 8087. En mode matériel : les instructions de
    // contrôle laissent les pointeurs, FNOP les pose et garde celui de l'opérande ; la disposition réelle de 32 bits
    // (66, sur le 386 et le 486), ses champs réservés non écrits ; FSETPM, sur le 287 et le 287XL, la disposition
    // protégée, ignoré du 387 et du 486 ; FLDENV relit les pointeurs de l'image, en 16 et en 32 bits. Le mode protégé
    // (les dispositions protégées, FCS et FDS) : pm-check, en C# seul.
    private static int Pb62(bool hw)
    {
        var bad = 0;
        foreach (var (fpu, nom) in FpusXL)
        foreach (var (prefixe, quoi) in new (byte[], string)[] { ([], ""), ([0x3E], ", sous DS:") })
        {
            Jouer87(fpu, [0xDB, 0xE3, .. prefixe, 0xDD, 0x06, 0x00, 0x03, 0xD9, 0x37], 3, Op(Un64));
            var ip = fpu == 1 ? 0x102 + prefixe.Length : 0x102;
            var att = hw ? $"{ip:X4} 0506 0300 0000" : "0000 9090 0000 9090";
            bad += Voir($"FLD m64{quoi} ; FNSTENV, +6 à +0Ch ({nom})", Mots4(0x206) == att, Mots4(0x206), att, hw);
        }
        // JMP FAR 2000:0010 ; MOV AX,3000h ; MOV DS,AX ; FNINIT ; FLD m64 [BX+8] (en 2000:0017, un déplacement d'un octet,
        // l'opérande en 3000:0208) ; FNSTENV [BX+20h] : l'adresse 20017h (+6 0017h, +8 2547h : IP 19-16 et l'opcode DD
        // 47), l'opérande 30208h (+0Ah 0208h, +0Ch 3000h).
        foreach (var (fpu, nom) in FpusXL)
        {
            Jouer87(fpu, [0xEA, 0x10, 0x00, 0x00, 0x20], 6, () =>
            {
                byte[] suite = [0xB8, 0x00, 0x30, 0x8E, 0xD8, 0xDB, 0xE3, 0xDD, 0x47, 0x08, 0xD9, 0x77, 0x20];
                for (var k = 0; k < suite.Length; k++)
                    mem.ram[0x20010 + k] = suite[k];
                for (var k = 0; k < 8; k++)
                    mem.ram[0x30208 + k] = Un64[k];
            });
            var att = hw ? "0017 2547 0208 3000" : "0000 9090 0000 9090";
            bad += Voir($"CS 2000h, DS 3000h : FLD m64 [BX+8] ; FNSTENV, +6 à +0Ch ({nom})", Mots4(0x30226) == att,
                        Mots4(0x30226), att, hw);
        }
        if (!hw)
            return bad + PmCheck386.CasMateriel(62, hw) + PmCheck386.CasMateriel(62, hw, core486: true);
        foreach (var (fpu, nom) in FpusXL)
        {
            // FLD m64 (0102h) ; FNOP (0106h) ; FNSTCW [BX+62h] ; FNCLEX ; FNSTENV : FNOP pose l'adresse et l'opcode (D9 D0),
            // garde l'opérande ; FNSTCW et FNCLEX ne changent rien.
            Jouer87(fpu, [0xDB, 0xE3, 0xDD, 0x06, 0x00, 0x03, 0xD9, 0xD0, 0xD9, 0x7F, 0x62, 0xDB, 0xE2, 0xD9, 0x37], 6, Op(Un64));
            bad += Voir($"FLD m64 ; FNOP ; FNSTCW ; FNCLEX ; FNSTENV ({nom})", Mots4(0x206) == "0106 01D0 0300 0000",
                        Mots4(0x206), "0106 01D0 0300 0000", hw);
            // FLDENV [0300h] d'une image réelle de 16 bits (IP 51234h, opcode 506h, opérande 64321h), puis FNSTENV [BX] et
            // FNSTCW [BX+20h] : les pointeurs relus, rangés tels quels ; le mot de contrôle de l'image, 0360h (les six
            // exceptions démasquées), rangé, puis masqué (PB-207 : 037Fh).
            Jouer87(fpu, [0xDB, 0xE3, 0xD9, 0x26, 0x00, 0x03, 0xD9, 0x37, 0xD9, 0x7F, 0x20], 4,
                    Op(0x60, 0x03, 0x00, 0x00, 0xFF, 0xFF, 0x34, 0x12, 0x06, 0x55, 0x21, 0x43, 0x00, 0x60));
            var vu = $"{W(0x200):X4} {W(0x202):X4} {W(0x204):X4} " + Mots4(0x206) + $", {W(0x220):X4}";
            const string aVu = "0360 0000 FFFF 1234 5506 4321 6000, 037F";
            bad += Voir($"FLDENV d'une image de 16 bits ; FNSTENV ; FNSTCW ({nom})", vu == aVu, vu, aVu, hw);
            if (fpu >= 4)
            {
                // 66 FNSTENV [BX] : la disposition réelle de 32 bits (fig. 2-6) ; +2, +0Eh, +16h, réservés, non écrits.
                Jouer87(fpu, [0xDB, 0xE3, 0xDD, 0x06, 0x00, 0x03, 0x66, 0xD9, 0x37], 3, Op(Un64));
                var v32 = $"{W(0x200):X4} {W(0x202):X4} " + Mots4(0x20C) + " " + Mots4(0x214);
                const string a32 = "037F 9090 0102 9090 0506 0000 0300 9090 0000 0000";
                bad += Voir($"FLD m64 ; 66 FNSTENV, la disposition réelle de 32 bits ({nom})", v32 == a32, v32, a32, hw);
                // 66 FLDENV, une image de 32 bits (IP ABCD1234h, opcode 506h, opérande 56784321h), puis 66 FNSTENV.
                Jouer87(fpu, [0xDB, 0xE3, 0x66, 0xD9, 0x26, 0x00, 0x03, 0x66, 0xD9, 0x37], 3,
                        Op(0x7F, 0x02, 0, 0, 0, 0, 0, 0, 0xFF, 0xFF, 0, 0, 0x34, 0x12, 0, 0, 0x06, 0xD5, 0xBC, 0x0A, 0x21, 0x43, 0, 0,
                           0x00, 0x80, 0x67, 0x05));
                var l32 = Mots4(0x20C) + " " + Mots4(0x214);
                bad += Voir($"66 FLDENV ; 66 FNSTENV ({nom})", l32 == "1234 9090 D506 0ABC 4321 9090 8000 0567", l32,
                            "1234 9090 D506 0ABC 4321 9090 8000 0567", hw);
            }
            if (fpu >= 2)
            {
                // FSETPM ; FLD m64 (0104h) ; FNSTENV : le 287 et le 287XL, la disposition protégée de 16 bits (IP 0104h,
                // CS 0000h, opérande 0300h, DS 0000h) ; le 387 et le 486 n'en font rien : la disposition réelle (+8, 0506h).
                Jouer87(fpu, [0xDB, 0xE3, 0xDB, 0xE4, 0xDD, 0x06, 0x00, 0x03, 0xD9, 0x37], 4, Op(Un64));
                var a = fpu <= 3 ? "0104 0000 0300 0000" : "0104 0506 0300 0000";
                bad += Voir($"FSETPM ; FLD m64 ; FNSTENV ({nom})", Mots4(0x206) == a, Mots4(0x206), a, hw);
            }
        }
        return bad + PmCheck386.CasMateriel(62, hw) + PmCheck386.CasMateriel(62, hw, core486: true);
    }

    // PB-199 — FSAVE range chaque registre en réel de 80 bits (SDM vol. 1, 2003, § 8.1.9) : FNINIT ; FILD m64 = 5 ;
    // FNSAVE [BX] → ST(0) en +0Eh, 4001h A000000000000000h, et les étiquettes 3FFFh ; PCem l'entier 5 suivi de 5555h, et
    // BFFFh. En mode matériel : après FNSAVE, l'état de FNINIT (le mot d'état 0000h, le mot de contrôle 037Fh, 03FFh sur
    // le 8087) ; deux FLDZ écrasent les deux registres (FNINIT les garde), et FRSTOR rend la pile : FISTP m64 ×2 → 1,
    // puis 5.
    private static int Pb199(bool hw)
    {
        var bad = 0;
        foreach (var (fpu, nom) in FpusXL)
        {
            Jouer87(fpu, [0xDB, 0xE3, 0xDF, 0x2E, 0x00, 0x03, 0xDD, 0x37], 3, Op(Le(5)));
            var vu = $"{W(0x216):X4} {Q(0x20E):X16}, {W(0x204):X4}";
            var att = hw ? "4001 A000000000000000, 3FFF" : "5555 0000000000000005, BFFF";
            bad += Voir($"FILD m64 5 ; FNSAVE, ST(0) et les étiquettes ({nom})", vu == att, vu, att, hw);
            if (!hw)
                continue;
            Jouer87(fpu, [0xDB, 0xE3, 0xDF, 0x2E, 0x00, 0x03, 0xD9, 0xE8, 0xDD, 0x37, 0xDD, 0x7F, 0x60, 0xD9, 0x7F, 0x62,
                          0xD9, 0xEE, 0xD9, 0xEE, 0xDD, 0x27, 0xDF, 0x3E, 0x00, 0x03, 0xDF, 0x3E, 0x08, 0x03], 11, Op(Le(5)));
            var cw = fpu == 1 ? 0x03FF : 0x037F;
            var etat = $"{W(0x260):X4} {W(0x262):X4}, {Q(0x300)} {Q(0x308)}";
            var aEtat = $"0000 {cw:X4}, 1 5";
            bad += Voir($"FNSAVE, puis FNSTSW et FNSTCW ; FLDZ ×2 ; FRSTOR ; FISTP m64 ×2 ({nom})", etat == aEtat, etat, aEtat, hw);
            if (fpu < 4)
                continue;
            // 66 FNSAVE et 66 FRSTOR : l'image de 32 bits, ST(0) = 1 en +1Ch (387 PRM, fig. 4-1).
            Jouer87(fpu, [0xDB, 0xE3, 0xDF, 0x2E, 0x00, 0x03, 0xD9, 0xE8, 0x66, 0xDD, 0x37, 0xD9, 0xEE, 0xD9, 0xEE,
                          0x66, 0xDD, 0x27, 0xDF, 0x3E, 0x00, 0x03, 0xDF, 0x3E, 0x08, 0x03], 9, Op(Le(5)));
            var e32 = $"{W(0x224):X4} {Q(0x21C):X16}, {Q(0x300)} {Q(0x308)}";
            const string a32 = "3FFF 8000000000000000, 1 5";
            bad += Voir($"66 FNSAVE ; FLDZ ×2 ; 66 FRSTOR ; FISTP m64 ×2 ({nom})", e32 == a32, e32, a32, hw);
        }
        return bad;
    }

    // PB-208 — les étiquettes (SDM vol. 1, 2003, § 8.1.6) : FNINIT ; FLD m64 = +∞ ; FNSTENV → BFFFh (10, spécial) ; PCem
    // 3FFFh. FNINIT ; FILD m64 = 5 ; FSTP ST(0) ; FLD1 ; FNSTENV ; l'étiquette de ST(0) forcée à 10 ; FLDENV ; FISTP m64
    // → 1 ; PCem relit 10 comme l'entier de FILD, 5. En mode matériel : une image qui dit 00 pour +∞, relue par FLDENV puis
    // rangée : 10, recalculée, sur le 287XL, le 387 et le 486 ; 00, gardée, sur le 8087 et le 287 (déduit).
    private static int Pb208(bool hw)
    {
        var bad = 0;
        var inf = Le(0x7FF0000000000000);
        foreach (var (fpu, nom) in FpusXL)
        {
            Jouer87(fpu, [0xDB, 0xE3, 0xDD, 0x06, 0x00, 0x03, 0xD9, 0x37], 3, Op(inf));
            var a = hw ? 0xBFFF : 0x3FFF;
            bad += Voir($"FLD m64 +∞ ; FNSTENV, les étiquettes ({nom})", W(0x204) == a, $"{W(0x204):X4}", $"{a:X4}", hw);
            Jouer87(fpu, [0xDB, 0xE3, 0xDF, 0x2E, 0x00, 0x03, 0xDD, 0xD8, 0xD9, 0xE8, 0xD9, 0x37, 0xC6, 0x47, 0x05, 0xBF,
                          0xD9, 0x27, 0xDF, 0x3E, 0x00, 0x03], 8, Op(Le(5)));
            var n = hw ? 1UL : 5UL;
            bad += Voir($"FILD 5 ; FSTP ST(0) ; FLD1 ; FNSTENV ; étiquette 10 ; FLDENV ; FISTP m64 ({nom})", Q(0x300) == n,
                        $"{Q(0x300)}", $"{n}", hw);
            if (!hw)
                continue;
            Jouer87(fpu, [0xDB, 0xE3, 0xDD, 0x06, 0x00, 0x03, 0xD9, 0x37, 0xC6, 0x47, 0x05, 0x3F, 0xD9, 0x27, 0xD9, 0x77, 0x20], 6,
                    Op(inf));
            var r = fpu >= 3 ? 0xBFFF : 0x3FFF;
            bad += Voir($"+∞ étiqueté 00 dans l'image ; FLDENV ; FNSTENV ({nom})", W(0x224) == r, $"{W(0x224):X4}", $"{r:X4}", hw);
        }
        return bad;
    }

    // PB-54 — la limite et les droits (386 PRM § 14.7, point 7 ; SDM vol. 2, FST) : en mode réel, un opérande à cheval sur
    // l'offset FFFFh lève l'exception 13 (le vecteur en 0000:0700, FLAGS, CS et IP empilés : SP = FFF8h), sans rien
    // écrire au-delà (DS:10000h ; DS:FFF8h à FFFDh, c'est la pile de la faute) ; PCem n'en contrôle aucun, sauf FSTP m64.
    // Le 386 et le 486 contrôlent l'opérande entier ; le 286, le premier mot (287 PRM, table 2-4), les suivants menant à
    // l'INT 9, non modélisée : ils y passent, comme dans PCem. Le 8087 n'a pas de limite. Témoin : FNSTENV [FFF2h],
    // quatorze octets jusqu'à FFFFh, sans faute. Un opérande au-delà de FFFFh (67, le 386 et le 486). Les
    // droits du mode protégé : pm-check, en C# seul.
    private static int Pb54(bool hw)
    {
        var bad = 0;
        foreach (var (fpu, nom) in FpusXL.Where(f => f.fpu >= 2))
        {
            var a386 = fpu >= 4;
            var cas = new List<(string, byte[], int, int, bool)>
            {
                ("FLD m32 [FFFFh], le premier mot à cheval", [0xDB, 0xE3, 0xD9, 0x06, 0xFF, 0xFF], 2, 0x106, true),
                ("FLD1 ; FST m64 [FFFCh]", [0xDB, 0xE3, 0xD9, 0xE8, 0xDD, 0x16, 0xFC, 0xFF], 3, 0x108, a386),
                ("FLD m32 [FFFEh]", [0xDB, 0xE3, 0xD9, 0x06, 0xFE, 0xFF], 2, 0x106, a386),
                ("FNSAVE [FFC0h]", [0xDB, 0xE3, 0xDD, 0x36, 0xC0, 0xFF], 2, 0x106, a386),
                ("FNSTENV [FFF4h]", [0xDB, 0xE3, 0xD9, 0x36, 0xF4, 0xFF], 2, 0x106, a386),
                ("FNSTENV [FFF2h], témoin", [0xDB, 0xE3, 0xD9, 0x36, 0xF2, 0xFF], 2, 0x106, false),
            };
            if (fpu >= 4)
                cas.Add(("FLD1 ; 67 FST m32 [00010000h]", [0xDB, 0xE3, 0xD9, 0xE8, 0x67, 0xD9, 0x15, 0x00, 0x00, 0x01, 0x00], 3, 0x10B, true));
            foreach (var (quoi, code, pas, ipPcem, faute) in cas)
            {
                var r = Jouer87(fpu, code, pas);
                var gp = hw && faute;
                var ok = gp ? r[(int)R.IP] == 0x0700 && r[(int)R.SP] == 0xFFF8 && W(0x10000) == 0x9090
                            : r[(int)R.IP] == ipPcem;
                bad += Voir($"{quoi} ({nom})", ok, $"IP = {r[(int)R.IP]:X4}, SP = {r[(int)R.SP]:X4}, DS:10000 = {W(0x10000):X4}",
                            gp ? "IP = 0700 (#GP), SP = FFF8, DS:10000 = 9090 (rien d'écrit)" : $"IP = {ipPcem:X4}", hw);
            }
        }
        return bad + PmCheck386.CasMateriel(54, hw) + PmCheck386.CasMateriel(54, hw, core486: true);
    }

    // ===== Le 8259 =====

    /// <summary>Le 8259 comme le BIOS le laisse : sur l'AT, ICW1 11h, ICW2 08h, ICW3 04h, ICW4 01h au maître, et 11h,
    /// 70h, 02h, 01h à l'esclave (AT TR 1502494, POST) ; sur l'XT, ICW1 13h, ICW2 08h, ICW4 01h. Masques à zéro.</summary>
    private static void Pic(bool at)
    {
        x86.AT = at ? 1 : 0;
        pic.pic_reset();
        foreach (var (port, v) in at
                     ? new (ushort, byte)[] { (0x20, 0x11), (0x21, 0x08), (0x21, 0x04), (0x21, 0x01), (0x21, 0x00) }
                     : [(0x20, 0x13), (0x21, 0x08), (0x21, 0x01), (0x21, 0x00)])
            pic.pic_write(port, v, null!);
        if (at)
            foreach (var (port, v) in new (ushort, byte)[] { (0xA0, 0x11), (0xA1, 0x70), (0xA1, 0x02), (0xA1, 0x01), (0xA1, 0x00) })
                pic.pic2_write(port, v, null!);
    }

    private static int Avec8259(Func<int> cas)
    {
        var at = x86.AT;
        try
        {
            return cas();
        }
        finally
        {
            x86.AT = at;
            pic.pic_reset();
        }
    }

    // PB-05 — servir une IRQ de l'esclave efface, dans l'IRR du maître, le bit de même indice. Fiche 8259A p. 7,
    // « Interrupt Sequence » : à l'INTA, seul le bit de l'IRR servi s'efface. AT, IRQ 0 masquée mais en attente (l'IMR ne
    // touche pas l'IRR), IRQ 8 en attente : l'acquittement sert 70h ; l'IRR du maître relu (OCW3 0Ah) : PCem 00h,
    // l'IRQ 0 perdue ; le 8259A 01h.
    private static int Pb05(bool hw) => Avec8259(() =>
    {
        Pic(at: true);
        pic.pic_write(0x21, 0x01, null!);
        pic.picint(1 << 0);
        pic.picint(1 << 8);
        var v = pic.picinterrupt();
        pic.pic_write(0x20, 0x0A, null!);
        var irr = pic.pic_read(0x20, null!);
        return Voir("AT, IRQ 0 masquée et IRQ 8 en attente, un acquittement, l'IRR du maître", v == 0x70 && irr == (hw ? 1 : 0),
                    $"vecteur {v:X2}h, IRR {irr:X2}h", $"vecteur 70h, IRR {(hw ? 1 : 0):X2}h", hw);
    });

    // PB-246 — la cascade passe avant l'IRQ 0 et l'IRQ 1. Fiche 8259A p. 15, « Fully Nested Mode » : IR0 la plus haute ;
    // AT TR p. 1-10 : IRQ 0, IRQ 1, puis IRQ 8 à 15 par IR2. AT, IRQ 0 et IRQ 9 en attente : deux acquittements, une fin
    // non spécifique entre eux. PCem 71h puis 08h ; le 8259A 08h puis 71h.
    private static int Pb246(bool hw) => Avec8259(() =>
    {
        Pic(at: true);
        pic.picint(1 << 0);
        pic.picint(1 << 9);
        var v1 = pic.picinterrupt();
        pic.pic_write(0x20, 0x20, null!);
        var v2 = pic.picinterrupt();
        var (a1, a2) = hw ? (0x08, 0x71) : (0x71, 0x08);
        return Voir("AT, IRQ 0 et IRQ 9 en attente, deux acquittements", v1 == a1 && v2 == a2, $"{v1:X2}h puis {v2:X2}h",
                    $"{a1:X2}h puis {a2:X2}h", hw);
    });

    // PB-247 — le masque de service ignoré. Fiche 8259A p. 15 : « While the IS bit is set, all further interrupts of the
    // same or lower priority are inhibited ». XT : l'IRQ 0 en service (son gestionnaire a fait STI, sans EOI), l'IRQ 1 en
    // attente. Le 8259 : PCem acquitte 09h ; le 8259A rien (FFh) avant l'EOI, 09h après. Le 8088, IF = 1, le vecteur 09h
    // en 0000:0500 : PCem y entre dans les trois pas, le 8088 reste dans son code. Et une chaîne REP : PCem l'arrête pour
    // l'IRQ (IRQTEST), le 8088 la finit.
    private static int Pb247(bool hw) => Avec8259(() =>
    {
        var bad = 0;
        Pic(at: false);
        pic.picint(1 << 0);
        pic.picinterrupt();
        pic.picint(1 << 1);
        var v = pic.picinterrupt();
        bad += Voir("XT, IRQ 0 en service, IRQ 1 en attente, un acquittement", v == (hw ? 0xFF : 0x09), $"{v:X2}h",
                    hw ? "FFh, rien" : "09h", hw);
        pic.pic_write(0x20, 0x20, null!);
        v = pic.picinterrupt();
        bad += Voir("puis une fin non spécifique, un acquittement", v == (hw ? 0x09 : 0xFF), $"{v:X2}h",
                    hw ? "09h" : "FFh, l'IRQ 1 déjà servie", hw);
        var r = Jouer([0x90, 0x90, 0x90], r => r[(int)R.FLAGS] = 0xF202, () =>
        {
            Pic(at: false);
            pic.picint(1 << 0);
            pic.picinterrupt();
            pic.picint(1 << 1);
            mem.ram[0x24] = 0x00;
            mem.ram[0x25] = 0x05;
            mem.ram[0x26] = 0x00;
            mem.ram[0x27] = 0x00;
        }, pas: 3);
        var entre = r[(int)R.IP] >= 0x500 && r[(int)R.IP] < 0x510;
        bad += Voir("le 8088, IF = 1, trois NOP", entre == !hw, $"CS:IP = {r[(int)R.CS]:X4}:{r[(int)R.IP]:X4}",
                    hw ? "dans son code (0000:0103)" : "dans INT 09h (0000:05xx)", hw);
        // REP STOSB, CX = 4, la même IRQ 1 retenue : le 8088 fait les quatre répétitions d'un pas ; PCem arrête la chaîne
        // avant la première (IRQTEST), pour l'interruption qu'il prendra ensuite.
        r = Jouer([0xF3, 0xAA], r => { r[(int)R.FLAGS] = 0xF202; r[(int)R.CX] = 4; r[(int)R.DI] = 0x600; r[(int)R.AX] = 0x55; },
                  () =>
                  {
                      Pic(at: false);
                      pic.picint(1 << 0);
                      pic.picinterrupt();
                      pic.picint(1 << 1);
                  });
        bad += Voir("le 8088, IF = 1, REP STOSB, CX = 4, un pas", r[(int)R.CX] == (hw ? 0 : 4),
                    $"CX = {r[(int)R.CX]}", hw ? "CX = 0" : "CX = 4, la chaîne arrêtée avant sa première répétition", hw);
        return bad;
    });

    // PB-248 — l'OCW2 et l'OCW3 réduits à l'EOI et à RR/RIS. Fiche 8259A p. 13-17, figure 8. XT : (1) IR0 en service,
    // OCW2 40h (« no operation ») puis l'ISR (OCW3 0Bh) : PCem 00h, l'ISR effacé ; le 8259A 01h. (2) Poll, IRQ 3 en
    // attente : OCW3 0Ch puis IN 20h ; le 8259A 83h, et IR3 passe en service ; PCem rend l'ISR, 00h. (3) La priorité
    // posée, C1h (IR1 au plus bas, IR2 au plus haut), IRQ 0 et IRQ 2 en attente : le 8259A sert 0Ah, PCem 08h. (4) AT, le
    // masque spécial (OCW3 68h), IR0 en service, IRQ 1 en attente : la demande vue par le processeur (pic_intpending),
    // PCem aucune, le 8259A l'IRQ 1.
    private static int Pb248(bool hw) => Avec8259(() =>
    {
        var bad = 0;
        Pic(at: false);
        pic.picint(1 << 0);
        pic.picinterrupt();
        pic.pic_write(0x20, 0x40, null!);
        pic.pic_write(0x20, 0x0B, null!);
        var isr = pic.pic_read(0x20, null!);
        bad += Voir("XT, IR0 en service, OCW2 40h, l'ISR", isr == (hw ? 1 : 0), $"{isr:X2}h", $"{(hw ? 1 : 0):X2}h", hw);

        Pic(at: false);
        pic.picint(1 << 3);
        pic.pic_write(0x20, 0x0C, null!);
        var mot = pic.pic_read(0x20, null!);
        pic.pic_write(0x20, 0x0B, null!);
        isr = pic.pic_read(0x20, null!);
        bad += Voir("XT, IRQ 3 en attente, poll (OCW3 0Ch), puis l'ISR", mot == (hw ? 0x83 : 0x00) && isr == (hw ? 0x08 : 0x00),
                    $"{mot:X2}h, ISR {isr:X2}h", hw ? "83h, ISR 08h" : "00h, ISR 00h", hw);

        Pic(at: false);
        pic.pic_write(0x20, 0xC1, null!);
        pic.picint(1 << 0);
        pic.picint(1 << 2);
        var v = pic.picinterrupt();
        bad += Voir("XT, priorité C1h, IRQ 0 et IRQ 2 en attente, un acquittement", v == (hw ? 0x0A : 0x08), $"{v:X2}h",
                    hw ? "0Ah" : "08h", hw);

        Pic(at: true);
        pic.picint(1 << 0);
        pic.picinterrupt();
        pic.pic_write(0x20, 0x68, null!);
        pic.picint(1 << 1);
        var dem = pic.pic_intpending;
        bad += Voir("AT, IR0 en service, masque spécial (OCW3 68h), IRQ 1 en attente : la demande", dem == (hw ? 2 : 0),
                    $"{dem:X2}h", $"{(hw ? 2 : 0):X2}h", hw);
        return bad;
    });

    // PB-255 — la lecture après ICW1. Fiche 8259A p. 10, point e : « Status Read is set to IRR ». XT : OCW3 0Bh (l'ISR),
    // puis ICW1 à ICW4, OCW1 FFh, un tic du PIT en attente ; IN 20h : PCem 00h (l'ISR), le 8259A 01h (l'IRR, que l'IMR
    // n'affecte pas). Et à la mise sous tension, sans OCW3 : PCem lit l'ISR du maître (pic_reset pose read = 1) et garde
    // la lecture d'avant à l'esclave ; le 8259A lit l'IRR des deux (au maître, 01h : l'esclave, masqué, ne demande pas
    // par la cascade).
    private static int Pb255(bool hw) => Avec8259(() =>
    {
        var bad = 0;
        Pic(at: false);
        pic.pic_write(0x20, 0x0B, null!);
        foreach (var (port, v) in new (ushort, byte)[] { (0x20, 0x13), (0x21, 0x08), (0x21, 0x01), (0x21, 0xFF) })
            pic.pic_write(port, v, null!);
        pic.picint(1 << 0);
        var l = pic.pic_read(0x20, null!);
        bad += Voir("XT, OCW3 0Bh, ICW1 à ICW4, OCW1 FFh, IRQ 0 en attente, IN 20h", l == (hw ? 1 : 0), $"{l:X2}h",
                    $"{(hw ? 1 : 0):X2}h", hw);
        x86.AT = 1;
        pic.pic2_write(0xA0, 0x0B, null!);                // l'esclave lu sur l'ISR, avant la mise sous tension
        pic.pic_reset();
        pic.picint(1 << 0);
        pic.picint(1 << 8);
        var m = pic.pic_read(0x20, null!);
        var e = pic.pic2_read(0xA0, null!);
        bad += Voir("AT, mise sous tension, IRQ 0 et IRQ 8 en attente, IN 20h et IN A0h", (m, e) == (hw ? (1, 1) : (0, 0)),
                    $"{m:X2}h et {e:X2}h", hw ? "01h et 01h" : "00h et 00h", hw);
        return bad;
    });

    // ===== Le 8237 =====

    private static void Dma(ushort port, byte v)
    {
        if (port >= 0xC0)
            dma.dma16_write(port, v, null!);
        else
            dma.dma_write(port, v, null!);
    }

    private static byte DmaLu(ushort port) => port >= 0xC0 ? dma.dma16_read(port, null!) : dma.dma_read(port, null!);

    /// <summary>Le compte courant d'un canal du 8237 bas (port 01h, 03h, 05h, 07h), la bascule remise à zéro d'abord.</summary>
    private static int Compte(int canal)
    {
        Dma(0x0C, 0);
        var lo = DmaLu((ushort)(canal * 2 + 1));
        var hi = DmaLu((ushort)(canal * 2 + 1));
        return lo | hi << 8;
    }

    private static string Fait(int r) => r == dma.DMA_NODATA ? "refusé (DMA_NODATA)" : "fait";

    private static int Avec8237(bool at, Func<int> cas)
    {
        var avant = x86.AT;
        try
        {
            _808x.Reset();
            x86.AT = at ? 1 : 0;
            dma.dma_reset();
            return cas();
        }
        finally
        {
            x86.AT = avant;
            dma.dma_reset();
        }
    }

    // PB-157 — la commande du 8237 haut, son bit 2 le désactive (fiche 8237A p. 7) ; DAh se lit sur le temporaire,
    // nul hors du transfert de mémoire à mémoire (p. 9). AT, le canal 5 en écriture, démasqué, OUT D0h,04h : PCem fait
    // le transfert, le 8237A le refuse. OUT DAh,5Ah (le master clear) puis IN DAh : PCem 5Ah, le 8237A 00h.
    private static int Pb157(bool hw) => Avec8237(at: true, () =>
    {
        var bad = 0;
        Dma(0xD6, 0x45);                                  // canal 5 : simple, écriture en mémoire
        Dma(0xD4, 0x01);                                  // démasque le canal 5
        Dma(0xD0, 0x04);                                  // le 8237 haut désactivé
        var r = dma.dma_channel_write(5, 0x1234);
        bad += Voir("AT, OUT D0h,04h, dma_channel_write du canal 5", (r == dma.DMA_NODATA) == hw, Fait(r),
                    hw ? "refusé" : "fait", hw);
        Dma(0xDA, 0x5A);
        var t = DmaLu(0xDA);
        bad += Voir("OUT DAh,5Ah, IN DAh", t == (hw ? 0x00 : 0x5A), $"{t:X2}h", hw ? "00h" : "5Ah", hw);
        return bad;
    });

    // PB-249 — Clear Mask efface les quatre masques d'un contrôleur (fiche p. 9 ; AT TR p. 1-14). PC, OUT 0Fh,0Fh puis
    // OUT 0Eh,00h, le canal 2 en écriture : PCem le laisse masqué (DMA_NODATA), le 8237A transfère. De même au 8237
    // haut, OUT DEh,0Fh puis OUT DCh,00h, le canal 5.
    private static int Pb249(bool hw) => Avec8237(at: false, () =>
    {
        var bad = 0;
        Dma(0x0B, 0x46);                                  // canal 2 : simple, écriture en mémoire
        Dma(0x0F, 0x0F);
        Dma(0x0E, 0x00);
        var r = dma.dma_channel_write(2, 0x55);
        bad += Voir("OUT 0Fh,0Fh, OUT 0Eh,00h, dma_channel_write du canal 2", (r != dma.DMA_NODATA) == hw, Fait(r),
                    hw ? "fait" : "refusé", hw);
        Dma(0xD6, 0x45);                                  // canal 5 : simple, écriture en mémoire
        Dma(0xDE, 0x0F);
        Dma(0xDC, 0x00);
        r = dma.dma_channel_write(5, 0x1234);
        bad += Voir("OUT DEh,0Fh, OUT DCh,00h, dma_channel_write du canal 5", (r != dma.DMA_NODATA) == hw, Fait(r),
                    hw ? "fait" : "refusé", hw);
        return bad;
    });

    // PB-250 — la requête logicielle (fiche p. 7) : non masquable, en mode bloc, jusqu'au TC. PC, le canal 1 masqué, en
    // bloc et en vérification (81h), compte 0003h ; OUT 09h,05h : le 8237A fait quatre transferts, l'état rend 02h
    // (TC du canal 1), le compte FFFFh ; PCem rien (00h, 0003h). De même au 8237 haut, le canal 5, compte 0002h,
    // OUT D2h,05h, l'état D0h : 02h.
    private static int Pb250(bool hw) => Avec8237(at: false, () =>
    {
        var bad = 0;
        Dma(0x0A, 0x05);                                  // masque le canal 1
        Dma(0x0B, 0x81);                                  // canal 1 : bloc, vérification
        Dma(0x0C, 0x00);
        Dma(0x03, 0x03);
        Dma(0x03, 0x00);
        DmaLu(0x08);
        Dma(0x09, 0x05);
        var st = DmaLu(0x08);
        var n = Compte(1);
        bad += Voir("canal 1 en bloc, compte 0003h, OUT 09h,05h ; l'état et le compte",
                    (st, n) == (hw ? (0x02, 0xFFFF) : (0x00, 0x0003)), $"{st:X2}h, {n:X4}h",
                    hw ? "02h, FFFFh" : "00h, 0003h", hw);
        Dma(0xD6, 0x81);                                  // canal 5 : bloc, vérification
        Dma(0xD8, 0x00);
        Dma(0xC6, 0x02);
        Dma(0xC6, 0x00);
        DmaLu(0xD0);
        Dma(0xD2, 0x05);
        st = DmaLu(0xD0);
        bad += Voir("canal 5 en bloc, compte 0002h, OUT D2h,05h ; l'état", st == (hw ? 0x02 : 0x00), $"{st:X2}h",
                    hw ? "02h" : "00h", hw);
        return bad;
    });

    // PB-251 — le master clear a l'effet du reset (fiche p. 9) : la commande, l'état et la requête effacés. PC, le canal
    // 2 au TC (compte 0), puis OUT 08h,04h et OUT 0Dh : l'état relu, PCem 04h, le 8237A 00h ; le canal 2 démasqué,
    // dma_channel_write : PCem refuse (le contrôleur reste désactivé), le 8237A transfère.
    private static int Pb251(bool hw) => Avec8237(at: false, () =>
    {
        var bad = 0;
        Dma(0x0B, 0x46);                                  // canal 2 : simple, écriture en mémoire, compte 0
        Dma(0x0A, 0x02);
        dma.dma_channel_write(2, 0x55);                   // le TC
        Dma(0x08, 0x04);
        Dma(0x0D, 0x00);
        var st = DmaLu(0x08);
        bad += Voir("le canal 2 au TC, OUT 08h,04h, OUT 0Dh ; l'état", st == (hw ? 0x00 : 0x04), $"{st:X2}h",
                    hw ? "00h" : "04h", hw);
        Dma(0x0A, 0x02);
        var r = dma.dma_channel_write(2, 0x55);
        bad += Voir("puis OUT 0Ah,02h, dma_channel_write du canal 2", (r != dma.DMA_NODATA) == hw, Fait(r),
                    hw ? "fait" : "refusé", hw);
        return bad;
    });

    // PB-252 — le reset (fiche p. 2, broche RESET) pose les masques et efface l'état. PC, le canal 2 au TC, puis
    // dma_reset : l'état relu, PCem 04h, le 8237A 00h ; le mode du canal 2 reposé, sans toucher au masque,
    // dma_channel_write : PCem transfère, le 8237A refuse (masqué).
    private static int Pb252(bool hw) => Avec8237(at: false, () =>
    {
        var bad = 0;
        Dma(0x0B, 0x46);
        Dma(0x0A, 0x02);
        dma.dma_channel_write(2, 0x55);
        dma.dma_reset();
        var st = DmaLu(0x08);
        bad += Voir("le canal 2 au TC, dma_reset ; l'état", st == (hw ? 0x00 : 0x04), $"{st:X2}h", hw ? "00h" : "04h", hw);
        Dma(0x0B, 0x46);
        var r = dma.dma_channel_write(2, 0x55);
        bad += Voir("le mode reposé, dma_channel_write du canal 2", (r == dma.DMA_NODATA) == hw, Fait(r),
                    hw ? "refusé" : "fait", hw);
        return bad;
    });

    // PB-253 — les canaux 0 à 3 de l'AT n'ont le bus que par le canal 4 en cascade (AT TR p. 1-13 ; fiche p. 5-6). AT,
    // le canal 4 en cascade (C0h) et démasqué, le canal 2 en écriture auto-initialisée : transféré dans les deux modes ;
    // OUT D4h,04h (le canal 4 masqué) : PCem transfère, le 8237A non ; le canal 4 démasqué mais en mode simple (40h) :
    // de même.
    private static int Pb253(bool hw) => Avec8237(at: true, () =>
    {
        var bad = 0;
        Dma(0xD6, 0xC0);
        Dma(0xD4, 0x00);
        Dma(0x0B, 0x56);                                  // canal 2 : simple, auto-initialisé, écriture en mémoire
        Dma(0x0A, 0x02);
        var r = dma.dma_channel_write(2, 0x55);
        bad += Voir("AT, le canal 4 en cascade, dma_channel_write du canal 2", r != dma.DMA_NODATA, Fait(r), "fait", hw);
        Dma(0xD4, 0x04);
        r = dma.dma_channel_write(2, 0x55);
        bad += Voir("OUT D4h,04h (le canal 4 masqué), dma_channel_write du canal 2", (r == dma.DMA_NODATA) == hw,
                    Fait(r), hw ? "refusé" : "fait", hw);
        Dma(0xD4, 0x00);
        Dma(0xD6, 0x40);
        r = dma.dma_channel_write(2, 0x55);
        bad += Voir("le canal 4 démasqué, en mode simple (40h), dma_channel_write du canal 2",
                    (r == dma.DMA_NODATA) == hw, Fait(r), hw ? "refusé" : "fait", hw);
        return bad;
    });

    // ===== Le 8042 et la souris PS/2 =====

    private static string Hex(List<int> l) => l.Count == 0 ? "rien" : string.Join(" ", l.Select(v => $"{v:X2}"));

    /// <summary>Le 8042 de l'AT neuf (keyboard_at_init), clavier actif (octet de commande 01h, traduction coupée),
    /// sans souris.</summary>
    private static void Clavier8042()
    {
        _808x.Reset();
        keyboard_at.keyboard_at_init();
        keyboard_at.keyboard_at_.mem[0] = 0x01;
        keyboard_at.keyboard_at_.translate = 0;
        mouse_ps2.mouse_scan = 0;
    }

    /// <summary>Le poll du 8042 tant qu'il livre : chaque octet arrivé au tampon de sortie, lu en 60h.</summary>
    private static List<int> Vider8042()
    {
        var lus = new List<int>();
        for (var k = 0; k < 400; k++)
        {
            keyboard_at.keyboard_at_poll();
            if ((keyboard_at.keyboard_at_.status & 1) != 0)
                lus.Add(keyboard_at.keyboard_at_read(0x60, null));
        }
        return lus;
    }

    // PB-254 — les files du 8042. Le clavier : AT TR p. 4-3, seize codes gardés, le dix-septième remplacé par le code de
    // débordement (00h ; FFh traduit), les suivants perdus. Un code au tampon de sortie, non lu, puis dix-neuf autres
    // (02h à 14h) : PCem rend 01h, 12h, 13h, 14h ; l'AT 01h à 11h puis 00h. Traduction posée, le premier puis dix-sept
    // « A » (1Ch) : PCem 01h... et un 1Eh ; l'AT seize 1Eh puis FFh. Le contrôleur, vingt fois 20h (lire l'octet de
    // commande) sans lecture : PCem en rend quatre, le 8042 vingt. La souris, cinq fois E9h (quatre octets chacune) :
    // PCem quatre octets, la souris vingt (le 8042 la retient par la ligne « clock » : PS/2 HITR 84F9735, p. 14-15).
    private static int Pb254(bool hw)
    {
        var bad = 0;
        Clavier8042();
        keyboard_at.keyboard_at_adddata_keyboard(0x01);
        keyboard_at.keyboard_at_poll();
        keyboard_at.keyboard_at_poll();
        for (var c = 2; c <= 20; c++)
            keyboard_at.keyboard_at_adddata_keyboard((byte)c);
        var lus = Vider8042();
        var att = hw ? Enumerable.Range(1, 17).Append(0).ToList() : [1, 18, 19, 20];
        bad += Voir("clavier, 01h au tampon de sortie, puis 02h à 14h sans lecture", lus.SequenceEqual(att), Hex(lus),
                    Hex(att), hw);

        Clavier8042();
        keyboard_at.keyboard_at_.translate = 1;
        keyboard_at.keyboard_at_adddata_keyboard(0x16);  // « 1 », traduit 02h
        keyboard_at.keyboard_at_poll();
        keyboard_at.keyboard_at_poll();
        for (var c = 0; c < 17; c++)
            keyboard_at.keyboard_at_adddata_keyboard(0x1c);
        lus = Vider8042();
        att = hw ? [0x02, .. Enumerable.Repeat(0x1e, 16), 0xff] : [0x02, 0x1e];
        bad += Voir("clavier traduit, 16h au tampon de sortie, puis dix-sept fois 1Ch", lus.SequenceEqual(att), Hex(lus),
                    Hex(att), hw);

        Clavier8042();
        for (var c = 0; c < 20; c++)
            keyboard_at.keyboard_at_write(0x64, 0x20, null);
        lus = Vider8042();
        var n = hw ? 20 : 4;
        bad += Voir("contrôleur, vingt fois 20h sans lecture", lus.Count == n && lus.All(v => v == 0x01),
                    $"{lus.Count} octet(s), {Hex(lus)}", $"{n} fois 01h", hw);

        Clavier8042();
        var m = mouse_ps2.mouse_ps2_init();
        mouse_ps2.mouse_scan = 1;
        for (var c = 0; c < 5; c++)
            mouse_ps2.mouse_ps2_write(0xe9, m);
        lus = Vider8042();
        n = hw ? 20 : 4;
        bad += Voir("souris, cinq fois E9h sans lecture", lus.Count == n, $"{lus.Count} octet(s), {Hex(lus)}",
                    $"{n} octets", hw);
        mouse_ps2.mouse_ps2_close(m);
        mouse_ps2.mouse_scan = 0;
        return bad;
    }

    /// <summary>Une souris PS/2 neuve du type donné (2, deux boutons ; 3, l'Intellimouse), sa file vide.</summary>
    private static mouse_ps2_t Souris(int type)
    {
        mouse.mouse_type = type;
        mouse.mouse_buttons = 0;
        keyboard_at.mouse_queue_start = keyboard_at.mouse_queue_end = 0;
        return (mouse_ps2_t)mouse_ps2.mouse_ps2_init();
    }

    /// <summary>Envoie un octet à la souris et rend ce qu'elle a mis dans la file.</summary>
    private static List<int> Commande(mouse_ps2_t m, int v)
    {
        mouse_ps2.mouse_ps2_write((byte)v, m);
        return FileSouris();
    }

    /// <summary>Ce que la souris a mis dans la file, retiré.</summary>
    private static List<int> FileSouris()
    {
        var l = new List<int>();
        while (keyboard_at.mouse_queue_start != keyboard_at.mouse_queue_end)
        {
            l.Add(keyboard_at.mouse_queue[keyboard_at.mouse_queue_start]);
            keyboard_at.mouse_queue_start = (keyboard_at.mouse_queue_start + 1) & 0xf;
        }
        return l;
    }

    private static int Echange(string nom, mouse_ps2_t m, int v, int[] pcem, int[] materiel, bool hw)
    {
        var l = Commande(m, v);
        var att = (hw ? materiel : pcem).ToList();
        return Voir($"{nom} ({v:X2}h)", l.SequenceEqual(att), Hex(l), Hex(att), hw);
    }

    // PB-94 — FAh à toute commande valide, FEh puis FCh à une invalide ; F6h, F0h, EAh, EEh, ECh (Chapweske ; Brouwer).
    // La souris neuve, E9h : PCem FAh 00h 00h 00h ; la souris FAh 00h 02h 64h, ses valeurs par défaut. E8h 03h, F3h 28h
    // (posées), F6h, E9h : PCem rien puis FAh 00h 03h 28h ; la souris FAh, puis FAh 00h 02h 64h. F4h, F0h, un mouvement
    // (PCem un paquet, en flux ; la souris, en mode distant, rien) ; EAh ; ECh hors de l'écho (FAh) ; EEh, 12h, ECh
    // (l'écho) ; EEh, FFh (le reset sort de l'écho : FAh AAh 00h, puis E9h, FAh 00h 02h 64h) ; EDh, F5h, EDh, EDh (FEh,
    // FAh, FEh, FCh : une commande valide remet le compte à zéro).
    private static int Pb94(bool hw)
    {
        var bad = 0;
        var m = Souris(2);
        mouse_ps2.mouse_scan = 1;
        bad += Echange("souris neuve, état", m, 0xe9, [0xfa, 0, 0, 0], [0xfa, 0, 2, 100], hw);
        Commande(m, 0xe8);
        Commande(m, 0x03);
        Commande(m, 0xf3);
        Commande(m, 0x28);
        bad += Echange("valeurs par défaut", m, 0xf6, [], [0xfa], hw);
        bad += Echange("état", m, 0xe9, [0xfa, 0, 3, 0x28], [0xfa, 0, 2, 100], hw);
        bad += Echange("flux activé", m, 0xf4, [0xfa], [0xfa], hw);
        bad += Echange("mode distant", m, 0xf0, [], [0xfa], hw);
        mouse_ps2.mouse_ps2_poll(5, 5, 0, 0, m);
        var l = FileSouris();
        bad += Voir("un mouvement en mode distant", l.Count == (hw ? 0 : 3), $"{l.Count} octet(s)", hw ? "0" : "3", hw);
        bad += Echange("mode flux", m, 0xea, [], [0xfa], hw);
        bad += Echange("fin de l'écho, hors de l'écho", m, 0xec, [], [0xfa], hw);
        bad += Echange("écho", m, 0xee, [], [0xfa], hw);
        bad += Echange("écho, un octet", m, 0x12, [], [0x12], hw);
        bad += Echange("fin de l'écho", m, 0xec, [], [0xfa], hw);
        bad += Echange("écho, encore", m, 0xee, [], [0xfa], hw);
        bad += Echange("reset dans l'écho", m, 0xff, [0xfa, 0xaa, 0], [0xfa, 0xaa, 0], hw);
        bad += Echange("après le reset, état", m, 0xe9, [0xfa, 0, 3, 0x28], [0xfa, 0, 2, 100], hw);
        bad += Echange("commande inconnue", m, 0xed, [], [0xfe], hw);
        bad += Echange("une commande valide", m, 0xf5, [0xfa], [0xfa], hw);
        bad += Echange("commande inconnue, après une valide", m, 0xed, [], [0xfe], hw);
        bad += Echange("commande inconnue, encore", m, 0xed, [], [0xfc], hw);
        mouse_ps2.mouse_ps2_close(m);
        mouse_ps2.mouse_scan = 0;
        return bad;
    }

    // PB-95 — l'octet d'état d'E9h, IBM 15F0306 p. 2-97 : gauche en bit 2, droit en bit 0, le flux activé en bit 5.
    // Souris à deux boutons, F4h, E9h : gauche tenu, PCem 21h et IBM 24h ; droit, 22h et 21h ; milieu, 23h et 20h.
    // L'Intellimouse, milieu tenu : PCem 23h ; le bit 1, celui du milieu d'une souris à trois boutons, 22h.
    private static int Pb95(bool hw)
    {
        var bad = 0;
        foreach (var (type, boutons, pcem, ibm) in new[] { (2, 1, 0x21, 0x24), (2, 2, 0x22, 0x21), (2, 4, 0x23, 0x20),
                                                           (3, 4, 0x23, 0x22) })
        {
            var m = Souris(type);
            Commande(m, 0xf4);
            mouse.mouse_buttons = boutons;
            int[] reste = [m.resolution, m.sample_rate];
            bad += Echange($"type {type}, boutons {boutons}, état", m, 0xe9, [0xfa, pcem, .. reste], [0xfa, ibm, .. reste], hw);
            mouse.mouse_buttons = 0;
            mouse_ps2.mouse_ps2_close(m);
        }
        mouse.mouse_type = 0;
        return bad;
    }

    // PB-261 — après le paquet d'EBh, la souris remet ses compteurs de mouvement à zéro (Chapweske, « Read Data »). La
    // souris neuve, un mouvement de (5, 3) accumulé, puis EBh deux fois : PCem FAh 00h 05h FDh deux fois ; la souris la
    // seconde fois FAh 00h 00h 00h.
    private static int Pb261(bool hw)
    {
        var bad = 0;
        var m = Souris(2);
        mouse_ps2.mouse_scan = 1;
        mouse_ps2.mouse_ps2_poll(5, 3, 0, 0, m);
        bad += Echange("un mouvement accumulé, lecture", m, 0xeb, [0xfa, 0x20, 5, 0xfd], [0xfa, 0x20, 5, 0xfd], hw);
        bad += Echange("lecture, encore", m, 0xeb, [0xfa, 0x20, 5, 0xfd], [0xfa, 0, 0, 0], hw);
        mouse_ps2.mouse_ps2_close(m);
        mouse_ps2.mouse_scan = 0;
        return bad;
    }

    // PB-101 — le PC1512 n'a qu'un port parallèle, en 378h (Amstrad PC1512 TRM, section 1, § 1.3, 1.4, 1.10). lpt_init
    // (common_init) pose LPT2 à 278h, puis amstrad_init : OUT 278h,AAh, IN 278h, PCem AAh (lpt2_dat) ; le PC1512 FFh,
    // rien ne répond.
    private static int Pb101(bool hw)
    {
        _808x.Reset();
        io.io_init();
        Lpt.lpt.lpt_init();
        amstrad.amstrad_init();
        io.outb(0x278, 0xaa);
        var v = io.inb(0x278);
        io.io_init();
        return Voir("PC1512, OUT 278h,AAh puis IN 278h", v == (hw ? 0xff : 0xaa), $"{v:X2}h", hw ? "FFh" : "AAh", hw);
    }

    // PB-103 — le chapeau à 315° (haut-gauche de l'hôte) se lit en haut, comme chacune des autres diagonales se lit à
    // la direction suivante dans le sens des aiguilles d'une montre. La CH : PCem F0h (au repos), « haut » 00h ; la TM,
    // l'axe 3 : PCem 0 (en bas), « haut » -32768. Les témoins, identiques dans les deux modes : 0° (le haut), 45° (la
    // droite), 135° (le bas), 225° (la gauche), -1 (au repos).
    private static int Pb103(bool hw)
    {
        var bad = 0;
        var js = plat_joystick.joystick_state[0];
        var (nr, pov) = (js.plat_joystick_nr, js.pov[0]);
        try
        {
            js.plat_joystick_nr = 1;
            foreach (var (angle, ch, tm) in new[] { (0, 0x00, -32768), (45, 0x40, -16384), (135, 0x80, 0), (225, 0xc0, 16384),
                                                     (-1, 0xf0, 32767), (315, hw ? 0x00 : 0xf0, hw ? -32768 : 0) })
            {
                js.pov[0] = angle;
                var c = joystick_ch_flightstick_pro_c.joystick_ch_flightstick_pro.read!(null);
                var t = joystick_tm_fcs_c.joystick_tm_fcs.read_axis!(null, 3);
                bad += Voir($"chapeau à {angle}°, la CH (201h) et la TM (axe 3)", c == ch && t == tm, $"{c:X2}h, {t}",
                            $"{ch:X2}h, {tm}", hw);
            }
        }
        finally
        {
            (js.plat_joystick_nr, js.pov[0]) = (nr, pov);
        }
        return bad;
    }
}
