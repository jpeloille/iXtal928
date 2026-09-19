// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — échafaudage temporaire M0.
//
// Les quatre portes du jalon M0. Elles prouvent que la convention de transcription
// C -> C# tient AVANT qu'on écrive 4 600 lignes dessus. Si G1 ou G2 échoue, la
// convention est fausse et on le sait au jour 1 pour 40 lignes.
//
// CE FICHIER EST JETABLE. Il est supprimé dès que les quatre portes sont vertes et
// que Cpu/x86.cs porte les vrais types. Il n'a pas vocation à être maintenu.
//
//   G1  propriété `ref`      remplace #define cycles cpu_state._cycles
//   G2  switch géant + goto  remplace le switch(opcode) de 2 600 lignes de 808x.c
//   G3  union explicite      remplace l'union x86reg (l / w / b.h / b.l)
//   G4  SDL3-CS sous net10   (prouvée hors de ce fichier : dotnet run -- --frames 5)

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace iXtal26;

internal static partial class M0Gates
{
    public static int Run()
    {
        var fail = 0;
        fail += Check("G1 propriete ref", G1_RefProperty());
        fail += Check("G2 switch geant + goto", G2_GiantSwitch());
        fail += Check("G3 union explicite", G3_ExplicitUnion());

        var il = typeof(M0Gates)
            .GetMethod("ExecScratch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .GetMethodBody()!.GetILAsByteArray()!.Length;
        Console.WriteLine($"       ExecScratch : {il} octets d'IL pour 256 cas / ~13 900 lignes source.");

        Console.WriteLine(fail == 0
            ? "M0 : portes G1-G3 vertes (G4 = dotnet run -- --frames 5)."
            : $"M0 : {fail} porte(s) en echec — la convention de transcription est fausse.");
        return fail == 0 ? 0 : 1;
    }

    private static int Check(string name, string? error)
    {
        Console.WriteLine(error is null ? $"  OK   {name}" : $"  FAIL {name} : {error}");
        return error is null ? 0 : 1;
    }

    // -----------------------------------------------------------------------
    // G1 — La propriété `ref`
    //
    // x86.h:122 : #define cycles cpu_state._cycles
    //
    // Ce #define existe pour que 4 000 lignes puissent écrire `cycles -= 3;`. Le
    // résoudre à la main, c'est réécrire chacune de ces lignes. Une propriété
    // ref-returning EST une variable : l'affectation composée compile et mute le
    // référent, et le passage par ref/out fonctionne.
    //
    // Si cette porte tombe, toute la section « Convention C -> C# » du plan
    // s'effondre et il faut repartir sur un autre modèle.
    // -----------------------------------------------------------------------

    private sealed class CpuStateScratch
    {
        public int _cycles;
        public uint64_t _tsc;
    }

    private static readonly CpuStateScratch cpu_state = new();

    private static ref int cycles
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref cpu_state._cycles;
    }

    private static ref uint64_t tsc
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref cpu_state._tsc;
    }

    private static void BurnCycles(ref int c) => c -= 100;

    private static string? G1_RefProperty()
    {
        cpu_state._cycles = 1000;
        cpu_state._tsc = 0;

        cycles -= 3;                       // la forme exacte de 808x.c
        if (cpu_state._cycles != 997)
            return $"affectation composee : attendu 997, obtenu {cpu_state._cycles}";

        cycles = cycles - 7;               // forme dépliée
        if (cpu_state._cycles != 990)
            return $"affectation simple : attendu 990, obtenu {cpu_state._cycles}";

        BurnCycles(ref cycles);            // passage par ref
        if (cpu_state._cycles != 890)
            return $"passage par ref : attendu 890, obtenu {cpu_state._cycles}";

        tsc += 0x1_0000_0000UL;            // même mécanisme sur un uint64
        if (cpu_state._tsc != 0x1_0000_0000UL)
            return $"ref sur uint64 : attendu 0x100000000, obtenu 0x{cpu_state._tsc:X}";

        return null;
    }

    // -----------------------------------------------------------------------
    // G3 — L'union explicite
    //
    // x86.h:29-35, et surtout x86.h:227 :
    //   #define getr8(r) ((r & 4) ? cpu_state.regs[r & 3].b.h : cpu_state.regs[r & 3].b.l)
    //
    // L'accès est INDEXÉ, piloté par cpu_reg/cpu_rm, avec la sélection haut/bas
    // pliée dans le bit 2 de l'index. LayoutKind.Explicit transcrit ça
    // directement ; un schéma à propriétés imposerait un switch et un
    // read-modify-write à chaque site.
    //
    // Le piège que cette porte vérifie aussi : regs DOIT être un champ tableau nu.
    // Via un getter de propriété on muterait une COPIE et l'écriture disparaîtrait
    // sans le moindre diagnostic.
    // -----------------------------------------------------------------------

    [StructLayout(LayoutKind.Explicit, Size = 4)]
    private struct x86reg
    {
        [FieldOffset(0)] public uint32_t l;
        [FieldOffset(0)] public uint16_t w;
        [FieldOffset(0)] public x86reg_b b;
    }

    [StructLayout(LayoutKind.Explicit, Size = 2)]
    private struct x86reg_b
    {
        [FieldOffset(0)] public uint8_t l;
        [FieldOffset(1)] public uint8_t h;
    }

    private static readonly x86reg[] regs = new x86reg[8];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint8_t getr8(int r) => (r & 4) != 0 ? regs[r & 3].b.h : regs[r & 3].b.l;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void setr8(int r, uint8_t v)
    {
        if ((r & 4) != 0) regs[r & 3].b.h = v;
        else regs[r & 3].b.l = v;
    }

    private static string? G3_ExplicitUnion()
    {
        Array.Clear(regs);

        regs[0].l = 0x12345678u;
        if (regs[0].w != 0x5678)
            return $"aliasing w : attendu 0x5678, obtenu 0x{regs[0].w:X4}";
        if (regs[0].b.l != 0x78)
            return $"aliasing b.l : attendu 0x78, obtenu 0x{regs[0].b.l:X2}";
        if (regs[0].b.h != 0x56)
            return $"aliasing b.h : attendu 0x56, obtenu 0x{regs[0].b.h:X2}";

        // Écriture d'un demi-octet : le reste du registre doit survivre intact.
        regs[0].b.h = 0xAB;
        if (regs[0].l != 0x1234AB78u)
            return $"ecriture b.h : attendu 0x1234AB78, obtenu 0x{regs[0].l:X8}";

        // L'accès indexé, la vraie forme de getr8/setr8.
        // r = 0..3 -> AL CL DL BL (b.l) ; r = 4..7 -> AH CH DH BH (b.h)
        Array.Clear(regs);
        for (var r = 0; r < 8; r++)
            setr8(r, (uint8_t)(0xA0 + r));

        for (var r = 0; r < 8; r++)
            if (getr8(r) != (uint8_t)(0xA0 + r))
                return $"getr8/setr8 r={r} : attendu 0x{0xA0 + r:X2}, obtenu 0x{getr8(r):X2}";

        // Et la vérification croisée : setr8(4) (=AH) doit avoir touché regs[0] haut.
        if (regs[0].w != 0xA4A0)
            return $"pliage haut/bas : regs[0].w attendu 0xA4A0, obtenu 0x{regs[0].w:X4}";

        // Le piège du lost write : muter à travers une copie doit être visible ici
        // comme une DIFFÉRENCE (on prouve que le tableau nu, lui, mute bien).
        var copy = regs[0];
        copy.b.l = 0xFF;
        if (regs[0].b.l != 0xA0)
            return "la copie de struct a mute l'original — layout inattendu";

        return null;
    }

    // -----------------------------------------------------------------------
    // G2 — Le switch géant avec goto
    //
    // 808x.c:1271-3905 est un switch(opcode) de ~2 600 lignes, avec quatre
    // `goto opcodestart` (les préfixes de segment, :1589/1664/1739/1798) qui
    // sautent HORS du switch vers un label du corps de la boucle englobante.
    //
    // Deux choses à prouver, et la seconde est la vraie raison de cette porte :
    //   1. La syntaxe : C# autorise ce saut arrière. (Facile.)
    //   2. La taille : RyuJIT n'a pas de limite documentée, mais les très gros
    //      corps de méthode ont historiquement provoqué des bailouts de JIT. Il
    //      faut le savoir au jour 1, pas à l'opcode 0xF3.
    //
    // Le corps ci-dessous est volontairement massif et généré : 256 cas, chacun
    // avec de la comptabilité de cycles, un sous-switch sur (rmdat & 0x38) et des
    // accès mémoire — la forme réelle de 808x.c.
    //
    // Contingence prévue si cette porte tombe : les quatre sites `goto opcodestart`
    // sont TOUS des préfixes de segment, donc convertibles en `continue` sur une
    // boucle de préfixe interne. Quatre sites, un après-midi. Mais il faut le
    // savoir maintenant : le fichier doit être conçu pour que cette transformation
    // soit un changement de 20 lignes, pas une réécriture.
    // -----------------------------------------------------------------------

    private static readonly uint8_t[] scratch_ram = new uint8_t[0x100000];
    private static int cpu_mod, cpu_reg, cpu_rm, ssegs;
    private static uint32_t ea_seg_base;

    private static string? G2_GiantSwitch()
    {
        Array.Clear(regs);
        cpu_state._cycles = 0;
        for (var i = 0; i < scratch_ram.Length; i += 997)
            scratch_ram[i] = (uint8_t)(i >> 3);

        // On exerce les 256 cas, y compris les quatre sites de goto.
        var acc = 0L;
        for (var op = 0; op < 256; op++)
        {
            cpu_state._cycles = 1000;
            acc += ExecScratch((uint8_t)op, (uint8_t)(op * 7 + 3));
            acc += cpu_state._cycles;
        }

        // Le résultat exact importe peu : ce qui compte est que la méthode ait
        // compilé, JITé et tourné sans bailout. On vérifie juste qu'elle a
        // réellement fait du travail et pris les chemins de goto.
        if (acc == 0)
            return "le switch n'a rien execute";
        if (goto_taken != 4)
            return $"sites de goto : attendu 4 pris, obtenu {goto_taken}";

        return null;
    }

    private static int goto_taken;
}
