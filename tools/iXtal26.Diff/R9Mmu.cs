// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// r9-mmu — la survie à PB-79, en C# SEUL (R9, TRANSCRIPTION.md).
//
// PCem tombe (segfault) quand la pagination lit une table hors RAM : mmu_readl déréférence
// _mem_exec[addr >> 14], nul. L'oracle ne peut donc pas être confronté à ce chemin — le fuzzeur
// l'écarte — et c'est ici, côté C# seul, qu'on prouve qu'iXtal26 survit : pagination active,
// CR3 hors des 16 Mo, quelques pas d'un MOV qui lit puis écrit la mémoire. Sur le cœur 386 et
// sur le 486, qui partagent mem.cs. Le verdict est « survit » ou une exception nommée.

using iXtal26.Cpu;
using iXtal26.Diag;
using iXtal26.Memory;

namespace iXtal26.Diff;

internal static class R9Mmu
{
    internal static int Run()
    {
        var bad = 0;
        foreach (var (nom, reset) in new (string, Action)[] { ("386", _386.Reset386), ("486", _386.Reset486) })
        {
            foreach (var cr3 in new uint[] { 0xFFFFF000, 0x7F000000, 0x01000000 })
            {
                reset();
                // MOV AX,[BX] ; MOV [BX+2],AX ; NOP ; NOP — en 2000:0000.
                byte[] code = [0x8B, 0x07, 0x89, 0x47, 0x02, 0x90, 0x90];
                for (var k = 0; k < code.Length; k++)
                    mem.ram[0x20000 + k] = code[k];
                var regs = new ushort[(int)R.COUNT];
                regs[(int)R.CS] = 0x2000;
                regs[(int)R.DS] = 0x3000;
                regs[(int)R.SS] = 0x4000;
                regs[(int)R.SP] = 0xFFF0;
                regs[(int)R.BX] = 0x0100;
                _808x.SetRegs(regs);
                x86.cr3 = cr3;
                x86.cr0 |= 0x80000000;
                try
                {
                    for (var p = 0; p < 4; p++)
                        _386.Step286();
                    Console.WriteLine($"  cœur {nom}, CR3 {cr3:X8} : survit — CS:IP {x86.CS:X4}:{_386_common.cpu_state.pc:X8}, " +
                                      $"AX {x86.AX:X4}, abrt {_386_common.cpu_state.abrt}");
                }
                catch (Exception e)
                {
                    bad++;
                    Console.WriteLine($"  cœur {nom}, CR3 {cr3:X8} : ARRÊT — {e.GetType().Name} : {e.Message.Trim()}");
                }
            }
        }
        Console.WriteLine(bad == 0 ? "\nVert : iXtal26 survit à une table de pages hors RAM (PB-79, R9)."
                                   : $"\n{bad} arrêt(s) : R9 n'est pas tenue.");
        return bad == 0 ? 0 : 1;
    }
}
