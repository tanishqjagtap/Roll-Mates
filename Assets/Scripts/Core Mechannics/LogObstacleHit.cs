using UnityEngine;

// Put this on LogRoot.
// When the log touches anything tagged "Obstacle", both players fall into the water,
// the log glides for a moment, then everything restarts (handled by LogBalance).
[RequireComponent(typeof(LogBalance))]
public class LogObstacleHit : MonoBehaviour
{
    [SerializeField] string obstacleTag = "Obstacle";
    [Tooltip("Ignore very light touches. 0 = any touch counts.")]
    [SerializeField, Min(0f)] float minImpactSpeed = 0f;

    LogBalance balance;

    void Awake()
    {
        balance = GetComponent<LogBalance>();
    }

    // Solid obstacles: the log bumps into them.
    void OnCollisionEnter(Collision collision)
    {
        if (collision.relativeVelocity.magnitude < minImpactSpeed) return;
        if (IsObstacle(collision.collider)) Hit();
    }

    // Trigger obstacles: the log passes through them but still counts as a hit.
    void OnTriggerEnter(Collider other)
    {
        if (IsObstacle(other)) Hit();
    }

    void Hit()
    {
        balance.TriggerFall(2, false, balance.ObstacleFallDuration); // 2 = both players
    }

    // The tag can be on the collider's object or on any parent,
    // so you only need to tag the root of each obstacle prefab.
    bool IsObstacle(Collider other)
    {
        Transform t = other.transform;
        while (t != null)
        {
            if (t.CompareTag(obstacleTag)) return true;
            t = t.parent;
        }
        return false;
    }
}