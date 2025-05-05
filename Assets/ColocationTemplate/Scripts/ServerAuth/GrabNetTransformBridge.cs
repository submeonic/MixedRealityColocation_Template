using UnityEngine;
using Oculus.Interaction;

public class GrabNetTransformBridge : MonoBehaviour
{
    [SerializeField] private float syncInt = 0.1f;

    private GrabNetController _grabNetController;
    private Grabbable _grabbable;

    private float syncTimer = 0f;
    private bool isGrabbed = false;

    private Vector3 queuedPosition;
    private Quaternion queuedRotation;
    private bool hasQueuedUpdate = false;

    private void Awake()
    {
        _grabbable = GetComponent<Grabbable>();
        _grabNetController = GetComponentInParent<GrabNetController>();
        _grabbable.WhenPointerEventRaised += OnPointerEvent;
    }

    private void OnDestroy()
    {
        if (_grabbable != null)
            _grabbable.WhenPointerEventRaised -= OnPointerEvent;
    }

    private void Update()
    {
        if (!isGrabbed || !hasQueuedUpdate || _grabNetController == null)
            return;

        syncTimer += Time.deltaTime;

        if (syncTimer >= syncInt)
        {
            syncTimer = 0f;
            _grabNetController.ClientSyncTransform(queuedPosition, queuedRotation);
            hasQueuedUpdate = false;
        }
    }

    private void OnPointerEvent(PointerEvent evt)
    {
        if (evt.Type == PointerEventType.Select)
        {
            isGrabbed = true;
            _grabNetController.ClientRequestGrab();
        }
        else if (evt.Type == PointerEventType.Unselect)
        {
            isGrabbed = false;
            _grabNetController.ClientRequestRelease();
        }
    }

    public void RequestForceGrab()
    {
        isGrabbed = true;
        _grabNetController.ClientRequestGrab();
    }

    /// <summary>
    /// Called every tick from grab system with latest transform.
    /// </summary>
    public void UpdateTransformFromTransformer(Vector3 pos, Quaternion rot)
    {
        if (!isGrabbed)
            return;
        
        queuedPosition = pos;
        queuedRotation = rot;
        hasQueuedUpdate = true;
        
        _grabNetController.ClientUpdateTransform(pos, rot);
    }
}