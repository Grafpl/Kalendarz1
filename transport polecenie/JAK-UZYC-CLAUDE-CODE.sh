#!/bin/bash
# ═══════════════════════════════════════════════════════════════
# JAK UŻYĆ TEGO PROMPTU W CLAUDE CODE
# ═══════════════════════════════════════════════════════════════
#
# KROK 1: Skopiuj plik PROMPT-CLAUDE-CODE-transport-editor.md 
#          do katalogu głównego projektu ZPSP
#
# KROK 2: Otwórz terminal w katalogu projektu ZPSP
#
# KROK 3: Uruchom Claude Code z jedną z poniższych komend:
#
# ═══════════════════════════════════════════════════════════════

# === OPCJA A: Podaj prompt jako plik (REKOMENDOWANE) ===
# Claude Code przeczyta cały plik jako kontekst

claude "Przeczytaj plik PROMPT-CLAUDE-CODE-transport-editor.md i wykonaj wszystko co jest w nim opisane. Przebuduj okno KursEditorForm zgodnie z opisem Wariantu A. Stwórz wszystkie pliki wymienione w sekcji 'STRUKTURA PLIKÓW DO STWORZENIA'. Zacznij od Theme/ZpspColors.cs i Theme/ZpspFonts.cs, potem Models/TransportModels.cs, potem Controls/ (wszystkie 5), potem Services/ConflictDetectionService.cs, a na końcu główną formę KursEditorForm.cs. Użyj dokładnie tych kolorów, fontów i rozmiarów co w pliku. Nie pomijaj żadnego szczegółu."

# === OPCJA B: Jeśli wolisz interaktywnie ===
# Otwórz Claude Code w trybie czatu i wklej:

claude

# Potem w interaktywnym trybie wklej:
# @PROMPT-CLAUDE-CODE-transport-editor.md Wykonaj wszystko z tego pliku. Zacznij od ZpspColors.cs.

# === OPCJA C: Jeśli chcesz krok po kroku ===
# Podziel na osobne komendy:

# Krok 1: Kolory i fonty
claude "Przeczytaj PROMPT-CLAUDE-CODE-transport-editor.md. Stwórz TYLKO pliki Theme/ZpspColors.cs i Theme/ZpspFonts.cs z dokładnie tymi kolorami i fontami co w sekcji 'PALETA KOLORÓW'."

# Krok 2: Modele danych
claude "Przeczytaj PROMPT-CLAUDE-CODE-transport-editor.md. Stwórz TYLKO plik Models/TransportModels.cs ze wszystkimi klasami: Order, CourseStop, TransportCourse, Driver, Vehicle, CourseConflict + enumy."

# Krok 3: Custom kontrolki
claude "Przeczytaj PROMPT-CLAUDE-CODE-transport-editor.md. Stwórz WSZYSTKIE pliki w Controls/: CapacityBarControl.cs (pasek ładowności z hatching), RoutePillsControl.cs (pills trasy), ConflictPanelControl.cs (kompaktowy panel alertów), TimelineControl.cs (Gantt oś czasu), AxleWeightControl.cs (waga na osiach). Użyj ZpspColors i ZpspFonts."

# Krok 4: Serwis konfliktów
claude "Przeczytaj PROMPT-CLAUDE-CODE-transport-editor.md. Stwórz Services/ConflictDetectionService.cs z WSZYSTKIMI 14 typami konfliktów opisanymi w sekcji 'KONFLIKTY'."

# Krok 5: Główna forma
claude "Przeczytaj PROMPT-CLAUDE-CODE-transport-editor.md. Stwórz KursEditorForm.cs — główną formę z DOKŁADNYM layoutem Wariantu A: TableLayoutPanel 52/48, ciemny lewy panel z headerem+timeline+konflikty+ładunki+4 zakładki, jasny prawy panel z zamówieniami. Implementuj WSZYSTKIE interakcje: double-click, Delete, ▲▼, Ctrl+S, multi-select, drag."

echo ""
echo "═══════════════════════════════════════════════════════════"
echo " GOTOWE! Sprawdź czy projekt się kompiluje."
echo " Jeśli są błędy, powiedz Claude Code: 'Napraw błędy kompilacji'"
echo "═══════════════════════════════════════════════════════════"
