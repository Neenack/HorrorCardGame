using System.Globalization;
using System.Runtime.InteropServices.WindowsRuntime;
using Unity.Netcode;
using UnityEngine;

public class UIFaceOwner : MonoBehaviour
{
    private Camera ownerCamera;

    private void OnEnable()
    {
        if (PlayerManager.Instance == null || NetworkManager.Singleton == null) return;

        PlayerData data = PlayerManager.Instance.GetPlayerDataById(NetworkManager.Singleton.LocalClientId);

        if (data == null) return;

        ownerCamera = data.gameObject.GetComponentInChildren<Camera>();
    }

    void LateUpdate()
    {
        if (ownerCamera == null) return;

        // Rotate UI to face the owner camera
        Vector3 direction = (transform.position - ownerCamera.transform.position).normalized;
        transform.forward = direction;
    }
}