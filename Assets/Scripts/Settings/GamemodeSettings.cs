using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.UIElements.UxmlAttributeDescription;

public class GamemodeSettings : NetworkSingleton<GamemodeSettings>
{
    [SerializeField] private Toggle AIToggle;

    public bool UseAI => AIToggle.isOn;
}
