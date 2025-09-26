using System;
using UnityEngine;
using static Interactable;


public interface IInteractable
{
    public event EventHandler<InteractEventArgs> OnInteract;

    public bool CanInteract();
    public void SetInteractable(bool interact);
    public void SetInteractMode(InteractMode mode);
    public void Interact(ulong playerID);
    public string GetText();
    public void SetText(string text);
}
