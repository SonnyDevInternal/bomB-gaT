using Unity.Netcode;
using UnityEngine;
using static Player;

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

        var camTransform = this.playerCamera.transform;
        Vector3 currentRotation = camTransform.localEulerAngles;
        currentRotation.x += rotation;
        currentRotation.x = Mathf.Clamp(currentRotation.x, -90f, 90f);

        camTransform.localRotation = Quaternion.Euler(currentRotation.x, 0, 0);

        var playerRotation = this.transform.rotation.eulerAngles;
        playerRotation.y += translation;
        this.transform.rotation = Quaternion.Euler(0, playerRotation.y, 0);

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

        this.owningPlayer.OnClientMoveRpc(movement, this.transform.rotation.eulerAngles);
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

        player.BindOnDestroy(OnPlayerDestroyed);
        player.BindOnUpdate(OnPlayerUpdated);

        Cursor.lockState = CursorLockMode.Locked;
    }
}
