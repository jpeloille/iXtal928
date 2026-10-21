#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# LPTPLAY.COM — l'écoute du port parallèle (GR.3i, PLAN-GR.md) ; le programme est lptplay.S (son en-tête dit ce qu'il
# joue et comment). Ce script l'assemble (GNU as en mode 16 bits, ld en binaire plat à 100h), le patron d'awebanc.py.
# Aucune porte ne le joue : il sert l'écoute, et tools/ecoute/ecoute.sh le pose sur la disquette d'écoute.
#
# Usage :
#   python3 tools/lptplay/lptplay.py            affiche la taille
#   python3 tools/lptplay/lptplay.py --com F    écrit aussi le .COM (objdump -D -b binary -mi8086 --adjust-vma=0x100 F)

import os
import subprocess
import sys
import tempfile

ORG = 0x100


def assemble(here):
    with tempfile.TemporaryDirectory() as t:
        obj, com = os.path.join(t, 'a.o'), os.path.join(t, 'a.com')
        subprocess.run(['as', '--32', '-o', obj, os.path.join(here, 'lptplay.S')], check=True)
        subprocess.run(['ld', '-m', 'elf_i386', f'-Ttext=0x{ORG:x}', '--oformat=binary', '-o', com, obj], check=True)
        return open(com, 'rb').read()


if __name__ == '__main__':
    code = assemble(os.path.dirname(os.path.abspath(__file__)))
    print(f'LPTPLAY.COM : {len(code)} octets')
    if '--com' in sys.argv:
        with open(sys.argv[sys.argv.index('--com') + 1], 'wb') as f:
            f.write(code)
