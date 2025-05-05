using Oculus.Interaction;
using UnityEngine;

public class NullTransformer : MonoBehaviour, ITransformer
{
    public void Initialize(IGrabbable grabbable) { }

    public void BeginTransform() { }

    public void UpdateTransform() { }

    public void EndTransform() { }
}