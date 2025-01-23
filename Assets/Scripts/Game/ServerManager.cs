using Assets.Scripts;
using Newtonsoft.Json;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public struct BombData
{
    public float BombTimer;
    public float BombTimeCurrent;
    public float BombTimerExtender;
}

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
    private GameObject bombPrefab = null;

    [SerializeField]
    private GameObject spawnPoint = null;

    [SerializeField]
    private GameObject deathPosition = null;

    [SerializeField]
    private Button HostBtn = null;

    [SerializeField]
    private Button JoinBtn = null;

    [SerializeField]
    private Button StartGameBtn = null;

    [SerializeField]
    private Text BombTimer = null;

    private BombBehaviour currentBombBehaviour = null;

    private float defaultPlayerSpeed = 6.0f;
    private float defaultPlayerRunningSpeed = 14.0f;
    private float defaultJumpHeight = 8.0f;
    private float defaultTerminalVelocity = 30.0f;
    private float defaultPlayerDrag = 40.0f;
    private float defaultPlayerGravity = 17.0f;

    private float defaultDegenStamina = 20.0f;
    private float defaultRegenStamina = 10.0f;
    private float defaultMaxStamina = 100.0f;

    private float defaultHeadHeight = 1.0f;

    private float defaultBombTimer = 30.0f;
    private float defaultBombTimerCurrent = 0.0f;
    private float defaultBombTimerExtender = 0.0f;

    private float passBombReach = 2.0f;

    [SerializeField]
    private float passBombPushStrength = 10.0f;

    private bool hasGameStarted = false;

    private List<Player> playerList = new List<Player>();
    private Dictionary<ulong, Player> playerIDDictionary = new Dictionary<ulong, Player>();

    private void Start()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += HandlePlayerJoin;

        JoinBtn.onClick.AddListener(this.ClientFindAndConnect);
        HostBtn.onClick.AddListener(this.HostServer);
        StartGameBtn.onClick.AddListener(this.OnStartGameBtnServerRpc);

        StartGameBtn.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        base.OnDestroy();

        if(NetworkManager.Singleton)
            NetworkManager.Singleton.OnClientConnectedCallback -= HandlePlayerJoin;

        if (currentBombBehaviour)
            currentBombBehaviour.serverManager = null;
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

            playerComp.degenStaminaAmount = defaultDegenStamina;
            playerComp.gravity = defaultPlayerGravity;
            playerComp.playerDrag = defaultPlayerDrag;
            playerComp.regenStaminaAmount = defaultRegenStamina;
            playerComp.runningSpeed = defaultPlayerRunningSpeed;
            playerComp.maxStamina = defaultMaxStamina;
            playerComp.SetStamina(defaultMaxStamina);
            playerComp.terminalVelocity = defaultTerminalVelocity;

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
        var playerComp = localPlayer.GetComponent<Player>();

        localPlayerComp.BindNetworkPlayer(playerComp);
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

        var obj = GetNetworkObject(entityID);

        if (obj)
        {
            var playerComp = obj.GetComponent<Player>();

            if (playerComp)
            {
                playerComp.playerName = newName;
                playerComp.SetPlayerNameLabel();
            }
            else
                DebugClass.Log("Object didnt have PlayerComponent");
        }
        else
            DebugClass.Log("Object id was invalid");
    }

    [ClientRpc]
    private void OnEndGameBombClientRpc()
    {
        currentBombBehaviour = null;
    }

    [ClientRpc]
    private void NotifyClientsGameStartClientRpc(ulong ID)
    {
        currentBombBehaviour = GetNetworkObject(ID).GetComponent<BombBehaviour>();

        currentBombBehaviour.serverManager = this;
    }


    [ServerRpc]
    private void OnEndGameServerRpc()
    {
        currentBombBehaviour.NetworkObject.Despawn();

        OnEndGameBombClientRpc();
    }

    [ServerRpc]
    private void OnStartGameBtnServerRpc()
    {
        hasGameStarted = true;

        var instantiatedObj = Instantiate(bombPrefab, Vector3.zero, Quaternion.Euler(Vector3.zero));

        var netObj = instantiatedObj.GetComponent<NetworkObject>();

        var bombComp = netObj.GetComponent<BombBehaviour>();

        bombComp.deathPosition = deathPosition.transform.position;

        bombComp.connectingUsers = this.playerList.Count;

        netObj.Spawn();

        NotifyClientsGameStartClientRpc(netObj.NetworkObjectId);

        //bombComp.ServerActivateBombClientRpc(GetRandomPlayer().id, defaultBombTimer, defaultBombTimerCurrent, defaultBombTimerExtender);
    }

    [Rpc(SendTo.Server)]
    public void OnPlayerCompletlyConnectedRpc(string cookie, RpcParams rpcParams = default)
    {
        ulong id = rpcParams.Receive.SenderClientId;

        if (playerIDDictionary.ContainsKey(id))
        {
            DebugClass.Log("Player tried Creating an Player Instance but Already has an Connected Player Instance!");
            return;
        }

        var playerList = GetPlayerDictionary();

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
    public void TryHitPlayersWithBombRpc(RpcParams param = default)
    {
        if (!currentBombBehaviour || !currentBombBehaviour.CanBePassed())
            return;

        var player = FindPlayer(param.Receive.SenderClientId);

        if(player)
        {
            var playerTransform = player.transform;

            if(Physics.Raycast(playerTransform.position, playerTransform.forward, out RaycastHit hitInfo, passBombReach))
            {
                if(hitInfo.transform.TryGetComponent<Player>(out Player ply))
                {
                    ply.SetForceSlidedClientRpc(1.0f);

                    Player.VelocityUpdate velocityUpdate = Player.VelocityUpdate.Forward | Player.VelocityUpdate.Right;

                    ply.OnNetworkUpdatePosition_ServerRpc(ply.transform.position, (playerTransform.forward * passBombPushStrength), velocityUpdate);

                    currentBombBehaviour.ServerPassBombToPlayerServerRpc(ply.id);
                }
            }
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

                        playerList[i].SetPlayerNameLabel();
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
        DebugClass.Log("Player joined with Client ID: " + clientId);
    }
    private void HandlePlayerLeaving(ulong clientId)
    {
        DebugClass.Log("Player left with Client ID: " + clientId);

        for (int i = 0; i < playerList.Count; i++)
        {
            if (playerList[i].id == clientId)
                Destroy(playerList[i].gameObject);
        }
    }

    public Player GetRandomPlayer()
    {
        Player player = null;

        var currentGameTime = Time.time;

        if (currentGameTime < 12.0f)
            currentGameTime *= 12.0f;

        var someCalculation = currentGameTime / 6.969f;

        int calc = Mathf.RoundToInt(someCalculation);

        for (int i = 0; i < calc;)
        {
            for (int i1 = 0; i1 < playerList.Count; i1++, i++)
            {
                if(i + 1 >= calc)
                {
                    player = playerList[i1];
                    i = calc;

                    break;
                }
                    
            }
        }

        return player;
    }

    public Dictionary<ulong, Player> GetPlayerDictionary()
    {
        Dictionary<ulong, Player> playerList = new Dictionary<ulong, Player>(this.playerList.Count);

        for (int i = 0; i < this.playerList.Count; i++)
        {
            playerList[this.playerList[i].id] = this.playerList[i];
        }

        return playerList;
    }

    public List<Player> GetPlayerList()
    {
        return playerList;
    }

    public Player FindPlayer(ulong id)
    {
        Player playerOut = null;

        var playerDictionary = GetPlayerDictionary();

        if (playerDictionary.TryGetValue(id, out Player player))
        {
            playerOut = player;
        }

        return playerOut;
    }

    public void AddPlayer(Player player)
    {
        playerList.Add(player);

        playerIDDictionary = GetPlayerDictionary();

        if(playerList.Count >= 2)
            StartGameBtn.gameObject.SetActive(true);
        else
            StartGameBtn.gameObject.SetActive(false);
    }

    public void RemovePlayer(Player player)
    {
        playerList.Remove(player);

        playerIDDictionary = GetPlayerDictionary();

        if (playerList.Count < 2)
            StartGameBtn.gameObject.SetActive(false);
    }

    public BombData GetBombData()
    {
        var bombData = new BombData();

        bombData.BombTimer = defaultBombTimer;
        bombData.BombTimeCurrent = defaultBombTimerCurrent;
        bombData.BombTimerExtender = defaultBombTimerExtender;

        return bombData;
    }
}
