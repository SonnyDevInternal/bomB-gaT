using Unity.Netcode;
using UnityEngine;

public class LocalPlayer : MonoBehaviour
{
    private float CameraRotSpeedX = 100.0f;
    private float CameraRotSpeedY = 100.0f;

    private Player owningPlayer = null;
    private Camera playerCamera = null;

    private bool hasPlayer = false;

    private void Start()
    {
        this.playerCamera = GetComponentInChildren<Camera>();
    }

    private void OnDestroy()
    {
        if (this.owningPlayer)
        {
            this.owningPlayer.UnbindOnDestroy();
            this.owningPlayer.UnbindOnUpdate();
        }
    }

    private void Update()
    {
        if (!this.hasPlayer)
            return;

        float translation = Input.GetAxis("Mouse X") * this.CameraRotSpeedX * Time.deltaTime;
        float rotation = -Input.GetAxis("Mouse Y") * this.CameraRotSpeedY * Time.deltaTime;



        Player.PlayerMovement movement = Player.PlayerMovement.None;

        if (Input.GetKey(KeyCode.W))
            movement |= Player.PlayerMovement.Forward;

        if (Input.GetKey(KeyCode.S))
            movement |= Player.PlayerMovement.Backward;

        if (Input.GetKey(KeyCode.A))
            movement |= Player.PlayerMovement.Left;

        if (Input.GetKey(KeyCode.D))
            movement |= Player.PlayerMovement.Right;

        if (Input.GetKey(KeyCode.Space))
            movement |= Player.PlayerMovement.Up;

        if(rotation != 0.0f)
        {
            this.playerCamera.transform.Rotate(rotation, 0.0f, 0.0f);
        }

        if(translation != 0.0f)
        {
            Vector3 addEuler = new Vector3(0.0f, translation, 0.0f);

            this.owningPlayer.OnClientRotateRpc(addEuler);
        }

        this.owningPlayer.OnClientMoveRpc(movement);
    }

    private void SpectatePlayer()
    {

    }

    private void OnPlayerDestroyed(Player player, bool ByScene)
    {
        this.owningPlayer = null;
        this.hasPlayer = false;

        Cursor.lockState = CursorLockMode.None;

        if (!ByScene)
        {

        }
    }

    private void OnPlayerUpdated(Player player)
    {
        //this.playerCamera.transform.rotation = player.transform.rotation;
    }


    public void BindNetworkPlayer(Player player)
    {
        this.owningPlayer = player;
        this.hasPlayer = true;

        player.isLocalPlayer = true;

        player.BindOnDestroy(OnPlayerDestroyed);
        player.BindOnUpdate(OnPlayerUpdated);

        Cursor.lockState = CursorLockMode.Locked;
    }

    public void OnInitializedLocalPlayer()
    {
        if (this.owningPlayer.serverManager)
        {
            var serverManager = this.owningPlayer.serverManager.GetComponent<ServerManager>();

            serverManager.GetServerPlayerDataRpc();
        }
    }
}
