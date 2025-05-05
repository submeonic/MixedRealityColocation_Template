using UnityEngine;

public class FloatingAnchor : MonoBehaviour
{
    [SerializeField] private OVRSkeleton ovrSkeleton; // OVRSkeleton component
    [SerializeField] private float forwardOffset = 0.0f; // Distance in front of the joint
    [SerializeField] private float heightOffset = 0.0f;  // Height adjustment
    [SerializeField] private float smoothingfactor = 20f; // Smoothing factor

    private OVRSkeleton.BoneId targetBoneId = OVRSkeleton.BoneId.Body_Chest; // Default to Spine Chest
    private OVRBone targetBone; // Cached reference to the target bone

    void Start()
    {
        if (ovrSkeleton == null)
        {
            Debug.LogError("OVRSkeleton component is not set.");
        }
        else if (ovrSkeleton.IsInitialized)
        {
            targetBone = FindBone(targetBoneId);
        }
    }

    void Update()
    {
        if (ovrSkeleton.IsInitialized)
        {
            if (targetBone == null)
            {
                targetBone = FindBone(targetBoneId);
            }
            else
            {
                // Base position and rotation of the chest joint
                Vector3 basePosition = targetBone.Transform.position;
                Quaternion baseRotation = targetBone.Transform.rotation;

                // Use the local +Y axis for forward direction
                Vector3 forwardDirection = targetBone.Transform.up; // +Y is forward for this joint

                // Offset the joint in front of the chest joint
                Vector3 forwardOffsetVector = forwardDirection.normalized * forwardOffset;
                Vector3 heightOffsetVector = Vector3.up * heightOffset;

                // Calculate final position
                Vector3 finalPosition = basePosition + forwardOffsetVector + heightOffsetVector;

                // Apply the final position to the joint GameObject
                transform.position = Vector3.Lerp(transform.position, finalPosition, Time.deltaTime * smoothingfactor);
                // Apply the base rotation with an additional 90-degree
                transform.rotation = Quaternion.Slerp(transform.rotation, baseRotation * Quaternion.Euler(-90, 0, 90), Time.deltaTime * smoothingfactor);
            }
        }
    }

    private OVRBone FindBone(OVRSkeleton.BoneId boneId)
    {
        Debug.LogWarning("Bone query");
        foreach (OVRBone bone in ovrSkeleton.Bones)
        {
            if (bone.Id == boneId)
            {
                return bone;
            }
        }
        return null; // Bone not found
    }
}
