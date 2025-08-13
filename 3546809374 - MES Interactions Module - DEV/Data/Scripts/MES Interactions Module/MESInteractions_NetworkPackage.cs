using Digi.NetworkLib;
using ProtoBuf;
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


        public void Setup(Vector3D position, float antennaRange, string senderName, string radioCall)
        {
            
            Position = position;
            AntennaRange = antennaRange;
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
