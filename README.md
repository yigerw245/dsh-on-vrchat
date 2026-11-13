# 大肥鱼 · DSH × VRChat Demo

这是一个把 DeepSeek Harness（DSH）接入 VRChat 世界 NPC 的 **Demo**，用于在同一台 Windows 电脑上做本地测试。你可以在游戏里的随身对话框打字，或从本机网页用麦克风讲话。大肥鱼会用字幕和中文配音回复。

![大肥鱼在 VRChat 本地测试世界中的演示画面](大肥鱼测试场景.png)

仓库提供 Unity 工程和可以直接生成的蓝鲸测试场景。上图是安装可选的 Kipfel 角色后的效果。走路、坐下、触摸、双手抱起和落地缓冲等骨骼动作需要本地导入该角色。

## 跑起 Demo

1. 安装 [VRChat Creator Companion（VCC）](https://vcc.docs.vrchat.com/guides/getting-started/) 和 Unity **2022.3.22f1**。在 VCC 的 **Projects → Add Existing Project** 添加本仓库里的 `unity` 文件夹，按提示恢复 Worlds SDK（工程记录的版本是 **3.10.6**），再打开工程。
2. 安装 Python **3.10+**。在仓库根目录运行 `start-npc.cmd --demo`，即可用固定回复测试字幕和配音。要接入真实对话，先在 DeepSeek Harness 登录并启用 `dsh` 命令，再运行 `start-npc.cmd`。配音使用 Windows 的中文语音包。
3. 在 Unity 选择 **Tools → Dafeiyu → Open Test World**。首次会生成蓝鲸场景。在 VRChat 设置中开启 **Allow Untrusted URLs**，再从 Unity 的 **VRChat SDK → Show Control Panel → Builder → Build & Test** 进入本地世界。
4. 桌面模式按 **F9** 打开聊天框。VR 模式扣一下**左手食指扳机**打开面板，再用右手扳机点击输入框和「发送」。也可以打开[本机对话页面](http://127.0.0.1:17821/)打字或录音。

麦克风转文字需要 `faster-whisper`。可在仓库根目录运行 `python -m venv .venv`，再运行 `.venv\Scripts\python.exe -m pip install faster-whisper`。首次录音会下载 `small` 模型。键盘聊天和 `--demo` 可以直接用 Python 运行。

## 本地导入 Kipfel（可选）

1. 准备你有权使用的 `大肥鱼kipfel.zip`，放到仓库根目录。这个导入脚本适用于含有 `Assets/deep.unity` 的大肥鱼 Unity 源项目压缩包。Kipfel 原版模型可从[作者的 BOOTH 页面](https://mukumi.booth.pm/items/5813187)获取。
2. 运行 `python import_kipfel.py`，把模型提取到本地的 `unity/Assets/DafeiyuKipfel`。在 VCC 中安装 [lilToon](https://lilxyzw.github.io/lilToon/)（原工程使用 **2.3.4**）。
3. 在 Unity 里先运行一次 **Tools → Dafeiyu → Open Test World**，再选择 **Tools → Dafeiyu → Install Kipfel Model in Test World**，然后重新 **Build & Test**。

装好后可以对大肥鱼说「到我跟前来吧」「歇一下」或「起来吧」。它会参考距离、姿态和附近路况来决定动作。在 VR 里可以摸头、牵手、抓住肩膀轻晃，或用双手从两侧腋下托起。抱起时它会朝向你，放开后触地弯膝缓冲。

## 实现与素材

游戏内对话和简要环境状态写入 VRChat 本地测试日志，由 Python 服务交给 DSH。世界约每六秒读取一次回复。本机语音识别生成的文本会交给 DeepSeek，对话记录保存在 `.dsh-vrchat/`。 [VRChat 本地测试日志](https://creators.vrchat.com/worlds/udon/debugging-udon-projects/) · [字符串下载](https://creators.vrchat.com/worlds/udon/string-loading/)

项目代码使用 MIT 许可证。字体保留各自的 SIL OFL 许可证。步行采样来自 Quaternius 的 CC0 动画 `Walk_Formal_Loop`，并按 Kipfel 的腿长和移动速度调整。 [Universal Animation Library](https://quaternius.com/packs/universalanimationlibrary.html)
