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

/// <summary>L'accélération du 4 octobre : vrai dès que la classe Oracle est initialisée, donc la .so
/// chargée et ses chemins de CMOS posés. Hors de la classe, pour que la lire n'initialise pas Oracle :
/// la vérification de sortie (Program.cs) n'interroge l'oracle que s'il a servi.</summary>
internal static class OracleEtat
{
    internal static bool Charge;
}

public static class Oracle
{
    internal const string Lib = "ixtal26oracle";
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
    // 19 a M19 : les Trident. h_vga_probe passe de 64 a 86 champs, la temporisation
    // suit la carte (video_speed = -1) et video_is_* lisent ses drapeaux.
    // 20 a M21 : COM1, COM2 et la souris serie Microsoft entrent dans l'oracle ;
    // h_mouse_poll s'ajoute au contrat.
    // 21 en G2, D0.1 : cr4 et dr[8] entrent dans h_state. Le vecteur change de taille.
    // 22 en G2, D0.2 : h_set_core accepte 2, le coeur 386. Le vecteur ne change pas.
    // 23 en G2, D0.4 : h_setregs386.
    // 24 en G2, D0.5 : h_setsys386 et h_flags_rebuild.
    // 25 en G2, D6 : h_mmutranslate et h_mmu_perm, pour page-check. h_state ne change pas.
    // 26 en G3.0 : h_set_nvr_paths — l'oracle lit le même CMOS que le C#.
    // 27 en G4.0 : l'état x87 entre dans h_state (le vecteur change de TAILLE) ; h_set_fpu,
    // h_setfpu et les sondes de parité h_libm, h_conv, h_fpu_arith s'ajoutent.
    // 28 en G5.0 : « mfm_at » monte enfin une carte côté oracle (comportement, pas forme).
    // 29 en G6.0 : h_set_core accepte le 486 (l'ami486).
    // 30 en G7.1 : la GD5429 (gfxcard 19) ; la sonde VGA passe de 86 à 102 champs.
    // 31 en G7.3 : la Trio64 Phoenix (gfxcard 22) ; la sonde VGA passe de 102 à 122 champs.
    // 32 en G1.0 : h_set_core accepte le 8086 (l'Olivetti M24, cpus_8086).
    // 33 en G1.1 : l'Olivetti M24 s'amorce ; la sonde VGA passe de 122 à 143 champs (la M24).
    // 34 en G1.2 : l'Amstrad PC1512 s'amorce ; la sonde VGA passe à 163 champs (le PC1512).
    // 35 en G8.0 : h_opl_tables (DBOPL, compilé en C++).
    // 36 en G8.1 : h_set_sndcard, h_opl_reset, h_sound_probe (21 champs).
    // 37 en G8.2 : la SB Pro v2 (sbprov2) ; la sonde du son passe à 41 champs (DSP, mélangeur).
    // 38 en G8.3 : h_clear_device_config, h_set_device_config (les sections de device du .cfg).
    // 39 en PS2.0 : h_set_mouse_type, h_mouse_probe ; h_mouse_poll pose mouse_buttons.
    // 40 en G9.0 : la MDA et sa sonde (champ 0 = 4).
    // 41 en G9.1 : l'Hercules et sa sonde (champ 0 = 5).
    // 42 en G9.2 : l'EGA et sa sonde (champ 0 = 6).
    // 43 en G9.3 : la Tseng ET4000AX.
    // 44 en G10.0 : LPT1/LPT2 posés, le port jeu sur xt_init/at_init, h_set_lpt1_device,
    //   h_set_lpt_jeu_hors_service.
    // 45 le 04/10 (outils) : h_trace_errno, l'écriture refusée de la trace.
    // 46 le 04/10 (l'accélération) : h_trace_hash_value, h_raz_fin, h_mem_size, h_ram_cmp.
    // 47 en G10.1 : h_set_joystick_type, h_joy_set (la manette).
    // 48 en G10.2 : le XTIDE (hdd_controller « xtide », xtide.c lié).
    public const int AbiVersion = 48;

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

        // G3.0 — LE CMOS DES DEUX CÔTÉS, poussé UNE fois pour tous les outils (boot-diff,
        // vga-probe, at-probe, cpu-config-check, bench…) : les deux chemins vivent dans des
        // tableaux statiques de la .so qu'aucun reset du harnais ne touche. Même résolution
        // que initpc (pc.cs:538-543). Sans elle, l'oracle composait ses chemins sur deux
        // chaînes vides et lisait un CMOS à 0xFF là où le C# lisait nvr/default/*.nvr —
        // mesuré : boot-diff --model ibmat divergeait à l'instruction 50, IN AL,71h.
        h_set_nvr_paths(
                PluginApi.config.append_slash(
                        PluginApi.paths.resolve_roms_path("nvr") is { Length: > 0 } nvr ? nvr : "nvr", 512),
                PluginApi.config.append_slash(
                        PluginApi.paths.resolve_roms_path("nvr/default") is { Length: > 0 } d ? d : "nvr/default", 512));
        OracleEtat.Charge = true;
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
    // G2, D0.4 — moitiés hautes, mot haut d'EFLAGS, FS et GS ; après h_setregs.
    [DllImport(Lib)] public static extern void h_setregs386(ushort[] hi, ushort eflags, ushort fs, ushort gs);
    // G2, D0.5 — cr0, cr3, dr6, dr7, tels qu'un cas SingleStepTests/80386 les pose.
    [DllImport(Lib)] public static extern void h_setsys386(uint cr0, uint cr3, uint dr6, uint dr7);
    [DllImport(Lib)] public static extern uint h_mmutranslate(uint addr, int rw, int cpl, int cplOverride, int abrtIn);
    [DllImport(Lib)] public static extern int h_mmu_perm();
    [DllImport(Lib)] public static extern void h_flags_rebuild();
    [DllImport(Lib)] public static extern void h_getregs(ushort[] r);
    // A2.0 — quel cœur l'oracle exécute. 0 = 8088 (execx86), 1 = 286 (exec386 avec
    // ops_286). À poser AVANT h_reset : c'est h_reset qui applique AT, et resetx86
    // branche dessus pour le vecteur de reset. Le défaut est 0, donc un appelant qui
    // l'ignore obtient le palier (a) inchangé.
    public const int Core8088 = 0;
    public const int Core286 = 1;
    // G2, D0.2 : 2 = 386, le MÊME exec386 sur l'ami386 (ops_386, is386, temps du 386).
    public const int Core386 = 2;
    // G6.0 : 3 = 486, toujours exec386 — PCem n'a pas de table d'opcodes 486 — sur l'ami486.
    public const int Core486 = 3;
    // G1.0 : 4 = 8086, le MÊME execx86 que le 8088, sur l'Olivetti M24 (cpus_8086) — fuzzeur
    // seulement ; une machine 8086 s'amorce par Core8088 (CoreForModel), cpu_set y pose is8086.
    public const int Core8086 = 4;

    /// <summary>Le 286 et le 386 empruntent le même exec386 : c'est ce prédicat, pas
    /// `core == Core286`, qui choisit Step286 contre _808x.Step — pendant de
    /// h_exec386() côté oracle.</summary>
    public static bool Exec386(int core) => core is Core286 or Core386 or Core486;

    /// <summary>G1.0 — le cœur a-t-il l'état 32 bits du 386 (Seed386, --0f) ? Un prédicat et
    /// non `core >= Core386` : Core8086 vaut 4, plus que Core386, et n'est qu'un execx86.</summary>
    public static bool Is386Class(int core) => core is Core386 or Core486;

    /// <summary>Le cœur d'une machine : 8088 hors AT ; sur un AT, le type de la
    /// première entrée de sa table de CPU. Un seul endroit, pour BootDiff, VgaProbe et
    /// CpuConfigCheck, qui le recopiaient chacun sur MODEL_AT seul.</summary>
    internal static int CoreForModel(iXtal26.Models.MODEL m)
        => (m.flags & iXtal26.Models.model_c.MODEL_AT) == 0 ? Core8088
         : m.cpu[0].cpus![0].cpu_type >= iXtal26.Cpu.cpu_c.CPU_i486SX ? Core486
         : m.cpu[0].cpus![0].cpu_type >= iXtal26.Cpu.cpu_c.CPU_386SX ? Core386 : Core286;
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
    // Le premier errno d'une écriture refusée de la trace, 0 si tout est passé (après h_trace_close).
    [DllImport(Lib)] public static extern int h_trace_errno();
    // L'accélération du 4 octobre. Le hachage de trace d'un état donné, plié (ref = 0) ou par
    // l'ancien MIX (ref = 1) : trace-hash-check.
    [DllImport(Lib)] public static extern ulong h_trace_hash_value(int @ref, ushort cs, uint pc, ushort[] regs,
                                                                    ushort ds, ushort es, ushort ss, ushort flags,
                                                                    ulong tsc, int notsc);
    // À la sortie : un dernier vidage des tables de traduction par l'anneau, puis leur balayage ;
    // le nombre d'écarts, -1 si le processus n'a pris aucune remise courte.
    [DllImport(Lib)] public static extern int h_raz_fin();
    // La RAM comparée octet par octet : mem_size de l'oracle, puis -1 si les n premiers octets
    // sont égaux, -2 si n dépasse la RAM de l'oracle, et sinon le premier décalage différent,
    // l'octet de l'oracle à ce décalage dans `octet`. Le tableau C# est épinglé par le
    // marshalling, sans copie.
    [DllImport(Lib)] public static extern int h_mem_size();
    [DllImport(Lib)] public static extern long h_ram_cmp(byte[] autre, uint n, out byte octet);
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
    [DllImport(Lib, CharSet = CharSet.Ansi)] public static extern void h_set_nvr_paths(string nvr, string nvrDefault);

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
    // G8.1 — la carte son (internal_name) et la sonde du son : haut-parleur, puis les deux OPL.
    [DllImport(Lib)] internal static extern void h_set_sndcard(string name);
    // G10.0 — le périphérique de LPT1 (internal_name : none, dss, lpt_dac, lpt_dac_stereo).
    [DllImport(Lib)] internal static extern void h_set_lpt1_device(string name);
    [DllImport(Lib)] internal static extern void h_set_lpt_jeu_hors_service(int on);
    // G8.3 — les sections de device du .cfg, que config_get_int/string de l'oracle consultent.
    [DllImport(Lib)] internal static extern void h_clear_device_config();
    [DllImport(Lib)] internal static extern void h_set_device_config(string head, string name, string data);
    [DllImport(Lib)] internal static extern void h_sound_probe([Out] ulong[] o);
    public const int SoundProbeN = 41;

    // M15 — vidéo. La carte (GFX_CGA = 0, GFX_VGA = 13, ibm.h:274-289), à poser avant
    // h_boot ; la sonde VGA, pendant de Video.vid_svga.Probe() ; et la VRAM brute,
    // lue sans passer par svga_read — qui mettrait à jour les verrous et facturerait
    // des cycles : lire l'écran changerait la machine.
    public const int GFX_CGA = 0;
    public const int GFX_TVGA = 4;        // ibm.h:280, la 8900D
    public const int GFX_VGA = 13;
    public const int GFX_CL_GD5429 = 19;  // ibm.h:295
    public const int GFX_PHOENIX_TRIO64 = 22;  // ibm.h:298
    public const int GFX_TVGA9000B = 42;  // ibm.h:318
    public const int VgaProbeN = 163;
    [DllImport(Lib)] public static extern void h_set_gfxcard(int g);
    [DllImport(Lib)] public static extern void h_mouse_poll(int x, int y, int z, int b);
    // PS2.0 — la souris de mouse_list (mouse.c lié), et la sonde de la souris PS/2.
    [DllImport(Lib)] internal static extern void h_set_mouse_type(int t);
    [DllImport(Lib)] internal static extern void h_mouse_probe([Out] ulong[] o);
    // G10.1 — la manette : le type, avant h_boot ; l'état de la manette n (branchée si nr ≠ 0,
    // axes 0 à 2, les 32 boutons en masque, chapeau 0), en fin de tranche.
    [DllImport(Lib)] internal static extern void h_set_joystick_type(int t);
    [DllImport(Lib)] internal static extern void h_joy_set(int n, int nr, int x, int y, int z, uint boutons, int pov);
    public const int MouseProbeN = 9;

    // M16 — le processeur : fabricant et INDICE dans la table de la machine, à poser
    // avant h_boot, qui fait tourner le vrai cpu_set() de PCem avec eux ; le budget de
    // tranche que h_runpc emploie ; et l'empreinte CPU, pendant de
    // CpuFingerprint.Csharp().
    public const int CpuFpN = 48;
    [DllImport(Lib)] public static extern void h_set_cpu(int manu, int n);
    // G4.0 — le coprocesseur (FPU_*, cpu.h:72), avant h_reset ou h_boot ; et l'état x87
    // tiré par le fuzzeur, ST en bits bruts.
    [DllImport(Lib)] public static extern void h_set_fpu(int type);
    [DllImport(Lib)] public static extern void h_setfpu(ulong[] st, ulong[] mm, ushort[] mmW4, byte[] tag,
                                                        int top, ushort npxs, ushort npxc);
    // G4.0 — les sondes de parité de harness_x87.c (voir X87Parity.cs).
    [DllImport(Lib)] public static extern ulong h_libm(int n, ulong a, ulong b);
    [DllImport(Lib)] public static extern ulong h_conv(int n, ulong a);
    [DllImport(Lib)] public static extern ulong h_fpu_arith(int op, int mode, ulong a, ulong b);
    [DllImport(Lib)] public static extern int h_slice_budget();
    [DllImport(Lib)] internal static extern void h_cpu_fingerprint([Out] ulong[] o);
    [DllImport(Lib)] internal static extern void h_vga_probe([Out] ulong[] o);
    [DllImport(Lib)] internal static extern IntPtr h_vga_vram();

}
