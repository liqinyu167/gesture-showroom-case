# 展厅交互系统架构

## 分层结构

```
HandInputAdapter.cs            ← Layer 2: 手势输入适配层（SDK 无关）
    ↓ OnGrabStart / OnGrabDrag / OnGrabEnd
ShowroomInteractionManager.cs  ← Layer 3: 业务逻辑层（点击/拖拽/缩放判断）
    ↓ 通过 IHandInteractable 接口调用
InteractableItem.cs            ← Layer 4: 展品层（3D 物品）
VideoExhibit.cs / ...          ← Layer 4: 其他展品类型（待扩展）
    ↑ Register / Unregister
ShowroomRegistry.cs            ← Layer 5: 全局状态层（单例，无静态变量）
    ↑↓ SetFocus / ClearFocus
ItemObserver.cs                ← 观察模式控制器（附加在展品 GameObject 上）
```

## 脚本职责

| 脚本 | 职责 |
|------|------|
| `IHandInteractable.cs` | 展品行为接口契约，所有展品必须实现 |
| `HandInputAdapter.cs` | 将 MediaPipe 原始捏合事件封装为高层意图事件 |
| `ShowroomInteractionManager.cs` | 管理光标、UI 射线、3D 射线、双击检测、双手缩放 |
| `ShowroomRegistry.cs` | 注册所有展品，管理当前"聚焦"的展品（观察模式） |
| `InteractableItem.cs` | 实现 IHandInteractable，处理旋转/缩放/颜色反馈 |
| `ItemObserver.cs` | 附加在展品上，控制双击进入/退出观察模式 |
| `ShowroomCursor.cs` | 手部光标 UI 渲染（捏合时变色） |
| `ModernUIBuilder.cs` | 展品信息面板的玻璃拟态 UI 生成 |

## 新增展品类型（只需 2 步）

```csharp
// 1. 创建新脚本实现接口
public class VideoExhibit : MonoBehaviour, IHandInteractable
{
    public string DisplayName => "产品视频";

    public void OnGrabbed()           { /* highlight */ }
    public void OnReleased()          { /* reset */    }
    public void OnRotate(Vector2 d)   { /* rotate */   }
    public void OnScale(float delta)  { /* resize */   }
    public void OnSingleClick()       { /* play/pause */ }
    public void OnHandDoubleClick()   { /* fullscreen */ }
    public void OnHoverEnter()        { }
    public void OnHoverExit()         { }
}

// 2. 挂载到 GameObject 即可，ShowroomInteractionManager 无需修改
```

## 关键参数（Inspector 可调）

### ShowroomInteractionManager
- `ClickDragThreshold` (150) — 点击判定最大拖拽距离（px），太小易误触
- `DoubleClickTimeWindow` (0.7s) — 双击时间窗口，手势跟踪建议 ≥ 0.5s

### ItemObserver
- `ObservationDistance` (8) — 观察模式物品到摄像机距离
- `MinObservationDistance` (2) / `MaxObservationDistance` (15) — 双指缩放范围
- `ZoomSensitivity` (0.005) — 双指缩放灵敏度
- `TransitionSpeed` (5) — 物品飞入/飞出动画速度

### HandTrackingManagerV2
- `PinchDetectionThreshold` (0.05) — 捏合判定阈值（归一化距离）
- `PinchReleaseToleranceTime` (0.15s) — 防抖：松开后延迟多久再判定释放
- `HandLossToleranceTime` (0.3s) — 手消失后延迟多久清除状态
