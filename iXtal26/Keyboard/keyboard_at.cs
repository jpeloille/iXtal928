// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/keyboard/keyboard_at.c + includes/private/keyboard/keyboard_at.h
// STATUS: partial — B1b : le 8042 de l'AT en entier. Omis : les branches de douze
//         machines etrangeres (T3100E, Xi8088, GRID1520, SPC6000A, Endeavor, Zappa,
//         Itautec, GA686BX), et les pclog. Voir le registre des omissions.
//
// LE 8042, ET IL TIENT DEUX CHOSES QUI NE SONT PAS DU CLAVIER.
//
// C'est un microcontroleur avec son propre programme, place entre le clavier et le
// bus. IBM avait deux broches libres dessus, et y a cable ce qui n'avait nulle part
// ou aller : LA PORTE A20 et LA LIGNE DE RESET DU PROCESSEUR. Les deux sont sur le
// meme registre, le « output port », bits 1 et 0.
//
// ET C'EST POUR CA QUE B1b NE SE SEPARE PAS DU MODE PROTEGE. Un 286 ne sait pas
// revenir en mode reel : LMSW ne peut pas effacer le bit qu'il a pose. La seule
// issue est la commande 0xFE ci-dessous, qui pulse le reset. Le BIOS de l'AT entre
// en mode protege pour dimensionner la memoire haute, puis ressort PAR LA.
//
// DEUX PORTS ET UN LOQUET, comme le MC146818 mais autrement : 0x60 est la donnee,
// 0x64 la commande. Ecrire en 0x64 arme `want60` quand la commande attend un
// operande, et l'ecriture suivante en 0x60 va alors au CONTROLEUR ; sinon elle va
// au CLAVIER. Ce seul drapeau double la signification du port de donnee.
//
// LES SEIZE OCTETS DE RAM INTERNE sont dans `mem[0x20]`, adressables par les
// commandes 0x20-0x3F en lecture et 0x60-0x7F en ecriture. mem[0] est l'octet de
// commande : bit 0 l'interruption clavier, bit 1 celle de la souris, bit 4
// l'inhibition du clavier, bit 5 celle de la souris, bit 6 la traduction de
// scancodes.
//
// TROIS FILES ET UN SEUL TAMPON DE SORTIE. key_ctrl_queue porte ce que le
// CONTROLEUR repond, key_queue ce que le CLAVIER envoie, mouse_queue la souris.
// Le poll les vide dans `out_new` selon une priorite stricte, et c'est l'ordre des
// `else if` qui la definit — le controleur d'abord, toujours.

using iXtal26.Models;
using static iXtal26.Cpu.cpu_c;
using static iXtal26.Cpu.x86;
using static iXtal26.Keyboard.keyboard;
using static iXtal26.Memory.mem;
using static iXtal26.Models.pic;
using static iXtal26.Models.pit;
using static iXtal26.Mouse.mouse_ps2;
using static iXtal26.Sound.sound_speaker;
using static iXtal26.Video.video;
using static iXtal26.io;
using static iXtal26.pc;
using static iXtal26.ppi_c;
using static iXtal26.timer;

namespace iXtal26.Keyboard;

// pcem: keyboard_at.c:31-65 — la struct anonyme de PCem. Classe et non struct, comme
// keyboard_xt_t : timer_add prend l'adresse des trois chronometres.
internal sealed class keyboard_at_t
{
    internal int initialised;
    internal int want60;
    internal int wantirq, wantirq12;
    internal uint8_t command;
    internal uint8_t status;
    internal readonly uint8_t[] mem = new uint8_t[0x20];
    internal uint8_t @out;
    internal int out_new, out_delayed;

    internal int scancode_set;
    internal int translate;
    internal int next_is_release;

    internal uint8_t input_port;
    internal uint8_t output_port;

    internal uint8_t key_command;
    internal int key_wantdata;

    internal int last_irq;

    internal mouse_write_fn? mouse_write;
    internal object? mouse_p;

    internal readonly pc_timer_t refresh_timer = new();
    internal int refresh;

    internal int is_ps2;

    internal readonly pc_timer_t send_delay_timer = new();

    internal int reset_delay;
}

// pcem: keyboard_at.h:7 — la signature que keyboard_at_set_mouse enregistre.
internal delegate void mouse_write_fn(uint8_t val, object? p);

internal static partial class keyboard_at
{
    // pcem: keyboard_at.c:17-25 — STAT_MFULL partage 0x20 avec STAT_TTIMEOUT, et ce
    // n'est pas une coquille de PCem : le meme bit dit « tampon souris plein » sur un
    // 8042 de PS/2 et « depassement de delai en emission » sur celui d'un AT.
    private const int STAT_PARITY = 0x80;
    private const int STAT_RTIMEOUT = 0x40;
    private const int STAT_TTIMEOUT = 0x20;
    private const int STAT_MFULL = 0x20;
    private const int STAT_LOCK = 0x10;
    private const int STAT_CD = 0x08;
    private const int STAT_SYSFLAG = 0x04;
    private const int STAT_IFULL = 0x02;
    private const int STAT_OFULL = 0x01;

    // pcem: keyboard_at.c:27
    private static uint64_t PS2_REFRESH_TIME => (uint64_t)(16 * TIMER_USEC);

    // pcem: keyboard_at.c:29 — 600 ms, et le commentaire de PCem le dit. La valeur est
    // un COMPTE DE TOURS DE POLL, pas une duree : le poll se rearme toutes les 100 us,
    // donc 1000 tours font bien 100 ms... et PCem en met 1000 en annoncant 600 ms.
    // L'ecart est celui de l'oracle, on le porte tel quel.
    private const int RESET_DELAY_TIME = 100 * 10;

    // pcem: keyboard_at.c:65
    // CS0542 : la globale `keyboard_at` ne peut pas porter le nom de la classe
    // conteneur. Suffixe `_`, comme keyboard_xt_, pic_ et pit_.
    // PAS `readonly` : keyboard_at_init la REMPLACE, voir plus bas.
    internal static keyboard_at_t keyboard_at_ = new();

    /*Translation table taken from https://www.win.tue.nl/~aeb/linux/kbd/scancodes-10.html#ss10.3*/
    // pcem: keyboard_at.c:67-80 — le jeu 2 vers le jeu 1. Le clavier d'un AT parle le
    // jeu 2 ; le BIOS et DOS attendent le jeu 1. C'est le 8042 qui traduit, quand
    // mem[0] bit 6 le lui demande, et cette table EST cette traduction.
    private static readonly uint8_t[] at_translation = [
        0xff, 0x43, 0x41, 0x3f, 0x3d, 0x3b, 0x3c, 0x58, 0x64, 0x44, 0x42, 0x40, 0x3e, 0x0f, 0x29, 0x59, 0x65, 0x38, 0x2a, 0x70,
        0x1d, 0x10, 0x02, 0x5a, 0x66, 0x71, 0x2c, 0x1f, 0x1e, 0x11, 0x03, 0x5b, 0x67, 0x2e, 0x2d, 0x20, 0x12, 0x05, 0x04, 0x5c,
        0x68, 0x39, 0x2f, 0x21, 0x14, 0x13, 0x06, 0x5d, 0x69, 0x31, 0x30, 0x23, 0x22, 0x15, 0x07, 0x5e, 0x6a, 0x72, 0x32, 0x24,
        0x16, 0x08, 0x09, 0x5f, 0x6b, 0x33, 0x25, 0x17, 0x18, 0x0b, 0x0a, 0x60, 0x6c, 0x34, 0x35, 0x26, 0x27, 0x19, 0x0c, 0x61,
        0x6d, 0x73, 0x28, 0x74, 0x1a, 0x0d, 0x62, 0x6e, 0x3a, 0x36, 0x1c, 0x1b, 0x75, 0x2b, 0x63, 0x76, 0x55, 0x56, 0x77, 0x78,
        0x79, 0x7a, 0x0e, 0x7b, 0x7c, 0x4f, 0x7d, 0x4b, 0x47, 0x7e, 0x7f, 0x6f, 0x52, 0x53, 0x50, 0x4c, 0x4d, 0x48, 0x01, 0x45,
        0x57, 0x4e, 0x51, 0x4a, 0x37, 0x49, 0x46, 0x54, 0x80, 0x81, 0x82, 0x41, 0x54, 0x85, 0x86, 0x87, 0x88, 0x89, 0x8a, 0x8b,
        0x8c, 0x8d, 0x8e, 0x8f, 0x90, 0x91, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9a, 0x9b, 0x9c, 0x9d, 0x9e, 0x9f,
        0xa0, 0xa1, 0xa2, 0xa3, 0xa4, 0xa5, 0xa6, 0xa7, 0xa8, 0xa9, 0xaa, 0xab, 0xac, 0xad, 0xae, 0xaf, 0xb0, 0xb1, 0xb2, 0xb3,
        0xb4, 0xb5, 0xb6, 0xb7, 0xb8, 0xb9, 0xba, 0xbb, 0xbc, 0xbd, 0xbe, 0xbf, 0xc0, 0xc1, 0xc2, 0xc3, 0xc4, 0xc5, 0xc6, 0xc7,
        0xc8, 0xc9, 0xca, 0xcb, 0xcc, 0xcd, 0xce, 0xcf, 0xd0, 0xd1, 0xd2, 0xd3, 0xd4, 0xd5, 0xd6, 0xd7, 0xd8, 0xd9, 0xda, 0xdb,
        0xdc, 0xdd, 0xde, 0xdf, 0xe0, 0xe1, 0xe2, 0xe3, 0xe4, 0xe5, 0xe6, 0xe7, 0xe8, 0xe9, 0xea, 0xeb, 0xec, 0xed, 0xee, 0xef,
        0xf0, 0xf1, 0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8, 0xf9, 0xfa, 0xfb, 0xfc, 0xfd, 0xfe, 0xff];

    // pcem: keyboard_at.c:81-85
    private static readonly uint8_t[] key_ctrl_queue = new uint8_t[16];
    private static int key_ctrl_queue_start = 0, key_ctrl_queue_end = 0;

    private static readonly uint8_t[] key_queue = new uint8_t[16];
    private static int key_queue_start = 0, key_queue_end = 0;

    // pcem: keyboard_at.c:87-88
    internal static readonly uint8_t[] mouse_queue = new uint8_t[16];
    internal static int mouse_queue_start = 0, mouse_queue_end = 0;

    // mouse_scan vit dans Mouse/mouse_ps2.cs (mouse_ps2.c:10), depuis PS2.0.

    // pcem: keyboard_at.c:92-149 — LE POLL, ET SON ORDRE DE PRIORITE EST LA SPEC.
    //
    // Un seul tampon de sortie pour trois files. La chaine de `else if` ci-dessous
    // decide qui parle : le CONTROLEUR d'abord (key_ctrl_queue), puis ce qui avait ete
    // differe, puis la SOURIS, puis le CLAVIER. Inverser deux branches changerait ce
    // que le POST lit.
    //
    // LES DRAPEAUX 0x100 ET 0x200 NE SONT PAS DES DONNEES : out_new est un int, pas un
    // octet, et ses bits hauts marquent la PROVENANCE — 0x100 la souris, 0x200 le
    // controleur, rien le clavier. C'est ce qui permet a keyboard_at_adddata de savoir
    // s'il peut ecraser ce qui est en attente. -1 veut dire « vide ».
    internal static void keyboard_at_poll()
    {
        timer_advance_u64(keyboard_at_.send_delay_timer, (uint64_t)(100 * TIMER_USEC));
        if (materiel.pb_254)
                keyboard_at_livrer_materiel();

        if (keyboard_at_.out_new != -1 && keyboard_at_.last_irq == 0)
        {
                keyboard_at_.wantirq = 0;
                if ((keyboard_at_.out_new & 0x100) != 0)
                {
                        if (mouse_scan != 0)
                        {
                                // omitted: pclog("keyboard_at : take IRQ12\n") — sortie pure.
                                // picint(0x1000) EST L'IRQ 12, sur le SECOND 8259 : le masque
                                // fait seize bits et le bit 12 est sa cinquieme ligne.
                                if ((keyboard_at_.mem[0] & 0x02) != 0)
                                        picint(0x1000);
                                keyboard_at_.@out = (uint8_t)(keyboard_at_.out_new & 0xff);
                                keyboard_at_.out_new = -1;
                                keyboard_at_.status |= STAT_OFULL;
                                keyboard_at_.status &= unchecked((uint8_t)~STAT_IFULL);
                                keyboard_at_.status |= STAT_MFULL;
                                keyboard_at_.last_irq = 0x1000;
                        }
                        else
                        {
                                // omitted: pclog("keyboard_at: suppressing IRQ12\n") — sortie pure.
                                keyboard_at_.out_new = -1;
                        }
                }
                else
                {
                        if ((keyboard_at_.mem[0] & 0x01) != 0)
                                picint(2);
                        keyboard_at_.@out = (uint8_t)(keyboard_at_.out_new & 0xff);
                        keyboard_at_.out_new = -1;
                        keyboard_at_.status |= STAT_OFULL;
                        keyboard_at_.status &= unchecked((uint8_t)~STAT_IFULL);
                        keyboard_at_.status &= unchecked((uint8_t)~STAT_MFULL);
                        // omitted: pclog("keyboard_at : take IRQ1\n") — sortie pure.
                        keyboard_at_.last_irq = 2;
                }
        }

        // LA TROISIEME BRANCHE EST MORTE, ET C'EST DANS PCem. Elle repete la deuxieme
        // en ajoutant `!(mem[0] & 0x10)` : une condition PLUS STRICTE sur un `else if`
        // dont la version LARGE precede. Aucune entree ne peut l'atteindre. Portee
        // telle quelle — la corriger serait reecrire l'oracle.
        if (keyboard_at_.out_new == -1 && (keyboard_at_.status & STAT_OFULL) == 0 &&
            key_ctrl_queue_start != key_ctrl_queue_end)
        {
                keyboard_at_.out_new = key_ctrl_queue[key_ctrl_queue_start] | 0x200;
                key_ctrl_queue_start = (key_ctrl_queue_start + 1) & 0xf;
        }
        else if ((keyboard_at_.status & STAT_OFULL) == 0 && keyboard_at_.out_new == -1 &&
                 keyboard_at_.out_delayed != -1)
        {
                keyboard_at_.out_new = keyboard_at_.out_delayed;
                keyboard_at_.out_delayed = -1;
        }
        else if ((keyboard_at_.status & STAT_OFULL) == 0 && keyboard_at_.out_new == -1 &&
                 (keyboard_at_.mem[0] & 0x10) == 0 && keyboard_at_.out_delayed != -1)
        {
                keyboard_at_.out_new = keyboard_at_.out_delayed;
                keyboard_at_.out_delayed = -1;
        }
        else if ((keyboard_at_.status & STAT_OFULL) == 0 && keyboard_at_.out_new == -1 &&
                 /*!(keyboard_at.mem[0] & 0x20) &&*/ mouse_queue_start != mouse_queue_end)
        {
                keyboard_at_.out_new = mouse_queue[mouse_queue_start] | 0x100;
                mouse_queue_start = (mouse_queue_start + 1) & 0xf;
        }
        else if ((keyboard_at_.status & STAT_OFULL) == 0 && keyboard_at_.out_new == -1 &&
                 (keyboard_at_.mem[0] & 0x10) == 0 && key_queue_start != key_queue_end)
        {
                keyboard_at_.out_new = key_queue[key_queue_start];
                key_queue_start = (key_queue_start + 1) & 0xf;
        }

        if (keyboard_at_.reset_delay != 0)
        {
                keyboard_at_.reset_delay--;
                if (keyboard_at_.reset_delay == 0)
                        keyboard_at_adddata_keyboard(0xaa);
        }
    }

    // pcem: keyboard_at.c:151-159 — la reponse du CONTROLEUR.
    //
    // ET ELLE PEUT ECRASER CE QUI ATTEND : si out_new ne porte ni 0x100 ni 0x200, donc
    // s'il vient du CLAVIER, il est repousse dans out_delayed. Le controleur passe
    // devant, et le poll rendra la main au clavier ensuite.
    internal static void keyboard_at_adddata(uint8_t val)
    {
        // pcem bug, fixed in hardware mode: PB-254 — aucune garde : au seizième octet en attente, end rejoint start
        //   et la file paraît vide.
        if (materiel.pb_254)
                if (keyboard_at_controleur_materiel(val))
                        return;
        key_ctrl_queue[key_ctrl_queue_end] = val;
        key_ctrl_queue_end = (key_ctrl_queue_end + 1) & 0xf;

        if ((keyboard_at_.out_new & 0x300) == 0)
        {
                keyboard_at_.out_delayed = keyboard_at_.out_new;
                keyboard_at_.out_new = -1;
        }
    }

    // pcem: keyboard_at.c:161-232 — ce que le CLAVIER envoie.
    internal static void keyboard_at_adddata_keyboard(uint8_t val)
    {
        if (keyboard_at_.reset_delay != 0)
                return;
        // pcem bug, fixed in hardware mode: PB-254 — le tampon du clavier (plus bas, la file sans garde).
        if (materiel.pb_254)
        {
                keyboard_at_clavier_materiel(val);
                return;
        }

        // omitted: le bloc `romset == ROM_T3100E` (keyboard_at.c:173-217) — quinze cas
        //   de t3100e_notify_set pour la touche « Fn » du Toshiba T3100e, machine non
        //   transcrite. La garde est fausse pour un IBM AT, mesure : AtProbe pose
        //   h_set_romset(ROM_IBMAT).

        // LA TRADUCTION EST A ETAT, et 0xF0 est son prefixe de relachement. Le jeu 2
        // annonce un relachement par un octet 0xF0 SEPARE ; le jeu 1 le code dans le
        // bit 7 de l'octet lui-meme. Le 8042 doit donc retenir qu'il a vu 0xF0 —
        // next_is_release — et le reporter sur l'octet SUIVANT. C'est le seul etat que
        // ce fichier porte entre deux appels.
        if (keyboard_at_.translate != 0)
        {
                if (val == 0xf0)
                {
                        keyboard_at_.next_is_release = 1;
                        return;
                }
                else
                {
                        val = at_translation[val];
                        if (keyboard_at_.next_is_release != 0)
                                val |= 0x80;
                        keyboard_at_.next_is_release = 0;
                }
        }
        // pcem bug, fixed in hardware mode: PB-254 — aucune garde : au seizième code en attente, la file paraît
        //   vide ; le clavier de l'AT en garde seize et remplace le dix-septième par 00h.
        key_queue[key_queue_end] = val;
        key_queue_end = (key_queue_end + 1) & 0xf;
        // omitted: pclog("keyboard_at : %02X added to key queue\n", val) — sortie pure.
        return;
    }

    // pcem: keyboard_at.c:234-238
    internal static void keyboard_at_adddata_mouse(uint8_t val)
    {
        // pcem bug, fixed in hardware mode: PB-254 — aucune garde : au seizième octet en attente, la file paraît
        //   vide ; la vraie souris attend que le contrôleur relâche la ligne « clock ».
        if (materiel.pb_254)
                if (keyboard_at_souris_materiel(val))
                        return;
        mouse_queue[mouse_queue_end] = val;
        mouse_queue_end = (mouse_queue_end + 1) & 0xf;
        // omitted: pclog("keyboard_at : %02X added to mouse queue\n", val) — sortie pure.
        return;
    }

    // pcem: keyboard_at.c:240-755
    internal static void keyboard_at_write(uint16_t port, uint8_t val, object? priv)
    {
        // omitted: pclog de trace (keyboard_at.c:241) — sortie pure.
        // omitted: `if (romset == ROM_XI8088 && port == 0x63) port = 0x61` (:242-243) —
        //   le Xi8088 duplique le port B du PPI en 0x63. Machine non transcrite.
        switch (port)
        {
        case 0x60:
                if (keyboard_at_.want60 != 0)
                {
                        /*Write to controller*/
                        keyboard_at_.want60 = 0;
                        switch (keyboard_at_.command)
                        {
                        // 0x60-0x7F : ECRIRE DANS LA RAM INTERNE. L'adresse est dans la
                        // commande elle-meme, `command & 0x1f`.
                        case 0x60: case 0x61: case 0x62: case 0x63:
                        case 0x64: case 0x65: case 0x66: case 0x67:
                        case 0x68: case 0x69: case 0x6a: case 0x6b:
                        case 0x6c: case 0x6d: case 0x6e: case 0x6f:
                        case 0x70: case 0x71: case 0x72: case 0x73:
                        case 0x74: case 0x75: case 0x76: case 0x77:
                        case 0x78: case 0x79: case 0x7a: case 0x7b:
                        case 0x7c: case 0x7d: case 0x7e: case 0x7f:
                                keyboard_at_.mem[keyboard_at_.command & 0x1f] = val;
                                if (keyboard_at_.command == 0x60)
                                {
                                        // mem[0] EST L'OCTET DE COMMANDE, et l'ecrire a des
                                        // effets de BORD : il peut lever une interruption
                                        // en attente, ou en retirer une.
                                        if ((val & 1) != 0 && (keyboard_at_.status & STAT_OFULL) != 0)
                                                keyboard_at_.wantirq = 1;
                                        if ((val & 1) == 0 && keyboard_at_.wantirq != 0)
                                                keyboard_at_.wantirq = 0;
                                        mouse_scan = (val & 0x20) == 0 ? 1 : 0;
                                        keyboard_at_.translate = val & 0x40;
                                }
                                break;

                        // omitted: case 0xb6 (:301-304) — T3100e, t3100e_mono_set.
                        case 0xcb: /*AMI - set keyboard mode*/
                                break;

                        case 0xcf: /*??? - sent by MegaPC BIOS*/
                                break;

                        // A20, ET C'EST ICI QUE LE 8042 LA COMMANDE. Bit 1 du port de
                        // sortie. Le test `^` est essentiel : on ne recalcule que sur un
                        // CHANGEMENT, sinon flushmmucache() serait appele a chaque
                        // ecriture du registre.
                        case 0xd1: /*Write output port*/
                                if (((keyboard_at_.output_port ^ val) & 0x02) != 0) /*A20 enable change*/
                                {
                                        mem_a20_key = val & 0x02;
                                        mem_a20_recalc();
                                        flushmmucache();
                                }
                                keyboard_at_.output_port = val;
                                break;

                        case 0xd2: /*Write to keyboard output buffer*/
                                keyboard_at_adddata(val);
                                break;

                        case 0xd3: /*Write to mouse output buffer*/
                                keyboard_at_adddata_mouse(val);
                                break;

                        case 0xd4: /*Write to mouse*/
                                if (keyboard_at_.mouse_write is not null)
                                {
                                        keyboard_at_.mouse_write(val, keyboard_at_.mouse_p);
                                        /*Implicitly enable mouse*/
                                        mouse_scan = 1;
                                        keyboard_at_.mem[0] &= unchecked((uint8_t)~0x20);
                                }
                                break;

                        default:
                                // omitted: pclog("Bad AT keyboard controller 0060 write...") — sortie pure.
                                break;
                        }
                }
                else
                {
                        /*Write to keyboard*/
                        keyboard_at_.mem[0] &= unchecked((uint8_t)~0x10);
                        if (keyboard_at_.key_wantdata != 0)
                        {
                                keyboard_at_.key_wantdata = 0;
                                switch (keyboard_at_.key_command)
                                {
                                case 0xed: /*Set/reset LEDs*/
                                        keyboard_at_adddata_keyboard(0xfa);
                                        break;

                                case 0xf0: /*Set scancode set*/
                                        switch (val)
                                        {
                                        case 0: /*Read current set*/
                                                keyboard_at_adddata_keyboard(0xfa);
                                                switch (keyboard_at_.scancode_set)
                                                {
                                                case SCANCODE_SET_1:
                                                        keyboard_at_adddata_keyboard(0x01);
                                                        break;
                                                case SCANCODE_SET_2:
                                                        keyboard_at_adddata_keyboard(0x02);
                                                        break;
                                                case SCANCODE_SET_3:
                                                        keyboard_at_adddata_keyboard(0x03);
                                                        break;
                                                }
                                                break;
                                        case 1:
                                                keyboard_at_.scancode_set = SCANCODE_SET_1;
                                                keyboard_set_scancode_set(SCANCODE_SET_1);
                                                keyboard_at_adddata_keyboard(0xfa);
                                                break;
                                        case 2:
                                                keyboard_at_.scancode_set = SCANCODE_SET_2;
                                                keyboard_set_scancode_set(SCANCODE_SET_2);
                                                keyboard_at_adddata_keyboard(0xfa);
                                                break;
                                        case 3:
                                                keyboard_at_.scancode_set = SCANCODE_SET_3;
                                                keyboard_set_scancode_set(SCANCODE_SET_3);
                                                keyboard_at_adddata_keyboard(0xfa);
                                                break;
                                        default:
                                                keyboard_at_adddata_keyboard(0xfe);
                                                break;
                                        }
                                        break;

                                case 0xf3: /*Set typematic rate/delay*/
                                        keyboard_at_adddata_keyboard(0xfa);
                                        break;

                                default:
                                        // omitted: pclog("Bad AT keyboard 0060 write...") — sortie pure.
                                        break;
                                }
                        }
                        else
                        {
                                keyboard_at_.key_command = val;
                                switch (val)
                                {
                                case 0x05: /*??? - sent by NT 4.0*/
                                        keyboard_at_adddata_keyboard(0xfe);
                                        break;

                                case 0xed: /*Set/reset LEDs*/
                                        keyboard_at_.key_wantdata = 1;
                                        keyboard_at_adddata_keyboard(0xfa);
                                        break;

                                case 0xee: /*Diagnostic echo*/
                                        keyboard_at_adddata_keyboard(0xee);
                                        break;

                                case 0xf0: /*Set scancode set*/
                                        keyboard_at_.key_wantdata = 1;
                                        keyboard_at_adddata_keyboard(0xfa);
                                        break;

                                case 0xf2: /*Read ID*/
                                        keyboard_at_adddata_keyboard(0xfa);
                                        keyboard_at_adddata_keyboard(0xab);
                                        keyboard_at_adddata_keyboard(0x83);
                                        break;

                                case 0xf3: /*Set typematic rate/delay*/
                                        keyboard_at_.key_wantdata = 1;
                                        keyboard_at_adddata_keyboard(0xfa);
                                        break;

                                case 0xf4: /*Enable keyboard*/
                                        keyboard_scan = 1;
                                        keyboard_at_adddata_keyboard(0xfa);
                                        break;
                                case 0xf5: /*Disable keyboard*/
                                        keyboard_scan = 0;
                                        keyboard_at_adddata_keyboard(0xfa);
                                        break;

                                case 0xff:                                   /*Reset*/
                                        key_queue_start = key_queue_end = 0; /*Clear key queue*/
                                        if (materiel.pb_254)
                                                keyboard_at_clavier_vider_materiel();
                                        keyboard_at_adddata_keyboard(0xfa);
                                        keyboard_at_.reset_delay = RESET_DELAY_TIME;
                                        break;

                                default:
                                        // omitted: pclog("Bad AT keyboard command %02X") — sortie pure.
                                        keyboard_at_adddata_keyboard(0xfe);
                                        break;
                                }
                        }
                }
                break;

        // 0x61 EST LE PORT B DU PPI, PAS LE 8042 — mais c'est le 8042 qui l'a sur son
        // decodage d'adresse, io_sethandler couvrant 0x60 a 0x64. Bits 0 et 1 : la
        // porte du canal 2 du PIT et l'autorisation du haut-parleur.
        case 0x61:
                ppi.pb = val;

                speaker_update();
                speaker_gated = val & 1;
                speaker_enable = val & 2;
                if (speaker_enable != 0)
                        was_speaker_enable = 1;
                pit_set_gate(pit_, 2, val & 1);

                // omitted: la branche `romset == ROM_XI8088` (:471-476) — xi8088_turbo_set.
                break;

        case 0x64:
                keyboard_at_.want60 = 0;
                keyboard_at_.command = val;
                /*New controller command*/
                switch (val)
                {
                // omitted: case 0x09 (:482-486) — GRID1520, delai de rétroeclairage.

                // 0x20-0x3F : LIRE LA RAM INTERNE, adresse dans `val & 0x1f`.
                case 0x20: case 0x21: case 0x22: case 0x23:
                case 0x24: case 0x25: case 0x26: case 0x27:
                case 0x28: case 0x29: case 0x2a: case 0x2b:
                case 0x2c: case 0x2d: case 0x2e: case 0x2f:
                case 0x30: case 0x31: case 0x32: case 0x33:
                case 0x34: case 0x35: case 0x36: case 0x37:
                case 0x38: case 0x39: case 0x3a: case 0x3b:
                case 0x3c: case 0x3d: case 0x3e: case 0x3f:
                        keyboard_at_adddata(keyboard_at_.mem[val & 0x1f]);
                        break;

                // 0x60-0x7F : ARMER l'ecriture. La donnee viendra par 0x60.
                case 0x60: case 0x61: case 0x62: case 0x63:
                case 0x64: case 0x65: case 0x66: case 0x67:
                case 0x68: case 0x69: case 0x6a: case 0x6b:
                case 0x6c: case 0x6d: case 0x6e: case 0x6f:
                case 0x70: case 0x71: case 0x72: case 0x73:
                case 0x74: case 0x75: case 0x76: case 0x77:
                case 0x78: case 0x79: case 0x7a: case 0x7b:
                case 0x7c: case 0x7d: case 0x7e: case 0x7f:
                        keyboard_at_.want60 = 1;
                        break;

                case 0xa1: /*AMI - get controlled version*/
                        break;

                case 0xa7: /*Disable mouse port*/
                        mouse_scan = 0;
                        keyboard_at_.mem[0] |= 0x20;
                        break;

                case 0xa8: /*Enable mouse port*/
                        mouse_scan = 1;
                        keyboard_at_.mem[0] &= unchecked((uint8_t)~0x20);
                        break;

                case 0xa9:                         /*Test mouse port*/
                        keyboard_at_adddata(0x00); /*no error*/
                        break;

                // 0xAA, L'AUTO-TEST — ET IL OUVRE A20. C'est la premiere commande que
                // le POST envoie, et le 0x55 qu'elle repond est ce qu'il attend. Son
                // effet de bord est le plus important du fichier : elle remet le port
                // de sortie a 0xCF, dont le bit 1 est pose, donc A20 OUVERTE.
                case 0xaa: /*Self-test*/
                        if (keyboard_at_.initialised == 0)
                        {
                                keyboard_at_.initialised = 1;
                                key_ctrl_queue_start = key_ctrl_queue_end = 0;
                                if (materiel.pb_254)
                                        keyboard_at_controleur_vider_materiel();
                                keyboard_at_.status &= unchecked((uint8_t)~STAT_OFULL);
                        }
                        // omitted: `romset == ROM_T3100E || ROM_SPC6000A` -> status |= STAT_IFULL
                        //   (:583-585) — deux machines non transcrites qui l'attendent
                        //   immediatement apres le 0xAA. Un IBM AT ne le veut pas.
                        keyboard_at_.status |= STAT_SYSFLAG;
                        keyboard_at_.mem[0] |= 0x04;
                        keyboard_at_adddata(0x55);
                        /*Self-test also resets the output port, enabling A20*/
                        if ((keyboard_at_.output_port & 0x02) == 0)
                        {
                                mem_a20_key = 2;
                                mem_a20_recalc();
                                flushmmucache();
                        }
                        keyboard_at_.output_port = 0xcf;
                        break;

                case 0xab:                         /*Interface test*/
                        keyboard_at_adddata(0x00); /*no error*/
                        break;

                case 0xad: /*Disable keyboard*/
                        keyboard_at_.mem[0] |= 0x10;
                        break;

                case 0xae: /*Enable keyboard*/
                        keyboard_at_.mem[0] &= unchecked((uint8_t)~0x10);
                        break;

                // omitted: les cas 0xb0 a 0xb6 et 0xbb, 0xbc (:611-670) — T3100e :
                //   turbo, choix d'ecran, configuration, octet couleur/mono, touche
                //   « Fn ». Tous gardes par `romset == ROM_T3100E`, donc sans effet sur
                //   un IBM AT. 0xb6 y arme want60, les autres repondent ou ne font rien.
                // omitted: case 0xba (:656-659) — Endeavor, Zappa, Itautec.
                // omitted: les commentaires de PCem sur 0xB7/0xB8 (T3100e, non
                //   implementees chez lui non plus) et 0xE8/0xE9 (AWARD 286, LED turbo,
                //   fonction non confirmee) : ils documentent du code ABSENT de l'oracle.

                // 0xC0 EST UN COMPTEUR DEGUISE. Il rend le port d'entree, mais il
                // INCREMENTE ses deux bits bas au passage — `(input_port + 1) & 3`, les
                // six bits hauts preserves. Le POST s'en sert pour verifier que le
                // 8042 repond vraiment et ne renvoie pas une valeur figee.
                case 0xc0: /*Read input port*/
                        // omitted: les deux branches par romset (:673-679) — T3100e
                        //   (input_port selon t3100e_mono_get) et Endeavor/Zappa/Itautec
                        //   (`| 4 | 0x40`). La branche de l'AT est celle-ci.
                        keyboard_at_adddata((uint8_t)(keyboard_at_.input_port | 4));

                        keyboard_at_.input_port =
                                (uint8_t)(((keyboard_at_.input_port + 1) & 3) | (keyboard_at_.input_port & 0xfc));
                        break;

                case 0xc9: /*AMI - block P22 and P23 ??? */
                        break;

                case 0xca:                                 /*AMI - read keyboard mode*/
                        // omitted: la branche `romset == ROM_GA686BX` (:692-693), marquee
                        //   marquee « a faire » par PCem lui-meme, qui rend 0x01 (mode PS/2).
                        keyboard_at_adddata(0x00); /*ISA mode*/
                        break;

                case 0xcb: /*AMI - set keyboard mode*/
                        keyboard_at_.want60 = 1;
                        break;

                case 0xcf: /*??? - sent by MegaPC BIOS*/
                        keyboard_at_.want60 = 1;
                        break;

                case 0xd0: /*Read output port*/
                        keyboard_at_adddata(keyboard_at_.output_port);
                        break;

                case 0xd1: /*Write output port*/
                        keyboard_at_.want60 = 1;
                        break;

                case 0xd2: /*Write keyboard output buffer*/
                        keyboard_at_.want60 = 1;
                        break;

                case 0xd3: /*Write mouse output buffer*/
                        keyboard_at_.want60 = 1;
                        break;

                case 0xd4: /*Write to mouse*/
                        keyboard_at_.want60 = 1;
                        break;

                case 0xe0: /*Read test inputs*/
                        keyboard_at_adddata(0x00);
                        break;

                case 0xef: /*??? - sent by AMI486*/
                        break;

                // LA LIGNE DE RESET, ET C'EST LA SEULE SORTIE DU MODE PROTEGE D'UN 286.
                //
                // Les huit opcodes pairs de 0xF0 a 0xFE sont « pulse output port », le
                // quartet bas disant quelles broches pulser. PCem ne distingue pas :
                // les huit font le reset, parce que la broche 0 EST la ligne de reset
                // et que seul ce cas-la compte.
                case 0xf0: case 0xf2: case 0xf4: case 0xf6:
                case 0xf8: case 0xfa: case 0xfc:
                case 0xfe:              /*Pulse output port - pin 0 selected - x86 reset*/
                        Cpu._808x.softresetx86(); /*Pulse reset!*/
                        // DEVIATION: cpu_set_edx() (:751) non appele, et l'oracle
                        //   l'enveloppe a vide (__wrap_cpu_set_edx, harness_stubs.c).
                        //   Jusqu'a M16 le motif etait « cpu[0].cpus est NUL » ; il ne
                        //   l'est plus. PCem pose EDX = edx_reset — 0 dans cpus_286 comme
                        //   dans cpus_ibmat — apres ce reset ; ici DX GARDE SA VALEUR, des
                        //   deux cotes. Le transcrire change l'etat vu par le BIOS au
                        //   retour du mode protege : un levier a part, des deux cotes.
                        break;

                case 0xff: /*Pulse output port - but no pins selected - sent by MegaPC BIOS*/
                        break;

                default:
                        // omitted: pclog("Bad AT keyboard controller command %02X") — sortie pure.
                        break;
                }
                break;
        }
    }

    // pcem: keyboard_at.c:757-800
    internal static uint8_t keyboard_at_read(uint16_t port, object? priv)
    {
        uint8_t temp = 0xff;

        // UN IBM AT NE PAIE PAS CES HUIT CYCLES, et c'est bien une condition vivante :
        // le 8042 d'un AT est sur le bus systeme, pas derriere un pont ISA. Mesure :
        // romset vaut ROM_IBMAT cote oracle (AtProbe fait h_set_romset), donc les deux
        // cotes prennent la meme branche — celle qui ne facture rien.
        if (romset != ROM_IBMAT && romset != ROM_IBMXT286)
                cycles -= ISA_CYCLES(8);
        // omitted: pclog de trace (:760) — sortie pure.
        // omitted: `romset == ROM_XI8088 && port == 0x63` (:761-762) — non transcrite.
        switch (port)
        {
        // LIRE 0x60 ACQUITTE L'INTERRUPTION. picintc(last_irq) est ce qui la retire, et
        // last_irq retient LAQUELLE — l'IRQ 1 du clavier ou l'IRQ 12 de la souris.
        case 0x60:
                temp = keyboard_at_.@out;
                keyboard_at_.status &= unchecked((uint8_t)~(STAT_OFULL /* | STAT_MFULL*/));
                picintc((uint16_t)keyboard_at_.last_irq);
                keyboard_at_.last_irq = 0;
                break;

        case 0x61:
                temp = (uint8_t)(ppi.pb & ~0xe0);
                if (ppispeakon != 0)
                        temp |= 0x20;
                if (keyboard_at_.is_ps2 != 0)
                {
                        if (keyboard_at_.refresh != 0)
                                temp |= 0x10;
                        else
                                temp &= unchecked((uint8_t)~0x10);
                }
                // omitted: la branche `romset == ROM_XI8088` (:783-788) — bit turbo.
                break;

        case 0x64:
                temp = (uint8_t)(keyboard_at_.status & ~4);
                if ((keyboard_at_.mem[0] & 0x04) != 0)
                        temp |= 0x04;
                keyboard_at_.status &= unchecked((uint8_t)~(STAT_RTIMEOUT /* | STAT_TTIMEOUT*/));
                break;
        }
        // omitted: pclog de trace (:806) — sortie pure.
        return temp;
    }

    // pcem: keyboard_at.c:810-827
    //
    // L'ETAT DE DEPART DIT DEJA CE QUE LE POST VA TROUVER : status = LOCK | CD, donc
    // « clavier deverrouille » et « la derniere ecriture etait une commande » ;
    // mem[0] = 0x11, donc interruption clavier autorisee ET clavier inhibe ; et le
    // port de sortie a 0xCF, dont le bit 1 laisse A20 ouverte.
    internal static void keyboard_at_reset()
    {
        keyboard_at_.initialised = 0;
        keyboard_at_.status = STAT_LOCK | STAT_CD;
        keyboard_at_.mem[0] = 0x11;
        keyboard_at_.wantirq = 0;
        keyboard_at_.output_port = 0xcf;
        // omitted: la branche `romset == ROM_XI8088` (:816-817), qui inverse 0xb0/0xf0.
        //
        // input_port EST LE DIP DE LA CARTE VIDEO : 0xF0 pour une MDA (G9.0), 0xB0 sinon —
        // l'oracle rend la meme reponse depuis gfxcard (harness_stubs.c, h_video_cards[]).
        keyboard_at_.input_port = (uint8_t)(video_is_mda() != 0 ? 0xf0 : 0xb0);
        keyboard_at_.out_new = -1;
        keyboard_at_.out_delayed = -1;
        keyboard_at_.last_irq = 0;

        keyboard_at_.key_wantdata = 0;

        keyboard_scan = 1;
    }

    // pcem: keyboard_at.c:822-825 — le bit de rafraichissement d'un PS/2, qui bascule
    // toutes les 16 us. Un AT n'a pas ce chronometre : keyboard_at_init_ps2 seul
    // l'enregistre.
    private static void at_refresh(object? p)
    {
        keyboard_at_.refresh = keyboard_at_.refresh == 0 ? 1 : 0;
        timer_advance_u64(keyboard_at_.refresh_timer, PS2_REFRESH_TIME);
    }

    // DEVIATION: keyboard_at.c:840 caste `void (*)()` en `void (*)(void *)` pour
    // timer_add. Le cast n'existe pas en C# et keyboard_at_poll doit rester sans
    // parametre pour keyboard_poll : adaptateur nomme, comme keyboard_xt.cs:239.
    private static void keyboard_at_poll_timer(object? p) => keyboard_at_poll();

    // pcem: keyboard_at.c:827-841
    internal static void keyboard_at_init()
    {
        // pcem: keyboard_at.c:829 — memset(&keyboard_at, 0, sizeof(keyboard_at)).
        //
        // UNE INSTANCE NEUVE, ET NON « le CLR l'a deja mise a zero ». C'est ce que
        // j'avais ecrit, en m'appuyant sur « rien ne rappelle keyboard_at_init ici » —
        // un compte d'APPELANTS, donc exactement l'erreur que ce jalon a faite cinq
        // fois deja. Le C remet a zero A CHAQUE init ; un champ statique ne l'est qu'au
        // PREMIER. Or l'oracle passe par ici a chaque h_boot, BootDiff en a cinq sites
        // d'appel — la phase 2 reamorce pour rejouer — et le reset materiel du menu en
        // sera un autre des que B3 cablera l'AT.
        //
        // CE QUE keyboard_at_reset NE RESTAURE PAS, et qui divergerait donc au SECOND
        // amorcage seulement : want60, command, key_command, mem[1..31], out, translate,
        // next_is_release, reset_delay, wantirq12. Un rouge de phase 2 sans rouge de
        // phase 1, le plus penible a diagnostiquer.
        //
        // SUR : resetpchard() appelle timer_reset() AVANT device_init() des deux cotes
        // (pc.cs et harness.c), donc les trois anciens pc_timer_t ont quitte la liste
        // quand timer_add y met les neufs. Et le memset du C NE TOUCHE PAS key_queue,
        // key_ctrl_queue ni mouse_queue : ce sont des statiques HORS de la struct. On
        // ne les remet donc pas a zero non plus.
        keyboard_at_ = new();
        io_sethandler(0x0060, 0x0005, keyboard_at_read, null, null, keyboard_at_write, null, null, null);
        keyboard_at_reset();
        if (materiel.pb_254)
                keyboard_at_init_materiel();
        keyboard_send = keyboard_at_adddata_keyboard;
        keyboard_poll = keyboard_at_poll;
        keyboard_at_.mouse_write = null;
        keyboard_at_.mouse_p = null;
        keyboard_at_.is_ps2 = 0;
        keyboard_set_scancode_set(SCANCODE_SET_2);
        keyboard_at_.scancode_set = SCANCODE_SET_2;

        timer_add(keyboard_at_.send_delay_timer, keyboard_at_poll_timer, null, 1);
    }

    // pcem: keyboard_at.c:843-846 — sans appelant ici : aucune machine du depot n'a de
    // souris PS/2. Transcrite parce que mouse_write est LU par la commande 0xD4, et
    // qu'un `null` non justifie invite a croire la branche morte.
    internal static void keyboard_at_set_mouse(mouse_write_fn? mouse_write, object? p)
    {
        keyboard_at_.mouse_write = mouse_write;
        keyboard_at_.mouse_p = p;
    }

    // pcem: keyboard_at.c:848-851
    internal static void keyboard_at_init_ps2()
    {
        timer_add(keyboard_at_.refresh_timer, at_refresh, null, 1);
        keyboard_at_.is_ps2 = 1;
    }

    // pcem: cpu.h:163
    private static int ISA_CYCLES(int x) => x * isa_cycles;
}
