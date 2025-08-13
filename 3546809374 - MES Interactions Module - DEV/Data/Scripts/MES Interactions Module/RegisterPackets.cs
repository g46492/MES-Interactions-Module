using ProtoBuf;
using PEPCO;


namespace Digi.NetworkLib
{
    [ProtoInclude(10, typeof(MESInteractions_NetworkPackage))]
    //[ProtoInclude(11, typeof(SomeOtherPacketClass))]
    //[ProtoInclude(12, typeof(Etc...))]
    public abstract partial class PacketBase
    {
    }
}