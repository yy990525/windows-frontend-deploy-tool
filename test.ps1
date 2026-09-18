$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot
$sourceRoot = Join-Path $repoRoot 'src'
$testRoot = Join-Path $repoRoot 'tests'
$binRoot = Join-Path $testRoot 'bin'

$compilerCandidates = @(
    'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe',
    'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'
)
$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) {
    throw '找不到 .NET Framework C# 编译器 csc.exe。'
}

New-Item -ItemType Directory -Force -Path $binRoot | Out-Null
$fakeNginx = Join-Path $binRoot 'nginx.exe'
$selfTest = Join-Path $binRoot 'DeploymentSelfTest.exe'
$uiTest = Join-Path $binRoot 'UiSmokeTest.exe'

& $compiler /nologo /target:exe /optimize+ /codepage:65001 "/out:$fakeNginx" (Join-Path $testRoot 'FakeNginx.cs')
if ($LASTEXITCODE -ne 0) { throw 'FakeNginx 编译失败。' }

& $compiler /nologo /target:exe /platform:anycpu /optimize+ /codepage:65001 "/out:$selfTest" `
    /reference:System.dll /reference:System.Core.dll /reference:System.Web.Extensions.dll `
    /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll `
    (Join-Path $sourceRoot 'Models.cs') `
    (Join-Path $sourceRoot 'DeploymentService.cs') `
    (Join-Path $sourceRoot 'NginxFormatter.cs') `
    (Join-Path $sourceRoot 'NginxController.cs') `
    (Join-Path $sourceRoot 'TextFileCodec.cs') `
    (Join-Path $testRoot 'SelfTest.cs')
if ($LASTEXITCODE -ne 0) { throw '部署自测程序编译失败。' }
& $selfTest $fakeNginx
if ($LASTEXITCODE -ne 0) { throw '部署自测失败。' }

& $compiler /nologo /target:exe /platform:anycpu /optimize+ /codepage:65001 "/out:$uiTest" `
    /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll `
    /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll `
    (Join-Path $sourceRoot 'Models.cs') `
    (Join-Path $sourceRoot 'DeploymentService.cs') `
    (Join-Path $sourceRoot 'ProjectManagerForm.cs') `
    (Join-Path $sourceRoot 'MainForm.cs') `
    (Join-Path $sourceRoot 'NginxFormatter.cs') `
    (Join-Path $sourceRoot 'NginxController.cs') `
    (Join-Path $sourceRoot 'NginxManagerForm.cs') `
    (Join-Path $sourceRoot 'TextFileCodec.cs') `
    (Join-Path $testRoot 'UiSmokeTest.cs')
if ($LASTEXITCODE -ne 0) { throw 'UI 冒烟测试程序编译失败。' }
& $uiTest
if ($LASTEXITCODE -ne 0) { throw 'UI 冒烟测试失败。' }

Write-Host 'ALL_TESTS_OK'
