using System;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Owns the “anchor-first, network-second” workflow.
/// • HOST  → advertises its LAN URI inside the shared-anchor metadata  
/// • CLIENT→ discovers anchor *or* LAN, waits for localisation, then tells
///           <see cref="ColocationNetworkManager"/> to connect.
/// </summary>
[AddComponentMenu("Networking/Colocation Manager")]
public class ColocationManager : MonoBehaviour
{
    /* ────────────────────────── inspector refs ─────────────────────────── */
    [Header("References")]
    [SerializeField] AlignmentManager        alignmentManager;
    [SerializeField] ColocationNetworkManager networkManager;
    [SerializeField] FullSceneResetManager   fullSceneResetManager;

    /* ────────────────────────── runtime state ──────────────────────────── */
    Guid _sharedAnchorGroupId;                       // stays constant for session
    public static bool ColocationSuccessful { get; private set; } = false;
    
    /// <summary>
    /// True only after the client has fully localised to (and aligned with)
    /// the shared anchor.  Used by ColocationNetworkManager to know when it can
    /// safely open the network connection.
    /// </summary>

    
    public static void ClearColocationFlag()
    {
        ColocationSuccessful = false;
    }

    /* ───────────────────────────── SERVER FLOW ─────────────────────────── */
    
    public void StartColocationSession()
    {
        Debug.Log("ColocationManager: starting anchor advertisement…");
        _ = AdvertiseColocationSession();
    }
    
    async Task AdvertiseColocationSession()
    {
        try
        {
            string uri = networkManager.GetLanServerUri();      // "kcp://ip:port"
            
            if (string.IsNullOrWhiteSpace(uri))
            {
                Debug.LogError("ColocationManager: no LAN URI available.");
                return;
            }
            
            Uri parsedUri = new Uri(uri);
            string ipAddress = parsedUri.Host;
            string advertisementMessage = $"SharedSpatialAnchorSession|{ipAddress}";
            byte[] advertisementData = Encoding.UTF8.GetBytes(advertisementMessage);

            var startResult = await OVRColocationSession.StartAdvertisementAsync(advertisementData);
            if (!startResult.Success)
            {
                Debug.LogError($"ColocationManager: advertisement failed ({startResult.Status}).");
                return;
            }

            _sharedAnchorGroupId = startResult.Value;
            Debug.Log($"ColocationManager: advertisement started. UUID={_sharedAnchorGroupId}, LAN: ={ipAddress}");

            await CreateAndShareAlignmentAnchor();
        }
        catch (Exception e)
        {
            Debug.LogError($"ColocationManager: advertise error → {e.Message}");
        }
    }

    /* creates + saves + shares anchor, retry loop unchanged … */
    async Task CreateAndShareAlignmentAnchor()
    {
        const int maxAttempts = 10;
        const int delayMs     = 1000;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            Debug.Log($"ColocationManager: anchor share attempt {attempt}");

            var anchor = await CreateAnchor(Vector3.zero, Quaternion.identity);
            if (anchor == null || !anchor.Localized)
            {
                await Task.Delay(delayMs); continue;
            }

            if (!anchor.Localized)
            {
                await Task.Delay(delayMs); continue;
            }

            var save = await anchor.SaveAnchorAsync();
            if (!save.Success)
            {
                await Task.Delay(delayMs); continue;
            }

            var shareResult = await OVRSpatialAnchor.ShareAsync(
                           new List<OVRSpatialAnchor> { anchor },
                           _sharedAnchorGroupId);

            if (shareResult.Success)
            {
                Debug.Log("ColocationManager: anchor shared.");
                return;
            }
            await Task.Delay(delayMs);
        }

        Debug.LogError("ColocationManager: all anchor-share attempts failed.");
        fullSceneResetManager.TriggerFullReset();
    }

    async Task<OVRSpatialAnchor> CreateAnchor(Vector3 pos, Quaternion rot)
    {
        var anchorGO = new GameObject("Alignment Anchor") { transform = { position = pos, rotation = rot } };
        var spatialAnchor = anchorGO.AddComponent<OVRSpatialAnchor>();
        while (!spatialAnchor.Created) await Task.Yield();
        return spatialAnchor;
    }

    public async void StopColocationAdvertisement()
    {
        var res = await OVRColocationSession.StopAdvertisementAsync();
        Debug.Log(res.Success
            ? "ColocationManager: stopped advertisement."
            : $"ColocationManager: stop advertisement failed ({res.Status})");
    }

    /* ───────────────────────────── CLIENT FLOW ─────────────────────────── */

    public void StartColocationDiscovery()
    {
        Debug.Log("ColocationManager: client searching (anchor + LAN)…");
        DiscoverNearbySession();              // OVR
    }

    async void DiscoverNearbySession()
    {
        try
        {
            OVRColocationSession.ColocationSessionDiscovered += OnColocationSessionDiscovered;
            var discoveryResult = await OVRColocationSession.StartDiscoveryAsync();
            if (!discoveryResult.Success)
                Debug.LogError($"ColocationManager: anchor discovery failed ({discoveryResult.Status})");
            else
                Debug.Log("ColocationManager: anchor discovery started.");
        }
        catch (Exception e)
        {
            Debug.LogError($"ColocationManager: discovery error → {e.Message}");
        }
    }

    /* ───── callbacks: anchor metadata or LAN broadcast supply the URI ──── */

    void OnColocationSessionDiscovered(OVRColocationSession.Data session)
    {
        Debug.Log("ColocationManager: Colocation session discovered.");
        OVRColocationSession.ColocationSessionDiscovered -= OnColocationSessionDiscovered;

        string advertisementMessage = Encoding.UTF8.GetString(session.Metadata);
        string[] splitData = advertisementMessage.Split('|');
        if (splitData.Length < 2)
        {
            Debug.LogError("ColocationManager: advertisement data missing URI.");
            return;
        }

        _sharedAnchorGroupId = session.AdvertisementUuid;
        string serverAddress = splitData[1];

        Debug.Log($"ColocationManager: Discovered session. GroupID: {_sharedAnchorGroupId}, Server Address: {serverAddress}");

        //TryConnect(uri);  
        _ = LoadAndAlignToAnchor(_sharedAnchorGroupId);
        networkManager.RequestLanConnection(serverAddress);
    }
    /* ────────────────── anchor load & user-alignment (unchanged) ───────── */

    async Task LoadAndAlignToAnchor(Guid groupId)
    {
        try
        {
            var unbound = new List<OVRSpatialAnchor.UnboundAnchor>();
            var res = await OVRSpatialAnchor.LoadUnboundSharedAnchorsAsync(groupId, unbound);
            if (!res.Success || unbound.Count == 0)
            {
                Debug.LogError($"ColocationManager: load anchors failed (count={unbound.Count}).");
                return;
            }

            foreach (var ub in unbound)
            {
                if (await ub.LocalizeAsync())
                {
                    var go   = new GameObject($"Anchor_{ub.Uuid}");
                    var sa   = go.AddComponent<OVRSpatialAnchor>();
                    ub.BindTo(sa);
                    alignmentManager.AlignUserToAnchor(sa);

                    ColocationSuccessful = true;   // set if LAN won the race first
                    
                    Debug.Log($"ColocationManager: anchor localised ({ub.Uuid}).");
                    return;
                }
            }

            Debug.LogWarning("ColocationManager: none of the anchors localised.");
        }
        catch (Exception e)
        {
            Debug.LogError($"ColocationManager: error loading anchors → {e.Message}");
        }
    }

    public async void StopColocationDiscovery()
    {
        var res = await OVRColocationSession.StopDiscoveryAsync();
        Debug.Log(res.Success
            ? "ColocationManager: stopped anchor discovery."
            : $"ColocationManager: stop discovery failed ({res.Status})");

        OVRColocationSession.ColocationSessionDiscovered -= OnColocationSessionDiscovered;
    }
}
