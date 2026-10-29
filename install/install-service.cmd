@echo off
setlocal EnableExtensions
chcp 65001 >nul

rem Użycie: install-service.cmd [katalog_instalacji]
rem Domyślny katalog instalacji: %ProgramFiles%\StockCalculator

set "SERVICE_NAME=StockCalculator"
set "SERVICE_DISPLAY_NAME=StockCalculator"
set "SERVICE_DESCRIPTION=Prosty kalkulator do obliczania zysków lub strat z akcji."
set "HTTPS_PORT=5001"
set "CERTIFICATE_SUBJECT=StockCalculator"
set "PROJECT_PATH=%~dp0..\src\StockCalculator.Web\StockCalculator.Web.csproj"
set "INSTALL_DIR=%ProgramFiles%\StockCalculator"
if not "%~1"=="" set "INSTALL_DIR=%~f1"
set "EXECUTABLE_PATH=%INSTALL_DIR%\StockCalculator.Web.exe"

set "KESTREL_ARGS=--Kestrel:Endpoints:Https:Url=https://localhost:%HTTPS_PORT%"
set "KESTREL_ARGS=%KESTREL_ARGS% --Kestrel:Endpoints:Https:Certificate:Subject=%CERTIFICATE_SUBJECT%"
set "KESTREL_ARGS=%KESTREL_ARGS% --Kestrel:Endpoints:Https:Certificate:Store=My"
set "KESTREL_ARGS=%KESTREL_ARGS% --Kestrel:Endpoints:Https:Certificate:Location=LocalMachine"
set "KESTREL_ARGS=%KESTREL_ARGS% --Kestrel:Endpoints:Https:Certificate:AllowInvalid=true"

net session >nul 2>&1 || (
    echo [BŁĄD] Skrypt wymaga uprawnień administratora. Uruchom go z wiersza poleceń "Uruchom jako administrator".
    exit /b 1
)

where dotnet >nul 2>&1 || (
    echo [BŁĄD] Nie znaleziono polecenia dotnet. Zainstaluj .NET 10 SDK.
    exit /b 1
)

if not exist "%PROJECT_PATH%" (
    echo [BŁĄD] Nie znaleziono projektu: "%PROJECT_PATH%"
    exit /b 1
)

echo [1/5] Usuwanie poprzedniej instalacji usługi %SERVICE_NAME%, jeśli istnieje...
call :RemoveExistingService || goto :Fail

echo [2/5] Publikowanie aplikacji do "%INSTALL_DIR%"...
dotnet publish "%PROJECT_PATH%" --configuration Release --output "%INSTALL_DIR%" --nologo || goto :Fail
if not exist "%EXECUTABLE_PATH%" (
    echo [BŁĄD] Po publikacji brak pliku "%EXECUTABLE_PATH%".
    goto :Fail
)

echo [3/5] Przygotowanie certyfikatu HTTPS CN=%CERTIFICATE_SUBJECT%...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "$ErrorActionPreference = 'Stop';" ^
    "$subject = 'CN=%CERTIFICATE_SUBJECT%';" ^
    "$certificate = Get-ChildItem Cert:\LocalMachine\My | Where-Object { $_.Subject -eq $subject -and $_.NotAfter -gt (Get-Date).AddDays(30) } | Sort-Object NotAfter -Descending | Select-Object -First 1;" ^
    "if (-not $certificate) {" ^
    "    $certificate = New-SelfSignedCertificate -Subject $subject -DnsName 'localhost', $env:COMPUTERNAME -CertStoreLocation Cert:\LocalMachine\My -NotAfter (Get-Date).AddYears(5) -KeyExportPolicy NonExportable -FriendlyName 'StockCalculator HTTPS';" ^
    "};" ^
    "Write-Host ('Certyfikat: ' + $certificate.Thumbprint);" ^
    "$rootStore = [System.Security.Cryptography.X509Certificates.X509Store]::new('Root', 'LocalMachine');" ^
    "$rootStore.Open('ReadWrite');" ^
    "$rootStore.Add([System.Security.Cryptography.X509Certificates.X509Certificate2]::new($certificate.RawData));" ^
    "$rootStore.Close();" || goto :Fail

echo [4/5] Tworzenie usługi %SERVICE_NAME%...
sc create "%SERVICE_NAME%" binPath= "\"%EXECUTABLE_PATH%\" %KESTREL_ARGS%" start= delayed-auto DisplayName= "%SERVICE_DISPLAY_NAME%" || goto :Fail
sc description "%SERVICE_NAME%" "%SERVICE_DESCRIPTION%" || goto :Fail
sc failure "%SERVICE_NAME%" reset= 86400 actions= restart/60000/restart/60000/restart/60000 || goto :Fail

echo [5/5] Uruchamianie usługi %SERVICE_NAME%...
sc start "%SERVICE_NAME%" || goto :Fail

echo.
echo Gotowe. Aplikacja: https://localhost:%HTTPS_PORT%
echo Logi: "%INSTALL_DIR%\Logs"
exit /b 0

:RemoveExistingService
sc query "%SERVICE_NAME%" >nul 2>&1 || exit /b 0
sc stop "%SERVICE_NAME%" >nul 2>&1
call :WaitForServiceStopped || exit /b 1
sc delete "%SERVICE_NAME%" || exit /b 1
exit /b 0

:WaitForServiceStopped
for /l %%i in (1,1,30) do (
    sc query "%SERVICE_NAME%" | find "STOPPED" >nul && exit /b 0
    timeout /t 1 /nobreak >nul
)
echo [BŁĄD] Usługa %SERVICE_NAME% nie zatrzymała się w ciągu 30 sekund.
exit /b 1

:Fail
echo.
echo [BŁĄD] Instalacja przerwana.
exit /b 1
