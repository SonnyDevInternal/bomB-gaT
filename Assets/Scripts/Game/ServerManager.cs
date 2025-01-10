using Newtonsoft.Json;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.Networking;
using UnityEngine.UI;

public struct PlayerData
{
    public ulong connectionid;
    public ulong entityID;
    public string playerName;
}

public struct PlayersData : INetworkSerializable
{
    public PlayerData[] Players;

    public PlayersData(PlayerData[] players)
    {
        Players = players;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        if (serializer.IsReader)
        {
            int length = 0;
            serializer.SerializeValue(ref length);
            Players = new PlayerData[length];
            for (int i = 0; i < length; i++)
            {
                serializer.SerializeValue(ref Players[i].connectionid);
                serializer.SerializeValue(ref Players[i].entityID);
                serializer.SerializeValue(ref Players[i].playerName);
            }
        }
        else
        {
            int length = Players?.Length ?? 0;
            serializer.SerializeValue(ref length);
            for (int i = 0; i < length; i++)
            {
                serializer.SerializeValue(ref Players[i].connectionid);
                serializer.SerializeValue(ref Players[i].entityID);
                serializer.SerializeValue(ref Players[i].playerName);
            }
        }
    }
}

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
    private float defaultJumpHeight = 15.0f;
    private float defaultHeadHeight = 1.0f;

    private List<Player> playerList = new List<Player>();
    private Dictionary<ulong, Player> playerIDDictionary = new Dictionary<ulong, Player>();

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

        NetworkManager.Singleton.OnClientConnectedCallback += OnPlayerCompletlyConnectedInternal;
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
            if(Cookie == null)
                NetworkManager.Singleton.DisconnectClient(connectionID);

            var transform = spawnPoint.transform;

            var instance = Instantiate(playerPrefab, transform.position, transform.rotation);

            var netObjComp = instance.GetComponent<NetworkObject>();
            var playerComp = netObjComp.GetComponent<Player>();

            playerComp.jumpHeight = defaultJumpHeight;
            playerComp.movementSpeed = defaultPlayerSpeed;

            if (connectionID != 0)
            {
                playerComp.BindOnDestroy(OnPlayerDestroyed);
            }
                

            netObjComp.Spawn(true);

            var senderID = ServerRpcParams.Receive.SenderClientId;

            if (connectionID != 0)
                playerComp.id = connectionID;
            else
                playerComp.id = senderID;

            StartCoroutine(this.FindPlayerName(netObjComp.NetworkObjectId, connectionID, Cookie));

            CreateLocalPlayerRpc(netObjComp.NetworkObjectId, RpcTarget.Single(playerComp.id, RpcTargetUse.Temp));
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    public void CreateLocalPlayerRpc(ulong EntityID, RpcParams toSender)
    {
        var localPlayer = GetNetworkObject(EntityID);

        var inst = Instantiate(localPlayerPrefab, localPlayer.transform.position + (Vector3.up * defaultHeadHeight), localPlayer.transform.rotation);

        inst.transform.parent = localPlayer.transform;

        inst.transform.rotation = localPlayer.transform.rotation;

        var localPlayerComp = inst.GetComponent<LocalPlayer>();

        localPlayerComp.BindNetworkPlayer(localPlayer.GetComponent<Player>());
        localPlayerComp.OnInitializedLocalPlayer();
    }


    public IEnumerator<UnityWebRequestAsyncOperation> FindPlayerName(ulong entityID, ulong connectionID, string Cookie)
    {
        if(IsHost)
        {
            var webrequest = UnityWebRequest.Get($"{host}/Api/User.php?cookie={Cookie}");

            yield return webrequest.SendWebRequest();

            if (webrequest.responseCode >= 400)
            {
                NetworkManager.Singleton.DisconnectClient(connectionID);
            }
            else
            {
                try
                {
                    var response = JsonConvert.DeserializeObject<Dictionary<string, string>>(webrequest.downloadHandler.text);

                    if (response != null && response.TryGetValue("ID", out string name))
                    {
                        ShareNewNameClientRpc(entityID, name);
                    }
                    else
                    {
                        NetworkManager.Singleton.DisconnectClient(connectionID);
                    }
                }
                catch (JsonException e)
                {
                    NetworkManager.Singleton.DisconnectClient(connectionID);
                }
            }
        }
    }

    [ClientRpc]
    public void ShareNewNameClientRpc(ulong entityID, string newName)
    {
        Debug.Log("Name is: " + newName);

        var obj = GetNetworkObject(entityID);

        if (obj)
        {
            var playerComp = obj.GetComponent<Player>();

            if (playerComp)
            {
                playerComp.playerName = newName;
            }
            else
                Debug.Log("Object didnt have PlayerComponent");
        }
        else
            Debug.Log("Object id was invalid");
    }

    [Rpc(SendTo.Server)]
    public void OnPlayerCompletlyConnectedRpc(string cookie, RpcParams rpcParams = default)
    {
        ulong id = rpcParams.Receive.SenderClientId;

        var playerList = GetPlayerList();

        if(!playerList.ContainsKey(id))
        {
            CreatePlayerServerRpc(cookie, id);
        }
        else
        {
            NetworkManager.Singleton.DisconnectClient(id);
        }
    }

    [Rpc(SendTo.Server)]
    public void GetServerPlayerDataRpc(RpcParams rpcParams = default)
    {
        List<PlayerData> playerDataList = new List<PlayerData>(playerList.Count);

        for (int i = 0; i < playerList.Count; i++)
        {
            PlayerData player = new PlayerData();

            player.playerName = playerList[i].playerName;
            player.entityID = playerList[i].NetworkObjectId;
            player.connectionid = playerList[i].id;

            playerDataList.Add(player);
        }

        SendPlayerDataClientRpc(new PlayersData(playerDataList.ToArray()), RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    public void SendPlayerDataClientRpc(PlayersData receivingData, RpcParams rpcParams)
    {
        var playerArray = receivingData.Players;

        Debug.Log($"Received Data of: {playerArray.Length} Clients!");

        for (int i = 0; i < playerList.Count; i++)
        {
            if (!playerList[i].isLocalPlayer)
            {
                for (int i1 = 0; i1 < playerArray.Length; i1++)
                {
                    if(playerArray[i].entityID == playerList[i].NetworkObjectId)
                    {
                        playerList[i].id = playerArray[i].connectionid;
                        playerList[i].playerName = playerArray[i].playerName;
                    }
                }
            }
        }
    }

    private void OnPlayerCompletlyConnectedInternal(ulong id)
    {
        NetworkManager.Singleton.OnClientConnectedCallback -= OnPlayerCompletlyConnectedInternal;

        OnPlayerCompletlyConnectedRpc(Login.currentCookie);
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

    public Dictionary<ulong, Player> GetPlayerList()
    {
        Dictionary<ulong, Player> playerList = new Dictionary<ulong, Player>(this.playerList.Count);

        for (int i = 0; i < this.playerList.Count; i++)
        {
            playerList[this.playerList[i].id] = this.playerList[i];
        }

        return playerList;
    }



    public void AddPlayer(Player player)
    {
        playerList.Add(player);

        playerIDDictionary = GetPlayerList();
    }

    public void RemovePlayer(Player player)
    {
        playerList.Remove(player);

        playerIDDictionary = GetPlayerList();
    }
}
