// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de vérification.
// STATUS: host
//
// Amorçage sur console : ce que la BDA contient, ce que le CGA a écrit en VRAM,
// ce que cga_poll a dessiné dans le framebuffer, et — avec --type — ce que la
// machine fait d'une frappe.

using iXtal26;
using iXtal26.Cpu;
using iXtal26.Host;
using iXtal26.Keyboard;
using iXtal26.Memory;
using SDL3;
namespace iXtal26;
public static class BootTest
{
    /// <param name="settle">Tranches laissées à l'application après chaque Entrée.
    /// Le défaut de KeyScript suffit à un DIR ; un FORMAT d'une disquette 360 Ko en
    /// demande ~4 000, soit 40 s émulées. Mesuré : à 200 tranches, FORMAT n'écrit que
    /// 18 432 des 368 640 octets et l'écran reste sur « Formatting... ».</param>
    internal static int Run(string roms, int slices, List<string>? types = null,
                            int settle = KeyScript.SlicesAfterLine)
    {
        if (!pc.initpc(roms)) return 1;
        Console.WriteLine($"initpc OK — reset CS:IP = {x86.CS:X4}:{_386_common.cpu_state.pc:X4}");
        Console.WriteLine($"octets au vecteur de reset : " +
            string.Join(" ", Enumerable.Range(0,5).Select(i => mem.readmembl((uint)(0xFFFF0+i)).ToString("X2"))));
        for (var i = 0; i < slices; i++)
        {
            pc.runpc();
            if (i % Math.Max(1, slices / 10) == 0)
                Console.WriteLine($"  tranche {i,5} : CS:IP {x86.CS:X4}:{_386_common.cpu_state.pc:X4}  ins {_808x.ins,9}  BDA[410]={mem.readmemwl(0x410):X4} [413]={mem.readmemwl(0x413)}");
        }
        Console.WriteLine($"apres {slices} tranches : CS:IP = {x86.CS:X4}:{_386_common.cpu_state.pc:X4}, " +
                          $"ins = {_808x.ins}, tsc = {timer.tsc}");
        Console.WriteLine("BDA equipement 0040:0010 = " +
            $"{mem.readmemwl(0x410):X4}, taille memoire 0040:0013 = {mem.readmemwl(0x413)} Ko");

        DumpTextScreen();
        ReportFramebuffer();

        if (types is not null)
            foreach (var type in types)
                TypeAndDump(type, settle);

        // pc.c:584-585, par closepc(). Ajouté à M13 : --boot ÉCRIT sur les images — le
        // README donne « --settle 4500 --type "FORMAT B:" » comme recette — et cette
        // fonction rendait sans jamais fermer, laissant le vidage des tampons au hasard
        // de la sortie de processus. Mesuré sur le disque dur : un FDISK seul écrit
        // 512 octets, qui restent dans le tampon, et l'image sur l'hôte reste à zéro.
        // BootDiff, lui, appelait déjà closepc (BootDiff.cs) — c'est ce chemin-ci qui
        // manquait.
        // pcem: wx-sdl2.c:623 — savenvr() AVANT closepc(), et c'est l'interface qui
        // l'appelle, pas pc.c. Le mettre dans closepc serait deplacer un geste
        // d'interface dans le coeur ; il est donc ici et dans SdlHost, les deux chemins
        // de sortie de ce depot.
        //
        // SANS CET APPEL LE CMOS NE SURVIT PAS, et le SETUP d'un BIOS AMI ne sert alors
        // a rien : la machine reecrirait sa configuration a chaque demarrage et
        // retrouverait « CMOS system options not set ».
        Devices.nvr.savenvr();
        pc.closepc();

        return 0;
    }

    /// <summary>
    /// Tape une chaîne dans la machine, puis vide l'écran.
    ///
    /// L'injection se fait dans keyboard.rawinputkey[] — EXACTEMENT le tableau que
    /// Host/SdlKeyboard remplit — et passe par la même Host.SdlKeyboard.MapScancode.
    /// Seule la livraison d'évènements par SDL est court-circuitée, parce qu'elle
    /// exige une fenêtre focalisée et qu'un gestionnaire de fenêtres n'est pas un
    /// oracle. Tout ce qui suit est exercé pour de vrai : keyboard_poll_host,
    /// keyboard_process, les tables de scancodes, keyboard_xt, l'IRQ 1, l'INT 9 du
    /// BIOS, le tampon clavier de la BDA et l'écho de l'application.
    ///
    /// Une touche doit rester enfoncée assez longtemps pour que keyboard_process la
    /// voie : il compare pcem_key[] à oldkey[] une fois par runpc(), donc au moins
    /// une tranche appuyée et une relâchée.
    /// </summary>
    private static void TypeAndDump(string text, int settle)
    {
        // M20 — deux commandes de script, pour dérouler une installation à plusieurs
        // disquettes sans fenêtre : « @A:chemin » (ou @B:) change la disquette comme le
        // menu Ctrl+F12 — disc_close puis disc_load, SdlMenu.Insert —, « @wait N » laisse
        // passer N tranches sans rien taper. Ni l'une ni l'autre ne frappe de touche.
        if (text.Length > 3 && text[0] == '@' && text[2] == ':' && (text[1] is 'A' or 'B'))
        {
            var drive = text[1] - 'A';
            Disc.disc.disc_close(drive);
            Disc.disc.disc_load(drive, text[3..]);
            Console.WriteLine($"\n--- {text[1]}: {text[3..]} inséré" +
                              (Disc.disc.drive_empty[drive] != 0 ? " — REFUSÉ par disc_load ---" : " ---"));
            return;
        }
        if (text.StartsWith("@wait ", StringComparison.Ordinal))
        {
            var n = int.Parse(text[6..]);
            for (var i = 0; i < n; i++)
                pc.runpc();
            Console.WriteLine($"\n--- {n} tranches d'attente ---");
            DumpTextScreen();
            return;
        }

        Console.WriteLine($"\n--- frappe de « {text} » puis Entrée ---");

        foreach (var ch in text)
            PressKey(ch);

        PressKey('\n');

        // Laisser l'application traiter la ligne. La cadence vit dans KeyScript,
        // partagée avec boot-diff : deux frappes différentes ne se comparent pas.
        for (var i = 0; i < settle; i++)
            pc.runpc();

        DumpTextScreen();
    }

    private static void PressKey(char ch)
    {
        var idx = KeyScript.IndexFor(ch, out var shift);
        var shiftIdx = KeyScript.ShiftIndex();

        if (idx < 0)
        {
            Console.Error.WriteLine($"caractère non mappé : « {ch} »");
            return;
        }

        // Maj AVANT la touche, et dans une passe SÉPARÉE. keyboard_process balaie
        // pcem_key[] de l'indice 0 à 271 et émet dans cet ordre : appuyer Maj (0x2A)
        // et « 8 » (0x09) dans la même passe enverrait le scancode de « 8 » AVANT
        // celui de Maj, et l'INT 9 du BIOS lirait un « 8 » au lieu d'une « * ».
        // Un humain n'a jamais ce problème — il appuie sur Maj un scrutin plus tôt.
        // Mesuré : sans cette séparation, « PRINT 6*7 » arrive en « print 687 ».
        if (shift && shiftIdx >= 0)
        {
            keyboard.rawinputkey[shiftIdx] = 1;
            Step();
        }

        keyboard.rawinputkey[idx] = 1;
        Step();

        keyboard.rawinputkey[idx] = 0;
        Step();

        if (shift && shiftIdx >= 0)
        {
            keyboard.rawinputkey[shiftIdx] = 0;
            Step();
        }

        static void Step()
        {
            for (var i = 0; i < KeyScript.SlicesPerStep; i++)
                pc.runpc();
        }
    }

    /// <summary>Le framebuffer, lui, est rempli par cga_poll — un chemin
    /// entièrement distinct du vidage texte ci-dessus, qui lit la VRAM à travers la
    /// carte mémoire. Des caractères justes en VRAM ne prouvent RIEN sur le rendu :
    /// si Buffer32 est vide, c'est que le chronomètre du CGA n'appelle pas cga_poll,
    /// et c'est une question d'enregistrement de timer, pas de rendu.</summary>
    private static void ReportFramebuffer()
    {
        // Le test « Buffer32.Length == 0 » qui tenait ici est mort à M5.2 : le tableau
        // est désormais alloué à la déclaration, donc jamais vide, et la branche ne
        // pouvait plus se déclencher. Ce qu'elle cherchait à dire — « cga_poll n'a
        // rien dessiné » — est repris plus bas sur le compte de pixels, qui lui reste
        // un témoin vivant. Un diagnostic qu'on rend inatteignable sans le remplacer,
        // c'est le défaut de M4.3 reproduit.
        var b = Video.video.Buffer32;

        var nz = 0;
        var lastLine = -1;
        for (var y = 0; y < Video.video.Height; y++)
        for (var x = 0; x < Video.video.Stride; x++)
            if (b[y * Video.video.Stride + x] != 0) { nz++; lastLine = y; }

        // Empreinte du contenu, pas seulement du compte : deux polices différentes
        // peuvent allumer le même nombre de pixels. FNV-1a 64 bits sur les mots.
        var fnv = 14695981039346656037UL;
        foreach (var px in b)
        {
            fnv ^= px;
            fnv *= 1099511628211UL;
        }

        Console.WriteLine($"\nframebuffer : {b.Length} pixels, {nz} non nuls, " +
                          $"dernière ligne touchée {lastLine}, empreinte FNV-1a {fnv:X16}");
        if (nz == 0)
            Console.WriteLine("  *** AUCUN pixel tracé : cga_poll n'a pas dessiné. " +
                              "Chercher du côté de l'enregistrement du chronomètre CGA, pas du rendu. ***");
        // La police est le seul maillon que ni le diff d'amorçage ni le fuzzer ne
        // voient : elle ne touche aucun état CPU. Un fontdat vide donne un écran noir
        // parfaitement silencieux — la VRAM reste juste, seul le tracé disparaît.
        var fontNz = 0;
        for (var c = 0; c < 2048; c++)
        for (var d = 0; d < 8; d++)
            if (Video.video.fontdat[(c << 3) | d] != 0) fontNz++;

        // La VGA ne lit PAS fontdat : sa police est dans le plan 2 de sa VRAM, que son
        // BIOS y charge. Le compte ne veut alors rien dire, et l'avertissement mentirait.
        if (Video.vid_svga.svga_get_pri() is null)
            Console.WriteLine($"  police : {fontNz} octets non nuls sur 16384 dans fontdat");
        if (fontNz == 0 && Video.vid_svga.svga_get_pri() is null)
            Console.WriteLine("  *** fontdat est VIDE : aucun caractère ne sera tracé. " +
                              "Vérifier que roms/mda.rom est présent et lisible. ***");

        Console.WriteLine($"  xsize={Video.video.xsize} ysize={Video.video.ysize} " +
                          $"res={Video.video.video_res_x}x{Video.video.video_res_y} " +
                          $"frames={Video.video.frames}");
    }

    /// <summary>Le tampon texte CGA, 80x25, un mot par cellule (caractère, attribut).
    /// C'est la seule preuve directe que le POST est allé au bout : la BDA dit ce que
    /// le BIOS a mesuré, l'écran dit ce qu'il a décidé d'en faire.</summary>
    /// <summary>Visible de boot-diff depuis M12 (internal depuis M15 : il prend un
    /// svga_t, interne, et InternalsVisibleTo couvre iXtal26.Diff) : boot-diff en a besoin. Un diff vert dont
    /// l'image de disque est restée INCHANGÉE ne dit pas POURQUOI la frappe n'a rien
    /// produit — et sans l'écran, on cherche à l'aveugle.</summary>
    /// <param name="svga">La carte VGA à lire, quand l'appelant l'a retenue avant que
    /// closepc ne la ferme ; sinon celle qui est montée, s'il y en a une.</param>
    internal static void DumpTextScreen(Video.svga_t? svga = null)
    {
        svga ??= Video.vid_svga.svga_get_pri();
        if (svga is not null)
        {
            DumpVgaTextScreen(svga);
            return;
        }

        Console.WriteLine("\n--- écran texte CGA (B800:0000, 80x25) ---");
        var blank = 0;
        for (var y = 0; y < 25; y++)
        {
            var line = new char[80];
            for (var x = 0; x < 80; x++)
            {
                var c = mem.mem_readb_phys((uint32_t)(0xB8000 + (y * 80 + x) * 2));
                line[x] = c is >= 0x20 and < 0x7F ? (char)c : ' ';
            }

            var text = new string(line).TrimEnd();
            if (text.Length == 0) { blank++; continue; }
            if (blank > 0) { Console.WriteLine($"  [{blank} ligne(s) vide(s)]"); blank = 0; }
            Console.WriteLine($"  |{text}");
        }

        if (blank > 0)
            Console.WriteLine($"  [{blank} ligne(s) vide(s)]");
    }

    /// <summary>L'écran texte d'une VGA, lu dans sa VRAM BRUTE et non par
    /// mem_readb_phys : celui-ci passerait par svga_read, qui met à jour les verrous
    /// la..ld et facture des cycles — lire l'écran changerait la machine, et boot-diff
    /// compare l'état de la carte APRÈS cet affichage.
    ///
    /// En mode texte la VGA range le caractère au plan 0 et l'attribut au plan 1, à
    /// l'adresse CPU décalée de deux (branche chain2_write de svga_write) : la cellule
    /// d'offset CPU o est en vram[(o &amp; ~1) &lt;&lt; 2]. Largeur et page viennent de la
    /// zone de données du BIOS, lue dans mem.ram — 0040:004A colonnes, 0040:004E
    /// décalage de la page, 0040:0084 lignes moins une.</summary>
    private static void DumpVgaTextScreen(Video.svga_t svga)
    {
        var cols = mem.ram[0x44A] | (mem.ram[0x44B] << 8);
        var page = mem.ram[0x44E] | (mem.ram[0x44F] << 8);
        var rows = mem.ram[0x484] + 1;
        if (cols is not (40 or 80))
            cols = 80;
        if (rows is < 25 or > 50)
            rows = 25;

        Console.WriteLine($"\n--- écran texte VGA (VRAM plans 0/1, {cols}x{rows}, page +0x{page:X}) ---");
        var blank = 0;
        for (var y = 0; y < rows; y++)
        {
            var line = new char[cols];
            for (var x = 0; x < cols; x++)
            {
                var o = (page + 2 * (y * cols + x)) & 0x7fff;
                var c = svga.vram[(o & ~1) << 2];
                line[x] = c is >= 0x20 and < 0x7F ? (char)c : ' ';
            }

            var text = new string(line).TrimEnd();
            if (text.Length == 0) { blank++; continue; }
            if (blank > 0) { Console.WriteLine($"  [{blank} ligne(s) vide(s)]"); blank = 0; }
            Console.WriteLine($"  |{text}");
        }

        if (blank > 0)
            Console.WriteLine($"  [{blank} ligne(s) vide(s)]");
    }
}
