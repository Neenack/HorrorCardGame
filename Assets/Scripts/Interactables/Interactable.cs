using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;


public class Interactable : NetworkBehaviour, IInteractable
{
    public event EventHandler<InteractEventArgs> OnInteract;

    public class InteractEventArgs : EventArgs
    {
        public ulong playerID;
        public InteractEventArgs(ulong playerID) { this.playerID = playerID; }
    }

    private NetworkVariable<bool> canInteract = new NetworkVariable<bool>(true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);


    [SerializeField] private string interactableText = "Interact";

    public string GetText() => interactableText;


    // Only server can change text and sync to clients
    public void SetText(string text)
    {
        if (!IsServer) return;

        interactableText = text;
        SetTextClientRpc(text);
    }

    [ClientRpc]
    private void SetTextClientRpc(string text)
    {
        if (IsServer) return;

        interactableText = text;
    }


    /// <summary>
    /// Checks if a player can interact
    /// </summary>
    public bool CanInteract() => canInteract.Value;


    /// <summary>
    /// Called when a player interacts, invokes an event
    /// </summary>
    public void Interact(ulong playerID)
    {
        if (playerID != NetworkManager.Singleton.LocalClientId)
        {
            return;
        }

        // Client-side validation for immediate feedback
        if (!CanInteract())
        {
            Debug.Log($"Cannot interact with {gameObject.name}");
            return;
        }

        Debug.Log($"Player {playerID} interacted with {gameObject.name}");

        OnInteract?.Invoke(this, new InteractEventArgs(playerID));

        InteractNotifyServerRpc(playerID);
    }

    [ServerRpc(RequireOwnership = false)]
    private void InteractNotifyServerRpc(ulong playerID)
    {
        Debug.Log($"[Server] Player {playerID} interacted with {gameObject.name}");
    }

    /// <summary>
    /// Server-only: Enable or disable interactable globally
    /// </summary>
    public void SetInteractable(bool interact)
    {
        if (!IsServer) return;

        canInteract.Value = interact;
    }
}