using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEditor.PackageManager;
using UnityEngine;


public class Interactable : NetworkBehaviour, IInteractable
{
    public event EventHandler<InteractEventArgs> OnInteract;

    public class InteractEventArgs : EventArgs
    {
        public ulong ClientID;
        public InteractEventArgs(ulong playerID) { this.ClientID = playerID; }
    }

    private NetworkList<ulong> allowedClients = new NetworkList<ulong>();
    private NetworkVariable<InteractDisplay> interactableDisplay;
    private bool isInteractable = true;

    [Header("Interact Display")]
    [SerializeField] private string interactText = "Interact";
    [SerializeField] private bool showInteractBox;
    [SerializeField] private string interactBoxTitle;
    [SerializeField] private string interactBoxBody;


    private void Awake()
    {
        interactableDisplay = new NetworkVariable<InteractDisplay>(
        new InteractDisplay(
            interactText,
            showInteractBox,
            interactBoxTitle,
            interactBoxBody
            ),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
        );
    }

    /// <summary>
    /// Returns the text display for interacting
    /// </summary>
    public InteractDisplay GetDisplay() => interactableDisplay.Value;



    /// <summary>
    /// Checks if a certain client can interact
    /// </summary>
    public bool CanInteract(ulong LocalClientID) => isInteractable && allowedClients.Contains(LocalClientID);


    /// <summary>
    /// Sets the interactable
    /// </summary>
    public void SetInteractable(bool interactable) => isInteractable = interactable;


    /// <summary>
    /// Called when a player interacts, invokes an event
    /// </summary>
    public void Interact()
    {
        ulong clientID = NetworkManager.Singleton.LocalClientId;

        // Client-side validation for immediate feedback
        if (!CanInteract(clientID)) return;

        InteractServerRpc(clientID);

        OnInteract?.Invoke(this, new InteractEventArgs(clientID));
    }

    [ServerRpc(RequireOwnership = false)]
    private void InteractServerRpc(ulong clientID)
    {
        PlayerData data = PlayerManager.Instance.GetPlayerDataById(clientID);
        ConsoleLog.Instance.Log($"{data.GetName()} has interacted with {gameObject.name}");
    }

    /// <summary>
    /// SERVER ONLY Sets the allowed clients for the interactable
    /// </summary>
    public void SetAllowedClients(params ulong[] clients)
    {
        if (!IsServer) return;

        allowedClients.Clear();
        foreach (ulong client in clients)
        {
            if (allowedClients.Contains(client)) continue;
            allowedClients.Add(client);
        }

        Debug.Log($"{gameObject.name} has been set interactable for [{string.Join(", ", clients)}]");
    }

    /// <summary>
    /// SERVER ONLY Adds an allowed client for the interactable
    /// </summary>
    public void AddAllowedClient(ulong clientId)
    {
        if (!IsServer) return;
        if (!allowedClients.Contains(clientId))
            allowedClients.Add(clientId);

        Debug.Log($"{gameObject.name} has added client [{clientId}]");
    }


    /// <summary>
    /// SERVER ONLY Removes an allowed client for the interacable
    /// </summary>
    public void RemoveAllowedClient(ulong clientId)
    {
        if (!IsServer) return;
        allowedClients.Remove(clientId);

        Debug.Log($"{gameObject.name} has removed client [{clientId}]");
    }

    /// <summary>
    /// SERVER ONLY Clears the allowed clients for the interactable
    /// </summary>
    public void ClearAllowedClients()
    {
        if (!IsServer || allowedClients.Count == 0) return;

        allowedClients.Clear();

        Debug.Log($"{gameObject.name} has cleared clients");
    }


    /// <summary>
    /// SERVER ONLY Set the interact display
    /// </summary>
    public void SetDisplay(InteractDisplay display)
    {
        interactableDisplay.Value = display;
    }

    /// <summary>
    /// SERVER ONLY Reset the interact display to default
    /// </summary>
    public void ResetDisplay()
    {
        interactableDisplay.Value = new InteractDisplay(
            interactText,
            showInteractBox,
            interactBoxTitle,
            interactBoxBody
            );
    }
}