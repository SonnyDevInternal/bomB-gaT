using Newtonsoft.Json;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class ServerManager : NetworkBehaviour
{
    private string host = "http://localhost";

    [SerializeField]
    private GameObject playerPrefab = null;

    [SerializeField]
    private GameObject localPlayerPrefab = null;

    [SerializeField]
    private GameObject spawnPoint = null;

    [SerializeField]
    private Button HostBtn = null;

    [SerializeField]
    private Button JoinBtn = null;

    private float defaultPlayerSpeed = 6.0f;
    private float defaultJumpHeight = 4.0f;
    private float defaultHeadHeight = 1.0f;

    private List<Player> playerList = new List<Player>();

    private void Start()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += HandlePlayerJoin;

        JoinBtn.onClick.AddListener(this.ClientFindAndConnect);
        HostBtn.onClick.AddListener(this.HostServer);
    }

    private void OnDestroy()
    {
        base.OnDestroy();

        NetworkManager.Singleton.OnClientConnectedCallback -= HandlePlayerJoin;
    }

    private void Update()
    {

    }

    public void HostServer()
    {
        SetConnectionData();
        NetworkManager.Singleton.StartHost();

        CreatePlayerServerRpc(Login.currentCookie);
    }

    public void ClientFindAndConnect()
    {
        SetConnectionData();
        NetworkManager.Singleton.StartClient();

        NetworkManager.Singleton.OnClientConnectedCallback += OnPlayerCompletlyConnectedRpc;
    }

    public void SetConnectionData()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null) return;

        transport.SetConnectionData("127.0.0.1", 7979, "0.0.0.0");

    }

    [ServerRpc]
    public void CreatePlayerServerRpc(string Cookie, ulong connectionID = 0, ServerRpcParams ServerRpcParams = default)
    {
        if(IsHost)
        {
            var transform = spawnPoint.transform;

            var instance = Instantiate(playerPrefab, transform.position, transform.rotation);

            var netObjComp = instance.GetComponent<NetworkObject>();
            var playerComp = netObjComp.GetComponent<Player>();

            playerComp.jumpHeight = defaultJumpHeight;
            playerComp.movementSpeed = defaultPlayerSpeed;

            if (connectionID != 0)
                playerComp.BindOnDestroy(OnPlayerDestroyed);

            netObjComp.Spawn(true);

            var senderID = ServerRpcParams.Receive.SenderClientId;

            if (connectionID != 0)
                playerComp.id = connectionID;
            else
                playerComp.id = senderID;

            this.FindPlayerName(netObjComp.NetworkObjectId, Cookie);

            CreateLocalPlayerRpc(netObjComp.NetworkObjectId, RpcTarget.Single(playerComp.id, RpcTargetUse.Temp));
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    public void CreateLocalPlayerRpc(ulong EntityID, RpcParams toSender)
    {
        var localPlayer = GetNetworkObject(EntityID);

        var inst = Instantiate(localPlayerPrefab, localPlayer.transform.position + (Vector3.up * defaultHeadHeight), localPlayer.transform.rotation);

        inst.transform.parent = localPlayer.transform;

        inst.GetComponent<LocalPlayer>().BindNetworkPlayer(localPlayer.GetComponent<Player>());
    }

    public IEnumerator<UnityWebRequestAsyncOperation> FindPlayerName(ulong entityID, string Cookie)
    {
        if(IsHost)
        {
            var webrequest = UnityWebRequest.Get($"{host}/Api/User.php/{Cookie}/");

            yield return webrequest.SendWebRequest();

            if (webrequest.responseCode >= 400)
            {
                ShareNewNameClientRpc(entityID, $"Unknown Player");

                Debug.Log("Name was unknown");
            }
            else
            {
                try
                {
                    var response = JsonConvert.DeserializeObject<Dictionary<string, string>>(webrequest.downloadHandler.text);

                    if (response.TryGetValue("ID", out string name))
                    {
                        ShareNewNameClientRpc(entityID, name);
                    }
                    else
                        ShareNewNameClientRpc(entityID, $"Unknown Player");
                }
                catch (JsonException e)
                {
                    Debug.LogError("Failed to parse JSON: " + e.Message);

                    ShareNewNameClientRpc(entityID, $"Unknown Player");
                }
            }
        }
    }

    [ClientRpc]
    public void ShareNewNameClientRpc(ulong entityID, string newName)
    {
        Debug.Log("Name is: " + newName);

        var obj = GetNetworkObject(entityID);

        if(obj)
        {
            var playerComp = GetComponent<Player>();

            if(playerComp)
                playerComp.playerName = newName;
        }
    }

    [Rpc(SendTo.Server)]
    public void OnPlayerCompletlyConnectedRpc(ulong id)
    {
        NetworkManager.Singleton.OnClientConnectedCallback -= OnPlayerCompletlyConnectedRpc;

        CreatePlayerServerRpc(Login.currentCookie, id);
    }
    private void OnPlayerDestroyed(Player player, bool ByScene)
    {
        RemovePlayer(player);
    }

    private void HandlePlayerJoin(ulong clientId)
    {
        Debug.Log("Player joined with Client ID: " + clientId);
    }
    private void HandlePlayerLeaving(ulong clientId)
    {
        Debug.Log("Player left with Client ID: " + clientId);

        for (int i = 0; i < playerList.Count; i++)
        {
            if (playerList[i].id == clientId)
                Destroy(playerList[i].gameObject);
        }
    }

    public void AddPlayer(Player player)
    {
        playerList.Add(player);
    }

    public void RemovePlayer(Player player)
    {
        playerList.Remove(player);
    }
}
