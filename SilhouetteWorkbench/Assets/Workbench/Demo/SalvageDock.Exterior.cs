using UnityEngine;

namespace Workbench.Demo
{
    public sealed partial class SalvageDock
    {
        void BuildDeck()
        {
            Tint("Dock Floor", deck);
            Tint("Observation Gantry", steel);
            var lane = Surface("Non-slip service paint", new Color32(46, 63, 69, 255), .12f);
            BuildArrivalDeck(lane);
            BuildAprons(lane);
        }

        void BuildArrivalDeck(Material lane)
        {
            Box(lane, 0, 2.008f, -9.85f, 3.07f, .018f, 9.5f);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(pale, side * 1.45f, 2.021f, -9.95f, .065f, .012f, 9.7f);
                Box(steel, side * 5.13f, 2.025f, -8.1f, 3.45f, .018f, 4.7f);
                for (int plate = 0; plate < 4; plate++)
                {
                    float z = -6.3f - plate * 1.05f;
                    Box(orange, side * 5.13f, 2.038f, z, 3.35f, .017f, .095f);
                }

                // Front rail leaves a clear 3.15 m opening over the original stairs.
                OpenRail("Gantry front guardrail", new Vector3(side * 1.58f, 2, -5.12f),
                    new Vector3(side * 7.88f, 2, -5.12f), .93f);
                OpenRail("Stair handrail", new Vector3(side * 1.55f, 2, -5.02f),
                    new Vector3(side * 1.55f, .08f, -1.04f), .88f);
            }

            OpenRail("Gantry aft guardrail", new Vector3(-7.88f, 2, -14.88f),
                new Vector3(7.88f, 2, -14.88f), .95f);
            OpenRail("Gantry port guardrail", new Vector3(-7.88f, 2, -14.88f),
                new Vector3(-7.88f, 2, -5.12f), .95f);
            OpenRail("Gantry starboard forward guardrail", new Vector3(7.88f, 2, -7.40f),
                new Vector3(7.88f, 2, -5.12f), .95f);
            OpenRail("Gantry starboard aft guardrail", new Vector3(7.88f, 2, -14.88f),
                new Vector3(7.88f, 2, -10.66f), .95f);
            BuildServiceRamp(lane);

            for (int step = 1; step <= 6; step++)
            {
                Tint("Gantry Step " + step, deck);
                Box(pale, 0, (7 - step) * 2f / 7 + .012f, -4.32f + .65f * (step - 1), 2.88f, .02f, .065f);
            }

            // The overhead sign and launch-side uplights frame the arrival overlook.
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 1.77f;
                Box(steel, x, 3.20f, -5.26f, .17f, 2.45f, .23f);
                Box(ice, x, 3.34f, -5.396f, .05f, 1.53f, .045f);
            }
            Box(deck, 0, 4.17f, -5.26f, 3.71f, .57f, .22f);
            Box(orange, 0, 3.88f, -5.26f, 3.67f, .06f, .23f);
            Label("SALVAGE CONTROL / 01", new Vector3(0, 4.17f, -5.39f), .24f,
                new Color32(199, 224, 224, 255));
            Lamp("Arrival key", new Vector3(-5.9f, 4.6f, -10.6f), new Color32(180, 222, 245, 255), 9, 5);
            Lamp("Stair task lamp", new Vector3(1.82f, 3.9f, -5.4f), new Color32(237, 206, 163, 255), 6, 3.5f);
        }

        void BuildServiceRamp(Material lane)
        {
            var near = new Vector3(7.82f, 1.905f, -9.03f);
            var far = new Vector3(12.35f, -.11f, -9.03f);
            var span = far - near;
            var slope = Fixture(scenery.transform, "Starboard service ramp", steel, (near + far) * .5f,
                new Vector3(span.magnitude, .21f, 3.26f), solid: true);
            slope.localRotation = Quaternion.FromToRotation(Vector3.right, span);
            Add(cube, lane, slope.localPosition + slope.up * .109f, slope.localRotation,
                new Vector3(span.magnitude - .05f, .016f, 2.85f));
            for (int side = -1; side <= 1; side += 2)
            {
                var railOffset = new Vector3(0, .105f, side * 1.565f);
                OpenRail("Service ramp guardrail", near + railOffset, far + railOffset, .95f);
                Add(cube, pale, slope.localPosition + slope.up * .118f + new Vector3(0, 0, side * 1.13f),
                    slope.localRotation, new Vector3(span.magnitude - .035f, .013f, .075f));
            }
        }

        void BuildAprons(Material lane)
        {
            const float edge = 15.82f;
            const float fore = -15.81f;
            const float aft = 31.79f;
            for (int side = -1; side <= 1; side += 2)
            {
                OpenRail("Dock perimeter guardrail", new Vector3(side * edge, 0, fore),
                    new Vector3(side * edge, 0, aft), 1.02f);
                Box(steel, side * 15.86f, -.46f, 8, .47f, .85f, 48);
                Box(lane, side * 12.28f, .012f, 10, 4.93f, .022f, 40.5f);
                for (int z = -10; z < 31; z += 5)
                {
                    Box(steel, side * 15.54f, .32f, z, .35f, .66f, .37f);
                    Box(ice, side * 15.54f, .667f, z, .24f, .045f, .29f);
                    Box(pale, side * 10.02f, .026f, z + .7f, .08f, .012f, 2.13f);
                    Box(steel, side * 4.83f, .012f, z + .45f, 9.52f, .018f, .042f);
                }
                for (int mark = 0; mark < 6; mark++)
                {
                    var p = new Vector3(side * 12.22f + (mark - 2.5f) * .58f, .035f, -2.54f);
                    Add(cube, orange, p, Quaternion.Euler(0, -27f, 0), new Vector3(.22f, .017f, .60f));
                }
            }
            OpenRail("Dock forward guardrail", new Vector3(-edge, 0, fore), new Vector3(edge, 0, fore), 1.02f);
            OpenRail("Dock aft port guardrail", new Vector3(-edge, 0, aft), new Vector3(-4.5f, 0, aft), 1.02f);
            OpenRail("Dock aft starboard guardrail", new Vector3(-2.3f, 0, aft), new Vector3(edge, 0, aft), 1.02f);
            Box(steel, 0, -.46f, 31.94f, 32, .85f, .36f);
            Box(lane, 0, .012f, 27.51f, 28.5f, .022f, 4.78f);
            Box(pale, 0, .028f, 29.75f, 28.9f, .014f, .09f);
            for (int z = 0; z < 22; z += 2)
                Box(orange, -2.95f, .034f, z, .09f, .016f, .96f);
            Lamp("Port loading apron light", new Vector3(-10.5f, 3.35f, 6.2f),
                new Color32(184, 213, 242, 255), 9f, 4f);
            Lamp("Starboard service light", new Vector3(12.25f, 3.8f, 8.3f),
                new Color32(129, 221, 247, 255), 10, 4);
        }

        void OpenRail(string name, Vector3 start, Vector3 end, float height)
        {
            var span = end - start;
            var top = Vector3.up * (height - .035f);
            Beam(orange, start + top, end + top, .11f, .105f);
            Beam(steel, start + Vector3.up * (.55f * height), end + Vector3.up * (.55f * height), .085f, .07f);
            int bays = Mathf.CeilToInt(span.magnitude / 2.55f);
            for (int i = 0; i <= bays; i++)
            {
                var post = Vector3.Lerp(start, end, (float)i / bays);
                Box(steel, post.x, post.y + height * .5f, post.z, .13f, height, .13f);
            }

            // One obstruction surface closes the spaces between the visual rail members.
            var barrier = new GameObject(name).transform;
            barrier.SetParent(scenery.transform, false);
            barrier.localPosition = (start + end) * .5f + Vector3.up * (height * .5f);
            barrier.localRotation = Quaternion.LookRotation(span);
            barrier.gameObject.AddComponent<BoxCollider>().size = new Vector3(.135f, height, span.magnitude);
        }
    }
}
