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
        public int distpenalty = 0;

        [KSPField]
        public float jumpPrecision = 10;

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

        private const string RnodeName = "RESOURCE";
        Logger log = new Logger("ESLDCore:ESLDBeacons: ");

        public ESLDBeacon() { }
        public ESLDBeacon(ConfigNode CFGnode)
        {
            this.buildFromConfigNode(CFGnode);
        }
        public ESLDBeacon(ConfigNode savenode, ConfigNode CFGnode)
        {
            this.buildFromConfigNode(CFGnode);
            this.buildFromConfigNode(savenode);
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
                gLimit = tempfloat;
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
            if (int.TryParse(node.GetValue("distpenalty"), out tempint))
                distpenalty = tempint;
            if (float.TryParse(node.GetValue("jumpPrecision"), out tempfloat))
                jumpPrecision = tempfloat;
            if (int.TryParse(node.GetValue("techBoxInventory"), out tempint))
                techBoxInventory = tempint;
            if (node.GetNodes(RnodeName).Count() > 0)
            {
                jumpResources.Clear();
                foreach (ConfigNode Rnode in node.GetNodes(RnodeName))
                {
                    string Rname = Rnode.GetValue("name");
                    float Rratio = 1;
                    double RfuelOnBoard = 0;
                    bool fuelCheck = true;
                    if (!float.TryParse(Rnode.GetValue("ratio"), out Rratio))
                        Rratio = 1; // Rratio is already 1. Replace this with a log entry. FIXME!!
                    if (!double.TryParse(Rnode.GetValue("fuelOnBoard"), out RfuelOnBoard))
                    {
                        RfuelOnBoard = 0;
                        fuelCheck = false;
                    }
                    jumpResources.Add(new ESLDJumpResource(Rname, Rratio, RfuelOnBoard, fuelCheck));
                }
            }
        }

        public override void OnUpdate()
        {
            opFloor = findAcceptableAltitude(vessel.mainBody); // Keep updating tooltip display.
            if (FlightGlobals.getGeeForceAtPosition(vessel.GetWorldPos3D()).magnitude <= gLimitEff) Fields["neededEC"].guiActive = !activated;
            Fields["constantEC"].guiActive = activated;
            /*ModuleAnimateGeneric MAG = part.FindModuleImplementing<ModuleAnimateGeneric>();
            if (MAG != null) {
                MAG.Events["Toggle"].guiActive = false;
                if (activated && MAG.Progress == 0 && !MAG.IsMoving())
                {
                    MAG.Toggle();
                }
                else if (!activated && MAG.Progress == 1 && !MAG.IsMoving())
                {
                    MAG.Toggle();
                }
            }*/
            foreach (ESLDJumpResource Jresource in jumpResources)
                Jresource.getFuelOnBoard(vessel);
        }


        public override void OnFixedUpdate()
        {
            checkOwnTechBoxes();
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

            if (!requireResource(vessel, "ElectricCharge", TimeWarp.deltaTime * constantEC , true))
            {
                ScreenMessages.PostScreenMessage("Warning: Electric Charge depleted.  Beacon has been shut down.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                BeaconShutdown();
            }
//          part.AddThermalFlux(TimeWarp.deltaTime * constantEC * 10);  // Not feasible until the fluctuation with high warp is nailed down.

            /*if (!requireResource(vessel, "Karborundum", 0.1, false))
            {
                ScreenMessages.PostScreenMessage("Warning: Karborundum depleted.  Beacon has been shut down.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                BeaconShutdown();
            }*/
        }

        // Calculate base cost in units of Karborundum before penalties for a transfer.
        public double getTripBaseCost(double tripdist, double tonnage)
        {
            double yardstick = Math.Pow(13599840256, 1 / (distPow + 1)); //Math.Sqrt(Math.Sqrt(13599840256));
            switch (beaconModel)
            {
                case "LB10":
                    massFctr = 0.001f;
                    coef = 0.01057371f;
                    distPow = 1;
                    massExp = 1f;
                    baseMult = 0f;
                    distpenalty = 0;
                    if (tripdist > 1000000000) distpenalty = 2;
                    return ((Math.Pow(tonnage, 1 + (.001 * tonnage) + distpenalty) / 10) * ((Math.Sqrt(Math.Sqrt(tripdist * (tripdist / 5E6)))) / yardstick) / tonnage * 10000) * tonnage / 2000;
                case "LB15":
                    massFctr = 0.0002f;
                    coef = 0.001057371f;
                    distPow = 1;
                    massExp = 2f;
                    baseMult = 0.35f;
                    distpenalty = 0;
                    return (700 + (Math.Pow(tonnage, 1 + (.0002 * Math.Pow(tonnage, 2))) / 10) * ((Math.Sqrt(Math.Sqrt(tripdist * (tripdist / 5E10)))) / yardstick) / tonnage * 10000) * tonnage / 2000;
                case "LB100":
                    massFctr = 0.00025f;
                    coef = 0.88650770f;
                    distPow = 3;
                    massExp = 1f;
                    baseMult = 0.25f;
                    distpenalty = 0;
                    return (500 + (Math.Pow(tonnage, 1 + (.00025 * tonnage)) / 20) * ((Math.Sqrt(Math.Sqrt(Math.Sqrt(tripdist * 25000)))) / Math.Sqrt(yardstick)) / tonnage * 10000) * tonnage / 2000;
                case "IB1":
                    massFctr = 1 / 6000;
                    coef = Convert.ToSingle(0.9 * Math.Pow((1 + tripdist * 2E11), 1 / 4));
                    distPow = 1;
                    massExp = 1f;
                    baseMult = 0f;
                    distpenalty = 0;
                    return ((((Math.Pow(tonnage, 1 + (tonnage / 6000)) * 0.9) / 10) * ((Math.Sqrt(Math.Sqrt(tripdist + 2E11))) / yardstick) / tonnage * 10000) * tonnage / 2000);
                default:
                    if ((distpenalty > 0) && (tripdist > distpenalty))
                    {
                        distpenalty = 2;
                    }
                    else
                    {
                        distpenalty = 0;
                    }
                    yardstick = Math.Pow(13599840256, 1 / Math.Pow(2, distPow + 1)); //Math.Sqrt(Math.Sqrt(13599840256));
                    return tonnage * baseMult + Math.Pow(tonnage, 1 + massFctr * Math.Pow(tonnage, massExp) + distpenalty) * Math.Pow(tripdist, 1 / Math.Pow(2, distPow)) * coef / yardstick;
            }
        }

        // Calculate AMU cost in units of Karborundum given two vessel endpoints and the tonnage of the transferring vessel.
        public double getAMUCost(Vessel near, Vessel far, double tton)
        {
            Vector3d velDiff = ESLDHailer.getJumpVelOffset(near, far, this) - far.orbit.vel;
            double comp = velDiff.magnitude;
            return Math.Round(((comp * tton) / Math.Pow(Math.Log10(comp * tton), 2)) / 2) / 100;
        }

        // Calculate how far away from a beacon the ship will arrive.
        public double getTripSpread(double tripdist)
        {
            switch (beaconModel)
            {
                case "LB10":
                    jumpPrecision = 7;
                    break;
                case "LB15":
                    jumpPrecision = 15;
                    break;
                case "LB100":
                    jumpPrecision = 80;
                    break;
                case "IB1":
                    jumpPrecision = 1.3f;
                    break;
            }
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
        public double findAcceptableAltitude(CelestialBody targetbody)
        {
            switch (beaconModel)
            {
                case "LB10":
                    gLimitEff = 1.0f;
                    break;
                case "LB15":
                    gLimitEff = 0.5f;
                    break;
                case "LB100":
                    gLimitEff = 0.1f;
                    break;
                case "IB1":
                    gLimitEff = 0.1f;
                    break;
                default:
                    gLimitEff = gLimit;
                    break;
            }
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
                fuelECMult += Jresource.fuelOnBoard * Jresource.ECMult;
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
            if (beaconModel == "IB1")
            {
                hasHCU = true;
                techBoxInventory += 2;
            }
            foreach (ESLDTechbox techbox in vessel.FindPartModulesImplementing<ESLDTechbox>())
            {
                if (techbox.activated)
                {
                    switch(techbox.techBoxModel)
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

        // Simple bool for resource checking and usage.  Returns true and optionally uses resource if resAmount of res is available.
        public bool requireResource(Vessel craft, string res, double resAmount, bool consumeResource = false)
        {
            if (!craft.loaded) return false; // Unloaded resource checking is unreliable.
            Dictionary<PartResource, double> toDraw = new Dictionary<PartResource,double>();
            double resRemaining = resAmount;
            foreach (Part cPart in craft.Parts)
            {
                foreach (PartResource cRes in cPart.Resources)
                {
                    if (cRes.resourceName != res) continue;
                    if (cRes.amount == 0) continue;
                    if (cRes.amount >= resRemaining)
                    {
                        toDraw.Add(cRes, resRemaining);
                        resRemaining = 0;
                    } else
                    {
                        toDraw.Add(cRes, cRes.amount);
                        resRemaining -= cRes.amount;
                    }
                }
                if (resRemaining <= 0) break;
            }
            if (resRemaining > 0) return false;
            if (consumeResource)
            {
                foreach (KeyValuePair<PartResource, double> drawSource in toDraw)
                {
                    drawSource.Key.amount -= drawSource.Value;
                }
            }
            return true;
        }

        // Startup sequence for beacon.
        [KSPEvent(name="BeaconInitialize", active = true, guiActive = true, guiName = "Initialize Beacon")]
        public void BeaconInitialize()
        {
            checkOwnTechBoxes();
            log.info("Crew bonus: Engineers on board reduce electrical usage by: " + (1 - getCrewBonuses(vessel, "Engineer", 0.5, 5)) * 100 + "%");
            log.info("Crew bonus: Scientists on board reduce jump costs by: " + (1 - getCrewBonuses(vessel, "Scientist", 0.5, 5)) * 100 + "%");
            log.info("Crew bonus: Pilots on board reduce drift by: " + (1 - getCrewBonuses(vessel, "Pilot", 0.5, 5)) * 100 + "%");
            foreach(ESLDJumpResource Jresource in jumpResources)
            {
                if (!requireResource(vessel, Jresource.name, double.Epsilon, false))
                {
                    ScreenMessages.PostScreenMessage("Cannot activate!  Insufficient " + Jresource.name + " to initiate reaction.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                    return;
                }
            }
            if (FlightGlobals.getGeeForceAtPosition(vessel.GetWorldPos3D()).magnitude > gLimitEff) // Check our G forces.
            {
                log.warning("Too deep in gravity well to activate!");
                string thevar = (vessel.mainBody.name == "Mun" || vessel.mainBody.name == "Sun") ? "the " : string.Empty;
                ScreenMessages.PostScreenMessage("Cannot activate!  Gravity from " + thevar + vessel.mainBody.name + " is too strong.",5.0f,ScreenMessageStyle.UPPER_CENTER);
                return;
            }
            if (vessel.altitude < (vessel.mainBody.Radius * .25f)) // Check for radius limit.
            {
                string thevar = (vessel.mainBody.name == "Mun" || vessel.mainBody.name == "Sun") ? "the " : string.Empty;
                ScreenMessages.PostScreenMessage("Cannot activate!  Beacon is too close to " + thevar + vessel.mainBody.name + ".", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }
            if (!requireResource(vessel, "ElectricCharge", neededEC, true))
            {
                ScreenMessages.PostScreenMessage("Cannot activate!  Insufficient electric power to initiate reaction.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }
//          part.AddThermalFlux(neededEC * 10);
            activated = true;
            part.force_activate();
            Fields["neededEC"].guiActive = false;
            Fields["constantEC"].guiActive = true;
            Events["BeaconInitialize"].active = false;
            Events["BeaconShutdown"].active = true;
            beaconStatus = "Active";
            log.info("EC Activation charge at " + neededEC + "(" + FlightGlobals.getGeeForceAtPosition(vessel.GetWorldPos3D()).magnitude + "/" + gLimitEff + ")");
            if(anim!=null)
            {
                anim[animationName].normalizedSpeed = 1f;
                anim.Play(animationName);
            }
            /*ModuleAnimateGeneric MAG = part.FindModuleImplementing<ModuleAnimateGeneric>();
            log.debug("Activating beacon!  Toggling MAG from " + MAG.status + "-" + MAG.Progress);
            if (MAG != null)
                MAG.Toggle();*/
        }

        [KSPEvent(name = "BeaconShutdown", active = false, guiActive = true, guiName = "Shutdown")]
        public void BeaconShutdown()
        {
            beaconStatus = "Offline";
            activated = false;
            Fields["constantEC"].guiActive = false;
            Events["BeaconShutdown"].active = false;
            Events["BeaconInitialize"].active = true;
            if (anim != null)
            {
                anim[animationName].normalizedSpeed = -1f;
                anim.Play(animationName);
            }
            /*ModuleAnimateGeneric MAG = part.FindModuleImplementing<ModuleAnimateGeneric>();
            log.debug("Deactivating beacon!  Toggling MAG from " + MAG.status + "-" + MAG.Progress);
            if (MAG != null)
                MAG.Toggle();*/
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            foreach (ESLDJumpResource Jresource in jumpResources)
            {
                ConfigNode Rnode = new ConfigNode(RnodeName);
                Rnode.AddValue("name", Jresource.name);
                Rnode.AddValue("ratio", Jresource.ratio);
                Rnode.AddValue("fuelOnBoard", Jresource.fuelOnBoard);
                node.AddNode(Rnode);
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            foreach (ConfigNode Rnode in node.GetNodes(RnodeName))
            {
                string Rname = Rnode.GetValue("name");
                float Rratio = 1;
                double RfuelOnBoard = 0;
                if (float.TryParse(Rnode.GetValue("ratio"), out Rratio))
                    Rratio = 1; // Rratio is already 1. Replace this with a log entry. FIXME!!
                bool fuelCheck = double.TryParse(Rnode.GetValue("fuelOnBoard"), out RfuelOnBoard);
                ESLDJumpResource Jresource = new ESLDJumpResource(Rname, Rratio, RfuelOnBoard);
                Jresource.fuelCheck = fuelCheck;
                jumpResources.Add(Jresource);
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
        }
    }

    public class ESLDJumpResource
    {
        public string name;
        public float ratio = 1;
        public double fuelOnBoard = 0;
        public float ECMult = 1;
        public bool fuelCheck = false;
        
        public ESLDJumpResource(string name, float ratio=1, double fuelOnBoard = 0, bool fuelCheck = false)
        {
            this.name = name;
            this.ratio = ratio;
            this.fuelOnBoard = fuelOnBoard;
            if (fuelOnBoard != 0)
                this.fuelCheck = true;
            else
                this.fuelCheck = fuelCheck;
            switch (this.name)
            {
                case "Karborundum":
                    this.ECMult = 1;
                    break;
                default:
                    this.ECMult = 1;
                    break;
            }
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
