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
        public static readonly bool _debug = true;

        // All loaded interactions from all mods that provide a config
        public readonly List<MesInteraction> Interactions = new List<MesInteraction>();

        public const ushort NetworkId = (ushort)(3547952468 % ushort.MaxValue); // Using the prod steam id

        public Network Net;

        MESInteractions_NetworkPackage MESInteractionsPacket;

        public override void LoadData()
        {
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

        public void SendInteraction(Vector3D position, float antennaRange, string senderName, string radioCall)
        {
            if (MyAPIGateway.Session?.Player == null)
            {
                Log.Error("Cannot send MESInteractions packet without a player context.");
                return;
            }
            if (string.IsNullOrWhiteSpace(radioCall))
            {
                Log.Error("Radio call cannot be null or empty.");
                return;
            }
            MESInteractionsPacket.Setup(position, antennaRange, senderName, radioCall);
            Net.SendToServer(MESInteractionsPacket);
            if (_debug)
            {
                Log.Info($"Sent MESInteractions packet from {senderName} with call: {radioCall} at position {position} and range {antennaRange}");
            }
        }

        void MESInteractions_OnReceive(MESInteractions_NetworkPackage packet, ref PacketInfo packetInfo, ulong senderSteamId)
        {
            if (_debug)
            {
                Log.Info($"Received MESInteractions packet from {packet.SenderName} with call: {packet.RadioCall} at position {packet.Position} and range {packet.AntennaRange}");
            }

            if (packet == null || string.IsNullOrWhiteSpace(packet.RadioCall))
            {
                Log.Error("Received an invalid MESInteractions packet.");
                return;
            }

            // This is called on everyone that receives the packet.
            var player = MyAPIGateway.Session.Player;
            if (player != null)
            {
                
                var playerPosition = player.GetPosition();
                var distance = Vector3D.Distance(playerPosition, packet.Position);

                if (distance <= packet.AntennaRange)  // Show the radio call to the player if within range
                {
                    MyAPIGateway.Utilities.ShowMessage(packet.SenderName, $"{packet.RadioCall}");
                }
                else
                {
                    if (_debug) Log.Info($"Player {player.DisplayName} is out of range for the radio call: {packet.RadioCall}");
                }
            }


            // Get the local player and test the distance to the packet's position against the antenna range
            packetInfo.Relay = RelayMode.ToEveryone;
            

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
                                Interactions.AddRange(validItems);
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

            bool hasIds = interaction.CommandProfileIds != null &&
                          interaction.CommandProfileIds.Any(id => !string.IsNullOrWhiteSpace(id));

            //bool hasCalls = interaction.RadioCalls != null &&
            //                interaction.RadioCalls.Any(rc => !string.IsNullOrWhiteSpace(rc));

            return hasIds
                && !string.IsNullOrWhiteSpace(interaction.AntennaCall)
                && !string.IsNullOrWhiteSpace(interaction.AntennaCallTooltip);
                //&& hasCalls;
        }

        // Optional helpers

        public MesInteraction GetByCommandProfileId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;

            return Interactions.FirstOrDefault(i =>
                i.CommandProfileIds != null &&
                i.CommandProfileIds.Any(cid => string.Equals(cid, id, StringComparison.OrdinalIgnoreCase)));
        }

        public IEnumerable<MesInteraction> GetAll() => Interactions;

        // XML models

        [XmlRoot("MESInteractions")]
        public class MesInteractionsConfig
        {
            [XmlElement("MESInteraction")]
            public List<MesInteraction> Items { get; set; } = new List<MesInteraction>();
        }

        public class MesInteraction
        {
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
