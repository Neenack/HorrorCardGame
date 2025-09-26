using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Netcode;
using UnityEngine;


public enum InteractMode
{
    Host, All
}

public class Interactable : NetworkBehaviour, IInteractable
{
    public event EventHandler<InteractEventArgs> OnInteract;

    public class InteractEventArgs : EventArgs
    {
        public ulong playerID;
        public InteractEventArgs(ulong playerID) { this.playerID = playerID; }
    }

    [SerializeField] private bool canInteract = true;

    [SerializeField] private NetworkVariable<InteractMode> interactMode = new NetworkVariable<InteractMode>(
        InteractMode.All,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    [SerializeField] private NetworkVariable<NetworkString> interactableText = new NetworkVariable<NetworkString>(
        "Interact",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);


    /// <summary>
    /// Returns the text display for interacting
    /// </summary>
    public string GetText() => interactableText.Value;



    /// <summary>
    /// Checks if a player can interact
    /// </summary>
    public bool CanInteract()
    {
        switch (interactMode.Value)
        {
            case InteractMode.All:
                return canInteract;

            case InteractMode.Host:
                return canInteract && IsServer;
        }

        return false;
    }


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
    /// Enable or disable interactable
    /// </summary>
    public void SetInteractable(bool interact)
    {
        canInteract = interact;
    }




    /// <summary>
    /// SERVER ONLY Set the interact text
    /// </summary>
    public void SetText(string text)
    {
        interactableText.Value = text;
    }


    /// <summary>
    /// SERVER ONLY Set the interact mode for the interactable
    /// </summary>
    public void SetInteractMode(InteractMode mode)
    {
        interactMode.Value = mode;
    }
}