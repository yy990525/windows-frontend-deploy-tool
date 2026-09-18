param(
    [string]$Version = '1.1.1'
)

$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot
$sourceRoot = Join-Path $repoRoot 'src'
$outputRoot = Join-Path $repoRoot 'dist'
$packageRoot = Join-Path $outputRoot 'Windows前端项目部署工具EXE'
$exePath = Join-Path $packageRoot '前端项目部署工具.exe'
$zipPath = Join-Path $outputRoot ("Windows前端项目部署工具EXE-v{0}.zip" -f $Version)
$iconPath = Join-Path $repoRoot 'assets\app-icon.ico'

$compilerCandidates = @(
    'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe',
    'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'
)
$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) {
    throw '找不到 .NET Framework C# 编译器 csc.exe。'
}

New-Item -ItemType Directory -Force -Path $packageRoot | Out-Null

$sources = @(
    'Program.cs',
    'Models.cs',
    'DeploymentService.cs',
    'ProjectManagerForm.cs',
    'MainForm.cs',
    'NginxFormatter.cs',
    'NginxController.cs',
    'NginxManagerForm.cs',
    'TextFileCodec.cs',
    'AssemblyInfo.cs'
) | ForEach-Object { Join-Path $sourceRoot $_ }

$arguments = @(
    '/nologo',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    '/codepage:65001',
    ('/win32icon:{0}' -f $iconPath),
    ('/win32manifest:{0}' -f (Join-Path $sourceRoot 'app.manifest')),
    ('/out:{0}' -f $exePath),
    '/reference:System.dll',
    '/reference:System.Core.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll',
    '/reference:System.Web.Extensions.dll',
    '/reference:System.IO.Compression.dll',
    '/reference:System.IO.Compression.FileSystem.dll'
) + $sources

& $compiler @arguments
if ($LASTEXITCODE -ne 0) {
    throw "编译失败，退出码：$LASTEXITCODE"
}

Copy-Item -LiteralPath (Join-Path $repoRoot 'package\projects.json') -Destination $packageRoot -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'package\settings.json') -Destination $packageRoot -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'package\使用说明.txt') -Destination $packageRoot -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'assets\app-icon.png') -Destination (Join-Path $packageRoot '程序图标.png') -Force
Copy-Item -LiteralPath $iconPath -Destination (Join-Path $packageRoot '程序图标.ico') -Force

foreach ($folder in @('logs', 'backups', 'nginx-config-backups')) {
    New-Item -ItemType Directory -Force -Path (Join-Path $packageRoot $folder) | Out-Null
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
Compress-Archive -LiteralPath $packageRoot -DestinationPath $zipPath -CompressionLevel Optimal

$exeHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $exePath).Hash
$zipHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath).Hash
Write-Host "EXE: $exePath"
Write-Host "EXE SHA-256: $exeHash"
Write-Host "ZIP: $zipPath"
Write-Host "ZIP SHA-256: $zipHash"
