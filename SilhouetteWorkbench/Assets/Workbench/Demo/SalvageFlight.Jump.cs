using UnityEngine;

namespace Workbench.Demo
{
    public sealed partial class SalvageFlight
    {
        static readonly int JumpClockProperty = Shader.PropertyToID("_JumpClock");
        static readonly int JumpEnergyProperty = Shader.PropertyToID("_JumpEnergy");

        static readonly Color IncomingLight = new Color32(100, 188, 255, 255);
        static readonly Color OutgoingLight = new Color32(255, 203, 154, 255);

        Light hullSweep;
        Light cabinSweep;
        bool animateVista;

        void BuildJumpEffects()
        {
            animateVista = vista.HasProperty(JumpClockProperty) && vista.HasProperty(JumpEnergyProperty);
            hullSweep = Lamp(Shuttle.Root, "Aperture light across hull", new Vector3(-2.7f, 1.55f, 4.8f),
                IncomingLight, 11f, 0);
            cabinSweep = Lamp(Shuttle.Root, "Aperture light in cabin", new Vector3(.12f, .59f, 2.83f),
                IncomingLight, 4.6f, 0);
        }

        void ApplyJumpEffects(float time)
        {
            float arrival = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(3.24f, 4.0f, time));
            float departure = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(4.04f, 4.7f, time));
            float energy = arrival * (1 - departure);
            float sweep = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(3.3f, 4.56f, time));
            float tint = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(4.02f, 4.53f, time));
            Color colour = Color.Lerp(IncomingLight, OutgoingLight, tint);

            // Flight time is explicit so scrubbing and switching cameras produce the same lighting.
            if (animateVista)
            {
                vista.SetFloat(JumpClockProperty, time);
                vista.SetFloat(JumpEnergyProperty, energy);
            }
            hullSweep.transform.localPosition = new Vector3(-2.7f, 1.55f, Mathf.Lerp(4.8f, -4.5f, sweep));
            cabinSweep.transform.localPosition = new Vector3(Mathf.Lerp(-.55f, .55f, sweep), .59f, 2.83f);
            Illuminate(hullSweep, colour, energy * 6);
            Illuminate(cabinSweep, colour, energy * 2.8f);
        }

        static void Illuminate(Light light, Color colour, float intensity)
        {
            light.color = colour;
            light.intensity = intensity;
            light.enabled = intensity > .01f;
        }
    }
}
