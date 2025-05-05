using Mirror; 
using UnityEngine;

public class GrabNetController : NetworkBehaviour
{
    // Cached component references.
    [Tooltip("Optional")]
    [SerializeField] private Rigidbody _rb;

    // Tracks whether the object is currently grabbed.
    [SyncVar] public bool isGrabbed = false;
    // Stores the network identity of the client that grabbed the object.
    [SyncVar] private NetworkIdentity grabber = null;

    private float lerpTimer = 0f;
    
    private Vector3 startPos;
    private Quaternion startRot;
    
    private Vector3 targetPos;
    private Quaternion targetRot;
    
    [SerializeField] private bool isVehicle = false;
    [SerializeField] private float syncInt = 0.1f;
    
    #region Initialization

    private void Awake()
    {
        if (_rb == null)
        {
            _rb = GetComponent<Rigidbody>();
        }
    }
    
    #endregion

    #region Public Methods (Called by Grab Net Transform Bridge)

    public void ClientRequestGrab()
    {
        CmdTryGrab(NetworkClient.connection.identity);
    }

    public void ClientRequestRelease()
    {
        CmdTryRelease(NetworkClient.connection.identity);
    }

    public void ClientUpdateTransform(Vector3 pos, Quaternion rot)
    {
        if (!IAmGrabber)
            return;
        
        transform.position = pos;
        transform.rotation = SafeNorm(rot);
    }

    public void ClientSyncTransform(Vector3 pos, Quaternion rot)
    {
        CmdSyncTransform(pos, rot);
    }
    
    private void Update()
    {
        if (!isGrabbed || IAmGrabber)
            return;

        lerpTimer += Time.deltaTime;
        float t = Mathf.Clamp01(lerpTimer / syncInt);

        transform.position = Vector3.Lerp(startPos, targetPos, t);
        transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
    }

    #endregion

    #region Network Commands & RPCs

    [Command(requiresAuthority = false)]
    private void CmdTryGrab(NetworkIdentity grabberId)
    {
        if (!isGrabbed)
        {
            isGrabbed = true;
            grabber = grabberId;
            Debug.Log($"[SERVER-AUTH] {gameObject.name} grabbed by {grabberId}");
            RpcOnGrabbed(grabberId);
            if (!isVehicle)
            {
                SetKinematicState(true);
            }
        }
        else
        {
            isGrabbed = true;
            grabber = grabberId;
            Debug.Log($"[SERVER-AUTH] {gameObject.name} is now grabbed by {grabberId}");
            RpcOnGrabbed(grabberId);
        }
    }

    [Command(requiresAuthority = false)]
    private void CmdTryRelease(NetworkIdentity grabberId)
    {
        if (isGrabbed && grabber == grabberId)
        {
            isGrabbed = false;
            grabber = null;
            Debug.Log($"[SERVER-AUTH] {gameObject.name} released by {grabberId}");
            RpcOnReleased();
            if (!isVehicle)
            {
                SetKinematicState(false);
            }
        }
        else
        {
            Debug.LogWarning($"[SERVER-AUTH] {gameObject.name} release attempt by {grabberId} rejected.");
        }
    }

    // Command that receives transform updates from the client.
    [Command(requiresAuthority = false)]
    private void CmdSyncTransform(Vector3 pos, Quaternion rot, NetworkConnectionToClient sender = null)
    {
        // Only update if the sender is the client that grabbed the object.
        if (isGrabbed && sender != null && sender.identity == grabber)
        {
            RpcSyncTransform(pos, SafeNorm(rot));
        }
    }

    [ClientRpc] 
    private void RpcSyncTransform(Vector3 pos, Quaternion rot)
    {
        if (IAmGrabber)
            return;
        if (!isGrabbed)
            return;
        
        startPos = transform.position;
        startRot = transform.rotation;

        targetPos = pos;
        targetRot = SafeNorm(rot);

        lerpTimer = 0f;
    }
    
    [Command(requiresAuthority = false)]
    private void SetKinematicState(bool isKinematic)
    {
        if (isKinematic)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
        
        _rb.isKinematic = isKinematic;
        _rb.useGravity = !isKinematic;
        
        RpcSetKinematic(isKinematic);
    }

    [ClientRpc]
    private void RpcSetKinematic(bool isKinematic)
    {
        if (isKinematic)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
        _rb.isKinematic = isKinematic;
        _rb.useGravity = !isKinematic;
    }

    [ClientRpc]
    private void RpcOnGrabbed(NetworkIdentity newGrabber)
    {
        Debug.Log($"[SERVER-AUTH] {gameObject.name} now grabbed by {newGrabber}");
    }

    [ClientRpc]
    private void RpcOnReleased()
    {
        Debug.Log($"[SERVER-AUTH] {gameObject.name} released");
    }
    
    private bool IAmGrabber =>
        NetworkClient.connection != null &&
        NetworkClient.connection.identity == grabber;

    #endregion
    
    private static Quaternion SafeNorm(Quaternion q) => Quaternion.Normalize(q);
}