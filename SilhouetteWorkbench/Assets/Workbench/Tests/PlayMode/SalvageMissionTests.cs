#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Workbench.Demo;

namespace Workbench.Tests
{
    public class SalvageMissionTests
    {
        [UnityTest]
        public IEnumerator FullRecoveryMissionCanBeCompletedAndRestarted()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Workbench/Scenes/OrbitalSalvage.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var mission = Object.FindAnyObjectByType<SalvageMission>();
            Assert.That(mission.Player, Is.Not.Null);
            var spawn = mission.Player.transform.position;
            mission.Interact();
            Assert.That(mission.Phase, Is.EqualTo(SalvagePhase.RecoverCell));

            WalkTo(0, -6.25f);
            WalkTo(0, 0);
            WalkTo(-2.8f, 8);
            WalkTo(-3.1f, 11);
            Assert.That(mission.Interact(), Is.True);
            Assert.That(mission.Phase, Is.EqualTo(SalvagePhase.PowerLift));
            WalkTo(-2.8f, 12.45f);
            for (int step = 0; step < 20; step++)
                mission.Player.Move(new Vector3(0, -.03f, .18f));
            Assert.That(mission.Player.transform.position.z, Is.LessThan(14.35f), "The unpowered lift must block passage.");
            Assert.That(mission.Interact(), Is.True);
            Assert.That(mission.Phase, Is.EqualTo(SalvagePhase.RetrieveCell));
            Assert.That(mission.Interact(), Is.False, "The lift must finish before the cell is available.");
            var dock = mission.GetComponent<SalvageDock>();
            yield return new WaitUntil(() => dock.LiftOpen, System.TimeSpan.FromSeconds(5),
                () => Assert.Fail("Cargo lift failed to open."));
            Assert.That(mission.Interact(), Is.True);
            Assert.That(mission.Phase, Is.EqualTo(SalvagePhase.RestoreEngine));

            WalkTo(-2.8f, 16);
            WalkTo(2, 16.7f);
            Assert.That(mission.Interact(), Is.True);
            Assert.That(mission.Phase, Is.EqualTo(SalvagePhase.RecoverCore));
            WalkTo(-2, 17);
            WalkTo(-2, 18.65f);
            Assert.That(mission.Interact(), Is.True);
            Assert.That(mission.Phase, Is.EqualTo(SalvagePhase.ReachExit));
            WalkTo(0, 20.65f);
            WalkTo(0, 21.7f);
            Assert.That(mission.Interact(), Is.False, "Extraction requires crossing the hatch.");
            yield return new WaitForSeconds(1.1f);
            WalkTo(0, 23.95f);
            Assert.That(mission.Interact(), Is.True);
            Assert.That(mission.Phase, Is.EqualTo(SalvagePhase.Complete));
            mission.RestartMission();
            Assert.That(mission.Phase, Is.EqualTo(SalvagePhase.RecoverCell));
            Assert.That(Vector3.Distance(mission.Player.transform.position, spawn), Is.LessThan(.3f));

            void WalkTo(float x, float z)
            {
                var target = new Vector2(x, z);
                for (int step = 0; step < 240; step++)
                {
                    var position = mission.Player.transform.position;
                    var remaining = target - new Vector2(position.x, position.z);
                    if (remaining.magnitude < .12f) return;
                    var delta = Vector2.ClampMagnitude(remaining, .09f);
                    mission.Player.Move(new Vector3(delta.x, -.04f, delta.y));
                }
                Assert.Fail($"Walkable mission route blocked before ({x}, {z}) at {mission.Player.transform.position}.");
            }
        }
    }
}
#endif
