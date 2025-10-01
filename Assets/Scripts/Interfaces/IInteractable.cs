using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using static Interactable;

[GenerateSerializationForType(typeof(InteractDisplay))]
public static class GeneratedInteractDisplaySerialization { }

public struct InteractDisplay : INetworkSerializeByMemcpy
{
    public FixedString128Bytes interactText;
    public bool showInteractBox;
    public FixedString64Bytes interactBoxTitle;
    public FixedString512Bytes interactBoxDescription;

    public InteractDisplay(string text, bool showInteractBox = false, string boxTitle = "", string interactBoxBody = "")
    {
        interactText = text;
        this.showInteractBox = showInteractBox;
        interactBoxTitle = boxTitle;
        interactBoxDescription = interactBoxBody;
    }
}


public interface IInteractable
{
    public event EventHandler<InteractEventArgs> OnInteract;

    public bool CanInteract();
    public void SetInteractable(bool interact);
    public void SetInteractMode(InteractMode mode);
    public void Interact(ulong playerID);



    public NetworkVariable<InteractDisplay> GetDisplay();
    public void SetDisplay(InteractDisplay display);
    public void ResetDisplay();
}
