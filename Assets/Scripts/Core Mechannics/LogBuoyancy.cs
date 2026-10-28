using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class LogBuoyancy : MonoBehaviour
{
    [Header("Water")]
    [Tooltip("Every object on this layer with a Renderer counts as water. No colliders needed.")]
    [SerializeField] LayerMask waterLayers;

    [Header("Float Points (spread along the log's length, on its center line)")]
    [SerializeField, Min(2)] int pointCount = 6;

    [Header("Buoyancy")]
    [Tooltip("1 = log floats exactly half-sunk. Above 1 floats higher, below 1 sits lower.")]
    [SerializeField, Range(0.5f, 1.5f)] float buoyancyScale = 1f;
    [Tooltip("Per point. Higher = fewer bobs after a splash, lower = bobs longer.")]
    [SerializeField] float verticalDamping = 40f;

    [Header("Gentle Waves (set Wave Height to 0 for flat water)")]
    [SerializeField] float waveHeight = 0.05f;
    [SerializeField] float waveSpeed = 1.2f;
    [SerializeField] float waveFrequency = 0.5f;

    [Header("Drag")]
    [SerializeField] float airLinearDrag = 0.5f;
    [SerializeField] float waterLinearDrag = 0.7f;
    [SerializeField] float airAngularDrag = 1f;
    [SerializeField] float waterAngularDrag = 1.2f;

    // 0 = fully out of water, 1 = fully under. Other scripts can read this later.
    public float SubmergedFraction { get; private set; }

    Rigidbody rb;
    CapsuleCollider capsule;
    Renderer[] surfaces = new Renderer[0];

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        CacheSurfaces();
    }

    void CacheSurfaces()
    {
        Renderer[] all = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        List<Renderer> found = new List<Renderer>();
        foreach (Renderer r in all)
        {
            if ((waterLayers.value & (1 << r.gameObject.layer)) != 0)
                found.Add(r);
        }
        surfaces = found.ToArray();

        if (surfaces.Length == 0)
            Debug.LogWarning("LogBuoyancy: no objects with a Renderer found on the Water Layers. " +
                             "Check the Water Layers field and the layer of your water objects.", this);
    }

    // Water surface height at this point, taken from the water objects' bounds.
    public bool TryGetSurfaceHeight(Vector3 p, out float surfaceY)
    {
        surfaceY = float.NegativeInfinity;
        bool found = false;

        foreach (Renderer r in surfaces)
        {
            Bounds b = r.bounds;
            if (p.x < b.min.x || p.x > b.max.x || p.z < b.min.z || p.z > b.max.z) continue;
            if (b.max.y > surfaceY)
            {
                surfaceY = b.max.y;
                found = true;
            }
        }
        return found;
    }

    float WaveOffset(Vector3 p)
    {
        if (waveHeight <= 0f) return 0f;
        return Mathf.Sin(Time.time * waveSpeed + (p.x + p.z) * waveFrequency) * waveHeight;
    }

    // Area of the part of a circle (radius r) that is below a waterline, where h is
    // the height of that submerged part (0 = nothing, 2r = the whole circle).
    static float SegmentArea(float r, float h)
    {
        h = Mathf.Clamp(h, 0f, 2f * r);
        float d = r - h; // distance from the circle's center to the waterline
        return r * r * Mathf.Acos(Mathf.Clamp(d / r, -1f, 1f))
               - d * Mathf.Sqrt(Mathf.Max(2f * r * h - h * h, 0f));
    }

    void FixedUpdate()
    {
        float r = Mathf.Max(capsule.radius, 0.01f);
        float halfArea = Mathf.PI * r * r * 0.5f;
        float weightPerPoint = rb.mass * Physics.gravity.magnitude / pointCount;

        float submergedSum = 0f;

        for (int i = 0; i < pointCount; i++)
        {
            Vector3 p = transform.TransformPoint(LocalPoint(capsule, i));

            if (!TryGetSurfaceHeight(p, out float surfaceY)) continue;
            surfaceY += WaveOffset(p);

            // How much of this slice of the log is under the surface.
            float centerDepth = surfaceY - p.y;
            float h = Mathf.Clamp(r + centerDepth, 0f, 2f * r);
            if (h <= 0f) continue;

            submergedSum += h / (2f * r);

            // Buoyancy follows the submerged area: exactly the log's weight when half-sunk.
            float buoyancy = weightPerPoint * buoyancyScale * (SegmentArea(r, h) / halfArea);

            // Water resists vertical motion, which calms the bobbing.
            float verticalSpeed = rb.GetPointVelocity(p).y;
            float force = Mathf.Max(buoyancy - verticalSpeed * verticalDamping, 0f);

            rb.AddForceAtPosition(Vector3.up * force, p);
        }

        SubmergedFraction = submergedSum / pointCount;
        rb.linearDamping = Mathf.Lerp(airLinearDrag, waterLinearDrag, SubmergedFraction);
        rb.angularDamping = Mathf.Lerp(airAngularDrag, waterAngularDrag, SubmergedFraction);
    }

    Vector3 LocalPoint(CapsuleCollider cc, int index)
    {
        Vector3 axis = cc.direction == 0 ? Vector3.right
                     : cc.direction == 1 ? Vector3.up
                     : Vector3.forward;
        float half = Mathf.Max(cc.height * 0.5f - cc.radius, 0f);
        float t = pointCount > 1 ? (float)index / (pointCount - 1) : 0.5f;
        return cc.center + axis * Mathf.Lerp(-half, half, t);
    }

    void OnDrawGizmosSelected()
    {
        CapsuleCollider cc = GetComponent<CapsuleCollider>();
        if (cc == null) return;
        Gizmos.color = Color.cyan;
        for (int i = 0; i < pointCount; i++)
            Gizmos.DrawSphere(transform.TransformPoint(LocalPoint(cc, i)), 0.08f);
    }
}