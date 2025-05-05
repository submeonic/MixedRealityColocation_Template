using UnityEngine;
using Oculus.Interaction;

using static Oculus.Interaction.TransformerUtils;

public class OneGrabNetTransformer : MonoBehaviour, ITransformer
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

    private GrabNetTransformBridge _grabNetTransformBridge;
    private IGrabbable _grabbable;
    private Pose _grabDeltaInLocalSpace;
    private PositionConstraints _parentConstraints;
    private Pose _localToTarget;

    public void Initialize(IGrabbable grabbable)
    {
        _grabbable = grabbable;
        Vector3 initialPosition = _grabbable.Transform.localPosition;
        _parentConstraints = GenerateParentConstraints(_positionConstraints, initialPosition);

        // Ensure we have a reference to the networked grabbable component
        _grabNetTransformBridge = GetComponent<GrabNetTransformBridge>();
        if (_grabNetTransformBridge == null)
        {
            Debug.LogError("[OneGrabNetTransformer] No GrabNetTransformBridge found on target!");
        }
    }

    public void BeginTransform()
    {
        Transform target = _grabbable.Transform;
        var grabPose = _grabbable.GrabPoints[0];
        _localToTarget = WorldToLocalPose(grabPose, target.worldToLocalMatrix);
    }

    public void UpdateTransform()
    {
        if (_grabNetTransformBridge == null) return;

        Transform target = _grabbable.Transform;
        var grabPose = _grabbable.GrabPoints[0];

        // Compute the desired position & rotation
        Pose result = AlignLocalToWorldPose(target.localToWorldMatrix, _localToTarget, grabPose);
        Vector3 newPosition = GetConstrainedTransformPosition(result.position, _parentConstraints, target.parent);
        Quaternion newRotation = GetConstrainedTransformRotation(result.rotation, _rotationConstraints);

        // Instead of applying movement locally, send it to the server
        _grabNetTransformBridge.UpdateTransformFromTransformer(newPosition, newRotation);
    }

    public void EndTransform()
    {

    }
}
