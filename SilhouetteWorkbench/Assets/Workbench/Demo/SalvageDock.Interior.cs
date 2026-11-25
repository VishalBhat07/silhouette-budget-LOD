using UnityEngine;
using UnityEngine.Rendering;

namespace Workbench.Demo
{
    public sealed partial class SalvageDock
    {
        const float LiftStartX = -2.88f;
        const float LiftTravel = 2.8f;
        const float LiftDepth = 14.4f;

        Transform lift;
        Material liftStatus;
        Light liftLamp;
        bool liftCommand;
        float liftOffset;

        public bool LiftOpen => liftCommand && liftOffset >= LiftTravel;

        public void SetLiftOpen(bool open)
        {
            liftCommand = open;
            if (!open && lift != null)
            {
                liftOffset = 0;
                lift.localPosition = new Vector3(LiftStartX, 0, LiftDepth);
            }
            SetLiftStatus(false);
        }

        void SetLiftStatus(bool clear)
        {
            if (liftStatus == null) return;
            Color state = clear ? new Color32(112, 255, 197, 255) : new Color32(255, 162, 63, 255);
            liftStatus.color = state;
            liftStatus.SetColor("_EmissionColor", state.linear * 2.4f);
            liftLamp.color = state;
        }

        void UpdateLift()
        {
            if (!liftCommand || LiftOpen) return;
            liftOffset = Mathf.MoveTowards(liftOffset, LiftTravel, Time.deltaTime * 2.5f);
            lift.localPosition = new Vector3(LiftStartX + liftOffset, 0, LiftDepth);
            if (LiftOpen) SetLiftStatus(true);
        }

        void BuildTugInterior()
        {
            var gasket = Surface("Tug panel joints", new Color32(22, 29, 33, 255), .22f);
            var liner = Surface("Coated tug interior", new Color32(113, 132, 134, 255), .18f);
            var floor = Surface("Brushed engineering tread", new Color32(96, 113, 118, 255), .46f);
            floor.mainTexture = CreateTreadTexture();
            floor.mainTextureScale = new Vector2(9, 2);
            var offWhite = Surface("Stencilled white paint", new Color32(202, 213, 201, 255), .12f);
            liftStatus = Surface("Lift circuit indicators", new Color32(255, 162, 63, 255), 0, 2.4f);

            BuildBoardingPassage(gasket, liner);
            BuildFreightLift(gasket, liner);
            BuildEngineering(gasket, liner, offWhite, floor);
        }

        Texture2D CreateTreadTexture()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
                { name = "Brushed deck grip pattern", filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            var colors = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int lane = (x / 32 + y / 32) % 2;
                float tx = (x % 32 - 15.5f) / 15.5f, ty = (y % 32 - 15.5f) / 15.5f;
                float longAxis = Mathf.Abs(tx + (lane == 0 ? ty : -ty));
                float shortAxis = Mathf.Abs(tx - (lane == 0 ? ty : -ty));
                float bevel = Mathf.Clamp01((1.16f - longAxis) * 6) * Mathf.Clamp01((.22f - shortAxis) * 10);
                float stock = .63f + .13f * Mathf.PerlinNoise(x * .35f, y * .06f);
                byte value = (byte)Mathf.Clamp(255 * (stock + .16f * bevel), 0, 255);
                colors[y * size + x] = new Color32(value, value, value, 255);
            }
            texture.SetPixels32(colors);
            texture.Apply(true, true);
            owned.Add(texture);
            return texture;
        }

        void BuildBoardingPassage(Material gasket, Material liner)
        {
            // The original cargo stack sits beside a partly open loading vestibule.
            // Solid wall bases keep feet and the third-person camera in the service aisle.
            Fixture(scenery.transform, "Port boarding wall", steel, new Vector3(-4.38f, 1.5f, 14.38f),
                new Vector3(.18f, 3f, 3.85f), solid: true);
            Fixture(scenery.transform, "Starboard boarding wall", gasket, new Vector3(-1.36f, 1.5f, 14.16f),
                new Vector3(.18f, 3f, 3.5f), solid: true);
            Fixture(scenery.transform, "Boarding roof", deck, new Vector3(-2.9f, 3.45f, 14.38f),
                new Vector3(3.17f, .18f, 3.91f), solid: true);
            Box(deck, -2.88f, -.012f, 14.48f, 2.82f, .045f, 4.05f);

            for (int frame = 0; frame < 5; frame++)
            {
                float z = 12.5f + frame * .86f;
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = -2.88f + side * 1.36f;
                    Box(liner, x, 1.25f, z, .15f, 2.42f, .24f);
                    Beam(liner, new Vector3(x, 2.42f, z), new Vector3(x - side * .39f, 3.22f, z), .16f, .24f);
                    Box(orange, x, .39f, z, .18f, .1f, .24f);
                    Box(ice, x - side * .12f, .23f, z + .23f, .054f, .055f, .16f);
                }
                Box(steel, -2.88f, 3.28f, z, 2.02f, .17f, .24f);
                Box(ice, -2.88f, 3.18f, z, 1.05f, .035f, .08f);
            }

            for (int panel = 0; panel < 4; panel++)
            {
                float z = 12.9f + .88f * panel;
                Box(gasket, -4.27f, 1.51f, z, .06f, .86f, .63f);
                for (int louver = 0; louver < 5; louver++)
                    Box(liner, -4.218f, 1.23f + louver * .13f, z, .05f, .046f, .54f);
                Box(liner, -1.49f, 1.24f, z, .11f, 1.04f, .57f);
                Box(pale, -1.56f, 1.6f, z, .022f, .055f, .37f);
            }

            Label("CARGO / 02", new Vector3(-2.88f, 2.98f, 12.33f), .2f, new Color32(202, 220, 213, 255));
            Lamp("Gangway practical", new Vector3(-2.89f, 2.78f, 12.95f), new Color32(148, 221, 255, 255), 4.3f, 3.0f);
            Lamp("Aft corridor practical", new Vector3(-2.94f, 2.7f, 15.64f), new Color32(139, 207, 255, 255), 4.0f, 2.8f);
        }

        void BuildFreightLift(Material gasket, Material liner)
        {
            lift = new GameObject("Sliding freight platform").transform;
            lift.SetParent(scenery.transform, false);
            lift.localPosition = new Vector3(LiftStartX, 0, LiftDepth);
            Fixture(lift, "Platform backplate", steel, new Vector3(0, 1.25f, 0), new Vector3(2.86f, 2.5f, .23f), solid: true);
            Fixture(lift, "Cast platform sill", deck, new Vector3(0, .16f, -.08f), new Vector3(2.97f, .3f, .54f));
            Fixture(lift, "Crossbar", liner, new Vector3(0, 2.46f, -.15f), new Vector3(2.95f, .22f, .22f));
            for (int i = 0; i < 7; i++)
            {
                float x = -1.14f + i * .38f;
                Fixture(lift, "Freight reinforcement", liner, new Vector3(x, 1.38f, -.14f), new Vector3(.085f, 1.9f, .17f));
                var stripe = Fixture(lift, "Load warning paint", orange, new Vector3(x, .39f, -.167f), new Vector3(.21f, .36f, .02f));
                stripe.localRotation = Quaternion.Euler(0, 0, -28);
            }

            Box(gasket, -2.85f, .025f, LiftDepth, 3.04f, .035f, .29f);
            Box(pale, -2.87f, .048f, LiftDepth - .08f, 2.94f, .028f, .043f);
            Box(orange, -2.86f, 2.8f, LiftDepth - .08f, 2.9f, .31f, .21f);
            Box(liftStatus, -2.88f, 2.8f, LiftDepth - .205f, 1.22f, .05f, .04f);
            // The interaction volume is centered in the aisle. The physical console is wall-mounted.
            Box(gasket, -1.67f, 1.02f, 13.09f, .24f, .57f, .51f);
            Box(liner, -1.81f, 1.04f, 13.11f, .08f, .51f, .46f);
            Box(liftStatus, -1.86f, 1.15f, 13.09f, .032f, .11f, .31f);
            Box(orange, -1.82f, .88f, 13.09f, .031f, .065f, .37f);
            Label("LIFT SUPPLY", new Vector3(-1.64f, 1.39f, 12.798f), .102f, new Color32(214, 224, 218, 255));
            liftLamp = Lamp("Lift circuit amber", new Vector3(-2.68f, 2.48f, 13.87f), new Color32(255, 162, 63, 255), 3.8f, 2.8f);
        }

        void BuildEngineering(Material gasket, Material liner, Material offWhite, Material floor)
        {
            // A taller room follows the tight boarding corridor. The turbine and radiator
            // sightlines stay open inside the floor-level walkway.
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 4.55f;
                Fixture(scenery.transform, "Engineering pressure wall", gasket, new Vector3(x, 2.35f, 19.72f),
                    new Vector3(.24f, 4.7f, 7.44f), solid: true);
                for (int bay = 0; bay < 5; bay++)
                {
                    float z = 16.55f + bay * 1.45f;
                    Box(liner, x - side * .16f, 1.37f, z, .22f, 2.66f, 1.32f);
                    Box(deck, x - side * .288f, 1.83f, z, .056f, .71f, 1.03f);
                    Box(offWhite, x - side * .324f, 2.83f, z, .054f, .068f, .97f);
                    Box(orange, x - side * .303f, .37f, z, .07f, .1f, 1.22f);
                    for (int vent = 0; vent < 5; vent++)
                        Box(liner, x - side * .32f, 1.63f + vent * .093f, z, .065f, .032f, .85f);
                }
                Beam(steel, new Vector3(x - side * .2f, 3.86f, 16.04f),
                    new Vector3(x - side * .2f, 3.86f, 23.38f), .28f, .42f);
                Beam(orange, new Vector3(x - side * .32f, 3.38f, 16.04f),
                    new Vector3(x - side * .32f, 3.38f, 23.38f), .115f, .115f);
            }

            Fixture(scenery.transform, "Engineering roof", gasket, new Vector3(0, 5.2f, 19.6f),
                new Vector3(9.26f, .23f, 7.6f), solid: true);
            for (int rib = 0; rib < 5; rib++)
            {
                float z = 16.26f + rib * 1.51f;
                Box(steel, 0, 4.97f, z, 8.02f, .3f, .26f);
                Box(liner, 0, 4.79f, z, 5.8f, .09f, .19f);
                Box(ice, -2.8f, 4.73f, z, .88f, .043f, .15f);
                Box(ice, 2.8f, 4.73f, z, .88f, .043f, .15f);
            }

            Fixture(scenery.transform, "Aft partition port", deck, new Vector3(-3.65f, 2.36f, 23.28f),
                new Vector3(1.83f, 4.72f, .24f), solid: true);
            Fixture(scenery.transform, "Aft partition starboard", deck, new Vector3(3.65f, 2.36f, 23.28f),
                new Vector3(1.83f, 4.72f, .24f), solid: true);
            Box(deck, 0, 4.74f, 23.28f, 9.1f, .29f, .25f);
            Label("ENGINEERING / 03", new Vector3(0, 4.49f, 16.21f), .24f, new Color32(216, 233, 225, 255));
            Label("EXIT", new Vector3(0, 4.54f, 22.7f), .22f, new Color32(147, 249, 201, 255));

            for (int tile = 0; tile < 6; tile++)
            {
                float z = 16.52f + tile * 1.14f;
                Box(gasket, 0, .016f, z, 8.68f, .028f, 1.1f);
                Box(floor, 0, .031f, z, 8.61f, .028f, 1.04f);
                foreach (float x in new[] { -3.25f, .65f })
                    Box(orange, x, .048f, z, .065f, .009f, .78f);
            }

            // Two service trays against the upper walls route power to the engine.
            // Support brackets, orange conduits and cast fittings read as separate pieces.
            var cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            for (int side = -1; side <= 1; side += 2)
            {
                float trayX = side * 3.77f;
                Box(deck, trayX, 3.64f, 20.25f, 1.02f, .094f, 5.98f);
                for (int line = 0; line < 4; line++)
                {
                    float x = trayX + (line - 1.5f) * .16f;
                    Add(cylinder, line == 1 || line == 2 ? orange : steel,
                        new Vector3(x, 3.78f, 20.25f), Quaternion.Euler(90, 0, 0),
                        new Vector3(.092f, 2.94f, .092f));
                }
                for (int span = 0; span < 5; span++)
                {
                    float z = 17.6f + span * 1.28f;
                    Box(liner, trayX, 3.82f, z, .94f, .11f, .11f);
                    Beam(steel, new Vector3(side * 4.43f, 3.98f, z),
                        new Vector3(trayX, 3.63f, z), .1f, .11f);
                }
            }

            // Recessed cradle around the collectible, kept at waist height.
            Box(gasket, -2, .44f, 19.95f, .79f, .88f, .66f);
            Box(liner, -2, .87f, 19.92f, .93f, .15f, .74f);
            Box(signal, -2, .97f, 19.48f, .61f, .052f, .024f);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(steel, -2 + side * .45f, 1.13f, 19.96f, .074f, .47f, .56f);
                Box(pale, -2 + side * .45f, 1.42f, 19.96f, .094f, .074f, .56f);
            }
            Label("FLIGHT DATA", new Vector3(-2, .71f, 19.603f), .108f, new Color32(218, 233, 223, 255));

            Lamp("Engineering port fill", new Vector3(-2.95f, 3.62f, 18.5f), new Color32(120, 188, 228, 255), 6.9f, 5.5f);
            Lamp("Engineering overhead key", new Vector3(2.95f, 4.28f, 19.25f), new Color32(255, 225, 188, 255), 7.1f, 5.8f);
            Lamp("Cradle task light", new Vector3(-2.06f, 2.5f, 20.25f), new Color32(154, 233, 234, 255), 3.2f, 2.6f);
        }

        Transform Fixture(Transform parent, string label, Material material, Vector3 p, Vector3 scale, bool solid = false)
        {
            var obj = new GameObject(label);
            var transform = obj.transform;
            transform.SetParent(parent, false);
            transform.localPosition = p;
            transform.localScale = scale;
            obj.AddComponent<MeshFilter>().sharedMesh = cube;
            obj.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (solid) obj.AddComponent<BoxCollider>();
            return transform;
        }

        Light Lamp(string label, Vector3 p, Color colour, float range, float intensity)
        {
            var emitter = new GameObject(label).AddComponent<Light>();
            emitter.transform.SetParent(scenery.transform, false);
            emitter.transform.localPosition = p;
            emitter.type = LightType.Point;
            emitter.color = colour;
            emitter.range = range;
            emitter.intensity = intensity;
            emitter.shadows = LightShadows.None;
            return emitter;
        }

        void Label(string text, Vector3 p, float height, Color colour)
        {
            var sign = new GameObject(text).AddComponent<TextMesh>();
            sign.transform.SetParent(scenery.transform, false);
            sign.transform.localPosition = p;
            sign.text = text;
            sign.anchor = TextAnchor.MiddleCenter;
            sign.fontSize = 54;
            sign.characterSize = height * .1f;
            sign.color = colour;
            sign.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
