# Third-Party Notices

iXtal26 is a C# transcription of **PCem**, and is therefore a derivative work of it. This
file reproduces the upstream notices that travel with that code, as the applicable
licences require.

Listing a project here does not imply that its authors endorse iXtal26.

---

## PCem

**Authors:** Sarah Walker and the PCem contributors
**Source:** <https://github.com/sarah-walker-pcem/pcem> · <https://pcem-emulator.co.uk/>
**Version:** v18 (see `VENDORED.md` for the content hash of the vendored tree)
**License:** **GNU General Public License, version 2.0** — full text in `COPYING`

iXtal26's emulation core is translated from PCem's C source, file by file, preserving its
structure and identifiers so the original remains usable as a verification oracle. This is
a translation in the sense of GPL v2 §2, so **iXtal26 as a whole is licensed GPL v2.0**
(SPDX: `GPL-2.0-only` — upstream states "GPL v2.0" with no "or later" clause).

The reference source is vendored unmodified at `pcem-dev/`, including its own `COPYING`
and `NOTICE`.

---

## Components bundled inside PCem

PCem itself incorporates code from the projects below, listed in `pcem-dev/NOTICE` with
their full author lists and licence notices. That file is the authoritative record and is
vendored with the source.

| Component | PCem location | Incorporated into iXtal26? |
|---|---|---|
| **DOSBox** (Sjoerd v.d. Berg, Peter Veenstra, et al. — GPL v2) | `src/dosbox/` — OPL, CGA composite, CD-ROM image | **No.** The CGA composite path is deliberately not transcribed |
| **Ayumi** (Peter Sovietov — MIT) | `src/sound/ayumi/` — AY-3-8910 | **No.** Sound is out of scope for the XT target |
| **reSID / reSID-fp** (Dag Lem, Antti Lankila — GPL v2) | `src/sound/resid-fp/` — SID | **No** |
| **MiniVHD** (Sherman Perry — MIT) | VHD disk images | **No** |
| **Slirp** (Danny Gasparovski et al.) | `src/networking/` — user-mode networking | **No** |

As the scope grows beyond the IBM PC/XT target, any component that starts being
transcribed must be moved to "yes" here, with its upstream notice reproduced in full from
`pcem-dev/NOTICE`.

---

## Berkeley SoftFloat, Release 3e

**Author:** John R. Hauser
**Source:** <http://www.jhauser.us/arithmetic/SoftFloat.html> — `SoftFloat-3e.zip`,
sha256 `21130ce885d35c1fe73fc1e1bf2244178167e05c6747cad5f450cc991714c746`
**License:** **BSD 3-Clause** (SPDX: `BSD-3-Clause`), compatible with GPL v2.0

`iXtal26.SoftFloat/` is a C# rewrite of the 80-bit subset of SoftFloat 3e (the
`8086` specialization, which follows the x87 rules for NaNs), file by file. Each rewritten
file carries the copyright line and the full notice of the C file it comes from;
`iXtal26.SoftFloat/COPYING.txt` is the upstream licence. It is verified against
TestFloat 3e, by the same author and under the same licence, which is fetched and built
outside the repository (`tools/softfloat/outils/preparer.sh`); no TestFloat code is
redistributed here. The upstream licence, as distributed:

```
License for Berkeley SoftFloat Release 3e

John R. Hauser
2018 January 20

The following applies to the whole of SoftFloat Release 3e as well as to
each source file individually.

Copyright 2011, 2012, 2013, 2014, 2015, 2016, 2017, 2018 The Regents of the
University of California.  All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

 1. Redistributions of source code must retain the above copyright notice,
    this list of conditions, and the following disclaimer.

 2. Redistributions in binary form must reproduce the above copyright
    notice, this list of conditions, and the following disclaimer in the
    documentation and/or other materials provided with the distribution.

 3. Neither the name of the University nor the names of its contributors
    may be used to endorse or promote products derived from this software
    without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE REGENTS AND CONTRIBUTORS "AS IS", AND ANY
EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE, ARE
DISCLAIMED.  IN NO EVENT SHALL THE REGENTS OR CONTRIBUTORS BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF
THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

---

## SingleStepTests

**Authors:** Daniel Balsom (GloriousCow) and the SingleStepTests contributors
**Source:** <https://github.com/SingleStepTests/8088>

iXtal26 consumes JSON test vectors from the SingleStepTests 8088 `v2` suite to validate
the CPU core. The vectors were captured from a physical AMD D8088 via the Arduino8088 rig.

**No SingleStepTests source code is redistributed here.** The vectors are fetched at test
time by `tools/fetch-sst.sh` into a `.gitignored` cache; only `vectors/sst/MANIFEST.sha256`
is committed. Anyone redistributing iXtal26 *with* the vectors bundled must check the
upstream licence terms and reproduce them here.

---

*Last updated: 2026-10-11*
