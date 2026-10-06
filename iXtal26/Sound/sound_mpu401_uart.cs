// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/sound/sound_mpu401_uart.c + includes/private/sound/sound_mpu401_uart.h
//         + src/sound/sdl2-midi.c (midi_write)
// STATUS: partial — G12.1 : le MPU-401 en mode UART de la SB 16 et de l'AWE32 : mpu401_uart_t,
//         mpu401_uart_raise_irq, mpu401_uart_write, mpu401_uart_read, mpu401_uart_init.
//         Omis : mpu401_uart_update_addr et mpu401_uart_update_irq (:75-82), que seul l'Aztech
//         (sound_azt2316a.c, exclu) appelle.
//
// LA SORTIE MIDI. midi_write est la frontière de l'hôte (plat-midi.h:5). Le build Linux par défaut de
// PCem prend sdl2-midi.c (USE_ALSA vaut OFF, CMakeLists.txt:61), dont midi_write est vide. Ici aussi
// (la sortie MIDI vers l'hôte revient à G17), avec en plus une empreinte des octets envoyés, pour la
// sonde du son : son pendant est la souche de harness_stubs.c (PLAN-G12.md, décision n° 8).

// CS8600 : `(mpu401_uart_t)p` part du `object` des delegates d'io.cs, comme à sound_sb.cs.
#pragma warning disable CS8600, CS8602

using static iXtal26.io;
using static iXtal26.Models.pic;

namespace iXtal26.Sound;

// pcem: sound_mpu401_uart.h:4-12
internal sealed class mpu401_uart_t
{
    internal uint8_t status;
    internal uint8_t rx_data;

    internal int uart_mode;
    internal uint16_t addr;
    internal int irq;

    internal int is_aztech;
}

internal static class sound_mpu401_uart
{
    // pcem: sound_mpu401_uart.c:7
    private const int STATUS_OUTPUT_NOT_READY = 0x40;
    private const int STATUS_INPUT_NOT_READY = 0x80;

    // pcem: sound_mpu401_uart.c:9-14
    private static void mpu401_uart_raise_irq(object p)
    {
        mpu401_uart_t mpu = (mpu401_uart_t)p;

        if (mpu.irq != -1)
                picint((uint16_t)(1 << mpu.irq));
    }

    // pcem: sound_mpu401_uart.c:16-50
    // pcem bug, reproduced: PB-154 — en mode UART, FFh rend quand même l'ACK FEh ; sur la SB 16 et l'AWE32,
    //   l'IRQ vaut -1 : rien n'est levé.
    private static void mpu401_uart_write(uint16_t addr, uint8_t val, object p)
    {
        mpu401_uart_t mpu = (mpu401_uart_t)p;

        if ((addr & 1) != 0) /*Command*/
        {
                switch (val) {
                case 0xff: /*Reset*/
                        // From Roland: "An ACK will not be sent back upon sending a SYSTEM RESET to leave the UART MODE ($3F)."
                        // But actual behaviour is weird. For example, the MPU401 port test in the AZT1605 drivers for Windows NT
                        // want this to return an Ack but the IRQ test in the same driver wants this to raise no interrupts!
                        mpu.rx_data = 0xfe; /*Acknowledge*/
                        mpu.uart_mode = 0;
                        if (mpu.is_aztech != 0)
                                mpu.status = STATUS_OUTPUT_NOT_READY;
                        else {
                                mpu.status = 0;
                                mpu401_uart_raise_irq(p);
                        }
                        break;

                case 0x3f:                   /*Enter UART mode*/
                        mpu.rx_data = 0xfe; /*Acknowledge*/
                        mpu.uart_mode = 1;
                        if (mpu.is_aztech != 0) {
                                mpu.status = STATUS_OUTPUT_NOT_READY;
                                mpu401_uart_raise_irq(p);
                        } else
                                mpu.status = 0;
                        break;
                }
                return;
        }

        /*Data*/
        if (mpu.uart_mode != 0)
                midi_write(val);
    }

    // pcem: sound_mpu401_uart.c:52-62
    private static uint8_t mpu401_uart_read(uint16_t addr, object p)
    {
        mpu401_uart_t mpu = (mpu401_uart_t)p;

        if ((addr & 1) != 0) /*Status*/
                return mpu.status;

        /*Data*/
        mpu.status = STATUS_INPUT_NOT_READY;
        return mpu.rx_data;
    }

    // pcem: sound_mpu401_uart.c:64-73
    internal static void mpu401_uart_init(mpu401_uart_t mpu, uint16_t addr, int irq, int is_aztech)
    {
        mpu.status = STATUS_INPUT_NOT_READY;
        mpu.uart_mode = 0;
        mpu.addr = addr;
        mpu.irq = irq;
        mpu.is_aztech = is_aztech;

        io_sethandler(addr, 0x0002, mpu401_uart_read, null, null, mpu401_uart_write, null, null, mpu);
    }

    // omitted: mpu401_uart_update_addr, mpu401_uart_update_irq (sound_mpu401_uart.c:75-82) — l'Aztech seul.

    // iXtal26 (outillage) — l'empreinte FNV-1a et le compte des octets MIDI, pendant de h_midi_hash et
    //   h_midi_count (harness_stubs.c). ORACLE PARITY : remis à zéro à chaque amorçage, des deux côtés.
    internal static uint64_t midi_hash = 1469598103934665603UL;
    internal static uint64_t midi_count;

    internal static void midi_raz()
    {
        midi_hash = 1469598103934665603UL;
        midi_count = 0;
    }

    // pcem: sdl2-midi.c:7 — `void midi_write(uint8_t val) {}`, et l'empreinte.
    internal static void midi_write(uint8_t val)
    {
        midi_hash ^= val;
        midi_hash *= 1099511628211UL;
        midi_count++;
    }
}
