using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// StartScene 干员选择界面。
/// 玩家从 allAvailableUnits 中选择 8 个带入游戏（前4手牌 + 后4队列）。
/// 选择结果存入 UnitSelectionData 静态类，GameScene 的 UIManager 读取。
///
/// Inspector 配置：
///   - allAvailableUnits: 与 Player Prefab 上 NetworkUnitPlacer / UIManager 的 allUnits 保持一致
///   - unitGridContent:   挂有 GridLayoutGroup 的容器（放可用干员卡片）
///   - selectedSlotsContent: 挂有 HorizontalLayoutGroup 的容器（放8个已选槽位）
///   - titleText / hostButton / clientButton: 对应 UI 组件
/// </summary>
public class UnitSelectionUI : MonoBehaviour
{
    [Header("干员数据（与 Player Prefab 上的 allUnits 保持一致）")]
    public UnitData[] allAvailableUnits;

    [Header("UI 容器")]
    [Tooltip("可用干员卡片的父容器（需要 GridLayoutGroup）")]
    public Transform unitGridContent;

    [Tooltip("已选干员槽位的父容器（需要 HorizontalLayoutGroup）")]
    public Transform selectedSlotsContent;

    [Header("UI 组件")]
    public Text titleText;
    public Button hostButton;
    public Button clientButton;

    [Header("外观")]
    public Font cardFont;

    private readonly Color _colorNormal = new Color(0.25f, 0.25f, 0.30f, 1f);
    private readonly Color _colorSelected = new Color(0.15f, 0.55f, 0.15f, 1f);
    private readonly Color _colorSlotEmpty = new Color(0.18f, 0.18f, 0.22f, 1f);
    private readonly Color _colorSlotFilled = new Color(0.20f, 0.40f, 0.60f, 1f);

    private List<Image> _cardBackgrounds = new List<Image>();
    private List<Image> _slotIcons = new List<Image>();
    private List<Text> _slotNames = new List<Text>();

    void Start()
    {
        if (cardFont == null)
        {
            cardFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (cardFont == null)
                cardFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        UnitSelectionData.Load();
        BuildUnitCards();
        BuildSelectedSlots();
        RefreshUI();
    }

    // ──────────────── 生成可用干员卡片 ────────────────

    void BuildUnitCards()
    {
        if (allAvailableUnits == null || unitGridContent == null) return;

        for (int i = 0; i < allAvailableUnits.Length; i++)
        {
            UnitData unit = allAvailableUnits[i];
            int idx = i;

            GameObject card = CreateCard(unitGridContent, $"UnitCard_{i}");
            Image bg = card.GetComponent<Image>();
            _cardBackgrounds.Add(bg);

            Button btn = card.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.highlightedColor = new Color(0.4f, 0.4f, 0.5f, 1f);
            cb.pressedColor = new Color(0.5f, 0.5f, 0.6f, 1f);
            btn.colors = cb;
            btn.onClick.AddListener(() => OnUnitCardClicked(idx));

            if (unit.unitIcon != null)
                CreateIcon(card.transform, unit.unitIcon, 0.1f, 0.30f, 0.9f, 0.92f);

            CreateLabel(card.transform, unit.unitName, 16,
                        0f, 0.12f, 1f, 0.30f, Color.white);

            CreateLabel(card.transform, $"{unit.elixirCost:F0}", 14,
                        0f, 0f, 1f, 0.14f, new Color(1f, 0.85f, 0.2f));
        }
    }

    // ──────────────── 生成已选槽位 ────────────────

    void BuildSelectedSlots()
    {
        if (selectedSlotsContent == null) return;

        for (int i = 0; i < UnitSelectionData.MaxSelection; i++)
        {
            int slotIdx = i;

            GameObject slot = CreateCard(selectedSlotsContent, $"Slot_{i}");
            Image bg = slot.GetComponent<Image>();
            bg.color = _colorSlotEmpty;

            Button btn = slot.AddComponent<Button>();
            btn.onClick.AddListener(() => OnSlotClicked(slotIdx));

            Image icon = CreateIcon(slot.transform, null, 0.1f, 0.25f, 0.9f, 0.90f);
            icon.gameObject.SetActive(false);
            _slotIcons.Add(icon);

            string label = i < 4 ? $"手牌{i + 1}" : $"队列{i - 3}";
            Text nameText = CreateLabel(slot.transform, label, 13,
                                        0f, 0.02f, 1f, 0.22f, new Color(0.6f, 0.6f, 0.6f));
            _slotNames.Add(nameText);
        }
    }

    // ──────────────── 交互 ────────────────

    void OnUnitCardClicked(int unitIndex)
    {
        if (UnitSelectionData.SelectedIndices.Contains(unitIndex))
        {
            UnitSelectionData.Remove(unitIndex);
        }
        else
        {
            UnitSelectionData.Add(unitIndex);
        }
        RefreshUI();
    }

    void OnSlotClicked(int slotIndex)
    {
        if (slotIndex < UnitSelectionData.SelectedIndices.Count)
        {
            UnitSelectionData.SelectedIndices.RemoveAt(slotIndex);
            RefreshUI();
        }
    }

    // ──────────────── UI 刷新 ────────────────

    void RefreshUI()
    {
        int count = UnitSelectionData.SelectedIndices.Count;

        if (titleText != null)
            titleText.text = $"选择干员 ({count}/{UnitSelectionData.MaxSelection})";

        // 卡片高亮
        for (int i = 0; i < _cardBackgrounds.Count; i++)
        {
            bool selected = UnitSelectionData.SelectedIndices.Contains(i);
            _cardBackgrounds[i].color = selected ? _colorSelected : _colorNormal;
        }

        // 槽位更新
        for (int i = 0; i < UnitSelectionData.MaxSelection; i++)
        {
            if (i < count)
            {
                int unitIdx = UnitSelectionData.SelectedIndices[i];
                UnitData unit = allAvailableUnits[unitIdx];

                _slotIcons[i].gameObject.SetActive(unit.unitIcon != null);
                _slotIcons[i].sprite = unit.unitIcon;
                _slotNames[i].text = unit.unitName;
                _slotIcons[i].transform.parent.GetComponent<Image>().color = _colorSlotFilled;
            }
            else
            {
                _slotIcons[i].gameObject.SetActive(false);
                string label = i < 4 ? $"手牌{i + 1}" : $"队列{i - 3}";
                _slotNames[i].text = label;
                _slotIcons[i].transform.parent.GetComponent<Image>().color = _colorSlotEmpty;
            }
        }

        // Host/Join 按钮
        bool valid = UnitSelectionData.HasValidSelection;
        if (hostButton != null) hostButton.interactable = valid;
        if (clientButton != null) clientButton.interactable = valid;
    }

    // ──────────────── UI 元素工厂方法 ────────────────

    GameObject CreateCard(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = _colorNormal;
        return go;
    }

    Image CreateIcon(Transform parent, Sprite sprite,
                     float ancMinX, float ancMinY, float ancMaxX, float ancMaxY)
    {
        GameObject go = new GameObject("Icon", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(ancMinX, ancMinY);
        rt.anchorMax = new Vector2(ancMaxX, ancMaxY);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return img;
    }

    Text CreateLabel(Transform parent, string text, int fontSize,
                     float ancMinX, float ancMinY, float ancMaxX, float ancMaxY,
                     Color color)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Text t = go.AddComponent<Text>();
        t.text = text;
        t.font = cardFont;
        t.fontSize = fontSize;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(ancMinX, ancMinY);
        rt.anchorMax = new Vector2(ancMaxX, ancMaxY);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return t;
    }
}
