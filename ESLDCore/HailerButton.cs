using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSP.UI.Screens;
using ToolbarControl_NS;

namespace ESLDCore
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	public class HailerButton : MonoBehaviour
	{
		public static HailerButton Instance;
        //public ApplicationLauncherButton button;
        internal ToolbarControl toolbarControl;

		private Vessel vessel;

		private ESLDHailer hailer;

		public bool canHail = false;
       // private Texture2D ESLDButtonOn = new Texture2D(38, 38, TextureFormat.ARGB32, false);
		private FlightCamera mainCam = null;

		private bool isDazzling = false;

		private float currentFOV = 60f;

		private float userFOV = 60f;

		private float currentDistance = 1f;

		private float userDistance = 1f;

		private Logger log = new Logger("ESLDCore:HailerButton: ");

		public void Update()
		{
			if (isDazzling)
			{
				currentFOV = Mathf.Lerp(currentFOV, userFOV, 0.04f);
				currentDistance = Mathf.Lerp(currentDistance, userDistance, 0.04f);
				mainCam.SetFoV(currentFOV);
				mainCam.SetDistance(currentDistance);
                //log.debug("Distance: " + currentDistance);
                if (userFOV + 0.25 >= currentFOV)
				{
					mainCam.SetFoV(userFOV);
					mainCam.SetDistance(userDistance);
					log.Debug("Done messing with camera!", null);
					isDazzling = false;
				}
			}
		}

		public void Awake()
		{
            if (Instance != null)
                Destroy(Instance);
			Instance = this;
			GameEvents.onGameSceneLoadRequested.Add(OnSceneChangeRequest);
			GameEvents.onVesselChange.Add(OnVesselChange);
			GameEvents.onGUIApplicationLauncherDestroyed.Add(KillButton);
            //ESLDButtonOn = GameDatabase.Instance.GetTexture("ESLDBeacons/Textures/launcher", false);
            //GameEvents.onGameSceneLoadRequested.Add(OnGameSceneLoadRequestedForAppLauncher);
			mainCam = FlightCamera.fetch;
			InitializeButton();
		}

		public void OnDestroy()
		{
           // GameEvents.onGUIApplicationLauncherReady.Remove(OnGUIApplicationLauncherReady);
			GameEvents.onGameSceneLoadRequested.Remove(OnSceneChangeRequest);
			GameEvents.onVesselChange.Remove(OnVesselChange);
			GameEvents.onGUIApplicationLauncherDestroyed.Remove(KillButton);
			KillButton();
            //GameEvents.onGameSceneLoadRequested.Remove(OnGameSceneLoadRequestedForAppLauncher);
		}

		private void OnTrue()
		{
			OnVesselChange(FlightGlobals.ActiveVessel);
			HailerGUI.ActivateGUI(FlightGlobals.ActiveVessel);
		}

		private void OnFalse()
            => HailerGUI.CloseAllGUIs();


        internal const string MODID = "esld_NS";
        internal const string MODNAME = "ESLDBeacon";
        private void InitializeButton()
		{
#if false
            if (this.button != null)
			{
				KillButton();
			}
			if (canHail)
			{
                this.button = ApplicationLauncher.Instance.AddModApplication(
                    this.OnTrue,
                    this.OnFalse,
                    null,
                    null,
                    null,
                    null,
                    ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.MAPVIEW,
                    ESLDButtonOn);
            }
#endif
            if (toolbarControl == null)
            {
                toolbarControl = gameObject.AddComponent<ToolbarControl>();
                toolbarControl.AddToAllToolbars(this.OnTrue,
                        this.OnFalse,
                   ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.MAPVIEW,
                    MODID,
                    "esldButton",
                    "ESLDBeacons/PluginData/Textures/launcher-38",
                    "ESLDBeacons/PluginData/Textures/launcher-24",
                    MODNAME
                );
			}
		}

		public void OnSceneChangeRequest(GameScenes _scene)
            => KillButton();

		public void OnVesselChange(Vessel vessel)
		{
			HailerGUI.CloseGUI(this.vessel);
			this.vessel = vessel;
			hailer = vessel?.FindPartModulesImplementing<ESLDHailer>().FirstOrDefault();

            canHail = hailer != null;
#if false
            if (canHail && button == null)
				OnGUIApplicationLauncherReady();
            else if (!canHail && button != null)
				KillButton();
#endif
            toolbarControl.Enabled = canHail;
		}

		private void KillButton()
		{
			HailerGUI.CloseAllGUIs();
            toolbarControl.OnDestroy();
            Destroy(toolbarControl);
#if false
            if (button != null && ApplicationLauncher.Instance != null)
			{
				ApplicationLauncher.Instance.RemoveModApplication(button);
				button = null;
			}
#endif
		}

        void OnGameSceneLoadRequestedForAppLauncher(GameScenes SceneToLoad)
            => KillButton();

        // Warp Effect
		public void Dazzle()
		{
			userFOV = mainCam.FieldOfView;
			userDistance = mainCam.Distance;
			currentFOV = 180f;
			currentDistance = 0.1f;
			isDazzling = true;
			log.Debug("Messing with camera!", null);
		}
	}
}
