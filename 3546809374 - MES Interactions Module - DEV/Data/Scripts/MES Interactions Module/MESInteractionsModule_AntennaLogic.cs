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
    // For more info about the gamelogic comp see https://github.com/THDigi/SE-ModScript-Examples/blob/master/Data/Scripts/Examples/BasicExample_GameLogicAndSession/GameLogic.cs
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_RadioAntenna), false)]
    public class MESAntenna_Logic : MyGameLogicComponent
    {
        IMyRadioAntenna _antenna;

        public MESInteractions_Session Mod => MESInteractions_Session.Instance;

        Random _rand = new Random();

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
            try
            {
                if (MESInteractions_Session.SpawnerAPI == null || !MESInteractions_Session.SpawnerAPI.MESApiReady)
                {
                    Log.Error("Error", "MES API is not ready. Please try again later.");
                    return;
                }

                int callIndex = -1;
                if (!int.TryParse(index, out callIndex) || callIndex < 0 || callIndex >= Mod.Interactions.Count)
                {
                    Log.Error("Error", "Invalid interaction index.");
                    return;
                }

                if (_antenna.Enabled == false || _antenna.EnableBroadcasting == false || _antenna.OwnerId == 0) return; // No owner, do nothing

                string callString = "Meeep";

                var RadioCalls = Mod.Interactions[callIndex].RadioCalls;
                if (RadioCalls.Count > 0)
                {
                    var randomCall = _rand.Next(0, RadioCalls.Count);
                    callString = RadioCalls[randomCall];

                    // For simplicity, get the current player's name
                    string playerName = MyAPIGateway.Session.Player?.DisplayName ?? "Nobody";

                    var commandProfileIds = Mod.Interactions[callIndex].CommandProfileIds;
                    var antennaPosition = _antenna.WorldMatrix.Translation;
                    var antennaRadius = _antenna.Radius;
                    var antennaOwner = _antenna.OwnerId;

                    MyAPIGateway.Utilities.ShowMessage(playerName, $"{callString}");
                    MESInteractions_Session.SpawnerAPI.SendBehaviorCommand(commandProfileIds, antennaPosition, "", antennaRadius, antennaOwner);
                    Log.Info($"Radio call made: {callString} by {playerName} for interaction index: {index} with commandProfileIds {string.Join(";", commandProfileIds)}");
                }
                else
                {
                    Log.Error($"No RadioCalls found for Interaction with index: {index}");
                    return;
                }

                

            }
            catch (Exception ex)
            {
                Log.Error("Error", $"An error occurred while trying to call: {ex.Message}");
            }

        }

    }
}