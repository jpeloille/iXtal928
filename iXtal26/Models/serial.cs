// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/models/serial.c + includes/private/models/serial.h
// STATUS: transcribed — SERIAL, serial1/serial2, serial_reset, serial_update_ints,
//         serial_write_fifo, serial_read_fifo, serial_write, serial_read,
//         serial_receive_callback, serial1_init/_set/_set_has_fifo/_remove et leurs
//         pendants serial2.

namespace iXtal26.Models;

// pcem: serial.h:15
internal delegate void serial_rcr_fn(SERIAL serial, object? p);

// pcem: serial.h:17-37
// Classe et non struct : son adresse est prise (io_sethandler, timer_add, la souris).
internal sealed class SERIAL
{
    internal uint8_t lsr, thr, mctrl, rcr, iir, ier, lcr, msr;
    internal uint8_t dlab1, dlab2;
    internal uint8_t dat;
    internal uint8_t int_status;
    internal uint8_t scratch;
    internal uint8_t fcr;

    internal int irq;
    internal uint16_t addr;

    internal serial_rcr_fn? rcr_callback;
    internal object? rcr_callback_p;
    internal uint8_t[] fifo = new uint8_t[256];
    internal int fifo_read, fifo_write;
    internal int has_fifo;

    internal pc_timer_t receive_timer = new();

    // memset(&serialN, 0, sizeof(serialN)) — le chronomètre, lui, est ré-armé par
    // timer_add juste après.
    internal void Clear()
    {
        lsr = thr = mctrl = rcr = iir = ier = lcr = msr = 0;
        dlab1 = dlab2 = dat = int_status = scratch = fcr = 0;
        irq = 0;
        addr = 0;
        rcr_callback = null;
        rcr_callback_p = null;
        Array.Clear(fifo);
        fifo_read = fifo_write = 0;
        has_fifo = 0;
        receive_timer = new pc_timer_t();
    }
}

internal static partial class serial
{
    // pcem: serial.c:8
    private const int SERIAL_INT_LSR = 1, SERIAL_INT_RECEIVE = 2, SERIAL_INT_TRANSMIT = 4, SERIAL_INT_MSR = 8;

    // pcem: serial.c:10
    internal static readonly SERIAL serial1 = new(), serial2 = new();

    // pcem: ibm.h — PCJR, que seul le modèle PCjr pose ; aucune machine du dépôt.
    internal static int PCJR;

    // pcem: serial.c:12-17
    internal static void serial_reset()
    {
        serial1.iir = serial1.ier = serial1.lcr = 0;
        serial2.iir = serial2.ier = serial2.lcr = 0;
        serial1.fifo_read = serial1.fifo_write = 0;
        serial2.fifo_read = serial2.fifo_write = 0;
    }

    // pcem: serial.c:19-48
    internal static void serial_update_ints(SERIAL serial)
    {
        int stat = 0;

        serial.iir = 1;

        if ((serial.ier & 4) != 0 && (serial.int_status & SERIAL_INT_LSR) != 0) /*Line status interrupt*/
        {
                stat = 1;
                serial.iir = 6;
        }
        else if ((serial.ier & 1) != 0 && (serial.int_status & SERIAL_INT_RECEIVE) != 0) /*Received data available*/
        {
                stat = 1;
                serial.iir = 4;
        }
        else if ((serial.ier & 2) != 0 && (serial.int_status & SERIAL_INT_TRANSMIT) != 0) /*Transmit data empty*/
        {
                stat = 1;
                serial.iir = 2;
        }
        else if ((serial.ier & 8) != 0 && (serial.int_status & SERIAL_INT_MSR) != 0) /*Modem status interrupt*/
        {
                stat = 1;
                serial.iir = 0;
        }

        if (stat != 0 && ((serial.mctrl & 8) != 0 || PCJR != 0))
                pic.picintlevel((uint16_t)(1 << serial.irq));
        else
                pic.picintc((uint16_t)(1 << serial.irq));
    }

    // pcem: serial.c:50-59
    internal static void serial_write_fifo(SERIAL serial, uint8_t dat)
    {
        serial.fifo[serial.fifo_write] = dat;
        serial.fifo_write = (serial.fifo_write + 1) & 0xFF;
        if ((serial.lsr & 1) == 0)
        {
                serial.lsr |= 1;
                serial.int_status |= SERIAL_INT_RECEIVE;
                serial_update_ints(serial);
        }
    }

    // pcem: serial.c:61-67
    internal static uint8_t serial_read_fifo(SERIAL serial)
    {
        if (serial.fifo_read != serial.fifo_write)
        {
                serial.dat = serial.fifo[serial.fifo_read];
                serial.fifo_read = (serial.fifo_read + 1) & 0xFF;
        }
        return serial.dat;
    }

    // pcem: serial.c:69-149
    internal static void serial_write(uint16_t addr, uint8_t val, object p)
    {
        SERIAL serial = (SERIAL)p;
        switch (addr & 7)
        {
        case 0:
                if ((serial.lcr & 0x80) != 0)
                {
                        serial.dlab1 = val;
                        return;
                }
                serial.thr = val;
                serial.lsr |= 0x20;
                serial.int_status |= SERIAL_INT_TRANSMIT;
                serial_update_ints(serial);
                if ((serial.mctrl & 0x10) != 0)
                {
                        serial_write_fifo(serial, val);
                }
                break;
        case 1:
                if ((serial.lcr & 0x80) != 0)
                {
                        serial.dlab2 = val;
                        return;
                }
                serial.ier = (uint8_t)(val & 0xf);
                serial_update_ints(serial);
                break;
        case 2:
                if (serial.has_fifo != 0)
                        serial.fcr = val;
                break;
        case 3:
                serial.lcr = val;
                break;
        case 4:
                if ((val & 2) != 0 && (serial.mctrl & 2) == 0)
                {
                        if (serial.rcr_callback != null)
                                serial.rcr_callback(serial, serial.rcr_callback_p);
                }
                serial.mctrl = val;
                if ((val & 0x10) != 0)
                {
                        uint8_t new_msr;

                        new_msr = (uint8_t)((val & 0x0c) << 4);
                        new_msr |= (uint8_t)((val & 0x02) != 0 ? 0x10 : 0);
                        new_msr |= (uint8_t)((val & 0x01) != 0 ? 0x20 : 0);

                        if (((serial.msr ^ new_msr) & 0x10) != 0)
                                new_msr |= 0x01;
                        if (((serial.msr ^ new_msr) & 0x20) != 0)
                                new_msr |= 0x02;
                        if (((serial.msr ^ new_msr) & 0x80) != 0)
                                new_msr |= 0x08;
                        if ((serial.msr & 0x40) != 0 && (new_msr & 0x40) == 0)
                                new_msr |= 0x04;

                        serial.msr = new_msr;
                }
                break;
        case 5:
                serial.lsr = val;
                if ((serial.lsr & 0x01) != 0)
                        serial.int_status |= SERIAL_INT_RECEIVE;
                if ((serial.lsr & 0x1e) != 0)
                        serial.int_status |= SERIAL_INT_LSR;
                if ((serial.lsr & 0x20) != 0)
                        serial.int_status |= SERIAL_INT_TRANSMIT;
                serial_update_ints(serial);
                break;
        case 6:
                serial.msr = val;
                if ((serial.msr & 0x0f) != 0)
                        serial.int_status |= SERIAL_INT_MSR;
                serial_update_ints(serial);
                break;
        case 7:
                serial.scratch = val;
                break;
        }
    }

    // pcem: serial.c:151-208
    internal static uint8_t serial_read(uint16_t addr, object p)
    {
        SERIAL serial = (SERIAL)p;
        uint8_t temp = 0;
        switch (addr & 7)
        {
        case 0:
                if ((serial.lcr & 0x80) != 0)
                {
                        temp = serial.dlab1;
                        break;
                }

                serial.lsr &= unchecked((uint8_t)~1);
                serial.int_status &= unchecked((uint8_t)~SERIAL_INT_RECEIVE);
                serial_update_ints(serial);
                temp = serial_read_fifo(serial);
                if (serial.fifo_read != serial.fifo_write)
                        timer.timer_set_delay_u64(serial.receive_timer, 1000 * timer.TIMER_USEC);
                break;
        case 1:
                if ((serial.lcr & 0x80) != 0)
                        temp = serial.dlab2;
                else
                        temp = serial.ier;
                break;
        case 2:
                temp = serial.iir;
                if ((temp & 0xe) == 2)
                {
                        serial.int_status &= unchecked((uint8_t)~SERIAL_INT_TRANSMIT);
                        serial_update_ints(serial);
                }
                if ((serial.fcr & 1) != 0)
                        temp |= 0xc0;
                break;
        case 3:
                temp = serial.lcr;
                break;
        case 4:
                temp = serial.mctrl;
                break;
        case 5:
                if ((serial.lsr & 0x20) != 0)
                        serial.lsr |= 0x40;
                serial.lsr |= 0x20;
                temp = serial.lsr;
                if ((serial.lsr & 0x1f) != 0)
                        serial.lsr &= unchecked((uint8_t)~0x1e);
                serial.int_status &= unchecked((uint8_t)~SERIAL_INT_LSR);
                serial_update_ints(serial);
                break;
        case 6:
                temp = serial.msr;
                serial.msr &= unchecked((uint8_t)~0x0f);
                serial.int_status &= unchecked((uint8_t)~SERIAL_INT_MSR);
                serial_update_ints(serial);
                break;
        case 7:
                temp = serial.scratch;
                break;
        }
        return temp;
    }

    // pcem: serial.c:210-218
    internal static void serial_receive_callback(object? p)
    {
        SERIAL serial = (SERIAL)p!;

        if (serial.fifo_read != serial.fifo_write)
        {
                serial.lsr |= 1;
                serial.int_status |= SERIAL_INT_RECEIVE;
                serial_update_ints(serial);
        }
    }

    /*Tandy might need COM1 at 2f8*/
    // pcem: serial.c:221-229
    internal static void serial1_init(uint16_t addr, int irq, int has_fifo)
    {
        serial1.Clear();
        io.io_sethandler(addr, 0x0008, serial_read, null, null, serial_write, null, null, serial1);
        serial1.irq = irq;
        serial1.addr = addr;
        serial1.rcr_callback = null;
        timer.timer_add(serial1.receive_timer, serial_receive_callback, serial1, 0);
        serial1.has_fifo = has_fifo;
    }
    // pcem: serial.c:230-235
    internal static void serial1_set(uint16_t addr, int irq)
    {
        serial1_remove();
        io.io_sethandler(addr, 0x0008, serial_read, null, null, serial_write, null, null, serial1);
        serial1.irq = irq;
        serial1.addr = addr;
    }
    // pcem: serial.c:236-237
    internal static void serial1_set_has_fifo(int has_fifo) { serial1.has_fifo = has_fifo; }
    internal static void serial1_remove() { io.io_removehandler(serial1.addr, 0x0008, serial_read, null, null, serial_write, null, null, serial1); }

    // pcem: serial.c:239-247
    internal static void serial2_init(uint16_t addr, int irq, int has_fifo)
    {
        serial2.Clear();
        io.io_sethandler(addr, 0x0008, serial_read, null, null, serial_write, null, null, serial2);
        serial2.irq = irq;
        serial2.addr = addr;
        serial2.rcr_callback = null;
        timer.timer_add(serial2.receive_timer, serial_receive_callback, serial2, 0);
        serial2.has_fifo = has_fifo;
    }
    // pcem: serial.c:248-253
    internal static void serial2_set(uint16_t addr, int irq)
    {
        serial2_remove();
        io.io_sethandler(addr, 0x0008, serial_read, null, null, serial_write, null, null, serial2);
        serial2.irq = irq;
        serial2.addr = addr;
    }
    // pcem: serial.c:254-255
    internal static void serial2_set_has_fifo(int has_fifo) { serial2.has_fifo = has_fifo; }
    internal static void serial2_remove() { io.io_removehandler(serial2.addr, 0x0008, serial_read, null, null, serial_write, null, null, serial2); }
}
