using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static CodeMonkey.Utils.UI_TextComplex;

public class GamemodeSettings : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Toggle AIToggle;
    [SerializeField] private Toggle stackingToggle;
    [SerializeField] private TMP_Dropdown aiDifficultyDropdown;

    private void Start()
    {
        PopulateDifficultyDropdown();

        aiDifficultyDropdown.value = (int)GamemodeSettingsManager.Instance.AIDifficulty;
        aiDifficultyDropdown.RefreshShownValue();

        AIToggle.onValueChanged.AddListener(OnAIToggleChanged);
        stackingToggle.onValueChanged.AddListener(OnStackingToggleChanged);
        aiDifficultyDropdown.onValueChanged.AddListener(OnDifficultyDropdownChanged);


        GamemodeSettingsManager.Instance.UseAI = AIToggle.isOn;
        GamemodeSettingsManager.Instance.Stacking = stackingToggle.isOn;
        GamemodeSettingsManager.Instance.AIDifficulty = (Difficulty)aiDifficultyDropdown.value;
    }

    private void PopulateDifficultyDropdown()
    {
        aiDifficultyDropdown.ClearOptions();

        var enumNames = Enum.GetNames(typeof(Difficulty));
        var options = new List<TMP_Dropdown.OptionData>();

        foreach (var name in enumNames)
            options.Add(new TMP_Dropdown.OptionData(name));

        aiDifficultyDropdown.AddOptions(options);
    }

    private void OnAIToggleChanged(bool isOn)
    {
        GamemodeSettingsManager.Instance.UseAI = isOn;
    }

    private void OnStackingToggleChanged(bool isOn)
    {
        GamemodeSettingsManager.Instance.Stacking = isOn;
    }

    private void OnDifficultyDropdownChanged(int index)
    {
        GamemodeSettingsManager.Instance.AIDifficulty = (Difficulty)index;
    }

    private void OnDestroy()
    {
        AIToggle.onValueChanged.RemoveListener(OnAIToggleChanged);
        stackingToggle.onValueChanged.RemoveListener(OnStackingToggleChanged);
        aiDifficultyDropdown.onValueChanged.RemoveListener(OnDifficultyDropdownChanged);
    }
}
