# AGENTS.md

本文件适用于仓库根目录下的全部文件。后续在本项目中工作的智能体/开发者，请优先遵循本文件；若用户或系统消息给出更高优先级要求，则以更高优先级要求为准。

## 项目结构

- `JXHLJSApp.sln`：解决方案文件。
- `JXHLJSApp.csproj`：.NET MAUI Android 应用项目文件，当前目标框架为 `net8.0-android`。
- `App.xaml` / `App.xaml.cs`：应用级资源与启动入口逻辑。
- `AppShell.xaml` / `AppShell.xaml.cs`：Shell 路由、登录态切换后的页面承载与路由注册。
- `MauiProgram.cs`：MAUI 应用构建、依赖注入、日志与 HttpClient 配置。
- `Pages/`：页面与 XAML 视图。
  - `LoginPage`：登录页。
  - `RoleHomePage`：按角色展示首页功能入口。
  - `AdminPage`、`LogPage`：管理与日志相关页面。
  - `WorkOrders/`：生产工单相关页面。
- `ViewModels/`：页面 ViewModel。
- `Models/`：接口响应模型与业务 DTO。
- `Services/`：后端接口调用、日志服务与通用服务工具。
- `Tools/`：鉴权状态、响应守卫、Token 失效处理等基础工具。
- `Resources/Raw/appconfig.json`：内置接口服务配置、服务路径、接口路径与日志配置。
- `Resources/Images/`、`Resources/Fonts/`、`Resources/Styles/`：图片、字体与样式资源。
- `Platforms/`：Android、Windows、Tizen 等平台特定配置。

## 启动方式

1. 安装 .NET 8 SDK 和 MAUI Android workload。
2. 准备 Android 模拟器或真机调试环境。
3. 在仓库根目录运行构建或启动命令。
4. 如果使用 Visual Studio / Rider，可直接打开 `JXHLJSApp.sln`，选择 Android 目标设备后运行。

常用命令：

```bash
dotnet workload restore
dotnet restore JXHLJSApp.sln
dotnet build JXHLJSApp.csproj -f net8.0-android
dotnet build JXHLJSApp.csproj -f net8.0-android -c Release
```

如需通过 CLI 运行到设备，可根据本机设备 ID 使用类似命令：

```bash
dotnet build JXHLJSApp.csproj -f net8.0-android -t:Run
```

## 构建命令

- 调试构建：

```bash
dotnet build JXHLJSApp.csproj -f net8.0-android
```

- 发布/Release 构建：

```bash
dotnet build JXHLJSApp.csproj -f net8.0-android -c Release
```

- 还原依赖：

```bash
dotnet restore JXHLJSApp.sln
```

- 恢复 MAUI workload：

```bash
dotnet workload restore
```

## 测试命令

当前仓库没有独立测试项目。代码变更后至少运行以下检查：

```bash
git diff --check
dotnet build JXHLJSApp.csproj -f net8.0-android
```

如果后续新增测试项目，请优先运行：

```bash
dotnet test JXHLJSApp.sln
```

若环境缺少 `dotnet`、Android workload 或网络访问导致无法执行，请在最终回复中明确标记为环境限制，并写明具体失败原因。

## 编码规范

- 使用 C# 可空引用类型语义，新增 DTO 属性优先使用可空类型以兼容接口缺省值。
- **所有后端接口返回值中类型为数字的属性，都必须允许为空**：在 DTO / 响应模型中统一使用可空数字类型，例如 `int?`、`long?`、`decimal?`、`double?`、`float?`。不要把接口返回的数字属性定义为非可空 `int`、`long`、`decimal`、`double` 或 `float`，避免接口返回 `null`、缺省值或空数据时反序列化/业务展示异常。
- DTO 与后端 JSON 字段名保持一致，项目现有风格使用小驼峰属性名，例如 `workOrderNo`、`plannedQuantity`。
- 不要在 `using` / import 外围添加 `try/catch`。
- HTTP 接口路径优先放入 `Resources/Raw/appconfig.json` 的 `apiEndpoints`，业务服务中通过 `IConfigLoader.GetApiPath(...)` 读取，避免硬编码散落在页面层。
- 页面跳转统一通过 `AppShell.xaml.cs` 注册路由，首页入口配置应复用路由常量。
- 新增服务时在 `MauiProgram.cs` 中注册依赖注入；页面不直接 new 业务服务。
- XAML 页面保持结构清晰，列表为空时提供 EmptyView 或明确空状态提示。
- 异步接口调用使用 `async/await`，支持 `CancellationToken` 的服务方法应继续透传取消参数。
- 不要引入不必要的大型依赖；新增 NuGet 包前先确认现有依赖无法满足需求。
- 代码变更后保持格式整洁，提交前运行 `git diff --check`。

## 个人习惯与协作偏好

- 用户习惯使用中文沟通，最终回复优先使用中文。
- 用户喜欢结构化、详细的 Markdown 回复，避免只给简短纯文本。
- 做代码变更时，最终回复应包含：
  - `Summary`：概述改动，并引用相关文件与行号。
  - `Testing`：列出运行过的检查/测试命令。
- 最终回复中每条测试命令前使用状态 emoji：
  - `✅`：通过。
  - `⚠️`：因环境限制无法完成或存在非代码问题。
  - `❌`：代码或实现导致失败。
- 回答问题时，需要说明参考过的文件和终端命令。
- 涉及可运行界面且能启动应用时，视觉改动应尽量截图；如果环境无法启动，应说明原因。
- 避免使用 `ls -R` 或 `grep -R`，大型仓库中请使用 `rg`、`find`、`git status` 等更可控的命令。
- 提交前注意检查当前分支状态，不要覆盖或回滚他人的未授权改动。

## 提交流程

- 修改完成后运行必要检查。
- 使用清晰、简短的 commit message。
- 如果环境要求创建 PR，commit 后必须创建 PR；没有代码变更则不要创建 PR。
