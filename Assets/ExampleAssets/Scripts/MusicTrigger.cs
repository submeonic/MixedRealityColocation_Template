using UnityEngine;
public class MusicTrigger : MonoBehaviour
{
    [SerializeField] private MusicController musicController;
    [SerializeField] private MusicController.MusicSnapShotLevel musicSnapShotLevel;
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Car"))
        {
            musicController.SetSnapshotLevel(musicSnapShotLevel);
        }
    }
}
