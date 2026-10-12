// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel du disque IDE (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans ide.c, appelées par les gardes de ide.cs (`if (materiel.pb_nn)`), seules sur
// leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans l'entrée PB de PCEM_BUGS.md. La
// référence : ATA-1, brouillon X3T9.2 791D révision 4 (17 mars 1993).

// CS8981 : `ide` n'a que des minuscules ASCII — le nom de l'unité C (ide.cs).
#pragma warning disable CS8981

using static iXtal26.Models.pic;
using static iXtal26.pc;

namespace iXtal26.Ide;

internal static partial class ide
{
    // L'état que PCem n'a pas, dans une classe à part : un champ statique de plus dans `ide` changerait son
    // constructeur statique, et le code que le JIT produit pour les méthodes qui le lisent, en mode PCem aussi.
    private static class EtatIde
    {
        /// <summary>Le fil INTRQ de chaque ligne d'interruption, tel que le 8259 le voit : 0, l'IRQ 14 ; 1, l'IRQ 15.</summary>
        internal static readonly bool[] ligne = new bool[2];
    }

    // pcem bug, fixed in hardware mode: PB-216 — RESET- négue INTRQ (ATA-1 r4 § 6.3.10) : au reset de l'IDE, aucune
    //   unité n'a d'interruption en attente, et les deux fils partent bas. PCem garde irqstat d'une machine à l'autre.
    private static void ide_raz_materiel()
    {
        Array.Clear(EtatIde.ligne);
        foreach (var d in ide_drives)
                d.irqstat = 0;
    }

    private static int ligne_de(IDE ide) => ide.board != 0 ? 1 : 0;

    // pcem bug, fixed in hardware mode: PB-216 — ide_irq_raise a levé la ligne (un front, picint), ide_irq_lower l'a
    //   baissée (picintc) : l'état du fil suit, pour qu'ide_irq_update ne refasse pas un front sur une ligne déjà haute.
    private static void ide_ligne_materiel(IDE ide, bool haute) => EtatIde.ligne[ligne_de(ide)] = haute;

    // pcem bug, fixed in hardware mode: PB-71, PB-216 — INTRQ est actif tant que l'unité choisie a une interruption en
    //   attente et que nIEN vaut 0 (ATA-1 r4 § 6.3.10) ; il ne dépend que de l'unité choisie de son canal, et chaque
    //   canal a sa ligne (l'IRQ 14 pour le primaire, l'IRQ 15 pour le secondaire). Le 8259A de l'AT est déclenché par
    //   front : seul un front montant pose une demande (picint) ; la ligne qui retombe la retire (picintc) ; une ligne
    //   restée haute n'en pose pas de seconde, même après l'EOI (déduit de la fiche du 8259A, ICW1, LTIM = 0). PCem
    //   testait le bit de l'IRQ 14 pour les deux canaux (PB-71), baissait la ligne que l'unité tenait encore et en
    //   reposait une à chaque appel (PB-216). Appelée à chaque écriture de 1F6h (l'unité choisie) et de 3F6h (nIEN, et
    //   SRST : « INTRQ shall be negated by … SRST », § 6.3.10 — le reset logiciel efface l'interruption en attente des
    //   deux unités du canal, que PCem garde). Une unité choisie de nouveau, son interruption toujours en attente,
    //   relève le fil : un front, lecture littérale d'ATA-1 (l'unité non choisie laisse INTRQ en haute impédance).
    private static bool ide_irq_update_materiel(IDE ide)
    {
        var l = ligne_de(ide);
        if ((ide.fdisk & 4) != 0)
        {
                ide_drives[l * 2].irqstat = 0;
                ide_drives[l * 2 + 1].irqstat = 0;
        }
        var haute = ide.irqstat != 0 && (ide.fdisk & 2) == 0;
        var irq = (uint16_t)(l != 0 ? 1 << 15 : 1 << 14);
        if (haute && !EtatIde.ligne[l])
                picint(irq);
        else if (!haute && EtatIde.ligne[l])
                picintc(irq);
        EtatIde.ligne[l] = haute;
        ModeMateriel.Sonde[71]++;
        ModeMateriel.Sonde[216]++;
        return true;
    }

    // pcem bug, fixed in hardware mode: PB-74 — READ VERIFY SECTOR(S) (40h, 41h) vérifie les secteurs que compte le
    //   registre de compte (0 : 256), sans transfert ; en fin normale, « the Command Block Registers contain the
    //   cylinder, head, and sector number of the last sector verified » et le compte est nul (ATA-1 r4 § 9.19, § 7.2.11) ;
    //   en erreur, l'adresse du secteur fautif et « the number of sectors not yet verified ». Un secteur hors du disque
    //   (au-delà de sa capacité, ou une adresse CHS hors de sa géométrie) ne présente pas de champ ID : IDNF (10h), comme
    //   READ SECTOR(S) (§ 9.17). PCem rend la main au premier rappel, l'adresse et le compte intacts. Le registre de
    //   compte a huit bits : 256 secteurs restants s'y écrivent 0, comme le compte de la commande.
    private static bool ide_verify_materiel(IDE ide)
    {
        var reste = ide.secount != 0 ? ide.secount : 256;
        ModeMateriel.Sonde[74]++;
        for (;;)
        {
                if (ide_hors_disque_materiel(ide))
                {
                        ide.secount = reste & 0xff;
                        ide_registres_lba_materiel(ide);
                        ide.error = IDNF_ERR_MATERIEL;
                        ide.atastat = READY_STAT | DSC_STAT | ERR_STAT;
                        ide.pos = 0;
                        ide_irq_raise(ide);
                        readflash_set(READFLASH_HDC, ide.drive);
                        return true;
                }
                if (--reste == 0)
                        break;
                ide_next_sector(ide);
        }
        ide.secount = 0;
        ide_registres_lba_materiel(ide);
        ide.pos = 0;
        ide.atastat = READY_STAT | DSC_STAT;
        ide_irq_raise(ide);
        readflash_set(READFLASH_HDC, ide.drive);
        return true;
    }

    /// <summary>En LBA, l'adresse rendue par les registres de secteur (bits 0-7), de cylindre (8-23) et de tête (24-27)
    /// (ATA-1 r4 § 7.2.9 à 7.2.12) : PCem n'avance que lba_addr, que ces registres ne relisent pas.</summary>
    private static void ide_registres_lba_materiel(IDE ide)
    {
        if (ide.lba == 0)
                return;
        ide.sector = (int)(ide.lba_addr & 0xff);
        ide.cylinder = (int)((ide.lba_addr >> 8) & 0xffff);
        ide.head = (int)((ide.lba_addr >> 24) & 0xf);
    }

    /// <summary>IDNF, « ID Not Found » : le bit 4 du registre d'erreur (ATA-1 r4 § 7.2.8).</summary>
    private const uint8_t IDNF_ERR_MATERIEL = 0x10;

    /// <summary>L'adresse courante est hors du disque : au-delà de sa capacité, ou, en CHS, un secteur nul ou au-delà
    /// de la piste, une tête au-delà des têtes de l'image (l'adressage de PCem, ide_get_sector).</summary>
    private static bool ide_hors_disque_materiel(IDE ide)
    {
        if (ide.lba == 0 && (ide.sector < 1 || ide.sector > ide.hdd_file.spt || ide.head >= ide.hdd_file.hpc))
                return true;
        return ide_get_sector(ide) - ide.skip512 >= ide.hdd_file.sectors;
    }
}
