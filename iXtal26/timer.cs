// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/timer.c (1-191) + pcem-dev/includes/private/timer.h (18-124)
// STATUS: partial — liste chaînée triée et inlines 32:32 transcrites ; magic, all_timers[], timer_valid() et les branches de récupération de liste omis.

using iXtal26.Diag;

namespace iXtal26;

// pcem: timer.h:26 — void (*callback)(void *p)
internal delegate void timer_callback_t(object? p);

// pcem: timer.h:20-30
internal sealed class pc_timer_t
{
    // omitted: uint32_t magic (timer.h:21) — voir l'omission groupée dans timer.
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

    // omitted: TIMER_MAGIC (timer.h:18), magic (timer.h:21), MAX_TIMERS /
    //   all_timers[256] / num_timers (timer.c:15-17), timer_valid() (timer.c:19-21)
    //   et les branches de récupération de liste (timer.c:48-66, 117-122, 133-137)
    //   — détection d'use-after-free sur une struct libérée, inatteignable sous GC.
    //   Corollaire : le test `!timer` que timer_valid() portait en tête de
    //   timer_enable/timer_disable disparaît aussi ; une référence nulle y serait
    //   un bug d'appelant, pas un pc_timer_t libéré.

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
                        timer_head.prev = null;
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
        // omitted: pclog("timer_reset\n") (timer.c:153) — sortie pure.
        // DEVIATION: la boucle de nettoyage de all_timers[] (timer.c:155-164) est
        //   omise avec all_timers[]. L'équivalence ne vaut que pour les timers
        //   RÉ-ENREGISTRÉS : timer_add (timer.c:177) remet enabled/prev/next à
        //   zéro. Un timer jamais ré-ajouté garde ici enabled=1 et ses chaînages,
        //   et un timer_enable ultérieur atteint fatal("timer_enable - timer->next")
        //   là où PCem passait sans bruit.

        timer_target = 0;
        tsc = 0;
        timer_head = null;
    }

    /*Add new timer. If start_timer is set, timer will be enabled with a zero
      timestamp - this is useful for permanently enabled timers*/
    // pcem: timer.c:172-191
    internal static void timer_add(pc_timer_t timer, timer_callback_t callback, object? p, int start_timer)
    {
        /* If this timer is still in the active list, disable it first */
        if (timer.enabled != 0)
                timer_disable(timer);

        // pcem: timer.c:177 — memset(timer, 0, sizeof(pc_timer_t)) ; seuls les
        // champs que les affectations suivantes ne réécrivent pas.
        timer.ts_integer = 0;
        timer.ts_frac = 0;

        timer.callback = callback;
        timer.p = p;
        timer.enabled = 0;
        timer.prev = timer.next = null;

        // omitted: l'enregistrement dans all_timers[] (timer.c:185-187).

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
