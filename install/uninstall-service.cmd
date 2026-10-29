@echo off
setlocal EnableExtensions
chcp 65001 >nul

set "SERVICE_NAME=StockCalculator"

net session >nul 2>&1 || (
    echo [BŁĄD] Skrypt wymaga uprawnień administratora. Uruchom go z wiersza poleceń "Uruchom jako administrator".
    exit /b 1
)

sc query "%SERVICE_NAME%" >nul 2>&1 || (
    echo Usługa %SERVICE_NAME% nie jest zainstalowana.
    exit /b 0
)

echo Zatrzymywanie usługi %SERVICE_NAME%...
sc stop "%SERVICE_NAME%" >nul 2>&1
for /l %%i in (1,1,30) do (
    sc query "%SERVICE_NAME%" | find "STOPPED" >nul && goto :Delete
    timeout /t 1 /nobreak >nul
)
echo [BŁĄD] Usługa %SERVICE_NAME% nie zatrzymała się w ciągu 30 sekund.
exit /b 1

:Delete
sc delete "%SERVICE_NAME%" || exit /b 1
echo Usługa %SERVICE_NAME% została usunięta. Pliki aplikacji, logi i certyfikat CN=StockCalculator pozostały bez zmian.
exit /b 0
