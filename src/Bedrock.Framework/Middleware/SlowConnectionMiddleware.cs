using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework.Middleware
{
    class SlowConnectionMiddleware
    {
        private readonly ConnectionDelegate _next;

        public SlowConnectionMiddleware(ConnectionDelegate next)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
        }

        public async Task OnConnectionAsync(ConnectionContext context)
        {
            var oldTransport = context.Transport;

            var pair = DuplexPipe.CreateConnectionPair(new PipeOptions(readerScheduler: PipeScheduler.Inline), new PipeOptions(readerScheduler: PipeScheduler.Inline));

            static async Task SlowCopyAsync(IDuplexPipe transport, IDuplexPipe application)
            {
                async Task Reading()
                {
                    while (true)
                    {
                        var result = await transport.Input.ReadAsync();

                        foreach (var item in result.Buffer)
                        {
                            for (int i = 0; i < item.Length; i++)
                            {
                                await application.Output.WriteAsync(item.Slice(i, 1));
                            }
                        }

                        if (result.IsCompleted)
                        {
                            break;
                        }

                        transport.Input.AdvanceTo(result.Buffer.End);
                    }

                    await application.Output.CompleteAsync();
                    await transport.Input.CompleteAsync();
                }

                async Task Writing()
                {
                    while (true)
                    {
                        var result = await application.Input.ReadAsync();

                        foreach (var item in result.Buffer)
                        {
                            for (int i = 0; i < item.Length; i++)
                            {
                                await transport.Output.WriteAsync(item.Slice(i, 1));
                            }
                        }

                        if (result.IsCompleted)
                        {
                            break;
                        }

                        application.Input.AdvanceTo(result.Buffer.End);
                    }

                    await transport.Output.CompleteAsync();
                    await application.Input.CompleteAsync();
                }

                var reading = Reading();
                var writing = Writing();

                await Task.WhenAll(reading, writing);
            }

            var task = SlowCopyAsync(oldTransport, pair.Application);

            try
            {
                context.Transport = pair.Transport;

                await _next(context).ConfigureAwait(false);
            }
            finally
            {
                await pair.Transport.Input.CompleteAsync();
                await pair.Transport.Output.CompleteAsync();

                await task;

                context.Transport = oldTransport;
            }
        }
    }
}
