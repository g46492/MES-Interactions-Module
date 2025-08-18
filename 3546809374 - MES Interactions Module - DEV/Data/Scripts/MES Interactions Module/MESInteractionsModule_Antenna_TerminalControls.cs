using Sandbox.Game.Localization;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using static PEPCO.MESInteractions_Session;

namespace PEPCO
{
    

    public static class MESAntenna_TerminalControls
    {
        const string IdPrefix = "MESInteractions";

        static bool Done = false;

        // just to clarify, don't store your states/values here, those should be per block and not static.

        private static readonly Dictionary<long, MyTerminalControlListBoxItem> _comboSelection = new Dictionary<long, MyTerminalControlListBoxItem>();

        public static void DoOnce(IMyModContext context, Dictionary<string,MesInteraction> Interactions)
        {
            if (Done)
                return;
            Done = true;

            CreateControls();
            CreateActions(context, Interactions);
        }

        static bool CustomVisibleCondition(IMyTerminalBlock b)
        {
            // only visible for the blocks having this gamelogic comp
            return b?.GameLogic?.GetAs<MESAntenna_Logic>() != null;
        }

        static void CreateControls()
        {
            // all the control types:
            {
                var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyRadioAntenna>(""); 
                c.SupportsMultipleBlocks = false;
                c.Visible = CustomVisibleCondition;

                MyAPIGateway.TerminalControls.AddControl<IMyRadioAntenna>(c);
            }
            {
                var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlLabel, IMyRadioAntenna>(IdPrefix + "MainLabel");
                c.Label = MyStringId.GetOrCompute("Interactions");
                c.SupportsMultipleBlocks = false;
                c.Visible = CustomVisibleCondition;

                MyAPIGateway.TerminalControls.AddControl<IMyRadioAntenna>(c);
            }

            {
                var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, IMyRadioAntenna>(IdPrefix + "InteractionsOptionsListBox");
                c.Title = MyStringId.GetOrCompute("Interaction Options");
                //c.Tooltip = MyStringId.GetOrCompute("This does some stuff!"); // Not going to use tooltip here, since I want my snarky remarks to be visible in the listbox
                c.SupportsMultipleBlocks = true;
                c.Visible = CustomVisibleCondition;

                c.VisibleRowsCount = 3;
                c.Multiselect = false; // don't get greedy, this is a listbox, not a multiselect listbox
                                       // Assuming Interactions is now:

                c.ListContent = (b, content, preSelect) =>
                {
                    var logic = b.GameLogic.GetAs<MESAntenna_Logic>();
                    if (logic == null)
                        return; // no gamelogic, no options

                    var interactions = logic.Mod.Interactions;

                    foreach (var kvp in interactions) // kvp.Key = MESInteractionId, kvp.Value = MESInteraction
                    {
                        var interaction = kvp.Value;

                        var item = new MyTerminalControlListBoxItem(
                            MyStringId.GetOrCompute($"{interaction.AntennaCall}"),
                            tooltip: MyStringId.GetOrCompute($"{interaction.AntennaCallTooltip}"),
                            userData: kvp.Key // MESInteractionId as UserData
                        );

                        content.Add(item);

                        MyTerminalControlListBoxItem testItem; // predeclare for C# 6
                        if (_comboSelection.TryGetValue(b.EntityId, out testItem) &&
                            Equals(testItem.UserData, item.UserData))
                        {
                            preSelect.Add(item);
                        }
                    }
                };

                c.ItemSelected = (b, selected) =>
                {
                    _comboSelection[b.EntityId] = selected.First();

                    // Force refresh toolbar display
                    b.ShowInToolbarConfig ^= true;
                    b.ShowInToolbarConfig ^= true;
                }; 


                MyAPIGateway.TerminalControls.AddControl<IMyRadioAntenna>(c);
            }
            {
                var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyRadioAntenna>(IdPrefix + "CallButton");
                c.Title = MyStringId.GetOrCompute("Send call");
                c.Tooltip = MyStringId.GetOrCompute("This sends the selected call!");
                c.SupportsMultipleBlocks = false;
                c.Visible = CustomVisibleCondition;
                c.Enabled = (b) =>
                {
                    // Only enable the button if there's a selection in the listbox
                    MyTerminalControlListBoxItem item;
                    return _comboSelection.TryGetValue(b.EntityId, out item) && item != null;
                };

                c.Action = (b) => {
                    MyTerminalControlListBoxItem item;
                    if (_comboSelection.TryGetValue(b.EntityId, out item))
                    {
                        var logic = b.GameLogic.GetAs<MESAntenna_Logic>();
                        if (logic != null)
                        {
                            // Call the method in the gamelogic with the selected call
                            logic.CallMESInteraction(item.UserData.ToString());
                        }
                    }
                };

                MyAPIGateway.TerminalControls.AddControl<IMyRadioAntenna>(c);
            }
        }

        static void CreateActions(
        IMyModContext context,
        Dictionary<string, MesInteraction> interactions)
        {
            if (interactions == null || interactions.Count == 0)
                return;

            int index = 0;
            foreach (var kvp in interactions) // kvp.Key = InteractionId, kvp.Value = MesInteraction
            {
                var item = kvp.Value;
                string interactionId = kvp.Key;

                var a = MyAPIGateway.TerminalControls
                    .CreateAction<IMyRadioAntenna>(IdPrefix + "Interaction_" + interactionId);

                a.Name = new StringBuilder(item.AntennaCall);
                a.ValidForGroups = false;
                a.Icon = @"Textures\GUI\Icons\Actions\SendSignal.dds";

                // Action trigger
                int capturedIndex = index; // needed to avoid closure issues
                a.Action = b =>
                {
                    var logic = b.GameLogic.GetAs<MESAntenna_Logic>();
                    if (logic != null)
                    {
                        logic.CallMESInteraction(interactionId);
                    }
                };

                // Status text in toolbar
                a.Writer = (b, sb) =>
                {
                    var lines = item.AntennaCall
                        .Replace(" ", "\n")
                        .Split('\n');

                    for (int l = 0; l < Math.Min(3, lines.Length); l++)
                    {
                        sb.Append(lines[l]);
                        if (l < 2 && l < lines.Length - 1)
                            sb.Append('\n');
                    }
                };

                a.Enabled = CustomVisibleCondition;

                MyAPIGateway.TerminalControls.AddAction<IMyRadioAntenna>(a);

                index++;
            }
        }


    }
}