// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/devices/nvr.c  (lignes 18-232 et 784-802)
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — B1 : le MC146818 lui-même. loadnvr (:233-543) et savenvr
//         (:544-783) sont réduits au SEUL cas de l'AT : les 550 lignes qu'ils
//         font sont un `switch` sur des dizaines de machines, et chacune ouvre
//         un fichier de sauvegarde différent. Voir le registre des omissions.
//
// L'HORLOGE TEMPS RÉEL ET LES 128 OCTETS DE CMOS, ET LE POST LES LIT AVANT TOUT.
//
// Le MC146818 tient deux choses derrière la même paire de ports, 0x70 et 0x71 :
// une horloge alimentée par pile, et 64 à 128 octets de mémoire non volatile où
// le BIOS de l'AT range la configuration de la machine. L'une des premières
// choses que fait le POST est d'en vérifier la somme de contrôle — et de
// s'arrêter sur « 162-System Options Not Set » si elle est fausse.
//
// DEUX PORTS, ET LE PREMIER EST UN LOQUET. Écrire en 0x70 choisit le registre ;
// lire ou écrire en 0x71 agit dessus. C'est pourquoi writenvr teste `addr & 1`
// avant toute chose.
//
// ET ÉCRIRE EN 0x70 TOUCHE LE MASQUE DE NMI. Le bit 7 de la valeur écrite n'est
// pas une adresse de registre : c'est l'inhibition des interruptions non
// masquables. Deux fonctions sans rapport sur le même port, parce qu'IBM
// n'avait plus de broche.
//
// LES REGISTRES C ET D SONT EN LECTURE SEULE, et lire C l'EFFACE — c'est ainsi
// qu'on acquitte une interruption d'horloge. Une sonde qui lirait C en passant
// détruirait l'état qu'elle observe.
//
// TROIS CHRONOMÈTRES : l'interruption périodique (taux réglé par le registre A),
// le tic de seconde, et la fin de mise à jour. Le troisième existe parce que le
// vrai composant met 244 + 1984 microsecondes à recopier son horloge interne
// dans ses registres, et que le logiciel peut lire pendant ce temps.

using iXtal26.Models;
using iXtal26.PluginApi;
using static iXtal26.rtc_h;

namespace iXtal26.Devices;

internal static class nvr
{
    // pcem: nvr.c:18-23
    internal static int oldromset;
    internal static int nvrmask = 63;
    internal static readonly uint8_t[] nvrram = new uint8_t[128];
    internal static int nvraddr;

    internal static int nvr_dosave;

    // pcem: nvr.c:25-31
    private sealed class nvr_t
    {
        internal readonly pc_timer_t rtc_timer = new();
        internal readonly pc_timer_t onesec_timer = new();
        internal readonly pc_timer_t update_end_timer = new();

        internal int onesec_cnt;
    }

    // omitted: nvrfopen (nvr.c:33-54) — elle compose un chemin depuis nvr_path
    //   et config_name, deux réglages de l'interface que ce dépôt ne pose pas.
    //   Sans elle, loadnvr ne peut prendre que sa branche « pas de fichier ».

    // pcem: nvr.c:56
    internal static void getnvrtime() { rtc.time_get(nvrram); }

    // pcem: nvr.c:58-68
    private static void nvr_speed_changed(object? p)
    {
        var nvr = (nvr_t)p!;

        if ((nvrram[RTC_REGA] & RTC_RS) == 0)
        {
                timer.timer_disable(nvr.rtc_timer);
                return;
        }
        else
        {
                int c = 1 << ((nvrram[RTC_REGA] & RTC_RS) - 1);
                timer.timer_set_delay_u64(nvr.rtc_timer, (uint64_t)(pit.RTCCONST * (uint64_t)c));
        }
    }

    // pcem: nvr.c:70-90 — l'interruption PÉRIODIQUE.
    //
    // ELLE PART SUR L'IRQ 8, et `picint(0x100)` le dit : le masque est sur
    // SEIZE bits, et le bit 8 est la première ligne du SECOND 8259. C'est pour
    // ça qu'un AT a besoin de pic2_init — sans le second contrôleur, l'horloge
    // n'a personne à qui parler.
    private static void nvr_rtc(object? p)
    {
        var nvr = (nvr_t)p!;

        if ((nvrram[RTC_REGA] & RTC_RS) == 0)
        {
                timer.timer_disable(nvr.rtc_timer);
                return;
        }
        else
        {
                int c = 1 << ((nvrram[RTC_REGA] & RTC_RS) - 1);
                timer.timer_advance_u64(nvr.rtc_timer, (uint64_t)(pit.RTCCONST * (uint64_t)c));
                nvrram[RTC_REGC] |= RTC_PF;
                if ((nvrram[RTC_REGB] & RTC_PIE) != 0)
                {
                        nvrram[RTC_REGC] |= RTC_IRQF;
                        if (Cpu.x86.AMSTRAD != 0)
                                pic.picint(2);
                        else
                                pic.picint(0x100);
                }
        }
    }

    // pcem: nvr.c:92
    internal static int nvr_update_status;

    // pcem: nvr.c:94
    private const uint8_t ALARM_DONTCARE = 0xc0;

    /// <summary>pcem: nvr.c:96-98 — l'alarme, avec son « peu importe ».
    ///
    /// Le champ d'alarme est à l'indice IMPAIR qui suit le champ d'heure :
    /// secondes en 0, alarme-secondes en 1. D'où le `nvraddr + 1`. Et une valeur
    /// dont les deux bits hauts sont à un signifie « n'importe quelle valeur
    /// convient » — c'est ce que 0xc0 encode.</summary>
    internal static int nvr_check_alarm(int nvraddr)
        => (nvrram[nvraddr + 1] == nvrram[nvraddr] ||
            (nvrram[nvraddr + 1] & ALARM_DONTCARE) == ALARM_DONTCARE) ? 1 : 0;

    // pcem: nvr.c:100-131 — la FIN de la mise à jour.
    //
    // LE COMMENTAIRE DE PCem EST REPRIS : le drapeau et l'interruption doivent
    // être levés à la fin de la mise à jour, pas à son début. Un logiciel qui
    // lirait l'heure entre les deux verrait des champs incohérents.
    private static void nvr_update_end(object? p)
    {
        if ((nvrram[RTC_REGB] & RTC_SET) == 0)
        {
                getnvrtime();
                /* Clear update status. */
                nvr_update_status = 0;

                if (nvr_check_alarm(RTC_SECONDS) != 0 && nvr_check_alarm(RTC_MINUTES) != 0 &&
                    nvr_check_alarm(RTC_HOURS) != 0)
                {
                        nvrram[RTC_REGC] |= RTC_AF;
                        if ((nvrram[RTC_REGB] & RTC_AIE) != 0)
                        {
                                nvrram[RTC_REGC] |= RTC_IRQF;
                                if (Cpu.x86.AMSTRAD != 0)
                                        pic.picint(2);
                                else
                                        pic.picint(0x100);
                        }
                }

                /* The flag and interrupt should be issued on update ended, not started. */
                nvrram[RTC_REGC] |= RTC_UF;
                if ((nvrram[RTC_REGB] & RTC_UIE) != 0)
                {
                        nvrram[RTC_REGC] |= RTC_IRQF;
                        if (Cpu.x86.AMSTRAD != 0)
                                pic.picint(2);
                        else
                                pic.picint(0x100);
                }
        }
    }

    // pcem: nvr.c:133-147 — le tic de SECONDE, qui n'en est pas un.
    //
    // Le chronomètre se réarme toutes les DIX MILLISECONDES et compte jusqu'à
    // cent : c'est cette division qui donne la seconde. Le vrai composant fait
    // autrement ; PCem a besoin d'un grain fin pour que RTC_UIP soit observable.
    private static void nvr_onesec(object? p)
    {
        var nvr = (nvr_t)p!;

        nvr.onesec_cnt++;
        if (nvr.onesec_cnt >= 100)
        {
                if ((nvrram[RTC_REGB] & RTC_SET) == 0)
                {
                        nvr_update_status = RTC_UIP;
                        rtc.rtc_tick();

                        timer.timer_set_delay_u64(nvr.update_end_timer,
                                                  (uint64_t)((244.0 + 1984.0) * timer.TIMER_USEC));
                }
                nvr.onesec_cnt = 0;
        }
        timer.timer_advance_u64(nvr.onesec_timer, (uint64_t)(10000 * timer.TIMER_USEC));
    }

    // pcem: nvr.c:149-205
    private static void writenvr(uint16_t addr, uint8_t val, object? p)
    {
        var nvr = (nvr_t)p!;
        int c, old;

        Cpu.x86.cycles -= ISA_CYCLES(8);
        if ((addr & 1) != 0)
        {
                if (nvraddr == RTC_REGC || nvraddr == RTC_REGD)
                        return; /* Registers C and D are read-only. There's no reason to continue. */
                if (nvraddr > RTC_REGD && nvrram[nvraddr] != val)
                        nvr_dosave = 1;

                old = nvrram[nvraddr];
                nvrram[nvraddr] = val;

                if (nvraddr == RTC_REGA)
                {
                        if ((val & RTC_RS) != 0)
                        {
                                c = 1 << ((val & RTC_RS) - 1);
                                timer.timer_set_delay_u64(nvr.rtc_timer, (uint64_t)(pit.RTCCONST * (uint64_t)c));
                        }
                        else
                                timer.timer_disable(nvr.rtc_timer);
                }
                else
                {
                        if (nvraddr == RTC_REGB)
                        {
                                if (((old ^ val) & RTC_SET) != 0 && (val & RTC_SET) != 0)
                                {
                                        nvrram[RTC_REGA] &= unchecked((uint8_t)~RTC_UIP); /* This has to be done according to the datasheet. */
                                        nvrram[RTC_REGB] &= unchecked((uint8_t)~RTC_UIE); /* This also has to happen per the specification. */
                                }
                        }

                        // LES INDICES 1, 3 ET 5 SONT LES CHAMPS D'ALARME, et les
                        // ecarter ici est ce qui empeche d'ecrire l'alarme dans
                        // l'horloge. time_update n'a pas de cas pour eux.
                        if ((nvraddr < RTC_REGA) || (nvraddr == RTC_CENTURY))
                        {
                                if ((nvraddr != 1) && (nvraddr != 3) && (nvraddr != 5))
                                {
                                        if ((old != val) && rtc.enable_sync == 0)
                                        {
                                                rtc.time_update(nvrram, nvraddr);
                                                nvr_dosave = 1;
                                        }
                                }
                        }
                }
        }
        else
        {
                nvraddr = val & nvrmask;
                /*PS/2 BIOSes will disable NMIs and expect the watchdog timer to still be able
                  to fire them. I suspect the watchdog is exempt from NMI masking. Currently NMIs
                  are always enabled for PS/2 machines - this would mean that other peripherals
                  could fire NMIs regardless of the mask state, but as there aren't any emulated
                  MCA peripherals that do this it's currently a moot point.*/

                /* Also don't update the NMI mask on Amstrad PCs - actually
                 * ought not to do it for any XT because their NMI mask
                 * register is at 0xA0. But particularly important on the
                 * PC200 and PPC because their video subsystem issues NMIs */
                // C'EST CETTE LIGNE QUI A FAIT PLANTER L'ORACLE : elle dereference
                // models[model], nul dans le harnais jusqu'a B2. Voir harness_stubs.c.
                if ((model_c.models[model_c.model].flags & (model_c.MODEL_MCA | model_c.MODEL_AMSTRAD)) == 0)
                {
                        Cpu._808x.nmi_mask = ~val & 0x80;
                }
        }
    }

    // pcem: nvr.c:207-231
    internal static uint8_t readnvr(uint16_t addr, object? p)
    {
        uint8_t temp;

        Cpu.x86.cycles -= ISA_CYCLES(8);
        if ((addr & 1) != 0)
        {
                if (nvraddr == RTC_REGA)
                        return (uint8_t)((nvrram[RTC_REGA] & 0x7F) | nvr_update_status);
                if (nvraddr == RTC_REGD)
                        nvrram[RTC_REGD] |= RTC_VRT;
                if (nvraddr == RTC_REGC)
                {
                        if (Cpu.x86.AMSTRAD != 0)
                                pic.picintc(2);
                        else
                                pic.picintc(0x100);
                        temp = nvrram[RTC_REGC];
                        nvrram[RTC_REGC] = 0;
                        return temp;
                }
                return nvrram[nvraddr];
        }
        return (uint8_t)nvraddr;
    }

    /// <summary>pcem: nvr.c:233-543 — loadnvr, RÉDUITE À SA BRANCHE SANS FICHIER.
    ///
    /// Les 310 lignes du C sont un `switch (romset)` sur une trentaine de
    /// machines, chacune ouvrant un fichier de sauvegarde à son nom. Ce dépôt
    /// n'en expose aucun — nvrfopen est omise — donc toutes les branches mènent
    /// au même endroit : celui qu'on garde. Sans fichier, le CMOS part à 0xFF,
    /// daté du 1er janvier 1980, le seul bit 24 heures posé.
    ///
    /// ET L'ORACLE N'APPELLE PAS CETTE FONCTION DU TOUT. C'est une MESURE, pas
    /// une lecture : `at-probe` vide nvrram[128] depuis la .so après h_boot et
    /// n'y trouve NI 0xFF NI la date de 1980 — les octets 0x10 à 0x7F sont à
    /// zéro, et 0x00 à 0x0F portent ce que le POST et l'horloge ont écrit
    /// par-dessus le .bss. Le resetpchard réduit du harnais omet loadnvr, que
    /// pc.c appelle pourtant en :289 et :397.
    ///
    /// CE PIÈGE EST POUR B3, et il est facile à ne pas voir : le POST de l'AT
    /// s'arrête sur « 162-System Options Not Set » dans les DEUX états, un CMOS
    /// à zéro ayant une somme de contrôle aussi fausse qu'un CMOS à 0xFF.
    /// L'écran ne départage rien. Un `boot-diff --model ibmat` qui appellerait
    /// loadnvr côté C# seul divergerait — sur la DATE, donc des centaines de
    /// milliers d'instructions après la cause.</summary>
    internal static void loadnvr()
    {
        nvrmask = 63;
        oldromset = pc.romset;

        // omitted: le switch sur romset et fread(nvrram, 128, 1, f) — pas de
        //   fichier de sauvegarde dans ce depot. On prend toujours la branche
        //   `if (!f)`, ci-dessous, verbatim.
        for (var i = 0; i < 128; i++)
                nvrram[i] = 0xFF;
        if (rtc.enable_sync == 0)
        {
                nvrram[RTC_SECONDS] = nvrram[RTC_MINUTES] = nvrram[RTC_HOURS] = 0;
                nvrram[RTC_DOM] = nvrram[RTC_MONTH] = 1;
                nvrram[RTC_YEAR] = BCD(80);
                nvrram[RTC_CENTURY] = BCD(19);
                nvrram[RTC_REGB] = RTC_2412;
        }
    }

    // omitted: savenvr (nvr.c:544-783) — 240 lignes du meme switch, en ecriture.
    //   Sans nvrfopen elle n'a nulle part ou ecrire. nvr_dosave reste donc pose
    //   sans lecteur, et c'est FIDELE : son seul lecteur chez PCem est la boucle
    //   de trames de l'interface, qui sauve toutes les 200 images
    //   (wx-sdl2.c:181, qt-sdl2.c:184) — deja omise avec src/wx-ui/ et src/qt-ui/.

    // pcem: nvr.c:784-794
    private static object? nvr_init()
    {
        var nvr = new nvr_t();

        io.io_sethandler(0x0070, 0x0002, readnvr, null, null, writenvr, null, null, nvr);
        timer.timer_add(nvr.rtc_timer, nvr_rtc, nvr, 1);
        timer.timer_add(nvr.onesec_timer, nvr_onesec, nvr, 1);
        timer.timer_add(nvr.update_end_timer, nvr_update_end, nvr, 0);

        return nvr;
    }

    // pcem: nvr.c:796-800 — nvr_close ne fait que free(), sans objet en C#.
    private static void nvr_close(object? p) { }

    // pcem: nvr.c:802
    internal static readonly device_t nvr_device =
        new("Motorola MC146818 RTC", 0, nvr_init, nvr_close, null, nvr_speed_changed, null, null, null);

    // pcem: cpu.h:163
    private static int ISA_CYCLES(int x) => x * Cpu.cpu.isa_cycles;
}
