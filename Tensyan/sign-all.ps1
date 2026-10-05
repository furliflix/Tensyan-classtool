# 一键给 Tensyan 的三个版本签名（程序本体 + 卸载器 + 安装程序）
#
# 用法：
#   1) 用自签名证书（内网/自用，需把 cer 装进目标机的“受信任的根”才能显示发布者）：
#        powershell -ExecutionPolicy Bypass -File sign-all.ps1 -SelfSigned
#   2) 用已有证书（推荐：正式购买/单位签发的代码签名证书）：
#        powershell -ExecutionPolicy Bypass -File sign-all.ps1 -Pfx .\mycert.pfx -Password 证书口令
#      或按指纹用证书库里的证书：
#        powershell -ExecutionPolicy Bypass -File sign-all.ps1 -Thumbprint <指纹>
#   3) 只签名、不重新构建（对已存在的产物补签）：
#        powershell -ExecutionPolicy Bypass -File sign-all.ps1 -Thumbprint <指纹> -SkipBuild
#
# 说明：签名必须“先签程序本体，再打进安装包，最后签安装包”，所以默认会重新构建一遍。

param(
    [switch]$SelfSigned,
    [string]$Pfx = '',
    [string]$Password = '',
    [string]$Thumbprint = '',
    [switch]$SkipBuild,
    [string]$TimestampServer = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$x86Root = Join-Path (Split-Path $root -Parent) 'Tensyan x86'
$certDir = Join-Path (Split-Path $root -Parent) 'Tensyan-codesign'

# ---------------- 1) 准备证书 ----------------
$cert = $null
if ($Pfx) {
    if (-not (Test-Path $Pfx)) { throw ('找不到 pfx：' + $Pfx) }
    $sec = ConvertTo-SecureString -String $Password -Force -AsPlainText
    $cert = Import-PfxCertificate -FilePath $Pfx -CertStoreLocation 'Cert:\CurrentUser\My' -Password $sec
    Write-Host ('已导入证书：' + $cert.Subject + '（' + $cert.Thumbprint + '）') -ForegroundColor Green
} elseif ($Thumbprint) {
    $cert = Get-ChildItem 'Cert:\CurrentUser\My' | Where-Object { $_.Thumbprint -eq $Thumbprint } | Select-Object -First 1
    if (-not $cert) { throw ('证书库里找不到指纹 ' + $Thumbprint) }
} elseif ($SelfSigned) {
    $cert = Get-ChildItem 'Cert:\CurrentUser\My' -CodeSigningCert -ErrorAction SilentlyContinue |
            Where-Object { $_.Subject -like 'CN=Clarusvita Studio*' } | Select-Object -First 1
    if (-not $cert) {
        Write-Host '证书库里没有，正在创建自签名代码签名证书…' -ForegroundColor Cyan
        $cert = New-SelfSignedCertificate -Subject 'CN=Clarusvita Studio, O=Clarusvita Studio, C=CN' `
            -Type CodeSigningCert -KeyUsage DigitalSignature -KeyAlgorithm RSA -KeyLength 3072 `
            -FriendlyName 'Tensyan 代码签名（自签名）' -CertStoreLocation 'Cert:\CurrentUser\My' `
            -NotAfter (Get-Date).AddYears(5)
    }
    New-Item -ItemType Directory -Force -Path $certDir | Out-Null
    $sec = ConvertTo-SecureString -String 'TensyanSign2026' -Force -AsPlainText
    Export-PfxCertificate -Cert $cert -FilePath (Join-Path $certDir 'Tensyan-CodeSign.pfx') -Password $sec | Out-Null
    Export-Certificate -Cert $cert -FilePath (Join-Path $certDir 'Tensyan-CodeSign.cer') -Type CERT | Out-Null
} else {
    throw '请指定 -SelfSigned，或 -Pfx/-Password，或 -Thumbprint'
}

Write-Host ('签名证书：' + $cert.Subject) -ForegroundColor Green
Write-Host ('指纹    ：' + $cert.Thumbprint)
Write-Host ('有效期  ：' + $cert.NotAfter)
Write-Host ''

# ---------------- 2) 重新构建（以便把已签名的程序本体打进安装包）----------------
if (-not $SkipBuild) {
    Write-Host '=== 64 位版 ===' -ForegroundColor Cyan
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'build-installer.ps1') -SignThumbprint $cert.Thumbprint
    Write-Host '=== 32 位版 ===' -ForegroundColor Cyan
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'build-installer-x86.ps1') -SignThumbprint $cert.Thumbprint
    Write-Host '=== XP 特别版 ===' -ForegroundColor Cyan
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $x86Root 'build-installer-xp.ps1') -SignThumbprint $cert.Thumbprint
}

# ---------------- 3) 给绿色版程序本体也补上签名 ----------------
Write-Host '=== 绿色版程序本体 ===' -ForegroundColor Cyan
$loose = @(
    (Join-Path $root 'dist\Tensyan\Tensyan.exe'),
    (Join-Path $root 'dist\Tensyan\TensyanUninstall.exe'),
    (Join-Path $x86Root 'dist\TensyanX86.exe'),
    (Join-Path $x86Root 'dist\TensyanSetup-XP.exe'),
    (Join-Path $x86Root 'dist\TensyanXP.exe')
)
foreach ($f in $loose) {
    if (-not (Test-Path $f)) { continue }
    $r = $null
    try { $r = Set-AuthenticodeSignature -FilePath $f -Certificate $cert -TimestampServer $TimestampServer -ErrorAction Stop } catch { }
    if (-not $r -or $r.Status -ne 'Valid') { try { $r = Set-AuthenticodeSignature -FilePath $f -Certificate $cert -ErrorAction Stop } catch { } }
    Write-Host ('    ' + (Split-Path $f -Leaf) + ' -> ' + $(if ($r) { $r.Status } else { '失败' })) -ForegroundColor $(if ($r -and $r.Status -eq 'Valid') { 'Green' } else { 'Yellow' })
}

# ---------------- 4) 汇总校验 ----------------
Write-Host ''
Write-Host '=== 校验结果 ===' -ForegroundColor Cyan
$targets = @(
    (Join-Path $root 'dist\TensyanSetup.exe'),
    (Join-Path $root 'dist\TensyanSetup-标准用户版.exe'),
    (Join-Path $root 'dist\TensyanSetup-x86.exe'),
    (Join-Path $root 'dist\TensyanSetup-x86-标准用户版.exe'),
    (Join-Path $x86Root 'dist\TensyanSetup-XP.exe'),
    (Join-Path $root 'dist\Tensyan\Tensyan.exe'),
    (Join-Path $x86Root 'dist\TensyanX86.exe'),
    (Join-Path $x86Root 'dist\TensyanXP.exe')
)
foreach ($t in $targets) {
    if (-not (Test-Path $t)) { continue }
    $s = Get-AuthenticodeSignature $t
    Write-Host ('    {0,-40} {1,-12} {2}' -f $t.Substring($t.LastIndexOf('\') + 1), $s.Status, $(if ($s.SignerCertificate) { $s.SignerCertificate.Subject } else { '' }))
}
Write-Host ''
Write-Host '提示：自签名证书只在“已把 Tensyan-CodeSign.cer 装进受信任的根”的机器上显示为有效；' -ForegroundColor Yellow
Write-Host '      要让别人的电脑不报“未知发布者/毒”，需要使用 CA 签发的代码签名证书（OV/EV）。' -ForegroundColor Yellow
