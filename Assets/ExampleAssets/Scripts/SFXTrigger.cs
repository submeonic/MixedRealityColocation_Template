using UnityEngine;

public class SFXTrigger : MonoBehaviour
{
    private AudioSource audioSource;
    [SerializeField] private AudioClip clip;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Car"))
        {
            if (!audioSource.isPlaying)
            {
                audioSource.PlayOneShot(clip);
            }
        }
    }
}
