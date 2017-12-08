using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
//using System.Threading.Tasks;
using UnityEngine;

namespace ESLDCore
{
    public class ESLDBeacon : PartModule
    {
        // Activation animation
        [KSPField]
        public string animationName = "";

        protected Animation anim;

        // Display beacon status in right click menu.
        [KSPField(guiName = "Beacon Status", guiActive = true)]
        public string beaconStatus;

        // Per-beacon G limit.
        [KSPField(isPersistant=true)]
        public float gLimitEff = 0.5f;

        [KSPField]
        public float gLimit = 0.5f;

        // Activated state.
        [KSPField(isPersistant = true)]
        public bool activated = false;

        // Self-reported fuel quantity.
        // Now handled by jumpResources
        //[KSPField(isPersistant = true)]
        //public double fuelOnBoard = 0;

        // Beacon model (from part config).
        // Can be any string, but overrides cost parameters when equal to one of the original 4 models.
        [KSPField(isPersistant = true)]
        public string beaconModel = string.Empty;

        // Cost equation:
        // M=tonnage, D=tripdist, f=massFctr, e=massExp, c=coef, p=distPow, b=baseMult, l=yardstick, d=2 if D>distpenalty
        // COST = M*b + M^(1+f*M^e+d) * (D/sqrt(l))^(1/2^p) * c

        // Cost parameter (from part config). Coefficient for total cost. (Does not affect base cost)
        [KSPField]
        public float coef = 0.01f;

        // Cost parameter (from part config). Coefficient for mass exponent.
        [KSPField]
        public float massFctr = 0.0002f;

        // Cost parameter (from part config). Second exponent of mass. (M^(1+f*M^e))
        [KSPField]
        public float massExp = 1f;

        // Cost parameter (from part config). Root order of distance cost. (tripdist^(1/(2^distPow)))
        [KSPField]
        public int distPow = 1;

        // Cost parameter (from part config). Coefficient for base cost from mass.
        [KSPField]
        public float baseMult = 0.25f;

        // Cost parameter (from part config). Distance beyond which cost becomes prohibitive. (Zero for infinite)
        [KSPField]
        public int distPenalty = 0;

        // Cost parameter (from part config). Cost added simply for using the beacon.
        [KSPField]
        public float baseCost = 0;

        [KSPField]
        public float jumpPrecision = 10;

        [KSPField]
        public bool canJumpSelf = false;

        [KSPField]
        public bool builtInHCU = false;

        [KSPField]
        public bool jumpTargetable = true;

        [KSPField]
        public bool needsResourcesToBoot = false;

        // Self-reported binary techbox capability.
        [KSPField(isPersistant = true)]
        public int techBoxInventory = 0;

        // Display beacon operational floor in right click menu.
        [KSPField(guiName = "Lowest Altitude", guiActive = true, guiUnits = "km")]
        public double opFloor;

        // Charge to initialize beacon.
        [KSPField(guiName = "Charge to Activate", guiActive = false, guiUnits = " EC")]
        public double neededEC;

        // Charge to run beacon.
        [KSPField(guiName = "Electric Use", guiActive = false, guiUnits = " EC/s")]
        public double constantEC;

        public List<ESLDJumpResource> jumpResources = new List<ESLDJumpResource>();

        public bool hasAMU = false;
        public bool hasHCU = false;
        public bool hasGMU = false;
        public bool hasSCU = false;
        public double massBonus = 1;

        public const string RnodeName = "RESOURCE";
        Logger log = new Logger("ESLDCore:ESLDBeacons: ");

        private int ECid;

        public ESLDBeacon() { ECid = PartResourceLibrary.Instance.GetDefinition("ElectricCharge").id; }
        public ESLDBeacon(ConfigNode CFGnode)
        {
            this.buildFromConfigNode(CFGnode);
            ECid = PartResourceLibrary.Instance.GetDefinition("ElectricCharge").id;
        }
        public ESLDBeacon(ConfigNode savenode, ConfigNode CFGnode)
        {
            this.buildFromConfigNode(CFGnode);
            this.buildFromConfigNode(savenode);
            ECid = PartResourceLibrary.Instance.GetDefinition("ElectricCharge").id;
        }

        protected void buildFromConfigNode(ConfigNode node)
        {
            bool tempbool = false;
            int tempint = 0;
            float tempfloat = 0;
            beaconModel = node.GetValue("beaconModel");
            if (bool.TryParse(node.GetValue("activated"), out tempbool))
                activated = tempbool;
            if (float.TryParse(node.GetValue("gLimit"), out tempfloat))
            {
                gLimit = tempfloat;
                gLimitEff = gLimit;
            }
            if (float.TryParse(node.GetValue("gLimitEff"), out tempfloat))
                gLimitEff = tempfloat;
            if (float.TryParse(node.GetValue("coef"), out tempfloat))
                coef = tempfloat;
            if (float.TryParse(node.GetValue("massFctr"), out tempfloat))
                massFctr = tempfloat;
            if (float.TryParse(node.GetValue("massExp"), out tempfloat))
                massExp = tempfloat;
            if (int.TryParse(node.GetValue("distPow"), out tempint))
                distPow = tempint;
            if (float.TryParse(node.GetValue("baseMult"), out tempfloat))
                baseMult = tempfloat;
            if (int.TryParse(node.GetValue("distPenalty"), out tempint))
                distPenalty = tempint;
            if (float.TryParse(node.GetValue("baseCost"), out tempfloat))
                baseCost = tempfloat;
            if (float.TryParse(node.GetValue("jumpPrecision"), out tempfloat))
                jumpPrecision = tempfloat;
            if (int.TryParse(node.GetValue("techBoxInventory"), out tempint))
                techBoxInventory = tempint;
            buildTBInventory();
            if (bool.TryParse(node.GetValue("canJumpSelf"), out tempbool))
                canJumpSelf = tempbool;
            if (bool.TryParse(node.GetValue("builtInHCU"), out tempbool))
                builtInHCU = tempbool;
            if (bool.TryParse(node.GetValue("jumpTargetable"), out tempbool))
                jumpTargetable = tempbool;
            if (bool.TryParse(node.GetValue("needsResourcesToBoot"), out tempbool))
                needsResourcesToBoot = tempbool;
            if (node.GetNodes(RnodeName).Count() > 0)
            {
                jumpResources.Clear();
                foreach (ConfigNode Rnode in node.GetNodes(RnodeName))
                {
                    jumpResources.Add(new ESLDJumpResource(Rnode));
                }
            }
        }

        public override void OnUpdate()
        {
            if (FlightGlobals.getGeeForceAtPosition(vessel.GetWorldPos3D()).magnitude <= gLimitEff) Fields["neededEC"].guiActive = !activated;
            //Fields["constantEC"].guiActive = activated;
            foreach (ESLDJumpResource Jresource in jumpResources)
                Jresource.getFuelOnBoard(vessel);
        }

        public override void OnFixedUpdate()
        {
            opFloor = findAcceptableAltitude(); // Keep updating tooltip display. Also, needed EC.
            if (activated)
            {
                if (FlightGlobals.getGeeForceAtPosition(vessel.GetWorldPos3D()).magnitude > gLimitEff)
                {
                    ScreenMessages.PostScreenMessage("Warning: Too deep in gravity well.  Beacon has been shut down for safety.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                    BeaconShutdown();
                }
                if (vessel.altitude < (vessel.mainBody.Radius * 0.25f))
                {
                    string thevar = (vessel.mainBody.name == "Mun" || vessel.mainBody.name == "Sun") ? "the " : string.Empty;
                    ScreenMessages.PostScreenMessage("Warning: Too close to " + thevar + vessel.mainBody.name + ".  Beacon has been shut down for safety.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                    BeaconShutdown();
                }
                double ECgotten = part.RequestResource("ElectricCharge", constantEC * TimeWarp.fixedDeltaTime);
                if (!Double.IsNaN(constantEC) && (constantEC > 0) &&
                    ECgotten <= constantEC * TimeWarp.fixedDeltaTime * 0.9)
                {
                    ScreenMessages.PostScreenMessage("Warning: Electric Charge depleted.  Beacon has been shut down.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                    log.debug("Requested: " + constantEC + ", got: " + ECgotten / TimeWarp.fixedDeltaTime);
                    BeaconShutdown();
                }
                if (needsResourcesToBoot)
                {
                    foreach (ESLDJumpResource Jresource in jumpResources)
                    {
                        if (Jresource.neededToBoot && !requireResource(vessel, Jresource.resID, double.Epsilon, false))
                        {
                            ScreenMessages.PostScreenMessage("Warning: " + Jresource.name + " depleted.  Beacon has been shut down.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                            BeaconShutdown();
                            break;
                        }
                    }
                }
                //part.AddThermalFlux(TimeWarp.fixedDeltaTime * constantEC * 10);  // Not feasible until the fluctuation with high warp is nailed down.
            }
        }

        // Calculate base cost in units of Karborundum before penalties for a transfer.
        public double getTripBaseCost(double tripdist, double tonnage)
        {
            double yardstick = Math.Pow(13599840256, 1 / (distPow + 1)); //Math.Sqrt(Math.Sqrt(13599840256));
            float distPenaltyCost = 0;
            switch (beaconModel)
            {
                case "IB1":
                    massFctr = 1 / 6000;
                    coef = Convert.ToSingle(0.9 * Math.Pow((1 + tripdist * 2E11), 1 / 4));
                    distPow = 1;
                    massExp = 1f;
                    baseMult = 0f;
                    distPenalty = 0;
                    return ((((Math.Pow(tonnage, 1 + (tonnage / 6000)) * 0.9) / 10) * ((Math.Sqrt(Math.Sqrt(tripdist + 2E11))) / yardstick) / tonnage * 10000) * tonnage / 2000);
                default:
                    if ((distPenalty > 0) && (tripdist > distPenalty))
                    {
                        distPenaltyCost = 2;
                    }
                    else
                    {
                        distPenaltyCost = 0;
                    }
                    yardstick = Math.Pow(13599840256, 1 / Math.Pow(2, distPow + 1)); //Math.Sqrt(Math.Sqrt(13599840256));
                    return tonnage * baseMult + Math.Pow(tonnage, 1 + massFctr * Math.Pow(tonnage, massExp) + distPenaltyCost) * Math.Pow(tripdist, 1 / Math.Pow(2, distPow)) * coef / yardstick + baseCost;
            }
        }

        // Calculate AMU cost in units of Karborundum given two vessel endpoints and the tonnage of the transferring vessel.
        public double getAMUCost(Vessel near, Vessel far, double tton)
        {
            Vector3d velDiff = ESLDHailer.getJumpVelOffset(near, far, this) - far.orbit.vel;
            double comp = velDiff.magnitude;
            return Math.Round(((comp * tton) / Math.Pow(Math.Log10(comp * tton), 2)) / 2) / 100;
        }

        public double getHCUCost(Vessel craft, List<Part> HCUParts = null)
        {
            double HCUCost = 0;
            if (HCUParts == null)
                HCUParts = ESLDHailer.getHCUParts(craft).Keys.ToList();
            foreach (Part vpart in HCUParts)
            {
                foreach (PartResource vres in vpart.Resources)
                {
                    if (ESLDHailer.highEnergyResources.ContainsKey(vres.resourceName) && vres.amount > 0)
                    {
                        HCUCost += (vres.info.density * vres.amount * 0.1) / 0.0058 * ESLDHailer.highEnergyResources[vres.resourceName];
                    }
                }
            }
            HCUCost += craft.GetCrewCount() * 0.9 / 1.13;
            HCUCost = Math.Round(HCUCost * 100) / 100;
            return HCUCost;
        }
        public double getHCUCost(Vessel craft, Dictionary<Part, string>.KeyCollection HCUParts = null)
        {
            return getHCUCost(craft, HCUParts.ToList());
        }

        // Calculate how far away from a beacon the ship will arrive.
        public double getTripSpread(double tripdist)
        {
            return Math.Round(Math.Log(tripdist) / Math.Log(jumpPrecision) * 10) * 100;
        }

        public double getCrewBonuses(Vessel craft, string neededTrait, double maxBenefit, int countCap)
        {
            int bonus = 0;
            int ccount = 0;
            foreach (ProtoCrewMember crew in vessel.GetVesselCrew())
            {
                if (crew.experienceTrait.Title == neededTrait) 
                {
                    bonus += crew.experienceLevel;
                    ccount += 1;
                }
            }
            if (ccount == 0) return 1;
            double bonusAvg = bonus / ccount;
            if (ccount > countCap) { ccount = countCap; }
            double endBenefit = 1 - (maxBenefit * ((ccount * bonusAvg) / 25));
            return endBenefit;
        }

        // Given a target body, get minimum ASL where beacon can function in km.
        public double findAcceptableAltitude()
        {
            return findAcceptableAltitude(vessel.mainBody);
        }

        public double findAcceptableAltitude(CelestialBody targetbody)
        {
            gLimitEff = gLimit;
            double neededMult = 10;
            double constantDiv = 50;
            double baseLimit = gLimitEff;
            if (hasGMU)
            {
                gLimitEff *= 1.25f;
                neededMult = 15;
                constantDiv = 5;
                double massOffset = (Math.Pow(Math.Abs(Math.Log(vessel.GetTotalMass() / 10, 2.25)), 4) * baseLimit) / 1500;
                massBonus = 1 - massOffset;
                if (massBonus < 0)
                {
                    massBonus = 0;
                }
            }
            double limbo = (Math.Sqrt((6.673E-11 * targetbody.Mass) / gLimitEff) - targetbody.Radius) * massBonus;
            gLimitEff = Convert.ToSingle((6.673E-11 * targetbody.Mass) / Math.Pow(limbo + targetbody.Radius, 2));
            if (limbo < targetbody.Radius * 0.25) limbo = targetbody.Radius * 0.25;
            double fuelECMult = 0;
            foreach (ESLDJumpResource Jresource in jumpResources)
                fuelECMult += Math.Max(Jresource.fuelOnBoard * Jresource.ECMult, Jresource.minEC);
            neededEC = Math.Round((fuelECMult * neededMult * (FlightGlobals.getGeeForceAtPosition(vessel.GetWorldPos3D()).magnitude / baseLimit)) * getCrewBonuses(vessel, "Engineer", 0.5, 5));
            constantEC = Math.Round(fuelECMult / constantDiv * (FlightGlobals.getGeeForceAtPosition(vessel.GetWorldPos3D()).magnitude / baseLimit) * 100 * getCrewBonuses(vessel, "Engineer", 0.5, 5)) / 100;
            return Math.Round(limbo / 1000);
        }

        public void checkOwnTechBoxes()
        {
            hasAMU = false;
            hasHCU = false;
            hasGMU = false;
            hasSCU = false;
            techBoxInventory = 0;
            if (builtInHCU)
            {
                hasHCU = true;
                techBoxInventory += 2;
            }
            if (vessel == null)
                return;
            foreach (ESLDTechbox techbox in vessel.FindPartModulesImplementing<ESLDTechbox>())
            {
                if (techbox.activated || techbox.alwaysActive)
                {
                    switch (techbox.techBoxModel.ToUpper())
                    {
                        case "AMU":
                            if (!hasAMU) techBoxInventory += 1;
                            hasAMU = true;
                            break;
                        case "HCU":
                            if (!hasHCU) techBoxInventory += 2;
                            hasHCU = true;
                            break;
                        case "GMU":
                            if (!hasGMU) techBoxInventory += 4;
                            hasGMU = true;
                            break;
                        case "SCU":
                            if (!hasSCU) techBoxInventory += 8;
                            hasSCU = true;
                            break;
                    }
                }
            }
        }

        protected void buildTBInventory()
        {
            hasSCU = (techBoxInventory) >= 8;
            hasGMU = (techBoxInventory % 8) >= 4;
            hasHCU = (techBoxInventory % 4) >= 2;
            hasAMU = (techBoxInventory % 2) >= 1;
        }

        // Simple bool for resource checking and usage.  Returns true and optionally uses resource if resAmount of res is available.
        public bool requireResource(Vessel craft, string res, double resAmount, bool consumeResource = false)
        {
            return requireResource(craft, PartResourceLibrary.Instance.GetDefinition(res).id, resAmount, consumeResource);
        }
        public bool requireResource(Vessel craft, int resID, double resAmount, bool consumeResource = false)
        {
            if (Double.IsNaN(resAmount))
            {
                log.error("NaN requested.");
                return true;
            }

            if (!craft.loaded)
            {
                log.warning("Tried to get resources of unloaded craft.");
                return false; // Unloaded resource checking is unreliable.
            }

            double amount;
            double maxamount;
            part.GetConnectedResourceTotals(resID, out amount, out maxamount);

            if (amount < resAmount)
                return false;

            if (consumeResource)
                part.RequestResource(resID, resAmount);
            return true;
        }

        public bool CheckInitialize()
        {
            if (needsResourcesToBoot)
            {
                foreach (ESLDJumpResource Jresource in jumpResources)
                {
                    if (Jresource.neededToBoot && !requireResource(vessel, Jresource.resID, double.Epsilon, false))
                    {
                        ScreenMessages.PostScreenMessage("Cannot activate!  Insufficient " + Jresource.name + " to initiate reaction.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                        return false;
                    }
                }
            }
            if (FlightGlobals.getGeeForceAtPosition(vessel.GetWorldPos3D()).magnitude > gLimitEff) // Check our G forces.
            {
                log.warning("Too deep in gravity well to activate!");
                string thevar = (vessel.mainBody.name == "Mun" || vessel.mainBody.name == "Sun") ? "the " : string.Empty;
                ScreenMessages.PostScreenMessage("Cannot activate!  Gravity from " + thevar + vessel.mainBody.name + " is too strong.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                return false;
            }
            if (vessel.altitude < (vessel.mainBody.Radius * .25f)) // Check for radius limit.
            {
                string thevar = (vessel.mainBody.name == "Mun" || vessel.mainBody.name == "Sun") ? "the " : string.Empty;
                ScreenMessages.PostScreenMessage("Cannot activate!  Beacon is too close to " + thevar + vessel.mainBody.name + ".", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                return false;
            }
            if (!requireResource(vessel, "ElectricCharge", neededEC, true))
            {
                ScreenMessages.PostScreenMessage("Cannot activate!  Insufficient electric power to initiate reaction.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                log.debug("Couldn't activate, " + neededEC + " EC needed.");
                return false;
            }
            return true;
        }

        // Startup sequence for beacon.
        [KSPEvent(name="BeaconInitialize", active = true, guiActive = true, guiName = "Initialize Beacon")]
        public void BeaconInitialize()
        {
            if (!activated)
            {
                checkOwnTechBoxes();
                log.info("Crew bonus: Engineers on board reduce electrical usage by: " + (1 - getCrewBonuses(vessel, "Engineer", 0.5, 5)) * 100 + "%");
                log.info("Crew bonus: Scientists on board reduce jump costs by: " + (1 - getCrewBonuses(vessel, "Scientist", 0.5, 5)) * 100 + "%");
                log.info("Crew bonus: Pilots on board reduce drift by: " + (1 - getCrewBonuses(vessel, "Pilot", 0.5, 5)) * 100 + "%");
                if (!CheckInitialize())
                    return;
                //          part.AddThermalFlux(neededEC * 10);
                activated = true;
                part.force_activate();
                opFloor = findAcceptableAltitude(); // Keep updating tooltip display. Also, needed EC.
                Fields["neededEC"].guiActive = false;
                Fields["constantEC"].guiActive = true;
                Events["BeaconInitialize"].active = false;
                Events["BeaconShutdown"].active = true;
                Actions["BeaconInitializeAction"].active = false;
                Actions["BeaconShutdownAction"].active = true;
                beaconStatus = "Active";
                log.info("EC Activation charge at " + neededEC + "(" + FlightGlobals.getGeeForceAtPosition(vessel.GetWorldPos3D()).magnitude + "/" + gLimitEff + ")");
                if (anim != null)
                {
                    anim[animationName].normalizedSpeed = 1f;
                    anim.Play(animationName);
                }
            }
            else
                log.warning("Can only initialize when shut down!");
        }

        [KSPEvent(name = "BeaconShutdown", active = false, guiActive = true, guiName = "Shutdown")]
        public void BeaconShutdown()
        {
            if (activated)
            {
                beaconStatus = "Offline";
                activated = false;
                Fields["constantEC"].guiActive = false;
                Events["BeaconShutdown"].active = false;
                Events["BeaconInitialize"].active = true;
                Actions["BeaconInitializeAction"].active = true;
                Actions["BeaconShutdownAction"].active = false;
                if (anim != null)
                {
                    anim[animationName].normalizedSpeed = -1f;
                    anim.Play(animationName);
                }
            }
            else
                log.warning("Can only shut down when activated!");
        }

        [KSPAction("Toggle Beacon")]
        public void toggleBeaconAction(KSPActionParam param)
        {
            if (activated)
                BeaconShutdown();
            else
                BeaconInitialize();
        }
        [KSPAction("Initialize Beacon")]
        public void BeaconInitializeAction(KSPActionParam param)
        {
            if (!activated)
                BeaconInitialize();
            else
                log.warning("Can only initialize when shut down!");
        }
        [KSPAction("Shutdown Beacon")]
        public void BeaconShutdownAction(KSPActionParam param)
        {
            if (activated)
                BeaconShutdown();
            else
                log.warning("Can only shut down when activated!");
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            checkOwnTechBoxes();
            foreach (ConfigNode Rnode in node.GetNodes(RnodeName))
            {
                jumpResources.Add(new ESLDJumpResource(Rnode));
            }
            // Not sure if there should be a default resource...
            if (jumpResources.Count == 0 && (beaconModel == "LB10" || beaconModel == "LB15" || beaconModel == "LB100" || beaconModel == "IB1"))
            {
                log.warning("Generating new ESLDJumpResource for legacy beacon savefile");
                jumpResources.Add(new ESLDJumpResource("Karborundum", ratio: 1));
            }

            if (activated) {
                Fields["neededEC"].guiActive = false;
                Fields["constantEC"].guiActive = true;
                Events["BeaconInitialize"].active = false;
                Events["BeaconShutdown"].active = true;
                beaconStatus = "Active";
            } else {
                beaconStatus = "Offline";
                activated = false;
                Fields["constantEC"].guiActive = false;
                Events["BeaconShutdown"].active = false;
                Events["BeaconInitialize"].active = true;
            }
        }

        public override void OnSave(ConfigNode node)
        {
            checkOwnTechBoxes();
            if (node.HasValue("techBoxInventory"))
                node.SetValue("techBoxInventory", techBoxInventory.ToString());
            foreach (ESLDJumpResource Jresource in jumpResources)
                node.AddNode(Jresource.OnSave());
            base.OnSave(node);
        }

        public override void OnStart(StartState state)
        {
            if (animationName != "")
            {
                anim = part.FindModelAnimators(animationName).FirstOrDefault();
                if (anim == null)
                    log.warning("Animation not found! " + animationName);
                else
                {
                    log.debug("Animation found: " + animationName);
                    anim[animationName].wrapMode = WrapMode.Once;
                    if (activated)
                    {
                        anim[animationName].normalizedTime = 1;
                        anim.Play(animationName);
                    }
                    else
                    {
                        anim[animationName].normalizedTime = 0;
                        anim[animationName].normalizedSpeed = -10;
                        anim.Play(animationName);
                    }
                }
            }
            if (HighLogic.LoadedSceneIsFlight)
            {
                checkOwnTechBoxes();
                opFloor = findAcceptableAltitude(); // Update tooltip display.
            }
        }

        public override string GetInfo()
        {
            StringBuilder info = new StringBuilder();

            info.AppendLine("Beacon Model: " + beaconModel);
            switch (beaconModel)
            {
                case ("LB10"):
                    info.AppendLine("Best for distances below 1Gm.");
                    break;
                case ("LB15"):
                    info.AppendLine("Best for masses below 60T and distances between 100Mm and 100Gm.");
                    break;
                case ("LB100"):
                    info.AppendLine("Best for distances above 1Gm.");
                    break;
            }
            info.AppendLine("Gravity limit: " + gLimit + "g");

            if (builtInHCU || canJumpSelf || (!jumpTargetable) || (distPenalty > 0))
                info.AppendLine();
            if (builtInHCU)
                info.AppendLine("<b><color=#99ff00ff>Contains a built-in HCU</color></b>");
            if (canJumpSelf)
                info.AppendLine("<color=#99ff00ff>Can self-transfer.</color>");
            if (!jumpTargetable)
                info.AppendLine("<b><color=#FDA401>Cannot be a target beacon</color></b>");
            if (distPenalty > 0)
                info.AppendFormat("<color=#FDA401>Cost prohibitive beyond {0} km</color>", distPenalty / 1000).AppendLine();

            if (jumpResources.Count() > 0)
                info.AppendLine().AppendLine("<b><color=#99ff00ff>Requires:</color></b>");
            foreach (ESLDJumpResource Jresource in jumpResources)
                info.AppendFormat("<b>{0}:</b> Ratio: {1}", Jresource.name, Jresource.ratio.ToString("F2")).AppendLine();
            return info.ToString().TrimEnd(Environment.NewLine.ToCharArray());
        }
    }

    public class ESLDJumpResource
    {
        public string name;
        public int resID;
        public float ratio = 1;
        public double fuelOnBoard = 0;
        public float ECMult = 1;
        public float minEC = 0;
        public bool fuelCheck = false;
        public bool neededToBoot = true;

        public static Dictionary<string, float> HEResources = new Dictionary<string, float>()
        {
            {"Karborundum", 1}
        };

        public ESLDJumpResource(string name, float ratio=1, double fuelOnBoard = 0, bool fuelCheck = false, float ECMult = 1, float minEC = 0, bool neededToBoot = true)
        {
            this.name = name;
            this.ratio = ratio;
            this.fuelOnBoard = fuelOnBoard;
            if (fuelOnBoard != 0)
                this.fuelCheck = true;
            else
                this.fuelCheck = fuelCheck;
            if (ECMult == 1 && HEResources.ContainsKey(this.name))
                this.ECMult = HEResources[this.name];
            resID = PartResourceLibrary.Instance.GetDefinition(this.name).id;
        }
        public ESLDJumpResource(ConfigNode node)
        {
            float tempfloat;
            double tempdouble;
            bool tempbool;
            this.name = node.GetValue("name");
            if (float.TryParse(node.GetValue("ratio"), out tempfloat))
                this.ratio = tempfloat;
            if (double.TryParse(node.GetValue("fuelOnBoard"), out tempdouble))
            {
                this.fuelOnBoard = tempdouble;
                this.fuelCheck = true;
            }
            else
            {
                this.fuelOnBoard = 0;
                this.fuelCheck = false;
            }
            if (float.TryParse(node.GetValue("ECMult"), out tempfloat))
                this.ECMult = tempfloat;
            else if (HEResources.ContainsKey(this.name))
                this.ECMult = HEResources[this.name];
            if (float.TryParse(node.GetValue("minEC"), out tempfloat))
                this.minEC = tempfloat;
            if (bool.TryParse(node.GetValue("neededToBoot"), out tempbool))
                this.neededToBoot = tempbool;
            resID = PartResourceLibrary.Instance.GetDefinition(this.name).id;
        }

        public ConfigNode OnSave()
        {
            ConfigNode node = new ConfigNode(ESLDBeacon.RnodeName);
            node.AddValue("name", name);
            node.AddValue("ratio", ratio);
            node.AddValue("fuelOnBoard", fuelOnBoard);
            if (ECMult != 1)    { node.AddValue("ECMult", 1); }
            if (minEC!=0)       { node.AddValue("minEC", minEC); }
            if (!neededToBoot)  { node.AddValue("neededToBoot", neededToBoot); }
            return node;
        }

        public double getFuelOnBoard(Vessel vessel)
        {
            fuelOnBoard = 0;
            if (vessel == null)
                return 0;
            foreach(Part part in vessel.parts)
            {
                foreach (PartResource resource in part.Resources)
                    if (resource.resourceName == this.name) fuelOnBoard += resource.amount;
            }
            return fuelOnBoard;
        }
    }
}
