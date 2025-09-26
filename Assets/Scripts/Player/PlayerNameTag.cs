using TMPro;
using UnityEngine;

public class PlayerNameTag : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;

    private PlayerData playerData;

    private void OnEnable()
    {
        playerData = GetComponentInParent<PlayerData>();
        playerData.OnPlayerSpawned += PlayerData_OnPlayerSpawned;
    }

    private void PlayerData_OnPlayerSpawned(PlayerData obj)
    {
        SetNameTag("Player " + playerData.GetName());
    }


    public void SetNameTag(string name)
    {
        nameText.text = name;
    }
}
