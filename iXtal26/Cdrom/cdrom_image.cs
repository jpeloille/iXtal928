// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/dosbox/cdrom_image.cpp + includes/private/dosbox/cdrom.h
// STATUS: deviated — G10.3 (PLAN-G10.md) : le moteur d'images de CD de DOSBox, entier — BinaryFile,
//         les pistes, l'ISO (LoadIsoFile, CanReadPVD), la feuille CUE (LoadCueSheet, AddTrack et ses
//         lecteurs), la lecture (ReadSector, ReadSectors). Omis : la classe abstraite CDROM_Interface
//         (cdrom.h:79-99, une seule implémentation) ; GetUPC, GetMediaTrayStatus, LoadUnloadMedia,
//         InitNewMedia et HasDataTrack (:92-96, :126-131, :151-153, :78-79, :452-459), qu'aucun code de
//         PCem n'appelle ; CDAudioCallBack (cdrom.h:154, déclaré, jamais défini) ; TCtrl et
//         CDROM_GetMountType (cdrom.h:72-77, inutilisés) ; le dirname de Win32 (:245-259).
//
// L'IFSTREAM EST MODÉLISÉ, bits d'état compris, d'après la libstdc++ de gcc 15 qui compile l'oracle
// (bits/istream.tcc, bits/sstream.tcc, bits/locale_facets.tcc, src/c++98/istream.cc) : un échec pose
// failbit, et failbit COLLE — seekg n'efface que eofbit (N3168), puis sa sentinelle refuse un flux qui
// n'est pas bon ; read, tellg et getline aussi. Une lecture qui atteint la fin copie ce qu'elle a lu et
// pose eofbit et failbit : le fichier ne se lit plus, sur toutes les pistes qui le partagent, jusqu'à
// sa fermeture (PB-109, « l'échec collant » de PLAN-G10.md). La feuille CUE se lit par
// un ifstream et des istringstream : mêmes règles, plus l'extraction d'un entier de num_get et le
// sscanf de la glibc.
//
// DEVIATIONS :
//   1. R9, PB-110 non reproduit (marqueurs ci-dessous) : deux plantages de l'hôte par une feuille CUE, une donnée de
//      l'utilisateur comme un .cfg (décision n° 4 de G10.3, 04/10) — une piste sans FILE lue
//      (ReadSector, :183) et une piste avant le premier FILE suivie d'une piste qui en a un
//      (AddTrack, :427). Survie : iXtal26.Diff r9-cue.
//   2. Le comportement indéfini rendu déterministe, des deux côtés (PB-111, décision n° 2 de G10.3) : les
//      variables automatiques non initialisées valent zéro (pvd[] de CanReadPVD, index de l'INDEX,
//      min/sec/fr de GetCueFrame) — l'oracle compile harness_cdrom.cpp avec
//      -ftrivial-auto-var-init=zero ; le tampon de new[] de ReadSectors vaut zéro — l'oracle le prend
//      d'un new[] enveloppé (--wrap=_Znam), calloc. C# initialise tout.
//   3. Les fichiers que PCem laisse ouverts (PB-112 ; LoadIsoFile refusé, :214 ; une feuille refusée après
//      des FILE) ne sont pas fermés à la main : le GC les finalise. FileShare.ReadWrite | Delete, sans
//      verrou, comme ifstream : un fichier encore ouvert n'empêche pas l'effacement, Windows compris.
//   4. Les chemins : PCem passe des octets à stat() et à l'ifstream. Les noms lus dans la feuille sont
//      gardés ici octet pour caractère (Latin-1), puis rendus en UTF-8 aux appels du système de
//      fichiers : identique pour tout nom en UTF-8 valide. Un FILE qui désigne un RÉPERTOIRE diverge :
//      ifstream l'ouvre sous Linux, FileStream le refuse (non couvert, PLAN-G10.md § les risques).

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace iXtal26.Cdrom;

// pcem: cdrom.h:66-70
internal struct TMSF
{
    internal uint8_t min;
    internal uint8_t sec;
    internal uint8_t fr;
}

internal sealed class CDROM_Interface_Image : IDisposable
{
    // pcem: cdrom.h:48-52
    internal const int RAW_SECTOR_SIZE = 2352;
    internal const int COOKED_SECTOR_SIZE = 2048;
    internal const int DATA_TRACK = 0x14;
    internal const int AUDIO_TRACK = 0x10;

    // pcem: cdrom.h:54-64
    internal const int CD_FPS = 75;

    internal static void FRAMES_TO_MSF(int f, ref TMSF t)
    {
        int value = f;
        t.fr = unchecked((uint8_t)(value % CD_FPS));
        value /= CD_FPS;
        t.sec = unchecked((uint8_t)(value % 60));
        value /= 60;
        t.min = unchecked((uint8_t)value);
    }

    internal static int MSF_TO_FRAMES(int m, int s, int f) => m * 60 * CD_FPS + s * CD_FPS + f;

    // pcem: cdrom_image.cpp:42-44
    private const int MAX_LINE_LENGTH = 512;
    private const int MAX_FILENAME_LENGTH = 256;

    // pcem: cdrom.h:103-108
    internal abstract class TrackFile : IDisposable
    {
        internal abstract bool read(uint8_t[] buffer, int o, int seek, int count);
        internal abstract int getLength();
        public abstract void Dispose();
    }

    // pcem: cdrom.h:110-120 — l'ifstream (:119) : un FileStream, et les trois bits d'un basic_ios.
    internal sealed class BinaryFile : TrackFile
    {
        private readonly FileStream? file;
        internal bool badbit, eofbit, failbit;

        // pcem: cdrom_image.cpp:48-51 — new ifstream(filename, ios::in | ios::binary) : un échec
        //   d'ouverture pose failbit.
        internal BinaryFile(string filename, out bool error)
        {
            try
            {
                file = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 0);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException
                                          or NotSupportedException)
            {
                failbit = true;
            }

            error = fail();
        }

        // pcem: cdrom_image.cpp:53-55
        public override void Dispose() => file?.Dispose();

        private bool good() => !badbit && !eofbit && !failbit;

        internal bool fail() => failbit || badbit;

        // basic_istream::sentry(in, noskipws = true) (istream.tcc:50-96) : un flux qui n'est pas bon
        // reçoit failbit, et l'opération ne se fait pas.
        private bool sentry()
        {
            if (good())
                return true;
            failbit = true;
            return false;
        }

        // basic_istream::seekg(off, dir) (istream.tcc:944-982) : eofbit effacé AVANT la sentinelle ;
        // basic_filebuf::seekoff rend -1 pour une position négative (lseek, EINVAL), d'où failbit. Une
        // position au-delà de la fin est permise.
        private void seekg(long off, SeekOrigin dir)
        {
            eofbit = false;
            if (!sentry())
                return;
            try
            {
                long p = dir == SeekOrigin.Begin ? off : file!.Length + off;
                if (p < 0)
                {
                    failbit = true;
                    return;
                }
                file!.Position = p;
            }
            catch (IOException)
            {
                badbit = true;
            }
        }

        // basic_istream::read (istream.tcc:707-732) : sgetn ; un compte court pose eofbit et failbit,
        // après avoir copié ce qui a été lu.
        private void read_(uint8_t[] s, int o, int n)
        {
            if (!sentry())
                return;
            int got = 0;
            try
            {
                while (got < n)
                {
                    int r = file!.Read(s, o + got, n - got);
                    if (r <= 0)
                        break;
                    got += r;
                }
            }
            catch (IOException)
            {
                badbit = true;
                return;
            }

            if (got != n)
            {
                eofbit = true;
                failbit = true;
            }
        }

        // basic_istream::tellg (istream.tcc:877-901) : la sentinelle aussi.
        private long tellg()
        {
            if (!sentry())
                return -1;
            return fail() ? -1 : file!.Position;
        }

        // pcem: cdrom_image.cpp:57-61
        // pcem bug, reproduced: PB-109 — pas de clear() avant seekg : après un échec, failbit reste posé, et toute
        //   lecture suivante du fichier échoue, sur toutes ses pistes, jusqu'à sa fermeture.
        internal override bool read(uint8_t[] buffer, int o, int seek, int count)
        {
            seekg(seek, SeekOrigin.Begin);
            read_(buffer, o, count);
            return !fail();
        }

        // pcem: cdrom_image.cpp:63-69
        // pcem bug, reproduced: PB-107 — (int) tronque une taille de plus de 2 Gio : négative jusqu'à 4 Gio,
        //   réduite modulo 4 Gio au-delà.
        // pcem bug, reproduced: PB-109 — sous failbit, seekg et tellg échouent : getLength rend -1 (:66-67).
        internal override int getLength()
        {
            seekg(0, SeekOrigin.End);
            int length = unchecked((int)tellg());
            if (fail())
                return -1;
            return length;
        }
    }

    // pcem: cdrom.h:122-132 — une valeur, copiée par push_back comme en C++.
    internal struct Track
    {
        internal int number;
        internal int track_number;
        internal int attr;
        internal int start;
        internal int length;
        internal int skip;
        internal int sectorSize;
        internal bool mode2;
        internal TrackFile? file;
    }

    // pcem: cdrom.h:167-169
    internal readonly List<Track> tracks = new();
    internal string mcn = "";

    // pcem: cdrom_image.cpp:74-76 — le destructeur, que `delete cdrom` appelle (cdrom-image.cc:450).
    public void Dispose() => ClearTracks();

    // pcem: cdrom_image.cpp:81-90
    internal bool SetDevice(string path, int forceCD)
    {
        if (LoadCueSheet(path))
            return true;
        if (LoadIsoFile(path))
            return true;

        // print error message on dosbox console
        //printf("Could not load image file: %s\n", path);
        return false;
    }

    // pcem: cdrom_image.cpp:98-103
    internal bool GetAudioTracks(ref int stTrack, ref int end, ref TMSF leadOut)
    {
        stTrack = 1;
        end = tracks.Count - 1;
        FRAMES_TO_MSF(tracks[tracks.Count - 1].start + 150, ref leadOut);
        return true;
    }

    // pcem: cdrom_image.cpp:105-112 — hors borne, rien n'est écrit (références C++).
    internal bool GetAudioTrackInfo(int track, ref int track_number, ref TMSF start, ref uint8_t attr)
    {
        if (track < 1 || track > tracks.Count)
            return false;
        FRAMES_TO_MSF(tracks[track - 1].start + 150, ref start);
        track_number = tracks[track - 1].track_number;
        attr = unchecked((uint8_t)tracks[track - 1].attr);
        return true;
    }

    // pcem: cdrom_image.cpp:114-124
    internal bool GetAudioSub(int sector, ref uint8_t attr, ref uint8_t track, ref uint8_t index, ref TMSF relPos,
                              ref TMSF absPos)
    {
        int cur_track = GetTrack(sector);
        if (cur_track < 1)
            return false;
        track = unchecked((uint8_t)cur_track);
        attr = unchecked((uint8_t)tracks[track - 1].attr);
        index = 1;
        FRAMES_TO_MSF(sector + 150, ref absPos);
        // pcem bug, reproduced: PB-123 — la position relative compte aussi les 150 secteurs de l'amorce (:122).
        FRAMES_TO_MSF(sector - tracks[track - 1].start + 150, ref relPos);
        return true;
    }

    // pcem: cdrom_image.cpp:133-149 — `buffer` est un PhysPt, ici (tableau, décalage).
    internal bool ReadSectors(uint8_t[] buffer, int o, bool raw, ulong sector, ulong num)
    {
        int sectorSize = raw ? RAW_SECTOR_SIZE : COOKED_SECTOR_SIZE;
        uint buflen = unchecked((uint)(num * (ulong)sectorSize));   // Bitu : unsigned int
        // pcem bug, not reproduced: PB-111 — neutralisé : zéro, des deux côtés (DEVIATION ci-dessous).
        // DEVIATION: PB-111 — new Bit8u[buflen] n'est pas initialisé en C, et memcpy le copie ENTIER,
        //   secteurs non lus compris (:145). Zéro ici ; zéro dans l'oracle (--wrap=_Znam, décision n° 2).
        uint8_t[] buf = new uint8_t[buflen];

        bool success = true; //Gobliiins reads 0 sectors
        for (ulong i = 0; i < num; i++)
        {
            success = ReadSector(buf, unchecked((int)(i * (ulong)sectorSize)), raw, sector + i);
            if (!success)
                break;
        }

        Array.Copy(buf, 0, buffer, o, buflen);
        return success;
    }

    // pcem: cdrom_image.cpp:155-167 — de begin() à end() - 1 : le lead-out n'est jamais rendu.
    internal int GetTrack(int sector)
    {
        for (int i = 0; i < tracks.Count - 1; i++)
        {
            Track curr = tracks[i];
            Track next = tracks[i + 1];
            if (curr.start <= sector && sector < next.start)
                return curr.number;
        }

        return -1;
    }

    // pcem: cdrom_image.cpp:169-184
    internal bool ReadSector(uint8_t[] buffer, int o, bool raw, ulong sector)
    {
        int track = GetTrack(unchecked((int)sector)) - 1;
        if (track < 0)
            return false;

        Track t = tracks[track];
        // int + unsigned long : le calcul se fait sur 64 bits non signés, puis se tronque dans l'int.
        int seek = unchecked((int)((ulong)(long)t.skip + (sector - (ulong)(long)t.start) * (ulong)(long)t.sectorSize));
        int length = raw ? RAW_SECTOR_SIZE : COOKED_SECTOR_SIZE;
        if (t.sectorSize != RAW_SECTOR_SIZE && raw)
            return false;
        if (t.sectorSize == RAW_SECTOR_SIZE && !t.mode2 && !raw)
            seek += 16;
        // pcem bug, reproduced: PB-106 — +24 est juste pour un secteur brut de 2 352 octets (12 de
        //   synchronisation, 4 d'en-tête, 8 de sous-en-tête), faux pour 2 336 (8 seulement) : une piste
        //   MODE2/2336 lit 16 octets trop loin.
        if (t.mode2 && !raw)
            seek += 24;

        // pcem bug, not reproduced: PB-110 (R9) — une piste sans fichier (une feuille CUE sans FILE avant
        //   elle) : NULL->read, l'hôte tombe. La lecture échoue ici.
        if (t.file is null)
            return false;
        return t.file.read(buffer, o, seek, length);
    }

    // pcem: cdrom_image.cpp:186-230
    private bool LoadIsoFile(string filename)
    {
        // pcem bug, reproduced: PB-112 — les fichiers d'une feuille refusée, oubliés ici sans être fermés (:187).
        tracks.Clear();

        // data track
        Track track = new();
        bool error;
        track.file = new BinaryFile(filename, out error);
        if (error)
        {
            track.file.Dispose();
            return false;
        }
        track.number = 1;
        track.track_number = 1;
        track.attr = DATA_TRACK;//data

        // try to detect iso type
        if (CanReadPVD(track.file, COOKED_SECTOR_SIZE, false))
        {
            track.sectorSize = COOKED_SECTOR_SIZE;
            track.mode2 = false;
        }
        else if (CanReadPVD(track.file, RAW_SECTOR_SIZE, false))
        {
            track.sectorSize = RAW_SECTOR_SIZE;
            track.mode2 = false;
        }
        else if (CanReadPVD(track.file, 2336, true))
        {
            track.sectorSize = 2336;
            track.mode2 = true;
        }
        else if (CanReadPVD(track.file, RAW_SECTOR_SIZE, true))
        {
            track.sectorSize = RAW_SECTOR_SIZE;
            track.mode2 = true;
        }
        // pcem bug, reproduced: PB-112 — l'image refusée garde son BinaryFile ouvert (:214-215).
        else
            return false;   // le BinaryFile n'est pas libéré (PB-112, DEVIATION n° 3 de l'en-tête)

        track.length = track.file.getLength() / track.sectorSize;
        tracks.Add(track);

        // leadout track
        track.number = 2;
        track.track_number = 0xAA;
        // pcem bug, reproduced: PB-122 — le lead-out sans ADR ni contrôle : READ TOC rend 00 dans son
        //   descripteur, où un vrai lecteur met ADR 1 et le contrôle de la dernière piste (14h, données).
        track.attr = 0;
        track.start = track.length;
        track.length = 0;
        track.file = null;
        tracks.Add(track);

        return true;
    }

    // pcem: cdrom_image.cpp:232-243
    private static bool CanReadPVD(TrackFile file, int sectorSize, bool mode2)
    {
        // pcem bug, not reproduced: PB-111 — neutralisé : zéro, des deux côtés (DEVIATION ci-dessous).
        // DEVIATION: PB-111 — pvd[] n'est pas initialisé en C ; une lecture courte en laisse tout ou
        //   partie indéterminé. Zéro, des deux côtés (décision n° 2).
        uint8_t[] pvd = new uint8_t[COOKED_SECTOR_SIZE];
        int seek = 16 * sectorSize;        // first vd is located at sector 16
        if (sectorSize == RAW_SECTOR_SIZE && !mode2)
            seek += 16;
        // pcem bug, reproduced: PB-106 — +24 aussi pour 2 336 : une vraie image MODE2/2336 n'est jamais
        //   reconnue.
        if (mode2)
            seek += 24;
        file.read(pvd, 0, seek, COOKED_SECTOR_SIZE);
        // pvd[0] = descriptor type, pvd[1..5] = standard identifier, pvd[6] = iso version (+8 for High Sierra)
        return (pvd[0] == 1 && Same(pvd, 1, "CD001") && pvd[6] == 1) ||
               (pvd[8] == 1 && Same(pvd, 9, "CDROM") && pvd[14] == 1);
    }

    // !strncmp((char *)&pvd[i], s, 5), s sans NUL.
    private static bool Same(uint8_t[] b, int i, string s)
    {
        for (int k = 0; k < s.Length; k++)
        {
            if (b[i + k] != s[k])
                return false;
        }

        return true;
    }

    // pcem: cdrom_image.cpp:261-393
    private bool LoadCueSheet(string cuefile)
    {
        Track track = new();
        tracks.Clear();
        int shift = 0;
        int currPregap = 0;
        int totalPregap = 0;
        int prestart = 0;
        bool success;
        bool canAddTrack = false;
        // safe_strncpy(tmp, cuefile, MAX_FILENAME_LENGTH), puis dirname (:270-272).
        string tmp = Octets(cuefile);
        if (tmp.Length > MAX_FILENAME_LENGTH - 1)
            tmp = tmp[..(MAX_FILENAME_LENGTH - 1)];
        string pathname = dirname(tmp);
        using CueStream in_ = new(cuefile);
        if (in_.fail())
            return false;

        while (!in_.eof())
        {
            // get next line
            string buf = in_.getline(MAX_LINE_LENGTH);
            if (in_.fail() && !in_.eof())
                return false;  // probably a binary file
            LineStream line = new(buf);

            string command = "";
            GetCueKeyword(ref command, line);

            if (command == "TRACK")
            {
                if (canAddTrack)
                    success = AddTrack(ref track, ref shift, prestart, ref totalPregap, currPregap);
                else
                    success = true;

                track.start = 0;
                track.skip = 0;
                currPregap = 0;
                prestart = 0;

                line.read(ref track.number);
                track.track_number = track.number;
                string type = "";
                GetCueKeyword(ref type, line);

                if (type == "AUDIO")
                {
                    track.sectorSize = RAW_SECTOR_SIZE;
                    track.attr = AUDIO_TRACK;
                    track.mode2 = false;
                }
                else if (type == "MODE1/2048")
                {
                    track.sectorSize = COOKED_SECTOR_SIZE;
                    track.attr = DATA_TRACK;
                    track.mode2 = false;
                }
                else if (type == "MODE1/2352")
                {
                    track.sectorSize = RAW_SECTOR_SIZE;
                    track.attr = DATA_TRACK;
                    track.mode2 = false;
                }
                else if (type == "MODE2/2336")
                {
                    track.sectorSize = 2336;
                    track.attr = DATA_TRACK;
                    track.mode2 = true;
                }
                else if (type == "MODE2/2352")
                {
                    track.sectorSize = RAW_SECTOR_SIZE;
                    track.attr = DATA_TRACK;
                    track.mode2 = true;
                }
                else
                    success = false;

                canAddTrack = true;
            }
            else if (command == "INDEX")
            {
                // pcem bug, not reproduced: PB-111 — neutralisé : zéro, des deux côtés (DEVIATION ci-dessous).
                // DEVIATION: PB-111 — `int index;` n'est pas initialisé en C (:330) — une ligne « INDEX »
                //   sans numéro le laisse tel. Zéro, des deux côtés (décision n° 2).
                int index = 0;
                line.read(ref index);
                int frame = 0;
                success = GetCueFrame(ref frame, line);

                if (index == 1)
                    track.start = frame;
                else if (index == 0)
                    prestart = frame;
                // ignore other indices
            }
            else if (command == "FILE")
            {
                if (canAddTrack)
                    success = AddTrack(ref track, ref shift, prestart, ref totalPregap, currPregap);
                else
                    success = true;
                canAddTrack = false;

                string filename = "";
                GetCueString(ref filename, line);
                GetRealFileName(ref filename, pathname);
                string type = "";
                GetCueKeyword(ref type, line);

                track.file = null;
                bool error = true;
                if (type == "BINARY")
                {
                    track.file = new BinaryFile(Chemin(filename), out error);
                }
                if (error)
                {
                    track.file?.Dispose();
                    success = false;
                }
            }
            else if (command == "PREGAP")
                success = GetCueFrame(ref currPregap, line);
            else if (command == "CATALOG")
                success = GetCueString(ref mcn, line);
            // ignored commands
            else if (command is "CDTEXTFILE" or "FLAGS" or "ISRC" or "PERFORMER" or "POSTGAP" or "REM"
                     or "SONGWRITER" or "TITLE" or "")
                success = true;
            // failure
            else
                success = false;

            if (!success)
                return false;
        }
        // add last track
        if (!AddTrack(ref track, ref shift, prestart, ref totalPregap, currPregap))
            return false;

        // add leadout track
        track.number++;
        track.track_number = 0xAA;
        // pcem bug, reproduced: PB-122 — comme LoadIsoFile : le lead-out sans ADR ni contrôle.
        track.attr = 0;//sync with load iso
        track.start = 0;
        track.length = 0;
        track.file = null;
        if (!AddTrack(ref track, ref shift, 0, ref totalPregap, 0))
            return false;

        return true;
    }

    // pcem: cdrom_image.cpp:395-450 — `curr` est la piste de LoadCueSheet elle-même (référence C++) :
    //   ses start et skip modifiés y restent.
    private bool AddTrack(ref Track curr, ref int shift, int prestart, ref int totalPregap, int currPregap)
    {
        // frames between index 0(prestart) and 1(curr.start) must be skipped
        int skip;
        // pcem bug, reproduced: PB-108 — un INDEX 00 en 00:00:00 ne compte pas (prestart > 0) : la piste
        //   d'un fichier qui commence par son prégap est lue en avance de la longueur de ce prégap.
        if (prestart > 0)
        {
            if (prestart > curr.start)
                return false;
            skip = curr.start - prestart;
        }
        else
            skip = 0;

        // first track (track number must be 1)
        if (tracks.Count == 0)
        {
            if (curr.number != 1)
                return false;
            curr.skip = skip * curr.sectorSize;
            curr.start += currPregap;
            totalPregap = currPregap;
            tracks.Add(curr);
            return true;
        }

        ref Track prev = ref CollectionsMarshal.AsSpan(tracks)[tracks.Count - 1];

        // current track consumes data from the same file as the previous
        if (prev.file == curr.file)
        {
            curr.start += shift;
            prev.length = curr.start + totalPregap - prev.start - skip;
            curr.skip += prev.skip + prev.length * prev.sectorSize + skip * curr.sectorSize;
            totalPregap += currPregap;
            curr.start += totalPregap;
            // current track uses a different file as the previous track
        }
        else
        {
            // pcem bug, not reproduced: PB-110 (R9) — une piste avant le premier FILE, puis une piste qui
            //   en a un : NULL->getLength(), l'hôte tombe pendant image_open. La feuille est refusée ici.
            if (prev.file is null)
                return false;
            int tmp = prev.file.getLength() - prev.skip;
            prev.length = tmp / prev.sectorSize;
            if (tmp % prev.sectorSize != 0)
                prev.length++; // padding

            curr.start += prev.start + prev.length + currPregap;
            curr.skip = skip * curr.sectorSize;
            shift += prev.start + prev.length;
            totalPregap = currPregap;
        }

        // error checks
        if (curr.number <= 1)
            return false;
        if (prev.number + 1 != curr.number)
            return false;
        if (curr.start < prev.start + prev.length)
            return false;
        if (curr.length < 0)
            return false;

        tracks.Add(curr);
        return true;
    }

    // pcem: cdrom_image.cpp:461-467
    internal bool HasAudioTracks()
    {
        foreach (Track t in tracks)
        {
            if (t.attr == AUDIO_TRACK)
                return true;
        }

        return false;
    }

    // pcem: cdrom_image.cpp:469-506 — la branche Unix.
    private static bool GetRealFileName(ref string filename, string pathname)
    {
        // check if file exists
        if (stat(filename))
            return true;

        // check if file with path relative to cue file exists
        string tmpstr = pathname + "/" + filename;
        if (stat(tmpstr))
        {
            filename = tmpstr;
            return true;
        }

        //Consider the possibility that the filename has a windows directory seperator (inside the CUE file)
        //which is common for some commercial rereleases of DOS games using DOSBox

        string copy = filename.Replace('\\', '/');

        if (stat(copy))
        {
            filename = copy;
            return true;
        }

        tmpstr = pathname + "/" + copy;
        if (stat(tmpstr))
        {
            filename = tmpstr;
            return true;
        }

        return false;
    }

    // stat(path) == 0 (:472) : un fichier ou un répertoire.
    private static bool stat(string octets)
    {
        string p = Chemin(octets);
        return p.Length > 0 && (File.Exists(p) || Directory.Exists(p));
    }

    // pcem: cdrom_image.cpp:508-514 — toupper de la locale « C » : a-z seulement.
    private static bool GetCueKeyword(ref string keyword, LineStream in_)
    {
        in_.read(ref keyword);
        var sb = new StringBuilder(keyword.Length);
        foreach (char c in keyword)
            sb.Append(c is >= 'a' and <= 'z' ? (char)(c - 'a' + 'A') : c);
        keyword = sb.ToString();

        return true;
    }

    // pcem: cdrom_image.cpp:516-524
    private static bool GetCueFrame(ref int frames, LineStream in_)
    {
        string msf = "";
        in_.read(ref msf);
        // pcem bug, not reproduced: PB-111 — neutralisé : zéro, des deux côtés (DEVIATION ci-dessous).
        // DEVIATION: PB-111 — min, sec et fr ne sont pas initialisés en C, et sscanf s'arrête au premier
        //   échec ; zéro, des deux côtés (décision n° 2).
        int min = 0, sec = 0, fr = 0;
        bool success = sscanf_msf(msf, ref min, ref sec, ref fr) == 3;
        frames = MSF_TO_FRAMES(min, sec, fr);

        return success;
    }

    // pcem: cdrom_image.cpp:526-541
    private static bool GetCueString(ref string str, LineStream in_)
    {
        int pos = in_.tellg();
        in_.read(ref str);
        if ((str.Length > 0 ? str[0] : '\0') == '"')
        {
            if (str[^1] == '"')
            {
                // str.assign(str, 1, str.size() - 2) : pour « " » seul, size() - 2 déborde en npos — vide.
                str = str.Length >= 2 ? str.Substring(1, str.Length - 2) : "";
            }
            else
            {
                in_.seekg(pos);
                in_.getline(MAX_FILENAME_LENGTH, '"');        // skip
                str = in_.getline(MAX_FILENAME_LENGTH, '"');
            }
        }
        return true;
    }

    // pcem: cdrom_image.cpp:543-557
    private void ClearTracks()
    {
        TrackFile? last = null;
        foreach (Track curr in tracks)
        {
            if (curr.file != last)
            {
                curr.file?.Dispose();
                last = curr.file;
            }
        }

        tracks.Clear();
    }

    // ---- Les chemins (DEVIATION n° 4) ---------------------------------------------------------------

    // Un chemin C# en octets UTF-8, un octet par caractère.
    private static string Octets(string s) => Encoding.Latin1.GetString(Encoding.UTF8.GetBytes(s));

    // L'inverse, au seuil du système de fichiers.
    private static string Chemin(string octets) => Encoding.UTF8.GetString(Encoding.Latin1.GetBytes(octets));

    // dirname de la glibc (misc/dirname.c) : « . » sans barre, les barres finales ignorées, « / » pour la
    // racine, « // » pour exactement deux barres en tête.
    private static string dirname(string path)
    {
        int last_slash = path.LastIndexOf('/');

        if (last_slash > 0 && last_slash == path.Length - 1)
        {
            /* Determine whether all remaining characters are slashes.  */
            int runp;

            for (runp = last_slash; runp != 0; --runp)
            {
                if (path[runp - 1] != '/')
                    break;
            }

            /* The '/' is the last character, we have to look further.  */
            if (runp != 0)
                last_slash = path.LastIndexOf('/', runp - 1);
        }

        if (last_slash >= 0)
        {
            /* Determine whether all remaining characters are slashes.  */
            int runp;

            for (runp = last_slash; runp != 0; --runp)
            {
                if (path[runp - 1] != '/')
                    break;
            }

            /* Terminate the path.  */
            if (runp == 0)
            {
                /* The last slash is the first character in the string.  We have to
                   return "/".  As a special case we have to return "//" if there
                   are exactly two slashes at the beginning of the string.  */
                return path[..(last_slash == 1 ? 2 : 1)];
            }

            return path[..runp];
        }

        return ".";
    }

    // ---- Les flux de la feuille --------------------------------------------------------------------

    private static bool isspace(int c) => c is ' ' or '\t' or '\n' or '\v' or '\f' or '\r';

    // L'ifstream de LoadCueSheet (:273-283), en mode texte — sans traduction sous Linux.
    private sealed class CueStream : IDisposable
    {
        private readonly FileStream? f;
        private bool bad, eofbit, failbit;
        private int peek = -2;     // -2 : rien de lu d'avance ; -1 : la fin

        internal CueStream(string path)
        {
            try
            {
                f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException
                                          or NotSupportedException)
            {
                failbit = true;
            }
        }

        public void Dispose() => f?.Dispose();

        internal bool fail() => failbit || bad;
        internal bool eof() => eofbit;
        private bool good() => !bad && !eofbit && !failbit;

        private int sgetc()
        {
            if (peek == -2)
                peek = f!.ReadByte();
            return peek;
        }

        private int snextc()
        {
            peek = -2;
            return sgetc();
        }

        // basic_istream<char>::getline(s, n) (src/c++98/istream.cc), délimiteur « \n » : au plus n - 1
        // octets ; le délimiteur consommé ; la fin pose eofbit ; n - 1 octets sans délimiteur, failbit ;
        // rien d'extrait, failbit. Rend la chaîne C que lit l'istringstream de :284 — jusqu'au premier NUL.
        internal string getline(int n)
        {
            var s = new StringBuilder();
            int gcount = 0;
            if (good())
            {
                try
                {
                    int c = sgetc();
                    while (gcount + 1 < n && c != -1 && c != '\n')
                    {
                        s.Append((char)c);
                        gcount++;
                        c = snextc();
                    }

                    if (c == -1)
                        eofbit = true;
                    else if (c == '\n')
                    {
                        gcount++;
                        peek = -2;
                    }
                    else
                        failbit = true;
                }
                catch (IOException)
                {
                    bad = true;
                }
            }
            else
                failbit = true;

            if (gcount == 0)
                failbit = true;

            string ligne = s.ToString();
            int nul = ligne.IndexOf('\0');
            return nul < 0 ? ligne : ligne[..nul];
        }
    }

    // L'istringstream d'une ligne (:284), et ce qu'en lisent GetCueKeyword, GetCueFrame et GetCueString.
    private sealed class LineStream
    {
        private readonly string s;
        private int pos;
        private bool eofbit, failbit;

        internal LineStream(string s) => this.s = s;

        private bool good() => !eofbit && !failbit;
        private bool fail() => failbit;

        // basic_istream::sentry (istream.tcc:50-96) : sans noskip, les blancs sautés — la fin atteinte en
        // les sautant pose eofbit, puis failbit.
        private bool sentry(bool noskip)
        {
            if (good() && !noskip)
            {
                while (pos < s.Length && isspace(s[pos]))
                    pos++;
                if (pos == s.Length)
                    eofbit = true;
            }

            if (good())
                return true;
            failbit = true;
            return false;
        }

        // operator>>(istream&, string&) (bits/basic_string.tcc) : jusqu'au prochain blanc ; la chaîne reste
        // telle quelle si la sentinelle échoue.
        internal void read(ref string str)
        {
            if (!sentry(false))
                return;
            int start = pos;
            while (pos < s.Length && !isspace(s[pos]))
                pos++;
            str = s[start..pos];
            if (pos == s.Length)
                eofbit = true;
            if (pos == start)
                failbit = true;
        }

        // basic_istream::operator>>(int&) (istream.tcc) sur num_get::_M_extract_int<long>
        // (locale_facets.tcc), base 10, locale « C » : signe, zéros de tête, chiffres ; aucun chiffre,
        // 0 et failbit ; débordement, la borne et failbit ; puis la borne de l'int. Inchangé si la
        // sentinelle échoue.
        internal void read(ref int n)
        {
            if (!sentry(false))
                return;

            bool testeof = pos == s.Length;
            bool negative = false;
            char c = '\0';
            if (!testeof)
            {
                c = s[pos];
                negative = c == '-';
                if (negative || c == '+')
                {
                    if (++pos != s.Length)
                        c = s[pos];
                    else
                        testeof = true;
                }
            }

            bool found_zero = false;
            int sep_pos = 0;
            while (!testeof)
            {
                if (c == '.' || c != '0')
                    break;
                found_zero = true;
                ++sep_pos;
                if (++pos != s.Length)
                    c = s[pos];
                else
                    testeof = true;
            }

            ulong max = negative ? 9223372036854775808UL : 9223372036854775807UL;
            ulong smax = max / 10;
            ulong result = 0;
            bool testoverflow = false;
            while (!testeof)
            {
                int digit = c is >= '0' and <= '9' ? c - '0' : -1;
                if (digit == -1)
                    break;
                if (result > smax)
                    testoverflow = true;
                else
                {
                    result *= 10;
                    testoverflow |= result > max - (ulong)digit;
                    result += (ulong)digit;
                    ++sep_pos;
                }

                if (++pos != s.Length)
                    c = s[pos];
                else
                    testeof = true;
            }

            long l;
            bool echec = false;
            if (sep_pos == 0 && !found_zero)
            {
                l = 0;
                echec = true;
            }
            else if (testoverflow)
            {
                l = negative ? long.MinValue : long.MaxValue;
                echec = true;
            }
            else
                l = negative ? unchecked((long)(0UL - result)) : (long)result;

            if (l < int.MinValue)
            {
                echec = true;
                n = int.MinValue;
            }
            else if (l > int.MaxValue)
            {
                echec = true;
                n = int.MaxValue;
            }
            else
                n = (int)l;

            if (echec)
                failbit = true;
            if (testeof)
                eofbit = true;
        }

        // basic_istream::tellg (istream.tcc:877-901) : la sentinelle (un eofbit seul y pose failbit), puis
        // stringbuf::seekoff(0, cur).
        internal int tellg()
        {
            if (!sentry(true))
                return -1;
            return fail() ? -1 : pos;
        }

        // basic_istream::seekg(off, ios::beg) (istream.tcc:944-982) : eofbit effacé d'abord ;
        // stringbuf::seekoff (sstream.tcc:172-215) refuse une position hors de [0, taille].
        internal void seekg(int off)
        {
            eofbit = false;
            if (!sentry(true))
                return;
            if (off >= 0 && off <= s.Length)
                pos = off;
            else
                failbit = true;
        }

        // basic_istream<char>::getline(s, n, delim) (src/c++98/istream.cc) — les règles de CueStream.getline.
        internal string getline(int n, char delim)
        {
            var sb = new StringBuilder();
            int gcount = 0;
            if (sentry(true))
            {
                int c = pos < s.Length ? s[pos] : -1;
                while (gcount + 1 < n && c != -1 && c != delim)
                {
                    sb.Append((char)c);
                    gcount++;
                    pos++;
                    c = pos < s.Length ? s[pos] : -1;
                }

                if (c == -1)
                    eofbit = true;
                else if (c == delim)
                {
                    gcount++;
                    pos++;
                }
                else
                    failbit = true;
            }

            if (gcount == 0)
                failbit = true;
            return sb.ToString();
        }
    }

    // sscanf(s, "%d:%d:%d", ...) de la glibc (stdio-common/vfscanf-internal.c) : chaque %d saute les
    // blancs, prend un signe puis au moins un chiffre, convertit par strtol (saturé à la borne du long)
    // et range (int) ; « : » doit suivre tel quel. Rend le nombre de conversions, -1 si l'entrée finit
    // avant la première.
    private static int sscanf_msf(string s, ref int a, ref int b, ref int c)
    {
        int pos = 0, n = 0;
        for (int k = 0; k < 3; k++)
        {
            if (k > 0)
            {
                if (pos >= s.Length || s[pos] != ':')
                    return n;
                pos++;
            }

            while (pos < s.Length && isspace(s[pos]))
                pos++;
            if (pos >= s.Length)
                return n == 0 ? -1 : n;
            int start = pos;
            if (s[pos] is '+' or '-')
                pos++;
            int chiffres = pos;
            while (pos < s.Length && s[pos] is >= '0' and <= '9')
                pos++;
            if (pos == chiffres)
                return n;

            if (!long.TryParse(s[start..pos], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long v))
                v = s[start] == '-' ? long.MinValue : long.MaxValue;
            int iv = unchecked((int)v);
            if (k == 0)
                a = iv;
            else if (k == 1)
                b = iv;
            else
                c = iv;
            n++;
        }

        return n;
    }
}
