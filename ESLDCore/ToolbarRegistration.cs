using UnityEngine;
using ToolbarControl_NS;


namespace ESLDCore
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class RegisterToolbar : MonoBehaviour
    {
        void Start()
        {
            ToolbarControl.RegisterMod(HailerButton.MODID, HailerButton.MODNAME);
        }
    }
}