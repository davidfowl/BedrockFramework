// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.IO.Pipelines;

internal class DuplexPipe(PipeReader reader, PipeWriter writer) : IDuplexPipe
{
    public PipeReader Input { get; } = reader;

    public PipeWriter Output { get; } = writer;

    public static DuplexPipePair CreateConnectionPair(PipeOptions inputOptions, PipeOptions outputOptions)
    {
        var input = new Pipe(inputOptions);
        var output = new Pipe(outputOptions);

        var transportToApplication = new DuplexPipe(output.Reader, input.Writer);
        var applicationToTransport = new DuplexPipe(input.Reader, output.Writer);

        return new DuplexPipePair(applicationToTransport, transportToApplication);
    }

    // This class exists to work around issues with value tuple on .NET Framework
    public readonly struct DuplexPipePair(IDuplexPipe transport, IDuplexPipe application)
    {
        public IDuplexPipe Transport { get; } = transport;
        public IDuplexPipe Application { get; } = application;
    }
}