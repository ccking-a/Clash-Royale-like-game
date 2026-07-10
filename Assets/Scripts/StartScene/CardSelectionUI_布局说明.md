# 卡牌选择界面布局说明（类皇室战争风格）

## 一、显示逻辑（重要）

| 区域 | 显示内容 |
|------|----------|
| **已选槽位** | 有卡时：**背景为干员图片** + 名称；空槽：占位色 +「手牌1」等 |
| **总卡池** | **背景为干员图片** + 名称；**已选中的卡牌隐藏**，卸下后重新显示 |
| **点击卡牌** | 在图片下方边缘生成两个按钮：**查看信息**、**装备**（或**卸下**） |

**交互流程**：点击卡牌 → 图片下方出现「查看信息」「装备/卸下」→ 点击「查看信息」打开详情弹窗；点击「装备」直接装备；点击「卸下」直接卸下

## 二、整体结构

```
Canvas
└── CardSelectionPanel (全屏背景)
    ├── ScrollView (可滑动区域)
    │   ├── Viewport (遮罩)
    │   │   └── Content (VerticalLayoutGroup) ← 赋值给 scrollContent
    │   │       ├── Header (标题)
    │   │       ├── SelectedSection (已选卡牌区)
    │   │       │   └── SelectedSlotsContent (HorizontalLayoutGroup) ← 赋值给 selectedSlotsContent
    │   │       ├── Spacer
    │   │       ├── Label "所有卡牌"
    │   │       └── AllCardsSection
    │   │           └── AllCardsContent (GridLayoutGroup) ← 赋值给 allCardsContent
    │   └── Scrollbar Vertical (可选)
    └── DetailPanel (详情弹窗，初始隐藏) ← 赋值给 detailPanel
        ├── Background (半透明遮罩)
        ├── Panel (白色/深色面板)
        │   ├── Icon (卡牌图标)
        │   ├── Name (名称 Text)
        │   ├── Desc (描述 Text)
        │   ├── Cost (消耗 Text)
        │   ├── Skill (技能 Text)
        │   ├── CloseButton (关闭)
        │   └── ActionButton (装备/卸下)
        └── ...
```

---

## 三、组件说明

### 1. ScrollView（可滑动界面）

| 组件 | 作用 |
|------|------|
| **Scroll Rect** | 使内部 Content 可上下滑动 |
| **Viewport** | 子物体，带 Mask 组件，限制可见区域 |
| **Content** | 实际滚动内容，需挂 **VerticalLayoutGroup** |

**Scroll Rect 配置：**
- Content: 拖入 Content 的 RectTransform
- Viewport: 拖入 Viewport 的 RectTransform
- Horizontal: 取消勾选（仅垂直滑动）
- Vertical: 勾选
- Movement Type: Elastic 或 Clamped
- Scroll Sensitivity: 20~30

### 2. Content（挂 VerticalLayoutGroup）

| 属性 | 建议值 |
|------|--------|
| Spacing | 16 |
| Padding | 左右上下 16 |
| Child Force Expand Width | 勾选 |
| Child Control Height | 不勾选 |

### 3. SelectedSlotsContent（已选卡牌区，挂 HorizontalLayoutGroup）

| 属性 | 建议值 |
|------|--------|
| Spacing | 12 |
| Child Force Expand | 都不勾选 |
| Child Control Width/Height | 勾选 |

**作用**：8 个槽位横向排列。**有卡时背景显示干员图片**，空槽显示占位。点击后在图片下方显示「查看信息」「卸下」按钮。

### 4. AllCardsContent（所有卡牌区，挂 GridLayoutGroup）

| 属性 | 建议值 |
|------|--------|
| Cell Size | 100 x 100 |
| Spacing | 12 x 12 |
| Constraint | Fixed Column Count |
| Constraint Count | 4 |

**作用**：卡牌以 4 列网格排列，可多行，整体可滑动。**背景为干员图片**，已选中的卡牌会隐藏，卸下后重新显示。点击后在图片下方显示「查看信息」「装备」按钮。

### 5. DetailPanel（详情弹窗，仅点击卡牌后弹出）

- 居中显示
- 包含：图标、名称、描述、消耗、技能、关闭按钮、**操作按钮（装备/卸下）**
- Text 命名建议：`Name`、`Desc`、`Cost`、`Skill`，脚本会按名称填充
- **关闭按钮**：点击关闭弹窗
- **操作按钮**：根据来源显示「装备」或「卸下」，点击后执行对应操作并关闭弹窗

**交互逻辑**：卡牌默认仅显示名称，点击任意卡牌（已选或未选）后弹出详情弹窗，显示完整信息（图标、描述、消耗、技能）及操作按钮（装备/卸下）。

---

## 四、制作步骤

### 步骤 1：创建 Canvas

1. 右键 Hierarchy → UI → Canvas
2. Canvas Scaler：Scale With Screen Size，Reference Resolution 1920x1080，Match 0.5

### 步骤 2：创建 ScrollView

1. 右键 Canvas → UI → Scroll View
2. 删除默认的 Content 子物体，按下面结构重建：

```
ScrollView
├── Viewport (已有，确保有 Mask 组件)
└── Content (新建空物体)
```

3. 将 Content 设为 Viewport 的子物体
4. 给 Content 添加 **Vertical Layout Group**
5. 给 Content 添加 **Content Size Fitter**（Vertical Fit: Preferred Size）

### 步骤 3：创建 Content 内部结构

在 Content 下创建：

```
Content
├── Header (空物体，可放标题 Text)
├── SelectedSection (空物体)
│   └── SelectedSlotsContent (空物体，添加 Horizontal Layout Group)
├── Spacer (空物体，Layout Element: Min Height 20)
├── AllCardsLabel (Text: "所有卡牌")
└── AllCardsSection (空物体)
    └── AllCardsContent (空物体，添加 Grid Layout Group)
```

### 步骤 4：创建 DetailPanel

**只需添加组件即可**，脚本会自动填充文字、图标并绑定按钮。样式设计为可选，用于美化界面。

1. 右键 Canvas → 创建空物体，命名 DetailPanel
2. 添加 Image 作为背景（半透明黑，用于点击遮罩）
3. 创建子物体 Panel（Image），作为内容面板
4. 在 Panel 下添加以下子物体，**务必修改 GameObject 名称**（Inspector 顶部）：
   - Icon (Image) — 命名为 `Icon` 或含 "Icon"
   - Name (Text) — 命名为 `Name` 或含 "Name"
   - Desc (Text) — 命名为 `Desc` 或含 "Desc"
   - Cost (Text) — 命名为 `Cost` 或含 "Cost"
   - Skill (Text) — 命名为 `Skill` 或含 "Skill"
   - CloseButton (Button，文字「关闭」)
   - ActionButton (Button，文字「装备」)

5. **若仍无法赋值**：在 CardSelectionUI 的 Inspector 中，找到「详情弹窗子组件」，将 Name、Icon 等拖入对应槽位，可跳过名称查找。

**可选**：调整 Panel 尺寸、颜色、字体大小等，使界面更美观。

### 步骤 5：挂载脚本

1. 在 Canvas 或 CardSelectionPanel 上添加 **CardSelectionUI** 组件
2. 在 Inspector 中赋值：
   - allAvailableUnits：与 Player 上的 allUnits 一致
   - scrollContent：Content 的 RectTransform
   - selectedSlotsContent：SelectedSlotsContent 的 RectTransform
   - allCardsContent：AllCardsContent 的 RectTransform
   - detailPanel：DetailPanel 的 RectTransform
   - detailCloseButton：关闭按钮（可选）
   - detailActionButton：装备/卸下按钮（可选）

---

## 五、卡牌样式设计

### 方式一：在 Inspector 中调整（推荐）

1. 选中挂有 **CardSelectionUI** 的 GameObject
2. 在 Inspector 中找到 **「外观」** 和 **「卡牌样式」** 区域
3. 可调整项：

| 参数 | 说明 |
|------|------|
| Selected Card Size | 已选槽位卡牌尺寸（宽高） |
| All Card Size | 卡池中卡牌尺寸 |
| Grid Columns | 卡池网格列数 |
| Card Font | 卡牌文字字体 |
| Color Normal | 卡池未选中卡牌背景色 |
| Color Selected | 卡池已选中卡牌背景色 |
| Color Slot Empty | 空槽位背景色 |
| Color Slot Filled | 已选槽位有卡时的背景色 |
| Card Background Sprite | 卡牌背景图（可选，不填为纯色） |

### 方式二：使用自定义卡牌背景图

1. 准备一张卡牌背景图（如圆角矩形、边框等），导入 Unity 并设置为 Sprite
2. 在 CardSelectionUI 的 Inspector 中，将 **Card Background Sprite** 拖入该 Sprite
3. 卡牌会使用该图作为背景，**Color Normal** 等会作为 tint 叠加

### 方式三：修改详情弹窗样式

1. 选中 **DetailPanel** 下的 **Panel** 子物体
2. 在 Image 组件中调整：
   - **Color**：面板背景色
   - **Sprite**：可换成带边框/圆角的 Sprite
3. 选中 **Name**、**Desc** 等 Text，调整 Font、Font Size、Color

---

## 六、组件总结

| 区域 | 布局组件 | 用途 |
|------|----------|------|
| 整体 | Scroll Rect + Viewport | 可滑动 |
| Content | VerticalLayoutGroup | 垂直排列：标题、已选、卡池 |
| 已选卡牌 | HorizontalLayoutGroup | 8 个槽位，有卡时背景为干员图 |
| 所有卡牌 | GridLayoutGroup | 4 列网格，背景为干员图，已选卡牌隐藏 |
| 详情弹窗 | 无布局 | 点击卡牌后弹出，显示完整信息、装备/卸下按钮 |

---

## 七、与 UnitSelectionData 的衔接

- 选择结果保存在 `UnitSelectionData.SelectedIndices`
- 确认后进入游戏时，UIManager 会读取该数据
- 需选满 8 张才能确认（与原有逻辑一致）
