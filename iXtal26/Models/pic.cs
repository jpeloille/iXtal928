// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/models/pic.c  (struct PIC : includes/private/ibm.h:144-150)
// STATUS: partial — 8259 transcrit (pic et pic2, écriture, lecture, EOI,
//         picint/picintlevel/picintc, picinterrupt, pic_intpending). Hors
//         périmètre : ELCR et dumppic. Les compteurs Counters.n_picint et
//         Counters.n_picinterrupt du stub de M1 sont conservés.

using iXtal26.Diag;
using static iXtal26.Cpu.x86;

namespace iXtal26.Models;

// pcem: ibm.h:144-150
internal sealed class PIC
{
    internal uint8_t icw1, icw4, mask, ins, pend, mask2;
    internal int icw;
    internal uint8_t vector;
    internal int read;
    internal uint8_t level_sensitive;
}

internal static partial class pic
{
    // omitted: `|| romset == ROM_XI8088` aux huit gardes AT (pic.c:14, 65, 299, 302,
    //          317, 338, 345, 359) — `romset` appartient à ibm.h:272 / pc.c, et le
    //          XI8088 est hors cible 5150. Les deux opérandes y sont faux.
    // omitted: pic_elcrx_read / pic_elcrx_write / pic_init_elcrx (pic.c:261-288) —
    //          ELCR, appelé seulement par sis496.c:128 et piix.c:312 (486/PCI).
    // omitted: dumppic (pic.c:396-399) — pclog pur, surface plugin-api.

    // pcem: pic.c:7
    internal static readonly PIC pic_ = new(), pic2 = new();

    // pcem: pic.c:9-11
    internal static int intclear;
    internal static int keywaiting = 0;
    internal static int pic_intpending;

    // pcem: pic.c:13-28
    internal static void pic_updatepending()
    {
        if (AT != 0)
        {
                if (((pic2.pend & ~pic2.mask) & ~pic2.mask2) != 0)
                        pic_.pend |= (1 << 2);
                else
                        pic_.pend &= unchecked((uint8_t)~(1 << 2));
                pic_intpending = (pic_.pend & ~pic_.mask) & ~pic_.mask2;
                if (((pic_.mask | pic_.mask2) & (1 << 2)) == 0)
                        pic_intpending |= ((pic2.pend & ~pic2.mask) & ~pic2.mask2);
        }
        else
                pic_intpending = (pic_.pend & ~pic_.mask) & ~pic_.mask2;
    }

    // pcem: pic.c:30-44
    internal static void pic_reset()
    {
        pic_.icw = 0;
        pic_.mask = 0xFF;
        pic_.mask2 = 0;
        pic_.pend = pic_.ins = 0;
        pic_.vector = 8;
        // pcem bug, fixed in hardware mode: PB-255 — read = 1 met la lecture de 20h sur l'ISR (pic.c:36), et
        //   pic2.read n'est jamais remis ; un 8259A initialisé se lit sur l'IRR.
        pic_.read = 1;
        pic2.icw = 0;
        pic2.mask = 0xFF;
        // pcem bug, reproduced: PB-06 — pic.c:39 écrit `pic.mask2` dans le bloc pic2 ;
        //   pic2.mask2 n'est donc jamais remis à zéro.
        pic_.mask2 = 0;
        pic2.pend = pic2.ins = 0;
        pic_intpending = 0;
        pic_.level_sensitive = 0;
        pic2.level_sensitive = 0;
        // pcem bug, fixed in hardware mode: PB-255 — les deux contrôleurs se lisent sur l'IRR.
        if (materiel.pb_255)
                pic_reset_255_materiel();
        // pcem bug, fixed in hardware mode: PB-248 — l'état qu'ICW1 laisserait : priorités, masque spécial, poll.
        if (materiel.pb_248)
                pic_reset_248_materiel();
    }

    // pcem: pic.c:46-55
    internal static void pic_update_mask(ref uint8_t mask, uint8_t ins)
    {
        int c;
        mask = 0;
        for (c = 0; c < 8; c++)
        {
                if ((ins & (1 << c)) != 0)
                {
                        mask = (uint8_t)(0xff << c);
                        return;
                }
        }
    }

    // pcem: pic.c:57-72
    private static void pic_autoeoi()
    {
        int c;

        for (c = 0; c < 8; c++)
        {
                if ((pic_.ins & (1 << c)) != 0)
                {
                        pic_.ins &= unchecked((uint8_t)~(1 << c));
                        pic_update_mask(ref pic_.mask2, pic_.ins);

                        if (AT != 0 && c == 2 && ((pic2.pend & ~pic2.mask) & ~pic2.mask2) != 0)
                                pic_.pend |= (1 << 2);

                        pic_updatepending();
                        return;
                }
        }
    }

    // pcem: pic.c:74-155
    internal static void pic_write(uint16_t addr, uint8_t val, object priv)
    {
        int c;
        if ((addr & 1) != 0)
        {
                switch (pic_.icw)
                {
                case 0: /*OCW1*/
                        pic_.mask = val;
                        pic_updatepending();
                        break;
                case 1: /*ICW2*/
                        pic_.vector = (uint8_t)(val & 0xF8);
                        if ((pic_.icw1 & 2) != 0)
                                pic_.icw = 3;
                        else
                                pic_.icw = 2;
                        break;
                case 2: /*ICW3*/
                        if ((pic_.icw1 & 1) != 0)
                                pic_.icw = 3;
                        else
                                pic_.icw = 0;
                        break;
                case 3: /*ICW4*/
                        pic_.icw4 = val;
                        pic_.icw = 0;
                        break;
                }
        }
        else
        {
                if ((val & 16) != 0) /*ICW1*/
                {
                        // pcem bug, fixed in hardware mode: PB-255 — ICW1 ne remet pas `read` sur l'IRR (pic.c:106-113).
                        pic_.mask = 0;
                        pic_.mask2 = 0;
                        pic_.icw = 1;
                        pic_.icw1 = val;
                        pic_.ins = 0;
                        if (materiel.pb_255)
                                pic_icw1_255_materiel(pic_);
                        // pcem bug, fixed in hardware mode: PB-248 — ICW1 remet aussi les priorités et le masque spécial.
                        if (materiel.pb_248)
                                pic_icw1_248_materiel(pic_);
                        pic_updatepending();
                }
                else if ((val & 8) == 0) /*OCW2*/
                {
                        // pcem bug, fixed in hardware mode: PB-248 — l'OCW2 selon la fiche (voir plus bas).
                        if (materiel.pb_248)
                        {
                                pic_ocw2_materiel(pic_, val);
                                return;
                        }
                        if ((val & 0xE0) == 0x60)
                        {
                                pic_.ins &= unchecked((uint8_t)~(1 << (val & 7)));
                                pic_update_mask(ref pic_.mask2, pic_.ins);
                                // pcem bug, reproduced: PB-13 — pic.c:121 teste `val == 2` là où tous les sites
                                //   frères testent `c == 2` ; inatteignable, la branche impose val >= 0x60.
                                if (val == 2 && ((pic2.pend & ~pic2.mask) & ~pic2.mask2) != 0)
                                        pic_.pend |= (1 << 2);
                                pic_updatepending();
                        }
                        else
                        {
                                // pcem bug, fixed in hardware mode: PB-248 — tout autre OCW2 (les rotations, la
                                //   priorité, 40h « sans opération ») sert d'EOI non spécifique (pic.c:126-145).
                                for (c = 0; c < 8; c++)
                                {
                                        if ((pic_.ins & (1 << c)) != 0)
                                        {
                                                pic_.ins &= unchecked((uint8_t)~(1 << c));
                                                pic_update_mask(ref pic_.mask2, pic_.ins);

                                                if (c == 2 && ((pic2.pend & ~pic2.mask) & ~pic2.mask2) != 0)
                                                        pic_.pend |= (1 << 2);

                                                if (c == 1 && keywaiting != 0)
                                                {
                                                        intclear &= ~1;
                                                }
                                                pic_updatepending();
                                                return;
                                        }
                                }
                        }
                }
                else /*OCW3*/
                {
                        // pcem bug, fixed in hardware mode: PB-248 — le poll (bit 2) et le masque spécial (bits 6-5)
                        //   sont ignorés (pic.c:146-153).
                        if (materiel.pb_248)
                        {
                                pic_ocw3_materiel(pic_, val);
                                return;
                        }
                        if ((val & 2) != 0)
                                pic_.read = (val & 1);
                        if ((val & 0x40) != 0)
                        {
                        }
                }
        }
    }

    // pcem: pic.c:157-166
    internal static uint8_t pic_read(uint16_t addr, object priv)
    {
        if ((addr & 1) != 0)
        {
                return pic_.mask;
        }
        // pcem bug, fixed in hardware mode: PB-248 — après un poll, la lecture rend le mot de poll et acquitte.
        if (materiel.pb_248)
                if (poll_attendu_materiel(pic_))
                        return pic_poll_lire_materiel(pic_);
        if (pic_.read != 0)
        {
                return (uint8_t)(pic_.ins | (pic2.ins != 0 ? 4 : 0));
        }
        return pic_.pend;
    }

    // pcem: pic.c:168
    internal static void pic_init() { io.io_sethandler(0x0020, 0x0002, pic_read, null, null, pic_write, null, null, null); }

    // pcem: pic.c:170-182
    private static void pic2_autoeoi()
    {
        int c;

        for (c = 0; c < 8; c++)
        {
                if ((pic2.ins & (1 << c)) != 0)
                {
                        pic2.ins &= unchecked((uint8_t)~(1 << c));
                        pic_update_mask(ref pic2.mask2, pic2.ins);

                        pic_updatepending();
                        return;
                }
        }
    }

    // pcem: pic.c:184-246
    internal static void pic2_write(uint16_t addr, uint8_t val, object priv)
    {
        int c;
        if ((addr & 1) != 0)
        {
                switch (pic2.icw)
                {
                case 0: /*OCW1*/
                        pic2.mask = val;
                        pic_updatepending();
                        break;
                case 1: /*ICW2*/
                        pic2.vector = (uint8_t)(val & 0xF8);
                        if ((pic2.icw1 & 2) != 0)
                                pic2.icw = 3;
                        else
                                pic2.icw = 2;
                        break;
                case 2: /*ICW3*/
                        if ((pic2.icw1 & 1) != 0)
                                pic2.icw = 3;
                        else
                                pic2.icw = 0;
                        break;
                case 3: /*ICW4*/
                        pic2.icw4 = val;
                        pic2.icw = 0;
                        break;
                }
        }
        else
        {
                if ((val & 16) != 0) /*ICW1*/
                {
                        // pcem bug, fixed in hardware mode: PB-255 — ICW1 ne remet pas `read` sur l'IRR (pic.c:214-221).
                        pic2.mask = 0;
                        pic2.mask2 = 0;
                        pic2.icw = 1;
                        pic2.icw1 = val;
                        pic2.ins = 0;
                        if (materiel.pb_255)
                                pic_icw1_255_materiel(pic2);
                        // pcem bug, fixed in hardware mode: PB-248 — ICW1 remet aussi les priorités et le masque spécial.
                        if (materiel.pb_248)
                                pic_icw1_248_materiel(pic2);
                        pic_updatepending();
                }
                else if ((val & 8) == 0) /*OCW2*/
                {
                        // pcem bug, fixed in hardware mode: PB-248 — l'OCW2 selon la fiche (voir plus bas).
                        if (materiel.pb_248)
                        {
                                pic_ocw2_materiel(pic2, val);
                                return;
                        }
                        if ((val & 0xE0) == 0x60)
                        {
                                pic2.ins &= unchecked((uint8_t)~(1 << (val & 7)));
                                pic_update_mask(ref pic2.mask2, pic2.ins);

                                pic_updatepending();
                        }
                        else
                        {
                                // pcem bug, fixed in hardware mode: PB-248 — tout autre OCW2 (les rotations, la
                                //   priorité, 40h « sans opération ») sert d'EOI non spécifique (pic.c:229-239).
                                for (c = 0; c < 8; c++)
                                {
                                        if ((pic2.ins & (1 << c)) != 0)
                                        {
                                                pic2.ins &= unchecked((uint8_t)~(1 << c));
                                                pic_update_mask(ref pic2.mask2, pic2.ins);

                                                pic_updatepending();
                                                return;
                                        }
                                }
                        }
                }
                else /*OCW3*/
                {
                        // pcem bug, fixed in hardware mode: PB-248 — le poll (bit 2) et le masque spécial (bits 6-5)
                        //   sont ignorés (pic.c:240-244).
                        if (materiel.pb_248)
                        {
                                pic_ocw3_materiel(pic2, val);
                                return;
                        }
                        if ((val & 2) != 0)
                                pic2.read = (val & 1);
                }
        }
    }

    // pcem: pic.c:248-257
    internal static uint8_t pic2_read(uint16_t addr, object priv)
    {
        if ((addr & 1) != 0)
        {
                return pic2.mask;
        }
        // pcem bug, fixed in hardware mode: PB-248 — après un poll, la lecture rend le mot de poll et acquitte.
        if (materiel.pb_248)
                if (poll_attendu_materiel(pic2))
                        return pic_poll_lire_materiel(pic2);
        if (pic2.read != 0)
        {
                return pic2.ins;
        }
        return pic2.pend;
    }

    // pcem: pic.c:259
    internal static void pic2_init() { io.io_sethandler(0x00a0, 0x0002, pic2_read, null, null, pic2_write, null, null, null); }

    // pcem: pic.c:290-294
    internal static void clearpic()
    {
        pic_.pend = pic_.ins = 0;
        pic_updatepending();
    }

    // pcem: pic.c:296
    internal static readonly int[] pic_current = new int[16];

    // pcem: pic.c:298-311
    internal static void picint(uint16_t num)
    {
        Counters.n_picint++;
        if (AT != 0 && num == (1 << 2))
                num = 1 << 9;
        if (AT != 0 && num > 0xFF)
        {
                pic2.pend |= (uint8_t)(num >> 8);
                if (((pic2.pend & ~pic2.mask) & ~pic2.mask2) != 0)
                        pic_.pend |= (1 << 2);
        }
        else if (num <= 0xff)
        {
                pic_.pend |= (uint8_t)num;
        }
        pic_updatepending();
    }

    // pcem: pic.c:313-331
    internal static void picintlevel(uint16_t num)
    {
        int c = 0;
        while ((num & (1 << c)) == 0)
                c++;
        if (AT != 0 && num == (1 << 2))
        {
                c = 9;
                num = 1 << 9;
        }
        if (pic_current[c] == 0)
        {
                pic_current[c] = 1;
                if (num > 0xFF)
                {
                        pic2.pend |= (uint8_t)(num >> 8);
                }
                else
                {
                        pic_.pend |= (uint8_t)num;
                }
        }
        pic_updatepending();
    }

    // pcem: pic.c:332-353
    internal static void picintc(uint16_t num)
    {
        int c = 0;
        if (num == 0)
                return;
        while ((num & (1 << c)) == 0)
                c++;
        if (AT != 0 && num == (1 << 2))
        {
                c = 9;
                num = 1 << 9;
        }
        pic_current[c] = 0;

        if (AT != 0 && num > 0xff)
        {
                pic2.pend &= unchecked((uint8_t)~(num >> 8));
                if (((pic2.pend & ~pic2.mask) & ~pic2.mask2) == 0)
                        pic_.pend &= unchecked((uint8_t)~(1 << 2));
        }
        else if (num <= 0xff)
        {
                pic_.pend &= unchecked((uint8_t)~num);
        }
        pic_updatepending();
    }

    // pcem: pic.c:355-394
    internal static uint8_t picinterrupt()
    {
        Counters.n_picinterrupt++;
        // pcem bug, fixed in hardware mode: PB-05, PB-246, PB-247 et PB-248 — l'acquittement selon la fiche, chaque
        //   correction seulement si on la demande (picinterrupt_materiel).
        if (materiel.pb_05)
                return picinterrupt_materiel();
        if (materiel.pb_246)
                return picinterrupt_materiel();
        if (materiel.pb_247)
                return picinterrupt_materiel();
        if (materiel.pb_248)
                return picinterrupt_materiel();
        // pcem bug, fixed in hardware mode: PB-247 — temp et temp2 ignorent mask2, le masque de service : la descente
        //   dans l'esclave ne tient pas compte de l'ISR du maître (pic.c:356, :360).
        uint8_t temp = (uint8_t)(pic_.pend & ~pic_.mask);
        int c;
        for (c = 0; c < 8; c++)
        {
                // pcem bug, fixed in hardware mode: PB-246 — la cascade est testée à chaque c, dès c = 0 : les IRQ 8
                //   à 15 passent avant l'IRQ 0 et l'IRQ 1 (pic.c:359).
                if (AT != 0 && (temp & (1 << 2)) != 0)
                {
                        uint8_t temp2 = (uint8_t)(pic2.pend & ~pic2.mask);
                        for (c = 0; c < 8; c++)
                        {
                                if ((temp2 & (1 << c)) != 0)
                                {
                                        if ((pic2.level_sensitive & (1 << c)) == 0)
                                                pic2.pend &= unchecked((uint8_t)~(1 << c));
                                        pic2.ins |= (uint8_t)(1 << c);
                                        pic_update_mask(ref pic2.mask2, pic2.ins);

                                        // pcem bug, fixed in hardware mode: PB-05 — pic.c:368-369 efface le bit `c` de
                                        //   pic.pend (l'indice de l'IRQ du pic2, appliqué au maître) là où
                                        //   les lignes sœurs visent la cascade 2.
                                        if ((pic2.level_sensitive & (1 << c)) == 0)
                                                pic_.pend &= unchecked((uint8_t)~(1 << c));
                                        pic_.ins |= (1 << 2); /*Cascade IRQ*/
                                        pic_update_mask(ref pic_.mask2, pic_.ins);

                                        pic_updatepending();

                                        if ((pic2.icw4 & 0x02) != 0)
                                                pic2_autoeoi();

                                        return (uint8_t)(c + pic2.vector);
                                }
                        }
                }
                else if ((temp & (1 << c)) != 0)
                {
                        if ((pic_.level_sensitive & (1 << c)) == 0)
                                pic_.pend &= unchecked((uint8_t)~(1 << c));
                        pic_.ins |= (uint8_t)(1 << c);
                        pic_update_mask(ref pic_.mask2, pic_.ins);
                        pic_updatepending();

                        if ((pic_.icw4 & 0x02) != 0)
                                pic_autoeoi();
                        return (uint8_t)(c + pic_.vector);
                }
        }
        return 0xFF;
    }
}
