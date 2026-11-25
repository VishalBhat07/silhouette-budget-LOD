using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Workbench.Demo
{
    /// <summary>Builds dock and tug scenery, practical lighting, and powered mechanisms.</summary>
    public sealed partial class SalvageDock : MonoBehaviour
    {
        readonly List<Object> owned = new();
        readonly Dictionary<Material, List<CombineInstance>> batches = new();
        GameObject scenery;
        Mesh cube;
        Shader lit;
        Material deck, steel, pale, orange, ice, signal, stars;
        Light socketLamp;
        readonly Transform[] leaves = new Transform[2];
        float hatchTravel;
        bool powered;
        SalvageMission mission;

        void Awake()
        {
            mission = GetComponent<SalvageMission>();
            scenery = new GameObject("Salvage dock outfitting");
            SceneManager.MoveGameObjectToScene(scenery, gameObject.scene);
            cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            lit = Shader.Find(GraphicsSettings.currentRenderPipeline == null
                ? "Standard" : "Universal Render Pipeline/Lit");
            deck = Surface("Basalt deck plating", new Color32(58, 77, 88, 255), .38f);
            steel = Surface("Gunmetal frame", new Color32(83, 107, 115, 255), .55f);
            pale = Surface("Worn ceramic cladding", new Color32(170, 181, 176, 255), .25f);
            orange = Surface("Rescue orange enamel", new Color32(203, 102, 51, 255), .25f);
            ice = Surface("Cold running lights", new Color32(147, 226, 239, 255), 0, 1.8f);
            signal = Surface("Power circuit indicators", Color.white, 0, 1);
            stars = Surface("Distant starlight", new Color32(196, 215, 229, 255), 0, 1.25f);

            LightDock();
            BuildDeck();
            CladHull();
            Cargo(-6, 3, 3);
            Cargo(-5, 11, 2);
            Tint("Port Cargo Rack", steel);
            Tint("Cargo Stack", orange);
            BuildAirlock();
            BuildTugInterior();
            BuildShuttleBerth();
            BuildSky();
            FlushBatches();
            SetPowered(powered);
        }

        Material Surface(string label, Color colour, float metal, float glow = 0)
        {
            var material = new Material(lit) { name = label, color = colour };
            material.SetFloat("_Metallic", metal);
            material.SetFloat("_Smoothness", .32f);
            if (glow > 0)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", colour.linear * glow);
            }
            owned.Add(material);
            return material;
        }

        void LightDock()
        {
            var camera = GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(7, 15, 29, 255);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color32(139, 168, 190, 255);
            RenderSettings.ambientEquatorColor = new Color32(91, 119, 137, 255);
            RenderSettings.ambientGroundColor = new Color32(67, 78, 95, 255);
            var key = GameObject.Find("Directional Light")?.GetComponent<Light>();
            if (key != null)
            {
                key.color = new Color32(255, 222, 186, 255);
                key.intensity = 1.65f;
                key.transform.rotation = Quaternion.Euler(32, 121, 0);
                key.shadowStrength = .72f;
            }
            socketLamp = new GameObject("Socket status lamp").AddComponent<Light>();
            socketLamp.transform.SetParent(scenery.transform, false);
            socketLamp.transform.localPosition = new Vector3(2, 2, 17);
            socketLamp.type = LightType.Point;
            socketLamp.range = 6;
            socketLamp.shadows = LightShadows.None;
        }

        public void SetPowered(bool on)
        {
            powered = on;
            if (signal == null) return;
            Color colour = on ? new Color32(92, 243, 187, 255) : new Color32(255, 149, 55, 255);
            signal.color = colour;
            signal.SetColor("_EmissionColor", colour.linear * 2);
            socketLamp.color = colour;
            socketLamp.intensity = on ? 3 : 1.4f;
        }

        static void Tint(string fixture, Material material)
        {
            var renderer = GameObject.Find(fixture)?.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = material;
        }

        void Box(Material material, float x, float y, float z, float w, float h, float d)
        {
            Add(cube, material, new Vector3(x, y, z), Quaternion.identity, new Vector3(w, h, d));
        }

        void Beam(Material material, Vector3 a, Vector3 b, float width, float depth)
        {
            var axis = b - a;
            Add(cube, material, (a + b) * .5f, Quaternion.FromToRotation(Vector3.up, axis),
                new Vector3(width, axis.magnitude, depth));
        }

        void Add(Mesh mesh, Material material, Vector3 p, Quaternion r, Vector3 scale)
        {
            if (!batches.TryGetValue(material, out var pieces))
                batches[material] = pieces = new List<CombineInstance>();
            pieces.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(p, r, scale) });
        }

        void CladHull()
        {
            Tint("Starboard Hull", steel);
            for (int i = 0; i < 4; i++)
            {
                float z = 6 + 2 * i;
                Tint("Exposed Hull Rib " + (i + 1), pale);
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = 1 + side * 2.55f;
                    Beam(pale, new Vector3(x, 1.3f, z), new Vector3(x + side * .36f, 4.5f, z), .24f, .27f);
                    if (i == 3) continue;
                    Box(deck, x, 2.8f, z + 1, .16f, 1.72f, 1.7f);
                    Box(orange, x + side * .1f, 3.34f, z + 1, .07f, .22f, 1.65f);
                    for (int vent = 0; vent < 5; vent++)
                        Box(steel, x + side * .11f, 2.65f, z + .4f + .28f * vent, .12f, .65f, .09f);
                }
            }
            for (int i = 0; i < 3; i++)
                Box(deck, -.5f + 1.5f * i, 4.28f, 9, .45f, .12f, 7.8f);
            // The tug's tapered prow projects from the forward bulkhead above the deck.
            var prow = new Mesh { name = "Tug prow" };
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            Vector2[] section = { new(1, -.5f), new(1, .4f), new(.62f, 1),
                new(-.62f, 1), new(-1, .4f), new(-1, -.5f), new(-.55f, -1), new(.55f, -1) };
            for (int ring = 0; ring < 2; ring++)
            {
                foreach (var corner in section)
                    vertices.Add(new Vector3(1 + corner.x * (ring == 0 ? 1.12f : 2.2f),
                        2.85f + corner.y * (ring == 0 ? .58f : 1.24f), ring == 0 ? 2.2f : 5.02f));
            }
            for (int i = 0; i < 8; i++)
            {
                int j = (i + 1) % 8;
                indices.AddRange(new[] { i, j, 8 + j, i, 8 + j, 8 + i });
                if (i > 0 && i < 7) indices.AddRange(new[] { 0, j, i });
            }
            prow.SetVertices(vertices);
            prow.SetTriangles(indices, 0);
            prow.RecalculateNormals();
            owned.Add(prow);
            Add(prow, pale, Vector3.zero, Quaternion.identity, Vector3.one);
            Box(deck, 1, 2.94f, 2.16f, 1.98f, .54f, .12f);
            for (int i = -1; i <= 1; i++)
                Box(ice, 1 + i * .59f, 3.03f, 2.085f, .48f, .15f, .035f);
            // Split armour on the tow-facing bulkhead.
            for (int i = 0; i < 3; i++)
            {
                Box(pale, -.57f + 1.56f * i, 2.9f, 4.91f, 1.42f, 2.32f, .18f);
                Box(orange, -.57f + 1.56f * i, 2.35f, 4.8f, 1.4f, .26f, .045f);
            }
        }

        void Cargo(float x, float z, float height)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                Box(deck, x, height - .09f, z + side * 1.5f, 3.16f, .24f, .17f);
                Box(deck, x + side * 1.5f, .12f, z, .17f, .24f, 3.12f);
                for (int corner = -1; corner <= 1; corner += 2)
                    Box(pale, x + side * 1.46f, height / 2, z + corner * 1.46f, .22f, height, .22f);
                for (int n = 0; n < 7; n++)
                {
                    Box(steel, x - 1.08f + n * .36f, height * .49f, z + side * 1.51f,
                        .1f, height * .66f, .11f);
                    Box(steel, x + side * 1.51f, height * .49f, z - 1.08f + n * .36f,
                        .11f, height * .66f, .1f);
                }
            }
        }

        void BuildAirlock()
        {
            var hatch = GameObject.Find("Exit Hatch");
            hatch.GetComponent<MeshRenderer>().enabled = false;
            hatch.GetComponent<BoxCollider>().enabled = false;
            Tint("Engine Socket Placeholder", steel);
            Box(deck, 0, .12f, 22.85f, 4.8f, .24f, 1.8f);
            Box(steel, 0, 4.35f, 23, 4.7f, .48f, .85f);
            Box(signal, 0, 4.39f, 22.55f, 2.3f, .15f, .09f);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(pale, side * 1.82f, 2.15f, 23, .62f, 4.3f, .8f);
                Box(orange, side * 1.82f, 1.12f, 22.54f, .58f, .5f, .13f);
                // Each side pocket receives a rigid door leaf as the circuit comes online.
                Box(deck, side * 2.62f, 2.05f, 23, 1.45f, 4.1f, .74f);
                var leaf = new GameObject(side < 0 ? "Port hatch leaf" : "Starboard hatch leaf");
                leaf.transform.SetParent(scenery.transform, false);
                leaf.transform.localScale = new Vector3(1.5f, 3.95f, .32f);
                leaf.transform.localPosition = new Vector3(side * .75f, 2.05f, 23);
                leaf.AddComponent<MeshFilter>().sharedMesh = cube;
                leaf.AddComponent<MeshRenderer>().sharedMaterial = steel;
                leaf.AddComponent<BoxCollider>();
                leaves[side < 0 ? 0 : 1] = leaf.transform;
                Box(signal, side * 1.57f, 2.13f, 22.6f, .07f, 2.82f, .08f);
                Box(deck, 2 + side * .52f, 1, 18, .14f, 2, 1.1f);
                Box(orange, 2 + side * .48f, .48f, 17.445f, .11f, .56f, .13f);
                Box(steel, 2 + side * .45f, 1.6f, 17.42f, .14f, .29f, .14f);
            }
            Box(deck, 2, 1.13f, 17.44f, .94f, .86f, .14f);
            Box(steel, 2, 1.0f, 17.32f, .74f, .58f, .16f);
            Box(deck, 2, 1.0f, 17.225f, .51f, .41f, .045f);
            Add(Ring(.219f, .269f), signal, new Vector3(2, 1.0f, 17.197f),
                Quaternion.Euler(-90, 0, 0), Vector3.one);
            Box(steel, 2, 1.47f, 17.31f, .75f, .22f, .12f);
            for (int indicator = 0; indicator < 4; indicator++)
                Box(signal, 1.77f + indicator * .15f, 1.46f, 17.237f, .08f, .07f, .026f);
            Label("ENGINE SUPPLY", new Vector3(2, 1.71f, 17.40f), .132f, new Color32(210, 224, 217, 255));
        }

        void Update()
        {
            float target = powered ? 1.5f : 0;
            if (mission != null && mission.IsFrozen) return;
            UpdateLift();
            if (hatchTravel == target) return;
            hatchTravel = Mathf.MoveTowards(hatchTravel, target, 1.8f * Time.deltaTime);
            for (int i = 0; i < leaves.Length; i++)
            {
                float side = i == 0 ? -1 : 1;
                leaves[i].localPosition = new Vector3(side * (.75f + hatchTravel), 2.05f, 23);
            }
        }

        void BuildSky()
        {
            var speck = new Mesh
            {
                name = "Star",
                vertices = new[] { new Vector3(-.65f, -.45f), new Vector3(0, .75f), new Vector3(.65f, -.45f) },
                triangles = new[] { 0, 1, 2 },
                normals = new[] { Vector3.back, Vector3.back, Vector3.back }
            };
            owned.Add(speck);
            var random = new System.Random(4183);
            for (int i = 0; i < 1150; i++)
            {
                float azimuth = (float)random.NextDouble() * 2 * Mathf.PI;
                float y = (float)random.NextDouble() * 1.2f - .24f;
                float horizontal = Mathf.Sqrt(1 - y * y);
                var point = new Vector3(horizontal * Mathf.Cos(azimuth), y,
                    horizontal * Mathf.Sin(azimuth)) * 235;
                float size = .08f + .26f * Mathf.Pow((float)random.NextDouble(), 3);
                Add(speck, stars, point, Quaternion.LookRotation(point), Vector3.one * size);
            }

            // A banded ice giant and its inclined rings establish the scale beyond the gantry.
            var planet = Surface("Cloud belts", Color.white, 0, .18f);
            var texture = new Texture2D(256, 128, TextureFormat.RGBA32, true) { name = "Ice giant atmosphere" };
            texture.wrapModeV = TextureWrapMode.Clamp;
            var pixels = new Color32[texture.width * texture.height];
            for (int row = 0; row < texture.height; row++)
            {
                float lat = (float)row / (texture.height - 1);
                for (int col = 0; col < texture.width; col++)
                {
                    float phi = 2 * Mathf.PI * col / texture.width;
                    float cloud = Mathf.PerlinNoise(4 + 2 * Mathf.Cos(phi), 6 + lat * 8 + Mathf.Sin(phi));
                    float belt = .48f + .23f * Mathf.Sin(lat * 38 + cloud * 3)
                        + .16f * Mathf.Sin(lat * 133 + cloud * 4) + .12f * cloud;
                    pixels[row * texture.width + col] = Color.Lerp(
                        new Color32(38, 77, 102, 255), new Color32(166, 197, 194, 255), belt);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            owned.Add(texture);
            planet.mainTexture = texture;
            planet.SetTexture("_EmissionMap", texture);
            var centre = new Vector3(-48, 26, 133);
            var tilt = Quaternion.Euler(-14, 0, -17);
            Add(Resources.GetBuiltinResource<Mesh>("Sphere.fbx"), planet, centre, tilt, Vector3.one * 39);
            var ring = Surface("Dust ring", new Color32(134, 155, 161, 255), 0, .2f);
            Add(Ring(24, 26.4f), ring, centre, tilt, Vector3.one);
            Add(Ring(27, 31), ring, centre, tilt, Vector3.one);
        }

        Mesh Ring(float inner, float outer)
        {
            const int segments = 100;
            var vertices = new Vector3[2 * segments];
            var faces = new int[12 * segments];
            for (int i = 0; i < segments; i++)
            {
                float a = i * 2 * Mathf.PI / segments;
                var radial = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                int p = i * 2, q = (p + 2) % vertices.Length;
                vertices[p] = radial * inner;
                vertices[p + 1] = radial * outer;
                int j = i * 12;
                faces[j] = p; faces[j + 1] = q; faces[j + 2] = p + 1;
                faces[j + 3] = p + 1; faces[j + 4] = q; faces[j + 5] = q + 1;
                for (int k = 0; k < 6; k += 3)
                {
                    faces[j + k + 6] = faces[j + k + 2];
                    faces[j + k + 7] = faces[j + k + 1];
                    faces[j + k + 8] = faces[j + k];
                }
            }
            var mesh = new Mesh { name = "Orbital ring", vertices = vertices, triangles = faces };
            var normals = new Vector3[vertices.Length];
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            mesh.normals = normals;
            owned.Add(mesh);
            return mesh;
        }

        void FlushBatches()
        {
            foreach (var entry in batches)
            {
                var mesh = new Mesh { name = entry.Key.name };
                mesh.CombineMeshes(entry.Value.ToArray());
                owned.Add(mesh);
                var part = new GameObject(entry.Key.name);
                part.transform.SetParent(scenery.transform, false);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = part.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = entry.Key;
                if (entry.Key.IsKeywordEnabled("_EMISSION"))
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
            }
            batches.Clear();
        }

        void OnDestroy()
        {
            if (scenery != null) Destroy(scenery);
            foreach (var item in owned) Destroy(item);
        }
    }
}
