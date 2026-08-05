# UI drive: prompt + style + generate with boat reference (non-person QA subject).
. "D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\tools\ui_drive.ps1"
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$root = "D:\My Windows Files\Desktop\Code\Published\TXT2IMG+"
$shots = Join-Path $root "tools\shots\ref_ui"
New-Item -ItemType Directory -Force -Path $shots | Out-Null
$exe = Join-Path $root "bin\Debug\net8.0-windows10.0.19041.0\TXT2IMG.exe"
$ref = Join-Path $root "tools\test_ref_boat.png"
$prompt = "make it a wooden toy boat"

$styles = @(
  "Claymation",
  "Pixel Art",
  "Fantasy Art",
  "Oil Painting",
  "Impasto",
  "Comic Book",
  "Cartoon",
  "Icon",
  "Watercolor"
)

Get-Process TXT2IMG -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 800

if (-not (Test-Path -LiteralPath $ref)) {
  Write-Error "Missing boat reference: $ref"
  exit 1
}

$env:TXT2IMG_REF = $ref
Start-Process -FilePath $exe
Start-Sleep -Seconds 7

function Get-RootElement {
  $hwnd = Get-Txt2ImgHwnd
  return [System.Windows.Automation.AutomationElement]::FromHandle([IntPtr]$hwnd)
}

function Select-StyleByName([string]$DisplayName) {
  Focus-Txt2Img | Out-Null
  $root = Get-RootElement
  $comboCond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
    [System.Windows.Automation.ControlType]::ComboBox)
  $combos = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $comboCond)
  if ($combos.Count -lt 1) { throw "No ComboBox found" }

  # First combo is Style (Aspect is second).
  $styleCombo = $combos[0]
  $expand = $styleCombo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
  $expand.Expand()
  Start-Sleep -Milliseconds 400

  $itemCond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
    [System.Windows.Automation.ControlType]::ListItem)
  $items = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $itemCond)
  $found = $false
  foreach ($item in $items) {
    $name = $item.Current.Name
    if ($name -eq $DisplayName) {
      try {
        $sel = $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
        $sel.Select()
      } catch {
        $inv = $item.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
        $inv.Invoke()
      }
      $found = $true
      break
    }
  }
  if (-not $found) {
    # Fallback: typeahead then Enter
    [System.Windows.Forms.SendKeys]::SendWait($DisplayName)
    Start-Sleep -Milliseconds 200
    [System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
  }
  Start-Sleep -Milliseconds 350
  try { $expand.Collapse() } catch {}
  Start-Sleep -Milliseconds 200
}

function Ensure-Prompt([string]$Text) {
  Type-Txt2ImgText $Text
}

function Click-Generate {
  Focus-Txt2Img | Out-Null
  $root = Get-RootElement
  $btnCond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
    [System.Windows.Automation.ControlType]::Button)
  $btns = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $btnCond)
  foreach ($b in $btns) {
    if ($b.Current.Name -eq "Generate") {
      $inv = $b.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
      $inv.Invoke()
      Start-Sleep -Milliseconds 400
      return
    }
  }
  Click-Txt2ImgClient 1050 145
  Start-Sleep -Milliseconds 400
}

function Wait-Gen([string]$Tag, [int]$Seconds = 75) {
  $deadline = (Get-Date).AddSeconds($Seconds)
  $n = 0
  while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 5
    $n++
    if ($n -eq 2 -or $n -eq 8 -or ((Get-Date) -ge $deadline.AddSeconds(-6))) {
      Save-Txt2ImgShot (Join-Path $shots ("{0}_p{1}.png" -f $Tag, $n))
    }
  }
  Save-Txt2ImgShot (Join-Path $shots ("{0}_done.png" -f $Tag))
  Write-Host ("Done {0}" -f $Tag)
}

Save-Txt2ImgShot (Join-Path $shots "00_startup_ref.png")

foreach ($style in $styles) {
  $tag = ($style -replace '[^A-Za-z0-9]', '')
  Write-Host ("=== {0} ===" -f $style)
  Select-StyleByName $style
  Ensure-Prompt $prompt
  Start-Sleep -Milliseconds 400
  Save-Txt2ImgShot (Join-Path $shots ("{0}_ready.png" -f $tag))
  Click-Generate
  Wait-Gen $tag 80
}

Write-Host "All UI style tests finished. Shots in $shots"
