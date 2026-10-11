using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class LogController : MonoBehaviour
{
    [Header("Keys")]
    public KeyCode leftForwardKey = KeyCode.W;
    public KeyCode leftBackKey = KeyCode.S;
    public KeyCode rightForwardKey = KeyCode.UpArrow;
    public KeyCode rightBackKey = KeyCode.DownArrow;
    public bool leftEnabled = true;
    public bool rightEnabled = true;

    [Header("Push feel (per second)")]
    public float pushRiseRate = 2f;
    public float pushFallRate = 2.5f;

    [Header("Forces")]
    public float moveForce = 6f;
    public float turnTorque = 2.5f;
    public float rollTorque = 3f;
    public float maxSpeed = 3f;
    public float reverseSpeedFactor = 0.6f;   // backward top speed = maxSpeed * this

    [Header("Tilt from uneven pushing")]
    public Transform leftSensor;
    public Transform rightSensor;
    public float tolerance = 0.25f;

    // -1 (full backward) to +1 (full forward)
    public float LeftPush { get; private set; }
    public float RightPush { get; private set; }

    Rigidbody rb;
    LogFloat floatSystem;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        floatSystem = GetComponent<LogFloat>();
    }

    void Update()
    {
        LeftPush = Ramp(LeftPush, ReadInput(leftEnabled, leftForwardKey, leftBackKey));
        RightPush = Ramp(RightPush, ReadInput(rightEnabled, rightForwardKey, rightBackKey));
    }

    float ReadInput(bool enabled, KeyCode fwd, KeyCode back)
    {
        if (!enabled) return 0f;
        float v = 0f;
        if (Input.GetKey(fwd)) v += 1f;
        if (Input.GetKey(back)) v -= 1f;
        return v;
    }

    float Ramp(float value, float target)
    {
        float rate = Mathf.Approximately(target, 0f) ? pushFallRate : pushRiseRate;
        return Mathf.MoveTowards(value, target, rate * Time.deltaTime);
    }

    void FixedUpdate()
    {
        float avg = (LeftPush + RightPush) * 0.5f;
        float diff = LeftPush - RightPush;

        Vector3 axis = transform.forward;
        Vector3 travel = Vector3.Cross(Vector3.up, axis).normalized;

        // forward or backward push, each with its own speed cap
        float speedAlong = Vector3.Dot(rb.linearVelocity, travel);
        bool canForward = avg > 0f && speedAlong < maxSpeed;
        bool canBackward = avg < 0f && speedAlong > -maxSpeed * reverseSpeedFactor;
        if (canForward || canBackward)
            rb.AddForce(travel * avg * moveForce, ForceMode.Acceleration);

        // turning (one forward + one backward spins the log on the spot)
        rb.AddTorque(Vector3.up * diff * turnTorque, ForceMode.Acceleration);

        // visual rolling, reverses automatically with negative avg
        rb.AddTorque(-axis * avg * rollTorque, ForceMode.Acceleration);

        // dip: whoever pushes with less strength can't keep up
        float strength = Mathf.Abs(LeftPush) - Mathf.Abs(RightPush);
        float tilt = 0f;
        if (Mathf.Abs(strength) > tolerance && leftSensor != null && rightSensor != null)
        {
            float factor = Mathf.Clamp01((Mathf.Abs(strength) - tolerance) / (1f - tolerance));
            Transform dipEnd = strength > 0f ? rightSensor : leftSensor;
            float side = Mathf.Sign(dipEnd.localPosition.z);
            tilt = -side * factor * floatSystem.maxTiltDeg;
        }
        floatSystem.targetPitchDeg = tilt;
    }
}