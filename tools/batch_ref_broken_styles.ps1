# Focused QA: styles the user said were broken.
# MUST enter prompt via UIA ValuePattern (clipboard paste was missing the TextBox).
. "D:\My Windows Files\Desktop\Code\Published\TXT2IMG+\tools\ui_drive.ps1"
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$root = "D:\My Windows Files\Desktop\Code\Published\TXT2IMG+"
$shots = Join-Path $root "tools\shots\ref_broken_styles"
New-Item -ItemType Directory -Force -Path $shots | Out-Null
$exe = Join-Path $root "bin\Debug\net8.0-windows10.0.19041.0\TXT2IMG.exe"
$ref = Join-Path $root "tools\test_ref_boat.png"
$prompt = "make it a wooden toy boat"

$styles = @(
  "Watercolor",
  "Impasto",
  "Oil Painting",
  "Pixel Art",
  "Icon",
  "Comic Book",
  "Cartoon",
  "Fantasy Art"
)

if (-not (Test-Path -LiteralPath $ref)) {
  Write-Error "Missing boat ref: $ref"
  exit 1
}

Get-Process TXT2IMG -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 800
$env:TXT2IMG_REF = $ref
Start-Process -FilePath $exe
Start-Sleep -Seconds 7

function Get-Root {
  $hwnd = Get-Txt2ImgHwnd
  return [System.Windows.Automation.AutomationElement]::FromHandle([IntPtr]$hwnd)
}

function Get-StyleCombo {
  $root = Get-Root
  $cond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
    [System.Windows.Automation.ControlType]::ComboBox)
  $combos = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
  if ($combos.Count -lt 1) { throw "Style ComboBox not found" }
  return $combos.Item(0)
}

function Get-PromptEdit {
  $root = Get-Root
  # Prefer AutomationId = PromptTextBox (WinUI x:Name)
  $idCond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
    "PromptTextBox")
  $byId = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $idCond)
  if ($byId) { return $byId }

  $editCond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
    [System.Windows.Automation.ControlType]::Edit)
  $edits = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $editCond)
  if ($edits.Count -lt 1) { throw "Prompt Edit not found (edits=0)" }
  # Prompt is the top/widest edit — pick the first.
  return $edits.Item(0)
}

function Set-Prompt([string]$Text) {
  Focus-Txt2Img | Out-Null
  $edit = Get-PromptEdit
  $edit.SetFocus()
  Start-Sleep -Milliseconds 200

  $set = $false
  try {
    $vp = $edit.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    $vp.SetValue($Text)
    $set = $true
    Write-Host ("Prompt ValuePattern set OK (readback='{0}')" -f $vp.Current.Value)
  } catch {
    Write-Host ("ValuePattern failed: {0}" -f $_.Exception.Message)
  }

  if (-not $set) {
    # Fallback: click bounding rect center, select-all, paste
    $rect = $edit.Current.BoundingRectangle
    $x = [int]($rect.X + $rect.Width / 2)
    $y = [int]($rect.Y + $rect.Height / 2)
    [UiDrive]::ClickScreen($x, $y)
    Start-Sleep -Milliseconds 200
    Set-Clipboard -Value $Text
    [System.Windows.Forms.SendKeys]::SendWait("^a")
    Start-Sleep -Milliseconds 100
    [System.Windows.Forms.SendKeys]::SendWait("^v")
    Start-Sleep -Milliseconds 250
    Write-Host "Prompt clipboard paste fallback used"
  }

  # Verify
  try {
    $edit2 = Get-PromptEdit
    $vp2 = $edit2.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    $val = $vp2.Current.Value
    if ($val -ne $Text) {
      throw "Prompt verify failed: got '$val'"
    }
  } catch {
    # WinUI sometimes won't re-read; still continue after paste fallback
    Write-Host ("Prompt verify note: {0}" -f $_.Exception.Message)
  }
}

function Select-Style([string]$DisplayName) {
  Focus-Txt2Img | Out-Null
  $combo = Get-StyleCombo
  $combo.SetFocus()
  Start-Sleep -Milliseconds 200

  $expand = $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
  $expand.Expand()
  Start-Sleep -Milliseconds 600

  $desktop = [System.Windows.Automation.AutomationElement]::RootElement
  $listCond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
    [System.Windows.Automation.ControlType]::ListItem)
  $items = $desktop.FindAll([System.Windows.Automation.TreeScope]::Descendants, $listCond)

  $matched = $null
  foreach ($item in $items) {
    if ($item.Current.Name -eq $DisplayName) { $matched = $item; break }
  }
  if (-not $matched) {
    try { $expand.Collapse() } catch {}
    throw "ListItem not found for '$DisplayName' (items=$($items.Count))"
  }

  $rect = $matched.Current.BoundingRectangle
  $x = [int]($rect.X + $rect.Width / 2)
  $y = [int]($rect.Y + $rect.Height / 2)
  [UiDrive]::ClickScreen($x, $y)
  Start-Sleep -Milliseconds 450
}

function Click-Generate {
  Focus-Txt2Img | Out-Null
  $root = Get-Root
  $btnCond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
    [System.Windows.Automation.ControlType]::Button)
  $btns = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $btnCond)
  foreach ($b in $btns) {
    if ($b.Current.Name -eq "Generate") {
      $b.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
      Start-Sleep -Milliseconds 500
      return
    }
  }
  throw "Generate button not found"
}

function Wait-Gen([string]$Tag, [int]$Seconds = 70) {
  Start-Sleep -Seconds $Seconds
  Save-Txt2ImgShot (Join-Path $shots ("{0}_done.png" -f $Tag))
  Write-Host ("Done {0}" -f $Tag)
}

Write-Host "PROMPT: $prompt"
Write-Host "REF: $ref"
Write-Host "STYLES: $($styles -join ', ')"
Save-Txt2ImgShot (Join-Path $shots "00_startup.png")

# Sanity: prove prompt entry works before the style loop
Set-Prompt $prompt
Save-Txt2ImgShot (Join-Path $shots "00_prompt_set.png")

foreach ($style in $styles) {
  $tag = ($style -replace '[^A-Za-z0-9]', '')
  Write-Host ("=== {0} ===" -f $style)
  # Prompt FIRST, then style. Re-entering prompt after style was resetting the combo to None.
  Set-Prompt $prompt
  try {
    Select-Style $style
  } catch {
    Write-Host ("FAIL select {0}: {1}" -f $style, $_)
    Save-Txt2ImgShot (Join-Path $shots ("{0}_select_fail.png" -f $tag))
    continue
  }
  Save-Txt2ImgShot (Join-Path $shots ("{0}_ready.png" -f $tag))
  Click-Generate
  Wait-Gen $tag 70
}

Write-Host "Finished. Shots in $shots"
