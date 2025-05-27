// Copyright (c) 2025 Mustafa Garip
// This code is licensed for educational and non-commercial use only.
// Commercial use is not permitted without written permission.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Terrain))]
[RequireComponent(typeof(NavMeshSurface))]
public class ProceduralTerrainGenerator : MonoBehaviour
{
    public const float goldenRatio = 0.6180339887f;

    [Header("Terrain Settings")]
    public int terrainSize = 512;
    public float maxHeight = 50f;
    public int seed = 42;
    public bool useRandomSeed = true;

    [Header("Noise Settings")]
    [Range(0.001f, 0.1f)] public float baseFrequency = 0.005f;
    [Range(1, 8)] public int octaves = 2;
    [Range(0f, 1f)] public float persistence = 0.25f;
    [Range(1f, 6f)] public float lacunarity = 1.5f;
    [Range(10f, 1000f)] public float noiseScale = 200f;
    [Range(0, 50)] public int voronoiPointCount = 40;

    [Header("Climate Settings")]
    public float baseTemperature = 25f;
    [Range(1f, 40f)] public float temperatureFalloffFactor = 10f;
    [Range(0.1f, 20f)] public float humidityFrequency = 2.5f;

    [Header("Biome Settings")]
    public List<Biome> biomes = new();

    private Terrain terrain;
    private TerrainData terrainData;
    private List<Vector2> voronoiPoints;
    private float[,] cachedHeights;
    private int[,] biomeMap;

    private void Start()
    {
        if (useRandomSeed)
            seed = Random.Range(0, int.MaxValue);

        GenerateTerrain();
    }

    #region Generate Terrain

    [ContextMenu("Generate Terrain")]
    public void GenerateTerrain()
    {
        if (useRandomSeed)
            seed = Random.Range(0, int.MaxValue);

        terrain = GetComponent<Terrain>();
        terrainData = terrain.terrainData;

        terrainData.heightmapResolution = terrainSize + 1;
        terrainData.alphamapResolution = terrainSize + 1;
        terrainData.size = new Vector3(terrainSize, maxHeight, terrainSize);

        GenerateVoronoiPoints();

        cachedHeights = GenerateHeightmap();
        terrainData.SetHeights(0, 0, cachedHeights);

        GenerateBiomeMap();
        ApplyBiomes();
        StartCoroutine(GenerateNavMeshSurface());
    }

    #endregion

    #region Generate NavMeshSurface

    System.Collections.IEnumerator GenerateNavMeshSurface()
    {
        Terrain terrain = GetComponent<Terrain>();
        NavMeshSurface surface = GetComponent<NavMeshSurface>();

        if (terrain == null || surface == null)
        {
            Debug.LogWarning("Terrain or NavMeshSurface not found.");
            yield break;
        }

        List<GameObject> tempTrees = new();
        foreach (TreeInstance tree in terrain.terrainData.treeInstances)
        {
            TreePrototype proto = terrain.terrainData.treePrototypes[tree.prototypeIndex];
            GameObject prefab = proto.prefab;
            if (prefab == null) continue;

            Vector3 worldPos = Vector3.Scale(tree.position, terrain.terrainData.size) + terrain.transform.position;

            GameObject treeObj = Instantiate(prefab, worldPos, Quaternion.identity, transform);

            if (treeObj.GetComponent<Collider>() == null)
            {
                CapsuleCollider col = treeObj.AddComponent<CapsuleCollider>();
                col.radius = 0.5f;
                col.height = 4f;
                col.center = Vector3.up * 2f;
            }

            treeObj.layer = gameObject.layer;
            treeObj.isStatic = true;

            tempTrees.Add(treeObj);
        }

        yield return null;

        surface.RemoveData();
        surface.BuildNavMesh();

        yield return null;

        foreach (var obj in tempTrees)
        {
#if UNITY_EDITOR
            DestroyImmediate(obj);
#else
            Destroy(obj);
#endif
        }
    }

    #endregion

    #region Heights & Topography

    float[,] GenerateHeightmap()
    {
        float[,] heights = new float[terrainSize + 1, terrainSize + 1];

        System.Random rng = new System.Random(seed);
        Vector2[] offsets = new Vector2[octaves];
        for (int i = 0; i < octaves; i++)
        {
            offsets[i] = new Vector2(rng.Next(-100000, 100000), rng.Next(-100000, 100000));
        }

        for (int y = 0; y <= terrainSize; y++)
        {
            for (int x = 0; x <= terrainSize; x++)
            {
                float nx = (float)x / terrainSize;
                float ny = (float)y / terrainSize;

                float elevation = GenerateTopographicHeight(nx, ny, offsets);
                heights[y, x] = elevation;
            }
        }

        return heights;
    }

    float GenerateTopographicHeight(float x, float y, Vector2[] offsets)
    {
        float warpX = x + Mathf.PerlinNoise(x * 1.5f, y * 1.5f) * 0.1f;
        float warpY = y + Mathf.PerlinNoise(x * 1.5f + 100f, y * 1.5f + 100f) * 0.1f;

        float baseHeight = FBM(warpX, warpY, offsets);
        float modulation = FBM(x * 0.5f + 5.2f, y * 0.5f + 3.7f, offsets);
        baseHeight *= Mathf.Lerp(0.5f, 1.0f, modulation);

        float hills = Mathf.Abs(FBM(x * 2, y * 2, offsets));
        float ridged = 1f - Mathf.Abs(FBM(x * 3, y * 3, offsets) * 2f - 1f);

        float combined = 0f;
        if (baseHeight < 0.3f)
            combined = Mathf.Lerp(baseHeight, hills, baseHeight / 0.3f);
        else if (baseHeight < 0.6f)
            combined = Mathf.Lerp(hills, ridged, (baseHeight - 0.3f) / 0.3f);
        else
            combined = Mathf.Lerp(ridged, ridged * 1.2f, (baseHeight - 0.6f) / 0.4f);

        float voronoi = VoronoiNoise(x, y) * 0.1f;
        combined += voronoi;

        return Mathf.Clamp01(combined);
    }

    float FBM(float x, float y, Vector2[] offsets)
    {
        float amplitude = 1f;
        float frequency = baseFrequency;
        float noiseHeight = 0f;
        float maxAmp = 0f;

        for (int i = 0; i < octaves; i++)
        {
            float sampleX = x * noiseScale * frequency + offsets[i].x / 10000f;
            float sampleY = y * noiseScale * frequency + offsets[i].y / 10000f;

            float perlin = Mathf.PerlinNoise(sampleX, sampleY);
            noiseHeight += perlin * amplitude;

            maxAmp += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return noiseHeight / maxAmp;
    }

    float VoronoiNoise(float x, float y)
    {
        Vector2 pos = new Vector2(x, y);
        float closestDist = float.MaxValue;
        float secondClosestDist = float.MaxValue;

        foreach (var point in voronoiPoints)
        {
            float dist = Vector2.SqrMagnitude(pos - point);
            if (dist < closestDist)
            {
                secondClosestDist = closestDist;
                closestDist = dist;
            }
            else if (dist < secondClosestDist)
            {
                secondClosestDist = dist;
            }
        }

        float edgeDist = secondClosestDist - closestDist;
        return Mathf.Clamp01(edgeDist * 5f);
    }

    void GenerateVoronoiPoints()
    {
        voronoiPoints = new List<Vector2>();
        Random.InitState(seed);

        for (int i = 0; i < voronoiPointCount; i++)
        {
            voronoiPoints.Add(new Vector2(Random.value, Random.value));
        }
    }

    #endregion

    #region Biome System

    void GenerateBiomeMap()
    {
        biomeMap = new int[terrainSize + 1, terrainSize + 1];

        for (int y = 0; y <= terrainSize; y++)
        {
            for (int x = 0; x <= terrainSize; x++)
            {
                float nx = x / (float)terrainSize;
                float ny = y / (float)terrainSize;

                float warpFreq = 8f;
                float warpAmp = 0.05f;

                float warpX = nx + (Mathf.PerlinNoise(nx * warpFreq + seed * 0.1f, ny * warpFreq + seed * 0.1f) - 0.5f) * warpAmp;
                float warpY = ny + (Mathf.PerlinNoise((nx + 42f) * warpFreq + seed * 0.1f, (ny + 42f) * warpFreq + seed * 0.1f) - 0.5f) * warpAmp;

                float height = cachedHeights[y, x];
                float elevation = height * maxHeight;
                float temp = GetTemperature(elevation);
                float humidity = GetHumidity(warpX, warpY, height);

                int biomeIndex = FindClosestBiomeIndex(temp, humidity, height);
                biomeMap[y, x] = biomeIndex;
            }
        }
    }

    int FindClosestBiomeIndex(float temp, float humidity, float height)
    {
        int bestIndex = 0;
        float bestScore = float.MinValue;

        for (int i = 0; i < biomes.Count; i++)
        {
            float biomeTemp = (biomes[i].minTemp + biomes[i].maxTemp) / 2f;
            float biomeHum = (biomes[i].minHumidity + biomes[i].maxHumidity) / 2f;
            float biomeHeight = (biomes[i].minHeight + biomes[i].maxHeight) / 2f;

            float hDist = Mathf.Abs(humidity - biomeHum);
            float tDist = Mathf.Abs(temp - biomeTemp);
            float eDist = Mathf.Abs(height - biomeHeight);

            float score = -(hDist * 4f + tDist * 1f + eDist * 0.5f);

            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    void ApplyBiomes()
    {
        int resolution = terrainData.alphamapResolution;
        float[,,] splatmap = new float[resolution, resolution, biomes.Count];

        int blurRadius = 12;
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int biomeIndex = biomeMap[(int)(y * terrainSize / (float)resolution), (int)(x * terrainSize / (float)resolution)];

                float[] weights = new float[biomes.Count];
                float total = 0f;

                for (int by = -blurRadius; by <= blurRadius; by++)
                {
                    for (int bx = -blurRadius; bx <= blurRadius; bx++)
                    {
                        int nx = Mathf.Clamp(x + bx, 0, resolution - 1);
                        int ny = Mathf.Clamp(y + by, 0, resolution - 1);

                        int neighborBiome = biomeMap[(int)(ny * terrainSize / (float)resolution), (int)(nx * terrainSize / (float)resolution)];
                        float dist = Mathf.Sqrt(bx * bx + by * by) + 1f;
                        float weight = 1f / dist;
                        weights[neighborBiome] += weight;
                        total += weight;
                    }
                }

                for (int i = 0; i < biomes.Count; i++)
                    splatmap[y, x, i] = weights[i] / total;

            }
        }

        TerrainLayer[] layers = new TerrainLayer[biomes.Count];
        for (int i = 0; i < biomes.Count; i++)
        {
            if (biomes[i].texture != null) layers[i] = biomes[i].texture;
            else layers[i] = CreateFallbackLayer(biomes[i].fallbackColor);
        }

        terrainData.terrainLayers = layers;
        terrainData.SetAlphamaps(0, 0, splatmap);

        PlaceTrees();
        PlaceDetails();
    }

    TerrainLayer CreateFallbackLayer(Color color)
    {
        Texture2D tex = new Texture2D(2, 2);

        tex.SetPixels(new[] { color, color, color, color });
        tex.Apply();

        TerrainLayer tl = new TerrainLayer();

        tl.diffuseTexture = tex;
        tl.tileSize = Vector2.one * 10;

        tl.specular = Color.black;
        tl.metallic = 0f;
        tl.smoothness = 0f;

        return tl;
    }

    #region Get Temperature & Humidity

    float GetTemperature(float elevation)
    {
        return baseTemperature - (elevation / (200f / temperatureFalloffFactor));
    }

    float GetHumidity(float x, float y, float height)
    {
        float offsetX = Mathf.Abs(Mathf.Sin(seed * Mathf.PI)) % 1f;
        float offsetY = Mathf.Abs(Mathf.Cos(seed * goldenRatio)) % 1f;

        float humidity = Mathf.PerlinNoise(x * humidityFrequency + offsetX, y * humidityFrequency + offsetY);

        humidity *= Mathf.Lerp(1.0f, 0.5f, height);
        return Mathf.Clamp01(humidity);
    }

    #endregion

    #endregion

    #region Place Tree and Details Functions

    void PlaceTrees()
    {
        List<TreeInstance> treeInstances = new List<TreeInstance>();
        List<TreePrototype> prototypes = new List<TreePrototype>();
        Dictionary<GameObject, int> prefabToIndex = new Dictionary<GameObject, int>();

        foreach (var biome in biomes)
        {
            if (biome.treeSettings == null) continue;
            foreach (var tree in biome.treeSettings)
            {
                if (tree.prefab == null) continue;
                if (!prefabToIndex.ContainsKey(tree.prefab))
                {
                    TreePrototype proto = new TreePrototype { prefab = tree.prefab };
                    prefabToIndex[tree.prefab] = prototypes.Count;
                    prototypes.Add(proto);
                }
            }
        }

        System.Random rng = new System.Random(seed);
        int attempts = terrainSize * 10;
        float minTreeDistance = 8f;

        for (int i = 0; i < attempts; i++)
        {
            float normX = (float)rng.NextDouble();
            float normZ = (float)rng.NextDouble();
            int mapX = Mathf.RoundToInt(normX * terrainSize);
            int mapZ = Mathf.RoundToInt(normZ * terrainSize);

            if (mapX < 0 || mapX >= terrainSize || mapZ < 0 || mapZ >= terrainSize) continue;
            int biomeIndex = biomeMap[mapZ, mapX];
            Biome biome = biomes[biomeIndex];

            if (biome.treeSettings == null || biome.treeSettings.Length == 0) continue;

            TreePlacement selected = biome.treeSettings[rng.Next(0, biome.treeSettings.Length)];
            if (selected.prefab == null || rng.NextDouble() > selected.density) continue;

            float slope = terrainData.GetSteepness(normX, normZ);
            if (slope > 30f) continue;

            float worldHeight = terrainData.GetInterpolatedHeight(normX, normZ);
            Vector3 pos = new Vector3(normX, worldHeight / terrainData.size.y, normZ);

            bool tooClose = false;
            foreach (var existingTree in treeInstances)
            {
                Vector3 existingPos = existingTree.position;
                existingPos.y = 0;
                Vector3 newPos = pos;
                newPos.y = 0;

                if (Vector3.Distance(existingPos, newPos) < minTreeDistance / terrainSize)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;

            int protoIndex = prefabToIndex[selected.prefab];

            float baseScale = Mathf.Lerp(selected.minScale, selected.maxScale, (float)rng.NextDouble());
            float heightMultiplier = Mathf.Lerp(0.95f, 1.05f, (float)rng.NextDouble());

            TreeInstance tree = new TreeInstance
            {
                prototypeIndex = protoIndex,
                position = pos,
                widthScale = baseScale,
                heightScale = baseScale * heightMultiplier,
                color = Color.white,
                lightmapColor = Color.white
            };
            treeInstances.Add(tree);
        }

        terrainData.treePrototypes = prototypes.ToArray();
        terrainData.treeInstances = treeInstances.ToArray();
    }

    void PlaceDetails()
    {
        terrainData.detailPrototypes = new DetailPrototype[0];
        List<DetailPrototype> prototypes = new List<DetailPrototype>();
        List<int> protoBiomeIndices = new List<int>();

        for (int b = 0; b < biomes.Count; b++)
        {
            var biome = biomes[b];
            if (biome.detailSettings == null) continue;

            foreach (var setting in biome.detailSettings)
            {
                if (setting.texture == null) continue;

                DetailPrototype proto = new DetailPrototype
                {
                    usePrototypeMesh = false,
                    prototypeTexture = setting.texture,
                    renderMode = DetailRenderMode.GrassBillboard,
                    minWidth = setting.minWidth,
                    maxWidth = setting.maxWidth,
                    minHeight = setting.minHeight,
                    maxHeight = setting.maxHeight,
                    healthyColor = Color.white,
                    dryColor = Color.gray
                };
                prototypes.Add(proto);
                protoBiomeIndices.Add(b);
            }
        }

        terrainData.detailPrototypes = prototypes.ToArray();

        int res = terrainData.detailResolution;
        for (int i = 0; i < prototypes.Count; i++)
        {
            int[,] layer = new int[res, res];
            int biomeIndexForThisLayer = protoBiomeIndices[i];

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float u = x / (float)(res - 1);
                    float v = y / (float)(res - 1);
                    float slope = terrainData.GetSteepness(u, v);
                    if (slope > 30f) continue;

                    int mapX = Mathf.RoundToInt(u * terrainSize);
                    int mapZ = Mathf.RoundToInt(v * terrainSize);
                    if (mapX < 0 || mapX >= terrainSize || mapZ < 0 || mapZ >= terrainSize) continue;

                    int currentBiomeIndex = biomeMap[mapZ, mapX];
                    if (currentBiomeIndex != biomeIndexForThisLayer) continue;

                    var detailArr = biomes[currentBiomeIndex].detailSettings;
                    if (detailArr == null || detailArr.Length == 0) continue;

                    int localDetailIndex = i;
                    int globalOffset = 0;
                    for (int b = 0; b < biomeIndexForThisLayer; b++)
                        if (biomes[b].detailSettings != null)
                            globalOffset += biomes[b].detailSettings.Length;

                    int localIndex = i - globalOffset;
                    if (localIndex < 0 || localIndex >= detailArr.Length) continue;

                    if (Random.value < detailArr[localIndex].density)
                        layer[y, x] = 1;
                }
            }

            terrainData.SetDetailLayer(0, 0, i, layer);
        }
    }

    #endregion

    #region Tree and Detail Structs and Biome Class

    [System.Serializable]
    public struct TreePlacement
    {
        public GameObject prefab;
        [Range(0f, 1f)] public float density;
        public float minScale;
        public float maxScale;
    }

    [System.Serializable]
    public struct DetailPlacement
    {
        public Texture2D texture;
        [Range(0f, 1f)] public float density;
        public float minWidth;
        public float maxWidth;
        public float minHeight;
        public float maxHeight;
    }

    [System.Serializable]
    public class Biome
    {
        public string name;
        public float minHeight, maxHeight;
        public float minTemp, maxTemp;
        public float minHumidity, maxHumidity;
        public TerrainLayer texture;
        public Color fallbackColor = Color.gray;
        public TreePlacement[] treeSettings;
        public DetailPlacement[] detailSettings;
    }

    #endregion

    #region Humidity Gizmo

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (terrainData == null) return;

        int step = 32;
        for (int y = 0; y < terrainData.alphamapHeight; y += step)
        {
            for (int x = 0; x < terrainData.alphamapWidth; x += step)
            {
                float normX = x / (float)(terrainData.alphamapWidth - 1);
                float normY = y / (float)(terrainData.alphamapHeight - 1);
                float height = terrainData.GetHeight(x, y) / terrainData.size.y;
                float humidity = GetHumidity(normX, normY, height);

                Vector3 worldPos = new Vector3(
                    normX * terrainData.size.x,
                    height * terrainData.size.y + 1f,
                    normY * terrainData.size.z
                ) + terrain.transform.position;

                Gizmos.color = Color.Lerp(Color.red, Color.green, humidity);
                float cubeHeight = 10 * Mathf.Lerp(.5f, 5f, humidity);
                Gizmos.DrawCube(worldPos + new Vector3(0, cubeHeight / 2f, 0), new Vector3(5f, cubeHeight, 5f));
            }
        }
    }
#endif
}

#endregion
