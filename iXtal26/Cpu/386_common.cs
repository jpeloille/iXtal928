// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/cpu/386_common.c  (lignes 6-37)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — palier (a) : seuls cpu_state et les globaux que 808x.c et
//         x86seg.c consomment réellement. x86_int et le reste arrivent au
//         palier (b).
//
// C'est ici que PCem définit `cpu_state`, malgré que son type vive dans x86.h.
// On garde ce découpage plutôt que de tout rassembler dans x86.cs : c'est ce qui
// fait qu'un `grep -n cpu_state` tombe au même endroit des deux côtés.

namespace iXtal26.Cpu;

internal static partial class _386_common
{
    // pcem: 386_common.c:6
    internal static readonly cpu_state_t cpu_state = new();

    // pcem: 386_common.c:28
    internal static int nmi_enable = 1;
}
