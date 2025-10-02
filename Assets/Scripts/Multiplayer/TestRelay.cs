using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class TestRelay : MonoSingleton<TestRelay>
{
    [SerializeField] private int maxPlayers = 1;

    private string joinCode = "";
    public string JoinCode => joinCode.ToUpper();

    private async void Start()
    {
        await UnityServices.InitializeAsync();
        AuthenticationService.Instance.SignedIn += () =>
        {
            Debug.Log("Signed in " + AuthenticationService.Instance.PlayerId);
        };
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    public async Task<bool> TryCreateRelay()
    {
        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers);

            joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            ConsoleLog.Instance.Log("Join Code: " + joinCode);

            var relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls");
            var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
            utp.SetRelayServerData(relayServerData);
            NetworkManager.Singleton.StartHost();
            return true;
        }
        catch (RelayServiceException e)
        {
            Debug.Log(e);
            return false;
        }
    }

    public async Task<bool> TryJoinRelay(string joinCode)
    {
        this.joinCode = joinCode;

        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            RelayServerData relayServerData = AllocationUtils.ToRelayServerData(joinAllocation, "dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);
            NetworkManager.Singleton.StartClient();
            return true;
        }
        catch (RelayServiceException e)
        {
            Debug.Log(e);
            return false;
        }
    }
}
