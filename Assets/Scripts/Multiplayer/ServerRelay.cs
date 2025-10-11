using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class ServerRelay : MonoSingleton<ServerRelay>
{
    private string joinCode = "";
    public string JoinCode => joinCode.ToUpper();

    private bool isClientConnecting = false;
    public bool IsClientConnecting => isClientConnecting;

    public async Task<string> TryCreateRelay()
    {
        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(3);
            joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            ConsoleLog.Instance.Log("Join Code: " + joinCode);

            var relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls");
            var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
            utp.SetRelayServerData(relayServerData);

            NetworkManager.Singleton.StartHost();

            return joinCode;
        }
        catch (RelayServiceException e)
        {
            Debug.Log(e);
            return null;
        }
    }

    // Made async to be awaitable
    public async Task TryJoinRelayAsync(string joinCode)
    {
        isClientConnecting = true;

        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            RelayServerData relayServerData = AllocationUtils.ToRelayServerData(joinAllocation, "dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

            NetworkManager.Singleton.StartClient();

            while (!NetworkManager.Singleton.IsClient || !NetworkManager.Singleton.IsConnectedClient)
            {
                await Task.Yield();
            }

            Debug.Log("Successfully connected to relay!");
        }
        catch (RelayServiceException e)
        {
            Debug.Log(e);
        }
        finally
        {
            isClientConnecting = false;
        }
    }

    // Keep the old method for backward compatibility if needed
    public async void TryJoinRelay(string joinCode)
    {
        await TryJoinRelayAsync(joinCode);
    }
}