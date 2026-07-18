# Emoji Autocomplete — Install

System-wide `:shortcode:` emoji autocomplete. Type `:sob:` in any app → 😭.

## Install (recommended)

1. Keep all files in this folder together (the `.msix`, the `.cer`, and `Install.ps1`).
2. Right-click **`Install.ps1`** → **Run with PowerShell**.
3. Approve the admin prompt. The script trusts the bundled certificate and installs the app.
4. Launch **Emoji Autocomplete** from the Start menu — it runs in the system tray.

## Why the certificate step?

The app is packaged as a self-signed MSIX. Windows only installs MSIX packages whose
signing certificate it trusts, so the installer adds the included `.cer` to the machine's
trusted stores first. (A commercial code-signing certificate would remove this step, but
isn't included here.)

## Manual install (alternative)

```powershell
# From an elevated PowerShell prompt, in this folder:
Import-Certificate -FilePath .\EmojiAutocomplete.cer -CertStoreLocation Cert:\LocalMachine\Root
Add-AppxPackage -Path .\<the-package>.msix
```

## Uninstall

Right-click **`Uninstall.ps1`** → Run with PowerShell, or remove **Emoji Autocomplete**
from Windows Settings › Apps.

## Notes

- The package is **self-contained** — no separate .NET or Windows App SDK runtime needed.
- On first launch it sits quietly in the tray. Right-click the tray icon for **Start on boot**
  and **Exit**.
- Requires 64-bit Windows 10 (1809 / build 17763) or later.
