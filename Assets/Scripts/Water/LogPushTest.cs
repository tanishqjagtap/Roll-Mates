using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class LogPushTest : MonoBehaviour
{
    public float pushForce = 15f;
    Rigidbody rb;
    void Awake() => rb = GetComponent<Rigidbody>();

    void FixedUpdate()
    {
        Vector3 fwd = transform.right;
        fwd.y = 0f;
        fwd.Normalize();

        if (Input.GetKey(KeyCode.W)) rb.AddForce(fwd * pushForce, ForceMode.Acceleration);
        if (Input.GetKey(KeyCode.S)) rb.AddForce(-fwd * pushForce, ForceMode.Acceleration);
    }
}