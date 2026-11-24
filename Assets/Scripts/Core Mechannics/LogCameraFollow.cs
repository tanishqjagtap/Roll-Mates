using UnityEngine;

// Put this on the Main Camera.
// Follows the log smoothly from behind, swings around slowly when the log turns,
// ignores the log's small bobbing, looks a little ahead of where it is going,
// and snaps instantly to the new spot when the log respawns.
[RequireComponent(typeof(Camera))]
public class LogCameraFollow : MonoBehaviour
{
    [Header("Target (finds the log automatically if left empty)")]
    [SerializeField] Transform target;

    [Header("Framing")]
    [SerializeField] float distance = 16f;
    [SerializeField] float height = 8f;
    [Tooltip("Camera aims this far above the log's center.")]
    [SerializeField] float lookAtHeight = 1f;

    [Header("Smoothing")]
    [Tooltip("Higher = the camera trails further behind the log.")]
    [SerializeField] float positionSmoothTime = 0.3f;
    [Tooltip("Slower than sideways so the log's bobbing doesn't shake the camera.")]
    [SerializeField] float verticalSmoothTime = 0.8f;
    [SerializeField] bool followHeading = true;
    [Tooltip("How slowly the camera swings around behind the log when it turns.")]
    [SerializeField] float headingSmoothTime = 0.9f;

    [Header("Look Ahead")]
    [SerializeField] float lookAheadFactor = 0.4f;
    [SerializeField] float maxLookAhead = 3f;

    [Header("Speed Feel")]
    [Tooltip("Extra field of view (degrees) at full speed. 0 = off.")]
    [SerializeField] float fovBoost = 5f;
    [SerializeField] float fovFullSpeed = 4f;
    [SerializeField] float fovSmoothing = 3f;

    Camera cam;
    LogMovement movement;
    LogBalance balance;
    Rigidbody body;

    float baseFov;
    float fixedYaw;

    Vector2 focusXZ;
    Vector2 focusVelocityXZ;
    float focusY;
    float focusVelocityY;
    float headingYaw;
    float headingVelocity;

    void Awake()
    {
        cam = GetComponent<Camera>();
        baseFov = cam.fieldOfView;
        fixedYaw = transform.eulerAngles.y;

        if (target == null)
        {
            movement = FindFirstObjectByType<LogMovement>();
            if (movement != null) target = movement.transform;
        }
        else
        {
            movement = target.GetComponentInParent<LogMovement>();
        }

        if (target == null)
        {
            Debug.LogError("LogCameraFollow: no target set and no LogMovement found in the scene.", this);
            enabled = false;
            return;
        }

        body = target.GetComponentInParent<Rigidbody>();
        balance = target.GetComponentInParent<LogBalance>();
    }

    void OnEnable()
    {
        if (balance != null) balance.Respawned += Snap;
    }

    void OnDisable()
    {
        if (balance != null) balance.Respawned -= Snap;
    }

    void Start()
    {
        Snap();
    }

    // Jump straight to the right spot with no smoothing (start of game, respawn).
    public void Snap()
    {
        if (target == null) return;

        Vector3 goal = FocusGoal();
        focusXZ = new Vector2(goal.x, goal.z);
        focusY = goal.y;
        focusVelocityXZ = Vector2.zero;
        focusVelocityY = 0f;

        headingYaw = GoalYaw();
        headingVelocity = 0f;

        Place();
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 goal = FocusGoal();
        focusXZ = Vector2.SmoothDamp(focusXZ, new Vector2(goal.x, goal.z), ref focusVelocityXZ, positionSmoothTime);
        focusY = Mathf.SmoothDamp(focusY, goal.y, ref focusVelocityY, verticalSmoothTime);
        headingYaw = Mathf.SmoothDampAngle(headingYaw, GoalYaw(), ref headingVelocity, Mathf.Max(headingSmoothTime, 0.01f));

        Place();
        UpdateFov();
    }

    // The point the camera tries to keep centered: the log, nudged ahead along its velocity.
    Vector3 FocusGoal()
    {
        Vector3 goal = target.position;
        if (body != null)
        {
            Vector3 flatVelocity = body.linearVelocity;
            flatVelocity.y = 0f;
            Vector3 ahead = Vector3.ClampMagnitude(flatVelocity * lookAheadFactor, maxLookAhead);
            goal += ahead;
        }
        return goal;
    }

    // Which way the camera should face (degrees around the vertical axis).
    float GoalYaw()
    {
        if (!followHeading || movement == null) return fixedYaw;
        Vector3 f = movement.Forward;
        return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
    }

    void Place()
    {
        Vector3 focus = new Vector3(focusXZ.x, focusY, focusXZ.y);
        Quaternion heading = Quaternion.Euler(0f, headingYaw, 0f);

        Vector3 position = focus - heading * Vector3.forward * distance + Vector3.up * height;
        transform.position = position;
        transform.rotation = Quaternion.LookRotation((focus + Vector3.up * lookAtHeight) - position, Vector3.up);
    }

    void UpdateFov()
    {
        if (fovBoost <= 0f || body == null) return;

        float speed01 = Mathf.Clamp01(body.linearVelocity.magnitude / Mathf.Max(fovFullSpeed, 0.1f));
        float goalFov = baseFov + fovBoost * speed01;
        float blend = 1f - Mathf.Exp(-fovSmoothing * Time.deltaTime);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, goalFov, blend);
    }
}