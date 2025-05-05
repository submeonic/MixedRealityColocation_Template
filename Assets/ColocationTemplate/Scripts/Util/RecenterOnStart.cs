using UnityEngine;
using System.Collections;

public class RecenterOnStart : MonoBehaviour
{
    private void Start()
    {
        StartCoroutine(OnStart());
    }
    private IEnumerator OnStart()
    {
        yield return new WaitForSeconds(1f);
        
        // Recenter and zero-out your rig
        OVRManager.display.RecenterPose();

        var skeletonGO = LocalReferenceManager.Instance?.OvrSkeleton?.gameObject;
        if (skeletonGO != null)
        {
            skeletonGO.transform.localPosition = Vector3.zero;
            skeletonGO.transform.localRotation = Quaternion.identity;
        }

        var trackingGO = LocalReferenceManager.Instance?.TrackingSpace?.gameObject;
        if (trackingGO != null)
        {
            trackingGO.transform.localPosition = Vector3.zero;
            trackingGO.transform.localRotation = Quaternion.identity;
        }

        Debug.Log("AlignmentManager: Camera rig aligned to world origin.");
    }
}
