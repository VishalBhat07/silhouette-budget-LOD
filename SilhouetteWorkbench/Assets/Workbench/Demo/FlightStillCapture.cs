using System.Collections;
using System.IO;
using UnityEngine;

namespace Workbench.Demo
{
    /// <summary>Renders the flight landmark stills for review and export.</summary>
    public sealed class FlightStillCapture : MonoBehaviour
    {
        public bool Done { get; private set; }
        public string Folder { get; set; } = "Results/flight/blockout";
        public int Width { get; set; } = 1600;
        public int Height { get; set; } = 900;

        public void Begin(SalvageFlight flight) => StartCoroutine(Run(flight));

        IEnumerator Run(SalvageFlight flight)
        {
            Directory.CreateDirectory(Folder);
            flight.Playing = false;

            foreach (var view in new[] { 0, 1 })
            {
                flight.SetView(view);
                var camera = view == 0 ? flight.CockpitCamera : flight.ChaseCamera;
                foreach (var landmark in SalvageFlight.Landmarks)
                {
                    flight.SetClock(landmark.Time);
                    yield return null;
                    yield return Capture(camera,
                        Path.Combine(Folder, $"{landmark.Name}-{(view == 0 ? "cockpit" : "chase")}.png"));
                }
            }

            var overview = new GameObject("Berth overview camera").AddComponent<Camera>();
            overview.fieldOfView = 55;
            overview.nearClipPlane = .1f;
            overview.farClipPlane = 900;
            overview.transform.position = new Vector3(13.5f, 8.5f, 27);
            overview.transform.rotation = Quaternion.LookRotation(
                new Vector3(0, 3, 40) - overview.transform.position, Vector3.up);
            yield return Capture(overview, Path.Combine(Folder, "berth-overview.png"));
            Destroy(overview.gameObject);

            var station = new GameObject("Station overview camera").AddComponent<Camera>();
            station.fieldOfView = 55;
            station.nearClipPlane = .1f;
            station.farClipPlane = 900;
            station.transform.position = new Vector3(5, 6.5f, 221);
            station.transform.rotation = Quaternion.LookRotation(
                new Vector3(26, 6, 256) - station.transform.position, Vector3.up);
            yield return Capture(station, Path.Combine(Folder, "station-overview.png"));
            Destroy(station.gameObject);

            flight.SetView(1);
            flight.SetClock(0);
            Done = true;
            Debug.Log($"Flight stills written to {Folder}");
        }

        IEnumerator Capture(Camera camera, string path)
        {
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            var previous = camera.targetTexture;
            camera.targetTexture = target;
            yield return new WaitForEndOfFrame();
            RenderTexture.active = target;
            var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            camera.targetTexture = previous;
            RenderTexture.active = null;
            Destroy(target);
            Destroy(texture);
        }
    }
}
