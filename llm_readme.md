# eComBox 项目概览 - LLM 上下文文档

本文档为 AI 智能体提供项目的全面概览，便于快速理解项目结构、技术栈和开发进度。

---

## 📋 项目基本信息

| 项 | 值 |
|---|---|
| **应用名称** | eComBox |
| **版本** | 0.4.1.0 |
| **类型** | Windows UWP 工具箱应用 |
| **发行商** | ecomter |
| **官方仓库** | https://github.com/ecomter/eComBox |
| **主要开发者** | ecomter |
| **多语言支持** | 中文（简体）、英文 |
| **目标用户** | Windows 10/11 用户 |

---

## 🏗️ 技术栈

| 技术 | 版本/说明 |
|---|---|
| **应用框架** | UWP (Universal Windows Platform) |
| **编程语言** | C# + XAML |
| **UI 框架** | Microsoft.UI.Xaml |
| **最低 Windows 版本** | 10.0.18362.0 |
| **目标 Windows 版本** | 10.0.26100.0 |
| **IDE** | Visual Studio 2026 |
| **架构模式** | MVVM-lite（页面级ViewModel） |
| **本地存储** | JSON 文件 (ApplicationData.Current.LocalFolder) |

---

## 📁 项目结构详解

```
eComBox/
│
├── Views/                              # UI 页面层
│   ├── TimeCounter.xaml(.cs)          # ⭐ 日期倒计时（核心功能）
│   ├── GeometryPage.xaml(.cs)         # 几何计算（planning）
│   ├── translatorPage.xaml(.cs)       # 英文翻译（进行中 89%）
│   ├── FloatingCardPage.xaml(.cs)     # 浮窗卡片展示
│   ├── HomePage.xaml(.cs)             # 应用主页
│   ├── SettingsPage.xaml(.cs)         # 设置页面
│   ├── betaPage.xaml(.cs)             # Beta 测试功能
│   ├── ShellPage.xaml(.cs)            # 应用壳层（NavigationView）
│   ├── CustomDialog.xaml(.cs)         # 自定义对话框
│   ├── FirstRunDialog.xaml(.cs)       # 首次运行对话
│   └── WhatsNewDialog.xaml(.cs)       # 更新说明对话
│
├── Services/                           # 业务逻辑层
│   ├── CountdownStorageService.cs     # 倒计时数据持久化（JSON）
│   ├── AIService.cs                   # AI 日期预测（接入 阿里云 qwen-turbo）
│   ├── AzureDatePredictionService.cs  # 已迁移为 QwenDatePredictionService（本地 AI 服务）
│   ├── NavigationService.cs           # 页面导航管理
│   ├── ThemeSelectorService.cs        # 主题选择（暗黑/亮色）
│   ├── ToastNotificationsService.cs   # 系统 Toast 通知
│   ├── ActivationService.cs           # 应用激活处理
│   ├── StartupService.cs              # 启动时初始化
│   ├── WindowManagerService.cs        # 窗口管理
│   ├── FirstRunDisplayService.cs      # 首次运行显示
│   ├── WhatsNewDisplayService.cs      # 更新内容显示
│   ├── StoreService.cs                # Microsoft Store 集成
│   ├── AIUsageService.cs              # AI 使用统计
│   ├── ConfigurationService.cs        # 配置管理
│   ├── ViewLifetimeControl.cs         # 视图生命周期控制
│   └── IAIService.cs                  # AI 服务接口
│
├── Models/                             # 数据模型
│   └── CountdownCardModel.cs          # 倒计时卡片数据结构
│
├── Tasks/                              # 后台任务
│   └── DateNotificationBackgroundTask.cs # 定时日期提醒任务
│
├── Helpers/                            # 辅助工具
│   ├── ResourceExtensions.cs          # 资源扩展方法
│   ├── NavHelper.cs                   # 导航辅助
│   ├── LoggingHelper.cs               # 日志工具
│   ├── EnumToBooleanConverter.cs      # 枚举到布尔转换器
│   └── SettingsStorageExtensions.cs   # 设置存储扩展
│
├── Behaviors/                          # XAML 行为
│   ├── NavigationViewHeaderBehavior.cs # 导航视图头部行为
│   └── NavigationViewHeaderMode.cs    # 头部模式枚举
│
├── Activation/                         # 应用激活处理
│   ├── ActivationHandler.cs           # 基础激活处理器
│   └── DefaultActivationHandler.cs    # 默认激活处理
│
├── Styles/                             # 全局样式资源
│   ├── _Colors.xaml                   # 色彩系统定义
│   ├── _FontSizes.xaml                # 字体尺寸（大 24px，中 16px）
│   ├── _Thickness.xaml                # 厚度/间距定义
│   ├── TextBlock.xaml                 # 文本块样式
│   └── Page.xaml                      # 页面通用样式
│
├── Strings/                            # 多语言资源
│   ├── zh-Hans-CN/Resources.resw      # 中文资源
│   └── en-us/Resources.resw           # 英文资源
│
├── Assets/                             # 应用资源
│   ├── APP.png                        # 应用图标
│   ├── Header-*.png                   # 页面头图
│   ├── *.scale-*.png                  # 多分辨率资源
│   └── StoreLogo.png                  # Store 标志
│
├── Properties/                         # 项目属性
│   ├── AssemblyInfo.cs
│   └── Default.rd.xml                 # 反射定义（.NET Native）
│
├── App.xaml(.cs)                       # 应用程序入口
├── Package.appxmanifest               # UWP 应用清单
├── eComBox.csproj                     # 项目文件
├── README.md                           # 项目说明（中文）
├── ecombox.pfx / eComBox_TemporaryKey.pfx  # 签名证书
└── Settings.XamlStyler               # XAML 格式化配置
```

---

## ⭐ 核心功能模块详解

### 1️⃣ 日期倒计时 (TimeCounter) - 最成熟功能 ✅

**路径**: `Views/TimeCounter.xaml(.cs)`

**功能特性**:
- ✅ **双视图系统**
  - 卡片视图 (GridView)：3 列响应式布局
  - 列表视图 (ListView)：条纹展示
  - 视图状态持久化（用户选择自动保存）

- ✅ **智能日期预测 AI**
  - 输入事件名称，自动推荐最可能的日期
  - 支持本地预测 (AzureDatePredictionService) 和云端 AI (AIService)
  - 显示预测置信度和原因

- ✅ **数据管理**
  - 创建、编辑、删除倒计时
  - 导入/导出 JSON 格式
  - 全量清空功能

- ✅ **排序和筛选**
  - 按日期从近到远排序（默认）
  - 按事件名称 A-Z 排序
  - 已过期项目标记

- ✅ **卡片显示逻辑**
  - **有自定义名称**: 显示事件名 + 目标日期行
  - **仅日期（无名称）**: 显示日期作为标题，隐藏副行日期（新增功能）
  - 日期型标题增大 4px (16→20)，视觉区分

- ✅ **倒计时计算**
  - "还有 N 天" / "就是今天" / "已过 N 天"
  - 相对时间提示："明天"、"后天"、"1月内" 等

- ✅ **通知功能**
  - 日期通知开关（每卡片独立）
  - 后台定时任务提醒
  - Toast 系统通知

- ✅ **UI 美化**
  - 8 种渐变色主题（极光青蓝、落日暖橙、星夜深蓝等）
  - 响应式卡片尺寸（自适应窗口宽度）
  - 等高行排列（同行卡片高度一致）
  - 流畅进入/退出动画

- ✅ **数据持久化**
  - JSON 文件存储于 `ApplicationData.Current.LocalFolder`
  - 自动同步通知设置
  - 线程安全（SemaphoreSlim）

**关键类**:
- `TimeCounterCardViewModel` - 卡片视图模型
- `CountdownStorageService` - 数据持久化
- `QwenDatePredictionService` - 日期预测（已从 Azure 迁移至阿里云 qwen-turbo）

---

### 2️⃣ AI 日期预测 (AIService) - 计划中

**路径**: `Services/AIService.cs`, `Services/AzureDatePredictionService.cs`

**接口**: `IAIService`

---

### 3️⃣ 几何计算 (GeometryPage) - 计划中 ⚪

**路径**: `Views/GeometryPage.xaml(.cs)`

**计划功能**:
- ⚪ 直线和圆的相交、相切、距离计算
- ⚪ 圆锥曲线（椭圆、双曲线、抛物线）分析

**状态**: 尚未实现

---

### 4️⃣ 英文翻译 (translatorPage) - 进行中 🔶

**路径**: `Views/translatorPage.xaml(.cs)`

**完成度**: 89%

**预期功能**:
- 英文翻译工具
- 可能使用在线翻译 API

---

### 5️⃣ 浮窗功能 (FloatingCardPage) - 辅助 ✅

**路径**: `Views/FloatingCardPage.xaml(.cs)`

**功能特性**:
- ✅ 倒计时卡片浮窗展示
- ✅ 从主应用固定到浮窗

---

### 6️⃣ 设置页面 (SettingsPage) - 配置中心 ✅

**路径**: `Views/SettingsPage.xaml(.cs)`

**功能特性**:
- ✅ 主题选择（暗黑/亮色/系统跟随）
- ✅ 应用配置管理
- ✅ 关于应用信息

---

## 🔧 数据模型

### CountdownCardModel
```csharp
public class CountdownCardModel
{
	public int Title { get; set; }                    // 卡片唯一 ID
	public string TaskName { get; set; }              // 事件名称（可为空）
	public DateTime? TargetDate { get; set; }         // 目标日期
	public string DisplayText { get; set; }           // 倒计时显示文本
	public string BorderColorHex { get; set; }        // 主题色（gradient:* 格式）
	public bool EnableDateNotification { get; set; }  // 通知开关
}
```

---

## 💾 数据持久化

### 本地存储位置
- **主数据**: `ApplicationData.Current.LocalFolder/data.json`
- **日期历史**: `ApplicationData.Current.LocalFolder/date_history.json`

### 存储服务
- `CountdownStorageService` - 同步的 JSON 持久化
- 线程安全（`SemaphoreSlim`）
- 原子操作（临时文件 + 文件复制）

---

## 🎨 主题系统

### 色彩方案
- **极光青蓝** (gradient:aurora) - 默认
- **落日暖橙** (gradient:sunset)
- **星夜深蓝** (gradient:starry)
- **森林青绿** (gradient:forest)
- **薰衣草紫** (gradient:lavender)
- **深海蓝** (gradient:ocean)
- **糖果粉蓝** (gradient:candy)
- **节日红金** (gradient:festive)

### 字体尺寸
- **大** (`LargeFontSize`): 24px
- **中** (`MediumFontSize`): 16px
- **日期标题特例**: 20px（增大 4px）

### 暗黑/亮色模式
- 完整支持系统主题切换
- `ThemeSelectorService` 管理

---

## 🚀 后台能力

### 后台任务
- **DateNotificationBackgroundTask**
  - 触发器：系统事件 + 计时器
  - 功能：定时检查倒计时，触发通知

### 通知系统
- `ToastNotificationsService` - 系统 Toast 通知
- 支持动作按钮

### 启动任务
- 应用可配置开机自启（已在 manifest 中声明）

---

## 📡 外部服务集成

### Azure 服务
- **Azure Cognitive Services** - 日期预测 AI
- 通过 `AzureDatePredictionService` 调用

### Microsoft Store
- `StoreService` - Store 集成（评分、链接等）

---

## 🌍 多语言支持

### 资源文件
- **中文** (`Strings/zh-Hans-CN/Resources.resw`)
- **英文** (`Strings/en-us/Resources.resw`)

### 本地化字符串
- 页面标题、按钮文本、错误提示等
- 通过 `.GetLocalized()` 扩展方法访问

---

## 🏢 应用清单

### Package.appxmanifest
- 应用身份：`12814ecomter.eComBox`
- 发布者：`CN=ecomter`
- 版本：0.4.1.0

---

## 📊 功能完成度矩阵

| 模块 | 状态 | 完成度 | 备注 |
|------|------|--------|------|
| 日期倒计时 | ✅ 完成 | 100% | 最稳定，支持双视图、AI 预测 |
| AI 日期预测 | 🔶 进行中 | |  |
| 英文翻译 | 🔶 进行中 |  | UI 框架完成，功能集成中 |
| 浮窗卡片 | ✅ 完成 | 100% | 支持将倒计时固定到浮窗 |
| 设置页面 | ✅ 完成 | 100% | 主题切换、配置管理 |
| 直线和圆 | ⚪ 计划中 | 20% | 尚未开始 |
| 圆锥曲线 | ⚪ 计划中 | 0% | 尚未开始 |
| 自定义背景 | ⚪ 计划中 | 0% | 尚未开始 |

---


## 🔄 版本历史

### v0.4.1 (当前)
- ✨ AI 日期预测功能
- 🎯 TimeCounter UI 优化
- 🐛 日期型条目显示修复

### v0.3.3
- 新"设置"页面
- 页面宽度上限
- 日期计数器导入/导出
- 暗色主题适配修复

### v0.0.1 - v0.3.2
- 日期倒计时核心功能
- 基础 UI 框架
- 本地存储

---

## 🎯 开发指南

### 常见任务

#### 添加新功能页面
1. 在 `Views/` 创建 `.xaml` 和 `.xaml.cs`
2. 继承 `Page`，实现 `INotifyPropertyChanged`
3. 在 `NavigationService` 注册路由
4. 添加导航菜单项

#### 修改 TimeCounter 功能
1. 编辑 `Views/TimeCounter.xaml(.cs)`
2. 若涉及 ViewModel，修改 `TimeCounterCardViewModel`
3. 若涉及数据，修改 `CountdownStorageService`
4. 测试视图状态恢复和排序

#### 添加新语言
1. 复制 `Strings/zh-Hans-CN/Resources.resw` 为新语言版本
2. 翻译所有字符串
3. 在 `Package.appxmanifest` 中声明语言

#### 扩展 AI 预测
1. 实现 `IAIService` 接口
2. 在 `AIService` 集成新的预测引擎
3. 测试降级逻辑

---

## 🐛 已知问题

- 英文翻译功能未完成（89% 进度）
- 几何计算功能待实现
- 自定义背景功能待规划

---

## 📚 参考资源

- **UWP 开发** https://docs.microsoft.com/windows/uwp/
- **WinUI 3** https://microsoft.github.io/microsoft-ui-xaml/
- **MVVM 模式** https://docs.microsoft.com/windows/uwp/data-binding/pattern-matching
- **项目仓库** https://github.com/ecomter/eComBox

---

## 📞 联系方式

- **开发者**: ecomter
- **GitHub**: https://github.com/ecomter
- **仓库问题**: https://github.com/ecomter/eComBox/issues

---

**文档最后更新**: 2026 年 (基于 v0.4.1)  
**用途**: AI 智能体项目理解和上下文补充  
**维护者**: 开发团队
