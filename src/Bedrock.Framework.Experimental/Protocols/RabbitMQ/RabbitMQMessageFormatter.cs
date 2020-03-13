using Bedrock.Framework.Experimental.Protocols.RabbitMQ.Methods;
using Bedrock.Framework.Protocols;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.RabbitMQ
{
    public class RabbitMQMessageFormatter : IMessageWriter<IAmqpMessage>, IMessageReader<IAmqpMessage>
    {
        public const int HeaderLength = 7;

        public const int Connection = 10;
        public const int ConnectionStart = 10;
        public const int ConnectionTune = 30;
        public const int ConnectionOpen = 41;

        public const int Channel = 20;
        public const int ChannelOpenOk = 11;

        public const int Queue = 50;
        public const int QueueDeclareOk = 11;

        public bool TryParseMessage(in ReadOnlySequence<byte> input, ref SequencePosition consumed, ref SequencePosition examined, out IAmqpMessage message)
        {
            message = default;
            if (input.Length < HeaderLength)
            {
                return false;
            }

            Span<byte> buffer = stackalloc byte[HeaderLength];
            input.Slice(0, HeaderLength).CopyTo(buffer);

            var frameType = (FrameType)buffer[0];
            var channel = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(1));
            var payloadSize = BinaryPrimitives.ReadInt32BigEndian(buffer.Slice(3));

            if (input.Length >= (HeaderLength + payloadSize + sizeof(byte)))
            {                
                var payload = input.Slice(HeaderLength, payloadSize + sizeof(byte));                
                if (frameType == FrameType.Method)
                {
                    Span<byte> methodBuffer = stackalloc byte[4];
                    payload.Slice(0, 4).CopyTo(methodBuffer);

                    var classId = BinaryPrimitives.ReadUInt16BigEndian(methodBuffer);
                    var methodId = BinaryPrimitives.ReadUInt16BigEndian(methodBuffer.Slice(2));
                    
                    payload = payload.Slice(4);
                   
                    message = classId switch
                    {
                        Connection => methodId switch
                        {
                            ConnectionStart => new ConnectionStart(),
                            ConnectionTune => new ConnectionTune(),
                            41 => new ConnectionOpenOk(),
                            _ => throw new Exception($"not (yet) supported classId {classId} - methodId {methodId}"),
                        },
                        Channel => methodId switch
                        {
                            ChannelOpenOk => new ChannelOpenOk(),
                            _ => throw new Exception($"not (yet) supported classId {classId} - methodId {methodId}"),
                        },
                        Queue => methodId switch
                        {
                            QueueDeclareOk => new QueueDeclareOk(),
                            _ => throw new Exception($"not (yet) supported classId {classId} - methodId {methodId}"),
                        },
                        _ => throw new Exception($"not (yet) supported classId {classId}"),
                    };                   
                }

                if (message.TryParse(payload, out SequencePosition end))
                {
                    var frameEnd = payload.Slice(end).FirstSpan[0];
                    if (frameEnd != (byte)FrameType.End)
                    {
                        throw new Exception($"unexcepted frame end");
                    }
                    consumed = payload.End;
                    examined = consumed;
                    return true;
                }
            }
            return false;
        }

        public void WriteMessage(IAmqpMessage message, IBufferWriter<byte> output) => message.Write(output);                
    }
}
