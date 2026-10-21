// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
//
// nvr-check (GR.3c) — --make-nvr carte par carte. Pour chaque carte du registre vidéo, le CMOS d'un ami286 qui la
// porte : les bits 5-4 de l'octet d'équipement (0x14) doivent dire 10 pour la CGA (20h), 11 pour la MDA et
// l'Hercules (30h), 00 pour une carte à ROM d'extension (l'EGA, la VGA, les SVGA) — le POST de l'AMI les compare à la
// carte qu'il trouve (« CMOS display type mismatch ») —, et la somme de 0x10 à 0x2D doit être rangée en 0x2E-0x2F et
// non nulle (les deux BIOS refusent la somme nulle, NvrImage.Checksum). Puis une carte inconnue, refusée (2).

using iXtal26.Host;

namespace iXtal26.Diff;

internal static class NvrCheck
{
    internal static int Run(string romsPath)
    {
        var d = Path.Combine(Path.GetTempPath(), $"nvr-check-{Environment.ProcessId}");
        Directory.CreateDirectory(d);
        var faux = 0;
        try
        {
            foreach (var carte in Video.video.video_cards)
            {
                var attendu = carte.internal_name switch { "cga" => 0x20, "mda" or "hercules" => 0x30, _ => 0x00 };
                var r = Fabrique(d, carte.internal_name, romsPath, out var cmos);
                var bits = cmos is null ? -1 : cmos[0x14] & 0x30;
                var somme = cmos is null ? 0 : NvrImage.Checksum(cmos);
                var rangee = cmos is null ? -1 : cmos[0x2E] << 8 | cmos[0x2F];
                var juste = r == 0 && bits == attendu && somme != 0 && rangee == somme;
                faux += juste ? 0 : 1;
                Console.WriteLine($"  {(juste ? "juste" : "FAUX ")} {carte.internal_name,-12} 0x14 & 30h = " +
                                  $"{bits:X2}h (attendu {attendu:X2}h), somme {somme:X4}h, rangée {rangee:X4}h");
            }

            var inconnue = Fabrique(d, "carte-inconnue", romsPath, out _);
            faux += inconnue == 2 ? 0 : 1;
            Console.WriteLine($"  {(inconnue == 2 ? "juste" : "FAUX ")} une carte inconnue : --make-nvr rend " +
                              $"{inconnue} (attendu 2)");
        }
        finally
        {
            Directory.Delete(d, true);
        }

        Console.WriteLine(faux == 0
            ? $"nvr-check : les {Video.video.video_cards.Length} cartes du registre ont leur octet 0x14 et leur " +
              "somme ; la carte inconnue refusée."
            : $"nvr-check : {faux} cas en défaut.");
        return faux == 0 ? 0 : 1;
    }

    private static int Fabrique(string d, string carte, string romsPath, out byte[]? cmos)
    {
        var cfg = Path.Combine(d, $"{carte}.cfg");
        var nvr = Path.Combine(d, $"{carte}.nvr");
        File.WriteAllText(cfg,
                          $"model = ami286\nmem_size = 4096\ndrive_a_type = 5\ndrive_b_type = 2\ngfxcard = {carte}\n");
        var r = NvrImage.Make(cfg, nvr, romsPath, force: true);
        cmos = r == 0 && File.Exists(nvr) ? File.ReadAllBytes(nvr) : null;
        return r;
    }
}
