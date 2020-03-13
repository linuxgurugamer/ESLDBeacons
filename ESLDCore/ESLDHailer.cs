using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ESLDCore
{
	public class ESLDHailer : PartModule
	{
		private Logger log = new Logger("ESLDCore:ESLDHailer: ");

		private HailerGUI hailerGui = null;

		private IBeacon nearBeacon = null;

		[KSPField(guiActive = false, isPersistant = true)]
		public bool hailerActive = false;

		[KSPField(guiName = "Beacon", guiActive = false)]
		public string hasNearBeacon;

		[KSPField(guiName = "Beacon Distance", guiActive = false, guiUnits = "m", guiFormat = "F1")]
		public double nearBeaconDistance;

		[KSPField(guiName = "Drift", guiActive = false, guiUnits = "m/s", guiFormat = "F2")]
		public double nearBeaconRelVel;

		public HailerGUI AttachedGui
		{
			get
			{
				return hailerGui;
			}
			set
			{
				hailerGui = value;
				if ((Object)hailerGui == (Object)null)
				{
					base.Events["HailerGUIClose"].active = false;
					base.Events["HailerGUIOpen"].active = hailerActive;
				}
				else
				{
					base.Events["HailerGUIOpen"].active = false;
					base.Events["HailerGUIClose"].active = hailerActive;
				}
			}
		}

		[KSPEvent(name = "HailerActivate", active = true, guiActive = true, guiName = "Initialize Hailer")]
		public void HailerActivate()
		{
			base.Events["HailerActivate"].active = false;
			base.Events["HailerDeactivate"].active = true;
			base.Events["HailerGUIOpen"].active = ((Object)hailerGui == (Object)null);
			base.Events["HailerGUIClose"].active = ((Object)hailerGui != (Object)null);
			((BaseFieldList<BaseField, KSPField>)base.Fields)["hasNearBeacon"].guiActive = true;
			((BaseFieldList<BaseField, KSPField>)base.Fields)["nearBeaconDistance"].guiActive = true;
			((BaseFieldList<BaseField, KSPField>)base.Fields)["nearBeaconRelVel"].guiActive = true;
			hailerActive = true;
		}

		[KSPEvent(name = "HailerGUIOpen", active = false, guiActive = true, guiName = "Beacon Interface")]
		public void HailerGUIOpen()
		{
			base.Events["HailerGUIOpen"].active = false;
			base.Events["HailerGUIClose"].active = true;
			hailerGui = HailerGUI.ActivateGUI(base.vessel);
		}

		[KSPEvent(name = "HailerGUIClose", active = false, guiActive = true, guiName = "Close Interface")]
		public void HailerGUIClose()
		{
			base.Events["HailerGUIClose"].active = false;
			base.Events["HailerGUIOpen"].active = true;
			HailerGUI.CloseGUI(base.vessel);
		}

		[KSPEvent(name = "HailerDeactivate", active = false, guiActive = true, guiName = "Shut Down Hailer")]
		public void HailerDeactivate()
		{
			hailerActive = false;
			if (!base.vessel.FindPartModulesImplementing<ESLDHailer>().Any((ESLDHailer hailer) => hailer.hailerActive))
			{
				HailerGUI.CloseGUI(base.vessel);
			}
			base.Events["HailerDeactivate"].active = false;
			base.Events["HailerActivate"].active = true;
			base.Events["HailerGUIOpen"].active = false;
			base.Events["HailerGUIClose"].active = false;
			((BaseFieldList<BaseField, KSPField>)base.Fields)["hasNearBeacon"].guiActive = false;
			((BaseFieldList<BaseField, KSPField>)base.Fields)["nearBeaconDistance"].guiActive = false;
			((BaseFieldList<BaseField, KSPField>)base.Fields)["nearBeaconRelVel"].guiActive = false;
		}

		public override void OnUpdate()
		{
			if (hailerActive)
			{
				if ((Object)hailerGui != (Object)null)
				{
					nearBeacon = hailerGui.nearBeacon;
				}
				else
				{
					nearBeacon = LimitedBeaconSearch();
					bool flag = nearBeacon != null;
					((BaseFieldList<BaseField, KSPField>)base.Fields)["nearBeaconDistance"].guiActive = flag;
					((BaseFieldList<BaseField, KSPField>)base.Fields)["nearBeaconRelVel"].guiActive = flag;
					hasNearBeacon = (flag ? "Present" : "Not Present");
					if (flag)
					{
						nearBeaconDistance = (double)Vector3.Distance(base.vessel.GetWorldPos3D(), nearBeacon.Vessel.GetWorldPos3D());
						nearBeaconRelVel = (double)Vector3.Magnitude(base.vessel.obt_velocity - nearBeacon.Vessel.obt_velocity);
					}
				}
			}
		}

		private IBeacon LimitedBeaconSearch()
		{
			IBeacon result = null;
			float distance = float.MaxValue;
			for (int i = FlightGlobals.VesselsLoaded.Count - 1; i >= 0; i--)
			{
				float vesselDistance = Vector3.Distance(base.vessel.GetWorldPos3D(), FlightGlobals.VesselsLoaded[i].GetWorldPos3D());
				if (!(vesselDistance >= distance))
				{
					List<ESLDBeacon> beaconsOnVessel = FlightGlobals.VesselsLoaded[i].FindPartModulesImplementing<ESLDBeacon>();
					for (int j = beaconsOnVessel.Count - 1; j >= 0; j--)
					{
						if (!beaconsOnVessel[j].activated || !beaconsOnVessel[j].moduleIsEnabled || (!((Object)FlightGlobals.VesselsLoaded[i] != (Object)base.vessel) && !beaconsOnVessel[j].canJumpSelf))
						{
							j--;
							continue;
						}
						result = beaconsOnVessel[j];
						distance = vesselDistance;
						break;
					}
				}
			}
			return result;
		}

		public override void OnStart(StartState state)
		{
			if (hailerActive)
			{
				HailerActivate();
			}
		}
	}
}
