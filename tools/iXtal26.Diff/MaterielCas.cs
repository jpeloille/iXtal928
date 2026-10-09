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
// du PC1512 ; celui de PB-103 pose l'état de la manette et lit la CH et la TM.

using System.Reflection;
using iXtal26.Cpu;
using iXtal26.Diag;
using iXtal26.Joystick;
using iXtal26.Keyboard;
using iXtal26.Memory;
using iXtal26.Models;
using iXtal26.Mouse;

namespace iXtal26.Diff;

internal static class MaterielCas
{
    private static readonly SortedDictionary<int, Func<bool, int>> Cas = new()
    {
        [1] = Pb01, [2] = Pb02, [3] = Pb03, [5] = Pb05, [7] = Pb07, [45] = Pb45, [87] = Pb87, [169] = Pb169, [170] = Pb170,
        [171] = Pb171, [172] = Pb172, [173] = Pb173, [174] = Pb174, [175] = Pb175, [176] = Pb176, [177] = Pb177,
        [179] = Pb179, [157] = Pb157, [246] = Pb246, [247] = Pb247, [248] = Pb248, [249] = Pb249, [250] = Pb250,
        [251] = Pb251, [252] = Pb252, [253] = Pb253, [255] = Pb255, [257] = Pb257, [258] = Pb258, [94] = Pb94,
        [95] = Pb95, [101] = Pb101, [103] = Pb103, [254] = Pb254, [261] = Pb261, [181] = Pb181, [182] = Pb182,
        [183] = Pb183, [184] = Pb184, [185] = Pb185, [186] = Pb186, [187] = Pb187, [188] = Pb188, [262] = Pb262,
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

    /// <summary>Joue une instruction sur le cœur 386 (ou 286, 486) en mode réel : le code en 0000:0100, sur un fond de NOP,
    /// SP = FFFEh, les drapeaux F002h, et les vecteurs 0 (#DE) et 6 (#UD) en 0000:0400 et 0000:0600, pour qu'une exception
    /// se lise à l'IP d'après. Rend les registres de 16 bits d'après.</summary>
    private static ushort[] Jouer386(byte[] code, Action<ushort[]> regs, Action? ram = null, int coeur = 386)
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
        for (var k = 0; k < code.Length; k++)
            mem.ram[0x100 + k] = code[k];
        ram?.Invoke();
        var r = new ushort[(int)R.COUNT];
        r[(int)R.SP] = 0xFFFE;
        r[(int)R.IP] = 0x100;
        r[(int)R.FLAGS] = 0xF002;
        regs(r);
        _808x.SetRegs(r);
        _386.Step286();
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
