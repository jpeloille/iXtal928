# G7 — La vidéo : Cirrus Logic GD5429 et S3 ; le plan, sur reconnaissance

> Écrit le 1er octobre 2026, après G6 (l'ami486). Bloc G7 de `PLAN.md`.
> Chaque constat cite la ligne de C qui le fonde, sur `pcem-dev/` tel que vendoré.
> Les décisions à trancher sont en fin de fichier ; rien ne s'écrit avant leur validation.

## Où on en est

Le socle SVGA (`vid_svga.cs`, `vid_svga_render.cs`) porte la VGA d'IBM et les deux Trident ;
la sonde VGA de fin de boot-diff compare VRAM, registres, palettes et framebuffer (M15, M19).
Restent OMIS, et les deux cartes de G7 en ont besoin : les accès 16 et 32 bits de la fenêtre
linéaire (`svga_writew_linear`, `svga_writel_linear`, `svga_readw_linear`, `svga_readl_linear`,
`vid_svga.c:1573-1658` — `TRANSCRIPTION.md`, registre des omissions) et les corps des rendus
32 bpp (`vid_svga_render.cs`, `fatal()`).

**Il n'y a pas de « carte VLB » chez PCem** : un seul `gd5429_device` (`vid_cl5429.c:2125-2201`),
qui lit la globale `has_vlb` — posée par `cpu_set` pour un 486 (`cpu.c:194`, transcrite en G6.1).
Aucune des cartes CL ni S3 n'est `DEVICE_PCI` : toutes se montent sur l'ami486.

| | GD5429 | S3 |
|---|---|---|
| fichier | `vid_cl5429.c`, 2 201 lignes | `vid_s3.c`, 3 142 lignes (+ `vid_sdac_ramdac.c`, 185) |
| cartes | `gd5429_device`, ROM `5429.vbi` (présente), 1 ou 2 Mo (défaut 2) | `s3_bahamas64_device` (Vision864, `bahamas64.bin`), `s3_9fx_device` (Trio64, `s3_764.bin`), `s3_phoenix_trio64_device` (`86c764x1.bin`), `s3_phoenix_trio32_device` — toutes les ROM présentes |
| horloge, RAMDAC | internes (SR0B-0E / SR1B-1E, DAC caché par 3C6) | Trio : PLL interne ; Vision864 : SDAC |
| à transcrire | banques GR9/GRA, chemin d'écriture étendu (X8, latches de 8 octets, modes 4/5), fenêtre linéaire, blitter (BitBLT, MMIO en B8000), curseur matériel, rendus 8/15/16/24 bpp | banques, fenêtre linéaire, MMIO A0000, accélérateur 2D (commandes par FIFO), curseur, rendus jusqu'à 32 bpp |
| obstacle | aucun | l'accélérateur tourne dans un THREAD (`fifo_thread`, `:840-913`) : non déterministe ; et sans PCI, `pci_clear_irq(-1)` à chaque trame lit hors tableau (UB) |
| temps vidéo | `video.c:99-100` (bus), déjà transcrit | `video.c:143`, `:156-167` |

## Les étapes

### G7.0 — Le socle : la fenêtre linéaire 16/32 bits, le rendu 32 bpp  ✅ *fait, VERIFICATION.md § G7.0*

`svga_*w_linear`, `svga_*l_linear` (`vid_svga.c:1573-1658`) et les corps 32 bpp. Aucune carte
existante ne les atteint : la porte est l'identité de toutes les séries, plus un banc dirigé
(écritures par la fenêtre, comparées à l'oracle).

### G7.1 — Cirrus Logic GD5429

`vid_cl5429.c` pour la GD5429 seule (décision n° 1), banques, chemin étendu, fenêtre linéaire,
curseur, rendus ; l'oracle l'inclut dans un `harness_cl5429.c` (patron de `harness_tvga.c` : le
`gd5429_t` est privé) avec sa sonde ; souches `pci_add` / `mca_add`. **Porte** : boot-diff
ami486 + GD5429 jusqu'au POST et à DOS, sonde vidéo étendue (registres CL, banques, curseur) ;
puis ami386dx (ISA, `has_vlb` = 0) pour la branche non VLB.

### G7.2 — Le blitter de la GD5429

`gd5429_start_blit` et son MMIO (`:1302-1805`). **Porte** : un banc dirigé (un .COM saisi dans
DEBUG, comme X87BANC et l'ide-check) qui programme des BitBLT, sous boot-diff ; puis Windows 3.1
avec un pilote Cirrus si l'image en porte un (décision n° 4).

### G7.3 — S3 (selon la décision n° 2)

`vid_s3.c` pour la carte retenue, l'accélérateur rendu SYNCHRONE des deux côtés (décision n° 3),
`pci_clear_irq(-1)` sous R9 ou reproduit selon ce qu'il fait réellement (à mesurer d'abord).
**Porte** : boot-diff, sonde, banc d'accélération dirigé.

### G7.4 — Les machines et les témoins

Profils Rider (décision n° 5) ; témoins : POST et mode texte, `MODE`, un programme VESA s'il en
existe un sur les disques, Windows 3.1 en 640 × 480 × 256 et plus.

## Les défauts de PCem déjà relevés (à lire à la ligne, puis inscrire)

1. `vid_s3.c:161-166`, `:889-894`, `:2936` : sans PCI, `card = -1`, puis `pci_clear_irq(-1)` à
   chaque trame — lecture (et écriture si non nul) hors de `pci_irq_routing[]` / `pci_irq_active[]`.
2. `vid_s3.c:3071` : la Bahamas 64 propose 1 ou 2 Mo mais démarre à 4 (`default_int = 4`), ce
   que le commentaire `:3066-3069` dit cassé.
3. `vid_cl5429.c:134` contre `:408` / `:439` : séquenceur étendu écrit à `seqaddr & 0x1f`, lu à
   `& 0x3f`, `switch` de lecture sur l'index non masqué.
4. `vid_cl5429.c:2139` : `avga2_cbm_sl386sx_device` teste la présence de la ROM de la GD5430.
5. `vid_cl5429.c:527-529` : le mode « 128 Ko en A0000 » ne mappe que 64 Ko.

## Les risques

1. **Le thread de l'accélérateur S3.** Un oracle à thread n'est pas un oracle ; la seule voie
   est de vider la FIFO en synchrone des DEUX côtés — une DEVIATION de l'oracle, inscrite (même
   arbitrage que la VRAM de svga_init).
2. **La sonde.** Le diff d'instructions ne voit pas le blitter ni le curseur s'ils ne sont pas
   relus : la sonde vidéo doit les couvrir, sinon un vert ne prouve rien.
3. **La taille.** ≈ 2 200 + 3 300 lignes ; le blitter de la CL et l'accélérateur S3 sont les plus
   denses du dépôt depuis le cœur 386.

## Les décisions à trancher

1. **Cirrus** : la GD5429 seule (proposé), ou aussi les variantes que `vid_cl5429.c` porte à peu
   de frais (GD5428, GD5426 PS/1, AVGA2) ?
2. **S3** : quelle carte ? *(proposé : la Trio64 Phoenix, `86c764x1.bin`, 1/2/4 Mo — la plus
   courante en VLB/PCI de la période ; la Vision864 tire en plus le SDAC)*
3. **L'accélérateur S3 synchrone** des deux côtés, en DEVIATION inscrite ? *(proposé : oui)*
4. **Pilotes Windows** : les images de l'utilisateur portent-elles un pilote Cirrus ou S3 ? Sinon,
   le témoin Windows reste en VGA, et le banc dirigé fait foi.
5. **Profils Rider** : la GD5429 sur le profil ami486 (proposé), la carte S3 sur un second profil ?
