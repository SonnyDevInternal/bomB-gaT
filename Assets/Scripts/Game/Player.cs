using Unity.Netcode;
using UnityEngine;


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
    }

    public GameObject serverManager = null;

    [SerializeField]
    private GameObject head = null;

    [SerializeField]
    public GameObject bombHand = null;

    [SerializeField]
    private TMPro.TextMeshPro playerNameLabel = null;

    private Rigidbody playerRigidBody = null;
    private CapsuleCollider playerCapsuleCollider = null;

    private bool isGrounded = false;
    private bool isForcedSliding = false;

    private float forceSlideTime = 2.0f;
    private float forceSlideTimeCurrent = 0.0f;

    private float playerDrag = 0.0f;

    private bool canMove = true;
    private bool canJump = true;

    public bool isLocalPlayer = false;

    public string playerName = "";
    public ulong id = 0; // Connection id this Player belongs to

    [SerializeField]
    private float gravity = 2.0f;

    public float movementSpeed = 6.0f; // can only be changed by Server
    public float jumpHeight = 4.0f; // can only be changed by Server

    public delegate void OnUpdatePlayer(Player _this);
    public delegate void OnDestroyPlayer(Player _this, bool ByScene);

    private OnUpdatePlayer onUpdatePlayer;
    private OnDestroyPlayer onDestroyPlayer;


    private void Start()
    {
        playerRigidBody = GetComponent<Rigidbody>();
        playerCapsuleCollider = GetComponent<CapsuleCollider>();
    }

    private void Update()
    {
        isGrounded = Physics.Raycast(transform.position, -transform.up, (playerCapsuleCollider.height / 2.0f) + 0.05f);

        if(IsHost)
        {
            if(isForcedSliding)
            {
                if (forceSlideTimeCurrent >= forceSlideTime)
                {
                    forceSlideTimeCurrent = 0.0f;
                    isForcedSliding = false;

                    canMove = true;

                    this.playerRigidBody.linearDamping = this.playerDrag;
                }
                else
                    forceSlideTimeCurrent += Time.deltaTime;
            }
        }
    }

    public void BindOnUpdate(OnUpdatePlayer onUpdateValue) {this.onUpdatePlayer = onUpdateValue;}
    public void BindOnDestroy(OnDestroyPlayer onDestroyValue) {this.onDestroyPlayer = onDestroyValue;}
    public void UnbindOnDestroy() { this.onDestroyPlayer = null; }
    public void UnbindOnUpdate() { this.onUpdatePlayer = null; }

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

    [ClientRpc]
    public void OnNetworkUpdatePositionClientRpc(Vector3 position, Vector3 velocity, bool updateVelocity = false)
    {
        if(this.playerRigidBody)
        {
            this.playerRigidBody.position = position;

            if(updateVelocity)
                this.playerRigidBody.linearVelocity = new Vector3(velocity.x, velocity.y, velocity.z);

            if (onUpdatePlayer != null)
                onUpdatePlayer(this);
        }
    }

    [ClientRpc]
    public void OnNetworkUpdateRotationClientRpc(Vector3 rotation)
    {
        this.transform.Rotate(rotation);
    }

    [ClientRpc]
    public void KillPlayerClientRpc()
    {
        if(this.onDestroyPlayer != null)
        {
            this.onDestroyPlayer(this, false);

            this.onDestroyPlayer = null;
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

        this.playerDrag = this.playerRigidBody.linearDamping;

        this.playerRigidBody.linearDamping = 0.01f;
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
        if(this.playerRigidBody.isKinematic)
            transform.position = position;
        else
            this.playerRigidBody.position = position;

        OnNetworkUpdatePositionClientRpc(position, Vector3.zero);
    }

    [ServerRpc]
    public void OnRotateCharacterServerRpc(Vector3 addValue)
    {
        OnNetworkUpdateRotationClientRpc(addValue);
    }

    [ServerRpc]
    public void OnMoveCharacterServerRpc(PlayerMovement movingDir, ServerRpcParams param = default)
    {
        if(IsHost)
        {
            float deltaTime = Time.deltaTime;

            Vector3 currentPosition = transform.position;

            Vector3 nextPosition = currentPosition;

            nextPosition.y -= gravity;

            if ((movingDir & PlayerMovement.Forward) == PlayerMovement.Forward)
            {
                Vector3 fw = transform.forward;

                nextPosition.x += (fw.x * movementSpeed);
                nextPosition.z += (fw.z * movementSpeed);
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
                if (isGrounded)
                {
                    Vector3 up = transform.up;

                    nextPosition.y += (up.y * jumpHeight);
                }
            }

            if ((movingDir & PlayerMovement.Down) == PlayerMovement.Down)
            {
                if (!isGrounded)
                {
                    Vector3 up = transform.up;

                    nextPosition.y -= (up.y * jumpHeight);
                }
            }

            bool isMoving = !(movingDir == PlayerMovement.None);


            this.OnNetworkUpdatePositionClientRpc(this.playerRigidBody.position, (nextPosition - currentPosition), true);
        }
    }

    [Rpc(SendTo.Server)]
    public void OnClientMoveRpc(Player.PlayerMovement movement, RpcParams rpcParams = default)
    {
        if (CheckAuthority(rpcParams) == false || !canMove)
            return; //Unauthorized Call!

        OnMoveCharacterServerRpc(movement);
    }

    [Rpc(SendTo.Server)]
    public void OnClientRotateRpc(Vector3 addValue, RpcParams rpcParams = default)
    {
        if (CheckAuthority(rpcParams) == false || !canMove)
            return; //Unauthorized Call!

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

    private bool CheckAuthority(RpcParams rpcParams)
    {
        var SenderID = rpcParams.Receive.SenderClientId;

        if (SenderID != id)
        {
            Debug.Log($"Player with ID: {SenderID}, tried Executing unauthorized Code");
            return false;
        }

        return true;
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
        if(IsHost)
        {
            this.OnNetworkUpdatePositionClientRpc(playerRigidBody.position, Vector3.zero);
        }
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

}
