# Le CMOS non volatil des machines qui en ont un.
#
# nvrfopen (nvr.c:33-54) compose « nvr/<config>.<machine>.nvr » en premier, et
# retombe sur « nvr/default/<machine>.nvr » en LECTURE SEULE — le CMOS de
# reference qu'un emulateur peut livrer.
#
# CE REPERTOIRE DOIT EXISTER : nvrfopen rend NULL en ecriture si le chemin manque,
# et savenvr ne verifie pas (PB-33). PCem livre le sien pour la meme raison.
