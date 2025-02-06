using Mirror;
using Mirror.Discovery;
using UnityEngine;
using UnityEngine.Events;
using System.Net;
using System.Threading.Tasks;

/// <summary>
/// Handles LAN-based server discovery for peer-to-peer connections.
/// Integrated with ColocationManager to prioritize spatial synchronization first.
/// </summary>
public class LANDiscovery : NetworkDiscoveryBase<LANDiscovery.LanRequest, LANDiscovery.LanResponse>
{
    public UnityEvent<string> onServerFound = new();
    private bool isSearching = false;
    
    #region Data Structures

    /// <summary>
    /// Structure for a LAN discovery request (sent by clients).
    /// </summary>
    [System.Serializable]
    public class LanRequest : NetworkMessage {}

    /// <summary>
    /// Structure for a LAN discovery response (sent by servers).
    /// </summary>
    [System.Serializable]
    public class LanResponse : NetworkMessage
    {
        public string serverUri;
    }

    #endregion

    #region Discovery Methods

    /// <summary>
    /// Processes LAN discovery requests sent by clients.
    /// </summary>
    protected override LanResponse ProcessRequest(LanRequest request, IPEndPoint endpoint)
    {
        CustomNetworkManager networkManager = GetComponent<CustomNetworkManager>();

        if (networkManager == null || networkManager.transport == null)
        {
            Debug.LogError("LANDiscovery: No valid NetworkManager or Transport found.");
            return new LanResponse { serverUri = "INVALID" }; // Avoid returning null
        }

        return new LanResponse
        {
            serverUri = networkManager.transport.ServerUri().ToString()
        };
    }

    /// <summary>
    /// Processes LAN discovery responses and connects to the first available server.
    /// </summary>
    protected override void ProcessResponse(LanResponse response, IPEndPoint endpoint)
    {
        if (isSearching)
        {
            Debug.Log($"LANDiscovery: Found LAN Server at {endpoint.Address}");
            onServerFound.Invoke(endpoint.Address.ToString());
            StopDiscovery(); // Stop searching once a server is found
        }
    }

    /// <summary>
    /// Starts LAN discovery but only if colocation is unavailable.
    /// </summary>
    public async void StartClientDiscoveryWithFallback()
    {
        if (isSearching)
        {
            Debug.Log("LANDiscovery: Already searching for LAN servers.");
            return;
        }

        isSearching = true;
        Debug.Log("LANDiscovery: Waiting for colocation before starting LAN discovery...");

        if (!ColocationManager.ColocationSuccessful)
        {
            Debug.LogWarning("LANDiscovery: No colocation session found, switching to LAN discovery.");
            StartDiscovery();
        }
        else
        {
            Debug.Log("LANDiscovery: Colocation succeeded, skipping LAN discovery.");
        }
    }

    /// <summary>
    /// Starts advertising the host's LAN server.
    /// </summary>
    public void StartHostAdvertisement()
    {
        AdvertiseServer();
    }

    /// <summary>
    /// Stops LAN Discovery when a connection is established.
    /// </summary>
    public new void StopDiscovery()
    {
        Debug.Log("LANDiscovery: Stopping LAN Discovery...");
        base.StopDiscovery();
        isSearching = false;
    }

    /// <summary>
    /// Returns the LAN Server URI for integration with ColocationManager.
    /// </summary>
    public string GetLanServerUri()
    {
        CustomNetworkManager networkManager = GetComponent<CustomNetworkManager>();
        return networkManager?.transport?.ServerUri()?.ToString() ?? string.Empty;
    }

    #endregion
}
