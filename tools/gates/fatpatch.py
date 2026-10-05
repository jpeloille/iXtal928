#!/usr/bin/env python3
# fatpatch.py IMAGE NOM FICHIER — remplace le contenu d'un fichier de la racine d'une partition
# FAT d'une image de disque dur (première partition de la MBR), SANS changer sa chaîne de
# clusters : le nouveau contenu doit tenir dans le premier cluster. Sert à g5w-recipe.sh pour
# retirer `KEYB FR` de l'AUTOEXEC.BAT des copies (KeyScript tape en QWERTY).
# G11.1 — `--disquette` en quatrième argument : l'image est une disquette, sans MBR (secteur 0 = amorce).
import struct, sys
img, name, src = sys.argv[1], sys.argv[2], sys.argv[3]
disquette = sys.argv[4:] == ['--disquette']
content = open(src, 'rb').read()
n, _, x = name.upper().partition('.')
key = n.ljust(8).encode() + x.ljust(3).encode()
f = open(img, 'r+b'); mbr = f.read(512)
if disquette:
    off = 0
else:
    p = [mbr[446 + 16 * k:462 + 16 * k] for k in range(4) if mbr[450 + 16 * k]][0]
    off = struct.unpack('<I', p[8:12])[0] * 512
f.seek(off); bs = f.read(512)
bps, spc, res, nf, rootn, tot16, med, fsz = struct.unpack('<HBHBHHBH', bs[11:24])
root = off + (res + nf * fsz) * bps; data = root + rootn * 32
f.seek(root); r = f.read(rootn * 32)
for i in range(0, len(r), 32):
    if r[i:i + 11] == key:
        c = struct.unpack('<H', r[i + 26:i + 28])[0]
        assert len(content) <= spc * bps, "le contenu dépasse un cluster"
        f.seek(data + (c - 2) * spc * bps); f.write(content)
        f.seek(root + i + 28); f.write(struct.pack('<I', len(content)))
        sys.exit(0)
sys.exit(f"{name} absent de la racine")
