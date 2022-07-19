using System;
using Bedrock.Framework.Experimental.Protocols.Framing.VariableSized.LengthFielded;

namespace ServerApplication.Framing.VariableSized.LengthFielded
{
    internal class Header : IHeader, IEquatable<Header>
    {
        private byte[] _headerAsArray;

        public int PayloadLength { get; }
        public int SomeCustomData { get; }

        public Header(int payloadLength, int someCustomData)
        {
            PayloadLength = payloadLength;
            SomeCustomData = someCustomData;
        }

        public Header(ReadOnlySpan<byte> headerAsSpan)
        {
            PayloadLength = BitConverter.ToInt32(headerAsSpan.Slice(0, 4));
            SomeCustomData = BitConverter.ToInt32(headerAsSpan.Slice(4));
        }

        public ReadOnlySpan<byte> AsSpan()
        {
            // Lazy creating the array.
            if (_headerAsArray is null)
            {
                var payloadLengthAsArray = BitConverter.GetBytes(PayloadLength);
                var someCustomDataAsArray = BitConverter.GetBytes(SomeCustomData);

                _headerAsArray = new byte[Helper.HeaderLength];
                _headerAsArray[0] = payloadLengthAsArray[0];
                _headerAsArray[1] = payloadLengthAsArray[1];
                _headerAsArray[2] = payloadLengthAsArray[2];
                _headerAsArray[3] = payloadLengthAsArray[3];
                _headerAsArray[4] = someCustomDataAsArray[0];
                _headerAsArray[5] = someCustomDataAsArray[1];
                _headerAsArray[6] = someCustomDataAsArray[2];
                _headerAsArray[7] = someCustomDataAsArray[3];
            }
            
            return _headerAsArray.AsSpan();
        }

        public override string ToString() => $"Payload length: {PayloadLength} - Some custom data: {SomeCustomData}";

        #region IEquatable
        public override bool Equals(object obj) => Equals((Header)obj);

        public override int GetHashCode() => HashCode.Combine(PayloadLength, SomeCustomData);

        public bool Equals(Header other) => PayloadLength == other.PayloadLength && SomeCustomData.Equals(other.SomeCustomData);

        public static bool operator ==(Header left, Header right) => left.Equals(right);

        public static bool operator !=(Header left, Header right) => !(left == right);
        #endregion
    }
}
