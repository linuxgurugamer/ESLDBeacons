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
        [KSPField(isPersistant = true)]
        public double fuelOnBoard = 0;

        // Beacon model (from part config).
        // Can be any string, but overrides cost parameters when equal to one of the original 4 models.
        [KSPField]
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

        public bool hasAMU = false;
        public bool hasHCU = false;
        public bool hasGMU = false;
        public bool hasSCU = false;
        public double massBonus = 1;

        public ESLDBeacon() { }
        public ESLDBeacon(ConfigNode configNode)
        {
            beaconModel = configNode.GetValue("beaconModel");
            if (!bool.TryParse(configNode.GetValue("activated"), out activated))
                activated = false;
            if (!float.TryParse(configNode.GetValue("gLimit"), out gLimit))
                gLimit = 0.5f;
            if (!float.TryParse(configNode.GetValue("gLimitEff"), out gLimitEff))
                gLimitEff = gLimit;
            if (!double.TryParse(configNode.GetValue("fuelOnBoard"), out fuelOnBoard))
                fuelOnBoard = 0;
            if (!float.TryParse(configNode.GetValue("coef"), out coef))
                coef = 0.001f;
            if (!float.TryParse(configNode.GetValue("massFctr"), out massFctr))
                massFctr = 0.0002f;
            if (!float.TryParse(configNode.GetValue("massExp"), out massExp))
                massExp = 1f;
            if (!int.TryParse(configNode.GetValue("distPow"), out distPow))
                distPow = 1;
            if (!float.TryParse(configNode.GetValue("baseMult"), out baseMult))
                baseMult = 0.25f;
            if (!int.TryParse(configNode.GetValue("distpenalty"), out distpenalty))
                distpenalty = 0;
            if (!float.TryParse(configNode.GetValue("jumpPrecision"), out jumpPrecision))
                jumpPrecision = 10;
            if (!int.TryParse(configNode.GetValue("techBoxInventory"), out techBoxInventory))
                techBoxInventory = 0;
        }

        public override void OnUpdate()
        {
            opFloor = findAcceptableAltitude(vessel.mainBody); // Keep updating tooltip display.
            if (FlightGlobals.getGeeForceAtPosition(vessel.GetWorldPos3D()).magnitude <= gLimitEff) Fields["neededEC"].guiActive = !activated;
            Fields["constantEC"].guiActive = activated;
            ModuleAnimateGeneric MAG = part.FindModuleImplementing<ModuleAnimateGeneric>();
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
            }
            double vfuel = 0;
            foreach (Part vpart in vessel.Parts)
            {
                foreach (PartResource vpr in vpart.Resources)
                {
                    if (vpr.resourceName == "Karborundum") vfuel += vpr.amount;
                }
            }
            fuelOnBoard = vfuel;
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
            neededEC = Math.Round((fuelOnBoard * neededMult * (FlightGlobals.getGeeForceAtPosition(vessel.GetWorldPos3D()).magnitude / baseLimit)) * getCrewBonuses(vessel, "Engineer", 0.5, 5));
            constantEC = Math.Round(fuelOnBoard / constantDiv * (FlightGlobals.getGeeForceAtPosition(vessel.GetWorldPos3D()).magnitude / baseLimit) * 100 * getCrewBonuses(vessel, "Engineer", 0.5, 5)) / 100;
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
        public bool requireResource(Vessel craft, string res, double resAmount, bool consumeResource)
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
            print("Crew bonus: Engineers on board reduce electrical usage by: " + (1 - getCrewBonuses(vessel, "Engineer", 0.5, 5)) * 100 + "%");
            print("Crew bonus: Scientists on board reduce Karborundum costs by: " + (1 - getCrewBonuses(vessel, "Scientist", 0.5, 5)) * 100 + "%");
            print("Crew bonus: Pilots on board reduce drift by: " + (1 - getCrewBonuses(vessel, "Pilot", 0.5, 5)) * 100 + "%");
            if (!requireResource(vessel, "Karborundum", 0.1, false))
            {
                ScreenMessages.PostScreenMessage("Cannot activate!  Insufficient Karborundum to initiate reaction.", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }
            if (FlightGlobals.getGeeForceAtPosition(vessel.GetWorldPos3D()).magnitude > gLimitEff) // Check our G forces.
            {
                print("Too deep in gravity well to activate!");
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
            ModuleAnimateGeneric MAG = part.FindModuleImplementing<ModuleAnimateGeneric>();
            print("Activating beacon!  Toggling MAG from " + MAG.status + "-" + MAG.Progress);
            print("EC Activation charge at " + neededEC + "(" + FlightGlobals.getGeeForceAtPosition(vessel.GetWorldPos3D()).magnitude + "/" + gLimitEff + ", " + fuelOnBoard + ")");
            if (MAG != null)
                MAG.Toggle();
        }

        [KSPEvent(name = "BeaconShutdown", active = false, guiActive = true, guiName = "Shutdown")]
        public void BeaconShutdown()
        {
            beaconStatus = "Offline";
            part.deactivate();
            activated = false;
            Fields["constantEC"].guiActive = false;
            Events["BeaconShutdown"].active = false;
            Events["BeaconInitialize"].active = true;
            ModuleAnimateGeneric MAG = part.FindModuleImplementing<ModuleAnimateGeneric>();
            print("Deactivating beacon!  Toggling MAG from " + MAG.status + "-" + MAG.Progress);
            if (MAG != null)
                MAG.Toggle();
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (activated) {
                Fields["neededEC"].guiActive = false;
                Fields["constantEC"].guiActive = true;
                Events["BeaconInitialize"].active = false;
                Events["BeaconShutdown"].active = true;
                beaconStatus = "Active";
            } else {
                beaconStatus = "Offline";
                part.deactivate();
                activated = false;
                Fields["constantEC"].guiActive = false;
                Events["BeaconShutdown"].active = false;
                Events["BeaconInitialize"].active = true;
            }
        }
    }
}
