using System;
using UnityEngine;

namespace Workbench
{
    [Serializable]
    public sealed class ReplayMotion
    {
        [Serializable]
        public sealed class Trace
        {
            public string id;
            public float firstYaw, lastYaw, firstRadius, lastRadius, firstHeight, lastHeight;
        }

        public int width, height, framesPerSecond, framesPerTrace, triangleBudget, warmupFrames, repeats;
        public float verticalFieldOfView, targetY;
        public Trace[] traces;

        public static TextAsset Source() => Resources.Load<TextAsset>("temporal-replay");

        public static ReplayMotion Load()
        {
            var source = Source();
            var config = source == null ? null : JsonUtility.FromJson<ReplayMotion>(source.text);
            if (config == null || config.width < 1 || config.height < 1 || config.framesPerSecond < 1 ||
                config.framesPerTrace < 2 || config.triangleBudget < 1 || config.warmupFrames < 10 ||
                config.repeats < 5 || config.traces == null || config.traces.Length == 0)
                throw new InvalidOperationException("The temporal replay protocol is missing or invalid.");
            return config;
        }

        public void Configure(Camera camera)
        {
            camera.orthographic = false;
            camera.fieldOfView = verticalFieldOfView;
            camera.aspect = (float)width / height;
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 50f;
        }

        // Positions are indexed, not driven by an editor/player's wall-clock delta.
        public void SetPose(Camera camera, Trace trace, int index)
        {
            var t = (float)index / (framesPerTrace - 1);
            var yaw = Mathf.Lerp(trace.firstYaw, trace.lastYaw, t) * Mathf.Deg2Rad;
            var radius = Mathf.Lerp(trace.firstRadius, trace.lastRadius, t);
            var position = new Vector3(radius * Mathf.Sin(yaw),
                Mathf.Lerp(trace.firstHeight, trace.lastHeight, t), -radius * Mathf.Cos(yaw));
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(new Vector3(0, targetY, 0) - position));
        }
    }
}
