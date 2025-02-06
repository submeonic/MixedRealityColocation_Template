using System; 
using System.Text;
using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using Mirror;

/// <summary>
/// Manages Proximity Discovery, Shared Spatial Anchors, and session alignment.
/// Now includes LAN connection details within the colocation advertisement.
/// </summary>
public class ColocationManager : NetworkBehaviour
{
    [SerializeField] private AlignmentManager alignmentManager;
    [SerializeField] private LANDiscovery lanDiscovery;
    [SerializeField] private CustomNetworkManager networkManager;
    private Guid _sharedAnchorGroupId;

    public static bool ColocationSuccessful { get; private set; } = false;
    
    #region Server Methods
    
    /// <summary>
    /// Called when the server starts, initiating the colocation session.
    /// </summary>
    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("Colocation: Server started, preparing colocation session.");
        StartColocationSession();
    }

    /// <summary>
    /// Starts advertising the colocation session, including LAN connection details.
    /// </summary>
    private void StartColocationSession()
    {
        if (isServer)
        {
            Debug.Log("Colocation: Starting Advertisement...");
            AdvertiseColocationSession();
        }
    }

    /// <summary>
    /// Advertises the colocation session along with the LAN server URI.
    /// </summary>
    private async Task AdvertiseColocationSession()
    {
        try
        {
            string uri = lanDiscovery.GetLanServerUri();

            if (string.IsNullOrEmpty(uri))
            {
                Debug.LogError("Colocation: No valid LAN server URI found.");
                return;
            }
            
            Uri parsedUri = new Uri(uri);
            string ipAddress = parsedUri.Host; // Extracts "192.168.xxx.xxx"
            
            string advertisementMessage = $"SharedSpatialAnchorSession|{ipAddress}";
            var advertisementData = Encoding.UTF8.GetBytes(advertisementMessage);
            
            var startAdvertisementResult = await OVRColocationSession.StartAdvertisementAsync(advertisementData);
            if (startAdvertisementResult.Success)
            {
                _sharedAnchorGroupId = startAdvertisementResult.Value;
                Debug.Log($"Colocation: Advertisement started. UUID: {_sharedAnchorGroupId}, LAN: {ipAddress}");
                CreateAndShareAlignmentAnchor();
            }
            else
            {
                Debug.LogError($"Colocation: Advertisement failed with status: {startAdvertisementResult.Status}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Colocation: Error during advertisement: {e.Message}");
        }
    }
    
    /// <summary>
    /// Creates and shares an alignment anchor for spatial synchronization.
    /// </summary>
    private async void CreateAndShareAlignmentAnchor()
    {
        try
        {
            Debug.Log("Colocation: Creating alignment anchor...");
            var anchor = await CreateAnchor(Vector3.zero, Quaternion.identity);
            if (anchor == null)
            {
                Debug.LogError($"Colocation: Failed to create alignment anchor.");
            }

            if (!anchor.Localized)
            {
                Debug.LogError("Colocation: Anchor is not localized. Cannot proceed with sharing.");
                return;
            }
            
            var saveResult = await anchor.SaveAnchorAsync();
            if (!saveResult.Success)
            {
                Debug.LogError($"Colocation: Failed to save anchor. Error: {saveResult}");
                return;
            }
            
            Debug.Log($"Colocation: Alignment anchor saved successfully. UUID: {anchor.Uuid}");
          
            var shareResult = await OVRSpatialAnchor.ShareAsync(new List<OVRSpatialAnchor> { anchor }, _sharedAnchorGroupId);
            if (!shareResult.Success)
            {
                Debug.LogError($"Colocation: Failed to share alignment anchor. Error: {shareResult}");
                return;
            }
            Debug.Log("Colocation: Alignment anchor shared successfully.");
        }
        catch (Exception e)
        {
            Debug.LogError($"Colocation: Error during anchor creation and sharing: {e.Message}");
        }
    }
    
    /// <summary>
    /// Creates a new spatial anchor at a given position and rotation.
    /// </summary>
    private async Task<OVRSpatialAnchor> CreateAnchor(Vector3 position, Quaternion rotation)
    {
        try
        {
            var anchorGameObject = new GameObject("Alignment Anchor") { transform = { position = position, rotation = rotation } };
            var spatialAnchor = anchorGameObject.AddComponent<OVRSpatialAnchor>();
            while (!spatialAnchor.Created) await Task.Yield();
            Debug.Log($"Colocation: Anchor created successfully. UUID: {spatialAnchor.Uuid}");
            return spatialAnchor;
        }
        catch (Exception e)
        {
            Debug.LogError($"Colocation: Error during anchor creation: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Stops advertising the Colocation session.
    /// Can be called manually or when max players are reached.
    /// </summary>
    public async void StopColocationAdvertisement()
    {
        try
        {
            var stopResult = await OVRColocationSession.StopAdvertisementAsync();
            if (stopResult.Success)
            {
                Debug.Log("Colocation: Stopped Proximity Advertisement.");
            }
            else
            {
                Debug.LogWarning($"Colocation: Failed to stop advertisement. Status: {stopResult.Status}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Colocation: Error stopping advertisement: {e.Message}");
        }
    }
    
    #endregion

    #region Client Methods
    
    /// <summary>
    /// Called when the client starts, initiating colocation discovery.
    /// </summary>
    public override void OnStartClient()
    {
        base.OnStartClient();
        Debug.Log("Colocation: Client started, Searching for Colocation Session...");
        DiscoverNearbySession();
    }

    /// <summary>
    /// Starts colocation discovery with a fallback to LAN discovery if unsuccessful.//////////////////
    /// </summary>
    private async void DiscoverNearbySession()
    {
        try
        {
            OVRColocationSession.ColocationSessionDiscovered += OnColocationSessionDiscovered;
            var discoveryResult = await OVRColocationSession.StartDiscoveryAsync();
            if (!discoveryResult.Success)
            {
                Debug.LogError($"Colocation: Discovery failed with status: {discoveryResult.Status}");
                return;
            }
            
            Debug.Log("Colocation: Discovery started. Will fall back to LAN if no session found.");
            await Task.Delay(100000);
            if (_sharedAnchorGroupId == Guid.Empty)
            {
                Debug.LogWarning("Colocation: No session found, switching to LAN discovery.");
                lanDiscovery.StartClientDiscoveryWithFallback();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Colocation: Error during discovery: {e.Message}");
        }
    }
    
    /// <summary>
    /// Handles a discovered colocation session, extracting LAN details and synchronizing the anchor.
    /// </summary>
    private void OnColocationSessionDiscovered(OVRColocationSession.Data session)
    {
        Debug.Log("Colocation: Session discovered.");
        OVRColocationSession.ColocationSessionDiscovered -= OnColocationSessionDiscovered;

        string advertisementMessage = Encoding.UTF8.GetString(session.Metadata);
        string[] splitData = advertisementMessage.Split('|');

        if (splitData.Length < 2)
        {
            Debug.LogError("Colocation: Advertisement data is invalid.");
            return;
        }

        _sharedAnchorGroupId = session.AdvertisementUuid;
        string lanServerAddress = splitData[1];

        Debug.Log($"Colocation: Discovered session. UUID: {_sharedAnchorGroupId}, LAN: {lanServerAddress}");

        // Mark colocation as successful
        ColocationSuccessful = true;

        RequestAnchorSync(_sharedAnchorGroupId);
        networkManager.RequestLanConnection(lanServerAddress);
    }
    

    /// <summary>
    /// Stops Colocation Discovery
    /// </summary>
    public async void StopColocationDiscovery()
    {
        Debug.Log("Colocation: Stopping Colocation Discovery.");
        OVRColocationSession.ColocationSessionDiscovered -= OnColocationSessionDiscovered;

        var stopResult = await OVRColocationSession.StopDiscoveryAsync();
        if (stopResult.Success)
        {
            Debug.Log("Colocation: Successfully stopped Proximity Discovery.");
        }
        else
        {
            Debug.LogWarning($"Colocation: Failed to stop Proximity Discovery. Status: {stopResult.Status}");
        }
    }
    
    #endregion

    #region Anchor Synchronization

    /// <summary>
    /// Sends a command to request anchor synchronization.
    /// </summary>
    private void RequestAnchorSync(Guid groupUuid)
    {
        LoadAndAlignToAnchor(groupUuid);
    }

    /// <summary>
    /// Loads and aligns the user to the shared spatial anchor.
    /// </summary>  
    private async void LoadAndAlignToAnchor(Guid groupUuid)
    {
        try
        {
            Debug.Log($"Colocation: Loading anchors for group {groupUuid}...");

            var unboundAnchors = new List<OVRSpatialAnchor.UnboundAnchor>();
            var loadResult = await OVRSpatialAnchor.LoadUnboundSharedAnchorsAsync(groupUuid, unboundAnchors);

            if (!loadResult.Success || unboundAnchors.Count == 0)
            {
                Debug.LogError($"Colocation: Failed to load anchors. Success: {loadResult.Success}, Count: {unboundAnchors.Count}");
                return;
            }

            foreach (var unboundAnchor in unboundAnchors)
            {
                if (await unboundAnchor.LocalizeAsync())
                {
                    Debug.Log($"Colocation: Anchor localized successfully. UUID: {unboundAnchor.Uuid}");

                    var anchorGameObject = new GameObject($"Anchor_{unboundAnchor.Uuid}");
                    var spatialAnchor = anchorGameObject.AddComponent<OVRSpatialAnchor>();
                    unboundAnchor.BindTo(spatialAnchor);
                    
                    alignmentManager.AlignUserToAnchor(spatialAnchor);
                    return;
                }
                
                Debug.LogWarning($"Colocation: Failed to localize anchor: {unboundAnchor.Uuid}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Colocation: Error during anchor loading and alignment: {e.Message}");
        }
    }
    
    #endregion
}