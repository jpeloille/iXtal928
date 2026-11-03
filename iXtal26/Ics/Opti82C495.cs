// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: pcem-dev/src/models/opti495.c
// SHA256: voir oracle.tsv ; vérifier avec tools/check-oracle.sh
// STATUS: transcribed — G3, le chipset de l'AMI 386DX. Seul le printf de trace des
//         écritures est omis.
//
// noms: PCem → iXtal26 → ce qu'il désigne — R1(e). « = » : nom inchangé.
//   opti495.c   Ics/Opti82C495.cs   le fichier ; avant G14, Models/opti495.cs
//   (aucun)     Opti82C495          le conteneur, le jeu de puces de l'ami386dx ; avant G14, la classe `opti495`
//                                   (iXtal26.Models). OPTi 82C495, que PCem nomme (opti495.c:1). La ROM de
//                                   l'ami386dx, opt495sx.ami (« This option ONLY valid for 495 Rev. B. »), vise par
//                                   son nom le 82C495SX, dont aucune fiche publique n'a été trouvée ; le suffixe tombe
//                                   (PLAN-G14.md, décision n° 3). Deux fiches d'OPTi de la famille décrivent le code
//                                   à l'identique : le 82C495XLC (sources/ O1, p. 25-26 du PDF) et le 82C493 que
//                                   PCem décrit (O2, p. 19-21) — l'index en 22h, les données en 24h, le bit 4 de
//                                   21h (le cache), le bit 7 de 22h (la ROM de F0000h, à 1 lue en ROM et écrite en
//                                   RAM, à 0 lue en RAM protégée). O1 documente 20h à 29h (O2 jusqu'à 2Bh, p. 28) et
//                                   pose 22h à E4h (p. 26) ; le code mémorise 20h à 2Ch et pose 80h : les autres bits
//                                   ne sont pas modélisés
//   Les FONCTIONS et les globales gardent les noms de PCem (opti495_init, optiregs…) : elles sont l'API entre
//   modules (fdc.cs:47 ; PLAN-G14.md, décision n° 1).
//
// LE CHIPSET OPTi 82C495 DE L'AMI 386DX, et le commentaire d'origine le dit (:1-2) :
// « This is the chipset used in the AMI386 model ». Toujours le même motif que le NEAT
// de l'ami286 et le Headland de l'ami386SX : un port d'index (0x22) et un port de
// données — mais ici 0x24, pas 0x23. Les registres vivent de 0x20 à 0x2C ; hors de
// cette plage, les écritures sont perdues et les lectures rendent 0xFF.
//
// DEUX REGISTRES SEULEMENT FONT QUELQUE CHOSE, les onze autres sont mémorisés et relus :
//   - 0x21, bit 4 : le cache externe. Il passe dans cpu_cache_ext_enabled, et
//     cpu_update_waitstates() recalcule les états d'attente ;
//   - 0x22, bit 7 : la ROM de F0000h-FFFFFh. À 1 (la valeur de mise sous tension,
//     posée par opti495_init), lecture en ROM et écriture en RAM ; à 0, lecture dans
//     la RAM d'ombre protégée en écriture. C'est l'ombre du BIOS, rien de plus : les
//     zones C0000h-EFFFFh (bits 6-3 de 0x22, registres 0x23 et 0x26) ne sont pas
//     modélisées.
//
// LA DESCRIPTION DES REGISTRES (:52-302) est celle du 82C493 selon la liste
// d'interruptions de Ralf Brown, que PCem garde en commentaire ; elle n'est pas
// recopiée ici, le fichier C fait foi.

using static iXtal26.io;
using static iXtal26.Memory.mem;

namespace iXtal26.Ics;

internal static class Opti82C495
{
    // pcem: opti495.c:8-9
    private static readonly uint8_t[] optiregs = new uint8_t[0x10];
    private static int optireg;

    // pcem: opti495.c:11-33
    private static void opti495_write(uint16_t addr, uint8_t val, object? p)
    {
        switch (addr)
        {
        case 0x22:
                optireg = val;
                break;
        case 0x24:
                // omitted: printf("Writing OPTI reg %02X %02X\n", optireg, val) (:17) — sortie
                //   pure, à chaque écriture de registre.
                if (optireg >= 0x20 && optireg <= 0x2C)
                {
                        optiregs[optireg - 0x20] = val;
                        if (optireg == 0x21)
                        {
                                Cpu.cpu_c.cpu_cache_ext_enabled = val & 0x10;
                                Cpu.cpu_c.cpu_update_waitstates();
                        }
                        if (optireg == 0x22)
                        {
                                if ((val & 0x80) == 0)
                                        mem_set_mem_state(0xf0000, 0x10000, MEM_READ_INTERNAL | MEM_WRITE_DISABLED);
                                else
                                        mem_set_mem_state(0xf0000, 0x10000, MEM_READ_EXTERNAL | MEM_WRITE_INTERNAL);
                        }
                }
                break;
        }
    }

    // pcem: opti495.c:35-44
    private static uint8_t opti495_read(uint16_t addr, object? p)
    {
        switch (addr)
        {
        case 0x24:
                // omitted: printf("Read OPTI reg %02X\n",optireg) (:38) commenté dans le C.
                if (optireg >= 0x20 && optireg <= 0x2C)
                        return optiregs[optireg - 0x20];
                break;
        }
        return 0xFF;
    }

    // pcem: opti495.c:46-50
    internal static void opti495_init()
    {
        io_sethandler(0x0022, 0x0001, opti495_read, null, null, opti495_write, null, null, null);
        io_sethandler(0x0024, 0x0001, opti495_read, null, null, opti495_write, null, null, null);
        optiregs[0x22 - 0x20] = 0x80;
    }
}
