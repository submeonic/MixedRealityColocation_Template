using System.Collections;
using Mirror;
using UnityEngine;

/// <summary>
/// Custom Network Manager that integrates LAN Discovery and Colocation.
/// Handles player connections, LAN matchmaking, and proximity-based discovery.
/// Ensures proper network cleanup when stopping the server.
/// </summary>
public class CustomNetworkManager : NetworkManager
{
    [SerializeField] private LANDiscovery lanDiscovery;
    [SerializeField] private ColocationManager colocationManager;

    #region Server Methods

    /// <summary>
    /// Called when the server starts.
    /// </summary>
    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("CustomNetworkManager: Server started.");
        lanDiscovery.StartHostAdvertisement();
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        Debug.Log("CustomNetworkManager: Client started!");
        // If you have a reference to ColocationManager in the scene,
        // you can call a method on it here.
        colocationManager.OnStartClient();
    }

    /// <summary>
    /// Called when a new player joins the session.
    /// Ensures alignment before allowing networked gameplay.
    /// </summary>
    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        base.OnServerAddPlayer(conn);
        Debug.Log($"CustomNetworkManager: Player joined. Total players: {numPlayers}/{maxConnections}");

        if (numPlayers >= maxConnections)
        {
            Debug.Log("CustomNetworkManager: Max connections reached, stopping Proximity Advertisement.");
            colocationManager.StopColocationAdvertisement();
        }
    }

    /// <summary>
    /// Public method to request LAN connection.
    /// Ensures client alignment before connecting.
    /// </summary>
    public void RequestLanConnection(string serverAddress)
    {
        if (NetworkClient.active)
        {
            Debug.Log("CustomNetworkManager: Client already connected.");
            return;
        }

        Debug.Log("CustomNetworkManager: Requesting LAN connection...");
        StartCoroutine(EnsureAlignmentBeforeConnecting(serverAddress));
    }

    private IEnumerator EnsureAlignmentBeforeConnecting(string serverAddress)
    {
        Debug.Log("Waiting for Colocation to succeed...");
        while (!ColocationManager.ColocationSuccessful)
        {
            yield return null;
        }

        Debug.Log("CustomNetworkManager: Alignment complete, connecting to LAN server...");
        ProcessRequestLanConnection(serverAddress);
    }

    /// <summary>
    /// Command to set the network address and start client connection.
    /// </summary>
    private void ProcessRequestLanConnection(string serverAddress)
    {
        Debug.Log($"CustomNetworkManager: Setting network address to {serverAddress}");
        networkAddress = serverAddress;
        StartClient();
    }

    #endregion
}
