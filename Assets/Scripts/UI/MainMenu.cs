using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoSingleton<MainMenu>
{
    public event Action OnOpen;
    public event Action OnClose;

    public event Action OnMainMenuRestartGame;

    [Header("References")]
    [SerializeField] private GameObject pauseMenuUI;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button quitButton;

    private bool isPaused = false;
    private bool menuEnabled = false;

    private void Start()
    {
        // Ensure menu is hidden at start
        pauseMenuUI.SetActive(false);

        // Subscribe button events
        resumeButton.onClick.AddListener(ResumeGame);
        restartButton.onClick.AddListener(RestartGame);
        quitButton.onClick.AddListener(QuitGame);

        LobbyManager.Instance.OnGameStarted += Instance_OnGameStarted;
    }

    private void Instance_OnGameStarted(object sender, EventArgs e)
    {
        menuEnabled = true;
    }

    void Update()
    {
        if (menuEnabled && Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
                ResumeGame();
            else
                PauseGame();
        }
    }

    private void PauseGame()
    {
        isPaused = true;
        pauseMenuUI.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        OnOpen?.Invoke();
    }

    private void ResumeGame()
    {
        isPaused = false;
        pauseMenuUI.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        OnClose?.Invoke();
    }

    private void RestartGame()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            OnMainMenuRestartGame?.Invoke();
            ResumeGame();
        }
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
