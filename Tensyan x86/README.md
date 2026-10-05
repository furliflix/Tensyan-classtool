# Tensyan x86 —— 32 位版与 XP 特别版

> 想先看"为什么这么做"，请读 **[方案讨论.md](方案讨论.md)**（32 位选型）与 **[XP兼容性分析.md](XP兼容性分析.md)**（XP 可行性）。

## 交付物一览

| 文件 | 目标系统 | 说明 |
| --- | --- | --- |
| `..\Tensyan\dist\TensyanSetup-x86.exe` | Win7 SP1 以上（32/64 位） | 440 KB，需管理员，默认 `C:\Program Files (x86)\Tensyan`，需 .NET Framework 4.8 |
| `..\Tensyan\dist\TensyanSetup-x86-标准用户版.exe` | 同上 | 440 KB，免 UAC |
| **`dist\TensyanSetup-XP.exe`** | **Windows XP SP3** | **606 KB，XP 特别版安装程序**，内嵌程序 + 卸载器，需 .NET Framework 3.5 |
| `dist\TensyanX86.exe` | Win7 SP1 以上 | 333 KB 绿色版（net48 / x86） |
| `dist\XP验证用软盘-安装程序.img` | XP 虚拟机验证用 | 1.44 MB 软盘镜像，内含 `TSETUP.EXE`（即 XP 安装程序） |
| `dist\XP验证用软盘-程序本体.img` | XP 虚拟机验证用 | 1.44 MB 软盘镜像，内含 `TENSYAN.EXE`（XP 版程序本体） |

## XP 特别版（net35 / x86）已完成

| 项 | 结果 |
| --- | --- |
| 目标框架 | .NET Framework **3.5**（XP SP3 老机器一般已装；.NET 4.0 机器也能跑） |
| 位数 | PE = I386，**真 32 位** |
| 产物 | 程序 337 KB + 安装程序 606 KB + 卸载器 64 KB |
| 自检 | **27 / 27 通过**（含自写 ZIP、自写 PBKDF2、JSON 垫片、U盘签名） |
| 界面自检 | **8 / 8 通过** |
| 与 x64 版互通 | 密码哈希互通（x64 建的账号能在 XP 版登录）；数据文件双向逐行一致；XP 版导出的 .xlsx 经 openpyxl 验证可正常打开 |
| XP 界面降级 | 无微软雅黑→回退黑体/宋体；无 DWM→直角窗口；占位提示自绘；动画默认关闭（可用 `TENSYAN_FORCE_XP=1` 在本机模拟 XP 模式自检） |
| 安装程序 | 同机实测：安装 exit=0 → 程序启动 → 安装副本自检 27/27 → 卸载保留数据 exit=0 |

### XP 真机验证进度（虚拟机）

本机已有可启动的 XP 虚拟机（`D:\App\VirtualBox\WindowsXP`，VirtualBox 7.2）。已做到：

- 虚拟机**能启动到桌面**（截图存证：XP 5.1.2600，用户 furliflix，德语版）
- 通过 `VBoxManage` 完成了**软盘挂载 + 键盘注入 + 截屏**的自动化通路
- 受阻点：该虚拟机是**德语键盘布局**，VBoxManage 的字符注入按其自身映射发送扫描码，
  导致 `:`→`ö`、`\`→`ß`、空格丢失、Shift/AltGr 时灵时不灵，命令无法可靠输入

**手动验证（推荐，1 分钟）**：在 VirtualBox 里给该虚拟机加一个软驱控制器（设置 → 存储 → 添加软盘控制器），
挂上 `dist\XP验证用软盘-安装程序.img`，启动后在 XP 里运行 `A:\TSETUP.EXE`；
或挂 `XP验证用软盘-程序本体.img` 后把 `TENSYAN.EXE` 拷到 C 盘直接运行。

## 现状（net48 32 位版，已验证）

32 位版**已经编译通过并跑通全部自检**，不是纸面方案：

| 项 | 结果 |
| --- | --- |
| 目标框架 | .NET Framework **4.8**（Win7 SP1 以上 / 32 位与 64 位 Windows 通吃，目标机无需装运行库） |
| 平台 | **x86**（PE Machine = 0x014C，真 32 位进程） |
| 产物 | `dist\TensyanX86.exe`（333 KB）+ `TensyanX86.exe.config` |
| 自检 | **27 / 27 通过**（U盘签名、PBKDF2、Excel 导出、写入权限探测、JSON 读写） |
| 界面自检 | **8 / 8 通过** |
| 与 x64 版数据互通 | **双向逐行一致**（x64 写的 3151 行数据 → x86 读入重写 → 完全一致） |
| 界面渲染 | 与 x64 版截图一致（28 张学生卡、字体、DPI 无差异） |

## 安装包（已完成）

| 文件 | 说明 |
| --- | --- |
| `..\Tensyan\dist\TensyanSetup-x86.exe` | 安装程序，**需要管理员**；仅 **440 KB**；默认装到 `C:\Program Files (x86)\Tensyan`；卸载信息登记到 32 位视图 `HKLM\SOFTWARE\WOW6432Node\...` |
| `..\Tensyan\dist\TensyanSetup-x86-标准用户版.exe` | 不需要管理员；装到当前用户目录；卸载信息登记到 HKCU |
| `dist\`（本目录内） | 上面两个安装包 + 32 位绿色版 `TensyanX86.exe` 的归档副本 |

一键打包：

```powershell
# 在 ..\Tensyan 目录下执行
powershell -ExecutionPolicy Bypass -File build-installer-x86.ps1
```

安装程序与卸载器**都编译成 x86**，保证注册表视图（WOW6432Node）与程序一致；
32 位安装包与已有的 64 位安装包**互不冲突**，可以同时装在一台机器上。

### 实测验证（提权真机安装）

| 项 | 结果 |
| --- | --- |
| 提权后默认安装位置 | `C:\Program Files (x86)\Tensyan` ✅（截图 `预览截图\07`） |
| 静默安装到 `D:\App\Tensyan-x86` | exit=0，6 个文件（Tensyan.exe / .config / 卸载器 / 三个说明文档） |
| 注册表 | 落在 `HKLM\SOFTWARE\WOW6432Node\...\Uninstall\Tensyan`（32 位视图），64 位版的登记项**不受影响** |
| 快捷方式 | 桌面 + 开始菜单（含“卸载 Tensyan”）共 3 个 |
| 安装后运行 | 进程存活，日志“主窗口已显示：登录界面” |
| 安装副本自检 | **27 / 27 通过** |
| 卸载（保留数据） | exit=0，程序与注册表项删除，`data` 保留 |
| 运行库检测 | 界面实时显示“已安装 .NET Framework 4.8.09032（Release 533320）”（截图 `预览截图\08`） |

## 构建

```powershell
dotnet build TensyanX86.csproj -c Release
# 或
powershell -ExecutionPolicy Bypass -File build-x86.ps1
```

## 源码组织

**不复制代码**：`TensyanX86.csproj` 直接引用 `..\Tensyan` 下的全部源码，
只有 .NET Framework 上不存在的 API 才用 `#if NETFRAMEWORK` 分支或垫片：

| 文件 | 作用 |
| --- | --- |
| `Compat\JsonCompat.cs` | 离线 JSON 读写（格式与 `System.Text.Json` 完全一致，保证两个版本共用数据文件） |
| `..\Tensyan\UI\WinCompat.cs` | 跨框架垫片：输入框占位提示（EM_SETCUEBANNER）、DPI 感知 |
| `..\Tensyan\Core\Store.cs` | `Json` 类按框架切换实现（`#if`，唯一被改动的共享文件） |
| `app.x86.manifest` | asInvoker + `dpiAware`（net48 的 DPI 靠清单） |

## 32 位注意事项（发布前必读）

1. 数据格式与 x64 版一致，可互换；**同一台机器建议只装一个版本**。
2. 若两版共用同一个 `data` 目录，务必让它们共用单实例互斥体名，否则可能同时写入损坏数据。
3. 默认安装目录应为 `%ProgramFiles(x86)%\Tensyan`（`SpecialFolder.ProgramFilesX86`）。
4. 注册表会重定向到 `WOW6432Node`：**安装器与程序必须同为 x86**。
5. 32 位进程内存上限约 2 GB：大数据量导出请走流式，避免一次读入超大备份。
6. net48 没有"单文件发布"：一个文件夹即可（本项目只有 exe + .config，无第三方依赖）。

## 待定项

- 正式名称：是否把 `TensyanX86.exe` 改回 `Tensyan.exe`（靠文件夹区分）？
- 是否需要 x86 安装包（可复用现有 net48 安装器，加 `PlatformTarget=x86` 即可）？
- 是否需要额外的 **AnyCPU 单包**（一个 exe 在 32/64 位系统各取所需）？
