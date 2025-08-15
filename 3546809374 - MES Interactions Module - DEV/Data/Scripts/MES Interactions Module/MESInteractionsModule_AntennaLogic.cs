using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using Digi;
using System.Linq;

namespace PEPCO
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_RadioAntenna), false)]
    public class MESAntenna_Logic : MyGameLogicComponent
    {
        IMyRadioAntenna _antenna;

        public MESInteractions_Session Mod => MESInteractions_Session.Instance;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {

            MESAntenna_TerminalControls.DoOnce(ModContext, Mod.Interactions);

            _antenna = (IMyRadioAntenna)Entity;
            if (_antenna.CubeGrid?.Physics == null)
                return; // ignore ghost/projected grids

            // stuff and things
        }


        public void CallMESInteraction(string index)
        {
            Mod.HandleMESInteraction(index, _antenna);

        }

    }
}