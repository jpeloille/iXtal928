// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// M20 — LE BANC DU MODE PROTÉGÉ DU 286.
//
// L'ami286 n'a pas de boot-diff (ni neat.c ni CMOS côté oracle) et le fuzzeur ne part
// qu'en mode réel. Or l'oracle lie x86seg.c EN ENTIER : loadcscall, taskswitch286,
// LAR, LSL y tournent déjà. Il manquait un moyen de mettre les deux cœurs dans le MÊME
// état de mode protégé. C'est LOADALL (0F 05, x86_ops_misc.h:827-881), transcrit et
// atteignable en mode réel : il recharge l'état entier — MSW, registres, caches
// descripteurs, GDTR, LDTR, IDTR, TR — depuis la table en 0x800.
//
// Chaque cas écrit les mêmes tables des deux côtés (GDT de 26 entrées, LDT, IDT, deux TSS, piles),
// pose LOADALL en 0000:7C00, puis exécute pas à pas : LOADALL, l'instruction testée, et
// quelques NOP à l'arrivée. Après CHAQUE pas : état complet (Fuzzer.Compare), journal
// d'écritures, hachage de la RAM entière. Un arrêt fatal() du C# est un résultat NOMMÉ.
//
// G13.5c — les cas qui discriminent une correction du mode protégé portent leur attente (`Expect`, celle de PCem,
// vérifiée sur l'état de l'oracle) et celle du mode matériel (`ExpectMateriel`, du manuel du 286 : 80286 PRM, pages CALL,
// JMP, INT et LTR, #GP partout, #NP pour la présence). `--target csharp` joue le C# seul, chaque cas jugé sur son
// attente, celle du mode matériel quand sa correction (`Pb`) est demandée ; LOADALL, celui du 286, y pose le décor dans
// les deux modes. `materiel-cas PB-nn` joue ces cas avec ceux du 386 (PmCheck386.CasMateriel).

using iXtal26.Cpu;
using iXtal26.Diag;
using iXtal26.Memory;

namespace iXtal26.Diff;

public static class PmCheck
{
    // Adresses physiques des structures (toutes sous 640 Ko).
    private const uint Gdt = 0x1000, Ldt = 0x2000, Idt = 0x3000, Tss = 0x4000, Tss2 = 0x4100;
    private const uint CodeBase = 0x20000, DataBase = 0x30000, StackBase = 0x40000;
    private const uint TargetBase = 0x50000, Code3Base = 0x60000, Stack3Base = 0x70000;

    // Sélecteurs de la GDT.
    private const ushort SelCode0 = 0x08, SelData0 = 0x10, SelStack0 = 0x18, SelTarget = 0x20,
        SelConform = 0x28, SelCode3 = 0x33, SelStack3 = 0x3B, SelGate3 = 0x43, SelGate0 = 0x48,
        SelTss = 0x50, SelNotPresent = 0x58, SelDataAsCode = 0x60, SelLdtDesc = 0x68,
        SelTaskGate = 0x70, SelTss2 = 0x78, SelTinyStack = 0x80, SelGateNp = 0x8B;

    private sealed class Case
    {
        public string Name = "";
        public bool Cpl3;
        public byte[] Code = [];
        public int GateWords;                  // mots copiés par les portes d'appel 0x40/0x88
        public ushort Bx;                      // BX au départ (LAR/LSL, VERR/VERW)
        public ushort Ax;                      // AX au départ (LLDT/LTR)
        public int Steps = 3;                  // pas APRÈS LOADALL
        public Action<Action<uint, byte[]>>? Tweak;
        public ushort[]? StackWords;           // mots posés en SS:SP au départ
        public ushort IdtLimit = 32 * 8 - 1;   // G13.5c : la limite de l'IDT
        public int Pb;                         // G13.5c : la correction que le cas discrimine
        public Func<HState, string?>? Expect;  // G13.5c : l'attente de PCem, sur l'état jugé
        public Func<HState, string?>? ExpectMateriel; // G13.5c : celle du mode matériel, si elle diffère
    }

    /// <summary>`--target csharp` : le C# seul (G13.5c).</summary>
    internal static bool Csharp;

    /// <summary>UN PROCESSUS PAR CAS. Ni h_reset ni Reset286 ni LOADALL ne remettent tout
    /// l'état d'un cœur : limit_raw des caches de segment, les sept globales du mode
    /// protégé (C7a), un optype laissé par une instruction qu'un fatal() a coupée en deux.
    /// Enchaînés dans un même processus, les cas héritaient chacun du précédent — MESURÉ :
    /// un cas qui chargeait un CS de limite 0xFF laissait « CS limit_raw 0xFF » à tous
    /// les suivants côté oracle. Isolés, les deux cœurs partent neufs.</summary>
    public static int Run(int only = -1)
    {
        var cases = Cases();
        if (Csharp)
            return RunCsharp(cases, only);
        Oracle.CheckAbi();
        if (only >= 0)
        {
            Console.WriteLine(RunCase(cases[only]));
            return 0;
        }
        int vert = 0, rouge = 0, arret = 0;
        Console.WriteLine($"pm-check — {cases.Count} cas en mode protégé, état construit par LOADALL, un processus par cas\n");
        var self = Environment.ProcessPath!;
        var dll = typeof(PmCheck).Assembly.Location;
        for (var k = 0; k < cases.Count; k++)
        {
            var c = cases[k];
            var psi = new System.Diagnostics.ProcessStartInfo(self)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            if (Path.GetFileNameWithoutExtension(self) == "dotnet")
                psi.ArgumentList.Add(dll);
            psi.ArgumentList.Add("pm-check");
            psi.ArgumentList.Add("--case");
            psi.ArgumentList.Add(k.ToString());
            using var proc = System.Diagnostics.Process.Start(psi)!;
            var outp = proc.StandardOutput.ReadToEnd();
            proc.StandardError.ReadToEnd();
            proc.WaitForExit();
            var r = outp.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim() ?? "ARRÊT : aucune sortie";
            if (r.StartsWith("vert", StringComparison.Ordinal)) vert++;
            else if (r.StartsWith("ARRÊT", StringComparison.Ordinal)) arret++;
            else rouge++;
            Console.WriteLine($"  {c.Name,-52} {r}");
        }
        Console.WriteLine($"\n{vert} vert(s), {rouge} rouge(s), {arret} arrêt(s) du C# sur {cases.Count}.");
        return rouge + arret == 0 ? 0 : 1;
    }

    private static byte[] Desc(uint limit, uint @base, byte access) =>
        [(byte)limit, (byte)(limit >> 8), (byte)@base, (byte)(@base >> 8), (byte)(@base >> 16), access, 0, 0];

    private static byte[] Gate(ushort off, ushort sel, int words, byte access) =>
        [(byte)off, (byte)(off >> 8), (byte)sel, (byte)(sel >> 8), (byte)(words & 31), access, 0, 0];

    private static byte[] W(params ushort[] w)
    {
        var b = new byte[w.Length * 2];
        for (var i = 0; i < w.Length; i++) { b[2 * i] = (byte)w[i]; b[2 * i + 1] = (byte)(w[i] >> 8); }
        return b;
    }

    private static void Build(Case c, Action<uint, byte[]> poke)
    {
        // GDT
        var g = new List<byte[]>
        {
            new byte[8],                                        // 00 nul
            Desc(0xFFFF, CodeBase, 0x9A),                       // 08 code DPL0
            Desc(0xFFFF, DataBase, 0x92),                       // 10 données DPL0
            Desc(0xFFFF, StackBase, 0x92),                      // 18 pile DPL0
            Desc(0xFFFF, TargetBase, 0x9A),                     // 20 code DPL0, cible
            Desc(0xFFFF, TargetBase, 0x9E),                     // 28 code conforme DPL0
            Desc(0xFFFF, Code3Base, 0xFA),                      // 30 code DPL3
            Desc(0xFFFF, Stack3Base, 0xF2),                     // 38 pile DPL3
            Gate(0x0100, SelTarget, c.GateWords, 0xE4),         // 40 porte d'appel DPL3 -> 20
            Gate(0x0100, SelTarget, 0, 0x84),                   // 48 porte d'appel DPL0 -> 20
            Desc(0x002B, Tss, 0x81),                            // 50 TSS 286 disponible
            Desc(0xFFFF, TargetBase, 0x1A),                     // 58 code NON présent
            Desc(0xFFFF, DataBase, 0x92),                       // 60 données (cible de CALL : #GP)
            Desc(0x00FF, Ldt, 0x82),                            // 68 LDT
            Gate(0x0000, SelTss2, 0, 0x85),                     // 70 porte de tâche -> 78
            Desc(0x002B, Tss2, 0x81),                           // 78 TSS 286 n°2
            Desc(0x000F, StackBase, 0x92),                      // 80 pile DPL0 minuscule
            Gate(0x0100, SelNotPresent, c.GateWords, 0xE4),     // 88 porte DPL3 -> code absent
        };
        // Index 25 — le sélecteur 00CB de Windows : GDT (TI = 0), RPL 3. Code DPL3.
        while (g.Count < 25) g.Add(new byte[8]);
        g.Add(Desc(0xFFFF, TargetBase, 0xFA));
        for (var i = 0; i < g.Count; i++) poke(Gdt + (uint)(8 * i), g[i]);

        // LDT : index 1 (sélecteur 000F en RPL 3), code DPL3.
        poke(Ldt + 8, Desc(0xFFFF, TargetBase, 0xFA));

        // IDT : 32 portes d'interruption DPL0 vers 08:E000, et 0x1F DPL3 (atteignable en CPL3).
        for (uint v = 0; v < 32; v++)
            poke(Idt + 8 * v, Gate(0xE000, SelCode0, 0, (byte)(v == 0x1F ? 0xE6 : 0x86)));
        // G13.5c : un cas qui discrimine une correction distingue #TS, #NP et #GP par leur gestionnaire.
        if (c.Pb != 0)
            foreach (var v in new[] { VecTs, VecNp, VecGp })
                poke(Idt + 8 * (uint)v, Gate((ushort)Gestionnaire(v), SelCode0, 0, 0x86));

        // TSS : SP0/SS0 = 18:8000 ; SS1/SS2 laissés nuls.
        poke(Tss, W(0, 0x8000, SelStack0, 0, 0, 0, 0));
        // TSS n°2 : cible du changement de tâche.
        poke(Tss2, W(0, 0x7000, SelStack0, 0, 0, 0, 0,
                     0x0200, 0x0002, 0x1111, 0x2222, 0x3333, 0x4444, 0x7000, 0x5555, 0x6666, 0x7777,
                     SelData0, SelCode0, SelStack0, SelData0, SelLdtDesc));

        // Pile de départ : paramètres reconnaissables pour les portes d'appel.
        var sp0 = (ushort)0xFFE0;
        var stackBase = c.Cpl3 ? Stack3Base : StackBase;
        var parms = new ushort[32];
        for (var i = 0; i < parms.Length; i++) parms[i] = (ushort)(0xA000 + i);
        poke(stackBase + sp0 - 0x40, W(parms));
        if (c.StackWords is not null)
            poke(stackBase + sp0, W(c.StackWords));

        // Le code testé, en CS:1000.
        poke((c.Cpl3 ? Code3Base : CodeBase) + 0x1000, c.Code);
        // Un pointeur lointain en DS:0000 pour CALL m16:16 (FF /3) : 20:0100.
        poke(DataBase, W(0x0100, SelTarget));

        c.Tweak?.Invoke(poke);

        // La table de LOADALL, en 0x800 (x86_ops_misc.h:827-881).
        var t = new byte[0x66];
        void Tw(int off, ushort v) { t[off] = (byte)v; t[off + 1] = (byte)(v >> 8); }
        void Td(int off, uint @base, byte access, ushort limit)
        {
            t[off] = (byte)@base; t[off + 1] = (byte)(@base >> 8); t[off + 2] = (byte)(@base >> 16);
            t[off + 3] = access; t[off + 4] = (byte)limit; t[off + 5] = (byte)(limit >> 8);
        }
        Tw(0x06, 0xFFF1);                                       // MSW : PE
        Tw(0x16, SelTss);                                       // TR
        Tw(0x18, 0x0002);                                       // FLAGS
        Tw(0x1A, 0x1000);                                       // IP
        Tw(0x1C, SelLdtDesc);                                   // LDTR
        Tw(0x1E, SelData0);                                     // DS
        Tw(0x20, c.Cpl3 ? SelStack3 : SelStack0);               // SS
        Tw(0x22, c.Cpl3 ? SelCode3 : SelCode0);                 // CS
        Tw(0x24, SelData0);                                     // ES
        Tw(0x2C, (ushort)(sp0 - 0x40));                         // SP : sous les paramètres
        if (c.StackWords is not null) Tw(0x2C, sp0);
        Tw(0x2E, c.Bx);                                         // BX
        Tw(0x34, c.Ax);                                         // AX
        Td(0x36, DataBase, 0x93, 0xFFFF);                       // ES
        Td(0x3C, c.Cpl3 ? Code3Base : CodeBase, (byte)(c.Cpl3 ? 0xFB : 0x9B), 0xFFFF);
        Td(0x42, c.Cpl3 ? Stack3Base : StackBase, (byte)(c.Cpl3 ? 0xF3 : 0x93), 0xFFFF);
        Td(0x48, DataBase, 0x93, 0xFFFF);                       // DS
        Td(0x4E, Gdt, 0, 26 * 8 - 1);                           // GDTR
        Td(0x54, Ldt, 0x82, 0x00FF);                            // LDTR
        Td(0x5A, Idt, 0, c.IdtLimit);                           // IDTR
        Td(0x60, Tss, 0x81, 0x002B);                            // TR
        poke(0x800, t);

        poke(0x7C00, [0x0F, 0x05]);                             // LOADALL
    }

    private static string RunCase(Case c)
    {
        Oracle.h_set_core(Oracle.Core286);
        Oracle.h_reset();
        Oracle.h_fill_ram(0x90);
        _386.Reset286();
        mem.fill_ram(0x90);

        Build(c, (addr, bytes) =>
        {
            Oracle.h_load(addr, bytes, (uint)bytes.Length);
            for (var i = 0; i < bytes.Length; i++) mem.ram[(addr + i) & mem.rammask] = bytes[i];
        });

        var regs = new ushort[(int)R.COUNT];
        regs[(int)R.IP] = 0x7C00;
        regs[(int)R.FLAGS] = 0x0002;
        Oracle.h_setregs(regs);
        _808x.SetRegs(regs);

        var a = HState.Create();
        var b = HState.Create();
        for (var s = 0; s <= c.Steps; s++)
        {
            Oracle.h_wlog_reset();
            mem.wlog_reset();
            var cycC = Oracle.h_step();
            int cycS;
            try
            {
                cycS = _386.Step286();
            }
            catch (Exception e)
            {
                return $"ARRÊT C# au pas {s} : {e.Message.Trim()}";
            }
            Oracle.h_getstate(out a);
            _808x.GetState(ref b);
            var d = Fuzzer.Compare(a, b, cycC, cycS) ?? Fuzzer.CmpWrites() ?? Fuzzer.CmpRam();
            if (d is not null)
                return $"ROUGE au pas {s} : {d}";
        }
        if (c.Expect?.Invoke(a) is { } attente)
            return $"ATTENTE NON TENUE (oracle) : {attente}";
        return $"vert — CS:IP {a.seg_sel[(int)Seg.CS]:X4}:{a.pc:X4}, SS:SP {a.seg_sel[(int)Seg.SS]:X4}:{a.regs[4] & 0xFFFF:X4}";
    }

    // ===== G13.5c — le C# seul =====

    private static int RunCsharp(List<Case> cases, int only)
    {
        if (only >= 0)
        {
            var r1 = RunCaseCsharp(cases[only], Materiel(cases[only]));
            Console.WriteLine(r1);
            return r1.StartsWith("vert", StringComparison.Ordinal) ? 0 : 1;
        }
        Console.WriteLine($"pm-check --target csharp ({ModeMateriel.Description}) — {cases.Count} cas, le 286 seul, décor par LOADALL\n");
        int vert = 0, rouge = 0, attentes = 0, materiel = 0;
        foreach (var c in cases)
        {
            var hw = Materiel(c);
            var r = RunCaseCsharp(c, hw);
            if (r.StartsWith("vert", StringComparison.Ordinal)) vert++; else rouge++;
            if (c.Expect is not null || (hw && c.ExpectMateriel is not null)) attentes++;
            if (hw && c.ExpectMateriel is not null) materiel++;
            Console.WriteLine($"  {c.Name,-52} {(hw && c.ExpectMateriel is not null ? "[matériel] " : "")}{r}");
        }
        Console.WriteLine($"\n{vert} vert(s), {rouge} rouge(s) sur {cases.Count} ; {attentes} cas jugés sur une attente, " +
                          $"dont {materiel} sur celle du mode matériel.");
        return rouge == 0 ? 0 : 1;
    }

    private static bool Materiel(Case c) => c.Pb != 0 && ModeMateriel.Demande(c.Pb);

    /// <summary>`materiel-cas PB-nn` : les cas du 286 qui discriminent la correction, en C# seul, jugés sur l'attente
    /// demandée. Rend le nombre d'échecs ; -1 si aucun cas.</summary>
    internal static int CasMateriel(int pb, bool materielAttendu)
    {
        Csharp = true;
        var cas = Cases().Where(c => c.Pb == pb).ToList();
        if (cas.Count == 0)
            return -1;
        var bad = 0;
        foreach (var c in cas)
        {
            var r = RunCaseCsharp(c, materielAttendu);
            var ok = r.StartsWith("vert", StringComparison.Ordinal);
            Console.WriteLine($"  [{(ok ? "ok" : "ECHEC")}] 286 : {c.Name} : {r} (attendu {(materielAttendu ? "du matériel" : "de PCem")})");
            if (!ok) bad++;
        }
        return bad;
    }

    private static string RunCaseCsharp(Case c, bool hw)
    {
        _386.Reset286();
        mem.fill_ram(0x90);
        _386.ClearSegResidue();
        Build(c, (addr, bytes) =>
        {
            for (var i = 0; i < bytes.Length; i++) mem.ram[(addr + i) & mem.rammask] = bytes[i];
        });
        var regs = new ushort[(int)R.COUNT];
        regs[(int)R.IP] = 0x7C00;
        regs[(int)R.FLAGS] = 0x0002;
        _808x.SetRegs(regs);
        for (var s = 0; s <= c.Steps; s++)
        {
            try
            {
                _386.Step286();
            }
            catch (Exception e)
            {
                return $"ARRÊT C# au pas {s} : {e.Message.Trim()}";
            }
        }
        var b = HState.Create();
        _808x.GetState(ref b);
        if ((hw ? c.ExpectMateriel ?? c.Expect : c.Expect)?.Invoke(b) is { } attente)
            return $"ATTENTE NON TENUE (C#) : {attente}";
        return $"vert — CS:IP {b.seg_sel[(int)Seg.CS]:X4}:{b.pc:X4}, SS:SP {b.seg_sel[(int)Seg.SS]:X4}:{b.regs[4] & 0xFFFF:X4}";
    }

    private const int VecTs = 0x0A, VecNp = 0x0B, VecGp = 0x0D;
    private static uint Gestionnaire(int v) => v is VecTs or VecNp or VecGp ? 0xE000u + (uint)v * 0x10 : 0xE000u;
    private static string? Att(bool ok, string quoi) => ok ? null : quoi;

    /// <summary>Un mot de la mémoire du côté jugé : l'oracle, ou le C# sous `--target csharp`.</summary>
    private static ushort MemW(uint addr) => Csharp
        ? (ushort)(mem.ram[addr] | (mem.ram[addr + 1] << 8))
        : (ushort)(Oracle.ReadByte(addr) | (Oracle.ReadByte(addr + 1) << 8));

    private static byte[] CallFar(ushort sel, ushort off) => [0x9A, (byte)off, (byte)(off >> 8), (byte)sel, (byte)(sel >> 8)];
    private static byte[] JmpFar(ushort sel, ushort off) => [0xEA, (byte)off, (byte)(off >> 8), (byte)sel, (byte)(sel >> 8)];

    private static List<Case> Cases()
    {
        var l = new List<Case>
        {
            // --- bloc C, déjà transcrit : vérification rétroactive ---
            new() { Name = "JMP FAR même privilège (loadcsjmp)", Code = JmpFar(SelTarget, 0x0100) },
            new() { Name = "JMP FAR conforme (loadcsjmp)", Code = JmpFar(SelConform, 0x0100) },
            new() { Name = "JMP FAR par porte d'appel (loadcsjmp)", Code = JmpFar(SelGate0, 0x0000) },
            new() { Name = "JMP FAR sélecteur nul -> #GP", Code = JmpFar(0x0000, 0x0000) },
            new() { Name = "JMP FAR code absent -> #NP", Code = JmpFar(SelNotPresent, 0x0100) },
            new() { Name = "RETF même privilège (pmoderetf)", Code = [0xCB], StackWords = [0x0100, SelTarget] },
            new() { Name = "RETF vers CPL3 (pmoderetf)", Code = [0xCB], StackWords = [0x0100, SelCode3, 0xE000, SelStack3] },
            new() { Name = "RETF 4 vers CPL3 (pmoderetf)", Code = [0xCA, 0x04, 0x00], StackWords = [0x0100, SelCode3, 0x1111, 0x2222, 0xE000, SelStack3] },
            new() { Name = "INT 10h, porte d'interruption (pmodeint)", Code = [0xCD, 0x10] },
            new() { Name = "INT 1Fh depuis CPL3, bascule de pile (pmodeint)", Cpl3 = true, Code = [0xCD, 0x1F] },
            new() { Name = "INT 10h depuis CPL3, DPL0 -> #GP (pmodeint)", Cpl3 = true, Code = [0xCD, 0x10] },
            new() { Name = "IRET même privilège (pmodeiret)", Code = [0xCF], StackWords = [0x0100, SelTarget, 0x0002] },
            new() { Name = "IRET vers CPL3 (pmodeiret)", Code = [0xCF], StackWords = [0x0100, SelCode3, 0x0202, 0xE000, SelStack3] },

            // --- loadcscall : CALL FAR ---
            new() { Name = "CALL FAR même privilège", Code = CallFar(SelTarget, 0x0100) },
            new() { Name = "CALL FAR conforme", Code = CallFar(SelConform, 0x0100) },
            new() { Name = "CALL FAR conforme depuis CPL3", Cpl3 = true, Code = CallFar(SelConform, 0x0100) },
            new() { Name = "CALL FAR m16:16 (FF /3)", Code = [0xFF, 0x1E, 0x00, 0x00] },
            new() { Name = "CALL FAR GDT index 25 (00CB, comme Windows)", Cpl3 = true, Code = CallFar(0x00CB, 0x0100) },
            new() { Name = "CALL FAR par la LDT (000F)", Cpl3 = true, Code = CallFar(0x000F, 0x0100) },
            new() { Name = "CALL FAR porte d'appel même privilège", Code = CallFar(SelGate0, 0x0000) },
            new() { Name = "CALL FAR sélecteur nul -> #GP", Code = CallFar(0x0000, 0x0000) },
            new() { Name = "CALL FAR hors de la GDT -> #GP", Code = CallFar(0x0400, 0x0000) },
            new() { Name = "CALL FAR segment de données -> #GP", Code = CallFar(SelDataAsCode, 0x0000) },
            new() { Name = "CALL FAR code absent -> #NP", Code = CallFar(SelNotPresent, 0x0100) },
            new() { Name = "CALL FAR DPL0 depuis CPL3 -> #GP", Cpl3 = true, Code = CallFar(0x0023, 0x0100) },
            new() { Name = "CALL FAR hors limite du code -> #GP", Code = CallFar(SelTarget, 0x0100),
                    Tweak = p => p(Gdt + 4 * 8, Desc(0x00FF, TargetBase, 0x9A)) },
        };
        foreach (var n in new[] { 0, 1, 5, 31 })
            l.Add(new() { Name = $"CALL FAR porte CPL3->0, {n} paramètre(s)", Cpl3 = true, GateWords = n, Code = CallFar(SelGate3, 0x0000) });
        l.AddRange(new Case[]
        {
            new() { Name = "CALL FAR porte vers code absent -> #NP", Cpl3 = true, Code = CallFar(SelGateNp, 0x0000) },
            new() { Name = "CALL FAR porte, SS0 nul dans la TSS -> #TS", Cpl3 = true, Code = CallFar(SelGate3, 0x0000),
                    Tweak = p => p(Tss + 4, W(0x0000)) },
            new() { Name = "CALL FAR porte, pile SS0 trop petite -> #SS", Cpl3 = true, GateWords = 5, Code = CallFar(SelGate3, 0x0000),
                    Tweak = p => p(Tss + 4, W(SelTinyStack)) },
            new() { Name = "CALL FAR porte de tâche (taskswitch286)", Code = CallFar(SelTaskGate, 0x0000) },
            new() { Name = "CALL FAR TSS directe (taskswitch286)", Code = CallFar(SelTss2, 0x0000) },
            new() { Name = "JMP FAR TSS directe (taskswitch286)", Code = JmpFar(SelTss2, 0x0000) },

            // --- LAR / LSL (0F 02 C3 : LAR AX,BX ; 0F 03 C3 : LSL AX,BX) ---
        });
        foreach (var (sel, what) in new (ushort, string)[]
                 { (SelCode0, "code"), (SelData0, "données"), (SelGate3, "porte d'appel"), (SelTss, "TSS"),
                   (SelNotPresent, "absent"), (SelLdtDesc, "LDT"), (0x0000, "nul"), (0x0400, "hors GDT"), (0x000F, "LDT idx 1") })
        {
            l.Add(new() { Name = $"LAR AX,BX — {what} ({sel:X4})", Bx = sel, Code = [0x0F, 0x02, 0xC3], Steps = 1 });
            l.Add(new() { Name = $"LSL AX,BX — {what} ({sel:X4})", Bx = sel, Code = [0x0F, 0x03, 0xC3], Steps = 1 });
        }
        foreach (var (sel, what) in new (ushort, string)[] { (SelTarget, "code"), (SelDataAsCode, "données") })
            l.Add(new() { Name = $"LAR AX,BX depuis CPL3 — {what} DPL0", Cpl3 = true, Bx = sel, Code = [0x0F, 0x02, 0xC3], Steps = 1 });

        // --- 0F 00 : SLDT, STR, LLDT, LTR, VERR, VERW (x86_ops_pmode.h:176-336) ---
        // Ajoutés après l'arrêt de Windows : DOSX fait LLDT AX puis LTR SI, et le C#
        // aiguillait les six sur la globale rmdat au lieu du paramètre.
        l.Add(new() { Name = "SLDT AX (0F 00 C0)", Code = [0x0F, 0x00, 0xC0], Steps = 1 });
        l.Add(new() { Name = "STR AX (0F 00 C8)", Code = [0x0F, 0x00, 0xC8], Steps = 1 });
        l.Add(new() { Name = "LLDT AX (0F 00 D0)", Ax = SelLdtDesc, Code = [0x0F, 0x00, 0xD0], Steps = 1 });
        l.Add(new() { Name = "LTR AX (0F 00 D8), TSS n°2", Ax = SelTss2, Code = [0x0F, 0x00, 0xD8], Steps = 1 });
        l.Add(new() { Name = "LTR depuis CPL3 -> #GP", Cpl3 = true, Ax = SelTss2, Code = [0x0F, 0x00, 0xD8], Steps = 1 });
        l.Add(new() { Name = "VERR BX — code (0F 00 E3)", Bx = SelCode0, Code = [0x0F, 0x00, 0xE3], Steps = 1 });
        l.Add(new() { Name = "VERW BX — données (0F 00 EB)", Bx = SelData0, Code = [0x0F, 0x00, 0xEB], Steps = 1 });
        l.Add(new() { Name = "VERW BX — code (0F 00 EB)", Bx = SelCode0, Code = [0x0F, 0x00, 0xEB], Steps = 1 });
        l.Add(new() { Name = "LLDT puis CALL FAR par la nouvelle LDT", Ax = SelLdtDesc,
                      Code = [0x0F, 0x00, 0xD0, .. CallFar(0x000C, 0x0100)] });
        l.Add(new() { Name = "LTR puis porte CPL3->0 (la TSS chargée donne SS0)", Cpl3 = false, Ax = SelTss2,
                      Code = [0x0F, 0x00, 0xD8], Steps = 1 });

        // --- double faute : #GP livré par une porte 13 absente ---
        l.Add(new() { Name = "double faute : #GP puis porte 13 absente", Code = JmpFar(0x0000, 0x0000),
                      Tweak = p => p(Idt + 13 * 8, Gate(0xE000, SelCode0, 0, 0x06)) });
        l.Add(new() { Name = "triple faute : portes 13 et 8 absentes", Code = JmpFar(0x0000, 0x0000),
                      Tweak = p => { p(Idt + 13 * 8, Gate(0xE000, SelCode0, 0, 0x06)); p(Idt + 8 * 8, Gate(0xE000, SelCode0, 0, 0x06)); } });

        // --- G13.5c : les cas qui discriminent les corrections du mode protégé, sur le 286. Un pas après LOADALL. Une
        // faute en anneau 0 empile FLAGS, CS, IP et son code sous SP (FFA0h) ; depuis l'anneau 3, la pile bascule sur
        // SS0:SP0 de la TSS (18:8000), avec SS et SP en plus. #TS, #NP et #GP ont chacun leur gestionnaire. Les
        // attentes du mode matériel sont celles du 80286 PRM : #GP partout, #NP pour la présence.
        static string? Faute(HState a, ushort sp, ushort code, int v = VecGp) =>
            Att(a.seg_sel[(int)Seg.CS] == SelCode0 && a.pc == Gestionnaire(v),
                $"CS:IP {a.seg_sel[(int)Seg.CS]:X4}:{a.pc:X4}, attendu {SelCode0:X4}:{Gestionnaire(v):X4} (vecteur {v:X2}h)")
            ?? Att(a.seg_sel[(int)Seg.SS] == SelStack0 && (a.regs[4] & 0xFFFF) == sp,
                   $"SS:SP {a.seg_sel[(int)Seg.SS]:X4}:{a.regs[4] & 0xFFFF:X4}, attendu {SelStack0:X4}:{sp:X4}")
            ?? Att(MemW(StackBase + sp) == code, $"code d'erreur 0x{MemW(StackBase + sp):X4}, attendu 0x{code:X4}");
        static string? Tr(HState a, ushort tr) => Att(a.sys_sel[(int)Sys.TR] == tr, $"TR {a.sys_sel[(int)Sys.TR]:X4}, attendu {tr:X4}");
        static string? Tache(HState a, ushort sp, bool nt) =>
            Tr(a, SelTss2)
            ?? Att(a.seg_sel[(int)Seg.CS] == SelCode0 && a.pc == 0x0200, $"CS:IP {a.seg_sel[(int)Seg.CS]:X4}:{a.pc:X4}")
            ?? Att((a.regs[4] & 0xFFFF) == sp, $"SP {a.regs[4] & 0xFFFF:X4}, attendu {sp:X4}")
            ?? Att((a.regs[0] & 0xFFFF) == 0x1111, $"AX {a.regs[0] & 0xFFFF:X4}")
            ?? Att(((a.flags & 0x4000) != 0) == nt, $"FLAGS {a.flags:X4}, NT attendu {nt}")
            ?? Att(!nt || MemW(Tss2) == SelTss, $"lien arrière {MemW(Tss2):X4}");
        void Acces(Action<uint, byte[]> p, byte acces) => p(Gdt + SelTss2 + 5, [acces]);
        // LOADALL, celui du 286, ne pose pas oldcpl (x86_ops_misc.h:827-881) : une faute juste après se livrerait comme
        // depuis l'anneau 0, sans bascule de pile. Un cas de l'anneau 3 fait d'abord un JMP FAR dans son propre code
        // (0033:1005), qui le pose, comme tout programme y arrive ; deux pas.
        static byte[] Anneau3(byte[] code) => [.. JmpFar(SelCode3, 0x1005), .. code];
        void TssDansLdt(Action<uint, byte[]> p) => p(Ldt + 0x10, Desc(0x002B, Tss2, 0x81));   // sélecteur 0014h
        const ushort Sp = 0xFFA0 - 8, Sp3 = 0x8000 - 12;

        l.Add(new() { Name = "INT 20h hors d'une IDT de 32 portes -> #GP", Code = [0xCD, 0x20], Steps = 1, Pb = 32,
                      Expect = a => Faute(a, Sp, 0), ExpectMateriel = a => Faute(a, Sp, 0x0102) });
        l.Add(new() { Name = "INT 1Fh, porte à cheval sur la limite (FCh) -> #GP", Code = [0xCD, 0x1F], Steps = 1, IdtLimit = 0xFC,
                      Pb = 192, Expect = a => Att(a.pc == 0xE000 && (a.regs[4] & 0xFFFF) == 0xFFA0 - 6, $"IP {a.pc:X4}, SP {a.regs[4] & 0xFFFF:X4}"),
                      ExpectMateriel = a => Faute(a, Sp, 0x00FA) });
        l.Add(new() { Name = "DIV BX nul, porte 0 de type nul -> #GP, EXT", Code = [0xF7, 0xF3], Steps = 1,
                      Tweak = p => p(Idt, new byte[8]), Pb = 192,
                      Expect = a => Faute(a, Sp, 0x0002), ExpectMateriel = a => Faute(a, Sp, 0x0003) });
        l.Add(new() { Name = "CALL FAR porte de tâche (286) -> commutation", Code = CallFar(SelTaskGate, 0), Steps = 1, Pb = 39,
                      Expect = a => Faute(a, Sp, SelTaskGate),
                      ExpectMateriel = a => Tache(a, ModeMateriel.Demande(40) ? (ushort)0x7000 : (ushort)0x6FFC, true) });
        l.Add(new() { Name = "JMP FAR porte de tâche (286) -> commutation", Code = JmpFar(SelTaskGate, 0), Steps = 1, Pb = 39,
                      Expect = a => Faute(a, Sp, 0), ExpectMateriel = a => Tache(a, 0x7000, false) });
        l.Add(new() { Name = "CALL FAR porte de tâche DPL 0 depuis CPL3 -> #GP (témoin)", Cpl3 = true,
                      Code = Anneau3(CallFar(SelTaskGate, 0)), Steps = 2, Pb = 39, Expect = a => Faute(a, Sp3, SelTaskGate) });
        l.Add(new() { Name = "CALL FAR TSS 286 : rien d'empilé", Code = CallFar(SelTss2, 0), Steps = 1, Pb = 40,
                      Expect = a => Tache(a, 0x6FFC, true),
                      ExpectMateriel = a => Tache(a, 0x7000, true)
                          ?? Att(MemW(StackBase + 0x6FFC) == 0x9090 && MemW(StackBase + 0x6FFE) == 0x9090, "écrit sous 7000h") });
        l.Add(new() { Name = "LTR AX, segment de données (0010h) -> #GP", Ax = SelData0, Code = [0x0F, 0x00, 0xD8], Steps = 1, Pb = 190,
                      Expect = a => Tr(a, SelData0), ExpectMateriel = a => Faute(a, Sp, SelData0) ?? Tr(a, SelTss) });
        l.Add(new() { Name = "LTR AX, TSS 386 (type 9) sur le 286 -> #GP", Ax = SelTss2, Code = [0x0F, 0x00, 0xD8], Steps = 1,
                      Tweak = p => Acces(p, 0x89), Pb = 190,
                      Expect = a => Tr(a, SelTss2), ExpectMateriel = a => Faute(a, Sp, SelTss2) ?? Tr(a, SelTss) });
        l.Add(new() { Name = "LTR AX, TSS occupée -> #GP", Ax = SelTss2, Code = [0x0F, 0x00, 0xD8], Steps = 1,
                      Tweak = p => Acces(p, 0x83), Pb = 190,
                      Expect = a => Tr(a, SelTss2), ExpectMateriel = a => Faute(a, Sp, SelTss2) ?? Tr(a, SelTss) });
        l.Add(new() { Name = "LTR AX, TSS absente -> #NP", Ax = SelTss2, Code = [0x0F, 0x00, 0xD8], Steps = 1,
                      Tweak = p => Acces(p, 0x01), Pb = 190,
                      Expect = a => Tr(a, SelTss2), ExpectMateriel = a => Faute(a, Sp, SelTss2, VecNp) ?? Tr(a, SelTss) });
        l.Add(new() { Name = "CALL FAR TSS de DPL 0 depuis CPL3 -> #GP", Cpl3 = true, Code = Anneau3(CallFar(SelTss2, 0)), Steps = 2,
                      Pb = 191, Expect = a => Tr(a, SelTss2), ExpectMateriel = a => Faute(a, Sp3, SelTss2) });
        l.Add(new() { Name = "CALL FAR TSS absente -> #NP", Code = CallFar(SelTss2, 0), Steps = 1, Tweak = p => Acces(p, 0x01), Pb = 191,
                      Expect = a => Tr(a, SelTss2), ExpectMateriel = a => Faute(a, Sp, SelTss2, VecNp) });
        l.Add(new() { Name = "JMP FAR TSS de la LDT (0014h) -> #GP", Code = JmpFar(0x0014, 0), Steps = 1, Tweak = TssDansLdt, Pb = 191,
                      Expect = a => Tr(a, 0x0014), ExpectMateriel = a => Faute(a, Sp, 0x0014) });
        l.Add(new() { Name = "JMP FAR TSS occupée -> #GP(TSS) (PCem : #GP(0))", Code = JmpFar(SelTss2, 0), Steps = 1,
                      Tweak = p => Acces(p, 0x83), Pb = 191,
                      Expect = a => Faute(a, Sp, 0), ExpectMateriel = a => Faute(a, Sp, SelTss2) });
        l.Add(new() { Name = "CALL FAR TSS occupée -> #GP (témoin)", Code = CallFar(SelTss2, 0), Steps = 1,
                      Tweak = p => Acces(p, 0x83), Pb = 191, Expect = a => Faute(a, Sp, SelTss2) });
        // Une TSS 386 (type 9) n'existe pas sur le 286 : #GP ; PCem la lit en TSS 386, trop courte (2Bh) : #TS.
        l.Add(new() { Name = "CALL FAR TSS 386 (type 9) sur le 286 -> #GP (PCem : #TS)", Code = CallFar(SelTss2, 0), Steps = 1,
                      Tweak = p => Acces(p, 0x89), Pb = 191,
                      Expect = a => Faute(a, Sp, SelTss2, VecTs), ExpectMateriel = a => Faute(a, Sp, SelTss2) });
        l.Add(new() { Name = "DIV BX nul, porte 0 de tâche vers la LDT -> #GP, EXT", Code = [0xF7, 0xF3], Steps = 1,
                      Tweak = p => { TssDansLdt(p); p(Idt, Gate(0, 0x0014, 0, 0x85)); }, Pb = 191,
                      Expect = a => Tr(a, 0x0014), ExpectMateriel = a => Faute(a, Sp, 0x0015) });
        l.Add(new() { Name = "INT 1Eh, porte de tâche vers la LDT (0014h) -> #GP", Code = [0xCD, 0x1E], Steps = 1,
                      Tweak = p => { TssDansLdt(p); p(Idt + 0x1E * 8, Gate(0, 0x0014, 0, 0x85)); }, Pb = 191,
                      Expect = a => Tr(a, 0x0014), ExpectMateriel = a => Faute(a, Sp, 0x0014) });
        return l;
    }
}
