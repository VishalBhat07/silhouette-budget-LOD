using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Workbench.Demo
{
    public enum SalvagePhase { RecoverCell, PowerLift, RetrieveCell, RestoreEngine, RecoverCore, ReachExit, Complete }

    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(-20)]
    public sealed class SalvageMission : MonoBehaviour
    {
        public SalvagePhase Phase { get; private set; }
        public CharacterController Player { get; private set; }
        public bool IsFrozen { get; private set; }

        static readonly Color Suit = new Color32(231, 208, 166, 255);
        static readonly Color Carbon = new Color32(39, 57, 66, 255);
        static readonly Color Signal = new Color32(255, 139, 51, 255);
        static readonly Color Coolant = new Color32(85, 248, 222, 255);
        static readonly string[] Objectives =
        {
            "Find the spare power cell", "Clear the freight passage", "Take back the power cell",
            "Restore emergency power", "Recover the navigation core", "Extract with the core", "Board the salvage shuttle"
        };
        static readonly string[] Calls =
        {
            "Drone: The core is locked. I marked a spare cell.",
            "Drone: Give the lift power for one cycle.",
            "Drone: Wait for the lift, then take the cell.",
            "Drone: The engine socket is behind the cage.",
            "Drone: The core cradle is open.",
            "Drone: Follow my light through the hatch.",
            "Drone: Core secured. Shuttle berth is ahead."
        };
        static readonly string[] Actions =
        {
            "E   Take power cell", "E   Power cargo lift", "E   Retrieve power cell",
            "E   Restore emergency power", "E   Take navigation core", "E   Confirm extraction", "E   Board shuttle"
        };
        static readonly string[] Chapters =
        {
            "OBSERVATION GANTRY", "CARGO PASSAGE", "CARGO PASSAGE",
            "ENGINEERING", "ENGINEERING", "EXTRACTION", "SHUTTLE BERTH"
        };

        Camera view;
        WorkbenchController allocation;
        SalvageDock dock;
        Transform cargo, liftSocket, socket, cradle, hatch, droneHome, drone, cell, core, body;
        SalvageTechnician technician;
        Material hullMaterial;
        MaterialPropertyBlock tint;
        float yaw, pitch, verticalSpeed, clock;
        float cameraArm, cameraZoom, measuredSpeed;
        Vector3 planarVelocity;
        int budgetStep;
        Vector3 spawn, dronePosition;
        Quaternion arrivalHeading;
        GUIStyle bodyText, heading, small;

        void Start()
        {
            view = GetComponent<Camera>();
            view.nearClipPlane = .12f;
            tint = new MaterialPropertyBlock();
            allocation = FindAnyObjectByType<WorkbenchController>();
            dock = GetComponent<SalvageDock>();
            budgetStep = allocation == null ? 200 : Mathf.Max(100, Mathf.RoundToInt(allocation.TriangleBudget / 1600f) * 100);
            cargo = GameObject.Find("Power Cell Pickup Placeholder").transform;
            socket = GameObject.Find("Engine Socket Placeholder").transform;
            droneHome = GameObject.Find("Service Drone Placeholder").transform;
            hatch = GameObject.Find("Exit Hatch").transform;
            liftSocket = Group("Lift objective", null, new Vector3(-2.8f, 1, 13.1f));
            cradle = Group("Core objective", null, new Vector3(-2, 1.1f, 19.7f));
            var gantryRenderer = GameObject.Find("Observation Gantry").GetComponent<Renderer>();
            var gantry = gantryRenderer.bounds;
            var arrival = GameObject.Find("Salvage Arrival")?.transform;
            spawn = arrival != null ? arrival.position : new Vector3(gantry.center.x - 2.4f, gantry.max.y + .08f, gantry.center.z);
            arrivalHeading = arrival != null ? arrival.rotation : Quaternion.Euler(0, 12, 0);
            hullMaterial = gantryRenderer.sharedMaterial;
            HidePlaceholder(cargo);
            HidePlaceholder(droneHome);
            SalvageCollision.Install(allocation);
            BuildActors();
            RestartMission();
        }

        static void HidePlaceholder(Transform marker)
        {
            marker.GetComponent<Renderer>().enabled = false;
            marker.GetComponent<Collider>().enabled = false;
        }

        static Transform Group(string label, Transform parent, Vector3 position)
        {
            var node = new GameObject(label).transform;
            node.SetParent(parent, false);
            node.localPosition = position;
            return node;
        }

        Transform Part(Transform parent, string label, PrimitiveType shape, Vector3 position, Vector3 size, Color color)
        {
            var obj = GameObject.CreatePrimitive(shape);
            obj.name = label;
            obj.layer = 2; // Ignore Raycast; camera collision should only see dock geometry.
            var collider = obj.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = size;
            var renderer = obj.GetComponent<Renderer>();
            renderer.sharedMaterial = hullMaterial;
            tint.SetColor("_BaseColor", color);
            tint.SetColor("_Color", color);
            renderer.SetPropertyBlock(tint);
            return obj.transform;
        }

        void BuildActors()
        {
            var rig = Group("Salvage technician", null, spawn);
            rig.gameObject.layer = 2;
            Player = rig.gameObject.AddComponent<CharacterController>();
            Player.height = 1.84f;
            Player.center = Vector3.up * .92f;
            Player.radius = .3f;
            Player.stepOffset = .35f;
            Player.skinWidth = .035f;
            body = Group("Technician visual root", rig, Vector3.zero);
            technician = SalvageTechnician.Create(body);

            drone = Group("Service drone", null, droneHome.position);
            Part(drone, "Chassis", PrimitiveType.Sphere, Vector3.zero, new Vector3(.85f, .50f, .64f), Signal);
            Part(drone, "Optics", PrimitiveType.Sphere, new Vector3(0, 0, .27f), new Vector3(.44f, .21f, .16f), Carbon);
            Part(drone, "Sensor", PrimitiveType.Sphere, new Vector3(0, .01f, .35f), new Vector3(.17f, .14f, .06f), Coolant);
            foreach (int side in new[] { -1, 1 })
                Part(drone, "Hover pod", PrimitiveType.Cylinder, new Vector3(side * .46f, 0, 0), new Vector3(.37f, .10f, .37f), Carbon);

            cell = Group("Power cell", null, cargo.position);
            Part(cell, "Cell core", PrimitiveType.Cylinder, Vector3.zero, new Vector3(.43f, .28f, .43f), Coolant);
            foreach (int side in new[] { -1, 1 })
                Part(cell, "Terminal", PrimitiveType.Cylinder, Vector3.up * (side * .29f), new Vector3(.49f, .06f, .49f), Signal);

            core = Group("Navigation core", null, cradle.position);
            Part(core, "Armored shell", PrimitiveType.Cube, Vector3.zero, new Vector3(.39f, .34f, .49f), Carbon);
            Part(core, "Flight recorder", PrimitiveType.Cube, Vector3.up * .18f, new Vector3(.31f, .06f, .42f), Signal);
            for (int rail = -1; rail <= 1; rail++)
                Part(core, "Status strip", PrimitiveType.Cube, new Vector3(rail * .085f, .215f, 0),
                    new Vector3(.035f, .012f, .32f), Coolant);
            foreach (int side in new[] { -1, 1 })
                Part(core, "Cradle connector", PrimitiveType.Cube, Vector3.right * (side * .205f),
                    new Vector3(.06f, .22f, .34f), Suit);
        }

        public void RestartMission()
        {
            Phase = SalvagePhase.RecoverCell;
            SetFrozen(false);
            yaw = arrivalHeading.eulerAngles.y;
            verticalSpeed = clock = 0;
            planarVelocity = Vector3.zero;
            measuredSpeed = 0;
            cameraZoom = 0;
            cameraArm = 0;
            pitch = 10;
            Player.enabled = false;
            Player.transform.SetPositionAndRotation(spawn, Quaternion.identity);
            Player.enabled = true;
            body.rotation = arrivalHeading;
            dronePosition = droneHome.position;
            drone.SetPositionAndRotation(dronePosition, Quaternion.identity);
            SetDown(cell, cargo.position);
            SetDown(core, cradle.position);
            technician.Animate(0, false);
            dock.SetLiftOpen(false);
            dock.SetPowered(false);
            UpdateCamera(0);
        }

        Transform Target => Phase switch
        {
            SalvagePhase.RecoverCell => cargo,
            SalvagePhase.PowerLift or SalvagePhase.RetrieveCell => liftSocket,
            SalvagePhase.RestoreEngine => socket,
            SalvagePhase.RecoverCore => cradle,
            SalvagePhase.Complete => dock.BoardingPort,
            _ => hatch
        };

        float TargetDistance()
        {
            var delta = Target.position - Player.transform.position;
            return new Vector2(delta.x, delta.z).magnitude;
        }

        bool InReach => TargetDistance() <= 2.15f &&
            Mathf.Abs(Target.position.y - (Player.transform.position.y + 1)) < 1.8f &&
            (Phase != SalvagePhase.RetrieveCell || dock.LiftOpen) &&
            (Phase != SalvagePhase.RestoreEngine || Player.transform.position.z > 14.4f) &&
            (Phase != SalvagePhase.ReachExit || Player.transform.position.z > hatch.position.z + .25f);

        static void SetDown(Transform item, Vector3 position)
        {
            item.SetParent(null, false);
            item.SetPositionAndRotation(position, Quaternion.identity);
        }

        void Carry(Transform item)
        {
            item.SetParent(technician.CarrySocket, false);
            item.localPosition = Vector3.zero;
            item.localRotation = Quaternion.identity;
        }

        public bool Interact()
        {
            if (IsFrozen || !InReach) return false;
            switch (Phase)
            {
                case SalvagePhase.RecoverCell:
                    Carry(cell);
                    Phase = SalvagePhase.PowerLift;
                    break;
                case SalvagePhase.PowerLift:
                    SetDown(cell, liftSocket.position);
                    dock.SetLiftOpen(true);
                    Phase = SalvagePhase.RetrieveCell;
                    break;
                case SalvagePhase.RetrieveCell:
                    Carry(cell);
                    Phase = SalvagePhase.RestoreEngine;
                    break;
                case SalvagePhase.RestoreEngine:
                    SetDown(cell, socket.position + Vector3.back * .57f);
                    dock.SetPowered(true);
                    Phase = SalvagePhase.RecoverCore;
                    break;
                case SalvagePhase.RecoverCore:
                    Carry(core);
                    Phase = SalvagePhase.ReachExit;
                    break;
                case SalvagePhase.ReachExit:
                    Phase = SalvagePhase.Complete;
                    break;
                case SalvagePhase.Complete:
                    SceneManager.LoadScene("ShuttleFlight");
                    break;
            }
            return true;
        }

        void Update()
        {
            var keys = Keyboard.current;
            if (keys != null)
            {
                if (keys.rKey.wasPressedThisFrame) { RestartMission(); return; }
                if (keys.fKey.wasPressedThisFrame) SetFrozen(!IsFrozen);
                if (allocation != null)
                {
                    if (keys.digit1Key.wasPressedThisFrame) allocation.ActivePolicy = LodPolicy.ScreenSize;
                    if (keys.digit2Key.wasPressedThisFrame) allocation.ActivePolicy = LodPolicy.Silhouette;
                    if (keys.digit3Key.wasPressedThisFrame) allocation.ActivePolicy = LodPolicy.ViewAverage;
                    if (keys.leftBracketKey.wasPressedThisFrame) allocation.TriangleBudget -= budgetStep;
                    if (keys.rightBracketKey.wasPressedThisFrame) allocation.TriangleBudget += budgetStep;
                }
            }
            if (IsFrozen) return;
            float dt = Time.deltaTime;
            var mouse = Mouse.current;
            if (mouse != null)
                cameraZoom = Mathf.Clamp(cameraZoom - mouse.scroll.ReadValue().y * .005f, -1.3f, 1.4f);
            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 look = mouse.delta.ReadValue();
                yaw += look.x * .16f;
                pitch = Mathf.Clamp(pitch - look.y * .14f, -8, 66);
            }

            Vector2 input = Vector2.zero;
            if (keys != null)
            {
                if (keys.wKey.isPressed || keys.upArrowKey.isPressed) input.y++;
                if (keys.sKey.isPressed || keys.downArrowKey.isPressed) input.y--;
                if (keys.dKey.isPressed || keys.rightArrowKey.isPressed) input.x++;
                if (keys.aKey.isPressed || keys.leftArrowKey.isPressed) input.x--;
            }
            Vector3 move = Quaternion.Euler(0, yaw, 0) * new Vector3(input.x, 0, input.y).normalized;
            bool sprint = keys != null && (keys.leftShiftKey.isPressed || keys.rightShiftKey.isPressed);
            float speed = sprint ? 7.2f : 4.1f;
            planarVelocity = Vector3.MoveTowards(planarVelocity, move * speed, (input.sqrMagnitude > 0 ? 20 : 26) * dt);
            // Stay on the descending service ramp at sprint speed.
            verticalSpeed = Player.isGrounded && verticalSpeed <= 0 ? -4.2f : Mathf.Max(verticalSpeed - 23 * dt, -35);
            var previousPosition = Player.transform.position;
            var displacement = (planarVelocity + Vector3.up * verticalSpeed) * dt;
            // Keep sprint steps small across long frames when crossing narrow rails or door seams.
            int steps = Mathf.Max(1, Mathf.CeilToInt(displacement.magnitude / .18f));
            for (int step = 0; step < steps; step++)
                Player.Move(displacement / steps);
            if (Player.transform.position.y < -6) { RestartMission(); return; }
            var travelled = Player.transform.position - previousPosition;
            travelled.y = 0;
            measuredSpeed = dt > 0 ? travelled.magnitude / dt : 0;
            if (travelled.sqrMagnitude > .000001f)
            {
                body.rotation = Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(travelled), 580 * dt);
            }
            if (keys != null && keys.eKey.wasPressedThisFrame) Interact();
            float animationSpeed = Mathf.Clamp01(measuredSpeed / 7.2f);
            technician.Animate(animationSpeed,
                cell.parent == technician.CarrySocket || core.parent == technician.CarrySocket);
            clock += dt;
            UpdateDrone(dt);
            if (Phase == SalvagePhase.RecoverCell)
                cell.position = cargo.position + Vector3.up * (.10f + .06f * Mathf.Sin(clock * 2));
            UpdateCamera(dt);
        }

        void UpdateDrone(float dt)
        {
            Vector3 goal = Phase switch
            {
                SalvagePhase.RecoverCell => Player.transform.position.z < 2 ? droneHome.position :
                    cargo.position + new Vector3(2.4f, -.5f, -.5f),
                SalvagePhase.PowerLift or SalvagePhase.RetrieveCell => new Vector3(-2.8f, 1.95f, 13.45f),
                SalvagePhase.RestoreEngine when dronePosition.z < 15.7f => new Vector3(-2.8f, 1.9f, 15.9f),
                SalvagePhase.RestoreEngine => socket.position + new Vector3(-1.65f, .75f, -1.3f),
                SalvagePhase.RecoverCore => cradle.position + new Vector3(1.1f, .85f, -.8f),
                _ => new Vector3(0, 2f, 24.4f)
            };
            dronePosition = Vector3.MoveTowards(dronePosition, goal, 3.5f * dt);
            drone.position = dronePosition + Vector3.up * (.13f * Mathf.Sin(clock * 2.7f));
            Vector3 toward = Player.transform.position - drone.position;
            toward.y = 0;
            if (toward.sqrMagnitude > .01f)
                drone.rotation = Quaternion.RotateTowards(drone.rotation, Quaternion.LookRotation(toward), 210 * dt);
        }

        void UpdateCamera(float dt)
        {
            var feet = Player.transform.position;
            var pivot = feet + Vector3.up * 1.52f;
            var rotation = Quaternion.Euler(pitch, yaw, 0);
            bool corridor = feet.z >= 12.4f && feet.z < 16.2f && feet.x < -1.4f && feet.x > -4.4f;
            bool engineering = feet.z >= 16.2f && feet.z < 23.3f && Mathf.Abs(feet.x) < 4.45f;
            float desiredFieldOfView = corridor || engineering ? 60 : 64;
            view.fieldOfView = dt <= 0 ? desiredFieldOfView : Mathf.MoveTowards(view.fieldOfView, desiredFieldOfView, 22 * dt);
            float distance = Mathf.Clamp((corridor ? 2.55f : engineering ? 3.5f : 4.8f) + cameraZoom, 1.35f, 6.6f);
            var boom = rotation * new Vector3(corridor ? .42f : 1.08f, 0, -distance);
            float radius = Mathf.Max(.22f, view.nearClipPlane * 1.4f);
            float limit = Physics.SphereCast(pivot, radius, boom.normalized, out var hit, boom.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? Mathf.Max(0, hit.distance - .08f) : boom.magnitude;
            cameraArm = dt <= 0 || cameraArm > limit ? limit : Mathf.MoveTowards(cameraArm, limit, 5.5f * dt);
            transform.SetPositionAndRotation(pivot + boom.normalized * cameraArm, rotation);
        }

        public void SetFrozen(bool freeze)
        {
            IsFrozen = freeze;
            technician.SetFrozen(freeze);
        }

        void OnDestroy()
        {
            if (Player != null) Destroy(Player.gameObject);
            if (drone != null) Destroy(drone.gameObject);
            if (cell != null && cell.parent == null) Destroy(cell.gameObject);
            if (core != null && core.parent == null) Destroy(core.gameObject);
            if (liftSocket != null) Destroy(liftSocket.gameObject);
            if (cradle != null) Destroy(cradle.gameObject);
        }

        static void Panel(Rect rect, Color color)
        {
            var original = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = original;
        }

        static GUIStyle LabelStyle(int size, Color color, bool bold = false)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = size, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft, padding = new RectOffset()
            };
            style.normal.textColor = color;
            return style;
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || Player == null) return;
            if (bodyText == null)
            {
                bodyText = LabelStyle(15, Color.white);
                heading = LabelStyle(22, Color.white, true);
                small = LabelStyle(13, Coolant, true);
            }

            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 940f, Screen.height / 590f), .45f, 1.35f);
            float width = Screen.width / scale, height = Screen.height / scale;
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            Color panel = new Color(.028f, .064f, .082f, .94f);
            int stage = (int)Phase;
            Panel(new Rect(20, 20, 393, 96), panel);
            Panel(new Rect(20, 20, 4, 96), Coolant);
            GUI.Label(new Rect(37, 28, 360, 18), $"ORBITAL SALVAGE   /   {Chapters[stage]}", small);
            GUI.Label(new Rect(37, 47, 368, 32), Objectives[stage], heading);
            GUI.Label(new Rect(37, 81, 369, 23), Calls[stage], bodyText);

            if (allocation != null)
            {
                float x = width - 245;
                Panel(new Rect(x, 20, 225, 118), panel);
                GUI.Label(new Rect(x + 15, 28, 200, 18), IsFrozen ? "LOD MONITOR   /   FROZEN" : "LOD MONITOR   /   LIVE", small);
                string policy = allocation.ActivePolicy == LodPolicy.ScreenSize ? "Screen size" :
                    allocation.ActivePolicy == LodPolicy.Silhouette ? "Directional silhouette" : "View averaged";
                GUI.Label(new Rect(x + 15, 49, 205, 28), policy, bodyText);
                var decision = allocation.CurrentDecision;
                string label = decision.Feasible ? "tri" : "tri  •  over cap";
                GUI.Label(new Rect(x + 15, 77, 207, 21), $"{decision.Spent:N0} / {allocation.TriangleBudget:N0} {label}", small);
                GUI.Label(new Rect(x + 15, 103, 207, 22), $"[ / ]   Adjust cap by {budgetStep:N0}", bodyText);
            }

            {
                var screen = view.WorldToViewportPoint(Target.position + Vector3.up * .45f);
                var bearing = new Vector2(screen.x - .5f, .5f - screen.y);
                if (screen.z < 0) bearing = -bearing;
                float x = Mathf.Clamp((.5f + bearing.x) * width, 42, width - 42);
                float y = Mathf.Clamp((.5f + bearing.y) * height, 151, height - 130);
                if (screen.z < 0 && Mathf.Abs(bearing.x) < .49f)
                    x = bearing.x < 0 ? 42 : width - 42;
                Panel(new Rect(x - 33, y - 12, 66, 24), panel);
                GUI.Label(new Rect(x - 25, y - 11, 60, 23), $"{TargetDistance():0} m", small);
            }

            string prompt = IsFrozen ? "Pose frozen   •   1 / 2 / 3 compare policies   •   F resume" :
                (Phase == SalvagePhase.RetrieveCell && !dock.LiftOpen ? "Freight lift cycling" :
                    InReach ? Actions[stage] : null);
            if (prompt != null)
            {
                float size = bodyText.CalcSize(new GUIContent(prompt)).x + 32;
                float x = (width - size) / 2;
                Panel(new Rect(x, height - 95, size, 35), panel);
                GUI.Label(new Rect(x + 16, height - 95, size - 32, 35), prompt, bodyText);
            }

            const string help = "WASD  Move   Shift  Run   RMB  Orbit   Wheel  Zoom   E  Interact   F  Freeze   1–3  Policy   R  Restart";
            float length = bodyText.CalcSize(new GUIContent(help)).x + 28;
            Panel(new Rect((width - length) / 2, height - 43, length, 31), panel);
            GUI.Label(new Rect((width - length) / 2 + 14, height - 43, length, 31), help, bodyText);
            GUI.matrix = previous;
        }
    }
}
