using Digi;
using Digi.NetworkLib;
using ModularEncountersSystems.API;
using Sandbox.Game;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace PEPCO
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class MESInteractions_Session : MySessionComponentBase
    {
        public static MESInteractions_Session Instance;

        public static MESApi SpawnerAPI;

        // Toggle for logging; set to false in release builds
        public bool _debug = false;

        // All loaded interactions from all mods that provide a config
        public readonly Dictionary<string, MesInteraction> Interactions = new Dictionary<string, MesInteraction>();

        public static readonly string version = "1755283354";

        public const ushort NetworkId = (ushort)(3547952468 % ushort.MaxValue); // Using the prod steam id

        public Network Net;

        MESInteractions_NetworkPackage MESInteractionsPacket;

        Random _rand = new Random();

        public override void LoadData()
        {
            _debug = ModContext.ModName.EndsWith("- DEV");
            Log.Info($"Is DEV mod: {_debug}\nVersion: {version}");

            Instance = this;
            SpawnerAPI = new MESApi();
            CollectConfig();

            Net = new Network(NetworkId, ModContext.ModName);
            //Net.SerializeTest = true;
            MESInteractionsPacket = new MESInteractions_NetworkPackage();
            MESInteractions_NetworkPackage.OnReceive += MESInteractions_OnReceive;



        }

        protected override void UnloadData()
        {
            Interactions.Clear();
            SpawnerAPI = null;
            Instance = null;
            MESInteractions_NetworkPackage.OnReceive -= MESInteractions_OnReceive;
        }

        public void HandleMESInteraction(string MESInteractionId, IMyRadioAntenna antenna)
        {
            try
            {

                if (string.IsNullOrWhiteSpace(MESInteractionId)) // Check if the index is valid
                {
                    Log.Error($"Error: Invalid interaction index. value: {MESInteractionId}");
                    return;
                }

                if (antenna.Enabled == false || antenna.EnableBroadcasting == false || antenna.OwnerId == 0) return; // No owner, do nothing, no broadcasting, no interaction

                var modInteraction = Interactions[MESInteractionId];

                if (modInteraction == null) // Check if the interaction exists
                {
                    Log.Error($"Error: No interaction found for index: {MESInteractionId}");
                    return;
                }

                var antennaOwner = antenna.OwnerId;
                List<IMyIdentity> identities = new List<IMyIdentity>();
                MyAPIGateway.Players.GetAllIdentites(identities);
                string playerName = identities.First(i => i.IdentityId == antennaOwner)?.DisplayName ?? "Nobody - ask Odysseus";
                var commandProfileIds = modInteraction.CommandProfileIds;
                var antennaPosition = antenna.WorldMatrix.Translation;
                var antennaRadius = antenna.Radius;

                string callString = "";

                var RadioCalls = modInteraction.RadioCalls;
                if (RadioCalls.Count > 0) // Only send radio calls if there are any defined
                {
                    var randomCall = _rand.Next(0, RadioCalls.Count);
                    callString = RadioCalls[randomCall];
                }

                // Send to the server


                MESInteractionsPacket.Setup(commandProfileIds, antennaPosition, antennaRadius, antennaOwner, playerName, callString);
                Net.SendToServer(MESInteractionsPacket);

                if (_debug)
                {
                    Log.Info($"Sending MESInteractions packet with commandProfileIds: {commandProfileIds}\n" +
                        $"Antenna position: {antennaPosition}\n" +
                        $"Antenna radius: {antennaRadius}\n" +
                        $"Antenna owner: {antennaOwner}\n" +
                        $"Sender name: {playerName}\n" +
                        $"Radio call: {callString}");

                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error: An error occurred while trying to call: {ex.Message}");
            }

        }


        void MESInteractions_OnReceive(MESInteractions_NetworkPackage packet, ref PacketInfo packetInfo, ulong senderSteamId)
        {
            if (_debug) Log.Info($"Received MESInteractions packet");

            if (SpawnerAPI == null) // Check if the API is ready
            {
                Log.Error($"MES API is null. Server: {MyAPIGateway.Multiplayer.IsServer}.");
                return;
            }
            if (MyAPIGateway.Multiplayer.IsServer && !SpawnerAPI.MESApiReady) // Check if the API is ready on the server
            {
                Log.Error($"MES API is not ready. Server: {MyAPIGateway.Multiplayer.IsServer}. Please try again later.");
                return;
            }

            if (packet == null)
            {
                Log.Error("Received an invalid MESInteractions packet.");
                return;
            }


            var player = MyAPIGateway.Session.Player;
            if (_debug) Log.Info($"Is player null? {player == null}");
            bool playerInRange = false;
            if (player != null)
            {
                playerInRange = IsPlayerInRange(player, packet.Position, packet.AntennaRange);
                if (_debug) Log.Info($"Player {player.DisplayName} is in range: {playerInRange}");
            }

            bool radioCallEmpty = string.IsNullOrWhiteSpace(packet.RadioCall);

            if (_debug) Log.Info($"Radio call empty: {radioCallEmpty}");

            if (playerInRange && !radioCallEmpty) // Ignore empty radio calls and show only to players within range
            {
                // Show the radio call to the player
                MyAPIGateway.Utilities.ShowMessage(packet.SenderName, $"{packet.RadioCall}");
            }

            // Do this only on servers
            if (_debug) Log.Info($"Is server? {MyAPIGateway.Multiplayer.IsServer}");
            if (MyAPIGateway.Multiplayer.IsServer)
            {
                SpawnerAPI.SendBehaviorCommand(packet.CommandProfileIds, packet.Position, "", packet.AntennaRange, packet.AntennaOwnerID);
            }

            packetInfo.Relay = RelayMode.ToEveryone;

            if (_debug) Log.Info($"Received MESInteractions packet from {packet.SenderName} with call: {packet.RadioCall} at position {packet.Position} and range {packet.AntennaRange}");

        }

        public bool IsPlayerInRange(IMyPlayer player, Vector3D position, float antennaRange)
        {
            var playerPosition = player.GetPosition();
            var distance = Vector3D.Distance(playerPosition, position);

            if (distance <= antennaRange)  // Show the radio call to the player if within range
            {
                return true;
            }
            else
            {
                return false; // Player is not in range, do not show the radio call
            }
        }

        private void CollectConfig()
        {
            if (_debug) Log.Info("Collecting MESInteractions configuration settings...");

            if (MyAPIGateway.Session?.Mods == null)
            {
                if (_debug) Log.Info("No mods list available.");
                return;
            }

            const string relativePath = "/data/MESInteractions_Config.xml";
            int totalLoaded = 0;

            foreach (var mod in MyAPIGateway.Session.Mods)
            {
                try
                {
                    if (!MyAPIGateway.Utilities.FileExistsInModLocation(mod.GetPath() + relativePath, mod))
                    {
                        if (_debug) Log.Info($"No config in mod: {mod.Name}");
                        continue;
                    }

                    if (_debug) Log.Info($"Config file found in mod: {mod.Name} at {mod.GetPath() + relativePath}");

                    using (var reader = MyAPIGateway.Utilities.ReadFileInModLocation(mod.GetPath() + relativePath, mod))
                    {
                        var xml = reader.ReadToEnd();
                        var cfg = MyAPIGateway.Utilities.SerializeFromXML<MesInteractionsConfig>(xml);

                        if (cfg?.Items != null && cfg.Items.Count > 0)
                        {
                            var validItems = new List<MesInteraction>();

                            foreach (var interaction in cfg.Items)
                            {
                                Normalize(interaction);

                                if (IsValidInteraction(interaction))
                                {
                                    validItems.Add(interaction);
                                }
                                else if (_debug)
                                {
                                    Log.Info($"Rejected invalid interaction in mod {mod.Name}: missing required fields.");
                                }
                            }

                            if (validItems.Count > 0)
                            {
                                foreach (var item in validItems)
                                {
                                    if (!Interactions.ContainsKey(item.MESInteractionId))
                                    {
                                        Interactions[item.MESInteractionId] = item;
                                    }
                                    else
                                    {
                                        Interactions[item.MESInteractionId] = item; // Overwrite existing entry
                                        Log.Info($"Duplicate MESInteractionId found: {item.MESInteractionId} in mod {mod.Name}. Overwriting duplicate.");
                                    }
                                }
                                totalLoaded += validItems.Count;
                                if (_debug) Log.Info($"Loaded {validItems.Count} valid interaction(s) from: {mod.Name}");
                            }
                            else
                            {
                                if (_debug) Log.Info($"No valid interactions found in config: {mod.Name}");
                            }
                        }
                        else
                        {
                            if (_debug) Log.Info($"Config in {mod.Name} contained no interactions.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Error loading config from mod {mod.Name}: {ex.Message}");
                }
            }

            if (_debug) Log.Info($"Total MESInteractions loaded: {totalLoaded}");
        }

        // Cleanup/normalization to avoid whitespace/dupes
        private void Normalize(MesInteraction interaction)
        {
            if (interaction == null) return;

            if (!string.IsNullOrEmpty(interaction.MESInteractionId))
                interaction.MESInteractionId = interaction.MESInteractionId.Trim();


            if (interaction.CommandProfileIds != null)
            {
                interaction.CommandProfileIds = interaction.CommandProfileIds
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            if (interaction.RadioCalls != null)
            {
                interaction.RadioCalls = interaction.RadioCalls
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim())
                    .ToList();
            }

            if (!string.IsNullOrEmpty(interaction.AntennaCall))
                interaction.AntennaCall = interaction.AntennaCall.Trim();

            if (!string.IsNullOrEmpty(interaction.AntennaCallTooltip))
                interaction.AntennaCallTooltip = interaction.AntennaCallTooltip.Trim();
        }

        // Gate to reject incomplete configs
        private bool IsValidInteraction(MesInteraction interaction)
        {
            if (interaction == null) return false;

            bool hasIdField = !string.IsNullOrWhiteSpace(interaction.MESInteractionId);
            bool hasIds = interaction.CommandProfileIds != null &&
                          interaction.CommandProfileIds.Any(id => !string.IsNullOrWhiteSpace(id));

            return hasIdField
                && hasIds
                && !string.IsNullOrWhiteSpace(interaction.AntennaCall)
                && !string.IsNullOrWhiteSpace(interaction.AntennaCallTooltip);
        }


        // Optional helpers

        public MesInteraction GetByCommandProfileId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;

            return Interactions[id];
        }


        // XML models

        [XmlRoot("MESInteractions")]
        public class MesInteractionsConfig
        {
            [XmlElement("MESInteraction")]
            public List<MesInteraction> Items { get; set; } = new List<MesInteraction>();
        }

        public class MesInteraction
        {
            [XmlElement("MESInteractionId")]
            public string MESInteractionId { get; set; }   // NEW: maps <MESInteractionId>

            [XmlArray("CommandProfileIds")]
            [XmlArrayItem("CommandProfileId")]
            public List<string> CommandProfileIds { get; set; } = new List<string>();

            [XmlElement("AntennaCall")]
            public string AntennaCall { get; set; }

            [XmlElement("AntennaCallTooltip")]
            public string AntennaCallTooltip { get; set; }

            [XmlArray("RadioCalls")]
            [XmlArrayItem("RadioCall")]
            public List<string> RadioCalls { get; set; } = new List<string>();
        }

    }
}
