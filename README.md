---

# 一贴良药

> 一款以中医问诊与制药为主题的 2D 叙事 + 节奏玩法独立游戏：玩家在主场景经营医馆，接诊病人、辨证施治、按节奏捣药，并在治愈后触发后续回访剧情。

## 游戏定位

- **类型**：剧情向模拟 / 轻节奏（Rhythm）  
- **平台**：PC（Unity 单机）  
- **体量**：多场景流程完整闭环，适合作为个人作品集 / 实习 Demo 展示  

## 核心玩法循环

### 主场景（MainScene）
医馆日常、背包与礼物、休息推进天数、新手引导入口。

### 诊所（Clinic）
按游戏日刷新候诊病人；选择病人进入诊断。部分病人在治愈后会在约定日期回馆道谢，弹出预告 Panel，点击后播放感谢对话。

### 诊断（Diagnosis）
播放病人问诊对话（文字打字机 + 语音 + 表情立绘）；玩家根据情绪类型完成辨证（如怒、喜、思、悲、恐等）。

### 制药（MakeHerb）
节奏小游戏：圆环收缩与音乐节拍对齐，判定 Perfect / Good / Bad / Miss；达标则治疗成功，失败可重试或重新选药。

### 药柜与方剂（Herb）
基于病人情绪匹配默认方剂（君臣佐使思路），选择草药后进入节奏关。

## 系统设计亮点

- **数据驱动**：病人、对话、节奏谱、成功台词等使用 ScriptableObject 配置，策划可在 Inspector 扩展内容。  
- **通用对话系统**：DialogueManager 封装线性 Line[] 播放（打字机、点击跳过、语音同步），诊断 / 治愈 / 道谢 / 新手引导复用同一套逻辑。  
- **跨场景状态**：GameManager + CurrentPatient 维护当前病人、治疗进度、日历与道谢排程。  
- **后续事件**：治愈后按规则写入 pendingThankYouVisits，进诊所时清理过期事件并播放道谢（双 Panel：预告 + 对话）。  
- **存档**：SaveManager 使用 JsonUtility 持久化治疗记录、背包、天数、新手标记、道谢排程等。  
- **新手引导**：MainsceneGuide 分场景分阶段引导（主场景 → 诊所 → 诊断 → 制药），与首次标记联动。  

## 技术栈

| 类别 | 技术 / 工具 |
|------|-------------|
| 引擎 | Unity 2022.3 LTS |
| 语言 | C# |
| UI | UGUI（Canvas / CanvasGroup / Button）、TextMeshPro |
| 数据 | ScriptableObject（病人、草药、对话、节奏谱）、可序列化 Line[] |
| 架构 | 单例常驻（GameManager、SaveManager、AudioManager、GlobalMedicineCabinet）；工具类 DialogueManager（非单例、参数注入） |
| 异步与流程 | Coroutine（对话、场景加载、节奏生成、道谢队列） |
| 场景 | SceneManager.LoadSceneAsync 异步切场景 |
| 存档 | JsonUtility + Application.persistentDataPath 本地 JSON |
| 音频 | AudioSource 语音与 BGM；独立 AudioManager |
| 表现 | ParticleSystem 节奏反馈、Image 立绘切换 |
| 编辑器 | Unity Inspector 配置、CreateAssetMenu 资源菜单 |
| 协作/IDE | Visual Studio / Rider（项目已配置对应包） |
