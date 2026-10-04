// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/cdrom/cdrom-ioctl-linux.c:23-24  (déclarées, et CD_STATUS_* :
//         includes/private/ibm.h:378-386)
// STATUS: partial — G10.3 : les deux globales du lecteur de CD-ROM, seules. Le pilote du lecteur
//         physique de l'hôte (ioctl_*) est exclu (PLAN.md : des images seulement, pour le déterminisme) ;
//         restent les variables que lisent cdrom-image.cc, et en G10.4 scsi_cd.c et pc.c.

namespace iXtal26.Cdrom;

internal static class cdrom_ioctl
{
    // pcem: cdrom-ioctl-linux.c:23-24 — -1 : aucun lecteur ; CDROM_IMAGE (ide.h:43) : une image.
    internal static int cdrom_drive;
    internal static int old_cdrom_drive;

    // pcem: ibm.h:382-386
    internal const int CD_STATUS_EMPTY = 0;
    internal const int CD_STATUS_DATA_ONLY = 1;
    internal const int CD_STATUS_PLAYING = 2;
    internal const int CD_STATUS_PAUSED = 3;
    internal const int CD_STATUS_STOPPED = 4;
}
