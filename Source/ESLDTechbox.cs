using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
//using System.Threading.Tasks;
using UnityEngine;

namespace ESLDCore
{
    public class ESLDTechbox : PartModule
    {
        [KSPField]
        public string animationName = "";

        Animation anim;

        [KSPField(guiName = "Status", isPersistant = true, guiActive = true)]
        public string techBoxStatus;

        [KSPField(isPersistant = true, guiActive = false)]
        public bool activated = false;

        [KSPField(guiActive = false)]
        public string techBoxModel;

        [KSPField]
        public bool alwaysActive = false;

        Logger log = new Logger("ESLDCore:ESLDTechbox: ");

        protected void ForceUpdateTechboxes()
        {
            if (vessel != null)
                foreach (ESLDBeacon beacon in vessel.FindPartModulesImplementing<ESLDBeacon>())
                {
                    beacon.CheckOwnTechBoxes();
                }
        }

        [KSPEvent(name = "TechBoxOn", active = true, guiActive = true, guiName = "Activate")]
        public void TechBoxOn()
        {
            if (!activated)
            {
                part.force_activate();
                activated = true;
                ForceUpdateTechboxes();
                techBoxStatus = techBoxModel + " Active.";
                Events["TechBoxOn"].active = false;
                Events["TechBoxOff"].active = !alwaysActive;
                Actions["ActivateTBAction"].active = false;
                Actions["DeactivateTBAction"].active = !alwaysActive;
                Actions["ToggleTBAction"].active = !alwaysActive;
                if (anim != null)
                {
                    anim[animationName].normalizedSpeed = 1f;
                    anim.Play(animationName);
                }
            }
            else
                log.Warning("Can only activate when deactivated!");
        }
        
        [KSPEvent(name = "TechBoxOff", active = false, guiActive = true, guiName = "Deactivate")]
        public void TechBoxOff()
        {
            if (activated)
            {
                activated = false;
                ForceUpdateTechboxes();
                techBoxStatus = techBoxModel + " Inactive.";
                Events["TechBoxOn"].active = true;
                Events["TechBoxOff"].active = false;
                Actions["ActivateTBAction"].active = true;
                Actions["DeactivateTBAction"].active = false;
                if (anim != null)
                {
                    anim[animationName].normalizedSpeed = -1f;
                    anim.Play(animationName);
                }
            }
            else
                log.Warning("Can only deactivate when activated!");
        }

        [KSPAction("Toggle TechBox")]
        public void ToggleTBAction(KSPActionParam param)
        {
            if (activated)
                TechBoxOff();
            else
                TechBoxOn();
        }
        [KSPAction("Activate TechBox")]
        public void ActivateTBAction(KSPActionParam param)
        {
            if (!activated)
                TechBoxOn();
            else
                log.Warning("Can only activate when deactivated!");
        }
        [KSPAction("Deactivate TechBox")]
        public void DeactivateTBAction(KSPActionParam param)
        {
            if (activated)
                TechBoxOff();
            else
                log.Warning("Can only deactivate when activated!");
        }

        public override void OnStart(StartState state)
        {
            if (animationName != "")
            {
                anim = part.FindModelAnimators(animationName).FirstOrDefault();
                if (anim == null)
                    log.Warning("Animation not found! " + animationName);
                else
                {
                    log.Debug("Animation found: " + animationName);
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
            if (alwaysActive)
                TechBoxOn();
            else if (activated)
            {
                part.force_activate();
                ForceUpdateTechboxes();
                techBoxStatus = techBoxModel + " Active.";
                Events["TechBoxOn"].active = false;
                Events["TechBoxOff"].active = !alwaysActive;
                Actions["ActivateTBAction"].active = false;
                Actions["DeactivateTBAction"].active = !alwaysActive;
                Actions["ToggleTBAction"].active = !alwaysActive;
            }
            else if (!activated)
            {
                techBoxStatus = techBoxModel + " Inactive.";
                Events["TechBoxOn"].active = true;
                Events["TechBoxOff"].active = false;
                Actions["ActivateTBAction"].active = true;
                Actions["DeactivateTBAction"].active = false;
            }
        }

        public override string GetInfo()
        {
            StringBuilder info = new StringBuilder();

            info.AppendLine("Techbox Type: " + techBoxModel);
            if (alwaysActive)
                info.AppendLine("<color=#99ff00ff>Always activated</color>");
            return info.ToString().TrimEnd(Environment.NewLine.ToCharArray());
        }
    }
}
