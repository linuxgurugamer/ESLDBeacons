using System.Collections.Generic;

namespace ESLDCore
{
	public interface IBeacon
	{
		List<ESLDJumpResource> JumpResources
		{
			get;
		}

		bool UnsafeTransfer
		{
			get;
		}

		bool CarriesVelocity
		{
			get;
		}

		string Description
		{
			get;
		}

		float PathGLimit
		{
			get;
		}

		Vessel Vessel
		{
			get;
		}

		float GetTripBaseCost(float tripdist, float tonnage);

		float GetTripSpread(float tripdist);

		float GetTripFinalCost(float baseCost, Vessel target, Vessel destination, float tonnage, List<Part> HCUParts = null);

		float GetCrewBonuses(string neededTrait, float maxBenefit, int countCap);

		bool RequireResource(string res, double resAmount, bool consumeResource = false);

		bool RequireResource(int resID, double resAmount, bool consumeResource = false);

		List<string> GetCostModifiers(Vessel target, Vessel destination, float tonnage, List<Part> HCUParts = null);

		void Warp(Vessel target, Vessel destination, float precision, List<Part> unsafeParts = null);
	}
}
