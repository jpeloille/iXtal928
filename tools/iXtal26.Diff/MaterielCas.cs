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
// en 0000:0100 sur un fond de NOP ; ceux de PB-03 et de PB-257 montent une machine, le 5150.

using System.Reflection;
using iXtal26.Cpu;
using iXtal26.Diag;
using iXtal26.Memory;
using iXtal26.Models;

namespace iXtal26.Diff;

internal static class MaterielCas
{
    private static readonly SortedDictionary<int, Func<bool, int>> Cas = new()
    {
        [1] = Pb01, [2] = Pb02, [3] = Pb03, [7] = Pb07, [45] = Pb45, [87] = Pb87, [169] = Pb169, [170] = Pb170,
        [171] = Pb171, [172] = Pb172, [173] = Pb173, [174] = Pb174, [175] = Pb175, [176] = Pb176, [177] = Pb177,
        [179] = Pb179, [257] = Pb257, [258] = Pb258,
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
}
