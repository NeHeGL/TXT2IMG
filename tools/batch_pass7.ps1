# Keep going: comic long tune + cartoon/anime/pixel/watercolor complex
. "D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\tools\ui_drive.ps1"
Add-Type -AssemblyName System.Windows.Forms

$shots = "D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\tools\shots"
$exe = "D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\bin\Debug\net8.0-windows10.0.19041.0\TXT2IMG.exe"
$complex = "studio close-up at eye level: an olive-skinned woman in neon green tracksuit holds purple-gold waterproof concealer against lavender background."

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

Get-Process TXT2IMG -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 800
Start-Process $exe
Start-Sleep -Seconds 4

$cases = @(
  @{ Tag = "pass7_comic_short"; Style = "Comic Book"; Prompt = "red boat" },
  @{ Tag = "pass7_comic_long"; Style = "Comic Book"; Prompt = $complex },
  @{ Tag = "pass7_cartoon_long"; Style = "Cartoon"; Prompt = $complex },
  @{ Tag = "pass7_pixel_long"; Style = "Pixel Art"; Prompt = $complex },
  @{ Tag = "pass7_anime_long"; Style = "Anime"; Prompt = $complex },
  @{ Tag = "pass7_watercolor_long"; Style = "Watercolor"; Prompt = $complex },
  @{ Tag = "pass7_cel_long"; Style = "Cel-Shaded"; Prompt = $complex },
  @{ Tag = "pass7_icon_long"; Style = "Icon"; Prompt = $complex }
)

foreach ($c in $cases) {
  Write-Host ("-- {0} --" -f $c.Tag)
  Select-StyleByName $c.Style
  Type-Txt2ImgText $c.Prompt
  Save-Txt2ImgShot (Join-Path $shots ("{0}_ready.png" -f $c.Tag))
  Click-Txt2ImgClient 1080 145
  Start-Sleep -Seconds 55
  Save-Txt2ImgShot (Join-Path $shots ("{0}_done.png" -f $c.Tag))
  Write-Host ("Done {0}" -f $c.Tag)
}
Write-Host "=== PASS7 DONE ==="
