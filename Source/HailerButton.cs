using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
//using System.Threading.Tasks;
using UnityEngine;
using KSP.UI.Screens;

namespace ESLDCore
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class HailerButton : MonoBehaviour
    {
        private ApplicationLauncherButton button;
        private Vessel vessel;
        private ESLDHailer hailer;
        private bool canHail = false;
        private Texture2D ESLDButtonOn = new Texture2D(38, 38, TextureFormat.ARGB32, false);
        public FlightCamera mainCam = null;
        public bool isDazzling = false;
        private float currentFOV = 60;
        private float userFOV = 60;
        private float currentDistance = 1;
        private float userDistance = 1;
        Logger log = new Logger("ESLDCore:HailerButton: ");

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
                    log.Debug("Done messing with camera!");
                    isDazzling = false;
                }
            }
            if (FlightGlobals.ActiveVessel != null) // Grab active vessel.
            {
                vessel = FlightGlobals.ActiveVessel;
                if (vessel.FindPartModulesImplementing<ESLDHailer>().Count == 0) // Has a hailer?
                {
                    canHail = false;
                    hailer = null;
                }
                else
                {
                    canHail = true;
                    hailer = vessel.FindPartModulesImplementing<ESLDHailer>().First();
                    foreach (ESLDHailer ehail in vessel.FindPartModulesImplementing<ESLDHailer>())
                    {
                        ehail.hailerButton = this;
                    }
                }
            }
            if (canHail && this.button == null)
            {
                OnGUIApplicationLauncherReady();
            }
            if (!canHail && this.button != null)
            {
                KillButton();
            }
            // Sync GUI & Button States
            if (this.button != null)
            {
                if (this.button.toggleButton.CurrentState == KSP.UI.UIRadioButton.State.True && !hailer.guiopen)
                {
                    this.button.SetFalse();
                }
                if (this.button.toggleButton.CurrentState == KSP.UI.UIRadioButton.State.False && hailer.guiopen)
                {
                    this.button.SetTrue();
                }
            }
        }

        public void Awake()
        {
            //GameEvents.onGUIApplicationLauncherReady.Add(onGUIApplicationLauncherReady);
            GameEvents.onGameSceneLoadRequested.Add(OnSceneChangeRequest);
            GameEvents.onVesselChange.Add(OnVesselChange);
            GameEvents.onGUIApplicationLauncherDestroyed.Add(KillButton);
            ESLDButtonOn = GameDatabase.Instance.GetTexture("ESLDBeacons/Textures/launcher", false);
            GameEvents.onGameSceneLoadRequested.Add(OnGameSceneLoadRequestedForAppLauncher);
            mainCam = FlightCamera.fetch;
        }

        public void OnDestroy()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(OnGUIApplicationLauncherReady);
            GameEvents.onGameSceneLoadRequested.Remove(OnSceneChangeRequest);
            GameEvents.onVesselChange.Remove(OnVesselChange);
            GameEvents.onGUIApplicationLauncherDestroyed.Remove(KillButton);
            KillButton();
            GameEvents.onGameSceneLoadRequested.Remove(OnGameSceneLoadRequestedForAppLauncher);
        }

        private void OnTrue()
        {
            if (hailer != null)
            {
                hailer.guiopen = true;
                hailer.HailerActivate();
                hailer.HailerGUIOpen();
            }
        }

        private void OnFalse()
        {
            if (hailer != null)
            {
                hailer.HailerDeactivate();
            }
        }


        private void OnGUIApplicationLauncherReady()
        {
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
        }

        public void OnSceneChangeRequest(GameScenes _scene)
        {
            KillButton();
        }

        public void OnVesselChange(Vessel _vessel)
        {
            KillButton();
        }

        private void KillButton()
        {
            if (button != null && ApplicationLauncher.Instance != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(button);
                button = null;
            }
        }

        void OnGameSceneLoadRequestedForAppLauncher(GameScenes SceneToLoad)
        {
            KillButton();
        }

        // Warp Effect
        public void Dazzle()
        {
            userFOV = mainCam.FieldOfView;
            userDistance = mainCam.Distance;
            currentFOV = 180;
            currentDistance = 0.1f;
            isDazzling = true;
            log.Debug("Messing with camera!");
        }
    }

}
