using UnityEngine;

public class MoveMapUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform moveMapTextTransform;
    [SerializeField] private Transform downArrowTransform;
    private Transform targetCamera;

    [Header("Settings")]
    [SerializeField] private float rotationSpeed = 5f; // How fast to track toward target
    [SerializeField] private float elasticLagFactor = 0.1f; // Lower = snappier, Higher = more soft lag

    private Quaternion moveMapTargetRotation;
    private Quaternion arrowTargetRotation;

    private void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main.transform;

        // Initialize target rotations so they don't slerp from identity
        moveMapTargetRotation = moveMapTextTransform.rotation;
        arrowTargetRotation = downArrowTransform.rotation;
    }

    private void Update()
    {
        if (targetCamera == null) return;

        // Handle Move Map Text (X and Y free, Z locked upright)
        Vector3 moveMapDirection = targetCamera.position - moveMapTextTransform.position;
        if (moveMapDirection != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(moveMapDirection);
            lookRotation *= Quaternion.Euler(0f, 180f, 0f); // Correct facing direction

            // Lock Z-axis rotation (prevent rolling)
            Vector3 euler = lookRotation.eulerAngles;
            euler.z = 0f;
            moveMapTargetRotation = Quaternion.Euler(euler);
        }

        // Smooth elastic slerp
        moveMapTextTransform.rotation = Quaternion.Slerp(
            moveMapTextTransform.rotation,
            moveMapTargetRotation,
            Time.deltaTime * rotationSpeed * (1f - elasticLagFactor)
        );

        // Handle Down Arrow (only Y-axis rotation)
        Vector3 arrowDirection = targetCamera.position - downArrowTransform.position;
        arrowDirection.y = 0f; // Flatten to horizontal plane
        if (arrowDirection != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(arrowDirection);
            lookRotation *= Quaternion.Euler(0f, 180f, 0f); // Correct facing direction

            arrowTargetRotation = lookRotation;
        }

        // Smooth elastic slerp
        downArrowTransform.rotation = Quaternion.Slerp(
            downArrowTransform.rotation,
            arrowTargetRotation,
            Time.deltaTime * rotationSpeed * (1f - elasticLagFactor)
        );
    }
}
