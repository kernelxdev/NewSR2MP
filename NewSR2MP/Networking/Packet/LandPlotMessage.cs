﻿

namespace NewSR2MP.Networking.Packet
{
    public class LandPlotMessage : ICustomMessage
    {
        public string id;
        public LandPlot.Id type;
        public LandPlot.Upgrade upgrade;
        public LandplotUpdateType messageType;
    
        public Message Serialize()
        {
            Message msg = Message.Create(MessageSendMode.Unreliable, PacketType.LandPlot);
            msg.AddByte((byte)messageType);
            msg.AddString(id);

            if (messageType == LandplotUpdateType.SET)
                msg.AddByte((byte)type);
            else
                msg.AddByte((byte)upgrade);

            return msg;
        }

        public void Deserialize(Message msg)
        {
            messageType = (LandplotUpdateType)msg.GetByte();
            id = msg.GetString();
            if (messageType == LandplotUpdateType.SET)
                type = (LandPlot.Id)msg.GetByte();
            else
                upgrade = (LandPlot.Upgrade)msg.GetByte();

        }
    }

    public enum LandplotUpdateType : byte
    {
        SET,
        UPGRADE
    }
}
