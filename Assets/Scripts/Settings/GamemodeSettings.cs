using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.UIElements.UxmlAttributeDescription;

public class GamemodeSettings : NetworkSingleton<GamemodeSettings>
{
    [SerializeField] private Toggle AIToggle;
    [SerializeField] private Toggle stackingEnabled;

    public bool UseAI => AIToggle.isOn;
    public bool Stacking => stackingEnabled;
}
