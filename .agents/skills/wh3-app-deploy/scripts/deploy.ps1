<#
.SYNOPSIS
    Script automatizado de instalación y actualización de WH3CharacterManager.
.DESCRIPTION
    Compila en Release, corre las pruebas unitarias, cierra procesos activos,
    publica los binarios en %LOCALAPPDATA%\Programs\WH3CharacterManager,
    actualiza los accesos directos (Escritorio y Menú Inicio) y opcionalmente
    inicia la aplicación desacoplada de la terminal (WMI).
.PARAMETER LaunchApp
    Si se especifica (por defecto $true), inicia la aplicación tras la publicación.
.PARAMETER SkipTests
    Si se especifica, omite la ejecución previa de pruebas unitarias.
.PARAMETER Configuration
    Configuración de compilación (Release por defecto).
#>
[CmdletBinding()]
param(
    [switch]$LaunchApp = $true,
    [switch]$SkipTests = $false,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..\..")).Path
Set-Location $repoRoot

Write-Host "==> [1/6] Validando repositorio en $repoRoot..." -ForegroundColor Cyan

# 1. Pruebas unitarias
if (-not $SkipTests) {
    Write-Host "==> [2/6] Ejecutando pruebas unitarias..." -ForegroundColor Cyan
    dotnet test WH3CharacterManager.sln -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Las pruebas unitarias fallaron. Despliegue cancelado."
        exit 1
    }
} else {
    Write-Host "==> [2/6] Pruebas unitarias omitidas (-SkipTests)." -ForegroundColor Yellow
}

# 2. Cerrar procesos activos para evitar file-lock
Write-Host "==> [3/6] Deteniendo instancias previas de WH3CharacterManager..." -ForegroundColor Cyan
$runningProcesses = Get-Process WH3CharacterManager -ErrorAction SilentlyContinue
if ($runningProcesses) {
    $runningProcesses | Stop-Process -Force
    Start-Sleep -Milliseconds 800
    Write-Host "    Instancia previa finalizada correctamente." -ForegroundColor Gray
}

# 3. Publicación
$installDir = Join-Path $env:LOCALAPPDATA "Programs\WH3CharacterManager"
Write-Host "==> [4/6] Publicando binarios en $installDir..." -ForegroundColor Cyan
if (-not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}

dotnet publish WH3CharacterManager.csproj `
    -c $Configuration `
    -r win-x64 `
    --self-contained false `
    -o $installDir `
    --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Error "Fallo en dotnet publish."
    exit 1
}

# Copiar icono explícitamente
$iconSource = Join-Path $repoRoot "WH3CharacterManager.ico"
if (Test-Path $iconSource) {
    Copy-Item $iconSource -Destination (Join-Path $installDir "WH3CharacterManager.ico") -Force
}

# 4. Accesos directos
Write-Host "==> [5/6] Creando/Actualizando accesos directos..." -ForegroundColor Cyan
$exePath = Join-Path $installDir "WH3CharacterManager.exe"
$iconPath = Join-Path $installDir "WH3CharacterManager.ico"
$wsh = New-Object -ComObject WScript.Shell

# Desktop shortcut
$desktopPath = [Environment]::GetFolderPath("Desktop")
$desktopLnk = Join-Path $desktopPath "WH3CharacterManager.lnk"
$scDesktop = $wsh.CreateShortcut($desktopLnk)
$scDesktop.TargetPath = $exePath
$scDesktop.WorkingDirectory = $installDir
$scDesktop.IconLocation = "$iconPath,0"
$scDesktop.Description = "Total War: Warhammer 3 Character Manager"
$scDesktop.Save()

# Start Menu shortcut
$startMenuDir = Join-Path ([Environment]::GetFolderPath("StartMenu")) "Programs\WH3CharacterManager"
if (-not (Test-Path $startMenuDir)) {
    New-Item -ItemType Directory -Path $startMenuDir -Force | Out-Null
}
$startMenuLnk = Join-Path $startMenuDir "WH3CharacterManager.lnk"
$scStart = $wsh.CreateShortcut($startMenuLnk)
$scStart.TargetPath = $exePath
$scStart.WorkingDirectory = $installDir
$scStart.IconLocation = "$iconPath,0"
$scStart.Description = "Total War: Warhammer 3 Character Manager"
$scStart.Save()

Write-Host "    Acceso directo Escritorio : $desktopLnk" -ForegroundColor Gray
Write-Host "    Acceso directo Menú Inicio: $startMenuLnk" -ForegroundColor Gray

# 5. Iniciar la aplicación desacoplada si corresponde
if ($LaunchApp) {
    Write-Host "==> [6/6] Iniciando aplicación desacoplada (WMI/Win32_Process)..." -ForegroundColor Cyan
    $result = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{
        CommandLine = "`"$exePath`""
        CurrentDirectory = $installDir
    }
    
    if ($result.ReturnValue -eq 0) {
        Write-Host "==> Despliegue completado con éxito! PID iniciado: $($result.ProcessId)" -ForegroundColor Green
    } else {
        Write-Warning "No se pudo iniciar la aplicación vía WMI. Código de retorno: $($result.ReturnValue)"
    }
} else {
    Write-Host "==> [6/6] Despliegue completado exitosamente (inicio omitido)." -ForegroundColor Green
}
