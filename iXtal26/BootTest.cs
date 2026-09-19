// essai jetable
using iXtal26;
using iXtal26.Cpu;
using iXtal26.Memory;
namespace iXtal26;
internal static class BootTest
{
    internal static int Run(string roms, int slices)
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
        return 0;
    }

    /// <summary>Le framebuffer, lui, est rempli par cga_poll — un chemin
    /// entièrement distinct du vidage texte ci-dessus, qui lit la VRAM à travers la
    /// carte mémoire. Des caractères justes en VRAM ne prouvent RIEN sur le rendu :
    /// si Buffer32 est vide, c'est que le chronomètre du CGA n'appelle pas cga_poll,
    /// et c'est une question d'enregistrement de timer, pas de rendu.</summary>
    private static void ReportFramebuffer()
    {
        var b = Video.video.Buffer32;
        if (b.Length == 0)
        {
            Console.WriteLine("\nframebuffer : NON ALLOUÉ (initvideo n'a pas tourné)");
            return;
        }

        var nz = 0;
        var lastLine = -1;
        for (var y = 0; y < Video.video.Height; y++)
        for (var x = 0; x < Video.video.Stride; x++)
            if (b[y * Video.video.Stride + x] != 0) { nz++; lastLine = y; }

        Console.WriteLine($"\nframebuffer : {b.Length} pixels, {nz} non nuls, " +
                          $"dernière ligne touchée {lastLine}");
        Console.WriteLine($"  xsize={Video.video.xsize} ysize={Video.video.ysize} " +
                          $"res={Video.video.video_res_x}x{Video.video.video_res_y} " +
                          $"frames={Video.video.frames}");
    }

    /// <summary>Le tampon texte CGA, 80x25, un mot par cellule (caractère, attribut).
    /// C'est la seule preuve directe que le POST est allé au bout : la BDA dit ce que
    /// le BIOS a mesuré, l'écran dit ce qu'il a décidé d'en faire.</summary>
    private static void DumpTextScreen()
    {
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
}
