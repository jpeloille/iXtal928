// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/sound/sound_dbopl.cc + includes/private/sound/sound_dbopl.h
// STATUS: transcribed — opl[2], opl_init, opl_status_update, opl_timer_over, opl_write,
//         opl_read, opl2_update, opl3_update (sound_dbopl.cc:1-175), branche DBOPL seule.
//         Omis : NukedOPL (opl3chip, OPL3_Reset, OPL3_WriteAddr, OPL3_WriteReg,
//         OPL3_GenerateStream) — opl_emu est figé à OPL_DBOPL (décision d'orchestration G8),
//         donc chaque test `!is_opl3 || !opl_emu` est vrai et chaque `if (opl_emu)` faux.
//
// PERSISTANCE : opl[] est statique et opl_init ne remet à zéro NI addr, NI timer[], NI
// timer_ctrl, NI status_mask, NI status, NI l'état des opérateurs que Setup ne réécrit pas
// (keyOn, state, volume, waveIndex...). Un reset matériel les garde chez PCem ; ici aussi.
// opl_clear_state_for_oracle_parity est le pendant de la remise à zéro que l'oracle fait à
// l'amorçage, et n'appartient pas à PCem.

namespace iXtal26.Sound;

// pcem: sound_dbopl.cc:16 — void (*timer_callback)(void *param, int timer, int64_t period)
internal delegate void opl_timer_callback_t(object? param, int timer, int64_t period);

internal static partial class sound_dbopl
{
    // pcem: sound_dbopl.cc:5-18
    // DEVIATION: structure ANONYME en C ; C# exige un nom. Classe : jamais copiée.
    private sealed class opl_state_t
    {
        internal readonly DBOPL.Chip chip = new DBOPL.Chip();
        // omitted: struct opl3_chip opl3chip (:7) — NukedOPL.
        internal int addr;
        internal readonly int[] timer = new int[2];
        internal uint8_t timer_ctrl;
        internal uint8_t status_mask;
        internal uint8_t status;
        internal int is_opl3;
        internal int opl_emu;

        // NULL jusqu'à opl_init, comme en C.
        internal opl_timer_callback_t timer_callback = null!;
        internal object? timer_param;
    }

    private static readonly opl_state_t[] opl = { new opl_state_t(), new opl_state_t() };  // éléments remplacés par opl_clear_state_for_oracle_parity

    // pcem: sound_dbopl.cc:20
    private const int STATUS_TIMER_1 = 0x40, STATUS_TIMER_2 = 0x20, STATUS_TIMER_ALL = 0x80;

    // pcem: sound_dbopl.cc:22-28
    private const int CTRL_IRQ_RESET = 0x80;
    private const int CTRL_TIMER1_MASK = 0x40;
    private const int CTRL_TIMER2_MASK = 0x20;
    private const int CTRL_TIMER2_CTRL = 0x02;
    private const int CTRL_TIMER1_CTRL = 0x01;

    // pcem: sound_dbopl.cc:30-46
    // omitted: la branche `else` (:39-45, OPL3_Reset de NukedOPL) — opl_emu figé à OPL_DBOPL,
    //   la condition `!is_opl3 || !opl_emu` de :32 est toujours vraie.
    internal static void opl_init(opl_timer_callback_t timer_callback, object? timer_param, int nr, int is_opl3,
                                  int opl_emu)
    {
        DBOPL.InitTables();
        opl[nr].chip.Setup(48000, is_opl3);
        opl[nr].timer_callback = timer_callback;
        opl[nr].timer_param = timer_param;
        opl[nr].is_opl3 = is_opl3;
        opl[nr].opl_emu = opl_emu;
    }

    // ORACLE PARITY (pas de contrepartie PCem) : remet à zéro ce que opl_init laisse vivre
    // d'un reset à l'autre (voir l'en-tête) et que l'oracle efface à son amorçage. Ne touche
    // pas la puce DBOPL elle-même.
    // DEVIATION (PLAN-G8.md, défaut n° 4) : à l'amorçage, la puce et tout l'état de opl[nr] repartent
    // de zéro, des deux côtés — chez PCem, opl[] est statique et traverse les amorçages d'un même
    // processus (opl_init ne remet ni l'état, ni le masque, ni les minuteries, ni les opérateurs
    // que Setup ne réécrit pas). Pendant de h_opl_reset côté oracle (harness_dbopl.cpp).
    internal static void opl_clear_state_for_oracle_parity(int nr)
    {
        opl[nr] = new opl_state_t();
    }

    // G8.1 — la sonde du son : les six champs de opl[nr] que h_opl_state rend côté oracle.
    internal static void ProbeState(int nr, uint64_t[] o, int f)
    {
        o[f++] = (uint64_t)(long)opl[nr].addr;
        o[f++] = opl[nr].status;
        o[f++] = opl[nr].status_mask;
        o[f++] = opl[nr].timer_ctrl;
        o[f++] = (uint64_t)(long)opl[nr].timer[0];
        o[f++] = (uint64_t)(long)opl[nr].timer[1];
    }

    // pcem: sound_dbopl.cc:48-53
    internal static void opl_status_update(int nr)
    {
        if ((opl[nr].status & (STATUS_TIMER_1 | STATUS_TIMER_2) & opl[nr].status_mask) != 0)
                opl[nr].status |= STATUS_TIMER_ALL;
        else
                opl[nr].status = (uint8_t)(opl[nr].status & ~STATUS_TIMER_ALL);
    }

    // pcem: sound_dbopl.cc:55-65
    internal static void opl_timer_over(int nr, int timer)
    {
        if (timer == 0) {
                opl[nr].status |= STATUS_TIMER_1;
                opl[nr].timer_callback(opl[nr].timer_param, 0, opl[nr].timer[0] * 4);
        } else {
                opl[nr].status |= STATUS_TIMER_2;
                opl[nr].timer_callback(opl[nr].timer_param, 1, opl[nr].timer[1] * 16);
        }

        opl_status_update(nr);
    }

    // pcem: sound_dbopl.cc:67-110
    // omitted: OPL3_WriteAddr (:72) et OPL3_WriteReg (:77) — NukedOPL, opl_emu figé à OPL_DBOPL.
    internal static void opl_write(int nr, uint16_t addr, uint8_t val)
    {
        if ((addr & 1) == 0) {
                opl[nr].addr = (int)opl[nr].chip.WriteAddr(addr, val) & (opl[nr].is_opl3 != 0 ? 0x1ff : 0xff);
        } else {
                opl[nr].chip.WriteReg((uint32_t)opl[nr].addr, val);

                switch (opl[nr].addr) {
                case 0x02: /*Timer 1*/
                        opl[nr].timer[0] = 256 - val;
                        break;
                case 0x03: /*Timer 2*/
                        opl[nr].timer[1] = 256 - val;
                        break;
                case 0x04:                        /*Timer control*/
                        if ((val & CTRL_IRQ_RESET) != 0) /*IRQ reset*/
                        {
                                opl[nr].status = (uint8_t)(opl[nr].status & ~(STATUS_TIMER_1 | STATUS_TIMER_2));
                                opl_status_update(nr);
                                return;
                        }
                        if (((val ^ opl[nr].timer_ctrl) & CTRL_TIMER1_CTRL) != 0) {
                                if ((val & CTRL_TIMER1_CTRL) != 0)
                                        opl[nr].timer_callback(opl[nr].timer_param, 0, opl[nr].timer[0] * 4);
                                else
                                        opl[nr].timer_callback(opl[nr].timer_param, 0, 0);
                        }
                        if (((val ^ opl[nr].timer_ctrl) & CTRL_TIMER2_CTRL) != 0) {
                                if ((val & CTRL_TIMER2_CTRL) != 0)
                                        opl[nr].timer_callback(opl[nr].timer_param, 1, opl[nr].timer[1] * 16);
                                else
                                        opl[nr].timer_callback(opl[nr].timer_param, 1, 0);
                        }
                        opl[nr].status_mask = (uint8_t)((~val & (CTRL_TIMER1_MASK | CTRL_TIMER2_MASK)) | 0x80);
                        opl[nr].timer_ctrl = val;
                        break;
                }
        }
    }

    // pcem: sound_dbopl.cc:112-117
    internal static uint8_t opl_read(int nr, uint16_t addr)
    {
        if ((addr & 1) == 0) {
                return (uint8_t)((opl[nr].status & opl[nr].status_mask) | (opl[nr].is_opl3 != 0 ? 0 : 0x06));
        }
        return (uint8_t)(opl[nr].is_opl3 != 0 ? 0 : 0xff);
    }

    // pcem: sound_dbopl.cc:129, :160 — #define chunk_size 1024
    private const int chunk_size = 1024;

    // pcem: sound_dbopl.cc:134, :165 — `Bit32s buffer_32[...]` sur la pile.
    // DEVIATION: tampons statiques réutilisés plutôt qu'une allocation par appel. Sans effet
    //   observable : GenerateBlock2/3 efface (memset) puis écrit chaque entrée lue ensuite.
    private static readonly int32_t[] buffer_32_2 = new int32_t[chunk_size];
    private static readonly int32_t[] buffer_32_3 = new int32_t[chunk_size * 2];

    // pcem: sound_dbopl.cc:119-126. `int16_t *buffer` devient (buffer, b).
    private static void opl2_update_impl(int nr, int16_t[] buffer, int b, int32_t[] buffer_32, int samples)
    {
        int c;

        opl[nr].chip.GenerateBlock2((uint32_t)samples, buffer_32, 0);

        for (c = 0; c < samples; c++)
                buffer[b + c * 2] = (int16_t)buffer_32[c];
    }

    // pcem: sound_dbopl.cc:128-144
    internal static void opl2_update(int nr, int16_t[] buffer, int b, int samples)
    {
        int n_chunks;
        int rest;
        int i_chunks;
        int32_t[] buffer_32 = buffer_32_2;

        n_chunks = samples / chunk_size;
        rest = samples - n_chunks * chunk_size;
        for(i_chunks = 0; i_chunks != n_chunks; ++i_chunks)
                opl2_update_impl(nr, buffer, b + i_chunks * chunk_size * 2, buffer_32, chunk_size);
        if(rest != 0)
                opl2_update_impl(nr, buffer, b + n_chunks * chunk_size * 2, buffer_32, rest);
    }

    // pcem: sound_dbopl.cc:146-157
    // omitted: OPL3_GenerateStream (:149-150) — NukedOPL ; opl_emu figé à OPL_DBOPL.
    private static void opl3_update_impl(int nr, int16_t[] buffer, int b, int32_t[] buffer_32, int samples)
    {
        int c;

        opl[nr].chip.GenerateBlock3((uint32_t)samples, buffer_32, 0);

        for (c = 0; c < samples * 2; c++)
                buffer[b + c] = (int16_t)buffer_32[c];
    }

    // pcem: sound_dbopl.cc:159-175
    internal static void opl3_update(int nr, int16_t[] buffer, int b, int samples)
    {
        int n_chunks;
        int rest;
        int i_chunk;
        int32_t[] buffer_32 = buffer_32_3;

        n_chunks = samples / chunk_size;
        rest = samples - n_chunks * chunk_size;
        for(i_chunk = 0; i_chunk != n_chunks; ++i_chunk)
                opl3_update_impl(nr, buffer, b + i_chunk * chunk_size * 2, buffer_32, chunk_size);
        if(rest != 0)
                opl3_update_impl(nr, buffer, b + n_chunks * chunk_size * 2, buffer_32, rest);
    }
}
