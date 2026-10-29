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
- Podatek Belki (`CapitalGainsTax`): 19% wyniku brutto, tylko gdy brutto > 0 (strata/0 → podatek 0), zaokrąglony do groszy. Wynik netto = brutto − podatek.
  - Uproszczenie: liczony per transakcja; nie uwzględnia rozliczenia rocznego PIT-38 (zaokrąglanie do pełnych zł, kompensata strat).
- Prezentacja pod formularzami — nagłówek z wartością netto + pola: wynik brutto, podatek Belki, wynik netto:
  - zysk → zielony (`alert-success`): „Zysk {|netto|}”
  - strata → czerwony (`alert-danger`): „Strata {|netto|}”
  - 0 → żółty (`alert-warning`): sama wartość
- Zmiana dowolnego pola czyści poprzedni wynik (brak nieaktualnych danych).

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
- `src/StockCalculator.Core` — domena: modele (`TradeInput`, `TradeCalculation`, `ProfitLossResult`), walidacja (`TradeInputValidator`, `TradeLimits`), `StockTradeCalculator` (logowanie przez `ILogger`), `CapitalGainsTax` (podatek Belki).
- `src/StockCalculator.Web` — Blazor: `Pages/Home.razor` (strona), `Calculator/TradeForm.razor`, `Calculator/ProfitLossSummary.razor`, `Forms/TradeFormModel` (+ `TradeFieldRuleAttribute` delegujący do walidatora Core — jedno źródło reguł).
- `tests/StockCalculator.Core.Tests` — xUnit + FluentAssertions.

## Konwencje (z instrukcji projektu)
Kod nowoczesny, produkcyjny, modułowy; samodokumentujące się nazwy zamiast komentarzy; obsługa błędów i edge case'ów; podejście deklaratywne; przy zmianach pokazywać tylko zmienione fragmenty.
