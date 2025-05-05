using UnityEngine;

public class LocalReferenceManager : MonoBehaviour
{
    // Static instance for singleton access
    private static LocalReferenceManager _instance;

    // Public static getter for the instance
    public static LocalReferenceManager Instance
    {
        get
        {
            if (_instance == null)
            {
                // Try to find an instance in the scene if one hasn't been set yet.
                _instance = FindObjectOfType<LocalReferenceManager>();
                if (_instance == null)
                {
                    Debug.LogError("LocalReferenceManager not found in the scene. " +
                                   "Please add one to your scene.");
                }
            }
            return _instance;
        }
    }

    // Serialized fields for OVR references. Assign these in the inspector.
    [SerializeField] 
    private OVRSkeleton ovrSkeleton;

    [SerializeField] 
    private GameObject trackingSpace;
    
    [SerializeField] 
    private ColocationManager colocationManager;

    // Public getters for other scripts to access the references.
    public OVRSkeleton OvrSkeleton
    {
        get { return ovrSkeleton; }
    }

    public GameObject TrackingSpace
    {
        get { return trackingSpace; }
    }
    
    public ColocationManager ColocationManager
    {
        get { return colocationManager; }
    }

    // Optional: Make this GameObject persist across scenes.
    private void Awake()
    {
        // Ensure that there's only one instance of this manager.
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        //DontDestroyOnLoad(gameObject); // Comment this out if persistence across scenes is not required.
    }
}