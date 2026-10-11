using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class LogFloat : MonoBehaviour
{
    public float waterLevel = 0f;
    public float sinkDepth = 0.1f;
    public float spring = 40f;
    public float damping = 10f;
    public float maxSpeedY = 3f;

    [Header("Tilt (end dipping)")]
    public float pitchStiffness = 25f;
    public float pitchDamping = 6f;
    public float maxTiltDeg = 20f;          // hard cap on how far an end can dip
    [HideInInspector] public float targetPitchDeg;   // set by LogController

    [Header("Safety")]
    public float maxSpinSpeed = 6f;

    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.maxAngularVelocity = maxSpinSpeed;
    }

    void FixedUpdate()
    {
        // height spring
        float error = (waterLevel - sinkDepth) - rb.position.y;
        float accel = error * spring - rb.linearVelocity.y * damping;
        rb.AddForce(Vector3.up * accel, ForceMode.Acceleration);

        Vector3 v = rb.linearVelocity;
        v.y = Mathf.Clamp(v.y, -maxSpeedY, maxSpeedY);
        rb.linearVelocity = v;

        // tilt servo: steer the log's long axis toward the target pitch
        Vector3 axis = transform.forward;
        Vector3 travel = Vector3.Cross(Vector3.up, axis);
        travel = travel.sqrMagnitude < 0.0001f ? Vector3.right : travel.normalized;

        float pitch = Mathf.Asin(Mathf.Clamp(axis.y, -1f, 1f));   // + = +Z end up
        float target = Mathf.Clamp(targetPitchDeg, -maxTiltDeg, maxTiltDeg) * Mathf.Deg2Rad;

        float pitchAccel = (target - pitch) * pitchStiffness
                         + Vector3.Dot(rb.angularVelocity, travel) * pitchDamping;
        rb.AddTorque(-travel * pitchAccel, ForceMode.Acceleration);
    }
}