using UnityEngine;

public class ChestDrawPosition : MonoBehaviour
{
    public OVRSkeleton skeleton; // Assign this in the Inspector.
    private OVRBone chest;

    void Update()
    {
        // Check if skeleton is initialized and has bones.
        if (skeleton != null && skeleton.IsInitialized && skeleton.Bones != null)
        {
            // If chest is not assigned, find it in the skeleton.
            if (chest == null)
            {
                foreach (OVRBone bone in skeleton.Bones)
                {
                    if (bone.Id == OVRSkeleton.BoneId.Body_Chest) // Updated to Body_SpineChest (commonly used chest ID).
                    {
                        chest = bone;
                        Debug.Log("Chest bone assigned.");
                        break;
                    }
                }

                // If the chest bone wasn't found, log a warning.
                if (chest == null)
                {
                    Debug.LogWarning("Chest bone not found in skeleton.");
                }
            }

            // If chest is assigned, update position and rotation.
            if (chest != null && chest.Transform != null)
            {
                // Align the rotation with the chest.
                transform.rotation = chest.Transform.rotation;

                // Calculate position offset along the chest's forward vector.
                Vector3 forwardOffset = chest.Transform.forward * 0.5f; // Adjust the distance as needed.
                transform.position = chest.Transform.position + forwardOffset;
            }
        }
        else
        {
            Debug.LogWarning("Skeleton not initialized or tracking not active.");
        }
    }
}
