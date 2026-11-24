
using UnityEngine;

public class LogRotator : MonoBehaviour
{
    public enum RotationDirection
    {
        Clockwise,
        Anticlockwise
    }

    [Header("Rotation Settings")]
    [Min(0f)]
    public float rotationSpeed = 50f;

    public RotationDirection direction = RotationDirection.Clockwise;

    private Vector3 rotationAxis = Vector3.right;

    void Start()
    {
        FindRotationAxis();
    }

    void Update()
    {
        float directionMultiplier =
            direction == RotationDirection.Clockwise ? 1f : -1f;

        transform.Rotate(
            rotationAxis,
            rotationSpeed * directionMultiplier * Time.deltaTime,
            Space.Self
        );
    }

    void FindRotationAxis()
    {
        MeshFilter[] meshes = GetComponentsInChildren<MeshFilter>();

        if (meshes.Length == 0)
            return;

        Bounds bounds = new Bounds();
        bool initialized = false;

        foreach (MeshFilter meshFilter in meshes)
        {
            if (meshFilter.sharedMesh == null)
                continue;

            Bounds meshBounds = meshFilter.sharedMesh.bounds;

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = meshBounds.center + Vector3.Scale(
                    meshBounds.extents,
                    new Vector3(
                        (i & 1) == 0 ? -1 : 1,
                        (i & 2) == 0 ? -1 : 1,
                        (i & 4) == 0 ? -1 : 1
                    )
                );

                Vector3 localPoint = transform.InverseTransformPoint(
                    meshFilter.transform.TransformPoint(corner)
                );

                if (!initialized)
                {
                    bounds = new Bounds(localPoint, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(localPoint);
                }
            }
        }

        if (!initialized)
            return;

        Vector3 size = bounds.size;

        if (size.x >= size.y && size.x >= size.z)
            rotationAxis = Vector3.right;
        else if (size.y >= size.x && size.y >= size.z)
            rotationAxis = Vector3.up;
        else
            rotationAxis = Vector3.forward;
    }
}
