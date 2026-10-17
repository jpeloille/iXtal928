// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// Les cas de l'EMU8000 de l'AWE32 (G13.8f, PLAN-G13.md) : PB-158 (WC avance à la lecture), PB-159 (le bit 31 et le
// masque de 24 bits des adresses de la DRAM, relues en A22h) et PB-160 (HWCF3 sans la rustine), sur le banc du son
// (MaterielCasSon.cs) ; G13.8f2 : l'octet impair de PB-158, et PB-162 (le chorus) et PB-163 (la réverbération), leurs
// moteurs joués en C# seul sur des états fabriqués, comme emu8k-kernel-check. L'EMU8000 se conduit par ses ports de
// mots : le pointeur en E22h (le registre dans les bits 5 à 7, le canal dans les bits 0 à 4), Data1 en A20h, Data2 en
// A22h. Le temps de sortie se joue à la main : sound_pos_global avance d'un tic par échantillon à 48 kHz, et rien
// d'autre ne le bouge, le processeur arrêté. Les drapeaux de la DRAM de PCem (dmareadbit, dmawritebit) sont statiques
// et traversent les machines : chaque cas les remet à zéro.

using iXtal26.Sound;

namespace iXtal26.Diff;

internal static partial class MaterielCas
{
    private static emu8k_t Emu() =>
        sound_sb.sb_pri?.emu8k ?? throw new InvalidOperationException("pas d'EMU8000 après initpc");

    private static void Pointeur(int reg, int canal) => io.outw(0xe22, (ushort)((reg << 5) | canal));

    private static string Mot(ushort port) => $"{io.inw(port):X4}";

    /// <summary>Un cas sur l'AWE32 : le corps, rendu face à l'attendu de PCem ou du matériel.</summary>
    private static int UnAwe(string nom, Func<string> corps, string pcem, string mat, bool hw, bool temoin = false) =>
        AvecSb("sbawe32", _ =>
        {
            sound_emu8k.dmareadbit = sound_emu8k.dmawritebit = 0;
            var v = corps();
            var att = temoin || hw ? mat : pcem;
            return Voir(nom, v == att, v, att, hw);
        });

    // PB-158 — WC compte les échantillons à 44,1 kHz (guide, p. 14), et CPF, CVCF et CCCA sont courants ; PCem
    // n'avance la puce qu'aux écritures et à chaque tampon. 240 tics de sortie (5 ms) entre deux lectures de WC, sans
    // écriture : ΔWC = 0 chez PCem, 220 sur la carte (240 × 44 100 / 48 000, depuis le tic 0 ; la puce part de pos 0,
    // que le cas montre). Les registres courants : la cible de hauteur (PTRX) ou de volume (VTFT) écrite, le pointeur mis
    // sur CPF ou CVCF (une écriture, au même tic), 48 tics, puis le mot haut relu sans écriture — chez PCem, la valeur
    // d'avant ; sur la carte, celle que la puce a jouée (le modèle de PCem pour la valeur : la hauteur posée à sa cible,
    // le volume glissant de 400h par échantillon, PB-160 (c) : 44 échantillons, B000h). Témoin : le pointeur réécrit
    // entre les deux lectures de WC.
    private static int Pb158(bool hw)
    {
        string Temps(Func<Func<int, string>, string> corps)
        {
            var avant = sound.sound_pos_global;
            sound.sound_pos_global = 0;
            try
            {
                return $"pos0={Emu().pos} " + corps(n => { sound.sound_pos_global += n; return ""; });
            }
            finally
            {
                sound.sound_pos_global = avant;
            }
        }
        string Wc(bool ecrit) => Temps(avance =>
        {
            Pointeur(1, 27);
            var w1 = io.inw(0xa22);
            avance(240);
            if (ecrit)
                Pointeur(1, 27);
            var w2 = io.inw(0xa22);
            return $"ΔWC={(ushort)(w2 - w1)}";
        });
        // La cible (registre cible du port 622h) écrite, puis le registre courant relu en 622h après 48 tics.
        string Courant(int cible, int courant, string nom) => Temps(avance =>
        {
            Pointeur(cible, 0);
            io.outw(0x622, 0xe000);
            Pointeur(courant, 0);
            avance(48);
            return $"{nom}={Mot(0x622)}";
        });
        var bad = UnAwe("AWE32 : WC (3Bh) lu, 240 tics de sortie, WC relu sans écriture", () => Wc(false),
                        "pos0=0 ΔWC=0", "pos0=0 ΔWC=220", hw);
        bad += UnAwe("AWE32 : PTRX de la voix 0 à E000h, le pointeur sur CPF, 48 tics, CPF relu",
                     () => Courant(1, 0, "CPF"), "pos0=0 CPF=0000", "pos0=0 CPF=E000", hw);
        bad += UnAwe("AWE32 : VTFT de la voix 0 à E000h, le pointeur sur CVCF, 48 tics, CVCF relu",
                     () => Courant(3, 2, "CVCF"),
                     "pos0=0 CVCF=0000", "pos0=0 CVCF=B000", hw);
        bad += UnAwe("AWE32 : WC lu, 240 tics, le pointeur réécrit, WC relu (témoin)", () => Wc(true),
                     "", "pos0=0 ΔWC=220", hw, temoin: true);
        // La part (b) : un octet lu à l'adresse impaire rend l'octet haut du mot (le bus AT, déduit) ; PCem, les bits 1
        // à 8. SMALR écrit en A20h seul (sans drapeau), puis lu par octets en A21h et en A20h (témoin, l'octet bas).
        string Octets()
        {
            Pointeur(1, 20);
            io.outw(0xa20, 0x1234);
            return $"A21h={io.inb(0xa21):X2} A20h={io.inb(0xa20):X2}";
        }
        bad += UnAwe("AWE32 : SMALR écrit en A20h (1234h), puis A21h et A20h lus par octets", Octets,
                     "A21h=1A A20h=34", "A21h=12 A20h=34", hw);
        return bad;
    }

    // PB-162 — le chorus de PCem (un « workalike ») : une ligne à retard ne lit pas hors d'elle-même, et chaque voie
    // interpole à sa propre position (déduit). Le moteur joué seul, un échantillon : la gauche tient une rampe i × 256,
    // la droite 1000000h + i × 256 ; la contre-réaction nulle. Aux réglages extrêmes (le délai 1FFFh, la profondeur
    // FFh, le LFO à son sommet, HWCF4 1FFFFFh), la droite lit à −24 541,996 : PCem replie une fois (−8 158) et lit dans
    // la gauche, avec la fraction de la gauche (0) ; replié modulo 4000h, 8 226, à sa fraction (256/65 536). Puis la
    // droite à un retard d'un demi-échantillon de plus que la gauche : la fraction seule. Témoin : un retard entier.
    private static int Pb162(bool hw)
    {
        sound_emu8k.tables_init(); // la table du LFO (chortable), que remplit l'initialisation de la carte
        string Chorus(int central, int profondeur, ushort lfo, int hwcf4)
        {
            var e = new emu8k_chorus_eng_t
            {
                write = 0, feedback = 0, delay_samples_central = central,
                lfodepth_multip = (profondeur * central) >> 8,
                delay_offset_samples_right = ((double)hwcf4) / 256.0,
            };
            e.lfo_pos.int_address = lfo;
            const int N = sound_emu8k.EMU8K_LFOCHORUS_SIZE;
            for (var i = 0; i < N; i++)
            {
                e.chorus_buffer[i] = i * 256;
                e.chorus_buffer[N + i] = 0x1000000 + i * 256;
            }
            var sortie = new int[2];
            sound_emu8k.emu8k_work_chorus([0], 0, sortie, 0, e, 1);
            return $"G={sortie[0]} D={sortie[1]}";
        }
        var bad = Voir("chorus extrême : délai 1FFFh, profondeur FFh, LFO au sommet, HWCF4 1FFFFFh",
                       Chorus(0x1fff, 0xff, 0x4000, 0x1fffff) is var v1 &&
                       v1 == (hw ? "G=8704 D=18883073" : "G=8704 D=2105856"), v1,
                       hw ? "G=8704 D=18883073" : "G=8704 D=2105856", hw);
        bad += Voir("chorus : délai 100, sans LFO, la droite un demi-échantillon plus loin (HWCF4 80h)",
                    Chorus(100, 0, 0, 0x80) is var v2 && v2 == (hw ? "G=4168704 D=20945792" : "G=4168704 D=20945664"),
                    v2, hw ? "G=4168704 D=20945792" : "G=4168704 D=20945664", hw);
        bad += Voir("chorus : délai 100, sans LFO, la droite cinq échantillons plus loin (témoin)",
                    Chorus(100, 0, 0, 0x500) is var v3 && v3 == "G=4168704 D=20944640", v3, "G=4168704 D=20944640",
                    hw);
        return bad;
    }

    // PB-163 — la réverbération de PCem (un « workalike ») convertit ses calculs en float vers l'int32 comme le C
    // (cvttss2si : INT_MIN hors bornes, même en positif) ; un processeur de signal à virgule fixe sature (déduit). Le
    // moteur joué seul, un échantillon d'entrée nul, sur un état fabriqué : l'amortisseur et le premier peigne
    // (filterstore 2^30, damp1 2,0, damp2 0) rendent 2^31 — INT_MIN chez PCem, 2^31 − 1 saturé (le peigne lit un écho
    // nul et reçoit une entrée nulle, refl_in_amp 0) ; le premier diffuseur de la queue gauche reçoit INT_MIN
    // (la queue le tenait) : `-in` déborde chez PCem, et il range INT_MIN ; saturé, 2^31 − 1. Témoin : l'amortisseur
    // et le peigne dans leurs bornes (filterstore 1000, damp1 0,5), la queue tenant 1234 : 500, 500 et −1234 dans les
    // deux modes.
    private static int Pb163(bool hw)
    {
        string Reverb(int filtre, float damp1, int queue)
        {
            var e = new emu8k_reverb_eng_t { out_mix = 0, link_return_amp = 0, link_return_type = 0, refl_in_amp = 0 };
            foreach (var c in e.reflections.Concat(e.allpass).Append(e.tailL).Append(e.tailR).Append(e.damper))
                c.bufsize = 4;
            e.damper.filterstore = e.reflections[0].filterstore = filtre;
            e.damper.damp1 = e.reflections[0].damp1 = damp1;
            e.tailL.reflection[0] = queue;
            var sortie = new int[2];
            sound_emu8k.emu8k_work_reverb([0], 0, sortie, 0, e, 1);
            return $"amortisseur={e.damper.filterstore} peigne={e.reflections[0].filterstore} " +
                   $"diffuseur={e.allpass[1].reflection[0]}";
        }
        const string pcem = "amortisseur=-2147483648 peigne=-2147483648 diffuseur=-2147483648";
        const string mat = "amortisseur=2147483647 peigne=2147483647 diffuseur=2147483647";
        var bad = Voir("réverbération : l'amortisseur et le premier peigne à 2^31, la queue gauche tenant INT_MIN",
                       Reverb(1 << 30, 2.0f, int.MinValue) is var v1 && v1 == (hw ? mat : pcem), v1, hw ? mat : pcem,
                       hw);
        const string temoin = "amortisseur=500 peigne=500 diffuseur=-1234";
        bad += Voir("réverbération : l'amortisseur et le peigne dans leurs bornes (1000 × 0,5), la queue à 1234 (témoin)",
                    Reverb(1000, 0.5f, 1234) is var v2 && v2 == temoin, v2, temoin, hw);
        return bad;
    }

    // PB-159 — SMALR, SMARR, SMALW et SMARW se relisent en A22h avec le bit 31 (MT ou FULL) et les bits 30 à 24 à zéro
    // (guide, p. 11-12). PCem pose son drapeau au bit 15 de la valeur de 32 bits : A22h rend le mot haut, le drapeau ne se
    // voit jamais ; SMARW, avancé par SMRD sans masque, montre son bit 24. Le drapeau dure jusqu'à la première lecture de
    // A22h, comme chez PCem (sa durée est inconnue). Témoins : A20h, le mot bas, et une adresse écrite sans drapeau.
    private static int Pb159(bool hw)
    {
        // SMALR (34h) ou SMARR (35h) écrit, A20h puis A22h (qui pose le drapeau MT) ; A22h lu deux fois, puis A20h.
        string Lecture(int canal)
        {
            Pointeur(1, canal);
            io.outw(0xa20, 0x1234);
            io.outw(0xa22, 0x0056);
            return $"{Mot(0xa22)} {Mot(0xa22)} {Mot(0xa20)}";
        }
        var bad = UnAwe("AWE32 : SMALR écrit (A20h 1234h, A22h 0056h), puis A22h, A22h et A20h lus", () => Lecture(20),
                        "0056 0056 1234", "8056 0056 1234", hw);
        bad += UnAwe("AWE32 : SMARR écrit (A20h 1234h, A22h 0056h), puis A22h, A22h et A20h lus", () => Lecture(21),
                     "0056 0056 1234", "8056 0056 1234", hw);
        // Le drapeau FULL, un pour les deux flux d'écriture et posé par SMRD seul, est celui de PCem (PB-159 (c)) : le cas
        // ne vérifie que son bit 31 en A22h, pour SMALW.
        bad += UnAwe("AWE32 : SMALW écrit (A22h 0042h), SMRD écrit (le drapeau partagé de PCem), puis SMALW relu en " +
                     "A22h deux fois", () =>
        {
            Pointeur(1, 22);
            io.outw(0xa22, 0x0042);
            Pointeur(1, 26);
            io.outw(0xa22, 0xbeef);
            Pointeur(1, 22);
            return $"{Mot(0xa22)} {Mot(0xa22)}";
        }, "0042 0042", "8042 0042", hw);
        bad += UnAwe("AWE32 : SMARW à FFFFFFh, SMRD écrit, puis SMARW relu en A22h deux fois et en A20h", () =>
        {
            Pointeur(1, 23);
            io.outw(0xa20, 0xffff);
            io.outw(0xa22, 0x00ff);
            Pointeur(1, 26);
            io.outw(0xa22, 0xbeef);
            Pointeur(1, 23);
            return $"{Mot(0xa22)} {Mot(0xa22)} {Mot(0xa20)}";
        }, "0100 0100 0000", "8000 0000 0000", hw);
        bad += UnAwe("AWE32 : SMALR écrit en A20h seul (2345h), puis A22h et A20h lus (témoin)", () =>
        {
            Pointeur(1, 20);
            io.outw(0xa20, 0x2345);
            return $"{Mot(0xa22)} {Mot(0xa20)}";
        }, "", "0000 2345", hw, temoin: true);
        return bad;
    }

    // PB-160 — HWCF3 est effacé au reset, et 0004h, écrit en fin d'initialisation, ouvre la sortie (guide, p. 14 et
    // 20) ; PCem le force à 0004h au premier allumage d'un moteur d'enveloppe (DCYSUSV bit 7 de 1 à 0), pour Doom. La
    // voix allumée, son enveloppe, est la même dans les deux modes : seul HWCF3 diffère. Témoins : HWCF3 déjà à 0004h, et
    // le moteur laissé arrêté.
    private static int Pb160(bool hw)
    {
        // HWCF3 (3Fh) écrit à hwcf3 (ou laissé), puis DCYSUSV de la voix 0 (A0h) à 0080h, puis à dcysusv.
        string Allume(int? hwcf3, int dcysusv)
        {
            if (hwcf3 is { } h)
            {
                Pointeur(1, 31);
                io.outw(0xa20, (ushort)h);
            }
            Pointeur(5, 0);
            io.outw(0xa20, 0x0080);
            io.outw(0xa20, (ushort)dcysusv);
            var e = Emu();
            var v = e.voice[0];
            return $"HWCF3={e.hwcf3:X4} moteur={v.env_engine_on} env={v.vol_envelope.state}";
        }
        var bad = UnAwe("AWE32 non initialisée : DCYSUSV de la voix 0 à 0080h puis 0000h", () => Allume(null, 0),
                        "HWCF3=0004 moteur=1 env=0", "HWCF3=0000 moteur=1 env=0", hw);
        bad += UnAwe("AWE32 : HWCF3 à 0001h, puis DCYSUSV de la voix 0 à 0080h puis 0000h", () => Allume(1, 0),
                     "HWCF3=0004 moteur=1 env=0", "HWCF3=0001 moteur=1 env=0", hw);
        bad += UnAwe("AWE32 : HWCF3 à 0004h, puis DCYSUSV de la voix 0 à 0080h puis 0000h (témoin)", () => Allume(4, 0),
                     "", "HWCF3=0004 moteur=1 env=0", hw, temoin: true);
        bad += UnAwe("AWE32 non initialisée : DCYSUSV de la voix 0 à 0080h, deux fois (témoin)", () => Allume(null, 0x80),
                     "", "HWCF3=0000 moteur=0 env=0", hw, temoin: true);
        return bad;
    }

    // PB-289, G13.8d3a — l'EMU8000 d'une AWE32 sans PnP suit la base du DSP (+400h ; EMU8000 Programmer's Guide,
    // p. 6-7) ; PCem le règle à part (emu_addr, 620h sans clé). Le pointeur (la base + C02h) écrit 45h, relu en E22h et
    // E42h (ou E62h) : 45h où la puce est montée, FFh ailleurs. L'AWE32 en 240h sans emu_addr : PCem 620h, la carte
    // 640h ; en 220h avec emu_addr = 660h : PCem 660h, la carte 620h. Témoin : 220h sans clé, 620h des deux côtés.
    private static int Pb289(bool hw)
    {
        int Un(string nom, string section, ushort[] ports, string pcem, string mat, bool temoin = false) =>
            AvecSb("sbawe32", _ =>
            {
                var v = string.Join(" ", ports.Select(p =>
                {
                    io.outw(p, 0x45);
                    return $"{p:X3}h={io.inw(p) & 0xff:X2}";
                }));
                var att = temoin || hw ? mat : pcem;
                return Voir(nom, v == att, v, att, hw);
            }, "[Sound Blaster AWE32]\nirq = 7\n" + section);
        var bad = Un("AWE32 en 240h, sans emu_addr", "addr = 576\n", [0xe22, 0xe42], "E22h=45 E42h=FF",
                     "E22h=FF E42h=45");
        bad += Un("AWE32 en 220h, emu_addr = 660h", "emu_addr = 1632\n", [0xe22, 0xe62], "E22h=FF E62h=45",
                  "E22h=45 E62h=FF");
        bad += Un("AWE32 en 220h, sans emu_addr (témoin)", "", [0xe22, 0xe42], "", "E22h=45 E42h=FF", temoin: true);
        return bad;
    }
}
