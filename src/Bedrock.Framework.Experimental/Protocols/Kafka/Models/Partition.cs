#nullable enable

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Models
{
    public readonly struct Partition
    {
        public readonly int Index;

        public Partition(int value)
            => this.Index = value;

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            return this.Index.Equals(obj);
        }

        public override int GetHashCode()
        {
            return this.Index.GetHashCode();
        }

        public static bool operator ==(Partition left, Partition right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Partition left, Partition right)
        {
            return !(left == right);
        }
    }
}
