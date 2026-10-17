// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// Les cas de l'enregistrement (G13.8e1, PLAN-G13.md) : PB-148 (la SB 16 et l'AWE32 enregistrent la MIDI par le
// mélangeur d'entrée ; 20h), PB-151 (l'anneau de 10000h entrées, sans alias), et la sélection additive de PB-153, sur
// le banc du son (MaterielCasSon.cs). Le temps se joue à la main : sound_poll, tic par tic à 48 kHz (le tampon du son,
// qui rend l'OPL et remplit l'anneau du CAN), et entre eux les tics d'entrée du DSP à sa fréquence. La source : une
// note de l'OPL (l'exemple du guide d'AdLib), sur les deux voies (le mode OPL2 du YMF262).

using iXtal26.Cdrom;
using iXtal26.Ide;
using iXtal26.Memory;
using iXtal26.Scsi;
using iXtal26.Sound;

namespace iXtal26.Diff;

internal static partial class MaterielCas
{
    /// <summary>Une note tenue de l'OPL, sur les deux voies ; le compte du tampon du son (statique, celui de la machine
    /// d'avant) remis à zéro, pour que deux machines enregistrent aux mêmes instants.</summary>
    private static void NoteOpl()
    {
        sound.sound_pos_global = 0;
        int[][] regs =
        [
            [0x20, 0x01], [0x40, 0x10], [0x60, 0xf0], [0x80, 0x77], [0xa0, 0x98],
            [0x23, 0x01], [0x43, 0x00], [0x63, 0xf0], [0x83, 0x77], [0xc0, 0x30], [0xb0, 0x31],
        ];
        foreach (var r in regs)
        {
            io.outb(0x388, (byte)r[0]);
            io.outb(0x389, (byte)r[1]);
        }
    }

    /// <summary><paramref name="n"/> tics du son (48 kHz) ; entre eux, les tics d'entrée du DSP à sb_freq, tant que sa
    /// minuterie tourne. S'arrête à l'IRQ 8 bits ou 16 bits si <paramref name="irq"/>.</summary>
    private static void Son(sb_dsp_t d, int n, bool irq = false, int pourmille = 1000, Action? apres = null)
    {
        long acc = 0;
        for (var i = 0; i < n; i++)
        {
            sound.sound_poll(null);
            apres?.Invoke();
            if (timer.timer_is_enabled(d.input_timer) == 0)
                continue;
            acc += (long)Math.Max(d.sb_freq, 0) * pourmille / 1000;
            while (acc >= 48000)
            {
                acc -= 48000;
                if (timer.timer_is_enabled(d.input_timer) != 0)
                    sound_sb_dsp.sb_poll_i(d);
            }
            if (irq && (d.sb_irq8 != 0 || d.sb_irq16 != 0))
                return;
        }
    }

    /// <summary>La MIDI à 0 dB (34h, 35h : le reset de PB-237 la laisse à −14 dB, trop bas pour comparer), la note de
    /// l'OPL, les registres du mélangeur <paramref name="mel"/> (index, valeur), 42h à 22 050 Hz,
    /// deux tampons de son, puis C8h <paramref name="mode"/> FFh 00h (256 octets d'entrée en 8 bits) sur le canal 1,
    /// joué jusqu'à l'IRQ : les octets reçus.</summary>
    private static byte[] Enregistre(sb_dsp_t d, int mode, params int[] mel)
    {
        MelEcrit(0x224, 0x34, 0xf8);
        MelEcrit(0x224, 0x35, 0xf8);
        for (var i = 0; i + 1 < mel.Length; i += 2)
            MelEcrit(0x224, mel[i], mel[i + 1]);
        NoteOpl();
        Dsp(0x42, 0x56, 0x22);
        Son(d, 4800);
        Dma1(0x10000, 0xff, false, entree: true);
        Dsp(0xc8, mode, 0xff, 0x00);
        Son(d, 48000, irq: true);
        return Enumerable.Range(0, 256).Select(i => (byte)mem.mem_readb_phys(0x10000 + (uint)i)).ToArray();
    }

    private static bool Muet(IEnumerable<byte> b) => b.All(x => x == 0x80);

    private static string Voies(byte[] b)
    {
        var g = b.Where((_, i) => i % 2 == 0).ToArray();
        var dr = b.Where((_, i) => i % 2 == 1).ToArray();
        if (Muet(b))
            return "muette";
        return $"G {(Muet(g) ? "muette" : "sonore")}, D {(Muet(dr) ? "muette" : "sonore")}" +
               (!Muet(g) && g.SequenceEqual(dr) ? ", G = D" : "");
    }

    /// <summary>La voie gauche de <paramref name="b"/> contre celle de <paramref name="a"/> : égale, doublée (à la
    /// troncature de l'octet près), ou autre ; « muette » si les deux le sont.</summary>
    private static string Rapport(byte[] a, byte[] b)
    {
        var ga = a.Where((_, i) => i % 2 == 0).Select(x => x - 0x80).ToArray();
        var gb = b.Where((_, i) => i % 2 == 0).Select(x => x - 0x80).ToArray();
        if (ga.All(x => x == 0) && gb.All(x => x == 0))
            return "muette";
        if (ga.SequenceEqual(gb))
            return "égale";
        if (ga.Zip(gb).All(p => Math.Abs(p.Second - 2 * p.First) <= 1) && gb.Any(x => Math.Abs(x) > 8))
            return "doublée";
        return "autre";
    }

    // PB-148 — la SB 16 et l'AWE32 enregistrent la MIDI (guide, fig. 4-5, p. 4-14) : la source après son volume, les
    // interrupteurs de 3Dh et 3Eh, le gain d'entrée (3Fh), le CAN. PCem ne remplit rien : 80h partout. Une note de
    // l'OPL sur les deux voies : 3Dh=40h, 3Eh=20h, les deux voies égales ; 3Dh=00h, la gauche muette ; en mono, la
    // gauche seule (« samples will only be taken from the left input mixer ») ; 3Fh=40h, la gauche doublée. La
    // sélection additive (PB-153) : 3Dh=60h contre 40h, doublée sous PB-153, égale sans lui (la priorité de PCem :
    // MIDI.L seul). 20h, l'entrée directe : l'échantillon de l'instant, sans toucher sb_freq (PCem : 80h, et 45 454
    // Hz). Témoin : la SB Pro 2 (24h, le micro : 80h des deux côtés). Le CD et la SB Pro : Pb148Cd (G13.8e2).
    private static int Pb148(bool hw)
    {
        int Un(string nom, string carte, Func<sb_dsp_t, string> corps, string pcem, string mat, bool temoin = false) =>
            AvecSb(carte, d =>
            {
                var v = corps(d);
                var att = temoin || hw ? mat : pcem;
                return Voir(nom, v == att, v, att, hw);
            });
        var bad = Un("SB 16 : la note, 3Dh=40h, 3Eh=20h, C8h 20h (stéréo)", "sb16",
                     d => Voies(Enregistre(d, 0x20, 0x3d, 0x40, 0x3e, 0x20)), "muette", "G sonore, D sonore, G = D");
        bad += Un("SB 16 : la note, 3Dh=00h, 3Eh=20h, C8h 20h", "sb16",
                  d => Voies(Enregistre(d, 0x20, 0x3d, 0x00, 0x3e, 0x20)), "muette", "G muette, D sonore");
        bad += Un("SB 16 : la note, 3Dh=40h, 3Eh=00h, C8h 00h (mono : la gauche)", "sb16",
                  d => Muet(Enregistre(d, 0x00, 0x3d, 0x40, 0x3e, 0x00)) ? "muette" : "sonore", "muette", "sonore");
        bad += Un("SB 16 : la note, 3Dh=00h, 3Eh=60h, C8h 00h (mono : la droite ignorée)", "sb16",
                  d => Muet(Enregistre(d, 0x00, 0x3d, 0x00, 0x3e, 0x60)) ? "muette" : "sonore", "muette", "muette");
        bad += Un("AWE32 : la note, 3Dh=40h, 3Eh=20h, C8h 20h", "sbawe32",
                  d => Voies(Enregistre(d, 0x20, 0x3d, 0x40, 0x3e, 0x20)), "muette", "G sonore, D sonore, G = D");

        byte[]? seule = null;
        AvecSb("sb16", d =>
        {
            seule = Enregistre(d, 0x20, 0x3d, 0x40, 0x3e, 0x00, 0x3f, 0x00);
            return 0;
        });
        bad += Un("SB 16 : 3Fh=40h (le gain d'entrée, 6 dB), contre 3Fh=00h", "sb16",
                  d => Rapport(seule!, Enregistre(d, 0x20, 0x3d, 0x40, 0x3e, 0x00, 0x3f, 0x40)), "muette", "doublée");
        bad += Un("SB 16 : 3Dh=60h (MIDI.L et MIDI.R), contre 3Dh=40h", "sb16",
                  d => Rapport(seule!, Enregistre(d, 0x20, 0x3d, 0x60, 0x3e, 0x00)), "muette",
                  materiel.pb_153 ? "doublée" : "égale");

        bad += Un("SB 16 : la note, puis 20h huit fois", "sb16", d =>
        {
            MelEcrit(0x224, 0x34, 0xf8);
            MelEcrit(0x224, 0x3d, 0x40);
            NoteOpl();
            Dsp(0x42, 0x56, 0x22);
            Son(d, 4800);
            var sonores = 0;
            for (var i = 0; i < 8; i++)
            {
                Dsp(0x20);
                if (LireDsp() != "80")
                    sonores++;
                Son(d, 7);
            }
            return $"{(sonores > 0 ? "sonore" : "muette")}, {d.sb_freq} Hz";
        }, "muette, 45454 Hz", "sonore, 22050 Hz");

        // L'anneau sur la durée (sous PB-148 seul : PCem n'en a pas) : une seconde d'entrée automatique, CEh 20h, les
        // tics à la cadence exacte, puis 10 % plus vite que le remplissage ; après chaque tic du son, l'écart des
        // têtes.
        string Duree(sb_dsp_t d, int pourmille)
        {
            if (!materiel.pb_148)
                return "sans objet";
            MelEcrit(0x224, 0x3d, 0x40);
            NoteOpl();
            Dsp(0x42, 0x56, 0x22);
            Son(d, 4800);
            Dma1(0x10000, 0xffff, true, entree: true);
            Dsp(0xce, 0x20, 0xff, 0xff);
            var an = sound_sb_dsp.anneau_materiel(d);
            int tenues = 0, depasse = 0;
            Son(d, 48000, pourmille: pourmille, apres: () =>
            {
                if (an.lit < 0)
                    return;
                if (an.tenue)
                    tenues++;
                if (((an.ecrit - an.lit) & 0xffff) > 0x8000)
                    depasse++;
            });
            return pourmille == 1000 ? $"{tenues} trame(s) tenue(s), dépassée {depasse} fois"
                                     : $"tenue{(tenues > 0 ? "s" : " aucune")}, dépassée {depasse} fois";
        }
        bad += Un("SB 16 : une seconde de CEh 20h, les tics à la cadence du remplissage", "sb16", d => Duree(d, 1000),
                  "sans objet", "0 trame(s) tenue(s), dépassée 0 fois");
        bad += Un("SB 16 : une seconde de CEh 20h, les tics 10 % plus vite", "sb16", d => Duree(d, 1100),
                  "sans objet", "tenues, dépassée 0 fois");

        bad += Un("SB Pro 2 : la note, 24h (le micro, témoin)", "sbprov2", d =>
        {
            NoteOpl();
            Dsp(0x40, 0xd3);
            Son(d, 4800);
            Dma1(0x10000, 0xff, false, entree: true);
            Dsp(0x24, 0xff, 0x00);
            Son(d, 48000, irq: true);
            return Muet(Enumerable.Range(0, 256).Select(i => (byte)mem.mem_readb_phys(0x10000 + (uint)i)))
                ? "muette" : "sonore";
        }, "", "muette", temoin: true);
        return bad + Pb148Cd(hw);
    }

    /// <summary>Un disque audio dans le pilote d'images (une piste de mille secteurs, chaque trame à 1000h à gauche
    /// et 2000h à droite), joué, puis deux blocs du lecteur (9 600 tics du son) : le CD sort son signal,
    /// constant.</summary>
    private static void CdJoue(sb_dsp_t d)
    {
        var trames = new byte[1000 * 2352];
        for (var i = 0; i < trames.Length; i += 4)
        {
            trames[i + 1] = 0x10;
            trames[i + 3] = 0x20;
        }
        Ecrire(DossierCd(), "cdson.bin", trames);
        var cue = Feuille(DossierCd(), "cdson.cue", "FILE \"cdson.bin\" BINARY", "  TRACK 01 AUDIO",
                          "    INDEX 01 00:00:00");
        if (cdrom_image.image_open(cue) != 0)
            throw new InvalidOperationException("cdson.cue refusée");
        ide_atapi.atapi!.playaudio(200, 500, 0);
        Son(d, 9600);
    }

    /// <summary>Les octets d'une entrée, par voie (les pairs, les impairs) : la valeur de chacune, si elle est
    /// constante ; sinon la liste des valeurs vues.</summary>
    private static string Paires(byte[] b)
    {
        string Voie(IEnumerable<byte> v) => string.Join("/", v.Distinct().Select(x => x.ToString("X2")));
        return $"G {Voie(b.Where((_, i) => i % 2 == 0))}, D {Voie(b.Where((_, i) => i % 2 == 1))}";
    }

    /// <summary>24h FFh 00h sur la SB Pro (256 octets d'entrée en 8 bits, la constante D3h et deux tampons de son,
    /// comme Enregistre), après les commandes
    /// <paramref name="avant"/>, le mélangeur réglé par <paramref name="mel"/> : les octets reçus.</summary>
    private static byte[] EnregistrePro(sb_dsp_t d, int[] avant, params int[] mel)
    {
        for (var i = 0; i + 1 < mel.Length; i += 2)
            MelEcrit(0x224, mel[i], mel[i + 1]);
        foreach (var c in avant)
        {
            if (c < 0)
                ResetDsp();
            else
                Dsp(c);
        }
        Dsp(0x40, 0xd3);
        Son(d, 4800);
        Dma1(0x10000, 0xff, false, entree: true);
        Dsp(0x24, 0xff, 0x00);
        Son(d, 48000, irq: true);
        return Enumerable.Range(0, 256).Select(i => (byte)mem.mem_readb_phys(0x10000 + (uint)i)).ToArray();
    }

    // PB-148, le CD (G13.8e2) — le CT1745 mène le CD par son volume (36h, 37h) aux interrupteurs de 3Dh et 3Eh (CD.L,
    // 04h ; CD.R, 02h) ; le CT1345 l'enregistre quand 0Ch le choisit (bits 1-2 à 1), sans son volume (déduit). Un
    // disque audio joué, constant (1000h à gauche, 2000h à droite) : sur la SB 16, C8h 20h, 3Dh=04h et 3Eh=02h,
    // 36h=37h=F8h (0 dB : 4095 et 8191), G 8Fh et D 9Fh ; croisés, G 9Fh et D 8Fh ; 36h=C0h (−14 dB), G 83h ; 3Dh=06h,
    // la somme (AFh) sous PB-153, CD.L seul sans lui ; l'AWE32 de même ; l'ATAPI à voies croisées, la gauche du lecteur
    // à 80h, G 9Fh et D 88h ; le pont lu sur une rampe (Rampe) ; témoin, le CD arrêté après une machine qui le jouait
    // (le pont remis à zéro à l'amorçage), muette. Sur la SB Pro 2, 0Ch=02h, 24h : 90h (le CD gauche, sans volume,
    // 28h=00h compris) ; A8h, 90h et A0h, une voie par tic ; A8h puis A0h, ou A8h puis le reset du DSP, 90h seul ;
    // témoin, 0Ch=06h (la ligne), muette. PCem : 80h partout.
    private static int Pb148Cd(bool hw)
    {
        int Un(string nom, string carte, Func<sb_dsp_t, string> corps, string mat, bool temoin = false,
               bool joue = true) =>
            AvecSb(carte, d =>
            {
                if (joue)
                    CdJoue(d);
                var v = corps(d);
                var att = temoin || hw ? mat : "G 80, D 80";
                return Voir(nom, v == att, v, att, hw);
            });
        string Seize(sb_dsp_t d, params int[] mel) =>
            Paires(Enregistre(d, 0x20, [0x36, 0xf8, 0x37, 0xf8, .. mel]));
        var bad = Un("SB 16 : le CD, 3Dh=04h, 3Eh=02h, C8h 20h", "sb16", d => Seize(d, 0x3d, 0x04, 0x3e, 0x02),
                     "G 8F, D 9F");
        bad += Un("SB 16 : le CD, 3Dh=02h, 3Eh=04h (croisés)", "sb16", d => Seize(d, 0x3d, 0x02, 0x3e, 0x04),
                  "G 9F, D 8F");
        bad += Un("SB 16 : le CD, 36h=C0h (−14 dB)", "sb16", d => Seize(d, 0x3d, 0x04, 0x3e, 0x02, 0x36, 0xc0),
                  "G 83, D 9F");
        bad += Un("SB 16 : le CD, 3Dh=06h (CD.L et CD.R), 3Eh=00h", "sb16", d => Seize(d, 0x3d, 0x06, 0x3e, 0x00),
                  materiel.pb_153 ? "G AF, D 80" : "G 8F, D 80");
        bad += Un("AWE32 : le CD, 3Dh=04h, 3Eh=02h, C8h 20h", "sbawe32", d => Seize(d, 0x3d, 0x04, 0x3e, 0x02),
                  "G 8F, D 9F");
        bad += Un("SB 16 : le CD arrêté (la machine d'avant le jouait), 3Dh=04h, 3Eh=02h", "sb16",
                  d => Seize(d, 0x3d, 0x04, 0x3e, 0x02), "G 80, D 80", temoin: true, joue: false);
        bad += Un("SB 16 : le CD, l'ATAPI à voies croisées, la gauche du lecteur à 80h (page 0Eh)", "sb16", d =>
        {
            var page = scsi_cd_c.mode_pages_in[0x0e];
            var (f, g, v, dr, vd) = (scsi_cd_c.page_flags[0x0e], page[6], page[7], page[8], page[9]);
            try
            {
                scsi_cd_c.page_flags[0x0e] |= 2;
                (page[6], page[7], page[8], page[9]) = (2, 0x80, 1, 0xff);
                Son(d, 9600);
                return Seize(d, 0x3d, 0x04, 0x3e, 0x02);
            }
            finally
            {
                scsi_cd_c.page_flags[0x0e] = f;
                (page[6], page[7], page[8], page[9]) = (g, v, dr, vd);
            }
        }, "G 9F, D 88");
        bad += AvecSb("sb16", d =>
        {
            var v = Rampe(d);
            var att = hw ? "cd_pos 1234 : la rampe suivie aux sept reculs, au-delà le silence" : "sans objet";
            return Voir("SB 16 : le pont du CD, une rampe, lue autour des tics du lecteur", v == att, v, att, hw);
        });
        bad += Un("SB Pro 2 : le CD, 0Ch=02h, 24h", "sbprov2", d => Paires(EnregistrePro(d, [], 0x0c, 0x02)),
                  "G 90, D 90");
        bad += Un("SB Pro 2 : le CD, 0Ch=02h, 28h=00h (le volume du CD), 24h", "sbprov2",
                  d => Paires(EnregistrePro(d, [], 0x0c, 0x02, 0x28, 0x00)), "G 90, D 90");
        bad += Un("SB Pro 2 : le CD, 0Ch=02h, A8h, 24h (stéréo)", "sbprov2",
                  d => Paires(EnregistrePro(d, [0xa8], 0x0c, 0x02)), "G 90, D A0");
        bad += Un("SB Pro 2 : le CD, 0Ch=02h, A8h puis A0h, 24h", "sbprov2",
                  d => Paires(EnregistrePro(d, [0xa8, 0xa0], 0x0c, 0x02)), "G 90, D 90");
        bad += Un("SB Pro 2 : le CD, 0Ch=02h, A8h puis le reset du DSP, 24h", "sbprov2",
                  d => Paires(EnregistrePro(d, [0xa8, -1], 0x0c, 0x02)), "G 90, D 90");
        bad += Un("SB Pro 2 : le CD, 0Ch=06h (la ligne, témoin), 24h", "sbprov2",
                  d => Paires(EnregistrePro(d, [], 0x0c, 0x06)), "G 80, D 80", temoin: true);
        return bad;
    }

    /// <summary>Le pont du CD lu directement (sous PB-148 seul : PCem n'en a pas) : une piste dont la gauche est une
    /// rampe (le rang de la trame, sur 15 bits), la droite 1234h ; 9 600 + 1 234 tics du son, cd_pos à 1 234 ; la
    /// trame lue à sept reculs — 0, la moitié de cd_pos, cd_pos (le début du bloc), cd_pos + 1 (la fin du précédent),
    /// cd_pos + 2 400, + 4 799 et + 4 800 (le début du précédent) — suit la rampe au prorata 44 100 / 48 000 ; au-delà
    /// du précédent, le silence.</summary>
    private static string Rampe(sb_dsp_t d)
    {
        if (!materiel.pb_148)
            return "sans objet";
        var trames = new byte[1000 * 2352];
        for (int i = 0, n = 0; i < trames.Length; i += 4, n++)
        {
            trames[i] = (byte)(n & 0xff);
            trames[i + 1] = (byte)((n >> 8) & 0x7f);
            (trames[i + 2], trames[i + 3]) = (0x34, 0x12);
        }
        Ecrire(DossierCd(), "rampe.bin", trames);
        var cue = Feuille(DossierCd(), "rampe.cue", "FILE \"rampe.bin\" BINARY", "  TRACK 01 AUDIO",
                          "    INDEX 01 00:00:00");
        if (cdrom_image.image_open(cue) != 0)
            return "rampe.cue refusée";
        ide_atapi.atapi!.playaudio(200, 500, 0);
        Son(d, 9600 + 1234);
        var o = new ulong[6];
        sound.CdSoundProbe(o);
        var pos = (int)o[0];
        sound.cd_trame_materiel(pos, out var g0, out _);
        var ko = new List<string>();
        foreach (var r in new[] { 0, pos / 2, pos, pos + 1, pos + 2400, pos + 4799, pos + 4800 })
        {
            sound.cd_trame_materiel(r, out var g, out var dr);
            var att = (g0 + (int)Math.Floor((pos - r) * 44100.0 / 48000)) & 0x7fff;
            if (g != att || dr != 0x1234)
                ko.Add($"recul {r} : {g:X4} {dr:X4}, attendu {att:X4} 1234");
        }
        sound.cd_trame_materiel(pos + 4801, out var gs, out var ds);
        if (gs != 0 || ds != 0)
            ko.Add($"recul {pos + 4801} : {gs:X4} {ds:X4}, attendu le silence");
        return ko.Count == 0 ? $"cd_pos {pos} : la rampe suivie aux sept reculs, au-delà le silence"
                             : string.Join(" ; ", ko);
    }

    // PB-151 — l'entrée stéréo lit record_buffer[record_pos_read + 1] : à FFFEh, l'indice FFFFh, au-delà du tableau,
    // est buffer[0], un échantillon de sortie. La carte n'a pas d'alias (déduit, et certain). SB 16, la tête à FFFEh,
    // buffer[0] = 1234h : C8h 20h 01h 00h, le second octet 92h chez PCem, 80h ; B8h 20h 01h 00h (16 bits, canal 5), le
    // second mot 9234h, 8000h. Témoin : C8h 00h (mono), 80h des deux côtés. Sous PB-148, l'anneau du CAN n'a pas
    // d'alias : PB-151 y est sans objet, l'attendu celui de la carte.
    private static int Pb151(bool hw)
    {
        int Un(string nom, Func<sb_dsp_t, string> corps, string pcem, string mat, bool temoin = false) =>
            AvecSb("sb16", d =>
            {
                var v = corps(d);
                var att = temoin || hw || materiel.pb_148 ? mat : pcem;
                return Voir(nom, v == att, v, att, hw);
            });
        string Octets(uint a, int n) =>
            string.Join(" ", Enumerable.Range(0, n).Select(i => mem.mem_readb_phys(a + (uint)i).ToString("X2")));
        var bad = Un("SB 16 : la tête à FFFEh, buffer[0]=1234h, C8h 20h 01h 00h", d =>
        {
            Dma1(0x10000, 0x01, false, entree: true);
            Dsp(0xc8, 0x20, 0x01, 0x00);
            d.record_pos_read = 0xfffe;
            d.buffer[0] = 0x1234;
            TicsEntree(d, 1);
            return Octets(0x10000, 2);
        }, "80 92", "80 80");
        bad += Un("SB 16 : la tête à FFFEh, buffer[0]=1234h, B8h 20h 01h 00h (16 bits)", d =>
        {
            Dma5(0x20000, 0x01, false, entree: true);
            Dsp(0xb8, 0x20, 0x01, 0x00);
            d.record_pos_read = 0xfffe;
            d.buffer[0] = 0x1234;
            TicsEntree(d, 1);
            return Octets(0x20000, 4);
        }, "00 80 34 92", "00 80 00 80");
        bad += Un("SB 16 : la tête à FFFEh, C8h 00h (mono, témoin)", d =>
        {
            Dma1(0x10000, 0x01, false, entree: true);
            Dsp(0xc8, 0x00, 0x01, 0x00);
            d.record_pos_read = 0xfffe;
            d.buffer[0] = 0x1234;
            TicsEntree(d, 1);
            return Octets(0x10000, 1);
        }, "", "80", temoin: true);
        return bad;
    }
}
