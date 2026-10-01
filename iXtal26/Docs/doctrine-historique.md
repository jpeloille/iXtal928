# Historique de la doctrine — ce qui a quitté TRANSCRIPTION.md

Déplacé le 1er octobre 2026, quand R9 a fait passer TRANSCRIPTION.md au-delà de son plafond
(251 lignes pour 240) : de la prose qui raconte, pas des règles. Aucune règle n'est perdue ;
R3 renvoie ici.

## R3 — le plafond et ses relèvements, texte d'origine

- **R3 — un seul fichier de prose, plafonné.** Celui-ci, 240 lignes — 200 jusqu'à M6.1,
  puis 220, puis 225 à M12 pour payer les cinq entrées du disque dur au registre des
  omissions : une famille de périphériques entière y entrait d'un coup. **Puis 240 au
  jalon 286**, et pour la même raison qu'à M12 : `x86seg.c` entre d'un bloc au bloc C —
  deux mille lignes vives, dont `loadcsjmp`, `loadcscall`, `pmoderetf`, `pmodeint`,
  `pmodeiret` et `taskswitch286` — et le mode protégé est le premier domaine de ce dépôt
  dont **le seul oracle est PCem lui-même**, sans SingleStepTests ni amorçage
  indépendant pour le départager. Ce qu'on y omet doit donc être écrit plus
  précisément, pas moins : une omission qu'aucun oracle ne peut contredire ne tient que
  par sa justification. Le fichier était à 225/225 exactement quand B1b s'est terminé,
  et B3 n'avait plus une ligne. Le relèvement
  s'inscrit ici, à chaque fois : un plafond qui bouge sans trace ne plafonne plus. Deux registres de
  **constats** en sont exemptés, parce que le plafond vise la prose de conception et pas
  les faits mesurés : `VERIFICATION.md` (ce que les oracles ont montré) et
  `PCEM_BUGS.md` (les défauts trouvés dans PCem lui-même, identifiants `PB-nn`, cités
  par les marqueurs `// pcem bug, reproduced:` du code). Les données volumineuses vont
  dans des fichiers générés (`sst-baseline.tsv`, `oracle.tsv`), jamais ici.
