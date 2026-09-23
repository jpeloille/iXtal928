// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/rtc.c  (en entier, lignes 17-222)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — B1 : l'horloge INTERNE et ses conversions. time_internal_sync
//         reste dehors : elle lit l'heure de l'HÔTE, ce qu'aucun oracle ne peut
//         comparer — voir le registre des omissions.
//
// L'HORLOGE DU MC146818, ET POURQUOI ELLE EST INTERNE.
//
// PCem tient sa propre horloge — `internal_clock` — au lieu de lire celle de la
// machine hôte à chaque tic. C'est ce qui rend l'émulation REPRODUCTIBLE : deux
// exécutions partant du même état donnent la même heure. `time_internal_sync`
// est le seul point qui touche l'hôte, et il n'est appelé que sur demande
// explicite de l'utilisateur.
//
// C'est aussi ce qui permet à cette transcription d'exister : une horloge qui
// lirait DateTime.Now ne pourrait jamais être comparée à l'oracle.
//
// DEUX REPRÉSENTATIONS, ET LE REGISTRE B DÉCIDE. Le bit RTC_DM dit si les
// champs sont en binaire ou en DCB ; le bit RTC_2412 dit si l'heure est sur 24
// ou 12 heures. Les quatre combinaisons existent, et time_set_nvrram les
// traite toutes — c'est la moitié de son volume.

using static iXtal26.rtc_h;

// CS8981 : « le nom de type ne contient que des minuscules ». Le dépôt nomme ses
// classes d'après le FICHIER C dont elles viennent — rtc.c donne rtc, comme io.c
// donne io et pic.c donne pic. C'est la règle de nommage verbatim, et elle prime
// sur une convention C# que ce code n'a pas vocation à suivre.
#pragma warning disable CS8981

namespace iXtal26;

/// <summary>pcem: includes/private/rtc.h — les constantes du MC146818.</summary>
internal static class rtc_h
{
    // pcem: rtc.h:3-4
    internal static uint8_t BCD(int X) => (uint8_t)((X % 10) | ((X / 10) << 4));
    internal static int DCB(int X) => ((X & 0xF0) >> 4) * 10 + (X & 0x0F);

    // pcem: rtc.h:6-20 — enum RTC_ADDR. Ce sont des INDICES dans nvrram[].
    internal const int RTC_SECONDS = 0;
    internal const int RTC_ALARMSECONDS = 1;
    internal const int RTC_MINUTES = 2;
    internal const int RTC_ALARMMINUTES = 3;
    internal const int RTC_HOURS = 4;
    internal const int RTC_ALARMHOURS = 5;
    internal const int RTC_DOW = 6;
    internal const int RTC_DOM = 7;
    internal const int RTC_MONTH = 8;
    internal const int RTC_YEAR = 9;
    internal const int RTC_REGA = 10;
    internal const int RTC_REGB = 11;
    internal const int RTC_REGC = 12;
    internal const int RTC_REGD = 13;

    // pcem: rtc.h:22-27. Le siècle est en 0x32 et le MC146818 y charge 20 tout
    // seul quand l'année passe de 99 à 00 — le bit de poids fort excepté.
    internal const int RTC_CENTURY = 0x32;
    internal const uint8_t RTC_AMPM = 0b10000000;

    // pcem: rtc.h:30-60 — registre A
    internal const uint8_t RTC_RS = 0b1111;
    internal const uint8_t RTC_DV0 = 0b1110000;
    internal const uint8_t RTC_UIP = 0b10000000;

    // pcem: rtc.h:62-118 — registre B
    internal const uint8_t RTC_DSE = 0b1;
    internal const uint8_t RTC_2412 = 0b10;
    internal const uint8_t RTC_DM = 0b100;
    internal const uint8_t RTC_SQWE = 0b1000;
    internal const uint8_t RTC_UIE = 0b10000;
    internal const uint8_t RTC_AIE = 0b100000;
    internal const uint8_t RTC_PIE = 0b1000000;
    internal const uint8_t RTC_SET = 0b10000000;

    // pcem: rtc.h:120-152 — registre C
    internal const uint8_t RTC_RC = 0b1111;
    internal const uint8_t RTC_UF = 0b10000;
    internal const uint8_t RTC_AF = 0b100000;
    internal const uint8_t RTC_PF = 0b1000000;
    internal const uint8_t RTC_IRQF = 0b10000000;

    // pcem: rtc.h:154-165 — registre D
    internal const uint8_t RTC_RD = 0b1111111;
    internal const uint8_t RTC_VRT = 0b10000000;
}

internal static class rtc
{
    // pcem: rtc.c:17
    internal static int enable_sync;

    // pcem: rtc.c:19-27 — l'horloge interne, en clair et non en DCB.
    private struct InternalClock { internal int sec, min, hour, mday, mon, year; }
    private static InternalClock internal_clock;

    // pcem: rtc.c:30
    private static readonly int[] rtc_days_in_month = [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];

    // pcem: rtc.c:32-41
    private static int rtc_is_leap(int org_year)
    {
        if (org_year % 400 == 0)
                return 1;
        if (org_year % 100 == 0)
                return 0;
        if (org_year % 4 == 0)
                return 1;
        return 0;
    }

    // pcem: rtc.c:43-49
    private static int rtc_get_days(int org_month, int org_year)
    {
        if (org_month != 2)
                return rtc_days_in_month[org_month - 1];
        else
                return rtc_is_leap(org_year) != 0 ? 29 : 28;
    }

    // pcem: rtc.c:51-73 — la propagation des retenues.
    //
    // ELLE NE BOUCLE PAS : chaque test est un `if`, pas un `while`. Avancer de
    // plus d'une seconde d'un coup ne propagerait donc PAS correctement — mais
    // rtc_tick n'avance que d'une seconde, et c'est son seul appelant.
    private static void rtc_recalc()
    {
        if (internal_clock.sec == 60)
        {
                internal_clock.sec = 0;
                internal_clock.min++;
        }
        if (internal_clock.min == 60)
        {
                internal_clock.min = 0;
                internal_clock.hour++;
        }
        if (internal_clock.hour == 24)
        {
                internal_clock.hour = 0;
                internal_clock.mday++;
        }
        if (internal_clock.mday == (rtc_get_days(internal_clock.mon, internal_clock.year) + 1))
        {
                internal_clock.mday = 1;
                internal_clock.mon++;
        }
        if (internal_clock.mon == 13)
        {
                internal_clock.mon = 1;
                internal_clock.year++;
        }
    }

    // pcem: rtc.c:75-78
    internal static void rtc_tick()
    {
        internal_clock.sec++;
        rtc_recalc();
    }

    // pcem: rtc.c:81-117 — appelé quand le logiciel ÉCRIT un champ de date.
    //
    // Les cas 1, 3 et 5 — les trois champs d'ALARME — ne sont pas ici : c'est
    // writenvr qui les écarte avant d'appeler (nvr.c:180). L'alarme ne touche
    // pas l'horloge.
    internal static void time_update(uint8_t[] nvrram, int reg)
    {
        int temp;

        switch (reg)
        {
        case RTC_SECONDS:
                internal_clock.sec = (nvrram[RTC_REGB] & RTC_DM) != 0 ? nvrram[RTC_SECONDS] : DCB(nvrram[RTC_SECONDS]);
                break;
        case RTC_MINUTES:
                internal_clock.min = (nvrram[RTC_REGB] & RTC_DM) != 0 ? nvrram[RTC_MINUTES] : DCB(nvrram[RTC_MINUTES]);
                break;
        case RTC_HOURS:
                temp = (nvrram[RTC_REGB] & RTC_DM) != 0 ? nvrram[RTC_HOURS] : DCB(nvrram[RTC_HOURS]);

                if ((nvrram[RTC_REGB] & RTC_2412) != 0)
                        internal_clock.hour = temp;
                else
                        internal_clock.hour = ((temp & ~RTC_AMPM) % 12) + ((temp & RTC_AMPM) != 0 ? 12 : 0);
                break;
        case RTC_DOM:
                internal_clock.mday = (nvrram[RTC_REGB] & RTC_DM) != 0 ? nvrram[RTC_DOM] : DCB(nvrram[RTC_DOM]);
                break;
        case RTC_MONTH:
                internal_clock.mon = (nvrram[RTC_REGB] & RTC_DM) != 0 ? nvrram[RTC_MONTH] : DCB(nvrram[RTC_MONTH]);
                break;
        case RTC_YEAR:
                internal_clock.year = (nvrram[RTC_REGB] & RTC_DM) != 0 ? nvrram[RTC_YEAR] : DCB(nvrram[RTC_YEAR]);
                internal_clock.year += (nvrram[RTC_REGB] & RTC_DM) != 0 ? 1900 : (DCB(nvrram[RTC_CENTURY]) * 100);
                break;
        case RTC_CENTURY:
                if ((nvrram[RTC_REGB] & RTC_DM) != 0)
                        return;
                internal_clock.year %= 100;
                internal_clock.year += (DCB(nvrram[RTC_CENTURY]) * 100);
                break;
        }
    }

    /// <summary>pcem: rtc.c:119-128 — le jour de la semaine, par la congruence
    /// de Zeller simplifiée.
    ///
    /// LE `+6` N'EST PAS DÉCORATIF, et le commentaire de PCem le dit : la somme
    /// modulo 7 rend 0 pour SAMEDI, et il faut 0 pour DIMANCHE.</summary>
    private static int time_week_day()
    {
        int day_of_month = internal_clock.mday;
        int month2 = internal_clock.mon;
        int year2 = internal_clock.year % 100;
        int century = ((internal_clock.year - year2) / 100) % 4;
        int sum = day_of_month + month2 + year2 + century;
        /* (Sum mod 7) gives 0 for Saturday, we need it for Sunday, so +6 for Saturday to get 6 and Sunday 0 */
        int raw_wd = ((sum + 6) % 7);
        return raw_wd;
    }

    /// <summary>Le `struct tm` de la bibliothèque C, réduit aux sept champs que
    /// rtc.c emploie. DEVIATION: C# n'a pas de `struct tm` ; un type maison, et
    /// les mêmes conventions — tm_mon part de ZÉRO, tm_year part de 1900.</summary>
    private struct Tm { internal int sec, min, hour, wday, mday, mon, year; }

    // pcem: rtc.c:131-139
    private static void time_internal_get(ref Tm time_var)
    {
        time_var.sec = internal_clock.sec;
        time_var.min = internal_clock.min;
        time_var.hour = internal_clock.hour;
        time_var.wday = time_week_day();
        time_var.mday = internal_clock.mday;
        time_var.mon = internal_clock.mon - 1;
        time_var.year = internal_clock.year - 1900;
    }

    // omitted: time_internal_set (rtc.c:141-148) — son seul appelant est
    //   time_internal_sync, omise aussi.

    // pcem: rtc.c:150-182 — l'écriture des champs de date dans nvrram.
    //
    // QUATRE COMBINAISONS, et les quatre sont là : binaire ou DCB croisé avec
    // 24 ou 12 heures. En 12 heures, minuit et midi rendent 12 et non 0 —
    // `(hour % 12) ? (hour % 12) : 12` — et l'après-midi allume RTC_AMPM.
    private static void time_set_nvrram(uint8_t[] nvrram, ref Tm cur_time_tm)
    {
        if ((nvrram[RTC_REGB] & RTC_DM) != 0)
        {
                nvrram[RTC_SECONDS] = (uint8_t)cur_time_tm.sec;
                nvrram[RTC_MINUTES] = (uint8_t)cur_time_tm.min;
                nvrram[RTC_DOW] = (uint8_t)(cur_time_tm.wday + 1);
                nvrram[RTC_DOM] = (uint8_t)cur_time_tm.mday;
                nvrram[RTC_MONTH] = (uint8_t)(cur_time_tm.mon + 1);
                nvrram[RTC_YEAR] = (uint8_t)(cur_time_tm.year % 100);

                if ((nvrram[RTC_REGB] & RTC_2412) != 0)
                {
                        nvrram[RTC_HOURS] = (uint8_t)cur_time_tm.hour;
                }
                else
                {
                        nvrram[RTC_HOURS] = (uint8_t)((cur_time_tm.hour % 12) != 0 ? (cur_time_tm.hour % 12) : 12);
                        if (cur_time_tm.hour > 11)
                                nvrram[RTC_HOURS] |= RTC_AMPM;
                }
        }
        else
        {
                nvrram[RTC_SECONDS] = BCD(cur_time_tm.sec);
                nvrram[RTC_MINUTES] = BCD(cur_time_tm.min);
                nvrram[RTC_DOW] = BCD(cur_time_tm.wday + 1);
                nvrram[RTC_DOM] = BCD(cur_time_tm.mday);
                nvrram[RTC_MONTH] = BCD(cur_time_tm.mon + 1);
                nvrram[RTC_YEAR] = BCD(cur_time_tm.year % 100);

                if ((nvrram[RTC_REGB] & RTC_2412) != 0)
                {
                        nvrram[RTC_HOURS] = BCD(cur_time_tm.hour);
                }
                else
                {
                        nvrram[RTC_HOURS] = (cur_time_tm.hour % 12) != 0 ? BCD(cur_time_tm.hour % 12) : BCD(12);
                        if (cur_time_tm.hour > 11)
                                nvrram[RTC_HOURS] |= RTC_AMPM;
                }
        }
    }

    // pcem: rtc.c:184-202 — charge l'horloge interne DEPUIS nvrram. Appelé par
    // loadnvr après lecture du fichier de sauvegarde.
    internal static void time_internal_set_nvrram(uint8_t[] nvrram)
    {
        int temp;

        /* Load the entire internal clock state from the NVR. */
        internal_clock.sec = (nvrram[RTC_REGB] & RTC_DM) != 0 ? nvrram[RTC_SECONDS] : DCB(nvrram[RTC_SECONDS]);
        internal_clock.min = (nvrram[RTC_REGB] & RTC_DM) != 0 ? nvrram[RTC_MINUTES] : DCB(nvrram[RTC_MINUTES]);

        temp = (nvrram[RTC_REGB] & RTC_DM) != 0 ? nvrram[RTC_HOURS] : DCB(nvrram[RTC_HOURS]);

        if ((nvrram[RTC_REGB] & RTC_2412) != 0)
                internal_clock.hour = temp;
        else
                internal_clock.hour = ((temp & ~RTC_AMPM) % 12) + ((temp & RTC_AMPM) != 0 ? 12 : 0);

        internal_clock.mday = (nvrram[RTC_REGB] & RTC_DM) != 0 ? nvrram[RTC_DOM] : DCB(nvrram[RTC_DOM]);
        internal_clock.mon = (nvrram[RTC_REGB] & RTC_DM) != 0 ? nvrram[RTC_MONTH] : DCB(nvrram[RTC_MONTH]);
        internal_clock.year = (nvrram[RTC_REGB] & RTC_DM) != 0 ? nvrram[RTC_YEAR] : DCB(nvrram[RTC_YEAR]);
        internal_clock.year += (nvrram[RTC_REGB] & RTC_DM) != 0 ? 1900 : (DCB(nvrram[RTC_CENTURY]) * 100);
    }

    // omitted: time_internal_sync (rtc.c:204-214) — elle appelle time() et
    //   localtime() de la bibliothèque C, donc l'heure de l'HÔTE. Aucun oracle
    //   ne peut comparer ça : deux exécutions ne rendraient pas la même valeur,
    //   et le diff d'amorçage rougirait au premier passage. PCem ne l'appelle
    //   que sur demande explicite de l'utilisateur (enable_sync), et ce dépôt
    //   ne l'expose pas. C'est la SEULE porte de rtc.c vers le monde extérieur,
    //   et la laisser dehors est ce qui rend le reste comparable.

    // pcem: rtc.c:216-222
    internal static void time_get(uint8_t[] nvrram)
    {
        Tm cur_time_tm = default;

        time_internal_get(ref cur_time_tm);

        time_set_nvrram(nvrram, ref cur_time_tm);
    }
}
