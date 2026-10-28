using UnityEngine;

// Put this on each player capsule (a child of LogRoot).
// Standing: the player stays on the log, leans and bobs when pushing, and turns red as strain builds.
// Falling: the player is thrown off the side, lands in the water and floats.
// Respawn: the player climbs back to the exact spot on the log.
public class PlayerOnLog : MonoBehaviour
{
    public enum Side { Left, Right }

    [SerializeField] Side side;

    [Header("Walking look")]
    [SerializeField] float leanAngle = 12f;
    [SerializeField] float bobHeight = 0.05f;
    [SerializeField] float bobSpeed = 9f;

    [Header("Fall")]
    [SerializeField] float sideLaunchSpeed = 3f;
    [SerializeField] float upLaunchSpeed = 2.5f;
    [SerializeField] float tumble = 2f;
    [SerializeField] float waterDrag = 2.5f;

    LogMovement movement;
    LogBalance balance;
    LogBuoyancy buoyancy;
    Rigidbody logBody;

    Transform originalParent;
    Vector3 baseLocalPosition;
    Quaternion baseLocalRotation;
    Vector3 localAxis;

    Collider[] colliders;
    Material material;
    string colorProperty;
    Color baseColor;
    float halfHeight = 1f;

    Rigidbody fallBody;
    bool fallen;
    float walkPhase;

    void Awake()
    {
        movement = GetComponentInParent<LogMovement>();
        balance = GetComponentInParent<LogBalance>();
        buoyancy = GetComponentInParent<LogBuoyancy>();

        if (movement == null || balance == null)
        {
            Debug.LogError("PlayerOnLog: put this player under LogRoot (the object with LogMovement and LogBalance).", this);
            enabled = false;
            return;
        }

        logBody = movement.GetComponent<Rigidbody>();

        originalParent = transform.parent;
        baseLocalPosition = transform.localPosition;
        baseLocalRotation = transform.localRotation;
        localAxis = (movement.RightDrive.localPosition - movement.LeftDrive.localPosition).normalized;

        // While standing, the player must not add physics to the log or fall on its own.
        colliders = GetComponentsInChildren<Collider>();
        foreach (Collider c in colliders) c.enabled = false;
        Rigidbody own = GetComponent<Rigidbody>();
        if (own != null) Destroy(own);

        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            halfHeight = Mathf.Max(rend.bounds.extents.y, 0.1f);
            material = rend.material;
            colorProperty = material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
            baseColor = material.GetColor(colorProperty);
        }
    }

    void OnEnable()
    {
        if (balance == null) return;
        balance.PlayerFell += OnPlayerFell;
        balance.Respawned += OnRespawned;
    }

    void OnDisable()
    {
        if (balance == null) return;
        balance.PlayerFell -= OnPlayerFell;
        balance.Respawned -= OnRespawned;
    }

    void OnPlayerFell(int fellSide)
    {
        if (fellSide == (int)side) Fall();
    }

    void OnRespawned()
    {
        ReturnToLog();
    }

    void Update()
    {
        if (fallen) return;

        float input = side == Side.Left ? movement.LeftInput : movement.RightInput;
        float strain = side == Side.Left ? balance.LeftStrain : balance.RightStrain;

        // Lean into the push and bob like stepping.
        walkPhase += Mathf.Abs(input) * bobSpeed * Time.deltaTime;
        float bob = Mathf.Abs(Mathf.Sin(walkPhase)) * bobHeight * Mathf.Abs(input);
        transform.localPosition = baseLocalPosition + Vector3.up * bob;
        transform.localRotation = Quaternion.AngleAxis(leanAngle * input, localAxis) * baseLocalRotation;

        // Turn red as strain builds.
        if (material != null)
            material.SetColor(colorProperty, Color.Lerp(baseColor, Color.red, strain));
    }

    void Fall()
    {
        if (fallen) return;
        fallen = true;

        Vector3 axisWorld = movement.RightDrive.position - movement.LeftDrive.position;
        axisWorld.y = 0f;
        axisWorld.Normalize();
        Vector3 outward = side == Side.Left ? -axisWorld : axisWorld;
        Vector3 inherited = logBody.GetPointVelocity(transform.position);

        // Turn on the player's own colliders but let them pass through the log.
        foreach (Collider c in colliders) c.enabled = true;
        foreach (Collider logCollider in logBody.GetComponents<Collider>())
            foreach (Collider c in colliders)
                Physics.IgnoreCollision(c, logCollider, true);

        transform.SetParent(null, true);
        fallBody = gameObject.AddComponent<Rigidbody>();
        fallBody.interpolation = RigidbodyInterpolation.Interpolate;
        fallBody.linearVelocity = inherited + outward * sideLaunchSpeed + Vector3.up * upLaunchSpeed;
        fallBody.angularVelocity = Random.insideUnitSphere * tumble;
    }

    void FixedUpdate()
    {
        if (!fallen || fallBody == null) return;

        // Simple water for the fallen player: floats up and slows down once in the water.
        if (buoyancy != null && buoyancy.TryGetSurfaceHeight(fallBody.position, out float surfaceY))
        {
            float submerged = Mathf.Clamp01((surfaceY - (fallBody.position.y - halfHeight)) / (2f * halfHeight));
            if (submerged > 0f)
            {
                fallBody.AddForce(Vector3.up * fallBody.mass * Physics.gravity.magnitude * submerged * 1.6f);
                fallBody.linearDamping = waterDrag;
                return;
            }
        }
        fallBody.linearDamping = 0f;
    }

    void ReturnToLog()
    {
        if (!fallen) return;
        fallen = false;

        if (fallBody != null)
        {
            fallBody.isKinematic = true;
            Destroy(fallBody);
            fallBody = null;
        }
        foreach (Collider c in colliders) c.enabled = false;

        transform.SetParent(originalParent, false);
        transform.localPosition = baseLocalPosition;
        transform.localRotation = baseLocalRotation;
    }
}