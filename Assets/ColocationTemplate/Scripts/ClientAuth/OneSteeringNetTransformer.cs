using Mirror;
using UnityEngine;
using Oculus.Interaction;

using static Oculus.Interaction.TransformerUtils;

public class OneSteeringNetTransformer : NetworkBehaviour, ITransformer
{
    [SerializeField]
    private PositionConstraints _positionConstraints =
        new PositionConstraints()
        {
            XAxis = new ConstrainedAxis(),
            YAxis = new ConstrainedAxis(),
            ZAxis = new ConstrainedAxis()
        };

    [SerializeField]
    private RotationConstraints _rotationConstraints =
        new RotationConstraints()
        {
            XAxis = new ConstrainedAxis(),
            YAxis = new ConstrainedAxis(),
            ZAxis = new ConstrainedAxis()
        };

    private SteeringNetTransformBridge _transformBridge;
    private IGrabbable _grabbable;
    private Pose _grabDeltaInLocalSpace;
    private PositionConstraints _parentConstraints;
    private Pose _localToTarget;

    public void Initialize(IGrabbable grabbable)
    {
        _grabbable = grabbable;

        if (!isOwned)
            return;
        
        Vector3 initialPosition = _grabbable.Transform.localPosition;
        _parentConstraints = GenerateParentConstraints(_positionConstraints, initialPosition);

        // Ensure we have a reference to the networked grabbable component
        _transformBridge = GetComponent<SteeringNetTransformBridge>();
        if (_transformBridge == null)
        {
            Debug.LogError("[OneGrabNetTransformer] No GrabNetTransformBridge found on target!");
        }
    }

    public void BeginTransform()
    {
        if (!isOwned)
            return;
        
        Transform target = _grabbable.Transform;
        var grabPose = _grabbable.GrabPoints[0];
        _localToTarget = WorldToLocalPose(grabPose, target.worldToLocalMatrix);
    }

    public void UpdateTransform()
    {
        if (_transformBridge == null || !isOwned) return;

        Transform target = _grabbable.Transform;
        var grabPose = _grabbable.GrabPoints[0];

        // Compute the desired position & rotation
        Pose result = AlignLocalToWorldPose(target.localToWorldMatrix, _localToTarget, grabPose);
        Vector3 newPosition = GetConstrainedTransformPosition(result.position, _parentConstraints, target.parent);
        Quaternion newRotation = GetConstrainedTransformRotation(result.rotation, _rotationConstraints);
        
        // Apply Transform Locally
        target.position = newPosition;
        target.rotation = newRotation;

        // Send transform to the server
        _transformBridge.UpdateTransformFromTransformer(newPosition, newRotation);
    }

    public void EndTransform()
    {

    }
}
