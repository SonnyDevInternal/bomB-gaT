using Unity.Netcode;
using UnityEngine;

public struct ServerObjectData : INetworkSerializable
{
    public Vector3 Position;
    public Vector3 Rotation;
    public Vector3 Velocity;
    public Vector3 Scale;


    ServerObjectData(Vector3 position, Vector3 rotation, Vector3 velocity, Vector3 scale)
    {
        this.Position = position;
        this.Rotation = rotation;
        this.Velocity = velocity;
        this.Scale = scale;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        if (serializer.IsReader)
        {
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Rotation);
            serializer.SerializeValue(ref Scale);
        }
        else
        {
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Rotation);
            serializer.SerializeValue(ref Scale);
        }
    }
}

public class ServerObject : NetworkBehaviour
{
    [SerializeField]
    protected Rigidbody owningRigidBody = null;
    protected Transform owningTransform = null;

    private bool isUsingRigidBody = false;
    private bool useClient = false; // set true to update the position through other stuff

    void Start()
    {
        owningTransform = transform;
        
        if(!owningRigidBody)
            owningRigidBody = GetComponent<Rigidbody>();

        isUsingRigidBody = (owningRigidBody != null);

        OnStart();
    }

    void Update()
    {
        OnUpdate();

        if (IsHost && !this.useClient)
        {
            ServerUpdateObjectServerRpc();
        }
    }

    protected virtual void OnStart()
    {

    }

    protected virtual void OnUpdate()
    {

    }

    [ClientRpc]
    public void SetUseRigidBodyClientRpc(bool userRigidBody)
    {
        this.isUsingRigidBody = userRigidBody;

        if (userRigidBody)
            this.owningRigidBody.isKinematic = false;
        else
            this.owningRigidBody.isKinematic = true;
    }

    [ServerRpc]
    public void SetUseRigidBody_ServerRpc(bool userRigidBody)
    {
        this.isUsingRigidBody = userRigidBody;

        SetUseRigidBodyClientRpc(userRigidBody);
    }

    [ServerRpc]
    public void ServerUpdateObjectServerRpc()
    {
        ServerObjectData obj;

        if (!isUsingRigidBody)
        {
            obj.Rotation = transform.rotation.eulerAngles;
            obj.Position = transform.position;
            obj.Velocity = Vector3.zero;
            obj.Scale = transform.localScale;
        }
        else
        {
            obj.Rotation = transform.rotation.eulerAngles;
            obj.Position = owningRigidBody.position;
            obj.Velocity = owningRigidBody.linearVelocity;
            obj.Scale = transform.localScale;
        }

        ServerUpdateObjectClientRpc(obj);
    }

    [ServerRpc]
    public void ObjectUseNonServerPos_ServerRpc(bool value)
    {
        this.useClient = value;
    }

    [ClientRpc]
    public void ServerUpdateObjectClientRpc(ServerObjectData obj)
    {
        transform.rotation = Quaternion.Euler(obj.Rotation);
        transform.localScale = obj.Scale;

        if (isUsingRigidBody)
        {
            owningRigidBody.position = obj.Position;
            owningRigidBody.linearVelocity = obj.Velocity;
        }
        else
        {
            transform.position = obj.Position;
        }
    }

    public bool isKinematic()
    {
        if (owningRigidBody)
            return owningRigidBody.isKinematic;

        return true;
    }
}
