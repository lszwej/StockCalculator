# StockCalculator

Prosty kalkulator do obliczania zysków lub strat z transakcji akcjami. Aplikacja webowa **.NET 10** (Blazor Web App, Interactive Server) z **Bootstrap 5.3.3** i logowaniem **Serilog**.

## Funkcje

- Dwa formularze: **Kupno** i **Sprzedaż**, każdy z polami:
  - **Ilość akcji**: liczba rzeczywista większa od 0.
  - **Cena za akcję**: liczba rzeczywista większa od 0.
  - **Prowizja %**: liczba nieujemna, maksymalnie 100.
- Ilość akcji przy sprzedaży nie może przekraczać ilości przy kupnie. Aplikacja sprawdza to na bieżąco.
- Wynik pokazuje wartość brutto, podatek Belki (19%) i wartość netto, z kolorem zależnym od rodzaju wyniku:
  - zysk na zielono;
  - strata na czerwono;
  - wynik 0 na żółto.
- Zmiana dowolnego pola czyści poprzedni wynik.
- Przełącznik motywu: jasny lub ciemny. Wybór jest zapamiętywany, a domyślnie obowiązuje motyw systemowy.
- Liczby są formatowane w kulturze `pl-PL`.

## Sposób obliczeń

Dla każdego formularza:

```
stocks     = ilość × cena
commission = stocks × prowizja / 100
total      = stocks + commission
```

Wynik:

```
brutto = Sprzedaż.total − Kupno.total      (zaokrąglenie do 2 miejsc, AwayFromZero)
podatek = 19% × brutto, tylko gdy brutto > 0 (zaokrąglenie do groszy)
netto  = brutto − podatek
```

> Podatek jest liczony osobno dla każdej transakcji. Obliczenia nie uwzględniają rocznego rozliczenia PIT-38, czyli zaokrąglania do pełnych złotych ani kompensaty strat.

## Wymagania

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- System Windows, jeśli aplikacja ma działać jako usługa.

## Uruchomienie

```bash
dotnet run --project src/StockCalculator.Web
```

## Testy

```bash
dotnet test
```

Testy korzystają z xUnit i FluentAssertions i obejmują logikę domenową: obliczenia, walidację i podatek Belki.

## Usługa Windows

Skrypty w folderze `install` uruchamia się z wiersza poleceń otwartego jako administrator.

```bash
install\install-service.cmd [katalog_instalacji]
```

Skrypt instalacyjny wykonuje kolejno:
1. Publikuje aplikację w konfiguracji Release do `%ProgramFiles%\StockCalculator` albo do podanego katalogu.
2. Tworzy certyfikat self-signed `CN=StockCalculator` w magazynie `LocalMachine\My` albo używa istniejącego i dodaje go do zaufanych w `LocalMachine\Root`.
3. Rejestruje usługę **StockCalculator** z opóźnionym startem automatycznym i restartem po awarii.

Po instalacji aplikacja jest dostępna pod adresem **https://localhost:5001**.

```bash
install\uninstall-service.cmd
```

Skrypt deinstalacyjny usuwa tylko usługę. Pliki aplikacji, logi i certyfikat zostają na dysku.

## Logi

Logi są zapisywane w folderze `Logs` w katalogu programu. Pliki rotują codziennie, przechowywanych jest 31 plików, a pojedynczy plik ma maksymalnie 10 MB.

| Folder | Zawartość |
|---|---|
| `Logs\Information` | Tylko informacja o wykonaniu obliczenia, bez danych wejściowych ani wyników. |
| `Logs\Warning` | Odrzucenia walidacji formularzy i ostrzeżenia frameworka. |
| `Logs\Error` | Błędy obliczeń, nieobsłużone wyjątki i błędy startu. |

Pozostałe logi trafiają tylko na konsolę. Konfiguracja znajduje się w sekcji `Serilog` w pliku `appsettings.json`.

## Struktura

```
src/StockCalculator.Core         domena: modele, walidacja, kalkulator, podatek Belki
src/StockCalculator.Web          aplikacja Blazor: strona, formularze, podsumowanie wyniku
tests/StockCalculator.Core.Tests testy jednostkowe
install/                         skrypty instalacji i deinstalacji usługi Windows
```
