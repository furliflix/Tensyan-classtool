# 打包 Tensyan 32 位版（.NET Framework 4.8）安装程序
# 用法：powershell -ExecutionPolicy Bypass -File build-installer-x86.ps1 [-SignThumbprint <证书指纹>]
#
# 与 build-installer.ps1 的区别：
#   · 负载是 net48 / x86 主程序（约 333 KB），安装包只有 ~440 KB（x64 版是 58 MB 自包含）
#   · 安装程序/卸载器本身也编成 x86，保证注册表视图（WOW6432Node）与程序一致
#   · 目标机需要 .NET Framework 4.8（Win10 1903+/Win11 自带；Win7 SP1 / 8.1 需自行安装）

param([switch]$KeepTemp, [string]$SignThumbprint = '')

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$x86Root = Join-Path (Split-Path $root -Parent) 'Tensyan x86'   # 32 位工程与主工程同级
$dist = Join-Path $root 'dist'
$temp = Join-Path $root '_buildx86'
$payloadSrc = Join-Path $temp 'payload'
$projRoot = Split-Path $PSScriptRoot -Parent                # 相对路径
$appProj = Join-Path $projRoot 'TensyanWpfX86\TensyanWpfX86.csproj'
$appOut = Join-Path $projRoot 'TensyanWpfX86\bin\Release\net48'
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

Write-Host '=== 1/5 编译 32 位主程序（net48 / x86）===' -ForegroundColor Cyan
# PowerShell 5.1 传原生参数时不加引号，"Tensyan x86" 里的空格会被拆开 → 先进目录用相对名构建
Push-Location (Split-Path $appProj)
& dotnet build (Split-Path $appProj -Leaf) -c Release --nologo -v q
$appExit = $LASTEXITCODE
Pop-Location
if ($appExit -ne 0) { throw '32 位主程序编译失败' }
$appExe = Join-Path $appOut 'TensyanWpfX86.exe'
if (-not (Test-Path $appExe)) { throw "找不到 32 位主程序：$appExe" }

Write-Host '=== 2/5 准备负载（安装后程序名统一为 Tensyan.exe）===' -ForegroundColor Cyan
Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $payloadSrc | Out-Null
Copy-Item $appExe (Join-Path $payloadSrc 'Tensyan.exe') -Force
$cfg = Join-Path $appOut 'TensyanWpfX86.exe.config'
if (Test-Path $cfg) { Copy-Item $cfg (Join-Path $payloadSrc 'Tensyan.exe.config') -Force }
Copy-Item (Join-Path $root 'README.md') $payloadSrc -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $root 'dist\Tensyan\使用说明.txt') $payloadSrc -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $root 'dist\安装说明.txt') $payloadSrc -Force -ErrorAction SilentlyContinue
Sign-Artifacts @((Join-Path $payloadSrc 'Tensyan.exe'))

Write-Host '=== 3/5 打包负载 ===' -ForegroundColor Cyan
$zip = Join-Path $temp 'payload-x86.zip'
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($payloadSrc, $zip, [System.IO.Compression.CompressionLevel]::Optimal, $false)
Write-Host ('    负载 ' + [math]::Round((Get-Item $zip).Length/1KB,1) + ' KB')

Write-Host '=== 4/5 编译并签名卸载器（x86）===' -ForegroundColor Cyan
$uninsOut = Join-Path $temp 'uninstaller'
& dotnet build $uninsProj -c Release -o $uninsOut --nologo -v q -p:PlatformTarget=x86 -p:Prefer32Bit=true
if ($LASTEXITCODE -ne 0) { throw '卸载器编译失败' }
$uninsExe = Join-Path $uninsOut 'TensyanUninstall.exe'
Sign-Artifacts @($uninsExe)

Write-Host '=== 5/5 编译安装程序（x86，两种权限清单）===' -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$commonArgs = @('-c','Release','--nologo','-v','q','-p:PlatformTarget=x86','-p:Prefer32Bit=true','-p:PayloadNetFx=true',
                ('-p:PayloadZip=' + $zip), ('-p:UninstallerSource=' + $uninsExe))

$setupOut = Join-Path $temp 'setup-admin'
& dotnet build $setupProj @commonArgs -o $setupOut -p:RequireAdmin=true
if ($LASTEXITCODE -ne 0) { throw '安装程序（管理员版）编译失败' }
$setupOutUser = Join-Path $temp 'setup-user'
& dotnet build $setupProj @commonArgs -o $setupOutUser
if ($LASTEXITCODE -ne 0) { throw '安装程序（标准用户版）编译失败' }

$adminTarget = Join-Path $dist 'TensyanSetup-x86.exe'
$userTarget = Join-Path $dist 'TensyanSetup-x86-标准用户版.exe'
Copy-Item (Join-Path $setupOut 'TensyanSetup.exe') $adminTarget -Force
Copy-Item (Join-Path $setupOutUser 'TensyanSetup.exe') $userTarget -Force
Sign-Artifacts @($adminTarget, $userTarget)

Write-Host '--- 校验位数 ---' -ForegroundColor Cyan
foreach ($f in @($adminTarget, $userTarget, (Join-Path $payloadSrc 'Tensyan.exe'))) {
    $b = [System.IO.File]::ReadAllBytes($f)
    $pe = [BitConverter]::ToInt32($b, 0x3C)
    $m = [BitConverter]::ToUInt16($b, $pe + 4)
    $arch = if ($m -eq 0x14C) { 'I386（32 位）✓' } elseif ($m -eq 0x8664) { 'AMD64（64 位）✗' } else { ('0x{0:X4}' -f $m) }
    Write-Host ('    {0,-34} {1}   {2} KB' -f (Split-Path $f -Leaf), $arch, [math]::Round((Get-Item $f).Length/1KB,1))
    if ($m -ne 0x14C) { throw ('产物不是 32 位：' + $f) }
}

Write-Host ''
Write-Host '完成：' -ForegroundColor Green
Write-Host '    dist\TensyanSetup-x86.exe            = 需要管理员（默认 Program Files (x86)，卸载信息写 HKLM）'
Write-Host '    dist\TensyanSetup-x86-标准用户版.exe  = 不需要管理员（装到当前用户目录）'

if (-not $KeepTemp) { Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue }
