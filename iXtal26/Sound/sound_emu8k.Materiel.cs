// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel de l'EMU8000 de l'AWE32 (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans sound_emu8k.c, appelées par les gardes de sound_emu8k.cs
// (`if (materiel.pb_nn)`), seules sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans
// l'entrée PB de PCEM_BUGS.md. La référence : le guide de l'EMU8000 (Dave Rossum, AWE32/EMU8000 Programmer's Guide,
// E-mu/Creative, 1994-1996), p. 11-14 (les adresses de la DRAM, WC, HWCF3) et p. 20 (l'initialisation). Les méthodes
// portent NoInlining (M2, G13.7c2). Le chorus et la réverbération (PB-162, PB-163) sont ceux de PCem, des « workalike »
// dont le microcode réel n'est pas publié : seule leur cohérence se corrige, une ligne à retard lue dans elle-même, à
// sa propre position, et un calcul qui sature ; leurs gardes se tiennent en tête du travail d'un tampon, jamais par
// échantillon.
//
// L'état que PCem n'a pas vit dans une classe à part (EtatEmu) : un champ de plus dans emu8k_t changerait sa
// disposition, et le code que le JIT tire des méthodes qui le lisent.

using System.Runtime.CompilerServices;

namespace iXtal26.Sound;

internal static partial class sound_emu8k
{
    private static class EtatEmu
    {
        /// <summary>PB-160 : l'écriture de DCYSUSV rejouée par le chemin de PCem ; la garde la laisse passer.</summary>
        internal static bool rejoue;
    }

    /// <summary>PB-158 : WC, CPF, CVCF et CCCA sont courants (guide, p. 8 à 11 et 14) : chaque lecture fait d'abord
    /// avancer la puce jusqu'à l'instant présent, comme PCem le fait à chaque écriture.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void lit_materiel(emu8k_t emu8k)
    {
        ModeMateriel.Sonde[158]++;
        emu8k_update(emu8k);
    }

    /// <summary>PB-159 : SMALR, SMARR, SMALW et SMARW relus en A22h — le bit 31, MT ou FULL (le drapeau de PCem, au bit 15
    /// du mot haut), et les bits 30 à 24 à zéro (guide, p. 11-12). Le drapeau s'efface à la lecture, comme chez PCem :
    /// sa durée sur la carte est inconnue.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static uint16_t dram_lu_materiel(uint32_t adresse, ref int drapeau)
    {
        ModeMateriel.Sonde[159]++;
        uint16_t ret = (uint16_t)(((adresse & EMU8K_MEM_ADDRESS_MASK) >> 16) | (uint32_t)(drapeau != 0 ? 0x8000 : 0));
        drapeau = 0;
        return ret;
    }

    /// <summary>PB-160 : HWCF3 n'est que ce que le programme y écrit (guide, p. 14 et 20 : son bit de sortie effacé au
    /// reset, 0004h écrit en fin d'initialisation, qui ouvre la sortie). Un moteur d'enveloppe allumé, HWCF3 autre que
    /// 0004h : l'écriture est rejouée par le chemin de PCem, puis HWCF3 rendu à sa valeur. Vrai : l'écriture est
    /// faite.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool dcysusv_materiel(uint16_t addr, uint16_t val, object p)
    {
        emu8k_t emu8k = (emu8k_t)p;
        if (EtatEmu.rejoue || DCYSUSV_GENERATOR_ENGINE_ON(val) == 0 ||
            emu8k.voice[emu8k.cur_voice].env_engine_on != 0 || emu8k.hwcf3 == 0x04)
            return false;
        ModeMateriel.Sonde[160]++;
        uint16_t hwcf3 = emu8k.hwcf3;
        EtatEmu.rejoue = true;
        emu8k_outw(addr, val, p);
        EtatEmu.rejoue = false;
        emu8k.hwcf3 = hwcf3;
        return true;
    }

    /// <summary>PB-158, la part (b) : un octet lu à l'adresse impaire d'une carte de 16 bits rend D15-D8, l'octet haut
    /// (le bus AT, déduit). L'accès reste un mot entier, effets de bord compris, comme chez PCem (la part (c), inconnue
    /// : le guide interdit l'accès par octet).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static uint8_t octet_impair_materiel(uint16_t addr, object p)
    {
        ModeMateriel.Sonde[158]++;
        return (uint8_t)(emu8k_inw((uint16_t)(addr & ~1), p) >> 8);
    }

    /// <summary>PB-162 : le chorus de PCem, chaque voie lue dans sa ligne à retard (l'indice replié modulo sa taille,
    /// autant de fois qu'il faut) et interpolée à sa propre position. Les positions se calculent dans l'ordre de PCem,
    /// au bit près ; les deux voies écrivent comme chez PCem.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool chorus_materiel(int32_t[] inbuf, int inoff, int32_t[] outbuf, int outoff,
                                        emu8k_chorus_eng_t engine, int count)
    {
        ModeMateriel.Sonde[162]++;
        int32_t[] b = engine.chorus_buffer;
        const int R = EMU8K_LFOCHORUS_SIZE;
        static int repli(int i)
        {
            i %= EMU8K_LFOCHORUS_SIZE;
            return i < 0 ? i + EMU8K_LFOCHORUS_SIZE : i;
        }
        // Une voie : la valeur de sa ligne (à partir de base) au retard `retard`, interpolée à sa fraction.
        int32_t lit(int @base, double readdouble)
        {
            int read = Cpu._386.CvtI32(Math.Floor(readdouble));
            int fraction = Cpu._386.CvtI32((readdouble - (double)read) * 65536.0);
            int32_t dat1 = b[@base + repli(read)];
            int32_t dat2 = b[@base + repli(read + 1)];
            return dat1 + (((dat2 - dat1) * fraction) >> 16);
        }
        for (int pos = 0; pos < count; pos++) {
                double offset_lfo = chortable[engine.lfo_pos.int_address] * engine.lfodepth_multip;
                double gauche = (double)engine.write - (double)engine.delay_samples_central - offset_lfo;
                int32_t dat1 = lit(0, gauche);
                b[engine.write] = inbuf[inoff] + ((dat1 * engine.feedback) >> 8);
                double droite = (double)engine.write - (double)engine.delay_samples_central -
                                engine.delay_offset_samples_right - offset_lfo;
                int32_t dat3 = lit(R, droite);
                b[R + engine.write] = inbuf[inoff] + ((dat3 * engine.feedback) >> 8);
                ++engine.write;
                engine.write %= EMU8K_LFOCHORUS_SIZE;
                engine.lfo_pos.addr += engine.lfo_inc.addr;
                engine.lfo_pos.int_address &= 0xFFFF;
                outbuf[outoff++] += dat1;
                outbuf[outoff++] += dat3;
                inoff++;
        }
        return true;
    }

    /// <summary>PB-163 : la conversion saturante, celle d'un processeur de signal à virgule fixe (déduit). NaN, que des
    /// entrées finies ne donnent pas, rend INT_MIN comme chez PCem : seuls les dépassements changent.</summary>
    private static int32_t sature(double d) =>
        double.IsNaN(d) ? int32_t.MinValue :
        d >= 2147483647.0 ? int32_t.MaxValue : d <= -2147483648.0 ? int32_t.MinValue : (int32_t)d;

    /// <summary>PB-163 : la réverbération de PCem, ses conversions saturantes, et `-in` calculé sans débordement. Le
    /// reste, les mêmes calculs dans le même ordre.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool reverb_materiel(int32_t[] inbuf, int inoff, int32_t[] outbuf, int outoff,
                                        emu8k_reverb_eng_t engine, int count)
    {
        ModeMateriel.Sonde[163]++;
        static int32_t peigne(emu8k_reverb_combfilter_t comb, int32_t @in)
        {
            int32_t output = comb.reflection[comb.read_pos];
            comb.filterstore = sature((double)((output * comb.damp2) + (comb.filterstore * comb.damp1)));
            comb.reflection[comb.read_pos] = sature((double)(@in - (comb.filterstore * comb.feedback)));
            if (++comb.read_pos >= comb.bufsize)
                    comb.read_pos = 0;
            return sature((double)(output * comb.output_gain));
        }
        static int32_t diffuseur(emu8k_reverb_combfilter_t comb, int32_t @in)
        {
            int32_t bufout = comb.reflection[comb.read_pos];
            int32_t bufin = sature((double)(-(float)@in + (bufout * comb.feedback)));
            int32_t output = sature((double)(bufout - (bufin * comb.feedback)));
            comb.reflection[comb.read_pos] = bufin;
            if (++comb.read_pos >= comb.bufsize)
                    comb.read_pos = 0;
            return output;
        }
        static int32_t queue(emu8k_reverb_combfilter_t comb, emu8k_reverb_combfilter_t[] allpasses, int a0, int32_t @in)
        {
            int32_t output = comb.reflection[comb.read_pos];
            comb.reflection[comb.read_pos] = @in;
            output = diffuseur(allpasses[a0 + 1], output);
            output = diffuseur(allpasses[a0 + 2], output);
            if (++comb.read_pos >= comb.bufsize)
                    comb.read_pos = 0;
            return output;
        }
        static int32_t amortisseur(emu8k_reverb_combfilter_t comb, int32_t @in)
        {
            comb.filterstore = sature((double)((@in * comb.damp2) + (comb.filterstore * comb.damp1)));
            return comb.filterstore;
        }
        bool lie = engine.link_return_type != 0;
        for (int pos = 0; pos < count; pos++) {
                int32_t dat1, dat2, @in, in2;
                @in = amortisseur(engine.damper, inbuf[inoff + pos]);
                in2 = (@in * engine.refl_in_amp) >> 8;
                if (lie) {
                        dat2 = peigne(engine.reflections[0], in2);
                        dat2 += peigne(engine.reflections[1], in2);
                        dat1 = peigne(engine.reflections[2], in2);
                        dat2 += peigne(engine.reflections[3], in2);
                        dat1 += peigne(engine.reflections[4], in2);
                        dat2 += peigne(engine.reflections[5], in2);
                } else {
                        dat1 = peigne(engine.reflections[0], in2);
                        dat1 += peigne(engine.reflections[1], in2);
                        dat1 += peigne(engine.reflections[2], in2);
                        dat1 += peigne(engine.reflections[3], in2);
                        dat1 += peigne(engine.reflections[4], in2);
                        dat1 += peigne(engine.reflections[5], in2);
                        dat2 = dat1;
                }
                dat1 += (queue(engine.tailL, engine.allpass, 0, @in + dat1) * engine.link_return_amp) >> 8;
                dat2 += (queue(engine.tailR, engine.allpass, 4, @in + dat2) * engine.link_return_amp) >> 8;
                outbuf[outoff++] += (dat1 * engine.out_mix) >> 8;
                outbuf[outoff++] += (dat2 * engine.out_mix) >> 8;
        }
        return true;
    }
}
