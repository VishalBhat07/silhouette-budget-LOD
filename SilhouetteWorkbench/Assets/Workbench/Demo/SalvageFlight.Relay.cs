using UnityEngine;
using UnityEngine.InputSystem;

namespace Workbench.Demo
{
    public sealed partial class SalvageFlight
    {
        static readonly Vector3 RelaySpawn = new(5.3f, .34f, 234.4f);
        static readonly Vector3 CradlePosition = new(22.4f, 4.65f, 250);
        static readonly Vector3[] RoverStops =
        {
            new(7.6f, .30f, 240),
            new(7.6f, -.2f, 250),
            new(13.05f, 3.2f, 250),
            new(14.6f, 3.2f, 250),
            new(18.7f, 3.2f, 250),
            new(21.8f, 3.2f, 248.5f)
        };
        static readonly Color Amber = new Color32(255, 168, 89, 255);
        static readonly Color Green = new Color32(43, 216, 94, 255);

        public CharacterController RelayPlayer { get; private set; }
        public bool RelayCoreSeated { get; private set; }

        SalvageTechnician relayTechnician;
        Transform relayCore, relayCradle;
        Material relayIndicator;
        Light cradleLight;
        Vector3 roverHome, doorHome;
        Quaternion roverHeading, dishHome;
        float relayYaw, relayPitch, fallingSpeed, doorTravel, finaleTime;
        int escortStop;

        void BuildRelaySequence()
        {
            roverHome = relayRover.localPosition;
            roverHeading = relayRover.localRotation;
            doorHome = relayDoor.localPosition;
            dishHome = relayDish.localRotation;

            relayIndicator = Surface("Relay status", Amber, 0, 2);
            relayCradle = new GameObject("Navigation core uplink").transform;
            relayCradle.SetParent(moonRoot, false);
            relayCradle.localPosition = CradlePosition;
            Box(relayCradle, "Relay pedestal", dark, new Vector3(.06f, -.72f, 0), new Vector3(.95f, 1.46f, 1.28f))
                .gameObject.AddComponent<BoxCollider>();
            Box(relayCradle, "Core bed", steel, new Vector3(0, -.22f, 0), new Vector3(1.22f, .12f, 1.38f));
            Box(relayCradle, "Panel face", pale, new Vector3(-.46f, -.44f, 0), new Vector3(.1f, .36f, 1.02f));
            Box(relayCradle, "Status screen", relayIndicator, new Vector3(-.52f, -.40f, 0), new Vector3(.025f, .12f, .80f));
            foreach (float z in new[] { -.43f, .43f })
            {
                Box(relayCradle, "Cradle guide", orange, new Vector3(0, -.06f, z), new Vector3(.76f, .23f, .15f));
                Box(relayCradle, "Guide strip", relayIndicator, new Vector3(-.05f, .067f, z), new Vector3(.54f, .025f, .055f));
            }
            Beam(moonRoot, "Uplink cable", orange, new Vector3(22.4f, 3.25f, 250.6f),
                new Vector3(22.4f, 3.26f, 252.9f), .09f);
            Label("FLIGHT LOG / UPLINK", new Vector3(21.88f, 4.55f, 250), .18f,
                new Color32(200, 224, 217, 255), 90);
            cradleLight = Lamp("Core status light", new Vector3(22.15f, 5.05f, 250), Amber, 5.5f, 2.4f);
        }

        public bool BeginRelayWalk()
        {
            if (Clock < Duration || RelayPlayer != null) return false;

            Playing = false;
            var walker = new GameObject("Relay technician");
            walker.transform.SetParent(moonRoot, false);
            walker.transform.localPosition = RelaySpawn;
            walker.layer = 2;
            RelayPlayer = walker.AddComponent<CharacterController>();
            RelayPlayer.height = 1.84f;
            RelayPlayer.center = Vector3.up * .92f;
            RelayPlayer.radius = .3f;
            RelayPlayer.stepOffset = .35f;
            RelayPlayer.skinWidth = .035f;

            var body = new GameObject("Technician visual root").transform;
            body.SetParent(walker.transform, false);
            body.localRotation = Quaternion.Euler(0, 33, 0);
            relayTechnician = SalvageTechnician.Create(body);
            relayCore = new GameObject("Recovered navigation core").transform;
            relayCore.SetParent(relayTechnician.CarrySocket, false);
            Box(relayCore, "Armoured shell", dark, Vector3.zero, new Vector3(.39f, .34f, .49f));
            Box(relayCore, "Recorder cover", orange, new Vector3(0, .18f, 0), new Vector3(.31f, .06f, .42f));
            for (int rail = -1; rail <= 1; rail++)
                Box(relayCore, "Recorder stripe", ice, new Vector3(rail * .085f, .215f, 0),
                    new Vector3(.035f, .012f, .32f));
            foreach (var item in relayCore.GetComponentsInChildren<Transform>())
                item.gameObject.layer = 2;

            relayYaw = 33;
            relayPitch = 12;
            SetView(1);
            FrameRelayTechnician();
            return true;
        }

        public bool TrySeatRelayCore()
        {
            if (RelayPlayer == null || RelayCoreSeated || doorTravel < .95f || RelayPlayer.transform.position.x < 20 ||
                Vector3.Distance(RelayPlayer.transform.position + Vector3.up, relayCradle.position) > 2.15f)
                return false;

            RelayCoreSeated = true;
            relayCore.SetParent(relayCradle, false);
            relayCore.localPosition = Vector3.zero;
            relayCore.localRotation = Quaternion.identity;
            relayIndicator.color = Green;
            relayIndicator.SetColor("_EmissionColor", Green.linear * 1.1f);
            cradleLight.color = Green;
            cradleLight.intensity = 4.2f;
            relayTechnician.Animate(0, false);
            return true;
        }

        void UpdateRelay(Keyboard keys, float elapsed)
        {
            if (keys != null && keys.rKey.wasPressedThisFrame)
            {
                ResetRelay();
                SetClock(0);
                Playing = true;
                return;
            }
            if (RelayCoreSeated)
            {
                FrameRelayFinale(elapsed);
                return;
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
            {
                var mouseDelta = mouse.delta.ReadValue();
                relayYaw += mouseDelta.x * .16f;
                relayPitch = Mathf.Clamp(relayPitch - mouseDelta.y * .14f, -6, 48);
            }

            Vector3 input = Vector3.zero;
            if (keys != null)
            {
                if (keys.wKey.isPressed || keys.upArrowKey.isPressed) input.z++;
                if (keys.sKey.isPressed || keys.downArrowKey.isPressed) input.z--;
                if (keys.dKey.isPressed || keys.rightArrowKey.isPressed) input.x++;
                if (keys.aKey.isPressed || keys.leftArrowKey.isPressed) input.x--;
            }
            var direction = Quaternion.Euler(0, relayYaw, 0) * input.normalized;
            bool sprinting = keys != null && (keys.leftShiftKey.isPressed || keys.rightShiftKey.isPressed);
            var before = RelayPlayer.transform.position;
            fallingSpeed = RelayPlayer.isGrounded ? -4 : Mathf.Max(-22, fallingSpeed - 20 * elapsed);
            var delta = (direction * (sprinting ? 6.5f : 4.1f) + Vector3.up * fallingSpeed) * elapsed;
            int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / .18f));
            for (int i = 0; i < steps; i++) RelayPlayer.Move(delta / steps);
            var travel = RelayPlayer.transform.position - before;
            travel.y = 0;
            if (travel.sqrMagnitude > .00001f)
                relayTechnician.transform.rotation = Quaternion.RotateTowards(relayTechnician.transform.rotation,
                    Quaternion.LookRotation(travel), elapsed * 580);
            relayTechnician.Animate(elapsed > 0 ? Mathf.Clamp01(travel.magnitude / (elapsed * 6.5f)) : 0, true);

            AdvanceRover(elapsed);
            FrameRelayTechnician();
            if (keys != null && keys.eKey.wasPressedThisFrame) TrySeatRelayCore();
        }

        void AdvanceRover(float elapsed)
        {
            var position = RelayPlayer.transform.position;
            if (escortStop < RoverStops.Length && Vector3.Distance(position, relayRover.position) < 8.5f)
            {
                var target = RoverStops[escortStop];
                bool crossingDoor = escortStop == 4 && doorTravel < .95f;
                if (!crossingDoor)
                {
                    var motion = target - relayRover.localPosition;
                    relayRover.localPosition = Vector3.MoveTowards(relayRover.localPosition, target, 3.3f * elapsed);
                    motion.y = 0;
                    if (motion.sqrMagnitude > .001f)
                        relayRover.localRotation = Quaternion.RotateTowards(relayRover.localRotation,
                            Quaternion.LookRotation(motion), 150 * elapsed);
                    if (Vector3.Distance(relayRover.localPosition, target) < .025f) escortStop++;
                }
            }
            if (escortStop >= 4 || position.x >= 13.4f && position.z is > 248 and < 252)
            {
                doorTravel = Mathf.MoveTowards(doorTravel, 1, elapsed * 1.65f);
                relayDoor.localPosition = doorHome + Vector3.up * Mathf.SmoothStep(0, 3.5f, doorTravel);
            }
        }

        void FrameRelayTechnician()
        {
            var pivot = RelayPlayer.transform.position + Vector3.up * 1.51f;
            var orientation = Quaternion.Euler(relayPitch, relayYaw, 0);
            bool indoor = RelayPlayer.transform.position.x is > 15.4f and < 20f;
            var boom = orientation * new Vector3(indoor ? .38f : .78f, 0, indoor ? -2.2f : -4.5f);
            float arm = boom.magnitude;
            if (Physics.SphereCast(pivot, .2f, boom.normalized, out var hit, arm,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                arm = Mathf.Max(.08f, hit.distance - .045f);
            ChaseCamera.transform.SetPositionAndRotation(pivot + boom.normalized * arm, orientation);
            ChaseCamera.fieldOfView = indoor ? 59 : 62;
        }

        void FrameRelayFinale(float elapsed)
        {
            finaleTime += elapsed;
            float move = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.7f, 3.45f, finaleTime));
            float turn = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.25f, 3.25f, finaleTime));
            relayDish.localRotation = Quaternion.Slerp(dishHome, Quaternion.Euler(-38, 56, 0), turn);
            Vector3 from = Vector3.Lerp(new Vector3(20.75f, 5.35f, 247.5f),
                new Vector3(20.7f, 6.25f, 246.05f), move);
            Vector3 at = Vector3.Lerp(new Vector3(22.4f, 4.66f, 250),
                new Vector3(26.95f, 8.1f, 250.9f), move);
            ChaseCamera.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from));
            ChaseCamera.fieldOfView = Mathf.Lerp(52, 63, move);
        }

        void ResetRelay()
        {
            if (RelayCoreSeated) Destroy(relayCore.gameObject);
            Destroy(RelayPlayer.gameObject);
            RelayPlayer = null;
            RelayCoreSeated = false;
            ChaseCamera.fieldOfView = 52;
            finaleTime = fallingSpeed = doorTravel = 0;
            escortStop = 0;
            relayRover.SetLocalPositionAndRotation(roverHome, roverHeading);
            relayDoor.localPosition = doorHome;
            relayDish.localRotation = dishHome;
            relayIndicator.color = Amber;
            relayIndicator.SetColor("_EmissionColor", Amber.linear * 2);
            cradleLight.color = Amber;
            cradleLight.intensity = 2.4f;
        }

        void RelayHud(float width, float height, Color panel)
        {
            string title = RelayCoreSeated ? "FLIGHT LOG TRANSMITTED" : "MOON RELAY STATION";
            string objective = RelayCoreSeated ? "Navigation core secured. Relay is online." :
                doorTravel < .95f ? "Follow the rover to the airlock" : "Carry the core to the uplink";
            Panel(new Rect(20, 20, 398, 72), panel);
            GUI.Label(new Rect(35, 27, 365, 24), title, strong);
            GUI.Label(new Rect(35, 53, 365, 27), objective, body);

            if (!RelayCoreSeated)
            {
                var goal = doorTravel >= .95f ? relayCradle.position : RoverStops[Mathf.Min(escortStop, 3)] + Vector3.up * .8f;
                var position = ChaseCamera.WorldToViewportPoint(goal);
                var x = Mathf.Clamp(position.z < 0 ? 1 - position.x : position.x, .085f, .915f) * width;
                var y = Mathf.Clamp(1 - position.y, .19f, .78f) * height;
                Panel(new Rect(x - 26, y - 12, 52, 25), panel);
                GUI.Label(new Rect(x - 19, y - 11, 46, 21),
                    $"{Vector3.Distance(RelayPlayer.transform.position, goal):0} m", small);
            }

            string help = RelayCoreSeated ? "R  Replay flight" :
                "WASD  Move   Shift  Run   Hold RMB  Orbit   E  Seat core   R  Replay";
            float length = body.CalcSize(new GUIContent(help)).x + 28;
            Panel(new Rect((width - length) / 2, height - 43, length, 28), panel);
            GUI.Label(new Rect((width - length) / 2 + 14, height - 43, length - 28, 28), help, body);
        }
    }
}
