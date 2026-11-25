using System;
using UnityEngine;

namespace Workbench.Demo
{
    public sealed class SalvageTechnician : MonoBehaviour
    {
        const string ModelPath = "SalvageTechnician/Technician";
        const float Height = 1.84f;

        public Transform CarrySocket { get; private set; }
        public bool HasLocomotion => idle != null && walk != null && run != null;

        Animation motion;
        Transform leftUpper, leftLower, leftWrist, rightUpper, rightLower, rightWrist;
        string idle, walk, run, current;
        Material[] suitMaterials;
        bool holding, frozen;

        public static SalvageTechnician Create(Transform body)
        {
            var technician = body.gameObject.AddComponent<SalvageTechnician>();
            technician.Setup();
            return technician;
        }

        void Setup()
        {
            CarrySocket = new GameObject("Technician carry socket").transform;
            CarrySocket.SetParent(transform, false);
            CarrySocket.localPosition = new Vector3(.04f, .96f, .34f);

            var prefab = Resources.Load<GameObject>(ModelPath);
            if (prefab == null)
            {
                Debug.LogError("Salvage technician asset is missing.");
                return;
            }
            var fit = new GameObject("Suit height").transform;
            fit.SetParent(transform, false);
            fit.localRotation = Quaternion.Euler(0, 180, 0);
            var model = Instantiate(prefab, fit);
            model.name = "Quaternius pressure suit";
            foreach (var node in model.GetComponentsInChildren<Transform>(true))
                node.gameObject.layer = 2;

            var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            DressSuit(renderers);

            var bones = model.GetComponentsInChildren<Transform>(true);
            Transform Bone(string name) => Array.Find(bones, bone => bone.name == name);
            leftUpper = Bone("UpperArm.L");
            leftLower = Bone("LowerArm.L");
            leftWrist = Bone("Wrist.L");
            rightUpper = Bone("UpperArm.R");
            rightLower = Bone("LowerArm.R");
            rightWrist = Bone("Wrist.R");

            motion = model.GetComponent<Animation>();
            if (motion == null) motion = model.AddComponent<Animation>();
            motion.playAutomatically = false;
            motion.cullingType = AnimationCullingType.AlwaysAnimate;
            foreach (var clip in Resources.LoadAll<AnimationClip>(ModelPath))
            {
                if (!clip.legacy) continue;
                if (clip.name.EndsWith("Idle_Neutral", StringComparison.OrdinalIgnoreCase)) idle = Register(clip, "Idle");
                if (clip.name.EndsWith("Walk", StringComparison.OrdinalIgnoreCase)) walk = Register(clip, "Walk");
                if (clip.name.EndsWith("Run", StringComparison.OrdinalIgnoreCase)) run = Register(clip, "Run");
            }
            if (idle != null)
            {
                motion.Play(idle);
                motion.Sample();
                current = idle;
            }
            FitSuit(fit, renderers);
        }

        static void FitSuit(Transform model, SkinnedMeshRenderer[] renderers)
        {
            if (renderers.Length == 0) return;
            var bounds = new Bounds();
            bool first = true;
            var snapshot = new Mesh();
            foreach (var renderer in renderers)
            {
                renderer.updateWhenOffscreen = true;
                renderer.BakeMesh(snapshot);
                foreach (Vector3 vertex in snapshot.vertices)
                {
                    var point = model.InverseTransformPoint(renderer.transform.TransformPoint(vertex));
                    if (first)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        first = false;
                    }
                    else bounds.Encapsulate(point);
                }
            }
            Destroy(snapshot);
            if (first || bounds.size.y <= .01f) return;
            float scale = Height / bounds.size.y;
            model.localScale = Vector3.one * scale;
            model.localPosition = Vector3.down * (bounds.min.y * scale);
        }

        void DressSuit(SkinnedMeshRenderer[] renderers)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return;
            suitMaterials = new[]
            {
                Surface("Rescue enamel", new Color32(206, 89, 33, 255), .13f, .38f),
                Surface("Ceramic pressure shell", new Color32(203, 205, 188, 255), .08f, .37f),
                Surface("Graphite seals", new Color32(37, 45, 49, 255), .04f, .26f),
                Surface("Dark pressure fabric", new Color32(77, 92, 94, 255), .06f, .31f),
                Surface("Reflective smoked visor", new Color32(37, 62, 69, 255), .55f, .79f)
            };
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    string label = materials[i] != null ? materials[i].name : "";
                    materials[i] = label switch
                    {
                        "SciFi_Light_Accent" => suitMaterials[0],
                        "SciFi_Light" => suitMaterials[1],
                        "SciFi_MainDark" => suitMaterials[2],
                        "SciFi_Main" => suitMaterials[3],
                        "Grey" => suitMaterials[4],
                        _ => suitMaterials[1]
                    };
                }
                renderer.sharedMaterials = materials;
            }

            Material Surface(string name, Color color, float metal, float smoothness)
            {
                var material = new Material(shader) { name = name, color = color };
                material.SetFloat("_Metallic", metal);
                material.SetFloat("_Smoothness", smoothness);
                return material;
            }
        }

        string Register(AnimationClip clip, string label)
        {
            foreach (AnimationState state in motion)
            {
                if (state.clip != clip) continue;
                state.wrapMode = WrapMode.Loop;
                return state.name;
            }
            motion.AddClip(clip, label);
            motion[label].wrapMode = WrapMode.Loop;
            return label;
        }

        public void Animate(float speedFraction, bool carrying)
        {
            holding = carrying;
            if (motion == null || frozen) return;
            string next = speedFraction < .035f ? idle : speedFraction < .72f ? walk : run;
            if (next == null) return;
            if (next != current)
            {
                motion.CrossFade(next, .14f);
                current = next;
            }
            motion[next].speed = next == idle ? 1f : next == walk
                ? Mathf.Lerp(.65f, 1.45f, speedFraction / .72f)
                : Mathf.Lerp(.8f, 1.15f, (speedFraction - .72f) / .28f);
        }

        public void SetFrozen(bool freeze)
        {
            frozen = freeze;
            if (motion == null) return;
            foreach (AnimationState state in motion)
                state.speed = freeze ? 0f : 1f;
        }

        void LateUpdate()
        {
            if (!holding || frozen) return;
            Grip(leftUpper, leftLower, leftWrist, CarrySocket.TransformPoint(new Vector3(-.19f, -.05f, -.015f)));
            Grip(rightUpper, rightLower, rightWrist, CarrySocket.TransformPoint(new Vector3(.19f, -.05f, -.015f)));
        }

        static void Grip(Transform upper, Transform lower, Transform wrist, Vector3 target)
        {
            if (upper == null || lower == null || wrist == null) return;
            for (int i = 0; i < 4; i++)
            {
                lower.rotation = Quaternion.FromToRotation(wrist.position - lower.position, target - lower.position) *
                    lower.rotation;
                upper.rotation = Quaternion.FromToRotation(wrist.position - upper.position, target - upper.position) *
                    upper.rotation;
            }
        }

        void OnDestroy()
        {
            if (suitMaterials == null) return;
            foreach (var material in suitMaterials) Destroy(material);
        }
    }
}
