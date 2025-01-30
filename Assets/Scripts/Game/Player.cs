using Unity.Netcode;
using UnityEngine;
using Assets.Scripts;

public struct PBool
{
    public enum EBoolState
    {
        Error = -1,
        False = 0,
        True = 1,
        FalseThisFrame = 2,
        TrueThisFrame = 3,
    }

    public char value;

    public PBool(EBoolState state)
    {
        value = (char)state;
    }

    public PBool(bool state)
    {
        value = '\0';

        value = boolToChar(state);
    }

    public PBool(char state)
    {
        value = state;
    }

    private char boolToChar(bool state)
    {
        char out_ = '\0';

        switch (state)
        {
            case false:
                out_ = '\0';
                break;
            case true:
                out_ = '\x0001';
                break;
        }

        return out_;
    }

    public void DeframeBool() //Call to deframe bool
    {
        switch(GetState())
        {
            case EBoolState.FalseThisFrame:
                value = boolToChar(false);
                break;

            case EBoolState.TrueThisFrame:
                value = boolToChar(true);
                break;
        }
    }

    public EBoolState GetState()
    {
        return (EBoolState)value;
    }

    public char GetCharState()
    {
        return value;
    }

    public bool GetBool() //Doesnt account for This frame or not!
    {
        switch (GetState())
        {
            case EBoolState.False:
                return false;

            case EBoolState.True:
                return true;

            case EBoolState.FalseThisFrame:
                return false;

            case EBoolState.TrueThisFrame:
                return true;
        }

        return false;
    }
}

public struct PlayerUpdateData : INetworkSerializable
{
    public Vector3 position;
    public Vector3 velocity;

    public float stamina;
    public char isAlive;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        if (serializer.IsReader)
        {
            serializer.SerializeValue(ref position);
            serializer.SerializeValue(ref velocity);
            serializer.SerializeValue(ref stamina);
            serializer.SerializeValue(ref isAlive);
        }
        else
        {
            serializer.SerializeValue(ref position);
            serializer.SerializeValue(ref velocity);
            serializer.SerializeValue(ref stamina);
            serializer.SerializeValue(ref isAlive);
        }
    }
}

public class Player : NetworkBehaviour
{
    public enum PlayerMovement
    {
        None,
        Forward = 1 << 1,
        Backward = 1 << 2,
        Right = 1 << 3,
        Left = 1 << 4,
        Up = 1 << 5,
        Down = 1 << 6,
        FastForward = 1 << 7, //running

        All = Forward | Backward | Right | Left | Up | Down | FastForward,
    }

    public enum VelocityUpdate
    {
        None,
        Forward = 1 << 1,
        Right = 1 << 2,
        Up = 1 << 3,
        ForwardRight = Forward | Right,
    }

    public enum GameState
    {
        GameStarted,
        GameEnded
    }

    public GameObject serverManager = null;

    [SerializeField]
    protected GameObject head = null;

    [SerializeField]
    protected TMPro.TextMeshPro playerNameLabel = null;

    protected Rigidbody playerRigidBody = null;
    protected CapsuleCollider playerCapsuleCollider = null;

    private bool isGrounded = false;
    private bool isForcedSliding = false;

    protected float forceSlideTime = 2.0f;
    protected float forceSlideTimeCurrent = 0.0f;

    public float playerDrag = 3.0f;
    public float terminalVelocity = 30.0f;

    protected bool canMove = true;
    protected bool canJump = true;
    protected bool canRotate = true;

    public bool isConsumingStamina = true;
    public bool isLocalPlayer = false;

    public PBool isAlive = new PBool(true);

    protected bool isMovementHooked = false;

    public string playerName = "";
    public ulong id = 0; // Connection id this Player belongs to

    public float gravity = 9.8f;

    public float runningSpeed = 14.0f;
    public float movementSpeed = 6.0f; // can only be changed by Server
    public float jumpHeight = 4.0f; // can only be changed by Server

    public float maxStamina = 100.0f;

    public float regenStaminaAmount = 10.0f;
    public float degenStaminaAmount = 20.0f;

    protected float timeSinceWalking = 0.0f;
    [SerializeField]
    protected float currentStamina = 0.0f;

    public delegate void OnUpdatePlayer(Player _this);
    public delegate void OnDestroyPlayer(Player _this, bool ByScene);
    public delegate PlayerMovement OnMovePlayerHook(Player _this, PlayerMovement originalMovement);
    public delegate void OnChangedLivingState(Player _this, bool alive);

    protected OnUpdatePlayer onUpdatePlayer = null;
    protected OnDestroyPlayer onDestroyPlayer = null;
    protected OnMovePlayerHook onMovePlayerHook = null;
    protected OnChangedLivingState onChangedLivingState = null;

    public void BindOnUpdate(OnUpdatePlayer onUpdateValue) {this.onUpdatePlayer = onUpdateValue;}
    public void BindOnDestroy(OnDestroyPlayer onDestroyValue) {this.onDestroyPlayer = onDestroyValue;}
    public void BindOnChangeLivingState(OnChangedLivingState onChangedLivingState) {this.onChangedLivingState = onChangedLivingState;}
    public void HookOnMovePlayer(OnMovePlayerHook onMovePlayerHook) { this.isMovementHooked = true; this.onMovePlayerHook = onMovePlayerHook;}

    public void UnbindOnDestroy() { this.onDestroyPlayer = null; }
    public void UnbindOnUpdate() { this.onUpdatePlayer = null; }
    public void UnbindOnChangeLivingState() { this.onChangedLivingState = null; }

    public void UnhookOnMovePlayer() { this.isMovementHooked = false; this.onMovePlayerHook = null; }

    private void Start()
    {
        playerRigidBody = GetComponent<Rigidbody>();
        playerCapsuleCollider = GetComponent<CapsuleCollider>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        serverManager = GameObject.FindWithTag("ServerManager");

        Initialize();
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if(onDestroyPlayer != null)
            onDestroyPlayer(this, false);
    }

    private void Update()
    {
        isGrounded = Physics.BoxCast(
        transform.position,
        new Vector3(playerCapsuleCollider.radius, 0.05f, playerCapsuleCollider.radius),
        -transform.up,
        Quaternion.identity,
        (playerCapsuleCollider.height / 2.0f) + 0.05f,
        ~0,
        QueryTriggerInteraction.Ignore);


        if (IsHost)
        {
            if (timeSinceWalking >= 2.0f)
            {
                if (currentStamina < maxStamina)
                {
                    currentStamina += (regenStaminaAmount * Time.deltaTime);

                    if (currentStamina > maxStamina)
                        currentStamina = maxStamina;
                }
            }
            else
                timeSinceWalking += Time.deltaTime;

            if (isForcedSliding)
            {

                if (forceSlideTimeCurrent >= forceSlideTime)
                {
                    forceSlideTimeCurrent = 0.0f;
                    isForcedSliding = false;

                    canMove = true;
                }
                else
                    forceSlideTimeCurrent += Time.deltaTime;
            }

            this.UpdatePlayerGravity_ServerRpc();

            this.OnNetworkRequestUpdateData_ServerRpc();

            this.isAlive.DeframeBool();

            DeframeBools_ServerRpc();
        }
    }

    [ServerRpc]
    protected virtual void DeframeBools_ServerRpc()
    {

    }


    [ServerRpc]
    private void UpdatePlayerGravity_ServerRpc()
    {
        if (gravity > 0.0f && !isGrounded)
        {
            var velocityCopy = this.playerRigidBody.linearVelocity;

            if (velocityCopy.y > -terminalVelocity)
            {
                velocityCopy.y -= (gravity * Time.deltaTime);

                if (velocityCopy.y < -terminalVelocity)
                    velocityCopy.y = -terminalVelocity;

                this.playerRigidBody.linearVelocity = velocityCopy;
            }
        }

        if (playerDrag > 0.0f && CanPerformDrag())
        {
            bool changedVelocity = false;

            Vector3 localVelocity = transform.InverseTransformDirection(this.playerRigidBody.linearVelocity);

            if (localVelocity.x > 0.0f)
            {
                localVelocity.x -= playerDrag * Time.deltaTime;
                if (localVelocity.x < 0.0f) localVelocity.x = 0.0f;
                changedVelocity = true;
            }
            else if (localVelocity.x < 0.0f)
            {
                localVelocity.x += playerDrag * Time.deltaTime;
                if (localVelocity.x > 0.0f) localVelocity.x = 0.0f;
                changedVelocity = true;
            }

            if (localVelocity.z > 0.0f)
            {
                localVelocity.z -= playerDrag * Time.deltaTime;
                if (localVelocity.z < 0.0f) localVelocity.z = 0.0f;
                changedVelocity = true;
            }
            else if (localVelocity.z < 0.0f)
            {
                localVelocity.z += playerDrag * Time.deltaTime;
                if (localVelocity.z > 0.0f) localVelocity.z = 0.0f;
                changedVelocity = true;
            }

            if (changedVelocity)
            {
                Vector3 transformedVelocity = transform.TransformDirection(localVelocity);

                Vector3 newVelocityCal = new Vector3(transformedVelocity.x, this.playerRigidBody.linearVelocity.y, transformedVelocity.z);

                this.playerRigidBody.linearVelocity = newVelocityCal;
            }
        }
    }

    [ServerRpc]
    public void OnNetworkUpdatePosition_ServerRpc(Vector3 position, Vector3 velocity, VelocityUpdate update = VelocityUpdate.None)
    {
        this.transform.position = position;

        if(this.playerRigidBody)
        {
            this.playerRigidBody.position = position;

            if(update != VelocityUpdate.None)
            {
                var velocityCopy = this.playerRigidBody.linearVelocity;

                Vector3 targetVelocity = new Vector3();

                if ((update & VelocityUpdate.Forward) == VelocityUpdate.Forward)
                    targetVelocity.x = velocity.x;
                else
                    targetVelocity.x = velocityCopy.x;

                if ((update & VelocityUpdate.Up) == VelocityUpdate.Up)
                    targetVelocity.y = velocity.y;
                else
                    targetVelocity.y = velocityCopy.y;

                if ((update & VelocityUpdate.Right) == VelocityUpdate.Right)
                    targetVelocity.z = velocity.z;
                else
                    targetVelocity.z = velocityCopy.z;

                this.playerRigidBody.linearVelocity = targetVelocity;
            }
        }
    }

    [ServerRpc]
    protected virtual void OnNetworkRequestUpdateData_ServerRpc()
    {
        OnNetworkUpdateData_ClientRpc(CraftPlayerUpdateData());
    }

    [ClientRpc]
    private void OnNetworkUpdateData_ClientRpc(PlayerUpdateData data)
    {
        if(this.playerRigidBody)
        {
            this.playerRigidBody.position = data.position;
            this.playerRigidBody.linearVelocity = data.velocity;
        }

        this.currentStamina = data.stamina;
        
        PBool livingState = new PBool(data.isAlive);

        switch(livingState.GetState())
        {
            case PBool.EBoolState.FalseThisFrame:
                if(onChangedLivingState != null)
                    onChangedLivingState(this, false);

                isAlive = new PBool(PBool.EBoolState.False);
            break;

            case PBool.EBoolState.TrueThisFrame:
                if (onChangedLivingState != null)
                    onChangedLivingState(this, true);

                isAlive = new PBool(PBool.EBoolState.True);
            break;
        }

        if (onUpdatePlayer != null)
            onUpdatePlayer(this);
    }

    [ClientRpc]
    public void OnNetworkUpdateRotationClientRpc(Vector3 rotation)
    {
        this.transform.Rotate(rotation);
    }

    [ServerRpc]
    public void KillPlayer_ServerRpc()
    {
        if (this.isAlive.GetBool())
        {
            ServerManager sv = serverManager.GetComponent<ServerManager>();

            this.isAlive = new PBool(PBool.EBoolState.FalseThisFrame);

            SetPositionServerRpc(sv.GetRandomSpawnLocation(true).position);
        }
    }

    [ServerRpc]
    public void RevivePlayer_ServerRpc()
    {
        if(!this.isAlive.GetBool())
        {
            ServerManager sv = serverManager.GetComponent<ServerManager>();

            this.isAlive = new PBool(PBool.EBoolState.TrueThisFrame);

            //SetPositionServerRpc(sv.GetRandomSpawnLocation(true).position);
        }
    }

    [ClientRpc]
    public void SetCanMoveClientRpc(bool canMove)
    {
        this.canMove = canMove;

        if(canMove)
            this.playerRigidBody.isKinematic = false;
        else
            this.playerRigidBody.isKinematic = true;
    }

    [ClientRpc]
    public void SetForceSlidedClientRpc(float seconds)
    {
        this.forceSlideTime = seconds;
        this.forceSlideTimeCurrent = 0.0f;

        this.isForcedSliding = true;

        this.canMove = false;
    }

    [ServerRpc]
    public void SetCanMoveServerRpc(bool canMove)
    {
        this.canMove = canMove;

        if(canMove)
            this.playerRigidBody.isKinematic = false;
        else
            this.playerRigidBody.isKinematic = true;
    }

    [ServerRpc]
    public void SetPositionServerRpc(Vector3 position)
    {
        transform.position = position;

        if (!this.playerRigidBody.isKinematic)
            this.playerRigidBody.position = position;

        OnNetworkUpdatePosition_ServerRpc(position, Vector3.zero);
    }

    [ServerRpc]
    public void OnRotateCharacterServerRpc(Vector3 addValue)
    {
        OnNetworkUpdateRotationClientRpc(addValue);
    }

    [ServerRpc]
    public void OnMoveCharacterServerRpc(PlayerMovement movingDir, ServerRpcParams param = default)
    {
        if (movingDir == PlayerMovement.None)
            return;

        VelocityUpdate velocityState = VelocityUpdate.ForwardRight;

        Vector3 currentPosition = transform.position;

        Vector3 nextPosition = currentPosition;

        float speedBoost = 0.0f;

        if ((movingDir & PlayerMovement.FastForward) == PlayerMovement.FastForward)
        {
            if(isConsumingStamina)
            {
                timeSinceWalking = 0.0f;

                if (currentStamina > 0.0f)
                {
                    currentStamina -= (degenStaminaAmount * Time.deltaTime);

                    if (currentStamina < 0.0f)
                        currentStamina = 0.0f;

                    speedBoost = runningSpeed - movementSpeed;
                }
            }
            else
                speedBoost = runningSpeed - movementSpeed;
        }

        if ((movingDir & PlayerMovement.Forward) == PlayerMovement.Forward)
        {
            Vector3 fw = transform.forward;

            nextPosition.x += (fw.x * (movementSpeed + speedBoost));
            nextPosition.z += (fw.z * (movementSpeed + speedBoost));
        }

        if ((movingDir & PlayerMovement.Backward) == PlayerMovement.Backward)
        {
            

            Vector3 fw = transform.forward;

            nextPosition.x -= (fw.x * movementSpeed);
            nextPosition.z -= (fw.z * movementSpeed);
        }

        if ((movingDir & PlayerMovement.Right) == PlayerMovement.Right)
        {
            

            Vector3 r = transform.right;

            nextPosition.x += (r.x * movementSpeed);
            nextPosition.z += (r.z * movementSpeed);
        }

        if ((movingDir & PlayerMovement.Left) == PlayerMovement.Left)
        {
            

            Vector3 r = transform.right;

            nextPosition.x -= (r.x * movementSpeed);
            nextPosition.z -= (r.z * movementSpeed);
        }

        if ((movingDir & PlayerMovement.Up) == PlayerMovement.Up)
        {
            if (isGrounded && canJump)
            {
                Vector3 up = transform.up;

                nextPosition.y += (up.y * jumpHeight);

                velocityState |= VelocityUpdate.Up;
            }
        }

        if ((movingDir & PlayerMovement.Down) == PlayerMovement.Down)
        {
            if (!isGrounded)
            {
                Vector3 up = transform.up;

                nextPosition.y -= (up.y * jumpHeight);

                velocityState |= VelocityUpdate.Up;
            }
        }

        this.OnNetworkUpdatePosition_ServerRpc(currentPosition, (nextPosition - currentPosition), velocityState);
    }

    [Rpc(SendTo.Server)]
    public void OnClientMoveRpc(Player.PlayerMovement movement, RpcParams rpcParams = default)
    {
        if (CheckAuthority(rpcParams) == false || !canMove)
            return; //Unauthorized Call!

        if (isMovementHooked)
            OnMoveCharacterServerRpc(onMovePlayerHook(this, movement));
        else
            OnMoveCharacterServerRpc(movement);
    }

    [Rpc(SendTo.Server)]
    public void OnClientRotateRpc(Vector3 addValue, RpcParams rpcParams = default)
    {
        if (CheckAuthority(rpcParams) == false || !canMove)
            return; //Unauthorized Call!

        if(canRotate)
            OnRotateCharacterServerRpc(addValue);
    }

    public void SetPlayerNameLabel()
    {
        if(playerNameLabel != null && !string.IsNullOrEmpty(playerName))
        {
            playerNameLabel.text = playerName;
        }
    }

    public void UpdatePlayerNameLabel(Camera camera)
    {
        if (camera && playerNameLabel != null)
        {
            var lableTransform = playerNameLabel.transform;
            var cameraTransform = camera.transform;

            var lookDirection = lableTransform.position - cameraTransform.position;

            lookDirection.y = 0.0f;

            Quaternion rotation = lableTransform.rotation;

            rotation.SetLookRotation(lookDirection);

            lableTransform.rotation = rotation;
        }
    }

    protected PlayerUpdateData CraftPlayerUpdateData()
    {
        PlayerUpdateData data = new PlayerUpdateData();

        data.isAlive = (isAlive.GetCharState());

        data.stamina = currentStamina;

        if (this.playerRigidBody)
        {
            data.velocity = this.playerRigidBody.linearVelocity;
            data.position = this.playerRigidBody.position;
        }

        return data;
    }

    protected bool CheckAuthority(RpcParams rpcParams)
    {
        var SenderID = rpcParams.Receive.SenderClientId;

        if (SenderID != id)
        {
            DebugClass.Log($"Player with ID: {SenderID}, tried Executing unauthorized Code");
            return false;
        }

        return true;
    }

    protected bool CanPerformDrag()
    {
        return !isForcedSliding;
    }

    private void OnDestroy()
    {
        if (serverManager)
        {
            var svManager = serverManager.GetComponent<ServerManager>();

            svManager.RemovePlayer(this);
        }
    }

    private void FixedUpdate()
    {

    }

    private void Initialize()
    {
        if(serverManager)
        {
            var svManager = serverManager.GetComponent<ServerManager>();

            svManager.AddPlayer(this);
        }
    }

    public string GetPlayerName()
    {
        return playerName;
    }

    public bool IsMovementHooked()
    {
        return isMovementHooked;
    }

    public bool IsGrounded()
    {
        return isGrounded;
    }

    public float GetStamina()
    {
        return currentStamina;
    }

    public float GetMaxStamina()
    {
        return maxStamina;
    }

    public void SetStamina(float stamina)
    {
        currentStamina = stamina;
    }
}
