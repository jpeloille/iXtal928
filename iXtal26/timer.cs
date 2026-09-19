// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/includes/private/timer.h  (macros de comparaison)
// STATUS: partial — stub de M1. Le vrai timer.c (liste triée, 32:32 sur le TSC)
//         arrive à M2.
//
// timer_target au maximum : aucun timer n'échoit, donc clockhardware() fait
// avancer le TSC sans jamais déclencher de rappel.

using iXtal26.Diag;

namespace iXtal26;

internal static partial class timer
{
    internal static uint64_t tsc;
    internal static uint32_t timer_target = 0x7FFFFFFF;

    // pcem: timer.h:59
    //   #define TIMER_VAL_LESS_THAN_VAL(a, b) ((int32_t)((a) - (b)) <= 0)
    //
    // Ordre CIRCULAIRE, pas total : avec un écart supérieur à 2^31 on construit
    // a<b, b<c, c<a. C'est aussi pourquoi le vrai timer.c gardera sa liste
    // chaînée à M2 — PriorityQueue et SortedSet corrompent leur tas en silence
    // sur un comparateur pareil. `unchecked` est explicite au site, comme
    // documentation de l'intention.
    internal static bool TIMER_VAL_LESS_THAN_VAL(uint32_t a, uint32_t b)
        => unchecked((int32_t)(a - b)) <= 0;

    internal static void timer_process() => Counters.n_timer_process++;
}
