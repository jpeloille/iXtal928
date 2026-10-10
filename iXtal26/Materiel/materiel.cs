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

    /// <summary>PB-32 : l'IDT trop courte lève #GP(n × 8 + 2 + EXT) ; avec PB-192.</summary>
    internal static readonly bool pb_32;

    /// <summary>PB-39 : CALL et JMP sur une porte de tâche changent de tâche.</summary>
    internal static readonly bool pb_39;

    /// <summary>PB-40 : CALL sur une tâche n'empile pas l'adresse de retour.</summary>
    internal static readonly bool pb_40;

    /// <summary>PB-43 : MOV CRx, DRx et TRx ignorent le champ mod (386, 486).</summary>
    internal static readonly bool pb_43;

    /// <summary>PB-45 : IDIV octet divise AX signé ; avec PB-169.</summary>
    internal static readonly bool pb_45;

    /// <summary>PB-50 : une instruction de plus de 15 octets (10 sur le 286) lève #GP ; avec PB-51.</summary>
    internal static readonly bool pb_50;

    /// <summary>PB-51 : la lecture d'une instruction contrôle la limite de CS (286, 386, 486) ; avec PB-50.</summary>
    internal static readonly bool pb_51;

    /// <summary>PB-57 : FCOM, FCOMP et FCOMPP de registre comparent comme le silicium, au temps de fcom ; avec PB-64 et PB-70.</summary>
    internal static readonly bool pb_57;

    /// <summary>PB-58 : FCOMPP compare −0 et +0 égaux, sans le contournement de détection de PCem ; avec PB-57.</summary>
    internal static readonly bool pb_58;

    /// <summary>PB-61 : FNSTSW AX rend le mot d'état entier, TOP compris (287, 387, 486).</summary>
    internal static readonly bool pb_61;

    /// <summary>PB-63 : FXAM rend la classe de ST(0) : NaN, infini, zéro, normal, vide, et le signe dans C1.</summary>
    internal static readonly bool pb_63;

    /// <summary>PB-64 : FTST, un NaN non ordonné ; avec PB-57 et PB-70.</summary>
    internal static readonly bool pb_64;

    /// <summary>PB-66 : les constantes du x87 au plus près (ln 2 compris), et selon RC sur le 287XL, le 387 et le 486.</summary>
    internal static readonly bool pb_66;

    /// <summary>PB-67 : FST et FSTP ST(i) copient aussi l'entier exact de TAG_UINT64.</summary>
    internal static readonly bool pb_67;

    /// <summary>PB-70 : le 8087 et le 287 comparent en projectif tant que IC est nul ; avec PB-57 et PB-64.</summary>
    internal static readonly bool pb_70;

    /// <summary>PB-78 : LOADALL386 lève #UD sur le 486.</summary>
    internal static readonly bool pb_78;

    /// <summary>PB-87 : au repli de l'IP, la lecture d'instruction se fait à l'offset 0 du segment.</summary>
    internal static readonly bool pb_87;

    /// <summary>PB-94 : la souris PS/2 répond FAh à F6h, EAh, F0h, EEh et ECh, et FEh ou FCh à une commande inconnue.</summary>
    internal static readonly bool pb_94;

    /// <summary>PB-95 : l'octet d'état de la souris PS/2 (E9h) selon IBM, et le mode distant en bit 6.</summary>
    internal static readonly bool pb_95;

    /// <summary>PB-101 : le PC1512 n'a pas de LPT2 à 278h.</summary>
    internal static readonly bool pb_101;

    /// <summary>PB-103 : le chapeau de la CH Flightstick Pro et de la TM FCS lit 315° en haut.</summary>
    internal static readonly bool pb_103;

    /// <summary>PB-157 : la commande du 8237 haut est rangée, et DAh se lit sur le temporaire.</summary>
    internal static readonly bool pb_157;

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

    /// <summary>PB-181 : l'AF d'ADC du cœur 286/386/486 est la retenue du bit 3, retenue entrante comprise.</summary>
    internal static readonly bool pb_181;

    /// <summary>PB-182 : LOCK lève #UD devant une instruction qui ne se verrouille pas, ou sa forme registre (386, 486).</summary>
    internal static readonly bool pb_182;

    /// <summary>PB-183 : BT, BTS, BTR et BTC déplacent l'adresse d'un décalage signé.</summary>
    internal static readonly bool pb_183;

    /// <summary>PB-184 : MOVSX r16,r/m16 (0F BF sans 66h) s'exécute : la table du mode, posée par cpu_set.</summary>
    internal static readonly bool pb_184;

    /// <summary>PB-185 : AAA et AAS du cœur 286/386/486 ajustent AX entier (AX + 106h, AX − 6 puis AH − 1).</summary>
    internal static readonly bool pb_185;

    /// <summary>PB-186 : AAD et AAM du cœur 286/386/486 posent SF, ZF et PF d'après AL.</summary>
    internal static readonly bool pb_186;

    /// <summary>PB-187 : AAM 0 du cœur 286/386/486 lève #DE.</summary>
    internal static readonly bool pb_187;

    /// <summary>PB-188 : le DAS du cœur 286/386/486 teste l'AL et le CF d'origine.</summary>
    internal static readonly bool pb_188;

    /// <summary>PB-189 : un opérande en mémoire tient entier dans la limite de son segment, sinon #GP(0) ou #SS(0).</summary>
    internal static readonly bool pb_189;

    /// <summary>PB-190 : LTR contrôle le sélecteur, le type et la présence de la TSS.</summary>
    internal static readonly bool pb_190;

    /// <summary>PB-191 : la voie TSS des CALL, JMP et INT contrôle le DPL, la présence, la GDT et le type.</summary>
    internal static readonly bool pb_191;

    /// <summary>PB-192 : la porte doit tenir tout entière dans l'IDT, et EXT marque un événement externe ; avec PB-32.</summary>
    internal static readonly bool pb_192;

    /// <summary>PB-193 : LOADALL386 lève #GP(0) hors du niveau 0, en mode protégé.</summary>
    internal static readonly bool pb_193;

    /// <summary>PB-246 : la cascade du 8259 se sert à son rang, après l'IRQ 0 et l'IRQ 1.</summary>
    internal static readonly bool pb_246;

    /// <summary>PB-247 : le masque de service du 8259 retient les niveaux de priorité égale ou moindre (8259, 808x).</summary>
    internal static readonly bool pb_247;

    /// <summary>PB-248 : l'OCW2 et l'OCW3 du 8259 selon la fiche : rotations, priorité, poll, masque spécial.</summary>
    internal static readonly bool pb_248;

    /// <summary>PB-249 : Clear Mask (0Eh, DCh) efface les quatre masques du 8237.</summary>
    internal static readonly bool pb_249;

    /// <summary>PB-250 : le registre de requête du 8237, la requête logicielle en mode bloc.</summary>
    internal static readonly bool pb_250;

    /// <summary>PB-251 : le master clear du 8237 efface la commande, l'état et la requête.</summary>
    internal static readonly bool pb_251;

    /// <summary>PB-252 : au reset, le 8237 pose ses masques et efface la commande, l'état et la requête.</summary>
    internal static readonly bool pb_252;

    /// <summary>PB-253 : les canaux 0 à 3 de l'AT n'ont le bus que par la cascade du canal 4.</summary>
    internal static readonly bool pb_253;

    /// <summary>PB-254 : les files du 8042 ; le tampon du clavier de l'AT, seize codes et le débordement.</summary>
    internal static readonly bool pb_254;

    /// <summary>PB-255 : après ICW1 et à la mise sous tension, le 8259 se lit sur l'IRR.</summary>
    internal static readonly bool pb_255;

    /// <summary>PB-257 : un transfert de DMA refusé ne coûte pas de cycle (5150, XT, M24, PC1512) ; avec PB-03.</summary>
    internal static readonly bool pb_257;

    /// <summary>PB-258 : l'erreur de division empile dans SS, sous un préfixe de segment ; avec PB-45 et PB-169.</summary>
    internal static readonly bool pb_258;

    /// <summary>PB-261 : la souris PS/2 remet ses compteurs de mouvement à zéro après le paquet d'EBh.</summary>
    internal static readonly bool pb_261;

    /// <summary>PB-262 : le décalage immédiat d'un BT, BTS, BTR ou BTC 16 bits se prend modulo 16.</summary>
    internal static readonly bool pb_262;

    /// <summary>PB-263 : après un CALL de tâche, l'IP contrôlé contre la limite du nouveau CS, #TS(0) (386, 486).</summary>
    internal static readonly bool pb_263;

    static materiel()
    {
        ModeMateriel.Gel();
        pb_01 = ModeMateriel.Demande(1);
        pb_02 = ModeMateriel.Demande(2);
        pb_03 = ModeMateriel.Demande(3);
        pb_05 = ModeMateriel.Demande(5);
        pb_07 = ModeMateriel.Demande(7);
        pb_32 = ModeMateriel.Demande(32);
        pb_39 = ModeMateriel.Demande(39);
        pb_40 = ModeMateriel.Demande(40);
        pb_43 = ModeMateriel.Demande(43);
        pb_45 = ModeMateriel.Demande(45);
        pb_50 = ModeMateriel.Demande(50);
        pb_51 = ModeMateriel.Demande(51);
        pb_57 = ModeMateriel.Demande(57);
        pb_58 = ModeMateriel.Demande(58);
        pb_61 = ModeMateriel.Demande(61);
        pb_63 = ModeMateriel.Demande(63);
        pb_64 = ModeMateriel.Demande(64);
        pb_66 = ModeMateriel.Demande(66);
        pb_67 = ModeMateriel.Demande(67);
        pb_70 = ModeMateriel.Demande(70);
        pb_78 = ModeMateriel.Demande(78);
        pb_87 = ModeMateriel.Demande(87);
        pb_94 = ModeMateriel.Demande(94);
        pb_95 = ModeMateriel.Demande(95);
        pb_101 = ModeMateriel.Demande(101);
        pb_103 = ModeMateriel.Demande(103);
        pb_157 = ModeMateriel.Demande(157);
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
        pb_181 = ModeMateriel.Demande(181);
        pb_182 = ModeMateriel.Demande(182);
        pb_183 = ModeMateriel.Demande(183);
        pb_184 = ModeMateriel.Demande(184);
        pb_185 = ModeMateriel.Demande(185);
        pb_186 = ModeMateriel.Demande(186);
        pb_187 = ModeMateriel.Demande(187);
        pb_188 = ModeMateriel.Demande(188);
        pb_189 = ModeMateriel.Demande(189);
        pb_190 = ModeMateriel.Demande(190);
        pb_191 = ModeMateriel.Demande(191);
        pb_192 = ModeMateriel.Demande(192);
        pb_193 = ModeMateriel.Demande(193);
        pb_246 = ModeMateriel.Demande(246);
        pb_247 = ModeMateriel.Demande(247);
        pb_248 = ModeMateriel.Demande(248);
        pb_249 = ModeMateriel.Demande(249);
        pb_250 = ModeMateriel.Demande(250);
        pb_251 = ModeMateriel.Demande(251);
        pb_252 = ModeMateriel.Demande(252);
        pb_253 = ModeMateriel.Demande(253);
        pb_254 = ModeMateriel.Demande(254);
        pb_255 = ModeMateriel.Demande(255);
        pb_257 = ModeMateriel.Demande(257);
        pb_258 = ModeMateriel.Demande(258);
        pb_261 = ModeMateriel.Demande(261);
        pb_262 = ModeMateriel.Demande(262);
        pb_263 = ModeMateriel.Demande(263);
    }
}
