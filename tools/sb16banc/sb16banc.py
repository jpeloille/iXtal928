#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# SB16BANC.COM — le banc de la Sound Blaster 16 (G12.1, PLAN-G12.md) ; le programme est sb16banc.S (son
# en-tête dit ce qu'il fait et dans quel ordre). Ce script l'assemble (GNU as en mode 16 bits, ld en binaire
# plat à 100h) et écrit sb16banc.keys : DEBUG, les octets en lignes « E », N, R CX, W, Q, puis le programme
# lancé — le patron d'atapibanc.py.
#
# Usage :
#   python3 tools/sb16banc/sb16banc.py            écrit sb16banc.keys, affiche la taille
#   python3 tools/sb16banc/sb16banc.py --com F    écrit aussi le .COM (objdump -D -b binary -mi8086
#                                               --adjust-vma=0x100 F)

import os
import subprocess
import sys
import tempfile

ORG = 0x100
NOM = 'SB16BANC'


def assemble(here):
    with tempfile.TemporaryDirectory() as t:
        obj, com = os.path.join(t, 'a.o'), os.path.join(t, 'a.com')
        subprocess.run(['as', '--32', '-o', obj, os.path.join(here, 'sb16banc.S')], check=True)
        subprocess.run(['ld', '-m', 'elf_i386', f'-Ttext=0x{ORG:x}', '--oformat=binary', '-o', com, obj], check=True)
        return open(com, 'rb').read()


def keys(code):
    lines = ['DEBUG']
    for i in range(0, len(code), 16):
        lines.append(f'E {ORG + i:X} ' + ' '.join(f'{b:02X}' for b in code[i:i + 16]))
    lines += [f'N {NOM}.COM', 'R CX', f'{len(code):X}', 'W', 'Q', NOM]
    return lines


if __name__ == '__main__':
    here = os.path.dirname(os.path.abspath(__file__))
    code = assemble(here)
    # Les tampons (BUF8 en 1000h, BUF16, RECB, RES) doivent rester au-delà du programme.
    assert ORG + len(code) <= 0x1000, len(code)
    print(f'{len(code)} octets')
    with open(os.path.join(here, 'sb16banc.keys'), 'w') as f:
        f.write('\n'.join(keys(code)) + '\n')
    if '--com' in sys.argv:
        with open(sys.argv[sys.argv.index('--com') + 1], 'wb') as f:
            f.write(code)
