// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// Les cas de l'EMU8000 de l'AWE32 (G13.8f, PLAN-G13.md) : PB-158 (WC avance à la lecture), PB-159 (le bit 31 et le
// masque de 24 bits des adresses de la DRAM, relues en A22h) et PB-160 (HWCF3 sans la rustine), sur le banc du son
// (MaterielCasSon.cs). L'EMU8000 se conduit par ses ports de mots : le pointeur en E22h (le registre dans les bits 5 à 7,
// le canal dans les bits 0 à 4), Data1 en A20h, Data2 en A22h. Le temps de sortie se joue à la main : sound_pos_global
// avance d'un tic par échantillon à 48 kHz, et rien d'autre ne le bouge, le processeur arrêté. Les drapeaux de la DRAM
// de PCem (dmareadbit, dmawritebit) sont statiques et traversent les machines : chaque cas les remet à zéro.

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
}
