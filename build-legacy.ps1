$ErrorActionPreference = 'Stop'

$source = Join-Path $PSScriptRoot 'DesktopPet.cs'
$outputDir = Join-Path $PSScriptRoot 'bin'
$output = Join-Path $outputDir 'DesktopPet.exe'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$referenceRoot = 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2'

if (-not (Test-Path -LiteralPath $compiler)) {
    throw "找不到 C# 编译器：$compiler"
}

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

$references = @(
    (Join-Path $referenceRoot 'WindowsBase.dll'),
    (Join-Path $referenceRoot 'PresentationCore.dll'),
    (Join-Path $referenceRoot 'PresentationFramework.dll'),
    (Join-Path $referenceRoot 'System.Xaml.dll')
)

$arguments = @('/nologo', '/target:winexe', "/out:$output")
foreach ($reference in $references) {
    $arguments += "/reference:$reference"
}
$arguments += $source

& $compiler @arguments
if ($LASTEXITCODE -ne 0) {
    throw "编译失败，退出码：$LASTEXITCODE"
}

$assetSource = Join-Path $PSScriptRoot 'assets'
$assetOutput = Join-Path $outputDir 'assets'
if (Test-Path -LiteralPath $assetSource) {
    Copy-Item -LiteralPath $assetSource -Destination $outputDir -Recurse -Force
}

Write-Host "已生成：$output"
