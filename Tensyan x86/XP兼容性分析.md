# Tensyan 兼容 Windows XP 的可行性分析

> **更新：XP 特别版已经做出来并交付了** —— 见文末「九、XP 特别版交付与验证」。
> 下面是当初的分析过程（选型、实测数据、32 位的坑），保留以说明"为什么是 net35 而不是 net40/net8"。

> 结论摘要：**技术上可行，但天花板是 .NET Framework 4.0（或 3.5），不是 .NET 8。**
> 而且本机目前**无法产出可信的 XP 构建**（缺少真正的 3.5/4.0 目标程序集，见第三节）。
> 好消息：你这台机器上有 XP SP3 安装盘 + 现成的 XP 虚拟机，**可以真机验证**。

---

## 一、硬约束：XP 能用的 .NET 到 4.0 为止

| 运行时 | 支持 XP？ |
| --- | --- |
| .NET Framework 2.0 / 3.0 / 3.5 | ✅ XP SP2/SP3 |
| **.NET Framework 4.0** | ✅ **XP SP3**（最后一个支持 XP 的版本） |
| .NET Framework 4.5 及以上 | ❌ 要求 Vista SP2 / Win7 |
| .NET Core / .NET 5~10 | ❌ 要求 Win7 SP1 以上 |

所以"XP 版"只有两条路：**net40（XP SP3）** 或 **net35**。

---

## 二、现场发现：你那台 XP 虚拟机里装的是 .NET 3.5，不是 4.0

我直接扫描了 `D:\App\VirtualBox\WindowsXP\WindowsXP.vdi`（1.41 GB）：

| 扫描目标 | 结果 |
| --- | --- |
| `Framework\v2.0.50727` | ✅ 找到 → 装了 .NET 2.0/3.5 |
| `Framework\v4.0.30319` | ❌ 未找到 |
| `.NET Framework 4` / `dotnetFx40` | ❌ 未找到 → 从未装过 .NET 4.0 |
| `msyh.ttf`（微软雅黑） | ❌ 未找到 → XP 没有这个字体 |
| `Tensyan` | ❌ 未找到 → 还没在上面跑过 |

**这条信息直接改变选型**：
- 若目标 XP 机器都像这台虚拟机（只有 3.5）→ **net35 是零部署方案**（不用装任何东西）；net40 反而要给每台机器装 48 MB 的 .NET 4.0 离线包（且仅支持 XP SP3）。
- 若目标机器可以装 .NET 4.0 → net40 需要的兼容改造更少。

---

## 三、本机目前的障碍：没有真正的 3.5/4.0 目标程序集（重要）

我实测了本机的"参考程序集"目录：

| 目录 | 实际情况 |
| --- | --- |
| `…\.NETFramework\v4.0` | 191 个文件，但**没有 mscorlib.dll**（只是空壳/部分文件） |
| `…\Reference Assemblies\…\v3.5` | 49 个文件，同样**没有 mscorlib.dll** |
| `…\.NETFramework\v4.8` | 368 个文件，**有 mscorlib** ✅（这就是 x64/x86 版能正常编译的原因） |
| `C:\Windows\Microsoft.NET\Framework\v4.0.30319\mscorlib.dll` | 文件版本是 **4.8.9310.0** → 是 4.8 的实现，用它会漏检 4.5+ API ⚠️ |

- **net40 试编：失败**——SDK 找不到 4.0 目标程序集，转而去 NuGet 下载（离线，报 NU1301）。
- **net35 试编：表面上"只差 1 个错误"（只有 `ZipArchive`）**，但我没有采信，回头做了验证：
  用一个"只调用 4.0/4.5 API"的小探针（`IsNullOrWhiteSpace` / `Task.Run` / `ConcurrentDictionary` / `Lazy<T>`）在同一套引用下编译 → 探针**报错**（`System.Threading.Tasks` 不存在），
  可我的主工程却把 `IsNullOrWhiteSpace`（25 处）和 `Task.Run`（2 处）都编过了 —— 两者矛盾。
  结合 `mscorlib 2.0` 的元数据里确实**没有** `IsNullOrWhiteSpace` 这个事实，可以判定：
  **SDK 在解析 net35 引用时，对找不到的程序集悄悄回退到了本机 4.8 的参考程序集**，于是"能编译"是假象 —— 这样出来的 exe 在真 XP 上会因为找不到成员而崩。

**所以：要产出可信的 XP 版，必须先补上真正的目标程序集**，任选其一：
1. 联网取 NuGet 包 `Microsoft.NETFramework.ReferenceAssemblies.net35`（或 `.net40`，各约 1–2 MB）——最省事；
2. 装 Windows SDK 7.0A / VS 的 .NET 4.0 targeting pack；
3. 从一台**真的装了 .NET 4.0 的机器**（可以就在 XP 虚拟机里先装 .NET 4.0）把 `C:\Windows\Microsoft.NET\Framework\v4.0.30319\*.dll` 取出来当 `FrameworkPathOverride`——那份是货真价实的 4.0 实现。

> 顺带说明：这个障碍只影响 XP 版。**net48（当前 32 位版）和 net8（x64 版）用的都是本机真实存在的目标程序集，编译结果可信**（前面 27/27 自检与数据互通测试都是基于它们）。

---

## 四、代码改造量预判（按目标框架分列）

扫描了 28 个源文件，XP 化会碰到的地方：

| 需要处理 | net40（XP SP3） | net35 |
| --- | --- | --- |
| `AppContext.BaseDirectory`（1 处） | 改成 `AppDomain.CurrentDomain.BaseDirectory`（4.0 没有 AppContext） | 同左 |
| `Task.Run`（2 处） | 改成 `ThreadPool.QueueUserWorkItem` 包装 | 同左 |
| `ZipArchive`（2 处，xlsx 导出） | **手写 ZIP 容器**（`DeflateStream` 在 2.0 就有，约 80 行） | 同左 |
| `Rfc2898DeriveBytes(..., SHA256)`（1 处） | **手写 PBKDF2-HMAC-SHA256**（`HMACSHA256` 有，约 40 行） | 同左（3.5 的 Rfc2898 只支持 SHA1） |
| `string.IsNullOrWhiteSpace`（25 处） | ✅ 4.0 自带 | ❌ 需自写 `IsBlank()` 并替换 25 处 |
| `List<T>.Clear()` / `StringBuilder.Clear()` | ✅ | `StringBuilder.Clear()` 需改写（`Length = 0`） |
| `Func<>` / `Action<>` / LINQ | ✅（System.Core 4.0） | ✅（需 System.Core 3.5） |
| `nameof` / 插值字符串 / `?.` 等语法 | ✅（编译器特性，与框架无关） | ✅ |
| **合计** | **约 4 类、6 处改动** | **约 8 类、35+ 处改动** |

---

## 五、XP 上一定会"变丑/变慢"的地方（要有心理准备）

1. **没有微软雅黑**：XP 自带的是宋体/黑体，界面字体必须回退（`Microsoft YaHei UI` → `SimHei` → `SimSun`），观感会明显退化；
2. **没有 DWM**：Win11 的圆角、阴影、边框效果在 XP 上全部失效，要么用 `Region` 做粗糙圆角，要么接受直角窗口；
3. **`EM_SETCUEBANNER`（输入框占位提示）在 XP 上无效**（那是 Vista+ 的 comctl32 v6 功能），需要自己画灰色提示文字；
4. **PBKDF2 慢**：120000 次迭代在本机约 190–340 ms，在 2005 年前后的单核 CPU 上可能 **1–3 秒**；建议 XP 版把迭代数降到 3–5 万，或保持"后台线程 + 登录中…"提示；
5. **GDI 句柄上限 10000**（XP 比后续系统更紧）：大量卡片/图标要复用，避免反复创建；
6. **无现代 DPI 感知**：XP 只有系统 DPI 缩放，高分屏体验有限；
7. **安全性**：XP 无安全更新，.NET 3.5/4.0 均已 EOL。本程序**不联网**（数据全在本机、U 盘密钥是本地 HMAC），风险相对可控，但仍不建议把 XP 机器接入互联网。

---

## 六、三条路线的取舍

| 路线 | 优点 | 代价 | 可行性 |
| --- | --- | --- | --- |
| **A. net35 x86（XP 原生支持）** | 目标机**零部署**（3.5 已在）；一份源码多一个 TFM | 兼容垫片最多（35+ 处）；界面明显降级；需要真正的 3.5 目标程序集 | ✅ 推荐给"只有 3.5 的老机器" |
| **B. net40 x86（XP SP3）** | 改造最少（约 6 处） | **每台 XP 机器要装 .NET 4.0**（48 MB 离线包，XP SP3 限定）；同样没有雅黑/DWM | ✅ 推荐给"能装 4.0 的机器" |
| **C. 原生重写（Free Pascal/Lazarus、Delphi）** | 无运行库、exe 极小、XP 原生观感最好、还能兼 98/2000 | **要重写整个界面**（自绘 Fluent、动画、DPI、随机点名……）+ 本机没有任何原生编译器，需先弄工具链 | ⚠️ 工作量 2–4 天，只在"XP 是硬需求且机器多"时才值得 |

**我的建议**：XP 通常是"少数几台老机器"，为它把完整的 Fluent 界面硬搬过去并不划算。更实际的做法是做一个 **XP 精简版**：
- net35 x86、**只用最朴素的 WinForms 控件**（无自绘卡片、无动画、无圆角）；
- 保留核心闭环：学生列表 / 加减分 / 排行 / 记录 / 导出 Excel / 账号 / U 盘登录；
- 与 x64/x86 版**共用同一份 data 文件**（JSON 格式已实测互通）。

这样"新机器用漂亮版、老 XP 机器用能用的版"，而不是让 XP 拖住整个界面。

---

## 七、下一步需要你定的事

1. **XP 机器有几台？上面装的是什么 .NET？**（你那台虚拟机只有 3.5；如果实际机器也是 3.5 → 直接走 net35，零部署）
2. **是否授权我用 VirtualBox 在你那台 XP 虚拟机上做真机验证？**
   （沙箱当前拒绝 VirtualBox 的 COM 访问，需要提权；VM 与 XP SP3 安装盘都在本机，验证是可行的）
3. **要不要按"XP 精简版"来做**，而不是把 Fluent 界面搬过去？
4. 如果选 net40/net35，**能否提供一次联网**（取 `Microsoft.NETFramework.ReferenceAssemblies.net35/net40`，约 2 MB）——否则我只能用"从 XP 虚拟机里取真实 4.0 运行时程序集"这个绕法。

---

## 九、XP 特别版交付与验证（已完成）

### 交付物

| 文件 | 大小 | 说明 |
| --- | --- | --- |
| `dist\TensyanSetup-XP.exe` | 606 KB | XP 安装程序：内嵌程序本体 + 卸载器 + 文档，不需要联网、不需要解压 |
| `bin\Release\net35\TensyanXP.exe` | 337 KB | XP 版程序（安装后改名为 `Tensyan.exe`） |
| `dist\XP验证用软盘-*.img` | 1.44 MB ×2 | 给 VirtualBox 用的软盘镜像（内含安装程序 / 程序本体），便于在虚拟机里手动验证 |

目标机要求：**Windows XP SP3 + .NET Framework 3.5**（老机器一般已装；装了 .NET 4.0 的机器同样能跑）。

### 为 XP 做的改造（全部在这份共享源码里，用 #if / 垫片隔离）

| 问题 | 处理 |
| --- | --- |
| net35 没有 `System.IO.Compression.ZipArchive`（Excel 导出要用） | 自己写了 `Core/ZipWriter.cs`（DeflateStream + CRC32 + 中央目录，约 300 行），**三个版本共用**；产出的 .xlsx 用 openpyxl 验证可正常打开 |
| net35 的 `Rfc2898DeriveBytes` 只支持 SHA1 | 自己实现 `Pbkdf2.DeriveKey`（HMAC-SHA256，约 40 行），**三个版本共用**；x64 版建的账号密码能在 XP 版登录成功（哈希一致） |
| 没有 `System.Text.Json` | `Tensyan x86\Compat\JsonCompat.cs`：反射实现，输出格式与 STJ 逐字节一致（已在 3151 行真实数据上逐行比对） |
| `IsNullOrWhiteSpace`(25 处)、`Task.Run`(2)、`AppContext`(1)、`Path.Combine` 三参、`Stopwatch.Restart`、`string.Join(params)`、`Environment.SpecialFolder.Windows`、`Stream.CopyTo`、`Enum`/注册表 API 等 | 统一收口到 `Core/Compat.cs`（`Str.Blank` / `TaskCompat.Run` / `AppInfo.BaseDirectory` / `PathCompat.Combine`）与少量 `#if NET35` 分支 |
| XP 没有微软雅黑 | `Theme.ResolveFamily` 字体链加上 黑体/宋体 兜底 |
| XP 没有 DWM | `ApplyRoundCorners` 在 XP 上直接跳过（**直角窗口**，已与用户确认可接受） |
| XP 的 `EM_SETCUEBANNER` 无效（占位提示） | `FluentInput.OnPaint` 在 XP 上自绘灰色提示文字 |
| 老机器性能有限 | XP 上**动画默认关闭**（`Anim.Enabled = ... && !Theme.IsXp`），日志里会标注 `[Windows XP 模式]` |
| 自检需要 | 新增环境变量 `TENSYAN_FORCE_XP=1`，可在新系统上强制走 XP 分支做验证 |

### 实测结果

| 项 | 结果 |
| --- | --- |
| 编译 | net8 x64 / net48 x86 / **net35 x86** 三套目标全部 **0 错误** |
| 真实目标程序集 | 用 NuGet 包 `Microsoft.NETFramework.ReferenceAssemblies.net35` 解出的**真 3.5 参考程序集**编译（不会"偷偷用 4.x API 却编过"） |
| XP 版自检 | **27 / 27 通过** |
| XP 版界面自检 | **8 / 8 通过** |
| 密码跨版本 | x64 版建的账号（PBKDF2 120000 次）→ XP 版登录成功 |
| 数据跨版本 | x64 写 → XP 读改 → x64 再读：8/8 通过，逐行一致 |
| Excel | XP 版自写 ZIP 产出的 .xlsx 经 openpyxl 打开正常（表名/表头/行数正确） |
| XP 模式渲染 | `TENSYAN_FORCE_XP=1` 出图 26 张，正常 |
| XP 安装程序 | 同机实测：安装 exit=0（6 个文件 + 3 个快捷方式）→ 程序启动 → 安装副本自检 27/27 → 卸载（保留数据）exit=0 |

### XP 虚拟机真机验证（进行中）

本机有可启动的 XP 虚拟机（`D:\App\VirtualBox\WindowsXP`，VirtualBox 7.2，XP 5.1.2600，德语版，用户 furliflix）。

- ✅ 已能启动到 XP 桌面并截屏取证
- ✅ 已打通 `VBoxManage` 的软盘挂载 / 键盘注入 / 截屏自动化
- ⚠️ 受阻：该虚拟机是**德语键盘布局**，VBoxManage 字符注入按其内部映射发送扫描码 → `:`→`ö`、`\`→`ß`、空格丢失、Shift/AltGr 时灵时不灵，命令无法可靠输入，因此"在虚拟机里自动安装并自检"这一步尚未完成
- 手动验证只需 1 分钟：给虚拟机加软驱控制器 → 挂 `dist\XP验证用软盘-安装程序.img` → 在 XP 里运行 `A:\TSETUP.EXE`

> 说明：以上"自检 27/27"等结论是在**本机的真 .NET 3.5 运行时**上跑出来的（本机同时装了 2.0/3.5/4.x），
> 因此它验证的是"代码在 .NET 3.5 下正确"，而不是"在某台具体 XP 机器上"；
> XP 虚拟机的手动验证可以补上最后一环。
