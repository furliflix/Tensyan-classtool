# 打包 Tensyan XP 特别版（net35 / x86）
# 用法：powershell -ExecutionPolicy Bypass -File build-installer-xp.ps1 [-KeepTemp] [-SignThumbprint <证书指纹>]
#
# 产物：dist\TensyanSetup-XP.exe（内嵌 32 位 XP 版程序 + 卸载器，无需联网、无需解压）
# 目标机要求：Windows XP SP3 + .NET Framework 3.5（老机器一般已装）

param([switch]$KeepTemp, [string]$SignThumbprint = '')

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot                       # 本脚本位于 "Tensyan x86" 目录
$dist = Join-Path $root 'dist'
$temp = Join-Path $root '_buildxp'
$payload = Join-Path $temp 'payload'

# 用 -File 从别处调用时工作目录不一定在这里；统一切到脚本目录（也避开路径里的空格）
Push-Location $root

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

Write-Host '=== 1/4 编译 XP 版主程序（net35 / x86）===' -ForegroundColor Cyan
& dotnet build 'TensyanXP.csproj' -c Release --nologo -v q -m:1 -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'XP 版主程序编译失败' }
$appExe = Join-Path $root 'bin\Release\net35\TensyanXP.exe'
if (-not (Test-Path $appExe)) { throw "找不到主程序：$appExe" }

Write-Host '=== 2/4 准备负载并签名（安装后程序名统一为 Tensyan.exe）===' -ForegroundColor Cyan
Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $payload | Out-Null
Copy-Item $appExe (Join-Path $payload 'Tensyan.exe') -Force
$cfg = Join-Path $root 'bin\Release\net35\TensyanXP.exe.config'
if (Test-Path $cfg) { Copy-Item $cfg (Join-Path $payload 'Tensyan.exe.config') -Force }
Copy-Item (Join-Path $root '..\Tensyan\README.md') $payload -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $root '..\Tensyan\dist\Tensyan\使用说明.txt') $payload -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $root '..\Tensyan\dist\安装说明.txt') $payload -Force -ErrorAction SilentlyContinue
Sign-Artifacts @((Join-Path $payload 'Tensyan.exe'))
Get-ChildItem $payload | ForEach-Object { Write-Host ('    ' + $_.Name + '  ' + [math]::Round($_.Length/1KB,1) + ' KB') }

Write-Host '=== 3/4 编译并签名 XP 版卸载器（net35 / x86）===' -ForegroundColor Cyan
$uninsOut = Join-Path $temp 'uninstaller'
& dotnet build 'TensyanUninstallXP.csproj' -c Release -o $uninsOut --nologo -v q -m:1 -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'XP 版卸载器编译失败' }
$uninsExe = Join-Path $uninsOut 'TensyanUninstallXP.exe'
if (-not (Test-Path $uninsExe)) { throw "找不到卸载器：$uninsExe" }
Sign-Artifacts @($uninsExe)

Write-Host '=== 4/4 编译安装程序（内嵌负载，net35）===' -ForegroundColor Cyan
$setupOut = Join-Path $temp 'setup'
& dotnet build 'TensyanSetupXP.csproj' -c Release -o $setupOut --nologo -v q -m:1 -p:UseSharedCompilation=false `
    -p:XpPayloadDir="$payload" -p:XpUninstaller="$uninsExe"
if ($LASTEXITCODE -ne 0) { throw 'XP 版安装程序编译失败' }

New-Item -ItemType Directory -Force -Path $dist | Out-Null
$target = Join-Path $dist 'TensyanSetup-XP.exe'
Copy-Item (Join-Path $setupOut 'TensyanSetupXP.exe') $target -Force
Sign-Artifacts @($target)

Write-Host '--- 校验位数 ---' -ForegroundColor Cyan
foreach ($f in @($target, (Join-Path $payload 'Tensyan.exe'), $uninsExe)) {
    $bytes = [System.IO.File]::ReadAllBytes($f)
    $pe = [BitConverter]::ToInt32($bytes, 0x3C)
    $machine = [BitConverter]::ToUInt16($bytes, $pe + 4)
    $arch = if ($machine -eq 0x14C) { 'I386（32 位）✓' } elseif ($machine -eq 0x8664) { 'AMD64 ✗' } else { ('0x{0:X4}' -f $machine) }
    Write-Host ('    {0,-26} {1}   {2} KB' -f (Split-Path $f -Leaf), $arch, [math]::Round((Get-Item $f).Length/1KB,1))
    if ($machine -ne 0x14C) { throw ('产物不是 32 位：' + $f) }
}

Write-Host ''
Write-Host ('完成：' + $target) -ForegroundColor Green
if (-not $KeepTemp) { Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue }
