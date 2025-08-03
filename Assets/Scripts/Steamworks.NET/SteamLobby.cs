using UnityEngine;
using Mirror;
using Steamworks;
using UnityEngine.UI;

public class SteamLobby : MonoBehaviour
{
    [SerializeField] private NetworkManager _networkManager;
    [SerializeField] private Button _hostButton = null;
    protected Callback<LobbyCreated_t> LobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> GameLobbyJoinRequested;
    protected Callback<LobbyEnter_t> LobbyEntered;
    private const string HOST_ADDRESS_KEY = "HostAddress";

    private void Start()
    {
        if (!SteamManager.Initialized) { return; }
        LobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        GameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        LobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        _hostButton.onClick.AddListener(HostLobby);
    }



    public void HostLobby()
    {
        _hostButton.gameObject.SetActive(false);
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, _networkManager.maxConnections);
    }



    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            _hostButton.gameObject.SetActive(true);
            return;
        }
        _networkManager.StartHost();
        SteamMatchmaking.SetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), HOST_ADDRESS_KEY, SteamUser.GetSteamID().ToString());
    }

    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    private void OnLobbyEntered(LobbyEnter_t callback)
    {

        if (NetworkServer.active)
        {
            return;
        }
        string hostAddress = SteamMatchmaking.GetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), HOST_ADDRESS_KEY);
        _networkManager.networkAddress = hostAddress;
        _networkManager.StartClient();
        _hostButton.gameObject.SetActive(false);
    }
}
