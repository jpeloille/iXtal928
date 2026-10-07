// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le mode matériel, figé (G13 ; PLAN-G13.md, § Le mécanisme ; R10 de TRANSCRIPTION.md).
// STATUS: host
//
// Un champ par défaut de PCem corrigé en mode matériel, et RIEN d'autre. Le constructeur statique, explicite, copie la
// demande de ModeMateriel une seule fois, quand ModeMateriel.Figer() le lance, avant le premier cœur. Le dépôt compile
// sans paliers : chaque méthode gardée l'est une fois, après le gel, et en Release le JIT plie `if (materiel.pb_nn)` en
// constante ; la branche morte n'est pas même importée. Le mode PCem ne coûte donc rien, et tools/listings-jit.sh le
// prouve, méthode par méthode, contre la référence M0. Une garde lit le champ directement, jamais par une locale ni
// une propriété (le JIT n'y plierait plus rien), et elle se tient seule sur sa ligne (R2). Aucune écriture après le
// gel : ni réflexion ni Unsafe.AsRef. Des champs, jamais un tableau : un tableau ne se plie pas.
//
// Le constructeur ne lève jamais : un type dont l'initialiseur a levé reste à vie non initialisé.

namespace iXtal26;

internal static class materiel
{
    /// <summary>PB-01 : l'AF d'ADC et de SBB du 8088 et du 8086 (Cpu/808x.cs, Cpu/808x.Materiel.cs).</summary>
    internal static readonly bool pb_01;

    static materiel()
    {
        ModeMateriel.Gel();
        pb_01 = ModeMateriel.Demande(1);
    }
}
