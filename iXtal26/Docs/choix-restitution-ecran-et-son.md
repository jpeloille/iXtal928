# Restitution : l'écran et le son — les choix retenus

Ce document consigne les choix techniques de Julien pour tout ce qui se trouve **entre la
machine émulée et l'hôte** : ce qu'on voit à l'écran et ce qu'on entend. La suite du
chantier écran est dans [plan-affichage.md](plan-affichage.md).

## Le principe commun

**La machine émulée n'est jamais touchée.** L'écran agit sur la fenêtre, le renderer et
des textures superposées, jamais sur `video.Buffer32`. Le son agit sur la copie int16
que `SdlAudio` envoie à SDL, jamais sur le tampon du mixeur (`outbuffer`). Les oracles
(`boot-diff`, empreintes de framebuffer, `speaker-probe`, `sound_hash`) restent aveugles
à la restitution, et c'est voulu : ils prouvent la machine, la restitution est un
réglage d'hôte.

**Par défaut, l'époque ; en option, la restitution moderne complète.** Écran : un
moniteur simulé, avec les pixels entiers en option. Son : le haut-parleur du 5150, avec
le signal complet en option.

**Tout se règle depuis le menu Ctrl+F12** et se retient dans la section `[SDL2]` du `.cfg`
machine. Ces clés n'existent pas chez PCem (DEVIATION, voir `DisplaySettings.cs`). Elles
ne sont réécrites que dans `configs/` : un `.cfg` documenté à la main ne perd pas ses
commentaires. Précédence : défauts, puis `.cfg`, puis ligne de commande.

**Les libellés de l'interface ne citent jamais PCem.** Ils décrivent ce qu'on obtient
(« réglage d'usine », « son fidèle », « pixels entiers »), pas l'origine du code.

## L'écran

| Choix | Retenu | Pourquoi |
|---|---|---|
| Surface | Toute trame remplit la surface 4:3 **visible** du tube, quelle que soit la résolution de l'invité | Un moniteur d'époque étale toute trame sur le même verre. La fenêtre ne suit plus les changements de mode de l'invité |
| Moniteur par défaut | `auto` : NEC MultiSync 3V derrière une VGA ou une Trident, générique 14" derrière la CGA | Le 3V ne synchronise ni la CGA (15,7 kHz) ni l'EGA (21,8 kHz) |
| NEC MultiSync 3V | JC-1535VMA, 1994 : 15" annoncés, 14" visibles (284,5 × 213,4 mm), 31-50 kHz, 55-90 Hz | Fiche crtdatabase.com. Les plages sont **respectées** : hors plage, écran noir et message, comme le vrai (au démarrage de la 9000B, 39,5 kHz / 154 Hz) |
| Génériques | 14, 15 et 17", 93 % de la diagonale visible, sans limite de fréquence | 0,93 est le ratio des NEC MultiSync (clé `visible_fraction`) |
| Taille réelle | Pixel hôte déduit de `--host-diagonal` et de la résolution native ; `--pixel-mm` l'emporte ; repli 0,2331 mm (27" en 2560 × 1440) | SDL3 ne donne pas la taille physique de la dalle |
| Taille d'image | 90 % de la surface visible par défaut, 70 à 100 %, marge noire (`--fill`, `fill_percent`) | Les molettes H-SIZE/V-SIZE : l'image ne touchait pas les bords du tube |
| Filtrage | Linéaire (« doux ») par défaut, plus proche voisin (« net ») au choix (`scale_mode`, avec le sens de la clé chez PCem) | Le facteur n'est pas entier : le linéaire évite des colonnes d'épaisseur inégale, et son flou est celui du faisceau |
| Pixels entiers | Option (`--monitor entier`) : facteur entier, proportions de la trame, facteur round(0,42 / pixel hôte) | La restitution moderne nette. Toujours en « net » ; taille d'image sans objet |
| Lignes de balayage | Option (`--crt`), dessinées seulement s'il y a au moins 2 pixels hôte par ligne émulée | En dessous, elles font du moiré au lieu de lignes |
| HiDPI | Fenêtre en `HighPixelDensity` | Pas de double mise à l'échelle |
| Technique | Textures superposées, **aucun shader** | Masque de phosphore, halo et courbure viendront plus tard (plan-affichage.md) |

## Le son

**Le constat.** Le mixeur rend un carré exact, 0 / 0x1400. La chaîne n'a aucun filtre, et
PipeWire tourne à 48 kHz comme le flux, donc sans rééchantillonnage. Rendu en entier par
un casque ou des enceintes modernes, ce carré paraît **rond** : on entend sa fondamentale.
Le haut-parleur du 5150 ne la rendait presque pas.

**Le choix : modéliser le haut-parleur, côté hôte** (`Host/SpeakerModel.cs`).

| Choix | Retenu | Pourquoi |
|---|---|---|
| Défaut | « Réglage d'usine » : le cône de 57 mm du 5150, sans baffle, dans un châssis métallique | Le son de l'époque : nasillard, métallique, criard |
| Option | « Son fidèle » : le signal complet, tel que le mixeur le rend | La restitution moderne complète |
| Emplacement | Dans `SdlAudio.GiveBuffer`, sur la copie int16 | Le tampon du mixeur, `sound_hash` et `speaker-probe` restent identiques à l'oracle |
| Modèle | Passe-haut 2ᵉ ordre à 350 Hz, résonance 2,8 kHz (Q 2, +6 dB), passe-bas 8 kHz, gain ×2,5, saturation douce (tanh, 24 000) | Respectivement : le cône sans baffle et la composante continue du carré unipolaire, la résonance du cône, sa limite haute, le niveau fort d'origine |
| Calcul | Biquads RBJ (« Audio EQ Cookbook »), en double, 48 kHz, état conservé d'un bloc à l'autre | Pas de discontinuité entre blocs |
| Canaux | Le canal gauche est filtré et recopié à droite | Le haut-parleur est mono ; `speaker_get_buffer` duplique déjà |
| Bascule | Remet les filtres à zéro | L'état d'une chaîne coupée ne vaut plus rien |
| Coefficients | **À l'estime**, à ajuster à l'oreille ou sur un enregistrement d'une vraie machine | Aucune mesure publiée du 5150 n'a servi |

**La mesure** (`--speaker-check [DOSSIER]`, bip du POST, 896 Hz) :

| | h1 | h3 | h5 | crête | valeur efficace |
|---|---|---|---|---|---|
| son fidèle | −20,0 dB | −29,6 dB | −34,0 dB | 5 120 | 3 620 |
| réglage d'usine | −12,5 dB | −16,6 dB | −26,7 dB | 13 242 | 6 602 |

L'écart entre la 3ᵉ harmonique et la fondamentale passe de −9,5 à −4,1 dB, et la crête
reste loin de la pleine échelle. Avec un dossier, les deux versions sont écrites en WAV
pour l'écoute.

## Portes

Restitution seule : `--menu-check` (et `--speaker-check` pour le son), plus un lancement
fenêtré. Le son a aussi été passé aux deux boot-diffs (5150, XT) et à `speaker-probe`,
verts : la preuve que le mixeur n'a pas bougé. Pas de fuzz tant qu'aucun commit ne
touche au cœur, à `Buffer32` ou au mixeur.
