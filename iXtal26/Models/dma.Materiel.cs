// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le code propre au mode matériel du 8237 (G13 ; R10 de TRANSCRIPTION.md).
// STATUS: materiel
//
// Les corrections des défauts de PCem dans dma.c, appelées par les gardes de dma.cs (`if (materiel.pb_nn)`), seules
// sur leur ligne. La source, le cas qui discrimine et la panne qui le rougit sont dans l'entrée PB de PCEM_BUGS.md.
// Les deux 8237 selon la fiche (231466-005) : `base` vaut 0 pour celui du bas (canaux 0 à 3, ports 00h à 0Fh), 4 pour
// celui du haut (canaux 4 à 7, ports C0h à DFh, adresses déjà ramenées à 0h à Fh par `addr >>= 1`).

using iXtal26.Cpu;
using static iXtal26.Cpu.x86;

namespace iXtal26.Models;

internal static partial class dma
{
    // L'état que PCem n'a pas, dans une classe à part : un champ statique de plus dans `dma` changerait son
    // constructeur statique, et le code que le JIT produit pour les méthodes qui le lisent, en mode PCem aussi.
    private static class Etat8237
    {
        /// <summary>Le registre de requête des deux 8237, un bit par canal (fiche p. 7, « Request Register »).</summary>
        internal static uint8_t requete;
    }

    // pcem bug, fixed in hardware mode: PB-257 — un canal masqué, ou dont le mode n'est pas celui du transfert, ne fait
    //   aucune requête au 8237A (fiche 8237A, p. 8, « Mask Register ») : pas de cycle de bus, rien à facturer. Le
    //   transfert accepté coûte son cycle, comme dans PCem ; la sonde compte les cycles qui ne sont plus facturés.
    private static void dma_cycle_materiel(int channel, int mode)
    {
        if (AT == 0)
        {
                if ((dma_m & (1 << channel)) == 0 && (dma_[channel].mode & 0xC) == mode)
                        _808x.refreshread();
                else
                        ModeMateriel.Sonde[257]++;
        }
    }

    // pcem bug, fixed in hardware mode: PB-252 — le reset (fiche p. 2, broche RESET) efface la commande, l'état, la
    //   requête et le temporaire, et pose les huit masques.
    private static void dma_reset_materiel()
    {
        dma_m = 0xff;
        dma_command = dma16_command = 0;
        dma_stat = 0;
        Etat8237.requete = 0;
        ModeMateriel.Sonde[252]++;
    }

    // pcem bug, fixed in hardware mode: PB-251 — le master clear d'un 8237 « a le même effet que le reset » (fiche p. 9) :
    //   sa commande, son état et sa requête effacés ; le temporaire n'est jamais écrit hors du transfert de mémoire à
    //   mémoire, que ni PCem ni ce mode ne font : il reste nul (0Dh, et DAh par PB-157).
    private static void dma_master_clear_materiel(int @base)
    {
        if (@base == 0)
        {
                dma_command = 0;
                dma_stat &= 0xf0;
                Etat8237.requete &= 0xf0;
        }
        else
        {
                dma16_command = 0;
                dma_stat &= 0x0f;
                Etat8237.requete &= 0x0f;
        }
        ModeMateriel.Sonde[251]++;
    }

    // pcem bug, fixed in hardware mode: PB-249 — Clear Mask (0Eh, DCh) efface les quatre masques du contrôleur (fiche
    //   p. 9 ; AT TR p. 1-14).
    private static bool dma_clear_mask_materiel(int @base, uint16_t addr)
    {
        if ((addr & 0xf) != 0xe)
                return false;
        dma_m &= (uint8_t)(@base == 0 ? 0xf0 : 0x0f);
        ModeMateriel.Sonde[249]++;
        return true;
    }

    // pcem bug, fixed in hardware mode: PB-157 — DAh se lit sur le temporaire, nul hors du transfert de mémoire à
    //   mémoire (fiche p. 9).
    private static bool dma_temporaire_materiel(uint16_t addr)
    {
        if ((addr & 0xf) != 0xd)
                return false;
        ModeMateriel.Sonde[157]++;
        return true;
    }

    // pcem bug, fixed in hardware mode: PB-157 — la commande du 8237 haut est rangée : son bit 2 le désactive.
    private static void dma16_command_materiel(uint8_t val)
    {
        dma16_command = val;
        ModeMateriel.Sonde[157]++;
    }

    // pcem bug, fixed in hardware mode: PB-253 — les canaux 0 à 3 n'ont le bus que par la cascade (AT TR p. 1-13) : le
    //   8237 bas le demande sur le canal 4, que le 8237 haut sert s'il n'est ni masqué ni désactivé, et s'il est en mode
    //   cascade (fiche p. 5-6). Sur un PC, un seul 8237 : rien ne s'interpose.
    private static bool dma_cascade_materiel()
    {
        if (AT == 0)
                return true;
        if ((dma16_command & 4) == 0 && (dma_m & 0x10) == 0 && (dma_[4].mode & 0xc0) == 0xc0)
                return true;
        ModeMateriel.Sonde[253]++;
        return false;
    }

    // pcem bug, fixed in hardware mode: PB-250 — le registre de requête (09h, D2h ; fiche p. 7 et figure 6) : le bit 2
    //   pose ou efface la requête logicielle du canal des bits 0 et 1. Les requêtes en attente se servent d'abord, avant
    //   tout accès aux registres : un programme ne voit jamais un transfert de bloc à moitié fait.
    private static bool dma_requete_materiel(int @base, uint16_t addr, uint8_t val)
    {
        dma_servir_materiel();
        if ((addr & 0xf) != 9)
                return false;
        var bit = (uint8_t)(1 << (@base + (val & 3)));
        if ((val & 4) != 0)
                Etat8237.requete |= bit;
        else
                Etat8237.requete &= (uint8_t)~bit;
        return true;
    }

    // pcem bug, fixed in hardware mode: PB-250 — une requête logicielle n'est pas masquable ; elle se sert en mode bloc
    //   seulement (fiche p. 7), jusqu'au TC : le compte de N à FFFFh, N + 1 transferts, l'adresse qui avance ou recule
    //   d'autant, puis l'auto-initialisation ou le masque posé, le bit TC de l'état, la requête effacée (p. 8). Un
    //   contrôleur désactivé (commande, bit 2), ou un canal du bas que la cascade ne sert pas (PB-253), la laisse en
    //   attente ; un canal hors du mode bloc aussi. La vérification ne touche à rien ; la lecture d'un transfert de la
    //   mémoire vers un port que personne n'acquitte, pas davantage ; l'écriture vers la mémoire n'écrit rien : la
    //   donnée est celle d'un bus que nul ne pilote, inconnue. Le transfert de mémoire à mémoire n'est pas modélisé.
    private static void dma_servir_materiel()
    {
        if (Etat8237.requete == 0)
                return;
        for (int c = 0; c < 8; c++)
        {
                if ((Etat8237.requete & (1 << c)) == 0)
                        continue;
                if ((dma_[c].mode & 0xc0) != 0x80)
                        continue;
                if (c < 4)
                {
                        if ((dma_command & 4) != 0)
                                continue;
                        if (materiel.pb_253)
                                if (!dma_cascade_materiel())
                                        continue;
                }
                else if ((dma16_command & 4) != 0)
                        continue;

                var d = dma_[c];
                var n = (d.cc & 0xffff) + 1;
                var pas = (d.mode & 0x20) != 0 ? -n : n;
                if (d.size == 0)
                        d.ac = (d.ac & 0xff0000) | ((uint32_t)(d.ac + pas) & 0xffff);
                else
                        d.ac = (d.ac & 0xfe0000) | ((uint32_t)(d.ac + 2 * pas) & 0x1ffff);
                d.cc = -1;
                if ((d.mode & 0x10) != 0)
                {
                        d.cc = d.cb;
                        d.ac = d.ab;
                }
                else
                        dma_m |= (uint8_t)(1 << c);
                dma_stat |= (uint8_t)(1 << c);
                Etat8237.requete &= (uint8_t)~(1 << c);
                ModeMateriel.Sonde[250]++;
        }
    }
}
