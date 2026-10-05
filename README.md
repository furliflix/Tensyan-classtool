# Tensyan 班级积分工具（Class Points Tool）

给中小学老师用的班级积分管理工具：点几下就给学生加分扣分，自动统计排行、导出 Excel，
**数据只存在本机**（不联网、不上传），可整个文件夹拷走备份。

> 本仓库只开源 **桌面端**。手机端（Android）尚不稳定，暂不公开。

## 三个版本

| 版本 | 目标框架 | 界面 | 适用 |
| --- | --- | --- | --- |
| **64 位版** | .NET 8 (WPF) | WPF | Win10 / Win11（首选，支持 300% 等高 DPI） |
| **32 位版** | .NET Framework 4.8 (WPF) | WPF | 32 位 Windows / Win7 SP1 以上 |
| **XP 特别版** | .NET Framework 3.5 (WinForms) | WinForms | Windows XP SP3 |

三者**共用同一份业务核心**（`Tensyan/Core`）与同一份数据文件 `data/classdata.json`，可互相拷贝。

## 目录结构

```
Tensyan/          业务核心 Core + WinForms 界面 + 安装程序源码 + 构建/签名脚本
Tensyan x86/      XP 特别版工程（net35 / WinForms）
TensyanWpf/       64 位 WPF 工程（net8.0-windows）
TensyanWpfX86/    32 位 WPF 工程（net48，与 64 位共用同一份 XAML/C#）
```

WPF 版的两个工程通过**链接同一份源码**共享界面（不是复制），所以改一处、两版同时生效。

## 主要能力

- **加减分**：卡片式点名，一键 +1/+2/-1，可填理由；右键菜单支持详情、编辑、归档、删除
- **撤销**：任何一步操作都能撤销，分数按事件重算（不是简单加减数字）
- **统计**：积分排行、积分分布图、理由排行、数据统计
- **导出**：Excel（.xlsx，自写 OOXML，无第三方依赖）与 CSV
- **账号权限**：管理员 / 成员两级，成员默认只能加分
- **登录 U 盘**：把账号密钥写入 U 盘，插上即可免密码登录（可选 PIN 与有效期）
- **配对手机**：局域网 6 位码配对，配对后手机自动登录；电脑关窗口收进**托盘**继续等手机
- **托盘常驻**：关闭窗口不退出，后台继续提供局域网配对/同步

## 构建

需要 .NET SDK（构建 64 位 WPF 版与 32 位 WPF 版）与 .NET Framework 3.5 开发包（构建 XP 版）。

```powershell
# 64 位（WPF）
dotnet publish TensyanWpf/TensyanWpf.csproj -c Release -r win-x64 --self-contained true

# 32 位（WPF，需 .NET Framework 4.8）
dotnet build TensyanWpfX86/TensyanWpfX86.csproj -c Release

# XP 版（WinForms，需 .NET Framework 3.5）
dotnet build "Tensyan x86/TensyanXP.csproj" -c Release
```

安装包脚本在各自目录下：`build-installer.ps1`（64 位）、`build-installer-x86.ps1`（32 位）、
`Tensyan x86/build-installer-xp.ps1`（XP），签名用 `sign-all.ps1`。

## 数据与隐私

- 所有数据保存在程序目录下的 `data/`（绿色版）或用户目录（安装版），**不上传任何服务器**
- `classdata.json` 为明文 JSON（PascalCase、2 空格缩进），方便老师自行备份与迁移
- 密码使用 PBKDF2 加盐哈希存储；配对使用一次性 6 位码换取长期令牌
- 局域网同步为**幂等合并**：事件按 Id 取并集、撤销按"或"合并、分数由事件重算，永不冲突

## 界面图标

使用 Microsoft 官方 **Fluent System Icons**（`FluentSystemIcons-Regular.ttf`，MIT 许可）。

## 许可

MIT License，见 `LICENSE`。

---

公司：Clarusvita Studio　作者：furliflix