# StockCalculator

Prosty kalkulator do obliczania zysków lub strat z transakcji akcjami. Aplikacja webowa **.NET 10** (Blazor Web App, Interactive Server) z **Bootstrap 5.3.3** i logowaniem **Serilog**.

## Funkcje

- Dwa formularze: **Kupno** i **Sprzedaż**, każdy z polami:
  - **Ilość akcji**: liczba rzeczywista większa od 0.
  - **Cena za akcję**: liczba rzeczywista większa od 0.
  - **Waluta**: PLN, EUR lub USD, domyślnie PLN.
  - **Prowizja %**: liczba nieujemna, maksymalnie 100.
- Ilość akcji przy sprzedaży nie może przekraczać ilości przy kupnie. Aplikacja sprawdza to na bieżąco.
- Waluta przy kupnie i sprzedaży musi być taka sama. Aplikacja sprawdza to na bieżąco i przy różnych walutach pokazuje błąd zamiast wyniku.
- Obliczenia są wykonywane w wybranej walucie, bez przeliczania na PLN, i w tej walucie są podawane wszystkie kwoty w wynikach.
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
podatek = 19% × brutto, tylko gdy brutto > 0 (zaokrąglenie do 2 miejsc)
netto  = brutto − podatek
```

> Podatek jest liczony osobno dla każdej transakcji. Obliczenia nie uwzględniają rocznego rozliczenia PIT-38, czyli zaokrąglania do pełnych złotych ani kompensaty strat.

> Dla walut obcych wynik i podatek są tylko szacunkiem w tej walucie. Do PIT-38 przychód i koszty przelicza się na PLN po kursie średnim NBP z ostatniego dnia roboczego przed dniem każdej transakcji, więc podatek należny może się znacząco różnić, a przy zmianie kursu zysk w walucie może nawet oznaczać stratę w PLN.

## Kursy walut

Aplikacja pobiera średnie kursy EUR i USD z dnia bieżącego z [API NBP](https://api.nbp.pl/) (tabela A) podczas uruchamiania, a potem w tle raz dziennie o 12:30, i trzyma je w pamięci. Godzinę można zmienić w `appsettings.json` (`ExchangeRates:DailyRefreshTime`), a liczy się czas lokalny serwera. Kursy są widoczne na stronie głównej nad formularzami i mają charakter wyłącznie informacyjny: nie wpływają na obliczenia ani na pole **Waluta**.

Jeśli brakuje kursu, panel pokazuje „brak kursu na dziś”, a kalkulator działa dalej we wszystkich walutach. Dzieje się tak, gdy:
- nie ma kursu na dany dzień: NBP nie publikuje kursów w weekendy i święta, a w dni robocze pojawiają się one około południa;
- pobieranie się nie powiodło, a kursu z tego dnia nie ma jeszcze w pamięci, np. przez brak połączenia. Błąd trafia wtedy do `Logs\Error`.

Kursy obowiązują tylko w dniu pobrania. Po północy są usuwane, więc w dni robocze panel nie pokazuje kursów do pobrania o 12:30. Przy błędzie pobierania kurs z bieżącego dnia zostaje w pamięci.

Nieudane pobranie nie jest ponawiane tego samego dnia. Jeśli o 12:30 NBP nie opublikuje jeszcze tabeli, panel nie pokazuje kursów do następnego dnia albo do restartu aplikacji lub usługi. Można wtedy ustawić późniejszą godzinę pobierania.

Otwarte strony reagują na zmianę kursów bez przeładowania i pokazują nowe wartości. Wybrane waluty i obliczony wynik zostają bez zmian.

## Wymagania

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Dostęp do `api.nbp.pl`, jeśli na stronie mają być widoczne kursy. Obliczenia działają bez niego.
- System Windows, jeśli aplikacja ma działać jako usługa.

## Uruchomienie

```bash
dotnet run --project src/StockCalculator.Web
```

## Testy

```bash
dotnet test
```

Testy korzystają z xUnit i FluentAssertions i obejmują logikę domenową: obliczenia, walidację, podatek Belki oraz pobieranie i przechowywanie kursów walut.

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
| `Logs\Error` | Błędy obliczeń, błędy pobierania kursów NBP, nieobsłużone wyjątki i błędy startu. |

Pozostałe logi trafiają tylko na konsolę. Konfiguracja znajduje się w sekcji `Serilog` w pliku `appsettings.json`.

## Struktura

```
src/StockCalculator.Core         domena: modele, walidacja, kalkulator, podatek Belki, kursy walut NBP
src/StockCalculator.Web          aplikacja Blazor: strona, formularze, podsumowanie wyniku, panel kursów
tests/StockCalculator.Core.Tests testy jednostkowe
install/                         skrypty instalacji i deinstalacji usługi Windows
```
