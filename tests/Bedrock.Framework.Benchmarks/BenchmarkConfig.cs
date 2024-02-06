using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using System;
using System.IO;

namespace Bedrock.Framework.Benchmarks
{
    public class BenchmarkConfig : ManualConfig
    {
        public BenchmarkConfig()
        {
            Add(DefaultConfig.Instance);
            AddDiagnoser(MemoryDiagnoser.Default);
            
            ArtifactsPath = Path.Combine(AppContext.BaseDirectory, "artifacts", DateTime.Now.ToString("yyyy-mm-dd_hh-MM-ss"));
        }
    }
}
