using System.Collections;
using System.Collections.Generic;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.UI;

public class AuthenticateUI : MonoBehaviour
{
    [SerializeField] private Button authenticateButton;

    private void Awake()
    {
        // Check if already authenticated
        if (UnityServices.State == ServicesInitializationState.Initialized &&
            AuthenticationService.Instance.IsSignedIn)
        {
            Hide();
            return;
        }

        authenticateButton.onClick.AddListener(() =>
        {
            authenticateButton.gameObject.SetActive(false);
            AuthenticateUser();
        });
    }

    private void OnEnable()
    {
        // Check again when enabled (when returning from game scene)
        if (UnityServices.State == ServicesInitializationState.Initialized &&
            AuthenticationService.Instance.IsSignedIn)
        {
            Hide();
        }
    }

    private async void AuthenticateUser()
    {
        bool success = await LobbyManager.Instance.Authenticate(EditPlayerName.Instance.GetPlayerName());
        if (success)
        {
            Hide();
        }
        else
        {
            authenticateButton.gameObject.SetActive(true);
        }
    }

    private void Hide()
    {
        gameObject.SetActive(false);
    }
}