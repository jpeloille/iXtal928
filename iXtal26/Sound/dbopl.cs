// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/dosbox/dbopl.cpp + includes/private/dosbox/dbopl.h
// STATUS: transcribed — tout ce que compile le mode WAVE_TABLEMUL (dbopl.h:46) : constantes,
//         tables statiques, EnvelopeSelect, InitTables (G8.0, tables vérifiées identiques à
//         l'oracle par opl-tables-check), Operator, Channel et Chip (G8.1). Omis : les branches
//         WAVE_HANDLER et WAVE_TABLELOG (ExpTable, SinTable, MakeVolume, WaveForm0-7), le bloc
//         WAVE_PRECISION (non défini), le Handler commenté (:1466-1490), Chip::Generate (déclaré
//         dbopl.h:257, jamais défini).
//
// DEVIATIONS (G8.1) :
//   1. Les pointeurs de fonction membre deviennent des énumérations : Operator.volHandler est
//      l'état (OFF..ATTACK) dont TemplateVolume<State> est l'instanciation, et
//      Channel.synthHandler est le SynthMode dont BlockTemplate<SynthMode> est l'instanciation.
//      Chaque appel `(this->*volHandler)()` devient `TemplateVolume(volHandler)`, chaque
//      `(ch->*(ch->synthHandler))(...)` devient `BlockTemplate(ch.synthHandler, ...)` — un switch
//      sur l'argument du template, même corps, même ordre.
//   2. Arithmétique de pointeurs sur Channel (`this + n`, `Op(index)`, BlockTemplate qui rend
//      `this + n`) : chaque Channel connaît son Chip et son rang dans chan[18], et `this + n`
//      devient `owner.chan[self + n]`. BlockTemplate et GenerateBlock2/3 manipulent des RANGS
//      (int) au lieu de Channel*.
//   3. ChanOffsetTable / OpOffsetTable (dbopl.cpp:177-180, :1404-1436) : des offsets d'OCTETS
//      depuis un Chip* nul. Ici, des INDICES : ChanOffsetTable[i] = rang dans chan[18],
//      OpOffsetTable[i] = 2 * rang + n° d'opérateur ; -1 remplace le 0 de « pas de registre »
//      (aucun offset valide n'est nul en C : chan[] suit 692 octets de compteurs et de tables).
//   4. waveBase (Bit16s* dans WaveTable) devient un indice int dans WaveTable ; les sorties
//      `Bit32s *output` deviennent (int32_t[] output, int o).

// CS8981 : `DBOPL` est le nom de l'espace de noms C++ ; les tables gardent leurs noms.
namespace iXtal26.Sound;

internal static partial class DBOPL
{
    // pcem: dbopl.cpp:43-45 (PI, :44)
    private const double PI = 3.14159265358979323846;

    // pcem: dbopl.cpp:49
    private const double OPLRATE = (double)(14318180.0 / 288.0);

    // pcem: dbopl.cpp:50 — 52
    internal const int TREMOLO_TABLE = 52;

    // pcem: dbopl.cpp:55-66 — WAVE_PRECISION non défini : WAVE_BITS 10.
    private const int WAVE_BITS = 10;
    private const int WAVE_SH = 32 - WAVE_BITS;
    // `uint` : le C l'applique à des Bit32u ; un `int` C# élargirait l'expression en long.
    private const uint WAVE_MASK = (1 << WAVE_SH) - 1;

    // pcem: dbopl.cpp:69-71
    private const int LFO_SH = WAVE_SH - 10;
    private const uint LFO_MAX = 256 << (LFO_SH);

    // pcem: dbopl.cpp:78-89 — ENV_BITS 9 en WAVE_TABLEMUL.
    private const int ENV_BITS = 9;
    private const int ENV_MIN = 0;
    private const int ENV_EXTRA = ENV_BITS - 9;
    private const int ENV_MAX = 511 << ENV_EXTRA;
    private const int ENV_LIMIT = (12 * 256) >> (3 - ENV_EXTRA);
    // pcem: dbopl.cpp:88 — ENV_SILENT(_X_). Deux surcharges : le C compare en NON SIGNÉ quand
    //   _X_ est un Bitu/Bit32u (GetSample, GeneratePercussion) et en signé pour un Bit32s
    //   (Silent).
    private static bool ENV_SILENT(uint32_t _X_) => (_X_) >= ENV_LIMIT;
    private static bool ENV_SILENT(int32_t _X_) => (_X_) >= ENV_LIMIT;

    // pcem: dbopl.cpp:91-92
    private const int RATE_SH = 24;
    private const uint RATE_MASK = (1 << RATE_SH) - 1;

    // pcem: dbopl.cpp:94
    private const int MUL_SH = 16;

    // pcem: dbopl.cpp:102-108
    private static readonly uint8_t[] KslCreateTable =
    {
            //0 will always be be lower than 7 * 8
            64, 32, 24, 19,
            16, 12, 11, 10,
            8, 6, 5, 4,
            3, 2, 1, 0,
    };

    // pcem: dbopl.cpp:110-115 — M(_X_) = (Bit8u)((_X_) * 2), développé.
    private static readonly uint8_t[] FreqCreateTable =
    {
            1, 2, 4, 6, 8, 10, 12, 14,
            16, 18, 20, 20, 24, 24, 30, 30
    };

    // pcem: dbopl.cpp:118-123
    private static readonly uint8_t[] AttackSamplesTable =
    {
            69, 55, 46, 40,
            35, 29, 23, 20,
            19, 15, 11, 10,
            9
    };

    // pcem: dbopl.cpp:125-130
    private static readonly uint8_t[] EnvelopeIncreaseTable =
    {
            4, 5, 6, 7,
            8, 10, 12, 14,
            16, 20, 24, 28,
            32,
    };

    // omitted: ExpTable (:132-134), SinTable (:136-139) — WAVE_HANDLER / WAVE_TABLELOG.

    // pcem: dbopl.cpp:151 — Bit16s WaveTable[8 * 512] (WAVE_TABLEMUL).
    internal static readonly int16_t[] WaveTable = new int16_t[8 * 512];

    // pcem: dbopl.cpp:153-157
    private static readonly uint16_t[] WaveBaseTable =
    {
            0x000, 0x200, 0x200, 0x800,
            0xa00, 0xc00, 0x100, 0x400,
    };

    // pcem: dbopl.cpp:159-162
    private static readonly uint16_t[] WaveMaskTable =
    {
            1023, 1023, 511, 511,
            1023, 1023, 512, 1023,
    };

    // pcem: dbopl.cpp:165-168
    private static readonly uint16_t[] WaveStartTable =
    {
            512, 0, 0, 0,
            0, 512, 512, 256,
    };

    // pcem: dbopl.cpp:172 — Bit16u MulTable[384] (WAVE_TABLEMUL).
    internal static readonly uint16_t[] MulTable = new uint16_t[384];

    // pcem: dbopl.cpp:175-176
    internal static readonly uint8_t[] KslTable = new uint8_t[8 * 16];
    internal static readonly uint8_t[] TremoloTable = new uint8_t[TREMOLO_TABLE];

    // pcem: dbopl.cpp:177-180 — DEVIATION n° 3 : indices et non offsets d'octets (voir l'en-tête).
    //Start of a channel behind the chip struct start
    internal static readonly int16_t[] ChanOffsetTable = new int16_t[32];
    //Start of an operator behind the chip struct start
    internal static readonly int16_t[] OpOffsetTable = new int16_t[64];

    // pcem: dbopl.cpp:185-188
    private static readonly int8_t[] VibratoTable =
    {
            1 - 0x00, 0 - 0x00, 1 - 0x00, 30 - 0x00,
            1 - 0x80, 0 - 0x80, 1 - 0x80, 30 - 0x80
    };

    // pcem: dbopl.cpp:191-193
    private static readonly uint8_t[] KslShiftTable =
    {
            31, 1, 2, 0
    };

    // pcem: dbopl.cpp:196-207
    private static void EnvelopeSelect(uint8_t val, out uint8_t index, out uint8_t shift)
    {
            if (val < 13 * 4) {                                //Rate 0 - 12
                    shift = (uint8_t)(12 - (val >> 2));
                    index = (uint8_t)(val & 3);
            } else if (val < 15 * 4) {                //rate 13 - 14
                    shift = 0;
                    index = (uint8_t)(val - 12 * 4);
            } else {                                                        //rate 15 and up
                    shift = 0;
                    index = 12;
            }
    }

    // omitted: MakeVolume, WaveForm0-7, WaveHandlerTable (:209-279) — WAVE_HANDLER.

    // pcem: dbopl.h:62-75
    internal enum SynthMode
    {
            sm2AM,
            sm2FM,
            sm3AM,
            sm3FM,
            sm4Start,
            sm3FMFM,
            sm3AMFM,
            sm3FMAM,
            sm3AMAM,
            sm6Start,
            sm2Percussion,
            sm3Percussion,
    }

    // pcem: dbopl.h:78-81
    private const int SHIFT_KSLBASE = 16;
    private const int SHIFT_KEYCODE = 24;

    // pcem: dbopl.h:83-174
    internal sealed class Operator
    {
        // pcem: dbopl.h:86-91
        internal const int MASK_KSR = 0x10;
        internal const int MASK_SUSTAIN = 0x20;
        internal const int MASK_VIBRATO = 0x40;
        internal const int MASK_TREMOLO = 0x80;

        // pcem: dbopl.h:93-99 — enum State, en constantes : `state` est un Bit8u et les
        //   rangs servent de décalages (1 << ATTACK).
        internal const int OFF = 0;
        internal const int RELEASE = 1;
        internal const int SUSTAIN = 2;
        internal const int DECAY = 3;
        internal const int ATTACK = 4;

        // pcem: dbopl.h:101 — DEVIATION n° 1 : l'état dont TemplateVolume<> est instancié,
        //   au lieu de &Operator::TemplateVolume<state>. SetState écrit les deux ensemble.
        internal uint8_t volHandler;

        // pcem: dbopl.h:106-108 — DEVIATION n° 4 : waveBase est un indice dans WaveTable.
        internal int waveBase;
        internal uint32_t waveMask;
        internal uint32_t waveStart;

        internal uint32_t waveIndex;   // WAVE_BITS shifted counter of the frequency index
        internal uint32_t waveAdd;     // The base frequency without vibrato
        internal uint32_t waveCurrent; // waveAdd + vibratao

        internal uint32_t chanData;     // Frequency/octave and derived data coming from whatever channel controls this
        internal uint32_t freqMul;      // Scale channel frequency with this, TODO maybe remove?
        internal uint32_t vibrato;      // Scaled up vibrato strength
        internal int32_t sustainLevel; // When stopping at sustain level stop here
        internal int32_t totalLevel;   // totalLevel is added to every generated volume
        internal uint32_t currentLevel; // totalLevel + tremolo
        internal int32_t volume;       // The currently active volume

        internal uint32_t attackAdd; // Timers for the different states of the envelope
        internal uint32_t decayAdd;
        internal uint32_t releaseAdd;
        internal uint32_t rateIndex; // Current position of the evenlope

        internal uint8_t rateZero; // Bits for the different states of the envelope having no changes
        internal uint8_t keyOn;    // Bitmask of different values that can generate keyon
        // Registers, also used to check for changes
        internal uint8_t reg20, reg40, reg60, reg80, regE0;
        // Active part of the envelope we're in
        internal uint8_t state;
        // 0xff when tremolo is enabled
        internal uint8_t tremoloMask;
        // Strength of the vibrato
        internal uint8_t vibStrength;
        // Keep track of the calculated KSR so we can check for changes
        internal uint8_t ksr;

        // pcem: dbopl.cpp:286-296
        //We zero out when rate == 0
        private void UpdateAttack(Chip chip)
        {
                uint8_t rate = (uint8_t)(reg60 >> 4);
                if (rate != 0) {
                        uint8_t val = (uint8_t)((rate << 2) + ksr);
                        attackAdd = chip.attackRates[val];
                        rateZero = (uint8_t)(rateZero & ~(1 << ATTACK));
                } else {
                        attackAdd = 0;
                        rateZero |= (1 << ATTACK);
                }
        }

        // pcem: dbopl.cpp:297-307
        private void UpdateDecay(Chip chip)
        {
                uint8_t rate = (uint8_t)(reg60 & 0xf);
                if (rate != 0) {
                        uint8_t val = (uint8_t)((rate << 2) + ksr);
                        decayAdd = chip.linearRates[val];
                        rateZero = (uint8_t)(rateZero & ~(1 << DECAY));
                } else {
                        decayAdd = 0;
                        rateZero |= (1 << DECAY);
                }
        }

        // pcem: dbopl.cpp:308-324
        private void UpdateRelease(Chip chip)
        {
                uint8_t rate = (uint8_t)(reg80 & 0xf);
                if (rate != 0) {
                        uint8_t val = (uint8_t)((rate << 2) + ksr);
                        releaseAdd = chip.linearRates[val];
                        rateZero = (uint8_t)(rateZero & ~(1 << RELEASE));
                        if ((reg20 & MASK_SUSTAIN) == 0) {
                                rateZero = (uint8_t)(rateZero & ~(1 << SUSTAIN));
                        }
                } else {
                        rateZero |= (1 << RELEASE);
                        releaseAdd = 0;
                        if ((reg20 & MASK_SUSTAIN) == 0) {
                                rateZero |= (1 << SUSTAIN);
                        }
                }
        }

        // pcem: dbopl.cpp:326-333
        internal void UpdateAttenuation()
        {
                uint8_t kslBase = (uint8_t)((chanData >> SHIFT_KSLBASE) & 0xff);
                uint32_t tl = (uint32_t)(reg40 & 0x3f);
                uint8_t kslShift = KslShiftTable[reg40 >> 6];
                //Make sure the attenuation goes to the right bits
                totalLevel = (int32_t)(tl << (ENV_BITS - 7));        //Total level goes 2 bits below max
                totalLevel += (kslBase << ENV_EXTRA) >> kslShift;
        }

        // pcem: dbopl.cpp:335-356 (sans WAVE_PRECISION). `block` vaut au plus 7 : les bits
        //   10-12 de chanData viennent de B0, les bits 16-17 de kslBase, toujours multiple de 4.
        internal void UpdateFrequency()
        {
                uint32_t freq = chanData & ((1 << 10) - 1);
                uint32_t block = (chanData >> 10) & 0xff;
                waveAdd = (freq << (int)block) * freqMul;
                if ((reg20 & MASK_VIBRATO) != 0) {
                        vibStrength = (uint8_t)(freq >> 7);

                        vibrato = (uint32_t)(vibStrength << (int)block) * freqMul;
                } else {
                        vibStrength = 0;
                        vibrato = 0;
                }
        }

        // pcem: dbopl.cpp:358-371
        internal void UpdateRates(Chip chip)
        {
                //Mame seems to reverse this where enabling ksr actually lowers
                //the rate, but pdf manuals says otherwise?
                uint8_t newKsr = (uint8_t)((chanData >> SHIFT_KEYCODE) & 0xff);
                if ((reg20 & MASK_KSR) == 0) {
                        newKsr >>= 2;
                }
                if (ksr == newKsr)
                        return;
                ksr = newKsr;
                UpdateAttack(chip);
                UpdateDecay(chip);
                UpdateRelease(chip);
        }

        // pcem: dbopl.cpp:373-378
        internal int32_t RateForward(uint32_t add)
        {
                rateIndex += add;
                int32_t ret = (int32_t)(rateIndex >> RATE_SH);
                rateIndex = rateIndex & RATE_MASK;
                return ret;
        }

        // pcem: dbopl.cpp:380-425 — template<Operator::State yes>, DEVIATION n° 1.
        internal int32_t TemplateVolume(int yes)
        {
                int32_t vol = volume;
                int32_t change;
                switch (yes) {
                case OFF: return ENV_MAX;
                case ATTACK: change = RateForward(attackAdd);
                        if (change == 0)
                                return vol;
                        vol += ((~vol) * change) >> 3;
                        if (vol < ENV_MIN) {
                                volume = ENV_MIN;
                                rateIndex = 0;
                                SetState(DECAY);
                                return ENV_MIN;
                        }
                        break;
                case DECAY: vol += RateForward(decayAdd);
                        if (vol >= sustainLevel) {
                                //Check if we didn't overshoot max attenuation, then just go off
                                if (vol >= ENV_MAX) {
                                        volume = ENV_MAX;
                                        SetState(OFF);
                                        return ENV_MAX;
                                }
                                //Continue as sustain
                                rateIndex = 0;
                                SetState(SUSTAIN);
                        }
                        break;
                case SUSTAIN:
                        if ((reg20 & MASK_SUSTAIN) != 0) {
                                return vol;
                        }
                        //In sustain phase, but not sustaining, do regular release
                        goto case RELEASE;
                case RELEASE: vol += RateForward(releaseAdd);
                        if (vol >= ENV_MAX) {
                                volume = ENV_MAX;
                                SetState(OFF);
                                return ENV_MAX;
                        }
                        break;
                }
                volume = vol;
                return vol;
        }

        // pcem: dbopl.cpp:427-433 — VolumeHandlerTable[5] : l'indice EST l'état (DEVIATION n° 1).

        // pcem: dbopl.cpp:435-437
        internal uint32_t ForwardVolume()
        {
                return currentLevel + (uint32_t)TemplateVolume(volHandler);
        }

        // pcem: dbopl.cpp:439-442
        internal uint32_t ForwardWave()
        {
                waveIndex += waveCurrent;
                return waveIndex >> WAVE_SH;
        }

        // pcem: dbopl.cpp:444-467
        internal void Write20(Chip chip, uint8_t val)
        {
                uint8_t change = (uint8_t)(reg20 ^ val);
                if (change == 0)
                        return;
                reg20 = val;
                //Shift the tremolo bit over the entire register, saved a branch, YES!
                tremoloMask = (uint8_t)((int8_t)(val) >> 7);
                tremoloMask = (uint8_t)(tremoloMask & ~((1 << ENV_EXTRA) - 1));
                //Update specific features based on changes
                if ((change & MASK_KSR) != 0) {
                        UpdateRates(chip);
                }
                //With sustain enable the volume doesn't change
                if ((reg20 & MASK_SUSTAIN) != 0 || (releaseAdd == 0)) {
                        rateZero |= (1 << SUSTAIN);
                } else {
                        rateZero = (uint8_t)(rateZero & ~(1 << SUSTAIN));
                }
                //Frequency multiplier or vibrato changed
                if ((change & (0xf | MASK_VIBRATO)) != 0) {
                        freqMul = chip.freqMul[val & 0xf];
                        UpdateFrequency();
                }
        }

        // pcem: dbopl.cpp:469-474
        internal void Write40(Chip chip, uint8_t val)
        {
                if ((reg40 ^ val) == 0)
                        return;
                reg40 = val;
                UpdateAttenuation();
        }

        // pcem: dbopl.cpp:476-485
        internal void Write60(Chip chip, uint8_t val)
        {
                uint8_t change = (uint8_t)(reg60 ^ val);
                reg60 = val;
                if ((change & 0x0f) != 0) {
                        UpdateDecay(chip);
                }
                if ((change & 0xf0) != 0) {
                        UpdateAttack(chip);
                }
        }

        // pcem: dbopl.cpp:487-499
        internal void Write80(Chip chip, uint8_t val)
        {
                uint8_t change = (uint8_t)(reg80 ^ val);
                if (change == 0)
                        return;
                reg80 = val;
                uint8_t sustain = (uint8_t)(val >> 4);
                //Turn 0xf into 0x1f
                sustain |= (uint8_t)((sustain + 1) & 0x10);
                sustainLevel = sustain << (ENV_BITS - 5);
                if ((change & 0x0f) != 0) {
                        UpdateRelease(chip);
                }
        }

        // pcem: dbopl.cpp:501-514 (branche #else : WAVE_TABLEMUL). `WaveStartTable[] << WAVE_SH`
        //   vaut 512 << 22 = 2^31 : débordement d'int en C, 0x80000000 une fois rangé dans le
        //   Bit32u ; le décalage non signé du C# rend les mêmes bits.
        internal void WriteE0(Chip chip, uint8_t val)
        {
                if ((regE0 ^ val) == 0)
                        return;
                //in opl3 mode you can always selet 7 waveforms regardless of waveformselect
                uint8_t waveForm = (uint8_t)(val & ((0x3 & chip.waveFormMask) | (0x7 & chip.opl3Active)));
                regE0 = val;
                waveBase = WaveBaseTable[waveForm];
                waveStart = (uint32_t)WaveStartTable[waveForm] << WAVE_SH;
                waveMask = WaveMaskTable[waveForm];
        }

        // pcem: dbopl.cpp:516-519
        private void SetState(uint8_t s)
        {
                state = s;
                volHandler = s;
        }

        // pcem: dbopl.cpp:521-527
        internal bool Silent()
        {
                if (!ENV_SILENT(totalLevel + volume))
                        return false;
                if ((rateZero & (1 << state)) == 0)
                        return false;
                return true;
        }

        // pcem: dbopl.cpp:529-540
        internal void Prepare(Chip chip)
        {
                currentLevel = (uint32_t)(totalLevel + (chip.tremoloValue & tremoloMask));
                waveCurrent = waveAdd;
                if ((vibStrength >> chip.vibratoShift) != 0) {
                        int32_t add = (int32_t)(vibrato >> chip.vibratoShift);
                        //Sign extend over the shift value
                        int32_t neg = chip.vibratoSign;
                        //Negate the add with -1 or 0
                        add = (add ^ neg) - neg;
                        waveCurrent += (uint32_t)add;
                }
        }

        // pcem: dbopl.cpp:542-554 (DBOPL_WAVE > WAVE_HANDLER)
        internal void KeyOn(uint8_t mask)
        {
                if (keyOn == 0) {
                        //Restart the frequency generator
                        waveIndex = waveStart;
                        rateIndex = 0;
                        SetState(ATTACK);
                }
                keyOn |= mask;
        }

        // pcem: dbopl.cpp:556-563
        internal void KeyOff(uint8_t mask)
        {
                keyOn = (uint8_t)(keyOn & ~mask);
                if (keyOn == 0) {
                        if (state != OFF) {
                                SetState(RELEASE);
                        }
                }
        }

        // pcem: dbopl.cpp:565-580 (WAVE_TABLEMUL). `vol` < ENV_LIMIT = 384 à chaque appel
        //   (GetSample et GeneratePercussion testent ENV_SILENT avant) : MulTable[384] tient.
        internal int32_t GetWave(uint32_t index, uint32_t vol)
        {
                return (WaveTable[waveBase + (int)(index & waveMask)] * MulTable[vol >> ENV_EXTRA]) >> MUL_SH;
        }

        // pcem: dbopl.cpp:582-593
        internal int32_t GetSample(int32_t modulation)
        {
                uint32_t vol = ForwardVolume();
                if (ENV_SILENT(vol)) {
                        //Simply forward the wave
                        waveIndex += waveCurrent;
                        return 0;
                } else {
                        uint32_t index = ForwardWave();
                        index += (uint32_t)modulation;
                        return GetWave(index, vol);
                }
        }

        // pcem: dbopl.cpp:595-615. Les champs que le constructeur ne pose pas (waveBase,
        //   waveMask, waveStart, attackAdd, decayAdd, rateIndex, vibrato, vibStrength,
        //   tremoloMask) sont nuls en C# comme dans l'opl[2] statique de sound_dbopl.cc,
        //   zéro-initialisé avant que le constructeur ne coure.
        internal Operator()
        {
                chanData = 0;
                freqMul = 0;
                waveIndex = 0;
                waveAdd = 0;
                waveCurrent = 0;
                keyOn = 0;
                ksr = 0;
                reg20 = 0;
                reg40 = 0;
                reg60 = 0;
                reg80 = 0;
                regE0 = 0;
                SetState(OFF);
                rateZero = (1 << OFF);
                sustainLevel = ENV_MAX;
                currentLevel = ENV_MAX;
                totalLevel = ENV_MAX;
                volume = ENV_MAX;
                releaseAdd = 0;
        }
    }

    // pcem: dbopl.h:176-206
    internal sealed class Channel
    {
        internal readonly Operator[] op = { new Operator(), new Operator() };

        // DEVIATION n° 2 : ce que `this` vaut dans chip->chan[18]. Un Channel n'existe en C
        //   que comme élément de Chip::chan, d'où l'arithmétique `this + n`.
        private readonly Chip owner;
        private readonly int self;

        // `this + n` (dbopl.cpp:663-665, :699-707, :731-735).
        private Channel Chan(int n) => owner.chan[self + n];

        // pcem: dbopl.h:178
        internal Operator Op(int index) { return owner.chan[self + (index >> 1)].op[index & 1]; }

        // pcem: dbopl.h:179 — DEVIATION n° 1.
        internal SynthMode synthHandler;
        internal uint32_t chanData; // Frequency/octave and derived values
        internal readonly int32_t[] old = new int32_t[2];   // Old data for feedback

        internal uint8_t feedback; // Feedback shift
        internal uint8_t regB0;    // Register values to check for changes
        internal uint8_t regC0;
        // This should correspond with reg104, bit 6 indicates a Percussion channel, bit 7 indicates a silent channel
        internal uint8_t fourMask;
        internal int8_t maskLeft; // Sign extended values for both channel's panning
        internal int8_t maskRight;

        // pcem: dbopl.cpp:621-631
        internal Channel(Chip owner, int self)
        {
                this.owner = owner;
                this.self = self;
                old[0] = old[1] = 0;
                chanData = 0;
                regB0 = 0;
                regC0 = 0;
                maskLeft = -1;
                maskRight = -1;
                feedback = 31;
                fourMask = 0;
                synthHandler = SynthMode.sm2FM;
        }

        // pcem: dbopl.cpp:633-649
        internal void SetChanData(Chip chip, uint32_t data)
        {
                uint32_t change = chanData ^ data;
                chanData = data;
                Op(0).chanData = data;
                Op(1).chanData = data;
                //Since a frequency update triggered this, always update frequency
                Op(0).UpdateFrequency();
                Op(1).UpdateFrequency();
                if ((change & (0xffu << SHIFT_KSLBASE)) != 0) {
                        Op(0).UpdateAttenuation();
                        Op(1).UpdateAttenuation();
                }
                if ((change & (0xffu << SHIFT_KEYCODE)) != 0) {
                        Op(0).UpdateRates(chip);
                        Op(1).UpdateRates(chip);
                }
        }

        // pcem: dbopl.cpp:651-667
        internal void UpdateFrequency(Chip chip, uint8_t fourOp)
        {
                //Extrace the frequency bits
                uint32_t data = chanData & 0xffff;
                uint32_t kslBase = KslTable[data >> 6];
                uint32_t keyCode = (data & 0x1c00) >> 9;
                if ((chip.reg08 & 0x40) != 0) {
                        keyCode |= (data & 0x100) >> 8;        /* notesel == 1 */
                } else {
                        keyCode |= (data & 0x200) >> 9;        /* notesel == 0 */
                }
                //Add the keycode and ksl into the highest bits of chanData
                data |= (keyCode << SHIFT_KEYCODE) | (kslBase << SHIFT_KSLBASE);
                Chan(0).SetChanData(chip, data);
                if ((fourOp & 0x3f) != 0) {
                        Chan(1).SetChanData(chip, data);
                }
        }

        // pcem: dbopl.cpp:669-679
        internal void WriteA0(Chip chip, uint8_t val)
        {
                uint8_t fourOp = (uint8_t)(chip.reg104 & chip.opl3Active & fourMask);
                //Don't handle writes to silent fourop channels
                if (fourOp > 0x80)
                        return;
                uint32_t change = (chanData ^ val) & 0xff;
                if (change != 0) {
                        chanData ^= change;
                        UpdateFrequency(chip, fourOp);
                }
        }

        // pcem: dbopl.cpp:681-710
        internal void WriteB0(Chip chip, uint8_t val)
        {
                uint8_t fourOp = (uint8_t)(chip.reg104 & chip.opl3Active & fourMask);
                //Don't handle writes to silent fourop channels
                if (fourOp > 0x80)
                        return;
                uint32_t change = (chanData ^ (uint32_t)(val << 8)) & 0x1f00;
                if (change != 0) {
                        chanData ^= change;
                        UpdateFrequency(chip, fourOp);
                }
                //Check for a change in the keyon/off state
                if (((val ^ regB0) & 0x20) == 0)
                        return;
                regB0 = val;
                if ((val & 0x20) != 0) {
                        Op(0).KeyOn(0x1);
                        Op(1).KeyOn(0x1);
                        if ((fourOp & 0x3f) != 0) {
                                Chan(1).Op(0).KeyOn(1);
                                Chan(1).Op(1).KeyOn(1);
                        }
                } else {
                        Op(0).KeyOff(0x1);
                        Op(1).KeyOff(0x1);
                        if ((fourOp & 0x3f) != 0) {
                                Chan(1).Op(0).KeyOff(1);
                                Chan(1).Op(1).KeyOff(1);
                        }
                }
        }

        // pcem: dbopl.cpp:712-772
        internal void WriteC0(Chip chip, uint8_t val)
        {
                uint8_t change = (uint8_t)(val ^ regC0);
                if (change == 0)
                        return;
                regC0 = val;
                feedback = (uint8_t)((val >> 1) & 7);
                if (feedback != 0) {
                        //We shift the input to the right 10 bit wave index value
                        feedback = (uint8_t)(9 - feedback);
                } else {
                        feedback = 31;
                }
                //Select the new synth mode
                if (chip.opl3Active != 0) {
                        //4-op mode enabled for this channel
                        if (((chip.reg104 & fourMask) & 0x3f) != 0) {
                                Channel chan0, chan1;
                                //Check if it's the 2nd channel in a 4-op
                                if ((fourMask & 0x80) == 0) {
                                        chan0 = this;
                                        chan1 = Chan(1);
                                } else {
                                        chan0 = Chan(-1);
                                        chan1 = this;
                                }

                                uint8_t synth = (uint8_t)(((chan0.regC0 & 1) << 0) | ((chan1.regC0 & 1) << 1));
                                switch (synth) {
                                case 0: chan0.synthHandler = SynthMode.sm3FMFM;
                                        break;
                                case 1: chan0.synthHandler = SynthMode.sm3AMFM;
                                        break;
                                case 2: chan0.synthHandler = SynthMode.sm3FMAM;
                                        break;
                                case 3: chan0.synthHandler = SynthMode.sm3AMAM;
                                        break;
                                }
                                //Disable updating percussion channels
                        } else if ((fourMask & 0x40) != 0 && (chip.regBD & 0x20) != 0) {

                                //Regular dual op, am or fm
                        } else if ((val & 1) != 0) {
                                synthHandler = SynthMode.sm3AM;
                        } else {
                                synthHandler = SynthMode.sm3FM;
                        }
                        maskLeft = (int8_t)((val & 0x10) != 0 ? -1 : 0);
                        maskRight = (int8_t)((val & 0x20) != 0 ? -1 : 0);
                        //opl2 active
                } else {
                        //Disable updating percussion channels
                        if ((fourMask & 0x40) != 0 && (chip.regBD & 0x20) != 0) {

                                //Regular dual op, am or fm
                        } else if ((val & 1) != 0) {
                                synthHandler = SynthMode.sm2AM;
                        } else {
                                synthHandler = SynthMode.sm2FM;
                        }
                }
        }

        // pcem: dbopl.cpp:774-778
        internal void ResetC0(Chip chip)
        {
                uint8_t val = regC0;
                regC0 ^= 0xff;
                WriteC0(chip, val);
        }

        // pcem: dbopl.cpp:780-832 — template<bool opl3Mode> : le booléen devient un paramètre.
        //   Toujours appelé sur chan[6] (seul WriteBD pose sm2/sm3Percussion) : Op(0..5) = chan[6..8].
        private void GeneratePercussion(bool opl3Mode, Chip chip, int32_t[] output, int o)
        {
                Channel chan = this;

                //BassDrum
                int32_t mod = (int32_t)((uint32_t)((old[0] + old[1])) >> feedback);
                old[0] = old[1];
                old[1] = Op(0).GetSample(mod);

                //When bassdrum is in AM mode first operator is ignoed
                if ((chan.regC0 & 1) != 0) {
                        mod = 0;
                } else {
                        mod = old[0];
                }
                int32_t sample = Op(1).GetSample(mod);


                //Precalculate stuff used by other outputs
                uint32_t noiseBit = chip.ForwardNoise() & 0x1;
                uint32_t c2 = Op(2).ForwardWave();
                uint32_t c5 = Op(5).ForwardWave();
                uint32_t phaseBit = (((c2 & 0x88) ^ ((c2 << 5) & 0x80)) | ((c5 ^ (c5 << 2)) & 0x20)) != 0 ? 0x02u : 0x00u;

                //Hi-Hat
                uint32_t hhVol = Op(2).ForwardVolume();
                if (!ENV_SILENT(hhVol)) {
                        uint32_t hhIndex = (phaseBit << 8) | (uint32_t)(0x34 << (int)(phaseBit ^ (noiseBit << 1)));
                        sample += Op(2).GetWave(hhIndex, hhVol);
                }
                //Snare Drum
                uint32_t sdVol = Op(3).ForwardVolume();
                if (!ENV_SILENT(sdVol)) {
                        uint32_t sdIndex = (0x100 + (c2 & 0x100)) ^ (noiseBit << 8);
                        sample += Op(3).GetWave(sdIndex, sdVol);
                }
                //Tom-tom
                sample += Op(4).GetSample(0);

                //Top-Cymbal
                uint32_t tcVol = Op(5).ForwardVolume();
                if (!ENV_SILENT(tcVol)) {
                        uint32_t tcIndex = (1 + phaseBit) << 8;
                        sample += Op(5).GetWave(tcIndex, tcVol);
                }
                sample <<= 1;
                if (opl3Mode) {
                        output[o + 0] += sample;
                        output[o + 1] += sample;
                } else {
                        output[o + 0] += sample;
                }
        }

        // pcem: dbopl.cpp:834-962 — template<SynthMode mode>, DEVIATION n° 1 et 2 : rend le RANG
        //   du canal suivant (le `this + n` du C).
        internal int BlockTemplate(SynthMode mode, Chip chip, uint32_t samples, int32_t[] output, int o)
        {
                switch (mode) {
                case SynthMode.sm2AM:
                case SynthMode.sm3AM:
                        if (Op(0).Silent() && Op(1).Silent()) {
                                old[0] = old[1] = 0;
                                return (self + 1);
                        }
                        break;
                case SynthMode.sm2FM:
                case SynthMode.sm3FM:
                        if (Op(1).Silent()) {
                                old[0] = old[1] = 0;
                                return (self + 1);
                        }
                        break;
                case SynthMode.sm3FMFM:
                        if (Op(3).Silent()) {
                                old[0] = old[1] = 0;
                                return (self + 2);
                        }
                        break;
                case SynthMode.sm3AMFM:
                        if (Op(0).Silent() && Op(3).Silent()) {
                                old[0] = old[1] = 0;
                                return (self + 2);
                        }
                        break;
                case SynthMode.sm3FMAM:
                        if (Op(1).Silent() && Op(3).Silent()) {
                                old[0] = old[1] = 0;
                                return (self + 2);
                        }
                        break;
                case SynthMode.sm3AMAM:
                        if (Op(0).Silent() && Op(2).Silent() && Op(3).Silent()) {
                                old[0] = old[1] = 0;
                                return (self + 2);
                        }
                        break;
                case SynthMode.sm2Percussion:
                case SynthMode.sm3Percussion: break;
                }
                //Init the operators with the the current vibrato and tremolo values
                Op(0).Prepare(chip);
                Op(1).Prepare(chip);
                if (mode > SynthMode.sm4Start) {
                        Op(2).Prepare(chip);
                        Op(3).Prepare(chip);
                }
                if (mode > SynthMode.sm6Start) {
                        Op(4).Prepare(chip);
                        Op(5).Prepare(chip);
                }
                for (uint32_t i = 0; i < samples; i++) {
                        //Early out for percussion handlers
                        if (mode == SynthMode.sm2Percussion) {
                                GeneratePercussion(false, chip, output, o + (int)i);
                                continue;        //Prevent some unitialized value bitching
                        } else if (mode == SynthMode.sm3Percussion) {
                                GeneratePercussion(true, chip, output, o + (int)i * 2);
                                continue;        //Prevent some unitialized value bitching
                        }

                        //Do unsigned shift so we can shift out all bits but still stay in 10 bit range otherwise
                        int32_t mod = (int32_t)((uint32_t)((old[0] + old[1])) >> feedback);
                        old[0] = old[1];
                        old[1] = Op(0).GetSample(mod);
                        // C# : `= 0` pour l'affectation définie ; chaque mode qui atteint ce point
                        //   l'écrit dans la chaîne ci-dessous, comme en C.
                        int32_t sample = 0;
                        int32_t out0 = old[0];
                        if (mode == SynthMode.sm2AM || mode == SynthMode.sm3AM) {
                                sample = out0 + Op(1).GetSample(0);
                        } else if (mode == SynthMode.sm2FM || mode == SynthMode.sm3FM) {
                                sample = Op(1).GetSample(out0);
                        } else if (mode == SynthMode.sm3FMFM) {
                                int32_t next = Op(1).GetSample(out0);
                                next = Op(2).GetSample(next);
                                sample = Op(3).GetSample(next);
                        } else if (mode == SynthMode.sm3AMFM) {
                                sample = out0;
                                int32_t next = Op(1).GetSample(0);
                                next = Op(2).GetSample(next);
                                sample += Op(3).GetSample(next);
                        } else if (mode == SynthMode.sm3FMAM) {
                                sample = Op(1).GetSample(out0);
                                int32_t next = Op(2).GetSample(0);
                                sample += Op(3).GetSample(next);
                        } else if (mode == SynthMode.sm3AMAM) {
                                sample = out0;
                                int32_t next = Op(1).GetSample(0);
                                sample += Op(2).GetSample(next);
                                sample += Op(3).GetSample(0);
                        }
                        switch (mode) {
                        case SynthMode.sm2AM:
                        case SynthMode.sm2FM:
                                if (chip.is_opl3 != 0) {
                                        output[o + (int)i * 2 + 0] += sample;
                                        output[o + (int)i * 2 + 1] += sample;
                                } else
                                        output[o + (int)i] += sample;
                                break;
                        case SynthMode.sm3AM:
                        case SynthMode.sm3FM:
                        case SynthMode.sm3FMFM:
                        case SynthMode.sm3AMFM:
                        case SynthMode.sm3FMAM:
                        case SynthMode.sm3AMAM: output[o + (int)i * 2 + 0] += sample & maskLeft;
                                output[o + (int)i * 2 + 1] += sample & maskRight;
                                break;
                        case SynthMode.sm2Percussion:
                        case SynthMode.sm3Percussion: break;
                        }
                }
                switch (mode) {
                case SynthMode.sm2AM:
                case SynthMode.sm2FM:
                case SynthMode.sm3AM:
                case SynthMode.sm3FM: return (self + 1);
                case SynthMode.sm3FMFM:
                case SynthMode.sm3AMFM:
                case SynthMode.sm3FMAM:
                case SynthMode.sm3AMAM: return (self + 2);
                case SynthMode.sm2Percussion:
                case SynthMode.sm3Percussion: return (self + 3);
                }
                // `return 0` : le pointeur nul du C, pour sm4Start/sm6Start, qu'aucun site ne
                //   pose comme synthHandler. Inatteignable ; -1 ferait lever chan[-1] au lieu
                //   de reboucler sur chan[0].
                return -1;
        }
    }

    // pcem: dbopl.h:208-261
    internal sealed class Chip
    {
        // This is used as the base counter for vibrato and tremolo
        internal uint32_t lfoCounter;
        internal uint32_t lfoAdd;

        internal uint32_t noiseCounter;
        internal uint32_t noiseAdd;
        internal uint32_t noiseValue;

        // Frequency scales for the different multiplications
        internal readonly uint32_t[] freqMul = new uint32_t[16];
        // Rates for decay and release for rate of this chip
        internal readonly uint32_t[] linearRates = new uint32_t[76];
        // Best match attack rates for the rate of this chip
        internal readonly uint32_t[] attackRates = new uint32_t[76];

        // 18 channels with 2 operators each
        internal readonly Channel[] chan = new Channel[18];

        internal uint8_t reg104;
        internal uint8_t reg08;
        internal uint8_t reg04;
        internal uint8_t regBD;
        internal uint8_t vibratoIndex;
        internal uint8_t tremoloIndex;
        internal int8_t vibratoSign;
        internal uint8_t vibratoShift;
        internal uint8_t tremoloValue;
        internal uint8_t vibratoStrength;
        internal uint8_t tremoloStrength;
        // Mask for allowed wave forms
        internal uint8_t waveFormMask;
        // 0 or -1 when enabled
        internal int8_t opl3Active;

        internal int is_opl3;

        // pcem: dbopl.cpp:968-974. La boucle construit les 18 membres chan[] que le C++
        //   construit implicitement avant le corps (Channel::Channel, :621-631).
        internal Chip()
        {
                for (int i = 0; i < 18; i++)
                        chan[i] = new Channel(this, i);
                reg08 = 0;
                reg04 = 0;
                regBD = 0;
                reg104 = 0;
                opl3Active = 0;
        }

        // pcem: dbopl.cpp:976-986. Masque par WAVE_MASK et non par (1 << LFO_SH) - 1 : comme en C.
        internal uint32_t ForwardNoise()
        {
                noiseCounter += noiseAdd;
                uint32_t count = noiseCounter >> LFO_SH;
                noiseCounter &= WAVE_MASK;
                for (; count > 0; --count) {
                        //Noise calculation from mame
                        noiseValue ^= (0x800302) & (0 - (noiseValue & 1));
                        noiseValue >>= 1;
                }
                return noiseValue;
        }

        // pcem: dbopl.cpp:988-1012
        internal uint32_t ForwardLFO(uint32_t samples)
        {
                //Current vibrato value, runs 4x slower than tremolo
                vibratoSign = (int8_t)((VibratoTable[vibratoIndex >> 2]) >> 7);
                vibratoShift = (uint8_t)((VibratoTable[vibratoIndex >> 2] & 7) + vibratoStrength);
                tremoloValue = (uint8_t)(TremoloTable[tremoloIndex] >> tremoloStrength);

                //Check hom many samples there can be done before the value changes
                uint32_t todo = LFO_MAX - lfoCounter;
                uint32_t count = (todo + lfoAdd - 1) / lfoAdd;
                if (count > samples) {
                        count = samples;
                        lfoCounter += count * lfoAdd;
                } else {
                        lfoCounter += count * lfoAdd;
                        lfoCounter &= (LFO_MAX - 1);
                        //Maximum of 7 vibrato value * 4
                        vibratoIndex = (uint8_t)((vibratoIndex + 1) & 31);
                        //Clip tremolo to the the table size
                        if (tremoloIndex + 1 < TREMOLO_TABLE)
                                ++tremoloIndex;
                        else
                                tremoloIndex = 0;
                }
                return count;
        }

        // pcem: dbopl.cpp:1014-1074
        internal void WriteBD(uint8_t val)
        {
                uint8_t change = (uint8_t)(regBD ^ val);
                if (change == 0)
                        return;
                regBD = val;
                //TODO could do this with shift and xor?
                vibratoStrength = (uint8_t)((val & 0x40) != 0 ? 0x00 : 0x01);
                tremoloStrength = (uint8_t)((val & 0x80) != 0 ? 0x00 : 0x02);
                if ((val & 0x20) != 0) {
                        //Drum was just enabled, make sure channel 6 has the right synth
                        if ((change & 0x20) != 0) {
                                if (is_opl3 != 0) {
                                        chan[6].synthHandler = SynthMode.sm3Percussion;
                                } else {
                                        chan[6].synthHandler = SynthMode.sm2Percussion;
                                }
                        }
                        //Bass Drum
                        if ((val & 0x10) != 0) {
                                chan[6].op[0].KeyOn(0x2);
                                chan[6].op[1].KeyOn(0x2);
                        } else {
                                chan[6].op[0].KeyOff(0x2);
                                chan[6].op[1].KeyOff(0x2);
                        }
                        //Hi-Hat
                        if ((val & 0x1) != 0) {
                                chan[7].op[0].KeyOn(0x2);
                        } else {
                                chan[7].op[0].KeyOff(0x2);
                        }
                        //Snare
                        if ((val & 0x8) != 0) {
                                chan[7].op[1].KeyOn(0x2);
                        } else {
                                chan[7].op[1].KeyOff(0x2);
                        }
                        //Tom-Tom
                        if ((val & 0x4) != 0) {
                                chan[8].op[0].KeyOn(0x2);
                        } else {
                                chan[8].op[0].KeyOff(0x2);
                        }
                        //Top Cymbal
                        if ((val & 0x2) != 0) {
                                chan[8].op[1].KeyOn(0x2);
                        } else {
                                chan[8].op[1].KeyOff(0x2);
                        }
                        //Toggle keyoffs when we turn off the percussion
                } else if ((change & 0x20) != 0) {
                        //Trigger a reset to setup the original synth handler
                        chan[6].ResetC0(this);
                        chan[6].op[0].KeyOff(0x2);
                        chan[6].op[1].KeyOff(0x2);
                        chan[7].op[0].KeyOff(0x2);
                        chan[7].op[1].KeyOff(0x2);
                        chan[8].op[0].KeyOff(0x2);
                        chan[8].op[1].KeyOff(0x2);
                }
        }

        // pcem: dbopl.cpp:1076-1081 — REGOP(_FUNC_) : l'opérateur, ou null là où le C saute
        //   l'appel (OpOffsetTable[index] == 0). DEVIATION n° 3. index < 64 pour reg < 0x200.
        private Operator? REGOP(uint32_t reg)
        {
                uint32_t index = ((reg >> 3) & 0x20) | (reg & 0x1f);
                if (OpOffsetTable[index] >= 0) {
                        Operator regOp = chan[OpOffsetTable[index] >> 1].op[OpOffsetTable[index] & 1];
                        return regOp;
                }
                return null;
        }

        // pcem: dbopl.cpp:1083-1088 — REGCHAN(_FUNC_), même forme. index < 32.
        private Channel? REGCHAN(uint32_t reg)
        {
                uint32_t index = ((reg >> 4) & 0x10) | (reg & 0xf);
                if (ChanOffsetTable[index] >= 0) {
                        Channel regChan = chan[ChanOffsetTable[index]];
                        return regChan;
                }
                return null;
        }

        // pcem: dbopl.cpp:1090-1142. Les deux chutes du C (case 0x00 -> 0x10, case 0xc0 -> 0xd0)
        //   tombent sur un `break` : elles s'écrivent `break` ici.
        internal void WriteReg(uint32_t reg, uint8_t val)
        {
                switch ((reg & 0xf0) >> 4) {
                case 0x00 >> 4:
                        if (reg == 0x01) {
                                waveFormMask = (uint8_t)((val & 0x20) != 0 ? 0x7 : 0x0);
                        } else if (reg == 0x104) {
                                //Only detect changes in lowest 6 bits
                                if (((reg104 ^ val) & 0x3f) == 0)
                                        return;
                                //Always keep the highest bit enabled, for checking > 0x80
                                reg104 = (uint8_t)(0x80 | (val & 0x3f));
                        } else if (reg == 0x105) {
                                //MAME says the real opl3 doesn't reset anything on opl3 disable/enable till the next write in another register
                                if (((opl3Active ^ val) & 1) == 0)
                                        return;
                                opl3Active = (int8_t)((val & 1) != 0 ? -1 : 0);   // 0xff dans un Bit8s
                                //Update the 0xc0 register for all channels to signal the switch to mono/stereo handlers
                                for (int i = 0; i < 18; i++) {
                                        chan[i].ResetC0(this);
                                }
                        } else if (reg == 0x08) {
                                reg08 = val;
                        }
                        break;
                case 0x10 >> 4: break;
                case 0x20 >> 4:
                case 0x30 >> 4: REGOP(reg)?.Write20(this, val);
                        break;
                case 0x40 >> 4:
                case 0x50 >> 4: REGOP(reg)?.Write40(this, val);
                        break;
                case 0x60 >> 4:
                case 0x70 >> 4: REGOP(reg)?.Write60(this, val);
                        break;
                case 0x80 >> 4:
                case 0x90 >> 4: REGOP(reg)?.Write80(this, val);
                        break;
                case 0xa0 >> 4: REGCHAN(reg)?.WriteA0(this, val);
                        break;
                case 0xb0 >> 4:
                        if (reg == 0xbd) {
                                WriteBD(val);
                        } else {
                                REGCHAN(reg)?.WriteB0(this, val);
                        }
                        break;
                case 0xc0 >> 4: REGCHAN(reg)?.WriteC0(this, val);
                        break;
                case 0xd0 >> 4: break;
                case 0xe0 >> 4:
                case 0xf0 >> 4: REGOP(reg)?.WriteE0(this, val);
                        break;
                }
        }

        // pcem: dbopl.cpp:1144-1154
        internal uint32_t WriteAddr(uint32_t port, uint8_t val)
        {
                switch (port & 3) {
                case 0: return val;
                case 2:
                        if (opl3Active != 0 || (val == 0x05))
                                return 0x100u | val;
                        else
                                return val;
                }
                return 0;
        }

        // pcem: dbopl.cpp:1156-1168
        internal void GenerateBlock2(uint32_t total, int32_t[] output, int o)
        {
                while (total > 0) {
                        uint32_t samples = ForwardLFO(total);
                        Array.Clear(output, o, (int)samples);
                        int count = 0;
                        for (int ch = 0; ch < 9;) {
                                count++;
                                ch = chan[ch].BlockTemplate(chan[ch].synthHandler, this, samples, output, o);
                        }
                        total -= samples;
                        o += (int)samples;
                }
        }

        // pcem: dbopl.cpp:1170-1182
        internal void GenerateBlock3(uint32_t total, int32_t[] output, int o)
        {
                while (total > 0) {
                        uint32_t samples = ForwardLFO(total);
                        Array.Clear(output, o, (int)(samples * 2));
                        int count = 0;
                        for (int ch = 0; ch < 18;) {
                                count++;
                                ch = chan[ch].BlockTemplate(chan[ch].synthHandler, this, samples, output, o);
                        }
                        total -= samples;
                        o += (int)(samples * 2);
                }
        }

        // pcem: dbopl.cpp:1184-1308 (sans WAVE_PRECISION). Le `Bit32s original` de :1227 masque
        //   en C++ le `double original` de :1185 ; C# interdit ce masquage : il s'appelle
        //   `original_` ici. `(guessAdd * mul)` (:1259, :1263) déborde un int pour les taux
        //   rapides (69 M x 4608 au taux 15) : le C enveloppe en pratique (imul 32 bits), le C#
        //   non vérifié aussi.
        internal void Setup(uint32_t rate, int chip_is_opl3)
        {
                double original = OPLRATE;
//	double original = rate;
                double scale = original / (double)rate;

                is_opl3 = chip_is_opl3;

                //Noise counter is run at the same precision as general waves
                noiseAdd = (uint32_t)(0.5 + scale * (1 << LFO_SH));
                noiseCounter = 0;
                noiseValue = 1;        //Make sure it triggers the noise xor the first time
                //The low frequency oscillation counter
                //Every time his overflows vibrato and tremoloindex are increased
                lfoAdd = (uint32_t)(0.5 + scale * (1 << LFO_SH));
                lfoCounter = 0;
                vibratoIndex = 0;
                tremoloIndex = 0;

                //With higher octave this gets shifted up
                //-1 since the freqCreateTable = *2
                uint32_t freqScale = (uint32_t)(0.5 + scale * (1 << (WAVE_SH - 1 - 10)));
                for (int i = 0; i < 16; i++) {
                        freqMul[i] = freqScale * FreqCreateTable[i];
                }

                //-3 since the real envelope takes 8 steps to reach the single value we supply
                for (uint8_t i = 0; i < 76; i++) {
                        uint8_t index, shift;
                        EnvelopeSelect(i, out index, out shift);
                        linearRates[i] = (uint32_t)(scale * (EnvelopeIncreaseTable[index] << (RATE_SH + ENV_EXTRA - shift - 3)));
                }
                //Generate the best matching attack rate
                for (uint8_t i = 0; i < 62; i++) {
                        uint8_t index, shift;
                        EnvelopeSelect(i, out index, out shift);
                        //Original amount of samples the attack would take
                        int32_t original_ = (int32_t)(uint32_t)((AttackSamplesTable[index] << shift) / scale);

                        int32_t guessAdd = (int32_t)(uint32_t)(scale * (EnvelopeIncreaseTable[index] << (RATE_SH - shift - 3)));
                        int32_t bestAdd = guessAdd;
                        uint32_t bestDiff = 1 << 30;
                        for (uint32_t passes = 0; passes < 16; passes++) {
                                int32_t volume = ENV_MAX;
                                int32_t samples = 0;
                                uint32_t count = 0;
                                while (volume > 0 && samples < original_ * 2) {
                                        count += (uint32_t)guessAdd;
                                        int32_t change = (int32_t)(count >> RATE_SH);
                                        count &= RATE_MASK;
                                        if (change != 0) { // less than 1 %
                                                volume += (~volume * change) >> 3;
                                        }
                                        samples++;

                                }
                                int32_t diff = original_ - samples;
                                uint32_t lDiff = (uint32_t)Math.Abs((long)diff);
                                //Init last on first pass
                                if (lDiff < bestDiff) {
                                        bestDiff = lDiff;
                                        bestAdd = guessAdd;
                                        if (bestDiff == 0)
                                                break;
                                }
                                //Below our target
                                if (diff < 0) {
                                        //Better than the last time
                                        int32_t mul = ((original_ - diff) << 12) / original_;
                                        guessAdd = ((guessAdd * mul) >> 12);
                                        guessAdd++;
                                } else if (diff > 0) {
                                        int32_t mul = ((original_ - diff) << 12) / original_;
                                        guessAdd = (guessAdd * mul) >> 12;
                                        guessAdd--;
                                }
                        }
                        attackRates[i] = (uint32_t)bestAdd;
                }
                for (uint8_t i = 62; i < 76; i++) {
                        //This should provide instant volume maximizing
                        attackRates[i] = 8 << RATE_SH;
                }
                //Setup the channels with the correct four op flags
                //Channels are accessed through a table so they appear linear here
                chan[0].fourMask = 0x00 | (1 << 0);
                chan[1].fourMask = 0x80 | (1 << 0);
                chan[2].fourMask = 0x00 | (1 << 1);
                chan[3].fourMask = 0x80 | (1 << 1);
                chan[4].fourMask = 0x00 | (1 << 2);
                chan[5].fourMask = 0x80 | (1 << 2);

                chan[9].fourMask = 0x00 | (1 << 3);
                chan[10].fourMask = 0x80 | (1 << 3);
                chan[11].fourMask = 0x00 | (1 << 4);
                chan[12].fourMask = 0x80 | (1 << 4);
                chan[13].fourMask = 0x00 | (1 << 5);
                chan[14].fourMask = 0x80 | (1 << 5);

                //mark the percussion channels
                chan[6].fourMask = 0x40;
                chan[7].fourMask = 0x40;
                chan[8].fourMask = 0x40;

                //Clear Everything in opl3 mode
                WriteReg(0x105, 0x1);
                for (int i = 0; i < 512; i++) {
                        if (i == 0x105)
                                continue;
                        WriteReg((uint32_t)i, 0xff);
                        WriteReg((uint32_t)i, 0x0);
                }
                WriteReg(0x105, 0x0);
                //Clear everything in opl2 mode
                for (int i = 0; i < 255; i++) {
                        WriteReg((uint32_t)i, 0xff);
                        WriteReg((uint32_t)i, 0x0);
                }
        }
    }

    // pcem: dbopl.cpp:1310
    private static bool doneTables = false;

    // pcem: dbopl.cpp:1311-1464 (WAVE_TABLEMUL)
    // omitted: les blocs WAVE_HANDLER / WAVE_TABLELOG (:1315-1331, :1352-1363) — hors de ce mode.
    // omitted: les vérifications `#if 0` (:1437-1463).
    internal static void InitTables()
    {
        if (doneTables)
                return;
        doneTables = true;
        //Multiplication based tables
        for (int i = 0; i < 384; i++)
        {
                int s = i * 8;
                //TODO maybe keep some of the precision errors of the original table?
                double val = (0.5 + (Math.Pow(2.0, -1.0 + (255 - s) * (1.0 / 256))) * (1 << MUL_SH));
                MulTable[i] = (uint16_t)(val);
        }

        //Sine Wave Base
        for (int i = 0; i < 512; i++)
        {
                WaveTable[0x0200 + i] = (int16_t)(Math.Sin((i + 0.5) * (PI / 512.0)) * 4084);
                WaveTable[0x0000 + i] = (int16_t)(-WaveTable[0x200 + i]);
        }
        //Exponential wave
        for (int i = 0; i < 256; i++)
        {
                WaveTable[0x700 + i] = (int16_t)(0.5 + (Math.Pow(2.0, -1.0 + (255 - i * 8) * (1.0 / 256))) * 4085);
                WaveTable[0x6ff - i] = (int16_t)(-WaveTable[0x700 + i]);
        }

        for (int i = 0; i < 256; i++)
        {
                //Fill silence gaps
                WaveTable[0x400 + i] = WaveTable[0];
                WaveTable[0x500 + i] = WaveTable[0];
                WaveTable[0x900 + i] = WaveTable[0];
                WaveTable[0xc00 + i] = WaveTable[0];
                WaveTable[0xd00 + i] = WaveTable[0];
                //Replicate sines in other pieces
                WaveTable[0x800 + i] = WaveTable[0x200 + i];
                //double speed sines
                WaveTable[0xa00 + i] = WaveTable[0x200 + i * 2];
                WaveTable[0xb00 + i] = WaveTable[0x000 + i * 2];
                WaveTable[0xe00 + i] = WaveTable[0x200 + i * 2];
                WaveTable[0xf00 + i] = WaveTable[0x200 + i * 2];
        }

        //Create the ksl table
        for (int oct = 0; oct < 8; oct++)
        {
                int @base = oct * 8;
                for (int i = 0; i < 16; i++)
                {
                        int val = @base - KslCreateTable[i];
                        if (val < 0)
                                val = 0;
                        //*4 for the final range to match attenuation range
                        KslTable[oct * 16 + i] = (uint8_t)(val * 4);
                }
        }
        //Create the Tremolo table, just increase and decrease a triangle wave
        for (uint8_t i = 0; i < TREMOLO_TABLE / 2; i++)
        {
                uint8_t val = (uint8_t)(i << ENV_EXTRA);
                TremoloTable[i] = val;
                TremoloTable[TREMOLO_TABLE - 1 - i] = val;
        }
        // pcem: dbopl.cpp:1404-1421 — DEVIATION n° 3 : `&chip->chan[index]` devient `index`,
        //   le 0 de « pas de canal » devient -1.
        //Create a table with offsets of the channels from the start of the chip
        for (uint32_t i = 0; i < 32; i++)
        {
                uint32_t index = i & 0xf;
                if (index >= 9)
                {
                        ChanOffsetTable[i] = -1;
                        continue;
                }
                //Make sure the four op channels follow eachother
                if (index < 6)
                {
                        index = (index % 3) * 2 + (index / 3);
                }
                //Add back the bits for highest ones
                if (i >= 16)
                        index += 9;
                ChanOffsetTable[i] = (int16_t)index;
        }
        // pcem: dbopl.cpp:1422-1436 — `ChanOffsetTable[chNum] + &chan->op[opNum]` devient
        //   2 * ChanOffsetTable[chNum] + opNum ; -1 pour « pas d'opérateur ».
        //Same for operators
        for (uint32_t i = 0; i < 64; i++)
        {
                if (i % 8 >= 6 || ((i / 8) % 4 == 3))
                {
                        OpOffsetTable[i] = -1;
                        continue;
                }
                uint32_t chNum = (i / 8) * 3 + (i % 8) % 3;
                //Make sure we use 16 and up for the 2nd range to match the chanoffset gap
                if (chNum >= 12)
                        chNum += 16 - 12;
                uint32_t opNum = (i % 8) / 3;
                OpOffsetTable[i] = (int16_t)(ChanOffsetTable[chNum] * 2 + (int)opNum);
        }
    }
}
