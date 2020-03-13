using CommNet;

namespace BeaconCommNet.CommNetLayer
{
	/// <summary>
    /// Extend the functionality of the KSP's CommNetNetwork (co-primary model in the Model–view–controller sense; CommNet<> is the other co-primary one)
    /// </summary>
	public class BeaconCommNetNetwork : CommNetNetwork
	{
		public new static BeaconCommNetNetwork Instance
		{
			get;
			protected set;
		}

		protected override void Awake()
		{
			CommNetNetwork.Instance = this;
			CommNet = new BeaconCommNetwork();
			if (HighLogic.LoadedScene == GameScenes.TRACKSTATION)
			{
				GameEvents.onPlanetariumTargetChanged.Add(OnMapFocusChange);
			}
			GameEvents.OnGameSettingsApplied.Add(ResetNetwork);
			ResetNetwork(); // Please retain this so that KSP can properly reset
		}

		protected new void ResetNetwork()
		{
			CommNet = new BeaconCommNetwork();
			GameEvents.CommNet.OnNetworkInitialized.Fire();
		}
	}
}
