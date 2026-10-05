// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — instrumentation des gardes R9 (TRANSCRIPTION.md, R9).
// STATUS: host — pas de code PCem transcrit.
//
// Chaque garde R9 se signale ici par le site de PCem qu'elle remplace, « fichier.c:ligne ». Les portes
// r9-* vérifient ainsi que chaque scénario passe bien par la garde qu'il vise : une survie qui n'y est
// pas passée ne prouve rien. Hors de ces portes, personne ne lit ces comptes, et une garde n'est
// atteinte que sur un chemin où PCem s'arrêterait.

namespace iXtal26.Diag;

internal static class R9
{
    private static readonly Dictionary<string, int> Atteints = new();

    internal static void Garde(string site) => Atteints[site] = Atteints.GetValueOrDefault(site) + 1;

    internal static int Compte(string site) => Atteints.GetValueOrDefault(site);

    internal static void Raz() => Atteints.Clear();
}
