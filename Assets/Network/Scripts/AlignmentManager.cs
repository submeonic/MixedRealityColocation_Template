using UnityEngine;
using System.Collections;

/// <summary>
/// Handles aligning the user's VR rig to a shared spatial anchor.
/// Ensures that all users are positioned correctly relative to the same anchor in mixed reality.
/// </summary>
public class AlignmentManager : MonoBehaviour
{
    private Transform _cameraRigTransform;

    #region Initialization

    /// <summary>
    /// Finds and stores a reference to the OVRCameraRig transform on Awake.
    /// Ensures that the user's camera rig is ready for alignment.
    /// </summary>
    private void Awake()
    {
        _cameraRigTransform = FindAnyObjectByType<OVRCameraRig>().transform;
    }

    #endregion

    #region Anchor Alignment

    /// <summary>
    /// Aligns the user's camera rig to the given spatial anchor.
    /// Ensures accurate colocation in shared mixed reality experiences.
    /// </summary>
    /// <param name="anchor">The OVRSpatialAnchor to align to.</param>
    public void AlignUserToAnchor(OVRSpatialAnchor anchor)
    {
        if (anchor == null || !anchor.Localized)
        {
            Debug.LogError("Colocation: Invalid or unlocalized anchor. Cannot align.");
            return;
        }

        Debug.Log($"Colocation: Starting alignment to anchor {anchor.Uuid}.");

        StartCoroutine(AlignmentCoroutine(anchor));
    }

    /// <summary>
    /// Coroutine that gradually aligns the user's camera rig to the anchor.
    /// Runs for two frames to ensure precise positioning.
    /// </summary>
    /// <param name="anchor">The spatial anchor used for alignment.</param>
    private IEnumerator AlignmentCoroutine(OVRSpatialAnchor anchor)
    {
        var anchorTransform = anchor.transform;

        for (int alignmentCount = 2; alignmentCount > 0; alignmentCount--)
        {
            // Reset position and rotation to ensure proper alignment
            _cameraRigTransform.position = Vector3.zero;
            _cameraRigTransform.eulerAngles = Vector3.zero;

            yield return null;

            // Move the camera rig relative to the anchor's position
            _cameraRigTransform.position = anchorTransform.InverseTransformPoint(Vector3.zero);
            _cameraRigTransform.eulerAngles = new Vector3(0, -anchorTransform.eulerAngles.y, 0);

            Debug.Log($"Colocation: Aligned Camera Rig Position: {_cameraRigTransform.position}, Camera Rig Rotation: {_cameraRigTransform.eulerAngles}");

            yield return new WaitForEndOfFrame();
        }

        Debug.Log("Colocation: Alignment complete.");
    }

    #endregion
}
