using System.Linq;
using Mirror;
using UnityEngine;

public class GrabMenuController : NetworkBehaviour
{
    private GameObject _activeCar;
    public GameObject activeCar => _activeCar;
    private bool _canSpawn = true;

    public bool CanSpawn
    {
        set => _canSpawn = value;
    }
    
    public void RequestSpawnObject(string prefabName, Vector3 position, Quaternion rotation)
    {
        CmdSpawnObject(prefabName, position, rotation);
        _canSpawn = false;
    }
    
    [Command]
    private void CmdSpawnObject(string prefabName,Vector3 position, Quaternion rotation, NetworkConnectionToClient sender = null)
    {
        var prefab = NetworkManager.singleton.spawnPrefabs.FirstOrDefault(p => p.name == prefabName);
        if (prefab == null)
        {
            Debug.LogError($"MenuGrabbable: prefab '{prefabName}' not registered.");
            return;
        }
        // Instantiate and spawn new object with server authority
        GameObject obj = Instantiate(prefab, position, rotation);
        NetworkServer.Spawn(obj);

        // Notify sender with netId
        TargetReceiveObject(sender, obj.GetComponent<NetworkIdentity>().netId);
    }
    
    // Called on the client that requested the object — gives netId to look up local object
    [TargetRpc]
    private void TargetReceiveObject(NetworkConnection target, uint netId)
    {
        if (NetworkClient.spawned.TryGetValue(netId, out NetworkIdentity identity))
        {
            GameObject localObj = identity.gameObject;
            Debug.Log($"[Client] Received reference to spawned object: {localObj.name}");

            // Do something with it — assign locally, show UI, etc.
            OnLocalObjectAssigned?.Invoke(localObj);
        }
        else
        {
            Debug.LogWarning($"[Client] Could not find spawned car with netId {netId}");
        }
    }
    
    // Called by client — requests a new car to be spawned and assigned
    public void RequestSpawnCar(string prefabName, Vector3 position, Quaternion rotation)
    {
        if (_canSpawn)
        {
            CmdSetActiveCar(prefabName, position, rotation);
            _canSpawn = false;
        }
    }

    [Command]
    private void CmdSetActiveCar(string prefabName,Vector3 position, Quaternion rotation, NetworkConnectionToClient sender = null)
    {
        // Destroy existing car if one is assigned
        if (_activeCar != null)
        {
            NetworkServer.Destroy(_activeCar);
            _activeCar = null;
        }
        
        var prefab = NetworkManager.singleton.spawnPrefabs.FirstOrDefault(p => p.name == prefabName);
        if (prefab == null)
        {
            Debug.LogError($"MenuGrabbable: prefab '{prefabName}' not registered.");
            return;
        }
        // Instantiate and spawn new car with server authority
        GameObject car = Instantiate(prefab, position, rotation);
        NetworkServer.Spawn(car);

        var carController = car.GetComponent<ArcadeVehicleController>();
        if (carController != null)
        {
            carController.ServerSetDriver(sender.identity);
        }
        
        // Assign to server-side reference
        _activeCar = car;

        // Notify sender with netId
        TargetReceiveActiveCar(sender, car.GetComponent<NetworkIdentity>().netId);
    }

    // Called on the client that requested the car — gives netId to look up local object
    [TargetRpc]
    private void TargetReceiveActiveCar(NetworkConnection target, uint netId)
    {
        if (NetworkClient.spawned.TryGetValue(netId, out NetworkIdentity identity))
        {
            GameObject localCar = identity.gameObject;
            Debug.Log($"[Client] Received reference to new car: {localCar.name}");

            // Do something with it — assign locally, show UI, etc.
            OnLocalActiveCarAssigned?.Invoke(localCar);
        }
        else
        {
            Debug.LogWarning($"[Client] Could not find spawned car with netId {netId}");
        }
    }

    // Event for immediate response on client
    public event System.Action<GameObject> OnLocalActiveCarAssigned;
    public event System.Action<GameObject> OnLocalObjectAssigned;
}