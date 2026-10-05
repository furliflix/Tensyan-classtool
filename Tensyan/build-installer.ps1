# 生成 Tensyan 64 位版安装程序（离线、自带运行库）
# 用法：powershell -ExecutionPolicy Bypass -File build-installer.ps1 [-FrameworkDependent] [-SignThumbprint <证书指纹>]
param(
    [switch]$KeepTemp,           # 保留中间产物
    [switch]$FrameworkDependent, # 精简版：负载不含运行库（目标机需已装 .NET 8 桌面运行时，约 1MB）
    [string]$SignThumbprint = '' # 代码签名证书指纹（留空则不签名）
)

$wpfProj = Join-Path (Split-Path $PSScriptRoot -Parent) 'TensyanWpf\TensyanWpf.csproj'   # 相对路径：整个工程可随意搬迁
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dist = Join-Path $root 'dist'
$temp = Join-Path $root '_build'
$payloadSrc = Join-Path $temp 'payload'
$setupProj = Join-Path $root 'Installer\TensyanSetup\TensyanSetup.csproj'
$uninsProj = Join-Path $root 'Installer\TensyanUninstall\TensyanUninstall.csproj'

# ---------------- 代码签名（顺序：先签程序本体 → 打进安装包 → 最后签安装包）----------------
function Sign-Artifacts([string[]]$paths) {
    if (-not $SignThumbprint) { return }
    $cert = Get-ChildItem 'Cert:\CurrentUser\My' -ErrorAction SilentlyContinue | Where-Object { $_.Thumbprint -eq $SignThumbprint } | Select-Object -First 1
    if (-not $cert) { Write-Host ('    找不到证书 ' + $SignThumbprint + '，跳过签名') -ForegroundColor Yellow; return }
    foreach ($p in $paths) {
        if (-not (Test-Path $p)) { continue }
        $r = $null
        try { $r = Set-AuthenticodeSignature -FilePath $p -Certificate $cert -TimestampServer 'http://timestamp.digicert.com' -ErrorAction Stop } catch { }
        if (-not $r -or $r.Status -ne 'Valid') { try { $r = Set-AuthenticodeSignature -FilePath $p -Certificate $cert -ErrorAction Stop } catch { } }
        $st = if ($r) { $r.Status } else { '失败' }
        Write-Host ('    签名 ' + (Split-Path $p -Leaf) + ' -> ' + $st) -ForegroundColor $(if ($st -eq 'Valid') { 'Green' } else { 'Yellow' })
    }
}

Write-Host '=== 1/5 发布主程序 ===' -ForegroundColor Cyan
Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $payloadSrc | Out-Null

if ($FrameworkDependent) {
    & dotnet publish $wpfProj -c Release -o $payloadSrc --nologo -v q
} else {
    & dotnet publish $wpfProj -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o $payloadSrc --nologo -v q
}
if ($LASTEXITCODE -ne 0) { throw '主程序发布失败' }

Copy-Item (Join-Path $root 'README.md') $payloadSrc -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $root 'dist\Tensyan\使用说明.txt') $payloadSrc -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $root 'dist\安装说明.txt') $payloadSrc -Force -ErrorAction SilentlyContinue
Get-ChildItem $payloadSrc -Filter '*.pdb' -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue

Write-Host '=== 2/5 签名程序本体并打包负载 ===' -ForegroundColor Cyan
Sign-Artifacts @((Join-Path $payloadSrc 'Tensyan.exe'))
$zip = Join-Path $temp 'payload.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($payloadSrc, $zip, [System.IO.Compression.CompressionLevel]::Optimal, $false)
Write-Host ('    负载 ' + [math]::Round((Get-Item $zip).Length / 1MB, 1) + ' MB')

Write-Host '=== 3/5 编译并签名卸载器 ===' -ForegroundColor Cyan
$uninsOut = Join-Path $temp 'uninstaller'
& dotnet build $uninsProj -c Release -o $uninsOut --nologo -v q
if ($LASTEXITCODE -ne 0) { throw '卸载器编译失败' }
$uninsExe = Join-Path $uninsOut 'TensyanUninstall.exe'
Sign-Artifacts @($uninsExe)

Write-Host '=== 4/5 编译安装程序（内嵌已签名的负载与卸载器）===' -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$setupOut = Join-Path $temp 'setup-admin'
& dotnet build $setupProj -c Release -o $setupOut --nologo -v q -p:PayloadZip="$zip" -p:UninstallerSource="$uninsExe" -p:RequireAdmin=true
if ($LASTEXITCODE -ne 0) { throw '安装程序（管理员版）编译失败' }
$setupOutUser = Join-Path $temp 'setup-user'
& dotnet build $setupProj -c Release -o $setupOutUser --nologo -v q -p:PayloadZip="$zip" -p:UninstallerSource="$uninsExe"
if ($LASTEXITCODE -ne 0) { throw '安装程序（标准用户版）编译失败' }

Write-Host '=== 5/5 输出并签名 ===' -ForegroundColor Cyan
$suffix = if ($FrameworkDependent) { '-lite' } else { '' }
$adminTarget = Join-Path $dist ("TensyanSetup{0}.exe" -f $suffix)
$userTarget = Join-Path $dist ("TensyanSetup-标准用户版{0}.exe" -f $suffix)
Copy-Item (Join-Path $setupOut 'TensyanSetup.exe') $adminTarget -Force
Copy-Item (Join-Path $setupOutUser 'TensyanSetup.exe') $userTarget -Force
Sign-Artifacts @($adminTarget, $userTarget)
foreach ($t in @($adminTarget, $userTarget)) {
    Write-Host ('    完成：{0}（{1} MB）' -f $t, [math]::Round((Get-Item $t).Length / 1MB, 1)) -ForegroundColor Green
}

if (-not $KeepTemp) { Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue }
