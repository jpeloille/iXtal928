// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — couche d'interopérabilité, pas de contrepartie C à transcrire.
//
// P/Invoke vers tools/oracle/libixtal26oracle.so : le vrai cœur 8088 de PCem.
//
// HState, R et Seg vivent dans l'assembly du cœur (iXtal26.Diag) : c'est le
// cœur qui remplit le vecteur d'état, et cet outillage qui y marshale l'oracle.
// La dépendance va outillage -> cœur, jamais l'inverse.
// C'est par ici que passe tout le diff différentiel — la seule source de vérité
// pour la comptabilité de cycles, que SingleStepTests ne peut pas valider.

using System.Runtime.InteropServices;
using iXtal26.Diag;

namespace iXtal26.Diff;

public static class Oracle
{
    private const string Lib = "ixtal26oracle";
    // 6 depuis M12 : h_set_hdd et h_set_hdd_controller s'ajoutent au contrat. Doit suivre
    // H_ABI_VERSION à l'identique — c'est ce garde, et lui seul, qui distingue « le .so
    // est périmé » d'un symbole introuvable au premier appel.
    // 7 et 8 au jalon 286 : h_state s'élargit (cache descripteur, puis descripteurs
    // système). 9 : h_set_core / h_get_core s'ajoutent au contrat. 10 : les quatre
    // drapeaux paresseux entrent dans h_state.
    // 13 au bloc C : h_seg_clear_residue s'ajoute au contrat. Le vecteur ne change
    // PAS de taille — c'est une fonction, pas un champ.
    // 14 au bloc C etape 6a : h_step_trace s'ajoute au contrat, pour que les deux
    // phases du boot-diff empruntent le MEME pas.
    // 15 a C7a : sept champs du mode protege entrent dans h_state et h_wlog_max
    // s'ajoute au contrat. Le vecteur change de TAILLE, contrairement aux trois
    // bumps precedents.
    // 16 : h_set_trace_notsc s'ajoute au contrat. 17 a M15 : la VGA.
    // 18 a M16 : h_set_cpu, h_slice_budget et h_cpu_fingerprint, et h_boot fait
    // tourner le vrai cpu_set(). Le vecteur ne change pas de taille.
    public const int AbiVersion = 18;

    static Oracle()
    {
        // La .so est construite par tools/oracle/Makefile et n'est pas installée
        // sur le système. On la résout par chemin plutôt que d'imposer un
        // LD_LIBRARY_PATH à l'appelant.
        NativeLibrary.SetDllImportResolver(typeof(Oracle).Assembly, (name, asm, path) =>
        {
            if (name != Lib)
                return IntPtr.Zero;

            foreach (var candidate in Candidates())
                if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out var h))
                    return h;

            throw new DllNotFoundException(
                $"libixtal26oracle.so introuvable. Construire l'oracle : (cd tools/oracle && make)\n" +
                $"Cherché dans :\n  {string.Join("\n  ", Candidates())}");
        });
    }

    /// <summary>Adresse d'un symbole GLOBAL de la .so, hors contrat h_*.
    ///
    /// Le contrat ABI couvre les fonctions ; les variables globales de PCem, elles,
    /// sont exportees par le lieur sans que personne les ait declarees. Les lire
    /// directement evite de faire grossir h_state pour une MESURE ponctuelle — et
    /// c'est la version C# du gdb dont on se sert pour lire ops_286[].</summary>
    public static IntPtr Symbole(string nom)
    {
        foreach (var candidate in Candidates())
            if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out var h))
                return NativeLibrary.GetExport(h, nom);
        throw new DllNotFoundException("libixtal26oracle.so introuvable");
    }

    private static IEnumerable<string> Candidates()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && dir is not null; i++)
        {
            yield return Path.Combine(dir, "libixtal26oracle.so");
            yield return Path.Combine(dir, "tools", "oracle", "libixtal26oracle.so");
            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
        }
    }

    [DllImport(Lib)] public static extern uint h_abi_version();
    [DllImport(Lib)] public static extern uint h_state_size();
    [DllImport(Lib)] public static extern void h_reset();
    [DllImport(Lib)] public static extern void h_load(uint addr, byte[] buf, uint len);
    [DllImport(Lib)] public static extern void h_read(uint addr, byte[] buf, uint len);
    // Lit par les MAPPAGES et non par ram[] : memoire video, ROM d'extension.
    // A appeler A LA FIN d'une campagne — voir harness.c, elle laisse une trace
    // dans readlookup2.
    [DllImport(Lib)] public static extern void h_read_phys(uint addr, byte[] buf, uint len);
    [DllImport(Lib)] public static extern void h_fill_ram(byte value);
    [DllImport(Lib)] public static extern void h_fill_ram2(byte a, byte b);
    [DllImport(Lib)] public static extern void h_set_cs_ip(ushort cs, ushort ip);
    [DllImport(Lib)] public static extern void h_setregs(ushort[] r);
    [DllImport(Lib)] public static extern void h_getregs(ushort[] r);
    // A2.0 — quel cœur l'oracle exécute. 0 = 8088 (execx86), 1 = 286 (exec386 avec
    // ops_286). À poser AVANT h_reset : c'est h_reset qui applique AT, et resetx86
    // branche dessus pour le vecteur de reset. Le défaut est 0, donc un appelant qui
    // l'ignore obtient le palier (a) inchangé.
    public const int Core8088 = 0;
    public const int Core286 = 1;
    [DllImport(Lib)] public static extern void h_set_core(int core);
    [DllImport(Lib)] public static extern void h_prefetch_reset();
    [DllImport(Lib)] public static extern void h_seg_clear_residue();
    [DllImport(Lib)] public static extern int h_step_trace();
    [DllImport(Lib)] public static extern int h_wlog_max();
    [DllImport(Lib)] public static extern void h_set_trace_notsc(int on);
    [DllImport(Lib)] public static extern int h_get_core();

    // A2.2a — le chemin de fetch de exec386, porte par porte. fastread* sont des
    // `static inline` de 386_common.h : tools/oracle/harness_fetch.c les instancie
    // dans son unité de traduction, la seule qui puisse inclure cet en-tête.
    [DllImport(Lib)] public static extern uint h_fastreadb(uint a);
    [DllImport(Lib)] public static extern uint h_fastreadw(uint a);
    [DllImport(Lib)] public static extern uint h_fastreadl(uint a);
    [DllImport(Lib)] public static extern uint h_pccache();

    [DllImport(Lib)] public static extern int h_step();
    [DllImport(Lib)] public static extern int h_run(int cycs);
    [DllImport(Lib)] public static extern void h_getstate(out HState s);
    [DllImport(Lib)] public static extern ulong h_ram_hash();
    [DllImport(Lib)] public static extern int h_boot([MarshalAs(UnmanagedType.LPStr)] string romspath);
    [DllImport(Lib)] public static extern void h_runpc();
    [DllImport(Lib)] public static extern int h_trace_open([MarshalAs(UnmanagedType.LPStr)] string path);
    [DllImport(Lib)] public static extern void h_trace_close();
    [DllImport(Lib)] public static extern void h_wlog_reset();
    [DllImport(Lib)] public static extern int h_wlog_count();
    [DllImport(Lib)] public static extern uint h_wlog_get_addr(int i);
    [DllImport(Lib)] public static extern byte h_wlog_get_val(int i);

    /// <summary>
    /// Vérifie que le contrat binaire tient. À appeler avant toute utilisation :
    /// une .so périmée ou un champ ajouté d'un seul côté doit échouer ici,
    /// bruyamment, et pas trois heures plus tard sous forme de divergence.
    /// </summary>
    public static void CheckAbi()
    {
        var v = h_abi_version();
        if (v != AbiVersion)
            throw new InvalidOperationException(
                $"Version d'ABI de l'oracle : {v}, attendu {AbiVersion}. Reconstruire tools/oracle.");

        var native = h_state_size();
        var managed = Marshal.SizeOf<HState>();
        if (native != managed)
            throw new InvalidOperationException(
                $"Taille de h_state : {native} octets côté C, {managed} côté C#. " +
                "Les champs ont divergé — toute comparaison serait silencieusement fausse.");

        // LA BORNE DU JOURNAL EST CONFRONTEE, PAS RECOPIEE. Les deux 16 etaient ecrits
        // en dur de chaque cote et jamais compares : monter un seul des deux donnait un
        // journal TRONQUE d'un cote, donc un vert creux, et rien ne le disait. C'est un
        // changement d'ABI par le COMPORTEMENT, qu'un .so perime ne signale pas — d'ou
        // l'accesseur plutot qu'une constante partagee.
        var natWlog = h_wlog_max();
        if (natWlog != Memory.mem.WLOG_MAX)
            throw new InvalidOperationException(
                $"Borne du journal d'ecritures : {natWlog} cote C, {Memory.mem.WLOG_MAX} cote C#. " +
                "Le journal serait tronque d'un seul cote, et la comparaison rendrait un vert creux.");
    }

    /// <summary>Lit un octet de la RAM de l'oracle.</summary>
    public static byte ReadByte(uint addr)
    {
        var b = new byte[1];
        h_read(addr, b, 1);
        return b[0];
    }

    /// <summary>Écrit un octet dans la RAM de l'oracle.</summary>
    public static void WriteByte(uint addr, byte value) => h_load(addr, [value], 1);
    [DllImport(Lib)]
    internal static extern void h_pit_probe(int t, [Out] ulong[] o);

    // M6 — disquette : image du lecteur A/B (à poser AVANT h_boot), et sonde des
    // globales de disc.c/fdc.c, pendant de Floppy.fdc_c.Probe().
    [DllImport(Lib)] public static extern void h_set_discfn(int drive, [MarshalAs(UnmanagedType.LPStr)] string fn);
    [DllImport(Lib)] public static extern void h_set_mem_size(int kb);
    [DllImport(Lib)] public static extern void h_set_drive_type(int drive, int type);
    [DllImport(Lib)] public static extern void h_set_bpb_disable(int v);

    // M10 — la machine, poussée en SCALAIRE. Le harnais ne lie ni pc.c ni model.c :
    // il n'a pas de models[] à indexer. Côté C# le romset dérive du nom via
    // loadconfig ; ici on pousse la valeur déjà résolue. À appeler avant h_boot.
    [DllImport(Lib)] public static extern void h_set_romset(int r);

    // M11 — de quoi TAPER dans l'oracle, et donc de quoi mettre le chemin d'écriture
    // du contrôleur sous comparaison. h_rawinputkey écrit dans le même tableau que la
    // pompe SDL ; h_kbd_process fait keyboard_poll_host puis keyboard_process, dans
    // l'ordre de runpc(). h_closepc vide les tampons d'écriture sur les images.
    [DllImport(Lib)] public static extern void h_rawinputkey(int idx, int val);
    [DllImport(Lib)] public static extern void h_kbd_process();
    [DllImport(Lib)] public static extern void h_closepc();

    // M12 — disque dur. `drive` est une LETTRE DE LECTEUR DOS (0 = C:, 1 = D:), pas
    // un numéro de contrôleur. À poser avant h_boot : xebec_init lit géométrie et
    // image par hdd_load dès sa construction.
    [DllImport(Lib)] public static extern void h_set_hdd(int drive,
        [MarshalAs(UnmanagedType.LPStr)] string fn, int spt, int hpc, int tracks);
    [DllImport(Lib)] public static extern void h_set_hdd_controller(
        [MarshalAs(UnmanagedType.LPStr)] string name);
    [DllImport(Lib)] internal static extern void h_disc_probe([Out] ulong[] o);

    // M9 — haut-parleur : sonde des globales de sound_speaker.c, pendant de
    // Sound.sound_speaker.Probe(). Le neuvième champ est l'empreinte du son
    // produit — la seule voix du chemin audio dans le diff.
    [DllImport(Lib)] internal static extern void h_speaker_probe([Out] ulong[] o);

    // M15 — vidéo. La carte (GFX_CGA = 0, GFX_VGA = 13, ibm.h:274-289), à poser avant
    // h_boot ; la sonde VGA, pendant de Video.vid_svga.Probe() ; et la VRAM brute,
    // lue sans passer par svga_read — qui mettrait à jour les verrous et facturerait
    // des cycles : lire l'écran changerait la machine.
    public const int GFX_CGA = 0;
    public const int GFX_VGA = 13;
    public const int VgaProbeN = 64;
    [DllImport(Lib)] public static extern void h_set_gfxcard(int g);

    // M16 — le processeur : fabricant et INDICE dans la table de la machine, à poser
    // avant h_boot, qui fait tourner le vrai cpu_set() de PCem avec eux ; le budget de
    // tranche que h_runpc emploie ; et l'empreinte CPU, pendant de
    // CpuFingerprint.Csharp().
    public const int CpuFpN = 48;
    [DllImport(Lib)] public static extern void h_set_cpu(int manu, int n);
    [DllImport(Lib)] public static extern int h_slice_budget();
    [DllImport(Lib)] internal static extern void h_cpu_fingerprint([Out] ulong[] o);
    [DllImport(Lib)] internal static extern void h_vga_probe([Out] ulong[] o);
    [DllImport(Lib)] internal static extern IntPtr h_vga_vram();

}
