using CommNet;
using CommNetManagerAPI;
using ESLDCore;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BeaconCommNet.CommNetLayer
{
    /// <summary>
    /// Extend the functionality of the KSP's CommNetwork (secondary model in the Model–view–controller sense)
    /// </summary>
	public class BeaconCommNetwork : CommNetwork
	{
		protected CommNetwork realNet;

		private Dictionary<CommNode, double> distanceOffsets = new Dictionary<CommNode, double>();

		public BeaconCommNetwork()
		{
			realNet = CommNetManagerChecker.GetCommNetManagerNetwork();
		}

        /// <summary>
        /// Edit the connectivity between two potential nodes
        /// </summary>
		[CNMAttrAndOr(CNMAttrAndOr.options.OR)]
		[CNMAttrSequence(CNMAttrSequence.options.EARLY)]
		protected override bool SetNodeConnection(CommNode a, CommNode b)
		{
			bool flag = false;
			Vessel vessel;
			if (a.TryGetVessel(out vessel) && b.TryGetVessel(out Vessel vessel2) && HasActiveBeacon(vessel) && HasActiveBeacon(vessel2))
			{
				if (a.distanceOffset != 0.0 && !distanceOffsets.ContainsKey(a))
				{
					distanceOffsets.Add(a, a.distanceOffset);
				}
				if (b.distanceOffset != 0.0 && !distanceOffsets.ContainsKey(b))
				{
					distanceOffsets.Add(b, b.distanceOffset);
				}
				a.distanceOffset = double.NegativeInfinity;
				b.distanceOffset = double.NegativeInfinity;
				flag = true;
			}
			return CommNetManagerChecker.CommNetManagerInstalled ? flag : base.SetNodeConnection(a, b);
		}

		[CNMAttrSequence(CNMAttrSequence.options.POST)]
		[CNMAttrAndOr(CNMAttrAndOr.options.AND)]
		protected override bool TryConnect(CommNode a, CommNode b, double distance, bool aCanRelay, bool bCanRelay, bool bothRelay)
		{
			bool result = true;
			if (!CommNetManagerChecker.CommNetManagerInstalled)
			{
				result = base.TryConnect(a, b, distance, aCanRelay, bCanRelay, bothRelay);
			}
			Vessel vessel;
			if (a.TryGetVessel(out vessel) && b.TryGetVessel(out Vessel vessel2) && HasActiveBeacon(vessel) && HasActiveBeacon(vessel2))
			{
				if (distanceOffsets.TryGetValue(a, out a.distanceOffset))
				{
					distanceOffsets.Remove(a);
					if (a.distanceOffset == double.NegativeInfinity)
					{
						Debug.LogWarning("ESLDBeaconCommNet: Somehow got a negInf. Setting to zero.");
						a.distanceOffset = 0.0;
					}
				}
				else
				{
					a.distanceOffset = 0.0;
				}
				if (distanceOffsets.TryGetValue(b, out b.distanceOffset))
				{
					distanceOffsets.Remove(b);
					if (b.distanceOffset == double.NegativeInfinity)
					{
						Debug.LogWarning("ESLDBeaconCommNet: Somehow got a negInf. Setting to zero.");
						b.distanceOffset = 0.0;
					}
				}
				else
				{
					b.distanceOffset = 0.0;
				}
			}
			return result;
		}

		public static bool HasActiveBeacon(Vessel vessel)
		{
			if (vessel.loaded)
			{
				return vessel.FindPartModulesImplementing<ESLDBeacon>().Any((ESLDBeacon beacon) => beacon.activated);
			}
			return vessel.protoVessel.protoPartSnapshots.Any((ProtoPartSnapshot protoPart) => protoPart.modules.Any((ProtoPartModuleSnapshot protoModule) => protoModule.moduleName == "ESLDBeacon" && protoModule.moduleValues.GetValue("activated") == "True"));
		}
	}
}
