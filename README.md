# SukeFlow

轻量级跨平台课程表应用：解析**正方教务系统**「个人课表查询」页面 HTML，用自绘控件展示 7 天 × 22 节的周课表。

Windows 桌面 · Android · 浏览器（WASM）三端同源，纯本地数据，不联网、不上传。

<p>
  <a href="https://github.com/Aslier16/SukeFlow/releases/latest"><img alt="Release" src="https://img.shields.io/github/v/release/Aslier16/SukeFlow?include_prereleases&sort=semver"></a>
  <a href="../../actions/workflows/ci.yml"><img alt="CI" src="https://github.com/Aslier16/SukeFlow/actions/workflows/ci.yml/badge.svg"></a>
  <a href="LICENSE"><img alt="License" src="https://img.shields.io/badge/license-MIT-blue.svg"></a>
</p>

## 下载

| 平台 | 获取方式 | 说明 |
| --- | --- | --- |
| **Web（推荐先试）** | <https://aslier16.github.io/SukeFlow/> | 无需安装；课表存在浏览器 `localStorage`。首次加载较大（内嵌中文字体），请耐心等待 |
| **Windows** | [Releases](https://github.com/Aslier16/SukeFlow/releases/latest) → `SukeFlow-*-win-x64.zip` | 解压即用（自包含，无需安装 .NET）。未做代码签名，首次运行可能提示 SmartScreen |
| **Android** | [Releases](https://github.com/Aslier16/SukeFlow/releases/latest) → `SukeFlow-*-android.apk` | 需在系统设置中允许「安装未知来源应用」（未上架应用商店） |

## 功能

- **导入课表**：在教务系统打开「个人课表查询」，页面另存为网页后全选复制 HTML，粘贴进应用「导入」面板即可；解析结果自动本地保存。
- **周课表**：7 天 × 22 节，节次时间可配置（默认 40 分钟/节 + 5 分钟间隔，07:30 起）。
- **周次切换**：左右滑动（触屏）/ 滚轮 / 顶栏按钮 / 方向键，1–20 周循环；显示非本周时卡片淡化并带「非本周」徽章。
- **课程详情**：点击课程卡片从底部弹出详情（教师、地点、周次、教学班、学分/学时、考核方式等）。
- **同格重叠**：单双周交叠课程按「当周优先」绘制；同周真重叠时并排平分格子宽度。
- 单双周、调课（`【调】`）、类型标记（★讲课 ☆实验 □实践 ■实践周 〇课外 ◆上机）均按原文解析；同一门课的多个时段自动归并。

## 数据与隐私

- 课表只保存在本机：桌面/Android 为应用目录下的 `data/timetable.json`，Web 为 `localStorage['sukeflow.timetable.json']`。
- 应用**不发起任何网络请求**，不采集任何信息。
- 仓库内置的示例课表 HTML 已做脱敏处理（虚构姓名/学号/教师），仅用于演示与解析测试。

## 从源码构建

需要 .NET 10 SDK（见 `global.json`）；构建 Android / WASM 需对应 workload：

```bash
dotnet workload install android wasm-tools

dotnet run --project SukeFlow.Desktop            # 桌面运行
dotnet test                                      # 解析器等单元测试

dotnet publish SukeFlow.Browser/SukeFlow.Browser.csproj -c Release -o publish   # WASM → publish/wwwroot
dotnet publish SukeFlow.Android/SukeFlow.Android.csproj -c Release -f net10.0-android
```

发布流水线见 `.github/workflows/`：`ci.yml`（构建 + 测试）、`pages.yml`（WASM → GitHub Pages）、`release.yml`（打 tag 时产出 Windows zip + Android APK）。
Android 发布签名步骤见 [docs/release-signing.md](docs/release-signing.md)。

## 项目结构

```
SukeFlow.Core/      模型 / 解析器 / 持久化 / CourseTable 自绘控件 / ViewModel（无平台依赖）
SukeFlow.Desktop/   Windows / Linux / macOS 入口
SukeFlow.Android/   Android 入口
SukeFlow.Browser/   WASM 入口
SukeFlow.Tests/     单元测试（解析器 / 模型）
```

技术栈：Avalonia 12 + CommunityToolkit.Mvvm，手写 HTML 解析器（不引入第三方解析库，兼容 WASM/AOT）。

## 许可

- 本项目代码：[MIT](LICENSE)。
- 内嵌字体 [Noto Sans SC](SukeFlow.Core/Assets/Fonts/LICENSE-OFL.txt)：SIL Open Font License 1.1；拉丁字形使用 Inter（Avalonia 内置包）。

> 本应用为个人项目，与任何学校或教务系统厂商无关。示例数据为虚构内容。
