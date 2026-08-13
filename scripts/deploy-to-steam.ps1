#Requires -Version 5.1
<#
.SYNOPSIS
  Deploy modern Septerra Core QoL overlay into a Steam install.

.DESCRIPTION
  Copies Albeoris Launcher + bats, applies dgVoodoo2 MS\x86 wrappers + tuned conf,
  writes Launch.bat (auto-run, movies enabled), optionally installs QuickTime into SysWOW64.
  Rewires Steam Play so septerra.exe is a trampoline into Albeoris inject (F-skip / -r).

  Does NOT ship or modify game DATA/*.db. Requires a purchased Steam install.
#>
[CmdletBinding()]
param(
  [string] $GameRoot = 'C:\Program Files (x86)\Steam\steamapps\common\Septerra Core',
  [string] $DgVoodooRoot = '',
  [switch] $SkipQuickTime,
  [switch] $SkipDgVoodoo
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function IndexOfBytes {
  param([byte[]] $Haystack, [byte[]] $Needle)
  for ($i = 0; $i -le $Haystack.Length - $Needle.Length; $i++) {
    $ok = $true
    for ($j = 0; $j -lt $Needle.Length; $j++) {
      if ($Haystack[$i + $j] -ne $Needle[$j]) { $ok = $false; break }
    }
    if ($ok) { return $i }
  }
  return -1
}

function Install-SteamPlayEntry {
  param(
    [string] $GameRoot,
    [string] $RepoRoot
  )

  $vanilla = Join-Path $GameRoot 'septerra.exe'
  $engine = Join-Path $GameRoot 'septerra.bin'
  $albeoris = Join-Path $GameRoot 'Launcher\Septerra.exe'

  if (-not (Test-Path $albeoris)) {
    throw "Albeoris launcher missing at $albeoris"
  }

  $vanillaInfo = Get-Item $vanilla
  if ($vanillaInfo.Length -gt 100000) {
    Copy-Item $vanilla $engine -Force
    Write-Host "Saved original engine as septerra.bin ($($vanillaInfo.Length) bytes)."
  }
  elseif (-not (Test-Path $engine)) {
    throw 'septerra.exe is already a stub but septerra.bin is missing. Restore the game from Steam, then re-run.'
  }

  $bytes = [System.IO.File]::ReadAllBytes($albeoris)
  $needle = [System.Text.Encoding]::Unicode.GetBytes('septerra.exe')
  $replace = [System.Text.Encoding]::Unicode.GetBytes('septerra.bin')
  $idx = IndexOfBytes $bytes $needle
  if ($idx -ge 0) {
    [Array]::Copy($replace, 0, $bytes, $idx, $replace.Length)
    [System.IO.File]::WriteAllBytes($albeoris, $bytes)
    Write-Host 'Albeoris now injects septerra.bin (Steam Play can own septerra.exe).'
  }
  else {
    Write-Host 'Albeoris already points at septerra.bin (or string not found).'
  }

  $csc = @(
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe",
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe",
    "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
  ) | Where-Object { Test-Path $_ } | Select-Object -First 1

  if (-not $csc) {
    throw 'csc.exe not found; cannot build Steam Play entry stub.'
  }

  $src = Join-Path $RepoRoot 'tools\SepterraSteamEntry.cs'
  & $csc /nologo /target:winexe /optimize+ /r:System.Windows.Forms.dll /out:$vanilla $src
  if ($LASTEXITCODE -ne 0) {
    throw "csc failed with exit $LASTEXITCODE"
  }
  Write-Host 'Steam Play septerra.exe is now the QoL trampoline.'
}

$RepoRoot = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path (Join-Path $GameRoot 'septerra.exe'))) {
  throw "septerra.exe not found under GameRoot: $GameRoot"
}

Write-Host "Deploying QoL into: $GameRoot"

$destLauncher = Join-Path $GameRoot 'Launcher'
if (Test-Path $destLauncher) {
  Remove-Item $destLauncher -Recurse -Force
}
Copy-Item (Join-Path $RepoRoot 'Launcher') $destLauncher -Recurse -Force

$tableSrc = Join-Path $destLauncher 'GameInjectionHookAddressTable'
if (Test-Path $tableSrc) {
  Copy-Item $tableSrc (Join-Path $GameRoot 'GameInjectionHookAddressTable') -Force
  Write-Host 'Injected GameInjectionHookAddressTable into game root (required by forked Core.dll).'
}

foreach ($bat in @(
  'Unpack.bat', 'UnpackAndConvert.bat',
  'ConvertAudio-Mp3-to-Vssf.bat', 'ConvertAudio-Vssf-to-Mp3.bat',
  'ConvertImages-Am-to-Tiff.bat', 'ConvertImages-Tiff-to-Am.bat',
  'ConvertText-Tx-to-Txt.bat', 'Launch.bat'
)) {
  Copy-Item (Join-Path $RepoRoot $bat) (Join-Path $GameRoot $bat) -Force
}

if (-not $SkipDgVoodoo) {
  if (-not $DgVoodooRoot) {
    $candidate = Join-Path $env:USERPROFILE 'Downloads\dgVoodoo2_86_4'
    if (Test-Path (Join-Path $candidate 'MS\x86\DDraw.dll')) {
      $DgVoodooRoot = $candidate
    }
  }
  if (-not $DgVoodooRoot -or -not (Test-Path (Join-Path $DgVoodooRoot 'MS\x86\DDraw.dll'))) {
    throw 'dgVoodoo2 root with MS\x86\DDraw.dll required (pass -DgVoodooRoot or extract to Downloads\dgVoodoo2_86_4).'
  }

  $ms = Join-Path $DgVoodooRoot 'MS\x86'
  foreach ($f in @('DDraw.dll', 'D3DImm.dll', 'D3D8.dll', 'D3D9.dll')) {
    Copy-Item (Join-Path $ms $f) (Join-Path $GameRoot $f) -Force
  }
  if (Test-Path (Join-Path $DgVoodooRoot 'dgVoodooCpl.exe')) {
    Copy-Item (Join-Path $DgVoodooRoot 'dgVoodooCpl.exe') (Join-Path $GameRoot 'dgVoodooCpl.exe') -Force
  }
  Copy-Item (Join-Path $RepoRoot 'dgVoodoo\dgVoodoo.conf') (Join-Path $GameRoot 'dgVoodoo.conf') -Force
  Write-Host 'dgVoodoo2 wrappers + conf installed.'
}

if (-not $SkipQuickTime) {
  $qtBat = Join-Path $GameRoot 'qt\QuickTimeInstaller.bat'
  if (Test-Path $qtBat) {
    Write-Host 'Installing QuickTime codecs (elevation prompt)...'
    Start-Process -FilePath 'cmd.exe' -ArgumentList '/c', "`"$qtBat`"" -Verb RunAs -Wait
  }
  else {
    Write-Warning "qt\QuickTimeInstaller.bat missing under game root; skip QT."
  }
}

Install-SteamPlayEntry -GameRoot $GameRoot -RepoRoot $RepoRoot

Write-Host 'Done. Steam Play and Launch.bat both go through Albeoris (F-skip, auto-run, movies).'
Write-Host 'OBS: Game Capture or Window Capture. Steam Verify Integrity restores vanilla septerra.exe — re-run this script.'
