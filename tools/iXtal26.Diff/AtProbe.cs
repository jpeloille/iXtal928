// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de diff.
//
// B2 — L'ORACLE AMORCE-T-IL UN AT ?
//
// La sonde ne compare rien : elle fait tourner le POST de l'IBM AT 5170 dans
// l'ORACLE SEUL et rapporte où il va. C'est délibéré, et c'est l'ordre du plan
// (PLAN-286.md, bloc B2) : tant que l'oracle ne sait pas amorcer un AT, la
// machine AT et le mode protégé s'écriraient côté C# sans rien à quoi les
// comparer.
//
// CE QU'ELLE MESURE :
//   - combien d'instructions le POST exécute avant de s'arrêter ou de boucler ;
//   - où il en est (CS:IP) à intervalles réguliers ;
//   - S'IL ENTRE EN MODE PROTEGE, et au bout de combien d'instructions — le
//     chiffre que PLAN-286.md dit explicitement ne pas connaître, et dont il
//     dit qu'il pourrait faire remonter le bloc C dans l'ordre.

using iXtal26.Diag;

namespace iXtal26.Diff;

public static class AtProbe
{
    private const int ROM_IBMAT = 25;

    public static int Run(string romsPath)
    {
        Oracle.CheckAbi();

        Console.WriteLine("Sonde d'amorçage AT — ORACLE SEUL, aucune comparaison.\n");

        Oracle.h_set_core(Oracle.Core286);
        Oracle.h_set_romset(ROM_IBMAT);
        Oracle.h_set_mem_size(512);

        if (Oracle.h_boot(romsPath) == 0)
        {
            Console.WriteLine("ROUGE : h_boot a échoué — le BIOS de l'AT n'a pas pu être chargé.");
            Console.WriteLine("        Attendu : roms/ibmat/62x0820.u27 et 62x0821.u47");
            return 1;
        }
        Console.WriteLine("h_boot : le BIOS de l'AT est chargé, la machine est montée.\n");

        var s = HState.Create();
        Oracle.h_getstate(out s);
        Console.WriteLine($"  au reset        CS:IP {s.seg_sel[0]:X4}:{s.pc:X4}   msw {s.cr0 & 0xFFFF:X4}");

        // Le POST tourne par tranches. On s'arrête a la PREMIERE des deux
        // conditions : le bit 0 du mot d'état machine passe a un (entrée en mode
        // protégé), ou le budget est épuisé.
        const int tranche = 200_000;
        const int tranches = 2000;
        ulong total = 0;
        var pmodeA = -1L;

        for (var t = 0; t < tranches; t++)
        {
            var n = Oracle.h_run(tranche);
            total += (ulong)n;
            Oracle.h_getstate(out s);

            if (pmodeA < 0 && (s.cr0 & 1) != 0)
            {
                pmodeA = (long)total;
                Console.WriteLine($"\n  *** MODE PROTEGE *** apres ~{pmodeA:N0} instructions");
                Console.WriteLine($"      CS:IP {s.seg_sel[0]:X4}:{s.pc:X4}  msw {s.cr0 & 0xFFFF:X4}");
                Console.WriteLine($"      gdt base {s.sys_base[0]:X8} limit {s.sys_limit[0]:X4}");
                Console.WriteLine($"      idt base {s.sys_base[2]:X8} limit {s.sys_limit[2]:X4}");
                Console.WriteLine("      — on CONTINUE : ce qui suit dit jusqu'ou le POST va.\n");
            }

            if (t % 500 == 0)
                Console.WriteLine($"  {total,12:N0} instr   CS:IP {s.seg_sel[0]:X4}:{s.pc:X4}   " +
                                  $"AX {s.regs[0]:X4}  msw {s.cr0 & 0xFFFF:X4}   " +
                                  $"timer {s.n_timer_process,10:N0}  inb {s.n_inb,9:N0}  " +
                                  $"pic {s.n_picinterrupt,6:N0}");
        }

        Console.WriteLine();
        Oracle.h_getstate(out s);
        Console.WriteLine($"  arret           CS:IP {s.seg_sel[0]:X4}:{s.pc:X4}   " +
                          $"{total:N0} instructions");

        if (pmodeA < 0)
            Console.WriteLine("\n  Le POST n'est PAS entre en mode protege dans ce budget.");

        // L'ECRAN, ET C'EST LA PREUVE QUI MANQUE AU RESTE. Les compteurs disent
        // que le coeur tourne ; seul le contenu de la memoire video dit ce que
        // le POST a VOULU dire. La carte montee est une CGA (h_boot fait
        // device_add(&cga_device)), donc le texte est en B8000, un octet de
        // caractere suivi d'un octet d'attribut.
        //
        // h_read_phys ET NON h_read : le second indexe ram[] et 0xB8000 sort
        // d'une machine a 512 Ko — il faisait SEGFAULT. C'est cette sonde qui
        // l'a trouve.
        Console.WriteLine("\n  Ecran (B8000, 80x25, lignes non vides) :");
        var vram = new byte[80 * 25 * 2];
        Oracle.h_read_phys(0xB8000, vram, (uint)vram.Length);
        for (var ligne = 0; ligne < 25; ligne++)
        {
            var txt = new char[80];
            var vide = true;
            for (var col = 0; col < 80; col++)
            {
                var ch = vram[(ligne * 80 + col) * 2];
                txt[col] = ch >= 32 && ch < 127 ? (char)ch : ' ';
                if (ch != 0 && ch != 32) vide = false;
            }
            if (!vide)
                Console.WriteLine($"   {ligne,2} | {new string(txt).TrimEnd()}");
        }

        // LE CMOS, LU DANS LA .so — et il ne dit PAS ce que je croyais.
        //
        // J'avais ecrit que l'oracle prenait la branche « pas de fichier » de
        // loadnvr. L'ecran 162 ne le prouvait pas : un CMOS a ZERO donne le meme
        // ecran, sa somme de controle etant fausse elle aussi. Cette sonde
        // separe les deux, et elle est le debut du comparateur de B3.
        //
        // La branche sans fichier (nvr.c:524-534) laisse 0xFF PARTOUT sauf
        // [0]=[2]=[4]=0, [7]=[8]=1, [9]=0x80, [0x0B]=0x02, [0x32]=0x19.
        var p_nvr = Oracle.Symbole("nvrram");
        var cmos = new byte[128];
        System.Runtime.InteropServices.Marshal.Copy(p_nvr, cmos, 0, 128);
        Console.WriteLine("\n  CMOS (nvrram[128]) apres h_boot :");
        for (var l = 0; l < 8; l++)
                Console.WriteLine($"   {l * 16:X2} | " +
                        string.Join(" ", cmos.Skip(l * 16).Take(16).Select(b => b.ToString("X2"))));
        var tousZero = cmos.All(b => b == 0);
        var motifSansFichier = cmos[0] == 0 && cmos[2] == 0 && cmos[4] == 0 &&
                               cmos[7] == 1 && cmos[8] == 1 && cmos[9] == 0x80 &&
                               cmos[0x32] == 0x19;
        Console.WriteLine(tousZero
                ? "   -> TOUT A ZERO : le harnais n'appelle PAS loadnvr. Son resetpchard\n" +
                  "      reduit l'omet, et le CMOS reste l'etat statique du .bss. C'est\n" +
                  "      CE qu'un futur boot-diff --model ibmat devra reproduire, PAS\n" +
                  "      la branche sans fichier de loadnvr."
                : motifSansFichier
                        ? "   -> motif de la branche SANS FICHIER de loadnvr (nvr.c:524-534)."
                        : "   -> ni zero ni le motif sans fichier : le POST a deja ecrit dedans.");

        // models[ROM_IBMAT]->cpu[0].cpus — ET IL EST NUL.
        //
        // h_model_ibmat ne pose que name, id, internal_name, flags et les trois
        // tailles de RAM. Le membre cpu[5] porte un POINTEUR, CPU *cpus, laisse a
        // zero. Or cpu_set_edx() (cpu.c) fait
        //   EDX = models[model]->cpu[cpu_manufacturer].cpus[cpu].edx_reset;
        // donc il DEREFERENCE NULL. Offsets mesures par le compilateur, pas
        // supposes : sizeof(MODEL) = 208, offsetof(cpu[0].cpus) = 104.
        //
        // ET RIEN NE PLANTE, parce que --wrap intercepte les DEUX appelants :
        // __wrap_cpu_set_edx() et __wrap_cpu_update_waitstates() sont VIDES
        // (harness_stubs.c:268 et :278). Le pointeur nul est donc reel et sans
        // consequence — mais il fixe le comportement que le C# doit avoir.
        //
        // CE QUE CA DECIDE POUR B1b ET B3 : la commande 0xFE du 8042 (« pulse
        // output port », la SEULE sortie du mode protege d'un 286) fait
        // softresetx86() puis cpu_set_edx(). Sur l'oracle, cpu_set_edx ne fait
        // RIEN : EDX garde sa valeur d'avant le reset. Un C# qui calculerait
        // edx_reset depuis cpus_286[] divergerait. Peupler cpu[0].cpus et
        // deleguer a __real_ est un choix a faire EXPLICITEMENT, pas par defaut.
        var p_models = Oracle.Symbole("models");
        var p_ibmat = System.Runtime.InteropServices.Marshal.ReadIntPtr(p_models, 25 * 8);
        Console.WriteLine($"\n  models[ROM_IBMAT] = 0x{p_ibmat.ToInt64():X}");
        if (p_ibmat != IntPtr.Zero)
        {
                var p_cpus = System.Runtime.InteropServices.Marshal.ReadIntPtr(p_ibmat, 104);
                Console.WriteLine($"  ->cpu[0].cpus      = 0x{p_cpus.ToInt64():X}" +
                        (p_cpus == IntPtr.Zero
                                ? "   NUL — mais --wrap vide ses deux appelants."
                                : "   peuple."));
        }

        // isa_cycles, pendant de la regle 3 : lier cpu.c fournit le SYMBOLE,
        // cpu_set() pose la VALEUR, et h_cpu_config_286 est ce qui en tient lieu.
        // readnvr et writenvr facturent tous deux ISA_CYCLES(8) : une valeur de
        // 8088 restee la ferait diverger chaque acces au CMOS.
        Console.WriteLine($"\n  isa_cycles = {System.Runtime.InteropServices.Marshal.ReadInt32(Oracle.Symbole("isa_cycles"))}" +
                          "   (0 = jamais pose : ISA_CYCLES(8) rendrait 0)");

        Console.WriteLine("\nVert : la sonde a tourne. Ce qu'elle rapporte est une MESURE,");
        Console.WriteLine("       pas une comparaison — le cote C# n'a pas encore de machine AT.");
        return 0;
    }
}
