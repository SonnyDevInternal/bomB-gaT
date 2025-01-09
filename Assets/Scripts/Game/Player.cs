using Mono.Cecil;
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

    private GameObject serverManager = null;

    [SerializeField]
    private GameObject head = null;

    private Rigidbody playerRigidBody = null;
    private bool isGrounded = false;

    public string playerName = "";
    public ulong id = 0; // Connection id this Player belongs to

    private float movementDrag = 6.0f;

    public float movementSpeed = 6.0f; // can only be changed by Server
    public float jumpHeight = 4.0f; // can only be changed by Server

    public delegate void OnUpdatePlayer(Player _this);
    public delegate void OnDestroyPlayer(Player _this, bool ByScene);

    private OnUpdatePlayer onUpdatePlayer;
    private OnDestroyPlayer onDestroyPlayer;

    private void Start()
    {
        playerRigidBody = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        
    }

    public void BindOnUpdate(OnUpdatePlayer onUpdateValue) {this.onUpdatePlayer = onUpdateValue;}
    public void BindOnDestroy(OnDestroyPlayer onDestroyValue) {this.onDestroyPlayer = onDestroyValue;}
    public void UnbindOnDestroy() { this.onDestroyPlayer = null; }
    public void UnbindOnUpdate() { this.onUpdatePlayer = null; }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        Initialize();
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if(onDestroyPlayer != null)
            onDestroyPlayer(this, false);
    }

    [ClientRpc]
    public void OnNetworkUpdatePositionClientRpc(Vector3 position, Vector3 velocity)
    {
        if(this.playerRigidBody)
        {
            this.playerRigidBody.position = position;

            if (velocity.x != 0.0f || velocity.y != 0.0f || velocity.z != 0.0f)
            {
                this.playerRigidBody.linearVelocity = new Vector3(velocity.x, velocity.y, velocity.z);
            }
                

            if (onUpdatePlayer != null)
                onUpdatePlayer(this);
        }
    }

    [ClientRpc]
    public void OnNetworkUpdateRotationClientRpc(Vector3 rotation)
    {
        this.transform.Rotate(rotation);
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

            Vector3 velocity = (isMoving ? (currentPosition - nextPosition) : Vector3.zero);

            if(isMoving)
                Debug.Log($"Velocity: x{velocity.x}, y{velocity.y}, z{velocity.z}");

            this.OnNetworkUpdatePositionClientRpc(this.playerRigidBody.position, velocity);
        }
    }

    [Rpc(SendTo.Server)]
    public void OnClientMoveRpc(Player.PlayerMovement movement)
    {
        OnMoveCharacterServerRpc(movement);
    }

    [Rpc(SendTo.Server)]
    public void OnClientRotateRpc(Vector3 addValue)
    {
        OnRotateCharacterServerRpc(addValue);
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

    private void OnCollisionEnter(Collision collision)
    {
        isGrounded = true;
    }

    private void OnCollisionExit(Collision collision)
    {
        isGrounded = false;
    }
}
