# Pass 6 — complex beauty prompt across styles after uniqueness harden.
. "D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\tools\ui_drive.ps1"
Add-Type -AssemblyName System.Windows.Forms

$shots = "D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\tools\shots"
$exe = "D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\bin\Debug\net8.0-windows10.0.19041.0\TXT2IMG.exe"

function Open-StyleCombo {
  Click-Txt2ImgClient 720 198
  Start-Sleep -Milliseconds 250
  [System.Windows.Forms.SendKeys]::SendWait("{ESC}")
  Start-Sleep -Milliseconds 150
  Click-Txt2ImgClient 720 198
  Start-Sleep -Milliseconds 300
}

function Select-StyleByName([string]$DisplayName) {
  Open-StyleCombo
  Start-Sleep -Milliseconds 200
  [System.Windows.Forms.SendKeys]::SendWait($DisplayName)
  Start-Sleep -Milliseconds 250
  [System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
  Start-Sleep -Milliseconds 300
}

$complex = "studio close-up at eye level: an olive-skinned woman in neon green tracksuit holds purple-gold waterproof concealer against lavender background."

$styles = @(
  @{ Name = "Cartoon"; Display = "Cartoon" },
  @{ Name = "CelShaded"; Display = "Cel-Shaded" },
  @{ Name = "PixelArt"; Display = "Pixel Art" },
  @{ Name = "Watercolor"; Display = "Watercolor" },
  @{ Name = "Icon"; Display = "Icon" },
  @{ Name = "Impasto"; Display = "Impasto" },
  @{ Name = "Anime"; Display = "Anime" },
  @{ Name = "Claymation"; Display = "Claymation" },
  @{ Name = "OilPainting"; Display = "Oil Painting" },
  @{ Name = "FantasyArt"; Display = "Fantasy Art" },
  @{ Name = "ComicBook"; Display = "Comic Book" }
)

Get-Process TXT2IMG -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 800
Start-Process $exe
Start-Sleep -Seconds 4

foreach ($s in $styles) {
  Write-Host ("-- pass6 {0} --" -f $s.Name)
  Select-StyleByName $s.Display
  Type-Txt2ImgText $complex
  Save-Txt2ImgShot (Join-Path $shots ("pass6_complex_{0}_ready.png" -f $s.Name))
  Click-Txt2ImgClient 1080 145
  Start-Sleep -Seconds 55
  Save-Txt2ImgShot (Join-Path $shots ("pass6_complex_{0}_done.png" -f $s.Name))
  Write-Host ("Done {0}" -f $s.Name)
}

Write-Host "=== PASS6 DONE ==="
