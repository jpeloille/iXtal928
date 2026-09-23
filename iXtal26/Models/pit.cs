// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/models/pit.c  (struct PIT/PIT_nr : includes/private/ibm.h:73-105)
// STATUS: partial — setpitclock et le 8253/8254 du 5150 : pit_reset, pit_set_out,
//         pit_read_timer, pit_dump_and_disable_timer, pit_load, pit_set_gate(_no_timer),
//         pit_over, pit_write, pit_read, pit_timer_over, pit_clock,
//         pit_set_using_timer, pit_set_out_func, pit_null_timer, pit_irq0_timer,
//         pit_refresh_timer_xt, pit_speaker_timer, pit_init, et depuis B3
//         pit_refresh_timer_at. Omis : pit2 et tout le PS/2, le PCjr, et les deux
//         globales mortes de pit.c. Cette ligne omettait « l'AT » en bloc, ce qui
//         n'est plus vrai : son rafraîchissement mémoire est transcrit.

// CS8618 : `PIT_nr.pit` transcrit `struct PIT *pit` (ibm.h:75). Le C le laisse
// indéterminé jusqu'à pit_init (pit.c:594) ; aucun constructeur ne peut le poser
// ici sans inventer une dépendance que le C n'a pas.
// CS8602 : `set_out_funcs[3]` est un tableau de pointeurs de fonction que
// memset() met à NULL et que pit_init renseigne. Le déréférencement de
// pit_set_out (pit.c:84) est exactement celui du C ; un `!` par site réécrirait
// la ligne, comme dans io.cs.
// CS8600 : `(PIT_nr *)p` (pit.c:488) part d'un void* que le delegate de timer.cs
// déclare `object?`.
#pragma warning disable CS8618, CS8602, CS8600

using iXtal26.Cpu;

namespace iXtal26.Models;

/*IBM AT -
  Write B0
  Write aa55
  Expects aa55 back*/

// pcem: ibm.h:73-76
internal sealed class PIT_nr
{
    internal int nr;
    internal PIT pit;
}

// pcem: ibm.h:104 — void (*set_out_funcs[3])(int new_out, int old_out)
internal delegate void set_out_func_t(int new_out, int old_out);

// pcem: ibm.h:78-105
internal sealed class PIT
{
    internal uint32_t[] l = new uint32_t[3];
    internal pc_timer_t[] timer = { new(), new(), new() };
    internal uint8_t[] m = new uint8_t[3];
    internal uint8_t ctrl;
    internal uint8_t[] ctrls = new uint8_t[3];
    internal int wp;
    internal int[] rm = new int[3], wm = new int[3];
    internal uint16_t[] rl = new uint16_t[3];
    internal int[] thit = new int[3];
    internal int[] delay = new int[3];
    internal int[] rereadlatch = new int[3];
    internal int[] gate = new int[3];
    internal int[] @out = new int[3];   // `out` est un mot-clé C#
    internal int[] running = new int[3];
    internal int[] enabled = new int[3];
    internal int[] newcount = new int[3];
    internal int[] count = new int[3];
    internal int[] using_timer = new int[3];
    internal int[] initial = new int[3];
    internal int[] latched = new int[3];
    internal int[] disabled = new int[3];

    internal uint8_t[] read_status = new uint8_t[3];
    internal int[] do_read_status = new int[3];

    internal PIT_nr[] pit_nr = { new(), new(), new() };

    internal set_out_func_t?[] set_out_funcs = new set_out_func_t?[3];
}

internal static partial class pit
{
    // CS0542: la globale `PIT pit` (pit.c:20, ibm.h:107) ne peut pas porter le
    // nom de sa classe conteneur. Même renommage que `PIC pic_` (pic.cs:35).
    internal static readonly PIT pit_ = new();

    // omitted: PIT pit2 (pit.c:20) et ses trois fonctions — pit_ps2_init
    //          (pit.c:604-621), pit_irq0_ps2 (pit.c:541-551), pit_nmi_ps2
    //          (pit.c:576-580) — machines PS/2, hors cible 5150.
    // omitted: pit_irq0_timer_pcjr (pit.c:532-539) — PCjr.
    // omitted: pit_refresh_timer_at (pit.c:558-561) — AT ; seul lecteur de
    //          ppi.pb dans ce fichier.
    // omitted: pit_timer0_freq (pit.c:76-82) — ses deux seuls appelants sont
    //          wx-sdl2-status.c:135 et qt-sdl2-status.c:135, remplacés par l'hôte.
    // omitted: int displine (pit.c:25) — globale morte : aucun `extern displine`
    //          dans includes/, et les homonymes de vid_hercules.c et
    //          vid_tvp3026_ramdac.c sont un membre de struct et un paramètre.
    // omitted: int firsttime (pit.c:36) — globale morte : zéro lecteur dans
    //          l'arbre PCem.

    /*B0 to 40, two writes to 43, then two reads - value does not change!*/
    /*B4 to 40, two writes to 43, then two reads - value _does_ change!*/
    // Tyrian writes 4300 or 17512

    // pcem: pit.c:27-34
    internal static uint64_t PITCONST;
    internal static uint64_t CGACONST;
    internal static uint64_t MDACONST;
    internal static uint64_t VGACONST1, VGACONST2;
    internal static uint64_t RTCCONST;

    internal static float cpuclock;
    internal static float isa_timing, bus_timing;

    // pcem: pit.c:37-59
    internal static void setpitclock(float clock)
    {
        cpuclock = clock;
        PITCONST = (uint64_t)(clock / 1193182.0 * (float)(1UL << 32));
        CGACONST = (uint64_t)((clock / (19687503.0 / 11.0)) * (float)(1UL << 32));
        MDACONST = (uint64_t)((clock / 2032125.0) * (float)(1UL << 32));
        VGACONST1 = (uint64_t)((clock / 25175000.0) * (float)(1UL << 32));
        VGACONST2 = (uint64_t)((clock / 28322000.0) * (float)(1UL << 32));
        isa_timing = (float)(clock / 8000000.0);
        bus_timing = (float)(clock / (double)cpu.cpu_busspeed);
        Video.video.video_updatetiming();

        _808x.xt_cpu_multi = (uint64_t)((14318184.0 * (double)(1UL << 32)) / (double)cpu.cpu_get_speed());
        RTCCONST = (uint64_t)((clock / 32768.0) * (float)(1UL << 32));
        timer.TIMER_USEC = (uint64_t)((clock / 1000000.0) * (float)(1UL << 32));
        PluginApi.device.device_speed_changed();
    }

    // pcem: pit.c:63-75
    internal static void pit_reset(PIT pit)
    {
        // pcem: pit.c:64 — memset(pit, 0, sizeof(PIT)), champ à champ.
        // DEVIATION: les pc_timer_t et PIT_nr imbriqués sont des objets ici, donc
        //   réalloués. Sur un SECOND pit_reset, le C garde l'adresse et remet
        //   prev/next à zéro — il corrompt la liste de timer.c ; ici l'ancien
        //   pc_timer_t reste dans timer_head et son rappel continue de tirer
        //   pit_over sur l'ancien PIT_nr. Inatteignable tant que pit_reset n'est
        //   appelé que depuis pit_init, mais pit.h:6 l'expose.
        for (int t = 0; t < 3; t++)
        {
                pit.l[t] = 0;
                pit.timer[t] = new pc_timer_t();
                pit.m[t] = 0;
                pit.ctrls[t] = 0;
                pit.rm[t] = pit.wm[t] = 0;
                pit.rl[t] = 0;
                pit.thit[t] = 0;
                pit.delay[t] = 0;
                pit.rereadlatch[t] = 0;
                pit.gate[t] = 0;
                pit.@out[t] = 0;
                pit.running[t] = 0;
                pit.enabled[t] = 0;
                pit.newcount[t] = 0;
                pit.count[t] = 0;
                pit.using_timer[t] = 0;
                pit.initial[t] = 0;
                pit.latched[t] = 0;
                pit.disabled[t] = 0;
                pit.read_status[t] = 0;
                pit.do_read_status[t] = 0;
                pit.pit_nr[t] = new PIT_nr();
                pit.set_out_funcs[t] = null;
        }
        pit.ctrl = 0;
        pit.wp = 0;

        pit.l[0] = 0xFFFF;
        pit.l[1] = 0xFFFF;
        pit.l[2] = 0xFFFF;
        pit.m[0] = pit.m[1] = pit.m[2] = 0;
        pit.ctrls[0] = pit.ctrls[1] = pit.ctrls[2] = 0;
        pit.thit[0] = 1;
        pit.gate[0] = pit.gate[1] = 1;
        pit.gate[2] = 0;
        pit.using_timer[0] = pit.using_timer[1] = pit.using_timer[2] = 1;
    }

    // pcem: pit.c:83-87
    private static void pit_set_out(PIT pit, int t, int @out)
    {
        pit.set_out_funcs[t](@out, pit.@out[t]);
        pit.@out[t] = @out;
    }

    // pcem: pit.c:88-110
    private static int pit_read_timer(PIT pit, int t)
    {
        if (pit.using_timer[t] != 0 && !(pit.m[t] == 3 && pit.gate[t] == 0) && timer.timer_is_enabled(pit.timer[t]) != 0)
        {
                int read = (int)((timer.timer_get_remaining_u64(pit.timer[t])) / PITCONST);
                if (pit.m[t] == 2)
                        read++;
                if (read < 0)
                        read = 0;
                if (read > 0x10000)
                        read = 0x10000;
                if (pit.m[t] == 3)
                        read <<= 1;
                return read;
        }
        if (pit.m[t] == 2)
                return pit.count[t] + 1;
        return pit.count[t];
    }

    /*Dump timer count back to pit->count[], and disable timer. This should be used
      when stopping a PIT timer, to ensure the correct value can be read back.*/
    // pcem: pit.c:111-117
    private static void pit_dump_and_disable_timer(PIT pit, int t)
    {
        if (pit.using_timer[t] != 0 && timer.timer_is_enabled(pit.timer[t]) != 0)
        {
                pit.count[t] = pit_read_timer(pit, t);
                timer.timer_disable(pit.timer[t]);
        }
    }

    // pcem: pit.c:118-180
    private static void pit_load(PIT pit, int t)
    {
        int l = pit.l[t] != 0 ? (int)pit.l[t] : 0x10000;

        pit.newcount[t] = 0;
        pit.disabled[t] = 0;
        switch (pit.m[t])
        {
        case 0: /*Interrupt on terminal count*/
                pit.count[t] = l;
                if (pit.using_timer[t] != 0)
                        timer.timer_set_delay_u64(pit.timer[t], (uint64_t)l * PITCONST);
                pit_set_out(pit, t, 0);
                pit.thit[t] = 0;
                pit.enabled[t] = pit.gate[t];
                break;
        case 1: /*Hardware retriggerable one-shot*/
                pit.enabled[t] = 1;
                break;
        case 2: /*Rate generator*/
                if (pit.initial[t] != 0)
                {
                        pit.count[t] = l - 1;
                        if (pit.using_timer[t] != 0)
                                timer.timer_set_delay_u64(pit.timer[t], (uint64_t)(l - 1) * PITCONST);
                        pit_set_out(pit, t, 1);
                        pit.thit[t] = 0;
                }
                pit.enabled[t] = pit.gate[t];
                break;
        case 3: /*Square wave mode*/
                if (pit.initial[t] != 0)
                {
                        pit.count[t] = l;
                        if (pit.using_timer[t] != 0)
                                timer.timer_set_delay_u64(pit.timer[t], (uint64_t)((l + 1) >> 1) * PITCONST);
                        pit_set_out(pit, t, 1);
                        pit.thit[t] = 0;
                }
                pit.enabled[t] = pit.gate[t];
                break;
        case 4: /*Software triggered stobe*/
                if (pit.thit[t] == 0 && pit.initial[t] == 0)
                        pit.newcount[t] = 1;
                else
                {
                        pit.count[t] = l;
                        if (pit.using_timer[t] != 0)
                                timer.timer_set_delay_u64(pit.timer[t], (uint64_t)l * PITCONST);
                        pit_set_out(pit, t, 0);
                        pit.thit[t] = 0;
                }
                pit.enabled[t] = pit.gate[t];
                break;
        case 5: /*Hardware triggered stobe*/
                pit.enabled[t] = 1;
                break;
        }
        pit.initial[t] = 0;
        pit.running[t] = (pit.enabled[t] != 0 && pit.using_timer[t] != 0 && pit.disabled[t] == 0) ? 1 : 0;
        if (pit.using_timer[t] != 0 && pit.running[t] == 0)
                pit_dump_and_disable_timer(pit, t);
    }

    // pcem: pit.c:182-234
    internal static void pit_set_gate_no_timer(PIT pit, int t, int gate)
    {
        int l = pit.l[t] != 0 ? (int)pit.l[t] : 0x10000;

        if (pit.disabled[t] != 0)
        {
                pit.gate[t] = gate;
                return;
        }

        switch (pit.m[t])
        {
        case 0: /*Interrupt on terminal count*/
        case 4: /*Software triggered stobe*/
                if (pit.using_timer[t] != 0 && pit.running[t] == 0)
                        timer.timer_set_delay_u64(pit.timer[t], (uint64_t)l * PITCONST);
                pit.enabled[t] = gate;
                break;
        case 1: /*Hardware retriggerable one-shot*/
        case 5: /*Hardware triggered stobe*/
                if (gate != 0 && pit.gate[t] == 0)
                {
                        pit.count[t] = l;
                        if (pit.using_timer[t] != 0)
                                timer.timer_set_delay_u64(pit.timer[t], (uint64_t)l * PITCONST);
                        pit_set_out(pit, t, 0);
                        pit.thit[t] = 0;
                        pit.enabled[t] = 1;
                }
                break;
        case 2: /*Rate generator*/
                if (gate != 0 && pit.gate[t] == 0)
                {
                        pit.count[t] = l - 1;
                        if (pit.using_timer[t] != 0)
                                timer.timer_set_delay_u64(pit.timer[t], (uint64_t)l * PITCONST);
                        pit_set_out(pit, t, 1);
                        pit.thit[t] = 0;
                }
                pit.enabled[t] = gate;
                break;
        case 3: /*Square wave mode*/
                if (gate != 0 && pit.gate[t] == 0)
                {
                        pit.count[t] = l;
                        if (pit.using_timer[t] != 0)
                                timer.timer_set_delay_u64(pit.timer[t], (uint64_t)((l + 1) >> 1) * PITCONST);
                        pit_set_out(pit, t, 1);
                        pit.thit[t] = 0;
                }
                pit.enabled[t] = gate;
                break;
        }
        pit.gate[t] = gate;
        pit.running[t] = (pit.enabled[t] != 0 && pit.using_timer[t] != 0 && pit.disabled[t] == 0) ? 1 : 0;
        if (pit.using_timer[t] != 0 && pit.running[t] == 0)
                pit_dump_and_disable_timer(pit, t);
    }

    // pcem: pit.c:236-244
    internal static void pit_set_gate(PIT pit, int t, int gate)
    {
        if (pit.disabled[t] != 0)
        {
                pit.gate[t] = gate;
                return;
        }

        pit_set_gate_no_timer(pit, t, gate);
    }

    // pcem: pit.c:246-319
    private static void pit_over(PIT pit, int t)
    {
        int l = pit.l[t] != 0 ? (int)pit.l[t] : 0x10000;
        if (pit.disabled[t] != 0)
        {
                pit.count[t] += 0xffff;
                if (pit.using_timer[t] != 0)
                        timer.timer_advance_u64(pit.timer[t], 0xffff * PITCONST);
                return;
        }

        switch (pit.m[t])
        {
        case 0: /*Interrupt on terminal count*/
        case 1: /*Hardware retriggerable one-shot*/
                if (pit.thit[t] == 0)
                        pit_set_out(pit, t, 1);
                pit.thit[t] = 1;
                pit.count[t] += 0xffff;
                if (pit.using_timer[t] != 0)
                        timer.timer_advance_u64(pit.timer[t], 0xffff * PITCONST);
                break;
        case 2: /*Rate generator*/
                pit.count[t] += l;
                if (pit.using_timer[t] != 0)
                        timer.timer_advance_u64(pit.timer[t], (uint64_t)l * PITCONST);
                pit_set_out(pit, t, 0);
                pit_set_out(pit, t, 1);
                break;
        case 3: /*Square wave mode*/
                if (pit.@out[t] != 0)
                {
                        pit_set_out(pit, t, 0);
                        pit.count[t] += (l >> 1);
                        if (pit.using_timer[t] != 0)
                                timer.timer_advance_u64(pit.timer[t], (uint64_t)(l >> 1) * PITCONST);
                }
                else
                {
                        pit_set_out(pit, t, 1);
                        pit.count[t] += ((l + 1) >> 1);
                        if (pit.using_timer[t] != 0)
                                timer.timer_advance_u64(pit.timer[t], (uint64_t)((l + 1) >> 1) * PITCONST);
                }
                break;
        case 4: /*Software triggered strove*/
                if (pit.thit[t] == 0)
                {
                        pit_set_out(pit, t, 0);
                        pit_set_out(pit, t, 1);
                }
                if (pit.newcount[t] != 0)
                {
                        pit.newcount[t] = 0;
                        pit.count[t] += l;
                        if (pit.using_timer[t] != 0)
                                timer.timer_advance_u64(pit.timer[t], (uint64_t)l * PITCONST);
                }
                else
                {
                        pit.thit[t] = 1;
                        pit.count[t] += 0xffff;
                        if (pit.using_timer[t] != 0)
                                timer.timer_advance_u64(pit.timer[t], 0xffff * PITCONST);
                }
                break;
        case 5: /*Hardware triggered strove*/
                if (pit.thit[t] == 0)
                {
                        pit_set_out(pit, t, 0);
                        pit_set_out(pit, t, 1);
                }
                pit.thit[t] = 1;
                pit.count[t] += 0xffff;
                if (pit.using_timer[t] != 0)
                        timer.timer_advance_u64(pit.timer[t], 0xffff * PITCONST);
                break;
        }
        pit.running[t] = (pit.enabled[t] != 0 && pit.using_timer[t] != 0 && pit.disabled[t] == 0) ? 1 : 0;
        if (pit.using_timer[t] != 0 && pit.running[t] == 0)
                pit_dump_and_disable_timer(pit, t);
    }

    // pcem: pit.c:321-431
    internal static void pit_write(uint16_t addr, uint8_t val, object p)
    {
        PIT pit = (PIT)p;
        int t;

        switch (addr & 3)
        {
        case 3: /*CTRL*/
                if ((val & 0xC0) == 0xC0)
                {
                        if ((val & 0x20) == 0)
                        {
                                if ((val & 2) != 0)
                                        pit.rl[0] = (uint16_t)pit_read_timer(pit, 0);
                                if ((val & 4) != 0)
                                        pit.rl[1] = (uint16_t)pit_read_timer(pit, 1);
                                if ((val & 8) != 0)
                                        pit.rl[2] = (uint16_t)pit_read_timer(pit, 2);
                        }
                        if ((val & 0x10) == 0)
                        {
                                if ((val & 2) != 0)
                                {
                                        pit.read_status[0] = (uint8_t)((pit.ctrls[0] & 0x3f) | (pit.@out[0] != 0 ? 0x80 : 0));
                                        pit.do_read_status[0] = 1;
                                }
                                if ((val & 4) != 0)
                                {
                                        pit.read_status[1] = (uint8_t)((pit.ctrls[1] & 0x3f) | (pit.@out[1] != 0 ? 0x80 : 0));
                                        pit.do_read_status[1] = 1;
                                }
                                if ((val & 8) != 0)
                                {
                                        pit.read_status[2] = (uint8_t)((pit.ctrls[2] & 0x3f) | (pit.@out[2] != 0 ? 0x80 : 0));
                                        pit.do_read_status[2] = 1;
                                }
                        }
                        return;
                }
                t = val >> 6;
                pit.ctrl = val;
                if ((val >> 7) == 3)
                {
                        // omitted: printf("Bad PIT reg select\n") — sortie pure.
                        return;
                }
                if ((pit.ctrl & 0x30) == 0)
                {
                        pit.rl[t] = (uint16_t)pit_read_timer(pit, t);
                        pit.ctrl |= 0x30;
                        pit.rereadlatch[t] = 0;
                        pit.rm[t] = 3;
                        pit.latched[t] = 1;
                }
                else
                {
                        pit.ctrls[t] = val;
                        pit.rm[t] = pit.wm[t] = (pit.ctrl >> 4) & 3;
                        pit.m[t] = (uint8_t)((val >> 1) & 7);
                        if (pit.m[t] > 5)
                                pit.m[t] &= 3;
                        if (pit.rm[t] == 0)
                        {
                                pit.rm[t] = 3;
                                pit.rl[t] = (uint16_t)pit_read_timer(pit, t);
                        }
                        pit.rereadlatch[t] = 1;
                        pit.initial[t] = 1;
                        if (pit.m[t] == 0)
                                pit_set_out(pit, t, 0);
                        else
                                pit_set_out(pit, t, 1);
                        pit.disabled[t] = 1;
                }
                pit.wp = 0;
                pit.thit[t] = 0;
                break;
        case 0:
        case 1:
        case 2: /*Timers*/
                t = addr & 3;
                switch (pit.wm[t])
                {
                case 1:
                        pit.l[t] = val;
                        pit_load(pit, t);
                        break;
                case 2:
                        pit.l[t] = (uint32_t)(val << 8);
                        pit_load(pit, t);
                        break;
                case 0:
                        pit.l[t] &= 0xFF;
                        pit.l[t] |= (uint32_t)(val << 8);
                        pit_load(pit, t);
                        pit.wm[t] = 3;
                        break;
                case 3:
                        pit.l[t] &= 0xFF00;
                        pit.l[t] |= val;
                        pit.wm[t] = 0;
                        break;
                }
                // pcem bug, reproduced: PB-21 — pit.c:418 divise par l[0] sans le
                //   tester, et le POST y passe : tranche 231 d'un amorçage 640 Ko,
                //   l[0] = 0, l[2] = 65535, donc +inf.
                // DEVIATION: le (int) du C rend alors l'entier indéfini de
                //   cvttss2si, 0x80000000, que le clamp ci-dessous ne rattrape pas.
                //   .NET SATURE : (int)float.PositiveInfinity vaut int.MaxValue, et
                //   le clamp le ramène à 0x2000. Écrire `(int)` ici ferait donc
                //   diverger speakval de l'oracle à chaque amorçage — mesuré par
                //   speaker-probe. La garde reproduit la conversion x86, elle ne la
                //   corrige pas.
                float speakf = (((float)pit.l[2] / (float)pit.l[0]) * 0x4000) - 0x2000;
                Sound.sound_speaker.speakval =
                    speakf >= -2147483648.0f && speakf < 2147483648.0f ? (int)speakf : int.MinValue;
                if (Sound.sound_speaker.speakval > 0x2000)
                        Sound.sound_speaker.speakval = 0x2000;
                break;
        }
    }

    // pcem: pit.c:433-485
    internal static uint8_t pit_read(uint16_t addr, object p)
    {
        PIT pit = (PIT)p;
        int t;
        uint8_t temp = 0xff;
        switch (addr & 3)
        {
        case 0:
        case 1:
        case 2: /*Timers*/
                t = addr & 3;
                if (pit.do_read_status[t] != 0)
                {
                        pit.do_read_status[t] = 0;
                        temp = pit.read_status[t];
                        break;
                }
                if (pit.rereadlatch[addr & 3] != 0 && pit.latched[addr & 3] == 0)
                {
                        pit.rereadlatch[addr & 3] = 0;
                        pit.rl[t] = (uint16_t)pit_read_timer(pit, t);
                }
                switch (pit.rm[addr & 3])
                {
                case 0:
                        temp = (uint8_t)(pit.rl[addr & 3] >> 8);
                        pit.rm[addr & 3] = 3;
                        pit.latched[addr & 3] = 0;
                        pit.rereadlatch[addr & 3] = 1;
                        break;
                case 1:
                        temp = (uint8_t)((pit.rl[addr & 3]) & 0xFF);
                        pit.latched[addr & 3] = 0;
                        pit.rereadlatch[addr & 3] = 1;
                        break;
                case 2:
                        temp = (uint8_t)((pit.rl[addr & 3]) >> 8);
                        pit.latched[addr & 3] = 0;
                        pit.rereadlatch[addr & 3] = 1;
                        break;
                case 3:
                        temp = (uint8_t)((pit.rl[addr & 3]) & 0xFF);
                        if ((pit.m[addr & 3] & 0x80) != 0)
                                pit.m[addr & 3] &= 7;
                        else
                                pit.rm[addr & 3] = 0;
                        break;
                }
                break;
        case 3: /*Control*/
                temp = pit.ctrl;
                break;
        }
        return temp;
    }

    // pcem: pit.c:487-494
    internal static void pit_timer_over(object? p)
    {
        PIT_nr pit_nr = (PIT_nr)p;
        PIT pit = pit_nr.pit;
        int timer = pit_nr.nr;

        pit_over(pit, timer);
    }

    // pcem: pit.c:496-506
    internal static void pit_clock(PIT pit, int t)
    {
        if (pit.thit[t] != 0 || pit.enabled[t] == 0)
                return;

        if (pit.using_timer[t] != 0)
                return;

        pit.count[t] -= (pit.m[t] == 3) ? 2 : 1;
        if (pit.count[t] == 0)
                pit_over(pit, t);
    }

    // pcem: pit.c:508-519
    internal static void pit_set_using_timer(PIT pit, int t, int using_timer)
    {
        timer.timer_process();
        if (pit.using_timer[t] != 0 && using_timer == 0)
                pit.count[t] = pit_read_timer(pit, t);
        pit.running[t] = (pit.enabled[t] != 0 && using_timer != 0 && pit.disabled[t] == 0) ? 1 : 0;
        if (pit.using_timer[t] == 0 && using_timer != 0 && pit.running[t] != 0)
                timer.timer_set_delay_u64(pit.timer[t], (uint64_t)pit.count[t] * PITCONST);
        else if (pit.running[t] == 0)
                timer.timer_disable(pit.timer[t]);
        pit.using_timer[t] = using_timer;
    }

    // pcem: pit.c:521
    internal static void pit_set_out_func(PIT pit, int t, set_out_func_t func) { pit.set_out_funcs[t] = func; }

    // pcem: pit.c:523
    internal static void pit_null_timer(int new_out, int old_out) { }

    // pcem: pit.c:525-530
    internal static void pit_irq0_timer(int new_out, int old_out)
    {
        if (new_out != 0 && old_out == 0)
                pic.picint(1);
        if (new_out == 0)
                pic.picintc(1);
    }

    // pcem: pit.c:558-561 — LE RAFRAÎCHISSEMENT MÉMOIRE D'UN AT, et le POST le COMPTE.
    //
    // Sur un XT le canal 1 du PIT déclenche un cycle de rafraîchissement DRAM par le
    // DMA ; sur un AT il fait basculer le bit 4 du port B du PPI, et le BIOS compte les
    // bascules pour vérifier que l'horloge tourne. Il en exige au moins 0xF600
    // (F000:05B8), et en dessous il s'arrête sur un HLT en F000:05C4.
    //
    // C'EST CE COMPTE QUI A FAIT ÉCHOUER B2 PENDANT CENT MILLIONS D'INSTRUCTIONS, et le
    // défaut n'était pas ici : le harnais n'avait que la branche XT de setpitclock, donc
    // PITCONST valait 12 cycles CPU par tic au lieu de 5,03 et le PIT tournait 2,4 fois
    // trop lentement par rapport au processeur. Le bit basculait, simplement pas assez.
    internal static void pit_refresh_timer_at(int new_out, int old_out)
    {
        if (new_out != 0 && old_out == 0)
                ppi_c.ppi.pb ^= 0x10;
    }

    // pcem: pit.c:553-556
    internal static void pit_refresh_timer_xt(int new_out, int old_out)
    {
        if (new_out != 0 && old_out == 0)
                dma.dma_channel_read(0);
    }

    // pcem: pit.c:563-574
    internal static void pit_speaker_timer(int new_out, int old_out)
    {
        int l;

        Sound.sound_speaker.speaker_update();

        l = pit_.l[2] != 0 ? (int)pit_.l[2] : 0x10000;
        if (l < 25)
                Sound.sound_speaker.speakon = 0;
        else
                Sound.sound_speaker.speakon = new_out;
        Sound.sound_speaker.ppispeakon = new_out;
    }

    // pcem: pit.c:582-602
    internal static void pit_init()
    {
        pit_reset(pit_);

        io.io_sethandler(0x0040, 0x0004, pit_read, null, null, pit_write, null, null, pit_);
        pit_.gate[0] = pit_.gate[1] = 1;
        pit_.gate[2] = 0;
        pit_.using_timer[0] = pit_.using_timer[1] = pit_.using_timer[2] = 1;

        pit_.pit_nr[0].nr = 0;
        pit_.pit_nr[1].nr = 1;
        pit_.pit_nr[2].nr = 2;
        pit_.pit_nr[0].pit = pit_.pit_nr[1].pit = pit_.pit_nr[2].pit = pit_;

        timer.timer_add(pit_.timer[0], pit_timer_over, pit_.pit_nr[0], 0);
        timer.timer_add(pit_.timer[1], pit_timer_over, pit_.pit_nr[1], 0);
        timer.timer_add(pit_.timer[2], pit_timer_over, pit_.pit_nr[2], 0);

        pit_set_out_func(pit_, 0, pit_irq0_timer);
        pit_set_out_func(pit_, 1, pit_null_timer);
        pit_set_out_func(pit_, 2, pit_speaker_timer);
    }
    /// <summary>Sonde de diagnostic — pendant exact de h_pit_probe()
    /// (tools/oracle/harness.c). Même ordre de champs, pour que le diff de boot
    /// nomme le champ divergent au lieu de le faire deviner.</summary>
    internal static void Probe(int t, uint64_t[] o)
    {
        o[0] = pit_.l[t];
        o[1] = pit_.m[t];
        o[2] = (uint64_t)(long)pit_.count[t];
        o[3] = pit_.rl[t];
        o[4] = (uint64_t)(long)pit_.using_timer[t];
        o[5] = (uint64_t)(long)pit_.gate[t];
        o[6] = (uint64_t)(long)pit_.enabled[t];
        o[7] = (uint64_t)(long)pit_.running[t];
        o[8] = (uint64_t)(long)pit_.disabled[t];
        o[9] = (uint64_t)(long)pit_.thit[t];
        o[10] = (uint64_t)(long)pit_.latched[t];
        o[11] = (uint64_t)(long)pit_.rereadlatch[t];
        o[12] = (uint64_t)(long)pit_.rm[t];
        o[13] = (uint64_t)(long)pit_.@out[t];
        o[14] = (uint64_t)(long)pit_.timer[t].enabled;
        o[15] = ((uint64_t)pit_.timer[t].ts_integer << 32) | pit_.timer[t].ts_frac;
        o[16] = timer.tsc;
        o[17] = PITCONST;
        o[18] = timer.timer_get_remaining_u64(pit_.timer[t]);
    }

}
