using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
//using System.Threading.Tasks;
using UnityEngine;

namespace ESLDCore
{
    public class ESLDHailer : PartModule
    {
        public static Dictionary<string, float> highEnergyResources = new Dictionary<string, float>()
        {
            // CRP/USI Curated Resources
            {"Karborundum", 1}, {"Uraninite", 0.8f},
            // NFT Curated Resources
            {"DepletedUranium", 0.3f}, {"EnrichedUranium", 0.7f},
            // KSPI-E Curated Resources
            {"Actinides", 0.6f}, {"Antimatter", 1}, {"ChargedParticles", 1}, {"DepletedFuel", 0.3f},
            {"ExoticMatter", 1}, {"Fluorine", 0.4f}, {"LqdHe3", 0.6f}, {"LqdTritium", 0.8f},
            {"Plutonium-238", 0.8f}, {"ThF4", 1}, {"UraniumNitride", 0.65f}, {"VacuumPlasma", 1},
            // Possibly Deprecated Resources
            {"Uranium", 0.7f}, {"Thorium", 0.6f}
        };

        protected Rect BeaconWindow;
        protected Rect ConfirmWindow;
        public ESLDBeacon nearBeacon = null;
        public Vessel farBeaconVessel = null;
        public List<Vessel> farTargets = new List<Vessel>();
        public Dictionary<ESLDBeacon, string> nearBeacons = new Dictionary<ESLDBeacon, string>();
        public double precision;
        public GameObject predictionGameObject = null;
        public OrbitDriver predictionOrbitDriver = null;
        public OrbitRenderer predictionOrbitRenderer = null;
        PatchedConicSolver predictionPatchedConicSolver;
        PatchedConicRenderer predictionPatchedConicRenderer;
        bool predictionsDrawn = false;
        //public Transform oOrigin = null;
        //public LineRenderer oDirection = null;
        public double lastRemDist;
        public bool wasInMapView;
        public bool nbWasUserSelected = false;
        public bool isJumping = false;
        public bool isActive = false;
        public int currentBeaconIndex;
        public string currentBeaconDesc;
        public HailerButton hailerButton = null;
        bool drawConfirmOn = false;
        bool drawGUIOn = false;
        Logger log = new Logger("ESLDCore:ESLDHailer: ");

        public static GUIStyle buttonNeutral;
        public static GUIStyle labelHasFuel;
        public static GUIStyle labelNoFuel;

        public static GUIStyle buttonHasFuel;
        public static GUIStyle buttonNoFuel;
        public static GUIStyle buttonNoPath;

        // GUI Open?
        [KSPField(guiName = "GUIOpen", isPersistant = true, guiActive = false)]
        public bool guiopen;

        [KSPField(guiName = "Beacon", guiActive = false)]
        public string hasNearBeacon;

        [KSPField(guiName = "Beacon Distance", guiActive = false, guiUnits = "m")]
        public double nearBeaconDistance;

        [KSPField(guiName = "Drift", guiActive = false, guiUnits = "m/s")]
        public double nearBeaconRelVel;

        // Find parts that need a HCU to transfer.
        public static Dictionary<Part, string> GetHCUParts(Vessel craft)
        {
            Dictionary<Part, string> HCUParts = new Dictionary<Part, string>();
            foreach (Part vpart in craft.Parts)
            {
                foreach (PartResource vres in vpart.Resources)
                {
                    if (highEnergyResources.ContainsKey(vres.resourceName) && vres.amount > 0)
                    {
                        if (!HCUParts.Keys.Contains(vpart))
                            HCUParts.Add(vpart, vres.resourceName);
                    }
                }
            }
            return HCUParts;
        }

        // Find loaded beacons.  Only in physics distance, since otherwise they're too far out.
        private ESLDBeacon ScanForNearBeacons()
        {
            nearBeacons.Clear();
            Fields["hasNearBeacon"].guiActive = true;
            ESLDBeacon nearBeaconCandidate = null;
            int candidateIndex = 0;
            string candidateDesc = "";
            foreach (ESLDBeacon selfBeacon in vessel.FindPartModulesImplementing<ESLDBeacon>())
            {
                if (selfBeacon.canJumpSelf && selfBeacon.activated)
                {
                    nearBeaconDistance = 0;
                    nearBeaconRelVel = 0;
                    Fields["nearBeaconDistance"].guiActive = false;
                    Fields["nearBeaconRelVel"].guiActive = false;
                    hasNearBeacon = "Onboard";
                    return selfBeacon;
                }
            }
            double closest = 3000;
            foreach (Vessel craft in FlightGlobals.Vessels)
            {
                if (!craft.loaded) continue;                // Eliminate far away craft.
                if (craft == vessel) continue;                      // Eliminate current craft.
                if (craft == FlightGlobals.ActiveVessel) continue;
                if (craft.FindPartModulesImplementing<ESLDBeacon>().Count == 0) continue; // Has beacon?
                foreach (ESLDBeacon craftbeacon in craft.FindPartModulesImplementing<ESLDBeacon>())
                {
                    if (!craftbeacon.activated) { continue; }   // Beacon active?
                    if (!craftbeacon.jumpTargetable) { continue; } // Jumpdrives can't do remote transfers.
                    string bIdentifier = craftbeacon.beaconModel + " (" + craft.vesselName + ")";
                    nearBeacons.Add(craftbeacon, bIdentifier);
                    int nbIndex = nearBeacons.Count - 1;
                    nearBeaconDistance = Math.Round(Vector3d.Distance(vessel.GetWorldPos3D(), craft.GetWorldPos3D()));
                    if (closest > nearBeaconDistance)
                    {
                        nearBeaconCandidate = craftbeacon;
                        candidateIndex = nbIndex;
                        candidateDesc = bIdentifier;
                        closest = nearBeaconDistance;
                    }
                }
            }
            if (nearBeacon != null && nearBeacon.vessel.loaded && nbWasUserSelected && nearBeacon.activated) // If we've already got one, just update the display.
            {
                nearBeaconDistance = Math.Round(Vector3d.Distance(vessel.GetWorldPos3D(), nearBeacon.vessel.GetWorldPos3D()));
                nearBeaconRelVel = Math.Round(Vector3d.Magnitude(vessel.obt_velocity - nearBeacon.vessel.obt_velocity) * 10) / 10;
                return nearBeacon;
            }
            if (nearBeacons.Count > 0) // If we hadn't selected one previously return the closest one.
            {
                nbWasUserSelected = false;
                Vessel craft = nearBeaconCandidate.vessel;
                Fields["nearBeaconDistance"].guiActive = true;
                nearBeaconDistance = Math.Round(Vector3d.Distance(vessel.GetWorldPos3D(), craft.GetWorldPos3D()));
                Fields["nearBeaconRelVel"].guiActive = true;
                nearBeaconRelVel = Math.Round(Vector3d.Magnitude(vessel.obt_velocity - craft.obt_velocity) * 10) / 10;
                hasNearBeacon = "Present";
                currentBeaconIndex = candidateIndex;
                currentBeaconDesc = candidateDesc;
                return nearBeaconCandidate;
            }
            hasNearBeacon = "Not Present";
            Fields["nearBeaconDistance"].guiActive = false;
            Fields["nearBeaconRelVel"].guiActive = false;
            nearBeacon = null;
            return null;
        }

        // Finds beacon targets.  Only starts polling when the GUI is open.
        public void ListFarBeacons()
        {
            farTargets.Clear();
            foreach (Vessel craft in FlightGlobals.VesselsUnloaded)
            {
                if (craft == vessel) continue;
                if (craft == FlightGlobals.ActiveVessel) continue;
                //if (craft.situation != Vessel.Situations.ORBITING) continue;

                bool vesselAdded = false;
                foreach (ProtoPartSnapshot ppart in craft.protoVessel.protoPartSnapshots)
                {
                    foreach (ProtoPartModuleSnapshot pmod in ppart.modules.FindAll(
                        (ProtoPartModuleSnapshot p) => p.moduleName == "ESLDBeacon"))
                    {
                        if (pmod.moduleValues.GetValue("activated") == "True")
                        {
                            if (!farTargets.Contains(craft))
                                farTargets.Add(craft);
                            vesselAdded = true;
                            break;
                        }
                    }
                    if (vesselAdded)
                        break;
                }
            }
        }

        public List<ESLDBeacon> GetBeaconsOnTarget(Vessel craft)
        {
            List<ESLDBeacon> value = new List<ESLDBeacon>();
            foreach (ProtoPartSnapshot ppart in craft.protoVessel.protoPartSnapshots)
            {
                foreach (ProtoPartModuleSnapshot pmod in ppart.modules.FindAll(
                    (ProtoPartModuleSnapshot p) => p.moduleName == "ESLDBeacon"))
                {
                    if (pmod.moduleValues.GetValue("activated") == "True")
                    {
                        ESLDBeacon protoBeacon = new ESLDBeacon(pmod.moduleValues, ppart.partInfo.partConfig.GetNodes("MODULE")[ppart.modules.IndexOf(pmod)]);
                        protoBeacon.activated = true;
                        value.Add(protoBeacon);
                    }
                }
            }
            return value;
        }

        public override void OnUpdate()
        {
            if (isActive)
            {
                var startState = hasNearBeacon;
                nearBeacon = ScanForNearBeacons();
                if (nearBeacon == null)
                {
                    if (startState != hasNearBeacon)
                    {
                        HailerGUIClose();
                    }
                    Events["HailerGUIClose"].active = false;
                    Events["HailerGUIOpen"].active = false;
                }
                else
                {
                    Events["HailerGUIClose"].active = false;
                    Events["HailerGUIOpen"].active = true;
                }
            }
        }

        // Screen 1 of beacon interface, displays beacons and where they go along with some fuel calculations.
        private void BeaconInterface(int GuiId)
        {
            if (!vessel.isActiveVessel) HailerGUIClose();

            GUILayout.BeginVertical(HighLogic.Skin.scrollView);
            if (farTargets.Count() < 1 || nearBeacon == null)
            {
                GUILayout.Label("No active beacons found.");
            }
            else
            {
                double tonnage = vessel.GetTotalMass();
                Vessel nbparent = nearBeacon.vessel;
                string nbModel = nearBeacon.beaconModel;
                nearBeacon.CheckOwnTechBoxes();
                //double nbfuel = nearBeacon.fuelOnBoard;
                double driftpenalty = Math.Round(Math.Pow(Math.Floor(nearBeaconDistance / 200), 2) + Math.Floor(Math.Pow(nearBeaconRelVel, 1.5)) * nearBeacon.GetCrewBonuses(nbparent,"Pilot",0.5,5));
                if (driftpenalty > 0) GUILayout.Label("+" + driftpenalty + "% due to Drift.");
                Dictionary<Part, string> HCUParts = GetHCUParts(vessel);
                if (!nearBeacon.hasHCU)
                {
                    if (vessel.GetCrewCount() > 0 || HCUParts.Count > 0) GUILayout.Label("WARNING: This beacon has no active Heisenkerb Compensator.");
                    if (vessel.GetCrewCount() > 0) GUILayout.Label("Transfer will kill crew.");
                    if (HCUParts.Count > 0) GUILayout.Label("Some resources will destabilize.");
                }
                foreach (Vessel farTargetVessel in farTargets)
                {
                    double tripdist = Vector3d.Distance(nbparent.GetWorldPos3D(), farTargetVessel.GetWorldPos3D());
                    double tripcost = nearBeacon.GetTripBaseCost(tripdist, tonnage);
                    double sciBonus = nearBeacon.GetCrewBonuses(nbparent, "Scientist", 0.5, 5);
                    if (nearBeacon.hasSCU)
                    {
                        if (driftpenalty == 0 && sciBonus >= 0.9)
                        {
                            tripcost *= 0.9;
                        }
                        if (sciBonus < 0.9 || (sciBonus < 1 && driftpenalty > 0))
                        {
                            tripcost *= sciBonus;
                        }

                    }
                    if (tripcost == 0) continue;
                    tripcost += tripcost * (driftpenalty * .01);
                    if (nearBeacon.hasAMU) tripcost += nearBeacon.GetAMUCost(vessel, farTargetVessel, tonnage);
                    double HCUCost = nearBeacon.GetHCUCost(vessel, HCUParts.Keys);
                    if (nearBeacon.builtInHCU) HCUCost = Math.Round((HCUCost - (tripcost * 0.02)) * 100) / 100;
                    if (nearBeacon.hasHCU) tripcost += HCUCost;
                    tripcost = Math.Round(tripcost * 100) / 100;
                    string targetSOI = farTargetVessel.mainBody.name;
                    double targetAlt = Math.Round(farTargetVessel.altitude / 1000);
                    GUIStyle fuelstate = buttonNoFuel;
                    string blockReason = "";
                    string blockRock = "";
                    bool affordable = true;
                    foreach (ESLDJumpResource Jresource in nearBeacon.jumpResources)
                    {
                        if (tripcost * Jresource.ratio > Jresource.fuelOnBoard)
                        {
                            affordable = false;
                            break;
                        }
                    }
                    if (affordable) // Show blocked status only for otherwise doable transfers.
                    {
                        fuelstate = buttonHasFuel;
                        KeyValuePair<string, CelestialBody> checkpath = HasTransferPath(nbparent, farTargetVessel, nearBeacon.gLimitEff);
                        if (checkpath.Key != "OK")
                        {
                            fuelstate = buttonNoPath;
                            blockReason = checkpath.Key;
                            blockRock = checkpath.Value.name;
                        }
                    }
                    if (GUILayout.Button(farTargetVessel.vesselName + "(" + targetSOI + ", " + targetAlt + "km) | " + tripcost, fuelstate))
                    {
                        if (fuelstate == buttonHasFuel)
                        {
                            farBeaconVessel = farTargetVessel;
                            if (!nearBeacon.hasAMU) ShowExitOrbit(vessel, farTargetVessel);
                            drawConfirmOn = true;
                            drawGUIOn = false;
                            Events["HailerGUIClose"].active = false;
                            Events["HailerGUIOpen"].active = true;
                        }
                        else
                        {
                            log.Info("Current beacon has a g limit of " + nearBeacon.gLimitEff);
                            string messageToPost = "I can't tell why the jump won't work. Please report this error with your save file and log.";
                            if (!affordable)
                            {
                                foreach(ESLDJumpResource Jresource in nearBeacon.jumpResources)
                                {
                                    if (tripcost * Jresource.ratio <= Jresource.fuelOnBoard)
                                        continue;
                                    messageToPost = "Cannot Warp: Origin beacon has " + Jresource.fuelOnBoard + " of " + tripcost * Jresource.ratio + " " + Jresource.name + " required to warp.";
                                }
                            }
                            string thevar = (blockRock == "Mun" || blockRock == "Sun") ? "the " : string.Empty;
                            if (fuelstate == buttonNoPath && blockReason == "Gravity") messageToPost = "Cannot Warp: Path of transfer intersects a high-gravity area around " + thevar + blockRock + ".";
                            if (fuelstate == buttonNoPath && blockReason == "Proximity") messageToPost = "Cannot Warp: Path of transfer passes too close to " + thevar + blockRock + ".";
                            ScreenMessages.PostScreenMessage(messageToPost, 5.0f, ScreenMessageStyle.UPPER_CENTER);
                            log.Debug(messageToPost);
                        }
                    }
                }
            }
            if(nearBeacons.Count > 1)
            {
                GUILayout.Label("Current Beacon: " + currentBeaconDesc);
                if (currentBeaconIndex >= nearBeacons.Count) currentBeaconIndex = nearBeacons.Count - 1;
                int nextIndex = currentBeaconIndex + 1;
                if (nextIndex >= nearBeacons.Count) nextIndex = 0;
                if (GUILayout.Button("Next Beacon (" + (currentBeaconIndex + 1) + " of " + nearBeacons.Count + ")", buttonNeutral))
                {
                    nbWasUserSelected = true;
                    nearBeacon = nearBeacons.ElementAt(nextIndex).Key;
                    currentBeaconDesc = nearBeacons.ElementAt(nextIndex).Value;
                    currentBeaconIndex = nextIndex;
                }
            }
            if (GUILayout.Button("Close Beacon Interface", buttonNeutral))
            {
                HailerGUIClose();
            }
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        public void Update()
        {
            UpdateExitOrbit(vessel, farBeaconVessel);
        }

        public void OnDestroy()
        {
            Destroy(predictionGameObject);
        }

        private static void SetupStyles()
        {
            if (buttonNeutral != null)
                return;

            buttonNeutral = new GUIStyle(GUI.skin.button);
            buttonNeutral.padding = new RectOffset(8, 8, 8, 8);
            buttonNeutral.normal.textColor = buttonNeutral.focused.textColor = Color.white;
            buttonNeutral.hover.textColor = buttonNeutral.active.textColor = Color.white;

            labelHasFuel = new GUIStyle(GUI.skin.label);
            labelHasFuel.normal.textColor = Color.green;

            labelNoFuel = new GUIStyle(GUI.skin.label);
            labelNoFuel.normal.textColor = Color.red;

            buttonHasFuel = new GUIStyle(GUI.skin.button);
            buttonHasFuel.padding = new RectOffset(8, 8, 8, 8);
            buttonHasFuel.normal.textColor = buttonHasFuel.focused.textColor = Color.green;
            buttonHasFuel.hover.textColor = buttonHasFuel.active.textColor = Color.white;

            buttonNoFuel = new GUIStyle(GUI.skin.button);
            buttonNoFuel.padding = new RectOffset(8, 8, 8, 8);
            buttonNoFuel.normal.textColor = buttonNoFuel.focused.textColor = Color.red;
            buttonNoFuel.hover.textColor = buttonNoFuel.active.textColor = Color.yellow;

            buttonNoPath = new GUIStyle(GUI.skin.button);
            buttonNoPath.padding = new RectOffset(8, 8, 8, 8);
            buttonNoPath.normal.textColor = buttonNoFuel.focused.textColor = Color.gray;
            buttonNoPath.hover.textColor = buttonNoFuel.active.textColor = Color.gray;
        }

        private void ConfirmInterface(int GuiID) // Second beacon interface window.  
        {
            GUILayout.BeginVertical(HighLogic.Skin.scrollView);
            if (nearBeacon != null)
            {
                double tripdist = Vector3d.Distance(nearBeacon.vessel.GetWorldPos3D(), farBeaconVessel.GetWorldPos3D());
                double tonnage = vessel.GetTotalMass();
                Vessel nbparent = nearBeacon.vessel;
                string nbModel = nearBeacon.beaconModel;
                nearBeacon.CheckOwnTechBoxes();
                double tripcost = nearBeacon.GetTripBaseCost(tripdist, tonnage);
                double driftpenalty = Math.Pow(Math.Floor(nearBeaconDistance / 200), 2) + Math.Floor(Math.Pow(nearBeaconRelVel, 1.5));
                if (driftpenalty > 0) GUILayout.Label("+" + driftpenalty + "% due to Drift.");
                Dictionary<Part, string> HCUParts = GetHCUParts(vessel);
                if (!nearBeacon.hasHCU)
                {
                    if (vessel.GetCrewCount() > 0 || HCUParts.Count > 0) GUILayout.Label("WARNING: This beacon has no active Heisenkerb Compensator.", labelNoFuel);
                    if (vessel.GetCrewCount() > 0) GUILayout.Label("Transfer will kill crew.", labelNoFuel);
                    if (HCUParts.Count > 0)
                    {
                        GUILayout.Label("These resources will destabilize in transit:", labelNoFuel);
                        foreach (KeyValuePair<Part, string> hcuresource in HCUParts)
                        {
                            GUILayout.Label(hcuresource.Key.name + " - " + hcuresource.Value, labelNoFuel);
                        }
                    }
                }
                GUILayout.Label("Confirm Warp:");
                var basecost = Math.Round(tripcost * 100) / 100;
                //log.Debug("Base cost: " + basecost + "/" + nearBeacon.jumpResources.Count());
                string tempLabel;
                tempLabel = "Base Cost: ";
                foreach (ESLDJumpResource Jresource in nearBeacon.jumpResources)
                {
                    tempLabel += basecost * Jresource.ratio + " " + Jresource.name;
                    if (nearBeacon.jumpResources.IndexOf(Jresource) + 1 < nearBeacon.jumpResources.Count())
                        tempLabel += ", ";
                }
                GUILayout.Label(tempLabel + ".");
                double sciBonus = nearBeacon.GetCrewBonuses(nbparent, "Scientist", 0.5, 5);
                if (nearBeacon.hasSCU)
                {
                    if (driftpenalty == 0 && sciBonus >= 0.9)
                    {
                        GUILayout.Label("Superconducting Coil Array reduces cost by 10%.");
                        tripcost *= 0.9;
                    }
                    if (sciBonus < 0.9 || (sciBonus < 1 && driftpenalty > 0))
                    {
                        double dispBonus = Math.Round((1-sciBonus) * 100);
                        GUILayout.Label("Scientists on beacon vessel reduce cost by " + dispBonus + "%.");
                        tripcost *= sciBonus;
                    }
                    
                }
                if (driftpenalty > 0) GUILayout.Label("Relative speed and distance to beacon adds " + driftpenalty + "%.");
                tripcost += tripcost * (driftpenalty * .01);
                tripcost = Math.Round(tripcost * 100) / 100;
                if (nearBeacon.hasAMU)
                {
                    double AMUCost = nearBeacon.GetAMUCost(vessel, farBeaconVessel, tonnage);
                    tempLabel = "AMU Compensation adds ";
                    foreach (ESLDJumpResource Jresource in nearBeacon.jumpResources)
                    {
                        tempLabel += AMUCost * Jresource.ratio + " " + Jresource.name;
                        if (nearBeacon.jumpResources.IndexOf(Jresource) + 1 < nearBeacon.jumpResources.Count())
                            tempLabel += ", ";
                    }
                    GUILayout.Label(tempLabel + ".");
                    tripcost += AMUCost;
                }
                if (nearBeacon.hasHCU)
                {
                    double HCUCost = nearBeacon.GetHCUCost(vessel, HCUParts.Keys);
                    if (nearBeacon.builtInHCU) HCUCost = Math.Round((HCUCost - (tripcost * 0.02)) * 100) / 100;
                    tempLabel = "HCU Shielding adds ";
                    foreach (ESLDJumpResource Jresource in nearBeacon.jumpResources)
                    {
                        tempLabel += HCUCost * Jresource.ratio + " " + Jresource.name;
                        if (nearBeacon.jumpResources.IndexOf(Jresource) + 1 < nearBeacon.jumpResources.Count())
                            tempLabel += ", ";
                    }
                    GUILayout.Label(tempLabel + ".");
                    tripcost += HCUCost;
                }
                tempLabel = "Total Cost: ";
                foreach (ESLDJumpResource Jresource in nearBeacon.jumpResources)
                {
                    tempLabel += tripcost * Jresource.ratio + " " + Jresource.name;
                    if (nearBeacon.jumpResources.IndexOf(Jresource) + 1 < nearBeacon.jumpResources.Count())
                        tempLabel += ", ";
                }
                GUILayout.Label(tempLabel + ".");
                GUILayout.Label("Destination: " + farBeaconVessel.mainBody.name + " at " + Math.Round(farBeaconVessel.altitude / 1000) + "km.");
                List<ESLDBeacon> beaconsOnFarBeaconVessel = GetBeaconsOnTarget(farBeaconVessel);
                precision = 0;
                double retTripCost = 0;
                bool fuelcheck = false;
                bool affordReturn = true;
                ESLDBeacon cheapFarBeacon = beaconsOnFarBeaconVessel[0];
                foreach (ESLDBeacon tempFarBeacon in beaconsOnFarBeaconVessel)
                {
                    if (retTripCost == 0)
                    {
                        retTripCost = tempFarBeacon.GetTripBaseCost(tripdist, tonnage);
                        cheapFarBeacon = tempFarBeacon;
                    }
                    else
                    {
                        double tempCost = tempFarBeacon.GetTripBaseCost(tripdist, tonnage);
                        if (tempCost < retTripCost)
                        // Maybe add a check if this beacon has sufficient fuel?
                        // It may be the cheapest, but if different beacons use different fuel types, this could return that the far beacon
                        // does not have sufficient fuel even though a different beacon on the target vessel does.
                        {
                            retTripCost = tempCost;
                            cheapFarBeacon = tempFarBeacon;
                        }
                    }
                    if (precision == 0)
                        precision = tempFarBeacon.GetTripSpread(tripdist);
                    else
                    {
                        double tempPrecision = tempFarBeacon.GetTripSpread(tripdist);
                        if (tempPrecision < precision)
                            precision = tempPrecision;
                    }
                }
                GUILayout.Label("Transfer will emerge within " + precision + "m of destination beacon.");
                if (farBeaconVessel.altitude - precision <= farBeaconVessel.mainBody.Radius * 0.1f || farBeaconVessel.altitude - precision <= farBeaconVessel.mainBody.atmosphereDepth)
                {
                    GUILayout.Label("Arrival area is very close to " + farBeaconVessel.mainBody.name + ".", labelNoFuel);
                }
                if (!nearBeacon.hasAMU)
                {
                    Vector3d transferVelOffset = GetJumpVelOffset(vessel, farBeaconVessel, nearBeacon) - farBeaconVessel.orbit.vel;
                    GUILayout.Label("Velocity relative to exit beacon will be " + Math.Round(transferVelOffset.magnitude) + "m/s.");
                }
                fuelcheck = cheapFarBeacon.jumpResources.All((ESLDJumpResource jr) => jr.fuelCheck);
                string fuelmessage = "Destination beacon's fuel could not be checked.";
                if (fuelcheck)
                {
                    fuelmessage = "Destination beacon has ";
                    foreach (ESLDJumpResource Jresource in cheapFarBeacon.jumpResources)
                    {
                        fuelmessage += Jresource.fuelOnBoard.ToString("F2") + " " + Jresource.name;
                        if (cheapFarBeacon.jumpResources.IndexOf(Jresource) + 1 < nearBeacon.jumpResources.Count())
                            fuelmessage += ", ";
                        if (retTripCost * Jresource.ratio > Jresource.fuelOnBoard)
                            affordReturn = false;
                    }
                }
                GUILayout.Label(fuelmessage+".");
                retTripCost = Math.Round(retTripCost * 100) / 100;
                if (fuelcheck && affordReturn)
                {
                    tempLabel = "Destination beacon can make return trip using ";
                    foreach (ESLDJumpResource Jresource in cheapFarBeacon.jumpResources)
                    {
                        tempLabel += retTripCost * Jresource.ratio + " " + Jresource.name;
                        if (cheapFarBeacon.jumpResources.IndexOf(Jresource) + 1 < nearBeacon.jumpResources.Count())
                            tempLabel += ", ";
                    }
                    tempLabel += " (base cost).";
                    GUILayout.Label(tempLabel, labelHasFuel);
                }
                else if (fuelcheck)
                {
                    tempLabel = "Destination beacon would need ";
                    foreach (ESLDJumpResource Jresource in cheapFarBeacon.jumpResources)
                    {
                        tempLabel += retTripCost * Jresource.ratio + " " + Jresource.name;
                        if (cheapFarBeacon.jumpResources.IndexOf(Jresource) + 1 < nearBeacon.jumpResources.Count())
                            tempLabel += ", ";
                    }
                    tempLabel += " (base cost) for return trip using active beacons.";
                    GUILayout.Label(tempLabel, labelNoFuel);
                }
                if (GUILayout.Button("Confirm and Warp", buttonNeutral))
                {
                    drawConfirmOn = false;
                    HailerGUIClose();
                    HideExitOrbit();
                    // Check transfer path one last time.
                    KeyValuePair<string, CelestialBody> checkpath = HasTransferPath(nbparent, farBeaconVessel, nearBeacon.gLimitEff); // One more check for a clear path in case they left the window open too long.
                    bool finalPathCheck = false;
                    if (checkpath.Key == "OK") finalPathCheck = true;
                    if (!finalPathCheck)
                        ScreenMessages.PostScreenMessage("Jump Failed!  Transfer path has become obstructed.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                    else
                        Warp(nbparent, tripcost, HCUParts);
                }
            }
            else
            {
                HideExitOrbit();
            }
            if (!vessel.isActiveVessel)
            {
                drawConfirmOn = false;
                HideExitOrbit();
            }
            if (GUILayout.Button("Back", buttonNeutral))
            {
                drawConfirmOn = false;
                HailerGUIOpen();
                HideExitOrbit();
            }
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        private void Warp(Vessel nbparent, double tripcost, Dictionary<Part, string> HCUParts)
        {
            // Check fuel one last time.
            bool fuelcheck = nearBeacon.jumpResources.All((ESLDJumpResource Jresource) =>
                nearBeacon.RequireResource(nbparent, Jresource.resID, tripcost * Jresource.ratio, false));
            if (fuelcheck) // Fuel is valid for and path is clear.
            {
                // Pay fuel
                foreach (ESLDJumpResource Jresource in nearBeacon.jumpResources)
                    nearBeacon.RequireResource(nbparent, Jresource.resID, tripcost * Jresource.ratio, true);
                // Buckle up!
                if (!nearBeacon.hasHCU) // Penalize for HCU not being present/online.
                {
                    List<ProtoCrewMember> crewList = new List<ProtoCrewMember>();
                    List<Part> crewParts = new List<Part>();
                    foreach (Part vpart in vessel.Parts)
                    {
                        foreach (ProtoCrewMember crew in vpart.protoModuleCrew)
                        {
                            crewParts.Add(vpart);
                            crewList.Add(crew);
                        }
                    }
                    for (int i = crewList.Count - 1; i >= 0; i--)
                    {
                        if (i >= crewList.Count)
                        {
                            if (crewList.Count == 0) break;
                            i = crewList.Count - 1;
                        }
                        ProtoCrewMember tempCrew = crewList[i];
                        crewList.RemoveAt(i);
                        ScreenMessages.PostScreenMessage(tempCrew.name + " was killed in transit!", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                        crewParts[i].RemoveCrewmember(tempCrew);
                        crewParts.RemoveAt(i);
                        tempCrew.Die();
                    }
                    List<Part> HCUList = new List<Part>();
                    HCUList.AddRange(HCUParts.Keys);
                    for (int i = HCUList.Count - 1; i >= 0; i--)
                    {
                        if (i >= HCUList.Count)
                        {
                            if (HCUList.Count == 0) break;
                            i = HCUList.Count - 1;
                        }
                        Part tempPart = HCUList[i];
                        HCUList.RemoveAt(i);
                        tempPart.explosionPotential = 1;
                        tempPart.explode();
                        tempPart.Die();
                    }
                }
                hailerButton.Dazzle();
                Vector3d transferVelOffset = GetJumpVelOffset(vessel, farBeaconVessel, nearBeacon);
                if (nearBeacon.hasAMU) transferVelOffset = farBeaconVessel.orbit.vel;
                Vector3d spread = ((UnityEngine.Random.onUnitSphere + UnityEngine.Random.insideUnitSphere) / 2) * (float)precision;
                // Making the spread less likely to throw you outside the SoI of the body.
                if ((farBeaconVessel.orbit.pos + spread).magnitude > farBeaconVessel.mainBody.sphereOfInfluence)
                    spread = -spread;   // Negative random is equally random.

                OrbitDriver vesOrb = vessel.orbitDriver;
                Orbit orbit = vesOrb.orbit;
                Orbit newOrbit = new Orbit(orbit.inclination, orbit.eccentricity, orbit.semiMajorAxis, orbit.LAN, orbit.argumentOfPeriapsis, orbit.meanAnomalyAtEpoch, orbit.epoch, orbit.referenceBody);
                newOrbit.UpdateFromStateVectors(farBeaconVessel.orbit.pos + spread, transferVelOffset, farBeaconVessel.mainBody, Planetarium.GetUniversalTime());
                vessel.Landed = false;
                vessel.Splashed = false;
                vessel.landedAt = string.Empty;

                OrbitPhysicsManager.HoldVesselUnpack(60);

                List<Vessel> allVessels = FlightGlobals.Vessels;
                foreach (Vessel v in allVessels.AsEnumerable())
                {
                    if (v.packed == false)
                        v.GoOnRails();
                }

                CelestialBody oldBody = vessel.orbitDriver.orbit.referenceBody;

                orbit.inclination = newOrbit.inclination;
                orbit.eccentricity = newOrbit.eccentricity;
                orbit.semiMajorAxis = newOrbit.semiMajorAxis;
                orbit.LAN = newOrbit.LAN;
                orbit.argumentOfPeriapsis = newOrbit.argumentOfPeriapsis;
                orbit.meanAnomalyAtEpoch = newOrbit.meanAnomalyAtEpoch;
                orbit.epoch = newOrbit.epoch;
                orbit.referenceBody = newOrbit.referenceBody;
                orbit.Init();
                orbit.UpdateFromUT(Planetarium.GetUniversalTime());
                if (orbit.referenceBody != newOrbit.referenceBody)
                    vesOrb.OnReferenceBodyChange?.Invoke(newOrbit.referenceBody);

                vessel.orbitDriver.pos = vessel.orbit.pos.xzy;
                vessel.orbitDriver.vel = vessel.orbit.vel;

                if (vessel.orbitDriver.orbit.referenceBody != oldBody)
                    GameEvents.onVesselSOIChanged.Fire(new GameEvents.HostedFromToAction<Vessel, CelestialBody>(vessel, oldBody, vessel.orbitDriver.orbit.referenceBody));

                ListFarBeacons();
            }
            else
            {
                ScreenMessages.PostScreenMessage("Jump failed!  Origin beacon did not have enough fuel to execute transfer.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        private void OnGUI()
        {
            if (drawGUIOn || drawConfirmOn)
                SetupStyles();
            if (drawGUIOn)
                DrawGUI();
            if (drawConfirmOn)
                DrawConfirm();
            if (!drawGUIOn && !drawConfirmOn && farTargets.Count > 0)
                farTargets.Clear();
        }

        private void DrawGUI()
        {
            if (farTargets.Count() == 0)
                ListFarBeacons();
            BeaconWindow = GUILayout.Window(1, BeaconWindow, BeaconInterface, "Warp Information", GUILayout.MinWidth(400), GUILayout.MinHeight(200));
            if ((BeaconWindow.x == 0) && (BeaconWindow.y == 0))
            {
                BeaconWindow = new Rect(Screen.width / 2, Screen.height / 2, 10, 10);
            }
        }

        private void DrawConfirm()
        {
            ConfirmWindow = GUILayout.Window(2, ConfirmWindow, ConfirmInterface, "Pre-Warp Confirmation", GUILayout.MinWidth(400), GUILayout.MinHeight(200));
            if ((ConfirmWindow.x == 0) && (ConfirmWindow.y == 0))
            {
                ConfirmWindow = new Rect(Screen.width / 2, Screen.height / 2, 10, 10);
            }
        }

        [KSPEvent(name = "HailerActivate", active = true, guiActive = true, guiName = "Initialize Hailer")]
        public void HailerActivate()
        {
//          part.force_activate();
            isActive = true;
            Events["HailerActivate"].active = false;
            Events["HailerDeactivate"].active = true;
            ScanForNearBeacons();
        }
        [KSPEvent(name = "HailerGUIOpen", active = false, guiActive = true, guiName = "Beacon Interface")]
        public void HailerGUIOpen()
        {
            drawGUIOn = true;
            Events["HailerGUIOpen"].active = false;
            Events["HailerGUIClose"].active = true;
            guiopen = true;
        }
        [KSPEvent(name = "HailerGUIClose", active = false, guiActive = true, guiName = "Close Interface")]
        public void HailerGUIClose()
        {
            drawGUIOn = false;
            Events["HailerGUIClose"].active = false;
            Events["HailerGUIOpen"].active = true;
            guiopen = false;
        }
        [KSPEvent(name = "HailerDeactivate", active = false, guiActive = true, guiName = "Shut Down Hailer")]
        public void HailerDeactivate()
        {
            isActive = false;
            HideExitOrbit();
            HailerGUIClose();
            drawConfirmOn = false;
            Events["HailerDeactivate"].active = false;
            Events["HailerActivate"].active = true;
            Events["HailerGUIOpen"].active = false;
            Events["HailerGUIClose"].active = false;
            Fields["hasNearBeacon"].guiActive = false;
            Fields["nearBeaconDistance"].guiActive = false;
            Fields["nearBeaconRelVel"].guiActive = false;
        }

        // Calculate Jump Velocity Offset
        public static Vector3d GetJumpVelOffset(Vessel nearObject, Vessel farObject, ESLDBeacon beacon)
        {
            Vector3d farRealVelocity = farObject.orbit.vel;
            CelestialBody farRefbody = farObject.mainBody;
            while (farRefbody.flightGlobalsIndex != 0) // Kerbol
            {
                farRealVelocity += farRefbody.orbit.vel;
                farRefbody = farRefbody.referenceBody;
            }
            Vector3d nearRealVelocity = nearObject.orbit.vel;
            CelestialBody nearRefbody = nearObject.mainBody;
            if (nearObject.mainBody.flightGlobalsIndex == farObject.mainBody.flightGlobalsIndex)
            {
                farRealVelocity -= farObject.orbit.vel;
                //log.Debug("In-system transfer, disregarding far beacon velocity.");
            }
            while (nearRefbody.flightGlobalsIndex != 0) // Kerbol
            {
                nearRealVelocity += nearRefbody.orbit.vel;
                nearRefbody = nearRefbody.referenceBody;
            }
            return nearRealVelocity - farRealVelocity;
        }

        // Finds if the path between beacons passes too close to a planet or is within its gravity well.
        public static KeyValuePair<string, CelestialBody> HasTransferPath(Vessel vOrigin, Vessel vDestination, double gLimit)
        {
            // Cribbed with love from RemoteTech.  I have no head for vectors.
            var returnPair = new KeyValuePair<string, CelestialBody>("start", vOrigin.mainBody);
            Vector3d opos = vOrigin.GetWorldPos3D();
            Vector3d dpos = vDestination.GetWorldPos3D();
            foreach (CelestialBody rock in FlightGlobals.Bodies)
            {
                Vector3d bodyFromOrigin = rock.position - opos;
                Vector3d destFromOrigin = dpos - opos;
                if (Vector3d.Dot(bodyFromOrigin, destFromOrigin) <= 0) continue;
                Vector3d destFromOriginNorm = destFromOrigin.normalized;
                if (Vector3d.Dot(bodyFromOrigin, destFromOriginNorm) >= destFromOrigin.magnitude) continue;
                Vector3d lateralOffset = bodyFromOrigin - Vector3d.Dot(bodyFromOrigin, destFromOriginNorm) * destFromOriginNorm;
                double limbo = Math.Sqrt((6.673E-11 * rock.Mass) / gLimit) - rock.Radius; // How low can we go?
                string limbotype = "Gravity";
                if (limbo < rock.Radius + rock.Radius * 0.25)
                {
                    limbo = rock.Radius + rock.Radius * .025;
                    limbotype = "Proximity";
                }
                if (lateralOffset.magnitude < limbo)
                {
                    returnPair = new KeyValuePair<string, CelestialBody>(limbotype, rock);
                    //log.debug("Lateral Offset was " + lateralOffset.magnitude + "m and needed to be " + limbo + "m, failed due to " + limbotype + " check for " + rock.name + ".");
                    return returnPair;
                }
            }
            if (FlightGlobals.getGeeForceAtPosition(vDestination.GetWorldPos3D()).magnitude > gLimit) return new KeyValuePair<string, CelestialBody>("Gravity", vDestination.mainBody);
            returnPair = new KeyValuePair<string, CelestialBody>("OK", null);
            return returnPair;
        }

        // Show exit orbital predictions
        private void ShowExitOrbit(Vessel nearObject, Vessel farObject)
        {
            // Recenter map, save previous state.
            wasInMapView = MapView.MapIsEnabled;
            if (!MapView.MapIsEnabled) MapView.EnterMapView();
            log.Debug("Finding target.");
            MapObject farTarget = FindVesselBody(farObject);
            if (farTarget != null) MapView.MapCamera.SetTarget(farTarget);
            Vector3 mapCamPos = ScaledSpace.ScaledToLocalSpace(MapView.MapCamera.transform.position);
            Vector3 farTarPos = ScaledSpace.ScaledToLocalSpace(farTarget.transform.position);
            float dirScalar = Vector3.Distance(mapCamPos, farTarPos);
            log.Debug("Initializing, camera distance is " + dirScalar);

            // Initialize projection stuff.
            if (!IsPatchedConicsAvailable)
            {
                HideExitOrbit();
                return;
            }
            predictionsDrawn = true;

            log.Debug("Beginning orbital projection.");
            Vector3d exitTraj = GetJumpVelOffset(nearObject, farObject, nearBeacon);
            if (predictionGameObject != null) Destroy(predictionGameObject);
            predictionGameObject = new GameObject("OrbitRendererGameObject");
            predictionOrbitDriver = predictionGameObject.AddComponent<OrbitDriver>();
            predictionOrbitDriver.orbit.referenceBody = farObject.mainBody;
            predictionOrbitDriver.orbit = new Orbit();
            predictionOrbitDriver.referenceBody = farObject.mainBody;
            predictionOrbitDriver.upperCamVsSmaRatio = 999999;  // Took forever to figure this out - this sets at what zoom level the orbit appears.  Was causing it not to appear at small bodies.
            predictionOrbitDriver.lowerCamVsSmaRatio = 0.0001f;
            predictionOrbitDriver.orbit.UpdateFromStateVectors(farObject.orbit.pos, exitTraj, farObject.mainBody, Planetarium.GetUniversalTime());
            predictionOrbitDriver.orbit.Init();
            Vector3d p = predictionOrbitDriver.orbit.getRelativePositionAtUT(Planetarium.GetUniversalTime());
            Vector3d v = predictionOrbitDriver.orbit.getOrbitalVelocityAtUT(Planetarium.GetUniversalTime());
            predictionOrbitDriver.orbit.h = Vector3d.Cross(p, v);
            predictionOrbitDriver.updateMode = OrbitDriver.UpdateMode.TRACK_Phys;
            predictionOrbitDriver.orbitColor = Color.red;
            log.Debug("Displaying orbital projection.");
            predictionOrbitRenderer = predictionGameObject.AddComponent<OrbitRenderer>();
            predictionOrbitRenderer.SetColor(Color.red);
            predictionOrbitRenderer.vessel = this.vessel;
            predictionOrbitDriver.vessel = this.vessel;
            predictionOrbitRenderer.upperCamVsSmaRatio = 999999;
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
            else
            {
                // This draws just the first patch, similar to a Level 1 tracking station.
                predictionOrbitRenderer.driver.drawOrbit = true;
                predictionOrbitRenderer.drawIcons = OrbitRenderer.DrawIcons.OBJ_PE_AP;
                predictionOrbitRenderer.drawMode = OrbitRenderer.DrawMode.REDRAW_AND_RECALCULATE;
                predictionOrbitRenderer.enabled = true;
            }
            this.StartCoroutine(NullOrbitDriverVessels());
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

        public System.Collections.IEnumerator NullOrbitDriverVessels()
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
                    log.Debug("Scene changed, breaking Coroutine.");
                    yield break;
                }
                yield return null;
            }
            predictionOrbitRenderer.vessel = null;
            predictionOrbitDriver.vessel = null;
        }

        // Update said predictions
        private void UpdateExitOrbit(Vessel nearObject, Vessel farObject)
        {
            if (!predictionsDrawn)
                return;
            if (!IsPatchedConicsAvailable)
            {
                HideExitOrbit();
                return;
            }

            Vector3 mapCamPos = ScaledSpace.ScaledToLocalSpace(MapView.MapCamera.transform.position);
            MapObject farTarget = MapView.MapCamera.target;
            Vector3 farTarPos = ScaledSpace.ScaledToLocalSpace(farTarget.transform.position);
            float dirScalar = Vector3.Distance(mapCamPos, farTarPos);
            Vector3d exitTraj = GetJumpVelOffset(nearObject, farObject, nearBeacon);
            predictionOrbitRenderer.driver.referenceBody = farObject.mainBody;
            predictionOrbitRenderer.driver.orbit.referenceBody = farObject.mainBody;
            predictionOrbitRenderer.driver.pos = farObject.orbit.pos;
            predictionOrbitRenderer.celestialBody = farObject.mainBody;
            predictionOrbitRenderer.SetColor(Color.red);
            predictionOrbitDriver.orbit.UpdateFromStateVectors(farObject.orbit.pos, exitTraj, farObject.mainBody, Planetarium.GetUniversalTime());

            /* Direction indicator is broken/not required
            float baseWidth = 20.0f;
            double baseStart = 10;
            double baseEnd = 50;
            oDirection.transform.position = ScaledSpace.LocalToScaledSpace(far.transform.position);
            if (dirScalar / 325000 > baseWidth) baseWidth = dirScalar / 325000f;
            oDirection.SetWidth(baseWidth, 0.01f);
            if (dirScalar / 650000 > baseStart) baseStart = dirScalar / 650000;
            if (dirScalar / 130000 > baseEnd) baseEnd = dirScalar / 130000;
//          log.debug("Camera distance is " + dirScalar + " results: " + baseWidth + " " + baseStart + " " + baseEnd);
            oDirection.SetPosition(0, Vector3d.zero + exitTraj.xzy.normalized * baseStart);
            oDirection.SetPosition(1, exitTraj.xzy.normalized * baseEnd);
            oDirection.transform.eulerAngles = Vector3d.zero;*/
        }

        // Back out of orbital predictions.
        private void HideExitOrbit()
        {
            MapView.MapCamera.SetTarget(MapView.MapCamera.targets.Find((MapObject mobj) => mobj.vessel != null && mobj.vessel.GetInstanceID() == FlightGlobals.ActiveVessel.GetInstanceID()));
            if (MapView.MapIsEnabled && !wasInMapView) MapView.ExitMapView();
            wasInMapView = true;    // Not really, but this stops it from trying to force it if the user enters again.

            if (!predictionsDrawn)
                return;
            
            predictionOrbitRenderer.drawMode = OrbitRenderer.DrawMode.OFF;
            predictionOrbitRenderer.driver.drawOrbit = false;
            predictionOrbitRenderer.drawIcons = OrbitRenderer.DrawIcons.NONE;
            Destroy(predictionGameObject);
            predictionGameObject = null;
            predictionsDrawn = false;

            //Direction indicator is broken/not required
            //oDirection.enabled = false;

            /*Deprecated foreach (MapObject mobj in MapView.MapCamera.targets)
            {
                if (mobj.vessel == null) continue;
                if (mobj.vessel.GetInstanceID() == FlightGlobals.ActiveVessel.GetInstanceID())
                {
                    MapView.MapCamera.SetTarget(mobj);
                }
            }*/
        }

        /// <summary> Check if patched conics are available in the current save. </summary>
        /// <returns>True if patched conics are available</returns>
        public static bool IsPatchedConicsAvailable
        {
            get
            {
                // Get our level of tracking station
                float trackingstation_level = ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.TrackingStation);

                // Check if the tracking station knows Patched Conics
                return GameVariables.Instance.GetOrbitDisplayMode(trackingstation_level).CompareTo(
                        GameVariables.OrbitDisplayMode.PatchedConics) >= 0;
            }
        }

        // Mapview Utility
        private MapObject FindVesselBody(Vessel craft)
        {
            int cInst = craft.mainBody.GetInstanceID();
            foreach (MapObject mobj in MapView.MapCamera.targets)
            {
                if (mobj.celestialBody == null) continue;
                if (mobj.celestialBody.GetInstanceID() == cInst)
                {
                    return mobj;
                }
            }
            return null;
        }
    }
}
