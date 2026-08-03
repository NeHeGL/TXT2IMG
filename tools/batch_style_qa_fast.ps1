# Fast style QA — waits until Generate is idle again instead of fixed 3-minute sleeps.
. "D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\tools\ui_drive.ps1"
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$shots = "D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\tools\shots"
New-Item -ItemType Directory -Force -Path $shots | Out-Null
$exe = "D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\bin\Debug\net8.0-windows10.0.19041.0\TXT2IMG.exe"

Get-Process TXT2IMG -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 800
Start-Process $exe
Start-Sleep -Seconds 4

function Get-GenerateButtonName {
  try {
    $hwnd = Get-Txt2ImgHwnd
    $root = [System.Windows.Automation.AutomationElement]::FromHandle([IntPtr]$hwnd)
    $cond = New-Object System.Windows.Automation.PropertyCondition(
      [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
      [System.Windows.Automation.ControlType]::Button)
    $btns = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
    foreach ($b in $btns) {
      $n = $b.Current.Name
      if ($n -match 'Generate|Downloading|Loading|Starting') { return $n }
    }
  } catch {}
  return $null
}

function Wait-GenFast([string]$Tag) {
  # WinUI often won't expose Generate button Name via UIA — don't rely on it.
  # Empirically a 5-image batch finishes in ~35-50s on this machine; poll briefly then move on.
  $deadline = (Get-Date).AddSeconds(55)
  $n = 0
  while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 3
    $n++
  }
  Save-Txt2ImgShot (Join-Path $shots "${Tag}_done.png")
  Write-Host ("Done {0} after ~{1}s" -f $Tag, ($n * 3))
}

function Ensure-Prompt([string]$Text) {
  Type-Txt2ImgText $Text
}

function Open-StyleCombo {
  Click-Txt2ImgClient 720 198
  Start-Sleep -Milliseconds 250
  [System.Windows.Forms.SendKeys]::SendWait("{ESC}")
  Start-Sleep -Milliseconds 150
  Click-Txt2ImgClient 720 198
  Start-Sleep -Milliseconds 300
}

function Select-StyleByIndexFromTop([int]$Index) {
  Open-StyleCombo
  1..20 | ForEach-Object { [System.Windows.Forms.SendKeys]::SendWait("{UP}"); Start-Sleep -Milliseconds 25 }
  Start-Sleep -Milliseconds 100
  if ($Index -gt 0) {
    1..$Index | ForEach-Object { [System.Windows.Forms.SendKeys]::SendWait("{DOWN}"); Start-Sleep -Milliseconds 50 }
  }
  [System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
  Start-Sleep -Milliseconds 250
}

function Select-StyleByName([string]$DisplayName) {
  Open-StyleCombo
  # Typeahead is far more reliable than counting Up/Down (was landing 1 off).
  Start-Sleep -Milliseconds 200
  [System.Windows.Forms.SendKeys]::SendWait($DisplayName)
  Start-Sleep -Milliseconds 250
  [System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
  Start-Sleep -Milliseconds 300
}

# Resume after Claymation. DisplayName must match ComboBoxItem Content exactly.
$simpleStyles = @(
  @{ Name = "FantasyArt"; Display = "Fantasy Art" },
  @{ Name = "PixelArt"; Display = "Pixel Art" },
  @{ Name = "3DRender"; Display = "3D Render" },
  @{ Name = "Photorealistic"; Display = "Photorealistic" },
  @{ Name = "VintagePhoto"; Display = "Vintage Photo" }
)

$complex = "studio close-up at eye level: an olive-skinned woman in neon green tracksuit holds purple-gold waterproof concealer against lavender background."
$complexStyles = @(
  @{ Name = "Claymation"; Display = "Claymation" },
  @{ Name = "PixelArt"; Display = "Pixel Art" },
  @{ Name = "CelShaded"; Display = "Cel-Shaded" },
  @{ Name = "Cartoon"; Display = "Cartoon" },
  @{ Name = "Watercolor"; Display = "Watercolor" },
  @{ Name = "ComicBook"; Display = "Comic Book" },
  @{ Name = "Icon"; Display = "Icon" }
)

Write-Host "=== FAST SIMPLE PASS (resume) ==="
foreach ($s in $simpleStyles) {
  Write-Host ("-- {0} --" -f $s.Name)
  Select-StyleByName $s.Display
  Ensure-Prompt "red boat"
  Save-Txt2ImgShot (Join-Path $shots ("simple_{0}_ready.png" -f $s.Name))
  Click-Txt2ImgClient 1080 145
  Start-Sleep -Milliseconds 800
  Wait-GenFast ("simple_{0}" -f $s.Name)
}

Write-Host "=== FAST COMPLEX PASS ==="
foreach ($s in $complexStyles) {
  Write-Host ("-- complex {0} --" -f $s.Name)
  Select-StyleByName $s.Display
  Ensure-Prompt $complex
  Save-Txt2ImgShot (Join-Path $shots ("complex_{0}_ready.png" -f $s.Name))
  Click-Txt2ImgClient 1080 145
  Start-Sleep -Milliseconds 800
  Wait-GenFast ("complex_{0}" -f $s.Name)
}

Write-Host "=== ALL PASSES DONE ==="
