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
       $(PCEM)/src/flash/rom.c

# Interposition sur les fonctions de PCem pour y poser compteurs et journal
# d'écritures SANS modifier l'arbre vendoré. Voir harness_wrap.c.
comma := ,
WRAP := readmembl writemembl readmemwl writememwl inb outb picint picinterrupt timer_process
LDFLAGS += $(patsubst %,-Wl$(comma)--wrap=%,$(WRAP))

OBJ := $(addprefix $(BUILD)/,$(notdir $(SRC:.c=.o)))
vpath %.c . $(PCEM)/src/cpu $(PCEM)/src/memory $(PCEM)/src $(PCEM)/src/models $(PCEM)/src/plugin-api $(PCEM)/src/flash

.PHONY: all clean selftest

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

clean:
	rm -rf $(BUILD) $(TARGET)
