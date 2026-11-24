using UnityEngine;

// Put this on the log MODEL (the child of LogRoot).
// It rolls the model around the log's length like a real rolling log: faster log = faster roll.
// Only the picture rolls. The physics body and the players stay put.
public class LogRollVisual : MonoBehaviour
{
    [Tooltip("1 = rolls exactly as fast as the log travels. Lower or higher to taste.")]
    [SerializeField] float rollMultiplier = 1f;

    LogMovement movement;
    Rigidbody body;
    CapsuleCollider capsule;

    Vector3 baseLocalPosition;
    Quaternion baseLocalRotation;
    Vector3 localAxis;
    float rollAngle;

    void Awake()
    {
        movement = GetComponentInParent<LogMovement>();
        if (movement == null)
        {
            Debug.LogError("LogRollVisual: put this on the log model, a child of LogRoot.", this);
            enabled = false;
            return;
        }

        body = movement.GetComponent<Rigidbody>();
        capsule = movement.GetComponent<CapsuleCollider>();

        baseLocalPosition = transform.localPosition;
        baseLocalRotation = transform.localRotation;
        localAxis = (movement.RightDrive.localPosition - movement.LeftDrive.localPosition).normalized;
    }

    void Update()
    {
        float radius = Mathf.Max(capsule.radius, 0.01f);
        float forwardSpeed = Vector3.Dot(body.linearVelocity, movement.Forward);

        // Rolling without slipping: angular speed = travel speed / radius.
        float degreesPerSecond = (forwardSpeed / radius) * Mathf.Rad2Deg * rollMultiplier;
        rollAngle = Mathf.Repeat(rollAngle + degreesPerSecond * Time.deltaTime, 360f);

        // Spin about the log's own center line, not the model's pivot.
        Quaternion roll = Quaternion.AngleAxis(rollAngle, localAxis);
        Vector3 pivot = capsule.center;
        transform.localPosition = pivot + roll * (baseLocalPosition - pivot);
        transform.localRotation = roll * baseLocalRotation;
    }
}