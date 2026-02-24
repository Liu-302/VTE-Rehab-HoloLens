# VTE-HoloLens

静脉曲张 / VTE（静脉血栓栓塞）患者 AR 康复训练 **可视化**项目。患者佩戴 AR 眼镜（HoloLens），跟随虚拟人物完成下肢康复动作，本仓库负责 **AR 端的所有可视化与交互**，不包含 OpenPose 姿态捕捉逻辑。

## 项目定位

- **姿态数据来源**：由外部 OpenPose 等系统采集的 BODY_25 3D 关节点数据（`pose_keypoints_3d`），以 JSON 形式提供。
- **本仓库职责**：在 Unity 中读取上述数据，驱动虚拟人物与引导线，实现「跟着虚拟人做动作」的 AR 康复训练体验。

## 功能概览

- **虚拟人物示范**：用预录的 OpenPose 数据驱动 3D 人物模型，展示标准康复动作。
- **实时引导线**：播放动作序列时绘制下肢轨迹折线，辅助患者理解动作路径。
- **五种康复动作**（与 `StreamingAssets` 中数据对应）：
  1. 脚踝屈伸（ankle_flex_ext）
  2. 脚踝环绕（ankle_circumduction）
  3. 交替抬腿（leg_raise）
  4. 交替屈脚/背屈（ankle_alternating_flexion）
  5. 屈膝抬腿/股四头肌等长（leg_combo_kneeflex_raise）
- **两种使用模式**：
  - **采集/预览**：选择动作后播放 All Data 序列，驱动虚拟人物，用于预览或标定，不显示实时折线。
  - **训练**：自动按顺序执行上述 5 个动作，虚拟人物由 Max Data 极值姿态驱动，同时播放 All Data 驱动折线，患者可对照虚拟人与折线进行跟练。

## 技术栈

- **Unity**（含 XR）
- **Mixed Reality Toolkit (MRTK)**，面向 HoloLens 等 MR 设备
- **数据格式**：OpenPose BODY_25 的 `pose_keypoints_3d`（每帧一个 JSON 文件或单帧 JSON）

## 数据与目录结构

姿态数据放在 `Assets/StreamingAssets/` 下，本工程只读取，不实现采集与 OpenPose 推理。

| 目录 | 说明 |
|------|------|
| `All Data/<动作名>/` | 某动作的连续帧序列，每帧一个 `*_keypoints.json`，用于驱动折线及采集模式下的虚拟人。 |
| `Max Data/` | 各动作的「极值姿态」单帧 JSON（如 `SimulateAnkleFlexionExtension_FootPitchMax.json`），训练模式下用于驱动虚拟人物到目标姿态。 |

JSON 需包含 `people[0].pose_keypoints_3d`，坐标可为世界坐标；本项目中会按需转换为以 MidHip 为原点的局部坐标用于驱动骨骼。

## 主要脚本说明

| 脚本 | 作用 |
|------|------|
| `UIController` | 采集/训练的开始与停止、动作选择等 UI 逻辑。 |
| `AutoTrainingOrchestrator` | 训练模式下的动作编排：按顺序播放 5 个动作，同步 All Data（折线）与 Max Data（虚拟人）。 |
| `CollectGuideFromFiles_AllData` | 从 All Data 按动作文件夹播放帧序列，并推送到 `PoseStreamBus`。 |
| `TargetMaxPosePlayer` | 从 Max Data 加载单帧 BODY_25 JSON，驱动人物模型骨骼（或推送到 PoseStreamBus）。 |
| `LegModelDriver` | 订阅 PoseStreamBus，用当前姿态数据驱动人物腿骨。 |
| `LegLineRenderer` | 订阅 PoseStreamBus，绘制下肢实时折线。 |
| `PoseStreamBus` | 姿态数据总线，供驱动模型与折线订阅。 |

## 使用与构建

1. 用 Unity 打开工程（版本需支持当前 XR 与 MRTK 配置）。
2. 确保 `StreamingAssets` 下已放置好 All Data / Max Data 的 JSON 数据。
3. 在场景中配置好 `UIController`、`AutoTrainingOrchestrator`、各 Player 与 Bus 的引用。
4. 运行后通过 UI 进入「采集」或「训练」模式，患者佩戴 HoloLens 跟随虚拟人与折线进行康复训练。

构建目标为 HoloLens 时，请在 Unity 中切换对应平台并安装 Windows Mixed Reality 相关依赖。

## 说明与免责

- 本仓库 **不包含** OpenPose 或任何姿态估计算法，仅消费已有的关节点 JSON。
- 临床使用前请遵循当地医疗器械与临床验证规范。

## 许可证

见仓库内 LICENSE 文件（如有）。
