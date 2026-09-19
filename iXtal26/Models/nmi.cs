// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/models/nmi.c
// STATUS: partial — seul nmi_mask, que la fin de boucle d'execx86 consulte
//         (808x.c:3953). Les handlers d'E/S du NMI arrivent à M3 avec xt_init().

namespace iXtal26.Models;

internal static partial class nmi
{
    // pcem: nmi.c:5
    internal static int nmi_mask;
}
