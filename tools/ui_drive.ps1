# Win32 UI helpers for driving TXT2IMG during automated testing.
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;

public static class UiDrive {
    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP   = 0x0004;
    public const uint MOUSEEVENTF_MOVE     = 0x0001;
    public const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
    public const uint KEYEVENTF_KEYUP      = 0x0002;
    public const byte VK_RETURN = 0x0D;
    public const byte VK_TAB    = 0x09;
    public const byte VK_CONTROL = 0x11;
    public const byte VK_A = 0x41;
    public const uint WM_SETTEXT = 0x000C;
    public const uint BM_CLICK = 0x00F5;

    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
    [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    public static void ClickScreen(int x, int y) {
        SetCursorPos(x, y);
        System.Threading.Thread.Sleep(40);
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
        System.Threading.Thread.Sleep(30);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
    }

    public static void TapKey(byte vk) {
        keybd_event(vk, 0, 0, UIntPtr.Zero);
        System.Threading.Thread.Sleep(30);
        keybd_event(vk, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    public static void SelectAll() {
        keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
        keybd_event(VK_A, 0, 0, UIntPtr.Zero);
        System.Threading.Thread.Sleep(30);
        keybd_event(VK_A, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    public static void SaveWindowBitmap(IntPtr hwnd, string path) {
        RECT r;
        GetWindowRect(hwnd, out r);
        int w = Math.Max(1, r.Right - r.Left);
        int h = Math.Max(1, r.Bottom - r.Top);
        using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(bmp)) {
            IntPtr hdc = g.GetHdc();
            // PW_RENDERFULLCONTENT = 2 — needed for DirectComposition / WinUI surfaces
            bool ok = PrintWindow(hwnd, hdc, 2);
            g.ReleaseHdc(hdc);
            if (!ok) {
                g.CopyFromScreen(r.Left, r.Top, 0, 0, new Size(w, h), CopyPixelOperation.SourceCopy);
            }
            bmp.Save(path, ImageFormat.Png);
        }
    }
}
"@ -ReferencedAssemblies System.Drawing

function Get-Txt2ImgHwnd {
    $p = Get-Process TXT2IMG -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    if (-not $p) { throw "TXT2IMG is not running" }
    return $p.MainWindowHandle
}

function Focus-Txt2Img {
    $hwnd = Get-Txt2ImgHwnd
    [UiDrive]::ShowWindow($hwnd, 9) | Out-Null # SW_RESTORE
    [UiDrive]::SetForegroundWindow($hwnd) | Out-Null
    Start-Sleep -Milliseconds 300
    return $hwnd
}

function Save-Txt2ImgShot([string]$Path) {
    $hwnd = Focus-Txt2Img
    $dir = Split-Path $Path -Parent
    if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
    [UiDrive]::SaveWindowBitmap($hwnd, $Path)
    Write-Host "Saved $Path"
}

function Click-Txt2ImgClient([int]$X, [int]$Y) {
    $hwnd = Focus-Txt2Img
    $pt = New-Object UiDrive+POINT
    $pt.X = $X; $pt.Y = $Y
    [UiDrive]::ClientToScreen($hwnd, [ref]$pt) | Out-Null
    [UiDrive]::ClickScreen($pt.X, $pt.Y)
}

function Type-Txt2ImgText([string]$Text) {
    # Prefer UIA ValuePattern on PromptTextBox — fixed client coords miss on some DPI setups.
    Add-Type -AssemblyName UIAutomationClient
    Add-Type -AssemblyName UIAutomationTypes
    Add-Type -AssemblyName System.Windows.Forms
    Focus-Txt2Img | Out-Null
    $hwnd = Get-Txt2ImgHwnd
    $root = [System.Windows.Automation.AutomationElement]::FromHandle([IntPtr]$hwnd)
    $idCond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        "PromptTextBox")
    $edit = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $idCond)
    if (-not $edit) {
        $editCond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::Edit)
        $edits = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $editCond)
        if ($edits.Count -gt 0) { $edit = $edits.Item(0) }
    }
    if ($edit) {
        $edit.SetFocus()
        Start-Sleep -Milliseconds 150
        try {
            $vp = $edit.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
            $vp.SetValue($Text)
            return
        } catch {}
        $rect = $edit.Current.BoundingRectangle
        [UiDrive]::ClickScreen([int]($rect.X + $rect.Width / 2), [int]($rect.Y + $rect.Height / 2))
    } else {
        Click-Txt2ImgClient 400 145
    }
    Start-Sleep -Milliseconds 200
    Set-Clipboard -Value $Text
    [System.Windows.Forms.SendKeys]::SendWait("^a")
    Start-Sleep -Milliseconds 120
    [System.Windows.Forms.SendKeys]::SendWait("^v")
    Start-Sleep -Milliseconds 300
}

function Press-Txt2ImgEnter {
    Focus-Txt2Img | Out-Null
    [UiDrive]::TapKey([UiDrive]::VK_RETURN)
}
