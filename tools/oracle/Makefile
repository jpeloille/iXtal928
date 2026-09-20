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
SRC := harness.c \
       harness_stubs.c \
       harness_wrap.c \
       $(PCEM)/src/cpu/x86seg.c \
       $(PCEM)/src/cpu/x87.c \
       $(PCEM)/src/cpu/x87_timings.c \
       $(PCEM)/src/cpu/386_common.c \
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
       $(PCEM)/src/video/vid_cga.c

# Interposition sur les fonctions de PCem pour y poser compteurs et journal
# d'écritures SANS modifier l'arbre vendoré. Voir harness_wrap.c.
comma := ,
WRAP := readmembl writemembl readmemwl writememwl inb outb picint picinterrupt timer_process
LDFLAGS += $(patsubst %,-Wl$(comma)--wrap=%,$(WRAP))

# -shared tolere les symboles non resolus : un .so peut se lier avec des trous et
# ne planter qu'au premier appel, des mois plus tard. Mesure : en liant
# src/video/video.c, l'oracle s'est lie proprement avec QUATRE-VINGT-DIX symboles
# manquants -- tout le registre VIDEO_CARD. Seul le lien de selftest (executable)
# l'a revele. On exige donc la resolution complete des le .so.
LDFLAGS += -Wl,--no-undefined

OBJ := $(addprefix $(BUILD)/,$(notdir $(SRC:.c=.o)))
vpath %.c . $(PCEM)/src/video $(PCEM)/src/cpu $(PCEM)/src/keyboard $(PCEM)/src/memory $(PCEM)/src $(PCEM)/src/models $(PCEM)/src/plugin-api $(PCEM)/src/flash

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
	$(CC) $(LDFLAGS) -o $@ $^ $(LDLIBS)
	@echo "oracle : $@ construit."

$(BUILD)/%.o: %.c | $(BUILD)
	$(CC) $(CFLAGS) $(INC) -c $< -o $@

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
	$(CC) $(BENCH_FLAGS) $(INC) -Wl,--no-undefined -o $@ bench.c $(BENCH_OBJ) $(LDLIBS)

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
