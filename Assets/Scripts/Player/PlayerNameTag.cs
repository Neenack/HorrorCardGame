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
            Invoke("SetNameTag", 1f);

            playerData.OnPlayerSpawned += PlayerData_OnPlayerSpawned;
        }
    }

    private void OnDestroy()
    {
        if (playerData != null) playerData.OnPlayerSpawned -= PlayerData_OnPlayerSpawned;
    }

    private void PlayerData_OnPlayerSpawned()
    {
        SetNameTag();
    }

    public void SetNameTag()
    {
        nameText.text = playerData.GetName();
    }
}
