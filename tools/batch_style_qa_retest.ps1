# Retest Claymation + Icon after style-lock harden.
. "D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\tools\ui_drive.ps1"
Add-Type -AssemblyName System.Windows.Forms

$shots = "D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\tools\shots"
$exe = "D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\bin\Debug\net8.0-windows10.0.19041.0\TXT2IMG.exe"

Get-Process TXT2IMG -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 800
Start-Process $exe
Start-Sleep -Seconds 4

function Wait-GenFast([string]$Tag) {
  Start-Sleep -Seconds 55
  Save-Txt2ImgShot (Join-Path $shots "${Tag}_done.png")
  Write-Host ("Done {0}" -f $Tag)
}

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

function Ensure-Prompt([string]$Text) {
  Type-Txt2ImgText $Text
}

$complex = "studio close-up at eye level: an olive-skinned woman in neon green tracksuit holds purple-gold waterproof concealer against lavender background."

$cases = @(
  @{ Tag = "retest_simple_Claymation"; Style = "Claymation"; Prompt = "red boat" },
  @{ Tag = "retest_complex_Claymation"; Style = "Claymation"; Prompt = $complex },
  @{ Tag = "retest_complex_Icon"; Style = "Icon"; Prompt = $complex },
  @{ Tag = "retest_simple_Icon"; Style = "Icon"; Prompt = "red boat" }
)

foreach ($c in $cases) {
  Write-Host ("-- {0} --" -f $c.Tag)
  Select-StyleByName $c.Style
  Ensure-Prompt $c.Prompt
  Save-Txt2ImgShot (Join-Path $shots ("{0}_ready.png" -f $c.Tag))
  Click-Txt2ImgClient 1080 145
  Start-Sleep -Milliseconds 800
  Wait-GenFast $c.Tag
}

Write-Host "=== RETEST DONE ==="
