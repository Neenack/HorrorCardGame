using TMPro;
using UnityEngine;

public class PlayerInteractUI : MonoBehaviour
{
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private TextMeshProUGUI interactText;

    [Header ("Interact Box")]
    [SerializeField] private GameObject interactBox;
    [SerializeField] private TextMeshProUGUI interactBoxTitle;
    [SerializeField] private TextMeshProUGUI interactBoxBody;


    private void Update()
    {
        IInteractable interactable = interactor.GetCurrentInteractable();

        if (interactable != null)
        {
            InteractDisplay display = interactable.GetDisplay();

            interactText.gameObject.SetActive(true);
            interactText.text = display.interactText.ToString();

            if (display.showInteractBox)
            {
                interactBox.gameObject.SetActive(true);
                interactBoxTitle.text = display.interactBoxTitle.ToString();
                interactBoxBody.text = display.interactBoxDescription.ToString();
            }
            else interactBox.gameObject.SetActive(false);
        }
        else
        {
            interactText.gameObject.SetActive(false);
            interactBox.gameObject.SetActive(false);
        }
    }
}
