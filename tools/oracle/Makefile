# SPDX-FileCopyrightText: 2026 Julien Peloille
# SPDX-License-Identifier: GPL-2.0-only
#
# L'oracle : libixtal26oracle.so, le vrai cœur 8088 de PCem pilotable depuis C#.
#
# Zéro dépendance système. Ni SDL, ni wx, ni OpenAL, ni GL, ni ninja, ni clang :
# gcc et make suffisent. C'est ce qui rend M0 à M3 vérifiables sans un seul
# apt install, et donc ce qui permet de commencer la transcription aujourd'hui.
#
#   make            construit la bibliothèque
#   make selftest   construit et exécute l'autotest (aucun C# requis)
#   make bench      construit build/bench, le banc « C de production » (voir bench.c)
#   make clean

PCEM    := ../../pcem-dev
BUILD   := build
TARGET  := libixtal26oracle.so

# Ordre des -I repris de pcem-dev/includes/includes.cmake : tous les private/*
# d'abord, public en DERNIER. L'ordre compte : includes/public/pcem/cpu.h
# masquerait includes/private/cpu/cpu.h si public passait devant.
# $(wildcard .../*) attrape aussi les .h posés à plat dans private/ ; on ne garde
# que les répertoires, sinon gcc avertit « not a directory » vingt fois.
PRIVDIRS := $(sort $(dir $(wildcard $(PCEM)/includes/private/*/.)))

INC := -I. -I$(PCEM)/includes/private \
       $(patsubst %,-I%,$(PRIVDIRS)) \
       -I$(PCEM)/includes/public

# -fno-strict-aliasing : PCem fait de la réinterprétation de types un peu partout
# (readlookup2 stocke des pointeurs hôtes biaisés, les unions x86reg/MMX_REG).
# Les mêmes drapeaux que le build amont Release, moins -flto et -march.
CFLAGS := -O2 -fno-strict-aliasing -fPIC -g \
          -Wall -Wno-unused-function -Wno-unused-variable -Wno-unused-but-set-variable \
          -Wno-misleading-indentation -Wno-implicit-fallthrough
LDFLAGS := -shared
LDLIBS  := -lm

# harness.c fait #include "808x.c" — 808x.c n'est donc PAS compilé séparément,
# sans quoi chaque symbole serait défini deux fois. Voir l'en-tête de harness.c.
# Même chose pour 386_dynarec.c (harness_386.c) et vid_tvga.c (harness_tvga.c, M19).
SRC := harness.c \
       harness_stubs.c \
       harness_wrap.c \
       harness_fetch.c \
       harness_386.c \
       harness_tvga.c \
       harness_cl5429.c \
       harness_s3.c \
       harness_m24.c \
       harness_pc1512.c \
       $(PCEM)/src/sound/sound_opl.c \
       $(PCEM)/src/sound/sound_adlib.c \
       $(PCEM)/src/models/amstrad.c \
       $(PCEM)/src/keyboard/keyboard_amstrad.c \
       $(PCEM)/src/models/olivetti_m24.c \
       $(PCEM)/src/keyboard/keyboard_olim24.c \
       $(PCEM)/src/joystick/gameport.c \
       $(PCEM)/src/joystick/joystick_standard.c \
       $(PCEM)/src/joystick/joystick_ch_flightstick_pro.c \
       $(PCEM)/src/joystick/joystick_sw_pad.c \
       $(PCEM)/src/joystick/joystick_tm_fcs.c \
       harness_x87.c \
       $(PCEM)/src/cpu/x86seg.c \
       $(PCEM)/src/cpu/x87.c \
       $(PCEM)/src/cpu/x87_timings.c \
       $(PCEM)/src/cpu/386_common.c \
       $(PCEM)/src/cpu/386.c \
       $(PCEM)/src/cpu/cpu.c \
       $(PCEM)/src/cpu/cpu_tables.c \
       $(PCEM)/src/memory/mem.c \
       $(PCEM)/src/io.c \
       $(PCEM)/src/timer.c \
       $(PCEM)/src/models/pit.c \
       $(PCEM)/src/models/pic.c \
       $(PCEM)/src/models/dma.c \
       $(PCEM)/src/ppi.c \
       $(PCEM)/src/models/nmi.c \
       $(PCEM)/src/plugin-api/device.c \
       $(PCEM)/src/flash/rom.c \
       $(PCEM)/src/memory/mem_bios.c \
       $(PCEM)/src/keyboard/keyboard.c \
       $(PCEM)/src/keyboard/keyboard_xt.c \
       $(PCEM)/src/keyboard/keyboard_at.c \
       $(PCEM)/src/devices/nvr.c \
       $(PCEM)/src/models/neat.c \
       $(PCEM)/src/models/headland.c \
       $(PCEM)/src/models/opti495.c \
       $(PCEM)/src/models/ali1429.c \
       $(PCEM)/src/rtc.c \
       $(PCEM)/src/video/vid_cga.c \
       $(PCEM)/src/video/vid_vga.c \
       $(PCEM)/src/video/vid_svga.c \
       $(PCEM)/src/video/vid_svga_render.c \
       $(PCEM)/src/video/vid_tkd8001_ramdac.c \
       $(PCEM)/src/video/vid_sdac_ramdac.c \
       $(PCEM)/src/models/serial.c \
       $(PCEM)/src/mouse/mouse_serial.c \
       $(PCEM)/src/floppy/fdc.c \
       $(PCEM)/src/floppy/fdd.c \
       $(PCEM)/src/disc/disc.c \
       $(PCEM)/src/disc/disc_img.c \
       $(PCEM)/src/disc/disc_sector.c \
       $(PCEM)/src/mfm/mfm_xebec.c \
       $(PCEM)/src/mfm/mfm_at.c \
       $(PCEM)/src/ide/ide.c \
       $(PCEM)/src/hdd/hdd_file.c

# Interposition sur les fonctions de PCem pour y poser compteurs et journal
# d'écritures SANS modifier l'arbre vendoré. Voir harness_wrap.c.
comma := ,
# writememll / readmemll ajoutes a C7a : writememl est une MACRO (386_common.h:32-36)
# qui descend dans writememll, absent de WRAP jusqu ici. x86_doabrt empile le code
# d erreur par writememl des que intgatesize != 16 — et intgatesize vaut 0 au depart —
# donc cette ecriture n avait AUCUN temoin : ni compteur (les quatre retires a M2), ni
# journal, ni hachage (RunSingle n appelle jamais h_ram_hash).
WRAP := readmembl writemembl readmemwl writememwl writememll readmemll inb outb picint picinterrupt timer_process
LDFLAGS += $(patsubst %,-Wl$(comma)--wrap=%,$(WRAP))

# Une interposition D'UNE AUTRE NATURE, et qui vaut pour la .so ET pour bench.
#
# Depuis que cpu.c est lié, resetx86() (808x.c:676) appelle le VRAI
# cpu_update_waitstates(), qui déréférence models[model]->cpu[...]. Or models[] est un
# tableau de pointeurs nuls ici : le harnais n'appelle jamais cpu_set() et ne lie pas
# model.c. Segmentation fault au premier reset, attrapée par l'autotest.
#
# Le no-op était le comportement du palier (a) — harness_stubs.c le documentait :
# « Le 8088 ne modélise pas de prefetch côté mémoire, ces compteurs restent nuls ».
# On le garde donc à l'identique, par interposition plutôt que par redéfinition. Le
# wrapper vit dans harness_stubs.c, pas dans harness_wrap.c, parce que bench exclut ce
# dernier ; et ce drapeau-ci s'applique aux deux cibles, là où les neuf ci-dessus sont
# propres à la .so (bench doit mesurer SANS compteurs).
#
# M16, levier A : cpu_get_speed et cpu_set_turbo SORTENT de la liste. Le budget de
# chaque tranche est celui de PCem, cpu_turbo ? cpu_turbo_speed : cpu_nonturbo_speed,
# que le vrai cpu_set() pose dans h_boot. Restent enveloppés les deux appels des resets
# (cpu_update_waitstates) et cpu_set_edx, DEVIATIONS déclarées des deux côtés.
# M16, étape 6 : cpu_update_waitstates SORT à son tour — models[] est peuplé par h_boot
# et h_reset, et le vrai remet le préfetch au coût de la RAM à chaque reset, comme PCem.
# Reste cpu_set_edx, DEVIATION déclarée des deux côtés.
# G7.1 : rom_init, qui met à zéro la queue d'une ROM plus courte que son allocation (le
# tas que PCem y laisse, PB-24 ; la VGA d'IBM en C6000-C7FFF). Voir harness_stubs.c.
WRAP_BOTH := cpu_set_edx rom_init
WRAPFLAGS_BOTH := $(patsubst %,-Wl$(comma)--wrap=%,$(WRAP_BOTH))
LDFLAGS += $(WRAPFLAGS_BOTH)

# -shared tolere les symboles non resolus : un .so peut se lier avec des trous et
# ne planter qu'au premier appel, des mois plus tard. Mesure : en liant
# src/video/video.c, l'oracle s'est lie proprement avec QUATRE-VINGT-DIX symboles
# manquants -- tout le registre VIDEO_CARD. Seul le lien de selftest (executable)
# l'a revele. On exige donc la resolution complete des le .so.
LDFLAGS += -Wl,--no-undefined

# G8.0 — le C++ : DBOPL (src/dosbox/dbopl.cpp, inclus par harness_dbopl.cpp). Mêmes -I, mêmes
# drapeaux ; le lien de la .so passe par $(CXX), qui tire libstdc++ (les constructeurs statiques
# de sound_dbopl.cc, G8.1). bench, qui ne lit ni l'OPL ni le DSP, reste en C seul.
CXX      := g++
CXXFLAGS := $(CFLAGS)
CXXSRC   := harness_dbopl.cpp

OBJ := $(addprefix $(BUILD)/,$(notdir $(SRC:.c=.o))) $(addprefix $(BUILD)/,$(notdir $(CXXSRC:.cpp=.o)))
vpath %.c . $(PCEM)/src/video $(PCEM)/src/cpu $(PCEM)/src/keyboard $(PCEM)/src/memory $(PCEM)/src $(PCEM)/src/models $(PCEM)/src/plugin-api $(PCEM)/src/flash $(PCEM)/src/floppy $(PCEM)/src/disc $(PCEM)/src/mfm $(PCEM)/src/hdd $(PCEM)/src/ide $(PCEM)/src/devices $(PCEM)/src/mouse $(PCEM)/src/joystick $(PCEM)/src/sound

# --- bench : la référence « C de production » ---------------------------------
# Un EXÉCUTABLE, pas la .so : mêmes sources SAUF harness_wrap.c, SANS -Wl,--wrap
# (les compteurs h_n_* sont définis dans harness_stubs.c, donc le lien passe ;
# simplement plus rien ne les incrémente), et avec les drapeaux du build amont
# Release — -flto et -march compris, que CFLAGS omet. Objets à part dans
# build/lto/ : -flto et -march changent le code, on ne les mélange pas à ceux de
# la .so. -fPIC n'a pas lieu d'être dans un exécutable.
#
# -Wl,--no-undefined est gardé par symétrie avec la .so ; pour un exécutable il
# est redondant (ld refuse déjà tout symbole non résolu), mais inoffensif.
BENCH_FLAGS := -O2 -fomit-frame-pointer -fno-strict-aliasing -flto -march=x86-64-v2 -g \
               -Wall -Wno-unused-function -Wno-unused-variable -Wno-unused-but-set-variable \
               -Wno-misleading-indentation -Wno-implicit-fallthrough
BENCH_SRC   := $(filter-out harness_wrap.c,$(SRC))
BENCH_OBJ   := $(addprefix $(BUILD)/lto/,$(notdir $(BENCH_SRC:.c=.o)))

.PHONY: all clean selftest bench bench-native

all: $(TARGET)

$(TARGET): $(OBJ)
	$(CXX) $(LDFLAGS) -o $@ $^ $(LDLIBS)
	@echo "oracle : $@ construit."

$(BUILD)/%.o: %.c | $(BUILD)
	$(CC) $(CFLAGS) $(INC) -c $< -o $@

$(BUILD)/%.o: %.cpp | $(BUILD)
	$(CXX) $(CXXFLAGS) $(INC) -c $< -o $@

$(BUILD):
	@mkdir -p $(BUILD)

selftest: $(BUILD)/selftest
	@./$(BUILD)/selftest

$(BUILD)/selftest: selftest.c $(TARGET) | $(BUILD)
	$(CC) $(CFLAGS) $(INC) -o $@ selftest.c -L. -l:$(TARGET) -Wl,-rpath,'$$ORIGIN/..' $(LDLIBS)

bench: $(BUILD)/bench
	@echo "bench : $(BUILD)/bench construit. Lancer : taskset -c 0-3 $(BUILD)/bench ../../roms"

# Le lien porte les mêmes drapeaux que la compilation : avec -flto, c'est à
# l'édition de liens que l'optimisation a lieu, et -march y est relu.
$(BUILD)/bench: bench.c $(BENCH_OBJ) | $(BUILD)
	$(CC) $(BENCH_FLAGS) $(INC) -Wl,--no-undefined $(WRAPFLAGS_BOTH) -o $@ bench.c $(BENCH_OBJ) $(LDLIBS)

$(BUILD)/lto/%.o: %.c | $(BUILD)/lto
	$(CC) $(BENCH_FLAGS) $(INC) -c $< -o $@

$(BUILD)/lto:
	@mkdir -p $(BUILD)/lto

# Variante -march=native, optionnelle : recompile tout d'une traite (pas de
# cache d'objets), parce que ses objets ne sont pas échangeables avec ceux de
# build/lto/. Chiffre valable sur CETTE machine seulement.
bench-native: bench.c $(BENCH_SRC) | $(BUILD)
	$(CC) $(subst -march=x86-64-v2,-march=native,$(BENCH_FLAGS)) $(INC) -Wl,--no-undefined \
	      -o $(BUILD)/bench-native bench.c $(BENCH_SRC) $(LDLIBS)
	@echo "bench-native : $(BUILD)/bench-native construit (-march=native)."

clean:
	rm -rf $(BUILD) $(TARGET)
