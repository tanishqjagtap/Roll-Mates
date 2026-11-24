using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(LogMovement))]
public class LogBalance : MonoBehaviour
{
    [Header("Strain (how long a player can over-push)")]
    [Tooltip("Seconds of full over-pushing before that player falls.")]
    [SerializeField] float timeToFall = 4f;
    [Tooltip("Below this push amount (0 to 1) strain recovers instead of building.")]
    [SerializeField, Range(0f, 1f)] float pushThreshold = 0.3f;
    [Tooltip("How much faster strain drains than it builds.")]
    [SerializeField] float recoverSpeed = 2f;
    [Tooltip("ON: strain only builds while one player pushes harder than the other. OFF: holding forward alone is enough.")]
    [SerializeField] bool onlyWhenPartnerDiffers = true;

    [Header("Fall and Respawn")]
    [Tooltip("Seconds the log glides on its own before everything restarts.")]
    [SerializeField] float fallDuration = 1.5f;
    [Tooltip("One last shove at the fallen player's end as the log lets go (impulse, N*s). 0 = none.")]
    [SerializeField] float slipImpulse = 120f;
    [Tooltip("Seconds before the restart after the players touch an obstacle. They fall the instant they touch it; this is only the wait before the restart.")]
    [SerializeField] float obstacleFallDuration = 0.8f;
    [SerializeField] float respawnLockTime = 0.75f;

    [Header("Push Dip (a pushing player presses their end into the water)")]
    [Tooltip("Downward press at the pushing player's end, as a fraction of the log's weight, at full push.")]
    [SerializeField, Range(0f, 0.5f)] float dipWeightFraction = 0.15f;
    [Tooltip("Extra dip as strain builds, so a player about to fall sinks their end more.")]
    [SerializeField] float strainDipBoost = 2f;

    [Header("Debug")]
    [SerializeField] bool showDebugBars = true;

    // 0 = left player, 1 = right player, 2 = both players.
    public event Action<int> PlayerFell;
    // Fired right after the log is put back at its start. Players use it to climb back on.
    public event Action Respawned;

    public float ObstacleFallDuration => obstacleFallDuration;

    // The log's velocity just before the latest physics step, i.e. before any crash slows it down.
    // Players use it so they keep flying forward when the log hits something.
    public Vector3 PreStepVelocity { get; private set; }

    public float LeftStrain => leftStrain;
    public float RightStrain => rightStrain;

    enum State { Playing, Falling }

    Rigidbody rb;
    LogMovement movement;
    State state = State.Playing;
    float leftStrain;
    float rightStrain;

    // Where the log restarts. Starts as the pose the log has when the scene begins.
    Vector3 respawnPosition;
    Quaternion respawnRotation;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        movement = GetComponent<LogMovement>();
        respawnPosition = transform.position;
        respawnRotation = transform.rotation;
    }

    // Checkpoints will call this later.
    public void SetCheckpoint(Vector3 position, Quaternion rotation)
    {
        respawnPosition = position;
        respawnRotation = rotation;
    }

    void FixedUpdate()
    {
        PreStepVelocity = rb.linearVelocity;

        if (state != State.Playing) return;

        float left = movement.LeftInput;
        float right = movement.RightInput;

        leftStrain = UpdateStrain(leftStrain, left, right);
        rightStrain = UpdateStrain(rightStrain, right, left);

        ApplyDip(movement.LeftDrive, left, leftStrain);
        ApplyDip(movement.RightDrive, right, rightStrain);

        if (leftStrain >= 1f) TriggerFall(0, true);
        else if (rightStrain >= 1f) TriggerFall(1, true);
    }

    void ApplyDip(Transform end, float input, float strain)
    {
        float push = Mathf.Max(input, 0f);
        if (push <= 0f) return;

        float weight = rb.mass * Physics.gravity.magnitude;
        float force = weight * dipWeightFraction * push * (1f + strain * strainDipBoost);
        rb.AddForceAtPosition(Vector3.down * force, end.position);
    }

    float UpdateStrain(float strain, float mine, float partner)
    {
        // Only forward pushing counts.
        float push = Mathf.Max(mine, 0f);
        float mismatch = onlyWhenPartnerDiffers ? Mathf.Max(push - Mathf.Max(partner, 0f), 0f) : push;

        float change = Time.fixedDeltaTime / Mathf.Max(timeToFall, 0.1f);
        if (mismatch > pushThreshold) strain += mismatch * change;
        else strain -= recoverSpeed * change;

        return Mathf.Clamp01(strain);
    }

    // Starts the fall and restart. side: 0 = left player, 1 = right player, 2 = both.
    // Ignored if a fall is already happening.
    public void TriggerFall(int side, bool slip = false, float restartDelay = -1f)
    {
        if (state != State.Playing) return;
        StartCoroutine(FallRoutine(side, slip, restartDelay >= 0f ? restartDelay : fallDuration));
    }

    IEnumerator FallRoutine(int side, bool slip, float restartDelay)
    {
        state = State.Falling;

        // 1. Log disconnects from the keyboard and glides on its own.
        movement.InputLocked = true;

        // 2. Over-pushing gives the log one last shove at that player's end.
        //    Then the player (or both players) go in the water.
        if (slip && side != 2 && slipImpulse > 0f)
        {
            Transform end = side == 0 ? movement.LeftDrive : movement.RightDrive;
            rb.AddForceAtPosition(movement.Forward * slipImpulse, end.position, ForceMode.Impulse);
        }
        PlayerFell?.Invoke(side);

        // 3. Let physics play out.
        yield return new WaitForSeconds(restartDelay);

        // 4. Back to the start.
        Respawn();
        Respawned?.Invoke();

        yield return new WaitForSeconds(respawnLockTime);
        movement.InputLocked = false;
        state = State.Playing;
    }

    void Respawn()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.position = respawnPosition;
        rb.rotation = respawnRotation;
        transform.SetPositionAndRotation(respawnPosition, respawnRotation);
        Physics.SyncTransforms();

        movement.ResetInputs();
        leftStrain = 0f;
        rightStrain = 0f;
    }

    // Simple strain bars in the Game view so you can tune the feel.
    void OnGUI()
    {
        if (!showDebugBars) return;
        DrawBar(new Rect(20f, 20f, 200f, 16f), leftStrain, "Left");
        DrawBar(new Rect(20f, 44f, 200f, 16f), rightStrain, "Right");
    }

    static void DrawBar(Rect rect, float value, string label)
    {
        GUI.color = Color.black;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.Lerp(Color.green, Color.red, value);
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * value, rect.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(rect.xMax + 8f, rect.y - 3f, 80f, 20f), label);
    }
}