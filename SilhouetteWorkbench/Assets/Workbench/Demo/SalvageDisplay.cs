using UnityEngine;

namespace Workbench.Demo
{
    static class SalvageDisplay
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void OpenLaptopWindow()
        {
            if (Application.platform != RuntimePlatform.LinuxPlayer || Application.productName != "Orbital Salvage")
                return;

            // Use the display hosting the window and leave room for its frame and the desktop panel.
            var display = Screen.mainWindowDisplayInfo;
            int width = display.width > 0 ? display.width : Screen.currentResolution.width;
            int height = display.height > 0 ? display.height : Screen.currentResolution.height;
            int units = Mathf.Min(80, width / 16, (height - 48) / 9);
            if (units > 0)
                Screen.SetResolution(16 * units, 9 * units, FullScreenMode.Windowed);
        }
    }
}
