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
        return 0;
    }
}
