// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// r9-joycfg — la clé joystick_type et la section [Joysticks] du .cfg (G10.1), en C# SEUL.
//
// Chez PCem, joystick_type indexe joystick_list sans borne (pc.c:786, gameport.c:27, :132) : une
// valeur hors des sept types écrite à la main fait tomber l'émulateur. Depuis G10.1 (traitement
// de PB-93), elle est ramenée au type 0 avec un avertissement ; les numéros de [Joysticks] hors
// borne aussi (R9 ; leur lecture, joystick_poll, est prouvée par iXtal26 --joystick-check). Essais,
// sur un 5150 amorcé (le port jeu de xt_init), chacun avec son verdict : le type retenu, le nombre
// d'avertissements, puis le port 201h conduit (une écriture qui arme les quatre chronomètres, des
// lectures, 200 tranches) — la survie ; sinon l'exception nommée.

using iXtal26.Joystick;

namespace iXtal26.Diff;

internal static class R9JoyCfg
{
    internal static int Run(string romsPath)
    {
        var bad = 0;
        var n = 0;
        var essais = new (string nom, string corps, int type, int nr0, int nr1, int avertis)[]
        {
            ("dans la liste : 6 (TM FCS)", "joystick_type = 6\n", 6, 0, 0, 0),
            ("PB-93 : 7", "joystick_type = 7\n", 0, 0, 0, 1),
            ("PB-93 : 99", "joystick_type = 99\n", 0, 0, 0, 1),
            ("PB-93 : -1", "joystick_type = -1\n", 0, 0, 0, 1),
            ("SideWinder, manettes 1 et 9", "joystick_type = 5\n\n[Joysticks]\njoystick_0_nr = 1\njoystick_1_nr = 9\n", 5, 1, 0, 1),
            ("CH, correspondances hors borne",
             "joystick_type = 4\n\n[Joysticks]\njoystick_0_nr = 1\njoystick_0_axis_0 = 8\njoystick_0_button_3 = 40\n" +
             "joystick_0_pov_0_x = 268435456\n", 4, 1, 0, 3),
        };
        foreach (var (nom, corps, type, nr0, nr1, avertis) in essais)
            bad += Essai(romsPath, nom, corps, avertis, n++, () =>
            {
                var lu = $"type {gameport.joystick_type}, manettes {plat_joystick.joystick_state[0].plat_joystick_nr} " +
                         $"et {plat_joystick.joystick_state[1].plat_joystick_nr}";
                Conduire();
                return gameport.joystick_type == type && plat_joystick.joystick_state[0].plat_joystick_nr == nr0 &&
                       plat_joystick.joystick_state[1].plat_joystick_nr == nr1
                    ? null
                    : $"ATTENDU type {type}, manettes {nr0} et {nr1} (lu {lu})";
            }, () => $"type {gameport.joystick_type} (« {gameport.joystick_get_name(gameport.joystick_type)} »)");

        Console.WriteLine(bad == 0 ? "r9-joycfg : tout survit, hors borne → défaut averti." : $"r9-joycfg : {bad} essai(s) en défaut.");
        return bad == 0 ? 0 : 1;
    }

    /// <summary>Un essai : écrit le .cfg, amorce en captant la sortie d'erreur, compte les
    /// avertissements « hors des », puis `verif` (null : conforme). Rend 0 ou 1.</summary>
    private static int Essai(string romsPath, string nom, string corps, int avertis, int n,
                             Func<string?> verif, Func<string> valeurs)
    {
        var cfg = Path.Combine(Path.GetTempPath(), $"r9-joycfg-{Environment.ProcessId}-{n}.cfg");
        File.WriteAllText(cfg, "model = ibmpc\n" + corps);
        var err = Console.Error;
        var capte = new StringWriter();
        try
        {
            for (var c = 0; c < plat_joystick.MAX_JOYSTICKS; c++)
                plat_joystick.joystick_state[c].plat_joystick_nr = 0;
            Console.SetError(capte);
            if (!pc.loadconfig(cfg))
                throw new InvalidOperationException("loadconfig");
            Floppy.fdd_c.discfns[0] = "";
            Floppy.fdd_c.discfns[1] = "";
            if (!pc.initpc(romsPath))
                throw new InvalidOperationException("initpc");
            var faute = verif();
            Console.SetError(err);
            var lignes = capte.ToString().Split('\n').Where(l => l.Contains(" : hors des ")).ToList();
            foreach (var l in lignes)
                Console.WriteLine($"    {l.Trim()}");
            if (faute is null && lignes.Count != avertis)
                faute = $"{lignes.Count} avertissement(s), ATTENDU {avertis}";
            Console.WriteLine($"  {nom} : survit — {valeurs()}{(faute is null ? "" : $" ; {faute}")}");
            return faute is null ? 0 : 1;
        }
        catch (Exception e)
        {
            Console.SetError(err);
            Console.WriteLine($"  {nom} : ARRÊT — {e.GetType().Name} : {e.Message.Trim()}");
            return 1;
        }
        finally
        {
            Console.SetError(err);
            File.Delete(cfg);
        }
    }

    /// <summary>Le port jeu conduit : 50 tranches d'amorçage, une écriture en 201h (les quatre
    /// chronomètres d'axe armés, la manette avertie), des lectures, puis 150 tranches où les
    /// chronomètres tombent.</summary>
    private static void Conduire()
    {
        for (var i = 0; i < 50; i++)
            pc.runpc();
        io.outb(0x201, 0);
        for (var i = 0; i < 64; i++)
            _ = io.inb(0x201);
        for (var i = 0; i < 150; i++)
            pc.runpc();
        _ = io.inb(0x201);
    }
}
