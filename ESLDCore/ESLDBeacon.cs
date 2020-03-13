using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ESLDCore
{
	public class ESLDBeacon : PartModule, IBeacon
	{
        // Activation animation
		[KSPField]
		public string animationName = "";

		protected Animation anim;

        // Display beacon status in right click menu.
		[KSPField(guiName = "Beacon Status", guiActive = true)]
		public string beaconStatus;

        // Per-beacon G limit.
		[KSPField(isPersistant = true)]
		public float gLimitEff = 0.5f;

		[KSPField]
		public float gLimit = 0.5f;

        // Activated state.
		[KSPField(isPersistant = true)]
		public bool activated = false;

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
		public float distPow = 1f;

        // Cost parameter (from part config). Coefficient for base cost from mass.
		[KSPField]
		public float baseMult = 0.25f;

        // Cost parameter (from part config). Distance beyond which cost becomes prohibitive. (Zero for infinite)
		[KSPField]
		public float distPenalty = 0f;

        // Cost parameter (from part config). Cost added simply for using the beacon.
		[KSPField]
		public float baseCost = 0f;

        // Cost multiplier. Applies to entire base jump cost.
		[KSPField]
		public float multiplier = 1f;

		[KSPField]
		public float jumpPrecision = 10f;

		[KSPField]
		public bool canJumpSelf = false;

		[KSPField]
		public bool builtInHCU = false;

		[KSPField]
		public bool jumpTargetable = true;

		[KSPField]
		public bool needsResourcesToBoot = false;

        // Display beacon operational floor in right click menu.
		[KSPField(guiName = "Lowest Altitude", guiActive = true, guiUnits = "km")]
		public double opFloor;

        // Charge to initialize beacon.
		[KSPField(guiName = "Charge to Activate", guiActive = false, guiUnits = " EC")]
		public double neededEC;

        // Charge to run beacon.
		[KSPField(guiName = "Electric Use", guiActive = false, guiUnits = " EC/s")]
		public double constantEC;

		protected List<ESLDJumpResource> jumpResources = new List<ESLDJumpResource>();

		[KSPField(isPersistant = true)]
		public bool hasAMU = false;

		[KSPField(isPersistant = true)]
		public bool hasHCU = false;

		[KSPField(isPersistant = true)]
		public bool hasGMU = false;

		[KSPField(isPersistant = true)]
		public bool hasSCU = false;

		[KSPField(isPersistant = true)]
		public double massBonus = 1.0;

		public const string RnodeName = "RESOURCE";

		public const string EC = "ElectricCharge";

		Logger log = new Logger("ESLDCore:ESLDBeacons: ");

		private static int ECid = -1;

		public List<ESLDJumpResource> JumpResources => jumpResources;

		public bool UnsafeTransfer => !hasHCU;

		public bool CarriesVelocity => !hasAMU;

		public string Description => beaconModel;

		public Vessel Vessel => base.vessel;

		public float PathGLimit => gLimitEff;

		public override void OnStart(StartState state)
		{
			if (ECid < 0)
				ECid = PartResourceLibrary.Instance.GetDefinition(ESLDBeacon.EC).id;
			if ((UnityEngine.Object)base.part != (UnityEngine.Object)null && animationName != "")
			{
				anim = base.part.FindModelAnimators(animationName).FirstOrDefault();
				if ((UnityEngine.Object)anim == (UnityEngine.Object)null)
				{
					log.Warning("Animation not found! " + animationName, null);
				}
				else
				{
					log.Debug("Animation found: " + animationName, null);
					anim[animationName].wrapMode = WrapMode.Once;
					if (activated)
					{
						anim[animationName].normalizedTime = 1f;
						anim.Play(animationName);
					}
					else
					{
						anim[animationName].normalizedTime = 0f;
						anim[animationName].normalizedSpeed = -10f;
						anim.Play(animationName);
					}
				}
			}
			if (HighLogic.LoadedSceneIsFlight)
			{
				CheckOwnTechBoxes();
                opFloor = FindAcceptableAltitude(); // Update tooltip display.
			}
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);
            if (this.part == null) return;
            log.Info("RnodeName: " + RnodeName);
            log.Info("node: " + node.ToString());
            jumpResources = SerializationHelper.LoadObjects<ESLDJumpResource>(this, RnodeName, node);

			if (!node.HasValue("gLimitEff"))
				gLimitEff = gLimit;

            // Not sure if there should be a default resource...
			if (jumpResources.Count == 0 && (beaconModel == "LB10" || beaconModel == "LB15" || beaconModel == "LB100" || beaconModel == "IB1"))
			{
				log.Warning("Generating new ESLDJumpResource for legacy beacon savefile", null);
				jumpResources.Add(new ESLDJumpResource("Karborundum", 1f, 0.0, false, 1f, 0f, true));
			}
			SetFieldsEventsActions(activated);
		}

		public override void OnSave(ConfigNode node)
		{
			CheckOwnTechBoxes();
			base.OnSave(node);
			for (int i = 0; i < jumpResources.Count; i++)
			{
				ConfigNode node2 = node.AddNode(RnodeName);
				jumpResources[i].Save(node2);
			}
		}

		public override void OnFixedUpdate()
		{
			for (int i = jumpResources.Count - 1; i >= 0; i--)
                jumpResources[i].GetFuelOnBoard(part);
            opFloor = FindAcceptableAltitude(); // Keep updating tooltip display. Also, needed EC.
			if (activated)
			{
				if (FlightGlobals.getGeeForceAtPosition(base.vessel.GetWorldPos3D()).magnitude > (double)gLimitEff)
				{
					ScreenMessages.PostScreenMessage("Warning: Too deep in gravity well.  Beacon has been shut down for safety.", 5f, ScreenMessageStyle.UPPER_CENTER);
					BeaconShutdown();
				}
                if (vessel.altitude < (vessel.mainBody.Radius * 0.25f))
				{
					string str = (base.vessel.mainBody.name == "Mun" || base.vessel.mainBody.name == "Sun") ? "the " : string.Empty;
					ScreenMessages.PostScreenMessage("Warning: Too close to " + str + base.vessel.mainBody.name + ".  Beacon has been shut down for safety.", 5f, ScreenMessageStyle.UPPER_CENTER);
					BeaconShutdown();
				}
				double ECgotten = base.part.RequestResource(ESLDBeacon.EC, constantEC * (double)TimeWarp.fixedDeltaTime);
				if (!double.IsNaN(constantEC) && constantEC > 0.0 && ECgotten <= constantEC * (double)TimeWarp.fixedDeltaTime * 0.9)
				{
					ScreenMessages.PostScreenMessage("Warning: Electric Charge depleted.  Beacon has been shut down.", 5f, ScreenMessageStyle.UPPER_CENTER);
					log.Debug("Requested: " + constantEC + ", got: " + ECgotten / (double)TimeWarp.fixedDeltaTime, null);
					BeaconShutdown();
				}
				if (needsResourcesToBoot)
				{
					foreach (ESLDJumpResource jumpResource in jumpResources)
					{
						if (jumpResource.neededToBoot && !RequireResource(jumpResource.resID, double.Epsilon, false))
						{
							ScreenMessages.PostScreenMessage("Warning: " + jumpResource.name + " depleted.  Beacon has been shut down.", 5f, ScreenMessageStyle.UPPER_CENTER);
							BeaconShutdown();
							break;
						}
					}
				}
			}
		}

		public void Warp(Vessel target, Vessel destination, float precision, List<Part> unsafeParts = null)
		{
			float tripdist = Vector3.Distance(base.vessel.GetWorldPos3D(), destination.GetWorldPos3D());
			float totalMass = target.GetTotalMass();
			float cost = GetTripFinalCost(GetTripBaseCost(tripdist, totalMass), target, destination, totalMass, unsafeParts);
			HailerGUI.PathCheck pathCheck = new HailerGUI.PathCheck(base.vessel, destination, gLimitEff);
			if (pathCheck.clear)
			{
				if (jumpResources.All(res => RequireResource(res.resID, (double)(res.ratio * cost), false)))
				{
					jumpResources.All(res => RequireResource(res.resID, (double)(res.ratio * cost), true));

					HailerButton.Instance.Dazzle();

					Vector3d transferVelOffset = GetJumpVelOffset(target, destination);
					if (hasAMU)
						transferVelOffset = destination.orbit.vel;

					Vector3d vector3d = (UnityEngine.Random.onUnitSphere + UnityEngine.Random.insideUnitSphere) / 2f * precision;
					// Making the spread less likely to throw you outside the SoI of the body.
					if ((destination.orbit.pos + vector3d).magnitude > destination.mainBody.sphereOfInfluence)
					{
						vector3d = -vector3d;// Negative random is equally random.
					}
					OrbitDriver orbitDriver = target.orbitDriver;
					Orbit orbit = orbitDriver.orbit;
					Orbit orbit2 = new Orbit(orbit.inclination, orbit.eccentricity, orbit.semiMajorAxis, orbit.LAN, orbit.argumentOfPeriapsis, orbit.meanAnomalyAtEpoch, orbit.epoch, orbit.referenceBody);
					orbit2.UpdateFromStateVectors(destination.orbit.pos + vector3d, transferVelOffset, destination.mainBody, Planetarium.GetUniversalTime());
					target.Landed = false;
					target.Splashed = false;
					target.landedAt = string.Empty;
					OrbitPhysicsManager.HoldVesselUnpack(60);
					List<Vessel> vessels = FlightGlobals.Vessels;
					foreach (Vessel item in vessels.AsEnumerable())
					{
						if (!item.packed)
							item.GoOnRails();

					}
					CelestialBody referenceBody = target.orbitDriver.orbit.referenceBody;
					orbit.inclination = orbit2.inclination;
					orbit.eccentricity = orbit2.eccentricity;
					orbit.semiMajorAxis = orbit2.semiMajorAxis;
					orbit.LAN = orbit2.LAN;
					orbit.argumentOfPeriapsis = orbit2.argumentOfPeriapsis;
					orbit.meanAnomalyAtEpoch = orbit2.meanAnomalyAtEpoch;
					orbit.epoch = orbit2.epoch;
					orbit.referenceBody = orbit2.referenceBody;
					orbit.Init();
					orbit.UpdateFromUT(Planetarium.GetUniversalTime());
					if ((UnityEngine.Object)orbit.referenceBody != (UnityEngine.Object)orbit2.referenceBody)
					{
						orbitDriver.OnReferenceBodyChange?.Invoke(orbit2.referenceBody);
					}
					target.orbitDriver.pos = target.orbit.pos.xzy;
					target.orbitDriver.vel = target.orbit.vel;
					if ((UnityEngine.Object)target.orbitDriver.orbit.referenceBody != (UnityEngine.Object)referenceBody)
					{
						GameEvents.onVesselSOIChanged.Fire(new GameEvents.HostedFromToAction<Vessel, CelestialBody>(target, referenceBody, target.orbitDriver.orbit.referenceBody));
					}
					if (UnsafeTransfer)
					{
						if (unsafeParts == null)
						{
							unsafeParts = HailerGUI.GetHCUParts(target).Keys.ToList();
						}
						for (int i = unsafeParts.Count - 1; i >= 0; i--)
						{
							unsafeParts[i].explosionPotential = Mathf.Max(1f, unsafeParts[i].explosionPotential);
							unsafeParts[i].explode();
						}
						for (int i = base.vessel.parts.Count - 1; i >= 0; i--)
						{
							Part part = base.vessel.parts[i];
							for (int j = part.protoModuleCrew.Count - 1; j >= 0; j--)
							{
								ProtoCrewMember protoCrewMember = part.protoModuleCrew[j];
								ScreenMessages.PostScreenMessage(protoCrewMember.name + " was killed in transit!", 5f, ScreenMessageStyle.UPPER_CENTER);
								base.vessel.parts[i].RemoveCrewmember(protoCrewMember);
								protoCrewMember.Die();
							}
						}
					}
				}
				else
				{
					ScreenMessages.PostScreenMessage("Jump failed!  Origin beacon did not have enough fuel to execute transfer.", 5f, ScreenMessageStyle.UPPER_CENTER);
				}
			}
		}

		public float GetTripBaseCost(float tripdist, float tonnage)
		{
			return GetTripBaseCost(tripdist, tonnage, distPenalty, distPow, baseMult, massFctr, massExp, coef, baseCost, beaconModel, multiplier);
		}

		public static float GetTripBaseCost(float tripdist, float tonnage, float distPenalty, float distPow, float baseMult, float massFctr, float massExp, float coef, float baseCost, string beaconModel = "", float multiplier = 1f)
		{
			float yardstick = Mathf.Pow(13599840256, 1 / (distPow + 1)); //Math.Sqrt(Math.Sqrt(13599840256));
			float distPenaltyCost = 0f;
			if (beaconModel == "IB1")
			{
				massFctr = 0f;
				coef = Convert.ToSingle(0.9 * Math.Pow(1.0 + (double)tripdist * 200000000000.0, 0.0));
				distPow = 1f;
				massExp = 1f;
				baseMult = 0f;
				distPenalty = 0f;
				return Mathf.Pow(tonnage, 1f + tonnage / 6000f) * 0.9f / 10f * (Mathf.Sqrt(Mathf.Sqrt(tripdist + 2E+11f)) / yardstick) / tonnage * 10000f * tonnage / 2000f * multiplier;
			}
			if (distPenalty > 0f && tripdist > distPenalty)
			{
				distPenaltyCost = 2f;
			}
			yardstick = Mathf.Pow(13599840256, 1f / Mathf.Pow(2f, distPow + 1f)); //Math.Sqrt(Math.Sqrt(13599840256));
			return (tonnage * baseMult + Mathf.Pow(tonnage, 1f + massFctr * Mathf.Pow(tonnage, massExp) + distPenaltyCost) * Mathf.Pow(tripdist, 1f / Mathf.Pow(2f, distPow)) * coef / yardstick + baseCost) * multiplier;
		}

		public float GetTripFinalCost(float baseCost, Vessel target, Vessel destination, float tonnage, List<Part> HCUParts = null)
			=> GetTripFinalCost(baseCost, base.vessel, target, destination, tonnage, hasSCU, hasAMU, hasHCU, HCUParts, multiplier);
		public static float GetTripFinalCost(float baseCost, Vessel beacon, Vessel target, Vessel destination, float tonnage, bool hasSCU, bool hasAMU, bool hasHCU, List<Part> HCUParts = null, float multiplier = 1f)
		{
			float cost = baseCost;
			if (hasSCU)
			{
				cost *= 0.9f;
			}
			cost *= GetCrewBonuses(beacon, "Scientist", 0.5f, 5);
			float distance = Vector3.Distance(beacon.GetWorldPos3D(), target.GetWorldPos3D());
			float relVelocity = Vector3.Magnitude(beacon.obt_velocity - target.obt_velocity);
			float driftPenalty = GetDriftPenalty(distance, relVelocity, GetCrewBonuses(target, "Pilot", 0.5f, 5));
			cost += cost * (driftPenalty * 0.01f);
			if (hasAMU)
			{
				cost += GetAMUCost(target, destination, tonnage, multiplier);
			}
			if (hasHCU)
			{
				cost += GetHCUCost(target, HCUParts, multiplier);
			}
			return cost;
		}

		public static float GetDriftPenalty(float distance, float relVelocity, float crewBonusMultiplier)
		{
			float driftpenalty = Mathf.Pow(distance / 200f, 2f) + Mathf.Pow(relVelocity, 1.5f) * crewBonusMultiplier;
			if (driftpenalty < 1f)
			{
				driftpenalty = 0f;
			}
			return driftpenalty;
		}

		public List<string> GetCostModifiers(Vessel target, Vessel destination, float tonnage, List<Part> HCUParts = null)
		{
			return GetCostModifiers(base.vessel, target, destination, tonnage, hasSCU, hasAMU, hasHCU, jumpResources, HCUParts, multiplier);
		}

		public static List<string> GetCostModifiers(Vessel beacon, Vessel target, Vessel destination, float tonnage, bool hasSCU, bool hasAMU, bool hasHCU, List<ESLDJumpResource> jumpResources, List<Part> HCUParts = null, float multiplier = 1f)
		{
			List<string> list = new List<string>();
			if (hasSCU)
			{
				list.Add("Superconducting Coil Array reduces cost by 10%.");
			}
			float crewBonuses = GetCrewBonuses(beacon, "Scientist", 0.5f, 5);
			if (crewBonuses < 1f)
			{
				list.Add($"Scientists on beacon vessel reduce cost by {1f - crewBonuses:P0}%.");
			}
			float distance = Vector3.Distance(beacon.GetWorldPos3D(), target.GetWorldPos3D());
			float relVelocity = Vector3.Magnitude(beacon.obt_velocity - target.obt_velocity);
			float driftPenalty = GetDriftPenalty(distance, relVelocity, GetCrewBonuses(target, "Pilot", 0.5f, 5));
			if (driftPenalty > 0f)
			{
				list.Add($"Relative speed and distance to beacon adds {driftPenalty:F2}%.");
			}
			if (hasAMU)
			{
				string str = "AMU Compensation adds ";
				float aMUCost = GetAMUCost(target, destination, tonnage, multiplier);
				for (int i = 0; i < jumpResources.Count; i++)
				{
					str += string.Format("{0:F2} {1}{2}", aMUCost * jumpResources[i].ratio, jumpResources[i].name, (i + 1 < jumpResources.Count) ? ", " : "");
				}
				list.Add(str + ".");
			}
			if (hasHCU)
			{
				string str2 = "HCU Shielding adds ";
				float hCUCost = GetHCUCost(target, HCUParts, multiplier);
				for (int j = 0; j < jumpResources.Count; j++)
				{
					str2 += string.Format("{0:F2} {1}{2}", hCUCost * jumpResources[j].ratio, jumpResources[j].name, (j + 1 < jumpResources.Count) ? ", " : "");
				}
				list.Add(str2 + ".");
			}
			return list;
		}

		// Calculate AMU cost in units of Karborundum given two vessel endpoints and the tonnage of the transferring vessel.
		public static float GetAMUCost(Vessel near, Vessel far, float tton, float multiplier)
		{
			float magnitude = ((Vector3)(GetJumpVelOffset(near, far) - far.orbit.vel)).magnitude;
			return magnitude * tton / Mathf.Pow(Mathf.Log10(magnitude * tton), 2f) / 2f / 100f * multiplier;
		}

		public static float GetHCUCost(Vessel vessel, IEnumerable<Part> HCUParts = null, float multiplier = 1f)
		{
			float HCUCost = 0f;
			if (HCUParts == null)
			{
				HCUParts = HailerGUI.GetHCUParts(vessel).Keys.ToList();
			}
			foreach (Part HCUPart in HCUParts)
			{
				foreach (PartResource resource in HCUPart.Resources)
				{
					if (HailerGUI.highEnergyResources.ContainsKey(resource.resourceName) && resource.amount > 0.0)
					{
						HCUCost += resource.info.density * (float)resource.amount * 0.1f / 0.0058f * HailerGUI.highEnergyResources[resource.resourceName];
					}
				}
			}
			HCUCost += (float)vessel.GetCrewCount() * 0.9f / 1.13f;
			return HCUCost * multiplier;
		}

		// Calculate how far away from a beacon the ship will arrive.
		public float GetTripSpread(float tripdist)
		{
			return GetTripSpread(tripdist, jumpPrecision);
		}

		public static float GetTripSpread(float tripdist, float jumpPrecision)
		{
			return Mathf.Round(Mathf.Log(tripdist) / Mathf.Log(jumpPrecision) * 10f) * 100f;
		}

		// Calculate Jump Velocity Offset
		public static Vector3 GetJumpVelOffset(Vessel nearObject, Vessel farObject)
		{
			Vector3 vector = farObject.orbit.vel;
			CelestialBody celestialBody = farObject.mainBody;
			while (celestialBody.flightGlobalsIndex != 0) // Kerbol
			{
				vector += celestialBody.orbit.vel;
				celestialBody = celestialBody.referenceBody;
			}
			Vector3 nearRealVelocity = nearObject.orbit.vel;
			CelestialBody nearRefbody = nearObject.mainBody;
			if (nearObject.mainBody.flightGlobalsIndex == farObject.mainBody.flightGlobalsIndex)
			{
				vector -= farObject.orbit.vel;
			}
			while (nearRefbody.flightGlobalsIndex != 0)
			{
				nearRealVelocity += nearRefbody.orbit.vel;
				nearRefbody = nearRefbody.referenceBody;
			}
			return nearRealVelocity - vector;
		}

		public float GetCrewBonuses(string neededTrait, float maxBenefit, int countCap)
		{
			return GetCrewBonuses(base.vessel, neededTrait, maxBenefit, countCap);
		}

		public static float GetCrewBonuses(Vessel vessel, string neededTrait, float maxBenefit, int countCap)
		{
			float bonus = 0f;
			int ccount = 0;
			foreach (ProtoCrewMember item in vessel.GetVesselCrew())
			{
				if (item.experienceTrait.Title == neededTrait)
				{
					bonus += (float)item.experienceLevel;
					ccount++;
				}
			}
			if (ccount == 0)
			{
				return 1f;
			}
			float bonusAvg = bonus / (float)ccount;
			if (ccount > countCap)
			{
				ccount = countCap;
			}
			return 1f - maxBenefit * ((float)ccount * bonusAvg / 25f);
		}

		// Given a target body, get minimum ASL where beacon can function in km.
		public double FindAcceptableAltitude()
		{
			return FindAcceptableAltitude(base.vessel.mainBody);
		}

		public double FindAcceptableAltitude(CelestialBody targetbody)
		{
			gLimitEff = gLimit;
			double neededMult = 10.0;
			double nconstantDivm2 = 50.0;
			double baseLimit = (double)gLimitEff;
			if (hasGMU)
			{
				gLimitEff *= 1.25f;
				neededMult = 15.0;
				nconstantDivm2 = 5.0;
				double massOffset = Math.Pow(Math.Abs(Math.Log((double)(base.vessel.GetTotalMass() / 10f), 2.25)), 4.0) * baseLimit / 1500.0;
				massBonus = 1.0 - massOffset;
				if (massBonus < 0.0)
				{
					massBonus = 0.0;
				}
			}
			double limbo = (Math.Sqrt(6.673E-11 * targetbody.Mass / (double)gLimitEff) - targetbody.Radius) * massBonus;
			gLimitEff = Convert.ToSingle(6.673E-11 * targetbody.Mass / Math.Pow(limbo + targetbody.Radius, 2.0));
			if (limbo < targetbody.Radius * 0.25)
			{
				limbo = targetbody.Radius * 0.25;
			}
			double fuelECMult = 0.0;
			foreach (ESLDJumpResource jumpResource in jumpResources)
			{
				fuelECMult += Math.Max(jumpResource.fuelOnBoard * (double)jumpResource.ECMult, (double)jumpResource.minEC);
			}
			// LGG Need to rename num7 & num8
			double num7 = fuelECMult * neededMult;
			Vector3d geeForceAtPosition = FlightGlobals.getGeeForceAtPosition(base.vessel.GetWorldPos3D());
			neededEC = Math.Round(num7 * (geeForceAtPosition.magnitude / baseLimit) * (double)GetCrewBonuses("Engineer", 0.5f, 5));

			double num8 = fuelECMult / nconstantDivm2;
			geeForceAtPosition = FlightGlobals.getGeeForceAtPosition(base.vessel.GetWorldPos3D());
			constantEC = Math.Round(num8 * (geeForceAtPosition.magnitude / baseLimit) * 100.0 * (double)GetCrewBonuses("Engineer", 0.5f, 5)) / 100.0;
			return Math.Round(limbo / 1000.0);
		}

		public void CheckOwnTechBoxes()
		{
			if (!((UnityEngine.Object)base.vessel == (UnityEngine.Object)null))
			{
				hasAMU = false;
				hasHCU = false;
				hasGMU = false;
				hasSCU = false;
				if (builtInHCU)
				{
					hasHCU = true;
				}
				foreach (ESLDTechbox item in base.vessel.FindPartModulesImplementing<ESLDTechbox>())
				{
					if (item.activated || item.alwaysActive)
					{
						switch (item.techBoxModel.ToUpper())
						{
						case "AMU":
							hasAMU = true;
							break;
						case "HCU":
							hasHCU = true;
							break;
						case "GMU":
							hasGMU = true;
							break;
						case "SCU":
							hasSCU = true;
							break;
						}
					}
				}
			}
		}

        // Simple bool for resource checking and usage.  Returns true and optionally uses resource if resAmount of res is available.
		public bool RequireResource(string res, double resAmount, bool consumeResource = false)
		{
			return RequireResource(PartResourceLibrary.Instance.GetDefinition(res).id, resAmount, consumeResource);
		}

		public bool RequireResource(int resID, double resAmount, bool consumeResource = false)
		{
			if (double.IsNaN(resAmount))
			{
				log.Error("NaN requested", null);

				return true;
			}
			if (!base.vessel.loaded)
			{
				log.Warning("Tried to get resources of unloaded craft.", null);
				return false; // Unloaded resource checking is unreliable.
			}
			base.part.GetConnectedResourceTotals(resID, out double amount, out double _, true);
			if (amount < resAmount)
			{
				return false;
			}
			if (consumeResource)
			{
				base.part.RequestResource(resID, resAmount);
			}
			return true;
		}

		public bool CheckInitialize()
		{
			if (needsResourcesToBoot)
			{
				foreach (ESLDJumpResource jumpResource in jumpResources)
				{
					if (jumpResource.neededToBoot && !RequireResource(jumpResource.resID, double.Epsilon, false))
					{
						ScreenMessages.PostScreenMessage("Cannot activate!  Insufficient " + jumpResource.name + " to initiate reaction.", 5f, ScreenMessageStyle.UPPER_CENTER);
						return false;
					}
				}
			}
			if (FlightGlobals.getGeeForceAtPosition(base.vessel.GetWorldPos3D()).magnitude > (double)gLimitEff) // Check our G forces.
			{
				log.Warning("Too deep in gravity well to activate!", null);
				string str = (base.vessel.mainBody.name == "Mun" || base.vessel.mainBody.name == "Sun") ? "the " : string.Empty;
				ScreenMessages.PostScreenMessage("Cannot activate!  Gravity from " + str + base.vessel.mainBody.name + " is too strong.", 5f, ScreenMessageStyle.UPPER_CENTER);
				return false;
			}
			if (base.vessel.altitude < base.vessel.mainBody.Radius * 0.25) // Check for radius limit.
			{
				string str2 = (base.vessel.mainBody.name == "Mun" || base.vessel.mainBody.name == "Sun") ? "the " : string.Empty;
				ScreenMessages.PostScreenMessage("Cannot activate!  Beacon is too close to " + str2 + base.vessel.mainBody.name + ".", 5f, ScreenMessageStyle.UPPER_CENTER);
				return false;
			}
			if (!RequireResource(ESLDBeacon.EC, neededEC, true))
			{
				ScreenMessages.PostScreenMessage("Cannot activate!  Insufficient electric power to initiate reaction.", 5f, ScreenMessageStyle.UPPER_CENTER);
				log.Debug("Couldn't activate, " + neededEC + " EC needed.", null);
				return false;
			}
			return true;
		}

        // Startup sequence for beacon.
		[KSPEvent(name = "BeaconInitialize", active = true, guiActive = true, guiName = "Initialize Beacon")]
		public void BeaconInitialize()
		{
			if (!activated)
			{
				CheckOwnTechBoxes();
				log.Info("Crew bonus: Engineers on board reduce electrical usage by: " + (1f - GetCrewBonuses("Engineer", 0.5f, 5)) * 100f + "%", null);
				log.Info("Crew bonus: Scientists on board reduce jump costs by: " + (1f - GetCrewBonuses("Scientist", 0.5f, 5)) * 100f + "%", null);
				log.Info("Crew bonus: Pilots on board reduce drift by: " + (1f - GetCrewBonuses("Pilot", 0.5f, 5)) * 100f + "%", null);
				if (CheckInitialize())
				{
					activated = true;
					base.part.force_activate();
					opFloor = FindAcceptableAltitude();
					SetFieldsEventsActions(true);
					log.Info("EC Activation charge at " + neededEC + "(" + FlightGlobals.getGeeForceAtPosition(base.vessel.GetWorldPos3D()).magnitude + "/" + gLimitEff + ")", null);
					PlayAnimation(1f);
				}
			}
			else
			{
				log.Warning("Can only initialize when shut down!", null);
			}
		}

		[KSPEvent(name = "BeaconShutdown", active = false, guiActive = true, guiName = "Shutdown")]
		public void BeaconShutdown()
		{
			if (activated)
			{
				activated = false;
				SetFieldsEventsActions(false);
				PlayAnimation(-1f);
			}
			else
			{
				log.Warning("Can only shut down when activated!", null);
			}
		}

		private void PlayAnimation(float speed)
		{
			if (!((UnityEngine.Object)anim == (UnityEngine.Object)null))
			{
				anim[animationName].normalizedSpeed = speed;
				anim.Play(animationName);
			}
		}

		private void SetFieldsEventsActions(bool activated)
		{
			beaconStatus = (activated ? "Active" : "Offline");
			((BaseFieldList<BaseField, KSPField>)base.Fields)["neededEC"].guiActive = !activated;
			((BaseFieldList<BaseField, KSPField>)base.Fields)["constantEC"].guiActive = activated;
			base.Events["BeaconInitialize"].active = !activated;
			base.Events["BeaconShutdown"].active = activated;
			base.Actions["BeaconInitializeAction"].active = !activated;
			base.Actions["BeaconShutdownAction"].active = activated;
		}

		[KSPAction("Toggle Beacon")]
		public void ToggleBeaconAction(KSPActionParam param)
		{
			if (activated)
			{
				BeaconShutdown();
			}
			else
			{
				BeaconInitialize();
			}
		}

		[KSPAction("Initialize Beacon")]
		public void BeaconInitializeAction(KSPActionParam param)
		{
			if (!activated)
			{
				BeaconInitialize();
			}
			else
			{
				log.Warning("Can only initialize when shut down!", null);
			}
		}

		[KSPAction("Shutdown Beacon")]
		public void BeaconShutdownAction(KSPActionParam param)
		{
			if (activated)
			{
				BeaconShutdown();
			}
			else
			{
				log.Warning("Can only shut down when activated!", null);
			}
		}

		public override string GetInfo()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("Beacon Model: " + beaconModel);
			switch (beaconModel)
			{
			case "LB10":
				stringBuilder.AppendLine("Best for distances below 1Gm.");
				break;
			case "LB15":
				stringBuilder.AppendLine("Best for masses below 60T and distances between 100Mm and 100Gm.");
				break;
			case "LB100":
				stringBuilder.AppendLine("Best for distances above 1Gm.");
				break;
			}
			stringBuilder.AppendLine("Gravity limit: " + gLimit + "g");
			if (builtInHCU || canJumpSelf || !jumpTargetable || distPenalty > 0f)
			{
				stringBuilder.AppendLine();
			}
			if (builtInHCU)
			{
				stringBuilder.AppendLine("<b><color=#99ff00ff>Contains a built-in HCU</color></b>");
			}
			if (canJumpSelf)
			{
				stringBuilder.AppendLine("<color=#99ff00ff>Can self-transfer.</color>");
			}
			if (!jumpTargetable)
			{
				stringBuilder.AppendLine("<b><color=#FDA401>Cannot be a target beacon</color></b>");
			}
			if (distPenalty > 0f)
			{
				stringBuilder.AppendFormat("<color=#FDA401>Cost prohibitive beyond {0} km</color>", distPenalty / 1000f).AppendLine();
			}
			if (jumpResources.Count() > 0)
			{
				stringBuilder.AppendLine().AppendLine("<b><color=#99ff00ff>Requires:</color></b>");
			}
			foreach (ESLDJumpResource jumpResource in jumpResources)
			{
				stringBuilder.AppendFormat("<b>{0}:</b> Ratio: {1}", jumpResource.name, jumpResource.ratio.ToString("F2")).AppendLine();
			}
			return stringBuilder.ToString().TrimEnd(Environment.NewLine.ToCharArray());
		}
	}
}
