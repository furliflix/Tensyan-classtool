# 构建 Tensyan 32 位版（net48 / x86）
# 用法：powershell -ExecutionPolicy Bypass -File build-x86.ps1 [-Portable]
param(
    [switch]$Portable,     # 用 ILMerge 合并成单文件（需要本机有 ILMerge；没有就跳过）
    [switch]$RunSelfTest
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$proj = Join-Path $root 'TensyanX86.csproj'
$out = Join-Path $root 'bin\Release\net48'
$dist = Join-Path $root 'dist'

Write-Host '=== 1/3 编译（net48 / x86）===' -ForegroundColor Cyan
& dotnet build $proj -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { throw '编译失败' }

Write-Host '=== 2/3 校验产物位数 ===' -ForegroundColor Cyan
$exe = Join-Path $out 'TensyanX86.exe'
if (-not (Test-Path $exe)) { throw "找不到产物：$exe" }
$bytes = [System.IO.File]::ReadAllBytes($exe)
$peOffset = [BitConverter]::ToInt32($bytes, 0x3C)
$machine = [BitConverter]::ToUInt16($bytes, $peOffset + 4)
$arch = switch ($machine) { 0x14C { 'I386（32 位）' } 0x8664 { 'AMD64（64 位）' } default { ('0x{0:X4}' -f $machine) } }
Write-Host ("    Machine = 0x{0:X4}  {1}" -f $machine, $arch)
if ($machine -ne 0x14C) { throw '产物不是 32 位，请检查 PlatformTarget' }

Write-Host '=== 3/3 输出到 dist ===' -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $dist | Out-Null
Copy-Item $exe $dist -Force
$cfg = Join-Path $out 'TensyanX86.exe.config'
if (Test-Path $cfg) { Copy-Item $cfg $dist -Force }
$kb = [math]::Round((Get-Item $exe).Length / 1KB, 1)
Write-Host ("    完成：{0}（{1} KB）" -f (Join-Path $dist 'TensyanX86.exe'), $kb) -ForegroundColor Green

if ($Portable) {
    Write-Host '=== 可选：合并为单文件（ILMerge）===' -ForegroundColor Cyan
    $ilmerge = Get-Command ILMerge.exe -ErrorAction SilentlyContinue
    if ($ilmerge) {
        & $ilmerge.Source /out:"$dist\TensyanX86.single.exe" "$dist\TensyanX86.exe" /targetplatform:v4
        Write-Host '    已生成 TensyanX86.single.exe' -ForegroundColor Green
    } else {
        Write-Host '    未找到 ILMerge，跳过（本项目只有 exe + .config，不合并也够干净）' -ForegroundColor Yellow
    }
}

if ($RunSelfTest) {
    Write-Host '=== 自检 ===' -ForegroundColor Cyan
    $tmp = Join-Path $env:TEMP ('tensyan-x86-selftest-' + [guid]::NewGuid().ToString('N').Substring(0, 6))
    # 注意：WinExe 用 & 调用时 PowerShell 不等待，必须显式等待；路径也要自己加引号
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $exe
    $psi.Arguments = '--selftest "' + $tmp + '"'
    $psi.UseShellExecute = $false
    [System.Diagnostics.Process]::Start($psi).WaitForExit()
    $report = Join-Path $tmp 'selftest.txt'
    if (Test-Path $report) { Get-Content $report -Encoding UTF8 | Select-Object -Last 3 }
    else { Write-Host '    未找到自检报告' -ForegroundColor Yellow }
}
