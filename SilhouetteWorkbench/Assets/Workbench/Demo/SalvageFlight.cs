using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Workbench.Demo
{
    /// <summary>Drives the 7.5-second shuttle flight with cockpit and chase cameras.</summary>
    [DefaultExecutionOrder(-40)]
    public sealed partial class SalvageFlight : MonoBehaviour
    {
        public const float Duration = 7.5f;

        public static readonly (string Name, float Time)[] Landmarks =
        {
            ("departure", .8f), ("bank", 2.45f), ("jump-entry", 3.4f), ("jump-exit", 4.6f), ("touchdown", 7.3f)
        };

        public Camera CockpitCamera { get; private set; }
        public Camera ChaseCamera { get; private set; }
        public ShuttleRig Shuttle { get; private set; }
        public float Clock { get; private set; }
        public bool Playing { get; set; } = true;
        public int View { get; private set; }

        readonly List<Object> owned = new();
        Transform world;
        Shader litShader;
        Material deck, steel, pale, orange, glow, ice, dark, glass, stripe;
        Transform aperture, moonRoot;
        GUIStyle body, strong, small;
        Vector3 chasePosition;
        bool chaseSnapped;
        BerthRig departureBerth;

        struct FlightPose
        {
            public Vector3 position;
            public float yaw, pitch, roll;
        }

        static readonly float[] Times = { 0, .24f, .8f, 1.6f, 2.6f, 3.3f, 4f, 4.8f, 6f, 7.5f };
        static readonly FlightPose[] Keys =
        {
            Pose(0, 1.55f, 38, 0, 0, 0),
            Pose(0, 1.55f, 38, 0, 0, 0),
            Pose(0, 3.2f, 44, 2, 6, 0),
            Pose(1.5f, 6f, 60, 8, 6, -6),
            Pose(6, 8.2f, 84, 8, 1, -20),
            Pose(9.5f, 9f, 100, 3, 1, -12),
            Pose(10, 9.2f, 118, 1, 1, -4),
            Pose(9, 8.6f, 140, 0, -3, 0),
            Pose(5, 5.4f, 180, -5, -8, 3),
            Pose(0, 1.75f, 232, 0, 0, 0)
        };

        static FlightPose Pose(float x, float y, float z, float yaw, float pitch, float roll) =>
            new() { position = new Vector3(x, y, z), yaw = yaw, pitch = pitch, roll = roll };

        void Awake()
        {
            world = new GameObject("Flight world").transform;
            world.SetParent(transform, false);
            CreatePalette();
            BuildWorld();
            BuildRelaySequence();
            Shuttle = ShuttleKit.Build(world, new ShuttlePalette
            {
                Ceramic = pale, Metal = steel, Dark = dark, Glass = glass,
                Stripe = stripe, Glow = ice, ConsoleGlow = glow, Seat = dark
            }, true, owned);
            BuildJumpEffects();
            BuildCameras();
            SetClock(0);
        }

        void CreatePalette()
        {
            litShader = Shader.Find(GraphicsSettings.currentRenderPipeline == null
                ? "Standard" : "Universal Render Pipeline/Lit");
            deck = Surface("Flight deck plating", new Color32(52, 68, 79, 255), .36f);
            steel = Surface("Flight gunmetal", new Color32(83, 107, 115, 255), .55f);
            pale = Surface("Flight ceramic", new Color32(170, 181, 176, 255), .25f);
            orange = Surface("Salvage orange", new Color32(206, 104, 52, 255), .24f);
            stripe = Surface("Crew stripe", new Color32(203, 102, 51, 255), .25f);
            dark = Surface("Engine dark metal", new Color32(40, 49, 56, 255), .66f);
            ice = Surface("Cold light", new Color32(150, 228, 240, 255), 0, 1.8f);
            glow = Surface("Instrument glow", new Color32(255, 196, 118, 255), 0, 1.2f);
            glass = Glass("Canopy glass", new Color32(126, 176, 190, 255));
        }

        Material Surface(string label, Color colour, float metal, float emission = 0, float smoothness = .34f)
        {
            var material = new Material(litShader) { name = label, color = colour };
            material.SetFloat("_Metallic", metal);
            material.SetFloat("_Smoothness", smoothness);
            if (emission > 0)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", colour.linear * emission);
            }
            owned.Add(material);
            return material;
        }

        Material Glass(string label, Color colour)
        {
            colour.a = .15f;
            var material = new Material(litShader) { name = label, color = colour };
            material.SetFloat("_Metallic", .1f);
            material.SetFloat("_Smoothness", .92f);
            material.SetFloat("_Surface", 1);
            material.SetFloat("_Blend", 0);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0);
            material.SetFloat("_Cull", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetOverrideTag("RenderType", "Transparent");
            owned.Add(material);
            return material;
        }

        void BuildCameras()
        {
            var cockpit = new GameObject("Cockpit camera").AddComponent<Camera>();
            cockpit.transform.SetParent(Shuttle.CockpitEye, false);
            cockpit.fieldOfView = 60;
            cockpit.nearClipPlane = .04f;
            cockpit.farClipPlane = 900;
            cockpit.clearFlags = CameraClearFlags.SolidColor;
            cockpit.backgroundColor = new Color32(5, 10, 20, 255);
            cockpit.depth = 2;
            cockpit.gameObject.AddComponent<AudioListener>();
            CockpitCamera = cockpit;

            var chase = new GameObject("Chase camera").AddComponent<Camera>();
            chase.transform.SetParent(world, false);
            chase.fieldOfView = 52;
            chase.nearClipPlane = .1f;
            chase.farClipPlane = 900;
            chase.clearFlags = CameraClearFlags.SolidColor;
            chase.backgroundColor = new Color32(5, 10, 20, 255);
            chase.depth = 1;
            ChaseCamera = chase;

            SetView(1);
        }

        public void SetView(int view)
        {
            View = Mathf.Clamp(view, 0, 1);
            if (CockpitCamera != null) CockpitCamera.enabled = View == 0;
            if (ChaseCamera != null) ChaseCamera.enabled = View == 1;
        }

        public void SetPlaying(bool playing) => Playing = playing;

        public void SetClock(float time)
        {
            Clock = Mathf.Clamp(time, 0, Duration);
            chaseSnapped = false;
            ApplyClock(Clock, true);
        }

        void Update()
        {
            var keys = Keyboard.current;
            if (RelayPlayer != null)
            {
                UpdateRelay(keys, Time.deltaTime);
                return;
            }
            if (keys != null)
            {
                if (keys.eKey.wasPressedThisFrame && BeginRelayWalk()) return;
                if (keys.spaceKey.wasPressedThisFrame)
                {
                    if (!Playing && Clock >= Duration) SetClock(0);
                    Playing = !Playing;
                }
                if (keys.digit1Key.wasPressedThisFrame) SetView(0);
                if (keys.digit2Key.wasPressedThisFrame) SetView(1);
                if (keys.leftBracketKey.wasPressedThisFrame) SetClock(Clock - .1f);
                if (keys.rightBracketKey.wasPressedThisFrame) SetClock(Clock + .1f);
                if (keys.lKey.wasPressedThisFrame) SetClock(NextLandmark());
                if (keys.rKey.wasPressedThisFrame) { SetClock(0); Playing = true; }
            }

            if (!Playing) return;
            Clock = Mathf.Min(Clock + Time.deltaTime, Duration);
            ApplyClock(Clock, false);
            if (Clock >= Duration) Playing = false;
        }

        float NextLandmark()
        {
            foreach (var landmark in Landmarks)
                if (landmark.Time > Clock + .05f) return landmark.Time;
            return 0;
        }

        void ApplyClock(float time, bool snap)
        {
            var pose = Evaluate(time);
            var rotation = Quaternion.Euler(pose.pitch, pose.yaw, pose.roll);
            Shuttle.Root.SetPositionAndRotation(pose.position, rotation);
            departureBerth.SetClampRelease(Mathf.SmoothStep(0, 1, time / .46f));
            Shuttle.SetGear(GearDeploy(time));
            Shuttle.SetFins(time is > 1.6f and < 4.2f
                ? 12 * Mathf.Sin((time - 1.6f) / 2.6f * Mathf.PI) : 0);
            ApplyJumpEffects(time);
            Chase(snap);
        }

        static FlightPose Evaluate(float time)
        {
            var segment = 0;
            while (segment < Times.Length - 2 && time > Times[segment + 1]) segment++;
            var span = Times[segment + 1] - Times[segment];
            var u = span <= 0 ? 0 : Mathf.Clamp01((time - Times[segment]) / span);
            var ease = u * u * (3 - 2 * u);
            var a = Keys[segment];
            var b = Keys[segment + 1];
            return new FlightPose
            {
                position = Spline(segment, u),
                yaw = Mathf.Lerp(a.yaw, b.yaw, ease),
                pitch = Mathf.Lerp(a.pitch, b.pitch, ease),
                roll = Mathf.Lerp(a.roll, b.roll, ease)
            };
        }

        static Vector3 Spline(int index, float u)
        {
            var start = Keys[index].position;
            var end = Keys[index + 1].position;
            var span = Times[index + 1] - Times[index];
            var startVelocity = KeyVelocity(index) * span;
            var endVelocity = KeyVelocity(index + 1) * span;
            var u2 = u * u;
            var u3 = u2 * u;
            return (2 * u3 - 3 * u2 + 1) * start + (u3 - 2 * u2 + u) * startVelocity
                + (-2 * u3 + 3 * u2) * end + (u3 - u2) * endVelocity;
        }

        static Vector3 KeyVelocity(int index)
        {
            if (index == 0 || index == Keys.Length - 1 ||
                Keys[index - 1].position == Keys[index].position ||
                Keys[index].position == Keys[index + 1].position)
                return Vector3.zero;
            return (Keys[index + 1].position - Keys[index - 1].position) /
                (Times[index + 1] - Times[index - 1]);
        }

        static float GearDeploy(float time)
        {
            if (time < 1f) return 1;
            if (time < 1.9f) return 1 - Mathf.SmoothStep(0, 1, (time - 1f) / .9f);
            if (time < 5f) return 0;
            if (time < 6.4f) return Mathf.SmoothStep(0, 1, (time - 5f) / 1.4f);
            return 1;
        }

        void Chase(bool snap)
        {
            var root = Shuttle.Root;
            var elevation = Mathf.Lerp(8.3f, 5.4f, Mathf.SmoothStep(0, 1, Clock / 1.7f));
            var desired = root.TransformPoint(new Vector3(-5f, elevation, -13.5f));
            if (snap || !chaseSnapped)
            {
                chasePosition = desired;
                chaseSnapped = true;
            }
            else
            {
                chasePosition = Vector3.Lerp(chasePosition, desired, 1 - Mathf.Exp(-9f * Time.deltaTime));
            }
            var aim = root.TransformPoint(new Vector3(0, 2.6f, 2.6f));
            ChaseCamera.transform.SetPositionAndRotation(chasePosition,
                Quaternion.LookRotation(aim - chasePosition, Vector3.up));
        }

        void OnDestroy()
        {
            if (world != null) Destroy(world.gameObject);
            foreach (var item in owned)
                if (item != null) Destroy(item);
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (body == null)
            {
                body = Style(15, Color.white);
                strong = Style(20, Color.white, true);
                small = Style(12, new Color32(146, 232, 238, 255), true);
            }

            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 960f, Screen.height / 600f), .45f, 1.4f);
            float width = Screen.width / scale, height = Screen.height / scale;
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            var panel = new Color(.026f, .06f, .078f, .94f);
            if (RelayPlayer != null)
            {
                RelayHud(width, height, panel);
                GUI.matrix = previous;
                return;
            }

            Panel(new Rect(20, 20, 300, 84), panel);
            Panel(new Rect(20, 20, 4, 84), new Color32(146, 232, 238, 255));
            GUI.Label(new Rect(37, 27, 270, 18), "ORBITAL SALVAGE   /   TRANSIT", small);
            GUI.Label(new Rect(37, 45, 270, 26), $"{Clock:0.00} s   /   7.50 s", strong);
            GUI.Label(new Rect(37, 73, 270, 20), Clock >= Duration ? "E   Disembark with the core" :
                Playing ? "Playing   •   Space pause" : "Paused   •   Space play", body);

            var bar = new Rect(20, height - 64, width - 40, 10);
            Panel(bar, new Color(.05f, .11f, .14f, .9f));
            Panel(new Rect(bar.x, bar.y, bar.width * (Clock / Duration), bar.height),
                new Color32(146, 232, 238, 255));
            foreach (var landmark in Landmarks)
            {
                float x = bar.x + bar.width * (landmark.Time / Duration);
                Panel(new Rect(x - 1, bar.y - 4, 2, bar.height + 8), new Color32(255, 170, 96, 255));
                GUI.Label(new Rect(x - 40, bar.y - 22, 80, 16), landmark.Name, small);
            }

            const string help = "Space  Pause   1  Cockpit   2  Chase   [ ]  Scrub   L  Landmark   R  Restart";
            float length = body.CalcSize(new GUIContent(help)).x + 28;
            Panel(new Rect((width - length) / 2, height - 40, length, 28), panel);
            GUI.Label(new Rect((width - length) / 2 + 14, height - 40, length, 28), help, body);
            GUI.matrix = previous;
        }

        static GUIStyle Style(int size, Color colour, bool bold = false)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = size, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft, padding = new RectOffset()
            };
            style.normal.textColor = colour;
            return style;
        }

        static void Panel(Rect rect, Color colour)
        {
            var original = GUI.color;
            GUI.color = colour;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = original;
        }
    }
}
