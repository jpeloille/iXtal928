// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// Les cas du compte 0 du 8254 dans le haut-parleur (G13.8g1, PLAN-G13.md, Q13) : PB-21 (speakval, le rapport des
// comptes des canaux 2 et 0) et PB-293 (la sortie de speaker_update hors des modes 0 et 4). Le 5150, l'UC arrêtée
// (XebecBanc) : le PIT et le PPI conduits par leurs ports. Le compte 0 vaut 2^16 (8254, 231164-005, PDF p. 17 :
// « The largest possible initial count is 0; this is equivalent to 2^16 for binary counting »), comme pit_load et
// pit_speaker_timer le lisent déjà.

using System.Reflection;
using iXtal26.Sound;

namespace iXtal26.Diff;

internal static partial class MaterielCas
{
    private static void Ports(params int[] o)
    {
        for (var i = 0; i < o.Length; i += 2)
            io.outb((ushort)o[i], (byte)o[i + 1]);
    }

    // PB-21 — speakval (pit.c:418) divise par l[0] sans la convention. Le canal 0 à 0 (43h ← 36h, 40h ← 00h 00h), le
    // canal 2 en mode 0 à FFFFh (43h ← B0h, 42h ← FFh FFh) : PCem +inf, l'entier indéfini (80000000h) ; le 8254
    // 65 535 / 65 536, 1FFFh. Puis le canal 2 à 0 : PCem 0/0, 80000000h ; le 8254 65 536 / 65 536, 2000h (la convention
    // au numérateur aussi). Témoin : 8000h et 4000h, aucun compte nul, 0 des deux côtés.
    private static int Pb21(bool hw) => Avec(tmp =>
    {
        XebecBanc.Demarrer("roms", Cfg(tmp, "model = ibmpc\nmem_size = 640\ngfxcard = cga\n"));
        var bad = 0;
        void Un(string nom, int[] ports, int pcem, int mat, bool temoin = false)
        {
            Ports(ports);
            var v = sound_speaker.speakval;
            var att = temoin || hw ? mat : pcem;
            bad += Voir(nom, v == att, $"{v:X8}", $"{att:X8}", hw);
        }
        Un("canal 0 à 0, canal 2 en mode 0 à FFFFh", [0x43, 0x36, 0x40, 0x00, 0x40, 0x00, 0x43, 0xB0, 0x42, 0xFF, 0x42,
            0xFF], int.MinValue, 0x1FFF);
        Un("puis le canal 2 à 0", [0x42, 0x00, 0x42, 0x00], int.MinValue, 0x2000);
        Un("canal 0 à 8000h, canal 2 à 4000h (témoin)", [0x40, 0x00, 0x40, 0x80, 0x42, 0x00, 0x42, 0x40], 0, 0,
           temoin: true);
        return bad + VoirArret();
    });

    /// <summary>Quatre échantillons de speaker_update, au début de son tampon (privé : lu par réflexion).</summary>
    private static string Echantillons()
    {
        const BindingFlags Prive = BindingFlags.NonPublic | BindingFlags.Static;
        typeof(sound_speaker).GetField("speaker_pos", Prive)!.SetValue(null, 0);
        sound.sound_pos_global = 4;
        sound_speaker.speaker_update();
        var b = (short[])typeof(sound_speaker).GetField("speaker_buffer", Prive)!.GetValue(null)!;
        return string.Join(" ", b.Take(4).Select(x => $"{(ushort)x:X4}"));
    }

    // PB-293 — hors des modes 0 et 4, speaker_update (sound_speaker.c:25-26) sort 0A00h, un niveau continu, pour un
    // compte du canal 2 sous 40h ; le compte 0 (2^16, 18,2 Hz) y tombe. Le canal 2 en mode 3 à 0 (43h ← B6h, 42h ← 00h
    // 00h), le haut-parleur ouvert (61h ← 03h) : PCem 0A00h ; le 8254, le carré de speakon (1400h, puis 0000h, speakon
    // posé à 1 puis à 0, la sortie de pit_speaker_timer). Même chose en mode 2 (43h ← B4h). Témoins, égaux dans les
    // deux modes : le compte 20h et le compte 3Fh en mode 3, 0A00h (la correction ne touche que le compte 0) ; le mode
    // 0 à 0, speakval posé à 1234h, la sortie de speakval (les modes 0 et 4 hors de la correction).
    private static int Pb293(bool hw) => Avec(tmp =>
    {
        XebecBanc.Demarrer("roms", Cfg(tmp, "model = ibmpc\nmem_size = 640\ngfxcard = cga\n"));
        var bad = 0;
        void Un(string nom, int[] ports, string? temoin = null)
        {
            Ports(ports);
            Ports(0x61, 0x03);
            foreach (var on in new[] { 1, 0 })
            {
                sound_speaker.speakon = on;
                if (temoin == "1234")
                    sound_speaker.speakval = 0x1234;
                var v = Echantillons();
                var un = temoin ?? (hw ? (on != 0 ? "1400" : "0000") : "0A00");
                var att = string.Join(" ", Enumerable.Repeat(un, 4));
                bad += Voir($"{nom}, speakon = {on}", v == att, v, att, hw);
            }
        }
        Un("canal 2 en mode 3 à 0", [0x43, 0xB6, 0x42, 0x00, 0x42, 0x00]);
        Un("canal 2 en mode 2 à 0", [0x43, 0xB4, 0x42, 0x00, 0x42, 0x00]);
        Un("canal 2 en mode 3 à 20h (témoin)", [0x43, 0xB6, 0x42, 0x20, 0x42, 0x00], "0A00");
        Un("canal 2 en mode 3 à 3Fh (témoin)", [0x43, 0xB6, 0x42, 0x3F, 0x42, 0x00], "0A00");
        Un("canal 2 en mode 0 à 0, speakval = 1234h (témoin)", [0x43, 0xB0, 0x42, 0x00, 0x42, 0x00], "1234");
        return bad + VoirArret();
    });
}
