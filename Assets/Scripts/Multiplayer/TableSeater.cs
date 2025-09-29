using System.Linq;
using Unity.Netcode;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

public class TableSeater : NetworkSingleton<TableSeater>
{
    private ITable table;

    private void Awake()
    {
        ITable temp = GetComponent<ITable>();
        if (temp != null)
        {
            table = temp;
            return;
        }

        GameObject cardGame = GameObject.FindWithTag("CardGame");
        table = cardGame.GetComponent<ITable>();
    }

    public Transform TrySetPlayerAtTable(PlayerData data) => table.TrySetPlayerAtTable(data);
}
