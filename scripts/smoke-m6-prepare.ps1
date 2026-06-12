# M6スモークテスト準備: テスト用DNG+JPEGを生成する(開発用)
$dll = "D:\projects\Lightroom Clone\repo\tests\OpenRetouch.Imaging.Tests\bin\Debug\net8.0-windows10.0.19041.0\OpenRetouch.Imaging.Tests.dll"
$asm = [System.Reflection.Assembly]::LoadFrom($dll)
$factory = $asm.GetType("OpenRetouch.Imaging.Tests.TestDngFactory")
$smokeDir = Join-Path $env:TEMP "lps-smoke-m6"
New-Item -ItemType Directory -Force $smokeDir | Out-Null

[string]$dng1 = Join-Path $smokeDir "sample1.dng"
[string]$dng2 = Join-Path $smokeDir "sample2.dng"
$factory.GetMethod("Create").Invoke($null, [object[]]@($dng1, [int]800, [int]600))
$factory.GetMethod("Create").Invoke($null, [object[]]@($dng2, [int]600, [int]800))

Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap(640, 480)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::DarkOliveGreen)
$g.Dispose()
$bmp.Save((Join-Path $smokeDir "normal.jpg"), [System.Drawing.Imaging.ImageFormat]::Jpeg)
$bmp.Dispose()

Get-ChildItem $smokeDir | Select-Object Name, Length
