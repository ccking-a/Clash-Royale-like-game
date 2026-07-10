using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;

/// <summary>
/// 类皇室战争风格的卡牌选择界面。
/// 卡牌默认仅显示名称，点击后弹出详情弹窗（图标、描述、消耗、技能）及操作按钮（装备/卸下）。
/// 上部分：已选卡牌槽位；下部分：所有卡牌；整体可滑动。
/// </summary>
public class CardSelectionUI : MonoBehaviour
{
    [Header("干员数据（与 Player Prefab 上的 allUnits 保持一致）")]
    public UnitData[] allAvailableUnits;

    [Header("UI 根节点（需提前创建，见下方布局说明）")]
    [Tooltip("ScrollView 的 Content，需挂 VerticalLayoutGroup")]
    public RectTransform scrollContent;

    [Tooltip("已选卡牌容器，需挂 HorizontalLayoutGroup")]
    public RectTransform selectedSlotsContent;

    [Tooltip("所有卡牌容器，需挂 GridLayoutGroup")]
    public RectTransform allCardsContent;

    [Tooltip("卡牌详情弹窗根节点")]
    public RectTransform detailPanel;

    [Tooltip("详情弹窗的关闭按钮（可选）")]
    public Button detailCloseButton;

    [Tooltip("详情弹窗的操作按钮（装备/卸下，可选）")]
    public Button detailActionButton;

    [Header("详情弹窗子组件（可选，不填则按名称查找）")]
    [Tooltip("名称 Text，不填则查找名称含 Name 的 Text")]
    public Text detailNameText;
    [Tooltip("图标 Image，不填则查找名称含 Icon 的 Image")]
    public Image detailIconImage;
    [Tooltip("描述 Text，不填则查找名称含 Desc 的 Text")]
    public Text detailDescText;
    [Tooltip("消耗 Text，不填则查找名称含 Cost 的 Text")]
    public Text detailCostText;
    [Tooltip("技能 Text，不填则查找名称含 Skill 的 Text")]
    public Text detailSkillText;

    [Header("可选：若为空则自动创建")]
    public Text titleText;
    public Button confirmButton;
    public Button backButton;

    [Header("外观")]
    public float selectedCardSize = 120f;
    public float allCardSize = 100f;
    public int gridColumns = 4;
    public Font cardFont;

    [Header("卡牌样式（可在 Inspector 中调整）")]
    [Tooltip("卡池中未选中卡牌的背景色")]
    public Color colorNormal = new Color(0.22f, 0.22f, 0.28f, 1f);
    [Tooltip("卡池中已选中卡牌的背景色")]
    public Color colorSelected = new Color(0.15f, 0.50f, 0.15f, 1f);
    [Tooltip("已选槽位为空时的背景色")]
    public Color colorSlotEmpty = new Color(0.16f, 0.16f, 0.20f, 0.9f);
    [Tooltip("已选槽位有卡牌时的背景色")]
    public Color colorSlotFilled = new Color(0.18f, 0.45f, 0.65f, 1f);
    [Tooltip("卡牌背景图（可选，不填则用纯色）")]
    public Sprite cardBackgroundSprite;

    [Header("主页canva")]
    public Canvas Lobbycanva;

    [Header("编队canva")]
    public Canvas Selectcanva;

    [Header("快捷按钮面板（点击卡牌后显示，可调）")]
    public float quickPanelWidth = 90f;
    public float quickPanelHeight = 56f;
    public float quickPanelOffsetY = -4f;
    public float quickPanelSpacing = 4f;
    public RectOffset quickPanelPadding;
    public Color quickPanelBgColor = new Color(0.1f, 0.1f, 0.15f, 0.95f);
    public Color quickButtonColor = new Color(0.25f, 0.4f, 0.6f, 1f);
    public int quickButtonFontSize = 11;
    public float quickButtonMinHeight = 24f;

    private List<GameObject> _slotCards = new List<GameObject>();
    private List<GameObject> _poolCards = new List<GameObject>();
    private int _detailUnitIndex = -1;
    private bool _detailIsFromSlot;
    private GameObject _quickActionPanel;  // 点击卡牌后在图片下方显示的按钮面板

    void Start()
    {
        if (cardFont == null)
            cardFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        if (quickPanelPadding.left == 0 && quickPanelPadding.right == 0 && quickPanelPadding.top == 0 && quickPanelPadding.bottom == 0)
            quickPanelPadding = new RectOffset(4, 4, 4, 4);

        UnitSelectionData.Load();

        if (scrollContent == null || selectedSlotsContent == null || allCardsContent == null)
        {
            Debug.LogError("CardSelectionUI: 请先创建 UI 层级并赋值 scrollContent / selectedSlotsContent / allCardsContent");
            return;
        }

        SetBackButton();
        EnsureLayoutGroups();
        BuildSelectedSlots();
        BuildAllCards();
        SetupDetailPanel();
        RefreshUI();
    }

    private void SetBackButton()
    {
        if (backButton != null)
        {
            backButton.onClick.AddListener(() =>
            {
                UnitSelectionData.Save();
                if(Lobbycanva!= null && Selectcanva != null)
                {
                    Lobbycanva.gameObject.SetActive(true);
                    Selectcanva.gameObject.SetActive(false);
                }
                Debug.Log("返回主菜单");
            });
        }
    }

    void EnsureLayoutGroups()
    {
        if (scrollContent.GetComponent<VerticalLayoutGroup>() == null)
        {
            var vlg = scrollContent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 16;
            vlg.padding = new RectOffset(16, 16, 16, 16);
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false;
        }
        if (selectedSlotsContent.GetComponent<HorizontalLayoutGroup>() == null)
        {
            var hlg = selectedSlotsContent.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12;
            hlg.padding = new RectOffset(0, 0, 0, 0);
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
        }
        if (allCardsContent.GetComponent<GridLayoutGroup>() == null)
        {
            var glg = allCardsContent.gameObject.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(allCardSize, allCardSize);
            glg.spacing = new Vector2(12, 12);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = gridColumns;
            glg.childAlignment = TextAnchor.UpperCenter;
        }
    }

    void BuildSelectedSlots()
    {
        // 槽位默认只显示名称（空槽为「手牌1」等），点击后弹出详情弹窗
        for (int i = 0; i < UnitSelectionData.MaxSelection; i++)
        {
            int slotIdx = i;
            GameObject slot = CreateCard(selectedSlotsContent, $"Slot_{i}", selectedCardSize, selectedCardSize);
            Image bg = slot.GetComponent<Image>();
            bg.color = colorSlotEmpty;
            if (cardBackgroundSprite != null) bg.sprite = cardBackgroundSprite;

            Button btn = slot.GetComponent<Button>() ?? slot.AddComponent<Button>();
            btn.onClick.AddListener(() => OnSlotClicked(slotIdx));

            CreateLabel(slot.transform, i < 4 ? $"手牌{i + 1}" : $"队列{i - 3}", 12, 0f, 0.1f, 1f, 0.9f, new Color(0.6f, 0.6f, 0.6f));

            _slotCards.Add(slot);
        }
    }

    void BuildAllCards()
    {
        // 卡牌默认只显示名称，点击后弹出详情弹窗
        if (allAvailableUnits == null) return;

        for (int i = 0; i < allAvailableUnits.Length; i++)
        {
            UnitData unit = allAvailableUnits[i];
            int idx = i;
            GameObject card = CreateCard(allCardsContent, $"Card_{i}", allCardSize, allCardSize);
            Image bg = card.GetComponent<Image>();
            bg.sprite = unit.unitIcon;
            bg.color = unit.unitIcon != null ? Color.white : colorNormal;

            Button btn = card.GetComponent<Button>() ?? card.AddComponent<Button>();
            btn.onClick.AddListener(() => OnPoolCardClicked(idx));

            CreateLabel(card.transform, unit.unitName, 12, 0f, 0.1f, 1f, 0.9f, Color.white);

            _poolCards.Add(card);
        }
    }

    void SetupDetailPanel()
    {
        if (detailPanel == null) return;
        detailPanel.gameObject.SetActive(false);

        Button closeBtn = detailCloseButton;
        if (closeBtn == null)
        {
            foreach (var b in detailPanel.GetComponentsInChildren<Button>(true))
            {
                if (b.name.Contains("Close") || (b.GetComponentInChildren<Text>()?.text?.Contains("关闭") ?? false))
                {
                    closeBtn = b;
                    break;
                }
            }
        }
        if (closeBtn == null)
            closeBtn = detailPanel.GetComponentInChildren<Button>();
        if (closeBtn != null)
            closeBtn.onClick.AddListener(HideDetail);
    }

    void OnSlotClicked(int slotIndex)
    {
        if (slotIndex >= UnitSelectionData.SelectedIndices.Count) return;
        int unitIdx = UnitSelectionData.SelectedIndices[slotIndex];
        _detailUnitIndex = unitIdx;
        _detailIsFromSlot = true;
        ShowQuickActionPanel(_slotCards[slotIndex].transform, unitIdx, true);
    }

    void OnPoolCardClicked(int unitIndex)
    {
        _detailUnitIndex = unitIndex;
        _detailIsFromSlot = false;
        ShowQuickActionPanel(_poolCards[unitIndex].transform, unitIndex, false);
    }

    /// <summary>
    /// 在卡牌图片下方边缘显示「查看信息」「装备/卸下」两个按钮（垂直排列）
    /// </summary>
    void ShowQuickActionPanel(Transform cardTransform, int unitIndex, bool isFromSlot)
    {
        HideQuickActionPanel();

        Canvas rootCanvas = cardTransform.GetComponentInParent<Canvas>();
        Transform panelParent = rootCanvas != null ? rootCanvas.transform : cardTransform;
        bool parentToCanvas = (panelParent != cardTransform);

        GameObject panel = new GameObject("QuickActionPanel", typeof(RectTransform));
        panel.transform.SetParent(panelParent, false);

        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(quickPanelWidth, quickPanelHeight);

        if (parentToCanvas)
        {
            RectTransform cardRt = cardTransform as RectTransform;
            if (cardRt != null)
            {
                Vector3[] cardCorners = new Vector3[4];
                cardRt.GetWorldCorners(cardCorners);
                rt.position = new Vector3((cardCorners[0].x + cardCorners[2].x) * 0.5f, cardCorners[0].y + quickPanelOffsetY, cardCorners[0].z);
            }
        }
        else
        {
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, quickPanelOffsetY);
        }

        if (!parentToCanvas)
        {
            LayoutElement panelLe = panel.AddComponent<LayoutElement>();
            panelLe.ignoreLayout = true;
        }

        Image panelBg = panel.AddComponent<Image>();
        panelBg.color = quickPanelBgColor;

        VerticalLayoutGroup vlg = panel.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = quickPanelSpacing;
        vlg.padding = quickPanelPadding;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlHeight = false;
        vlg.childControlWidth = true;

        CreateQuickButton(panel.transform, "信息", () =>
        {
            HideQuickActionPanel();
            ShowDetail(unitIndex, isFromSlot);
        });
        CreateQuickButton(panel.transform, isFromSlot ? "卸下" : "装备", () =>
        {
            HideQuickActionPanel();
            if (isFromSlot)
                OnUnequipClicked();
            else
                OnEquipClicked();
        });

        float contentHeight = quickPanelPadding.top + quickPanelPadding.bottom + quickPanelSpacing + 2f * quickButtonMinHeight;
        rt.sizeDelta = new Vector2(quickPanelWidth, Mathf.Max(quickPanelHeight, contentHeight));
        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        _quickActionPanel = panel;
    }

    Button CreateQuickButton(Transform parent, string label, System.Action onClick)
    {
        GameObject go = new GameObject("Btn_" + label, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = quickButtonColor;
        Button btn = go.AddComponent<Button>();

        GameObject textObj = new GameObject("Text", typeof(RectTransform));
        textObj.transform.SetParent(go.transform, false);
        Text t = textObj.AddComponent<Text>();
        t.text = label;
        t.font = cardFont;
        t.fontSize = quickButtonFontSize;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = textRt.offsetMax = Vector2.zero;

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = 1;
        le.minHeight = quickButtonMinHeight;
        le.preferredHeight = quickButtonMinHeight;

        btn.onClick.AddListener(() => onClick?.Invoke());
        return btn;
    }

    void HideQuickActionPanel()
    {
        if (_quickActionPanel != null)
        {
            Destroy(_quickActionPanel);
            _quickActionPanel = null;
        }
    }

    void ShowDetail(int unitIndex, bool isFromSlot)
    {
        if (detailPanel == null || unitIndex < 0 || unitIndex >= allAvailableUnits.Length) return;

        UnitData unit = allAvailableUnits[unitIndex];
        if (unit == null) return;
        detailPanel.gameObject.SetActive(true);

        // 名称
        Text nameText = detailNameText != null ? detailNameText : FindDetailText("Name");
        if (nameText != null) nameText.text = unit.unitName ?? "";

        // 描述
        Text descText = detailDescText != null ? detailDescText : FindDetailText("Desc");
        if (descText != null) descText.text = unit.unitDescription ?? "";

        // 消耗
        Text costText = detailCostText != null ? detailCostText : FindDetailText("Cost");
        if (costText != null) costText.text = $"消耗: {unit.elixirCost:F0}";

        // 技能
        Text skillText = detailSkillText != null ? detailSkillText : FindDetailText("Skill");
        if (skillText != null) skillText.text = unit.skillName ?? "技能";

        // 图标（遍历所有 Image 查找含 Icon 的，避免误取背景/面板）
        Image iconImg = detailIconImage != null ? detailIconImage : FindDetailIcon();
        if (iconImg != null && unit.unitIcon != null)
        {
            iconImg.sprite = unit.unitIcon;
            iconImg.gameObject.SetActive(true);
        }

        Button actionBtn = detailActionButton;
        Button closeBtn = detailCloseButton != null ? detailCloseButton : null;
        if (closeBtn == null)
        {
            foreach (var b in detailPanel.GetComponentsInChildren<Button>(true))
            {
                if (b.name.Contains("Close") || (b.GetComponentInChildren<Text>()?.text?.Contains("关闭") ?? false))
                {
                    closeBtn = b;
                    break;
                }
            }
        }
        if (actionBtn == null)
        {
            foreach (var b in detailPanel.GetComponentsInChildren<Button>(true))
            {
                if (b != closeBtn)
                {
                    actionBtn = b;
                    break;
                }
            }
        }
        if (actionBtn != null)
        {
            actionBtn.onClick.RemoveAllListeners();
            var actionText = actionBtn.GetComponentInChildren<Text>();
            if (isFromSlot)
            {
                if (actionText != null) actionText.text = "卸下";
                actionBtn.onClick.AddListener(OnUnequipClicked);
            }
            else
            {
                if (actionText != null) actionText.text = "装备";
                actionBtn.onClick.AddListener(OnEquipClicked);
            }
        }
    }

    void OnEquipClicked()
    {
        if (_detailUnitIndex < 0 || UnitSelectionData.SelectedIndices.Count >= UnitSelectionData.MaxSelection) return;
        if (UnitSelectionData.SelectedIndices.Contains(_detailUnitIndex)) return;
        UnitSelectionData.Add(_detailUnitIndex);
        HideDetail();
        RefreshUI();
    }

    void OnUnequipClicked()
    {
        if (_detailUnitIndex < 0) return;
        int slotIdx = UnitSelectionData.SelectedIndices.IndexOf(_detailUnitIndex);
        if (slotIdx >= 0)
        {
            UnitSelectionData.SelectedIndices.RemoveAt(slotIdx);
        }
        HideDetail();
        RefreshUI();
    }

    void HideDetail()
    {
        if (detailPanel != null)
            detailPanel.gameObject.SetActive(false);
        HideQuickActionPanel();
        _detailUnitIndex = -1;
    }

    Text FindDetailText(string keyword)
    {
        if (detailPanel == null) return null;
        foreach (var t in detailPanel.GetComponentsInChildren<Text>(true))
        {
            if (t.gameObject.name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return t;
        }
        return null;
    }

    Image FindDetailIcon()
    {
        if (detailPanel == null) return null;
        foreach (var img in detailPanel.GetComponentsInChildren<Image>(true))
        {
            if (img.gameObject.name.IndexOf("Icon", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return img;
        }
        return null;
    }

    void RefreshUI()
    {
        HideQuickActionPanel();
        int count = UnitSelectionData.SelectedIndices.Count;

        if (titleText != null)
            titleText.text = $"选择干员 ({count}/{UnitSelectionData.MaxSelection})";

        // 总卡池：被选中的卡牌隐藏，卸下后再显示
        for (int i = 0; i < _poolCards.Count; i++)
        {
            bool selected = UnitSelectionData.SelectedIndices.Contains(i);
            _poolCards[i].SetActive(!selected);
            if (!selected)
            {
                var img = _poolCards[i].GetComponent<Image>();
                var unit = allAvailableUnits[i];
                img.sprite = unit.unitIcon;
                img.color = unit.unitIcon != null ? Color.white : colorNormal;
            }
        }

        // 已选槽位：有卡时背景显示干员图片，空槽显示占位
        for (int i = 0; i < UnitSelectionData.MaxSelection; i++)
        {
            Transform slot = _slotCards[i].transform;
            Image bg = slot.GetComponent<Image>();
            Text label = slot.GetComponentInChildren<Text>();

            if (i < count)
            {
                int unitIdx = UnitSelectionData.SelectedIndices[i];
                UnitData unit = allAvailableUnits[unitIdx];
                bg.sprite = unit.unitIcon;
                bg.color = unit.unitIcon != null ? Color.white : colorSlotFilled;
                label.text = unit.unitName;
            }
            else
            {
                bg.sprite = cardBackgroundSprite;
                bg.color = colorSlotEmpty;
                label.text = i < 4 ? $"手牌{i + 1}" : $"队列{i - 3}";
            }
        }

        if (confirmButton != null)
            confirmButton.interactable = UnitSelectionData.HasValidSelection;
    }

    GameObject CreateCard(Transform parent, string name, float w, float h)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = colorNormal;
        if (cardBackgroundSprite != null) img.sprite = cardBackgroundSprite;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(w, h);

        return go;
    }

    Image CreateIcon(Transform parent, Sprite sprite, float ax, float ay, float bx, float by)
    {
        GameObject go = new GameObject("Icon", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        if (sprite == null) go.SetActive(false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(ax, ay);
        rt.anchorMax = new Vector2(bx, by);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return img;
    }

    Text CreateLabel(Transform parent, string text, int fontSize, float ax, float ay, float bx, float by, Color color)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Text t = go.AddComponent<Text>();
        t.text = text;
        t.font = cardFont;
        t.fontSize = fontSize;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(ax, ay);
        rt.anchorMax = new Vector2(bx, by);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return t;
    }
}
