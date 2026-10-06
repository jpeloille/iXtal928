/* SPDX-FileCopyrightText: 2026 Julien Peloille
 * SPDX-License-Identifier: GPL-2.0-only
 *
 * G12.2 — sound_emu8k.c INCLUS ET NON LIÉ (PLAN-G12.md), comme scsi_aha1540.c par harness_aha.c : les tables de
 * l'EMU8000 et random_helper sont `static` (sound_emu8k.c:99-205), et la sonde (h_emu8k_probe) comme le contrôle
 * des tables (h_emu8k_tables) les lisent. Les drapeaux et les -I sont ceux de tous les objets de l'oracle.
 */
#include "../../pcem-dev/src/sound/sound_emu8k.c"

#include "harness.h"
#include "sound_opl.h"
#include "sound_sb.h"

extern device_t *devices[DEV_MAX];   /* device.c:15, :24 */
extern void *device_priv[DEV_MAX];
extern device_t sb_awe32_device;
extern uint16_t (*port_inw[0x10000][2])(uint16_t addr, void *priv);   /* io.c:9, :16 */
extern void *port_priv[0x10000][2];
void h_set_roms_path(const char *p);

#define H_FNV_SEED 1469598103934665603ULL
#define H_FNV_PRIME 1099511628211ULL

static uint64_t h_fnv_octets(uint64_t h, const void *p, size_t n) {
        const uint8_t *b = (const uint8_t *)p;
        size_t c;

        for (c = 0; c < n; c++) {
                h ^= b[c];
                h *= H_FNV_PRIME;
        }
        return h;
}

/* Une valeur, étendue sur 64 bits (le signe pour les types signés), mêlée d'un coup. */
static uint64_t h_fnv_mot(uint64_t h, uint64_t v) {
        h ^= v;
        h *= H_FNV_PRIME;
        return h;
}

static uint64_t h_flt(float f) {
        uint32_t u;
        memcpy(&u, &f, 4);
        return u;
}

static uint64_t h_dbl(double d) {
        uint64_t u;
        memcpy(&u, &d, 8);
        return u;
}

static uint64_t h_env(uint64_t h, const emu8k_envelope_t *e) {
        h = h_fnv_mot(h, (uint64_t)(int64_t)e->state);
        h = h_fnv_mot(h, (uint64_t)(int64_t)e->delay_samples);
        h = h_fnv_mot(h, (uint64_t)(int64_t)e->hold_samples);
        h = h_fnv_mot(h, (uint64_t)(int64_t)e->attack_samples);
        h = h_fnv_mot(h, (uint64_t)(int64_t)e->value_amp_hz);
        h = h_fnv_mot(h, (uint64_t)(int64_t)e->value_db_oct);
        h = h_fnv_mot(h, (uint64_t)(int64_t)e->sustain_value_db_oct);
        h = h_fnv_mot(h, (uint64_t)(int64_t)e->attack_amount_amp_hz);
        h = h_fnv_mot(h, (uint64_t)(int64_t)e->ramp_amount_db_oct);
        return h;
}

/* Un canal, champ par champ, dans l'ordre du struct (sound_emu8k.h:164-341) ; pendant de sound_emu8k.VoiceFnv. */
static uint64_t h_voice(const emu8k_voice_t *v) {
        uint64_t h = H_FNV_SEED;
        int c;

        h = h_fnv_mot(h, v->cpf);
        h = h_fnv_mot(h, v->ptrx);
        h = h_fnv_mot(h, v->cvcf);
        h = h_fnv_mot(h, (uint64_t)(int64_t)v->volumeslide.last);
        h = h_fnv_mot(h, v->vtft);
        h = h_fnv_mot(h, v->unknown_data0_4);
        h = h_fnv_mot(h, v->unknown_data0_5);
        h = h_fnv_mot(h, v->psst);
        h = h_fnv_mot(h, v->csl);
        h = h_fnv_mot(h, v->ccca);
        h = h_fnv_mot(h, v->envvol);
        h = h_fnv_mot(h, v->dcysusv);
        h = h_fnv_mot(h, v->envval);
        h = h_fnv_mot(h, v->dcysus);
        h = h_fnv_mot(h, v->atkhldv);
        h = h_fnv_mot(h, v->lfo1val);
        h = h_fnv_mot(h, v->lfo2val);
        h = h_fnv_mot(h, v->atkhld);
        h = h_fnv_mot(h, v->ip);
        h = h_fnv_mot(h, v->ifatn);
        h = h_fnv_mot(h, v->pefe);
        h = h_fnv_mot(h, v->fmmod);
        h = h_fnv_mot(h, v->tremfrq);
        h = h_fnv_mot(h, v->fm2frq2);
        h = h_fnv_mot(h, (uint64_t)(int64_t)v->env_engine_on);
        h = h_fnv_mot(h, v->addr.addr);
        h = h_fnv_mot(h, v->loop_start.addr);
        h = h_fnv_mot(h, v->loop_end.addr);
        h = h_fnv_mot(h, (uint64_t)(int64_t)v->initial_att);
        h = h_fnv_mot(h, (uint64_t)(int64_t)v->initial_filter);
        h = h_env(h, &v->vol_envelope);
        h = h_env(h, &v->mod_envelope);
        h = h_fnv_mot(h, (uint64_t)v->lfo1_speed);
        h = h_fnv_mot(h, (uint64_t)v->lfo2_speed);
        h = h_fnv_mot(h, v->lfo1_count.addr);
        h = h_fnv_mot(h, v->lfo2_count.addr);
        h = h_fnv_mot(h, (uint64_t)(int64_t)v->lfo1_delay_samples);
        h = h_fnv_mot(h, (uint64_t)(int64_t)v->lfo2_delay_samples);
        h = h_fnv_mot(h, (uint64_t)(int64_t)v->vol_l);
        h = h_fnv_mot(h, (uint64_t)(int64_t)v->vol_r);
        h = h_fnv_mot(h, (uint64_t)(int64_t)v->fixed_modenv_filter_height);
        h = h_fnv_mot(h, (uint64_t)(int64_t)v->fixed_modenv_pitch_height);
        h = h_fnv_mot(h, (uint64_t)(int64_t)v->fixed_lfo1_filt_mod);
        h = h_fnv_mot(h, (uint64_t)(int64_t)v->fixed_lfo1_vibrato);
        h = h_fnv_mot(h, (uint64_t)(int64_t)v->fixed_lfo1_tremolo);
        h = h_fnv_mot(h, (uint64_t)(int64_t)v->fixed_lfo2_vibrato);
        h = h_fnv_mot(h, (uint64_t)(int64_t)v->filterq_idx);
        h = h_fnv_mot(h, (uint64_t)(int64_t)v->filt_att);
        for (c = 0; c < 5; c++)
                h = h_fnv_mot(h, (uint64_t)v->filt_buffer[c]);
        return h;
}

/* Un peigne de la réverbération : ses scalaires, puis les MAX_REFL_SIZE premières entrées de son tampon (au-delà, le
 * C déborde : PB-161) ; pendant de sound_emu8k.CombFnv. */
static uint64_t h_comb(const emu8k_reverb_combfilter_t *r) {
        uint64_t h = H_FNV_SEED;
        int c;

        h = h_fnv_mot(h, (uint64_t)(int64_t)r->read_pos);
        h = h_fnv_mot(h, (uint64_t)(int64_t)r->bufsize);
        h = h_fnv_mot(h, (uint64_t)(int64_t)r->filterstore);
        h = h_fnv_mot(h, h_flt(r->output_gain));
        h = h_fnv_mot(h, h_flt(r->feedback));
        h = h_fnv_mot(h, h_flt(r->damp1));
        h = h_fnv_mot(h, h_flt(r->damp2));
        for (c = 0; c < MAX_REFL_SIZE; c++)
                h = h_fnv_mot(h, (uint64_t)(int64_t)r->reflection[c]);
        return h;
}

/* La sonde de l'EMU8000 (H_EMU8K_PROBE_N champs), dans l'ordre de sound_emu8k.Probe côté C# : l'état de la puce, la
 * mémoire entière (la ROM, le bloc vide, la RAM), le chorus, la réverbération (ses dix-sept peignes), les trente-deux
 * canaux. Tout à zéro sans AWE32 montée. */
void h_emu8k_probe(uint64_t *o) {
        sb_t *sb = NULL;
        emu8k_t *e;
        emu8k_reverb_eng_t *rv;
        uint64_t h;
        int c, p;

        memset(o, 0, H_EMU8K_PROBE_N * sizeof(uint64_t));
        for (c = 0; c < DEV_MAX; c++)
                if (devices[c] == &sb_awe32_device) {
                        sb = (sb_t *)device_priv[c];
                        break;
                }
        if (!sb)
                return;
        e = &sb->emu8k;
        rv = &e->reverb_engine;
        /* L'adresse de la puce : le premier port où emu8k_init a posé emu8k_inw pour elle (io.c:8, :16). */
        for (p = 0; p < 0x10000; p++)
                if ((port_inw[p][0] == emu8k_inw && port_priv[p][0] == e) || (port_inw[p][1] == emu8k_inw && port_priv[p][1] == e))
                        break;
        *o++ = 1 | ((uint64_t)(uint8_t)e->cur_reg << 8) | ((uint64_t)(uint8_t)e->cur_voice << 16) |
               ((uint64_t)(p & 0xffff) << 24);
        *o++ = e->hwcf1 | ((uint64_t)e->hwcf2 << 16) | ((uint64_t)e->hwcf3 << 32);
        *o++ = e->hwcf4 | ((uint64_t)e->hwcf5 << 32);
        *o++ = e->hwcf6 | ((uint64_t)e->hwcf7 << 32);
        *o++ = e->smalr | ((uint64_t)e->smarr << 32);
        *o++ = e->smalw | ((uint64_t)e->smarw << 32);
        *o++ = e->smld_buffer | ((uint64_t)e->smrd_buffer << 16) | ((uint64_t)e->wc << 32) | ((uint64_t)e->id << 48);
        h = h_fnv_octets(H_FNV_SEED, e->init1, sizeof(e->init1));
        h = h_fnv_octets(h, e->init2, sizeof(e->init2));
        h = h_fnv_octets(h, e->init3, sizeof(e->init3));
        *o++ = h_fnv_octets(h, e->init4, sizeof(e->init4));
        *o++ = e->ram_end_addr | ((uint64_t)(uint32_t)e->pos << 32);
        *o++ = (uint16_t)e->out_l | ((uint64_t)(uint16_t)e->out_r << 16);
        *o++ = (uint32_t)random_helper | ((uint64_t)(uint32_t)dmareadbit << 16) | ((uint64_t)(uint32_t)dmawritebit << 40);
        h = h_fnv_octets(H_FNV_SEED, e->rom, 1024 * 1024);
        h = h_fnv_octets(h, e->empty, 2 * 0x10000);
        if (e->ram)
                h = h_fnv_octets(h, e->ram, (size_t)(e->ram_end_addr - EMU8K_RAM_MEM_START) * 2);
        *o++ = h;
        *o++ = (uint32_t)e->chorus_engine.write | ((uint64_t)(uint32_t)e->chorus_engine.feedback << 32);
        *o++ = (uint32_t)e->chorus_engine.delay_samples_central;
        *o++ = h_dbl(e->chorus_engine.lfodepth_multip);
        *o++ = h_dbl(e->chorus_engine.delay_offset_samples_right);
        *o++ = e->chorus_engine.lfo_inc.addr;
        *o++ = e->chorus_engine.lfo_pos.addr;
        h = h_fnv_octets(H_FNV_SEED, e->chorus_engine.chorus_left_buffer, sizeof(e->chorus_engine.chorus_left_buffer));
        *o++ = h_fnv_octets(h, e->chorus_engine.chorus_right_buffer, sizeof(e->chorus_engine.chorus_right_buffer));
        *o++ = h_fnv_octets(H_FNV_SEED, e->chorus_in_buffer, sizeof(e->chorus_in_buffer));
        *o++ = (uint16_t)rv->out_mix | ((uint64_t)(uint16_t)rv->link_return_amp << 16) |
               ((uint64_t)(uint8_t)rv->link_return_type << 32) | ((uint64_t)rv->refl_in_amp << 40);
        *o++ = h_fnv_octets(H_FNV_SEED, e->reverb_in_buffer, sizeof(e->reverb_in_buffer));
        *o++ = h_fnv_octets(H_FNV_SEED, e->buffer, sizeof(e->buffer));
        for (c = 0; c < 6; c++)
                *o++ = h_comb(&rv->reflections[c]);
        for (c = 0; c < 8; c++)
                *o++ = h_comb(&rv->allpass[c]);
        *o++ = h_comb(&rv->tailL);
        *o++ = h_comb(&rv->tailR);
        *o++ = h_comb(&rv->damper);
        for (c = 0; c < 32; c++)
                *o++ = h_voice(&e->voice[c]);
}

/* emu8k-tables-check : les tables de emu8k_init (sound_emu8k.c:2066-2225), calculées par le vrai emu8k_init sur un
 * EMU8000 de brouillon (sans RAM ; ses ports posés puis laissés : le processus ne fait que ce contrôle), puis
 * l'empreinte de la ROM chargée (après la correction AWE-DUMP, :2026-2029). Rend 0 sans awe32.raw. */
int h_emu8k_tables(const char *romspath, int64_t *freq, int32_t *atten, int32_t *voldb, int32_t *ampdb, int32_t *hzoct,
                   int32_t *attack, int32_t *lfo, int64_t *lfospeed, double *chor, int32_t *filt, float *cubic,
                   uint64_t *rom) {
        static emu8k_t e;
        FILE *f;

        h_set_roms_path(romspath);
        f = romfopen("awe32.raw", "rb");
        if (!f)
                return 0;
        fclose(f);
        memset(&e, 0, sizeof(e));
        emu8k_init(&e, 0x620, 0);
        memcpy(freq, freqtable, sizeof(freqtable));
        memcpy(atten, attentable, sizeof(attentable));
        memcpy(voldb, env_vol_db_to_vol_target, sizeof(env_vol_db_to_vol_target));
        memcpy(ampdb, env_vol_amplitude_to_db, sizeof(env_vol_amplitude_to_db));
        memcpy(hzoct, env_mod_hertz_to_octave, sizeof(env_mod_hertz_to_octave));
        memcpy(attack, env_attack_to_samples, sizeof(env_attack_to_samples));
        memcpy(lfo, lfotable, sizeof(lfotable));
        memcpy(lfospeed, lfofreqtospeed, sizeof(lfofreqtospeed));
        memcpy(chor, chortable, sizeof(chortable));
        memcpy(filt, filt_coeffs, sizeof(filt_coeffs));
        memcpy(cubic, cubic_table, sizeof(cubic_table));
        *rom = h_fnv_octets(H_FNV_SEED, e.rom, 1024 * 1024);
        emu8k_close(&e);
        return 1;
}

/* emu8k-kernel-check : les noyaux de la réverbération, du chorus et de la pente du volume (sound_emu8k.c:1416-1602),
 * sur des états fabriqués par un générateur que l'outil partage (splitmix64 ; EmuKernelCheck.cs) : des valeurs
 * qu'aucun invité n'atteint en temps de porte, jusqu'aux bornes de l'int32, et des réglages pris parmi ceux que les
 * registres posent. Les tables (chortable) doivent avoir été remplies par h_emu8k_tables. kind : 0 le peigne,
 * 1 le diffuseur, 2 la queue et ses deux diffuseurs, 3 l'amortisseur, 4 le chorus, 5 la réverbération entière,
 * 6 la pente du volume. o[0] : l'empreinte des sorties ; o[1] : celle de l'état final ; o[2] : le nombre de sorties
 * qui valent INT_MIN. */
#define H_KN 4096

static uint64_t h_sm(uint64_t *s) {
        uint64_t z = (*s += 0x9E3779B97F4A7C15ULL);

        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9ULL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBULL;
        return z ^ (z >> 31);
}

/* Un int32 : un échantillon, une somme de voix (±2^25), n'importe lequel, ou l'une des bornes. */
static int32_t h_val(uint64_t *s) {
        uint64_t r = h_sm(s);

        switch (r & 3) {
        case 0:
                return (int32_t)((r >> 8) & 0xFFFF) - 0x8000;
        case 1:
                return (int32_t)((r >> 8) & 0x3FFFFFF) - 0x2000000;
        case 2:
                return (int32_t)(uint32_t)(r >> 32);
        default:
                return (r & 4) ? (int32_t)(0x7FFFFFFFu - (uint32_t)((r >> 8) & 0xFF))
                               : (int32_t)(0x80000000u + (uint32_t)((r >> 8) & 0xFF));
        }
}

/* Un peigne : sa taille, sa position, ses gains comme les registres les posent (:926-946, :1176-1213 ; ceux d'un
 * diffuseur, :920, si diff), son amortissement (:1185-1194), puis ses valeurs. Une fois sur deux, le gain et le
 * retour au maximum, l'amortissement à l'un de ses bouts : là où les conversions débordent. */
static void h_fab_comb(emu8k_reverb_combfilter_t *c, uint64_t *s, int diff) {
        uint64_t r = h_sm(s), b = h_sm(s);
        int i, v, f;

        c->bufsize = 1 + (int)((r >> 8) % MAX_REFL_SIZE);
        c->read_pos = (int)((r >> 24) % (uint64_t)c->bufsize);
        v = (int)(r & 0xFF);
        if (b & 1)
                v |= 0xF0;
        c->output_gain = ((v & 0xF0) >> 4) / 15.0;
        f = (int)((r >> 40) & 0xFF);
        if (b & 2)
                f = 0xFF;
        c->feedback = diff ? f / ((float)0xFF) : (f & 0xF) / 15.0;
        v = (int)((r >> 48) & 0xFF);
        if (b & 4)
                v = (b & 8) ? 0 : 0xFF;
        c->damp1 = v / 255.0;
        c->damp2 = (0xFF - v) / 255.0;
        c->filterstore = h_val(s);
        for (i = 0; i < MAX_REFL_SIZE; i++)
                c->reflection[i] = h_val(s);
}

/* Le chorus : ses réglages comme init3, init4, hwcf4 et hwcf5 les posent (:962-966, :1102-1121, :1222-1225). */
static void h_fab_chorus(emu8k_chorus_eng_t *e, uint64_t *s) {
        uint64_t r = h_sm(s);
        double osc;
        int i;

        e->write = (int32_t)(r % EMU8K_LFOCHORUS_SIZE);
        e->feedback = (int32_t)((r >> 16) & 0xFF);
        e->delay_samples_central = (int32_t)((r >> 24) & 0x1FFF);
        e->lfodepth_multip = ((int32_t)((r >> 40) & 0xFF) * e->delay_samples_central) >> 8;
        r = h_sm(s);
        e->delay_offset_samples_right = ((double)(int32_t)(r & 0x1FFFFF)) / 256.0;
        osc = (double)(uint32_t)(r >> 32);
        osc *= 65.536 / 44100.0;
        osc *= 65536.0 * 65536.0;
        e->lfo_inc.addr = (uint64_t)osc;
        e->lfo_pos.addr = h_sm(s) & 0xFFFFFFFFFFFFULL;
        for (i = 0; i < EMU8K_LFOCHORUS_SIZE; i++)
                e->chorus_left_buffer[i] = h_val(s);
        for (i = 0; i < EMU8K_LFOCHORUS_SIZE; i++)
                e->chorus_right_buffer[i] = h_val(s);
}

/* La réverbération : ses quatre réglages (:915, :924, :969, :1229), ses dix-sept peignes. */
static void h_fab_reverb(emu8k_reverb_eng_t *e, uint64_t *s) {
        uint64_t r = h_sm(s);
        int c;

        e->out_mix = (int16_t)(r & 0xFF);
        e->link_return_amp = (int16_t)((r >> 8) & 0xFF);
        e->link_return_type = (int8_t)((r >> 16) & 1);
        e->refl_in_amp = (uint8_t)(r >> 24);
        for (c = 0; c < 6; c++)
                h_fab_comb(&e->reflections[c], s, 0);
        for (c = 0; c < 8; c++)
                h_fab_comb(&e->allpass[c], s, 1);
        h_fab_comb(&e->tailL, s, 0);
        h_fab_comb(&e->tailR, s, 0);
        h_fab_comb(&e->damper, s, 0);
}

static uint64_t h_reverb_fnv(const emu8k_reverb_eng_t *e) {
        uint64_t h = H_FNV_SEED;
        int c;

        h = h_fnv_mot(h, (uint64_t)(int64_t)e->out_mix);
        h = h_fnv_mot(h, (uint64_t)(int64_t)e->link_return_amp);
        h = h_fnv_mot(h, (uint64_t)(int64_t)e->link_return_type);
        h = h_fnv_mot(h, e->refl_in_amp);
        for (c = 0; c < 6; c++)
                h = h_fnv_mot(h, h_comb(&e->reflections[c]));
        for (c = 0; c < 8; c++)
                h = h_fnv_mot(h, h_comb(&e->allpass[c]));
        h = h_fnv_mot(h, h_comb(&e->tailL));
        h = h_fnv_mot(h, h_comb(&e->tailR));
        return h_fnv_mot(h, h_comb(&e->damper));
}

void h_emu8k_kernel(int kind, uint64_t seed, uint64_t *o) {
        static emu8k_reverb_eng_t rv; /* dix-sept peignes de 31 Ko : hors de la pile */
        static emu8k_chorus_eng_t ch;
        static int32_t in[H_KN], out[2 * H_KN];
        emu8k_slide_t sl;
        uint64_t s = seed, h = H_FNV_SEED;
        int32_t x;
        int c, n = 0;

        switch (kind) {
        case 0:
        case 1:
        case 2:
        case 3:
                h_fab_comb(&rv.tailL, &s, kind == 1);
                for (c = 0; c < 4; c++)
                        h_fab_comb(&rv.allpass[c], &s, 1);
                for (c = 0; c < H_KN; c++) {
                        x = h_val(&s);
                        if (kind == 0)
                                x = emu8k_reverb_comb_work(&rv.tailL, x);
                        else if (kind == 1)
                                x = emu8k_reverb_diffuser_work(&rv.tailL, x);
                        else if (kind == 2)
                                x = emu8k_reverb_tail_work(&rv.tailL, &rv.allpass[0], x);
                        else
                                x = emu8k_reverb_damper_work(&rv.tailL, x);
                        n += x == INT32_MIN;
                        h = h_fnv_mot(h, (uint64_t)(int64_t)x);
                }
                o[1] = h_comb(&rv.tailL);
                for (c = 0; c < 4; c++)
                        o[1] = h_fnv_mot(o[1], h_comb(&rv.allpass[c]));
                break;
        case 4:
                h_fab_chorus(&ch, &s);
                for (c = 0; c < H_KN; c++)
                        in[c] = h_val(&s);
                for (c = 0; c < 2 * H_KN; c++)
                        out[c] = h_val(&s);
                emu8k_work_chorus(in, out, &ch, H_KN);
                o[1] = h_fnv_mot(h_fnv_mot(H_FNV_SEED, (uint64_t)(int64_t)ch.write), ch.lfo_pos.addr);
                o[1] = h_fnv_octets(o[1], ch.chorus_left_buffer, sizeof(ch.chorus_left_buffer));
                o[1] = h_fnv_octets(o[1], ch.chorus_right_buffer, sizeof(ch.chorus_right_buffer));
                break;
        case 5:
                h_fab_reverb(&rv, &s);
                for (c = 0; c < H_KN; c++)
                        in[c] = h_val(&s);
                for (c = 0; c < 2 * H_KN; c++)
                        out[c] = h_val(&s);
                emu8k_work_reverb(in, out, &rv, H_KN);
                o[1] = h_reverb_fnv(&rv);
                break;
        default:
                sl.last = (int32_t)(h_sm(&s) & 0x3FFFF) - 0x20000;
                for (c = 0; c < H_KN; c++) {
                        x = emu8k_vol_slide(&sl, (int32_t)(h_sm(&s) & 0x3FFFF) - 0x20000);
                        h = h_fnv_mot(h, (uint64_t)(int64_t)x);
                }
                o[1] = (uint64_t)(int64_t)sl.last;
                break;
        }
        if (kind == 4 || kind == 5)
                for (c = 0; c < 2 * H_KN; c++) {
                        n += out[c] == INT32_MIN;
                        h = h_fnv_mot(h, (uint64_t)(int64_t)out[c]);
                }
        o[0] = h;
        o[2] = (uint64_t)n;
}
