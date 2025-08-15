using Digi.NetworkLib;
using ProtoBuf;
using System.Collections.Generic;
using VRageMath;

namespace PEPCO
{
    
    [ProtoContract]
    public class MESInteractions_NetworkPackage : PacketBase
    {
        public MESInteractions_NetworkPackage() { }

        [ProtoMember(1)]
        public Vector3D Position;

        [ProtoMember(2)]
        public float AntennaRange;

        [ProtoMember(3)]
        public string SenderName;

        [ProtoMember(4)]
        public string RadioCall;

        [ProtoMember(5)]
        public List<string> CommandProfileIds = new List<string>();

        [ProtoMember(6)]
        public long AntennaOwnerID;

        public void Setup(List<string> commandProfileIds, Vector3D position, float antennaRange, long antennaOwnerID, string senderName, string radioCall)
        {
            CommandProfileIds = commandProfileIds;
            Position = position;
            AntennaRange = antennaRange;
            AntennaOwnerID = antennaOwnerID;
            SenderName = senderName; 
            RadioCall = radioCall; 
        }

       
        public static event ReceiveDelegate<MESInteractions_NetworkPackage> OnReceive;

        public override void Received(ref PacketInfo packetInfo, ulong senderSteamId)
        {
            OnReceive?.Invoke(this, ref packetInfo, senderSteamId);
        }
    }
}
