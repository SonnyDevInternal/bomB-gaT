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
    public void OnNetworkUpdatePositionClientRpc(Vector3 position, Vector3 velocity, Quaternion rotation)
    {
        if(this.playerRigidBody)
        {

            var euelers = rotation.eulerAngles;
            var otherEueler = this.transform.rotation.eulerAngles;

            euelers.x = otherEueler.x;
            euelers.z = otherEueler.z;

            this.transform.rotation = Quaternion.Euler(euelers);
            this.playerRigidBody.position = position;

            if (velocity.x != 0.0f || velocity.y != 0.0f || velocity.z != 0.0f)
            {
                var normalizedRotation = this.transform.forward.normalized;

                this.playerRigidBody.linearVelocity = new Vector3(velocity.x, velocity.y * normalizedRotation.y, velocity.z * normalizedRotation.z);
            }
                

            if (onUpdatePlayer != null)
                onUpdatePlayer(this);
        }
    }

    [ServerRpc]
    public void OnMoveCharacterServerRpc(PlayerMovement movingDir, Quaternion rotation, ServerRpcParams param = default)
    {
        if(IsHost)
        {
            float forward = 0.0f;
            float right = 0.0f;
            float up = 0.0f;

            if ((movingDir & PlayerMovement.Forward) == PlayerMovement.Forward)
            {
                forward = 1.0f;
            }

            if ((movingDir & PlayerMovement.Backward) == PlayerMovement.Backward)
            {
                forward -= 1.0f;
            }

            if ((movingDir & PlayerMovement.Right) == PlayerMovement.Right)
            {
                right = 1.0f;
            }

            if ((movingDir & PlayerMovement.Left) == PlayerMovement.Left)
            {
                right -= 1.0f;
            }

            if ((movingDir & PlayerMovement.Up) == PlayerMovement.Up)
            {
                if (!isGrounded)
                    up = 1.0f;
            }

            if ((movingDir & PlayerMovement.Down) == PlayerMovement.Down)
            {
                if (!isGrounded)
                    up -= 1.0f;
            }

            Vector3 velocity = ((Vector3.forward * forward) * this.movementSpeed) + ((Vector3.right * right) * this.movementSpeed) +
                ((Vector3.up * up) * this.movementSpeed);

            this.OnNetworkUpdatePositionClientRpc(this.playerRigidBody.position, velocity, Quaternion.Euler(0, rotation.eulerAngles.y, 0));
        }
    }

    [Rpc(SendTo.Server)]
    public void OnClientMoveRpc(Player.PlayerMovement movement, Vector3 eulerAngles)
    {
        Debug.Log("Rotation: " + eulerAngles.y);

        OnMoveCharacterServerRpc(movement, Quaternion.Euler(new Vector3(eulerAngles.x, eulerAngles.y, 0.0f)));
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
            this.OnNetworkUpdatePositionClientRpc(playerRigidBody.position, Vector3.zero, transform.rotation);
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
