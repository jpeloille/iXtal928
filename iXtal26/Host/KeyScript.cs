// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
// STATUS: host
//
// La table de frappe, et la CADENCE de frappe, partagées.
//
// Deux appelants : BootTest (--boot --type, le cœur C# seul) et BootDiff
// (boot-diff --type, les deux cœurs côte à côte). Ils DOIVENT taper la même
// chose aux mêmes tranches — une table dupliquée qui dériverait ferait comparer
// deux frappes différentes, et le diff appellerait ça une divergence de cœur.
//
// L'injection se fait dans keyboard.rawinputkey[] — exactement le tableau que
// Host/SdlKeyboard remplit depuis la pompe SDL, et que h_rawinputkey remplit
// côté oracle. Seule la livraison d'évènements par SDL est court-circuitée :
// elle exige une fenêtre focalisée, et un gestionnaire de fenêtres n'est pas un
// oracle. Tout le reste est exercé pour de vrai — keyboard_poll_host,
// keyboard_process, les tables de scancodes, keyboard_xt, l'IRQ 1, l'INT 9 du
// BIOS, le tampon clavier de la BDA, et l'écho de l'application.

using SDL3;

namespace iXtal26.Host;

public static class KeyScript
{
    /// <summary>
    /// Tranches pendant lesquelles une touche reste dans son état. Une touche doit
    /// rester enfoncée assez longtemps pour que keyboard_process la voie : il
    /// compare pcem_key[] à oldkey[] une fois par tranche, donc au moins une
    /// tranche appuyée et une relâchée. Quatre laisse de la marge sans allonger
    /// inutilement les campagnes de diff.
    /// </summary>
    public const int SlicesPerStep = 4;

    /// <summary>
    /// Tranches laissées à l'application après Entrée. Deux secondes émulées : un
    /// DIR sur disquette doit lire la FAT et le répertoire, relance du moteur
    /// comprise. Un FORMAT en demande beaucoup plus — c'est à l'appelant de le
    /// dire, ceci n'est que le défaut.
    /// </summary>
    public const int SlicesAfterLine = 200;

    /// <summary>
    /// Indice dans rawinputkey[] pour un caractère, ou -1 s'il n'est pas mappé.
    /// <paramref name="shift"/> dit si la touche exige Maj.
    /// </summary>
    public static int IndexFor(char ch, out bool shift)
    {
        shift = false;
        var sc = ScancodeFor(ch, ref shift);
        if (sc == SDL.Scancode.Unknown)
            return -1;
        return SdlKeyboard.MapScancode(sc);
    }

    /// <summary>Indice de la touche Maj gauche, ou -1.</summary>
    public static int ShiftIndex() => SdlKeyboard.MapScancode(SDL.Scancode.LShift);

    /// <summary>Sous-ensemble ASCII suffisant pour une ligne de BASIC ou de DOS. Pas
    /// une table de clavier : juste de quoi rendre le test lisible.</summary>
    private static SDL.Scancode ScancodeFor(char ch, ref bool shift)
    {
        if (ch is >= 'A' and <= 'Z')
        {
                shift = true;
                return SDL.Scancode.A + (ch - 'A');
        }

        if (ch is >= 'a' and <= 'z')
                return SDL.Scancode.A + (ch - 'a');
        if (ch is >= '1' and <= '9')
                return SDL.Scancode.Alpha1 + (ch - '1');

        switch (ch)
        {
        case '0': return SDL.Scancode.Alpha0;
        // Échap, écrit « \e ». Ajouté pour sortir des menus plein écran comme celui
        // de FDISK. NON EXERCÉ par la campagne de § M12, et c'est une correction :
        // le commentaire d'origine annonçait ici une touche sans laquelle le chemin
        // d'écriture du disque dur restait hors d'atteinte. Faux — FDISK redémarre
        // la machine de lui-même une fois la partition écrite, et l'arc complet
        // n'envoie jamais Échap. La touche reste, elle n'a simplement pas le rôle
        // qu'on lui prêtait.
        case '\e': return SDL.Scancode.Escape;
        // F1, ecrite « \1 ». Ajoutee pour repondre a l'invite du POST de l'IBM AT :
        // « 161-System Options Not Set-(Run SETUP) / (RESUME = "F1" KEY) ». Sans elle,
        // un AT sans fichier at.nvr s'arrete la et ne demarre jamais — ce n'est pas une
        // panne mais une question, et il faut pouvoir y repondre.
        case '\u0001': return SDL.Scancode.F1;
        case ' ': return SDL.Scancode.Space;
        case '\n': return SDL.Scancode.Return;
        case '.': return SDL.Scancode.Period;
        case ',': return SDL.Scancode.Comma;
        case '-': return SDL.Scancode.Minus;
        case '"': shift = true; return SDL.Scancode.Apostrophe;
        case '*': shift = true; return SDL.Scancode.Alpha8;
        case '+': shift = true; return SDL.Scancode.Equals;
        case '(': shift = true; return SDL.Scancode.Alpha9;
        case ')': shift = true; return SDL.Scancode.Alpha0;
        case '?': shift = true; return SDL.Scancode.Slash;
        // Sans les deux suivants, aucune commande DOS ne peut désigner un lecteur ni
        // porter un commutateur : « FORMAT B: » arrivait en « FORMAT B », et DOS 2.00
        // répondait « Invalid parameter ». C'est ce qui bloquait la vérification du
        // chemin d'écriture sans fenêtre (VERIFICATION.md § M8.1).
        case ':': shift = true; return SDL.Scancode.Semicolon;
        case '/': return SDL.Scancode.Slash;
        // G4.7 — la Calculatrice de Windows 3.1 : « @ » est sa racine carrée, « = » son
        // égal. Disposition US, celle que SYSTEM.INI déclare sur les disques de ce dépôt.
        case '@': shift = true; return SDL.Scancode.Alpha2;
        case '=': return SDL.Scancode.Equals;
        default: return SDL.Scancode.Unknown;
        }
    }

    /// <summary>Un changement d'état de touche, à une tranche donnée.</summary>
    public readonly struct Event
    {
        public Event(int slice, int index, int value) { Slice = slice; Index = index; Value = value; }
        public int Slice { get; }
        public int Index { get; }
        public int Value { get; }
    }

    /// <summary>
    /// Déroule les lignes à taper en une suite d'évènements horodatés EN TRANCHES.
    /// Le calendrier est purement arithmétique : il ne dépend d'aucun état de la
    /// machine, donc les deux côtés du diff le reconstruisent à l'identique sans
    /// avoir à se parler. C'est ce qui rend la frappe comparable.
    ///
    /// Maj est posée dans une passe SÉPARÉE, avant la touche. keyboard_process
    /// balaie pcem_key[] de 0 à 271 et émet dans cet ordre : appuyer Maj (0x2A) et
    /// « 8 » (0x09) dans la même passe enverrait le scancode de « 8 » AVANT celui de
    /// Maj, et l'INT 9 du BIOS lirait un « 8 » au lieu d'une « * ». Mesuré : sans
    /// cette séparation, « PRINT 6*7 » arrive en « print 687 ».
    /// </summary>
    /// <param name="startSlice">Tranche de la première frappe : le temps d'amorcer
    /// et d'atteindre une invite.</param>
    /// <param name="settle">Tranches laissées à l'application après chaque Entrée.</param>
    /// <returns>Les évènements, et la tranche à laquelle tout est fini.</returns>
    public static (List<Event> events, int endSlice) Build(
        IReadOnlyList<string> lines, int startSlice, int settle = SlicesAfterLine)
    {
        var events = new List<Event>();
        var slice = startSlice;
        var shiftIdx = ShiftIndex();

        foreach (var line in lines)
        {
            foreach (var ch in line + "\n")
            {
                var idx = IndexFor(ch, out var shift);
                if (idx < 0)
                {
                    Console.Error.WriteLine($"caractère non mappé, ignoré : « {ch} »");
                    continue;
                }

                if (shift && shiftIdx >= 0)
                {
                    events.Add(new Event(slice, shiftIdx, 1));
                    slice += SlicesPerStep;
                }

                events.Add(new Event(slice, idx, 1));
                slice += SlicesPerStep;
                events.Add(new Event(slice, idx, 0));
                slice += SlicesPerStep;

                if (shift && shiftIdx >= 0)
                {
                    events.Add(new Event(slice, shiftIdx, 0));
                    slice += SlicesPerStep;
                }
            }

            slice += settle;
        }

        return (events, slice);
    }
}
