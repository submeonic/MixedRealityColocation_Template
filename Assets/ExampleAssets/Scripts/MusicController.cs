using UnityEngine;
using UnityEngine.Audio;

public class MusicController : MonoBehaviour
{
    public enum MusicSnapShotLevel
    {
        Start,
        LowMelody,
        MediumMelody,
        Drums,
        DrumsFill,
        DrumsMelody
    }

    [Header("References")]
    [SerializeField] private AudioMixer audioMixer;

    [Header("Snapshots")]
    [SerializeField] private AudioMixerSnapshot startSnapshot;
    [SerializeField] private AudioMixerSnapshot lowMelodySnapshot;
    [SerializeField] private AudioMixerSnapshot mediumMelodySnapshot;
    [SerializeField] private AudioMixerSnapshot drumsFillSnapshot;
    [SerializeField] private AudioMixerSnapshot drumsSnapshot;
    [SerializeField] private AudioMixerSnapshot drumsMelodySnapshot;

    [Header("Settings")]
    [SerializeField] private float transitionTime = 1.0f; // Time to crossfade

    private MusicSnapShotLevel currentSnapShotLevel = MusicSnapShotLevel.Start;

    private void Start()
    {
        // Start with the initial snapshot
        TransitionToSnapshot(currentSnapShotLevel);
    }

    /// <summary>
    /// Public method to set the snapshot level from other scripts.
    /// </summary>
    /// <param name="newLevel">The snapshot level to transition to.</param>
    public void SetSnapshotLevel(MusicSnapShotLevel newLevel)
    {
        if (newLevel != currentSnapShotLevel)
        {
            currentSnapShotLevel = newLevel;
            TransitionToSnapshot(currentSnapShotLevel);
        }
    }

    private void TransitionToSnapshot(MusicSnapShotLevel level)
    {
        switch (level)
        {
            case MusicSnapShotLevel.Start:
                startSnapshot.TransitionTo(transitionTime);
                break;
            case MusicSnapShotLevel.LowMelody:
                lowMelodySnapshot.TransitionTo(transitionTime);
                break;
            case MusicSnapShotLevel.MediumMelody:
                mediumMelodySnapshot.TransitionTo(transitionTime);
                break;
            case MusicSnapShotLevel.Drums:
                drumsSnapshot.TransitionTo(transitionTime);
                break;
            case MusicSnapShotLevel.DrumsFill:
                drumsFillSnapshot.TransitionTo(transitionTime);
                break;
            case MusicSnapShotLevel.DrumsMelody:
                drumsMelodySnapshot.TransitionTo(transitionTime);
                break;
            default:
                Debug.LogWarning("MusicController: Unknown snapshot level.");
                break;
        }
    }
}
