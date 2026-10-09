// SPDX-FileCopyrightText: 2026 Julien Peloille
// SPDX-License-Identifier: GPL-2.0-only
//
// ORACLE: (aucun) — le mode matériel, figé (G13 ; PLAN-G13.md, § Le mécanisme ; R10 de TRANSCRIPTION.md).
// STATUS: host
//
// Un champ par défaut de PCem corrigé en mode matériel, et RIEN d'autre. Le constructeur statique, explicite, copie la
// demande de ModeMateriel une seule fois, quand ModeMateriel.Figer() le lance, avant le premier cœur. Le dépôt compile
// sans paliers : chaque méthode gardée l'est une fois, après le gel, et en Release le JIT plie `if (materiel.pb_nn)` en
// constante ; la branche morte n'est pas même importée. Le mode PCem ne coûte donc rien, et tools/listings-jit.sh le
// prouve, méthode par méthode, contre la référence M0. Une garde lit le champ directement, jamais par une locale ni
// une propriété (le JIT n'y plierait plus rien), et elle se tient seule sur sa ligne (R2). Aucune écriture après le
// gel : ni réflexion ni Unsafe.AsRef. Des champs, jamais un tableau : un tableau ne se plie pas.
//
// Le constructeur ne lève jamais : un type dont l'initialiseur a levé reste à vie non initialisé.

namespace iXtal26;

internal static class materiel
{
    /// <summary>PB-01 : l'AF d'ADC et de SBB du 8088 et du 8086 (Cpu/808x.cs, Cpu/808x.Materiel.cs).</summary>
    internal static readonly bool pb_01;

    /// <summary>PB-02 : le CF de RCL et de RCR mot par CL.</summary>
    internal static readonly bool pb_02;

    /// <summary>PB-03 : les cycles du rafraîchissement (5150, XT) portés au TSC ; avec PB-257.</summary>
    internal static readonly bool pb_03;

    /// <summary>PB-05 : servir l'esclave du 8259 ne touche à l'IRR du maître que par la cascade.</summary>
    internal static readonly bool pb_05;

    /// <summary>PB-07 : un mot à cheval sur deux pages lit et écrit son octet haut par sa propre page ; avec PB-179.</summary>
    internal static readonly bool pb_07;

    /// <summary>PB-45 : IDIV octet divise AX signé ; avec PB-169.</summary>
    internal static readonly bool pb_45;

    /// <summary>PB-87 : au repli de l'IP, la lecture d'instruction se fait à l'offset 0 du segment.</summary>
    internal static readonly bool pb_87;

    /// <summary>PB-169 : DIV et IDIV lèvent INT 0 sur un quotient hors capacité ; avec PB-45.</summary>
    internal static readonly bool pb_169;

    /// <summary>PB-170 : DAA compare l'AL d'origine à 99h, ou à 9Fh si AF valait 1.</summary>
    internal static readonly bool pb_170;

    /// <summary>PB-171 : DAS teste l'AL et le CF d'origine, au même seuil que DAA.</summary>
    internal static readonly bool pb_171;

    /// <summary>PB-172 : REP LODSB et REP LODSW chargent AL et AX.</summary>
    internal static readonly bool pb_172;

    /// <summary>PB-173 : SETMO et SETMOC (D0 à D3 /6) mettent l'opérande à FFh (FFFFh).</summary>
    internal static readonly bool pb_173;

    /// <summary>PB-174 : les décalages par CL posent OF, celui du dernier pas.</summary>
    internal static readonly bool pb_174;

    /// <summary>PB-175 : AAM et AAD posent SF et ZF d'après AL.</summary>
    internal static readonly bool pb_175;

    /// <summary>PB-176 : SAR par CL rend CF, la copie du signe, au-delà de 8 (16).</summary>
    internal static readonly bool pb_176;

    /// <summary>PB-177 : rep() — 6Eh est JLE, REP DS: répète, un préfixe placé avant REP vaut.</summary>
    internal static readonly bool pb_177;

    /// <summary>PB-179 : un mot à l'offset FFFFh replie à l'offset 0 du segment ; avec PB-07.</summary>
    internal static readonly bool pb_179;

    /// <summary>PB-246 : la cascade du 8259 se sert à son rang, après l'IRQ 0 et l'IRQ 1.</summary>
    internal static readonly bool pb_246;

    /// <summary>PB-247 : le masque de service du 8259 retient les niveaux de priorité égale ou moindre (8259, 808x).</summary>
    internal static readonly bool pb_247;

    /// <summary>PB-248 : l'OCW2 et l'OCW3 du 8259 selon la fiche : rotations, priorité, poll, masque spécial.</summary>
    internal static readonly bool pb_248;

    /// <summary>PB-255 : après ICW1 et à la mise sous tension, le 8259 se lit sur l'IRR.</summary>
    internal static readonly bool pb_255;

    /// <summary>PB-257 : un transfert de DMA refusé ne coûte pas de cycle (5150, XT, M24, PC1512) ; avec PB-03.</summary>
    internal static readonly bool pb_257;

    /// <summary>PB-258 : l'erreur de division empile dans SS, sous un préfixe de segment ; avec PB-45 et PB-169.</summary>
    internal static readonly bool pb_258;

    static materiel()
    {
        ModeMateriel.Gel();
        pb_01 = ModeMateriel.Demande(1);
        pb_02 = ModeMateriel.Demande(2);
        pb_03 = ModeMateriel.Demande(3);
        pb_05 = ModeMateriel.Demande(5);
        pb_07 = ModeMateriel.Demande(7);
        pb_45 = ModeMateriel.Demande(45);
        pb_87 = ModeMateriel.Demande(87);
        pb_169 = ModeMateriel.Demande(169);
        pb_170 = ModeMateriel.Demande(170);
        pb_171 = ModeMateriel.Demande(171);
        pb_172 = ModeMateriel.Demande(172);
        pb_173 = ModeMateriel.Demande(173);
        pb_174 = ModeMateriel.Demande(174);
        pb_175 = ModeMateriel.Demande(175);
        pb_176 = ModeMateriel.Demande(176);
        pb_177 = ModeMateriel.Demande(177);
        pb_179 = ModeMateriel.Demande(179);
        pb_246 = ModeMateriel.Demande(246);
        pb_247 = ModeMateriel.Demande(247);
        pb_248 = ModeMateriel.Demande(248);
        pb_255 = ModeMateriel.Demande(255);
        pb_257 = ModeMateriel.Demande(257);
        pb_258 = ModeMateriel.Demande(258);
    }
}
