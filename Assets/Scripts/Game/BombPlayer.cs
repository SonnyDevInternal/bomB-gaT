using Unity.Netcode;
using UnityEngine;

struct BombPlayerData : INetworkSerializable
{
    public PlayerUpdateData baseData;

    public char hasBomb;
    public char hasWon;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        if (serializer.IsReader)
        {
            serializer.SerializeValue(ref baseData);
            serializer.SerializeValue(ref hasBomb);
            serializer.SerializeValue(ref hasWon);
        }
        else
        {
            serializer.SerializeValue(ref baseData);
            serializer.SerializeValue(ref hasBomb);
            serializer.SerializeValue(ref hasWon);
        }
    }
}

public class BombPlayer : Player
{
    [SerializeField]
    public GameObject bombHand = null;

    public PBool hasBomb = new PBool(false);
    public PBool hasWon = new PBool(false);

    public delegate void OnChangedBombHoldState(Player _this, bool hasBomb);
    public delegate void OnGameResultState(Player _this, bool hasWon);

    private OnChangedBombHoldState onChangedBombHoldState = null;
    private OnGameResultState onGameResultState = null;

    public void BindOnChangedBombHoldState(OnChangedBombHoldState onChangedBombHoldState) { this.onChangedBombHoldState = onChangedBombHoldState; }
    public void BindOnGameResultState(OnGameResultState onGameResultState) { this.onGameResultState = onGameResultState; }

    public void UnbindOnChangedBombHoldState() { this.onChangedBombHoldState = null; }
    public void UnbindOnGameResultState() { this.onGameResultState = null; }

    [ServerRpc]
    protected override void DeframeBools_ServerRpc()
    {
        this.hasBomb.DeframeBool();
        this.hasWon.DeframeBool();
    }

    [ServerRpc]
    protected override void OnNetworkRequestUpdateData_ServerRpc()
    {
        OnNetworkUpdateData_ClientRpc(CraftBombPlayerUpdateData());
    }

    [ClientRpc]
    private void OnNetworkUpdateData_ClientRpc(BombPlayerData data_)
    {
        PlayerUpdateData data = data_.baseData;

        if (this.playerRigidBody)
        {
            this.playerRigidBody.position = data.position;
            this.playerRigidBody.linearVelocity = data.velocity;
        }

        this.currentStamina = data.stamina;

        PBool livingState = new PBool(data.isAlive);
        PBool bombState = new PBool(data_.hasBomb);
        PBool wonState = new PBool(data_.hasWon);

        switch (livingState.GetState())
        {
            case PBool.EBoolState.FalseThisFrame:
                if (onChangedLivingState != null)
                    onChangedLivingState(this, false);

                isAlive = new PBool(PBool.EBoolState.False);
                break;

            case PBool.EBoolState.TrueThisFrame:
                if (onChangedLivingState != null)
                    onChangedLivingState(this, true);

                isAlive = new PBool(PBool.EBoolState.True);
                break;
        }

        switch (bombState.GetState())
        {
            case PBool.EBoolState.FalseThisFrame:
                if (onChangedBombHoldState != null)
                    onChangedBombHoldState(this, false);

                hasBomb = new PBool(PBool.EBoolState.False);
                break;

            case PBool.EBoolState.TrueThisFrame:
                if (onChangedBombHoldState != null)
                    onChangedBombHoldState(this, true);

                hasBomb = new PBool(PBool.EBoolState.True);
                break;
        }

        switch (wonState.GetState())
        {
            case PBool.EBoolState.FalseThisFrame:
                if (onGameResultState != null)
                    onGameResultState(this, false);

                wonState = new PBool(PBool.EBoolState.False);
                break;

            case PBool.EBoolState.TrueThisFrame:
                if (onGameResultState != null)
                    onGameResultState(this, true);

                wonState = new PBool(PBool.EBoolState.True);
                break;
        }

        if (onUpdatePlayer != null)
            onUpdatePlayer(this);
    }

    private BombPlayerData CraftBombPlayerUpdateData()
    {
        BombPlayerData dataOut = new BombPlayerData();

        dataOut.baseData = CraftPlayerUpdateData();

        dataOut.hasBomb = (hasBomb.GetCharState());
        dataOut.hasWon = (hasWon.GetCharState());

        return dataOut;
    }

    [ServerRpc]
    public void PlayerChangeBombState_ServerRpc(bool hasBomb)
    {
        if (hasBomb)
            this.hasBomb = new PBool(PBool.EBoolState.TrueThisFrame);
        else
            this.hasBomb = new PBool(PBool.EBoolState.FalseThisFrame);
    }

    [ServerRpc]
    public void PlayerGameResult_ServerRpc(bool hasWon)
    {
        if (hasWon)
            this.hasWon = new PBool(PBool.EBoolState.TrueThisFrame);
        else
            this.hasWon = new PBool(PBool.EBoolState.FalseThisFrame);
    }
}
