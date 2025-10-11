using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using TMPro;
using System.Threading;

public class GamemodeSettings : MonoSingleton<GamemodeSettings>
{
    [Header("UI References")]
    [SerializeField] private Toggle AIToggle;
    [SerializeField] private Toggle stackingEnabled;
    [SerializeField] private TMP_Dropdown aiDifficultyDropdown;

    private Difficulty aiDifficulty = Difficulty.Normal;

    public bool UseAI => AIToggle.isOn;
    public bool Stacking => stackingEnabled.isOn;
    public Difficulty AIDifficulty => aiDifficulty;

    private void Start()
    {
        PopulateDifficultyDropdown();
        aiDifficultyDropdown.onValueChanged.AddListener(OnDifficultyChanged);

        aiDifficulty = (Difficulty)aiDifficultyDropdown.value;
    }

    private void PopulateDifficultyDropdown()
    {
        aiDifficultyDropdown.ClearOptions();

        var enumNames = Enum.GetNames(typeof(Difficulty));
        var options = new List<TMP_Dropdown.OptionData>();

        foreach (var name in enumNames)
        {
            options.Add(new TMP_Dropdown.OptionData(name));
        }

        aiDifficultyDropdown.AddOptions(options);

        aiDifficultyDropdown.value = (int)aiDifficulty;
        aiDifficultyDropdown.RefreshShownValue();
    }

    private void OnDifficultyChanged(int index)
    {
        aiDifficulty = (Difficulty)index;
    }
}
