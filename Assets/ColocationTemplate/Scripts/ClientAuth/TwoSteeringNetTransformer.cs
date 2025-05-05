using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Oculus.Interaction
{
    /// <summary>
    /// SteeringTransformer controls the rotation of a steering wheel object and calculates input
    /// for steering and throttle based on hand movements in VR using OVRSkeleton.
    /// </summary>
    public class TwoSteeringNetTransformer : NetworkBehaviour, ITransformer
    {
        #region Serialized Fields

        [Header("References")]
        [SerializeField] private GameObject steeringWheelContainer; // Steering wheel GameObject
        [SerializeField] private GameObject rightHandAttatch; // Reference to the right hand attachment

        #endregion

        #region Private Fields
        
        // [Insert Comment]
        private SteeringNetTransformBridge transformBridge;
        private SteeringReferenceProvider str;
        private SteeringInputManager inputManager;
        
        // Bone and grabbable references
        private OVRSkeleton.BoneId targetBoneId = OVRSkeleton.BoneId.Body_Chest; // Target chest joint
        private OVRBone targetBone; // Cached reference to the chest bone
        private IGrabbable grabbable; // Reference to the grabbable component

        // Rotation and throttle control variables
        private float cumulativeRotation = 0f; // Tracks Z-axis rotation for steering input
        private int firstPoseIndex = 0; // Index of the first grab point
        private float maxSteeringAngle = 110f; // Maximum steering angle in degrees
        private float throttleInput = 0.0f; // Current throttle input value
        private float throttleThreshold = 1.0f; // Distance threshold for throttle input
        private float armLength = 0.6f; // Length of the user's arms

        #endregion

        #region Initialization Methods

        /// <summary>
        /// Initializes the SteeringTransformer with a given IGrabbable object.
        /// </summary>
        /// <param name="grabbable">The IGrabbable object to attach.</param>
        public void Initialize(IGrabbable grabbable)
        {
            this.grabbable = grabbable;

            if (!isOwned)
                return;

            str = GetComponent<SteeringReferenceProvider>();
            transformBridge = GetComponent<SteeringNetTransformBridge>();
            inputManager = GetComponent<SteeringInputManager>();
            
            if (str.OvrSkeleton == null)
            {
                Debug.LogError("OVRSkeleton component is not set.");
                return;
            }

            if (str.OvrSkeleton.IsInitialized)
            {
                targetBone = FindBone(targetBoneId);
            }
        }

        #endregion

        #region Transformation Methods

        /// <summary>
        /// Called when the transformation begins, initializing rotation and input values.
        /// </summary>
        public void BeginTransform()
        {
            if (!isOwned)
                return;
            
            if (targetBone == null)
            {
                targetBone = FindBone(targetBoneId);
            }

            if (targetBone != null)
            {
                cumulativeRotation = 0f;
                throttleInput = 0f;
            }

            float distance1 = Vector3.Distance(grabbable.GrabPoints[0].position, rightHandAttatch.transform.position);
            float distance2 = Vector3.Distance(grabbable.GrabPoints[1].position, rightHandAttatch.transform.position);

            firstPoseIndex = distance1 < distance2 ? 0 : 1;
        }

        /// <summary>
        /// Updates the transformation each frame, applying steering rotation and throttle input based on hand movements.
        /// </summary>
        public void UpdateTransform()
        {
            if (targetBone == null || grabbable.GrabPoints.Count == 0 || !isOwned)
                return;

            Transform targetTransform = grabbable.Transform;

            // Step 1: Update position (GrabFreeTransformer behavior)
            Vector3 grabPosition = GetCentroid(grabbable.GrabPoints);
            targetTransform.position = grabPosition;

            // Step 2: Rotate to face the player's chest
            Vector3 chestPosition = targetBone.Transform.position;
            Vector3 directionToChest = (chestPosition - targetTransform.position).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(directionToChest, Vector3.up);
            targetTransform.rotation = targetRotation;
            
            // Step 3: Send updated position/rotation to the network bridge for syncing
            transformBridge.UpdateTransformFromTransformer(
                steeringWheelContainer.transform.position,
                steeringWheelContainer.transform.rotation
            );
            
            // Step 4: Rotate along Z-axis for steering input
            CalculateSteeringInput(targetTransform);

            // Step 5: Calculate throttle input based on distance to chest
            CalculateThrottleInput(targetTransform.position);
            
            // Step 6: Send normalized steering/throttle values to the input manager
            float steeringInput = cumulativeRotation / maxSteeringAngle;
            inputManager.SetInput(steeringInput, throttleInput);
        }

        /// <summary>
        /// Called when the transformation ends, resetting rotation and input values.
        /// </summary>
        public void EndTransform()
        {
            if (!isOwned)
                return;
            
            steeringWheelContainer.transform.localRotation = Quaternion.identity;
            cumulativeRotation = 0f;
            throttleInput = 0f;
            armLength = 0.6f;
            
            float steeringInput = cumulativeRotation / maxSteeringAngle;
            inputManager.SetInput(steeringInput, throttleInput);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Finds and returns the OVRBone associated with the specified BoneId.
        /// </summary>
        /// <param name="boneId">The target BoneId.</param>
        /// <returns>The corresponding OVRBone, or null if not found.</returns>
        private OVRBone FindBone(OVRSkeleton.BoneId boneId)
        {
            foreach (OVRBone bone in str.OvrSkeleton.Bones)
            {
                if (bone.Id == boneId)
                {
                    return bone;
                }
            }
            return null;
        }

        /// <summary>
        /// Calculates the centroid of a list of Pose objects.
        /// </summary>
        /// <param name="poses">The list of Pose objects.</param>
        /// <returns>The centroid position.</returns>
        private Vector3 GetCentroid(List<Pose> poses)
        {
            Vector3 sum = Vector3.zero;
            foreach (var pose in poses)
            {
                sum += pose.position;
            }
            return sum / poses.Count;
        }

        /// <summary>
        /// Calculates and applies steering input based on hand positions.
        /// </summary>
        /// <param name="targetTransform">The transform of the grabbable object.</param>
        private void CalculateSteeringInput(Transform targetTransform)
        {
            Pose firstPose = grabbable.GrabPoints[0];
            Pose secondPose = grabbable.GrabPoints[1];

            // Calculate positions in local space relative to the wheel
            Vector3 position1 = targetTransform.InverseTransformPoint(firstPose.position);
            Vector3 position2 = targetTransform.InverseTransformPoint(secondPose.position);

            // Determine direction order based on relative X positions
            Vector3 direction = firstPoseIndex == 0 ? (position2 - position1) : (position1 - position2);

            // Calculate rotation in degrees
            float currentRotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            if (Mathf.Approximately(cumulativeRotation, 0f))
            {
                cumulativeRotation = currentRotation;
            }

            float deltaRotation = Mathf.Clamp(Mathf.DeltaAngle(cumulativeRotation, currentRotation), -15f, 15f);
            cumulativeRotation = Mathf.Clamp(cumulativeRotation + deltaRotation, -maxSteeringAngle, maxSteeringAngle);

            // Apply rotation to the steering wheel
            Quaternion localZRotation = Quaternion.Euler(0, 0, cumulativeRotation);
            steeringWheelContainer.transform.localRotation = localZRotation;
        }

        /// <summary>
        /// Calculates throttle input based on the distance to the chest.
        /// </summary>
        /// <param name="targetPosition">The position of the grabbable object.</param>
        private void CalculateThrottleInput(Vector3 targetPosition)
        {
            float distanceToChest = Vector3.Distance(targetPosition, targetBone.Transform.position);
            float distanceRatio = distanceToChest / armLength;

            if (distanceRatio <= 0.45f)
                throttleInput = -1f;
            else if (distanceRatio > 0.45f && distanceRatio <= 0.55f)
                throttleInput = -1f + ((distanceRatio - 0.45f) / 0.1f);
            else if (distanceRatio > 0.55f && distanceRatio <= 0.575f)
                throttleInput = 0f;
            else if (distanceRatio > 0.575f && distanceRatio <= 0.85f)
                throttleInput = (distanceRatio - 0.575f) / 0.275f;
            else
                throttleInput = 1f;

            if (distanceToChest > armLength)
            {
                armLength = distanceToChest;
            }
        }

        #endregion
    }
}
