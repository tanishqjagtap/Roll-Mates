using UnityEngine;

// Spins an obstacle around its own X, Y or Z axis, in either direction.
// Put this on the ROOT of the obstacle prefab.
// If the obstacle has colliders, also add a Rigidbody with "Is Kinematic" ticked
// and the script will spin it through the physics system automatically.
public class ObstacleSpinner : MonoBehaviour
{
    public enum SpinDirection
    {
        Clockwise,
        Anticlockwise
    }

    public enum SpinAxis
    {
        X,
        Y,
        Z
    }

    [Header("Spin")]
    [Tooltip("Y = flat, like a propeller seen from above. X or Z = like a wheel.")]
    public SpinAxis spinAxis = SpinAxis.Y;

    [Tooltip("Clockwise is as seen from the + end of the axis (for Y: looking down from above; " +
             "for X: from the right side; for Z: from in front).")]
    public SpinDirection direction = SpinDirection.Clockwise;

    [Tooltip("Degrees per second.")]
    [Min(0f)] public float speed = 25f;

    [Header("Variation")]
    [Tooltip("Start at a random angle so several obstacles do not spin in sync.")]
    public bool randomStartAngle = true;

    Rigidbody body;
    bool useBody;

    void Awake()
    {
        useBody = TryGetComponent(out body) && body.isKinematic;
        if (useBody) body.interpolation = RigidbodyInterpolation.Interpolate;

        if (randomStartAngle)
            transform.Rotate(AxisVector, Random.Range(0f, 360f), Space.Self);
    }

    void Update()
    {
        if (useBody) return;
        transform.Rotate(AxisVector, Step(Time.deltaTime), Space.Self);
    }

    void FixedUpdate()
    {
        if (!useBody) return;
        body.MoveRotation(body.rotation * Quaternion.AngleAxis(Step(Time.fixedDeltaTime), AxisVector));
    }

    Vector3 AxisVector
    {
        get
        {
            switch (spinAxis)
            {
                case SpinAxis.X: return Vector3.right;
                case SpinAxis.Z: return Vector3.forward;
                default: return Vector3.up;
            }
        }
    }

    float Step(float deltaTime)
    {
        float sign = direction == SpinDirection.Clockwise ? 1f : -1f;
        return speed * sign * deltaTime;
    }
}