using UnityEngine;

[RequireComponent(typeof(Camera))]
public class FollowCamera : MonoBehaviour
{
    public Transform target;            // LogRoot
    public Rigidbody targetBody;        // optional, found automatically

    [Header("Position")]
    public float distance = 14f;
    public float height = 6f;
    public float lookAhead = 4f;
    public float lookHeight = 0.5f;

    [Header("Stay behind")]
    public float turnSmooth = 5f;           // higher = camera swings around faster
    public float maxLagAngle = 20f;         // camera can never be further off than this
    public float positionSmoothTime = 0.12f;
    public float lookSmooth = 10f;

    [Header("Speed effects")]
    public float pullBackAtSpeed = 2.5f;    // extra distance at full speed
    public float baseFov = 60f;
    public float maxFovBoost = 6f;
    public float fullEffectSpeed = 3f;
    public float effectSmooth = 3f;

    Camera cam;
    Vector3 heading;
    Vector3 velocity;
    float extraDistance;
    float shakeTime, shakeDuration, shakeStrength;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (target != null && targetBody == null)
            targetBody = target.GetComponent<Rigidbody>();
    }

    void Start()
    {
        if (target == null) return;
        heading = GetTravelDirection();
        transform.position = DesiredPosition();
        transform.rotation = Quaternion.LookRotation(LookPoint() - transform.position);
    }

    // log's long axis (local Z) doesn't change when it rolls
    Vector3 GetTravelDirection()
    {
        Vector3 dir = Vector3.Cross(Vector3.up, target.forward);
        dir.y = 0f;
        return dir.sqrMagnitude < 0.0001f ? Vector3.forward : dir.normalized;
    }

    Vector3 DesiredPosition() =>
        target.position - heading * (distance + extraDistance) + Vector3.up * height;

    Vector3 LookPoint() =>
        target.position + heading * lookAhead + Vector3.up * lookHeight;

    void LateUpdate()
    {
        if (target == null) return;

        // 1. smoothly follow the log's heading
        Vector3 raw = GetTravelDirection();
        float t = 1f - Mathf.Exp(-turnSmooth * Time.deltaTime);
        heading = Vector3.Slerp(heading, raw, t).normalized;

        // 2. but never lag more than maxLagAngle behind it
        float off = Vector3.SignedAngle(heading, raw, Vector3.up);
        if (Mathf.Abs(off) > maxLagAngle)
        {
            float fix = off - Mathf.Sign(off) * maxLagAngle;
            heading = Quaternion.AngleAxis(fix, Vector3.up) * heading;
        }

        // 3. speed effects
        float speed = targetBody != null ? targetBody.linearVelocity.magnitude : 0f;
        float k = Mathf.Clamp01(speed / fullEffectSpeed);
        float e = 1f - Mathf.Exp(-effectSmooth * Time.deltaTime);
        extraDistance = Mathf.Lerp(extraDistance, pullBackAtSpeed * k, e);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, baseFov + maxFovBoost * k, e);

        // 4. position and look
        Vector3 pos = Vector3.SmoothDamp(transform.position, DesiredPosition(),
                                         ref velocity, positionSmoothTime);
        Quaternion wanted = Quaternion.LookRotation(LookPoint() - pos);
        float r = 1f - Mathf.Exp(-lookSmooth * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, wanted, r);

        // 5. shake
        if (shakeTime > 0f)
        {
            shakeTime -= Time.deltaTime;
            pos += Random.insideUnitSphere * shakeStrength * (shakeTime / shakeDuration);
        }
        transform.position = pos;
    }

    public void Shake(float strength, float duration)
    {
        shakeStrength = strength;
        shakeDuration = Mathf.Max(0.01f, duration);
        shakeTime = shakeDuration;
    }
}