using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Put this on a ground mesh. It scatters grass and trees ON the actual surface of that mesh
// (not just inside its bounding box), skips walls and steep slopes, and can merge all the grass
// into a few meshes so it costs almost nothing to draw.
public class EnvironmentScatterer : MonoBehaviour
{
    [Header("Grass Prefabs")]
    public List<GameObject> grassPrefabs = new List<GameObject>();
    public int grassCount = 100;

    [Header("Tree Prefabs")]
    public List<GameObject> treePrefabs = new List<GameObject>();
    public int treeCount = 50;

    [Header("Random Scale")]
    public float minGrassScale = 0.7f;
    public float maxGrassScale = 1.2f;

    public float minTreeScale = 0.8f;
    public float maxTreeScale = 1.3f;

    [Header("Placement")]
    public float heightOffset = 0.05f;
    [Tooltip("Same seed = same layout every time. Change it for a new layout.")]
    public int seed = 12345;
    [Tooltip("Only surfaces facing up count as ground. 1 = perfectly flat only, 0 = everything, walls included.")]
    [Range(0f, 1f)] public float minUpDot = 0.7f;
    [Tooltip("0 = always stand straight up, 1 = tilt to match the ground slope.")]
    [Range(0f, 1f)] public float grassAlignToSurface = 0.5f;
    [Range(0f, 1f)] public float treeAlignToSurface = 0f;
    [Tooltip("Minimum distance between trees in meters. 0 = they may overlap.")]
    public float treeMinSpacing = 3f;

    [Header("Optimization")]
    [Tooltip("Merge all grass into a few meshes (very few draw calls). Colliders and scripts on the grass prefab are dropped.")]
    public bool combineGrass = true;
    [Tooltip("Same for trees. Leave off if your trees need colliders.")]
    public bool combineTrees = false;
    [Tooltip("Grass should never have colliders. Only used when Combine Grass is off.")]
    public bool removeGrassColliders = true;
    public bool grassCastShadows = false;
    public bool treeCastShadows = true;

    struct Placement
    {
        public GameObject prefab;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
    }

    struct Part
    {
        public Mesh mesh;
        public int subMesh;
        public Material material;
        public Matrix4x4 local;
        public int vertexCount;
    }

    // The ground surface as a list of up-facing triangles in world space.
    class Surface
    {
        public Vector3[] a, b, c, normal;
        public float[] cumulativeArea;
        public int count;
        public float totalArea;
    }

    const int MaxCombinedVertices = 60000;

    // Remembers what this component generated, so clearing never touches anything else.
    [SerializeField, HideInInspector] GameObject generatedRoot;

    void OnValidate()
    {
        grassCount = Mathf.Max(0, grassCount);
        treeCount = Mathf.Max(0, treeCount);
        maxGrassScale = Mathf.Max(minGrassScale, maxGrassScale);
        maxTreeScale = Mathf.Max(minTreeScale, maxTreeScale);
        treeMinSpacing = Mathf.Max(0f, treeMinSpacing);
    }

    [ContextMenu("Generate Environment")]
    public void GenerateEnvironment()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();

        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogError("EnvironmentScatterer: attach this script to a ground mesh!", this);
            return;
        }

        // Read the ground first, so a failure here never wipes the old result.
        Surface surface = BuildSurface(meshFilter);
        if (surface == null) return;
        if (surface.count == 0)
        {
            Debug.LogWarning("EnvironmentScatterer: no up-facing ground found. Try lowering Min Up Dot.", this);
            return;
        }

        ClearEnvironment();

        System.Random rng = new System.Random(seed);

        generatedRoot = new GameObject("Generated_Environment (" + name + ")");
        Transform root = generatedRoot.transform;

        List<Placement> grass = Plan(grassPrefabs, grassCount, minGrassScale, maxGrassScale,
                                     grassAlignToSurface, 0f, surface, rng);
        List<Placement> trees = Plan(treePrefabs, treeCount, minTreeScale, maxTreeScale,
                                     treeAlignToSurface, treeMinSpacing, surface, rng);

        Build(grass, combineGrass, removeGrassColliders, grassCastShadows, root, "Grass");
        Build(trees, combineTrees, false, treeCastShadows, root, "Trees");

        Debug.Log("Environment generated! Grass: " + grass.Count + "/" + grassCount +
                  " | Trees: " + trees.Count + "/" + treeCount, this);
    }

    [ContextMenu("Generate With New Seed")]
    public void GenerateWithNewSeed()
    {
        seed = Random.Range(0, 1000000);
        GenerateEnvironment();
    }

    [ContextMenu("Clear Environment")]
    public void ClearEnvironment()
    {
        if (generatedRoot == null) return;

        // Free the merged meshes we created, then remove the objects.
        foreach (MeshFilter mf in generatedRoot.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh != null && mf.sharedMesh.name.StartsWith("EnvCombined_"))
                SafeDestroy(mf.sharedMesh);
        }

        SafeDestroy(generatedRoot);
        generatedRoot = null;
    }

    // ---------------------------------------------------------------- surface sampling

    Surface BuildSurface(MeshFilter meshFilter)
    {
        Mesh mesh = meshFilter.sharedMesh;

        if (!mesh.isReadable)
        {
            Debug.LogError("EnvironmentScatterer: the ground mesh is not readable. " +
                           "Select the mesh asset and tick Read/Write in its import settings.", this);
            return null;
        }

        Vector3[] vertices = mesh.vertices;
        int[] indices = mesh.triangles;
        Matrix4x4 toWorld = transform.localToWorldMatrix;
        bool mirrored = toWorld.determinant < 0f;

        int triangleCount = indices.Length / 3;
        Surface s = new Surface
        {
            a = new Vector3[triangleCount],
            b = new Vector3[triangleCount],
            c = new Vector3[triangleCount],
            normal = new Vector3[triangleCount],
            cumulativeArea = new float[triangleCount]
        };

        float total = 0f;
        int n = 0;

        for (int t = 0; t < triangleCount; t++)
        {
            Vector3 p0 = toWorld.MultiplyPoint3x4(vertices[indices[t * 3]]);
            Vector3 p1 = toWorld.MultiplyPoint3x4(vertices[indices[t * 3 + 1]]);
            Vector3 p2 = toWorld.MultiplyPoint3x4(vertices[indices[t * 3 + 2]]);

            Vector3 cross = Vector3.Cross(p1 - p0, p2 - p0);
            float area = cross.magnitude * 0.5f;
            if (area < 1e-6f) continue;

            Vector3 normal = cross / (area * 2f);
            if (mirrored) normal = -normal;

            // Skip walls and steep faces.
            if (normal.y < minUpDot) continue;

            total += area;
            s.a[n] = p0;
            s.b[n] = p1;
            s.c[n] = p2;
            s.normal[n] = normal;
            s.cumulativeArea[n] = total;
            n++;
        }

        s.count = n;
        s.totalArea = total;
        return s;
    }

    // Picks a random point on the surface. Bigger triangles are picked more often,
    // so the spread is even over the whole ground.
    static void SamplePoint(Surface s, System.Random rng, out Vector3 point, out Vector3 normal)
    {
        float target = (float)(rng.NextDouble() * s.totalArea);

        int lo = 0;
        int hi = s.count - 1;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (s.cumulativeArea[mid] < target) lo = mid + 1;
            else hi = mid;
        }

        float u = (float)rng.NextDouble();
        float v = (float)rng.NextDouble();
        if (u + v > 1f)
        {
            u = 1f - u;
            v = 1f - v;
        }

        point = s.a[lo] + (s.b[lo] - s.a[lo]) * u + (s.c[lo] - s.a[lo]) * v;
        normal = s.normal[lo];
    }

    // ---------------------------------------------------------------- planning

    List<Placement> Plan(List<GameObject> prefabs, int count, float minScale, float maxScale,
                         float align, float minSpacing, Surface surface, System.Random rng)
    {
        List<Placement> result = new List<Placement>();
        if (prefabs == null || count <= 0) return result;

        List<GameObject> valid = new List<GameObject>();
        foreach (GameObject prefab in prefabs)
            if (prefab != null) valid.Add(prefab);
        if (valid.Count == 0) return result;

        bool useSpacing = minSpacing > 0f;
        Dictionary<Vector2Int, List<Vector3>> grid =
            useSpacing ? new Dictionary<Vector2Int, List<Vector3>>() : null;

        int maxAttempts = count * (useSpacing ? 10 : 1);

        for (int attempt = 0; attempt < maxAttempts && result.Count < count; attempt++)
        {
            SamplePoint(surface, rng, out Vector3 point, out Vector3 normal);

            if (useSpacing && TooClose(grid, point, minSpacing)) continue;

            GameObject chosen = valid[rng.Next(valid.Count)];

            Vector3 up = Vector3.Slerp(Vector3.up, normal, align);
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, up) *
                                  Quaternion.Euler(0f, RandomRange(rng, 0f, 360f), 0f);

            float scale = RandomRange(rng, minScale, maxScale);

            result.Add(new Placement
            {
                prefab = chosen,
                position = point + up * heightOffset,
                rotation = rotation,
                scale = chosen.transform.localScale * scale
            });

            if (useSpacing) AddToGrid(grid, point, minSpacing);
        }

        if (result.Count < count)
            Debug.LogWarning("EnvironmentScatterer: only found room for " + result.Count + " of " + count +
                             " objects. Lower the spacing or the count.", this);

        return result;
    }

    static float RandomRange(System.Random rng, float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

    static Vector2Int Cell(Vector3 p, float size)
    {
        return new Vector2Int(Mathf.FloorToInt(p.x / size), Mathf.FloorToInt(p.z / size));
    }

    static void AddToGrid(Dictionary<Vector2Int, List<Vector3>> grid, Vector3 p, float spacing)
    {
        Vector2Int cell = Cell(p, spacing);
        if (!grid.TryGetValue(cell, out List<Vector3> list))
        {
            list = new List<Vector3>();
            grid[cell] = list;
        }
        list.Add(p);
    }

    // Only checks the 9 nearby cells, so it stays fast with thousands of objects.
    static bool TooClose(Dictionary<Vector2Int, List<Vector3>> grid, Vector3 p, float spacing)
    {
        Vector2Int cell = Cell(p, spacing);
        float sqrSpacing = spacing * spacing;

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                if (!grid.TryGetValue(new Vector2Int(cell.x + dx, cell.y + dz), out List<Vector3> list))
                    continue;

                foreach (Vector3 other in list)
                {
                    float ox = other.x - p.x;
                    float oz = other.z - p.z;
                    if (ox * ox + oz * oz < sqrSpacing) return true;
                }
            }
        }
        return false;
    }

    // ---------------------------------------------------------------- building

    void Build(List<Placement> placements, bool combine, bool stripColliders,
               bool castShadows, Transform root, string label)
    {
        if (placements.Count == 0) return;

        Transform group = new GameObject(label).transform;
        group.SetParent(root, false);

        if (combine) BuildCombined(placements, group, castShadows);
        else BuildInstances(placements, group, stripColliders, castShadows);
    }

    void BuildInstances(List<Placement> placements, Transform group, bool stripColliders, bool castShadows)
    {
        ShadowCastingMode shadowMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

        foreach (Placement p in placements)
        {
            // The group sits at the world origin with no scale, so the ground's own scale
            // never distorts the objects.
            GameObject obj = Instantiate(p.prefab, p.position, p.rotation, group);
            obj.name = p.prefab.name;
            obj.transform.localScale = p.scale;

            if (stripColliders)
            {
                foreach (Collider col in obj.GetComponentsInChildren<Collider>())
                    SafeDestroy(col);
            }

            foreach (Renderer r in obj.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = shadowMode;

#if UNITY_EDITOR
            obj.isStatic = true; // lets Unity batch these when you build
#endif
        }
    }

    void BuildCombined(List<Placement> placements, Transform group, bool castShadows)
    {
        Dictionary<GameObject, List<Part>> partsByPrefab = new Dictionary<GameObject, List<Part>>();
        Dictionary<Material, List<CombineInstance>> buckets = new Dictionary<Material, List<CombineInstance>>();
        Dictionary<Material, int> bucketVertices = new Dictionary<Material, int>();
        Dictionary<Material, int> bucketIndex = new Dictionary<Material, int>();

        Matrix4x4 groupInverse = group.worldToLocalMatrix;

        foreach (Placement p in placements)
        {
            if (!partsByPrefab.TryGetValue(p.prefab, out List<Part> parts))
            {
                parts = GetParts(p.prefab);
                partsByPrefab[p.prefab] = parts;
            }

            Matrix4x4 placementMatrix = groupInverse * Matrix4x4.TRS(p.position, p.rotation, p.scale);

            foreach (Part part in parts)
            {
                if (!buckets.TryGetValue(part.material, out List<CombineInstance> list))
                {
                    list = new List<CombineInstance>();
                    buckets[part.material] = list;
                    bucketVertices[part.material] = 0;
                    bucketIndex[part.material] = 0;
                }

                // Keep each merged mesh under the vertex limit that phones handle well.
                if (list.Count > 0 && bucketVertices[part.material] + part.vertexCount > MaxCombinedVertices)
                {
                    FlushBucket(part.material, list, group, bucketIndex[part.material], castShadows);
                    bucketIndex[part.material]++;
                    list.Clear();
                    bucketVertices[part.material] = 0;
                }

                list.Add(new CombineInstance
                {
                    mesh = part.mesh,
                    subMeshIndex = part.subMesh,
                    transform = placementMatrix * part.local
                });
                bucketVertices[part.material] += part.vertexCount;
            }
        }

        foreach (KeyValuePair<Material, List<CombineInstance>> pair in buckets)
        {
            if (pair.Value.Count > 0)
                FlushBucket(pair.Key, pair.Value, group, bucketIndex[pair.Key], castShadows);
        }
    }

    // Every mesh + material in a prefab, with its position relative to the prefab root.
    List<Part> GetParts(GameObject prefab)
    {
        List<Part> parts = new List<Part>();
        Matrix4x4 rootInverse = prefab.transform.worldToLocalMatrix;

        foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>())
        {
            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            if (mr == null || mf.sharedMesh == null) continue;

            Material[] materials = mr.sharedMaterials;
            if (materials.Length == 0) continue;

            Matrix4x4 relative = rootInverse * mf.transform.localToWorldMatrix;

            for (int s = 0; s < mf.sharedMesh.subMeshCount; s++)
            {
                Material material = materials[Mathf.Min(s, materials.Length - 1)];
                if (material == null) continue;

                parts.Add(new Part
                {
                    mesh = mf.sharedMesh,
                    subMesh = s,
                    material = material,
                    local = relative,
                    vertexCount = mf.sharedMesh.GetSubMesh(s).vertexCount
                });
            }
        }

        if (parts.Count == 0)
            Debug.LogWarning("EnvironmentScatterer: '" + prefab.name + "' has no MeshFilter + MeshRenderer, " +
                             "so it cannot be combined. Turn off the Combine option for it.", this);

        return parts;
    }

    void FlushBucket(Material material, List<CombineInstance> list, Transform group, int index, bool castShadows)
    {
        Mesh combined = new Mesh { name = "EnvCombined_" + material.name };
        combined.CombineMeshes(list.ToArray(), true, true);

        GameObject go = new GameObject("Combined_" + material.name + "_" + index);
        go.transform.SetParent(group, false);

        go.AddComponent<MeshFilter>().sharedMesh = combined;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

#if UNITY_EDITOR
        go.isStatic = true;
#endif
    }

    static void SafeDestroy(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }
}