using TMPro;
using Unity.Netcode;
using UnityEngine;

public class AIToggleUI : MonoSingleton<AIToggleUI>
{
    [SerializeField] private TextMeshProUGUI toggleAIText;
    private bool useAI = true;

    public bool UseAI => useAI;

    private void Awake()
    {
        UpdateText();
        toggleAIText.gameObject.SetActive(false);
    }

    public void JoinGame()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            toggleAIText.gameObject.SetActive(true);
            UpdateText();
        }
    }

    private void Update()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        if (Input.GetKeyDown(KeyCode.I))
        {
            useAI = !useAI;
            UpdateText();
        }
    }

    private void UpdateText()
    {
        toggleAIText.text = "Fill with AI (I): " + useAI;
    }
}
