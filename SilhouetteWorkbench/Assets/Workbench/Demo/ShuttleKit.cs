using System.Collections.Generic;
using UnityEngine;

namespace Workbench.Demo
{
    /// <summary>Materials a shuttle build needs. The owner creates and disposes them.</summary>
    public sealed class ShuttlePalette
    {
        public Material Ceramic, Metal, Dark, Glass, Stripe, Glow, ConsoleGlow, Seat;
    }

    /// <summary>Handles on the animated parts of a built shuttle.</summary>
    public sealed class ShuttleRig
    {
        public Transform Root;
        public Transform CockpitEye;
        public Transform BoardingHatch;
        public readonly List<Transform> Gear = new();
        public readonly List<Transform> Fins = new();

        public void SetGear(float deploy)
        {
            deploy = Mathf.Clamp01(deploy);
            for (var i = 0; i < Gear.Count; i++)
            {
                var folded = i == 0 ? 88f : 96f;
                Gear[i].localRotation = Quaternion.Euler(Mathf.Lerp(folded, 0f, deploy), 0, 0);
            }
        }

        public void SetFins(float spread)
        {
            for (var i = 0; i < Fins.Count; i++)
            {
                var side = Fins[i].localPosition.x < 0 ? -1f : 1f;
                var rest = Fins[i].localPosition.y > 0 ? 18f : 32f;
                Fins[i].localRotation = Quaternion.Euler(0, 0, side * (rest + spread));
            }
        }
    }

    /// <summary>Builds the salvage shuttle and, optionally, its cockpit.</summary>
    public static class ShuttleKit
    {
        public const float NoseZ = 2.9f;

        static Mesh cube, cylinder, sphere;

        static Mesh Cube => cube != null ? cube : cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        static Mesh Cylinder => cylinder != null ? cylinder : cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        static Mesh Sphere => sphere != null ? sphere : sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");

        public static ShuttleRig Build(Transform parent, ShuttlePalette palette, bool interior, List<Object> owned)
        {
            var rig = new ShuttleRig();
            var root = new GameObject("Salvage shuttle").transform;
            root.SetParent(parent, false);
            rig.Root = root;

            Add(root, "Hull", Loft(owned), palette.Ceramic);
            Nose(root, palette);
            Windshield(root, palette);
            Pods(root, palette, owned);
            Radiators(rig, root, palette, owned);
            LandingGear(rig, root, palette);
            Trim(rig, root, palette);
            if (interior) Cockpit(rig, root, palette);
            rig.SetGear(1);
            rig.SetFins(0);
            return rig;
        }

        static Mesh Loft(List<Object> owned)
        {
            float[] z = { -4.3f, -2.6f, -.6f, 1.6f, NoseZ };
            float[] w = { 1.35f, 1.8f, 1.92f, 1.8f, 1.62f };
            float[] h = { .62f, .9f, .98f, .94f, .86f };
            float[] y = { .1f, .02f, 0, 0, .02f };
            Vector2[] section =
            {
                new(0, 1), new(.72f, .72f), new(1, 0), new(.72f, -.72f),
                new(0, -1), new(-.72f, -.72f), new(-1, 0), new(-.72f, .72f)
            };
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (var i = 0; i < z.Length; i++)
            foreach (var corner in section)
                vertices.Add(new Vector3(corner.x * w[i], corner.y * h[i] + y[i], z[i]));
            for (var i = 0; i < z.Length - 1; i++)
            for (var j = 0; j < 8; j++)
            {
                var a = i * 8 + j;
                var b = i * 8 + (j + 1) % 8;
                var c = (i + 1) * 8 + (j + 1) % 8;
                var d = (i + 1) * 8 + j;
                triangles.AddRange(new[] { a, c, b, a, d, c });
            }
            // Close the aft bulkhead. The forward section opens into the canopy.
            for (var j = 1; j < section.Length - 1; j++)
                triangles.AddRange(new[] { 0, j, j + 1 });
            var mesh = new Mesh { name = "Shuttle hull" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            owned.Add(mesh);
            return mesh;
        }

        static void Nose(Transform root, ShuttlePalette palette)
        {
            Box(root, "Chin plate", palette.Ceramic, new Vector3(0, -.42f, 3.55f), new Vector3(2.55f, .5f, 1.55f), Quaternion.Euler(-12, 0, 0));
            Box(root, "Nose tip", palette.Ceramic, new Vector3(0, -.08f, 4.32f), new Vector3(1.55f, .58f, .72f), Quaternion.Euler(6, 0, 0));
            Box(root, "Brow", palette.Ceramic, new Vector3(0, .84f, 3.35f), new Vector3(2.9f, .22f, 1.45f), Quaternion.Euler(14, 0, 0));
            foreach (var side in new[] { -1, 1 })
                Box(root, "Cheek", palette.Ceramic, new Vector3(side * 1.3f, .3f, 3.3f),
                    new Vector3(.46f, .92f, 1.25f), Quaternion.Euler(0, side * -14, 0));
        }

        static void Windshield(Transform root, ShuttlePalette palette)
        {
            var centre = new Vector3(0, .42f, 3.68f);
            Pane(root, "Centre pane", palette, centre, new Vector3(1.46f, .82f, .035f), Quaternion.Euler(-20, 0, 0));
            Pane(root, "Port pane", palette, new Vector3(-1.2f, .42f, 3.42f),
                new Vector3(1.22f, .82f, .035f), Quaternion.Euler(-20, 40, 0));
            Pane(root, "Starboard pane", palette, new Vector3(1.2f, .42f, 3.42f),
                new Vector3(1.22f, .82f, .035f), Quaternion.Euler(-20, -40, 0));
            foreach (var side in new[] { -1, 1 })
            {
                Box(root, "A-pillar", palette.Metal, new Vector3(side * .76f, .46f, 3.6f),
                    new Vector3(.1f, 1.0f, .12f), Quaternion.Euler(-20, side * -22, 0));
                Box(root, "C-pillar", palette.Metal, new Vector3(side * 1.68f, .44f, 3.15f),
                    new Vector3(.1f, .98f, .12f), Quaternion.Euler(-20, side * -46, 0));
            }
            Box(root, "Sill", palette.Metal, new Vector3(0, -.02f, 3.66f), new Vector3(2.95f, .1f, .16f), Quaternion.Euler(-20, 0, 0));
            Box(root, "Header", palette.Metal, new Vector3(0, .9f, 3.55f), new Vector3(2.95f, .11f, .16f), Quaternion.Euler(-20, 0, 0));
            Box(root, "Wiper", palette.Dark, new Vector3(-.42f, .1f, 3.74f), new Vector3(.62f, .035f, .05f), Quaternion.Euler(-20, 0, 18));
        }

        static void Pods(Transform root, ShuttlePalette palette, List<Object> owned)
        {
            var casing = Revolve("Tapered pod cowling",
                new[] { new Vector2(.43f, -1.16f), new Vector2(.58f, -.80f), new Vector2(.58f, .56f),
                    new Vector2(.48f, .99f), new Vector2(.33f, 1.20f), new Vector2(.0f, 1.20f),
                    new Vector2(0, -1.16f) }, owned);
            var nozzle = Revolve("Open expansion nozzle",
                new[] { new Vector2(.57f, -.42f), new Vector2(.43f, .12f), new Vector2(.28f, .37f),
                    new Vector2(.23f, .33f), new Vector2(.38f, .03f), new Vector2(.50f, -.42f) }, owned);
            var rim = Revolve("Machined nozzle rim", new[]
            {
                new Vector2(.576f, -.43f), new Vector2(.58f, -.36f),
                new Vector2(.527f, -.34f), new Vector2(.5f, -.43f)
            }, owned);
            foreach (var side in new[] { -1, 1 })
            {
                var z = side < 0 ? -1.6f : -1.3f;
                var pod = new Vector3(side * 1.95f, -.08f, z);
                Add(root, "Engine cowling", casing, palette.Ceramic, pod, Vector3.one, Quaternion.identity);
                var throat = pod + Vector3.back * 1.18f;
                Add(root, "Nozzle bell", nozzle, palette.Dark, throat, Vector3.one, Quaternion.identity);
                Add(root, "Nozzle rim", rim, palette.Metal, throat, Vector3.one, Quaternion.identity);
                Add(root, "Recessed thrust core", Cylinder, palette.Glow, throat + Vector3.forward * .21f,
                    new Vector3(.39f, .018f, .39f), Quaternion.Euler(90, 0, 0));
                for (var i = 0; i < 10; i++)
                {
                    var azimuth = Quaternion.Euler(0, 0, i * 36);
                    Box(root, "Nozzle cooling rib", palette.Metal,
                        throat + azimuth * new Vector3(0, .47f, -.14f),
                        new Vector3(.045f, .07f, .42f), azimuth * Quaternion.Euler(16, 0, 0));
                }
                Box(root, "Pod pylon", palette.Metal, new Vector3(side * 1.52f, -.06f, z),
                    new Vector3(.78f, .34f, 1.5f), Quaternion.Euler(0, side * -8, 0));
                Box(root, "Pod service stripe", palette.Stripe, pod + new Vector3(side * .562f, .10f, .25f),
                    new Vector3(.045f, .12f, .86f), Quaternion.Euler(0, 0, side * -10));
            }
        }

        static Mesh Revolve(string name, Vector2[] profile, List<Object> owned)
        {
            const int sides = 24;
            var vertices = new Vector3[profile.Length * sides];
            var indices = new List<int>(vertices.Length * 6);
            for (var ring = 0; ring < profile.Length; ring++)
            for (var side = 0; side < sides; side++)
            {
                var angle = side * Mathf.PI * 2 / sides;
                var point = profile[ring];
                vertices[ring * sides + side] = new Vector3(Mathf.Cos(angle) * point.x,
                    Mathf.Sin(angle) * point.x, point.y);
                var a = ring * sides + side;
                var b = ring * sides + (side + 1) % sides;
                var c = (ring + 1) % profile.Length * sides + (side + 1) % sides;
                var d = (ring + 1) % profile.Length * sides + side;
                if (point.x > 0) indices.AddRange(new[] { a, b, c });
                if (profile[(ring + 1) % profile.Length].x > 0) indices.AddRange(new[] { a, c, d });
            }
            var mesh = new Mesh { name = name, vertices = vertices };
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            owned.Add(mesh);
            return mesh;
        }

        static void Radiators(ShuttleRig rig, Transform root, ShuttlePalette palette, List<Object> owned)
        {
            var exchanger = RadiatorShell(owned);
            foreach (var side in new[] { -1, 1 })
            {
                var large = new GameObject(side < 0 ? "Port radiator" : "Starboard radiator").transform;
                large.SetParent(root, false);
                large.localPosition = new Vector3(side * 2.05f, .42f, -1.0f);
                Add(large, "Swept radiator shell", exchanger, palette.Metal, new Vector3(side * .62f, .42f, 0),
                    Vector3.one, Quaternion.identity);
                Box(large, "Radiator spine", palette.Dark, new Vector3(side * .62f, .38f, -.05f),
                    new Vector3(.12f, .12f, 2.8f));
                for (var fin = 0; fin < 10; fin++)
                    Box(large, "Radiator slat", palette.Ceramic, new Vector3(side * .62f, .44f, -.98f + fin * .205f),
                        new Vector3(.155f, .86f, .028f), Quaternion.Euler(9, 0, 0));
                Box(large, "Radiator leading-edge cap", palette.Stripe, new Vector3(side * .62f, .23f, 1.2f),
                    new Vector3(.09f, .52f, .055f), Quaternion.Euler(-10, 0, 0));
                rig.Fins.Add(large);

                var small = new GameObject(side < 0 ? "Port lower fin" : "Starboard lower fin").transform;
                small.SetParent(root, false);
                small.localPosition = new Vector3(side * 1.95f, -.42f, -1.7f);
                Add(small, "Lower fin panel", exchanger, palette.Dark, new Vector3(side * .42f, -.18f, 0),
                    new Vector3(1, .58f, .72f), Quaternion.Euler(180, 0, 0));
                rig.Fins.Add(small);
            }
        }

        static Mesh RadiatorShell(List<Object> owned)
        {
            Vector2[] outline = { new(-.56f, -1.5f), new(.40f, -1.22f), new(.6f, -.73f),
                new(.41f, .65f), new(-.08f, 1.32f), new(-.56f, 1.20f) };
            var n = outline.Length;
            var vertices = new Vector3[n * 2];
            var faces = new List<int>();
            for (var i = 0; i < n; i++)
            {
                vertices[i] = new Vector3(-.044f, outline[i].x, outline[i].y);
                vertices[n + i] = new Vector3(.044f, outline[i].x, outline[i].y);
                var next = (i + 1) % n;
                faces.AddRange(new[] { i, next, n + next, i, n + next, n + i });
                if (i < 1 || i >= n - 1) continue;
                faces.AddRange(new[] { 0, i + 1, i, n, n + i, n + i + 1 });
            }
            var mesh = new Mesh { name = "Swept radiator shell", vertices = vertices };
            mesh.SetTriangles(faces, 0);
            mesh.RecalculateNormals();
            owned.Add(mesh);
            return mesh;
        }

        static void LandingGear(ShuttleRig rig, Transform root, ShuttlePalette palette)
        {
            var nose = new GameObject("Nose gear").transform;
            nose.SetParent(root, false);
            nose.localPosition = new Vector3(0, -.5f, 2.25f);
            Strut(nose, palette, .1f, .95f, new Vector3(.44f, .07f, .58f));
            rig.Gear.Add(nose);
            foreach (var side in new[] { -1, 1 })
            {
                var leg = new GameObject(side < 0 ? "Port main gear" : "Starboard main gear").transform;
                leg.SetParent(root, false);
                leg.localPosition = new Vector3(side * 1.2f, -.5f, -.85f);
                Strut(leg, palette, .12f, .92f, new Vector3(.52f, .08f, .74f));
                rig.Gear.Add(leg);
            }
        }

        static void Strut(Transform pivot, ShuttlePalette palette, float radius, float length, Vector3 foot)
        {
            Add(pivot, "Main strut casing", Cylinder, palette.Metal, new Vector3(0, -length * .29f, 0),
                new Vector3(radius * 2.35f, length * .29f, radius * 2.35f), Quaternion.identity);
            Add(pivot, "Exposed piston", Cylinder, palette.Ceramic, new Vector3(0, -length * .67f, 0),
                new Vector3(radius * 1.3f, length * .26f, radius * 1.3f), Quaternion.identity);
            Add(pivot, "Gear brace", Cylinder, palette.Dark, new Vector3(0, -length * .42f, -.12f),
                new Vector3(radius * 1.2f, length * .4f, radius * 1.2f), Quaternion.Euler(16, 0, 0));
            Add(pivot, "Gear hinge", Cylinder, palette.Stripe, new Vector3(0, -.055f, 0),
                new Vector3(.24f, .16f, .24f), Quaternion.Euler(0, 0, 90));
            Box(pivot, "Gear foot", palette.Dark, new Vector3(0, -length, 0), foot);
            Box(pivot, "Foot wear plate", palette.Metal, new Vector3(0, -length + .048f, 0), foot * .82f);
            Box(pivot, "Gear lamp", palette.Glow, new Vector3(0, -length + .04f, foot.z * .5f), new Vector3(.16f, .03f, .04f));
        }

        static void Trim(ShuttleRig rig, Transform root, ShuttlePalette palette)
        {
            foreach (var side in new[] { -1, 1 })
            {
                Box(root, "Crew stripe", palette.Stripe, new Vector3(side * 1.9f, -.06f, .55f), new Vector3(.035f, .24f, 4.5f));
                Box(root, "Hull seam", palette.Metal, new Vector3(side * 1.93f, .42f, -1.9f), new Vector3(.02f, .05f, 2.2f));
                Box(root, "Equipment bay", palette.Dark, new Vector3(side * 1.87f, .11f, .05f),
                    new Vector3(.09f, .31f, .86f));
                for (var i = 0; i < 6; i++)
                    Box(root, "Bay vent", palette.Metal, new Vector3(side * 1.93f, .11f, -.26f + i * .11f),
                        new Vector3(.035f, .24f, .04f), Quaternion.Euler(12, 0, 0));
                Add(root, "Nav light", Cube, palette.Glow,
                    new Vector3(side * 2.32f, .86f, -2.3f), Vector3.one * .12f, Quaternion.identity);
            }

            Box(root, "Cargo hatch", palette.Dark, new Vector3(0, -.99f, .1f), new Vector3(1.15f, .05f, 1.55f));
            Box(root, "Cargo lip", palette.Metal, new Vector3(0, -1.02f, .1f), new Vector3(1.28f, .03f, 1.68f));

            var hatch = new GameObject("Boarding hatch").transform;
            hatch.SetParent(root, false);
            hatch.localPosition = new Vector3(-1.93f, -.16f, .9f);
            Box(hatch, "Hatch recess", palette.Dark, Vector3.zero, new Vector3(.05f, 1.0f, .92f));
            foreach (var side in new[] { -1, 1 })
            {
                Box(hatch, "Hatch jamb", palette.Metal, new Vector3(-.05f, 0, side * .49f),
                    new Vector3(.045f, 1.11f, .07f));
                Box(hatch, "Hatch crosspiece", palette.Metal, new Vector3(-.05f, side * .52f, 0),
                    new Vector3(.045f, .07f, .94f));
            }
            Box(hatch, "Latch handle", palette.Stripe, new Vector3(-.075f, -.06f, -.25f),
                new Vector3(.06f, .085f, .24f));
            Box(hatch, "Access lamp", palette.Glow, new Vector3(-.08f, .17f, -.28f),
                new Vector3(.02f, .04f, .12f));
            rig.BoardingHatch = hatch;

            Add(root, "Comms mast", Cylinder, palette.Metal, new Vector3(0, 1.08f, -.55f), new Vector3(.045f, .8f, .045f), Quaternion.identity);
            Add(root, "Comms tip", Cube, palette.Glow, new Vector3(0, 1.5f, -.55f), Vector3.one * .1f, Quaternion.identity);
            Box(root, "Dorsal spine", palette.Metal, new Vector3(0, .92f, -.9f), new Vector3(.16f, .1f, 3.1f));
        }

        static void Cockpit(ShuttleRig rig, Transform root, ShuttlePalette palette)
        {
            var cabin = new GameObject("Cockpit").transform;
            cabin.SetParent(root, false);

            Box(cabin, "Cabin floor", palette.Seat, new Vector3(0, -.54f, 1.35f), new Vector3(1.72f, .06f, 2.7f));
            foreach (var side in new[] { -1, 1 })
                Box(cabin, "Cabin wall", palette.Seat, new Vector3(side * .84f, .12f, 1.14f), new Vector3(.06f, 1.3f, 2.29f));
            Box(cabin, "Cabin roof", palette.Seat, new Vector3(0, .74f, 1.3f), new Vector3(1.72f, .06f, 2.5f));
            Box(cabin, "Rear bulkhead", palette.Seat, new Vector3(0, .12f, .02f), new Vector3(1.72f, 1.3f, .08f));

            Box(cabin, "Dashboard", palette.Dark, new Vector3(0, -.08f, 2.42f), new Vector3(1.6f, .34f, .5f), Quaternion.Euler(-16, 0, 0));
            Box(cabin, "Dashboard lip", palette.Metal, new Vector3(0, .07f, 2.55f), new Vector3(1.6f, .05f, .14f), Quaternion.Euler(-16, 0, 0));
            Box(cabin, "Centre console", palette.Dark, new Vector3(0, -.33f, 1.95f), new Vector3(.36f, .3f, .85f));
            foreach (var side in new[] { -1, 1 })
            {
                Box(cabin, "Side console", palette.Dark, new Vector3(side * .6f, -.14f, 1.98f), new Vector3(.42f, .26f, .95f));
                Box(cabin, "Side switch panel", palette.Metal, new Vector3(side * .6f, -.005f, 2.08f),
                    new Vector3(.3f, .012f, .42f));
                for (var i = 0; i < 4; i++)
                {
                    var toggle = new Vector3(side * .57f, .012f, 1.94f + i * .085f);
                    Box(cabin, "Toggle", palette.Dark, toggle, new Vector3(.033f, .035f, .038f),
                        Quaternion.Euler(16, 0, 0));
                    Box(cabin, "Toggle status", i == 2 ? palette.ConsoleGlow : palette.Stripe,
                        toggle + new Vector3(side * .065f, -.005f, 0), new Vector3(.022f, .01f, .023f));
                }
                Box(cabin, "Panel wear strip", palette.Ceramic, new Vector3(side * .47f, .01f, 2.06f),
                    new Vector3(.013f, .01f, .22f));
            }

            FlightDisplays(cabin, palette);

            Box(cabin, "Seat pan", palette.Seat, new Vector3(0, -.32f, .72f), new Vector3(.62f, .14f, .62f));
            Box(cabin, "Seat back", palette.Seat, new Vector3(0, .03f, .42f), new Vector3(.62f, .66f, .12f), Quaternion.Euler(9, 0, 0));
            Box(cabin, "Headrest", palette.Seat, new Vector3(0, .38f, .36f), new Vector3(.42f, .22f, .1f), Quaternion.Euler(9, 0, 0));

            Box(cabin, "Cabin frame", palette.Metal, new Vector3(0, .62f, 2.85f), new Vector3(2.2f, .055f, .09f), Quaternion.Euler(-20, 0, 0));
            foreach (var side in new[] { -1, 1 })
                Box(cabin, "Cabin pillar", palette.Metal, new Vector3(side * .78f, .3f, 2.8f), new Vector3(.08f, .95f, .1f), Quaternion.Euler(-20, side * -22, 0));

            rig.CockpitEye = new GameObject("Cockpit eye").transform;
            rig.CockpitEye.SetParent(cabin, false);
            rig.CockpitEye.localPosition = new Vector3(0, .43f, 1.91f);
            rig.CockpitEye.localRotation = Quaternion.Euler(5, 0, 0);
        }

        static void FlightDisplays(Transform cabin, ShuttlePalette palette)
        {
            for (var i = -1; i <= 1; i++)
            {
                var display = new GameObject("Flight instrument").transform;
                display.SetParent(cabin, false);
                display.localPosition = new Vector3(i * .365f, .083f, 2.30f);
                display.localRotation = Quaternion.Euler(42, i * -9, 0);
                Box(display, "Instrument trim", palette.Metal, Vector3.zero, new Vector3(.317f, .185f, .04f));
                Box(display, "Dark display glass", palette.Dark, new Vector3(0, 0, -.026f),
                    new Vector3(.275f, .145f, .013f));
                Box(display, "Reference line", palette.ConsoleGlow, new Vector3(0, -.023f, -.035f),
                    new Vector3(.17f, .005f, .003f));
                if (i == 0)
                {
                    Box(display, "Attitude horizon", palette.Glow, new Vector3(0, .025f, -.035f),
                        new Vector3(.206f, .006f, .003f));
                    for (var tick = -2; tick <= 2; tick++)
                        Box(display, "Horizon tick", palette.Glow, new Vector3(tick * .04f, .038f, -.035f),
                            new Vector3(.003f, .018f, .003f));
                }
                else
                {
                    for (var bar = 0; bar < 5; bar++)
                        Box(display, "Status trace", bar > 2 ? palette.Stripe : palette.ConsoleGlow,
                            new Vector3(-.072f + bar * .036f, -.012f + bar * .007f, -.035f),
                            new Vector3(.018f, .025f + bar * .011f, .003f));
                }
            }
            Box(cabin, "Annunciator", palette.Glow, new Vector3(.58f, .11f, 2.26f),
                new Vector3(.082f, .018f, .026f));
        }

        static void Pane(Transform parent, string name, ShuttlePalette palette, Vector3 position, Vector3 scale, Quaternion rotation)
        {
            var pane = Box(parent, name, palette.Glass, position, scale, rotation);
            pane.gameObject.layer = 2;
        }

        static Transform Add(Transform parent, string name, Mesh mesh, Material material)
            => Add(parent, name, mesh, material, Vector3.zero, Vector3.one, Quaternion.identity);

        static Transform Add(Transform parent, string name, Mesh mesh, Material material, Vector3 position,
            Vector3 scale, Quaternion rotation)
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

        static Transform Box(Transform parent, string name, Material material, Vector3 position, Vector3 scale,
            Quaternion? rotation = null)
        {
            return Add(parent, name, Cube, material, position, scale, rotation ?? Quaternion.identity);
        }
    }
}
