using Bedrock.Framework.Experimental.Infrastructure;
using System.Net;
using System.Runtime.InteropServices;

namespace Bedrock.Framework.Experimental.Protocols.Tls.BulkCiphers
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct AdditionalInfo
    {
        private ulong _sequenceNumber;
        private TlsRecordType _recordType;
        private TlsProtocolVersion _tlsVersion;
        private ushort _plainTextLength;

        public ulong SequenceNumber { get => Endianness.FromNetworkOrder(_sequenceNumber); set => _sequenceNumber = Endianness.ToNetworkOrder(value); }
        public TlsRecordType RecordType { get => _recordType; set => _recordType = value; }
        public TlsProtocolVersion TlsVersion { get => Endianness.FromNetworkOrder(_tlsVersion); set => _tlsVersion = Endianness.ToNetworkOrder(value); }
        public ushort PlainTextLength { get => Endianness.FromNetworkOrder(_plainTextLength); set => _plainTextLength = Endianness.ToNetworkOrder(value); }
    }
}
