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

using iXtal26.Cpu;
using iXtal26.Diag;
using iXtal26.Memory;

namespace iXtal26.Diff;

internal static class MaterielCas
{
    internal static int Run(string[] args)
    {
        var pb = args.FirstOrDefault(a => a.StartsWith("PB-", StringComparison.OrdinalIgnoreCase))?.ToUpperInvariant();
        var i = Array.IndexOf(args, "--attendu");
        var attendu = i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        if (attendu is not (null or "pcem" or "materiel"))
        {
            Console.Error.WriteLine("materiel-cas : --attendu pcem ou --attendu materiel.");
            return 2;
        }
        switch (pb)
        {
            case "PB-01":
                return Pb01((attendu ?? (materiel.pb_01 ? "materiel" : "pcem")) == "materiel");
            default:
                Console.Error.WriteLine("materiel-cas PB-nn : une correction du mode matériel (PB-01).");
                return 2;
        }
    }

    // PB-01 — l'AF d'ADC et de SBB du 8088. AF est la retenue (l'emprunt) du bit 3, retenue entrante comprise (SDM
    // vol. 1, § 3.4.3.1 ; mesuré par SST 8088 v2, formes 10 à 15 et 18 à 1D) ; PCem l'oublie quand les quartets bas
    // ne font la retenue qu'avec elle. Un cas par fonction de 808x.cs : setadc8, setadc16, setsbc8, setsbc16.
    private static int Pb01(bool materielAttendu)
    {
        var sonde = ModeMateriel.Sonde[1];
        var bad = 0;
        // 09h + 06h + 1 = 10h : PCem AF = 0, et DAA rend 10h ; le 8088 AF = 1, et DAA rend 16h.
        bad += Cas("ADC AL,06h puis DAA, CF = 1, AL = 09h", [0x14, 0x06, 0x27], 0x0009, 2,
                   materielAttendu ? (true, 0x0016) : (false, 0x0010), materielAttendu);
        bad += Cas("ADC AX,0006h, CF = 1, AX = 0009h", [0x15, 0x06, 0x00], 0x0009, 1,
                   materielAttendu ? (true, 0x0010) : (false, 0x0010), materielAttendu);
        // 15h - 05h - 1 = 0Fh : l'emprunt du bit 3 ne vient que de la retenue entrante.
        bad += Cas("SBB AL,05h, CF = 1, AL = 15h", [0x1C, 0x05], 0x0015, 1,
                   materielAttendu ? (true, 0x000F) : (false, 0x000F), materielAttendu);
        bad += Cas("SBB AX,0005h, CF = 1, AX = 0015h", [0x1D, 0x05, 0x00], 0x0015, 1,
                   materielAttendu ? (true, 0x000F) : (false, 0x000F), materielAttendu);
        var passages = ModeMateriel.Sonde[1] - sonde;
        Console.WriteLine($"  la sonde : PB-01 a servi {passages} fois");
        if (materiel.pb_01 && passages != 4)
        {
            bad++;
            Console.WriteLine("  la correction n'a pas servi aux quatre cas : le cas ne prouve rien");
        }
        Console.WriteLine(bad == 0
            ? $"\nVert : PB-01, les quatre cas rendent la valeur {(materielAttendu ? "du 8088 (mode matériel)" : "de PCem (mode PCem)")}."
            : $"\n{bad} échec(s) : PB-01 ne rend pas la valeur {(materielAttendu ? "du 8088" : "de PCem")}.");
        return bad == 0 ? 0 : 1;
    }

    // Les octets de code en 0000:0100, AX et CF = 1 posés ; `pas` instructions exécutées ; AF lu après la première,
    // AX après la dernière.
    private static int Cas(string nom, byte[] code, ushort ax, int pas, (bool af, int ax) attendu, bool materielAttendu)
    {
        _808x.Reset();
        mem.fill_ram(0x90);
        for (var k = 0; k < code.Length; k++)
            mem.ram[0x100 + k] = code[k];
        var regs = new ushort[(int)R.COUNT];
        regs[(int)R.AX] = ax;
        regs[(int)R.SP] = 0xFFFE;
        regs[(int)R.IP] = 0x0100;
        regs[(int)R.FLAGS] = 0xF003;
        _808x.SetRegs(regs);
        _808x.Step();
        _808x.GetRegs(regs);
        var af = (regs[(int)R.FLAGS] & 0x10) != 0;
        for (var k = 1; k < pas; k++)
            _808x.Step();
        _808x.GetRegs(regs);
        var ok = af == attendu.af && regs[(int)R.AX] == attendu.ax;
        Console.WriteLine($"  [{(ok ? "ok" : "ECHEC")}] {nom} : AF = {(af ? 1 : 0)}, AX = {regs[(int)R.AX]:X4} " +
                          $"(attendu {(materielAttendu ? "du 8088" : "de PCem")} : AF = {(attendu.af ? 1 : 0)}, " +
                          $"AX = {attendu.ax:X4})");
        return ok ? 0 : 1;
    }
}
