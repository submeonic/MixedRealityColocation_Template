using Mirror;
using Oculus.Interaction;
using UnityEngine;

public class SteeringWheelUtilities : NetworkBehaviour
{
    [Header("Poke Menu")]
    [Tooltip("The Poke Menu GameObject to toggle based on grab state.")]
    [SerializeField] private GameObject pokeMenu;

    private Grabbable grabbable;
    private GameObject trackingSpace;
    private SteeringNetTransformBridge transformBridge;
    private OVRSkeleton.BoneId targetBoneId = OVRSkeleton.BoneId.Body_Hips; // Target chest joint
    private OVRBone targetBone;
    private OVRSkeleton ovrSkeleton;
    private bool grabbed = false;

    public override void OnStartAuthority()
    {
        // Always get the grabbable, regardless of authority
        grabbable = GetComponentInChildren<Grabbable>();

        if (grabbable == null)
        {
            Debug.LogError("[SteeringWheelUtilities] No Grabbable found on this object or children.");
            return;
        }

        // Only subscribe/interact if this client owns the object
        if (!isOwned)
            return;

        // Get tracking space from shared steering reference
        var provider = GetComponent<SteeringReferenceProvider>();
        if (provider != null)
        {
            trackingSpace = provider.TrackingSpace;
            ovrSkeleton = provider.OvrSkeleton;
            targetBone = FindBone(targetBoneId);
        }
        else
        {
            Debug.LogWarning("[SteeringWheelUtilities] No SteeringReferenceProvider found.");
        }
        
        transformBridge = GetComponent<SteeringNetTransformBridge>();
        
        grabbable.WhenPointerEventRaised += OnPointerEventRaised;
    }

    private void OnDestroy()
    {
        // Always unsubscribe if we had the grabbable
        if (grabbable != null)
        {
            grabbable.WhenPointerEventRaised -= OnPointerEventRaised;
        }
    }

    private void OnPointerEventRaised(PointerEvent pointerEvent)
    {
        if (!isOwned || grabbable == null)
            return;

        int grabbingCount = grabbable.SelectingPointsCount;

        // Parent to tracking space if necessary
        if (trackingSpace != null && transform.parent != trackingSpace.transform)
        {
            transform.SetParent(trackingSpace.transform);
        }

        // Show/hide poke menu based on grab count
        if (grabbingCount == 1)
        {
            pokeMenu?.SetActive(true);
        }
        else
        {
            pokeMenu?.SetActive(false);
        }

        if (grabbingCount == 0)
        {
            grabbed = false;
        }
        else
        {
            grabbed = true;
        }
    }

    private void Update()
    {
        if (!isOwned || grabbed || targetBone == null || transformBridge == null)
            return;

        Transform targetTransform = grabbable.Transform;

        // === CONFIGURABLE OFFSETS ===      (down, forward, right)
        Vector3 positionOffset = new Vector3(0.2f, 0.25f, -0.25f); // Slight forward offset from hips
        Quaternion rotationOffset = Quaternion.Euler(30, 0, 0); // You can tweak this to match hand-alignment

        // === TARGET POSITION & ROTATION ===
        Vector3 targetPosition = targetBone.Transform.position + targetBone.Transform.rotation * positionOffset;
        Quaternion targetRotation = targetBone.Transform.rotation * rotationOffset;

        // === SMOOTH LERPING ===
        float lerpSpeed = 5f; // Increase for faster return-to-position
        targetTransform.position = Vector3.Lerp(targetTransform.position, targetPosition, Time.deltaTime * lerpSpeed);
        targetTransform.rotation = Quaternion.Slerp(targetTransform.rotation, targetRotation, Time.deltaTime * lerpSpeed);

        // === NETWORK SYNC ===
        transformBridge.UpdateTransformFromTransformer(targetTransform.position, targetTransform.rotation);
    }


    private void OnDisable()
    {
        // Optional: detach when disabled
        // transform.SetParent(null);
    }
    
    private OVRBone FindBone(OVRSkeleton.BoneId boneId)
    {
        foreach (OVRBone bone in ovrSkeleton.Bones)
        {
            if (bone.Id == boneId)
            {
                return bone;
            }
        }
        return null;
    }
}