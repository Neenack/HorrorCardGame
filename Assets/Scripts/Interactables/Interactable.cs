using System;
using Unity.Netcode;
using UnityEngine;


public enum InteractMode
{
    All, Host
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

    private NetworkVariable<InteractMode> interactMode;
    private NetworkVariable<InteractDisplay> interactableDisplay;

    [SerializeField] private InteractMode defaultInteractMode = InteractMode.All;

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

        interactMode = new NetworkVariable<InteractMode>(
        defaultInteractMode,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    }


    /// <summary>
    /// Returns the text display for interacting
    /// </summary>
    public InteractDisplay GetDisplay() => interactableDisplay.Value;



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
            return;
        }

        InteractServerRpc(playerID);

        OnInteract?.Invoke(this, new InteractEventArgs(playerID));
    }

    [ServerRpc(RequireOwnership = false)]
    private void InteractServerRpc(ulong playerID)
    {

        PlayerData data = PlayerManager.Instance.GetPlayerDataById(playerID);
        ConsoleLog.Instance.Log($"{data.GetName()} has interacted with {gameObject.name}");
    }

    /// <summary>
    /// Enable or disable interactable
    /// </summary>
    public void SetInteractable(bool interact)
    {
        canInteract = interact;
    }




    /// <summary>
    /// SERVER ONLY Set the interact display
    /// </summary>
    public void SetDisplay(InteractDisplay display)
    {
        interactableDisplay.Value = display;
    }


    /// <summary>
    /// SERVER ONLY Set the interact mode for the interactable
    /// </summary>
    public void SetInteractMode(InteractMode mode)
    {
        interactMode.Value = mode;
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