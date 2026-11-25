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
    public class SalvageCollisionTests
    {
        [UnityTest]
        public IEnumerator ServiceRampLeadsToTheApronAndItsGuardrail()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Workbench/Scenes/OrbitalSalvage.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            var player = Object.FindAnyObjectByType<SalvageMission>().Player;
            var spawn = GameObject.Find("Salvage Arrival").transform.position;
            Assert.That(Vector3.Distance(spawn, player.transform.position), Is.LessThan(.15f));
            WalkTo(7, -9.03f);
            WalkTo(10, -9.03f);
            for (int i = 0; i < 15; i++)
                player.Move(Vector3.down * .08f);
            Assert.That(player.transform.position.y, Is.InRange(.6f, 1.5f));
            WalkTo(13, -9.03f);
            for (int i = 0; i < 18; i++)
                player.Move(Vector3.down * .08f);
            Assert.That(player.transform.position.y, Is.LessThan(.2f));
            WalkTo(13, 12);
            for (int i = 0; i < 36; i++)
                player.Move(new Vector3(.18f, -.03f, 0));
            Assert.That(player.transform.position.x, Is.InRange(15f, 15.82f));

            void WalkTo(float x, float z)
            {
                var goal = new Vector2(x, z);
                for (int step = 0; step < 260; step++)
                {
                    var position = player.transform.position;
                    var remaining = goal - new Vector2(position.x, position.z);
                    if (remaining.magnitude <= .15f) return;
                    var delta = Vector2.ClampMagnitude(remaining, .12f);
                    player.Move(new Vector3(delta.x, -.06f, delta.y));
                }
                Assert.Fail($"Service route to {goal} blocked at {player.transform.position}.");
            }
        }

        [UnityTest]
        public IEnumerator PressureVesselStopsDirectAndObliqueSprint()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Workbench/Scenes/OrbitalSalvage.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            var mission = Object.FindAnyObjectByType<SalvageMission>();
            var player = mission.Player;
            var vessel = GameObject.Find("Forward Pressure Vessel").GetComponent<BoxCollider>();
            Assert.That(vessel, Is.Not.Null);
            Assert.That(vessel.isTrigger, Is.False);
            var front = vessel.bounds.min.z;
            var centerX = vessel.bounds.center.x;

            PlacePlayer(new Vector3(centerX, .03f, front - 2.2f));
            var direct = Move(new Vector3(0, -.025f, .24f));
            Assert.That(direct.HasFlag(CollisionFlags.Sides), Is.True);
            Assert.That(player.transform.position.z, Is.InRange(front - .55f, front));

            PlacePlayer(new Vector3(centerX - .55f, .03f, front - 1.5f));
            var angled = Move(new Vector3(.065f, -.025f, .23f));
            Assert.That(angled.HasFlag(CollisionFlags.Sides), Is.True);
            Assert.That(player.transform.position.z, Is.InRange(front - .55f, front));
            Assert.That(player.transform.position.x, Is.GreaterThan(centerX),
                "An oblique sprint should slide along the front of the vessel.");

            void PlacePlayer(Vector3 position)
            {
                player.enabled = false;
                player.transform.position = position;
                player.enabled = true;
                Physics.SyncTransforms();
            }

            CollisionFlags Move(Vector3 sprintStep)
            {
                var flags = CollisionFlags.None;
                for (int step = 0; step < 12; step++)
                    flags |= player.Move(sprintStep);
                return flags;
            }
        }
    }
}
#endif
