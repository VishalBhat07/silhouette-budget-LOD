using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Workbench.Demo
{
    public sealed class BerthPalette
    {
        public Material Deck, Steel, Pale, Orange, Glow, Dark;
        public ShuttlePalette Shuttle;
    }

    public sealed class BerthRig
    {
        public Transform Root;
        public ShuttleRig Shuttle;
        public Transform BoardingPort;
        internal readonly List<(Transform Jaw, Quaternion Closed, Quaternion Open)> ClampJaws = new();

        internal void SetClampRelease(float release)
        {
            foreach (var clamp in ClampJaws)
                clamp.Jaw.localRotation = Quaternion.Slerp(clamp.Closed, clamp.Open, release);
        }
    }

    /// <summary>Builds the shuttle berth that extends the dock's extraction hatch.</summary>
    public static class SalvageBerth
    {
        public static readonly Vector3 ShuttlePosition = new(0, 1.5f, 38);

        static Mesh cube, cylinder;

        static Mesh Cube => cube != null ? cube : cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        static Mesh Cylinder => cylinder != null ? cylinder : cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");

        public static BerthRig Build(Transform parent, BerthPalette palette, bool parkShuttle, List<Object> owned)
        {
            var rig = new BerthRig();
            var root = new GameObject("Shuttle berth").transform;
            root.SetParent(parent, false);
            rig.Root = root;

            Platform(root, palette);
            BoardingBridge(root, palette);
            rig.BoardingPort = new GameObject("Shuttle boarding point").transform;
            rig.BoardingPort.SetParent(root, false);
            rig.BoardingPort.localPosition = new Vector3(-3.4f, 2.45f, 38.4f);
            Clamps(rig, palette);
            FuelBoom(root, palette);
            Lighting(root, palette);
            if (parkShuttle)
            {
                rig.Shuttle = ShuttleKit.Build(root, palette.Shuttle, false, owned);
                rig.Shuttle.Root.localPosition = ShuttlePosition;
            }
            return rig;
        }

        static void Platform(Transform root, BerthPalette palette)
        {
            Box(root, "Berth platform", palette.Deck, new Vector3(-3, -.3f, 39.2f), new Vector3(16, .6f, 13)).gameObject.AddComponent<BoxCollider>();
            Box(root, "Berth edge trim", palette.Orange, new Vector3(-3, .02f, 32.78f), new Vector3(16, .04f, .12f));
            for (var i = 0; i < 3; i++)
                Box(root, "Berth support", palette.Steel, new Vector3(-3, -.78f, 34.4f + i * 4.6f), new Vector3(15.4f, .34f, .5f));
            foreach (var side in new[] { -1, 1 })
                Box(root, "Berth skirt", palette.Steel, new Vector3(-3 + side * 7.9f, -.36f, 39.2f), new Vector3(.35f, .55f, 12.7f));

            for (var mark = 0; mark < 7; mark++)
                Box(root, "Approach chevron", palette.Orange, new Vector3(-3 + (mark - 3) * .95f, .04f, 33.55f),
                    new Vector3(.34f, .02f, .9f), Quaternion.Euler(0, 24, 0));

            for (var post = 0; post < 5; post++)
            {
                var z = 33.4f + post * 2.6f;
                Box(root, "Berth rail post", palette.Steel, new Vector3(4.7f, .62f, z), new Vector3(.14f, 1.24f, .14f));
                Box(root, "Berth rail", palette.Orange, new Vector3(4.7f, 1.2f, z + 1.3f), new Vector3(.1f, .1f, 2.6f));
            }

            Label(root, "BERTH 02", new Vector3(-8.6f, 2.35f, 32.85f), .42f, new Color32(214, 231, 226, 255));
            Label(root, "SHUTTLE / SALVAGE", new Vector3(-8.6f, 2.0f, 32.85f), .2f, new Color32(255, 166, 92, 255));
        }

        static void BoardingBridge(Transform root, BerthPalette palette)
        {
            // The bridge clears the dock's aft guardrail and lands beside the port hatch.
            Box(root, "Boarding bridge", palette.Steel, new Vector3(-3.4f, 1.42f, 35.45f), new Vector3(2.0f, .14f, 7.1f)).gameObject.AddComponent<BoxCollider>();
            Box(root, "Bridge deck", palette.Pale, new Vector3(-3.4f, 1.5f, 35.45f), new Vector3(1.86f, .03f, 6.95f));
            foreach (var side in new[] { -1, 1 })
            {
                var x = -3.4f + side * .97f;
                Box(root, "Bridge rail", palette.Orange, new Vector3(x, 2.42f, 35.45f), new Vector3(.09f, .09f, 7.1f));
                Box(root, "Bridge midrail", palette.Steel, new Vector3(x, 1.96f, 35.45f), new Vector3(.06f, .06f, 7.1f));
                var barrier = new GameObject("Bridge rail collision").AddComponent<BoxCollider>();
                barrier.transform.SetParent(root, false);
                barrier.center = new Vector3(x, 1.97f, 35.45f);
                barrier.size = new Vector3(.09f, 1, 7.1f);
                for (var post = 0; post < 5; post++)
                    Box(root, "Bridge post", palette.Steel, new Vector3(x, 1.96f, 32.3f + post * 1.58f), new Vector3(.09f, .95f, .09f));
            }

            for (var step = 0; step < 6; step++)
            {
                var top = .25f * (step + 1);
                Box(root, "Boarding step", palette.Pale, new Vector3(-3.4f, top * .5f, 30.15f + step * .32f),
                    new Vector3(2.0f, top, .32f)).gameObject.AddComponent<BoxCollider>();
            }

            foreach (var side in new[] { -1, 1 })
                Box(root, "Gate post", palette.Orange, new Vector3(-3.4f + side * 1.05f, 1.45f, 31.79f), new Vector3(.12f, 2.9f, .12f));
            Box(root, "Gate header", palette.Orange, new Vector3(-3.4f, 2.86f, 31.79f), new Vector3(2.25f, .12f, .12f));
            Box(root, "Gate lamp", palette.Glow, new Vector3(-3.4f, 2.86f, 31.72f), new Vector3(1.5f, .04f, .03f));

            Box(root, "Hatch collar", palette.Steel, new Vector3(-2.16f, 1.28f, 38.9f), new Vector3(.62f, .95f, 1.2f));
            Box(root, "Collar seal", palette.Orange, new Vector3(-1.87f, 1.28f, 38.9f), new Vector3(.06f, 1.05f, 1.3f));
        }

        static void Clamps(BerthRig rig, BerthPalette palette)
        {
            Clamp(rig, palette, new Vector3(0, .2f, 40.25f), 1.1f);
            Clamp(rig, palette, new Vector3(-1.2f, .2f, 37.15f), .9f);
            Clamp(rig, palette, new Vector3(1.2f, .2f, 37.15f), .9f);
        }

        static void Clamp(BerthRig rig, BerthPalette palette, Vector3 foot, float width)
        {
            var root = rig.Root;
            Box(root, "Clamp base", palette.Steel, foot + Vector3.down * .18f, new Vector3(width + .5f, .12f, width + .4f));
            foreach (var side in new[] { -1, 1 })
            {
                var jaw = Box(root, "Clamp jaw", palette.Orange, foot + new Vector3(side * width * .5f, .16f, 0),
                    new Vector3(.12f, .5f, width + .2f));
                jaw.localRotation = Quaternion.Euler(0, 0, side * -14);
                rig.ClampJaws.Add((jaw, jaw.localRotation, Quaternion.Euler(0, 0, side * -72)));
            }
            Box(root, "Clamp status", palette.Glow, foot + new Vector3(0, .02f, width * .5f + .18f), new Vector3(.5f, .04f, .03f));
        }

        static void FuelBoom(Transform root, BerthPalette palette)
        {
            var shoulder = new Vector3(-6.4f, 1.5f, 34.4f);
            Box(root, "Boom base", palette.Steel, shoulder + Vector3.down * .55f, new Vector3(.9f, 1.1f, .9f));
            Beam(root, palette.Steel, shoulder, shoulder + new Vector3(1.5f, .9f, 1.5f), .26f);
            Beam(root, palette.Orange, shoulder + new Vector3(1.5f, .9f, 1.5f),
                shoulder + new Vector3(2.6f, .3f, 3.4f), .18f);
            Beam(root, palette.Dark, shoulder + new Vector3(2.6f, .3f, 3.4f),
                shoulder + new Vector3(2.1f, -.15f, 4.6f), .12f);
            Add(root, "Boom joint", Cylinder, palette.Steel, shoulder + new Vector3(1.5f, .9f, 1.5f),
                new Vector3(.5f, .2f, .5f), Quaternion.Euler(90, 0, 0));
        }

        static void Lighting(Transform root, BerthPalette palette)
        {
            for (var i = 0; i < 6; i++)
            {
                var angle = i * Mathf.PI * 2 / 6 + .3f;
                var p = new Vector3(-3 + Mathf.Cos(angle) * 7.4f, .35f, 39.2f + Mathf.Sin(angle) * 5.4f);
                Box(root, "Berth light post", palette.Steel, p + Vector3.up * .35f, new Vector3(.14f, .7f, .14f));
                Box(root, "Berth light", palette.Glow, p + Vector3.up * .72f, new Vector3(.34f, .06f, .34f));
            }
            Lamp(root, "Berth work light", new Vector3(-5.4f, 3.1f, 35.6f), new Color32(198, 226, 255, 255), 8, 4.2f);
            Lamp(root, "Berth boarding light", new Vector3(-2.6f, 2.7f, 38.9f), new Color32(255, 214, 168, 255), 4.5f, 3f);
        }

        static void Lamp(Transform root, string name, Vector3 position, Color colour, float range, float intensity)
        {
            var lamp = new GameObject(name).AddComponent<Light>();
            lamp.transform.SetParent(root, false);
            lamp.transform.localPosition = position;
            lamp.type = LightType.Point;
            lamp.color = colour;
            lamp.range = range;
            lamp.intensity = intensity;
            lamp.shadows = LightShadows.None;
        }

        static void Label(Transform root, string text, Vector3 position, float height, Color colour)
        {
            var sign = new GameObject(text).AddComponent<TextMesh>();
            sign.transform.SetParent(root, false);
            sign.transform.localPosition = position;
            sign.transform.localRotation = Quaternion.Euler(0, 180, 0);
            sign.text = text;
            sign.anchor = TextAnchor.MiddleCenter;
            sign.fontSize = 54;
            sign.characterSize = height * .1f;
            sign.color = colour;
            sign.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static void Beam(Transform root, Material material, Vector3 a, Vector3 b, float width)
        {
            var axis = b - a;
            var beam = Box(root, "Boom arm", material, (a + b) * .5f, new Vector3(width, axis.magnitude, width));
            beam.localRotation = Quaternion.FromToRotation(Vector3.up, axis);
        }

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
