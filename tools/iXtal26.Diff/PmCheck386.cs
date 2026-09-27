// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// G2, D5 — LE BANC DU MODE PROTÉGÉ DU 386, pendant de PmCheck (286), qu'il ne touche pas.
//
// Même principe : les deux cœurs sont mis dans le MÊME état protégé par un LOADALL, puis
// avancent pas à pas, état complet, journal d'écritures et RAM comparés après chaque pas.
// Ici c'est LOADALL386 (0F 07, x86_ops_misc.h:930-974), qui lit un bloc de 0xCC octets en
// ES:EDI et pose use32 / stack32 d'après le bit 0x40 de l'octet 6 des attributs.
//
// LES BRANCHES 32 BITS DE loadcscall, pmoderetf, pmodeint ET pmodeiret SONT ÉCRITES DEPUIS
// M20, et n'ont jamais tourné avec is32, stack32 ou use32 non nuls. Ce banc les exerce.
//
// UN VERT PAR ÉGALITÉ NE PROUVE PAS QUE LA BRANCHE 32 BITS A TOURNÉ : un attribut mal posé
// et les deux côtés prendraient ensemble le chemin 16 bits. D'où l'attente `Expect`, écrite
// à la main pour chaque cas et vérifiée sur l'état final de l'ORACLE : use32, stack32, ESP
// à l'octet près, sélecteurs. Une attente fausse est un défaut du banc, jamais un vert.
//
// PG reste nul : la pagination de PCem indexe la RAM sans borne (voir Fuzzer.SeedSys386).

using iXtal26.Cpu;
using iXtal26.Diag;
using iXtal26.Memory;

namespace iXtal26.Diff;

public static class PmCheck386
{
    private const uint Gdt = 0x1000, Ldt = 0x2000, Idt = 0x3000, Tss = 0x4000, Tss2 = 0x4100, Tss286 = 0x4200;
    private const uint CodeBase = 0x20000, DataBase = 0x30000, StackBase = 0x40000;
    private const uint TargetBase = 0x50000, Code3Base = 0x60000, Stack3Base = 0x70000;
    private const uint Bloc = 0x800;

    // D6 — le répertoire et la table de pages vivent DANS DS (base DataBase), pour que le
    // code testé puisse les réécrire : DS:2000 et DS:3000. La table fait l'identité des
    // 4 premiers Mo, U/S, R/W et P posés (7) ; un cas retouche ses entrées par Tweak.
    private const uint Pd = DataBase + 0x2000, Pt = DataBase + 0x3000;
    private static uint PteDe(uint lineaire) => Pt + (lineaire >> 12) * 4;

    // D7 — L'ENTRÉE EN V86 EST UN VRAI IRETD depuis l'anneau 0 (pmodeiret, x86seg.c:2087-2141) :
    // c'est lui qui charge les six segments à la façon V86, et non LOADALL386. Le code V86
    // vit en 2100:0010 (linéaire CodeBase + 0x1010), juste après l'IRETD ; sa pile en
    // 4000:E000 (StackBase + 0xE000) ; DS = ES = 3000 (DataBase).
    private const ushort V86Cs = 0x2100, V86Ss = 0x4000, V86Ds = 0x3000;
    private const uint V86Ip = 0x10, V86Sp = 0xE000;
    private static byte[] AvecEntreeV86(byte[] code)
    {
        var b = new byte[V86Ip + code.Length];
        Array.Fill(b, (byte)0x90);
        b[0] = 0xCF;                                             // IRETD (code 32 bits)
        Array.Copy(code, 0, b, V86Ip, code.Length);
        return b;
    }
    private static uint[] CadreV86(int iopl) =>
        [V86Ip, V86Cs, 0x00020002u | ((uint)iopl << 12), V86Sp, V86Ss, V86Ds, V86Ds, 0x5000, 0x6000];
    // FS ET GS NON NULS À DESSEIN : nuls, la remise à zéro que pmodeint leur fait en quittant
    // le V86 (x86seg.c) serait invisible — mesuré, le contrôle négatif passait vert.
    private const int GdtEntries = 23;

    // Sélecteurs. « 32 » : bit D/B posé dans l'octet 6.
    private const ushort SelCode16 = 0x08, SelData = 0x10, SelStack16 = 0x18, SelTarget32 = 0x20,
        SelConform32 = 0x28, SelCode3_32 = 0x33, SelStack3_32 = 0x3B, SelGate3 = 0x43, SelGate0 = 0x48,
        SelTss = 0x50, SelNotPresent = 0x58, SelDataAsCode = 0x60, SelLdtDesc = 0x68, SelTaskGate = 0x70,
        SelTss2 = 0x78, SelStack32 = 0x80, SelCode32 = 0x88, SelDataG = 0x90, SelTarget16 = 0x98,
        SelCode3_16 = 0xA3, SelStack3_16 = 0xAB, SelTss286 = 0xB0;

    private const uint Esp0 = 0x8000;                // ESP0 de la TSS 386
    private const uint EspDepart = 0xFF60;           // sous les paramètres

    private enum Depart { Ring0_16, Ring0_32, Ring3_16, Ring3_32 }

    private sealed class Case
    {
        public string Name = "";
        public Depart Start;
        public bool Stack32In16;                       // Ring0_16 avec SS 32 bits
        public byte[] Code = [];
        public int GateDwords;
        public uint Eax, Ebx;
        public ushort Flags = 0x0002;
        public int Steps = 1;                          // pas APRÈS LOADALL386
        public uint[]? StackDwords;
        public Action<Action<uint, byte[]>>? Tweak;
        public Func<HState, string?>? Expect;          // sur l'état final de l'oracle
        public uint[]? Regs;                           // fuzzeur : EAX..EDI, ordre de cpu_state
        public bool Fuzz;                              // fuzzeur : la TSS de TR est occupée
        public bool Paging;                            // D6 : PG posé, carte identité des 4 premiers Mo
        public int V86 = -1;                           // D7 : IOPL d'entrée en V86 (par IRETD), -1 sinon
    }

    public static int Run(int only = -1)
    {
        Oracle.CheckAbi();
        var cases = Cases();
        if (only >= 0)
        {
            Console.WriteLine(RunCase(cases[only]));
            return 0;
        }
        int vert = 0, rouge = 0, arret = 0;
        Console.WriteLine($"pm-check --core 386 — {cases.Count} cas, état construit par LOADALL386, un processus par cas\n");
        var self = Environment.ProcessPath!;
        var dll = typeof(PmCheck386).Assembly.Location;
        for (var k = 0; k < cases.Count; k++)
        {
            var psi = new System.Diagnostics.ProcessStartInfo(self)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            if (Path.GetFileNameWithoutExtension(self) == "dotnet")
                psi.ArgumentList.Add(dll);
            foreach (var a in new[] { "pm-check", "--core", "386", "--case", k.ToString() })
                psi.ArgumentList.Add(a);
            using var proc = System.Diagnostics.Process.Start(psi)!;
            var outp = proc.StandardOutput.ReadToEnd();
            proc.StandardError.ReadToEnd();
            proc.WaitForExit();
            var r = outp.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim()
                    ?? $"ARRÊT : aucune sortie (code {proc.ExitCode})";
            if (r.StartsWith("vert", StringComparison.Ordinal)) vert++;
            else if (r.StartsWith("ARRÊT", StringComparison.Ordinal)) arret++;
            else rouge++;
            Console.WriteLine($"  {cases[k].Name,-58} {r}");
        }
        Console.WriteLine($"\n{vert} vert(s), {rouge} rouge(s), {arret} arrêt(s) du C# sur {cases.Count}.");
        return rouge + arret == 0 ? 0 : 1;
    }

    /// <summary>Descripteur complet : l'octet 6 porte G (0x80), D/B (0x40) et les bits 19:16
    /// de la limite.</summary>
    private static byte[] Desc(uint limit, uint @base, byte access, byte flags = 0) =>
        [(byte)limit, (byte)(limit >> 8), (byte)@base, (byte)(@base >> 8), (byte)(@base >> 16), access,
         (byte)(flags | ((limit >> 16) & 0xF)), (byte)(@base >> 24)];

    private static byte[] Gate(uint off, ushort sel, int count, byte access) =>
        [(byte)off, (byte)(off >> 8), (byte)sel, (byte)(sel >> 8), (byte)(count & 31), access,
         (byte)(off >> 16), (byte)(off >> 24)];

    private static byte[] W(params ushort[] w)
    {
        var b = new byte[w.Length * 2];
        for (var i = 0; i < w.Length; i++) { b[2 * i] = (byte)w[i]; b[2 * i + 1] = (byte)(w[i] >> 8); }
        return b;
    }

    private static byte[] L(params uint[] d)
    {
        var b = new byte[d.Length * 4];
        for (var i = 0; i < d.Length; i++)
            for (var k = 0; k < 4; k++) b[4 * i + k] = (byte)(d[i] >> (8 * k));
        return b;
    }

    private static bool Est32(Depart d) => d is Depart.Ring0_32 or Depart.Ring3_32;
    private static bool EstRing3(Depart d) => d is Depart.Ring3_16 or Depart.Ring3_32;

    private static void Build(Case c, Action<uint, byte[]> poke)
    {
        var g = new byte[GdtEntries][];
        g[0x00 >> 3] = new byte[8];
        g[0x08 >> 3] = Desc(0xFFFF, CodeBase, 0x9A);                 // code 16 DPL0
        g[0x10 >> 3] = Desc(0xFFFF, DataBase, 0x92);                 // données
        g[0x18 >> 3] = Desc(0xFFFF, StackBase, 0x92);                // pile 16 DPL0
        g[0x20 >> 3] = Desc(0xFFFF, TargetBase, 0x9A, 0x40);         // code 32 DPL0, cible
        g[0x28 >> 3] = Desc(0xFFFF, TargetBase, 0x9E, 0x40);         // code 32 conforme
        g[0x30 >> 3] = Desc(0xFFFF, Code3Base, 0xFA, 0x40);          // code 32 DPL3
        g[0x38 >> 3] = Desc(0xFFFF, Stack3Base, 0xF2, 0x40);         // pile 32 DPL3
        g[0x40 >> 3] = Gate(0x0100, SelTarget32, c.GateDwords, 0xEC); // porte d'appel 386 DPL3
        g[0x48 >> 3] = Gate(0x0100, SelTarget32, 0, 0x8C);           // porte d'appel 386 DPL0
        // En fuzz, la TSS de TR est OCCUPÉE (0x8B), comme sur un vrai processeur qui l'a
        // chargée : un CALL ou JMP tiré sur elle prend le #GP du `default` de loadcscall.
        g[0x50 >> 3] = Desc(0x67, Tss, (byte)(c.Fuzz ? 0x8B : 0x89)); // TSS 386
        g[0x58 >> 3] = Desc(0xFFFF, TargetBase, 0x1A, 0x40);         // code NON présent
        g[0x60 >> 3] = Desc(0xFFFF, DataBase, 0x92);                 // données (cible de CALL : #GP)
        g[0x68 >> 3] = Desc(0x00FF, Ldt, 0x82);                      // LDT
        g[0x70 >> 3] = Gate(0, SelTss2, 0, 0x85);                    // porte de tâche -> 78
        g[0x78 >> 3] = Desc(0x67, Tss2, 0x89);                       // TSS 386 n°2
        g[0x80 >> 3] = Desc(0xFFFF, StackBase, 0x92, 0x40);          // pile 32 DPL0
        g[0x88 >> 3] = Desc(0xFFFF, CodeBase, 0x9A, 0x40);           // code 32 DPL0, départ
        g[0x90 >> 3] = Desc(0xFFFFF, DataBase, 0x92, 0xC0);          // données, G = 1, 4 Go
        g[0x98 >> 3] = Desc(0xFFFF, TargetBase, 0x9A);               // code 16 DPL0, cible
        g[0xA0 >> 3] = Desc(0xFFFF, Code3Base, 0xFA);                // code 16 DPL3
        g[0xA8 >> 3] = Desc(0xFFFF, Stack3Base, 0xF2);               // pile 16 DPL3
        g[0xB0 >> 3] = Desc(0x2B, Tss286, 0x81);                     // TSS 286 disponible
        for (var i = 0; i < g.Length; i++) poke(Gdt + (uint)(8 * i), g[i]);

        poke(Ldt + 8, Desc(0xFFFF, TargetBase, 0xFA, 0x40));         // LDT idx 1 : code 32 DPL3

        // IDT : portes d'interruption 386 vers 88:0000E000 ; 0x1F en DPL3.
        for (uint v = 0; v < 32; v++)
            poke(Idt + 8 * v, Gate(0xE000, SelCode32, 0, (byte)(v == 0x1F ? 0xEE : 0x8E)));

        // TSS 386 : ESP0 en +4, SS0 en +8 (x86seg.c:1063-1066, `tr.access & 8`).
        poke(Tss, L(0, Esp0, SelStack32));
        // TSS 386 n°2, cible du changement de tâche (x86seg.c:2393-) : EIP +0x20, EFLAGS
        // +0x24, EAX..EDI +0x28, ES +0x48, CS +0x4C, SS +0x50, DS +0x54, FS, GS, LDT +0x60.
        var t2 = new byte[0x68];
        void P(int off, byte[] b) => Array.Copy(b, 0, t2, off, b.Length);
        P(0x04, L(0x7000)); P(0x08, L(SelStack32));
        P(0x20, L(0x0100)); P(0x24, L(0x0002));
        P(0x28, L(0x11111111, 0x22222222, 0x33333333, 0x44444444, 0x7000, 0x55555555, 0x66666666, 0x77777777));
        P(0x48, L(SelData, SelTarget32, SelStack32, SelData, SelData, SelData, SelLdtDesc));
        poke(Tss2, t2);
        // TSS 286 : IP +0x0E, FLAGS +0x10, AX..DI +0x12, ES CS SS DS +0x22, LDT +0x2A.
        poke(Tss286, W(0, 0, 0, 0, 0, 0, 0, 0x0100, 0x0002, 0x1111, 0x2222, 0x3333, 0x4444, 0x7000,
                        0x5555, 0x6666, 0x7777, SelData, SelTarget16, SelStack16, SelData, SelLdtDesc));

        // Pile de départ : paramètres reconnaissables, puis ce que le cas y pose.
        var ring3 = EstRing3(c.Start);
        var stackBase = ring3 ? Stack3Base : StackBase;
        var parms = new uint[32];
        for (var i = 0; i < parms.Length; i++) parms[i] = 0xA0000000u + (uint)i;
        poke(stackBase + EspDepart, L(parms));
        if (c.StackDwords is not null) poke(stackBase + EspDepart, L(c.StackDwords));

        poke((ring3 ? Code3Base : CodeBase) + 0x1000, c.V86 >= 0 ? AvecEntreeV86(c.Code) : c.Code);
        if (c.V86 >= 0)
            poke(stackBase + EspDepart, L(CadreV86(c.V86)));
        // DS:0000 : un pointeur lointain 16:32 ; DS:0010 : des sélecteurs pour les formes
        // mémoire de LAR/LSL/VERR ; DS:0020 : un pseudo-descripteur pour LGDT/LIDT.
        poke(DataBase, L(0x0100, SelTarget32));
        poke(DataBase + 0x10, W(SelDataG));
        poke(DataBase + 0x20, [0xFF, 0x00, 0x00, 0x10, 0x00, 0x00]);

        if (c.Paging)
        {
            poke(Pd, L(Pt | 7));
            var t = new uint[1024];
            for (uint i = 0; i < 1024; i++) t[i] = (i << 12) | 7;
            poke(Pt, L(t));
        }

        c.Tweak?.Invoke(poke);

        // Le bloc de LOADALL386 (x86_ops_misc.h:930-974).
        var b = new byte[0xCC];
        void Bd(int off, uint v) { for (var k = 0; k < 4; k++) b[off + k] = (byte)(v >> (8 * k)); }
        void Bw(int off, ushort v) { b[off] = (byte)v; b[off + 1] = (byte)(v >> 8); }
        void Seg(int off, uint @base, uint limit, byte access, byte segdat3)
        {
            Bd(off, ((uint)access << 8) | ((uint)segdat3 << 16));
            Bd(off + 4, @base);
            Bd(off + 8, limit);
        }
        var cs32 = Est32(c.Start);
        var ss32 = cs32 || c.Stack32In16;
        ushort csSel = c.Start switch
        {
            Depart.Ring0_16 => SelCode16, Depart.Ring0_32 => SelCode32,
            Depart.Ring3_16 => SelCode3_16, _ => SelCode3_32,
        };
        ushort ssSel = ring3 ? (cs32 ? SelStack3_32 : SelStack3_16) : (ss32 ? SelStack32 : SelStack16);
        Bd(0x00, c.Paging ? 0x80000011u : 0x00000011u);         // CR0 : PE, ET ; PG pour D6
        Bw(0x04, c.Flags);                                      // FLAGS
        Bw(0x06, 0x0000);                                       // EFLAGS haut
        Bd(0x08, 0x1000);                                       // EIP
        Bd(0x18, EspDepart);                                    // ESP
        Bd(0x1C, c.Ebx);
        Bd(0x28, c.Eax);
        if (c.Regs is not null)
        {
            // Ordre de cpu_state (EAX ECX EDX EBX ESP EBP ESI EDI) vers celui du bloc ; ESP
            // reste EspDepart, sans quoi la plupart des tirages ne feraient que des #SS.
            int[] off = [0x28, 0x24, 0x20, 0x1C, 0x18, 0x14, 0x10, 0x0C];
            for (var i = 0; i < 8; i++)
                if (i != 4) Bd(off[i], c.Regs[i]);
        }
        Bw(0x34, SelTss);
        Bw(0x38, SelLdtDesc);
        Bw(0x3C, SelData); Bw(0x40, SelData); Bw(0x44, SelData);
        Bw(0x48, ssSel); Bw(0x4C, csSel); Bw(0x50, SelData);
        Seg(0x54, Tss, 0x67, (byte)(c.Fuzz ? 0x8B : 0x89), 0);   // TR : TSS 386
        Seg(0x60, Idt, 32 * 8 - 1, 0, 0);                        // IDTR
        Seg(0x6C, Gdt, GdtEntries * 8 - 1, 0, 0);                // GDTR
        Seg(0x78, Ldt, 0xFF, 0x82, 0);                           // LDTR
        Seg(0x84, DataBase, 0xFFFF, 0x93, 0);                    // GS
        Seg(0x90, DataBase, 0xFFFF, 0x93, 0);                    // FS
        Seg(0x9C, DataBase, 0xFFFF, 0x93, 0);                    // DS
        Seg(0xA8, ring3 ? Stack3Base : StackBase, 0xFFFF, (byte)(ring3 ? 0xF3 : 0x93), (byte)(ss32 ? 0x40 : 0));
        Seg(0xB4, ring3 ? Code3Base : CodeBase, 0xFFFF, (byte)(ring3 ? 0xFB : 0x9B), (byte)(cs32 ? 0x40 : 0));
        Seg(0xC0, DataBase, 0xFFFF, 0x93, 0);                    // ES
        poke(Bloc, b);

        // LOADALL386 ne charge pas CR3 : en D6, deux instructions de mode réel le posent
        // d'abord — MOV EAX,Pd ; MOV CR3,EAX.
        if (c.Paging)
            poke(0x7C00, [0x66, 0xB8, .. L(Pd), 0x0F, 0x22, 0xD8, 0x0F, 0x07]);
        else
            poke(0x7C00, [0x0F, 0x07]);                          // LOADALL386
    }

    private static string RunCase(Case c)
    {
        Oracle.h_set_core(Oracle.Core386);
        Oracle.h_reset();
        Oracle.h_fill_ram(0x90);
        _386.Reset386();
        mem.fill_ram(0x90);
        // CE QUE NI h_reset NI Reset386 NE REMETTENT : abrt, les globales C7a, limit_raw,
        // checked. Le banc s'en passait (un processus par cas) ; le fuzzeur enchaîne les
        // itérations dans un seul. Mesuré : un LIDT registre sous TF laissait abrt = 13
        // (l'INT 1 fautait sur l'IDT chargée), et l'itération suivante livrait ce #GP en
        // mode réel au lieu d'exécuter LOADALL386 — des deux côtés, d'où le contrôle du pas 0.
        Oracle.h_seg_clear_residue();
        _386.ClearSegResidue();

        Build(c, (addr, bytes) =>
        {
            Oracle.h_load(addr, bytes, (uint)bytes.Length);
            for (var i = 0; i < bytes.Length; i++) mem.ram[(addr + i) & mem.rammask] = bytes[i];
        });

        // Mode réel en 0000:7C00, ES:EDI sur le bloc, moitiés hautes nulles.
        var regs = new ushort[(int)R.COUNT];
        regs[(int)R.IP] = 0x7C00;
        regs[(int)R.FLAGS] = 0x0002;
        regs[(int)R.DI] = (ushort)Bloc;
        Oracle.h_setregs(regs);
        _808x.SetRegs(regs);
        var hi = new ushort[8];
        Oracle.h_setregs386(hi, 0, 0, 0);
        _808x.SetRegs386(hi, 0, 0, 0);

        var a = HState.Create();
        var b = HState.Create();
        var pre = c.Paging ? 2 : 0;                             // le préambule de CR3
        var etapes = c.Steps + (c.V86 >= 0 ? 1 : 0);            // l'IRETD d'entrée en V86
        if (Environment.GetEnvironmentVariable("PMFUZZ_TRACE") is not null)
        {
            Oracle.h_getstate(out a);
            Console.WriteLine($"    avant : noint {a.noint} inhlt {a.inhlt} takeint {a.takeint} abrt {a.abrt} cur {a.cur_status:X} " +
                              $"flags {a.flags:X4} eflags {a.eflags:X4} cr0 {a.cr0:X} idt {a.sys_base[(int)Sys.IDT]:X}/{a.sys_limit[(int)Sys.IDT]:X}");
        }
        for (var s = 0; s <= etapes + pre; s++)
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
            if (s == pre)
            {
                // Le banc lui-même : LOADALL386 a-t-il posé l'état voulu ?
                var cs32 = Est32(c.Start);
                if (a.use32 != (cs32 ? 0x300u : 0u) || a.stack32 != (cs32 || c.Stack32In16 ? 1 : 0))
                    return $"BANC FAUX après LOADALL386 : use32 0x{a.use32:X}, stack32 {a.stack32}, CS:EIP {a.seg_sel[(int)Seg.CS]:X4}:{a.pc:X8}, CR0 {a.cr0:X8}, IDT {a.sys_base[(int)Sys.IDT]:X8}/{a.sys_limit[(int)Sys.IDT]:X}, abrt {a.abrt}";
            }
        }
        var attente = c.Expect?.Invoke(a);
        if (attente is not null)
            return $"ATTENTE NON TENUE (oracle) : {attente}";
        return $"vert — CS:EIP {a.seg_sel[(int)Seg.CS]:X4}:{a.pc:X8}, SS:ESP {a.seg_sel[(int)Seg.SS]:X4}:{a.regs[4]:X8}";
    }

    private static string? Att(bool ok, string quoi) => ok ? null : quoi;
    private static uint MemO(uint addr) =>
        (uint)(Oracle.ReadByte(addr) | (Oracle.ReadByte(addr + 1) << 8) | (Oracle.ReadByte(addr + 2) << 16) | (Oracle.ReadByte(addr + 3) << 24));
    private static string? Pile(HState a, ushort ss, uint esp) =>
        Att(a.seg_sel[(int)Seg.SS] == ss && a.regs[4] == esp,
            $"SS:ESP {a.seg_sel[(int)Seg.SS]:X4}:{a.regs[4]:X8}, attendu {ss:X4}:{esp:X8}");
    private static string? Cs(HState a, ushort cs, uint use32) =>
        Att(a.seg_sel[(int)Seg.CS] == cs && a.use32 == use32,
            $"CS {a.seg_sel[(int)Seg.CS]:X4} use32 0x{a.use32:X}, attendu {cs:X4} 0x{use32:X}");

    private static byte[] Imm32(uint v) => [(byte)v, (byte)(v >> 8), (byte)(v >> 16), (byte)(v >> 24)];
    private static byte[] Imm16(ushort v) => [(byte)v, (byte)(v >> 8)];
    private static byte[] Far32(byte op, ushort sel, uint off) => [op, .. Imm32(off), .. Imm16(sel)];
    private static byte[] Far16(byte op, ushort sel, ushort off) => [op, .. Imm16(off), .. Imm16(sel)];

    /// <summary>G2, D5 — LE FUZZEUR EN MODE PROTÉGÉ. Chaque itération construit l'état par
    /// LOADALL386 comme un cas du banc, anneau et taille de code tirés, registres et FLAGS
    /// tirés, NT compris (IRET suit alors le lien arrière de la TSS, taskswitch286), puis
    /// exécute UNE instruction tirée et compare tout.
    ///
    /// PG N'EST JAMAIS POSÉ : `0F 22` (MOV CRx,r) et `0F 07` (LOADALL386) sont réécrits en
    /// `0F 20`, derrière les préfixes. Avec PG, l'oracle tombe (voir Fuzzer.SeedSys386).
    ///
    /// Un seul processus pour toutes les itérations, à la différence du banc : l'état qui
    /// survit à un reset est le même des deux côtés tant qu'ils ne divergent pas, et le
    /// premier arrêt fatal termine la campagne.</summary>
    public static int Fuzz(byte[] opcodes, byte[]? second0F, int iterations, ulong seed)
    {
        Oracle.CheckAbi();
        Console.WriteLine($"Fuzz mode protégé 386 — {iterations} itérations, graine {seed}, " +
                          $"{opcodes.Length} opcodes" + (second0F is null ? "" : $", 0F : {second0F.Length}"));
        var st = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
        uint Next() { st ^= st << 13; st ^= st >> 7; st ^= st << 17; return (uint)(st >> 32); }
        var parDepart = new int[4];
        var parV86 = 0;
        var arrets = 0;
        for (var it = 0; it < iterations; it++)
        {
            var code = new byte[12];
            for (var i = 0; i < code.Length; i++) code[i] = (byte)Next();
            var n = 0;
            switch (Next() & 3)
            {
                case 1: code[n++] = 0x66; break;
                case 2: code[n++] = 0x67; break;
                case 3: code[n++] = 0x66; code[n++] = 0x67; break;
            }
            if (second0F is not null)
            {
                code[n++] = 0x0F;
                code[n] = second0F[Next() % (uint)second0F.Length];
            }
            else
                code[n] = opcodes[Next() % (uint)opcodes.Length];
            SansPagination(code);
            var regs = new uint[8];
            for (var i = 0; i < 8; i++)
                regs[i] = (Next() & 3) == 0 ? (Next() & 0xFF) : Next();
            var v86 = Next() % 5 == 0;
            var depart = v86 ? Depart.Ring0_32 : (Depart)(Next() & 3);
            if (v86) parV86++; else parDepart[(int)depart]++;
            var c = new Case
            {
                Name = $"itération {it}", Start = depart, Code = code, Regs = regs, Fuzz = true,
                V86 = v86 ? (int)(Next() & 3) : -1,
                Flags = (ushort)((Next() & 0x7FD5) | 0x0002),
            };
            if (Environment.GetEnvironmentVariable("PMFUZZ_FROM") is { } fr && it < int.Parse(fr))
                continue;
            var r = RunCase(c);
            if (Environment.GetEnvironmentVariable("PMFUZZ_TRACE") is { } tr && it >= int.Parse(tr))
                Console.WriteLine($"  [{it}] {depart} {string.Join(" ", code.Select(x => x.ToString("X2")))} FL {c.Flags:X4} -> {r}");
            if (r.StartsWith("vert", StringComparison.Ordinal))
                continue;
            Console.WriteLine($"\n{(r.StartsWith("ARRÊT", StringComparison.Ordinal) ? "ARRET" : "DIVERGENCE")} itération {it}, départ {depart}");
            Console.WriteLine($"  octets {string.Join(" ", code.Select(x => x.ToString("X2")))}");
            Console.WriteLine($"  EAX {regs[0]:X8} ECX {regs[1]:X8} EDX {regs[2]:X8} EBX {regs[3]:X8} " +
                              $"EBP {regs[5]:X8} ESI {regs[6]:X8} EDI {regs[7]:X8} FLAGS {c.Flags:X4}");
            Console.WriteLine($"  {r}");
            Console.WriteLine($"\n  Rejouer : pm-fuzz --seed {seed} --iter {it + 1}");
            arrets++;
            return 1;
        }
        Console.WriteLine($"\nVert : {iterations} instructions en mode protégé, zéro divergence " +
                          $"(départs 0/16 {parDepart[0]}, 0/32 {parDepart[1]}, 3/16 {parDepart[2]}, 3/32 {parDepart[3]}, V86 {parV86}).");
        return arrets;
    }

    private static void SansPagination(byte[] code)
    {
        var i = 0;
        while (i < code.Length - 1 && code[i] is 0x26 or 0x2E or 0x36 or 0x3E or 0x64 or 0x65 or 0x66 or 0x67
                                              or 0xF0 or 0xF2 or 0xF3)
            i++;
        if (i < code.Length - 1 && code[i] == 0x0F && code[i + 1] is 0x22 or 0x07)
            code[i + 1] = 0x20;
    }

    private static List<Case> Cases()
    {
        var l = new List<Case>
        {
            // --- l'état de départ et les transferts de même privilège ---
            new() { Name = "NOP en code 32 bits (le banc)", Start = Depart.Ring0_32, Code = [0x90],
                    Expect = a => Cs(a, SelCode32, 0x300) ?? Pile(a, SelStack32, EspDepart) },
            new() { Name = "JMP FAR 16 -> code 32 (loadcsjmp)", Start = Depart.Ring0_16,
                    Code = Far16(0xEA, SelTarget32, 0x0100), Expect = a => Cs(a, SelTarget32, 0x300) },
            new() { Name = "JMP FAR 32 -> code 16 (loadcsjmp)", Start = Depart.Ring0_32,
                    Code = Far32(0xEA, SelTarget16, 0x0100), Expect = a => Cs(a, SelTarget16, 0) },
            new() { Name = "CALL FAR 32 même privilège (loadcscall)", Start = Depart.Ring0_32,
                    Code = Far32(0x9A, SelTarget32, 0x0100),
                    Expect = a => Cs(a, SelTarget32, 0x300) ?? Pile(a, SelStack32, EspDepart - 8) },
            new() { Name = "66 CALL FAR depuis code 16 (loadcscall, op32)", Start = Depart.Ring0_16,
                    Code = [0x66, .. Far32(0x9A, SelTarget32, 0x0100)],
                    Expect = a => Cs(a, SelTarget32, 0x300) ?? Pile(a, SelStack16, EspDepart - 8) },
            new() { Name = "CALL FAR 16 sur pile 32 (stack32, code 16)", Start = Depart.Ring0_16, Stack32In16 = true,
                    Code = Far16(0x9A, SelTarget16, 0x0100),
                    Expect = a => Cs(a, SelTarget16, 0) ?? Pile(a, SelStack32, EspDepart - 4) },
            new() { Name = "CALL FAR 32 conforme", Start = Depart.Ring0_32, Code = Far32(0x9A, SelConform32, 0x0100),
                    Expect = a => Cs(a, SelConform32, 0x300) },
            new() { Name = "CALL FAR 32 m16:32 (FF /3)", Start = Depart.Ring0_32, Code = [0xFF, 0x1D, .. Imm32(0)],
                    Expect = a => Cs(a, SelTarget32, 0x300) ?? Pile(a, SelStack32, EspDepart - 8) },

            // --- pmoderetf ---
            new() { Name = "RETF 32 même privilège", Start = Depart.Ring0_32, Code = [0xCB],
                    StackDwords = [0x0100, SelTarget32],
                    Expect = a => Cs(a, SelTarget32, 0x300) ?? Pile(a, SelStack32, EspDepart + 8) },
            new() { Name = "RETF 32 vers CPL3", Start = Depart.Ring0_32, Code = [0xCB],
                    StackDwords = [0x0100, SelCode3_32, 0xE000, SelStack3_32],
                    Expect = a => Cs(a, SelCode3_32, 0x300) ?? Pile(a, SelStack3_32, 0xE000) },
            new() { Name = "RETF 32 imm 8 vers CPL3", Start = Depart.Ring0_32, Code = [0xCA, 0x08, 0x00],
                    StackDwords = [0x0100, SelCode3_32, 0x1111, 0x2222, 0xE000, SelStack3_32],
                    Expect = a => Pile(a, SelStack3_32, 0xE008) },
            new() { Name = "66 RETF depuis code 16 vers code 32", Start = Depart.Ring0_16, Code = [0x66, 0xCB],
                    StackDwords = [0x0100, SelTarget32], Expect = a => Cs(a, SelTarget32, 0x300) },

            // --- pmodeint : portes d'interruption 386 ---
            new() { Name = "INT 10h, porte 386, même privilège", Start = Depart.Ring0_32, Code = [0xCD, 0x10],
                    Expect = a => Cs(a, SelCode32, 0x300) ?? Pile(a, SelStack32, EspDepart - 12) },
            new() { Name = "INT 1Fh depuis CPL3 (32), bascule de pile 386", Start = Depart.Ring3_32, Code = [0xCD, 0x1F],
                    Expect = a => Cs(a, SelCode32, 0x300) ?? Pile(a, SelStack32, Esp0 - 20) },
            new() { Name = "INT 1Fh depuis CPL3 (16), bascule de pile 386", Start = Depart.Ring3_16, Code = [0xCD, 0x1F],
                    Expect = a => Cs(a, SelCode32, 0x300) ?? Pile(a, SelStack32, Esp0 - 20) },
            new() { Name = "INT 10h depuis CPL3, DPL0 -> #GP", Start = Depart.Ring3_32, Code = [0xCD, 0x10],
                    Expect = a => Pile(a, SelStack32, Esp0 - 24) },

            // --- pmodeiret ---
            new() { Name = "IRETD même privilège", Start = Depart.Ring0_32, Code = [0xCF],
                    StackDwords = [0x0100, SelTarget32, 0x0002],
                    Expect = a => Cs(a, SelTarget32, 0x300) ?? Pile(a, SelStack32, EspDepart + 12) },
            new() { Name = "IRETD vers CPL3", Start = Depart.Ring0_32, Code = [0xCF],
                    StackDwords = [0x0100, SelCode3_32, 0x0202, 0xE000, SelStack3_32],
                    Expect = a => Cs(a, SelCode3_32, 0x300) ?? Pile(a, SelStack3_32, 0xE000) },
            new() { Name = "66 IRET depuis code 16 (IRETD)", Start = Depart.Ring0_16, Code = [0x66, 0xCF],
                    StackDwords = [0x0100, SelTarget32, 0x0002], Expect = a => Cs(a, SelTarget32, 0x300) },

            // --- loadcscall : portes d'appel 386 ---
            new() { Name = "CALL FAR porte 386 même privilège", Start = Depart.Ring0_32, Code = Far32(0x9A, SelGate0, 0),
                    Expect = a => Cs(a, SelTarget32, 0x300) ?? Pile(a, SelStack32, EspDepart - 8) },
            new() { Name = "JMP FAR porte 386 (loadcsjmp)", Start = Depart.Ring0_32, Code = Far32(0xEA, SelGate0, 0),
                    Expect = a => Cs(a, SelTarget32, 0x300) },
        };
        foreach (var n in new[] { 0, 1, 5, 31 })
        {
            var n1 = n;
            l.Add(new()
            {
                Name = $"CALL FAR porte 386 CPL3->0, {n} dword(s)", Start = Depart.Ring3_32, GateDwords = n,
                Code = Far32(0x9A, SelGate3, 0),
                Expect = a => Cs(a, SelTarget32, 0x300) ?? Pile(a, SelStack32, Esp0 - 16 - 4 * (uint)n1),
            });
        }
        l.Add(new() { Name = "CALL FAR porte 386 depuis CPL3 16 bits", Start = Depart.Ring3_16, GateDwords = 2,
                      Code = Far16(0x9A, SelGate3, 0), Expect = a => Pile(a, SelStack32, Esp0 - 24) });

        // --- changement de tâche 386 (taskswitch286) ---
        // Attente commune : TR = TSS n°2, CS:EIP et EAX lus dans elle, TS posé dans CR0.
        static string? Tache(HState a, uint esp, bool nt) =>
            Att(a.sys_sel[(int)Sys.TR] == SelTss2, $"TR {a.sys_sel[(int)Sys.TR]:X4}")
            ?? Cs(a, SelTarget32, 0x300) ?? Pile(a, SelStack32, esp)
            ?? Att(a.regs[0] == 0x11111111, $"EAX 0x{a.regs[0]:X8}")
            ?? Att((a.cr0 & 8) != 0, $"CR0 0x{a.cr0:X8} sans TS")
            ?? Att(((a.flags & 0x4000) != 0) == nt, $"FLAGS 0x{a.flags:X4}, NT attendu {nt}");
        // CALL : ESP 0x6FF8 et non 0x7000. CALL_FAR_l (x86_ops_call.h:51-76) empile l'adresse
        // de retour APRÈS loadcscall, donc sur la pile de la NOUVELLE tâche. Le 386 ne le
        // fait pas ; PCem si, et le C# le reproduit.
        l.Add(new() { Name = "CALL FAR TSS 386 (taskswitch286)", Start = Depart.Ring0_32, Code = Far32(0x9A, SelTss2, 0),
                      Expect = a => Tache(a, 0x6FF8, true) });
        // TSS 286 depuis un 386 : la branche 16 bits, et son `| 0xFFFF0000` (x86seg.c:2800).
        l.Add(new() { Name = "JMP FAR TSS 286 depuis un 386 (moitiés hautes à FFFF)", Start = Depart.Ring0_32,
                      Code = Far32(0xEA, SelTss286, 0),
                      Expect = a => Att(a.sys_sel[(int)Sys.TR] == SelTss286, $"TR {a.sys_sel[(int)Sys.TR]:X4}")
                                    ?? Cs(a, SelTarget16, 0) ?? Pile(a, SelStack16, 0xFFFF7000)
                                    ?? Att(a.regs[0] == 0xFFFF1111, $"EAX 0x{a.regs[0]:X8}")
                                    ?? Att(a.seg_sel[(int)Seg.FS] == 0 && a.seg_sel[(int)Seg.GS] == 0, "FS/GS non nuls") });
        l.Add(new() { Name = "JMP FAR TSS 386 (taskswitch286)", Start = Depart.Ring0_32, Code = Far32(0xEA, SelTss2, 0),
                      Expect = a => Tache(a, 0x7000, false) });
        // PCem NE CHANGE PAS DE TÂCHE PAR UN CALL SUR PORTE DE TÂCHE : loadcscall aiguille les
        // types 1 et 9 (TSS disponibles, commentés « Task gate » à x86seg.c:1284-1285) vers
        // taskswitch286, et le vrai type 5 tombe dans `default` — #GP. Transcrit tel quel.
        l.Add(new() { Name = "CALL FAR porte de tâche -> #GP (PCem : type 5 non géré)", Start = Depart.Ring0_32,
                      Code = Far32(0x9A, SelTaskGate, 0), Expect = a => Pile(a, SelStack32, EspDepart - 16) });
        l.Add(new() { Name = "INT 1Eh par porte de tâche -> TSS 386 (pmodeint)", Start = Depart.Ring0_32, Code = [0xCD, 0x1E],
                      Tweak = p => p(Idt + 0x1E * 8, Gate(0, SelTss2, 0, 0x85)), Expect = a => Tache(a, 0x7000, true) });
        l.Add(new() { Name = "IRETD avec NT -> lien arrière (pmodeiret)", Start = Depart.Ring0_32, Code = [0xCF],
                      Flags = 0x4002, Tweak = p => p(Tss, W(SelTss2)), Expect = a => Tache(a, 0x7000, false) });

        // --- D4, angles morts : MOV CRx/DRx/TRx depuis CPL3 -> #GP ---
        foreach (var (code, what) in new (byte[], string)[]
                 {
                     ([0x0F, 0x20, 0xC0], "MOV EAX,CR0"), ([0x0F, 0x22, 0xC0], "MOV CR0,EAX"),
                     ([0x0F, 0x21, 0xC0], "MOV EAX,DR0"), ([0x0F, 0x23, 0xF8], "MOV DR7,EAX"),
                     ([0x0F, 0x24, 0xF0], "MOV EAX,TR6"), ([0x0F, 0x26, 0xF8], "MOV TR7,EAX"),
                 })
            l.Add(new() { Name = $"{what} depuis CPL3 -> #GP", Start = Depart.Ring3_32, Code = code,
                          Expect = a => Pile(a, SelStack32, Esp0 - 24) });

        // --- D4 : lectures après écriture, CPL0 (CR2 et DR0 que le fuzzeur ne voit qu'à zéro) ---
        l.Add(new() { Name = "MOV DR0,EAX ; MOV EBX,DR0", Start = Depart.Ring0_32, Eax = 0x12345678,
                      Code = [0x0F, 0x23, 0xC0, 0x0F, 0x21, 0xC3], Steps = 2,
                      Expect = a => Att(a.regs[3] == 0x12345678 && a.dr[0] == 0x12345678, $"EBX 0x{a.regs[3]:X8}") });
        l.Add(new() { Name = "MOV CR2,EAX ; MOV EBX,CR2", Start = Depart.Ring0_32, Eax = 0xDEADBEEF,
                      Code = [0x0F, 0x22, 0xD0, 0x0F, 0x20, 0xD3], Steps = 2,
                      Expect = a => Att(a.regs[3] == 0xDEADBEEF && a.cr2 == 0xDEADBEEF, $"EBX 0x{a.regs[3]:X8}") });
        l.Add(new() { Name = "MOV CR3,EAX en mode protégé (flushmmucache)", Start = Depart.Ring0_32, Eax = 0x00123000,
                      Code = [0x0F, 0x22, 0xD8], Expect = a => Att(a.cr3 == 0x00123000, $"CR3 0x{a.cr3:X8}") });
        l.Add(new() { Name = "MOV EAX,CR0 en mode protégé", Start = Depart.Ring0_32, Code = [0x0F, 0x20, 0xC0],
                      Expect = a => Att(a.regs[0] == 0x11, $"EAX 0x{a.regs[0]:X8}") });
        l.Add(new() { Name = "MOV CR0,EAX : sortie du mode protégé", Start = Depart.Ring0_32, Eax = 0x10,
                      Code = [0x0F, 0x22, 0xC0], Expect = a => Att((a.cr0 & 1) == 0, $"CR0 0x{a.cr0:X8}") });

        // --- D4 : LAR/LSL 32 bits (0F 02 C3 / 0F 03 C3 en code 32 : LAR EAX,EBX) ---
        foreach (var (sel, what) in new (ushort, string)[]
                 { (SelCode32, "code 32"), (SelDataG, "données G=1"), (SelGate3, "porte 386"), (SelTss, "TSS 386"),
                   (SelNotPresent, "absent"), (SelLdtDesc, "LDT"), (0, "nul"), (0x0400, "hors GDT"), (0x000F, "LDT idx 1") })
        {
            l.Add(new() { Name = $"LAR EAX,EBX — {what} ({sel:X4})", Start = Depart.Ring0_32, Ebx = sel, Code = [0x0F, 0x02, 0xC3] });
            l.Add(new() { Name = $"LSL EAX,EBX — {what} ({sel:X4})", Start = Depart.Ring0_32, Ebx = sel, Code = [0x0F, 0x03, 0xC3] });
        }
        l.Add(new() { Name = "LSL EAX,EBX — G=1 : 0xFFFFFFFF", Start = Depart.Ring0_32, Ebx = SelDataG, Code = [0x0F, 0x03, 0xC3],
                      Expect = a => Att(a.regs[0] == 0xFFFFFFFF, $"EAX 0x{a.regs[0]:X8}") });
        l.Add(new() { Name = "LAR EAX,EBX — octet 6 gardé (0x00CF9200)", Start = Depart.Ring0_32, Ebx = SelDataG, Code = [0x0F, 0x02, 0xC3],
                      Expect = a => Att(a.regs[0] == 0x00CF9200, $"EAX 0x{a.regs[0]:X8}") });
        l.Add(new() { Name = "66 LAR EAX,BX depuis code 16", Start = Depart.Ring0_16, Ebx = SelDataG, Code = [0x66, 0x0F, 0x02, 0xC3],
                      Expect = a => Att(a.regs[0] == 0x00CF9200, $"EAX 0x{a.regs[0]:X8}") });

        // --- D4 : formes a32 à opérande mémoire (67 en code 16 ; [EBX] = DS:0010) ---
        l.Add(new() { Name = "67 LAR AX,[EBX]", Start = Depart.Ring0_16, Ebx = 0x10, Code = [0x67, 0x0F, 0x02, 0x03] });
        l.Add(new() { Name = "67 LSL AX,[EBX]", Start = Depart.Ring0_16, Ebx = 0x10, Code = [0x67, 0x0F, 0x03, 0x03] });
        l.Add(new() { Name = "67 66 LSL EAX,[EBX]", Start = Depart.Ring0_16, Ebx = 0x10, Code = [0x67, 0x66, 0x0F, 0x03, 0x03],
                      Expect = a => Att(a.regs[0] == 0xFFFFFFFF, $"EAX 0x{a.regs[0]:X8}") });
        l.Add(new() { Name = "67 STR [EBX] (0F 00 a32)", Start = Depart.Ring0_16, Ebx = 0x40, Code = [0x67, 0x0F, 0x00, 0x0B] });
        l.Add(new() { Name = "67 VERR [EBX] (0F 00 a32)", Start = Depart.Ring0_16, Ebx = 0x10, Code = [0x67, 0x0F, 0x00, 0x23] });
        l.Add(new() { Name = "SLDT [EBX] en code 32", Start = Depart.Ring0_32, Ebx = 0x40, Code = [0x0F, 0x00, 0x03] });
        l.Add(new() { Name = "LLDT AX en code 32", Start = Depart.Ring0_32, Eax = SelLdtDesc, Code = [0x0F, 0x00, 0xD0] });

        // --- D4 : 0F 01 en formes 32 bits ---
        l.Add(new() { Name = "SGDT [EBX] en code 32 (0F 01 l_a32)", Start = Depart.Ring0_32, Ebx = 0x40, Code = [0x0F, 0x01, 0x03] });
        l.Add(new() { Name = "66 SIDT [EBX] en code 32 (w_a32)", Start = Depart.Ring0_32, Ebx = 0x40, Code = [0x66, 0x0F, 0x01, 0x0B] });
        l.Add(new() { Name = "LGDT [EBX] en code 32 : base 32 bits", Start = Depart.Ring0_32, Ebx = 0x20, Code = [0x0F, 0x01, 0x13],
                      Tweak = p => p(DataBase + 0x20, [0xFF, 0x00, 0x00, 0x10, 0x00, 0x01]),
                      Expect = a => Att(a.sys_base[(int)Sys.GDT] == 0x01001000, $"GDT base 0x{a.sys_base[(int)Sys.GDT]:X8}") });
        l.Add(new() { Name = "66 LGDT [EBX] : base tronquée à 24 bits", Start = Depart.Ring0_32, Ebx = 0x20, Code = [0x66, 0x0F, 0x01, 0x13],
                      Tweak = p => p(DataBase + 0x20, [0xFF, 0x00, 0x00, 0x10, 0x00, 0x01]),
                      Expect = a => Att(a.sys_base[(int)Sys.GDT] == 0x00001000, $"GDT base 0x{a.sys_base[(int)Sys.GDT]:X8}") });
        // --- D6 : la pagination, par instructions. DS:5000 = linéaire 0x35000, DS:6000 = 0x36000.
        static byte[] Dw(uint v) => [(byte)v, (byte)(v >> 8), (byte)(v >> 16), (byte)(v >> 24)];
        void Donnees(Action<uint, byte[]> p) { p(0x35000, Dw(0x11112222)); p(0x36000, Dw(0x33334444)); }
        l.Add(new() { Name = "PG : lecture, bits A posés dans le PDE et le PTE", Start = Depart.Ring0_32, Paging = true, Ebx = 0x5000,
                      Code = [0x8B, 0x03], Tweak = Donnees,
                      Expect = a => Att(a.regs[0] == 0x11112222, $"EAX 0x{a.regs[0]:X8}")
                                    ?? Att((MemO(PteDe(0x35000)) & 0x60) == 0x20, $"PTE 0x{MemO(PteDe(0x35000)):X8}")
                                    ?? Att((MemO(Pd) & 0x20) != 0, $"PDE 0x{MemO(Pd):X8}") });
        l.Add(new() { Name = "PG : écriture, bit D posé", Start = Depart.Ring0_32, Paging = true, Ebx = 0x5000, Eax = 0xCAFEBABE,
                      Code = [0x89, 0x03], Tweak = Donnees,
                      Expect = a => Att((MemO(PteDe(0x35000)) & 0x60) == 0x60, $"PTE 0x{MemO(PteDe(0x35000)):X8}")
                                    ?? Att(MemO(0x35000) == 0xCAFEBABE, "donnée non écrite") });
        l.Add(new() { Name = "PG : page absente -> #PF, cr2 et code d'erreur", Start = Depart.Ring0_32, Paging = true, Ebx = 0x5000,
                      Code = [0x8B, 0x03], Tweak = p => p(PteDe(0x35000), Dw(0x35006)),
                      Expect = a => Att(a.cr2 == 0x35000, $"cr2 0x{a.cr2:X8}") ?? Cs(a, SelCode32, 0x300)
                                    ?? Pile(a, SelStack32, EspDepart - 16) ?? Att(MemO(StackBase + EspDepart - 16) == 0, "code d'erreur") });
        l.Add(new() { Name = "PG : dword à cheval, seconde page absente -> #PF", Start = Depart.Ring0_32, Paging = true, Ebx = 0x5FFE,
                      Code = [0x8B, 0x03], Tweak = p => p(PteDe(0x36000), Dw(0x36006)),
                      Expect = a => Att(a.cr2 == 0x36001, $"cr2 0x{a.cr2:X8}") ?? Pile(a, SelStack32, EspDepart - 16) });
        l.Add(new() { Name = "PG : mot à cheval, écriture, les deux pages", Start = Depart.Ring0_32, Paging = true, Ebx = 0x5FFF, Eax = 0xBEEF,
                      Code = [0x66, 0x89, 0x03], Tweak = Donnees,
                      Expect = a => Att((MemO(PteDe(0x35000)) & 0x40) != 0 && (MemO(PteDe(0x36000)) & 0x40) != 0, "bits D") });
        l.Add(new() { Name = "PG : CPL3 sur une page superviseur -> #PF 5", Start = Depart.Ring3_32, Paging = true, Ebx = 0x5000,
                      Code = [0x8B, 0x03], Tweak = p => p(PteDe(0x35000), Dw(0x35003)),
                      Expect = a => Att(a.cr2 == 0x35000, $"cr2 0x{a.cr2:X8}") ?? Pile(a, SelStack32, Esp0 - 24)
                                    ?? Att(MemO(StackBase + Esp0 - 24) == 5, $"code d'erreur {MemO(StackBase + Esp0 - 24)}") });
        l.Add(new() { Name = "PG : CPL3 écrit une page en lecture seule -> #PF 7", Start = Depart.Ring3_32, Paging = true, Ebx = 0x5000,
                      Code = [0x89, 0x03], Tweak = p => p(PteDe(0x35000), Dw(0x35005)),
                      Expect = a => Att(MemO(StackBase + Esp0 - 24) == 7, $"code d'erreur {MemO(StackBase + Esp0 - 24)}") });
        l.Add(new() { Name = "PG : CPL0 écrit une page en lecture seule (WP nul) — permis", Start = Depart.Ring0_32, Paging = true, Ebx = 0x5000,
                      Eax = 0x0BADF00D, Code = [0x89, 0x03], Tweak = p => p(PteDe(0x35000), Dw(0x35005)),
                      Expect = a => Att(MemO(0x35000) == 0x0BADF00D, "donnée non écrite") });
        // LE CACHE DE TRADUCTION : un PTE réécrit SANS vidage garde l'ancienne traduction
        // (readlookup2), jusqu'au MOV CR3 qui vide. MOV EAX,[EBX] ; MOV [PTE],0x36007 ;
        // MOV ECX,[EBX] (ancien) ; MOV CR3,EDX ; MOV EDX,[EBX] (nouveau).
        l.Add(new() { Name = "PG : PTE réécrit sans vidage, puis MOV CR3", Start = Depart.Ring0_32, Paging = true, Ebx = 0x5000,
                      Code = [0x8B, 0x03, 0xC7, 0x05, .. Dw(0x3000 + 0x35 * 4), .. Dw(0x36007), 0x8B, 0x0B,
                              0xBA, .. Dw(Pd), 0x0F, 0x22, 0xDA, 0x8B, 0x13],
                      Steps = 6, Tweak = Donnees,
                      Expect = a => Att(a.regs[1] == 0x11112222, $"ECX 0x{a.regs[1]:X8} (traduction en cache attendue)")
                                    ?? Att(a.regs[2] == 0x33334444, $"EDX 0x{a.regs[2]:X8} (nouvelle traduction attendue)") });
        l.Add(new() { Name = "PG : fetch à cheval vers une page absente -> #PF", Start = Depart.Ring0_32, Paging = true,
                      Code = [0xE9, .. Dw(0x1FFE - 0x1005)],        // JMP vers 88:1FFE, linéaire 0x21FFE
                      Steps = 2, Tweak = p => { p(0x21FFE, [0xB8, 0x78]); p(PteDe(0x22000), Dw(0x22006)); },
                      Expect = a => Att(a.cr2 >= 0x22000 && a.cr2 <= 0x22003, $"cr2 0x{a.cr2:X8}") ?? Cs(a, SelCode32, 0x300) });
        l.Add(new() { Name = "PG : MOV CR0 efface PG, puis lecture physique", Start = Depart.Ring0_32, Paging = true, Ebx = 0x5000,
                      Eax = 0x11, Code = [0x0F, 0x22, 0xC0, 0x8B, 0x0B], Steps = 2, Tweak = Donnees,
                      Expect = a => Att((a.cr0 >> 31) == 0 && a.regs[1] == 0x11112222, $"CR0 0x{a.cr0:X8}, ECX 0x{a.regs[1]:X8}") });

        // --- D7 : le mode virtuel 8086. Entrée par IRETD (pas 1), puis le code testé.
        static string? EnV86(HState a) =>
            Att((a.eflags & 2) != 0, $"EFLAGS haut 0x{a.eflags:X4}, VM attendu")
            ?? Att((a.seg_access[(int)Seg.CS] >> 5 & 3) == 3, "CPL attendu 3");
        static string? FauteDepuisV86(HState a, uint poussé) =>
            Cs(a, SelCode32, 0x300) ?? Pile(a, SelStack32, Esp0 - poussé)
            ?? Att((a.eflags & 2) == 0, "VM encore posé")
            ?? Att(a.seg_sel[(int)Seg.DS] == 0 && a.seg_sel[(int)Seg.ES] == 0 && a.seg_sel[(int)Seg.FS] == 0
                   && a.seg_sel[(int)Seg.GS] == 0, "DS/ES/FS/GS non remis à zéro");
        l.Add(new() { Name = "V86 : IRETD d'entrée (segments V86, stack32 à 0)", Start = Depart.Ring0_32, V86 = 3, Code = [0x90],
                      Expect = a => EnV86(a) ?? Cs(a, V86Cs, 0) ?? Pile(a, V86Ss, V86Sp)
                                    ?? Att(a.seg_base[(int)Seg.GS] == 0x60000, $"base GS 0x{a.seg_base[(int)Seg.GS]:X8}")
                                    ?? Att(a.stack32 == 0, $"stack32 {a.stack32} (x86seg.c:428-429)")
                                    ?? Att(a.seg_base[(int)Seg.CS] == 0x21000, $"base CS 0x{a.seg_base[(int)Seg.CS]:X8}") });
        l.Add(new() { Name = "V86 : MOV AX,1234 ; MOV DS,AX", Start = Depart.Ring0_32, V86 = 3, Code = [0xB8, 0x34, 0x12, 0x8E, 0xD8], Steps = 2,
                      Expect = a => EnV86(a) ?? Att(a.seg_base[(int)Seg.DS] == 0x12340, $"base DS 0x{a.seg_base[(int)Seg.DS]:X8}") });
        l.Add(new() { Name = "V86 : INT 1Fh, IOPL 3 -> anneau 0 (9 dwords)", Start = Depart.Ring0_32, V86 = 3, Code = [0xCD, 0x1F],
                      Expect = a => FauteDepuisV86(a, 36) });
        l.Add(new() { Name = "V86 : INT 1Fh, IOPL 0 -> #GP (10 dwords)", Start = Depart.Ring0_32, V86 = 0, Code = [0xCD, 0x1F],
                      Expect = a => FauteDepuisV86(a, 40) });
        l.Add(new() { Name = "V86 : INT 1Fh puis IRETD de l'anneau 0 vers V86", Start = Depart.Ring0_32, V86 = 3, Code = [0xCD, 0x1F], Steps = 2,
                      Tweak = p => p(CodeBase + 0xE000, [0xCF]),
                      Expect = a => EnV86(a) ?? Cs(a, V86Cs, 0) ?? Att(a.pc == V86Ip + 2, $"IP 0x{a.pc:X}")
                                    ?? Att(a.seg_sel[(int)Seg.DS] == V86Ds, $"DS {a.seg_sel[(int)Seg.DS]:X4}") });
        foreach (var (code, quoi) in new (byte[], string)[]
                 { ([0xFA], "CLI"), ([0xFB], "STI"), ([0x9C], "PUSHF"), ([0x9D], "POPF"), ([0x66, 0x9D], "POPFD"),
                   ([0x66, 0x9C], "PUSHFD"), ([0xCC], "INT3"), ([0xCE], "INTO") })
            l.Add(new() { Name = $"V86 : {quoi}, IOPL 0 -> #GP", Start = Depart.Ring0_32, V86 = 0, Code = code,
                          Expect = a => FauteDepuisV86(a, 40) });
        l.Add(new() { Name = "V86 : CLI, IOPL 3 -> permis", Start = Depart.Ring0_32, V86 = 3, Code = [0xFA],
                      Expect = a => EnV86(a) ?? Att((a.flags & 0x200) == 0, "IF encore posé") });
        l.Add(new() { Name = "V86 : PUSHF, IOPL 3 -> SP - 2", Start = Depart.Ring0_32, V86 = 3, Code = [0x9C],
                      Expect = a => EnV86(a) ?? Pile(a, V86Ss, V86Sp - 2) });
        l.Add(new() { Name = "V86 : IN AL,DX, bitmap d'E/S hors limite -> #GP", Start = Depart.Ring0_32, V86 = 3, Code = [0xEC],
                      Expect = a => FauteDepuisV86(a, 40) });
        l.Add(new() { Name = "V86 : HLT -> #GP", Start = Depart.Ring0_32, V86 = 3, Code = [0xF4], Expect = a => FauteDepuisV86(a, 40) });
        l.Add(new() { Name = "V86 : MOV EAX,CR0 -> #GP", Start = Depart.Ring0_32, V86 = 3, Code = [0x0F, 0x20, 0xC0],
                      Expect = a => FauteDepuisV86(a, 40) });
        l.Add(new() { Name = "V86 : LAR -> INT 6 (NOTRM)", Start = Depart.Ring0_32, V86 = 3, Code = [0x0F, 0x02, 0xC3],
                      Expect = a => FauteDepuisV86(a, 36) });

        l.Add(new() { Name = "SMSW AX en mode protégé (386 : | 0xFF00)", Start = Depart.Ring0_32, Code = [0x66, 0x0F, 0x01, 0xE0],
                      Expect = a => Att((a.regs[0] & 0xFFFF) == 0xFF11, $"AX 0x{a.regs[0] & 0xFFFF:X4}") });
        return l;
    }
}
