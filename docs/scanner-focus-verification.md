# PDA 扫码对焦验证

## 实现范围

- 保留 .NET 8 和 ZXing.Net.Maui 0.4.0，Android 扫码控件改用专用 CameraX Handler，继续复用 ZXing 解码器。
- 预览使用 COMPATIBLE 模式，进入 Streaming 状态后对预览中心执行 AF/AE；对焦不阻塞二维码解码。该模式为 [CameraX 官方建议](https://developer.android.com/reference/androidx/camera/view/PreviewView#getPreviewStreamState())的精确预览就绪信号方案。
- 点击预览可指定对焦位置，使用 PreviewView 的坐标转换处理预览缩放与旋转。
- 单次对焦两秒后自动取消锁定，恢复设备支持的连续对焦。未识别时最多进行两次间隔三秒的自动重试；正在执行对焦或刚进行手动对焦时跳过本次重试。
- 固定焦距设备保留解码和扫码枪输入；能力查询或对焦失败不会终止解码。
- 离开扫码页、进入相册或扫码完成时，取消对焦、停止分析并仅解绑当前控件的相机用例。恢复页面时重新订阅识别事件。
- 对有行填充或像素间隔的 Y 平面进行紧密排列，防止部分 PDA 的图像数据错行。
- 解码前根据 CameraX 的 `ImageInfo.RotationDegrees` 校正图像方向，90/270 度时同步交换宽高；使用纯托管灰度图像源并保留四向自动旋转，QR 检测器负责识别任意平面倾角。
- 仅在解码选项变化时更新配置，避免每帧重置解码器状态。
- 监听 PDA 的物理四向（包含界面不翻转的 180 度倒置），在象限边界使用约 10 度滞回。Analysis 跟随物理方向，Preview 独立跟随实际屏幕方向；布局变化和监控补同步延迟到达的屏幕旋转。
- 象限切换后取消旧对焦，等待 350 毫秒稳定后重新中心对焦。应用层三秒超时释放未完成的对焦请求，旧回调不能取消新的请求。
- 每两秒记录前台帧流状态。前台且预览 Streaming 时，六秒无帧/处理未完成或连续十次帧异常触发相机用例重建，每次扫码最多恢复两次。后台和预览 Idle 不消耗恢复预算，自动重建不重置预算。
- 每次绑定拥有独立的解码器、互斥锁及帧统计，退出时取消方向监听、布局事件、监控、稳定等待及对焦任务。迟到的旧帧不能更新新相机会话的心跳，卡住的旧解码器不会占用新会话的锁。

## 自动检查

在 PowerShell 7 中运行，无需还原额外测试依赖：

```powershell
./Tests/CameraLuminanceBuffer.Tests.ps1
./Tests/CameraBarcodeRotation.Tests.ps1
./Tests/ScannerOrientationTracker.Tests.ps1
./Tests/ScannerRecoveryPolicy.Tests.ps1
git diff --check
dotnet build JXHLJSApp.csproj -f net8.0-android
```

如果本机无法自动发现 SDK，可显式指定路径；下面是本次验证使用的路径及独立输出目录：

```powershell
dotnet build JXHLJSApp.csproj -f net8.0-android -r android-arm64 `
  '-p:AndroidSdkDirectory=D:/Program Files (x86)/Microsoft Visual Studio/Shared/Android/android-sdk' `
  -p:IntermediateOutputPath=obj/ScannerDeviceRotation/ `
  -p:OutputPath=bin/ScannerDeviceRotation/
```

Debug 配置现已设置 `EmbedAssembliesIntoApk=True`，构建出的 APK 内嵌托管程序集，可以复制安装。请使用 `bin/ScannerDeviceRotation/com.jxhljs.app-Signed.apk`，并在进入扫码页后确认日志包含本次代码标识 `pda-device-rotation-20261028`。安装签名不兼容时使用现场既有签名构建，保留设备上的业务数据。

方向回归测试直接调用生产代码 `CameraBarcodeDecoder`，生成真实二维码和 Code128 图像。覆盖非正方形帧的逐像素旋转、二维码 0–359 度与四种相机旋转元数据的 1440 个组合、一维码四向的 16 个组合、带行填充的倾斜二维码以及空白帧后的解码恢复。测试使用全部码制配置，旋转倾角只在测试中生成，生产代码不会逐帧枚举 360 个角度。

方向状态测试直接使用生产 `ScannerOrientationTracker`，881 条断言覆盖四种初始屏幕方向、正反全圆旋转、倒置、快速反转、边界抖动及未知方向。恢复策略测试直接使用生产 `ScannerRecoveryPolicy`，2421 条断言覆盖六秒/十次错误边界、后台和 Idle、处理卡住、两次恢复预算与用户退出重置。这些测试不能替代下列真机旋转链路验收。

## 真机验收

| 场景 | 检查内容 |
| --- | --- |
| 首次进入扫码页 | 预览出现后自动对焦；清晰二维码可被识别，对焦尚未完成也不阻止识别 |
| 标签距离由远到近、由近到远 | 焦点随距离调整；两秒后不持续锁定旧焦距 |
| 点击预览不同位置 | 对焦位置与点击一致，提示显示正在对焦及结果 |
| 二维码正放、横放、倒放及斜放 | 无需摆正二维码即可识别；同步测试 PDA 本身旋转后的方向 |
| 二维码固定，进入扫码后再转 PDA | 先对准空白区域，再将 PDA 转到 90/180/270 度并对准同一标签；无需退出页面即可扫码 |
| 先横放/倒放 PDA，再进入扫码 | 与进入扫码后再旋转的结果一致 |
| 关闭系统自动旋转后倒置 PDA | 界面保持竖直，分析方向仍随物理方向变化，二维码可识别 |
| 持续不放二维码 | 自动重试次数有限，不持续反复拉焦 |
| 扫码后立即返回并重新扫码 | 不发生旧回调跳转、重复上机或摄像头被占用 |
| 连续点击取消、系统返回键 | 页面退出且不再触发对焦或识别回调 |
| 相册取消、无二维码图片、相册识别成功 | 相机能正确恢复或关闭，恢复后仍能收到识别事件 |
| 应用进入后台再恢复 | 预览恢复后重新对焦，无后台对焦重试 |
| 固定焦距设备或拒绝相机权限 | 无异常退出，扫码枪及手动输入仍可使用 |
| 同一设备连续进出扫码页 20 次 | 无黑屏、线程持续增长或相机资源无法释放 |

同一台 PDA、同一二维码、相同光照分别记录修改前后多次识别耗时，区分首次启动与再次进入。实际改善需要以真机结果为准。

## 日志定位

应用现有 Serilog 日志增加以下事件，不记录二维码内容：

- `Scanner first frame`：开始启动到首帧的毫秒数。
- `Scanner focus completed`：单次对焦是否成功及耗时。
- `Scanner decoded`：本次启动到解码成功的毫秒数。
- `Scanner camera initialization failed` / `Scanner camera release failed`：初始化或释放异常。
- `Scanner session started`：本次代码标识、程序集 MVID、绑定代数及已使用的恢复次数，用于核对现场代码版本。
- `Scanner device rotated` / `Scanner rotations synchronized`：物理方向、预览及分析目标方向（0/1/2/3 是 Surface 旋转常量，并非度数）。
- `Scanner analysis status`：收到帧数、已处理帧数、异常数、预览状态、相机状态、目标方向、待完成对焦及最后帧/完成时间间隔。每两秒输出，后台跳过。
- `Scanner frame layout`：格式、尺寸、裁剪区域、旋转元数据、row/pixel stride 及 buffer position/limit/remaining；首次及方向切换后输出。
- `Scanner frame analysis failed`：可进入现有 Information 级别日志的 Warning，异常最多每五秒记录一次，避免逐帧刷日志。
- `Scanner focus timed out` / `Scanner analysis stalled` / `Scanner automatic recovery exhausted`：对焦超时、有限相机恢复及预算用尽。

这些时间只涵盖相机与解码阶段，不包含机台绑定或工单查询请求。
