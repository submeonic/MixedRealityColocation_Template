using Mirror;
using UnityEngine;
using TMPro;
using System;

public class SteeringInputManager : NetworkBehaviour
{
    [Header("Optional debug")]
    [SerializeField] private TextMeshProUGUI debugText;

    [SerializeField] private float syncInt = 0.1f;   // how often to forward to server

    /* NetId of the car we control, set by MenuGrabbable */
    private uint controlledCarNetId;

    /* local queue */
    private float queuedS, queuedT, queuedB;
    private bool  hasQueuedUpdate;
    private float sendTimer;

    public event Action<uint> OnCarAssigned;

    /* ------------------------------------------------- */
    public void AssignCar(uint carNetId)
    {
        controlledCarNetId = carNetId;
        OnCarAssigned?.Invoke(carNetId);
        debugText?.SetText($"Driving Car: {carNetId}");
    }

    /* called every frame by your wheel / trigger code */
    public void SetInput(float steering, float throttle)
    {
        if (!isOwned || controlledCarNetId == 0) return;

        queuedS = steering;
        queuedT = throttle;
        queuedB = (steering == 0f && throttle == 0f) ? 1f : 0f;
        hasQueuedUpdate = true;
        
        // Apply input locally for immediate response
        if (NetworkClient.spawned.TryGetValue(controlledCarNetId, out var identity))
        {
            identity.GetComponent<ArcadeVehicleController>()?.ClientProvideInputs(queuedS, queuedT, queuedB);
        }
    }

    /* throttle network traffic */
    private void Update()
    {
        if (!isOwned || !hasQueuedUpdate) return;

        sendTimer += Time.deltaTime;
        if (sendTimer < syncInt) return;

        sendTimer        = 0f;
        hasQueuedUpdate  = false;

        CmdSendInput(queuedS, queuedT, queuedB, controlledCarNetId);
    }

    /* ------------- network ------------- */

    [Command]
    private void CmdSendInput(float s, float t, float b, uint carNetId,
                              NetworkConnectionToClient sender = null)
    {
        if (!NetworkServer.spawned.TryGetValue(carNetId, out var id)) return;

        var vc = id.GetComponent<ArcadeVehicleController>();
        if (vc == null) return;

        /* broadcast to everyone for interpolation */
        vc.ClientSyncInputs(s, t, b);
    }
}
