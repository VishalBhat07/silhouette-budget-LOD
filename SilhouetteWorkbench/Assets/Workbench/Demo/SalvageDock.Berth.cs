using UnityEngine;
using UnityEngine.Rendering;

namespace Workbench.Demo
{
    public sealed partial class SalvageDock
    {
        Material shuttleGlass;
        BerthRig berth;
        public Transform BoardingPort => berth?.BoardingPort;

        void BuildShuttleBerth()
        {
            var dark = Surface("Shuttle engine metal", new Color32(44, 54, 60, 255), .62f);
            shuttleGlass = Glass("Shuttle canopy glass", new Color32(126, 176, 190, 255));
            var palette = new BerthPalette
            {
                Deck = deck, Steel = steel, Pale = pale, Orange = orange, Glow = ice, Dark = dark,
                Shuttle = new ShuttlePalette
                {
                    Ceramic = pale, Metal = steel, Dark = dark, Glass = shuttleGlass,
                    Stripe = orange, Glow = ice, ConsoleGlow = signal, Seat = dark
                }
            };
            berth = SalvageBerth.Build(scenery.transform, palette, true, owned);
            Lamp("Berth approach light", new Vector3(-3.4f, 3.1f, 31.4f), new Color32(198, 226, 255, 255), 7f, 3.6f);
        }

        Material Glass(string label, Color colour)
        {
            colour.a = .15f;
            var material = new Material(lit) { name = label, color = colour };
            material.SetFloat("_Metallic", .1f);
            material.SetFloat("_Smoothness", .92f);
            material.SetFloat("_Surface", 1);
            material.SetFloat("_Blend", 0);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0);
            material.SetFloat("_Cull", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetOverrideTag("RenderType", "Transparent");
            owned.Add(material);
            return material;
        }
    }
}
