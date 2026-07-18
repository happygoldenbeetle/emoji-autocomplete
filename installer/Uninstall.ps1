# Emoji Autocomplete — uninstaller
$ErrorActionPreference = 'SilentlyContinue'
Get-AppxPackage -Name "EmojiAutocomplete" | Remove-AppxPackage
Write-Host "Emoji Autocomplete removed." -ForegroundColor Green
Read-Host "Press Enter to close"
