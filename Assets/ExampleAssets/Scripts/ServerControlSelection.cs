using UnityEngine;

/// <summary>
/// Simple “Start” / “Join” selector.  When the user’s hand enters the trigger
/// it either hosts a session (Mirror + anchor advertisement) or starts the
/// anchor-discovery workflow that will connect to an existing host.
/// </summary>
public class ServerControlSelection : MonoBehaviour
{
    enum State { START, JOIN }

    [Header("Mode")]
    [SerializeField] State state = State.START;

    [Header("Systems")]
    [SerializeField] ColocationNetworkManager networkManager;
    [SerializeField] ColocationManager colocationManager;   // drag from scene
                                                            // (LANDiscovery ref no longer needed)

    [Header("Feedback")]
    [SerializeField] AudioClip  selectClip;
    [SerializeField] AudioSource audioSource;
    [SerializeField] MusicController          musicController;
    [SerializeField] MusicController.MusicSnapShotLevel snapshot;
    [SerializeField] GameObject  staticMap;                 // map overlay

    /* ───────────────────────── trigger logic ────────────────────────── */
    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Selector")) return;

        musicController.SetSnapshotLevel(snapshot);
        audioSource.PlayOneShot(selectClip);

        Activate();                         // ≤–––– the important call

        staticMap.SetActive(true);
        Destroy(other.gameObject);          // consume selector
        Destroy(transform.parent.gameObject);
    }

    /* ───────────────────────── session start / join ─────────────────── */
    void Activate()
    {
        switch (state)
        {
            case State.START:   // HOST
                networkManager.StartHost();
                break;

            case State.JOIN:    // CLIENT
                colocationManager.StartColocationDiscovery();
                break;
        }
    }
}
