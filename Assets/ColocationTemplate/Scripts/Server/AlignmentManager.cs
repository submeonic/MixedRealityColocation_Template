using UnityEngine;
using System.Collections;

public class AlignmentManager : MonoBehaviour
{
    private Transform _cameraRigTransform;

    #region Initialization
    private void Awake()
    {
        // Assumes an OVRCameraRig is present in the scene.
        _cameraRigTransform = FindObjectOfType<OVRCameraRig>().transform;
    }
    #endregion

    #region Anchor Alignment

    /// <summary>
    /// Aligns the user's rig to the given spatial anchor.
    /// </summary>
    /// <param name="anchor">The spatial anchor to align to.</param>
    public void AlignUserToAnchor(OVRSpatialAnchor anchor)
    {
        if (!anchor || !anchor.Localized)
        {
            Debug.LogError("AlignmentManager: Invalid or unlocalized anchor. Cannot align.");
            return;
        }
        Debug.Log($"AlignmentManager: Starting alignment to anchor {anchor.Uuid}.");
        StartCoroutine(AlignmentCoroutine(anchor));
    }

    private IEnumerator AlignmentCoroutine(OVRSpatialAnchor anchor)
    {
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        
        var anchorTransform = anchor.transform;

        for (var alignmentCount = 2; alignmentCount > 0; alignmentCount--)
        {
            _cameraRigTransform.position = Vector3.zero;
            _cameraRigTransform.eulerAngles = Vector3.zero;
            yield return null;
            
            // Align rig relative to the anchor.
            _cameraRigTransform.position = anchorTransform.InverseTransformPoint(Vector3.zero);
            _cameraRigTransform.eulerAngles = new Vector3(0, -anchorTransform.eulerAngles.y, 0);
            Debug.Log($"AlignmentManager: Aligned Camera Rig Position: {_cameraRigTransform.position}, Rotation: {_cameraRigTransform.eulerAngles}");
            yield return new WaitForEndOfFrame();
        }
        Debug.Log("AlignmentManager: Alignment complete.");
    }

    #endregion
    
    #region Map Alignment
    
    /// <summary>
    /// Re-roots the player so that <markerPos, markerRot> becomes local (0,0,0 / +Z).
    /// Called from PlaceMap via RPC.
    /// </summary>
    public void OffsetPlayerToMap(Vector3 markerPos, Quaternion markerRot)
    {
        if (_cameraRigTransform == null)
        {
            Debug.LogError("[AlignmentManager] Camera rig transform not found.");
            return;
        }

        // ── inverse yaw (keep player upright) ───────────────────────────────
        float      yaw  = markerRot.eulerAngles.y;
        Quaternion Rinv = Quaternion.Euler(0f, -yaw, 0f);   // R⁻¹

        // ── inverse translation expressed in the rotated frame ─────────────
        Vector3    Tinv = -(Rinv * markerPos);              // –R⁻¹·Pₘ

        // ── apply to rig (rotation then position) ──────────────────────────
        _cameraRigTransform.rotation = Rinv * _cameraRigTransform.rotation;
        _cameraRigTransform.position = Rinv * _cameraRigTransform.position + Tinv;

        // (optional sanity check)
        Vector3 local = _cameraRigTransform.InverseTransformPoint(markerPos);
        Debug.Log($"[Alignment] marker local {local:F4}  rig world {_cameraRigTransform.position:F3}");
    }
    #endregion
}