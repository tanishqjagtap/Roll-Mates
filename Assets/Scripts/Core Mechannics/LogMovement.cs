using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class LogMovement : MonoBehaviour
{
    [Header("Drive Points (place at the log's two ends, at axis height)")]
    [SerializeField] Transform leftDrive;
    [SerializeField] Transform rightDrive;

    [Header("Drive")]
    [SerializeField] float driveStrength = 400f;
    [SerializeField] float maxSpeed = 4f;
    [Tooltip("Seconds for a push to build up to full. Higher = lazier, heavier feel.")]
    [SerializeField] float pushRampTime = 0.5f;

    [Header("Grip (lower = more slippery and harder to control)")]
    [Tooltip("How firmly the player who is NOT pushing holds their end. Low = that end drifts and the turn gets sloppy.")]
    [SerializeField] float staticEndGrip = 60f;
    [Tooltip("How much the log resists sliding along its own length. Low = skids sideways in turns.")]
    [SerializeField] float sideGrip = 60f;
    [Tooltip("Safety cap on spin speed, degrees per second.")]
    [SerializeField] float maxTurnSpeed = 140f;

    [Header("Surface (the surface system will drive this later)")]
    public float surfaceMultiplier = 1f;

    // Read by the balance system, the players and the roll animation.
    public float LeftInput => leftInput;
    public float RightInput => rightInput;
    public Transform LeftDrive => leftDrive;
    public Transform RightDrive => rightDrive;

    // While true the log is disconnected from the keyboard: no pushing, no holding.
    // It just glides on its momentum and the water slows it down.
    public bool InputLocked { get; set; }

    // Flat direction the log travels in when both ends push forward.
    public Vector3 Forward
    {
        get
        {
            Vector3 axis = rightDrive.position - leftDrive.position;
            axis.y = 0f;
            if (axis.sqrMagnitude < 0.0001f) return transform.forward;
            return Vector3.Cross(axis.normalized, Vector3.up);
        }
    }

    Rigidbody rb;
    float leftInput;
    float rightInput;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    public void ResetInputs()
    {
        leftInput = 0f;
        rightInput = 0f;
    }

    void FixedUpdate()
    {
        Vector3 fullAxis = rightDrive.position - leftDrive.position;
        Vector3 axis = fullAxis;
        axis.y = 0f;
        if (axis.sqrMagnitude < 0.0001f) return;
        axis.Normalize();

        // Facing "forward", the right end sits on the +axis side.
        Vector3 forward = Vector3.Cross(axis, Vector3.up);

        // Left player: W/S. Right player: Up/Down arrows.
        float leftTarget = InputLocked ? 0f : ReadAxis(Key.W, Key.S);
        float rightTarget = InputLocked ? 0f : ReadAxis(Key.UpArrow, Key.DownArrow);

        float step = Time.fixedDeltaTime / Mathf.Max(pushRampTime, 0.01f);
        leftInput = Mathf.MoveTowards(leftInput, leftTarget, step);
        rightInput = Mathf.MoveTowards(rightInput, rightTarget, step);

        // Disconnected from the keyboard = no forces from the players at all.
        if (!InputLocked)
        {
            ApplyEnd(leftDrive, leftInput, forward);
            ApplyEnd(rightDrive, rightInput, forward);
        }

        // Log resists sliding along its own length.
        float lateralSpeed = Vector3.Dot(rb.linearVelocity, axis);
        rb.AddForce(-axis * lateralSpeed * sideGrip);

        // The log never physically spins along its length (that would tip the players over).
        // The rolling you see is animated by LogRollVisual instead.
        Vector3 av = rb.angularVelocity;
        Vector3 rollAxis = fullAxis.normalized;
        av -= rollAxis * Vector3.Dot(av, rollAxis);

        // Safety cap on turning speed.
        float maxYaw = maxTurnSpeed * Mathf.Deg2Rad;
        if (Mathf.Abs(av.y) > maxYaw) av.y = Mathf.Sign(av.y) * maxYaw;
        rb.angularVelocity = av;
    }

    void ApplyEnd(Transform drive, float input, Vector3 forward)
    {
        float endSpeed = Vector3.Dot(rb.GetPointVelocity(drive.position), forward);

        if (Mathf.Abs(input) > 0.01f)
        {
            // Push, tapering off as this end approaches max speed.
            float ratio = Mathf.Clamp01(endSpeed * Mathf.Sign(input) / maxSpeed);
            float force = input * driveStrength * surfaceMultiplier * (1f - ratio);
            rb.AddForceAtPosition(forward * force, drive.position);
        }
        else
        {
            // Static end: resist movement so the other end pivots around it.
            rb.AddForceAtPosition(-forward * endSpeed * staticEndGrip, drive.position);
        }
    }

    static float ReadAxis(Key positive, Key negative)
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return 0f;
        float v = 0f;
        if (kb[positive].isPressed) v += 1f;
        if (kb[negative].isPressed) v -= 1f;
        return v;
    }

    // Draws the log axis (yellow) and forward direction (green) in the Scene view.
    void OnDrawGizmosSelected()
    {
        if (leftDrive == null || rightDrive == null) return;
        Vector3 axis = rightDrive.position - leftDrive.position;
        axis.y = 0f;
        if (axis.sqrMagnitude < 0.0001f) return;
        axis.Normalize();
        Vector3 forward = Vector3.Cross(axis, Vector3.up);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(leftDrive.position, rightDrive.position);
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, forward * 3f);
    }
}