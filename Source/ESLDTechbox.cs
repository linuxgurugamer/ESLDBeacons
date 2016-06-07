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

        [KSPField(isPersistant = true, guiActive = false)]
        public string techBoxModel;

        Logger log = new Logger("ESLDCore:ESLDTechbox: ");

        [KSPEvent(name = "TechBoxOn", active = true, guiActive = true, guiName = "Activate")]
        public void TechBoxOn()
        {
            part.force_activate();
            activated = true;
            techBoxStatus = techBoxModel + " Active.";
            Events["TechBoxOn"].active = false;
            Events["TechBoxOff"].active = true;
            foreach (ESLDBeacon beacon in vessel.FindPartModulesImplementing<ESLDBeacon>())
            {
                beacon.checkOwnTechBoxes();
            }
            if (anim != null)
            {
                anim[animationName].normalizedSpeed = 1f;
                anim.Play(animationName);
            }
        }

        [KSPEvent(name = "TechBoxOff", active = false, guiActive = true, guiName = "Deactivate")]
        public void TechBoxOff()
        {
            activated = false;
            techBoxStatus = techBoxModel + " Inactive.";
            Events["TechBoxOn"].active = true;
            Events["TechBoxOff"].active = false;
            foreach (ESLDBeacon beacon in vessel.FindPartModulesImplementing<ESLDBeacon>())
            {
                beacon.checkOwnTechBoxes();
            }
            if (anim != null)
            {
                anim[animationName].normalizedSpeed = -1f;
                anim.Play(animationName);
            }
        }

        public override void OnStart(StartState state)
        {
            if (animationName != "")
            {
                anim = part.FindModelAnimators(animationName).FirstOrDefault();
                if (anim == null)
                    log.warning("Animation not found! " + animationName);
                else
                {
                    log.debug("Animation found: " + animationName);
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
        }
    }
}
