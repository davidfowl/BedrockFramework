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
                        10 => methodId switch
                        {
                            10 => new ConnectionStart(),
                            30 => new ConnectionTune(),
                            41 => new ConnectionOpenOk(),
                            _ => throw new Exception($"not (yet) supported classId {classId} - methodId {methodId}"),
                        },
                        20 => methodId switch
                        {
                            11 => new ChannelOpenOk(),
                            _ => throw new Exception($"not (yet) supported classId {classId} - methodId {methodId}"),
                        },
                        50 => methodId switch
                        {
                            11 => new QueueDeclareOk(),
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
