using CommNet;
using CommNetManagerAPI;
using UnityEngine;

namespace BeaconCommNet.CommNetLayer
{
    /// <summary>
    /// This class is the key that allows to break into and customise KSP's CommNet. This is possibly the secondary model in the Model–view–controller sense
    /// </summary>
	[KSPScenario(ScenarioCreationOptions.AddToAllGames, new GameScenes[]
	{
		GameScenes.FLIGHT,
		GameScenes.TRACKSTATION,
		GameScenes.EDITOR
	})]
	public class BeaconCommNetScenario : CommNetScenario
	{
		internal CommNetNetwork CustomCommNetNetwork = null;

		public new static BeaconCommNetScenario Instance
		{
			get;
			protected set;
		}

		protected override void Start()
		{
			Instance = this;

            //Replace the CommNet network
            // Use CommNetManager's methods:
			this.SetCommNetManagerIfAvailable(typeof(BeaconCommNetNetwork), out CustomCommNetNetwork);
		}

		public override void OnAwake()
		{
			//override to turn off CommNetScenario's instance check
		}

		private void OnDestroy()
		{
			if ((Object)CustomCommNetNetwork != (Object)null)
			{
				Object.Destroy(CustomCommNetNetwork);
			}
		}
	}
}
