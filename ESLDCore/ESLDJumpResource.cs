using System;
using System.Collections.Generic;
using UnityEngine;

namespace ESLDCore
{
	[Serializable]
	public class ESLDJumpResource : IConfigNode
	{
		[SerializeField]
		public string name;

		public int resID;

		[SerializeField]
		public float ratio = 1f;

		[SerializeField]
		public double fuelOnBoard = 0.0;

		[SerializeField]
		public float ECMult = 1f;

		[SerializeField]
		public float minEC = 0f;

		public bool fuelCheck = false;

		[SerializeField]
		public bool neededToBoot = true;

		public static Dictionary<string, float> HEResources = new Dictionary<string, float>
		{
			{
				"Karborundum",
				1f
			}
		};

		public ESLDJumpResource()
		{
		}

		public ESLDJumpResource(string name, float ratio = 1f, double fuelOnBoard = 0.0, bool fuelCheck = false, float ECMult = 1f, float minEC = 0f, bool neededToBoot = true)
		{
			this.name = name;
			this.ratio = ratio;
			this.fuelOnBoard = fuelOnBoard;
			if (fuelOnBoard != 0.0)
			{
				this.fuelCheck = true;
			}
			else
			{
				this.fuelCheck = fuelCheck;
			}
			if (ECMult == 1f && HEResources.ContainsKey(this.name))
			{
				this.ECMult = HEResources[this.name];
			}
			resID = PartResourceLibrary.Instance.GetDefinition(this.name).id;
		}

		public void Load(ConfigNode node)
		{
			name = node.GetValue("name");
			node.TryGetValue("ratio", ref ratio);
			fuelCheck = node.TryGetValue("fuelOnBoard", ref fuelOnBoard);
			if (!node.TryGetValue("ECMult", ref ECMult) && HEResources.ContainsKey(name))
			{
				ECMult = HEResources[name];
			}
			node.TryGetValue("minEC", ref minEC);
			node.TryGetValue("neededToBoot", ref neededToBoot);
			resID = PartResourceLibrary.Instance.GetDefinition(this.name).id;
		}

		public void Save(ConfigNode node)
		{
			node.AddValue("name", name);
			node.AddValue("fuelOnBoard", fuelOnBoard);
		}

		public double GetFuelOnBoard(Part beaconPart)
		{
			fuelOnBoard = 0.0;
			if ((UnityEngine.Object)beaconPart == (UnityEngine.Object)null)
			{
				return 0.0;
			}
			beaconPart.crossfeedPartSet.GetConnectedResourceTotals(resID, out fuelOnBoard, out double _, true);
			return fuelOnBoard;
		}
	}
}
