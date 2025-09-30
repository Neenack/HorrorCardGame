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

    public InteractDisplay(string text, bool showInteractBox = false, string boxTitle = null, string interactBoxBody = null)
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

    //public string GetText();
    //public void SetText(string text);

    public InteractDisplay GetDisplay();
    public void SetDisplay(InteractDisplay display);
}
