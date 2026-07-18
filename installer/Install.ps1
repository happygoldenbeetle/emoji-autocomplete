# Emoji Autocomplete — installer
# Trusts the bundled self-signed certificate, then installs the MSIX package.
# Right-click this file > "Run with PowerShell" (it will ask for admin).

$ErrorActionPreference = 'Stop'

# --- Re-launch elevated if needed (trusting a cert + install needs admin) ----
$principal = New-Object Security.Principal.WindowsPrincipal(
    [Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Start-Process powershell.exe -Verb RunAs `
        -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    return
}

$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$cer  = Get-ChildItem -Path $dir -Filter *.cer  | Select-Object -First 1
$msix = Get-ChildItem -Path $dir -Filter *.msix | Select-Object -First 1

if (-not $cer)  { throw "No .cer found next to this script." }
if (-not $msix) { throw "No .msix found next to this script." }

Write-Host "Trusting certificate: $($cer.Name)" -ForegroundColor Cyan
Import-Certificate -FilePath $cer.FullName -CertStoreLocation Cert:\LocalMachine\Root        | Out-Null
Import-Certificate -FilePath $cer.FullName -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null

Write-Host "Installing: $($msix.Name)" -ForegroundColor Cyan
Add-AppxPackage -Path $msix.FullName

Write-Host ""
Write-Host "Done. Launch 'Emoji Autocomplete' from the Start menu." -ForegroundColor Green
Write-Host "It lives in the system tray — right-click the tray icon for options." -ForegroundColor Green
Write-Host ""
Read-Host "Press Enter to close"
