using System;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class PlayerInteractUI : NetworkBehaviour
{
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private TextMeshProUGUI interactText;

    [Header ("Interact Box")]
    [SerializeField] private GameObject interactBox;
    [SerializeField] private TextMeshProUGUI interactBoxTitle;
    [SerializeField] private TextMeshProUGUI interactBoxBody;

    private IInteractable interactable;

    private void Awake()
    {
        DisableUI();
    }

    public override void OnNetworkSpawn()
    {
        interactor.OnInteractTargetUpdated += Interactor_OnInteractTargetUpdated;
    }

    public override void OnNetworkDespawn()
    {
        interactor.OnInteractTargetUpdated -= Interactor_OnInteractTargetUpdated;
    }

    private void DisableUI()
    {
        interactText.text = "";
        interactBox.SetActive(false);
    }

    private void Interactor_OnInteractTargetUpdated()
    {
        interactable = interactor.GetCurrentInteractable();

        if (interactable != null)
        {
            SetDisplay(interactable.GetDisplay());
        }
        else
        {
            DisableUI();
        }
    }

    private void SetDisplay(InteractDisplay display)
    {
        interactText.text = display.interactText.ToString();
        interactBox.SetActive(display.showInteractBox);
        interactBoxTitle.text = display.interactBoxTitle.ToString();
        interactBoxBody.text = display.interactBoxDescription.ToString();
    }
}
