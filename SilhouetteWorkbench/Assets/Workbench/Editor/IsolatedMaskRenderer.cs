using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Workbench
{
    // A direct draw avoids the scene camera's lighting, effects, and antialiasing.
    sealed class IsolatedMaskRenderer : IDisposable
    {
        public const string ShaderPath = "Assets/Workbench/Shaders/ObjectId.shader";
        public const float FramingMargin = 1.10f;
        static readonly int ObjectToClip = Shader.PropertyToID("_WorkbenchObjectToClip");
        static readonly int ObjectId = Shader.PropertyToID("_WorkbenchObjectId");

        readonly int resolution;
        readonly Material material;
        readonly RenderTexture target;
        readonly Texture2D readback;
        readonly CommandBuffer commands = new CommandBuffer { name = "Workbench silhouette" };
        readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();

        public IsolatedMaskRenderer(int resolution)
        {
            if (resolution < 3) throw new ArgumentOutOfRangeException(nameof(resolution));
            var shader = Shader.Find("Hidden/Workbench/ObjectId");
            if (shader == null || !shader.isSupported)
                throw new InvalidOperationException("Workbench ObjectId shader is unavailable.");

            this.resolution = resolution;
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            target = RenderTexture.GetTemporary(resolution, resolution, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear, 1);
            target.filterMode = FilterMode.Point;
            readback = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true);
        }

        public byte[] Capture(Mesh mesh, Bounds reference, Vector3 direction)
        {
            direction = direction.normalized;
            var radius = reference.extents.magnitude;
            if (radius <= 0 || direction == Vector3.zero)
                throw new ArgumentException("Capture needs nonempty reference bounds and a direction.");

            var up = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > .95f ? Vector3.forward : Vector3.up;
            var eye = reference.center + direction * (3 * radius);
            var rotation = Quaternion.LookRotation(-direction, up);
            var view = Matrix4x4.Scale(new Vector3(1, 1, -1)) *
                Matrix4x4.TRS(eye, rotation, Vector3.one).inverse;
            var extent = radius * FramingMargin;
            var ortho = Matrix4x4.Ortho(-extent, extent, -extent, extent, .1f * radius, 5 * radius);
            properties.SetMatrix(ObjectToClip, GL.GetGPUProjectionMatrix(ortho, true) * view);
            properties.SetVector(ObjectId, Vector4.one);

            var previousTarget = RenderTexture.active;
            try
            {
                commands.Clear();
                commands.SetRenderTarget(target);
                commands.ClearRenderTarget(true, true, Color.black);
                for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
                    commands.DrawMesh(mesh, Matrix4x4.identity, material, submesh, 0, properties);
                Graphics.ExecuteCommandBuffer(commands);
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0, false);
                var pixels = readback.GetPixels32();
                var mask = new byte[pixels.Length];
                for (var i = 0; i < pixels.Length; i++)
                {
                    var color = pixels[i];
                    if (color.r != color.g || color.g != color.b || (color.r != 0 && color.r != 255))
                        throw new InvalidOperationException($"Non-binary pixel in {mesh.name} at {i}: {color}.");
                    mask[i] = color.r;
                }
                return mask;
            }
            finally
            {
                RenderTexture.active = previousTarget;
            }
        }

        public void Dispose()
        {
            commands.Dispose();
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(material);
            UnityEngine.Object.DestroyImmediate(readback);
        }
    }
}
