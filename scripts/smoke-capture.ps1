# アプリを起動して引数を渡し、ウィンドウをDPI補正付きでキャプチャする(開発用)
param(
    [string[]]$AppArgs = @(),
    [int]$WaitSeconds = 14,
    [string]$OutPng = "$env:TEMP\lps-shot.png"
)

$exe = "D:\projects\Lightroom Clone\repo\src\OpenRetouch.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\OpenRetouch.App.exe"
$p = if ($AppArgs.Count -gt 0) { Start-Process $exe -ArgumentList $AppArgs -PassThru } else { Start-Process $exe -PassThru }
Start-Sleep -Seconds $WaitSeconds

Add-Type @"
using System;
using System.Runtime.InteropServices;
public struct SRECT { public int Left, Top, Right, Bottom; }
public class SWin32 {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out SRECT rect);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hWnd);
}
"@
Add-Type -AssemblyName System.Drawing

$p.Refresh()
$h = $p.MainWindowHandle
$r = New-Object SRECT
[SWin32]::GetWindowRect($h, [ref]$r) | Out-Null
$dpi = [SWin32]::GetDpiForWindow($h)
$scale = $dpi / 96.0
$w = [int](($r.Right - $r.Left) * $scale)
$ht = [int](($r.Bottom - $r.Top) * $scale)

$bmp = New-Object System.Drawing.Bitmap($w, $ht)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
[SWin32]::PrintWindow($h, $hdc, 2) | Out-Null
$g.ReleaseHdc($hdc)
$g.Dispose()

$half = New-Object System.Drawing.Bitmap($bmp, [int]($w / 2), [int]($ht / 2))
$half.Save($OutPng)
$half.Dispose()
$bmp.Dispose()

if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force }
Write-Output "shot: $OutPng (window ${w}x${ht} @ dpi $dpi)"
