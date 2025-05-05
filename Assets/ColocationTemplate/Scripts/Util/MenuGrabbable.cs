using System.Linq;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

public class MenuGrabbable : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private GameObject prefabToSpawn;
    [SerializeField] private float      spawnCooldown = 1.5f;
    
    [Header("Local References")]
    [SerializeField] private GrabMenuController   grabMenuController;
    [SerializeField] private GameObject           menuItemGO;
    [SerializeField] private GameObject           menuModel;
    [SerializeField] private HandGrabInteractable handGrabInteractable;
    [SerializeField] private Grabbable            grabbable;

    private HandGrabInteractor   localInteractor;      // hand that grabbed the tile

    private void Awake()
    {
        grabbable.WhenPointerEventRaised += OnMenuSelect;
    }

    private void OnDestroy()
    {
        grabbable.WhenPointerEventRaised -= OnMenuSelect;
    }

    private void OnMenuSelect(PointerEvent evt)
    {
        if (evt.Type != PointerEventType.Select) return;
        if (prefabToSpawn == null) return;
        if (grabbable.SelectingPointsCount == 0 || !handGrabInteractable.SelectingInteractors.Any()) return;

        grabMenuController.OnLocalObjectAssigned += HandleLocalObjectSpawned;
        
        localInteractor = handGrabInteractable.SelectingInteractors.FirstOrDefault();
        if (localInteractor == null)
        {
            Debug.LogWarning("[MenuGrabbable] LocalInteractor was null.");
            return;
        }

        localInteractor.ForceRelease();
        menuItemGO.SetActive(false);
        
        Vector3 spawnPos = menuModel.transform.position;
        Quaternion spawnRot = menuModel.transform.rotation.normalized;
        
        grabMenuController.RequestSpawnObject(
            prefabToSpawn.name,
            spawnPos,
            spawnRot
            );
    }
    
    private void HandleLocalObjectSpawned(GameObject localObject)
    {
        Debug.Log($"[MenuGrabbable] Received reference to newly spawned local object: {localObject.name}");
        grabMenuController.OnLocalObjectAssigned -= HandleLocalObjectSpawned;
        _ = TryGrabLoopAsync(localObject);
    }
    
    private async Task TryGrabLoopAsync(GameObject localObject)
    {
        float timeout = 2f;
        float elapsed = 0f;
        float retryDelay = 0.05f;

        while (elapsed < timeout)
        {
            if (localInteractor == null)
            {
                Debug.LogWarning("[MenuGrabbable] localInteractor was null during retry loop.");
                return;
            }

            var gntb = localObject.GetComponentInChildren<GrabNetTransformBridge>();
            var hgi = localObject.GetComponentInChildren<HandGrabInteractable>();

            if (gntb != null && hgi != null)
            {
                gntb.RequestForceGrab();
                localInteractor.ForceSelect(hgi, true);
                Debug.Log("[MenuGrabbable] Force grab succeeded.");
                StartCoroutine(GrabReset());
                return;
            }

            await Task.Delay((int)(retryDelay * 1000));
            elapsed += retryDelay;
        }

        Debug.LogWarning("[MenuGrabbable] Force grab failed after timeout.");
        menuItemGO.SetActive(true);
    }
    
    private IEnumerator GrabReset()
    {
        yield return new WaitForSeconds(spawnCooldown);
        grabMenuController.CanSpawn = true;
        menuItemGO.SetActive(true);
    }
}
