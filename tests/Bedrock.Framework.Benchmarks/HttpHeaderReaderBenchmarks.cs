using Bedrock.Framework.Protocols.Http.Http1;
using BenchmarkDotNet.Attributes;
using System;
using System.Buffers;
using System.Text;

namespace Bedrock.Framework.Benchmarks
{
    public class HttpHeaderReaderBenchmarks
    {
        [Params(10, 1000)]
        public int HeaderValueLength { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            var data = "/search?rlz=1C1GCEA_enIL874IL874&sxsrf=ACYBGNQAjkw0VCQs1ElFY43_D_Wq9DMwQA%3A1580624707397&ei=Q2s2XtjxF8_RwAK35ocQ&q=request+header+example&oq=request+header+example&gs_l=psy-ab.3..0i7i30l10.9636.9636..9981...0.2..0.245.245.2-1......0....1..gws-wiz.......0i71.JfZoTvof620&ved=0ahUKEwiYn9TxnbLnAhXPKFAKHTfzAQIQ4dUDCAs&uact=5/search?rlz=1C1GCEA_enIL874IL874&sxsrf=ACYBGNQAjkw0VCQs1ElFY43_D_Wq9DMwQA%3A1580624707397&ei=Q2s2XtjxF8_RwAK35ocQ&q=request+header+example&oq=request+header+example&gs_l=psy-ab.3..0i7i30l10.9636.9636..9981...0.2..0.245.245.2-1......0....1..gws-wiz.......0i71.JfZoTvof620&ved=0ahUKEwiYn9TxnbLnAhXPKFAKHTfzAQIQ4dUDCAs&uact=5/search?rlz=1C1GCEA_enIL874IL874&sxsrf=ACYBGNQAjkw0VCQs1ElFY43_D_Wq9DMwQA%3A1580624707397&ei=Q2s2XtjxF8_RwAK35ocQ&q=request+header+example&oq=request+header+example&gs_l=psy-ab.3..0i7i30l10.9636.9636..9981...0.2..0.245.245.2-1......0....1..gws-wiz.......0i71.JfZoTvof620&ved=0ahUKEwiYn9TxnbLnAhXPKFAKHTfzAQIQ4dUDCAs&uact=5/search?rlz=1C1GCEA_enIL874IL874&sxsrf=ACYBGNQAjkw0VCQs1ElFY43_D_Wq9DMwQA%3A1580624707397&ei=Q2s2XtjxF8_RwAK35ocQ&q=request+header+example&oq=request+header+example&gs_l=psy-ab.3..0i7i30l10.9636.9636..9981...0.2..0.245.245.2-1......0....1..gws-wiz.......0i71.JfZoTvof620&ved=0ahUKEwiYn9TxnbLnAhXPKFAKHTfzAQIQ4dUDCAs&uact=5";
            bytes = new ReadOnlySequence<byte>(Encoding.ASCII.GetBytes($"Header-Name:{data[..HeaderValueLength]}\r\n"));
        }

        private ReadOnlySequence<byte> bytes;
        private Http1HeaderReader reader = new Http1HeaderReader();

        [Benchmark]
        public bool ReadHeader()
        {
            var consumed = bytes.Start;
            var examined = bytes.Start;
            return reader.TryParseMessage(in bytes, ref consumed, ref examined, out var result);
        }
    }
}
