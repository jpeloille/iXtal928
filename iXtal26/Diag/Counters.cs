// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: tools/oracle/harness_stubs.c (les compteurs h_n_*)
// STATUS: host — instrumentation, pas de code PCem transcrit.
//
// Compteurs d'appels des stubs — mitigation du risque n°3 du plan.
//
// Le cas dangereux n'est pas le désaccord entre le C# et l'oracle : il est
// bruyant. C'est l'accord vide — les deux côtés stubent à la même constante,
// donc une divergence dans le chemin qui la consomme est masquée. « Aucun côté
// n'a jamais appelé inb » se lit exactement comme « les deux sont d'accord ».
//
// Ces compteurs entrent dans le vecteur d'état diffé. Un stub resté à zéro des
// deux côtés alors que le test devait l'exercer fait donc ÉCHOUER la passe au
// lieu de la faire passer.

namespace iXtal26.Diag;

internal static class Counters
{
    internal static uint64_t n_inb, n_outb;
    internal static uint64_t n_picint, n_picinterrupt;
    internal static uint64_t n_timer_process;
    internal static uint64_t n_readmembl, n_writemembl, n_readmemwl, n_writememwl;
    internal static uint64_t n_fatal;

    internal static void Reset()
    {
        n_inb = n_outb = 0;
        n_picint = n_picinterrupt = 0;
        n_timer_process = 0;
        n_readmembl = n_writemembl = n_readmemwl = n_writememwl = 0;
        n_fatal = 0;
    }
}
