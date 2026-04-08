# 🦜 Squawk: Multiplayer Parrot Battle

Wysokowydajny serwer gier wieloosobowych z estetycznym panelem sterowania dla gry **Squawk**. Projekt oferuje solidny backend dla bitew papug, synchronizację w czasie rzeczywistym, zaawansowane AI botów oraz nowoczesny interfejs zarządzania zbudowany w **Avalonia UI**.

## 🚀 Kluczowe Funkcje

- **Silnik Gry High-Performance**: Obsługa ruchu papug, kolizji i mechaniki energii w czasie rzeczywistym na mapie o promieniu 1500 jednostek.
- **Komunikacja WebSocket**: Niskolatencyjna interakcja klient-serwer napędzana przez `WatsonWebsocket`.
- **Zintegrowany Web Server**: Serwuje klienta gry bezpośrednio z zasobów pliku wykonywalnego.
- **Panel Sterowania**: Nowoczesny interfejs w klimacie dżungli (Avalonia UI) umożliwiający:
  - Zarządzanie silnikiem gry (Start/Stop).
  - Monitoring usług sieciowych (WebSocket & HTTP).
  - Kontrolę botów (włączanie/wyłączanie, agresywność).
- **System Botów**: Zawsze aktywna czwórka botów (Bot1-Bot4) z unikalnym systemem punktacji i inteligentnym zbieraniem jedzenia.
- **Dynamiczne Jedzenie**: Stała liczba 400 punktów jedzenia na mapie z inteligentnym systemem respawnu (3 sekundy opóźnienia).
- **Single-File Executable**: W pełni samowystarczalny build `.exe` (wszystkie DLL i zasoby klienta w jednym pliku).

## 🛠 Stos Technologiczny

- **Backend**: .NET 10.0 (C#)
- **GUI**: Avalonia UI (Modern XAML)
- **Networking**: WatsonWebsocket, TcpListener
- **Baza Danych**: SQLite (Ranking TOP 10 i statystyki 24h)
- **Frontend**: HTML5 Canvas (Pure JS) - zoptymalizowany pod kątem wydajności.

## 📁 Struktura Projektu

- `server/`: Rdzeń logiki, networking i mechanika gry.
- `server/SquawkTests/`: Testy jednostkowe (xUnit) weryfikujące mechanikę silnika.
- `client/`: Zasoby frontendowe i logika renderowania Canvas (osadzone w serwerze).
- `build.ps1`: Zaawansowany skrypt automatyzujący proces kompilacji do jednego pliku .exe.

## 🔨 Budowanie i Uruchamianie

### Wymagania
- .NET 10 SDK

### Szybka Kompilacja (Skrypt PowerShell)
Aby wygenerować gotowy plik `.exe` w głównym katalogu:
```powershell
.\build.ps1
```

### Uruchamianie Testów
```powershell
dotnet test server/SquawkTests/SquawkTests.csproj
```

## 📜 Detale Techniczne

### Sieć
- **Port WebSocket**: `5005` (Aktualizacje stanu gry)
- **Port HTTP**: `5006` (Serwowanie klienta gry)

### Mechanika Świata
Serwer zarządza pełnym stanem świata, w tym:
- **Papugi**: Zarządzanie pozycją, kątem obrotu i segmentami ciała (dynamiczne skalowanie rozmiaru).
- **Bezpieczny Spawn**: Algorytm wyszukujący wolną przestrzeń na mapie, zapobiegający kolizjom na starcie.
- **Ranking**: System TOP 10 aktualizowany co 500ms, przesyłający nazwę, punkty oraz aktualną długość węża.

---
Developed with ❤️ by **Hnato.**
