using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ESLDCore
{
    public class HailerGUI : MonoBehaviour
    {
        public enum DisplayMode
        {
            Selection,
            Confirmation
        }

        private class TargetDetails
        {
            public string targetSOI = "";

            public float targetAlt = 0f;

            public readonly Vessel targetVessel;

            public readonly string vesselName;

            public float tripCost = 0f;

            private readonly HailerGUI hailerGUI;

            public bool affordable = false;

            public PathCheck pathCheck;

            public float precision = 0f;

            public float returnCost = 0f;

            public ProtoBeacon returnBeacon = null;

            public bool returnFuelCheck = false;

            public bool returnAffordable = false;

            public TargetDetails(Vessel target, HailerGUI hailerGUI)
            {
                this.hailerGUI = hailerGUI;
                targetVessel = target;
                vesselName = target.vesselName;
                if (hailerGUI.nearBeacon != null)
                {
                    pathCheck = new PathCheck(hailerGUI.nearBeacon.Vessel, targetVessel, hailerGUI.nearBeacon.PathGLimit);
                }
                else
                {
                    pathCheck = new PathCheck(null, targetVessel, 0f);
                }
                Update();
            }

            public void Update()
            {
                targetSOI = targetVessel.mainBody.name;
                targetAlt = (float)targetVessel.altitude / 1000f;
                if (hailerGUI.nearBeacon != null)
                {
                    pathCheck = new PathCheck(hailerGUI.nearBeacon.Vessel, targetVessel, hailerGUI.nearBeacon.PathGLimit);
                    float tripdist = Vector3.Distance(hailerGUI.nearBeacon.Vessel.GetWorldPos3D(), targetVessel.GetWorldPos3D());
                    tripCost = hailerGUI.nearBeacon.GetTripBaseCost(tripdist, hailerGUI.tonnage);
                    tripCost = hailerGUI.nearBeacon.GetTripFinalCost(tripCost, hailerGUI.vessel, targetVessel, hailerGUI.tonnage, hailerGUI.HCUPartsList);
                    affordable = true;
                    int i = hailerGUI.nearBeacon.JumpResources.Count - 1;
                    while (i >= 0)
                    {
                        if (hailerGUI.nearBeacon.RequireResource(hailerGUI.nearBeacon.JumpResources[i].resID, (double)(tripCost * hailerGUI.nearBeacon.JumpResources[i].ratio), false))
                        {
                            i--;
                            continue;
                        }
                        affordable = false;
                        break;
                    }
                    returnCost = float.PositiveInfinity;
                    returnAffordable = false;
                    if (farBeaconVessels.ContainsKey(targetVessel))
                    {
                        returnFuelCheck = farBeaconVessels[targetVessel].Any((ProtoBeacon pb) => pb.JumpResources.All((ESLDJumpResource res) => res.fuelCheck));
                        precision = 3.40282347E+38f;
                        for (int i2 = farBeaconVessels[targetVessel].Count - 1; i2 >= 0; i2--)
                        {
                            precision = Math.Min(farBeaconVessels[targetVessel][i2].GetTripSpread(tripdist), precision);
                            float possibleReturnCost = farBeaconVessels[targetVessel][i2].GetTripBaseCost(tripdist, hailerGUI.tonnage);
                            bool flag = false;
                            if (returnFuelCheck)
                            {
                                flag = farBeaconVessels[targetVessel][i2].JumpResources.All((ESLDJumpResource res) => res.fuelCheck && (double)(res.ratio * possibleReturnCost) <= res.fuelOnBoard);
                                if (flag)
                                {
                                    returnAffordable = true;
                                }
                            }
                            if (possibleReturnCost < returnCost && ((!returnFuelCheck || !returnAffordable) | flag))
                            {
                                returnCost = possibleReturnCost;
                                returnBeacon = farBeaconVessels[targetVessel][i2];
                            }
                        }
                    }
                    else
                    {
                        returnFuelCheck = false;
                        precision = hailerGUI.nearBeacon.GetTripSpread(tripdist);
                    }
                }
                else
                {
                    pathCheck = new PathCheck(null, targetVessel, 0f);
                }
            }
        }

        public struct PathCheck
        {
            public bool clear;

            public string blockReason;

            public CelestialBody blockingBody;

            public PathCheck(Vessel pointA, Vessel pointB, float gLimit)
            {
                blockingBody = null;
                if ((UnityEngine.Object)pointA == (UnityEngine.Object)null || (UnityEngine.Object)pointB == (UnityEngine.Object)null)
                {
                    clear = false;
                    blockReason = "Null";
                }
                else
                {
                    clear = true;
                    blockReason = "Ok!";
                    Vector3d worldPos3D = pointA.GetWorldPos3D();
                    Vector3d worldPos3D2 = pointB.GetWorldPos3D();
                    int count = FlightGlobals.Bodies.Count;
                    for (int i = 0; i < count; i++)
                    {
                        CelestialBody celestialBody = FlightGlobals.Bodies[i];
                        Vector3d vector3d = celestialBody.position - worldPos3D;
                        Vector3d rhs = worldPos3D2 - worldPos3D;
                        if (!(Vector3d.Dot(vector3d, rhs) <= 0.0))
                        {
                            Vector3d normalized = rhs.normalized;
                            if (!(Vector3d.Dot(vector3d, normalized) >= rhs.magnitude))
                            {
                                Vector3d vector3d2 = vector3d - Vector3d.Dot(vector3d, normalized) * normalized;
                                double limbo = Math.Sqrt(6.673E-11 * celestialBody.Mass / (double)gLimit) - celestialBody.Radius;
                                string limbotype = "Gravity";
                                if (limbo < celestialBody.Radius + celestialBody.Radius * 0.25)
                                {
                                    limbo = celestialBody.Radius + celestialBody.Radius * 0.025;
                                    limbotype = "Proximity";
                                }
                                if (vector3d2.magnitude < limbo)
                                {
                                    blockingBody = celestialBody;
                                    blockReason = limbotype;
                                    clear = false;
                                    return;
                                }
                            }
                        }
                    }
                    if (FlightGlobals.getGeeForceAtPosition(pointB.GetWorldPos3D()).magnitude > (double)gLimit)
                    {
                        blockingBody = pointB.mainBody;
                        blockReason = "Gravity";
                        clear = false;
                    }
                }
            }
        }

        public static Dictionary<string, float> highEnergyResources = new Dictionary<string, float>
        {
            {
                "Karborundum",
                1f
            },
            {
                "Uraninite",
                0.8f
            },
            {
                "DepletedUranium",
                0.3f
            },
            {
                "EnrichedUranium",
                0.7f
            },
            {
                "Actinides",
                0.6f
            },
            {
                "Antimatter",
                1f
            },
            {
                "ChargedParticles",
                1f
            },
            {
                "DepletedFuel",
                0.3f
            },
            {
                "ExoticMatter",
                1f
            },
            {
                "Fluorine",
                0.4f
            },
            {
                "LqdHe3",
                0.6f
            },
            {
                "LqdTritium",
                0.8f
            },
            {
                "Plutonium-238",
                0.8f
            },
            {
                "ThF4",
                1f
            },
            {
                "UraniumNitride",
                0.65f
            },
            {
                "VacuumPlasma",
                1f
            },
            {
                "Uranium",
                0.7f
            },
            {
                "Thorium",
                0.6f
            }
        };

        public static List<HailerGUI> openWindows = new List<HailerGUI>();

        public Vessel vessel = null;

        public IBeacon nearBeacon;

        public List<ESLDHailer> hailers = new List<ESLDHailer>();

        private List<IBeacon> nearBeacons = new List<IBeacon>();

        private static Dictionary<Vessel, List<ProtoBeacon>> farBeaconVessels = new Dictionary<Vessel, List<ProtoBeacon>>();

        private static List<ProtoBeacon> farBeacons = new List<ProtoBeacon>();

        private TargetDetails selectedTarget;

        private int currentBeaconIndex = -1;

        protected Rect window;

        public GameObject predictionGameObject = null;

        public OrbitDriver predictionOrbitDriver = null;

        public OrbitRenderer predictionOrbitRenderer = null;

        private PatchedConicSolver predictionPatchedConicSolver;

        private PatchedConicRenderer predictionPatchedConicRenderer;

        private bool predictionsDrawn = false;

        public bool wasInMapView;

        public static GUIStyle buttonNeutral;

        public static GUIStyle labelHasFuel;

        public static GUIStyle labelNoFuel;

        public static GUIStyle buttonHasFuel;

        public static GUIStyle buttonNoFuel;

        public static GUIStyle buttonNoPath;

        public DisplayMode displayMode = DisplayMode.Selection;

        private float driftpenalty;

        private List<TargetDetails> targetDetails = new List<TargetDetails>();

        private float tonnage;

        private float sciBonus;

        private Dictionary<Part, string> HCUParts;

        private List<Part> HCUPartsList = new List<Part>();

        private List<string> HCUPartFailures = new List<string>();

        public static bool IsPatchedConicsAvailable
        {
            get
            {
                float facilityLevel = ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.TrackingStation);
                return GameVariables.Instance.GetOrbitDisplayMode(facilityLevel).CompareTo(GameVariables.OrbitDisplayMode.PatchedConics) >= 0;
            }
        }

        public void OnGUI()
        {
            SetupStyles();
            if (window.x == 0f && window.y == 0f)
            {
                window = new Rect((float)(Screen.width / 2), (float)(Screen.height / 2), 10f, 10f);
            }
            switch (displayMode)
            {
                case DisplayMode.Selection:
                    window = GUILayout.Window(base.GetInstanceID(), window, WindowInterface, "Warp Jump Information", HighLogic.Skin.window, GUILayout.MinWidth(400f), GUILayout.MinHeight(200f));
                    break;
                case DisplayMode.Confirmation:
                    window = GUILayout.Window(base.GetInstanceID(), window, WindowInterface, "Warp Jump Information", HighLogic.Skin.window, GUILayout.MinWidth(400f), GUILayout.MinHeight(200f));
                    break;
            }
        }

        public void WindowInterface(int GuiId)
        {
            GUILayout.BeginVertical();
            switch (displayMode)
            {
                case DisplayMode.Selection:
                    if (farBeacons.Count <= 0 || nearBeacons.Count <= 0 || nearBeacon == null)
                    {
                        GUILayout.Label("No active beacons found.");
                    }
                    else
                    {
                        if (driftpenalty > 0f)
                        {
                            GUILayout.Label($"+{driftpenalty:F2}% due to Drift.");
                        }
                        if (nearBeacon.UnsafeTransfer)
                        {
                            if (vessel.GetCrewCount() > 0 || HCUPartFailures.Count > 0)
                            {
                                GUILayout.Label("WARNING: This beacon has no active Heisenkerb Compensator.");
                            }
                            if (vessel.GetCrewCount() > 0)
                            {
                                GUILayout.Label("Transfer will kill crew.");
                            }
                            if (HCUPartFailures.Count > 0)
                            {
                                GUILayout.Label("Some resources will destabilize.");
                            }
                        }
                        int i;
                        for (i = targetDetails.Count - 1; i >= 0; i--)
                        {
                            GUIStyle style = targetDetails[i].affordable ? (targetDetails[i].pathCheck.clear ? buttonHasFuel : buttonNoPath) : buttonNoFuel;
                            if (GUILayout.Button($"{targetDetails[i].vesselName} ({targetDetails[i].targetSOI}, {targetDetails[i].targetAlt:F1}km) | {targetDetails[i].tripCost:F2}", style))
                            {
                                if (targetDetails[i].affordable && targetDetails[i].pathCheck.clear)
                                {
                                    selectedTarget = targetDetails[i];
                                    if (nearBeacon.CarriesVelocity)
                                    {
                                        ShowExitOrbit(vessel, selectedTarget.targetVessel);
                                    }
                                    displayMode = DisplayMode.Confirmation;
                                }
                                else
                                {
                                    string message = "";
                                    if (!targetDetails[i].affordable)
                                    {
                                        int index = nearBeacon.JumpResources.FindIndex((ESLDJumpResource res) => (double)(res.ratio * targetDetails[i].tripCost) > res.fuelOnBoard);
                                        message = ((index >= 0) ? $"Cannot Warp: Origin beacon has {nearBeacon.JumpResources[index].fuelOnBoard:F2} of {nearBeacon.JumpResources[index].ratio * targetDetails[i].tripCost:F2} {nearBeacon.JumpResources[index].name} required to warp." : "Index error.");
                                    }
                                    else if (!targetDetails[i].pathCheck.clear)
                                    {
                                        string arg = (targetDetails[i].pathCheck.blockingBody.name == "Mun" || targetDetails[i].pathCheck.blockingBody.name == "Sun") ? "the " : "";
                                        switch (targetDetails[i].pathCheck.blockReason)
                                        {
                                            case "Gravity":
                                                message = string.Format("Cannot Warp: Path of transfer intersects a high-gravity area around {1}{0}.", targetDetails[i].pathCheck.blockingBody.name, arg);
                                                break;
                                            case "Proximity":
                                                message = string.Format("Cannot Warp: Path of transfer passes too close to {1}{0}.", targetDetails[i].pathCheck.blockingBody.name, arg);
                                                break;
                                            case "Null":
                                                message = "Cannot Warp: No near beacon assigned. This is an error.";
                                                break;
                                            default:
                                                message = "Cannot Warp: Path is blocked.";
                                                break;
                                        }
                                    }
                                    ScreenMessages.PostScreenMessage(message, 5f, ScreenMessageStyle.UPPER_CENTER);
                                }
                            }
                        }
                        GUILayout.FlexibleSpace();
                        if (nearBeacons.Count > 1)
                        {
                            GUILayout.Label($"Current Beacon: {nearBeacon.Description} ({nearBeacon.Vessel.vesselName})");
                            if (GUILayout.Button($"Next Beacon ({currentBeaconIndex + 1} of {nearBeacons.Count})", buttonNeutral))
                            {
                                if (currentBeaconIndex + 1 < nearBeacons.Count)
                                {
                                    nearBeacon = nearBeacons[currentBeaconIndex + 1];
                                }
                                else
                                {
                                    nearBeacon = nearBeacons[0];
                                }
                            }
                        }
                        if (GUILayout.Button("Close Beacon Interface", buttonNeutral))
                        {
                            UnityEngine.Object.Destroy(this);
                        }
                    }
                    break;
                case DisplayMode.Confirmation:
                    {
                        if (driftpenalty > 0f)
                        {
                            GUILayout.Label($"+{driftpenalty:F2}% due to Drift.");
                        }
                        if (nearBeacon.UnsafeTransfer)
                        {
                            if (vessel.GetCrewCount() > 0 || HCUPartFailures.Count > 0)
                            {
                                GUILayout.Label("WARNING: This beacon has no active Heisenkerb Compensator.", labelNoFuel);
                            }
                            if (vessel.GetCrewCount() > 0)
                            {
                                GUILayout.Label("Transfer will kill crew.", labelNoFuel);
                            }
                            if (HCUPartFailures.Count > 0)
                            {
                                GUILayout.Label("These resources will destabilize in transit:", labelNoFuel);
                                for (int j = 0; j < HCUPartFailures.Count; j++)
                                {
                                    GUILayout.Label(HCUPartFailures[j], labelNoFuel);
                                }
                            }
                        }
                        GUILayout.Label("Confirm Warp:");
                        string str = "Base cost: ";
                        for (int k = 0; k < nearBeacon.JumpResources.Count; k++)
                        {
                            str += string.Format("{0:F2} {1}{2}", selectedTarget.tripCost * nearBeacon.JumpResources[k].ratio, nearBeacon.JumpResources[k].name, (k + 1 < nearBeacon.JumpResources.Count) ? ", " : "");
                        }
                        GUILayout.Label(str + ".");
                        List<string> costModifiers = nearBeacon.GetCostModifiers(vessel, selectedTarget.targetVessel, tonnage, HCUParts.Keys.ToList());
                        for (int l = 0; l < costModifiers.Count; l++)
                        {
                            GUILayout.Label(costModifiers[l]);
                        }
                        GUILayout.Label($"Destination: {selectedTarget.targetVessel.mainBody.name} at {selectedTarget.targetAlt:F1}km.");
                        GUILayout.Label($"Transfer will emerge within {selectedTarget.precision:N0}m of destination beacon.");
                        if (selectedTarget.targetVessel.altitude - (double)selectedTarget.precision <= selectedTarget.targetVessel.mainBody.Radius * 0.10000000149011612 || selectedTarget.targetVessel.altitude - (double)selectedTarget.precision <= selectedTarget.targetVessel.mainBody.atmosphereDepth)
                        {
                            GUILayout.Label($"Arrival area is very close to {selectedTarget.targetVessel.mainBody.name}.", labelNoFuel);
                        }
                        if (nearBeacon.CarriesVelocity)
                        {
                            GUILayout.Label($"Velocity relative to exit beacon will be {(ESLDBeacon.GetJumpVelOffset(vessel, selectedTarget.targetVessel) - selectedTarget.targetVessel.orbit.vel).magnitude:F0}m/s.");
                        }
                        if (selectedTarget.returnFuelCheck && selectedTarget.returnBeacon != null)
                        {
                            string str2 = (!selectedTarget.returnAffordable) ? "Destination beacon would need " : "Destination beacon can make return trip using ";
                            string str3 = "Destination beacon has ";
                            for (int m = 0; m < selectedTarget.returnBeacon.JumpResources.Count; m++)
                            {
                                str3 += string.Format("{0:F0} {1}{2}", selectedTarget.returnBeacon.JumpResources[m].fuelOnBoard, selectedTarget.returnBeacon.JumpResources[m].name, (m + 1 < selectedTarget.returnBeacon.JumpResources.Count) ? ", " : "");
                            }
                            for (int n = 0; n < selectedTarget.returnBeacon.JumpResources.Count; n++)
                            {
                                str2 += string.Format("{0:F2} {1}{2}", selectedTarget.returnCost * selectedTarget.returnBeacon.JumpResources[n].ratio, selectedTarget.returnBeacon.JumpResources[n].name, (n + 1 < selectedTarget.returnBeacon.JumpResources.Count) ? ", " : "");
                            }
                            GUILayout.Label(str3 + ".");
                            GUILayout.Label(str2 + (selectedTarget.returnAffordable ? "." : " for return trip using active beacons."), selectedTarget.returnAffordable ? labelHasFuel : labelNoFuel);
                        }
                        else
                        {
                            GUILayout.Label("Destination beacon's fuel could not be checked.");
                        }
                        if (GUILayout.Button("Confirm and Warp", buttonNeutral))
                        {
                            if (selectedTarget.pathCheck.clear)
                            {
                                HideExitOrbit();
                                nearBeacon.Warp(vessel, selectedTarget.targetVessel, selectedTarget.precision, HCUPartsList);
                                UnityEngine.Object.Destroy(this);
                            }
                            else
                            {
                                ScreenMessages.PostScreenMessage("Jump Failed!  Transfer path has become obstructed.", 5f, ScreenMessageStyle.UPPER_CENTER);
                            }
                        }
                        if (GUILayout.Button("Back", buttonNeutral))
                        {
                            displayMode = DisplayMode.Selection;
                            window.height = 0f;
                        }
                        break;
                    }
            }
            GUILayout.EndVertical();
            GUI.DragWindow(/* new Rect(0f, 0f, 10000f, 20f) */);
        }

        public void Update()
        {
            UpdateNearBeacons();
            if (nearBeacon == null || (UnityEngine.Object)nearBeacon.Vessel != (UnityEngine.Object)vessel)
            {
                bool flag = nearBeacon != null;
                for (int i = hailers.Count - 1; i >= 0; i--)
                {
                    bool hailerActive = hailers[i].hailerActive;
                    ((BaseFieldList<BaseField, KSPField>)hailers[i].Fields)["nearBeaconDistance"].guiActive = (flag & hailerActive);
                    ((BaseFieldList<BaseField, KSPField>)hailers[i].Fields)["nearBeaconRelVel"].guiActive = (flag & hailerActive);
                    ((BaseFieldList<BaseField, KSPField>)hailers[i].Fields)["hasNearBeacon"].guiActive = hailerActive;
                    hailers[i].hasNearBeacon = (flag ? "Present" : "Not Present");
                }
            }
            else
            {
                for (int i = hailers.Count - 1; i >= 0; i--)
                {
                    ((BaseFieldList<BaseField, KSPField>)hailers[i].Fields)["nearBeaconDistance"].guiActive = false;
                    ((BaseFieldList<BaseField, KSPField>)hailers[i].Fields)["nearBeaconRelVel"].guiActive = false;
                    ((BaseFieldList<BaseField, KSPField>)hailers[i].Fields)["hasNearBeacon"].guiActive = hailers[i].hailerActive;
                    hailers[i].hasNearBeacon = "Onboard";
                }
            }
            if (nearBeacon != null)
            {
                float nearBeaconDistance = Vector3.Distance(vessel.GetWorldPos3D(), nearBeacon.Vessel.GetWorldPos3D());
                float nearBeaconRelVel = Vector3.Magnitude(vessel.obt_velocity - nearBeacon.Vessel.obt_velocity);
                driftpenalty = ESLDBeacon.GetDriftPenalty(nearBeaconDistance, nearBeaconRelVel, nearBeacon.GetCrewBonuses("Pilot", 0.5f, 5));
                HCUParts = GetHCUParts(vessel);
                //[Part name] - [Resource name]
                HCUPartFailures.Clear();
                HCUPartFailures.AddRange(from kvp in HCUParts
                                         select kvp.Key.name + " - " + kvp.Value);
                HCUPartsList = HCUParts.Keys.ToList();
                tonnage = vessel.GetTotalMass();
                sciBonus = nearBeacon.GetCrewBonuses("Scientist", 0.5f, 5);
                for (int i = targetDetails.Count - 1; i >= 0; i--)
                {
                    targetDetails[i].Update();
                }
                for (int i = hailers.Count - 1; i >= 0; i--)
                {
                    hailers[i].nearBeaconDistance = (double)nearBeaconDistance;
                    hailers[i].nearBeaconRelVel = (double)nearBeaconRelVel;
                }
            }
        }

        public void UpdateNearBeacons()
        {
            nearBeacons.Clear();
            for (int i = FlightGlobals.VesselsLoaded.Count - 1; i >= 0; i--)
            {
                List<ESLDBeacon> list = FlightGlobals.VesselsLoaded[i].FindPartModulesImplementing<ESLDBeacon>();
                for (int j = list.Count - 1; j >= 0; j--)
                {
                    if (list[j].activated && list[j].moduleIsEnabled && ((UnityEngine.Object)FlightGlobals.VesselsLoaded[i] != (UnityEngine.Object)vessel || list[j].canJumpSelf))
                    {
                        nearBeacons.Add(list[j]);
                    }
                }
            }
            if (nearBeacons.Count == 0)
            {
                nearBeacon = null;
                currentBeaconIndex = -1;
                displayMode = DisplayMode.Selection;
            }
            else
            {
                nearBeacons.OrderBy(b => Vector3.SqrMagnitude(b.Vessel.GetWorldPos3D() - vessel.GetWorldPos3D()));

                currentBeaconIndex = nearBeacons.IndexOf(nearBeacon);
                if (currentBeaconIndex < 0)
                {
                    nearBeacon = nearBeacons[0];
                    currentBeaconIndex = 0;
                    displayMode = DisplayMode.Selection;
                }
            }
        }

        public static List<ProtoBeacon> QueryVesselFarBeacons(ProtoVessel vesselToQuery)
        {
            List<ProtoBeacon> list = new List<ProtoBeacon>();
            List<ProtoPartSnapshot> protoPartSnapshots = vesselToQuery.protoPartSnapshots;
            int count = protoPartSnapshots.Count;
            for (int i = 0; i < count; i++)
            {
                List<ProtoPartModuleSnapshot> list2 = protoPartSnapshots[i].modules.FindAll((ProtoPartModuleSnapshot m) => m.moduleName == "ESLDBeacon");
                int count2 = list2.Count;
                for (int j = 0; j < count2; j++)
                {
                    ProtoBeacon protoBeacon = new ProtoBeacon(list2[j].moduleValues, protoPartSnapshots[i].partInfo.partConfig.GetNodes("MODULE", "name", "ESLDBeacon")[j]);
                    protoBeacon.Vessel = vesselToQuery.vesselRef;
                    if (protoBeacon.activated && protoBeacon.moduleIsEnabled && protoBeacon.jumpTargetable)
                    {
                        list.Add(protoBeacon);
                    }
                }
            }
            return list;
        }

        public static HailerGUI ActivateGUI(Vessel hostVessel)
        {
            HailerGUI hailerGUI = openWindows.FirstOrDefault((HailerGUI gui) => (UnityEngine.Object)gui.vessel == (UnityEngine.Object)hostVessel);
            if ((UnityEngine.Object)hailerGUI != (UnityEngine.Object)null)
            {
                return hailerGUI;
            }
            hailerGUI = hostVessel.gameObject.AddComponent<HailerGUI>();
            hailerGUI.vessel = hostVessel;
            return hailerGUI;
        }

        public static void CloseGUI(Vessel hostVessel)
        {
            HailerGUI hailerGUI = openWindows.FirstOrDefault((HailerGUI gui) => (UnityEngine.Object)gui.vessel == (UnityEngine.Object)hostVessel);
            if ((UnityEngine.Object)hailerGUI != (UnityEngine.Object)null)
            {
                UnityEngine.Object.Destroy(hailerGUI);
            }
        }

        public static void CloseAllGUIs()
        {
            for (int i = openWindows.Count - 1; i >= 0; i--)
                Destroy(openWindows[i]);
        }

        public void Awake()
        {
            openWindows.Add(this);
            GameEvents.onVesselWasModified.Add(VesselWasModified);
            GameEvents.onVesselGoOffRails.Add(VesselOffRails);
            GameEvents.onVesselGoOnRails.Add(VesselOnRails);
            GameEvents.onNewVesselCreated.Add(VesselCreated);
            GameEvents.onVesselCreate.Add(VesselCreated);
            GameEvents.onVesselWillDestroy.Add(VesselDestroyed);
        }

        public void Start()
        {
            hailers = vessel.FindPartModulesImplementing<ESLDHailer>();
            for (int i = hailers.Count - 1; i >= 0; i--)
                hailers[i].AttachedGui = this;

            List<Vessel> vesselsUnloaded = FlightGlobals.VesselsUnloaded;
            for (int i = vesselsUnloaded.Count - 1; i >= 0; i--)
            {
                if (!farBeaconVessels.ContainsKey(vesselsUnloaded[i]))
                {
                    farBeaconVessels.Add(vesselsUnloaded[i], QueryVesselFarBeacons(vesselsUnloaded[i].protoVessel));
                    farBeacons.AddRange(farBeaconVessels[vesselsUnloaded[i]]);
                }
                if (farBeaconVessels[vesselsUnloaded[i]].Count > 0)
                {
                    targetDetails.Add(new TargetDetails(vesselsUnloaded[i], this));
                }
            }
        }

        public void OnDestroy()
        {
            for (int i = hailers.Count - 1; i >= 0; i--)
            {
                hailers[i].AttachedGui = null;
            }
            GameEvents.onVesselWasModified.Remove(VesselWasModified);
            GameEvents.onVesselGoOffRails.Remove(VesselOffRails);
            GameEvents.onVesselGoOnRails.Remove(VesselOnRails);
            GameEvents.onNewVesselCreated.Remove(VesselCreated);
            GameEvents.onVesselCreate.Remove(VesselCreated);
            GameEvents.onVesselWillDestroy.Remove(VesselDestroyed);
            UnityEngine.Object.Destroy(predictionGameObject);
            openWindows.Remove(this);
            if (openWindows.Count == 0)
                HailerButton.Instance.toolbarControl.SetFalse(false);
            //HailerButton.Instance.button.SetFalse(false);
        }

        public void VesselDestroyed(Vessel vessel)
        {
            if (farBeaconVessels.ContainsKey(vessel))
            {
                for (int i = farBeaconVessels[vessel].Count - 1; i >= 0; i--)
                {
                    farBeacons.Remove(farBeaconVessels[vessel][i]);
                }
                farBeaconVessels.Remove(vessel);
            }
            targetDetails.RemoveAll(tgt => tgt.targetVessel == vessel);
        }

        public void VesselOnRails(Vessel vessel)
        {
            if (vessel == this.vessel)
                return;
            if (!vessel.loaded)
            {
                if (!farBeaconVessels.ContainsKey(vessel))
                {
                    farBeaconVessels.Add(vessel, QueryVesselFarBeacons(vessel.protoVessel));
                    farBeacons.AddRange(farBeaconVessels[vessel]);
                }
                if (farBeaconVessels[vessel].Count > 0)
                    targetDetails.Add(new TargetDetails(vessel, this));
            }
        }

        public void VesselOffRails(Vessel vessel)
        {
            if (farBeaconVessels.ContainsKey(vessel))
            {
                for (int i = farBeaconVessels[vessel].Count - 1; i >= 0; i--)
                    farBeacons.Remove(farBeaconVessels[vessel][i]);
                farBeaconVessels.Remove(vessel);
            }
            targetDetails.RemoveAll(tgt => tgt.targetVessel == vessel);
        }

        public void VesselCreated(Vessel vessel)
        {
            if (!vessel.loaded)
            {
                if (!farBeaconVessels.ContainsKey(vessel))
                {
                    farBeaconVessels.Add(vessel, QueryVesselFarBeacons(vessel.protoVessel));
                    farBeacons.AddRange(farBeaconVessels[vessel]);
                }
                if (farBeaconVessels[vessel].Count > 0)
                    targetDetails.Add(new TargetDetails(vessel, this));
            }
        }

        public void VesselWasModified(Vessel vessel)
        {
            if (vessel != this.vessel)
                return;
            //for (int i = hailers.Count - 1; i >= 0; i--)
            //{
            //hailers[i].
            //}
            hailers = this.vessel.FindPartModulesImplementing<ESLDHailer>();
            if (hailers.Count == 0)
                UnityEngine.Object.Destroy(this);


        }

        // Show exit orbital predictions
        private void ShowExitOrbit(Vessel nearObject, Vessel farObject)
        {
            // Recenter map, save previous state.
            wasInMapView = MapView.MapIsEnabled;
            if (!MapView.MapIsEnabled) MapView.EnterMapView();
            MapObject mapObject = FindVesselBody(farObject);
            if ((UnityEngine.Object)mapObject != (UnityEngine.Object)null)
            {
                MapView.MapCamera.SetTarget(mapObject);
            }
            Vector3 mapCamPos = ScaledSpace.ScaledToLocalSpace(MapView.MapCamera.transform.position);
            Vector3 farTarPos = ScaledSpace.ScaledToLocalSpace(mapObject.transform.position);
            float dirScalar = Vector3.Distance(mapCamPos, farTarPos);
            //log.Debug("Initializing, camera distance is " + dirScalar);
            if (!IsPatchedConicsAvailable)
            {
                HideExitOrbit();
            }
            else
            {
                predictionsDrawn = true;
                Vector3d vel = ESLDBeacon.GetJumpVelOffset(nearObject, farObject);
                if (predictionGameObject != null) Destroy(predictionGameObject);

                predictionGameObject = new GameObject("OrbitRendererGameObject");
                predictionOrbitDriver = predictionGameObject.AddComponent<OrbitDriver>();
                predictionOrbitDriver.orbit.referenceBody = farObject.mainBody;
                predictionOrbitDriver.orbit = new Orbit();
                predictionOrbitDriver.referenceBody = farObject.mainBody;
                predictionOrbitDriver.upperCamVsSmaRatio = 999999f;  // Took forever to figure this out - this sets at what zoom level the orbit appears.  Was causing it not to appear at small bodies.
                predictionOrbitDriver.lowerCamVsSmaRatio = 0.0001f;
                predictionOrbitDriver.orbit.UpdateFromStateVectors(farObject.orbit.pos, vel, farObject.mainBody, Planetarium.GetUniversalTime());
                predictionOrbitDriver.orbit.Init();
                Vector3d relativePositionAtUT = predictionOrbitDriver.orbit.getRelativePositionAtUT(Planetarium.GetUniversalTime());
                Vector3d orbitalVelocityAtUT = predictionOrbitDriver.orbit.getOrbitalVelocityAtUT(Planetarium.GetUniversalTime());
                predictionOrbitDriver.orbit.h = Vector3d.Cross(relativePositionAtUT, orbitalVelocityAtUT);
                predictionOrbitDriver.updateMode = OrbitDriver.UpdateMode.TRACK_Phys;
                predictionOrbitDriver.orbitColor = Color.red;
                predictionOrbitRenderer = predictionGameObject.AddComponent<OrbitRenderer>();
                predictionOrbitRenderer.SetColor(Color.red);
                predictionOrbitRenderer.vessel = vessel;
                predictionOrbitDriver.vessel = vessel;
                predictionOrbitRenderer.upperCamVsSmaRatio = 999999f;
                predictionOrbitRenderer.lowerCamVsSmaRatio = 0.0001f;
                predictionOrbitRenderer.celestialBody = farObject.mainBody;
                predictionOrbitRenderer.driver = predictionOrbitDriver;
                predictionOrbitDriver.Renderer = predictionOrbitRenderer;

                if (true)
                {
                    // This draws the full Patched Conics prediction.
                    predictionPatchedConicSolver = predictionGameObject.AddComponent<PatchedConicSolver>();
                    predictionPatchedConicRenderer = predictionGameObject.AddComponent<PatchedConicRenderer>();
                    predictionOrbitRenderer.drawIcons = OrbitRendererBase.DrawIcons.NONE;
                    predictionOrbitRenderer.drawMode = OrbitRendererBase.DrawMode.OFF;
                }
#if false
                else
                {
                    // This draws just the first patch, similar to a Level 1 tracking station.
                    predictionOrbitRenderer.driver.drawOrbit = true;
                    predictionOrbitRenderer.drawIcons = OrbitRenderer.DrawIcons.OBJ_PE_AP;
                    predictionOrbitRenderer.drawMode = OrbitRenderer.DrawMode.REDRAW_AND_RECALCULATE;
                    predictionOrbitRenderer.enabled = true;
                }
#endif
                base.StartCoroutine(NullOrbitDriverVessels());
                // Splash some color on it.

                // Directional indicator.
                /*
	            float baseWidth = 20.0f;
	            double baseStart = 10;
	            double baseEnd = 50;
	            oDirObj = new GameObject("Indicator");
	            oDirObj.layer = 10; // Map layer!
	            oDirection = oDirObj.AddComponent<LineRenderer>();
	            oDirection.useWorldSpace = false;
	            oOrigin = null;
	            foreach (Transform sstr in ScaledSpace.Instance.scaledSpaceTransforms)
	            {
	                if (sstr.name == far.mainBody.name)
	                {
	                    oOrigin = sstr;
	                    log.debug("Found origin: " + sstr.name);
	                    break;
	                }
	            }
	            oDirection.transform.parent = oOrigin;
	            oDirection.transform.position = ScaledSpace.LocalToScaledSpace(far.transform.position);
	            oDirection.material = new Material(Shader.Find("Particles/Additive"));
	            oDirection.SetColors(Color.clear, Color.red);
	            if (dirScalar / 325000 > baseWidth) baseWidth = dirScalar / 325000f;
	            oDirection.SetWidth(baseWidth, 0.01f);
	            log.debug("Base Width set to " + baseWidth);
	            oDirection.SetVertexCount(2);
	            if (dirScalar / 650000 > baseStart) baseStart = dirScalar / 650000;
	            if (dirScalar / 130000 > baseEnd) baseEnd = dirScalar / 130000;
	            log.debug("Base Start set to " + baseStart);
	            log.debug("Base End set to " + baseEnd);
	            oDirection.SetPosition(0, Vector3d.zero + exitTraj.xzy.normalized * baseStart);
	            oDirection.SetPosition(1, exitTraj.xzy.normalized * baseEnd);
	            oDirection.enabled = true;
	             */
            }
        }

        public IEnumerator NullOrbitDriverVessels()
        {
            // Initially tried letting it keep this vessel, but things flicker.
            // Then, tried it as null all the time, but it throws a NullRef in PatchedConicRenderer.Start()
            // But, once PCR is initialized, it's all good to set the OrbitDriver.vessel to null.
            if (predictionPatchedConicRenderer.relativeTo == null)
                yield return new WaitForEndOfFrame();
            while (predictionPatchedConicRenderer.relativeTo == null)
            {
                if (!HighLogic.LoadedSceneIsFlight)
                {
                    //log.Debug("Scene changed, breaking Coroutine.");
                    yield break;
                }
                yield return null;
            }
            predictionOrbitRenderer.vessel = null;
            predictionOrbitDriver.vessel = null;
        }

        private void UpdateExitOrbit(Vessel nearObject, Vessel farObject)
        {
            if (predictionsDrawn)
            {
                if (!IsPatchedConicsAvailable)
                {
                    HideExitOrbit();
                }
                else
                {
                    Vector3 a = ScaledSpace.ScaledToLocalSpace(MapView.MapCamera.transform.position);
                    MapObject target = MapView.MapCamera.target;
                    Vector3 b = ScaledSpace.ScaledToLocalSpace(target.transform.position);
                    Vector3.Distance(a, b);
                    Vector3d vel = ESLDBeacon.GetJumpVelOffset(nearObject, farObject);
                    predictionOrbitRenderer.driver.referenceBody = farObject.mainBody;
                    predictionOrbitRenderer.driver.orbit.referenceBody = farObject.mainBody;
                    predictionOrbitRenderer.driver.pos = farObject.orbit.pos;
                    predictionOrbitRenderer.celestialBody = farObject.mainBody;
                    predictionOrbitRenderer.SetColor(Color.red);
                    predictionOrbitDriver.orbit.UpdateFromStateVectors(farObject.orbit.pos, vel, farObject.mainBody, Planetarium.GetUniversalTime());
                    /* Direction indicator is broken/not required
        		    float baseWidth = 20.0f;
		            double baseStart = 10;
        		    double baseEnd = 50;
		            oDirection.transform.position = ScaledSpace.LocalToScaledSpace(far.transform.position);
        		    if (dirScalar / 325000 > baseWidth) baseWidth = dirScalar / 325000f;
		            oDirection.SetWidth(baseWidth, 0.01f);
        		    if (dirScalar / 650000 > baseStart) baseStart = dirScalar / 650000;
		            if (dirScalar / 130000 > baseEnd) baseEnd = dirScalar / 130000;
//          		log.debug("Camera distance is " + dirScalar + " results: " + baseWidth + " " + baseStart + " " + baseEnd);
        		    oDirection.SetPosition(0, Vector3d.zero + exitTraj.xzy.normalized * baseStart);
    		        oDirection.SetPosition(1, exitTraj.xzy.normalized * baseEnd);
		            oDirection.transform.eulerAngles = Vector3d.zero;*/
                }
            }
        }

        // Back out of orbital predictions.
        private void HideExitOrbit()
        {
            MapView.MapCamera.SetTarget(MapView.MapCamera.targets.Find((MapObject mobj) => (UnityEngine.Object)mobj.vessel != (UnityEngine.Object)null && mobj.vessel.GetInstanceID() == FlightGlobals.ActiveVessel.GetInstanceID()));
            if (MapView.MapIsEnabled && !wasInMapView) MapView.ExitMapView();

            wasInMapView = true; // Not really, but this stops it from trying to force it if the user enters again.
            if (predictionsDrawn)
            {
                predictionOrbitRenderer.drawMode = OrbitRendererBase.DrawMode.OFF;
                predictionOrbitRenderer.driver.drawOrbit = false;
                predictionOrbitRenderer.drawIcons = OrbitRendererBase.DrawIcons.NONE;
                UnityEngine.Object.Destroy(predictionGameObject);
                predictionGameObject = null;
                predictionsDrawn = false;

                /*Deprecated foreach (MapObject mobj in MapView.MapCamera.targets)
    	        {
	                if (mobj.vessel == null) continue;
                	if (mobj.vessel.GetInstanceID() == FlightGlobals.ActiveVessel.GetInstanceID())
            	    {
        	            MapView.MapCamera.SetTarget(mobj);
    	            }
	            }*/
            }
        }

        // Mapview Utility
        private MapObject FindVesselBody(Vessel craft)
        {
            int instanceID = craft.mainBody.GetInstanceID();
            foreach (MapObject target in MapView.MapCamera.targets)
            {
                if (!((UnityEngine.Object)target.celestialBody == (UnityEngine.Object)null) && target.celestialBody.GetInstanceID() == instanceID)
                {
                    return target;
                }
            }
            return null;
        }

        private static void SetupStyles()
        {
            if (buttonNeutral == null)
            {
                buttonNeutral = new GUIStyle(HighLogic.Skin.button);
                //buttonNeutral.padding = new RectOffset(8, 8, 8, 8);
                //buttonNeutral.normal.textColor = buttonNeutral.focused.textColor = Color.white;
                //buttonNeutral.hover.textColor = buttonNeutral.active.textColor = Color.white;

                labelHasFuel = new GUIStyle(HighLogic.Skin.label);
                labelHasFuel.normal.textColor = Color.green;
                labelNoFuel = new GUIStyle(HighLogic.Skin.label);
                labelNoFuel.normal.textColor = Color.red;
                buttonHasFuel = new GUIStyle(HighLogic.Skin.button);
                GUIStyleState normal = buttonHasFuel.normal;
                GUIStyleState hover = buttonHasFuel.hover;
                Color color3 = normal.textColor = (hover.textColor = new Color(0.67f, 1f, 0f));
                GUIStyleState focused = buttonHasFuel.focused;
                GUIStyleState active = buttonHasFuel.active;
                color3 = (focused.textColor = (active.textColor = new Color(0.8f, 1f, 0f)));
                buttonNoFuel = new GUIStyle(HighLogic.Skin.button);
                GUIStyleState normal2 = buttonNoFuel.normal;
                GUIStyleState hover2 = buttonNoFuel.hover;
                color3 = (normal2.textColor = (hover2.textColor = new Color(0.89f, 0.75f, 0.06f)));
                GUIStyleState focused2 = buttonNoFuel.focused;
                GUIStyleState active2 = buttonNoFuel.active;
                color3 = (focused2.textColor = (active2.textColor = Color.yellow));
                buttonNoPath = new GUIStyle(HighLogic.Skin.button);
                GUIStyleState normal3 = buttonNoPath.normal;
                GUIStyleState focused3 = buttonNoPath.focused;
                color3 = (normal3.textColor = (focused3.textColor = Color.black));
                GUIStyleState hover3 = buttonNoPath.hover;
                GUIStyleState active3 = buttonNoPath.active;
                color3 = (hover3.textColor = (active3.textColor = Color.black));
            }
        }

        // Find parts that need a HCU to transfer.
        public static Dictionary<Part, string> GetHCUParts(Vessel craft)
        {
            Dictionary<Part, string> dictionary = new Dictionary<Part, string>();
            foreach (Part part in craft.Parts)
            {
                foreach (PartResource resource in part.Resources)
                {
                    if (highEnergyResources.ContainsKey(resource.resourceName) && resource.amount > 0.0 && !dictionary.Keys.Contains(part))
                    {
                        dictionary.Add(part, resource.resourceName);
                    }
                }
            }
            return dictionary;
        }
    }
}
