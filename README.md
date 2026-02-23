SquawkServer
============

Opis
----
SquawkServer to jednoplikowy serwer gry wieloosobowej z papugami, napisany w C# (.NET), który:
- uruchamia silnik gry,
- udostępnia serwer WebSocket na porcie 5004 (0.0.0.0:5004 oraz localhost:5004),
- działa jako panel sterujący z prostą konsolą,
- pozwala włączać i wyłączać pętlę gry oraz boty.

Serwer nie hostuje już statycznej strony WWW. Klient (front‑end) należy podpiąć osobno i połączyć z serwerem przez WebSocket `ws://HOST:5004`.

Technologie
-----------
- .NET 10.0-windows (self-contained, single file)
- Fleck (WebSocket server)
- Newtonsoft.Json
- Windows Forms (panel sterujący)

Struktura projektu
------------------
- `Server/`
  - `Program.cs` – główna aplikacja SquawkServer, panel sterujący, serwer WebSocket.
  - `GameEngine.cs` – silnik gry (logika, fizyka, AI botów).
  - `Models/` – modele danych i wiadomości wymienianych z klientem.
  - `Gui/MainForm.cs` – okno panelu sterującego z logiem i przyciskami.
- `Client/`
  - `index.html`, `game.js`, zasoby – front‑end gry (do hostowania osobno, np. na GitHub Pages lub dowolnym serwerze HTTP).
- `server/`
  - katalog docelowy publikacji – tutaj ląduje jednoplikowy `Squawk.exe` gotowy do uruchomienia.

Porty
-----
- WebSocket: `5004`
  - `ws://0.0.0.0:5004`
  - `ws://localhost:5004`

W projekcie nie ma już żadnej logiki w oparciu o port `5005`.

Budowanie
---------
Wymagania:
- zainstalowane .NET SDK z obsługą `net10.0-windows`.

Kroki:

1. Wejdź do katalogu głównego projektu:

   cd C:\Users\user\Music\Squawk

2. Zbuduj serwer w trybie Release i opublikuj jednoplikowe EXE:

   dotnet publish .\Server\Server.csproj -c Release

   Gotowy plik wykonywalny znajdziesz w:

   C:\Users\user\Music\Squawk\server\Squawk.exe

Właściwości pliku EXE
---------------------
- Ikona: `Client\logo.ico`
- Nazwa produktu: `SquawkServer`
- Wydawca (Company/Authors): `Hnato.`
- Typ: `WinExe` (aplikacja okienkowa z panelem)
- Runtime: self-contained (`win-x64`), nie wymaga instalacji .NET na maszynie docelowej.

Uruchamianie
------------
1. Skopiuj folder `server` na maszynę docelową (wystarczy cały katalog z `Squawk.exe`).
2. Uruchom:

   cd C:\ścieżka\do\server
   Squawk.exe

3. Pojawi się okno panelu sterującego z prostą konsolą logów oraz przyciskami:
   - **Start serwera** – startuje serwer WebSocket na porcie 5004.
   - **Stop serwera** – zatrzymuje serwer WebSocket i zrywa połączenia.
   - **Start gry** – uruchamia pętlę gry (silnik fizyki, aktualizacja stanu).
   - **Stop gry** – zatrzymuje pętlę gry (stan jest zamrażany, brak aktualizacji).
   - **Boty ON** – włącza boty (AI), dodaje je na mapę.
   - **Boty OFF** – usuwa boty z mapy i wyłącza ich AI.

Logi
----
- `Squawk.log` – log informacyjny i diagnostyczny (start/stop serwera, start/stop gry, podłączenia klientów, błędy).
- `Squawk_error.log` – krytyczne błędy startowe (jeśli wystąpią).

Logi są zapisywane w katalogu z `Squawk.exe`. Panel wyświetla również logi na żywo w polu tekstowym.

Łączenie klienta (front‑end)
---------------------------
Front‑end gry (np. `Client/index.html` + `Client/game.js`) musi łączyć się z serwerem przez WebSocket:

- Adres WebSocket:

  ws://HOST:5004

Przykład (w JavaScript):

  const socket = new WebSocket("ws://localhost:5004");

Stronę możesz hostować:
- lokalnie w dowolnym serwerze HTTP,
- na GitHub Pages,
- na innym hostingu statycznych plików.

Tryby pracy
-----------
- Panel + serwer WebSocket + silnik gry w jednym EXE.
- Możliwość pracy bez interaktywnej konsoli systemowej (SafeConsole w `Program.cs`).

Licencja
--------
Projekt może zostać otagowany dowolną licencją zgodnie z preferencjami wydawcy `Hnato.`. Aktualna licencja nie jest zdefiniowana w repozytorium.

