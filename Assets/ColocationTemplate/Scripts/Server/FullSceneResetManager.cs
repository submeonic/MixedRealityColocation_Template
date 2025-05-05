using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;

/// <summary>
/// Shuts down all colocation + networking systems and clean-reloads the active
/// scene.  Intended as a “panic reset” when the anchor or network handshake
/// cannot be recovered.
/// </summary>
[AddComponentMenu("Networking/Full-Scene Reset Manager")]
public class FullSceneResetManager : MonoBehaviour
{
    [Tooltip("Extra time (seconds) to give Mirror sockets before reloading.")]
    [SerializeField] float shutdownGraceSeconds = 0.5f;

    bool isResetting;

    /* ───────────────────────── public entry point ─────────────────────── */
    public void TriggerFullReset()
    {
        if (!isResetting) StartCoroutine(ResetAndReload());
    }

    /* ─────────────────────────── coroutine body ───────────────────────── */
    IEnumerator ResetAndReload()
    {
        isResetting = true;
        Debug.Log("[FullSceneReset] Starting full reset…");

        /* 1) Stop colocation workflows (anchor discovery / advertisement) */
        ColocationManager cm = LocalReferenceManager.Instance?.ColocationManager;
        if (cm != null)
        {
            Debug.Log("[FullSceneReset] Stopping colocation processes…");
            cm.StopColocationDiscovery();      // also stops LANDiscovery
            cm.StopColocationAdvertisement();
            yield return null;                 // give async voids a frame
        }

        /* 2) Shut down Mirror networking cleanly */
        NetworkManager nm = NetworkManager.singleton;
        if (NetworkServer.active && NetworkClient.isConnected)
        {
            Debug.Log("[FullSceneReset] Stopping host (server + client) …");
            nm.StopHost();
        }
        else if (NetworkServer.active)
        {
            Debug.Log("[FullSceneReset] Stopping dedicated server …");
            nm.StopServer();
        }
        else if (NetworkClient.isConnected || NetworkClient.active)
        {
            Debug.Log("[FullSceneReset] Stopping client …");
            nm.StopClient();
        }

        /* give sockets a moment to close */
        float t = 0f;
        while (t < shutdownGraceSeconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        /* 3) Reset static flags so the next session starts fresh */
        ColocationManager.ClearColocationFlag();

        /* 4) Reload the active scene */
        Debug.Log("[FullSceneReset] Reloading scene …");
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex, LoadSceneMode.Single);
    }
}
