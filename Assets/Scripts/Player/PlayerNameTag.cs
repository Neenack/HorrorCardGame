using TMPro;
using Unity.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerNameTag : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    private PlayerData playerData;

    private void Awake()
    {
        playerData = GetComponentInParent<PlayerData>();
        if (playerData != null)
        {
            SetNameTag(playerData.GetName());

            playerData.OnPlayerSpawned += PlayerData_OnPlayerSpawned;
        }
    }

    private void OnDestroy()
    {
        if (playerData != null) playerData.OnPlayerSpawned -= PlayerData_OnPlayerSpawned;
    }

    private void PlayerData_OnPlayerSpawned()
    {
        SetNameTag(playerData.GetName());
    }

    public void SetNameTag(string name)
    {
        nameText.text = name;
    }
}
