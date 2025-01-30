using Assets.Scripts;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using static Player;

public struct Translated_PlayerMovement
{
    public int shufflingTimes;
    public int Forward;
    public int Backward;
    public int Right;
    public int Left;
    public int Up;

    public Translated_PlayerMovement(int shufflingTimes)
    {
        this.shufflingTimes = shufflingTimes;

        this.Forward = 1 << 1;
        this.Backward = 1 << 2;
        this.Right = 1 << 3;
        this.Left = 1 << 4;
        this.Up = 1 << 5;
    }

    public void Shuffle()
    {
        for (int i = 0; i < this.shufflingTimes; i++)
        {
            var forwardValue = this.Forward;

            this.Forward = Backward;
            this.Backward = Right;
            this.Right = Left;
            this.Left = Up;
            this.Up = forwardValue;
            //this.Down = backwardValue;
        }
    }
}

public class BombBehaviour : ServerObject
{
    public Vector3 deathPosition = Vector3.zero;

    private BombPlayer owningPlayer = null;
    public BombGameServerManager serverManager = null;

    public Collider collider = null;

    [SerializeField]
    private TMPro.TextMeshPro EndGameText = null;

    private bool hasExploded = false;
    private bool hasActivatedBomb = false;

    private float delayPassBombTimer = 0.4f;
    private float delayPassBombTimerCurrent = 0.4f;

    private float detonationTimer = 0.0f;

    [SerializeField]
    private float detonationTimerCurrent = 0.0f;

    private float detonationTimerExtender = 0.6f;

    private List<ulong> connectedUsers = new List<ulong>();
    private List<ulong> aliveUsers = null;

    private BombPlayer movementHookedPlayer = null; //in case of abrupt destruction, free hook from player
    private Translated_PlayerMovement currentMovementTranslation = new Translated_PlayerMovement(1);

    public int connectingUsers = 0;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        SendBombHasBeenLoadedRpc();
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if(IsHost)
            UnhookPlayerMovement_ServerRpc();
    }

    protected override void OnUpdate()
    {
        if(IsHost)
        {
            if(hasActivatedBomb)
            {
                if (detonationTimerCurrent >= detonationTimer)
                {
                    detonationTimerCurrent = 0.0f;

                    OnPlayerDie_ServerRpc();
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


    [ClientRpc]
    private void ServerExplodeBombClientRpc()
    {
        if(!hasExploded)
        {
            hasExploded = true;

            SpawnExplodeEffect();
        }
    }

    [ClientRpc]
    private void UpdateBombTimerClientRpc(float detonationTimerCurrent)
    {
        this.detonationTimerCurrent = detonationTimerCurrent;
    }


    [ClientRpc]
    private void ServerActivateBombClientRpc(ulong IDToGive, float detonationTimer, float detonationTimerCurrent, float detonationTimerExtender) //call to start Timer!
    {
        if (hasActivatedBomb)
            return;

        hasActivatedBomb = true;

        this.detonationTimer = detonationTimer;
        this.detonationTimerCurrent = detonationTimerCurrent;
        this.detonationTimerExtender = detonationTimerExtender;

        SetBombColliders(false);
    }

    [ClientRpc]
    private void ClientPassBombClientRpc(ulong PlayerID)
    {
        owningPlayer = (BombPlayer)serverManager.FindPlayer(PlayerID);
    }

    [ServerRpc]
    public void ServerPassBombToPlayerServerRpc(ulong PlayerID)
    {
        if (owningPlayer != null)
        {
            if (aliveUsers.Contains(owningPlayer.id))
            {
                owningPlayer.PlayerChangeBombState_ServerRpc(false);
                owningPlayer.isConsumingStamina = true;
            }
        }

        this.delayPassBombTimerCurrent = 0.0f;

        var percentage = (this.detonationTimerCurrent / this.detonationTimer);

        if (percentage > 0.86f)
        {
            detonationTimerCurrent -= this.detonationTimerExtender;
        }

        UnhookPlayerMovement_ServerRpc();

        owningPlayer = (BombPlayer)serverManager.FindPlayer(PlayerID);

        if(!owningPlayer)  //Check for Disconnected Players
        {
            if (aliveUsers.Count > 1)
            {
                this.connectedUsers.Remove(PlayerID);

                ServerPassBombToPlayerServerRpc(this.GetRandomPlayerID());
            }
            else
                OnGameEnd_ServerRpc();

            return;
        }

        currentMovementTranslation.Shuffle();

        owningPlayer.PlayerChangeBombState_ServerRpc(true);

        owningPlayer.isConsumingStamina = false;

        transform.SetParent(owningPlayer.transform, false);

        transform.position = owningPlayer.bombHand.transform.position;

        ServerUpdateObjectServerRpc();

        HookPlayerMovement_ServerRpc(PlayerID);

        UpdateBombTimerClientRpc(detonationTimerCurrent);
        ClientPassBombClientRpc(PlayerID);
    }

    [ServerRpc]
    private void OnPlayerDie_ServerRpc()
    {
        ServerExplodeBombClientRpc();

        owningPlayer.PlayerChangeBombState_ServerRpc(false);
        owningPlayer.isConsumingStamina = true;

        owningPlayer.KillPlayer_ServerRpc();

        aliveUsers.Remove(owningPlayer.id);

        if (aliveUsers.Count > 1)
        {
            float perc = (aliveUsers.Count / connectedUsers.Count);

            detonationTimerCurrent = (detonationTimer * perc) + 2.0f;

            owningPlayer = null;

            ServerPassBombToPlayerServerRpc(this.GetRandomPlayerID());
        }
        else
            OnGameEnd_ServerRpc();
    }

    [ServerRpc]
    private void OnGameStart_ServerRpc()
    {
        BombData bombData = serverManager.GetBombData();

        currentMovementTranslation.Shuffle();

        aliveUsers = new List<ulong>(connectedUsers);

        var randomPlayerID = serverManager.GetRandomPlayer().id;

        for (int i = 0; i < aliveUsers.Count; i++) //Teleport players to Game Area
        {
            var player = serverManager.FindPlayer(aliveUsers[i]);

            if(player)
                player.SetPositionServerRpc(serverManager.GetRandomSpawnLocation().position);
        }

        this.SetUseRigidBody_ServerRpc(false);

        ObjectUseNonServerPos_ServerRpc(true);

        ServerPassBombToPlayerServerRpc(randomPlayerID);

        ServerActivateBombClientRpc(randomPlayerID, bombData.BombTimer, bombData.BombTimeCurrent, bombData.BombTimerExtender);
    }

    [ServerRpc]
    private void OnGameEnd_ServerRpc()
    {
        DebugClass.Log("Game Ended!");

        if(aliveUsers.Count > 0)
        {
            ulong playerAliveID = aliveUsers[0];

            BombPlayer alivePlayer = (BombPlayer)serverManager.FindPlayer(playerAliveID);

            if (alivePlayer) //Check if player left
            {
                alivePlayer.SetPositionServerRpc(serverManager.GetRandomSpawnLocation(true).position);
                alivePlayer.PlayerGameResult_ServerRpc(true);
            }
            else
                DebugClass.Log("Couldnt Find Player with aliveID");
                
            connectedUsers.Remove(playerAliveID);

            for (int i = 0; i < connectedUsers.Count; i++)
            {
                BombPlayer deadPlayer = (BombPlayer)serverManager.FindPlayer(connectedUsers[i]);

                if (deadPlayer) //Check if player left
                {
                    deadPlayer.PlayerGameResult_ServerRpc(false);
                    deadPlayer.RevivePlayer_ServerRpc();
                }
            }
        }

        serverManager.OnEndGameServerRpc();
    }

    [Rpc(SendTo.Server)]
    private void SendBombHasBeenLoadedRpc(RpcParams rpcParams = default)
    {
        var SenderClientID = rpcParams.Receive.SenderClientId;

        if (connectedUsers.Exists(x => x == SenderClientID))
        {
            DebugClass.Log($"Player with id {SenderClientID}, tried Sending Load Request twice!");
            return;
        }
        else
        {
            int connected = 0;

            connectedUsers.Add(SenderClientID);

            for (int i = 0; i < connectedUsers.Count; i++)
            {
                if (NetworkManager.ConnectedClients.ContainsKey(connectedUsers[i]))
                {
                    connected++;
                }
                else
                    connectedUsers.Remove(connectedUsers[i]);
            }

            if(connected >= connectingUsers)
            {
                DebugClass.Log("All Clients Loaded, Game Starting!");

                OnGameStart_ServerRpc();
            }
        }
    }

    [ServerRpc]
    private void UnhookPlayerMovement_ServerRpc()
    {
        if (movementHookedPlayer && movementHookedPlayer.IsMovementHooked())
        {
            movementHookedPlayer.UnhookOnMovePlayer();
            movementHookedPlayer = null;
        }
            
    }

    [ServerRpc]
    private void HookPlayerMovement_ServerRpc(ulong playerID)
    {
        var player = (BombPlayer)serverManager.FindPlayer(playerID);

        if (player && !player.IsMovementHooked())
        {
            player.HookOnMovePlayer(this.OnMovePlayerHook);
            movementHookedPlayer = player;
        }
    }

    private PlayerMovement OnMovePlayerHook(Player _this, PlayerMovement originalMovement)
    {
        return TranslateMovementToCurrent(originalMovement);
    }

    private void SetBombColliders(bool active)
    {
        var colliders = GetComponentsInChildren<Collider>();

        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = active;
        }
    }

    private PlayerMovement TranslateMovementToCurrent(PlayerMovement movement)
    {
        PlayerMovement out_ = PlayerMovement.None;

        if ((movement & PlayerMovement.Forward) == PlayerMovement.Forward)
            out_ |= (PlayerMovement)currentMovementTranslation.Forward;

        if ((movement & PlayerMovement.Backward) == PlayerMovement.Backward)
            out_ |= (PlayerMovement)currentMovementTranslation.Backward;

        if ((movement & PlayerMovement.Right) == PlayerMovement.Right)
            out_ |= (PlayerMovement)currentMovementTranslation.Right;

        if ((movement & PlayerMovement.Left) == PlayerMovement.Left)
            out_ |= (PlayerMovement)currentMovementTranslation.Left;

        if ((movement & PlayerMovement.Up) == PlayerMovement.Up)
            out_ |= (PlayerMovement)currentMovementTranslation.Up;

        if ((movement & PlayerMovement.Down) == PlayerMovement.Down)
            out_ |= PlayerMovement.Down;

        out_ |= PlayerMovement.FastForward;

        return out_;
    }

    private ulong GetRandomPlayerID()
    {
        ulong player = 0;

        var currentGameTime = Time.time;

        if (currentGameTime < 12.0f)
            currentGameTime *= 12.0f;

        var someCalculation = currentGameTime / 6.969f;

        int calc = Mathf.RoundToInt(someCalculation);

        for (int i = 0; i < calc;)
        {
            for (int i1 = 0; i1 < aliveUsers.Count; i1++, i++)
            {
                if (i + 1 >= calc)
                {
                    player = aliveUsers[i1];
                    i = calc;

                    break;
                }

            }
        }

        return player;
    }

    public bool CanBePassed()
    {
        return delayPassBombTimerCurrent >= delayPassBombTimer;
    }

    public bool IsValidPassablePlayer(ulong ID)
    {
        return aliveUsers.Contains(ID); 
    }
}
