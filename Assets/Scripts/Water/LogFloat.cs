using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class LogFloat : MonoBehaviour
{
    public float waterLevel = 1f;   // Y of your water plane
    public float sinkDepth = 0.1f;  // how far the log's center sits under the surface
    public float spring = 40f;      // how strongly it's pulled to the surface
    public float damping = 8f;      // stops endless bobbing
    public float maxSpeedY = 3f;    // safety cap, so it can never shoot away
    public float levelStrength = 6f;  // how strongly the log stays level front-to-back

    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
    }

    void FixedUpdate()
    {
        float targetY = waterLevel - sinkDepth;
        float error = targetY - rb.position.y;
        float accel = error * spring - rb.linearVelocity.y * damping;
        rb.AddForce(Vector3.up * accel, ForceMode.Acceleration);

        Vector3 v = rb.linearVelocity;
        v.y = Mathf.Clamp(v.y, -maxSpeedY, maxSpeedY);
        rb.linearVelocity = v;
        
        Vector3 fwd = transform.forward;
        Vector3 flat = new Vector3(fwd.x, 0f, fwd.z).normalized;
        rb.AddTorque(Vector3.Cross(fwd, flat) * levelStrength, ForceMode.Acceleration);
    }
}