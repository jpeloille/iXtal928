// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/timer.c (1-191) + pcem-dev/includes/private/timer.h (18-124)
// STATUS: transcribed — liste chaînée triée, inlines 32:32, et la couche de
//         validité (magic / all_timers[] / timer_valid). Seules les branches de
//         récupération de liste corrompue portent un // omitted: motivé.

using iXtal26.Diag;

namespace iXtal26;

// pcem: timer.h:26 — void (*callback)(void *p)
internal delegate void timer_callback_t(object? p);

// pcem: timer.h:20-30
internal sealed class pc_timer_t
{
    internal uint32_t magic;
    internal uint32_t ts_integer;
    internal uint32_t ts_frac;
    internal int enabled;

    internal timer_callback_t? callback;
    internal object? p;

    internal pc_timer_t? prev, next;
}

/*Timers are based on the CPU Time Stamp Counter. Timer timestamps are in a
  32:32 fixed point format, with the integer part compared against the TSC. The
  fractional part is used when advancing the timestamp to ensure a more accurate
  period.

  As the timer only stores 32 bits of integer timestamp, and the TSC is 64 bits,
  the timer period can only be at most 0x7fffffff CPU cycles. To allow room for
  (optimistic) CPU frequency growth, timer period must be at most 1 second.

  When a timer callback is called, the timer has been disabled. If the timer is
  to repeat, the callback must call timer_advance_u64(). This is a change from
  the old timer API.*/
internal static partial class timer
{
    // pcem: cpu.c:102 — `tsc` est une globale de cpu.c, hissée ici parce que
    // timer.c et timer.h sont ses seuls consommateurs transcrits ; 808x.cs:278
    // et 808x.State.cs:54 l'écrivent déjà sous ce nom.
    internal static uint64_t tsc = 0;

    // pcem: timer.c:5-6 — TIMER_USEC reste nul jusqu'à setpitclock() (pit.c:57).
    internal static uint64_t TIMER_USEC;
    internal static uint32_t timer_target;

    /*Enabled timers are stored in a linked list, with the first timer to expire at
      the head.*/
    private static pc_timer_t? timer_head = null;

    /*All timers ever registered via timer_add are tracked here so we can
      safely disable them all on reset, even if the caller forgot to call
      timer_disable before freeing the containing struct.*/
    // pcem: timer.c:15-17, timer.h:18
    private const uint32_t TIMER_MAGIC = 0x544D5243; /* 'TMRC' */
    private const int MAX_TIMERS = 256;
    private static readonly pc_timer_t?[] all_timers = new pc_timer_t?[MAX_TIMERS];
    private static int num_timers = 0;

    // pcem: timer.c:19-21
    //
    // Ce n'est PAS qu'un détecteur d'use-after-free : timer_reset() remet magic à
    // zéro sur tous les timers enregistrés (timer.c:162), et ce test les rend donc
    // inertes jusqu'au prochain timer_add(). C'est du contrôle de flux vivant. Je
    // l'avais omis en le prenant pour du durcissement propre au C ; le diff de boot
    // l'a démenti à l'instruction 66 (VERIFICATION.md).
    private static bool timer_valid(pc_timer_t? timer) => timer != null && timer.magic == TIMER_MAGIC;

    // DEVIATION: fatal() appartient à ibm.h/pc.c, qui n'est pas encore transcrit.
    //   Déclaré ici pour que timer.c:33 et timer.c:100 aient une contrepartie.
    //   Comme harness_stubs.c:260-267, il compte et n'interrompt pas.
    private static void fatal(string format)
    {
        Counters.n_fatal++;
        Console.Error.Write("iXtal26 FATAL: " + format);
    }

    /*True if timer a expires before timer b*/
    // pcem: timer.h:55
    internal static bool TIMER_LESS_THAN(pc_timer_t a, pc_timer_t b)
        => unchecked((int32_t)(a.ts_integer - b.ts_integer)) <= 0;

    /*True if timer a expires before 32 bit integer timestamp b*/
    // pcem: timer.h:57
    internal static bool TIMER_LESS_THAN_VAL(pc_timer_t a, uint32_t b)
        => unchecked((int32_t)(a.ts_integer - b)) <= 0;

    /*True if 32 bit integer timestamp a expires before 32 bit integer timestamp b*/
    // pcem: timer.h:59
    internal static bool TIMER_VAL_LESS_THAN_VAL(uint32_t a, uint32_t b)
        => unchecked((int32_t)(a - b)) <= 0;

    /*Enable timer, without updating timestamp*/
    // pcem: timer.c:23-91
    internal static void timer_enable(pc_timer_t timer)
    {
        pc_timer_t? timer_node;

        if (!timer_valid(timer))
                return;

        if (timer.enabled != 0)
                timer_disable(timer);

        if (timer.next != null || timer.prev != null)
                fatal("timer_enable - timer->next\n");

        timer.enabled = 1;

        /*List currently empty - add to head*/
        if (timer_head == null)
        {
                timer_head = timer;
                timer.next = timer.prev = null;
                timer_target = timer_head.ts_integer;
                return;
        }

        timer_node = timer_head;

        while (true)
        {
                /*Timer expires before timer_node. Add to list in front of timer_node*/
                if (TIMER_LESS_THAN(timer, timer_node))
                {
                        timer.next = timer_node;
                        timer.prev = timer_node.prev;
                        timer_node.prev = timer;
                        if (timer.prev != null)
                                timer.prev.next = timer;
                        else
                        {
                                timer_head = timer;
                                timer_target = timer_head.ts_integer;
                        }
                        return;
                }

                /*timer_node is last in the list. Add timer to end of list*/
                if (timer_node.next == null)
                {
                        timer_node.next = timer;
                        timer.prev = timer_node;
                        return;
                }

                timer_node = timer_node.next;
        }
    }

    /*Disable timer*/
    // pcem: timer.c:92-111
    internal static void timer_disable(pc_timer_t timer)
    {
        if (!timer_valid(timer))
                return;

        if (timer.enabled == 0)
                return;

        if (timer.next == null && timer.prev == null && timer != timer_head)
                fatal("timer_disable - !timer->next\n");

        timer.enabled = 0;

        if (timer.prev != null)
                timer.prev.next = timer.next;
        else
                timer_head = timer.next;
        if (timer.next != null)
                timer.next.prev = timer.prev;
        timer.prev = timer.next = null;
    }

    // pcem: timer.c:112-127
    private static void timer_remove_head()
    {
        if (timer_head != null)
        {
                pc_timer_t timer = timer_head;
                timer_head = timer.next;
                if (timer_head != null)
                {
                        if (!timer_valid(timer_head))
                        {
                                // omitted: pclog (timer.c:118) — sortie pure.
                                timer_head = null;
                        }
                        else
                        {
                                timer_head.prev = null;
                        }
                }
                timer.next = timer.prev = null;
                timer.enabled = 0;
        }
    }

    /*Process any pending timers*/
    // pcem: timer.c:129-148
    internal static void timer_process()
    {
        // DEVIATION: compteur du harnais différentiel (Diag/Counters.cs, pendant
        //   de h_n_timer_process, harness_stubs.c:194). Absent de PCem.
        Counters.n_timer_process++;

        while (timer_head != null)
        {
                pc_timer_t timer = timer_head;

                if (!timer_valid(timer))
                {
                        // omitted: pclog (timer.c:134) — sortie pure.
                        timer_head = null;
                        break;
                }

                if (!TIMER_LESS_THAN_VAL(timer, (uint32_t)tsc))
                        break;

                timer_remove_head();
                timer.callback!(timer.p);
        }

        if (timer_head != null)
                timer_target = timer_head.ts_integer;
    }

    /*Reset timer system*/
    // pcem: timer.c:150-170
    internal static void timer_reset()
    {
        int i;

        // omitted: pclog("timer_reset\n") (timer.c:153) — sortie pure.

        /* Disable every registered timer so their next/prev pointers are
           cleaned up before device_close_all() frees the containing structs. */
        for (i = 0; i < num_timers; i++)
        {
                pc_timer_t? t = all_timers[i];
                if (t != null && t.magic == TIMER_MAGIC)
                {
                        t.enabled = 0;
                        t.prev = t.next = null;
                        t.magic = 0; /* Invalidate so freed memory is detectable */
                }
        }

        timer_target = 0;
        tsc = 0;
        timer_head = null;
        num_timers = 0;
    }

    /*Add new timer. If start_timer is set, timer will be enabled with a zero
      timestamp - this is useful for permanently enabled timers*/
    // pcem: timer.c:172-191
    internal static void timer_add(pc_timer_t timer, timer_callback_t callback, object? p, int start_timer)
    {
        /* If this timer is still in the active list, disable it first */
        if (timer.magic == TIMER_MAGIC && timer.enabled != 0)
                timer_disable(timer);

        // pcem: timer.c:177 — memset(timer, 0, sizeof(pc_timer_t)) ; seuls les
        // champs que les affectations suivantes ne réécrivent pas.
        timer.ts_integer = 0;
        timer.ts_frac = 0;

        timer.magic = TIMER_MAGIC;
        timer.callback = callback;
        timer.p = p;
        timer.enabled = 0;
        timer.prev = timer.next = null;

        /* Track this timer for cleanup on reset */
        if (num_timers < MAX_TIMERS)
                all_timers[num_timers++] = timer;


        if (start_timer != 0)
                timer_set_delay_u64(timer, 0);
    }

    /*Advance timer by delay, specified in 32:32 format. This should be used to
      resume a recurring timer in a callback routine*/
    // pcem: timer.h:63-73
    internal static void timer_advance_u64(pc_timer_t timer, uint64_t delay)
    {
        uint32_t int_delay = (uint32_t)(delay >> 32);
        uint32_t frac_delay = (uint32_t)(delay & 0xffffffff);

        if ((frac_delay + timer.ts_frac) < frac_delay)
                timer.ts_integer++;
        timer.ts_frac += frac_delay;
        timer.ts_integer += int_delay;

        timer_enable(timer);
    }

    /*Set a timer to the given delay, specified in 32:32 format. This should be used
      when starting a timer*/
    // pcem: timer.h:77-85
    internal static void timer_set_delay_u64(pc_timer_t timer, uint64_t delay)
    {
        uint32_t int_delay = (uint32_t)(delay >> 32);
        uint32_t frac_delay = (uint32_t)(delay & 0xffffffff);

        timer.ts_frac = frac_delay;
        timer.ts_integer = int_delay + (uint32_t)tsc;

        timer_enable(timer);
    }

    /*True if timer currently enabled*/
    // pcem: timer.h:88
    internal static int timer_is_enabled(pc_timer_t timer) { return timer.enabled; }

    /*Return integer timestamp of timer*/
    // pcem: timer.h:91
    internal static uint32_t timer_get_ts_int(pc_timer_t timer) { return timer.ts_integer; }

    /*Return remaining time before timer expires, in us. If the timer has already
      expired then return 0*/
    // pcem: timer.h:95-105
    internal static uint32_t timer_get_remaining_us(pc_timer_t timer)
    {
        if (timer.enabled != 0)
        {
                int64_t remaining = unchecked((int64_t)(((((uint64_t)timer.ts_integer) << 32) | timer.ts_frac) - (tsc << 32)));

                if (remaining < 0)
                        return 0;
                return (uint32_t)((uint64_t)remaining / TIMER_USEC);
        }

        return 0;
    }

    /*Return remaining time before timer expires, in 32:32 timestamp format. If the
      timer has already expired then return 0*/
    // pcem: timer.h:109-119
    internal static uint64_t timer_get_remaining_u64(pc_timer_t timer)
    {
        if (timer.enabled != 0)
        {
                int64_t remaining = unchecked((int64_t)(((((uint64_t)timer.ts_integer) << 32) | timer.ts_frac) - (tsc << 32)));

                if (remaining < 0)
                        return 0;
                return (uint64_t)remaining;
        }

        return 0;
    }

    /*Set timer callback function*/
    // pcem: timer.h:122
    internal static void timer_set_callback(pc_timer_t timer, timer_callback_t callback) { timer.callback = callback; }

    /*Set timer private data*/
    // pcem: timer.h:124
    internal static void timer_set_p(pc_timer_t timer, object? p) { timer.p = p; }
}
