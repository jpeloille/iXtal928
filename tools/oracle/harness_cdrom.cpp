/* SPDX-FileCopyrightText: 2026 Julien Peloille
 * SPDX-License-Identifier: GPL-2.0-only
 *
 * G10.3 — le moteur d'images de CD (PLAN-G10.md) : dosbox/cdrom_image.cpp (CDROM_Interface_Image) et
 * cdrom/cdrom-image.cc (le pilote ATAPI des images), INCLUS ET NON LIÉS, comme DBOPL
 * (harness_dbopl.cpp) : l'état du pilote est static au fichier (image_changed, cdrom_capacity,
 * image_cd_*, cd_buffer), et la porte cdimage-check doit le lire et le remettre à zéro.
 *
 * `#define private public` autour de cdrom.h SEUL : la table des pistes, mcn et l'ifstream de chaque
 * BinaryFile sont privés, et la porte les compare au C#. Tous les en-têtes standard sont inclus
 * AVANT, pour que la macro ne touche qu'eux.
 *
 * Le comportement indéfini rendu déterministe, des deux côtés (décision n° 2 de G10.3, 04/10) :
 *   - ce fichier, et lui seul, se compile avec -ftrivial-auto-var-init=zero (Makefile) : les
 *     variables automatiques non initialisées des deux fichiers inclus valent zéro — pvd[] de
 *     CanReadPVD, index de l'INDEX, min/sec/fr de GetCueFrame, attr d'is_track_audio et de
 *     playaudio, les champs de getcurrentsubchannel ;
 *   - le new[] de ReadSectors (cdrom_image.cpp:136) passe par __wrap__Znam, un calloc (Makefile,
 *     -Wl,--wrap=_Znam) : les secteurs non lus valent zéro. Aucun autre objet de l'oracle ne
 *     référence _Znam (nm -u, VERIFICATION.md § G10.3).
 * Le C# fait de même (iXtal26/Cdrom/cdrom_image.cs, cdrom-image.cs).
 *
 * Les feuilles r9-*.cue d'isogen font tomber ce code (cdrom_image.cpp:183, :427) : la porte ne les
 * lui donne jamais (r9-cue, C# seul).
 */

#include <cctype>
#include <climits>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <fstream>
#include <iostream>
#include <limits>
#include <new>
#include <sstream>
#include <string>
#include <vector>
#include <libgen.h>
#include <limits.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>
#include <sys/stat.h>

#define private public
#include "dosbox/cdrom.h"
#undef private

#include "../../pcem-dev/src/dosbox/cdrom_image.cpp"
#include "../../pcem-dev/src/cdrom/cdrom-image.cc"

extern "C" {

/* cdrom-ioctl-linux.c:23-24 — le pilote du lecteur de CD de l'hôte n'est pas lié (PLAN.md : des
 * images seulement) ; ses deux globales, si : cdrom-image.cc les lit (:186-188, :461). */
int cdrom_drive;
int old_cdrom_drive;

int cdrom_null_open(char d);

/* -Wl,--wrap=_Znam : tout new[] de l'oracle arrive ici — celui de ReadSectors seul. Un calloc : le
 * tampon vaut zéro, comme new byte[] en C#. delete[] reste celui de libstdc++, qui rend à free. */
void *__wrap__Znam(size_t n) {
        void *p = calloc(n ? n : 1, 1);
        if (!p)
                throw std::bad_alloc();
        return p;
}

/* Les entrées de la table ATAPI, dans l'ordre de ide_atapi.h:8-26. */
enum {
        H_CD_READY, H_CD_MEDIUM_CHANGED, H_CD_READTOC, H_CD_READTOC_SESSION, H_CD_READTOC_RAW,
        H_CD_SUBCHANNEL, H_CD_READSECTOR, H_CD_READSECTOR_RAW, H_CD_PLAYAUDIO, H_CD_SEEK, H_CD_LOAD,
        H_CD_EJECT, H_CD_PAUSE, H_CD_RESUME, H_CD_SIZE, H_CD_STATUS, H_CD_IS_TRACK_AUDIO, H_CD_STOP,
        H_CD_EXIT
};

static ATAPI *h_cd_harnais;     /* `atapi` au premier h_cd_reset, rendu par h_cd_fin */
static int h_cd_garde;
static ATAPI *h_cd_nulle;       /* la table que pose cdrom_null_open (static dans cdrom-null.c) */

/* Le début d'un cas : l'état statique du pilote à zéro (pendant d'image_clear_state_for_oracle_parity),
 * les deux globales du lecteur à zéro, atapi nul comme au lancement de PCem (ide_atapi.c:26). */
void h_cd_reset(void) {
        if (!h_cd_garde) {
                h_cd_harnais = atapi;
                h_cd_garde = 1;
        }
        image_close();
        image_path[0] = 0;
        image_changed = 0;
        cdrom_capacity = 0;
        image_cd_state = CD_STOPPED;
        image_cd_pos = image_cd_end = 0;
        memset(cd_buffer, 0, sizeof(cd_buffer));
        cd_buflen = 0;
        cdrom_drive = old_cdrom_drive = 0;
        atapi = NULL;
}

/* La fin de la porte : l'image fermée, atapi rendu au harnais. */
void h_cd_fin(void) {
        image_close();
        if (h_cd_garde)
                atapi = h_cd_harnais;
}

int h_cd_open(const char *path) { return image_open((char *)path); }

void h_cd_close(void) { image_close(); }

void h_cd_null_open(void) {
        cdrom_null_open(0);
        h_cd_nulle = atapi;
}

void h_cd_set_drive(int drive, int old) {
        cdrom_drive = drive;
        old_cdrom_drive = old;
}

void h_cd_audio_callback(int16_t *out, int len) { image_audio_callback(out, len); }
void h_cd_audio_stop(void) { image_audio_stop(); }   /* G10.5 — sound_reset (sound.c:267) */

/* G10.4 — l'amorçage (PLAN-G10.md). La configuration du lecteur, posée par h_set_cdrom (harness.c) avant
 * h_boot : cdrom_drive (pc.c:702) et cdrom_path (pc.c:707-711), bornée à 1 023 octets par le C#. */
static int h_cdrom_drive_cfg = -1;
static char h_cdrom_path_cfg[1024];

void h_cd_config(int drive, const char *path) {
        h_cdrom_drive_cfg = drive;
        snprintf(h_cdrom_path_cfg, sizeof(h_cdrom_path_cfg), "%s", path ? path : "");
}

/* ORACLE PARITY, pendant de la remise à zéro d'initpc côté C# (pc.cs) : l'état statique du pilote et les deux
 * globales du lecteur repartent de zéro à chaque amorçage, la configuration reposée, atapi nul comme au
 * lancement de PCem (ide_atapi.c:26). */
void h_cd_boot_raz(void) {
        image_close();
        image_changed = 0;
        cdrom_capacity = 0;
        image_cd_state = CD_STOPPED;
        image_cd_pos = image_cd_end = 0;
        memset(cd_buffer, 0, sizeof(cd_buffer));
        cd_buflen = 0;
        cdrom_drive = h_cdrom_drive_cfg;
        old_cdrom_drive = 0;
        strcpy(image_path, h_cdrom_path_cfg);
        atapi = NULL;
}

void cdrom_null_reset(void);

/* pc.c:297-312 (__unix) — la branche CDROM_IMAGE. ioctl_set_drive (:312) : le lecteur physique, que la
 * configuration écarte (pc.cs). Une image illisible laisse atapi tel quel, comme chez PCem ; le C# s'en écarte
 * (PB-116), et aucune porte ne la donne à l'oracle. */
static void h_cd_image(void) {
        if (cdrom_drive == CDROM_IMAGE) {
                FILE *ff = fopen(image_path, "rb");
                if (ff) {
                        fclose(ff);
                        image_open(image_path);
                } else {
                        cdrom_drive = -1;
                        cdrom_null_open(cdrom_drive);
                }
        }
}

/* pc.c:291-313 — le bloc CD d'initpc. */
void h_cd_initpc(void) {
        if (cdrom_drive == -1)
                cdrom_null_open(cdrom_drive);
        else
                h_cd_image();
}

/* pc.c:411-433 — le bloc CD de resetpchard. */
void h_cd_resetpchard(void) {
        image_close();
        if (cdrom_drive == -1)
                cdrom_null_reset();
        else
                h_cd_image();
}

/* pc.c:321-333 — le reset du pilote, à la fin d'initpc ; vide des deux sorts. */
void h_cd_initpc_fin(void) {
        if (cdrom_drive == -1)
                cdrom_null_reset();
        else
                image_reset();
}

/* pc.c:578 — atapi->exit(), dans closepc. */
void h_cd_exit(void) { atapi->exit(); }

/* Le pilote posé, pour --expect-cd : 0 aucun, 1 le lecteur vide (cdrom-null.c), 2 l'image. */
int h_cd_driver(void) { return !atapi ? 0 : atapi == &image_atapi ? 2 : 1; }

/* Une entrée de la table atapi ; buf + off est le tampon. INT64_MIN si atapi est nul. */
int64_t h_cd_call(int op, int64_t a, int64_t b, int64_t c, int64_t d, uint8_t *buf, int off) {
        uint8_t *p = buf ? buf + off : NULL;

        if (!atapi)
                return INT64_MIN;
        switch (op) {
        case H_CD_READY:
                return atapi->ready();
        case H_CD_MEDIUM_CHANGED:
                return atapi->medium_changed();
        case H_CD_READTOC:
                return atapi->readtoc(p, (uint8_t)a, (int)b, (int)c, (int)d);
        case H_CD_READTOC_SESSION:
                return atapi->readtoc_session(p, (int)a, (int)b);
        case H_CD_READTOC_RAW:
                return atapi->readtoc_raw(p, (int)a);
        case H_CD_SUBCHANNEL:
                return atapi->getcurrentsubchannel(p, (int)a);
        case H_CD_READSECTOR:
                return atapi->readsector(p, (int)a, (int)b);
        case H_CD_READSECTOR_RAW:
                atapi->readsector_raw(p, (int)a);
                return 0;
        case H_CD_PLAYAUDIO:
                atapi->playaudio((uint32_t)a, (uint32_t)b, (int)c);
                return 0;
        case H_CD_SEEK:
                atapi->seek((uint32_t)a);
                return 0;
        case H_CD_LOAD:
                atapi->load();
                return 0;
        case H_CD_EJECT:
                atapi->eject();
                return 0;
        case H_CD_PAUSE:
                atapi->pause();
                return 0;
        case H_CD_RESUME:
                atapi->resume();
                return 0;
        case H_CD_SIZE:
                return atapi->size();
        case H_CD_STATUS:
                return atapi->status();
        case H_CD_IS_TRACK_AUDIO:
                return atapi->is_track_audio((uint32_t)a, (int)b);
        case H_CD_STOP:
                atapi->stop();
                return 0;
        case H_CD_EXIT:
                atapi->exit();
                return 0;
        }
        return INT64_MIN + 1;
}

static uint64_t h_cd_fnv(const uint8_t *p, size_t n) {
        uint64_t h = 0xcbf29ce484222325ull;
        for (size_t i = 0; i < n; i++) {
                h ^= p[i];
                h *= 0x100000001b3ull;
        }
        return h;
}

/* L'état du pilote et du moteur, en entiers : 12 scalaires, puis 10 par piste — number,
 * track_number, attr, start, length, skip, sectorSize, mode2, le rang du fichier (dans l'ordre de
 * première apparition, -1 sans fichier) et l'état de son ifstream (rdstate : 1 bad, 2 eof, 4 fail ;
 * -1 sans fichier). image_path et mcn en chaînes. Rend le nombre d'entiers écrits, -1 si max ne
 * suffit pas. */
int h_cd_state(int64_t *v, int max, char *path, int pathmax, char *mcn, int mcnmax) {
        int n = 0;
        int nt = cdrom ? (int)cdrom->tracks.size() : -1;
        std::vector<CDROM_Interface_Image::TrackFile *> vus;

        if (max < 12 + 10 * (nt > 0 ? nt : 0))
                return -1;
        v[n++] = cdrom != NULL;
        v[n++] = !atapi ? 0 : atapi == h_cd_nulle ? 1 : atapi == &image_atapi ? 2 : atapi == h_cd_harnais ? 3 : 4;
        v[n++] = image_changed;
        v[n++] = cdrom_capacity;
        v[n++] = image_cd_state;
        v[n++] = image_cd_pos;
        v[n++] = image_cd_end;
        v[n++] = cd_buflen;
        v[n++] = cdrom_drive;
        v[n++] = old_cdrom_drive;
        v[n++] = (int64_t)h_cd_fnv((const uint8_t *)cd_buffer, sizeof(cd_buffer));
        v[n++] = nt;
        for (int i = 0; i < nt; i++) {
                CDROM_Interface_Image::Track &t = cdrom->tracks[i];
                int rang = -1;

                if (t.file) {
                        for (rang = 0; rang < (int)vus.size() && vus[rang] != t.file; rang++)
                                ;
                        if (rang == (int)vus.size())
                                vus.push_back(t.file);
                }
                v[n++] = t.number;
                v[n++] = t.track_number;
                v[n++] = t.attr;
                v[n++] = t.start;
                v[n++] = t.length;
                v[n++] = t.skip;
                v[n++] = t.sectorSize;
                v[n++] = t.mode2;
                v[n++] = rang;
                v[n++] = t.file ? (int64_t)((CDROM_Interface_Image::BinaryFile *)t.file)->file->rdstate() : -1;
        }
        snprintf(path, pathmax, "%s", image_path);
        snprintf(mcn, mcnmax, "%s", cdrom ? cdrom->mcn.c_str() : "");
        return n;
}

}
