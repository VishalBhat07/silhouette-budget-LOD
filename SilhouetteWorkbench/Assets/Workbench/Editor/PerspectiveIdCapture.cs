using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Workbench
{
    public sealed class PerspectiveIdCapture : IDisposable
    {
        static readonly int ObjectToClip = Shader.PropertyToID("_WorkbenchObjectToClip");
        static readonly int ObjectId = Shader.PropertyToID("_WorkbenchObjectId");
        readonly int width;
        readonly int height;
        readonly Material material;
        readonly RenderTexture target;
        readonly Texture2D readback;
        readonly CommandBuffer commands = new CommandBuffer { name = "Workbench perspective IDs" };
        readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();

        public PerspectiveIdCapture(int width, int height)
        {
            var shader = Shader.Find("Hidden/Workbench/ObjectId");
            if (width < 1 || height < 1 || shader == null || !shader.isSupported)
                throw new ArgumentException("Perspective ID capture needs a valid size and ID shader.");
            this.width = width;
            this.height = height;
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear, 1);
            readback = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        }

        // Object IDs are red-channel bytes. ID 0 is background or an unmeasured depth-writing occluder.
        public byte[] Capture(Camera camera, Mesh[] meshes, Matrix4x4[] transforms, byte[] ids)
        {
            if (meshes.Length != transforms.Length || meshes.Length != ids.Length)
                throw new ArgumentException("Each mesh needs one transform and one ID.");
            var viewProjection = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * camera.worldToCameraMatrix;
            var previous = RenderTexture.active;
            try
            {
                commands.Clear();
                commands.SetRenderTarget(target);
                commands.ClearRenderTarget(true, true, Color.black);
                for (var i = 0; i < meshes.Length; i++)
                {
                    var mesh = meshes[i];
                    if (mesh == null) continue;
                    properties.SetMatrix(ObjectToClip, viewProjection * transforms[i]);
                    properties.SetVector(ObjectId, new Vector4(ids[i] / 255f, 0, 0, 1));
                    for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
                        commands.DrawMesh(mesh, Matrix4x4.identity, material, submesh, 0, properties);
                }
                Graphics.ExecuteCommandBuffer(commands);
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                var colors = readback.GetRawTextureData<Color32>();
                var pixels = new byte[width * height];
                for (var i = 0; i < pixels.Length; i++)
                {
                    var color = colors[i];
                    if (color.g != 0 || color.b != 0)
                        throw new InvalidOperationException($"Invalid ID pixel at {i}: {color}.");
                    pixels[i] = color.r;
                }
                return pixels;
            }
            finally
            {
                RenderTexture.active = previous;
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
