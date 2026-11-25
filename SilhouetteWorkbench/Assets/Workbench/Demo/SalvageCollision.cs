using UnityEngine;

namespace Workbench.Demo
{
    // Rendering swaps MeshFilter.sharedMesh as the budget changes. Give each
    // assembly a fixed collider derived from its reference geometry instead.
    public static class SalvageCollision
    {
        public static void Install(WorkbenchController controller)
        {
            if (controller == null) return;

            foreach (var part in controller.PrepareEvaluation())
            {
                if (part.TryGetComponent<Collider>(out _)) continue;

                Bounds bounds = part.MeshAt(part.FinestLevel).bounds;
                switch (part.Id)
                {
                    case "crane":
                        // The boom and hook clear the deck. Only the tower and its footing block a walker.
                        bounds = new Bounds(new Vector3(0, 3.7f, 0), new Vector3(2.1f, 7.4f, 2.1f));
                        break;
                    case "hull_relay":
                    case "exit_relay":
                        // The dish overhangs its mast. Use the plinth at ankle height and the mast above it.
                        AddBox(part, new Bounds(new Vector3(0, .09f, 0), new Vector3(1.2f, .18f, 1.05f)));
                        bounds = new Bounds(new Vector3(0, .91f, 0), new Vector3(.3f, 1.52f, .3f));
                        break;
                }
                AddBox(part, bounds);
            }
        }

        static void AddBox(PartLods part, Bounds bounds)
        {
            var collider = part.gameObject.AddComponent<BoxCollider>();
            collider.center = bounds.center;
            collider.size = bounds.size;
        }
    }
}
