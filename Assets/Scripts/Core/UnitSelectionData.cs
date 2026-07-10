using System.Collections.Generic;
using UnityEngine;

public static class UnitSelectionData
{
    public const int MaxSelection = 8;
    private const string PrefsKey = "UnitSelection_Deck";

    public static List<int> SelectedIndices { get; private set; } = new List<int>();

    public static bool HasValidSelection => SelectedIndices.Count >= MaxSelection;

    public static void Add(int unitIndex)
    {
        if (SelectedIndices.Count >= MaxSelection) return;
        if (SelectedIndices.Contains(unitIndex)) return;
        SelectedIndices.Add(unitIndex);
    }

    public static void Remove(int unitIndex)
    {
        SelectedIndices.Remove(unitIndex);
    }

    public static void Clear()
    {
        SelectedIndices.Clear();
    }

    /// <summary>将编队保存到 PlayerPrefs</summary>
    public static void Save()
    {
        if (SelectedIndices == null || SelectedIndices.Count == 0) return;
        string json = string.Join(",", SelectedIndices);
        PlayerPrefs.SetString(PrefsKey, json);
        PlayerPrefs.Save();
    }

    /// <summary>从 PlayerPrefs 加载编队，若有则填充 SelectedIndices，否则清空</summary>
    public static void Load()
    {
        if (!PlayerPrefs.HasKey(PrefsKey))
        {
            Clear();
            return;
        }
        string json = PlayerPrefs.GetString(PrefsKey, "");
        if (string.IsNullOrEmpty(json))
        {
            Clear();
            return;
        }
        SelectedIndices.Clear();
        foreach (string s in json.Split(','))
        {
            if (int.TryParse(s.Trim(), out int idx) && idx >= 0)
                SelectedIndices.Add(idx);
        }
        if (SelectedIndices.Count > MaxSelection)
            SelectedIndices.RemoveRange(MaxSelection, SelectedIndices.Count - MaxSelection);
    }
}
