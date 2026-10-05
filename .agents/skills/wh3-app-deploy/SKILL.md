---
name: wh3-app-deploy
description: Procedimientos y comandos para compilar, publicar, instalar o actualizar la aplicación de escritorio WH3CharacterManager (.NET 10 WPF) en la PC local del usuario, gestionar procesos activos, accesos directos y ejecución desacoplada.
---

# WH3CharacterManager Deployment Skill (`wh3-app-deploy`)

Esta skill define el procedimiento canónico para compilar, probar, publicar, instalar o actualizar la aplicación de escritorio **WH3CharacterManager** (.NET 10 WPF x64) en la máquina local de Alonso (`Windows 11 / x64`).

---

## 1. 📌 Rutas y Parámetros del Entorno

* **Repositorio:** `C:\Users\alons\Proyectos\Personales\WH3CharacterManager`
* **Directorio de Instalación:**
  `%LOCALAPPDATA%\Programs\WH3CharacterManager` (`$env:LOCALAPPDATA\Programs\WH3CharacterManager`)
* **Ejecutable Principal:**
  `%LOCALAPPDATA%\Programs\WH3CharacterManager\WH3CharacterManager.exe`
* **Ícono de Aplicación:**
  `%LOCALAPPDATA%\Programs\WH3CharacterManager\WH3CharacterManager.ico`
* **Directorio de Accesos Directos:**
  * **Escritorio:** `"$([Environment]::GetFolderPath('Desktop'))\WH3CharacterManager.lnk"`
  * **Menú Inicio:** `"$([Environment]::GetFolderPath('StartMenu'))\Programs\WH3CharacterManager\WH3CharacterManager.lnk"`
* **Directorio de Datos y Caché de Imágenes:**
  `%LOCALAPPDATA%\WH3CharacterManager\cache\images`
* **Directorio de Personajes Guardados del Juego:**
  `%APPDATA%\The Creative Assembly\Warhammer3\saved_characters`
* **Runtime Target:** .NET 10 Windows (`net10.0-windows`), x64.

---

## 2. ⚡ Modo Rápido (Script Automatizado)

El repositorio incluye un script PowerShell listo para producción en el directorio de la skill:

```powershell
# Compilar, ejecutar tests, matar proceso viejo, publicar, actualizar accesos directos y abrir la app:
pwsh -File .agents/skills/wh3-app-deploy/scripts/deploy.ps1

# O si se desea omitir la ejecución de tests para rapidez:
pwsh -File .agents/skills/wh3-app-deploy/scripts/deploy.ps1 -SkipTests

# O si se desea instalar sin abrir la ventana:
pwsh -File .agents/skills/wh3-app-deploy/scripts/deploy.ps1 -LaunchApp:$false
```

---

## 3. 🛠️ Flujo Canónico Paso a Paso

Si se ejecutan los comandos manualmente o paso a paso en una sesión interactiva:

### Paso 1: Ejecutar Pruebas Unitarias
Verificar que ningún cambio haya roto la suite de pruebas del analizador de archivos `.twc` y nombres:
```powershell
dotnet test WH3CharacterManager.sln -c Release --nologo
```
> [!IMPORTANT]
> Nunca procedas con el despliegue si `dotnet test` reporta pruebas fallidas.

### Paso 2: Finalizar Procesos Activos (Evitar File-Lock)
Si la app está en ejecución, Windows bloqueará `WH3CharacterManager.exe` y `WH3CharacterManager.dll`, impidiendo que `dotnet publish` sobrescriba los archivos:
```powershell
Get-Process WH3CharacterManager -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 800
```

### Paso 3: Publicar Binarios
Compilar y publicar la versión Release dirigida a x64:
```powershell
$installDir = "$env:LOCALAPPDATA\Programs\WH3CharacterManager"
if (-not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}

dotnet publish WH3CharacterManager.csproj `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -o $installDir `
    --nologo
```

### Paso 4: Copiar el Ícono de la Aplicación
Asegurar que el archivo `.ico` esté presente en la carpeta de instalación:
```powershell
Copy-Item WH3CharacterManager.ico -Destination "$installDir\WH3CharacterManager.ico" -Force
```

### Paso 5: Actualizar Accesos Directos (Escritorio y Menú Inicio)
Crear o refrescar los accesos directos para que apunten al ejecutable con el ícono correspondiente:
```powershell
$wsh = New-Object -ComObject WScript.Shell
$exePath = "$installDir\WH3CharacterManager.exe"
$iconPath = "$installDir\WH3CharacterManager.ico"

# Escritorio
$desktopLnk = "$([Environment]::GetFolderPath('Desktop'))\WH3CharacterManager.lnk"
$scDesktop = $wsh.CreateShortcut($desktopLnk)
$scDesktop.TargetPath = $exePath
$scDesktop.WorkingDirectory = $installDir
$scDesktop.IconLocation = "$iconPath,0"
$scDesktop.Description = "Total War: Warhammer 3 Character Manager"
$scDesktop.Save()

# Menú Inicio
$startMenuDir = "$([Environment]::GetFolderPath('StartMenu'))\Programs\WH3CharacterManager"
if (-not (Test-Path $startMenuDir)) {
    New-Item -ItemType Directory -Path $startMenuDir -Force | Out-Null
}
$startMenuLnk = "$startMenuDir\WH3CharacterManager.lnk"
$scStart = $wsh.CreateShortcut($startMenuLnk)
$scStart.TargetPath = $exePath
$scStart.WorkingDirectory = $installDir
$scStart.IconLocation = "$iconPath,0"
$scStart.Description = "Total War: Warhammer 3 Character Manager"
$scStart.Save()
```

### Paso 6: Lanzar la Aplicación Desacoplada (WMI / CIM)
> [!CAUTION]
> **REGLA CRÍTICA PARA AGENTES IA:**
> Las herramientas de ejecución de comandos (`run_command`, CLI, wrappers) corren en Windows dentro de un **Job Object** con el flag `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`.
> Si ejecutas la aplicación con `Start-Process`, `& $exePath` o `Invoke-Item`, **la ventana se cerrará inmediatamente** en cuanto el agente finalice su comando.
> **Solución:** Utilizar WMI (`Win32_Process.Create`), lo cual desacopla el nuevo proceso del árbol de procesos del agente:

```powershell
Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{
    CommandLine = "`"$installDir\WH3CharacterManager.exe`""
    CurrentDirectory = $installDir
}
```

### Paso 7: Comprobar Estado del Proceso
```powershell
Get-Process WH3CharacterManager -ErrorAction SilentlyContinue | Select-Object Id, ProcessName, StartTime, Responding
```

---

## 4. 🔍 Diagnóstico y Solución de Problemas (Troubleshooting)

1. **Error: `The process cannot access the file because it is being used by another process`:**
   - Causa: Hay una instancia de `WH3CharacterManager` abierta.
   - Solución: Ejecutar `Get-Process WH3CharacterManager | Stop-Process -Force` antes de publicar.

2. **La app no abre o crashea al iniciar:**
   - Verificar si falta el runtime: `.NET 10 Desktop Runtime (x64)` debe estar instalado en el sistema.
   - Revisar el Visor de Eventos de Windows para excepciones no controladas:
     ```powershell
     Get-WinEvent -ProviderName ".NET Runtime" -MaxEvents 5 -ErrorAction SilentlyContinue | Format-List TimeCreated, Message
     ```

3. **La caché de imágenes está corrupta o se necesita forzar su regeneración:**
   - La caché local de retratos y escudos se almacena en:
     `$env:LOCALAPPDATA\WH3CharacterManager\cache\images`
   - Si se requiere limpiarla:
     ```powershell
     Remove-Item "$env:LOCALAPPDATA\WH3CharacterManager\cache\images\*" -Recurse -Force -ErrorAction SilentlyContinue
     ```
