using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet.Adapter;
using MQTTnet.AspNetCore;
using MQTTnet.Packets;
using MQTTnet.Protocol;

namespace ServerApplication
{
    public class MqttApplication : MqttConnectionHandler
    {
        public MqttApplication()
        {
            ClientHandler = OnClientConnectedAsync;
        }

        private async Task OnClientConnectedAsync(IMqttChannelAdapter adapter)
        {
            while (true)
            {
                var packet = await adapter.ReceivePacketAsync(Timeout.InfiniteTimeSpan, default);

                switch (packet)
                {
                    case MqttConnectPacket connectPacket:
                        await adapter.SendPacketAsync(new MqttConnAckPacket
                        {
                            ReturnCode = MqttConnectReturnCode.ConnectionAccepted,
                            ReasonCode = MqttConnectReasonCode.Success,
                            IsSessionPresent = false
                        }, Timeout.InfiniteTimeSpan,
                        default);
                        break;
                    case MqttDisconnectPacket disconnectPacket:
                        break;
                    case MqttAuthPacket mqttAuthPacket:
                        break;
                    case MqttConnAckPacket connAckPacket:
                        break;
                    case MqttPublishPacket mqttPublishPacket:
                        break;
                    case MqttSubscribePacket mqttSubscribePacket:
                        var ack = new MqttSubAckPacket
                        {
                            PacketIdentifier = mqttSubscribePacket.PacketIdentifier,
                            ReturnCodes = new List<MqttSubscribeReturnCode> { MqttSubscribeReturnCode.SuccessMaximumQoS0 }
                        };
                        ack.ReasonCodes.Add(MqttSubscribeReasonCode.GrantedQoS0);

                        await adapter.SendPacketAsync(ack, Timeout.InfiniteTimeSpan, default);
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
