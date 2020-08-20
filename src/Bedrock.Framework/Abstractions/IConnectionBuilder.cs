using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bedrock.Framework
{
    public interface IConnectionBuilder
    {
        /// <summary>
        /// Gets the <see cref="IServiceProvider"/> that provides access to the application's service container.
        /// </summary>
        IServiceProvider ApplicationServices { get; }

        /// <summary>
        /// Adds a middleware delegate to the application's connection pipeline.
        /// </summary>
        /// <param name="middleware">The middleware delegate.</param>
        /// <returns>The <see cref="IConnectionBuilder"/>.</returns>
        IConnectionBuilder Use(Func<ConnectionDelegate, ConnectionDelegate> middleware);

        /// <summary>
        /// Builds the delegate used by this application to process connections.
        /// </summary>
        /// <returns>The connection handling delegate.</returns>
        ConnectionDelegate Build();
    }
}
