using Mirror;
using UnityEngine;

public class SteeringReferenceProvider : NetworkBehaviour
{
    private OVRSkeleton ovrSkeleton = null;
    private GameObject trackingSpace = null;
    void Awake()
    {
        if (!isOwned)
        {
            ovrSkeleton = LocalReferenceManager.Instance.OvrSkeleton;
            trackingSpace = LocalReferenceManager.Instance.TrackingSpace;
        }
    }
    
    public OVRSkeleton OvrSkeleton
    {
        get { return ovrSkeleton; }
    }

    public GameObject TrackingSpace
    {
        get { return trackingSpace; }
    }
}
