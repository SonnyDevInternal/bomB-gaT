using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class BombBehaviour : ServerObject
{
    public Vector3 deathPosition = Vector3.zero;

    private Player owningPlayer = null;
    public ServerManager serverManager = null;

    public Collider collider = null;

    private bool hasExploded = false;
    private bool hasActivatedBomb = false;

    private float delayPassBombTimer = 0.4f;
    private float delayPassBombTimerCurrent = 0.4f;

    private float detonationTimer = 0.0f;

    [SerializeField]
    private float detonationTimerCurrent = 0.0f;

    private float detonationTimerExtender = 0.6f;

    private List<ulong> connectedUsers = new List<ulong>();
    public int connectingUsers = 0;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        SendBombHasBeenLoadedRpc();
    }

    protected override void OnUpdate()
    {
        if(owningPlayer)
        {
            if (!isKinematic())
                SetUseRigidBodyServerRpc(false);
        }

        if(IsHost)
        {
            if(hasActivatedBomb)
            {
                if (detonationTimerCurrent >= detonationTimer)
                {
                    detonationTimerCurrent = 0.0f;

                    owningPlayer.SetCanMoveServerRpc(false);

                    owningPlayer.SetPositionServerRpc(deathPosition);

                    owningPlayer = null;

                    ServerExplodeBombClientRpc();

                    NetworkObject.Despawn();
                }
                else
                {
                    detonationTimerCurrent += Time.deltaTime;
                    delayPassBombTimerCurrent += Time.deltaTime;

                    this.transform.position = owningPlayer.bombHand.transform.position;
                    this.transform.rotation = owningPlayer.bombHand.transform.rotation;

                    UpdateBombTimerClientRpc(detonationTimerCurrent);
                    ServerUpdateObjectServerRpc();
                }
            }
        }
    }

    private void SpawnExplodeEffect()
    {

    }

    public bool CanBePassed()
    {
        return delayPassBombTimerCurrent >= delayPassBombTimer;
    }


    [ClientRpc]
    public void ServerExplodeBombClientRpc()
    {
        if(!hasExploded)
        {
            hasExploded = true;

            SpawnExplodeEffect();
        }
    }

    [ClientRpc]
    public void UpdateBombTimerClientRpc(float detonationTimerCurrent)
    {
        this.detonationTimerCurrent = detonationTimerCurrent;
    }


    [ClientRpc]
    public void ServerActivateBombClientRpc(ulong IDToGive, float detonationTimer, float detonationTimerCurrent, float detonationTimerExtender) //call to start Timer!
    {
        if (hasActivatedBomb)
            return;

        hasActivatedBomb = true;

        this.detonationTimer = detonationTimer;
        this.detonationTimerCurrent = detonationTimerCurrent;
        this.detonationTimerExtender = detonationTimerExtender;

        owningPlayer = serverManager.FindPlayer(IDToGive);

        var colliders = GetComponentsInChildren<Collider>();

        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
    }

    [ClientRpc]
    public void ClientPassBombClientRpc(ulong PlayerID)
    {
        owningPlayer = serverManager.FindPlayer(PlayerID);
    }

    [ServerRpc]
    public void ServerPassBombToPlayerServerRpc(ulong PlayerID)
    {
        var percentage = (this.detonationTimerCurrent / this.detonationTimer);

        if (percentage > 0.86f)
        {
            detonationTimerCurrent += this.detonationTimerExtender;
        }

        UpdateBombTimerClientRpc(detonationTimerCurrent);
        ClientPassBombClientRpc(PlayerID);
    }

    [Rpc(SendTo.Server)]
    public void SendBombHasBeenLoadedRpc(RpcParams rpcParams = default)
    {
        if(connectedUsers.Exists(x => x == rpcParams.Receive.SenderClientId))
        {
            Debug.Log($"Player with id {rpcParams.Receive.SenderClientId}, tried Sending Load Request twice!");
            return;
        }
        else
        {
            int connected = 0;

            connectedUsers.Add(rpcParams.Receive.SenderClientId);

            for (int i = 0; i < connectedUsers.Count; i++)
            {
                if (NetworkManager.ConnectedClients.ContainsKey(connectedUsers[i]))
                    connected++;
                else
                    connectedUsers.Remove(connectedUsers[i]);
            }

            if(connected >= connectingUsers)
            {
                Debug.Log("All Clients Loaded, Game Starting!");

                ServerActivateBombClientRpc(serverManager.GetRandomPlayer().id, serverManager.defaultBombTimer, serverManager.defaultBombTimerCurrent, serverManager.defaultBombTimerExtender);
            }
        }
    }
}
