using Mirror;
using UnityEngine;

/// <summary>
/// Receives network transform updates and interpolates between them on non-owning clients.
/// </summary>
public class SteeringNetController : NetworkBehaviour
{
    [Tooltip("Must match sender's sync interval for clean interpolation")]
    [SerializeField] private float syncInt = 0.1f;

    private Transform steeringTransform;

    private Vector3 startPosition;
    private Quaternion startRotation;

    private Vector3 targetPosition;
    private Quaternion targetRotation;

    private float lerpTimer = 0f;

    private void Awake()
    {
        steeringTransform = transform;
    }

    /// <summary>
    /// Called by the local client to send transform to server (throttled externally).
    /// </summary>
    public void ClientUpdateTransform(Vector3 pos, Quaternion rot)
    {
        if (!isOwned) return;
        CmdSendTransform(pos, rot);
    }

    [Command]
    private void CmdSendTransform(Vector3 pos, Quaternion rot)
    {
        RpcApplyTransform(pos, rot);
    }

    [ClientRpc]
    private void RpcApplyTransform(Vector3 pos, Quaternion rot)
    {
        if (isOwned) return; // Ignore updates from self

        // Set up interpolation
        startPosition = steeringTransform.position;
        startRotation = steeringTransform.rotation;

        targetPosition = pos;
        targetRotation = rot;

        lerpTimer = 0f;
    }

    private void Update()
    {
        if (isOwned) return;

        lerpTimer += Time.deltaTime;
        float t = Mathf.Clamp01(lerpTimer / syncInt);

        steeringTransform.position = Vector3.Lerp(startPosition, targetPosition, t);
        steeringTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
    }
}