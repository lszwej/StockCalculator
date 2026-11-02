# StockCalculator — wymagania i decyzje

## Cel
Aplikacja webowa .NET 10 (Blazor Web App, Interactive Server) — kalkulator zysku/straty z transakcji akcjami.

## Wymagania funkcjonalne
- Dwa formularze: **Kupno** i **Sprzedaż**, każdy z polami:
  - Ilość akcji — liczba rzeczywista dodatnia (> 0) (`decimal`, limit techniczny 1e12)
  - Cena za akcję — liczba rzeczywista dodatnia (> 0) (`decimal`, limit techniczny 1e12)
  - Prowizja % — liczba rzeczywista nieujemna, maksimum 100
- Przeliczenie dla każdego formularza:
  - `stocks = ilość * cena`
  - `commission = stocks * prowizja / 100`
  - `total = stocks + commission` (dla obu formularzy, zgodnie ze specyfikacją)
- Ilość akcji przy Sprzedaży ≤ ilość przy Kupnie (`TradeInputValidator.ValidateSellQuantity`; w UI komunikat pod polem „Ilość akcji” Sprzedaży, sprawdzany na bieżąco po zmianie którejkolwiek ilości).
- Wynik brutto = `Sprzedaż.total - Kupno.total`, zaokrąglony do 2 miejsc (`MidpointRounding.AwayFromZero`).
- Podatek Belki (`CapitalGainsTax`): 19% wyniku brutto, tylko gdy brutto > 0 (strata/0 → podatek 0), zaokrąglony do 2 miejsc. Wynik netto = brutto − podatek. Kwoty są w wybranej walucie.
  - Uproszczenie: liczony per transakcja; nie uwzględnia rozliczenia rocznego PIT-38 (zaokrąglanie do pełnych zł, kompensata strat).
- Kwoty w wynikach (podsumowanie formularzy i wynik końcowy) są pokazywane z oznaczeniem wybranej waluty: „zł” dla PLN, a dla pozostałych jej kod, np. „EUR” (`NumberFormatter.Money`, formatowanie `pl-PL`).
- Prezentacja pod formularzami — nagłówek z wartością netto + pola: wynik brutto, podatek Belki, wynik netto:
  - zysk → zielony (`alert-success`): „Zysk {|netto|}”
  - strata → czerwony (`alert-danger`): „Strata {|netto|}”
  - 0 → żółty (`alert-warning`): sama wartość
- Zmiana dowolnego pola czyści poprzedni wynik (brak nieaktualnych danych).
- Kursy walut: średnie kursy **USD** i **EUR** z bieżącego dnia wg NBP, pobierane podczas uruchamiania aplikacji i raz dziennie o 12:30 przez background worker, trzymane w pamięci w `Dictionary<string, decimal>` (klucz: symbol waluty, wartość: kurs). Pokazywane na stronie głównej nad formularzami (`ExchangeRatesPanel`).
- Pole wyboru **Waluta** w obu formularzach, w jednej linii z polem „Cena za akcję”: PLN, EUR, USD, domyślnie PLN. Pole jest zawsze aktywne, także gdy brakuje kursu lub jego pobranie się nie powiodło, bo obliczenia nie korzystają z kursów. Spoza tej listy waluta jest odrzucana (`TradeInputValidator.ValidateCurrency`).
- Waluta przy Kupnie i Sprzedaży musi być identyczna (`TradeInputValidator.ValidateSellCurrency`; w UI komunikat pod polem „Waluta” Sprzedaży, sprawdzany na bieżąco po zmianie którejkolwiek waluty; `StockTradeCalculator` odrzuca różne waluty także niezależnie od UI). Przy różnych walutach „Przelicz” nie liczy wyniku, a odrzucenie trafia do `Logs\Warning`.
- Obliczenia w wybranej walucie: wartość akcji, prowizja, wynik brutto, podatek i wynik netto są liczone i pokazywane w walucie z formularza, bez przeliczania na PLN (`TradeInput.Currency`, `TradeCalculation.Currency`, `ProfitLossResult.Currency`). Kursy NBP mają charakter wyłącznie informacyjny: są pokazywane w panelu, a nie wpływają na obliczenia ani na pole Waluta. Zmiana waluty czyści wynik jak zmiana każdego pola.
  - Uproszczenie: podatek Belki to 19% wyniku w wybranej walucie. Do PIT-38 przychód i koszty przelicza się na PLN po kursie średnim NBP z ostatniego dnia roboczego przed dniem każdej transakcji, więc dla walut obcych wynik i podatek są tylko szacunkiem.

## Pobieranie kursów walut (API NBP)
- Dokumentacja: https://api.nbp.pl/. API publiczne, bez klucza.
- Endpoint (tabela A, kursy średnie): `GET https://api.nbp.pl/api/exchangerates/rates/a/{usd|eur}/today/?format=json`.
- Odpowiedź 200: `{"table":"A","currency":"dolar amerykański","code":"USD","rates":[{"no":"213/A/NBP/2026","effectiveDate":"2026-11-02","mid":3.9389}]}`. Kurs średni to `rates[0].mid` (PLN za 1 jednostkę waluty, 4 miejsca po przecinku).
- 404 (`Brak danych`, zwykły tekst) to stan oczekiwany: tabela A nie jest publikowana w weekendy i święta, a w dni robocze pojawia się dopiero około południa. Wtedy provider zwraca `null`, a panel pokazuje „brak kursu na dziś”. Świadomie bez przechodzenia na ostatni opublikowany kurs (`/a/{kod}/`), bo wymaganie dotyczy bieżącego dnia.
- Implementacja: `IExchangeRateProvider` / `NbpExchangeRateProvider` w `Core/ExchangeRates`, typowany `HttpClient` rejestrowany w `Program.cs`, timeout 10 s.
- Inne błędy (status inny niż 404, sieć, timeout, nieprawidłowa treść) są logowane jako Error (`Logs\Error`) i zgłaszane jako `ExchangeRateUnavailableException`. Anulowanie przez wywołującego przechodzi dalej jako `OperationCanceledException`.
- Harmonogram: `ExchangeRateRefreshService` (`BackgroundService` w `Web/ExchangeRates`) wywołuje `ExchangeRateStore.LoadTodayRatesAsync` przed uruchomieniem Kestrela, a potem raz dziennie o `ExchangeRates:DailyRefreshTime` z `appsettings.json` (domyślnie `12:30:00`, czas lokalny serwera). Pobieranie przy starcie zostaje, żeby aplikacja uruchomiona po południu miała kursy od razu. Start po 12:30 zastępuje pobranie z tego dnia.
- Worker sprawdza harmonogram co 30 s (`DailyRefreshSchedule`), więc pobranie następuje do 30 s po ustawionej godzinie, a po uśpieniu komputera nadrabia je po wybudzeniu. Nieudane lub puste pobranie nie jest ponawiane tego samego dnia.
- Godzina 12:30 daje zapas na publikację tabeli około południa. Jeśli NBP opublikuje tabelę później, pobranie dostaje 404 i panel pokazuje „brak kursu na dziś” do następnego dnia albo restartu. Godzinę można zmienić w konfiguracji.
- Kursy obowiązują tylko w dniu pobrania: worker usuwa je po północy (`RemoveExpiredRates`), bez zapytań do API. Od północy do najbliższego udanego pobrania panel pokazuje „brak kursu na dziś”.
- Błąd pobierania nie usuwa kursu z bieżącego dnia. Kurs z dnia poprzedniego i kurs z odpowiedzią 404 nie są zachowywane.
- `ExchangeRateStore` (singleton) przechowuje słownik i udostępnia `RatesBySymbol` oraz zdarzenie `RatesChanged`. `Home.razor` subskrybuje zdarzenie i przekazuje dane do `ExchangeRatesPanel`; otwieranie strony nie wysyła zapytań do API.
- `RatesChanged` jest zgłaszane tylko przy faktycznej zmianie kursów. Wtedy na otwartych stronach odświeża się tylko panel. Wybrane waluty i obliczony wynik zostają bez zmian.
- Klient HTTP jest pobierany z nowego scope przy każdym pobraniu, żeby `IHttpClientFactory` mógł rotować połączenia. Brak kursu (404) jest logowany tylko na poziomie Debug.

## Wymagania niefunkcjonalne
- **Serilog** (konfiguracja w `appsettings.json` → `Serilog:WriteTo`), osobne foldery, rolling dzienny, 31 plików, 10 MB.
  - Ścieżki względne (`Logs\...`) rozwiązywane przy starcie względem katalogu programu (`AppContext.BaseDirectory`) przez `Logging/SerilogConfigurationExtensions.ResolveRelativeLogPathsAgainst`; ścieżki absolutne zostają bez zmian.
  - `Logs\Information\stockcalculator-YYYYMMDD.log` — wyłącznie informacja „Profit/loss calculation performed” (bez danych wejściowych/wyników); filtr `Serilog.Expressions` po poziomie Information i `SourceContext` = `StockTradeCalculator`.
  - `Logs\Error\stockcalculator-YYYYMMDD.log` — Error i Fatal: błędy obliczeń, wyjątki nieobsłużone/obwodu Blazor, crash startu.
  - `Logs\Warning\stockcalculator-YYYYMMDD.log` — wyłącznie Warning: odrzucenia walidacji formularzy, ostrzeżenia frameworka.
  - Pozostałe (requesty HTTP i inne Information) tylko na konsolę.
- **Bootstrap 5.3.3** (CDN jsDelivr + SRI) z przełącznikiem motywu jasny/ciemny (`data-bs-theme`, `wwwroot/js/theme.js`, zapamiętanie w `localStorage`, domyślnie wg `prefers-color-scheme`).
- Kultura `pl-PL` do formatowania liczb.
- **Usługa Windows** `StockCalculator` (`UseWindowsService`, content root = katalog programu): `install\install-service.cmd [katalog]` (jako administrator) — publikuje Release do `%ProgramFiles%\StockCalculator` (lub podanego katalogu), tworzy/używa certyfikatu self-signed `CN=StockCalculator` w `LocalMachine\My` (+ zaufanie w `LocalMachine\Root`), rejestruje usługę (start opóźniony automatyczny, restart po awarii) z Kestrel na `https://localhost:5001` przez argumenty `--Kestrel:Endpoints:Https:*`. Opis: „Prosty kalkulator do obliczania zysków lub strat z akcji.”. `install\uninstall-service.cmd` usuwa usługę (pliki, logi, certyfikat zostają).

## Struktura
- `src/StockCalculator.Core` — domena: modele (`TradeInput`, `TradeCalculation`, `ProfitLossResult`), walidacja (`TradeInputValidator`, `TradeLimits`), `StockTradeCalculator` (logowanie przez `ILogger`), `CapitalGainsTax` (podatek Belki), `ExchangeRates/` (kursy walut NBP, `ExchangeRateStore`, `DailyRefreshSchedule`).
- `src/StockCalculator.Web` — Blazor: `Pages/Home.razor` (strona), `Calculator/TradeForm.razor`, `Calculator/ProfitLossSummary.razor`, `Calculator/ExchangeRatesPanel.razor`, `ExchangeRates/ExchangeRateRefreshService`, `Forms/TradeFormModel` (+ `TradeFieldRuleAttribute` delegujący do walidatora Core — jedno źródło reguł).
- `tests/StockCalculator.Core.Tests` — xUnit + FluentAssertions.

## Konwencje (z instrukcji projektu)
Kod nowoczesny, produkcyjny, modułowy; samodokumentujące się nazwy zamiast komentarzy; obsługa błędów i edge case'ów; podejście deklaratywne; przy zmianach pokazywać tylko zmienione fragmenty.
