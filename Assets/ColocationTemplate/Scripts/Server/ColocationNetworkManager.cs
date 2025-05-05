using System;
using System.Collections;
using Mirror;
using UnityEngine;

/// <summary>
/// Network-manager wrapper that combines Mirror networking with spatial-anchor
/// colocation.  The host advertises its URI via <see cref="LANDiscovery"/>.
/// A joining client waits until the shared anchor is localised, then connects
/// to the host address supplied by either anchor-metadata or LAN discovery.
/// </summary>
[AddComponentMenu("Networking/Colocation Network Manager")]
public class ColocationNetworkManager : NetworkManager
{
    /* ────────────────────────── inspector refs ─────────────────────────── */
    [Header("Colocation")]
    [SerializeField] private ColocationManager colocationManager;

    /* ──────────────────────────  SERVER HOOKS  ─────────────────────────── */

    public override void OnStartServer()
    {
        base.OnStartServer();

        Debug.Log("ColocationNetworkManager: Server started.");
        // ② kick off anchor-advertisement + share
        colocationManager.StartColocationSession();
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        base.OnServerAddPlayer(conn);
        Debug.Log($"ColocationNetworkManager: Player added ({numPlayers}/{maxConnections})");

        if (numPlayers >= maxConnections)
        {
            Debug.Log("ColocationNetworkManager: Max players reached → stop advertising.");
            colocationManager.StopColocationAdvertisement();
        }
    }
    
    public string GetLanServerUri()
    {
        return transport?.ServerUri()?.ToString() ?? string.Empty;
    }

    /* ───────────────────────────  CLIENT HOOKS  ────────────────────────── */

    public override void OnStartClient()
    {
        base.OnStartClient();
        Debug.Log("ColocationNetworkManager: Client started.");
    }

    /* ────────────────── Connection helper called by ColocationManager ───── */

    /// <summary>
    /// Called after ColocationManager has discovered the server address
    /// </summary>
    public void RequestLanConnection(string serverAddress)
    {
        if (NetworkClient.isConnected)
        {
            Debug.Log("ColocationNetworkManager: already connected → abort.");
            return;
        }

        // stale client running with the wrong IP?  stop it first
        if (NetworkClient.active)
        {
            Debug.Log("ColocationNetworkManager: restarting stale client.");
            StopClient();
        }
        
        Debug.Log("ColocationNetworkManager: Requesting LAN connection...");
        StartCoroutine(EnsureAlignmentBeforeConnecting(serverAddress));
    }

    /* ──────────────────────── private implementation ───────────────────── */

    private IEnumerator EnsureAlignmentBeforeConnecting(string serverAddress)
    {
        Debug.Log("ColocationNetworkManager: waiting for anchor alignment…");
        while (!ColocationManager.ColocationSuccessful)
            yield return null;

        Debug.Log($"ColocationNetworkManager: alignment done → connecting to {serverAddress}");
        ProcessRequestLanConnection(serverAddress);
    }

    private void ProcessRequestLanConnection(string serverAddress)
    {
        Debug.Log($"ColocationNetworkManager: Setting network address to {serverAddress}");
        networkAddress = serverAddress;
        StartClient();
    }
}
