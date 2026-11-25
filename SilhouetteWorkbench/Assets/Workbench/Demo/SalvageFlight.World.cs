using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Workbench.Demo
{
    /// <summary>Builds the dock backdrop, jump aperture, moon pad and relay station.</summary>
    public sealed partial class SalvageFlight
    {
        Material regolith, rock, habitat, solar, warm, vista, planetSkin, ringDust, starfield, cometDust;
        Mesh cube, cylinder, sphere;
        Transform relayDoor, relayDish, relayRover;

        void BuildWorld()
        {
            cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");

            regolith = Surface("Lunar regolith", new Color32(96, 92, 86, 255), .05f, 0, .12f);
            rock = Surface("Broken moon rock", new Color32(72, 69, 64, 255), .04f, 0, .08f);
            habitat = Surface("Habitat shell", new Color32(198, 200, 190, 255), .32f, 0, .38f);
            solar = Surface("Solar wing", new Color32(26, 44, 76, 255), .72f, 0, .6f);
            warm = Surface("Airlock light", new Color32(255, 176, 102, 255), 0, 1.7f);
            cometDust = Surface("Comet dust", new Color32(150, 178, 196, 255), 0, .5f);
            starfield = Surface("Distant starlight", new Color32(198, 216, 230, 255), 0, 1.4f);
            ringDust = Surface("Planet ring dust", new Color32(128, 150, 158, 255), 0, .25f);
            vista = Vista();
            planetSkin = PlanetSkin();

            LightWorld();
            BuildDock();
            BuildAperture();
            BuildMoon();
            BuildStation();
            BuildRover();
            BuildSky();
        }

        // ---- shared builders -------------------------------------------------

        Transform Add(string name, Mesh mesh, Material material, Vector3 position, Vector3 scale, Quaternion rotation)
            => Add(world, name, mesh, material, position, scale, rotation);

        Transform Add(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Vector3 scale,
            Quaternion rotation)
        {
            var item = new GameObject(name);
            var transform = item.transform;
            transform.SetParent(parent, false);
            transform.localPosition = position;
            transform.localScale = scale;
            transform.localRotation = rotation;
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            item.AddComponent<MeshRenderer>().sharedMaterial = material;
            return transform;
        }

        Transform Box(string name, Material material, Vector3 position, Vector3 scale, Quaternion? rotation = null)
            => Box(world, name, material, position, scale, rotation);

        Transform Box(Transform parent, string name, Material material, Vector3 position, Vector3 scale,
            Quaternion? rotation = null)
            => Add(parent, name, cube, material, position, scale, rotation ?? Quaternion.identity);

        Transform Cylinder(string name, Material material, Vector3 position, float diameter, float length,
            Quaternion? rotation = null)
            => Add(world, name, cylinder, material, position, new Vector3(diameter, length * .5f, diameter),
                rotation ?? Quaternion.identity);

        Transform Cylinder(Transform parent, string name, Material material, Vector3 position, float diameter,
            float length, Quaternion? rotation = null)
            => Add(parent, name, cylinder, material, position, new Vector3(diameter, length * .5f, diameter),
                rotation ?? Quaternion.identity);

        Transform Sphere(string name, Material material, Vector3 position, float diameter,
            Quaternion? rotation = null)
            => Add(world, name, sphere, material, position, Vector3.one * diameter,
                rotation ?? Quaternion.identity);

        Transform Sphere(Transform parent, string name, Material material, Vector3 position, float diameter,
            Quaternion? rotation = null)
            => Add(parent, name, sphere, material, position, Vector3.one * diameter,
                rotation ?? Quaternion.identity);

        Transform Beam(string name, Material material, Vector3 a, Vector3 b, float width, float depth = 0)
            => Beam(world, name, material, a, b, width, depth);

        Transform Beam(Transform parent, string name, Material material, Vector3 a, Vector3 b, float width,
            float depth = 0)
        {
            var axis = b - a;
            if (axis.sqrMagnitude < .0001f) axis = Vector3.up * .001f;
            var beam = Box(parent, name, material, (a + b) * .5f,
                new Vector3(width, axis.magnitude, depth > 0 ? depth : width));
            beam.localRotation = Quaternion.FromToRotation(Vector3.up, axis);
            return beam;
        }

        Light Lamp(string name, Vector3 position, Color colour, float range, float intensity,
            LightShadows shadows = LightShadows.None)
            => Lamp(world, name, position, colour, range, intensity, shadows);

        Light Lamp(Transform parent, string name, Vector3 position, Color colour, float range, float intensity,
            LightShadows shadows = LightShadows.None)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.localPosition = position;
            light.type = LightType.Point;
            light.color = colour;
            light.range = range;
            light.intensity = intensity;
            light.shadows = shadows;
            return light;
        }

        void Label(string text, Vector3 position, float height, Color colour, float yaw = 180)
        {
            var sign = new GameObject(text).AddComponent<TextMesh>();
            sign.transform.SetParent(world, false);
            sign.transform.localPosition = position;
            sign.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            sign.text = text;
            sign.anchor = TextAnchor.MiddleCenter;
            sign.fontSize = 54;
            sign.characterSize = height * .1f;
            sign.color = colour;
            sign.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        Mesh RingMesh(float inner, float outer, int segments = 72)
        {
            var vertices = new Vector3[2 * segments];
            var faces = new int[12 * segments];
            for (var i = 0; i < segments; i++)
            {
                var a = i * 2 * Mathf.PI / segments;
                var radial = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                int p = i * 2, q = (p + 2) % vertices.Length;
                vertices[p] = radial * inner;
                vertices[p + 1] = radial * outer;
                var j = i * 12;
                faces[j] = p; faces[j + 1] = q; faces[j + 2] = p + 1;
                faces[j + 3] = p + 1; faces[j + 4] = q; faces[j + 5] = q + 1;
                for (var k = 0; k < 6; k += 3)
                {
                    faces[j + k + 6] = faces[j + k + 2];
                    faces[j + k + 7] = faces[j + k + 1];
                    faces[j + k + 8] = faces[j + k];
                }
            }
            var mesh = new Mesh { name = "Ring", vertices = vertices, triangles = faces };
            var normals = new Vector3[vertices.Length];
            for (var i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            mesh.normals = normals;
            owned.Add(mesh);
            return mesh;
        }

        Mesh DiscMesh(float radius, int segments = 72)
        {
            var vertices = new List<Vector3> { Vector3.zero };
            var uvs = new List<Vector2> { new(.5f, .5f) };
            var triangles = new List<int>();
            for (var i = 0; i <= segments; i++)
            {
                var a = i * Mathf.PI * 2 / segments;
                vertices.Add(new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0));
                uvs.Add(new Vector2(Mathf.Cos(a) * .5f + .5f, Mathf.Sin(a) * .5f + .5f));
                if (i < segments) triangles.AddRange(new[] { 0, i + 2, i + 1 });
            }
            var mesh = new Mesh { name = "Vista disc" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            owned.Add(mesh);
            return mesh;
        }

        // ---- materials -------------------------------------------------------

        Material Vista()
        {
            var shader = Resources.Load<Shader>("OrbitalJumpVista");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Texture");
            if (shader == null) shader = litShader;
            var material = new Material(shader) { name = "Moon vista" };
            var texture = new Texture2D(256, 256, TextureFormat.RGBA32, true) { name = "Moon vista" };
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[texture.width * texture.height];
            var random = new System.Random(7717);
            for (var y = 0; y < texture.height; y++)
            {
                var v = (y + .5f) / texture.height;
                for (var x = 0; x < texture.width; x++)
                {
                    var u = (x + .5f) / texture.width;
                    float dx = u * 2 - 1, dy = v * 2 - 1;
                    var r = Mathf.Sqrt(dx * dx + dy * dy);
                    Color colour;
                    if (v < .46f)
                    {
                        var noise = Mathf.PerlinNoise(u * 9f, v * 26f) * .55f
                            + Mathf.PerlinNoise(u * 31f + 4, v * 57f) * .3f;
                        colour = Color.Lerp(new Color32(62, 59, 55, 255), new Color32(128, 123, 112, 255), noise);
                        if (v > .38f) colour *= .78f;
                    }
                    else
                    {
                        colour = new Color32(9, 17, 32, 255);
                        if (random.NextDouble() > .9965) colour = new Color32(186, 208, 228, 255);
                    }
                    if (r > .9f)
                        colour = Color.Lerp(colour, new Color32(132, 196, 232, 255),
                            Mathf.InverseLerp(.9f, 1f, r) * .85f);
                    pixels[y * texture.width + x] = colour;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            material.mainTexture = texture;
            if (material.HasProperty("_BaseColor")) material.color = Color.white;
            owned.Add(texture);
            owned.Add(material);
            return material;
        }

        Material PlanetSkin()
        {
            var material = new Material(litShader) { name = "Ice giant atmosphere" };
            var texture = new Texture2D(256, 128, TextureFormat.RGBA32, true) { name = "Ice giant bands" };
            texture.wrapModeV = TextureWrapMode.Clamp;
            var pixels = new Color32[texture.width * texture.height];
            for (var row = 0; row < texture.height; row++)
            {
                var lat = (float)row / (texture.height - 1);
                for (var col = 0; col < texture.width; col++)
                {
                    var phi = 2 * Mathf.PI * col / texture.width;
                    var cloud = Mathf.PerlinNoise(6 + 2 * Mathf.Cos(phi), 3 + lat * 9 + Mathf.Sin(phi));
                    var belt = .5f + .22f * Mathf.Sin(lat * 31 + cloud * 3)
                        + .14f * Mathf.Sin(lat * 97 + cloud * 5) + .12f * cloud;
                    pixels[row * texture.width + col] = Color.Lerp(
                        new Color32(30, 66, 88, 255), new Color32(178, 204, 196, 255), belt);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            material.mainTexture = texture;
            material.SetFloat("_Smoothness", .25f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(.22f, .28f, .3f) * .5f);
            owned.Add(texture);
            owned.Add(material);
            return material;
        }

        // ---- lighting --------------------------------------------------------

        void LightWorld()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color32(44, 62, 88, 255);
            RenderSettings.ambientEquatorColor = new Color32(74, 80, 86, 255);
            RenderSettings.ambientGroundColor = new Color32(42, 39, 36, 255);
            RenderSettings.ambientIntensity = 1;

            var sun = GameObject.Find("Directional Light")?.GetComponent<Light>();
            if (sun == null)
            {
                sun = new GameObject("Directional Light").AddComponent<Light>();
                sun.transform.SetParent(world, false);
            }
            sun.type = LightType.Directional;
            sun.color = new Color32(255, 233, 203, 255);
            sun.intensity = 1.9f;
            sun.shadowStrength = .82f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(20, 22, 0);

            var fill = new GameObject("Sky fill").AddComponent<Light>();
            fill.transform.SetParent(world, false);
            fill.type = LightType.Directional;
            fill.color = new Color32(96, 142, 188, 255);
            fill.intensity = .38f;
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(32, 196, 0);
        }

        // ---- dock backdrop ---------------------------------------------------

        void BuildDock()
        {
            Box("Dock deck", deck, new Vector3(0, -.25f, 8), new Vector3(32, .5f, 48));
            Box("Service lane", steel, new Vector3(-3.4f, .012f, 17), new Vector3(2.3f, .024f, 26));
            for (var i = 0; i < 10; i++)
                Box("Lane chevron", orange, new Vector3(-3.4f, .02f, 5.5f + i * 2.7f),
                    new Vector3(1.9f, .02f, .26f), Quaternion.Euler(0, 28, 0));
            for (var i = 0; i < 6; i++)
                Box("Deck plate seam", dark, new Vector3(-13 + i * 5.2f, .016f, 24.4f),
                    new Vector3(.06f, .02f, 14));

            // Bulkhead with the extraction hatch the technician walks through.
            foreach (var side in new[] { -1, 1 })
                Box("Dock bulkhead", steel, new Vector3(side * 8.8f, 4.5f, 23), new Vector3(14.4f, 9, .8f));
            Box("Bulkhead header", steel, new Vector3(0, 6.5f, 23), new Vector3(3.2f, 5, .8f));
            foreach (var side in new[] { -1, 1 })
                Box("Hatch trim", orange, new Vector3(side * 1.78f, 2, 22.58f), new Vector3(.22f, 4.3f, .22f));
            Box("Hatch lintel", orange, new Vector3(0, 4.16f, 22.58f), new Vector3(3.72f, .22f, .22f));
            Box("Hatch interior glow", warm, new Vector3(0, 2, 23.6f), new Vector3(2.7f, 3.5f, .06f));
            Label("EXTRACTION 02", new Vector3(0, 5.35f, 22.56f), .3f, new Color32(214, 231, 226, 255));
            Lamp("Hatch interior light", new Vector3(0, 3.2f, 22.4f), new Color32(255, 194, 130, 255), 9, 5);

            // Deck edge rails.
            for (var side = -1; side <= 1; side += 2)
                RailRunZ(side * 15.7f, -14, 31.4f);
            RailRunX(31.79f, -16, -4.55f);
            RailRunX(31.79f, -2.25f, 16);

            // Side service catwalks with light poles: these sweep past the cockpit panes.
            foreach (var side in new[] { -1, 1 })
            {
                var x = side * 7.2f;
                Box("Service catwalk", deck, new Vector3(x, -.08f, 41), new Vector3(1.4f, .16f, 19));
                Box("Catwalk rail", orange, new Vector3(x + side * .62f, .9f, 41), new Vector3(.08f, .08f, 19));
                for (var i = 0; i < 5; i++)
                {
                    var z = 33.5f + i * 3.7f;
                    Box("Light pole", steel, new Vector3(x, 2.6f, z), new Vector3(.16f, 5.2f, .16f));
                    Box("Pole band", orange, new Vector3(x, 1.1f, z), new Vector3(.22f, .3f, .22f));
                    Box("Pole lamp", ice, new Vector3(x, 5.2f, z), new Vector3(.46f, .14f, .46f));
                    if (i % 2 == 0)
                        Lamp("Berth pole light", new Vector3(x, 5.1f, z),
                            new Color32(188, 222, 255, 255), 9, 3.4f);
                }
            }

            // Gantry frame the chase camera flies through.
            foreach (var side in new[] { -1, 1 })
            {
                Box("Gantry column", steel, new Vector3(side * 10.5f, 4, 33), new Vector3(.5f, 8, .5f));
                Box("Gantry foot", dark, new Vector3(side * 10.5f, .18f, 33), new Vector3(1.5f, .36f, 1.5f));
                Box("Gantry stripe", orange, new Vector3(side * 10.5f, 1.4f, 33), new Vector3(.56f, .4f, .56f));
            }
            Box("Gantry beam", steel, new Vector3(0, 8, 33), new Vector3(21.5f, .5f, .5f));
            Box("Gantry lamp", ice, new Vector3(0, 7.7f, 33), new Vector3(5, .1f, .3f));
            Lamp("Gantry work light", new Vector3(0, 7.4f, 33.4f), new Color32(255, 206, 156, 255), 12, 4.5f);

            // Exposed machinery along the deck.
            foreach (var side in new[] { -1, 1 })
            {
                var x = side * 12.4f;
                Cylinder("Coolant tank", pale, new Vector3(x, 1.3f, 27), 2.4f, 2.6f);
                Cylinder("Tank cap", orange, new Vector3(x, 2.65f, 27), 2.5f, .16f);
                for (var pipe = 0; pipe < 3; pipe++)
                    Cylinder("Pipe run", steel, new Vector3(x - .7f + pipe * .7f, 1.7f, 29.4f), .24f, 4.2f,
                        Quaternion.Euler(90, 0, 0));
                Box("Crate", dark, new Vector3(x - side * 2.6f, .8f, 29.6f), new Vector3(2.2f, 1.6f, 2.2f));
                Box("Crate band", orange, new Vector3(x - side * 2.6f, 1.3f, 28.48f), new Vector3(2.24f, .22f, .04f));
            }
            Beam("Crane mast", steel, new Vector3(13.4f, 0, 25), new Vector3(13.4f, 9, 25), .55f);
            Beam("Crane boom", steel, new Vector3(13.4f, 9, 25), new Vector3(5, 9, 25), .4f);
            Beam("Crane stay", dark, new Vector3(13.4f, 6.4f, 25), new Vector3(6.6f, 8.8f, 25), .12f);
            Box("Crane hook", orange, new Vector3(5.4f, 8.3f, 25), new Vector3(.5f, .9f, .5f));
            Lamp("Deck work light", new Vector3(-6, 3.2f, 29), new Color32(255, 198, 138, 255), 10, 4.5f);
            Lamp("Deck work light", new Vector3(6, 3.2f, 29), new Color32(255, 198, 138, 255), 10, 4.5f);

            // Tug mass over the bulkhead, mostly a silhouette behind the departure.
            Cylinder("Tug hull", pale, new Vector3(0, 9.4f, 12), 8.4f, 19, Quaternion.Euler(90, 0, 0));
            Sphere("Tug bow", pale, new Vector3(0, 9.4f, 21.4f), 8.4f);
            Cylinder("Tug engine ring", dark, new Vector3(0, 9.4f, 2.8f), 9, .8f, Quaternion.Euler(90, 0, 0));
            for (var i = 0; i < 4; i++)
                Cylinder("Tug rib", orange, new Vector3(0, 9.4f, 5 + i * 4), 8.8f, .3f, Quaternion.Euler(90, 0, 0));

            departureBerth = SalvageBerth.Build(world, new BerthPalette
            {
                Deck = deck, Steel = steel, Pale = pale, Orange = orange, Glow = ice, Dark = dark,
                Shuttle = new ShuttlePalette
                {
                    Ceramic = pale, Metal = steel, Dark = dark, Glass = glass,
                    Stripe = stripe, Glow = ice, ConsoleGlow = glow, Seat = dark
                }
            }, false, owned);
        }

        void RailRunZ(float x, float z0, float z1)
        {
            var length = Mathf.Abs(z1 - z0);
            var mid = (z0 + z1) * .5f;
            Box("Rail", orange, new Vector3(x, 1.06f, mid), new Vector3(.09f, .09f, length));
            Box("Mid rail", steel, new Vector3(x, .62f, mid), new Vector3(.06f, .06f, length));
            var posts = Mathf.Max(2, Mathf.RoundToInt(length / 2.4f));
            for (var i = 0; i <= posts; i++)
            {
                var z = Mathf.Lerp(z0, z1, i / (float)posts);
                Box("Rail post", steel, new Vector3(x, .55f, z), new Vector3(.12f, 1.1f, .12f));
            }
        }

        void RailRunX(float z, float x0, float x1)
        {
            var length = Mathf.Abs(x1 - x0);
            var mid = (x0 + x1) * .5f;
            Box("Rail", orange, new Vector3(mid, 1.06f, z), new Vector3(length, .09f, .09f));
            Box("Mid rail", steel, new Vector3(mid, .62f, z), new Vector3(length, .06f, .06f));
            var posts = Mathf.Max(2, Mathf.RoundToInt(length / 2.4f));
            for (var i = 0; i <= posts; i++)
            {
                var x = Mathf.Lerp(x0, x1, i / (float)posts);
                Box("Rail post", steel, new Vector3(x, .55f, z), new Vector3(.12f, 1.1f, .12f));
            }
        }

        // ---- jump aperture ---------------------------------------------------

        void BuildAperture()
        {
            aperture = new GameObject("Jump aperture").transform;
            aperture.SetParent(world, false);
            aperture.localPosition = new Vector3(10, 9.2f, 118);

            const int segments = 44;
            const float radius = 9.5f;
            var segLength = 2 * Mathf.PI * radius / segments * 1.08f;
            for (var i = 0; i < segments; i++)
            {
                var degrees = i * 360f / segments;
                if (degrees is > 96 and < 132) continue;
                var rad = degrees * Mathf.Deg2Rad;
                var point = new Vector3(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius, 0);
                Add(aperture, "Aperture segment", cube, steel, point,
                    new Vector3(.95f, segLength, .95f), Quaternion.Euler(0, 0, degrees));
                if (i % 2 != 0) continue;
                var inner = new Vector3(Mathf.Cos(rad) * 8.82f, Mathf.Sin(rad) * 8.82f, -.5f);
                Add(aperture, "Aperture rim light", cube, ice, inner,
                    new Vector3(.26f, segLength * .92f, .26f), Quaternion.Euler(0, 0, degrees));
            }

            // Detached shards drifting beyond the broken arc.
            for (var i = 0; i < 3; i++)
            {
                var degrees = 101 + i * 11;
                var rad = degrees * Mathf.Deg2Rad;
                var point = new Vector3(Mathf.Cos(rad) * 10.8f, Mathf.Sin(rad) * 10.8f, .3f * i);
                Add(aperture, "Broken shard", cube, steel, point, new Vector3(.8f, 1.3f, .8f),
                    Quaternion.Euler(0, 0, degrees + 22));
            }

            Pylon(204);
            Pylon(342);
            Pylon(76);

            Add(aperture, "Moon vista", DiscMesh(8.6f), vista, new Vector3(0, 0, .7f), Vector3.one,
                Quaternion.identity);
            Lamp("Aperture glow", new Vector3(10, 9.2f, 113.5f), new Color32(122, 196, 255, 255), 26, 6);
            Label("JUMP RELAY / 07", new Vector3(10, 20.4f, 118.2f), .34f,
                new Color32(154, 220, 240, 255));
        }

        void Pylon(float degrees)
        {
            var rad = degrees * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0);
            var tangent = new Vector3(-dir.y, dir.x, 0);
            var inner = dir * 11.2f;
            var outer = dir * 17f;
            Beam(aperture, "Pylon arm", steel, inner, outer, .55f, .9f);
            Beam(aperture, "Pylon brace", steel, inner + tangent * 1.1f, outer + tangent * .5f, .28f);
            Beam(aperture, "Pylon brace", steel, inner - tangent * 1.1f, outer - tangent * .5f, .28f);
            Box(aperture, "Pylon housing", dark, outer, new Vector3(2.6f, 2.6f, 3.4f),
                Quaternion.Euler(0, 0, degrees));
            for (int i = -1; i <= 1; i++)
            {
                Vector3 plate = outer + tangent * (.57f * i) + Vector3.back * 1.73f;
                Box(aperture, "Pylon cooling rib", steel, plate, new Vector3(.82f, .18f, .21f),
                    Quaternion.Euler(0, 0, degrees));
            }
            Beam(aperture, "Pylon tracking rail", steel, outer + dir * .8f - tangent * .88f,
                outer + dir * .8f + tangent * .88f, .15f);
            Add(aperture, "Pylon joint", sphere, steel, inner, Vector3.one * 1.8f, Quaternion.identity);
            Add(aperture, "Pylon lamp", sphere, ice, inner - dir * .6f, Vector3.one * .7f, Quaternion.identity);
            Beam(aperture, "Pylon conduit", orange, inner + tangent * .8f, outer + tangent * .4f, .1f);
        }

        // ---- moon ------------------------------------------------------------

        void BuildMoon()
        {
            moonRoot = new GameObject("Moon relay").transform;
            moonRoot.SetParent(world, false);

            RelaySolid(Box(moonRoot, "Regolith plain", regolith, new Vector3(0, -1.2f, 238),
                new Vector3(360, 2, 220)));
            for (var i = 0; i < 8; i++)
                Box(moonRoot, "Dust bank", rock,
                    new Vector3(-150 + i * 42, -.55f, 300 - (i % 3) * 24), new Vector3(34, 1.2f, 26));

            var random = new System.Random(5511);
            for (var i = 0; i < 80; i++)
            {
                var x = (float)random.NextDouble() * 340 - 170;
                var z = 138 + (float)random.NextDouble() * 196;
                if (Mathf.Abs(x) < 20 && z < 274) continue;
                if (x is > 8 and < 46 && z is > 242 and < 272) continue;
                var s = .6f + (float)random.NextDouble() * 5.2f;
                Add(moonRoot, "Boulder", sphere, i % 3 == 0 ? rock : regolith,
                    new Vector3(x, s * .28f - .2f, z),
                    new Vector3(s, s * .62f, s * (.8f + (float)random.NextDouble() * .4f)),
                    Quaternion.Euler(0, (float)random.NextDouble() * 360,
                        (float)random.NextDouble() * 12 - 6));
            }

            for (var i = 0; i < 9; i++)
                Add(moonRoot, "Crater rim", sphere, rock, new Vector3(-164 + i * 41, 4.2f, 334),
                    new Vector3(48, 14, 28), Quaternion.Euler(0, i * 23, 0));

            // Landing pad.
            var landingPad = Add(moonRoot, "Landing pad", cylinder, deck, new Vector3(0, .05f, 232),
                new Vector3(28, .25f, 28), Quaternion.identity);
            landingPad.gameObject.AddComponent<MeshCollider>().sharedMesh = cylinder;
            Add(moonRoot, "Pad rim", RingMesh(13.4f, 14.6f), steel, new Vector3(0, .31f, 232),
                Vector3.one, Quaternion.identity);
            Add(moonRoot, "Pad centre ring", RingMesh(2.8f, 3.2f), pale, new Vector3(0, .315f, 232),
                Vector3.one, Quaternion.identity);
            for (var i = 0; i < 8; i++)
            {
                var degrees = i * 45f;
                var rad = degrees * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(rad), 0, Mathf.Sin(rad));
                Box(moonRoot, "Pad chevron", orange, new Vector3(0, .318f, 232) + dir * 9.4f,
                    new Vector3(1.1f, .02f, 3.4f), Quaternion.LookRotation(dir, Vector3.up));
            }
            for (var i = 0; i < 12; i++)
            {
                var degrees = i * 30f + 15;
                var rad = degrees * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(rad) * 15.7f, 0, 232 + Mathf.Sin(rad) * 15.7f);
                Box(moonRoot, "Pad light post", steel, p + Vector3.up * .36f, new Vector3(.18f, .72f, .18f));
                Box(moonRoot, "Pad light", ice, p + Vector3.up * .76f, new Vector3(.44f, .12f, .44f));
                if (i % 3 == 0)
                    Lamp("Pad flood", p + Vector3.up * 1.2f, new Color32(188, 222, 255, 255), 24, 7);
            }

            for (var i = 0; i < 3; i++)
            {
                Beam("Power cable", orange, new Vector3(9.5f + i * .55f, .36f, 238),
                    new Vector3(12.6f + i * .55f, .36f, 247.5f), .12f);
                Beam("Power cable", orange, new Vector3(12.6f + i * .55f, .36f, 247.5f),
                    new Vector3(14.5f + i * .55f, 3.25f, 249.4f), .12f);
            }
            Cylinder("Cable reel", dark, new Vector3(10.5f, .55f, 244), 1.8f, .8f, Quaternion.Euler(90, 0, 0));

            // The stencilled service lane connects the lit pad to the ramp beside the cable trench.
            for (var i = 0; i < 5; i++)
                Box(moonRoot, "Airlock path marker", warm, new Vector3(7.8f, -.181f, 241.8f + i * 1.66f),
                    new Vector3(.12f, .025f, .50f));
            RelayRail(moonRoot, new Vector3(10.5f, .28f, 236.8f), new Vector3(7.5f, .28f, 243.4f));
            RelayRail(moonRoot, new Vector3(-11.5f, .28f, 229), new Vector3(-11.5f, .28f, 238.5f));
        }

        // ---- relay station ---------------------------------------------------

        static Transform RelaySolid(Transform item)
        {
            item.gameObject.AddComponent<BoxCollider>();
            return item;
        }

        void RelayRail(Transform root, Vector3 start, Vector3 end)
        {
            var span = end - start;
            var rise = Vector3.up * 1.12f;
            Beam(root, "Relay handrail", orange, start + rise, end + rise, .12f);
            Beam(root, "Relay midrail", steel, start + rise * .48f, end + rise * .48f, .08f);
            int bays = Mathf.CeilToInt(span.magnitude / 2.25f);
            for (int i = 0; i <= bays; i++)
                Box(root, "Relay rail post", steel, Vector3.Lerp(start, end, (float)i / bays) + rise * .5f,
                    new Vector3(.14f, rise.y, .14f));

            // Keep the visual openings between rails while retaining a solid safety barrier.
            var barrier = new GameObject("Relay rail collision").AddComponent<BoxCollider>();
            barrier.transform.SetParent(root, false);
            barrier.transform.localPosition = (start + end) * .5f + rise * .5f;
            barrier.transform.localRotation = Quaternion.FromToRotation(Vector3.right, span);
            barrier.size = new Vector3(span.magnitude, rise.y, .16f);
        }

        void BuildRelayAccess(Transform station)
        {
            var start = new Vector3(7.55f, -.2f, 250);
            var end = new Vector3(13.01f, 3.2f, 250);
            var slope = end - start;
            var tilt = Quaternion.FromToRotation(Vector3.right, slope);
            var normal = tilt * Vector3.up;
            RelaySolid(Box(station, "Airlock access ramp", deck, (start + end) * .5f - normal * .15f,
                new Vector3(slope.magnitude, .30f, 4.1f), tilt));
            for (int i = 1; i <= 10; i++)
                Box(station, "Non-slip ramp tread", steel, Vector3.Lerp(start, end, i / 11f) + normal * .012f,
                    new Vector3(.065f, .02f, 3.9f), tilt);
            foreach (int side in new[] { -1, 1 })
            {
                var offset = Vector3.forward * (side * 1.99f);
                var railStart = side < 0 ? Vector3.Lerp(start, end, .16f) : start;
                RelayRail(station, railStart + offset, end + offset);
                Beam(station, "Ramp support", steel, end + offset - Vector3.up * 2.85f, start + offset + slope * .48f, .16f);
                Box(station, "Landing edge marker", ice, end + offset + Vector3.up * .16f,
                    new Vector3(.21f, .11f, .11f));
            }

            RelaySolid(Box(station, "Airlock roof", habitat, new Vector3(17.8f, 6.75f, 250),
                new Vector3(4.8f, .35f, 4.4f)));
            Box(station, "Door lift housing", dark, new Vector3(15.47f, 8.17f, 250),
                new Vector3(.63f, 3.55f, 3.82f));
            Box(station, "Door lift cover", habitat, new Vector3(15.12f, 8.20f, 250),
                new Vector3(.09f, 3.41f, 3.64f));
            Box(station, "Door lintel", orange, new Vector3(15.1f, 6.43f, 250),
                new Vector3(.4f, .23f, 3.62f));

            foreach (int side in new[] { -1, 1 })
            {
                float z = 250 + side * 1.93f;
                RelaySolid(Box(station, "Airlock sidewall", habitat, new Vector3(17.8f, 4.92f, z),
                    new Vector3(4.8f, 3.44f, .36f)));
                Box(station, "Airlock wall lining", steel, new Vector3(17.8f, 4.22f, z - side * .195f),
                    new Vector3(4.63f, 1.64f, .07f));
                Box(station, "Airlock frame post", dark, new Vector3(15.35f, 4.8f, 250 + side * 1.68f),
                    new Vector3(.28f, 3.2f, .19f));
                Box(station, "Airlock status strip", warm, new Vector3(15.20f, 4.77f, 250 + side * 1.67f),
                    new Vector3(.02f, 1.36f, .06f));
                for (int i = 0; i < 3; i++)
                {
                    float x = 16.15f + i * 1.52f;
                    Box(station, "Airlock frame rib", dark, new Vector3(x, 4.92f, z - side * .23f),
                        new Vector3(.17f, 3.4f, .12f));
                    Box(station, "Guide light", warm, new Vector3(x, 3.246f, 250 + side * 1.40f),
                        new Vector3(.77f, .028f, .06f));
                    Beam(station, "Exterior conduit", orange, new Vector3(x, 3.2f, z + side * .22f),
                        new Vector3(x, 6.52f, z + side * .22f), .09f);
                }
            }

            relayDoor = new GameObject("Relay airlock door").transform;
            relayDoor.SetParent(station, false);
            relayDoor.localPosition = new Vector3(15.34f, 4.72f, 250);
            RelaySolid(Box(relayDoor, "Seal panel", steel, Vector3.zero, new Vector3(.16f, 3.02f, 3.28f)));
            Box(relayDoor, "Door rib", orange, new Vector3(-.092f, .04f, 0), new Vector3(.04f, .19f, 3.16f));
            for (int i = 0; i < 6; i++)
                Box(relayDoor, "Door louver", dark, new Vector3(-.091f, -.32f - i * .14f, 0),
                    new Vector3(.02f, .048f, 2.51f));

            Box(station, "Airlock threshold", orange, new Vector3(15.36f, 3.215f, 250),
                new Vector3(.48f, .03f, 3.19f));
            Box(station, "Airlock ceiling light", warm, new Vector3(17.87f, 6.546f, 250),
                new Vector3(3.62f, .05f, .29f));
            for (int i = 0; i < 4; i++)
            {
                float x = 16.1f + 1.21f * i;
                Box(station, "Airlock floor plate", i % 2 == 0 ? steel : deck,
                    new Vector3(x, 3.212f, 250), new Vector3(1.17f, .024f, 3.37f));
                Box(station, "Service seam", dark, new Vector3(x + .6f, 3.228f, 250), new Vector3(.035f, .02f, 3.28f));
                Box(station, "Cable tray bracket", dark, new Vector3(x, 6.25f, 251.52f),
                    new Vector3(.09f, .24f, .40f));
            }
            foreach (float z in new[] { 251.4f, 251.62f })
                Beam(station, "Airlock cable tray", orange, new Vector3(15.76f, 6.2f, z),
                    new Vector3(20.17f, 6.2f, z), .054f);
            Label("AIRLOCK", new Vector3(15.057f, 7.35f, 250), .24f, new Color32(255, 214, 168, 255), 90);
            Lamp("Airlock light", new Vector3(14.4f, 5.2f, 249.5f), new Color32(255, 178, 108, 255), 9, 5.2f);
            Lamp("Airlock interior", new Vector3(17.7f, 6.18f, 250), new Color32(255, 196, 132, 255), 6, 4.2f);
        }

        void BuildStation()
        {
            var station = new GameObject("Relay station").transform;
            station.SetParent(moonRoot, false);

            RelaySolid(Box(station, "Station deck", deck, new Vector3(28, 1.6f, 257),
                new Vector3(30, 3.2f, 24)));
            Box(station, "Deck kerb", orange, new Vector3(13.2f, 3.28f, 246.45f), new Vector3(.35f, .16f, 2.7f));
            Box(station, "Deck kerb", orange, new Vector3(13.2f, 3.28f, 260.55f), new Vector3(.35f, .16f, 16.9f));
            Box(station, "Deck kerb", orange, new Vector3(28, 3.28f, 245.2f), new Vector3(30, .16f, .35f));
            Box(station, "Deck kerb", orange, new Vector3(28, 3.28f, 268.8f), new Vector3(30, .16f, .35f));
            for (var i = 0; i < 5; i++)
            {
                var x = 15.5f + i * 6;
                Beam(station, "Deck strut", steel, new Vector3(x, .05f, 246.4f),
                    new Vector3(x, 1.7f, 250.2f), .4f);
                Beam(station, "Deck strut", steel, new Vector3(x, .05f, 267.6f),
                    new Vector3(x, 1.7f, 263.8f), .4f);
            }

            BuildRelayAccess(station);

            // Habitats.
            Add(station, "Habitat A", cylinder, habitat, new Vector3(26, 6.2f, 257.8f),
                new Vector3(5.4f, 6.2f, 5.4f), Quaternion.Euler(0, 0, 90));
            Sphere(station, "Habitat A cap", habitat, new Vector3(19.8f, 6.2f, 257.8f), 5.4f);
            Sphere(station, "Habitat A cap", habitat, new Vector3(32.2f, 6.2f, 257.8f), 5.4f);
            for (var i = 0; i < 3; i++)
                Add(station, "Habitat band", cylinder, orange, new Vector3(22 + i * 4, 6.2f, 257.8f),
                    new Vector3(5.55f, .2f, 5.55f), Quaternion.Euler(0, 0, 90));
            for (var i = 0; i < 4; i++)
            {
                float x = 22.6f + i * 2.1f;
                Box(station, "Habitat window surround", dark, new Vector3(x, 6.5f, 255.05f),
                    new Vector3(1.15f, .79f, .14f));
                Box(station, "Habitat window", glow, new Vector3(x, 6.5f, 254.96f),
                    new Vector3(.89f, .53f, .06f));
                Box(station, "Habitat window spine", steel, new Vector3(x, 6.5f, 254.92f),
                    new Vector3(.06f, .54f, .03f));
            }
            foreach (var x in new[] { 21, 31 })
            {
                Box(station, "Habitat cradle", steel, new Vector3(x, 3.55f, 257.8f), new Vector3(1.1f, .7f, 5.8f));
            }

            Add(station, "Habitat B", cylinder, habitat, new Vector3(35, 5.8f, 262),
                new Vector3(4.6f, 4.4f, 4.6f), Quaternion.Euler(0, 0, 90));
            Sphere(station, "Habitat B cap", habitat, new Vector3(30.6f, 5.8f, 262), 4.6f);
            Sphere(station, "Habitat B cap", habitat, new Vector3(39.4f, 5.8f, 262), 4.6f);
            Box(station, "Connector", habitat, new Vector3(32.6f, 5.9f, 258.6f), new Vector3(2.6f, 2.8f, 7),
                Quaternion.Euler(0, 24, 0));
            for (var i = 0; i < 3; i++)
                Box(station, "Habitat B window", glow, new Vector3(32.5f + i * 1.8f, 6.1f, 259.75f),
                    new Vector3(.7f, .5f, .06f));

            // Truss and solar wings.
            for (var i = 0; i < 3; i++)
            {
                var x = 39.6f + i * 1.35f;
                Beam(station, "Truss leg", steel, new Vector3(x, 3.2f, 250),
                    new Vector3(x, 13.2f, 250), .16f);
            }
            for (var i = 0; i < 5; i++)
            {
                var y = 4.2f + i * 2.2f;
                Beam(station, "Truss cross", steel, new Vector3(39.6f, y, 250),
                    new Vector3(42.3f, y, 250), .12f);
                Beam(station, "Truss brace", dark, new Vector3(39.6f, y, 250),
                    new Vector3(42.3f, y + 2.2f, 250), .08f);
            }
            Beam(station, "Array boom", steel, new Vector3(41, 13.1f, 246), new Vector3(41, 13.1f, 258), .22f);
            foreach (var side in new[] { -1, 1 })
            for (var i = 0; i < 3; i++)
            {
                var x = 41 + side * (3.4f + i * 2.1f);
                Box(station, "Solar panel", solar, new Vector3(x, 13.1f, 252), new Vector3(1.9f, .08f, 3.4f));
                Box(station, "Solar frame", steel, new Vector3(x, 13.1f, 252), new Vector3(2.02f, .05f, 3.52f));
            }

            // Relay dish on a mast, tilted toward the pad and the sky.
            Beam(station, "Dish mast", steel, new Vector3(30.9f, 3.2f, 252.9f), new Vector3(30.9f, 12.7f, 252.9f), .3f);
            Beam(station, "Dish brace", steel, new Vector3(30.9f, 7.8f, 252.9f), new Vector3(29.3f, 3.3f, 253.35f), .14f);
            var dish = relayDish = new GameObject("Relay dish").transform;
            dish.SetParent(station, false);
            dish.localPosition = new Vector3(30.9f, 12.7f, 252.9f);
            dish.localRotation = Quaternion.Euler(-55, 0, 0);
            Add(dish, "Dish rim", RingMesh(4.2f, 4.7f), steel, Vector3.zero, Vector3.one, Quaternion.identity);
            Add(dish, "Dish hub", RingMesh(.9f, 1.3f), steel, Vector3.zero, Vector3.one, Quaternion.identity);
            for (var i = 0; i < 8; i++)
            {
                var rad = i * 45f * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(rad), 0, Mathf.Sin(rad));
                Beam(dish, "Dish spoke", steel, dir * 1.2f, dir * 4.3f, .1f);
            }
            Sphere(dish, "Dish feed", dark, new Vector3(0, 3.0f, 0), .7f);
            Beam(dish, "Feed strut", steel, Vector3.zero, new Vector3(0, 3.0f, 0), .09f);
            Lamp("Dish beacon", new Vector3(30.9f, 14.25f, 252.9f), new Color32(150, 220, 255, 255), 5, 2.2f);

            // Docking arm with two elbows and a grapple head.
            var arm = new GameObject("Docking arm").transform;
            arm.SetParent(station, false);
            arm.localPosition = new Vector3(14.6f, 3.4f, 259);
            Box(arm, "Arm base", steel, new Vector3(0, .7f, 0), new Vector3(1.9f, 1.4f, 1.9f));
            Add(arm, "Shoulder", cylinder, orange, new Vector3(0, 1.5f, 0), new Vector3(1.1f, .7f, 1.1f),
                Quaternion.Euler(0, 0, 90));
            Beam(arm, "Upper arm", pale, new Vector3(0, 1.5f, 0), new Vector3(-4.6f, 4.4f, 1.6f), .42f);
            Add(arm, "Elbow", cylinder, orange, new Vector3(-4.6f, 4.4f, 1.6f), new Vector3(1.2f, .75f, 1.2f),
                Quaternion.Euler(0, 0, 90));
            Beam(arm, "Forearm", pale, new Vector3(-4.6f, 4.4f, 1.6f), new Vector3(-8.6f, 3.2f, 4.6f), .34f);
            Add(arm, "Wrist", cylinder, orange, new Vector3(-8.6f, 3.2f, 4.6f), new Vector3(.95f, .65f, .95f),
                Quaternion.Euler(0, 0, 90));
            Sphere(arm, "Grapple head", dark, new Vector3(-9.5f, 3.05f, 5.4f), 1.25f);
            Beam(arm, "Grapple jaw", steel, new Vector3(-9.5f, 3.05f, 5.4f),
                new Vector3(-10.7f, 2.5f, 6.2f), .13f);
            Beam(arm, "Grapple jaw", steel, new Vector3(-9.5f, 3.05f, 5.4f),
                new Vector3(-10.5f, 3.6f, 6.1f), .13f);
            Beam(arm, "Arm cable", orange, new Vector3(0, 1.7f, 0), new Vector3(-4.6f, 4.6f, 1.6f), .07f);
            Beam(arm, "Arm cable", orange, new Vector3(-4.6f, 4.6f, 1.6f),
                new Vector3(-8.6f, 3.4f, 4.6f), .07f);

            // Pressure tank beside the arm.
            Cylinder("Pressure tank", pale, new Vector3(16.4f, 4.9f, 263), 3.1f, 5.2f);
            foreach (var y in new[] { 4.0f, 5.8f })
                Cylinder("Tank band", orange, new Vector3(16.4f, y, 263), 3.2f, .22f);
            Box("Tank base", steel, new Vector3(16.4f, 3.35f, 263), new Vector3(3.4f, .3f, 3.4f));

            RelayRail(station, new Vector3(13.17f, 3.2f, 252.1f), new Vector3(13.17f, 3.2f, 268.7f));
            RelayRail(station, new Vector3(13.17f, 3.2f, 245.25f), new Vector3(13.17f, 3.2f, 247.9f));
            RelayRail(station, new Vector3(13.17f, 3.2f, 245.25f), new Vector3(42.8f, 3.2f, 245.25f));
            RelayRail(station, new Vector3(13.17f, 3.2f, 268.7f), new Vector3(42.8f, 3.2f, 268.7f));
            RelayRail(station, new Vector3(42.8f, 3.2f, 245.25f), new Vector3(42.8f, 3.2f, 268.7f));

            Label("RELAY STATION", new Vector3(20.4f, 7.1f, 249.4f), .3f,
                new Color32(214, 231, 226, 255));
            Label("COMMS / 02", new Vector3(23.2f, 5.32f, 252.23f), .16f,
                new Color32(214, 231, 226, 255));
            for (int i = 0; i < 4; i++)
            {
                float x = 20.65f + .77f * i;
                Box(station, "Uplink lane chevron", orange, new Vector3(x, 3.226f, 251.29f),
                    new Vector3(.19f, .025f, .46f), Quaternion.Euler(0, -35, 0));
            }
            Lamp("Station work light", new Vector3(20, 7.2f, 253), new Color32(255, 216, 174, 255), 12, 5.5f);
            Lamp("Station work light", new Vector3(30, 7.2f, 259), new Color32(186, 220, 255, 255), 12, 4.5f);
        }

        // ---- rover -----------------------------------------------------------

        void BuildRover()
        {
            var rover = relayRover = new GameObject("Maintenance rover").transform;
            rover.SetParent(moonRoot, false);
            rover.localPosition = new Vector3(7.4f, 0, 239);
            rover.localRotation = Quaternion.Euler(0, 42, 0);

            Box(rover, "Rover chassis", pale, new Vector3(0, 1.15f, 0), new Vector3(2.2f, .9f, 3.2f));
            Box(rover, "Rover skirt", dark, new Vector3(0, .72f, 0), new Vector3(2.4f, .35f, 3.4f));
            Box(rover, "Rover deck", steel, new Vector3(0, 1.66f, 0), new Vector3(2.0f, .12f, 2.9f));
            foreach (var x in new[] { -1.25f, 1.25f })
            foreach (var z in new[] { -1.05f, 1.05f })
            {
                Add(rover, "Rover wheel", cylinder, dark, new Vector3(x, .55f, z),
                    new Vector3(1.15f, .42f, 1.15f), Quaternion.Euler(0, 0, 90));
                Add(rover, "Wheel hub", cylinder, steel, new Vector3(x, .55f, z),
                    new Vector3(.52f, .44f, .52f), Quaternion.Euler(0, 0, 90));
            }

            Add(rover, "Sensor mast", cylinder, steel, new Vector3(0, 2.5f, 1.1f),
                new Vector3(.12f, 1.2f, .12f), Quaternion.identity);
            Box(rover, "Sensor head", dark, new Vector3(0, 3.3f, 1.1f), new Vector3(.5f, .35f, .7f));
            Sphere(rover, "Sensor eye", glow, new Vector3(0, 3.3f, 1.48f), .22f);
            Add(rover, "Rover dish", sphere, pale, new Vector3(.6f, 2.0f, -1.0f),
                new Vector3(.9f, .25f, .9f), Quaternion.Euler(-28, 0, 0));
            Box(rover, "Guide light", warm, new Vector3(0, 1.5f, 1.63f), new Vector3(.72f, .16f, .06f));
            Box(rover, "Rover stripe", orange, new Vector3(0, 1.2f, 1.62f), new Vector3(2.24f, .16f, .04f));
            Lamp(rover, "Rover guide light", new Vector3(0, 1.6f, 2.0f),
                new Color32(255, 186, 112, 255), 6, 3.2f);
        }

        // ---- sky -------------------------------------------------------------

        void BuildSky()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var random = new System.Random(9091);
            const float radius = 520;
            for (var i = 0; i < 1100; i++)
            {
                var y = (float)random.NextDouble() * 1.4f - .35f;
                var azimuth = (float)random.NextDouble() * Mathf.PI * 2;
                var horizontal = Mathf.Sqrt(Mathf.Max(0, 1 - y * y));
                var dir = new Vector3(horizontal * Mathf.Cos(azimuth), y, horizontal * Mathf.Sin(azimuth));
                var centre = dir * radius;
                var right = Vector3.Cross(Vector3.up, dir);
                if (right.sqrMagnitude < .01f) right = Vector3.right;
                right.Normalize();
                var up = Vector3.Cross(dir, right).normalized;
                var size = .28f + 1.1f * Mathf.Pow((float)random.NextDouble(), 4);
                var start = vertices.Count;
                vertices.Add(centre - right * size - up * size * .6f);
                vertices.Add(centre + right * size - up * size * .6f);
                vertices.Add(centre + up * size);
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 1);
            }
            var starMesh = new Mesh { name = "Flight stars" };
            starMesh.SetVertices(vertices);
            starMesh.SetTriangles(triangles, 0);
            var normals = new Vector3[vertices.Count];
            for (var i = 0; i < normals.Length; i++) normals[i] = -vertices[i].normalized;
            starMesh.normals = normals;
            owned.Add(starMesh);
            Add(world, "Star field", starMesh, starfield, Vector3.zero, Vector3.one, Quaternion.identity);

            var planet = new Vector3(-150, 108, 468);
            var tilt = Quaternion.Euler(-12, 0, -18);
            Add(world, "Ice giant", sphere, planetSkin, planet, Vector3.one * 120, tilt);
            Add(world, "Planet ring", RingMesh(76, 83, 96), ringDust, planet, Vector3.one, tilt);
            Add(world, "Planet ring", RingMesh(89, 97, 96), ringDust, planet, Vector3.one, tilt);

            var comet = new GameObject("Distant comet").transform;
            comet.SetParent(world, false);
            comet.localPosition = new Vector3(-118, 60, 296);
            Add(comet, "Comet nucleus", sphere, rock, Vector3.zero, new Vector3(2.8f, 1.9f, 2.3f),
                Quaternion.Euler(18, 22, 8));
            for (var i = 0; i < 8; i++)
            {
                var s = 2.2f - i * .22f;
                Add(comet, "Comet tail", cube, cometDust,
                    new Vector3(2.6f + i * 3.1f, i * 1.15f, i * .4f),
                    new Vector3(s, s * .5f, s * .4f), Quaternion.Euler(0, 0, 32));
            }

            // A small service craft drifts across the bank view.
            var craft = new GameObject("Service craft").transform;
            craft.SetParent(world, false);
            craft.localPosition = new Vector3(7.2f, 11.4f, 72);
            craft.localRotation = Quaternion.Euler(4, -18, 8);
            Box(craft, "Craft body", pale, Vector3.zero, new Vector3(1.6f, 1.1f, 3.2f));
            Box(craft, "Craft nose", dark, new Vector3(0, 0, 1.8f), new Vector3(1.2f, .8f, .8f));
            foreach (var side in new[] { -1, 1 })
            {
                Beam(craft, "Craft arm", steel, new Vector3(side * .8f, .1f, .4f),
                    new Vector3(side * 2.4f, .9f, -.6f), .16f);
                Add(craft, "Craft thruster", cylinder, dark, new Vector3(side * 1.1f, .1f, -1.5f),
                    new Vector3(.5f, .35f, .5f), Quaternion.Euler(90, 0, 0));
            }
            Box(craft, "Craft beacon", ice, new Vector3(0, .62f, .2f), new Vector3(.3f, .08f, .3f));
        }
    }
}
