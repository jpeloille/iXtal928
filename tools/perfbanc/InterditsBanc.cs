// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — outillage de mesure.
//
// CE QUE COÛTENT LES CONSTRUCTIONS INTERDITES (G15.0a ; PLAN.md § G15, « démontré » ; PLAN-G15.md § G15.0a,
// décision n° 6 ; R1-interdits.md § 2, I1, et R1-contre.md § 4).
//
// PLAN.md interdit dans les chemins chauds les classes statiques passées en instances, LINQ, les fermetures, les
// allocations dans les boucles (`new`, boxing, `string`, `params`), les interfaces et les méthodes virtuelles ; il veut
// qu'un banc mesure chaque construction contre sa forme retenue, sur un vrai chemin d'iXtal. Neuf cas, un par classe
// `InterditN…`, hors de la référence des 0 % (compare-ref.sh ne les nomme pas, comme GardeBanc et Is486Banc) :
//   1. readmemb (Cpu/386_common.cs:163-169), statique contre une instance (InterditsBanc.cs) ;
//   2. speaker_get_buffer (Sound/sound_speaker.cs:62-75), `for` contre LINQ (ici) ;
//   3. l'ADD, l'OR et le SUB d'OP_ARITH (Cpu/386_ops_arith.cs, OP_ARITH<TAlu>), un appel direct contre une interface
//      et un `override`, à un site polymorphe (InterditsCpu.cs) ;
//   4. le Jcc (Cpu/386_ops_jump.cs:83-100), une fermeture contre une méthode nommée (InterditsCpu.cs) ;
//   5. les affectations de rendu de svga_recalctimings (Video/vid_svga.cs:550-728), un groupe de méthodes mis en
//      cache contre une lambda qui capture, créée à chaque appel (InterditsSvga.cs) ;
//   6. le descripteur de loadseg (Cpu/x86seg.cs:68-268, `new uint16_t[4]`), le tas contre stackalloc
//      (InterditsCpu.cs) ;
//   7. l'aiguillage d'outb (io.cs:187-202), `priv` tel quel contre `priv` mis en boîte à chaque appel (ici) ;
//   8. svga_write (Video/vid_svga.cs:1089-1353), une interpolation de chaîne gardée (par la constante de PCem, ou par
//      un champ) contre la même sans garde (InterditsSvga.cs) ;
//   9. PREFETCH_RUN et prefetch_run (Cpu/386.cs:245-319), huit `int` contre `params int[]` (ici).
//
// LES COLONNES. Chaque cas a les mêmes, chacune une méthode [Benchmark] :
// - Reel : le vrai code d'iXtal26, appelé comme le produit l'appelle (directement, ou par le délégué que le produit
//   appelle : la table des opcodes, le gestionnaire du son, celui du port) ;
// - Retenue (la base du rapport) : une copie de ce code dans la forme que PLAN.md retient ;
// - AA : la même copie, compilée une seconde fois : le placement du code, que l'écart de Retenue à AA mesure ;
// - Interdite : la même copie, la seule construction interdite changée (au cas 3, deux colonnes : Interface et
//   Virtuel ; et Delegue, des délégués capturés, la forme du vrai jusqu'à G15.P ; au cas 1, une seconde,
//   InterditeInlinee ; au cas
//   8, Champ, la garde par un champ, en plus de l'interpolation sans garde).
// Retenue et AA sont un seul texte, une méthode générique instanciée sur deux structures vides (CopieA, CopieB) : le
// JIT compile un corps distinct pour chaque structure, à une autre adresse, sans rien partager. Aux cas 4 et 6, le
// vrai code EST une forme interdite (une fermeture, un tableau alloué) : sa colonne se compare à celle qu'il incarne
// (Interdite), et non à la retenue. Le cas 3 en était aussi (une fermeture qui appelle des délégués, Delegue)
// jusqu'à G15.P : depuis, le vrai est dans la forme retenue.
//
// L'ÉQUIVALENCE est vérifiée dans [GlobalSetup], avant toute mesure : depuis le même état, chaque colonne laisse le
// même état que le vrai code (les registres, les drapeaux paresseux, les cycles, la mémoire touchée, selon le cas) ;
// une copie qui diverge refuse le cas. Le processus imprime ensuite une ligne « // Interdit (…) : … », son cas, son
// mode et ce qui a été comparé.
//
// LE MODE. Tous les cas tournent en mode « aucun », posé avant le gel et vérifié après (Reference.Demander,
// Reference.VerifierMode) : le mode de l'oracle, et celui où la copie de chaque cas est celle du produit sans garde
// active. Aucun des neuf chemins ne porte de garde du mode matériel qui change ce qu'il exécute, sauf PB-220 dans
// svga_recalctimings (cas 5, une affectation de htotal, que « aucun » laisse pliée).
//
// LE LANCEMENT ET LE VERDICT : tools/perfbanc/interdits.sh (l'en-tête dit la commande et la règle de la décision n° 6).

using System.Reflection;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using iXtal26.Cpu;
using iXtal26.Diag;
using iXtal26.Ics;
using iXtal26.Memory;
using iXtal26.Sound;

using int16_t = System.Int16;
using int32_t = System.Int32;

namespace iXtal26.PerfBanc;

/// <summary>Les deux instanciations d'une copie retenue : la retenue et son A/A.</summary>
internal struct CopieA { }

/// <summary>Voir CopieA.</summary>
internal struct CopieB { }

/// <summary>Ce que partagent les neuf cas.</summary>
internal static class Interdits
{
    /// <summary>Le mode de chaque cas (voir l'en-tête).</summary>
    internal const string Mode = "aucun";

    internal static void Refuser(string cas, string colonne, string attendu, string obtenu) =>
        throw new InvalidOperationException($"{cas} : la colonne {colonne} diverge du vrai code.\n" +
                                            $"  attendu : {attendu}\n  obtenu  : {obtenu}");

    internal static void Imprimer(string cas, string etat) =>
        Console.WriteLine($"// Interdit ({ModeMateriel.Description}, {cas}) : {etat}");

    /// <summary>Un membre privé d'iXtal26, lu par réflexion une fois, avant la mesure.</summary>
    internal static FieldInfo Champ(Type t, string nom) =>
        t.GetField(nom, BindingFlags.NonPublic | BindingFlags.Static) ??
        throw new InvalidOperationException($"{t.Name}.{nom} est introuvable");

    internal static T Methode<T>(Type t, string nom) where T : Delegate =>
        (T)(t.GetMethod(nom, BindingFlags.NonPublic | BindingFlags.Static) ??
            throw new InvalidOperationException($"{t.Name}.{nom} est introuvable")).CreateDelegate(typeof(T));
}

// ---- 1. readmemb : statique contre une instance ----

/// <summary>Cas 1. Une lecture d'octet de la RAM de l'ami486 monté (le chemin rapide, la table de traduction), faite
/// par un gestionnaire non inliné, comme un opcode la fait : le vrai readmemb, la copie statique, et la copie
/// d'instance (le processeur tient sa mémoire par un champ, la mémoire ses tables par les siens). Le JIT n'inline pas
/// le readmemb statique, ni ici ni dans le produit (les listings de R gardent ses appels), mais il inline celui de
/// l'instance : Interdite garde l'appel, pour ne mesurer que l'accès par l'instance ; InterditeInlinee le laisse au
/// JIT.</summary>
public class Interdit1Instance
{
    [GlobalSetup]
    public void Preparer() => Interdit1.Preparer();

    [Benchmark(OperationsPerInvoke = Interdit1.Appels)]
    public int Reel() => Interdit1.BoucleReel();

    [Benchmark(Baseline = true, OperationsPerInvoke = Interdit1.Appels)]
    public int Retenue() => Interdit1.BoucleStatique<CopieA>();

    [Benchmark(OperationsPerInvoke = Interdit1.Appels)]
    public int AA() => Interdit1.BoucleStatique<CopieB>();

    [Benchmark(OperationsPerInvoke = Interdit1.Appels)]
    public int Interdite() => Interdit1.BoucleInstance();

    [Benchmark(OperationsPerInvoke = Interdit1.Appels)]
    public int InterditeInlinee() => Interdit1.BoucleInstanceInlinee();
}

/// <summary>La forme interdite : les mêmes tables, tenues par une instance.</summary>
internal sealed class MemoireInstance
{
    internal int[] readlookup2 = mem.readlookup2;
    internal byte[] ram = mem.ram;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal uint8_t readmemb(uint32_t s, uint32_t a)
    {
        var addr = s + a;
        if (readlookup2[addr >> 12] == -1)
                return mem.readmembl(addr);
        return ram[unchecked(readlookup2[addr >> 12] + (int)addr)];
    }

    internal uint8_t readmemb_inlinee(uint32_t s, uint32_t a)
    {
        var addr = s + a;
        if (readlookup2[addr >> 12] == -1)
                return mem.readmembl(addr);
        return ram[unchecked(readlookup2[addr >> 12] + (int)addr)];
    }
}

/// <summary>Le processeur en instance : sa mémoire est un champ.</summary>
internal sealed class ProcesseurInstance
{
    internal MemoireInstance memoire = new();

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal int Lire(uint32_t a) => memoire.readmemb(0, a);

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal int LireInlinee(uint32_t a) => memoire.readmemb_inlinee(0, a);
}

internal static class Interdit1
{
    internal const int Appels = 4096;

    /// <summary>Quatre pages de RAM au-dessus de 64 Ko, que la préparation fait entrer dans la table de
    /// traduction.</summary>
    private const uint32_t Base = 0x10000, Masque = 0x3fff;

    private static ProcesseurInstance _processeur = null!;

    private static uint32_t Adresse(int i) => Base + ((uint32_t)(i * 0x41) & Masque);

    internal static void Preparer()
    {
        Reference.Demander(Interdits.Mode, "Interdit1Instance");
        Reference.Monter("model = ami486\ncpu = 10\nfpu = builtin\nmem_size = 4096\ngfxcard = cl_gd5429\n" +
                         "sndcard = none\n", "Interdit1Instance");
        Reference.VerifierMode(Interdits.Mode, "Interdit1Instance");
        for (var k = 0; k <= (int)Masque; k++)
            mem.ram[Base + k] = (byte)(k * 7 + (k >> 8));
        mem.flushmmucache();
        for (uint32_t p = 0; p <= Masque; p += 0x1000)
            _386_common.readmemb(0, Base + p);
        _processeur = new ProcesseurInstance();
        var lents = Counters.n_readmembl;
        int[] colonnes = [BoucleReel(), BoucleStatique<CopieA>(), BoucleStatique<CopieB>(), BoucleInstance(),
                          BoucleInstanceInlinee()];
        lents = Counters.n_readmembl - lents;
        string[] noms = ["Reel", "Retenue", "AA", "Interdite", "InterditeInlinee"];
        for (var c = 1; c < colonnes.Length; c++)
            if (colonnes[c] != colonnes[0])
                Interdits.Refuser("Interdit1Instance", noms[c], $"{colonnes[0]:X8}", $"{colonnes[c]:X8}");
        if (lents != 0)
            throw new InvalidOperationException($"Interdit1Instance : {lents} lectures par le chemin lent");
        Interdits.Imprimer("Interdit1Instance", $"ami486, {Appels} lectures par colonne, la somme {colonnes[0]:X8} " +
                           "dans les cinq, 0 par le chemin lent");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int LireReel(uint32_t a) => _386_common.readmemb(0, a);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int LireStatique<T>(uint32_t a) where T : struct => ReadmembStatique<T>(0, a);

    internal static int BoucleReel()
    {
        var r = 0;
        for (var i = 0; i < Appels; i++)
            r += LireReel(Adresse(i)) * (i | 1);
        return r;
    }

    internal static int BoucleStatique<T>() where T : struct
    {
        var r = 0;
        for (var i = 0; i < Appels; i++)
            r += LireStatique<T>(Adresse(i)) * (i | 1);
        return r;
    }

    internal static int BoucleInstance()
    {
        var p = _processeur;
        var r = 0;
        for (var i = 0; i < Appels; i++)
            r += p.Lire(Adresse(i)) * (i | 1);
        return r;
    }

    internal static int BoucleInstanceInlinee()
    {
        var p = _processeur;
        var r = 0;
        for (var i = 0; i < Appels; i++)
            r += p.LireInlinee(Adresse(i)) * (i | 1);
        return r;
    }

    // Cpu/386_common.cs:163-169, ligne pour ligne.
    private static uint8_t ReadmembStatique<T>(uint32_t s, uint32_t a) where T : struct
    {
        var addr = s + a;
        if (mem.readlookup2[addr >> 12] == -1)
                return mem.readmembl(addr);
        return mem.ram[unchecked(mem.readlookup2[addr >> 12] + (int)addr)];
    }
}

// ---- 2. speaker_get_buffer : `for` contre LINQ ----

/// <summary>Cas 2. Le gestionnaire du haut-parleur que sound_poll appelle par son délégué, sur un tampon de 2 400 tics
/// (sound_buf_len_al) : le vrai, la boucle `for` copiée, et la même par Enumerable.Range et Select.</summary>
public class Interdit2Linq
{
    private sound_get_buffer_t reel = null!, retenue = null!, aa = null!, interdite = null!;

    [GlobalSetup]
    public void Preparer()
    {
        reel = Interdit2.Preparer();
        retenue = Interdit2.GetBuffer<CopieA>;
        aa = Interdit2.GetBuffer<CopieB>;
        interdite = Interdit2.GetBufferLinq;
        Interdit2.VerifierEquivalence(reel, [retenue, aa, interdite]);
    }

    [Benchmark(OperationsPerInvoke = Interdit2.Appels)]
    public int Reel() => Interdit2.Boucle(reel);

    [Benchmark(Baseline = true, OperationsPerInvoke = Interdit2.Appels)]
    public int Retenue() => Interdit2.Boucle(retenue);

    [Benchmark(OperationsPerInvoke = Interdit2.Appels)]
    public int AA() => Interdit2.Boucle(aa);

    [Benchmark(OperationsPerInvoke = Interdit2.Appels)]
    public int Interdite() => Interdit2.Boucle(interdite);
}

internal static class Interdit2
{
    internal const int Appels = 16;

    private static int16_t[] speaker_buffer = [];
    private static int32_t[] _tampon = [];
    private static FieldInfo _pos = null!;

    /// <summary>Monte l'XT, remplit le tampon du haut-parleur, et rend le vrai gestionnaire. sound_pos_global reste à
    /// 0 : speaker_update n'a rien à rattraper, et speaker_pos reste à 0 (vérifié à chaque appel de la
    /// préparation), ce qui permet aux copies d'omettre sa remise à zéro (un champ privé).</summary>
    internal static sound_get_buffer_t Preparer()
    {
        Reference.Demander(Interdits.Mode, "Interdit2Linq");
        Reference.Monter("model = ibmxt\nmem_size = 640\ngfxcard = cga\nsndcard = none\n", "Interdit2Linq");
        Reference.VerifierMode(Interdits.Mode, "Interdit2Linq");
        speaker_buffer = (int16_t[])Interdits.Champ(typeof(sound_speaker), "speaker_buffer").GetValue(null)!;
        _pos = Interdits.Champ(typeof(sound_speaker), "speaker_pos");
        for (var k = 0; k < speaker_buffer.Length; k++)
            speaker_buffer[k] = (int16_t)((k * 37) ^ (k << 5));
        sound.sound_pos_global = 0;
        _tampon = new int32_t[sound.sound_buf_len_al * 2];
        if (sound_speaker.speaker_mute != 0)
            throw new InvalidOperationException("Interdit2Linq : le haut-parleur est muet");
        return Interdits.Methode<sound_get_buffer_t>(typeof(sound_speaker), "speaker_get_buffer");
    }

    internal static void VerifierEquivalence(sound_get_buffer_t reel, sound_get_buffer_t[] copies)
    {
        var depart = new int32_t[_tampon.Length];
        for (var k = 0; k < depart.Length; k++)
            depart[k] = k * 1001 - 500000;
        var attendu = Releve(reel, depart);
        string[] noms = ["Retenue", "AA", "Interdite"];
        for (var c = 0; c < copies.Length; c++)
        {
            var obtenu = Releve(copies[c], depart);
            if (obtenu != attendu)
                Interdits.Refuser("Interdit2Linq", noms[c], $"{attendu:X16}", $"{obtenu:X16}");
        }
        Array.Clear(_tampon);
        Interdits.Imprimer("Interdit2Linq", $"ibmxt, {sound.sound_buf_len_al} tics, deux appels par colonne, " +
                           $"le tampon {attendu:X16} dans les quatre");
    }

    private static ulong Releve(sound_get_buffer_t g, int32_t[] depart)
    {
        depart.CopyTo(_tampon, 0);
        g(_tampon, sound.sound_buf_len_al, null);
        g(_tampon, sound.sound_buf_len_al, null);
        if ((int)_pos.GetValue(null)! != 0 || sound.sound_pos_global != 0)
            throw new InvalidOperationException("Interdit2Linq : speaker_pos ou sound_pos_global n'est plus nul");
        return Reference.Fnv<int32_t>(_tampon);
    }

    internal static int Boucle(sound_get_buffer_t g)
    {
        var t = _tampon;
        var len = sound.sound_buf_len_al;
        for (var i = 0; i < Appels; i++)
            g(t, len, null);
        return t[len];
    }

    // Sound/sound_speaker.cs:62-75, ligne pour ligne, hors de `speaker_pos = 0` (voir Preparer).
    internal static void GetBuffer<T>(int32_t[] buffer, int len, object? p) where T : struct
    {
        int c;

        sound_speaker.speaker_update();

        if (sound_speaker.speaker_mute == 0)
        {
                for (c = 0; c < len * 2; c++)
                        buffer[c] += speaker_buffer[c >> 1];
        }
    }

    // La même, la boucle en LINQ, recopiée dans le tampon.
    internal static void GetBufferLinq(int32_t[] buffer, int len, object? p)
    {
        sound_speaker.speaker_update();

        if (sound_speaker.speaker_mute == 0)
        {
                var haut = speaker_buffer;
                Enumerable.Range(0, len * 2).Select(c => buffer[c] + haut[c >> 1]).ToArray().CopyTo(buffer, 0);
        }
    }
}

// ---- 7. outb : `priv` tel quel contre `priv` mis en boîte ----

/// <summary>Cas 7. Une écriture de port par outb, vers les registres de page du DMA (80h-87h, dma_page_write, que
/// tout PC pose avec un `priv` nul) : le vrai outb, la copie de son aiguillage, et la même qui passe au gestionnaire
/// le numéro du port mis en boîte (un entier en `object`, alloué à chaque appel).</summary>
public class Interdit7Boite
{
    [GlobalSetup]
    public void Preparer() => Interdit7.Preparer();

    [Benchmark(OperationsPerInvoke = Interdit7.Appels)]
    public int Reel() => Interdit7.BoucleReel();

    [Benchmark(Baseline = true, OperationsPerInvoke = Interdit7.Appels)]
    public int Retenue() => Interdit7.Boucle<CopieA>();

    [Benchmark(OperationsPerInvoke = Interdit7.Appels)]
    public int AA() => Interdit7.Boucle<CopieB>();

    [Benchmark(OperationsPerInvoke = Interdit7.Appels)]
    public int Interdite() => Interdit7.BoucleBoite();
}

internal static class Interdit7
{
    internal const int Appels = 4096;

    /// <summary>dmapages (Ics/Intel8237.cs:68), privé : le tableau lu une fois.</summary>
    private static uint8_t[] dmapages = [];

    private static uint16_t Port(int i) => (uint16_t)(0x80 + (i & 7));

    internal static void Preparer()
    {
        Reference.Demander(Interdits.Mode, "Interdit7Boite");
        Reference.Monter("model = ibmxt\nmem_size = 640\ngfxcard = cga\nsndcard = none\n", "Interdit7Boite");
        Reference.VerifierMode(Interdits.Mode, "Interdit7Boite");
        dmapages = (uint8_t[])Interdits.Champ(typeof(Intel8237), "dmapages").GetValue(null)!;
        for (var port = 0x80; port <= 0x87; port++)
            if (io.port_outb[port, 0]?.Method.Name != "dma_page_write" || io.port_outb[port, 1] != null ||
                io.port_priv[port, 0] != null)
                throw new InvalidOperationException($"Interdit7Boite : le port {port:X2}h n'est pas " +
                                                    "dma_page_write seul");
        Func<int>[] colonnes = [BoucleReel, Boucle<CopieA>, Boucle<CopieB>, BoucleBoite];
        string[] noms = ["Reel", "Retenue", "AA", "Interdite"];
        string? attendu = null;
        for (var c = 0; c < colonnes.Length; c++)
        {
            Array.Clear(dmapages);
            var avant = Counters.n_outb;
            var r = colonnes[c]();
            var obtenu = $"{r:X8}, pages {Reference.Fnv<uint8_t>(dmapages):X16}, " +
                         $"{Counters.n_outb - avant} écritures comptées";
            if (attendu == null)
                attendu = obtenu;
            else if (obtenu != attendu)
                Interdits.Refuser("Interdit7Boite", noms[c], attendu, obtenu);
        }
        Interdits.Imprimer("Interdit7Boite", $"ibmxt, {Appels} écritures par colonne, {attendu} dans les quatre");
    }

    internal static int BoucleReel()
    {
        for (var i = 0; i < Appels; i++)
            io.outb(Port(i), (uint8_t)i);
        return dmapages[1];
    }

    internal static int Boucle<T>() where T : struct
    {
        for (var i = 0; i < Appels; i++)
            Outb<T>(Port(i), (uint8_t)i);
        return dmapages[1];
    }

    internal static int BoucleBoite()
    {
        for (var i = 0; i < Appels; i++)
            OutbBoite(Port(i), (uint8_t)i);
        return dmapages[1];
    }

    // io.cs:187-202 (outb et outb_corps), ligne pour ligne.
    private static void Outb<T>(uint16_t port, uint8_t val) where T : struct
    {
        Counters.n_outb++;
        OutbCorps<T>(port, val);
    }

    private static void OutbCorps<T>(uint16_t port, uint8_t val) where T : struct
    {
        if (io.port_outb[port, 0] != null)
            io.port_outb[port, 0]!(port, val, io.port_priv[port, 0]!);
        if (io.port_outb[port, 1] != null)
            io.port_outb[port, 1]!(port, val, io.port_priv[port, 1]!);

        return;
    }

    // Les mêmes, `priv` un entier mis en boîte à chaque appel.
    private static void OutbBoite(uint16_t port, uint8_t val)
    {
        Counters.n_outb++;
        OutbCorpsBoite(port, val);
    }

    private static void OutbCorpsBoite(uint16_t port, uint8_t val)
    {
        if (io.port_outb[port, 0] != null)
            io.port_outb[port, 0]!(port, val, (object)(int)port);
        if (io.port_outb[port, 1] != null)
            io.port_outb[port, 1]!(port, val, (object)(int)port);

        return;
    }
}

// ---- 9. PREFETCH_RUN : huit `int` contre `params int[]` ----

/// <summary>Cas 9. Le modèle de la file d'instruction du 286 (cpu_prefetch_cycles = 2), appelé comme un opcode
/// l'appelle après chaque instruction : le vrai PREFETCH_RUN, sa copie, et la même dont les huit paramètres sont un
/// `params int[]` (un tableau alloué à chaque appel).</summary>
public class Interdit9Params
{
    [GlobalSetup]
    public void Preparer() => Interdit9.Preparer();

    [Benchmark(OperationsPerInvoke = Interdit9.Appels)]
    public int Reel() => Interdit9.BoucleReel();

    [Benchmark(Baseline = true, OperationsPerInvoke = Interdit9.Appels)]
    public int Retenue() => Interdit9.Boucle<CopieA>();

    [Benchmark(OperationsPerInvoke = Interdit9.Appels)]
    public int AA() => Interdit9.Boucle<CopieB>();

    [Benchmark(OperationsPerInvoke = Interdit9.Appels)]
    public int Interdite() => Interdit9.BoucleParams();
}

internal static class Interdit9
{
    internal const int Appels = 4096;

    internal static void Preparer()
    {
        Reference.Demander(Interdits.Mode, "Interdit9Params");
        _386.Reset286();
        Reference.VerifierMode(Interdits.Mode, "Interdit9Params");
        if (cpu_c.cpu_prefetch_cycles == 0)
            throw new InvalidOperationException("Interdit9Params : cpu_prefetch_cycles est nul, le modèle est inerte");
        Func<int>[] colonnes = [BoucleReel, Boucle<CopieA>, Boucle<CopieB>, BoucleParams];
        string[] noms = ["Reel", "Retenue", "AA", "Interdite"];
        string? attendu = null;
        for (var c = 0; c < colonnes.Length; c++)
        {
            var r = colonnes[c]();
            var obtenu = $"{r:X8}, prefetch_bytes {_386.prefetch_bytes}, prefetch_prefixes {_386.prefetch_prefixes}";
            if (attendu == null)
                attendu = obtenu;
            else if (obtenu != attendu)
                Interdits.Refuser("Interdit9Params", noms[c], attendu, obtenu);
        }
        Interdits.Imprimer("Interdit9Params", $"ami286, {Appels} appels par colonne, {attendu} dans les quatre");
    }

    /// <summary>Les arguments d'un opcode à ModRM : ses cycles, ses octets, son ModRM, une lecture une fois sur
    /// deux.</summary>
    internal static int BoucleReel()
    {
        x86.cycles = 0;
        _386.prefetch_bytes = 0;
        for (var i = 0; i < Appels; i++)
            _386.PREFETCH_RUN(2 + (i & 7), 2, i & 0xff, i & 1, 0, 0, 0, 0);
        return x86.cycles;
    }

    internal static int Boucle<T>() where T : struct
    {
        x86.cycles = 0;
        _386.prefetch_bytes = 0;
        for (var i = 0; i < Appels; i++)
            PrefetchRun<T>(2 + (i & 7), 2, i & 0xff, i & 1, 0, 0, 0, 0);
        return x86.cycles;
    }

    internal static int BoucleParams()
    {
        x86.cycles = 0;
        _386.prefetch_bytes = 0;
        for (var i = 0; i < Appels; i++)
            PrefetchRunParams(2 + (i & 7), 2, i & 0xff, i & 1, 0, 0, 0, 0);
        return x86.cycles;
    }

    // Cpu/386.cs:314-319 (PREFETCH_RUN) et :245-307 (prefetch_run), ligne pour ligne.
    private static void PrefetchRun<T>(int instr_cycles, int bytes, int modrm, int reads, int reads_l,
                                       int writes, int writes_l, int ea32) where T : struct
    {
        if (cpu_c.cpu_prefetch_cycles != 0)
                Corps<T>(instr_cycles, bytes, modrm, reads, reads_l, writes, writes_l, ea32);
    }

    private static void Corps<T>(int instr_cycles, int bytes, int modrm, int reads, int reads_l,
                                 int writes, int writes_l, int ea32) where T : struct
    {
        int mem_cycles = reads * cpu_c.cpu_cycles_read + reads_l * cpu_c.cpu_cycles_read_l
                       + writes * cpu_c.cpu_cycles_write + writes_l * cpu_c.cpu_cycles_write_l;

        if (instr_cycles < mem_cycles)
                instr_cycles = mem_cycles;

        _386.prefetch_bytes -= _386.prefetch_prefixes;
        _386.prefetch_bytes -= bytes;
        if (modrm != -1)
        {
                if (ea32 != 0)
                {
                        if ((modrm & 7) == 4)
                        {
                                if ((modrm & 0x700) == 0x500)
                                        _386.prefetch_bytes -= 5;
                                else if ((modrm & 0xc0) == 0x40)
                                        _386.prefetch_bytes -= 2;
                                else if ((modrm & 0xc0) == 0x80)
                                        _386.prefetch_bytes -= 5;
                        }
                        else
                        {
                                if ((modrm & 0xc7) == 0x05)
                                        _386.prefetch_bytes -= 4;
                                else if ((modrm & 0xc0) == 0x40)
                                        _386.prefetch_bytes--;
                                else if ((modrm & 0xc0) == 0x80)
                                        _386.prefetch_bytes -= 4;
                        }
                }
                else
                {
                        if ((modrm & 0xc7) == 0x06)
                                _386.prefetch_bytes -= 2;
                        else if ((modrm & 0xc0) != 0xc0)
                                _386.prefetch_bytes -= ((modrm & 0xc0) >> 6);
                }
        }

        /*Fill up prefetch queue*/
        while (_386.prefetch_bytes < 0)
        {
                _386.prefetch_bytes += cpu_c.cpu_prefetch_width;
                x86.cycles -= cpu_c.cpu_prefetch_cycles;
        }

        /*Subtract cycles used for memory access by instruction*/
        instr_cycles -= mem_cycles;

        while (instr_cycles >= cpu_c.cpu_prefetch_cycles)
        {
                _386.prefetch_bytes += cpu_c.cpu_prefetch_width;
                instr_cycles -= cpu_c.cpu_prefetch_cycles;
        }

        _386.prefetch_prefixes = 0;
        if (_386.prefetch_bytes > 16)
                _386.prefetch_bytes = 16;
    }

    // La même, les huit paramètres en `params int[]`.
    private static void PrefetchRunParams(params int[] a)
    {
        if (cpu_c.cpu_prefetch_cycles != 0)
                CorpsParams(a);
    }

    private static void CorpsParams(params int[] a)
    {
        int instr_cycles = a[0], bytes = a[1], modrm = a[2], reads = a[3], reads_l = a[4], writes = a[5],
            writes_l = a[6], ea32 = a[7];
        int mem_cycles = reads * cpu_c.cpu_cycles_read + reads_l * cpu_c.cpu_cycles_read_l
                       + writes * cpu_c.cpu_cycles_write + writes_l * cpu_c.cpu_cycles_write_l;

        if (instr_cycles < mem_cycles)
                instr_cycles = mem_cycles;

        _386.prefetch_bytes -= _386.prefetch_prefixes;
        _386.prefetch_bytes -= bytes;
        if (modrm != -1)
        {
                if (ea32 != 0)
                {
                        if ((modrm & 7) == 4)
                        {
                                if ((modrm & 0x700) == 0x500)
                                        _386.prefetch_bytes -= 5;
                                else if ((modrm & 0xc0) == 0x40)
                                        _386.prefetch_bytes -= 2;
                                else if ((modrm & 0xc0) == 0x80)
                                        _386.prefetch_bytes -= 5;
                        }
                        else
                        {
                                if ((modrm & 0xc7) == 0x05)
                                        _386.prefetch_bytes -= 4;
                                else if ((modrm & 0xc0) == 0x40)
                                        _386.prefetch_bytes--;
                                else if ((modrm & 0xc0) == 0x80)
                                        _386.prefetch_bytes -= 4;
                        }
                }
                else
                {
                        if ((modrm & 0xc7) == 0x06)
                                _386.prefetch_bytes -= 2;
                        else if ((modrm & 0xc0) != 0xc0)
                                _386.prefetch_bytes -= ((modrm & 0xc0) >> 6);
                }
        }

        /*Fill up prefetch queue*/
        while (_386.prefetch_bytes < 0)
        {
                _386.prefetch_bytes += cpu_c.cpu_prefetch_width;
                x86.cycles -= cpu_c.cpu_prefetch_cycles;
        }

        /*Subtract cycles used for memory access by instruction*/
        instr_cycles -= mem_cycles;

        while (instr_cycles >= cpu_c.cpu_prefetch_cycles)
        {
                _386.prefetch_bytes += cpu_c.cpu_prefetch_width;
                instr_cycles -= cpu_c.cpu_prefetch_cycles;
        }

        _386.prefetch_prefixes = 0;
        if (_386.prefetch_bytes > 16)
                _386.prefetch_bytes = 16;
    }
}
