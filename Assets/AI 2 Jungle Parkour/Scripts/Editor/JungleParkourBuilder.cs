using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Builds the jungle parkour course into the open scene: sculpted terrain with a river ravine and a sunken pit,
/// temple ruins, the jumping course with coins and checkpoints, jungle foliage, lighting, fog and post processing.
/// Running it again rebuilds everything; generated textures, materials and meshes go to the Generated folder.
/// </summary>
public static class JungleParkourBuilder
{
    private const string Folder = "Assets/AI 2 Jungle Parkour";
    private const string GeneratedFolder = Folder + "/Generated";
    private const string MeshAssetPath = GeneratedFolder + "/Meshes.asset";
    private const string RootName = "Jungle Course";
    private const float TerrainSize = 160f;
    private const float TerrainHeight = 60f;
    private const float Plateau = 6f;
    // World size of one texture repeat on stone boxes
    private const float StoneTile = 2.5f;

    private class Plat
    {
        public readonly string Name;
        public readonly float X, Z, Top, SizeX, SizeZ;
        public readonly bool Ledge;

        public Plat(string name, float x, float z, float top, float sizeX, float sizeZ, bool ledge = false)
        {
            Name = name; X = x; Z = z; Top = top; SizeX = sizeX; SizeZ = sizeZ; Ledge = ledge;
        }
    }

    private struct Area
    {
        public Vector2 Center, Half;
        public Area(float x, float z, float sizeX, float sizeZ) { Center = new Vector2(x, z); Half = new Vector2(sizeX, sizeZ) * 0.5f; }
    }

    private static readonly Plat[] Platforms =
    {
        new Plat("Start Plaza", 80f, 26f, 6.4f, 10f, 8f),
        // Ravine crossing
        new Plat("Pillar A1", 79f, 34f, 6.4f, 1.6f, 1.6f),
        new Plat("Pillar A2", 81f, 37.5f, 6.4f, 1.6f, 1.6f),
        new Plat("Pillar A3", 79.5f, 41f, 6.4f, 1.6f, 1.6f),
        new Plat("Pillar A4", 81.5f, 44.5f, 6.4f, 1.6f, 1.6f),
        new Plat("Pillar A5", 80f, 48f, 6.4f, 1.6f, 1.6f),
        new Plat("Pillar A6", 79.5f, 55f, 6.4f, 1.6f, 1.6f),
        new Plat("Pillar A7", 81f, 58.5f, 6.4f, 1.6f, 1.6f),
        new Plat("Pillar A8", 79.5f, 62f, 6.4f, 1.6f, 1.6f),
        new Plat("Far Bank Landing", 80f, 67f, 6.4f, 6f, 5f),
        // Temple stairs
        new Plat("Stair B1", 85f, 67f, 7.0f, 2f, 2f),
        new Plat("Stair B2", 88.2f, 67.5f, 7.7f, 2f, 2f),
        new Plat("Stair B3", 91.4f, 67f, 8.4f, 2f, 2f),
        new Plat("Stair B4", 94.6f, 67.5f, 9.1f, 2f, 2f),
        new Plat("Wall Landing", 98.3f, 67f, 9.8f, 3f, 3f),
        // Ledges along the temple wall
        new Plat("Ledge C1", 101f, 71f, 9.8f, 1.6f, 2f, true),
        new Plat("Ledge C2", 101f, 74.5f, 9.8f, 1.6f, 2f, true),
        new Plat("Ledge C3", 101f, 78f, 9.8f, 1.6f, 2f, true),
        new Plat("Ledge C4", 101f, 81.5f, 9.8f, 1.6f, 2f, true),
        new Plat("Ledge C5", 101f, 85f, 9.8f, 1.6f, 2f, true),
        new Plat("Wall End Landing", 100.5f, 89.2f, 9.8f, 3f, 3f),
        // Over the pit
        new Plat("Broken Column", 91.2f, 89.2f, 9.8f, 1.8f, 1.8f),
        new Plat("Pit Landing", 81.5f, 89.2f, 9.8f, 3f, 3f),
        // Rising pillars to the pyramid
        new Plat("Pillar E1", 81.5f, 93f, 10.5f, 1.8f, 1.8f),
        new Plat("Pillar E2", 79f, 95.8f, 11.2f, 1.8f, 1.8f),
        new Plat("Pillar E3", 76f, 97.7f, 11.9f, 1.8f, 1.8f),
        new Plat("Pillar E4", 72.8f, 98.7f, 12.6f, 1.8f, 1.8f),
    };

    // Pyramid tiers: centre x, centre z, size, top height
    private static readonly Vector4[] PyramidTiers =
    {
        new Vector4(64f, 108f, 16f, 8.5f),
        new Vector4(66f, 106f, 12f, 10.5f),
        new Vector4(67f, 105f, 9f, 12f),
        new Vector4(68f, 104f, 6f, 13.3f),
    };

    private static readonly Vector2 PitCenter = new Vector2(90f, 89f);
    private const float WallX = 102.55f, WallZ = 76.5f, WallThickness = 1.5f, WallLength = 21f, WallHeight = 16f;

    private static readonly List<Area> courseAreas = new List<Area>();
    private static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();
    private static System.Random rng;
    private static Terrain terrain;

    private const string CharacterPrefab = "Assets/AI 1 Character/Warrior2/Player.prefab";
    // Highest ledge the character walks up without jumping, e.g. the start plaza from the ground
    private const float StepHeight = 0.5f;

    private static Material stone, stoneDark, bark, leaves, leavesDark, gold, water, mud, waterfall, wood, rune, flame, fern, doorway, teleportParticle;
    private static Transform waterfallTransform;

    [MenuItem("Tools/Jungle Parkour/Build Course")]
    public static void Build()
    {
        rng = new System.Random(20261028);
        meshCache.Clear();
        EnsureFolder(Folder, GeneratedFolder);
        AssetDatabase.DeleteAsset(MeshAssetPath);

        DefineCourseAreas();
        CreateMaterials();
        BuildTerrain();

        var old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);
        Transform root = new GameObject(RootName).transform;

        Transform course = Group(root, "Course");
        Transform coins = Group(root, "Coins");
        BuildCourse(course, coins);
        ParkourCheckpoint start = BuildCheckpoints(Group(root, "Checkpoints"));
        BuildPyramid(Group(root, "Pyramid"), coins);
        BuildDecor(Group(root, "Ruins"));
        BuildWater(Group(root, "Water"));
        BuildFoliage(Group(root, "Foliage"));
        SetupLighting();
        SetUpCharacter();
        SetupPlayer(start);

        var manager = new GameObject("Course Manager", typeof(ParkourCourse), typeof(RespawnTransition), typeof(JungleAmbience));
        manager.transform.SetParent(root);
        SetField(manager.GetComponent<ParkourCourse>(), "startCheckpoint", start);
        SetField(manager.GetComponent<RespawnTransition>(), "particleMaterial", teleportParticle);
        var ambience = manager.GetComponent<JungleAmbience>();
        SetField(ambience, "waterfall", waterfallTransform);
        var river = new SerializedObject(ambience);
        SerializedProperty points = river.FindProperty("riverPoints");
        points.arraySize = 6;
        for (int i = 0; i < points.arraySize; i++)
        {
            float x = 32f + i * 19f;
            points.GetArrayElementAtIndex(i).vector3Value = new Vector3(x, 2.4f, RavineCenter(x));
        }
        river.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"Jungle parkour built: {Object.FindObjectsByType<JungleCoin>(FindObjectsSortMode.None).Length} coins.");
    }

    // ---------------------------------------------------------------- Layout

    private static void DefineCourseAreas()
    {
        courseAreas.Clear();
        foreach (Plat p in Platforms) courseAreas.Add(new Area(p.X, p.Z, p.SizeX, p.SizeZ));
        courseAreas.Add(new Area(80f, 51.5f, 2.4f, 2.4f));
        courseAreas.Add(new Area(95.5f, 89.2f, 9f, 1f));
        courseAreas.Add(new Area(86.6f, 89.2f, 5f, 1f));
        courseAreas.Add(new Area(WallX, WallZ, WallThickness + 1f, WallLength));
        Vector4 baseTier = PyramidTiers[0];
        courseAreas.Add(new Area(baseTier.x, baseTier.y, baseTier.z, baseTier.z));
    }

    private static float DistanceToCourse(float x, float z)
    {
        float best = float.MaxValue;
        foreach (Area a in courseAreas)
        {
            float dx = Mathf.Max(Mathf.Abs(x - a.Center.x) - a.Half.x, 0f);
            float dz = Mathf.Max(Mathf.Abs(z - a.Center.y) - a.Half.y, 0f);
            best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dz * dz));
        }
        return best;
    }

    private static float Smooth(float from, float to, float value)
    {
        float t = Mathf.Clamp01((value - from) / (to - from));
        return t * t * (3f - 2f * t);
    }

    private static float Fbm(float x, float z)
    {
        float sum = 0f, amplitude = 0.5f, frequency = 1f;
        for (int i = 0; i < 4; i++)
        {
            sum += Mathf.PerlinNoise(x * frequency + 31.7f * i, z * frequency + 17.3f * i) * amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }
        return sum / 0.9375f;
    }

    private static float RavineCenter(float x) => 49f + 3f * Mathf.Sin(x * 0.04f);

    /// <summary>Terrain height in metres: a flat plateau around the course, rolling jungle further out,
    /// cliffs along the edges, a river ravine across the middle and a sunken pit.</summary>
    private static float HeightAt(float x, float z)
    {
        float n = Fbm(x * 0.03f, z * 0.03f);
        float distance = DistanceToCourse(x, z);
        float far = Smooth(4f, 16f, distance);
        float h = Plateau + (n - 0.5f) * 0.25f * (1f - far) + far * (n - 0.4f) * 4f;

        float edge = Mathf.Min(Mathf.Min(x, TerrainSize - x), Mathf.Min(z, TerrainSize - z));
        h += Smooth(34f, 6f, edge) * Smooth(6f, 22f, distance) * (14f + Fbm(x * 0.05f + 7f, z * 0.05f + 3f) * 22f);

        float ravine = Smooth(13f, 8f, Mathf.Abs(z - RavineCenter(x))) * Smooth(18f, 28f, x) * Smooth(142f, 132f, x);
        h = Mathf.Lerp(h, 0.8f + Fbm(x * 0.1f, z * 0.1f) * 0.6f, ravine);

        float pit = Smooth(13f, 9f, Vector2.Distance(new Vector2(x, z), PitCenter));
        h = Mathf.Lerp(h, 1f, pit);
        return Mathf.Max(h, 0.2f);
    }

    // ---------------------------------------------------------------- Terrain

    private static void BuildTerrain()
    {
        terrain = Object.FindFirstObjectByType<Terrain>();
        if (terrain == null) terrain = Terrain.CreateTerrainGameObject(null).GetComponent<Terrain>();
        terrain.name = "Jungle Terrain";
        terrain.transform.position = Vector3.zero;

        string dataPath = Folder + "/Jungle Terrain.asset";
        var data = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
        if (data == null)
        {
            data = new TerrainData();
            AssetDatabase.CreateAsset(data, dataPath);
        }

        const int res = 257;
        data.heightmapResolution = res;
        data.size = new Vector3(TerrainSize, TerrainHeight, TerrainSize);
        var heights = new float[res, res];
        for (int zi = 0; zi < res; zi++)
            for (int xi = 0; xi < res; xi++)
                heights[zi, xi] = HeightAt(xi / (res - 1f) * TerrainSize, zi / (res - 1f) * TerrainSize) / TerrainHeight;
        data.SetHeights(0, 0, heights);

        data.terrainLayers = new[]
        {
            Layer("Jungle Grass", GrassTexture(), 5f),
            Layer("Jungle Mud", MudTexture(), 4f),
            Layer("Mossy Rock", RockTexture(), 7f),
        };
        PaintLayers(data);
        PaintGrass(data);

        terrain.terrainData = data;
        var terrainCollider = terrain.GetComponent<TerrainCollider>();
        if (terrainCollider != null) terrainCollider.terrainData = data;
        terrain.basemapDistance = 250f;
        terrain.detailObjectDistance = 70f;
        terrain.detailObjectDensity = 1f;
        terrain.heightmapPixelError = 4f;
        EditorUtility.SetDirty(data);
    }

    private static TerrainLayer Layer(string name, Texture2D texture, float tile)
    {
        string path = $"{GeneratedFolder}/{name}.terrainlayer";
        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (layer == null)
        {
            layer = new TerrainLayer();
            AssetDatabase.CreateAsset(layer, path);
        }
        layer.diffuseTexture = texture;
        layer.tileSize = new Vector2(tile, tile);
        layer.smoothness = 0.1f;
        EditorUtility.SetDirty(layer);
        return layer;
    }

    private static void PaintLayers(TerrainData data)
    {
        const int res = 256;
        data.alphamapResolution = res;
        var maps = new float[res, res, 3];
        for (int zi = 0; zi < res; zi++)
        {
            for (int xi = 0; xi < res; xi++)
            {
                float nx = (xi + 0.5f) / res, nz = (zi + 0.5f) / res;
                float x = nx * TerrainSize, z = nz * TerrainSize;
                float h = data.GetInterpolatedHeight(nx, nz);
                float steep = data.GetSteepness(nx, nz);
                float noise = Mathf.PerlinNoise(x * 0.15f, z * 0.15f);

                float rock = Smooth(26f, 38f, steep + noise * 6f);
                float path = Smooth(3.5f, 0.5f, DistanceToCourse(x, z)) * 0.7f;
                float mud = Mathf.Max(Smooth(4.5f, 2.6f, h), path, Smooth(0.62f, 0.8f, noise) * 0.6f) * (1f - rock);
                float grass = Mathf.Max(0f, 1f - rock - mud);
                float total = rock + mud + grass;
                maps[zi, xi, 0] = grass / total;
                maps[zi, xi, 1] = mud / total;
                maps[zi, xi, 2] = rock / total;
            }
        }
        data.SetAlphamaps(0, 0, maps);
    }

    private static void PaintGrass(TerrainData data)
    {
        const int res = 512;
        data.SetDetailResolution(res, 16);
        data.detailPrototypes = new[]
        {
            new DetailPrototype
            {
                prototypeTexture = GrassBladeTexture(),
                renderMode = DetailRenderMode.Grass,
                usePrototypeMesh = false,
                minWidth = 0.7f, maxWidth = 1.3f,
                minHeight = 0.5f, maxHeight = 1.1f,
                healthyColor = new Color(0.45f, 0.6f, 0.25f),
                dryColor = new Color(0.55f, 0.55f, 0.3f),
                noiseSpread = 0.3f,
            },
        };

        var density = new int[res, res];
        float[,,] maps = data.GetAlphamaps(0, 0, data.alphamapResolution, data.alphamapResolution);
        int alphaRes = data.alphamapResolution;
        for (int zi = 0; zi < res; zi++)
        {
            for (int xi = 0; xi < res; xi++)
            {
                float nx = (xi + 0.5f) / res, nz = (zi + 0.5f) / res;
                float grass = maps[(int)(nz * alphaRes), (int)(nx * alphaRes), 0];
                float h = data.GetInterpolatedHeight(nx, nz);
                if (h < 2.8f || grass < 0.4f) continue;
                density[zi, xi] = (int)(grass * 200f);
            }
        }
        data.SetDetailLayer(0, 0, 0, density);
    }

    // ---------------------------------------------------------------- Course

    private static void BuildCourse(Transform parent, Transform coins)
    {
        foreach (Plat p in Platforms)
        {
            if (p.Ledge) Ledge(parent, p);
            else Platform(parent, p.Name, p.X, p.Z, p.Top, p.SizeX, p.SizeZ);
        }

        // Pillar that sinks into the river and rises again
        var sinking = new GameObject("Sinking Pillar").transform;
        sinking.SetParent(parent);
        sinking.position = new Vector3(80f, 6.4f, 51.5f);
        GameObject cap = Box("Cap", sinking, new Vector3(80f, 6.15f, 51.5f), new Vector3(2.4f, 0.5f, 2.4f), stone);
        Box("Shaft", sinking, new Vector3(80f, 1.9f, 51.5f), new Vector3(2.1f, 8f, 2.1f), stoneDark);
        var sliding = sinking.gameObject.AddComponent<SlidingPlatform>();
        SetField(sliding, "top", cap.GetComponent<Collider>());
        SetField(sliding, "travel", new Vector3(0f, -2.2f, 0f));
        SetField(sliding, "period", 5f);

        // Temple wall with the ledges, pilasters on the back and a broken top
        Transform wall = Group(parent, "Temple Wall");
        Box("Wall", wall, new Vector3(WallX, WallHeight / 2f, WallZ), new Vector3(WallThickness, WallHeight, WallLength), stone);
        for (float z = WallZ - WallLength / 2f + 1f; z < WallZ + WallLength / 2f; z += 4f)
            Box("Pilaster", wall, new Vector3(WallX + 0.95f, WallHeight / 2f, z), new Vector3(0.4f, WallHeight, 1.1f), stoneDark);
        for (float z = WallZ - WallLength / 2f + 0.75f; z < WallZ + WallLength / 2f; z += 1.5f)
        {
            float h = Range(0f, 1.6f);
            if (h > 0.4f) Box("Merlon", wall, new Vector3(WallX, WallHeight + h / 2f, z), new Vector3(WallThickness, h, 1.2f), stone);
        }
        for (int i = 0; i < 18; i++)
        {
            float z = Range(WallZ - WallLength / 2f + 0.5f, WallZ + WallLength / 2f - 0.5f);
            float length = Range(3f, 7f);
            Cylinder("Vine", wall, new Vector3(WallX - WallThickness / 2f - 0.06f, WallHeight - length / 2f, z), new Vector3(0.08f, length / 2f, 0.08f), Quaternion.identity, leavesDark, false);
        }

        // Fallen columns across the pit, resting on short supports
        FallenColumn(parent, "Fallen Column 1", new Vector3(95.55f, 9.4f, 89.2f), 6.9f);
        FallenColumn(parent, "Fallen Column 2", new Vector3(86.6f, 9.4f, 89.2f), 4f);
        Platform(parent, "Support", 88f, 89.2f, 9f, 0.9f, 0.9f);
        Platform(parent, "Support", 85.2f, 89.2f, 9f, 0.9f, 0.9f);

        // Start gate
        Transform gate = Group(parent, "Start Gate");
        Box("Gate Column", gate, new Vector3(75.8f, 9.4f, 29.4f), new Vector3(1.2f, 6f, 1.2f), stone);
        Box("Gate Column", gate, new Vector3(84.2f, 9.4f, 29.4f), new Vector3(1.2f, 6f, 1.2f), stone);
        Box("Gate Lintel", gate, new Vector3(80f, 12.9f, 29.4f), new Vector3(10f, 1f, 1.5f), stoneDark);

        Torch(parent, new Vector3(75.7f, 6.4f, 22.4f));
        Torch(parent, new Vector3(84.3f, 6.4f, 22.4f));
        Torch(parent, new Vector3(77.4f, 6.4f, 69.2f));
        Torch(parent, new Vector3(99.5f, 9.8f, 65.8f));

        // Coins: one floating over every stepping spot, plus a few over the gaps to grab mid-jump
        Coin(coins, 80f, 7.4f, 27.5f);
        Coin(coins, 80f, 7.4f, 29.5f);
        foreach (Plat p in Platforms)
        {
            bool landing = p.SizeX >= 3f;
            if (!landing) Coin(coins, p.X, p.Top + 1f, p.Z);
        }
        Coin(coins, 80f, 7.4f, 51.5f);
        Coin(coins, 80.25f, 7.9f, 39.25f);
        Coin(coins, 80.75f, 7.9f, 46.25f);
        Coin(coins, 80.25f, 7.9f, 60.25f);
        Coin(coins, 101f, 11.4f, 76.25f);
        Coin(coins, 101f, 11.4f, 83.25f);
        Coin(coins, 97f, 10.8f, 89.2f);
        Coin(coins, 94f, 10.8f, 89.2f);
        Coin(coins, 89.4f, 11.3f, 89.2f);
        Coin(coins, 86.6f, 10.8f, 89.2f);
    }

    private static ParkourCheckpoint BuildCheckpoints(Transform parent)
    {
        ParkourCheckpoint start = Checkpoint(parent, "Checkpoint Start", new Vector3(80f, 6.4f, 24f), 0f, 3.9f);
        Checkpoint(parent, "Checkpoint Far Bank", new Vector3(80f, 6.4f, 67f), 90f, 3.9f);
        Checkpoint(parent, "Checkpoint Wall", new Vector3(98.3f, 9.8f, 67f), 0f, 7.3f);
        Checkpoint(parent, "Checkpoint Pit", new Vector3(100.5f, 9.8f, 89.2f), -90f, 7.3f);
        Checkpoint(parent, "Checkpoint Pyramid", new Vector3(81.5f, 9.8f, 89.2f), 0f, 9f);
        return start;
    }

    private static void BuildPyramid(Transform parent, Transform coins)
    {
        foreach (Vector4 tier in PyramidTiers)
        {
            Box("Tier", parent, new Vector3(tier.x, tier.w / 2f, tier.y), new Vector3(tier.z, tier.w, tier.z), stone);
            // Carved band just under each tier's top edge
            Box("Band", parent, new Vector3(tier.x, tier.w - 0.45f, tier.y), new Vector3(tier.z + 0.2f, 0.3f, tier.z + 0.2f), stoneDark, false);
        }

        Vector4 baseTier = PyramidTiers[0];
        float face = baseTier.y - baseTier.z / 2f;
        Box("Doorway", parent, new Vector3(baseTier.x, Plateau + 1.75f, face - 0.05f), new Vector3(2.4f, 3.5f, 0.1f), doorway, false);
        Box("Door Lintel", parent, new Vector3(baseTier.x, Plateau + 3.8f, face - 0.3f), new Vector3(3.6f, 0.6f, 0.6f), stoneDark);
        Box("Guardian", parent, new Vector3(baseTier.x - 2.6f, Plateau + 1.2f, face - 0.6f), new Vector3(1f, 2.4f, 1f), stoneDark);
        Box("Guardian Head", parent, new Vector3(baseTier.x - 2.6f, Plateau + 2.8f, face - 0.6f), new Vector3(0.8f, 0.8f, 0.9f), stone);
        Box("Guardian", parent, new Vector3(baseTier.x + 2.6f, Plateau + 1.2f, face - 0.6f), new Vector3(1f, 2.4f, 1f), stoneDark);
        Box("Guardian Head", parent, new Vector3(baseTier.x + 2.6f, Plateau + 2.8f, face - 0.6f), new Vector3(0.8f, 0.8f, 0.9f), stone);

        Vector4 top = PyramidTiers[PyramidTiers.Length - 1];
        Torch(parent, new Vector3(top.x - 2.6f, top.w, top.y + 2.6f));
        Torch(parent, new Vector3(top.x + 2.6f, top.w, top.y + 2.6f));

        Chest(parent, new Vector3(top.x, top.w, top.y + 1f));

        Coin(coins, top.x + 2f, top.w + 1f, top.y - 2f);
        Coin(coins, top.x - 1.5f, top.w + 1f, top.y - 0.5f);
        Coin(coins, top.x + 1.5f, top.w + 1f, top.y + 2f);
    }

    private const string ChestPrefab = Folder + "/Prefabs/Treasure Chest.prefab";

    /// <summary>Instance of the Treasure Chest prefab; falls back to a box-built chest if the prefab is missing.</summary>
    private static void Chest(Transform parent, Vector3 position)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChestPrefab);
        if (prefab != null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.position = position;
            return;
        }

        // Treasure chest with a lid hinged at the back
        var chest = new GameObject("Treasure Chest").transform;
        chest.SetParent(parent);
        chest.position = position;
        Box("Chest Body", chest, chest.position + new Vector3(0f, 0.35f, 0f), new Vector3(1.3f, 0.7f, 0.85f), wood);
        Box("Chest Band", chest, chest.position + new Vector3(-0.4f, 0.36f, 0f), new Vector3(0.12f, 0.72f, 0.87f), gold, false);
        Box("Chest Band", chest, chest.position + new Vector3(0.4f, 0.36f, 0f), new Vector3(0.12f, 0.72f, 0.87f), gold, false);
        var lid = new GameObject("Lid").transform;
        lid.SetParent(chest);
        lid.position = chest.position + new Vector3(0f, 0.7f, 0.425f);
        Box("Lid Top", lid, lid.position + new Vector3(0f, 0.15f, -0.425f), new Vector3(1.32f, 0.3f, 0.87f), wood, false);
        Box("Lid Trim", lid, lid.position + new Vector3(0f, 0.15f, -0.86f), new Vector3(1.34f, 0.32f, 0.06f), gold, false);
        Box("Treasure", chest, chest.position + new Vector3(0f, 0.68f, 0f), new Vector3(1.1f, 0.06f, 0.65f), gold, false);
        var glow = new GameObject("Treasure Glow", typeof(Light)).GetComponent<Light>();
        glow.transform.SetParent(chest);
        glow.transform.position = chest.position + new Vector3(0f, 1.2f, 0f);
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.8f, 0.35f);
        glow.range = 6f;
        glow.intensity = 3f;
        var goal = chest.gameObject.AddComponent<TreasureChest>();
        SetField(goal, "lid", lid);
        chest.gameObject.AddComponent<AI_2_Jungle_Parkour.Scripts.CourseObjective>();
    }

    // ---------------------------------------------------------------- Scenery

    private static void BuildDecor(Transform parent)
    {
        // Broken and fallen columns, block piles
        int placed = 0;
        for (int attempt = 0; attempt < 400 && placed < 26; attempt++)
        {
            float x = Range(45f, 115f), z = Range(14f, 120f);
            float ground = terrain.SampleHeight(new Vector3(x, 0f, z));
            if (ground < 5f || ground > 8f || DistanceToCourse(x, z) < 3f) continue;
            placed++;

            int kind = rng.Next(3);
            if (kind == 0)
            {
                float h = Range(1f, 4.5f), r = Range(0.5f, 0.85f);
                var c = Cylinder("Broken Column", parent, new Vector3(x, ground + h / 2f - 0.2f, z), new Vector3(r * 2f, h / 2f, r * 2f),
                    Quaternion.Euler(Range(-6f, 6f), Range(0f, 360f), Range(-6f, 6f)), stone, true);
                if (rng.NextDouble() < 0.5) Sphere("Moss", parent, c.transform.position + Vector3.up * (h / 2f), new Vector3(r * 2.4f, 0.4f, r * 2.4f), leavesDark);
            }
            else if (kind == 1)
            {
                float length = Range(3f, 6f), r = Range(0.45f, 0.75f);
                Cylinder("Fallen Column", parent, new Vector3(x, ground + r * 0.6f, z), new Vector3(r * 2f, length / 2f, r * 2f),
                    Quaternion.Euler(0f, Range(0f, 360f), 90f + Range(-4f, 4f)), stone, true);
            }
            else
            {
                float y = ground - 0.2f;
                int count = rng.Next(2, 4);
                for (int i = 0; i < count; i++)
                {
                    var size = new Vector3(Range(0.8f, 1.8f), Range(0.5f, 1f), Range(0.8f, 1.6f));
                    var block = Box("Ruin Block", parent, new Vector3(x + Range(-0.3f, 0.3f), y + size.y / 2f, z + Range(-0.3f, 0.3f)), size, i % 2 == 0 ? stone : stoneDark);
                    block.transform.rotation = Quaternion.Euler(Range(-4f, 4f), Range(0f, 360f), Range(-4f, 4f));
                    y += size.y * 0.9f;
                }
            }
        }

        // Giant fallen tree bridging the ravine further west
        Cylinder("Fallen Giant", parent, new Vector3(45f, 6.1f, 52f), new Vector3(1.8f, 17f, 1.8f), Quaternion.Euler(90f, 8f, 0f), bark, true);
        Sphere("Fallen Giant Roots", parent, new Vector3(42.7f, 6.4f, 35.6f), new Vector3(4.5f, 3.5f, 2f), bark);
    }

    private static void BuildWater(Transform parent)
    {
        GameObject river = Box("River", parent, new Vector3(80f, 2.15f, 49f), new Vector3(130f, 0.1f, 32f), water, false);
        river.AddComponent<TextureScroll>();
        SetField(river.GetComponent<TextureScroll>(), "speed", new Vector2(0.03f, 0.01f));
        Cylinder("Pit Mud", parent, new Vector3(PitCenter.x, 2f, PitCenter.y), new Vector3(24f, 0.02f, 24f), Quaternion.identity, mud, false);

        for (int i = 0; i < 14; i++)
        {
            float x = Range(30f, 130f);
            float z = RavineCenter(x) + Range(-4f, 4f);
            if (Mathf.Abs(x - 80f) < 4f) continue;
            Sphere("River Rock", parent, new Vector3(x, 2f, z), new Vector3(Range(1f, 2.6f), Range(0.8f, 1.6f), Range(1f, 2.4f)), stoneDark, true);
        }

        // Waterfall pouring down the cliff at the east end of the ravine
        var up = new Vector3(10f, 24f, 0f).normalized;
        var forward = new Vector3(24f, -10f, 0f).normalized;
        var fall = GameObject.CreatePrimitive(PrimitiveType.Quad);
        fall.name = "Waterfall";
        fall.transform.SetParent(parent);
        fall.transform.SetPositionAndRotation(new Vector3(141f, 14f, RavineCenter(141f)), Quaternion.LookRotation(forward, up));
        fall.transform.localScale = new Vector3(8f, 27f, 1f);
        Object.DestroyImmediate(fall.GetComponent<Collider>());
        fall.GetComponent<Renderer>().sharedMaterial = waterfall;
        fall.AddComponent<TextureScroll>();
        waterfallTransform = fall.transform;
        Sphere("Waterfall Foam", parent, new Vector3(136.5f, 2.2f, RavineCenter(136.5f)), new Vector3(9f, 1.2f, 6f), waterfall);
    }

    private static void BuildFoliage(Transform parent)
    {
        Transform trees = Group(parent, "Trees");
        Transform plants = Group(parent, "Plants");

        int treeCount = 0;
        for (int attempt = 0; attempt < 3000 && treeCount < 230; attempt++)
        {
            float x = Range(4f, TerrainSize - 4f), z = Range(4f, TerrainSize - 4f);
            float ground = terrain.SampleHeight(new Vector3(x, 0f, z));
            if (ground < 4f || DistanceToCourse(x, z) < 6f) continue;
            bool giant = rng.NextDouble() < 0.12;
            Tree(trees, new Vector3(x, ground - 0.3f, z), giant ? Range(20f, 28f) : Range(10f, 17f), giant ? Range(0.9f, 1.3f) : Range(0.35f, 0.7f));
            treeCount++;
        }

        int bushCount = 0;
        for (int attempt = 0; attempt < 4000 && bushCount < 420; attempt++)
        {
            float x = Range(4f, TerrainSize - 4f), z = Range(4f, TerrainSize - 4f);
            float ground = terrain.SampleHeight(new Vector3(x, 0f, z));
            if (ground < 2.6f || DistanceToCourse(x, z) < 2.5f) continue;
            float w = Range(1f, 3f);
            Sphere("Bush", plants, new Vector3(x, ground + 0.1f, z), new Vector3(w, Range(0.7f, 1.6f), w * Range(0.7f, 1.3f)), rng.NextDouble() < 0.5 ? leaves : leavesDark);
            bushCount++;
        }

        int fernCount = 0;
        for (int attempt = 0; attempt < 4000 && fernCount < 380; attempt++)
        {
            float x = Range(20f, 140f), z = Range(10f, 140f);
            float ground = terrain.SampleHeight(new Vector3(x, 0f, z));
            if (ground < 2.4f || DistanceToCourse(x, z) < 1.5f) continue;
            Fern(plants, new Vector3(x, ground, z), Range(0.9f, 1.8f));
            fernCount++;
        }
    }

    private static void Tree(Transform parent, Vector3 basePosition, float height, float radius)
    {
        var tree = new GameObject("Jungle Tree").transform;
        tree.SetParent(parent);
        tree.SetPositionAndRotation(basePosition, Quaternion.Euler(Range(-5f, 5f), Range(0f, 360f), Range(-5f, 5f)));

        Cylinder("Trunk", tree, tree.TransformPoint(0f, height / 2f, 0f), new Vector3(radius * 2f, height / 2f, radius * 2f), tree.rotation, bark, true);
        Cylinder("Flare", tree, tree.TransformPoint(0f, 0.6f, 0f), new Vector3(radius * 3.4f, 0.8f, radius * 3.4f), tree.rotation, bark, false);
        int roots = rng.Next(3, 6);
        for (int i = 0; i < roots; i++)
        {
            float angle = i * 360f / roots + Range(-20f, 20f);
            Quaternion rotation = tree.rotation * Quaternion.Euler(0f, angle, 0f);
            Primitive(PrimitiveType.Cube, "Buttress Root", tree, basePosition + rotation * new Vector3(0f, radius * 1.3f, radius * 1.4f),
                new Vector3(0.22f, radius * 4f, radius * 3f), rotation * Quaternion.Euler(-25f, 0f, 0f), bark, false);
        }

        // Lumpy crown from many overlapping clumps at different heights
        int clumps = rng.Next(6, 10);
        float spread = height * 0.24f;
        for (int i = 0; i < clumps; i++)
        {
            Vector3 offset = new Vector3(Range(-spread, spread), height + Range(-height * 0.18f, height * 0.08f), Range(-spread, spread));
            float w = Range(height * 0.2f, height * 0.34f);
            Sphere("Canopy", tree, tree.TransformPoint(offset), new Vector3(w, w * Range(0.5f, 0.75f), w * Range(0.8f, 1.2f)), rng.NextDouble() < 0.5 ? leaves : leavesDark);
        }
        int branches = rng.Next(2, 4);
        for (int i = 0; i < branches; i++)
        {
            Quaternion rotation = tree.rotation * Quaternion.Euler(0f, Range(0f, 360f), 0f) * Quaternion.Euler(Range(40f, 60f), 0f, 0f);
            float length = spread * 1.1f;
            Vector3 start = tree.TransformPoint(0f, height * Range(0.7f, 0.85f), 0f);
            Cylinder("Branch", tree, start + rotation * Vector3.up * (length / 2f), new Vector3(radius, length / 2f, radius), rotation, bark, false);
        }

        int vines = rng.Next(0, 4);
        for (int i = 0; i < vines; i++)
        {
            float length = Range(height * 0.3f, height * 0.6f);
            Vector3 top = tree.TransformPoint(Range(-spread, spread), height - 1f, Range(-spread, spread));
            Cylinder("Vine", tree, top - Vector3.up * (length / 2f), new Vector3(0.07f, length / 2f, 0.07f), Quaternion.identity, leavesDark, false);
        }
    }

    private static void Fern(Transform parent, Vector3 position, float size)
    {
        var plant = new GameObject("Fern").transform;
        plant.SetParent(parent);
        plant.position = position;
        int fronds = rng.Next(4, 7);
        float yaw = Range(0f, 360f);
        for (int i = 0; i < fronds; i++)
        {
            var frond = GameObject.CreatePrimitive(PrimitiveType.Quad);
            frond.name = "Frond";
            Object.DestroyImmediate(frond.GetComponent<Collider>());
            frond.transform.SetParent(plant);
            Quaternion rotation = Quaternion.Euler(0f, yaw + i * 360f / fronds, 0f) * Quaternion.Euler(Range(35f, 60f), 0f, 0f);
            frond.transform.SetPositionAndRotation(position + rotation * new Vector3(0f, size * 0.5f, 0f), rotation);
            frond.transform.localScale = new Vector3(size * 0.45f, size, 1f);
            frond.GetComponent<Renderer>().sharedMaterial = fern;
        }
    }

    // ---------------------------------------------------------------- Lighting

    private static void SetupLighting()
    {
        Light sun = null;
        foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (light.type == LightType.Directional) sun = light;
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(42f, -50f, 0f);
            sun.color = new Color(1f, 0.9f, 0.72f);
            sun.intensity = 1.7f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            RenderSettings.sun = sun;
        }

        var sky = LoadOrCreateMaterial("Jungle Sky", Shader.Find("Skybox/Procedural"));
        sky.SetFloat("_SunSize", 0.03f);
        sky.SetFloat("_AtmosphereThickness", 1.35f);
        sky.SetColor("_SkyTint", new Color(0.55f, 0.68f, 0.6f));
        sky.SetColor("_GroundColor", new Color(0.18f, 0.24f, 0.16f));
        sky.SetFloat("_Exposure", 1.15f);
        RenderSettings.skybox = sky;

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.5f, 0.62f, 0.55f);
        RenderSettings.ambientEquatorColor = new Color(0.32f, 0.4f, 0.27f);
        RenderSettings.ambientGroundColor = new Color(0.12f, 0.14f, 0.08f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.013f;
        RenderSettings.fogColor = new Color(0.6f, 0.68f, 0.56f);

        Camera camera = Camera.main;
        if (camera != null) camera.farClipPlane = 600f;

        var volume = Object.FindFirstObjectByType<Volume>();
        if (volume == null) return;
        string profilePath = GeneratedFolder + "/Jungle Volume Profile.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile == null)
        {
            // Copy the scene's existing profile so the original scene keeps its look
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
            if (volume.sharedProfile != null)
            {
                foreach (VolumeComponent component in volume.sharedProfile.components)
                {
                    VolumeComponent copy = Object.Instantiate(component);
                    copy.name = component.GetType().Name;
                    profile.components.Add(copy);
                    AssetDatabase.AddObjectToAsset(copy, profile);
                }
            }
        }

        Override<Tonemapping>(profile).mode.Override(TonemappingMode.ACES);
        Bloom bloom = Override<Bloom>(profile);
        bloom.intensity.Override(0.7f);
        bloom.threshold.Override(1f);
        bloom.scatter.Override(0.7f);
        ColorAdjustments color = Override<ColorAdjustments>(profile);
        color.postExposure.Override(0.35f);
        color.contrast.Override(14f);
        color.saturation.Override(12f);
        color.colorFilter.Override(new Color(1f, 0.97f, 0.9f));
        Vignette vignette = Override<Vignette>(profile);
        vignette.intensity.Override(0.28f);
        vignette.smoothness.Override(0.45f);
        EditorUtility.SetDirty(profile);
        volume.sharedProfile = profile;
    }

    private static T Override<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (profile.TryGet(out T component)) return component;
        component = profile.Add<T>(true);
        component.name = typeof(T).Name;
        AssetDatabase.AddObjectToAsset(component, profile);
        return component;
    }

    /// <summary>
    /// Gives the player prefab a normal step height, procedural step smoothing with a foot lift, and footstep sounds,
    /// and turns on IK Pass for the base animator layer so the foot lift can run.
    /// </summary>
    [MenuItem("Tools/Jungle Parkour/Set Up Character")]
    public static void SetUpCharacter()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefab);
        if (prefab != null)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(CharacterPrefab);
            AddCharacterFeatures(contents);
            PrefabUtility.SaveAsPrefabAsset(contents, CharacterPrefab);
            PrefabUtility.UnloadPrefabContents(contents);
        }

        // Also covers a scene player that isn't an instance of the prefab or overrides its values
        var controller = Object.FindFirstObjectByType<ThirdPersonController>();
        if (controller != null) AddCharacterFeatures(controller.gameObject);
    }

    private static void AddCharacterFeatures(GameObject player)
    {
        var characterController = player.GetComponent<CharacterController>();
        if (characterController == null) return;
        characterController.stepOffset = StepHeight;
        if (player.GetComponent<PlayerSounds>() == null) player.AddComponent<PlayerSounds>();

        var animator = player.GetComponentInChildren<Animator>();
        if (animator == null) return;
        if (animator.GetComponent<StepSmoother>() == null) animator.gameObject.AddComponent<StepSmoother>();
        if (animator.runtimeAnimatorController is UnityEditor.Animations.AnimatorController animatorController)
        {
            UnityEditor.Animations.AnimatorControllerLayer[] layers = animatorController.layers;
            if (!layers[0].iKPass)
            {
                layers[0].iKPass = true;
                animatorController.layers = layers;
                EditorUtility.SetDirty(animatorController);
            }
        }
    }

    private static void SetupPlayer(ParkourCheckpoint start)
    {
        var controller = Object.FindFirstObjectByType<ThirdPersonController>();
        if (controller == null) return;
        var characterController = controller.GetComponent<CharacterController>();
        float feetOffset = characterController.center.y - characterController.height / 2f;
        controller.transform.SetPositionAndRotation(start.transform.position - Vector3.up * (feetOffset * controller.transform.lossyScale.y) + Vector3.up * 0.05f, start.transform.rotation);

        Camera camera = Camera.main;
        if (camera != null)
            camera.transform.SetPositionAndRotation(start.transform.position + new Vector3(0f, 2.5f, -4.5f), Quaternion.Euler(15f, 0f, 0f));
    }

    // ---------------------------------------------------------------- Pieces

    private static void Platform(Transform parent, string name, float x, float z, float top, float sizeX, float sizeZ)
    {
        var platform = new GameObject(name).transform;
        platform.SetParent(parent);
        platform.position = new Vector3(x, top, z);
        float bottom = Mathf.Min(terrain.SampleHeight(new Vector3(x, 0f, z)) - 1.5f, top - 1f);
        Box("Cap", platform, new Vector3(x, top - 0.25f, z), new Vector3(sizeX, 0.5f, sizeZ), stone);
        float shaft = top - 0.5f - bottom;
        Box("Shaft", platform, new Vector3(x, bottom + shaft / 2f, z), new Vector3(sizeX * 0.86f, shaft, sizeZ * 0.86f), stoneDark);
    }

    private static void Ledge(Transform parent, Plat p)
    {
        var ledge = new GameObject(p.Name).transform;
        ledge.SetParent(parent);
        ledge.position = new Vector3(p.X, p.Top, p.Z);
        Box("Ledge", ledge, new Vector3(p.X, p.Top - 0.25f, p.Z), new Vector3(p.SizeX, 0.5f, p.SizeZ), stone);
        Box("Bracket", ledge, new Vector3(p.X + p.SizeX * 0.25f, p.Top - 0.9f, p.Z), new Vector3(p.SizeX * 0.5f, 0.8f, p.SizeZ * 0.6f), stoneDark);
    }

    private static void FallenColumn(Transform parent, string name, Vector3 center, float length)
    {
        var column = Cylinder(name, parent, center, new Vector3(0.8f, length / 2f, 0.8f), Quaternion.Euler(0f, 0f, 90f), stone, false);
        // A flat box collider so the top is walkable instead of a rounded capsule
        var box = column.AddComponent<BoxCollider>();
        box.size = new Vector3(1f, 2f, 0.9f);
    }

    private static void Torch(Transform parent, Vector3 floor)
    {
        var torch = new GameObject("Torch").transform;
        torch.SetParent(parent);
        torch.position = floor;
        Cylinder("Post", torch, floor + Vector3.up * 0.65f, new Vector3(0.22f, 0.65f, 0.22f), Quaternion.identity, stoneDark, false);
        Cylinder("Bowl", torch, floor + Vector3.up * 1.35f, new Vector3(0.5f, 0.08f, 0.5f), Quaternion.identity, stoneDark, false);
        Sphere("Flame", torch, floor + Vector3.up * 1.55f, new Vector3(0.32f, 0.45f, 0.32f), flame);
        var light = new GameObject("Torch Light", typeof(Light)).GetComponent<Light>();
        light.transform.SetParent(torch);
        light.transform.position = floor + Vector3.up * 1.9f;
        light.type = LightType.Point;
        light.color = new Color(1f, 0.6f, 0.25f);
        light.range = 9f;
        light.intensity = 4f;
        light.gameObject.AddComponent<TorchFlicker>();
    }

    private const string CoinPrefab = Folder + "/Prefabs/Coin_mayan.prefab";

    /// <summary>Instance of the Coin_mayan prefab (which carries its own JungleCoin); falls back to a flattened gold cylinder if the prefab is missing.</summary>
    private static void Coin(Transform parent, float x, float y, float z)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CoinPrefab);
        if (prefab != null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.position = new Vector3(x, y, z);
            if (instance.GetComponent<JungleCoin>() == null) instance.AddComponent<JungleCoin>();
            return;
        }
        var coin = Cylinder("Coin", parent, new Vector3(x, y, z), new Vector3(0.55f, 0.035f, 0.55f), Quaternion.Euler(90f, 0f, 0f), gold, false);
        coin.AddComponent<JungleCoin>();
    }

    private static ParkourCheckpoint Checkpoint(Transform parent, string name, Vector3 position, float yaw, float killY)
    {
        var point = new GameObject(name).transform;
        point.SetParent(parent);
        point.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        var disc = Cylinder("Rune", point, position + Vector3.up * 0.015f, new Vector3(1.3f, 0.015f, 1.3f), Quaternion.identity, rune, false);
        var checkpoint = point.gameObject.AddComponent<ParkourCheckpoint>();
        SetField(checkpoint, "killY", killY);
        SetField(checkpoint, "rune", disc.GetComponent<Renderer>());
        return checkpoint;
    }

    private static Transform Group(Transform parent, string name)
    {
        var group = new GameObject(name).transform;
        group.SetParent(parent);
        return group;
    }

    /// <summary>Box with UVs in world units, so stone textures keep the same scale on every size of block.</summary>
    private static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material material, bool collider = true)
    {
        var box = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        box.transform.SetParent(parent);
        box.transform.position = center;
        box.GetComponent<MeshFilter>().sharedMesh = BoxMesh(size);
        box.GetComponent<MeshRenderer>().sharedMaterial = material;
        if (collider) box.AddComponent<BoxCollider>().size = size;
        GameObjectUtility.SetStaticEditorFlags(box, StaticEditorFlags.BatchingStatic);
        return box;
    }

    private static GameObject Cylinder(string name, Transform parent, Vector3 position, Vector3 scale, Quaternion rotation, Material material, bool collider)
        => Primitive(PrimitiveType.Cylinder, name, parent, position, scale, rotation, material, collider);

    private static GameObject Sphere(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool collider = false)
        => Primitive(PrimitiveType.Sphere, name, parent, position, scale, Quaternion.identity, material, collider);

    private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Quaternion rotation, Material material, bool collider)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent);
        go.transform.SetPositionAndRotation(position, rotation);
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }

    private static Mesh BoxMesh(Vector3 size)
    {
        string key = $"{size.x:F2}x{size.y:F2}x{size.z:F2}";
        if (meshCache.TryGetValue(key, out Mesh mesh)) return mesh;

        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();

        // u x w == normal, so the triangles below face outwards
        void Face(Vector3 normal, Vector3 u, Vector3 w, float sizeU, float sizeW)
        {
            int i = vertices.Count;
            Vector3 center = Vector3.Scale(normal, size) * 0.5f;
            Vector3 hu = u * (sizeU * 0.5f), hw = w * (sizeW * 0.5f);
            vertices.Add(center - hu - hw);
            vertices.Add(center + hu - hw);
            vertices.Add(center + hu + hw);
            vertices.Add(center - hu + hw);
            for (int k = 0; k < 4; k++) normals.Add(normal);
            float tu = sizeU / StoneTile, tw = sizeW / StoneTile;
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(tu, 0f));
            uvs.Add(new Vector2(tu, tw));
            uvs.Add(new Vector2(0f, tw));
            triangles.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }

        Face(Vector3.right, Vector3.back, Vector3.up, size.z, size.y);
        Face(Vector3.left, Vector3.forward, Vector3.up, size.z, size.y);
        Face(Vector3.forward, Vector3.right, Vector3.up, size.x, size.y);
        Face(Vector3.back, Vector3.left, Vector3.up, size.x, size.y);
        Face(Vector3.up, Vector3.right, Vector3.back, size.x, size.z);
        Face(Vector3.down, Vector3.right, Vector3.forward, size.x, size.z);

        mesh = new Mesh { name = "Box " + key };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();

        if (AssetDatabase.LoadMainAssetAtPath(MeshAssetPath) == null) AssetDatabase.CreateAsset(mesh, MeshAssetPath);
        else AssetDatabase.AddObjectToAsset(mesh, MeshAssetPath);
        meshCache[key] = mesh;
        return mesh;
    }

    // ---------------------------------------------------------------- Materials and textures

    private static void CreateMaterials()
    {
        Texture2D stoneTexture = StoneTexture();
        stone = Lit("Temple Stone", new Color(0.82f, 0.82f, 0.76f), stoneTexture, 0.15f);
        stoneDark = Lit("Temple Stone Dark", new Color(0.6f, 0.62f, 0.56f), stoneTexture, 0.1f);
        bark = Lit("Bark", Color.white, BarkTexture(), 0.1f);
        leaves = Lit("Leaves", new Color(0.7f, 0.85f, 0.62f), LeafTexture(), 0.15f);
        leavesDark = Lit("Leaves Dark", new Color(0.45f, 0.58f, 0.4f), LeafTexture(), 0.1f);
        wood = Lit("Chest Wood", new Color(0.55f, 0.35f, 0.2f), BarkTexture(), 0.25f);
        doorway = Lit("Doorway", new Color(0.02f, 0.02f, 0.015f), null, 0f);

        gold = Lit("Gold", new Color(1f, 0.78f, 0.3f), null, 0.8f, 1f);
        Emissive(gold, new Color(1.1f, 0.7f, 0.12f));
        rune = Lit("Checkpoint Rune", new Color(0.15f, 0.3f, 0.28f), null, 0.6f);
        Emissive(rune, new Color(0.1f, 0.35f, 0.3f));
        flame = Lit("Torch Flame", new Color(1f, 0.6f, 0.2f), null, 0f);
        Emissive(flame, new Color(4f, 1.8f, 0.4f));

        water = Lit("River Water", new Color(0.18f, 0.36f, 0.3f, 0.82f), WaterTexture(), 0.92f);
        Transparent(water);
        water.SetTextureScale("_BaseMap", new Vector2(12f, 3f));
        mud = Lit("Pit Mud", new Color(0.22f, 0.2f, 0.11f), null, 0.85f);
        waterfall = Lit("Waterfall", new Color(0.85f, 0.95f, 0.95f, 0.7f), WaterfallTexture(), 0.8f);
        Transparent(waterfall);
        waterfall.SetFloat("_Cull", 0f);
        waterfall.SetTextureScale("_BaseMap", new Vector2(2f, 3f));

        // Additive glow for the teleport particles; their HDR colours feed the bloom
        teleportParticle = LoadOrCreateMaterial("Teleport Particle", Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        teleportParticle.SetTexture("_BaseMap", SoftDotTexture());
        teleportParticle.SetColor("_BaseColor", Color.white);
        teleportParticle.SetFloat("_Surface", 1f);
        teleportParticle.SetFloat("_Blend", 2f);
        teleportParticle.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        teleportParticle.SetFloat("_DstBlend", (float)BlendMode.One);
        teleportParticle.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        teleportParticle.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
        teleportParticle.SetFloat("_ZWrite", 0f);
        teleportParticle.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        teleportParticle.SetOverrideTag("RenderType", "Transparent");
        teleportParticle.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(teleportParticle);

        fern = Lit("Fern", new Color(0.75f, 0.95f, 0.65f), FernTexture(), 0.2f);
        fern.SetFloat("_AlphaClip", 1f);
        fern.SetFloat("_Cutoff", 0.5f);
        fern.SetFloat("_Cull", 0f);
        fern.EnableKeyword("_ALPHATEST_ON");
        fern.renderQueue = (int)RenderQueue.AlphaTest;
        fern.SetOverrideTag("RenderType", "TransparentCutout");
        AssetDatabase.SaveAssets();
    }

    private static Material Lit(string name, Color color, Texture2D texture, float smoothness, float metallic = 0f)
    {
        Material material = LoadOrCreateMaterial(name, Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", color);
        material.SetTexture("_BaseMap", texture);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", metallic);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material LoadOrCreateMaterial(string name, Shader shader)
    {
        string path = $"{GeneratedFolder}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        return material;
    }

    private static void Emissive(Material material, Color emission)
    {
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", emission);
        // URP re-validates keywords on save and drops _EMISSION unless the GI flags mark the material emissive
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
    }

    private static void Transparent(Material material)
    {
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    /// <summary>Perlin noise that wraps around at the texture edges.</summary>
    private static float TileNoise(float u, float v, float frequency, float seed)
    {
        const float offset = 100f;
        float a = Mathf.PerlinNoise(offset + seed + u * frequency, offset + v * frequency);
        float b = Mathf.PerlinNoise(offset + seed + (u - 1f) * frequency, offset + v * frequency);
        float c = Mathf.PerlinNoise(offset + seed + u * frequency, offset + (v - 1f) * frequency);
        float d = Mathf.PerlinNoise(offset + seed + (u - 1f) * frequency, offset + (v - 1f) * frequency);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    private static float TileFbm(float u, float v, float frequency, float seed)
        => TileNoise(u, v, frequency, seed) * 0.55f + TileNoise(u, v, frequency * 2f, seed + 9f) * 0.3f + TileNoise(u, v, frequency * 4f, seed + 21f) * 0.15f;

    private static float Hash(int a, int b)
    {
        unchecked
        {
            int h = a * 374761393 + b * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xffff) / 65535f;
        }
    }

    private static Texture2D StoneTexture() => SaveTexture("Stone", 512, (u, v) =>
    {
        const int rows = 4;
        int row = Mathf.FloorToInt(v * rows);
        float shifted = u * 2f + (row % 2) * 0.5f;
        int column = Mathf.FloorToInt(shifted) % 2;
        float bx = shifted - Mathf.Floor(shifted), by = v * rows - row;
        float edge = Mathf.Min(Mathf.Min(bx, 1f - bx) * 0.5f, Mathf.Min(by, 1f - by) / rows);
        float noise = TileFbm(u, v, 8f, 0f);
        float tint = Hash(row, column) * 0.18f;
        var color = new Color(0.55f + tint, 0.52f + tint * 0.9f, 0.45f + tint * 0.7f) * (0.72f + noise * 0.5f);
        float moss = Smooth(0.5f, 0.68f, TileFbm(u, v, 5f, 50f) + (1f - by) * 0.08f);
        color = Color.Lerp(color, new Color(0.2f, 0.3f, 0.1f) * (0.8f + noise * 0.4f), moss * 0.85f);
        float mortar = Smooth(0.012f, 0.004f, edge + (noise - 0.5f) * 0.006f);
        color = Color.Lerp(color, new Color(0.16f, 0.16f, 0.12f), mortar);
        color.a = 1f;
        return color;
    });

    private static Texture2D BarkTexture() => SaveTexture("Bark", 256, (u, v) =>
    {
        float streak = TileNoise(u, v, 2f, 3f) * 0.3f + TileNoise(u * 1f, v, 24f, 7f) * 0.7f;
        float fine = TileNoise(u, v, 48f, 11f);
        var color = new Color(0.32f, 0.24f, 0.16f) * (0.55f + streak * 0.6f + fine * 0.2f);
        float moss = Smooth(0.55f, 0.7f, TileFbm(u, v, 4f, 19f));
        color = Color.Lerp(color, new Color(0.22f, 0.3f, 0.12f), moss * 0.7f);
        color.a = 1f;
        return color;
    });

    private static Texture2D LeafTexture() => SaveTexture("Leaves", 256, (u, v) =>
    {
        float n = TileFbm(u, v, 12f, 33f);
        float cells = TileNoise(u, v, 40f, 61f);
        var color = Color.Lerp(new Color(0.08f, 0.22f, 0.06f), new Color(0.3f, 0.5f, 0.14f), n);
        color *= 0.7f + cells * 0.5f;
        color.a = 1f;
        return color;
    });

    private static Texture2D GrassTexture() => SaveTexture("Ground Grass", 256, (u, v) =>
    {
        float n = TileFbm(u, v, 6f, 70f);
        float fine = TileNoise(u, v, 64f, 75f);
        var color = Color.Lerp(new Color(0.14f, 0.24f, 0.07f), new Color(0.3f, 0.4f, 0.14f), n) * (0.75f + fine * 0.45f);
        if (TileNoise(u, v, 30f, 90f) > 0.72f) color = Color.Lerp(color, new Color(0.35f, 0.27f, 0.15f), 0.6f);
        color.a = 1f;
        return color;
    });

    private static Texture2D MudTexture() => SaveTexture("Ground Mud", 256, (u, v) =>
    {
        float n = TileFbm(u, v, 6f, 110f);
        float fine = TileNoise(u, v, 50f, 120f);
        var color = Color.Lerp(new Color(0.24f, 0.18f, 0.11f), new Color(0.42f, 0.33f, 0.21f), n) * (0.8f + fine * 0.35f);
        if (TileNoise(u, v, 20f, 130f) > 0.7f) color = Color.Lerp(color, new Color(0.25f, 0.3f, 0.1f), 0.5f);
        color.a = 1f;
        return color;
    });

    private static Texture2D RockTexture() => SaveTexture("Ground Rock", 256, (u, v) =>
    {
        float n = TileFbm(u, v, 5f, 150f);
        float cracks = Mathf.Abs(TileNoise(u, v, 10f, 160f) - 0.5f);
        var color = Color.Lerp(new Color(0.32f, 0.33f, 0.29f), new Color(0.52f, 0.51f, 0.46f), n);
        color *= Mathf.Lerp(0.78f, 1f, Smooth(0f, 0.04f, cracks));
        color = Color.Lerp(color, new Color(0.2f, 0.3f, 0.12f), Smooth(0.55f, 0.7f, TileFbm(u, v, 4f, 170f)) * 0.7f);
        color.a = 1f;
        return color;
    });

    private static Texture2D WaterTexture() => SaveTexture("Water", 256, (u, v) =>
    {
        float n = TileFbm(u, v, 6f, 200f);
        float shade = 0.8f + n * 0.4f;
        return new Color(shade, shade, shade, 1f);
    });

    private static Texture2D WaterfallTexture() => SaveTexture("Waterfall", 256, (u, v) =>
    {
        float streak = TileNoise(u, v, 1f, 230f) * 0.3f + TileNoise(u, v * 0.1f, 30f, 240f) * 0.7f;
        float foam = TileNoise(u, v, 16f, 250f);
        float a = Mathf.Clamp01(0.35f + streak * 0.6f + foam * 0.2f);
        float shade = 0.75f + streak * 0.3f;
        return new Color(shade, shade, shade, a);
    }, alpha: true);

    private static Texture2D GrassBladeTexture() => SaveTexture("Grass Blades", 128, (u, v) =>
    {
        for (int i = 0; i < 9; i++)
        {
            float root = 0.08f + i * 0.105f + (Hash(i, 3) - 0.5f) * 0.05f;
            float height = 0.5f + Hash(i, 7) * 0.5f;
            float lean = (Hash(i, 11) - 0.5f) * 0.25f;
            if (v > height) continue;
            float t = v / height;
            float x = root + lean * t * t;
            float width = 0.035f * (1f - t);
            if (Mathf.Abs(u - x) < width)
                return Color.Lerp(new Color(0.25f, 0.35f, 0.12f), new Color(0.6f, 0.75f, 0.3f), t);
        }
        return new Color(0.3f, 0.45f, 0.15f, 0f);
    }, alpha: true, clamp: true);

    private static Texture2D FernTexture() => SaveTexture("Fern", 128, (u, v) =>
    {
        // Frond: a stem up the middle with leaflets that taper towards the tip
        float width = 0.42f * Mathf.Sin(Mathf.PI * Mathf.Pow(v, 0.8f)) * (1f - v * 0.3f);
        float dx = Mathf.Abs(u - 0.5f);
        float leaflet = 0.5f + 0.5f * Mathf.Sin(v * 70f - dx * 25f);
        bool stem = dx < 0.015f;
        bool leaf = dx < width && leaflet > 0.3f + dx * 0.8f;
        if (!stem && !leaf) return new Color(0.2f, 0.4f, 0.1f, 0f);
        float shade = 0.7f + 0.3f * (1f - dx / Mathf.Max(width, 0.01f));
        return new Color(0.2f, 0.45f, 0.12f) * shade + new Color(0f, 0f, 0f, 1f);
    }, alpha: true, clamp: true);

    private static Texture2D SoftDotTexture() => SaveTexture("Soft Dot", 64, (u, v) =>
    {
        float r = Mathf.Clamp01(Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f);
        float falloff = (1f - r) * (1f - r);
        return new Color(1f, 1f, 1f, falloff);
    }, alpha: true, clamp: true);

    private static Texture2D SaveTexture(string name, int size, System.Func<float, float, Color> pixel, bool alpha = false, bool clamp = false)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = pixel((x + 0.5f) / size, (y + 0.5f) / size);
        texture.SetPixels(pixels);
        texture.Apply();

        string path = $"{GeneratedFolder}/{name}.png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
        importer.alphaIsTransparency = alpha;
        importer.wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ---------------------------------------------------------------- Helpers

    private static float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);

    private static void EnsureFolder(string parent, string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    private static void SetField(Object target, string field, object value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);
        switch (value)
        {
            case Object reference: property.objectReferenceValue = reference; break;
            case float number: property.floatValue = number; break;
            case Vector2 vector2: property.vector2Value = vector2; break;
            case Vector3 vector3: property.vector3Value = vector3; break;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
