﻿#nullable enable

using System;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Models
{
    public readonly struct Broker
    {
        public readonly int NodeId;
        public readonly string Host;
        public readonly int Port;
        public readonly string? Rack;

        public Broker(int nodeId, string host, int port, string? rack)
        {
            this.NodeId = nodeId;
            this.Host = host;
            this.Port = port;
            this.Rack = rack;
        }

        public override bool Equals(object? obj)
        {
            if (obj == null || !(obj is Broker))
            {
                return false;
            }

            var that = (Broker)obj;

            return this.NodeId.Equals(that.NodeId)
                && this.Host.Equals(that.Host)
                && this.Port.Equals(that.Port)
                && this.Rack == that.Rack;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                this.NodeId,
                this.Host,
                this.Port,
                this.Rack);
        }

        public static bool operator ==(Broker left, Broker right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Broker left, Broker right)
        {
            return !(left == right);
        }
    }
}
