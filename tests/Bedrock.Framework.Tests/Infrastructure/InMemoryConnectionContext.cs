using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;

namespace Bedrock.Framework.Tests.Infrastructure
{
    public class InMemoryConnectionContext : ConnectionContext
    {
        public override string ConnectionId { get; set; } = Guid.NewGuid().ToString();

        public override IFeatureCollection Features => new FeatureCollection();

        public override IDictionary<object, object> Items { get; set; } = new Dictionary<object, object>();

        public override IDuplexPipe Transport { get; set; }

        public IDuplexPipe Application { get; set; }

        public InMemoryConnectionContext(PipeOptions options = default)
        {
            var duplexPipePair = DuplexPipe.CreateConnectionPair(options ?? PipeOptions.Default, options ?? PipeOptions.Default);
            Transport = duplexPipePair.Transport;
            Application = duplexPipePair.Application;
        }
    }
}
