using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class LogController : MonoBehaviour
{
    [Header("Keys")]
    public KeyCode leftPlayerKey = KeyCode.W;
    public KeyCode rightPlayerKey = KeyCode.UpArrow;
    public bool leftEnabled = true;
    public bool rightEnabled = true;

    [Header("Push feel (per second)")]
    public float pushRiseRate = 1.2f;
    public float pushFallRate = 2.5f;

    [Header("Forces")]
    public float moveForce = 6f;
    public float turnTorque = 2.5f;
    public float rollTorque = 3f;
    public float maxSpeed = 3f;

    [Header("Tilt from uneven pushing")]
    public Transform leftSensor;
    public Transform rightSensor;
    public float tolerance = 0.25f;

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
        LeftPush = Ramp(LeftPush, leftEnabled && Input.GetKey(leftPlayerKey));
        RightPush = Ramp(RightPush, rightEnabled && Input.GetKey(rightPlayerKey));
    }

    float Ramp(float value, bool held)
    {
        float rate = held ? pushRiseRate : pushFallRate;
        return Mathf.MoveTowards(value, held ? 1f : 0f, rate * Time.deltaTime);
    }

    void FixedUpdate()
    {
        float avg = (LeftPush + RightPush) * 0.5f;
        float diff = LeftPush - RightPush;

        Vector3 axis = transform.forward;
        Vector3 travel = Vector3.Cross(Vector3.up, axis).normalized;

        if (Vector3.Dot(rb.linearVelocity, travel) < maxSpeed)
            rb.AddForce(travel * avg * moveForce, ForceMode.Acceleration);

        rb.AddTorque(Vector3.up * diff * turnTorque, ForceMode.Acceleration);
        rb.AddTorque(-axis * avg * rollTorque, ForceMode.Acceleration);

        // uneven pushing -> dip the end of the player who can't keep up
        float tilt = 0f;
        float imbalance = Mathf.Abs(diff);
        if (imbalance > tolerance && leftSensor != null && rightSensor != null)
        {
            float factor = Mathf.Clamp01((imbalance - tolerance) / (1f - tolerance));
            Transform dipEnd = diff > 0f ? rightSensor : leftSensor;
            float side = Mathf.Sign(dipEnd.localPosition.z);
            tilt = -side * factor * floatSystem.maxTiltDeg;
        }
        floatSystem.targetPitchDeg = tilt;
    }
}