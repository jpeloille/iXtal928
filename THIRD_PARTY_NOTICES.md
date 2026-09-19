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

*Last updated: 2026-09-19*
