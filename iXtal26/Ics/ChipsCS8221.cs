// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/models/neat.c
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: partial — les deux ports de configuration et les quatre pages EMS ;
//         neat_writeems et neat_readems omises, sans appelant dans l'arbre lié.
//
// noms: PCem → iXtal26 → ce qu'il désigne — R1(e). « = » : nom inchangé.
//   neat.c      Ics/ChipsCS8221.cs  le fichier ; avant G14, Models/neat.cs
//   (aucun)     ChipsCS8221         le conteneur, le jeu de puces de l'ami286 ; avant G14, la classe `neat`
//                                   (iXtal26.Models). Le NEAT de Chips and Technologies, CS8221 : 82C211, 82C212,
//                                   82C215 et 82C206 (sources/ CT1, p. 1-2 du PDF). La fiche confirme l'index en 22h
//                                   et les données en 23h (82C211, p. 29 ; 82C212, p. 46), RB10 « EMS Address
//                                   Extension » en 6Eh, deux bits par page, la page 3 dans les bits 1-0 et la page 0
//                                   dans les bits 7-6 (p. 51-52), les registres de page en 208h/209h, la base que RB9
//                                   donne au code 0000 (p. 51). Elle ne dit ni le choix de la page par A15-A14 (0208h,
//                                   4208h, 8208h, C208h) ni le format des registres de page ; elle lit les bits
//                                   d'extension comme des blocs (1 à 2 Mo … 6 à 8 Mo, p. 51), PCem comme les bits 7-8
//                                   du numéro de page, sans effet ici : aucune fenêtre EMS n'est posée
//   Les FONCTIONS et les globales gardent les noms de PCem (neat_init, neat_regs…) : elles sont l'API entre modules
//   (fdc.cs:47 ; PLAN-G14.md, décision n° 1).
//
// LE CHIPSET DE L'AMI 286, ET SON COMMENTAIRE D'ORIGINE LE DIT : « This is the
// chipset used in the AMI 286 clone model ». Chips & Technologies NEAT, le jeu de
// puces qui a rendu les clones d'AT petits et bon marché en 1988.
//
// DEUX PORTS ET UN LOQUET, comme le MC146818 et le 8042 : 0x22 choisit le registre,
// 0x23 agit dessus. C'est le motif de toute cette génération de matériel — un
// espace d'adressage d'E/S de dix bits ne laissait pas la place d'exposer deux cent
// cinquante-six registres.
//
// ET LE SEUL REGISTRE QUI FASSE QUELQUE CHOSE ICI EST 0x6E. Les 255 autres sont
// mémorisés et relus sans effet : le BIOS y range sa configuration de cadencement
// mémoire et de fenêtre ROM, que cette émulation ne modélise pas. Ce n'est pas une
// omission — PCem les mémorise aussi, et c'est ce que le BIOS attend : il écrit,
// relit, et compare.
//
// 0x6E PORTE LES BITS HAUTS DES QUATRE PAGES EMS, deux bits chacune, EN ORDRE
// INVERSE — la page 3 dans les bits 0-1, la page 0 dans les bits 6-7. Les sept bits
// bas viennent des ports 0x208/0x4208/0x8208/0xC208, dont l'adresse EST le numéro
// de page : `port >> 14` rend 0, 1, 2, 3. Neuf bits par page, donc 512 pages de
// 16 Ko, soit huit mégaoctets d'EMS.

using static iXtal26.io;

namespace iXtal26.Ics;

internal static class ChipsCS8221
{
    // pcem: neat.c:6-8
    private static readonly uint8_t[] neat_regs = new uint8_t[256];
    private static int neat_index;
    private static readonly int[] neat_emspage = new int[4];

    // pcem: neat.c:10-38
    internal static void neat_write(uint16_t port, uint8_t val, object? priv)
    {
        switch (port)
        {
        case 0x22:
                neat_index = val;
                break;

        case 0x23:
                neat_regs[neat_index] = val;
                switch (neat_index)
                {
                case 0x6E: /*EMS page extension*/
                        neat_emspage[3] = (neat_emspage[3] & 0x7F) | ((val & 3) << 7);
                        neat_emspage[2] = (neat_emspage[2] & 0x7F) | (((val >> 2) & 3) << 7);
                        neat_emspage[1] = (neat_emspage[1] & 0x7F) | (((val >> 4) & 3) << 7);
                        neat_emspage[0] = (neat_emspage[0] & 0x7F) | (((val >> 6) & 3) << 7);
                        break;
                }
                break;

        case 0x0208:
        case 0x0209:
        case 0x4208:
        case 0x4209:
        case 0x8208:
        case 0x8209:
        case 0xC208:
        case 0xC209:
                neat_emspage[port >> 14] = (neat_emspage[port >> 14] & 0x180) | (val & 0x7F);
                break;
        }
    }

    // pcem: neat.c:40-49
    internal static uint8_t neat_read(uint16_t port, object? priv)
    {
        switch (port)
        {
        case 0x22:
                return (uint8_t)neat_index;

        case 0x23:
                return neat_regs[neat_index];
        }
        return 0xff;
    }

    // omitted: neat_writeems / neat_readems (neat.c:51-53) — SANS APPELANT dans
    //   l'arbre lie, et ce n'est pas une supposition : neat_init n'enregistre AUCUN
    //   mappage memoire, donc rien ne route une adresse vers ces deux fonctions.
    //   PCem a cable les ports de pagination EMS sans cabler la fenetre qu'ils
    //   paginent. Le BIOS de l'AMI 286 ecrit dans les ports et ne voit jamais la
    //   difference — il n'y a pas d'EMS a trouver.
    //   Verifie : `grep -rn 'neat_readems\|neat_writeems' pcem-dev/src` ne rend que
    //   leurs definitions et le prototype de neat.h.

    // pcem: neat.c:55-61
    internal static void neat_init()
    {
        io_sethandler(0x0022, 0x0002, neat_read, null, null, neat_write, null, null, null);
        io_sethandler(0x0208, 0x0002, neat_read, null, null, neat_write, null, null, null);
        io_sethandler(0x4208, 0x0002, neat_read, null, null, neat_write, null, null, null);
        io_sethandler(0x8208, 0x0002, neat_read, null, null, neat_write, null, null, null);
        io_sethandler(0xc208, 0x0002, neat_read, null, null, neat_write, null, null, null);
    }
}
