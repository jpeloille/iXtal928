// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/devices/cassette.c
// STATUS: partial — interface seulement, sans lecteur.
//
// Le 5150 a un port cassette, interrogé par le PPI (port 0x62 bit 4) et par le
// BIOS au démarrage. Sans cassette branchée, l'entrée reste basse et le BIOS
// passe à la suite — c'est le comportement d'une machine sans lecteur, pas une
// omission qui casse l'amorçage.

namespace iXtal26.Devices;

internal static partial class cassette
{
    // pcem: cassette.c — niveau de l'entrée cassette. Pas de lecteur : toujours 0.
    internal static int cassette_input() => 0;

    // pcem: cassette.c — commande du moteur, depuis le port 0x61 bit 3.
    internal static void cassette_set_motor(int on) { }
}
