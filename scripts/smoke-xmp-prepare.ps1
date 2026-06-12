# XMPスモークテスト準備: DNG2枚(片方にLightroom風XMP)を生成する(開発用)
$dll = "D:\projects\Lightroom Clone\repo\tests\OpenRetouch.Imaging.Tests\bin\Debug\net8.0-windows10.0.19041.0\OpenRetouch.Imaging.Tests.dll"
$asm = [System.Reflection.Assembly]::LoadFrom($dll)
$factory = $asm.GetType("OpenRetouch.Imaging.Tests.TestDngFactory")
$smokeDir = Join-Path $env:TEMP "lps-smoke-xmp"
New-Item -ItemType Directory -Force $smokeDir | Out-Null

[string]$dng1 = Join-Path $smokeDir "with-xmp.dng"
[string]$dng2 = Join-Path $smokeDir "no-xmp.dng"
$factory.GetMethod("Create").Invoke($null, [object[]]@($dng1, [int]800, [int]600))
$factory.GetMethod("Create").Invoke($null, [object[]]@($dng2, [int]800, [int]600))

# 露出+2.5、50%クロップ、評価5のLightroom風XMP
$xmp = @'
<x:xmpmeta xmlns:x="adobe:ns:meta/">
 <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
  <rdf:Description rdf:about=""
    xmlns:xmp="http://ns.adobe.com/xap/1.0/"
    xmlns:crs="http://ns.adobe.com/camera-raw-settings/1.0/"
    xmp:Rating="5"
    xmp:Label="Red"
    crs:Exposure2012="+2.50"
    crs:HasCrop="True"
    crs:CropTop="0.25"
    crs:CropLeft="0.25"
    crs:CropBottom="0.75"
    crs:CropRight="0.75"/>
 </rdf:RDF>
</x:xmpmeta>
'@
Set-Content -Path (Join-Path $smokeDir "with-xmp.xmp") -Value $xmp -Encoding UTF8

Get-ChildItem $smokeDir | Select-Object Name, Length
