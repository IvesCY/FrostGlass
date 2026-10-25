$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = Join-Path $root 'src'

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $csc)) {
    throw 'csc.exe not found. .NET Framework 4.x is required.'
}

$outName = ([string][char]0x6BDB) + [char]0x73BB + [char]0x7483 + '.exe'
$sources = @(
    'AppSettings.cs',
    'DarkMenuRenderer.cs',
    'FastBlur.cs',
    'FrostPane.cs',
    'NativeMethods.cs',
    'PaneBar.cs',
    'PaneResizer.cs',
    'Program.cs',
    'SelectorForm.cs',
    'SettingsPanel.cs',
    'TrayAppContext.cs',
    'Ui.cs'
)

Push-Location $src
try {
    & $csc -nologo -codepage:65001 -target:winexe -optimize+ -platform:anycpu `
        -win32icon:..\assets\app.ico -win32manifest:..\app.manifest `
        -r:System.dll -r:System.Drawing.dll -r:System.Windows.Forms.dll `
        "-out:..\$outName" $sources
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

Write-Host ''
Write-Host "Built: $root\$outName" -ForegroundColor Green
