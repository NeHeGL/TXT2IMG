# Quick retest: complex Claymation subject lock (woman + concealer).
. "D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\tools\ui_drive.ps1"
Add-Type -AssemblyName System.Windows.Forms

$shots = "D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\tools\shots"
$exe = "D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\bin\Debug\net8.0-windows10.0.19041.0\TXT2IMG.exe"

Get-Process TXT2IMG -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 800
Start-Process $exe
Start-Sleep -Seconds 4

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

Write-Host "-- clay complex subject lock --"
Select-StyleByName "Claymation"
Type-Txt2ImgText $complex
Save-Txt2ImgShot (Join-Path $shots "retest3_complex_Claymation_ready.png")
Click-Txt2ImgClient 1080 145
Start-Sleep -Seconds 55
Save-Txt2ImgShot (Join-Path $shots "retest3_complex_Claymation_done.png")
Write-Host "DONE"
