// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// Les cas qui discriminent les corrections des mailbox de l'AHA-1542C (G13.7e1b, PLAN-G13.md), en C# seul : PB-141
// (01h et 02h, le tourniquet, le MBI libre, HRST et SRST) et PB-139 (ABORT). Sur le banc de G13.7d2a (AvecScsi), une machine
// neuve par situation : une mailbox de n emplacements en 80100h (les MBO, puis les MBI ; la mémoire de R9Aha — 84000h,
// elle, est écrite par le BIOS qui attend « Press F1 »), des CCB TEST UNIT READY vers
// l'ID 1 (le disque d'AvecScsi), la sense automatique coupée, en 80000h + 40h × k. La carte conduite par ses ports.

using iXtal26.Memory;

namespace iXtal26.Diff;

internal static partial class MaterielCas
{
    private const uint Mb = 0x80100;

    private static uint CcbK(int k) => 0x80000 + (uint)(k * 0x40);

    /// <summary>AvecScsi, puis 600 tranches : le POST efface la mémoire basse entre la 400e et la 450e tranche de la
    /// machine (mesuré), puis attend « Press F1 » sans plus y toucher — la mailbox doit être écrite après.</summary>
    private static int AvecMb(Func<int> corps) => AvecScsi(() =>
    {
        Tourner(600);
        // La mailbox doit tenir : une marque écrite, 100 tranches, relue — sinon le POST y touche encore, le banc est faux.
        mem.mem_writeb_phys(Mb, 0xA5);
        Tourner(100);
        if (mem.mem_readb_phys(Mb) != 0xA5)
            throw new InvalidOperationException("le POST écrit encore la mémoire de la mailbox après 600 tranches");
        mem.mem_writeb_phys(Mb, 0);
        return corps();
    });

    private static void Tourner(int n)
    {
        for (var i = 0; i < n; i++)
            pc.runpc();
    }

    /// <summary>La mailbox de n emplacements vidée, puis Mailbox Initialization ; rend l'état relevé à HACC.</summary>
    private static byte InitMb(int n)
    {
        for (var i = 0; i < 8 * Math.Max(n, 1); i++)
            mem.mem_writeb_phys(Mb + (uint)i, 0);
        R9Aha.Cmd(0x01, n, (int)(Mb >> 16) & 0xFF, (int)(Mb >> 8) & 0xFF, (int)Mb & 0xFF);
        return R9Aha.Hacc();
    }

    /// <summary>Le CCB k : TEST UNIT READY vers l'ID 1, sans sense automatique.</summary>
    private static void PoserCcb(int k)
    {
        var c = R9Aha.CcbDe(0, 1, new byte[6], 0);
        c[3] = 1;
        for (var i = 0; i < c.Length; i++)
            mem.mem_writeb_phys(CcbK(k) + (uint)i, c[i]);
    }

    private static void Mbo(int i, int code, uint ccb)
    {
        mem.mem_writeb_phys(Mb + (uint)(i * 4) + 1, (byte)(ccb >> 16));
        mem.mem_writeb_phys(Mb + (uint)(i * 4) + 2, (byte)(ccb >> 8));
        mem.mem_writeb_phys(Mb + (uint)(i * 4) + 3, (byte)ccb);
        mem.mem_writeb_phys(Mb + (uint)(i * 4), (byte)code);
    }

    private static byte MboCode(int i) => mem.mem_readb_phys(Mb + (uint)(i * 4));

    /// <summary>Le MBI i d'une mailbox de n emplacements : son code et son pointeur.</summary>
    private static (byte code, uint ccb) Mbi(int n, int i)
    {
        var a = Mb + (uint)(n * 4 + i * 4);
        return (mem.mem_readb_phys(a),
                (uint)(mem.mem_readb_phys(a + 1) << 16 | mem.mem_readb_phys(a + 2) << 8 | mem.mem_readb_phys(a + 3)));
    }

    private static string Mbis(int n) => string.Join(" ", Enumerable.Range(0, n).Select(i => Mbi(n, i)).Select(m => m.code == 0 ? "--" : $"{m.code:X2}:{m.ccb:X}"));

    /// <summary>Start SCSI Command (02h), puis `tranches` tranches ; rend HACC et l'état relevé, IRST ensuite.</summary>
    private static (bool hacc, byte etat) Start(int tranches = 400)
    {
        R9Aha.Cmd(0x02);
        var hacc = false;
        byte etat = 0;
        for (var i = 0; i < tranches; i++)
        {
            pc.runpc();
            if (!hacc && (R9Aha.Isr() & HACC) != 0)
            {
                hacc = true;
                etat = R9Aha.Etat();
            }
        }
        iXtal26.io.outb(R9Aha.P, 0x20);
        return (hacc, etat);
    }

    // PB-141 — les mailbox (AHA-1540C TR, chapitre 5 : 01h, 02h, MBO, MBI ; chapitre 4 : HRST). 01h de compte 0 :
    // HACC seul chez PCem, INVDCMD et HACC sur la carte. Deux CCB postés (MBO 0 et 1), un seul 02h : PCem en lance un,
    // la carte les deux. Le MBI 0 encore occupé (01h, 0ABCDEh) : PCem l'écrase, la carte rend l'achèvement au MBI 1 ;
    // l'unique MBI occupé : la carte attend, le MBO reste plein, puis rend l'achèvement quand l'hôte le libère. HRST (puis SRST),
    // puis un CCB posté et 02h sans 01h : rien chez PCem (le 02h est compté), INVDCMD et HACC sur la carte ; puis 01h :
    // chez PCem le CCB part sans 02h, sur la carte il attend. Le tourniquet : un CCB servi au MBO 1, puis deux au MBO 0
    // et 2 et un 02h — PCem sert le MBO 0 seul, la carte le 2 puis le 0 (rendus aux MBI 1 et 2, le MBI courant étant le 1). Un code de MBO inconnu (05h) : PCem le laisse,
    // la carte le remet à zéro.
    private static int Pb141(bool hw)
    {
        var bad = 0;
        bad += AvecMb(() =>
        {
            var st = InitMb(0);
            return Voir("01h de compte 0 : INVDCMD", (st & 1) == (hw ? 1 : 0), $"état {st:X2}h", hw ? "INVDCMD posé" : "INVDCMD absent", hw);
        });
        bad += AvecMb(() =>
        {
            InitMb(2);
            PoserCcb(0);
            PoserCcb(1);
            Mbo(0, 1, CcbK(0));
            Mbo(1, 1, CcbK(1));
            Start();
            var v = Mbis(2);
            var att = hw ? "01:80000 01:80040" : "01:80000 --";
            return Voir("deux CCB, un seul 02h : les MBI", v == att, v, att, hw);
        });
        bad += AvecMb(() =>
        {
            InitMb(2);
            mem.mem_writeb_phys(Mb + 8 + 1, 0x0A);
            mem.mem_writeb_phys(Mb + 8 + 2, 0xBC);
            mem.mem_writeb_phys(Mb + 8 + 3, 0xDE);
            mem.mem_writeb_phys(Mb + 8, 0x01);
            PoserCcb(0);
            Mbo(0, 1, CcbK(0));
            Start();
            var v = Mbis(2);
            var att = hw ? "01:ABCDE 01:80000" : "01:80000 --";
            return Voir("le MBI 0 occupé : les MBI", v == att, v, att, hw);
        });
        bad += AvecMb(() =>
        {
            var b = 0;
            InitMb(1);
            mem.mem_writeb_phys(Mb + 4 + 3, 0x77);
            mem.mem_writeb_phys(Mb + 4, 0x01);
            PoserCcb(0);
            Mbo(0, 1, CcbK(0));
            Start();
            var v = $"MBO {MboCode(0):X2}, MBI {Mbis(1)}";
            var att = hw ? "MBO 01, MBI 01:77" : "MBO 00, MBI 01:80000";
            b += Voir("l'unique MBI occupé : le MBO et le MBI", v == att, v, att, hw);
            mem.mem_writeb_phys(Mb + 4, 0x00);
            Tourner(400);
            v = $"MBO {MboCode(0):X2}, MBI {Mbis(1)}";
            att = hw ? "MBO 00, MBI 01:80000" : "MBO 00, MBI --";
            b += Voir("le MBI libéré par l'hôte : le MBO et le MBI", v == att, v, att, hw);
            return b;
        });
        foreach (var (ctrl, nom) in new[] { (0x80, "HRST"), (0x40, "SRST") })
            bad += AvecMb(() =>
            {
                var b = 0;
                InitMb(1);
                iXtal26.io.outb(R9Aha.P, (byte)ctrl);
                Tourner(50);
                iXtal26.io.outb(R9Aha.P, 0x20);
                PoserCcb(0);
                Mbo(0, 1, CcbK(0));
                var (hacc, etat) = Start();
                var v = hacc ? $"HACC, état {etat:X2}h" : "aucun HACC";
                var invd = hacc && (etat & 1) != 0;
                b += Voir($"{nom}, puis 02h sans 01h", invd == hw && (hw || !hacc), v, hw ? "HACC, INVDCMD posé" : "aucun HACC", hw);
                InitMb(1);
                Mbo(0, 1, CcbK(0));
                Tourner(400);
                v = Mbis(1);
                var att = hw ? "--" : "01:80000";
                b += Voir($"{nom}, puis 01h, sans 02h : le MBI", v == att, v, att, hw);
                return b;
            });
        bad += AvecMb(() =>
        {
            InitMb(3);
            PoserCcb(0);
            PoserCcb(1);
            PoserCcb(2);
            Mbo(1, 1, CcbK(1));
            Start();
            for (var i = 0; i < 3; i++)
                mem.mem_writeb_phys(Mb + 12 + (uint)(i * 4), 0);
            Mbo(0, 1, CcbK(0));
            Mbo(2, 1, CcbK(2));
            Start();
            var v = Mbis(3);
            var att = hw ? "-- 01:80080 01:80000" : "-- 01:80000 --";
            return Voir("le tourniquet (servi au MBO 1, puis MBO 0 et 2) : les MBI", v == att, v, att, hw);
        });
        bad += AvecMb(() =>
        {
            InitMb(1);
            Mbo(0, 5, CcbK(0));
            Start();
            var v = $"{MboCode(0):X2}";
            var att = hw ? "00" : "05";
            return Voir("un code de MBO inconnu (05h) : le MBO", v == att, v, att, hw);
        });
        return bad;
    }

    // PB-139 — ABORT (AHA-1540C TR, chapitre 5 : MBO, « sets the MBO command byte back to zero » ; MBI 02h « CCB aborted
    // by host », 03h « Aborted CCB not found »). Quatre emplacements : ABORT au MBO 1 d'un CCB inconnu (80F00h), START
    // au MBO 2, deux 02h — PCem efface l'octet 8 (le START du MBO 2, perdu) et rend 02h deux fois, l'ABORT restant ; la
    // carte efface le MBO 1, rend 03h, puis lance le CCB. Deux emplacements : ABORT au MBO 0 d'un CCB qui attend au MBO 1,
    // deux 02h — PCem rend 02h puis lance le CCB quand même ; la carte efface les deux MBO et ne rend que 02h.
    private static int Pb139(bool hw)
    {
        var bad = 0;
        bad += AvecMb(() =>
        {
            InitMb(4);
            PoserCcb(0);
            Mbo(1, 2, 0x80F00);
            Mbo(2, 1, CcbK(0));
            Start();
            Start();
            var v = $"MBO {MboCode(1):X2} {MboCode(2):X2}, MBI {Mbis(4)}";
            var att = hw ? "MBO 00 00, MBI 03:80F00 01:80000 -- --" : "MBO 02 00, MBI 02:80F00 02:80F00 -- --";
            return Voir("ABORT d'un CCB inconnu au MBO 1, START au MBO 2", v == att, v, att, hw);
        });
        bad += AvecMb(() =>
        {
            InitMb(2);
            PoserCcb(0);
            Mbo(0, 2, CcbK(0));
            Mbo(1, 1, CcbK(0));
            Start();
            Start();
            var v = $"MBO {MboCode(0):X2} {MboCode(1):X2}, MBI {Mbis(2)}";
            var att = hw ? "MBO 00 00, MBI 02:80000 --" : "MBO 00 00, MBI 02:80000 01:80000";
            return Voir("ABORT au MBO 0 du CCB qui attend au MBO 1", v == att, v, att, hw);
        });
        return bad;
    }
}
