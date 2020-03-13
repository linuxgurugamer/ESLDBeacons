using System;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ESLDCore
{
	public class ESLDTechbox : PartModule
	{
		[KSPField]
		public string animationName = "";

		private Animation anim;

		[KSPField(guiName = "Status", isPersistant = true, guiActive = true)]
		public string techBoxStatus;

		[KSPField(isPersistant = true, guiActive = false)]
		public bool activated = false;

		[KSPField(guiActive = false)]
		public string techBoxModel;

		[KSPField]
		public bool alwaysActive = false;

		private Logger log = new Logger("ESLDCore:ESLDTechbox: ");

		protected void ForceUpdateTechboxes()
		{
			foreach (ESLDBeacon item in base.vessel.FindPartModulesImplementing<ESLDBeacon>())
			{
				item.CheckOwnTechBoxes();
			}
		}

		[KSPEvent(name = "TechBoxOn", active = true, guiActive = true, guiName = "Activate")]
		public void TechBoxOn()
		{
			if (!activated)
			{
				base.part.force_activate();
				activated = true;
				ForceUpdateTechboxes();
				SetEventsActions(true);
				PlayAnimation(1f);
			}
			else
			{
				log.Warning("Can only activate when deactivated!", null);
			}
		}

		[KSPEvent(name = "TechBoxOff", active = false, guiActive = true, guiName = "Deactivate")]
		public void TechBoxOff()
		{
			if (activated)
			{
				activated = false;
				ForceUpdateTechboxes();
				SetEventsActions(false);
				PlayAnimation(-1f);
			}
			else
			{
				log.Warning("Can only deactivate when activated!", null);
			}
		}

		[KSPAction("Toggle TechBox")]
		public void ToggleTBAction(KSPActionParam param)
		{
			if (activated)
			{
				TechBoxOff();
			}
			else
			{
				TechBoxOn();
			}
		}

		[KSPAction("Activate TechBox")]
		public void ActivateTBAction(KSPActionParam param)
			=> 	TechBoxOn();

		[KSPAction("Deactivate TechBox")]
		public void DeactivateTBAction(KSPActionParam param)
			=> 	TechBoxOff();

		public override void OnStart(StartState state)
		{
			if (animationName != "")
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
			if (alwaysActive)
			{
				base.part.force_activate();
				activated = true;
				ForceUpdateTechboxes();
				SetEventsActions(true);
				base.Events["TechBoxOff"].active = false;
				base.Actions["DeactivateTBAction"].active = false;
				base.Actions["ToggleTBAction"].active = false;
				PlayAnimation(1f);
			}
		}

		private void SetEventsActions(bool activated)
		{
			techBoxStatus = techBoxModel + (activated ? " Active." : " Inactive.");
			base.Events["TechBoxOn"].active = !activated;
			base.Events["TechBoxOff"].active = activated;
			base.Actions["ActivateTBAction"].active = !activated;
			base.Actions["DeactivateTBAction"].active = activated;
		}

		private void PlayAnimation(float speed)
		{
			if (!((UnityEngine.Object)anim == (UnityEngine.Object)null))
			{
				anim[animationName].normalizedSpeed = speed;
				anim.Play(animationName);
			}
		}

		public override string GetInfo()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("Techbox Type: " + techBoxModel);
			if (alwaysActive)
			{
				stringBuilder.AppendLine("<color=#99ff00ff>Always activated</color>");
			}
			return stringBuilder.ToString().TrimEnd(Environment.NewLine.ToCharArray());
		}
	}
}
