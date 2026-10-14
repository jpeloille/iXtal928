#!/usr/bin/env bash
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# listings-jit.sh — les listings du JIT d'iXtal26, scénario par scénario (G13 : M0 et M2, PLAN-G13.md § La
# vérification). Le mode PCem ne coûte rien si chaque garde du mode matériel est pliée par le JIT : le code machine
# des méthodes gardées est alors celui d'avant la garde, à l'octet près. C'est la preuve directe du 0 %.
#
#   tools/listings-jit.sh capture DLL SORTIE [SCÉNARIO…]   joue les scénarios avec iXtal26.dll (Release), un listing
#                                                         par scénario dans SORTIE (défaut : tous les scénarios)
#   tools/listings-jit.sh compare REF AUTRE               compare deux captures méthode par méthode : les méthodes
#                                                         changées, apparues ou disparues ; retour 1 s'il y en a
#   tools/listings-jit.sh methode CAPTURE NOM             imprime le listing d'une méthode (le premier scénario qui
#                                                         la compile), pour lire un écart
#
# La référence M0 est la capture du commit 860f2b1 (G13.1), le dernier sans garde ; une capture se rejoue à
# l'identique depuis son commit, avec le même runtime (noté dans SORTIE/runtime.txt). Chaque scénario tourne dans un
# bac à sable, comme les portes de par.sh : nvr/default et os/pcdos20 y sont COPIÉS (une machine AT réécrit son CMOS
# en sortant), le reste du dépôt y est lié. Les listings viennent de DOTNET_JitDisasm='*' et JitDisasmDiffable (les
# adresses remplacées), le résumé de JitDisasmSummary (aucune méthode ne doit y être en MinOpts) ; le projet compile
# sans paliers, chaque méthode une seule fois. Release seulement : en Debug, rien ne se plie. Les scénarios tournent
# sans randomisation des adresses (setarch -R) : selon l'endroit où tombent ses champs statiques, le JIT les atteint
# par un adressage relatif ou par une adresse absolue, et deux exécutions du même binaire donnaient deux listings.
set -uo pipefail
REPO=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)

# Les scénarios : un nom, puis les arguments d'iXtal26. Ils couvrent les cœurs 8088, 286, 386 et 486, le 8087, le 287
# et le 387 (les tests de coprocesseur du POST), PC-DOS sur l'XT, et sous DEBUG les instructions dont une garde
# n'est compilée par aucun amorçage (SBB octet : setsbc8). G13.4 : le PC1512 (amstrad_init), et la CH et la TM lues
# sous DEBUG (O 201, I 201). La souris PS/2 n'a pas de scénario : aucune machine du dépôt ne la monte, et --force-ps2
# n'existe sous --boot que depuis G13.4. G13.5 : le 386 sous PC-DOS (CMOS de ami386dx-fd.cfg), et sous DEBUG, entrées
# en octets, les instructions dont les handlers sont gardés (AAA, AAS, AAD, AAM, DAS, ADC, LOCK, BT, BTS et 0F BA, en
# adresses de 16 et de 32 bits) ; G13.5b, un second (ami386dx-ctrl) : MOV SS, POP SS, et MOV vers et depuis CR0,
# DR7 et TR6, en adresses de 16 et de 32 bits (LOADALL, qui écraserait la machine, n'a pas de scénario). G13.5c, un
# troisième (ami386dx-pm) : le mode protégé que le POST ne visite pas. LGDT d'une GDT à deux entrées (la seconde, une
# TSS 386 de dix-sept octets), PE posé, puis 66 CALL FAR sur la TSS : loadcscall, CALL_FAR_l et taskswitch286, qui lève
# #TS sur la limite, puis pmodeint sur l'IDT du mode réel ; la triple faute remet la machine à zéro. G13.6a, un
# quatrième (ami386dx-x87, avec un 387) : FNINIT, FCOMPP, FLDLN2, FXAM, FTST, FCOM et FCOMP de registre, FST et FSTP de
# registre, FCOM m64 (x87_compare) et FNSTSW AX. G13.6c, un cinquième (ami386dx-x87div) : FDIVP d'un quotient fini,
# puis 1 / 0 sous ZE masquée (x87_div, dont la branche démasquée est gardée), FNCLEX et WAIT. Dans un texte de --type,
# « _ » tient lieu d'espace. G13.7a, deux disques MFM : l'XT et son Xebec (xt-xebec ; le POST de la ROM de la carte, un
# PC-DOS amorcé de A: qui copie COMMAND.COM sur B:, l'écriture d'une disquette), et l'IBM AT et son contrôleur
# (ibmat-mfm ; le POST, SET PARAMETERS et la lecture du secteur d'amorce de C:). Leurs images vierges (hdc_fn = os/…)
# sont COPIÉES dans le bac à sable. G13.7b, l'IDE : l'IBM AT et un disque IDE vierge (ibmat-ide ; le POST, son
# interruption, le secteur d'amorce). G13.7c1, le moteur d'images de CD : l'ami486 et un lecteur ATAPI, chargé de la
# feuille multi.cue d'isogen (ami486-cd-cue ; LoadCueSheet et AddTrack, puis, sous DEBUG, un PACKET READ(10) du
# secteur 16 conduit par les ports 170h-177h, le registre de fonctions remis à 0 après le POST : le premier reçoit
# UNIT ATTENTION (le disque vient d'être chargé), le deuxième ABRT, le troisième lit le PVD ; ReadSector) ; G13.7c2 :
# le même programme, la longueur de chaque bloc lue en 174h-175h, rejoue READ TOC au format 10b (image_readtoc_raw),
# puis un second, tapé après les lectures (le secteur lu en 300h couvrirait 400h), envoie MODE SELECT(10) de la page
# audio (cdrom_mode_select) ; G13.7c4 : un troisième, sans données (600h, le paquet en 700h), joue la piste 2 en MSF,
# cherche son début (SEEK) et la joue en LBA (image_playaudio, image_is_track_audio, image_seek), puis le premier lit
# READ SUB-CHANNEL (image_getcurrentsubchannel, GetAudioSub). G13.7d1, le ZIP : ami486-zip, le lecteur ZIP 100 en maître
# secondaire sur une image vierge créée creuse dans le bac à sable ; les mêmes programmes sous DEBUG — READ CAPACITY
# (trois essais, l'attention du disque chargé), WRITE(10) d'un secteur du LBA 16 (le second programme, 512 octets),
# READ(10) et READ(6) de ce secteur (scsi_zip_command, scsi_add_data, scsi_get_data, scsi_zip_read, scsi_zip_write).
# G13.7d2a, le disque SCSI : ami486-aha, l'AHA-1542C et un disque vierge à l'ID 0, créé creux ; le POST de la ROM de la
# carte (INQUIRY, TEST UNIT READY, READ CAPACITY, la lecture du secteur 0), puis PC-DOS sur A: (scsi_hd_command) ou de l'ISO iso-2048.iso (ami486-cd-iso, le POST ;
# LoadIsoFile et CanReadPVD). Les images d'isogen sont ÉCRITES dans le bac à sable (isogen/). G13.8a, le DSP de la
# Sound Blaster : ami486-sb16, la SB 16 conduite sous DEBUG par ses ports — le reset (2x6h), E1h, 08h puis E1h, 38h puis
# 90h, 34h, D3h et D1h, et les lectures de 2xAh, 2xCh et 2xEh (sb_write, sb_read, sb_exec_command, sb_dsp_reset).
# G13.8b, l'ADPCM : ami486-adpcm, le canal 1 du 8237 en automatique sur 64 octets, puis 48h 0Fh 00h et les commandes
# ADPCM de sortie, 75h, 77h, 17h, 74h, 76h, 16h (des blocs de 16 octets), 7Dh, 7Fh et 1Fh (automatiques, chacun arrêté
# par un reset) : pollsb dans ses trois cas ADPCM, sb_8_read_dma.
# G13.8c1, les commandes de DMA : ami486-dma, la SB 16 sous DEBUG — le canal 1 et le canal 5 en automatique, 48h, 1Ch
# puis 14h (la fin de bloc 8 bits), C6h puis C0h, C8h à l'entrée, B6h puis B0h (le 16 bits), chacun arrêté par un
# reset : les cas 14h et B0h à CFh, les deux fins de bloc de pollsb.
# G13.8f, l'EMU8000 : ami486-awe32, l'AWE32 sous DEBUG, par octets (emu8k_outb et emu8k_inb passent aux mots) — WC
# (3Bh) lu deux fois, SMALR (34h) écrit en A22h puis relu, DCYSUSV de la voix 0 (A0h) à 80h puis 0, HWCF3 (3Fh) à 4,
# l'allumage repris : emu8k_inw (ses gardes en tête et aux quatre adresses de la DRAM), emu8k_outw (DCYSUSV).
# G13.9a, le socle SVGA : ami486-et4000 et ami486-gd5429, l'ET4000AX et la GD5429 (que la GD5429 passe en chain4 compact
# par SR7) sous DEBUG — le mode 13h, puis la mémoire d'affichage lue en mot, en double mot et en octet par le chemin
# rapide, puis GR8 = 7Fh et le mot et le double mot par le chemin lent (svga_read_linear, gd5429_read_linear,
# svga_recalctimings).
# G13.9a2 : les deux scénarios de l'ET4000 et de la GD5429 écrivent aussi un double mot et un mot (svga_writel,
# svga_writew et leurs formes linéaires compilées ; GR8 = 7Fh, le chemin lent pris) ; ami486-tvga24, la TVGA8900D en mode 13h, son RAMDAC TKD8001 passé en 24 bits (3C6h lu quatre fois, puis
# C0h), CR13 récrit pour recalculer : le rendu 24 bpp basse résolution.
# G13.9b, la MDA et l'Hercules : xt-mda et xt-hercules, l'XT et la carte, PC-DOS (DIR), puis sous DEBUG 3BAh lu, le
# curseur 0607h écrit (R10, R11) et R11 relu ; l'Hercules passée en graphique (3BFh, 3B8h) puis rendue au texte
# (mda_out, mda_in, mda_recalctimings, mda_poll ; hercules_out, hercules_in, hercules_recalctimings).
SCENARIOS=(
  "xt-dos|--boot roms 8000 --model ibmxt --floppy-a os/pcdos20/pcdos20b.img --floppy-b os/pcdos20/pcdos20s.img --settle 600 --type  --type  --type DIR --type B:DEBUG --type A_100 --type SBB_AL,1 --type SBB_AX,1 --type INT_3 --type  --type G=100 --type Q"
  "pc-8087-dos|--boot roms 7000 --config tools/gates/cfg/pc-8087.cfg --floppy-a os/pcdos20/pcdos20b.img --type  --type "
  "ibmat-287|--boot roms 3000 --config tools/gates/cfg/ibmat-287.cfg"
  "ami386dx-387|--boot roms 3000 --config tools/gates/cfg/ami386dx-387.cfg"
  "ami486-dx2|--boot roms 3000 --config tools/gates/cfg/ami486-dx2.cfg"
  "pc1512|--boot roms 3000 --model pc1512"
  "pc-joy-ch|--boot roms 7000 --config tools/gates/cfg/pc-joy-ch.cfg --floppy-a os/pcdos20/pcdos20b.img --floppy-b os/pcdos20/pcdos20s.img --settle 300 --type  --type  --type B:DEBUG --type O_201_0 --type I_201 --type Q"
  "pc-joy-tm|--boot roms 7000 --config tools/gates/cfg/pc-joy-tm.cfg --floppy-a os/pcdos20/pcdos20b.img --floppy-b os/pcdos20/pcdos20s.img --settle 300 --type  --type  --type B:DEBUG --type O_201_0 --type I_201 --type Q"
  "ami386dx-dos|--boot roms 2500 --config tools/gates/cfg/ami386dx-fd.cfg.in --floppy-a os/pcdos20/pcdos20b.img --floppy-b os/pcdos20/pcdos20s.img --settle 600 --type  --type  --type B:DEBUG --type R_BX --type 200 --type E_100_37_3F_D5_0A_D4_0A_2F_14_0F_F0_01_07_0F_A3_07 --type E_10F_66_0F_A3_07_0F_AB_07_F0_0F_AB_07_0F_BA_E0_05 --type E_11E_67_0F_A3_03_66_67_0F_A3_03_67_0F_BA_23_05_CC --type G=100 --type Q --type @wait_2000|tools/gates/cfg/ami386dx-fd.cfg.in ami386dx_opti495"
  "ami386dx-ctrl|--boot roms 2500 --config tools/gates/cfg/ami386dx-fd.cfg.in --floppy-a os/pcdos20/pcdos20b.img --floppy-b os/pcdos20/pcdos20s.img --settle 600 --type  --type  --type B:DEBUG --type E_100_8C_D0_8E_D0_67_8E_D0_16_17_66_16_66_17_0F_20_C0 --type E_110_0F_22_C0_67_0F_20_C0_67_0F_22_C0_0F_21_F8_0F_23_F8 --type E_121_67_0F_21_F8_67_0F_23_F8_0F_24_F0_0F_26_F0 --type E_12F_67_0F_24_F0_67_0F_26_F0_CC --type G=100 --type Q --type @wait_2000|tools/gates/cfg/ami386dx-fd.cfg.in ami386dx_opti495"
  "ami386dx-x87|--boot roms 2500 --config tools/gates/cfg/ami386dx-387-fd.cfg.in --floppy-a os/pcdos20/pcdos20b.img --floppy-b os/pcdos20/pcdos20s.img --settle 600 --type  --type  --type B:DEBUG --type E_100_DB_E3_D9_E8_D9_EE_DE_D9_D9_ED_D9_E5_D9_E4_D9_E8 --type E_110_D8_D1_D8_D9_DD_D1_DD_D9_DC_16_00_02_DF_E0_CC --type G=100 --type Q --type @wait_2000|tools/gates/cfg/ami386dx-387-fd.cfg.in ami386dx_opti495"
  "ami386dx-x87div|--boot roms 2500 --config tools/gates/cfg/ami386dx-387-fd.cfg.in --floppy-a os/pcdos20/pcdos20b.img --floppy-b os/pcdos20/pcdos20s.img --settle 600 --type  --type  --type B:DEBUG --type E_100_DB_E3_D9_E8_D9_E8_DE_F9_D9_EE_DE_F9_DB_E2_9B_CC --type G=100 --type Q --type @wait_2000|tools/gates/cfg/ami386dx-387-fd.cfg.in ami386dx_opti495"
  "xt-xebec|--boot roms 9000 --config tools/gates/cfg/xt-xebec.cfg --floppy-a os/pcdos20/pcdos20b.img --floppy-b os/pcdos20/pcdos20s.img --settle 600 --type  --type  --type COPY_COMMAND.COM_B:X.COM --type @wait_2000"
  "ibmat-mfm|--boot roms 4000 --config tools/gates/cfg/ibmat-mfm.cfg|tools/gates/cfg/ibmat-mfm.cfg at"
  "ibmat-ide|--boot roms 4000 --config tools/gates/cfg/ibmat-ide.cfg|tools/gates/cfg/ibmat-ide.cfg at"
  "ami486-cd-cue|--boot roms 6000 --config tools/gates/cfg/ami486-cd-fd.cfg --floppy-a os/pcdos20/pcdos20b.img --floppy-b os/pcdos20/pcdos20s.img --settle 600 --type  --type  --type B:DEBUG --type O_176_A0 --type O_171_0 --type E_100_BA_76_01_B0_A0_EE_BA_74_01_B0_00_EE_42_B0_08_EE --type E_110_BA_77_01_B0_A0_EE_EC_A8_80_75_FB_A8_08_74_F7_BA --type E_120_70_01_BE_00_02_B9_06_00_AD_EF_E2_FC_BA_77_01_EC --type E_130_A8_80_75_FB_A8_09_74_F7_A8_01_75_17_BA_74_01_EC --type E_140_88_C1_42_EC_88_C5_41_D1_E9_BA_70_01_BF_00_03_ED --type E_150_AB_E2_FC_CC --type E_200_28_00_00_00_00_10_00_00_01_00_00_00 --type G=100 --type G=100 --type G=100 --type D_300_30F --type E_200_43_00_02_00_00_00_00_08_00_00_00_00 --type G=100 --type D_300_30F --type E_400_BA_76_01_B0_A0_EE_BA_74_01_B0_00_EE_42_B0_08_EE --type E_410_BA_77_01_B0_A0_EE_EC_A8_80_75_FB_A8_08_74_F7_BA --type E_420_70_01_BE_00_05_B9_06_00_AD_EF_E2_FC_BA_77_01_EC --type E_430_A8_80_75_FB_A8_09_74_F7_A8_01_75_0D_BA_70_01_BE --type E_440_20_05_B9_0C_00_AD_EF_E2_FC_CC --type E_500_55_10_00_00_00_00_00_00_18_00_00_00 --type E_520_00_00_00_00_00_00_00_00_0E_0E_05_04_00_80_00_4B_01_80_02_40_00_00_00_00 --type G=400 --type I_177 --type E_600_BA_76_01_B0_A0_EE_BA_74_01_B0_00_EE_42_B0_08_EE --type E_610_BA_77_01_B0_A0_EE_EC_A8_80_75_FB_A8_08_74_F7_BA --type E_620_70_01_BE_00_07_B9_06_00_AD_EF_E2_FC_BA_77_01_EC --type E_630_A8_80_75_FB_CC --type E_700_47_00_00_00_02_25_00_02_28_00_00_00 --type G=600 --type I_177 --type E_700_2B_00_00_00_00_25_00_00_00_00_00_00 --type G=600 --type I_177 --type E_700_45_00_00_00_00_25_00_00_02_00_00_00 --type G=600 --type I_177 --type E_200_42_02_40_01_00_00_00_00_10_00_00_00 --type G=100 --type D_300_30F --type Q --type @wait_2000|tools/gates/cfg/ami486-cd-fd.cfg ami486"
  "ami486-cd-iso|--boot roms 3000 --config tools/gates/cfg/ami486-cd-iso-fd.cfg|tools/gates/cfg/ami486-cd-iso-fd.cfg ami486"
  "ami486-zip|--boot roms 6000 --config tools/gates/cfg/ami486-zip-fd.cfg --floppy-a os/pcdos20/pcdos20b.img --floppy-b os/pcdos20/pcdos20s.img --settle 600 --type  --type  --type B:DEBUG --type O_176_A0 --type O_171_0 --type E_100_BA_76_01_B0_A0_EE_BA_74_01_B0_00_EE_42_B0_08_EE --type E_110_BA_77_01_B0_A0_EE_EC_A8_80_75_FB_A8_08_74_F7_BA --type E_120_70_01_BE_00_02_B9_06_00_AD_EF_E2_FC_BA_77_01_EC --type E_130_A8_80_75_FB_A8_09_74_F7_A8_01_75_17_BA_74_01_EC --type E_140_88_C1_42_EC_88_C5_41_D1_E9_BA_70_01_BF_00_03_ED --type E_150_AB_E2_FC_CC --type E_200_25_00_00_00_00_00_00_00_00_00_00_00 --type G=100 --type G=100 --type G=100 --type D_300_307 --type E_400_BA_76_01_B0_A0_EE_BA_74_01_B0_00_EE_42_B0_08_EE --type E_410_BA_77_01_B0_A0_EE_EC_A8_80_75_FB_A8_08_74_F7_BA --type E_420_70_01_BE_00_05_B9_06_00_AD_EF_E2_FC_BA_77_01_EC --type E_430_A8_80_75_FB_A8_09_74_F7_A8_01_75_0D_BA_70_01_BE --type E_440_20_05_B9_00_01_AD_EF_E2_FC_CC --type E_500_2A_00_00_00_00_10_00_00_01_00_00_00 --type E_520_5A_A5 --type G=400 --type I_177 --type E_200_28_00_00_00_00_10_00_00_01_00_00_00 --type G=100 --type D_300_30F --type E_200_08_00_00_10_01_00_00_00_00_00_00_00 --type G=100 --type D_300_30F --type Q --type @wait_2000|tools/gates/cfg/ami486-zip-fd.cfg ami486"
  "ami486-aha|--boot roms 6000 --config tools/gates/cfg/ami486-aha-m2.cfg --floppy-a os/pcdos20/pcdos20b.img --floppy-b os/pcdos20/pcdos20s.img --settle 600 --type  --type  --type @wait_2000|tools/gates/cfg/ami486-aha-m2.cfg ami486"
  "ami486-sb16|--boot roms 6000 --config tools/gates/cfg/ami486-sb16-fd.cfg --floppy-a os/pcdos20/pcdos20b.img --floppy-b os/pcdos20/pcdos20s.img --settle 600 --type  --type  --type B:DEBUG --type O_226_1 --type O_226_0 --type I_22E --type I_22A --type O_22C_E1 --type I_22E --type I_22A --type I_22A --type I_22C --type O_22C_8 --type O_22C_E1 --type I_22A --type I_22A --type I_22A --type O_22C_38 --type O_22C_90 --type O_226_1 --type O_226_0 --type I_22A --type O_22C_34 --type O_22C_D3 --type O_22C_D1 --type O_226_1 --type O_226_0 --type I_22A --type Q --type @wait_2000|tools/gates/cfg/ami486-sb16-fd.cfg ami486"
  "ami486-adpcm|--boot roms 6000 --config tools/gates/cfg/ami486-sb16-fd.cfg --floppy-a os/pcdos20/pcdos20b.img --floppy-b os/pcdos20/pcdos20s.img --settle 600 --type  --type  --type B:DEBUG --type O_A_5 --type O_C_0 --type O_B_59 --type O_2_0 --type O_2_0 --type O_83_1 --type O_3_3F --type O_3_0 --type O_A_1 --type O_22C_D1 --type O_22C_48 --type O_22C_F --type O_22C_0 --type O_22C_75 --type O_22C_F --type O_22C_0 --type O_22C_77 --type O_22C_F --type O_22C_0 --type O_22C_17 --type O_22C_F --type O_22C_0 --type O_22C_74 --type O_22C_F --type O_22C_0 --type O_22C_76 --type O_22C_F --type O_22C_0 --type O_22C_16 --type O_22C_F --type O_22C_0 --type O_22C_7D --type I_22E --type O_226_1 --type O_226_0 --type I_22A --type O_22C_48 --type O_22C_F --type O_22C_0 --type O_22C_7F --type I_22E --type O_226_1 --type O_226_0 --type I_22A --type O_22C_48 --type O_22C_F --type O_22C_0 --type O_22C_1F --type I_22E --type O_226_1 --type O_226_0 --type I_22A --type O_22C_48 --type O_22C_F --type O_22C_0 --type I_22E --type Q --type @wait_2000|tools/gates/cfg/ami486-sb16-fd.cfg ami486"
  "ami486-dma|--boot roms 6000 --config tools/gates/cfg/ami486-sb16-fd.cfg --floppy-a os/pcdos20/pcdos20b.img --floppy-b os/pcdos20/pcdos20s.img --settle 600 --type  --type  --type B:DEBUG --type O_D6_C0 --type O_D4_0 --type O_A_5 --type O_C_0 --type O_B_59 --type O_2_0 --type O_2_0 --type O_83_1 --type O_3_FF --type O_3_0 --type O_A_1 --type O_D4_5 --type O_D8_0 --type O_D6_59 --type O_C4_0 --type O_C4_0 --type O_8B_2 --type O_C6_FF --type O_C6_0 --type O_D4_1 --type O_22C_48 --type O_22C_3F --type O_22C_0 --type O_22C_1C --type O_22C_14 --type O_22C_F --type O_22C_0 --type I_22E --type I_22E --type O_226_1 --type O_226_0 --type I_22A --type O_22C_C6 --type O_22C_0 --type O_22C_3F --type O_22C_0 --type O_22C_C0 --type O_22C_14 --type O_22C_F --type O_22C_0 --type I_22E --type O_226_1 --type O_226_0 --type I_22A --type O_22C_C8 --type O_22C_4 --type O_22C_F --type O_22C_0 --type I_22E --type O_226_1 --type O_226_0 --type I_22A --type O_22C_B6 --type O_22C_10 --type O_22C_3F --type O_22C_0 --type O_22C_B0 --type O_22C_10 --type O_22C_F --type O_22C_0 --type I_22F --type I_22F --type O_226_1 --type O_226_0 --type I_22A --type Q --type @wait_2000|tools/gates/cfg/ami486-sb16-fd.cfg ami486"
  "ami486-awe32|--boot roms 6000 --config tools/gates/cfg/ami486-awe32-fd.cfg --floppy-a os/pcdos20/pcdos20b.img --floppy-b os/pcdos20/pcdos20s.img --settle 600 --type  --type  --type B:DEBUG --type O_E22_3B --type I_A22 --type I_A22 --type O_E22_34 --type O_A20_34 --type O_A22_12 --type I_A22 --type I_A22 --type O_E22_A0 --type O_A20_80 --type O_A20_0 --type O_E22_3F --type O_A20_4 --type O_E22_A0 --type O_A20_80 --type O_A20_0 --type O_E22_3B --type I_A22 --type Q --type @wait_2000|tools/gates/cfg/ami486-awe32-fd.cfg ami486"
  "ami486-et4000|--boot roms 6000 --config tools/gates/cfg/ami486-et4000-fd.cfg --floppy-a os/pcdos20/pcdos20b.img --floppy-b os/pcdos20/pcdos20s.img --settle 600 --type  --type  --type B:DEBUG --type E_100_B8_13_00_CD_10_B8_00_A0_8E_D8_A1_00_00_66_A1_04 --type E_110_00_A0_08_00_BA_CE_03_B8_08_7F_EF_A1_10_00_66_A1 --type E_120_14_00_66_C7_06_20_00_44_33_22_11_C7_06_24_00_22 --type E_130_11_CC --type G=100 --type Q --type @wait_2000|tools/gates/cfg/ami486-et4000-fd.cfg ami486"
  "ami486-gd5429|--boot roms 6000 --config tools/gates/cfg/ami486-gd5429-fd.cfg --floppy-a os/pcdos20/pcdos20b.img --floppy-b os/pcdos20/pcdos20s.img --settle 600 --type  --type  --type B:DEBUG --type E_100_B8_13_00_CD_10_BA_C4_03_B8_06_12_EF_B8_07_01_EF --type E_110_B8_00_A0_8E_D8_A1_00_00_66_A1_04_00_A0_08_00_BA --type E_120_CE_03_B8_08_7F_EF_A1_10_00_66_A1_14_00_66_C7_06 --type E_130_20_00_44_33_22_11_C7_06_24_00_22_11_CC --type G=100 --type Q --type @wait_2000|tools/gates/cfg/ami486-gd5429-fd.cfg ami486"
  "ami486-tvga24|--boot roms 6000 --config tools/gates/cfg/ami486-tvga8900d-fd.cfg --floppy-a os/pcdos20/pcdos20b.img --floppy-b os/pcdos20/pcdos20s.img --settle 600 --type  --type  --type B:DEBUG --type E_100_B8_13_00_CD_10_BA_C6_03_EC_EC_EC_EC_B0_C0_EE_BA --type E_110_D4_03_B8_13_29_EF_B8_13_28_EF_CC --type G=100 --type Q --type @wait_2000|tools/gates/cfg/ami486-tvga8900d-fd.cfg ami486"
  "xt-mda|--boot roms 8000 --model ibmxt --gfxcard mda --floppy-a os/pcdos20/pcdos20b.img --floppy-b os/pcdos20/pcdos20s.img --settle 600 --type  --type  --type DIR --type B:DEBUG --type I_3BA --type O_3B4_A --type O_3B5_6 --type O_3B4_B --type O_3B5_7 --type I_3B5 --type Q --type @wait_2000"
  "xt-hercules|--boot roms 8000 --model ibmxt --gfxcard hercules --floppy-a os/pcdos20/pcdos20b.img --floppy-b os/pcdos20/pcdos20s.img --settle 600 --type  --type  --type DIR --type B:DEBUG --type I_3BA --type O_3B4_A --type O_3B5_6 --type O_3B4_B --type O_3B5_7 --type I_3B5 --type O_3BF_1 --type O_3B8_2 --type I_3BA --type O_3B8_28 --type O_3BF_0 --type Q --type @wait_2000"
  "ami386dx-pm|--boot roms 2500 --config tools/gates/cfg/ami386dx-fd.cfg.in --floppy-a os/pcdos20/pcdos20b.img --floppy-b os/pcdos20/pcdos20s.img --settle 600 --type  --type  --type B:DEBUG --type E_100_8C_C8_66_0F_B7_C0_66_C1_E0_04_66_05_00_02_00_00 --type E_110_66_A3_82_01_0F_01_16_80_01_0F_20_C0_0C_01_0F_22 --type E_120_C0_66_9A_00_00_00_00_08_00 --type E_180_0F_00 --type E_200_00_00_00_00_00_00_00_00_10_00_00_00_00_89_00_00 --type G=100 --type @wait_2000|tools/gates/cfg/ami386dx-fd.cfg.in ami386dx_opti495"
)

capture() {
  local dll out; dll=$(realpath -m "$1"); out=$(realpath -m "$2"); shift 2
  [ -f "$dll" ] || { echo "listings-jit : $dll introuvable" >&2; exit 2; }
  mkdir -p "$out"
  { dotnet --list-runtimes | grep Microsoft.NETCore.App; echo "dll $(sha256sum "$dll" | cut -c1-16)"; } > "$out/runtime.txt"
  local s nom args w
  for s in "${SCENARIOS[@]}"; do
    nom=${s%%|*}; args=${s#*|}; cmos=""
    if [[ "$args" == *"|"* ]]; then cmos=${args#*|}; args=${args%%|*}; fi
    if [ $# -gt 0 ] && [[ " $* " != *" $nom "* ]]; then continue; fi
    w=$(mktemp -d "${TMPDIR:-/tmp}/listings-jit-XXXXXX")
    mkdir -p "$w/nvr" "$w/os"
    for e in "$REPO"/*; do case "$(basename "$e")" in nvr|os) ;; *) ln -s "$e" "$w/";; esac; done
    cp -r "$REPO/nvr/default" "$w/nvr/"; cp -r "$REPO/os/pcdos20" "$w/os/"
    # Les images de disque dur que nomme la configuration du scénario (hdc_fn = os/…), copiées.
    local cfgs; cfgs=$(grep -o -- '--config [^ ]*' <<< "${args%%|*}" | cut -d' ' -f2)
    for c in $cfgs; do
      grep -oE '^hd[c-f]_fn = os/[^ ]+' "$REPO/$c" 2>/dev/null | sed 's/^.* = //' | while read -r img; do cp "$REPO/$img" "$w/os/"; done
    done
    # Les images de CD d'isogen (cdrom_path = isogen/…), écrites dans le bac à sable.
    for c in $cfgs; do
      if grep -qE '^cdrom_path = isogen/' "$REPO/$c" 2>/dev/null; then
        mkdir -p "$w/isogen"; python3 "$REPO/tools/isogen/isogen.py" "$w/isogen" > /dev/null
      fi
    done
    # L'image vierge du ZIP 100 (zip_path = zip100.img), et celle du disque SCSI (hdc_fn = scsi-m2.img), creuses, dans le
    # bac à sable.
    for c in $cfgs; do
      if grep -qE '^zip_path = zip100.img' "$REPO/$c" 2>/dev/null; then truncate -s 100663296 "$w/zip100.img"; fi
      if grep -qE '^hdc_fn = scsi-m2.img' "$REPO/$c" 2>/dev/null; then truncate -s 20971520 "$w/scsi-m2.img"; fi
    done
    # Un troisième champ, « CONFIG MACHINE » : le CMOS de la machine, fabriqué dans le bac à sable par --make-nvr (comme
    # g5w-recipe.sh), sans listing, avant le scénario.
    if [ -n "$cmos" ]; then
      (cd "$w" && dotnet "$dll" --make-nvr "${cmos% *}" cmos.nvr --force > /dev/null 2>&1 && cp cmos.nvr "nvr/.${cmos#* }.nvr")
    fi
    # Le texte de --type vide (Entrée seule) se passe par un argument vide : l'aplatissement des arguments le perdrait.
    local -a argv=()
    read -r -a mots <<< "$args"
    local i=0
    while [ $i -lt ${#mots[@]} ]; do
      argv+=("${mots[$i]}")
      if [ "${mots[$i]}" = "--type" ]; then
        if [ $((i + 1)) -lt ${#mots[@]} ] && [[ "${mots[$((i + 1))]}" != --* ]]; then
          argv+=("${mots[$((i + 1))]//_/ }"); i=$((i + 1))
        else
          argv+=("")
        fi
      fi
      i=$((i + 1))
    done
    rm -f "$out/$nom.txt"
    (cd "$w" && env -u DOTNET_TieredCompilation -u DOTNET_TieredPGO -u DOTNET_ReadyToRun \
      DOTNET_JitDisasm='*' DOTNET_JitDisasmDiffable=1 DOTNET_JitDisasmSummary=1 DOTNET_JitStdOutFile="$out/$nom.txt" \
      setarch "$(uname -m)" -R dotnet "$dll" "${argv[@]}" > "$out/$nom.sortie" 2>&1)
    echo "  $nom : retour $?, $(grep -c '^; Assembly listing for method' "$out/$nom.txt") méthodes," \
         "$(grep -c 'MinOpts' "$out/$nom.txt") mentions de MinOpts"
    rm -rf "$w"
  done
}

# Le découpage : un listing commence à « ; Assembly listing for method NOM (NIVEAU) ».
decoupe() {
  python3 - "$@" << 'PY'
import os, re, sys, json
def lire(d):
    m = {}
    for f in sorted(os.listdir(d)):
        if not f.endswith('.txt') or f == 'runtime.txt':
            continue
        cur, buf = None, []
        for l in open(os.path.join(d, f), encoding='utf-8', errors='replace'):
            h = re.match(r'^; Assembly listing for method (.*?)(?: \(([^)]*)\))?\s*$', l)
            if h:
                if cur and cur not in m:
                    m[cur] = (f, ''.join(buf))
                cur, buf = h.group(1), [l]
            elif cur is not None:
                if re.match(r'^\s*\d+: JIT compiled ', l):
                    continue
                buf.append(l)
        if cur and cur not in m:
            m[cur] = (f, ''.join(buf))
    return m
mode = sys.argv[1]
if mode == 'compare':
    a, b = lire(sys.argv[2]), lire(sys.argv[3])
    seuls_a = sorted(set(a) - set(b)); seuls_b = sorted(set(b) - set(a))
    change = sorted(k for k in set(a) & set(b) if a[k][1] != b[k][1])
    print(f'  {len(a)} méthodes dans la référence, {len(b)} dans l\'autre ; {len(change)} changées, '
          f'{len(seuls_a)} disparues, {len(seuls_b)} apparues')
    for k in change: print('  changée   ', k)
    for k in seuls_a: print('  disparue  ', k)
    for k in seuls_b: print('  apparue   ', k)
    sys.exit(1 if change or seuls_a or seuls_b else 0)
else:
    m = lire(sys.argv[2]); nom = sys.argv[3]
    hits = [k for k in m if nom in k]
    for k in hits[:5]:
        print(f'=== {k} ({m[k][0]})'); print(m[k][1])
    sys.exit(0 if hits else 1)
PY
}

case "${1:-}" in
capture) shift; [ $# -ge 2 ] || { echo "usage : listings-jit.sh capture DLL SORTIE [SCÉNARIO…]" >&2; exit 2; }; capture "$@" ;;
compare) [ $# -eq 3 ] || { echo "usage : listings-jit.sh compare REF AUTRE" >&2; exit 2; }; decoupe compare "$2" "$3" ;;
methode) [ $# -eq 3 ] || { echo "usage : listings-jit.sh methode CAPTURE NOM" >&2; exit 2; }; decoupe methode "$2" "$3" ;;
*) sed -n '5,12p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; exit 2 ;;
esac
