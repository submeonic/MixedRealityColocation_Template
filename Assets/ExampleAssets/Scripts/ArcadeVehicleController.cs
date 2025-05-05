using Mirror;
using UnityEngine;

public class ArcadeVehicleController : NetworkBehaviour
{
    public enum groundCheck { rayCast, sphereCaste };
    public enum MovementMode { Velocity, AngularVelocity };
    public MovementMode movementMode;
    public groundCheck GroundCheck;
    public LayerMask drivableSurface;

    public float MaxSpeed, accelaration, turn, gravity = 7f, downforce = 5f;
    [Tooltip("if true : can turn vehicle in air")]
    public bool AirControl = false;
    [Tooltip("if true : vehicle will drift instead of brake while holding space")]
    public bool kartLike = false;
    [Tooltip("turn more while drifting (while holding space) only if kart Like is true")]
    public float driftMultiplier = 1.5f;

    public Rigidbody rb, carBody;

    [HideInInspector]
    public RaycastHit hit;
    public AnimationCurve frictionCurve;
    public AnimationCurve turnCurve;
    public PhysicsMaterial frictionMaterial;
    [Header("Visuals")]
    public Transform BodyMesh;
    public Transform[] FrontWheels = new Transform[2];
    public Transform[] RearWheels = new Transform[2];
    [HideInInspector]
    public Vector3 carVelocity;

    [Range(0, 10)]
    public float BodyTilt;
    [Header("Audio settings")]
    public AudioSource engineSound;
    [Range(0, 1)]
    public float minPitch;
    [Range(1, 3)]
    public float MaxPitch;
    public AudioSource SkidSound;

    [HideInInspector]
    public float skidWidth;


    private float radius, steeringInput, accelerationInput, brakeInput;
    private Vector3 origin;

    private GrabNetController grabNetController;
    
    [SyncVar] private NetworkIdentity driver; 

    private float inputLerpTimer = 0f;

    private float startS;
    private float startT;
    private float startB;
    
    private float targetS;
    private float targetT;
    private float targetB;

    private float syncTimer = 0;
    
    private Vector3 queuedSpherePos;
    private Vector3 queuedBodyPos;
    private Quaternion queuedBodyRot;
    private Vector3 queuedVel;
    private bool hasQueuedUpdate = false;
    
    private float transformLerpTimer = 0f;
    
    private Vector3 startSpherePos;
    private Vector3 targetSpherePos;
    
    private Vector3 startBodyPos;
    private Vector3 targetBodyPos;
    
    private Quaternion startBodyRot;
    private Quaternion targetBodyRot;
    
    [HideInInspector]
    private Vector3 ref_velocity;

    private Vector3 startVel;
    private Vector3 targetVel;
    
    [SerializeField] private float syncInt = 0.1f;
    
    private void Start()
    {
        radius = rb.GetComponent<SphereCollider>().radius;
        grabNetController = GetComponent<GrabNetController>();
        if (movementMode == MovementMode.AngularVelocity)
        {
            Physics.defaultMaxAngularSpeed = 100;
        }

        syncInterval = syncInt;
    }
    
    public void ServerSetDriver(NetworkIdentity id)    // called from CmdSetDriver
    {
        driver = id;
    }
    
    private bool IAmDriver
    {
        get
        {
            var myIdentity = NetworkClient.connection?.identity;

            // No identity or driver info = not the driver
            if (myIdentity == null || driver == null)
                return false;

            // Driver hasn’t synced yet — fallback to optimistic match
            if (driver.netId == 0)
                return true;

            return myIdentity == driver;
        }
    }

    public void ClientProvideInputs(float s, float t, float b)
    {
        if (!IAmDriver) return;
        steeringInput = s;
        accelerationInput = t;
        brakeInput = b;
    }
    
    public void ClientSyncInputs(float s, float t, float b)
    {
        CmdSyncInputs(s,t,b);
    }

    [Command(requiresAuthority = false)]
    private void CmdSyncInputs(float s, float t, float b, NetworkConnectionToClient sender = null)
    {
        RpcSyncInputs(s, t, b);
    }

    [ClientRpc]
    private void RpcSyncInputs(float s, float t, float b)
    {
        if (IAmDriver) return;  
        
        startS = steeringInput;
        startT = accelerationInput;
        startB = brakeInput;

        targetS = s;
        targetT = t;
        targetB = b;

        inputLerpTimer = 0f;
    }

    [Command(requiresAuthority = false)]
    private void CmdSyncTransform(Vector3 spherePos, Vector3 bodyPos, Quaternion bodyRot, Vector3 vel, NetworkConnectionToClient sender = null)
    {
        // everyone except connected player
        RpcSyncTransform(spherePos, bodyPos, bodyRot, vel);
    }

    [ClientRpc]
    private void RpcSyncTransform(Vector3 spherePos, Vector3 bodyPos, Quaternion bodyRot, Vector3 vel)
    {
        if (IAmDriver) return;
        
        startSpherePos = rb.position;
        startBodyPos = carBody.position;
        startBodyRot = carBody.rotation;
        startVel  = ref_velocity;
        
        targetSpherePos = spherePos;
        targetBodyPos = bodyPos;
        targetBodyRot = Quaternion.Normalize(bodyRot);
        targetVel = vel;
        
        transformLerpTimer = 0f;
    }
    
    private void Update()
    {
        if (grabNetController.isGrabbed)
        {
            steeringInput = 0f;
            accelerationInput = 0f;
            brakeInput = 1f;
            
            rb.useGravity = false;
            rb.isKinematic = true;
            carBody.isKinematic = true;
            
            rb.position = carBody.position + new Vector3(0, -0.00518389f, 0);
            return; 
        }

        if (!IAmDriver)
        {
            rb.useGravity = true;
            rb.isKinematic = false;
            carBody.isKinematic = false;
            
            float i = Mathf.Clamp01((inputLerpTimer += Time.deltaTime) / syncInt);
            float t = Mathf.Clamp01((transformLerpTimer += Time.deltaTime) / syncInt);

            steeringInput     = Mathf.Lerp(startS, targetS, i);
            accelerationInput = Mathf.Lerp(startT, targetT, i);
            brakeInput        = Mathf.Lerp(startB, targetB, i);

            rb.position = Vector3.Lerp(startSpherePos, targetSpherePos, t);
            carBody.position = Vector3.Lerp(startBodyPos, targetBodyPos, t);
            carBody.rotation = Quaternion.Normalize(Quaternion.Slerp(startBodyRot, targetBodyRot, t));
            ref_velocity = Vector3.Lerp(startVel, targetVel, t);
        }
        else
        {
            rb.useGravity = true;
            rb.isKinematic = false;
            carBody.isKinematic = false;
        }

        Visuals();
        AudioManager();
    }

    public void AudioManager()
    {
        Vector3 vel;
        if (IAmDriver)
        {
            vel = carVelocity;
        }
        else
        {
            vel = ref_velocity;
        }
        engineSound.pitch = Mathf.Lerp(minPitch, MaxPitch, Mathf.Abs(vel.z) / MaxSpeed);
        if (Mathf.Abs(vel.x) > 10 && grounded())
        {
            SkidSound.mute = false;
        }
        else
        {
            SkidSound.mute = true;
        }
    }
    
    void FixedUpdate()
    {
        if (grabNetController.isGrabbed) return;
        if (!IAmDriver) return;
        
        carVelocity = carBody.transform.InverseTransformDirection(carBody.linearVelocity);

        if (Mathf.Abs(carVelocity.x) > 0)
        {
            //changes friction according to sideways speed of car
            frictionMaterial.dynamicFriction = frictionCurve.Evaluate(Mathf.Abs(carVelocity.x / 100));
        }
        
        if (grounded())
        {
            //turnlogic
            float sign = Mathf.Sign(carVelocity.z);
            float TurnMultiplyer = turnCurve.Evaluate(carVelocity.magnitude / MaxSpeed);
            if (kartLike && brakeInput > 0.1f) { TurnMultiplyer *= driftMultiplier; } //turn more if drifting


            if (accelerationInput > 0.1f || carVelocity.z > 1)
            {
                carBody.AddTorque(Vector3.up * steeringInput * sign * turn * 100 * TurnMultiplyer);
            }
            else if (accelerationInput < -0.1f || carVelocity.z < -1)
            {
                carBody.AddTorque(Vector3.up * steeringInput * sign * turn * 100 * TurnMultiplyer);
            }
            

            // mormal brakelogic
            if (!kartLike)
            {
                if (brakeInput > 0.1f)
                {
                    rb.constraints = RigidbodyConstraints.FreezeRotationX;
                }
                else
                {
                    rb.constraints = RigidbodyConstraints.None;
                }
            }

            //accelaration logic

            if (movementMode == MovementMode.AngularVelocity)
            {
                if (Mathf.Abs(accelerationInput) > 0.1f && brakeInput < 0.1f && !kartLike)
                {
                    rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, carBody.transform.right * accelerationInput * MaxSpeed / radius, accelaration * Time.deltaTime);
                }
                else if (Mathf.Abs(accelerationInput) > 0.1f && kartLike)
                {
                    rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, carBody.transform.right * accelerationInput * MaxSpeed / radius, accelaration * Time.deltaTime);
                }
            }
            else if (movementMode == MovementMode.Velocity)
            {
                if (Mathf.Abs(accelerationInput) > 0.1f && brakeInput < 0.1f && !kartLike)
                {
                    rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, carBody.transform.forward * accelerationInput * MaxSpeed, accelaration / 10 * Time.deltaTime);
                }
                else if (Mathf.Abs(accelerationInput) > 0.1f && kartLike)
                {
                    rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, carBody.transform.forward * accelerationInput * MaxSpeed, accelaration / 10 * Time.deltaTime);
                }
            }

            // down force
            rb.AddForce(-transform.up * downforce * rb.mass);

            //body tilt
            carBody.MoveRotation(
                Quaternion.Normalize(
                    Quaternion.Slerp(
                        carBody.rotation,
                        Quaternion.FromToRotation(carBody.transform.up, hit.normal) * carBody.transform.rotation,
                        0.12f
                    )
                )
            );
        }
        else
        {
            if (AirControl)
            {
                //turnlogic
                float TurnMultiplyer = turnCurve.Evaluate(carVelocity.magnitude / MaxSpeed);

                carBody.AddTorque(Vector3.up * steeringInput * turn * 100 * TurnMultiplyer);
            }
            carBody.MoveRotation(
                Quaternion.Normalize(
                    Quaternion.Slerp(
                        carBody.rotation,
                        Quaternion.FromToRotation(carBody.transform.up, hit.normal) * carBody.transform.rotation,
                        0.02f
                    )
                )
            );
        }
        
        //sync queue
        queuedSpherePos = rb.position;
        queuedBodyPos = carBody.position;
        queuedBodyRot = carBody.rotation;
        queuedVel = carVelocity;
        hasQueuedUpdate = true;
        
        if (IAmDriver && hasQueuedUpdate &&
            (syncTimer += Time.fixedDeltaTime) >= syncInterval)
        {
            syncTimer = 0f; hasQueuedUpdate = false;
            CmdSyncTransform(queuedSpherePos, queuedBodyPos, queuedBodyRot, queuedVel);
        }
    }
    
    
    public void Visuals()
    {
        Vector3 vel;
        if (IAmDriver)
        {
            vel = carVelocity;
        }
        else
        {
            vel = ref_velocity;
        }
        //tires
        foreach (Transform FW in FrontWheels)
        {
            FW.localRotation = Quaternion.Slerp(FW.localRotation, Quaternion.Euler(FW.localRotation.eulerAngles.x,
                               30 * steeringInput, FW.localRotation.eulerAngles.z), 0.7f * Time.deltaTime / Time.fixedDeltaTime);
            FW.GetChild(0).localRotation = rb.transform.localRotation;
        }
        RearWheels[0].localRotation = rb.transform.localRotation;
        RearWheels[1].localRotation = rb.transform.localRotation;

        //Body
        if (vel.z > 1)
        {
            BodyMesh.localRotation = Quaternion.Slerp(BodyMesh.localRotation, Quaternion.Euler(Mathf.Lerp(0, -5, vel.z / MaxSpeed),
                               BodyMesh.localRotation.eulerAngles.y, BodyTilt * steeringInput), 0.4f * Time.deltaTime / Time.fixedDeltaTime);
        }
        else
        {
            BodyMesh.localRotation = Quaternion.Slerp(BodyMesh.localRotation, Quaternion.Euler(0, 0, 0), 0.4f * Time.deltaTime / Time.fixedDeltaTime);
        }


        if (kartLike)
        {
            if (brakeInput > 0.1f)
            {
                BodyMesh.parent.localRotation = Quaternion.Slerp(BodyMesh.parent.localRotation,
                Quaternion.Euler(0, 45 * steeringInput * Mathf.Sign(vel.z), 0),
                0.1f * Time.deltaTime / Time.fixedDeltaTime);
            }
            else
            {
                BodyMesh.parent.localRotation = Quaternion.Slerp(BodyMesh.parent.localRotation,
                Quaternion.Euler(0, 0, 0),
                0.1f * Time.deltaTime / Time.fixedDeltaTime);
            }

        }

    }

    public bool grounded() //checks for if vehicle is grounded or not
    {
        origin = rb.position + rb.GetComponent<SphereCollider>().radius * Vector3.up;
        var direction = -transform.up;
        var maxdistance = rb.GetComponent<SphereCollider>().radius + 0.2f;

        if (GroundCheck == groundCheck.rayCast)
        {
            if (Physics.Raycast(rb.position, Vector3.down, out hit, maxdistance, drivableSurface))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        else if (GroundCheck == groundCheck.sphereCaste)
        {
            if (Physics.SphereCast(origin, radius + 0.1f, direction, out hit, maxdistance, drivableSurface))
            {
                return true;

            }
            else
            {
                return false;
            }
        }
        else { return false; }
    }

    private void OnDrawGizmos()
    {
        //debug gizmos
        radius = rb.GetComponent<SphereCollider>().radius;
        float width = 0.02f;
        if (!Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(rb.transform.position + ((radius + width) * Vector3.down), new Vector3(2 * radius, 2 * width, 4 * radius));
            if (GetComponent<BoxCollider>())
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(transform.position, GetComponent<BoxCollider>().size);
            }

        }
    }
}