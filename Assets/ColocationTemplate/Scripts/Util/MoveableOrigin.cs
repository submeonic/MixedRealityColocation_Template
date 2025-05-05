// ─────────────────────────────────────────────────────────────────────────────
// PlaceMap.cs  ✦  revised for OffsetPlayerToMap(Transform)
// -----------------------------------------------------------------------------
// A network‑spawned grabbable marker. When the local user releases it on a valid
// floor surface, the *server* tells every client to move their play‑space by the
// inverse of the marker’s pose, so the marker appears to stay put while the
// entire world slides underneath. After broadcasting, the server immediately
// resets the marker back to the origin to keep the shared world tidy.
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections;
using Mirror;
using Oculus.Interaction;
using UnityEngine;

[RequireComponent(typeof(Grabbable))]
public class MovableOrigin : NetworkBehaviour
{
    [Header("Visual Feedback")] 
    [SerializeField] private Transform transformParent;
    [SerializeField] private GrabNetController gnc;
    [SerializeField] private GameObject highlight;  // outline / glow shown when "placeable"
    
    [SerializeField] private AudioClip placeSound;
    private AudioSource audioSource;
    private Grabbable _grabbable;

    private bool _selected  = false;   // true while hand is holding it

    // Authoritative flag maintained on the server that says the marker is over a
    // valid floor collider and may therefore be placed.
    [SyncVar(hook = nameof(OnPlaceableChanged))]
    private bool _placeable = false;

    // ─────────────────────────────────────────────────────────────────────
    #region Unity‑lifecycle

    private void Awake()
    {
        _grabbable = GetComponent<Grabbable>();
        _grabbable.WhenPointerEventRaised += OnPointerEvent;
        audioSource = GetComponent<AudioSource>();
    }

    private void OnDestroy()
    {
        if (_grabbable != null)
            _grabbable.WhenPointerEventRaised -= OnPointerEvent;
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────
    #region Placement logic

    /// <summary>
    /// Called by the *server* to tell every client (server included) to inverse‑
    /// teleport their rig so that <this> marker becomes local origin.
    /// </summary>
    [ClientRpc]
    private void RpcReRootPlayers(Vector3 position, Quaternion rotation)
    {
        AlignmentManager am = FindObjectOfType<AlignmentManager>();
        if (am)
        {
            am.OffsetPlayerToMap(position, rotation);   // uses the fully corrected version
            StartCoroutine(ResetMarker());
        }
        else
        {
            Debug.LogError("[PlaceMap] AlignmentManager not found on client.");
        }
    }

    private IEnumerator ResetMarker()
    {
        audioSource.PlayOneShot(placeSound);
        yield return new WaitForSeconds(0.5f);
        transformParent.position = Vector3.zero;
        transformParent.rotation = Quaternion.identity;
    }

    /// <summary>
    /// Server‑side entry‑point: broadcast the re‑root and then snap the marker
    /// back to the origin so subsequent placements start from a clean slate.
    /// </summary>
    [Command(requiresAuthority = false)]
    private void CmdCommitPlacement(Vector3 position, Quaternion rotation)
    {
        RpcReRootPlayers(position, rotation);

        // Return marker to (0,0,0) in shared space so it can be moved again.
        StartCoroutine(ResetMarker());
        _placeable = false;     // prevent double‑fires while it resets

        Debug.Log("[PlaceMap] Placement committed and marker reset on server.");
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────
    #region Pointer & trigger events

    [Command(requiresAuthority = false)]
    private void CmdSetPlaceable(bool canPlace) => _placeable = canPlace;

    private void OnPlaceableChanged(bool _, bool newVal)
    {
        if (highlight) highlight.SetActive(newVal);
    }

    private void OnPointerEvent(PointerEvent evt)
    {
        switch (evt.Type)
        {
            case PointerEventType.Select:
                _selected = true;
                break;

            case PointerEventType.Unselect:
                _selected = false;
                if (_placeable)
                {
                    transformParent.position = new Vector3(transformParent.position.x, 0f, transformParent.position.z);
                    // Ask the server to finalise the placement.
                    CmdCommitPlacement(transformParent.position, transformParent.rotation);
                }
                break;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Floor") && _selected)
        {
            CmdSetPlaceable(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Floor"))
        {
            CmdSetPlaceable(false);
        }
    }

    #endregion
}
