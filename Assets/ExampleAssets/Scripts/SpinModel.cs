using UnityEngine;

public class SpinModel : MonoBehaviour
{
    [SerializeField] private float spinSpeed = 20f; // degrees per second

    void Update()
    {
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);
    }
}