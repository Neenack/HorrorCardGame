using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using TMPro;
using System.Threading;

public class GamemodeSettingsManager : MonoSingleton<GamemodeSettingsManager>
{
    public bool UseAI { get; set; }
    public bool Stacking { get; set; }
    public Difficulty AIDifficulty { get; set; }
}
