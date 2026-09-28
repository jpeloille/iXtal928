// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun)
// STATUS: host

namespace iXtal26.Host.CommandLine;

internal static class UsageText
{
    public static void Print() => StandardOutput.WriteAtOnce(
        $"""
        Usage : iXtal26 [--rom-path CHEMIN] [--floppy-a IMG] [--floppy-b IMG] [--slices N]
                        [--headless] [--verbose] [--turbo [N]]
                        [--monitor auto|nec3v|14|15|17|entier] [--host-diagonal POUCES]
                        [--pixel-mm MM] [--crt] [--fill PCT]
                iXtal26 --boot [CHEMIN] [N] [--floppy-a IMG] [--type TEXTE]...
                iXtal26 --timer-check [CHEMIN] [SECONDES] [--model NOM] [--config FICHIER]...

        Sans argument : ouvre une fenêtre et émule l'IBM PC 5150 jusqu'à sa fermeture.
        Dans la fenêtre, Ctrl+F12 ouvre le menu : insérer ou éjecter une disquette,
        réinitialiser la machine. La disquette en place au moment du reset est celle
        sur laquelle le BIOS amorce.

          --config CHEMIN      fichier de configuration machine (format .cfg de PCem :
                               « clé = valeur », sections [entre crochets], # en
                               commentaire). Clés : model, mem_size, drive_a_type,
                               drive_b_type, disc_a, disc_b, bpb_disable
          --model NOM          machine : ibmpc (IBM PC 5150) ou ibmxt (IBM XT 5160).
                               Un nom inconnu est refusé en citant ce qui existe
          --gfxcard NOM        carte vidéo : cga (défaut), vga (ROM ibm_vga.bin), tvga8900d
                               (trident.bin, 1 Mo) ou tvga9000b (tvga9000b/BIOS.BIN). Même
                               précédence que --model : l'emporte sur la clé gfxcard
          --cpu N              processeur : l'INDICE dans la table de la machine, appliqué
                               après --model. ibmpc/ibmxt : 0 = 8088/4.77 … 5 = 8088/16 ;
                               ibmat : 0 = 286/6, 1 = 286/8 ; ami286 : 0 = 286/6 …
                               5 = 286/20, 6 = 286/25. Hors table : refusé, en listant
                               ce qui existe. Aussi sous --boot et --timer-check
          --ram N              taille RAM en Ko. Les bornes viennent de la MACHINE :
                               64 à 640 par pas de 32 sur ibmpc, par pas de 64 sur ibmxt
          --drive-a T          type de lecteur : 0 aucun, 1 5,25" DD (le 5150),
                               2 5,25" HD, 4 3,5" DD, 5 3,5" HD. --drive-b T de même

          Précédence : défauts, puis --config, puis les options ci-dessus. Sans
          --config aucun fichier n'est lu, et la machine est celle que décrit
          VERIFICATION.md : 640 Ko, deux lecteurs 5,25" DD, CGA.

          --rom-path CHEMIN    où chercher les images de ROM du 5150 (défaut : roms,
                               cherché d'abord depuis le répertoire courant, puis en
                               remontant depuis l'emplacement du binaire)
          --floppy-a IMG       image .img (brute, 160 à 360 Ko sur ce lecteur 5,25" DD)
                               montée dans le lecteur A: avant l'amorçage ; le BIOS
                               démarre dessus. --floppy-b IMG : le lecteur B:.
                               Ouverte en lecture-écriture : DOS y écrit pour de vrai
          --slices N           s'arrête au bout de N tranches de 10 ms émulées, et
                               n'attend pas l'horloge murale entre elles : deux
                               exécutions traversent alors les mêmes états. Ctrl+F12
                               y est inerte, pour ne pas ruiner ce contrat
          --headless           n'initialise aucune vidéo SDL ; exige --slices
          --verbose            en fin d'exécution, compte les blits émis par le CGA
                               et ceux réellement téléversés dans la texture
          --turbo [N]          n'attend pas l'horloge murale pendant les N premières
                               tranches (défaut : {SdlHost.DefaultTurboSlices}, soit juste après l'invite
                               BASIC), puis rend la machine au temps réel. Le POST du
                               5150 dure 57 s, dont 46 s de test mémoire : c'est
                               authentique, et la trajectoire émulée est inchangée —
                               seule la vitesse à laquelle l'hôte la déroule change.
                               Une frappe y met fin. Incompatible avec --slices, qui
                               n'attend déjà jamais l'horloge
          --monitor T          moniteur simulé. nec3v : NEC MultiSync 3V (14" visibles,
                               31-50 kHz, 55-90 Hz ; hors plage, écran noir) ; 14, 15,
                               17 : génériques, 93 % visibles, acceptent tout ; auto
                               (défaut) : le 3V derrière une VGA ou Trident, un 14"
                               derrière la CGA ; entier : pixels entiers, sans moniteur.
                               Toute trame remplit la surface 4:3 du tube, à taille réelle
          --fill PCT           part du tube couverte par l'image, de 70 à 100 (défaut 90) :
                               les molettes H-SIZE/V-SIZE. Sans effet en pixels entiers
          --host-diagonal P    diagonale de l'écran hôte en pouces (ex. 27) : la taille
                               d'un pixel en découle, avec la résolution native que
                               donne SDL
          --pixel-mm MM        taille d'un pixel de l'écran hôte, en mm, qui l'emporte
                               sur --host-diagonal (défaut 0.2331 ; 0.27 pour un 24"
                               Full HD, 0.16 pour un 27" 4K)
          --crt                lignes de balayage, dessinées s'il y a au moins deux
                               pixels hôte par ligne émulée
                               Moniteur, lignes et filtrage se règlent au menu Ctrl+F12, et
                               s'enregistrent dans [SDL2] quand la config est dans
                               configs/. Affichage seul : l'image émulée n'est pas touchée
          --boot [CHEMIN] [N]  amorce et raconte en console ce que le POST a écrit
                               en mémoire et à l'écran (défauts : roms, 20 tranches)
              --hdd IMG        après --boot : monte un disque dur, comme l'option
                               principale. --hdd-type N et --hdd-controller NOM la
                               complètent
              --type TEXTE     après --boot : tape TEXTE puis Entrée dans la machine,
                               et revide l'écran. Répétable, dans l'ordre. C'est la
                               seule vérification du chemin clavier qui ne dépende
                               pas d'une fenêtre ayant le focus
          --hdd IMG            monte une image de disque dur EXISTANTE en C:, géométrie
                               déduite de sa taille. --hdd-d IMG : le disque D:. Une
                               taille qui ne correspond à aucun des 46 types du BIOS
                               est refusée. Pose le contrôleur de la machine (mfm_at
                               sur un AT, mfm_xebec ailleurs) si rien n'en a nommé, et
                               l'emporte sur les clés hdc_*/hdd_*
          --hdd-controller NOM contrôleur de disque dur : mfm_xebec (IBM Fixed Disk
                               Adapter), dtc5150x (DTC 5150X), ou mfm_at (IBM AT, sur
                               les seules machines AT). Même précédence que --gfxcard :
                               l'emporte sur la clé hdd_controller
          --hdd-type N         force le type de disque de C: quand sa taille en désigne
                               plusieurs — 21 307 392 octets, c'est le type 13 (306x8)
                               ou le type 16 (612x4). --hdd-d-type N : le disque D:
          --create-hdd [TYPE|CYL,TETES,SECT] [CHEMIN]
                               fabrique une image de disque dur VIERGE — des zéros, à
                               la taille exacte de la géométrie — puis imprime les
                               clés à coller dans un .cfg. Sans argument : liste les
                               46 types de disque du BIOS et sort sans rien créer.
                               TYPE va de 1 à 46 ; CYL,TETES,SECT est la saisie libre,
                               bornée comme chez PCem (secteurs 63, têtes 16). Sans
                               CHEMIN, écrit le premier nom libre dans os/. Un fichier
                               existant est refusé, jamais écrasé
          --create-floppy [FORMAT] [CHEMIN]
                               fabrique une image de disquette FORMATÉE en FAT12, vide
                               et non système : secteur d'amorce, BPB, deux FAT,
                               répertoire racine. Contrairement au « Creer une
                               disquette vierge » du menu, qui écrit des zéros que DOS
                               ne sait pas lire tant que FORMAT n'est pas passé DANS la
                               machine. Sans argument : liste les quatre formats et sort
                               sans rien créer. FORMAT vaut 160k, 180k, 320k ou 360k —
                               les seuls que le lecteur 5,25" DD du 5150 sait lire.
                               Sans CHEMIN, écrit le premier nom libre dans os/. Un
                               fichier existant est refusé, jamais écrasé
          --floppy-put IMAGE FICHIER...
                               dépose des fichiers du disque hôte dans la racine d'une
                               image formatée. Les noms doivent tenir en 8.3 : un nom
                               trop long est REFUSÉ, jamais tronqué — tronquer
                               fabriquerait un doublon silencieux. Refuse aussi une
                               image montée dans A: ou B:, qu'il faut éjecter d'abord
          --fat-check          auto-contrôle du formateur : l'empaquetage FAT12 contre
                               la FAT d'une disquette réellement formatée par DOS 2.00,
                               le secteur d'amorce, et l'invariant de géométrie — les
                               deux branches d'img_load doivent lire la même chose
          --speaker-check [DOSSIER]
                               auto-contrôle du haut-parleur « réglage d'usine » : le
                               bip du POST mesuré avant et après le modèle du cône.
                               Avec DOSSIER, y écrit les deux versions en WAV
          --timer-check [CHEMIN] [SECONDES] [--model NOM] [--config FICHIER] [--cpu N]
                        [--gfxcard NOM] [--floppy-a IMG] [--floppy-b IMG] [--boot-slices N]
                        [--charge repos|ram]
                               amorce, vérifie que l'INT 8 du BIOS tourne, puis
                               compte les tops de la BDA (0040:006C) sur SECONDES
                               secondes ÉMULÉES et compare à 1193182/65536 =
                               18,2065 Hz (défauts : roms, 300 s). Imprime aussi la
                               CADENCE : fréquence du processeur vue par l'invité,
                               vitesse du temps invité, et marge de l'hôte
          --setup              ouvre l'écran de construction de machine avant de
                               démarrer : modèle, mémoire, disquettes, disques durs,
                               charger ou enregistrer une machine de configs/. Il
                               s'ouvre DÉJÀ tout seul sur un lancement sans argument ;
                               jamais sous --slices, --headless ni --boot
          --setup-check        auto-contrôle de l'écran de construction, sans fenêtre :
                               navigation, bascules d'écran, recalage de la liste. Ce
                               code n'a aucun autre moyen d'être exécuté ici
          -h, --help           affiche cette aide

        Codes de sortie : 0 succès, 1 échec d'exécution, 2 erreur d'usage.

        """);
}
