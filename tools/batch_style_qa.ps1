# Batch style QA: simple prompt through every style, then complex prompt spot-checks.
. "D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\tools\ui_drive.ps1"
Add-Type -AssemblyName System.Windows.Forms

$shots = "D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\tools\shots"
New-Item -ItemType Directory -Force -Path $shots | Out-Null

$exe = "D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\bin\Debug\net8.0-windows10.0.19041.0\TXT2IMG.exe"
Get-Process TXT2IMG -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 800
Start-Process $exe
Start-Sleep -Seconds 5

function Wait-Gen([string]$Tag, [int]$MaxPolls = 14) {
  for ($n = 1; $n -le $MaxPolls; $n++) {
    Start-Sleep -Seconds 12
    Save-Txt2ImgShot (Join-Path $shots "${Tag}_p$n.png")
  }
  Save-Txt2ImgShot (Join-Path $shots "${Tag}_done.png")
}

function Ensure-Prompt([string]$Text) {
  Click-Txt2ImgClient 400 145
  Start-Sleep -Milliseconds 250
  Type-Txt2ImgText $Text
  Start-Sleep -Milliseconds 300
}

function Open-StyleCombo {
  Click-Txt2ImgClient 720 198
  Start-Sleep -Milliseconds 300
  [System.Windows.Forms.SendKeys]::SendWait("{ESC}")
  Start-Sleep -Milliseconds 200
  Click-Txt2ImgClient 720 198
  Start-Sleep -Milliseconds 400
}

function Select-StyleByIndexFromTop([int]$Index) {
  # Open dropdown, walk from the top with Up spam then Down — never SendKeys {HOME}
  # while focus might be in the prompt TextBox (that jumps the caret / clears selection).
  Open-StyleCombo
  1..20 | ForEach-Object {
    [System.Windows.Forms.SendKeys]::SendWait("{UP}")
    Start-Sleep -Milliseconds 40
  }
  Start-Sleep -Milliseconds 150
  if ($Index -gt 0) {
    1..$Index | ForEach-Object {
      [System.Windows.Forms.SendKeys]::SendWait("{DOWN}")
      Start-Sleep -Milliseconds 80
    }
  }
  [System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
  Start-Sleep -Milliseconds 350
}

# Styles to sweep with simple prompt (skip None)
$simpleStyles = @(
  @{ Name = "Cartoon"; Index = 1 },
  @{ Name = "Icon"; Index = 2 },
  @{ Name = "ComicBook"; Index = 3 },
  @{ Name = "CelShaded"; Index = 4 },
  @{ Name = "Anime"; Index = 5 },
  @{ Name = "Watercolor"; Index = 6 },
  @{ Name = "Impasto"; Index = 7 },
  @{ Name = "OilPainting"; Index = 8 },
  @{ Name = "Claymation"; Index = 9 },
  @{ Name = "FantasyArt"; Index = 10 },
  @{ Name = "PixelArt"; Index = 11 },
  @{ Name = "3DRender"; Index = 12 },
  @{ Name = "Photorealistic"; Index = 13 },
  @{ Name = "VintagePhoto"; Index = 14 }
)

Write-Host "=== SIMPLE PASS: red boat x all styles ==="
foreach ($s in $simpleStyles) {
  Write-Host ("-- {0} --" -f $s.Name)
  # Style first, THEN prompt — selecting the combo must not wipe the TextBox.
  Select-StyleByIndexFromTop $s.Index
  Ensure-Prompt "red boat"
  Save-Txt2ImgShot (Join-Path $shots ("simple_{0}_ready.png" -f $s.Name))
  Click-Txt2ImgClient 1080 145
  Start-Sleep -Seconds 2
  Wait-Gen ("simple_{0}" -f $s.Name) 14
}

$complex = "studio close-up at eye level: an olive-skinned woman in neon green tracksuit holds purple-gold waterproof concealer against lavender background."
$complexStyles = @(
  @{ Name = "Claymation"; Index = 9 },
  @{ Name = "PixelArt"; Index = 11 },
  @{ Name = "CelShaded"; Index = 4 },
  @{ Name = "Cartoon"; Index = 1 },
  @{ Name = "Watercolor"; Index = 6 },
  @{ Name = "ComicBook"; Index = 3 }
)

Write-Host "=== COMPLEX PASS: studio beauty x key styles ==="
foreach ($s in $complexStyles) {
  Write-Host ("-- complex {0} --" -f $s.Name)
  Select-StyleByIndexFromTop $s.Index
  Ensure-Prompt $complex
  Save-Txt2ImgShot (Join-Path $shots ("complex_{0}_ready.png" -f $s.Name))
  Click-Txt2ImgClient 1080 145
  Start-Sleep -Seconds 2
  Wait-Gen ("complex_{0}" -f $s.Name) 14
}

Write-Host "=== ALL PASSES DONE ==="
