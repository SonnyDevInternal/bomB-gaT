using Assets.Scripts;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public struct BombData
{
    public float BombTimer;
    public float BombTimeCurrent;
    public float BombTimerExtender;
}

public class BombGameServerManager : ServerManager
{
    [SerializeField]
    private GameObject bombPrefab = null;

    [SerializeField]
    private Text BombTimer = null;

    private BombBehaviour currentBombBehaviour = null;

    [SerializeField]
    private float passBombPushStrength = 10.0f;

    private float defaultBombTimer = 30.0f;
    private float defaultBombTimerCurrent = 0.0f;
    private float defaultBombTimerExtender = 0.0f;

    private float passBombReach = 2.0f;

    private bool hasGameStarted = false;

    protected override void OnDestroyServer()
    {
        if (currentBombBehaviour)
            currentBombBehaviour.serverManager = null;
    }

    [ClientRpc]
    private void OnEndGameBombClientRpc()
    {
        hasGameStarted = false;
        currentBombBehaviour = null;
    }

    [ClientRpc]
    private void NotifyClientsGameStartClientRpc(ulong ID)
    {
        currentBombBehaviour = GetNetworkObject(ID).GetComponent<BombBehaviour>();

        currentBombBehaviour.serverManager = this;
    }

    [ServerRpc]
    public void OnEndGameServerRpc()
    {
        currentBombBehaviour.NetworkObject.Despawn();

        OnEndGameBombClientRpc();
    }

    [ServerRpc]
    protected override void OnStartGameBtnServerRpc()
    {
        if (hasGameStarted)
        {
            DebugClass.Log("Can´t start Game!, Game is already running!");
            return;
        }

        hasGameStarted = true;

        for (int i = 0; i < this.playerList.Count; i++)
        {
            this.playerList[i].SetPositionServerRpc(GetRandomSpawnLocation().position);
        }

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
    public void TryHitPlayersWithBombRpc(RpcParams param = default)
    {
        if (!currentBombBehaviour || !currentBombBehaviour.CanBePassed())
            return;

        var player = FindPlayer(param.Receive.SenderClientId);

        if (player)
        {
            var playerTransform = player.transform;

            if (Physics.Raycast(playerTransform.position, playerTransform.forward, out RaycastHit hitInfo, passBombReach))
            {
                if (hitInfo.transform.TryGetComponent<Player>(out Player ply) && currentBombBehaviour.IsValidPassablePlayer(ply.id))
                {
                    ply.SetForceSlidedClientRpc(1.0f);

                    Player.VelocityUpdate velocityUpdate = Player.VelocityUpdate.Forward | Player.VelocityUpdate.Right;

                    ply.OnNetworkUpdatePosition_ServerRpc(ply.transform.position, (playerTransform.forward * passBombPushStrength), velocityUpdate);

                    currentBombBehaviour.ServerPassBombToPlayerServerRpc(ply.id);
                }
            }
        }
    }

    public override Transform GetRandomSpawnLocation(bool forceOutbound = false)
    {
        Transform spawnLocation = defaultSpawnPoint.transform;

        if (!hasGameStarted ||forceOutbound)
        {
            var spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPointOutbound");

            var currentGameTime = Time.time;

            if (currentGameTime < 12.0f)
                currentGameTime *= 12.0f;

            var someCalculation = currentGameTime / 6.969f;

            int calc = Mathf.RoundToInt(someCalculation);

            for (int i = 0; i < calc;)
            {
                for (int i1 = 0; i1 < spawnPoints.Length; i1++, i++)
                {
                    if (i + 1 >= calc)
                    {
                        spawnLocation = spawnPoints[i1].transform;
                        i = calc;
                        break;
                    }

                }
            }
        }
        else
        {
            var spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPointInbound");

            var currentGameTime = Time.time;

            if (currentGameTime < 12.0f)
                currentGameTime *= 12.0f;

            var someCalculation = currentGameTime / 6.969f;

            int calc = Mathf.RoundToInt(someCalculation);

            for (int i = 0; i < calc;)
            {
                for (int i1 = 0; i1 < spawnPoints.Length; i1++, i++)
                {
                    if (i + 1 >= calc)
                    {
                        spawnLocation = spawnPoints[i1].transform;
                        i = calc;
                        break;
                    }

                }
            }
        }

        return spawnLocation;
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