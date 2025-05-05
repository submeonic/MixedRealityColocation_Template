using UnityEngine;
using Oculus.Interaction;

public class GrabConfirmAudio : MonoBehaviour
{
    [SerializeField] private AudioClip[] audioClips;
    
    private Grabbable _grabbable;
    private AudioSource _audioSource;

    private void Awake()
    {
        _grabbable = GetComponent<Grabbable>();
        _grabbable.WhenPointerEventRaised += OnPointerEvent;
        _audioSource = GetComponent<AudioSource>();
    }

    private void OnPointerEvent(PointerEvent evt)
    {
        if (evt.Type == PointerEventType.Select && audioClips.Length > 0)
        {
            int randomIndex = Random.Range(0, audioClips.Length);
            _audioSource.PlayOneShot(audioClips[randomIndex]);
        }
    }
}
