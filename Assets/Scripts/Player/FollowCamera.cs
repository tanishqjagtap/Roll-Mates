using UnityEngine;

[RequireComponent(typeof(Camera))]
public class FollowCamera : MonoBehaviour
{
    public Transform target;            // drag LogRoot here
    public Rigidbody targetBody;        // optional, found automatically

    [Header("Position")]
    public float distance = 14f;        // behind the log
    public float height = 6f;           // above the log
    public float lookAhead = 4f;        // aim point in front of the log
    public float lookHeight = 0.5f;

    [Header("Smoothing")]
    public float positionSmoothTime = 0.25f;
    public float turnSmooth = 2.5f;     // lower = lazier turns
    public float lookSmooth = 6f;

    [Header("Speed FOV")]
    public float baseFov = 60f;
    public float maxFovBoost = 8f;
    public float fullBoostSpeed = 4f;
    public float fovSmooth = 3f;

    Camera cam;
    Vector3 heading;
    Vector3 velocity;
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

    // The log's long axis (local Z) doesn't change when it rolls,
    // so heading is built from it. Same formula as LogController.
    Vector3 GetTravelDirection()
    {
        Vector3 dir = Vector3.Cross(Vector3.up, target.forward);
        dir.y = 0f;
        return dir.sqrMagnitude < 0.0001f ? Vector3.forward : dir.normalized;
    }

    Vector3 DesiredPosition() =>
        target.position - heading * distance + Vector3.up * height;

    Vector3 LookPoint() =>
        target.position + heading * lookAhead + Vector3.up * lookHeight;

    void LateUpdate()
    {
        if (target == null) return;

        // smoothly follow the log's heading
        Vector3 raw = GetTravelDirection();
        float t = 1f - Mathf.Exp(-turnSmooth * Time.deltaTime);
        heading = Vector3.Slerp(heading, raw, t).normalized;

        // position
        Vector3 pos = Vector3.SmoothDamp(transform.position, DesiredPosition(),
                                         ref velocity, positionSmoothTime);

        // rotation
        Quaternion wanted = Quaternion.LookRotation(LookPoint() - pos);
        float r = 1f - Mathf.Exp(-lookSmooth * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, wanted, r);

        // shake
        if (shakeTime > 0f)
        {
            shakeTime -= Time.deltaTime;
            float falloff = shakeTime / shakeDuration;
            pos += Random.insideUnitSphere * shakeStrength * falloff;
        }
        transform.position = pos;

        // speed-based FOV
        if (targetBody != null)
        {
            float speed = targetBody.linearVelocity.magnitude;
            float goal = baseFov + maxFovBoost * Mathf.Clamp01(speed / fullBoostSpeed);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, goal,
                                         1f - Mathf.Exp(-fovSmooth * Time.deltaTime));
        }
    }

    // call from other scripts: cameraScript.Shake(0.3f, 0.4f);
    public void Shake(float strength, float duration)
    {
        shakeStrength = strength;
        shakeDuration = Mathf.Max(0.01f, duration);
        shakeTime = shakeDuration;
    }
}