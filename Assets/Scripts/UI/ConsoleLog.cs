using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.Netcode;

public class ConsoleLog : NetworkSingleton<ConsoleLog>
{
    [Header("UI References")]
    [SerializeField] private GameObject consoleLogBox;
    [SerializeField] private TextMeshProUGUI logText;

    [Header("Settings")]
    [SerializeField] private int maxLogs = 10;

    private Queue<string> logQueue = new Queue<string>();

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer) consoleLogBox.SetActive(false);
    }

    private void Update()
    {
        if (!IsServer) return;

        if (Input.GetKeyDown(KeyCode.P))
        {
            consoleLogBox.SetActive(!consoleLogBox.activeSelf);
        }
    }

    /// <summary>
    /// Adds a log to the console
    /// </summary>
    public void Log(string message)
    {
        if (!IsServer)
        {
            LogServerRpc(message);
            return;
        }

        message = $"[Server] {message}";

        // Add new message
        logQueue.Enqueue(message);

        // Remove oldest if we exceed the max
        if (logQueue.Count > maxLogs)
            logQueue.Dequeue();

        // Rebuild text
        logText.text = string.Join("\n", logQueue);
    }

    [ServerRpc(RequireOwnership = false)] private void LogServerRpc(string message) => Log(message);
}
