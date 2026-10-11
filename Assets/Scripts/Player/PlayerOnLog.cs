using UnityEngine;

public class PlayerOnLog : MonoBehaviour
{
    public Transform log;               // LogRoot
    public BalanceSystem balance;       // BalanceManager
    public Transform sensor;            // LeftSensor or RightSensor
    public bool isLeft = true;

    [Header("Standing")]
    public float standHeight = 1.95f;   // log radius + half the capsule height
    public float endInset = 0.9f;       // 1 = right at the sensor, lower = nearer the middle

    [Header("Feel")]
    public float leanDegrees = 12f;     // leans into the push
    public float wobbleDegrees = 10f;   // wobbles when this end is dipping
    public float wobbleSpeed = 14f;

    [Header("Fall")]
    public float sideSpeed = 3f;
    public float upSpeed = 3f;
    public float gravity = 18f;
    public float sinkBelow = 2.5f;

    LogController ctrl;
    LogFloat water;
    bool falling;
    Vector3 vel, spin;
    float lean;

    void Awake()
    {
        ctrl = log.GetComponent<LogController>();
        water = log.GetComponent<LogFloat>();
    }

    void LateUpdate()
    {
        bool fell = isLeft ? balance.LeftFell : balance.RightFell;
        if (fell && !falling) StartFall();

        if (falling) Fall();
        else Follow();
    }

    void Follow()
    {
        // roll-free frame: forward = log's long axis, up = world up
        Quaternion frame = Quaternion.LookRotation(log.forward, Vector3.up);
        Vector3 local = new Vector3(0f, standHeight, sensor.localPosition.z * endInset);
        transform.position = log.position + frame * local;

        float push = isLeft ? ctrl.LeftPush : ctrl.RightPush;
        lean = Mathf.Lerp(lean, push * leanDegrees, 1f - Mathf.Exp(-8f * Time.deltaTime));

        float dip = isLeft ? balance.LeftDipProgress : balance.RightDipProgress;
        float wobble = Mathf.Sin(Time.time * wobbleSpeed) * dip * wobbleDegrees;

        transform.rotation =
            Quaternion.AngleAxis(-lean, frame * Vector3.forward) *
            Quaternion.AngleAxis(wobble, frame * Vector3.right) * frame;
    }

    void StartFall()
    {
        falling = true;
        Vector3 outward = log.forward * Mathf.Sign(sensor.localPosition.z);
        outward.y = 0f;
        vel = outward.normalized * sideSpeed + Vector3.up * upSpeed;
        spin = Random.onUnitSphere * 200f;
    }

    void Fall()
    {
        float dt = Time.deltaTime;
        vel.y -= gravity * dt;
        if (transform.position.y < water.waterLevel) vel *= 1f - 4f * dt;   // water slows it
        transform.position += vel * dt;
        transform.Rotate(spin * dt, Space.World);

        if (transform.position.y < water.waterLevel - sinkBelow)
            gameObject.SetActive(false);
    }
}