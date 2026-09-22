# SukeFlow — 项目指南 (AGENTS.md)

## 1. 项目概览

- **SukeFlow** 是一个轻量级跨平台课程表应用，基于 Avalonia UI 构建，界面面向中文用户。
- **计划发布平台**：Windows 桌面、Android、WASM（浏览器）。
- **核心目标**：解析学校教务系统（正方教务）导出的课表 HTML，并使用**自绘 `CourseTable` 控件**显示课程表。
- 已确认的产品决策：
  - 采用**多 Head 拆分**的项目结构（见 §3）。
  - 每周显示 **7 天**，每天 **22 节**（节数要求可调）。
  - 暂不分段（无上午/下午/晚上标题）。
  - 学期起始 **2026-09-07**，总周数 **20**（先写死用于测试，后续可配置）。
  - **只做显示 + 本地导入**（解析粘贴的教务 HTML、本地保存课表），暂不做账号对接/云同步。
  - 手势左右滑动切换周次（1–20 周**循环**）；点击课程从**底部弹出卡片**显示详情。
  - HTML 中的「其它课程」（无固定时间的课程）**忽略**，不进入模型、不显示。
  - 默认节次时间见 §7.5（22 节，统一 40 分钟/节 + 5 分钟间隔，从 07:30 起）。
  - 课表持久化：桌面/Android 存文件、WASM 存 localStorage（见 §9）；顶栏「导入」按钮可粘贴 HTML 解析并保存。

## 2. 技术栈

| 项目 | 版本 / 说明 |
| --- | --- |
| .NET | 10.0（`dotnet --version` = 10.0.202） |
| Avalonia | 12.1.2（Fluent 主题 + Inter 字体） |
| MVVM | CommunityToolkit.Mvvm 8.4.2（源生成器） |
| 开发工具 | `AvaloniaUI.DiagnosticsSupport`（仅 Debug） |
| 解析 | 不引入第三方 HTML 库，用手写解析器（保持轻量，兼容 WASM/AOT） |
| 字体 | 内嵌 `Noto Sans SC`（OFL，`Assets/Fonts/`）+ Inter；WASM 无系统中文字体，必须内嵌（见 `AppBuilderFontExtensions`） |

> 已安装 workload：`android`、`wasm-tools`（`dotnet workload list`）。

## 3. 计划目录结构（多 Head 拆分）

```
SukeFlow/
├── AGENTS.md
├── SukeFlow.sln
├── SukeFlow.Core/               # 类库(net10.0)：模型/解析/持久化/自绘控件/VM/视图，无平台依赖
│   ├── Models/                  #   课程表数据结构（JSON 属性已标注）
│   ├── Services/                #   正方 HTML 解析器、示例数据、JSON 序列化、存储抽象
│   ├── Controls/                #   CourseTable 自绘控件、配色
│   ├── ViewModels/
│   ├── Views/                   #   MainView（共享内容）+ MainWindow（桌面壳）
│   ├── Assets/                  #   图标、字体（运行时）与 HTML/PNG（仅开发参考）
│   ├── App.axaml(.cs)
│   ├── AppBuilderFontExtensions.cs
│   └── ViewLocator.cs
├── SukeFlow.Desktop/            # net10.0 桌面入口（Windows/Linux/macOS）
├── SukeFlow.Android/            # net10.0-android（ApplicationId: com.sukeflow.app）
├── SukeFlow.Browser/            # net10.0-browser (WASM)
├── SukeFlow.Tests/              # xunit 单测（解析器/周次/模型/序列化），CI 门禁
├── .github/workflows/           # ci.yml / pages.yml / release.yml
├── docs/release-signing.md      # Android 签名密钥操作手册
├── global.json                  # 固定 SDK 10.0.2xx（rollForward: latestFeature）
├── Directory.Build.props        # 共享版本/作者/仓库元数据
├── LICENSE / README.md
```

> 拆分方式以 Avalonia 官方多平台模板为参考：Core 放共享代码，各 Head 只放平台引导代码。

## 4. 构建与运行

```bash
dotnet build SukeFlow.sln                        # 构建全部（桌面 + Android + WASM）
dotnet run --project SukeFlow.Desktop            # 桌面运行

# Android：产出已签名 APK（本机已具备 Android SDK）
dotnet build SukeFlow.Android/SukeFlow.Android.csproj -f net10.0-android
# → SukeFlow.Android/bin/Debug/net10.0-android/com.sukeflow.app-Signed.apk

# WASM：发布后需用静态服务器托管（.wasm 需 application/wasm MIME）
dotnet publish SukeFlow.Browser/SukeFlow.Browser.csproj -c Release -o publish
# → 托管 publish/wwwroot
```

## 5. 编码约定

- 命名空间：`SukeFlow.Core.Models` / `.Services` / `.Controls` / `.ViewModels` / `.Views`，文件作用域 namespace。
- 自定义控件通过重写 `Render(DrawingContext)` 自绘；对外属性用 `AvaloniaProperty`（`StyledProperty`/`DirectProperty`）注册。
- ViewModel 继承 `ViewModelBase`（= `ObservableObject`），使用 `[ObservableProperty]` / `[RelayCommand]`。
- XAML 必须声明 `x:DataType` 启用编译绑定；控件前缀 `sfc:`。
- `Core` 中禁止引用平台相关 API（保证 Android/WASM 可编译）。
- 模型/解析逻辑应可单元测试（与 UI 解耦）。

## 6. 课程数据模型（正方教务 HTML 解析）

### 6.1 原始 HTML 结构（`SukeFlow/Assets/个人课表查询.html`）

正方教务「个人课表查询」页面，数据在 `<table id="kblist_table">` 中：

- 表头信息（示例文件已脱敏）：`2026-2027学年第1学期`、`陈思远的课表`、`学号：20260000001`。
- 按星期分组：`<tbody id="xq_1">` ～ `<tbody id="xq_7">`（周六/周日可能 `display:none` 且为空）。
- 节次单元格：`<td id="jc_{星期}-{起始节}-{结束节}">`，如 `jc_2-3-4` = 周二 3~4 节；实践周课程可能是 `jc_2-1-8`（1~8 节整段）。
  **该单元格只放节次（`<span class="festival">1-8</span>`），课程内容在它的下一个无 id 的 `<td>` 里**；
  解析器只从「无节次 id 的 td」收集 `timetable_con`（`ParseDay` 的 if/else 链）。写测试夹具时必须按此结构。
- 课程信息：`<div class="timetable_con">` 内：
  - 课程名：`<u class="title showJxbtkjl" data-jxb_id="...">`（可点击）或 `<span class="title">`；
    名称可能带 `【调】` 前缀（调课）与标记 `★-讲课 ☆-实验 □-实践 ■-实践周 〇-课外 ◆-上机`。
  - 详情：`<p>` 内以图标 + `键：值` 形式罗列（见 6.2）。
  - 字体颜色：`<font color="blue">` = 已选上，`red` = 待筛选（斜体）。
- **同格多门课**（单双周重叠）的两种写法都要支持：
  1. 多个 `<tr>` 共享同一个节次单元格（靠 `rowspan`），后续行没有 `id`，需沿用上方节次；
  2. 同一个 `<td>` 内出现多个 `timetable_con`（防御性支持）。
- 表格末尾有 **其它课程**（无固定时间）行，纯文本：
  `形势与政策（五）★王贺(共4周)/1-4周/无;`（名称+标记、教师、(共N周)、周次、地点；`无` = 无地点）。

### 6.2 模型字段（从 HTML 中可提取的全部字段）

| 字段 | 来源 | 说明 |
| --- | --- | --- |
| Name | `.title` 文本 | 去掉 `【调】` 前缀；保留/解析类型标记 |
| TypeMark | 名称后缀 ★☆□■〇◆ | 讲课/实验/实践/实践周/课外/上机，可能多个 |
| IsAdjusted | 名称前缀 `【调】` | 调课标记 |
| JxbId | `data-jxb_id` | 教学班 ID（唯一标识一次开课） |
| DayOfWeek | 单元格 `id` | 1=周一 … 7=周日 |
| StartPeriod / EndPeriod | 单元格 `id` | 节次区间（含端点） |
| WeekPattern | `周数：2-16周` / `3-15周(单)` / `2-16周(双)` / `17周` | 起始周、结束周、单双周 |
| Teacher | `教师` | |
| Location | `上课地点` | 可能为空/`无` |
| Campus | `校区` | |
| TeachingClass | `教学班` | |
| ClassComposition | `教学班组成` | |
| AssessmentMethod | `考核方式` | |
| SelectionNote | `选课备注` | |
| HoursComposition | `课程学时组成` | |
| WeeklyHours / TotalHours / Credits | `周学时`/`总学时`/`学分` | |
| SelectionState | `<font color>` | blue=已选上 / red=待筛选 |
| Semester | 表头 | 如 `2026-2027学年第1学期` |
| StudentName / StudentId | 表头 | |
| OtherCourses | 表尾「其它课程」 | 无固定时间（**忽略，不解析**） |

### 6.3 解析规则与注意事项

- 文本中存在全角空格、换行符，`键：值` 需按首个 `：` 切分并 `Trim`。
- `周数` 支持：`N周`、`A-B周`、`A-B周(单)`、`A-B周(双)`。
- 同一课程（同 `JxbId`）可能出现在多个星期/多个节次 → 视为同一门课的多个时段。
- 课程颜色不来自 HTML：按课程名（或 JxbId）**哈希到固定调色板**，保证同一课程到处同色（参考图行为）。
- **「其它课程」不解析**（无固定时间的课程，格式如 `形势与政策（五）★王贺(共4周)/1-4周/无;`）。
- 解析器输入为 HTML 字符串，内置 `Assets/个人课表查询.html` 作为测试/示例数据。

## 7. CourseTable 参考 UI 规格（来自 `Assets/CourseTableRef.png`）

参考图为手机竖屏课表 App 截图（467×853 逻辑像素）。以下为逐像素分析后的实际参数：

### 7.1 布局

| 区域 | 位置/尺寸 | 内容 |
| --- | --- | --- |
| 顶栏 | y 0–60，无分隔线 | 左：`第 2 周（非本周）`（约 16–17px，半粗，深色）；下方 `2026/9/15`（约 13–14px，灰）；右侧：图标按钮 |
| 日期表头 | y 60–130 | 左上角显示月份（如 `9月`）；7 列各显示星期（约 11px 浅灰）+ 日期（约 13px 粗体深色，如 `14`…`20`） |
| 节次时间列 | x 0–36 | 每节两行小字（约 11px 灰）：开始时间 / 结束时间（如 `8:40` / `9:20`） |
| 课程网格 | x 36–467 | 7 列，列距 ≈62px；节次行高 ≈90px；课程卡片宽 ≈55px，卡片间距 ≈8px |

> 实现说明：控件自适应可用宽度，日列宽 = (控件宽 − 36 − 0) / 7，卡片宽 = 日列宽 − 8；行高固定 90px，纵向滚动。

### 7.2 配色（参考图取色）

- 页面背景：浅薰衣草白 ≈ `#E5E7F5`；空单元格无网格线、无独立底色；卡片无阴影、圆角 ≈6px。
- 课程卡片为**柔和粉彩色**，每门课固定颜色，参考色板：
  `#EC9E8B`(鲑红) `#8EC2EF`(天蓝) `#8AE6D6`(薄荷) `#91B5F4`(长春花蓝) `#E4A4B9`(粉) `#BFABF4`(淡紫) `#76A2CF`(钢蓝) `#DFC9B3`(米色)
- 卡片文字为卡片同色系的深色；**非本周**课程整体淡化（向背景混合）并在卡片底部居中显示 `非本周` 小徽章。

### 7.3 卡片内容（自上而下，左对齐，内边距 ≈6px）

1. 课程名（约 17–18px、半粗，窄列中按每行约 2 个汉字换行）
2. 上课地点（约 14px，前置小图标）
3. 教师（约 14px，可选）
4. （非本周时）底部 `非本周` 徽章

卡片高度 = 节数 × 行高 − 8px（如 2 节 = 172px）。内容超出时裁剪/省略。

### 7.4 行为

- **左右滑动手势**切换周次（1–20 周循环，首尾相接）：拖动时相邻周内容跟随手指 1:1 滑动；松手后按位移判定提交切换（滑完整页、缓动落位）或回弹；顶部显示 `第 X 周`，若所显示周不等于当前周则追加 `（非本周）`。
  顶栏按钮 / 方向键切周同样带滑入动画；跨多周跳转（如「今天」）直接切换。
- **点击课程卡片**从**底部弹出卡片**（Bottom Sheet）显示课程详情；点击外部/下滑关闭；弹层带滑入/滑出动画（约 280ms，缓动）。
- **同格重叠**：优先绘制当周课程；非当周（如单双周差异）课程淡化绘制在其后。若同周真重叠（周次完全相同），全部绘制：卡片左右并排、横向平分单元格宽度。
- 当前周计算：以学期起始 2026-09-07 为第 1 周起算。

> 动画实现注记：`CourseTable` 翻页动画由 `DispatcherTimer`（≈16ms）逐帧驱动（缓动 ease-out cubic，与自绘渲染同频）；
> 底部弹层用 XAML 的 `TranslateTransform.Y` + `DoubleTransition`（配合遮罩 `Opacity` 过渡）实现。

### 7.5 默认节次时间（22 节）

规则：**每节 40 分钟，节间 5 分钟**；第 n 节开始时间 = 07:30 + (n−1)×45min。可配置，后续可替换为学校时间表。

| 节 | 时间 | 节 | 时间 | 节 | 时间 | 节 | 时间 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | 07:30–08:10 | 7 | 12:00–12:40 | 13 | 16:30–17:10 | 19 | 21:00–21:40 |
| 2 | 08:15–08:55 | 8 | 12:45–13:25 | 14 | 17:15–17:55 | 20 | 21:45–22:25 |
| 3 | 09:00–09:40 | 9 | 13:30–14:10 | 15 | 18:00–18:40 | 21 | 22:30–23:10 |
| 4 | 09:45–10:25 | 10 | 14:15–14:55 | 16 | 18:45–19:25 | 22 | 23:15–23:55 |
| 5 | 10:30–11:10 | 11 | 15:00–15:40 | 17 | 19:30–20:10 | | |
| 6 | 11:15–11:55 | 12 | 15:45–16:25 | 18 | 20:15–20:55 | | |

## 8. 当前状态与路线图

- [x] Avalonia 模板初始化（桌面可运行、构建通过）
- [x] 参考图与原始 HTML 分析完毕（规格见 §6、§7）
- [x] 项目拆分为 Core + Desktop（Android/Browser 待接入）
- [x] `Models`：Course / CourseSession / WeekPattern / PeriodSchedule / SemesterInfo / Timetable
- [x] `Services`：正方教务 HTML 解析器 + 内置示例数据（已用示例 HTML 验证：8 门课 / 16 个时段）
- [x] `CourseTable` 自绘控件：网格、表头、时间列、课程卡片、非本周淡化/徽章（含重叠并排、非本周拆分绘制）
- [x] 交互：滑动手势/方向键切周、滚轮滚动、点击命中测试、课程详情 Bottom Sheet
- [x] ViewModel/View 接线（已用截图 + 像素/OCR 验证：布局、卡片颜色、当周优先、淡化徽章、详情弹层）
- [x] Android / WASM 目标接入（Android 产物 APK；WASM 已发布并在无头 Chrome 中渲染验证）
- [x] 中文显示：内嵌 Noto Sans SC（WASM 修复）
- [x] 持久化 + HTML 导入（顶栏「导入」面板；桌面/Android 文件、WASM localStorage）
- [x] 翻页与弹层动画（三页跟手滑动 + 提交/回弹缓动；底部弹层滑入/滑出）
- [x] 本地存储各端打通（WASM localStorage 写入/读取已端到端验证）
- [x] 单元测试：`SukeFlow.Tests`（50 个用例，覆盖解析器/周次/模型/序列化）
- [x] 发布链路：GitHub Actions（CI 门禁、WASM → Pages、tag → Windows zip + 签名 APK + Release）
- [ ] 发布配置细化（应用图标、WASM 字体子集化/压缩、APK AOT 与裁剪选项、Windows 代码签名）

## 9. 数据持久化与导入

### 9.1 导入流程（UI）

1. 顶栏「导入」按钮 → 底部弹出导入面板：粘贴教务系统「个人课表查询」页面 HTML（另存网页后全选复制即可）。
2. 点「解析并保存」→ `ZfsoftTimetableParser` 解析 → 替换当前课表 → 写入本地存储 → 状态栏提示「解析成功：N 门课程 / M 个时段」。
3. 「载入示例」加载内置示例课表并保存；「清除数据」删除本地数据并恢复示例课表。

### 9.2 存储实现（各端）

| 平台 | 实现 | 位置 | 备注 |
| --- | --- | --- | --- |
| 桌面 | `FileTimetableStorage`（主路径 = 应用目录） | `<应用目录>/data/timetable.json` | 保持用户目录清洁；应用目录不可写时降级到 `%LocalAppData%/SukeFlow/timetable.json` |
| Android | `FileTimetableStorage`（Android Head 注入 `FilesDir`） | `/data/data/com.sukeflow.app/files/timetable.json` | 应用私有目录，卸载即清除 |
| WASM | `LocalStorageTimetableStorage`（JSImport → `localStorage`） | `localStorage['sukeflow.timetable.json']` | 浏览器无法写“应用目录”，故用 localStorage；写入/读取已用无头 Chrome + CDP 实测 |

- 入口：`AppStorage.Timetable`（平台 Head 在启动时替换实现，见 `SukeFlow.Desktop/Program.cs`、`SukeFlow.Android/Application.cs`、`SukeFlow.Browser/Program.cs`）；首次启动无本地数据时显示内置示例课表。
- 数据优先放应用目录，保持用户目录清洁；若应用目录不存在旧数据、而降级路径（旧版本用户目录）有数据，会在首次读取时**自动迁移并清理旧文件/空目录**。
- 格式：`TimetableSerializer` + `TimetableJsonContext`（源生成，兼容 AOT）：camelCase、缩进、中文不转义；示例课表 JSON 约 6–10 KB。

### 9.3 方案调研与取舍

**浏览器（WASM）候选方案**：

| 方案 | 容量 | 持久性 | 复杂度 | 结论 |
| --- | --- | --- | --- | --- |
| `localStorage` | ~5 MB/域 | 持久（除用户清理/隐私模式） | 极低（同步字符串） | ✅ 采用：数据仅 10 KB 量级 |
| IndexedDB | 数百 MB+ | 持久 | 高（异步事务 + JS interop 包装） | 数据量大或需多课表/历史版本时再迁移 |
| OPFS（Origin Private File System） | 配额大 | 持久 | 中（Worker/异步 API） | 适合存储文件（图片、附件），本项目用不上 |
| Cache Storage | 中等 | 持久 | 中 | 面向请求缓存，不适合键值数据 |
| 内存（回退） | — | 刷新即丢 | 无 | 仅当 localStorage 不可用时降级（不写入） |

**桌面 / Android**：直接用文件（JSON）——无容量顾虑、方便备份/调试。
应用优先把数据放在**应用目录**（`<应用目录>/data/timetable.json`），保持用户目录清洁；
Android 用 `FilesDir` 显式指定应用私有目录，不依赖 `SpecialFolder` 在移动端的映射行为；
应用目录不可写时（如安装到 `Program Files` 等只读位置）自动降级到 `%LocalAppData%/SukeFlow/timetable.json`。

**可靠性处理**：

- 读取失败/JSON 损坏 → 返回 `null`，回退到内置示例课表，并在导入面板提示（`InitializeAsync` 捕获）。
- 写入失败（隐私模式、配额满）→ 不静默：抛出可读异常，界面提示「保存失败：…」；「清除数据」失败也会提示。
- 格式演进：当前未加版本号；若后续变更字段语义，建议在外层加 `version` 包装并做迁移。
- 模型上仅标注 `[JsonIgnore]`（计算属性）与可写集合属性，便于直接序列化。

## 10. 设计决策记录

| 决策 | 结论 |
| --- | --- |
| 项目结构 | 多 Head 拆分：`SukeFlow.Core` + `SukeFlow.Desktop/Android/Browser` |
| 同周真重叠 | 卡片横向平分格子宽度、并排绘制 |
| 课程详情 | 底部弹出卡片（Bottom Sheet） |
| 周次切换 | 1–20 周循环 |
| 「其它课程」 | 忽略，不解析不显示 |
| 默认节次时间 | 22 节，40 分钟/节、5 分钟间隔，07:30 起（§7.5） |
| 持久化 | JSON 文件（桌面/Android）/ localStorage（WASM），仅保存解析后的课表 |
| 导入方式 | 粘贴 HTML 文本（不依赖文件选择器，WASM 同样可用） |

## 11. 开发验证提示（无头环境）

- 桌面截图：用 Win32 `PrintWindow(hWnd, hdc, 2)` 抓窗口内容（`CopyFromScreen` 在无头/远程会话中只能抓到桌面）。
- WASM 验证：`dotnet publish` 后用静态服务器托管 `wwwroot`（`.wasm` 要返回 `application/wasm`），无头 Chrome 需软件渲染参数：
  `chrome --headless=new --no-sandbox --use-gl=angle --use-angle=swiftshader --enable-unsafe-swiftshader --screenshot=shot.png --virtual-time-budget=60000 <url>`
  （无此参数时 WebGL 上下文不可用，画布空白；`--virtual-time-budget` 常会在 WASM 加载完成前超时，只能抓到启动页）。
- WASM 可靠截图/断言：用 `--remote-debugging-port=9222` 启动 Chrome，通过 CDP（`Page.navigate` → 轮询 `canvas.avalonia-canvas` 与 `.avalonia-splash.splash-close` → `Page.captureScreenshot`）等待应用就绪后截图；
  也可用 `Runtime.evaluate` 读写 `localStorage` 验证持久化。
- 无头会话中无法注入真实鼠标/触摸事件，滑动手势类交互需要在本机人工验证。

## 12. 发布与 CI/CD

### 12.1 工作流

| 文件 | 触发 | 内容 |
| --- | --- | --- |
| `.github/workflows/ci.yml` | push/PR 到 `master`、手动 | Core+Desktop 构建 + `SukeFlow.Tests`（50 用例）+ Android Release 构建 + WASM publish 校验 |
| `.github/workflows/pages.yml` | push 到 `master`、手动 | WASM → GitHub Pages（artifact 部署，不走 Jekyll） |
| `.github/workflows/release.yml` | tag `v*`、手动（需填 version） | Windows zip + 签名 APK + WASM zip → GitHub Release，并同步部署 Pages |

站点地址：<https://aslier16.github.io/SukeFlow/>（`index.html` 全部用相对路径，因此兼容子路径部署；**不要**加 `href="/"` 的 `<base>`）。

### 12.2 版本号

- tag 形如 `v1.2.3`（可带 `-pre` 后缀 → 预发布）。`prepare` job 计算：
  `Version` = `1.2.3`，`ApplicationDisplayVersion` = `1.2.3`，`ApplicationVersion`（versionCode）= `MA*10000+MI*100+PA`（各段 < 100）。
- 本地构建默认 `0.1.0`（`Directory.Build.props`）；CI 用命令行 `-p:Version=` 覆盖。

### 12.3 Android 签名

- 4 个仓库 Secret：`ANDROID_KEYSTORE_BASE64` / `ANDROID_KEYSTORE_PASSWORD` / `ANDROID_KEY_ALIAS` / `ANDROID_KEY_PASSWORD`；生成与上传步骤见 `docs/release-signing.md`。
- 密钥文件不入库，且必须在密码管理器/离线介质备份（丢失 = 老用户无法覆盖安装）。
- **Release 流水线会校验签名**：`apksigner verify --verbose --print-certs`，出现 `CN=Android Debug` 直接失败。
  原因：本地实测增量构建曾产出过**未签名**的 `-Signed.apk`（无 META-INF，`apksigner` 报 `DOES NOT VERIFY`），不能只看「构建成功」。
- APK 体积约 56 MB（含全量 AOT），后续优化方向见 §8。

### 12.4 仓库红线（务必遵守）

- `SukeFlow.Core/Assets/个人课表查询.html` 是**唯一入库的示例数据**，必须保持脱敏（虚构姓名/学号/教师/教学班编号）。
  原文含真实学号与教师工号，备份在仓库外 `~/.sukeflow/个人课表查询.original.html`，禁止提交。
- `SukeFlow.Core/Assets/CourseTableRef.png` 是含真实教师/教室信息的第三方截图，已加入 `.gitignore`，仅作本地 UI 参考（规格已记录在 §7）。
- 任何 `*.keystore` / `*.jks` / `*.b64` / `.idea/` / `*.DotSettings.user` 都不入库。
