using System;
using System.Threading;
using Xunit;

namespace InterMap.Tests
{
    public class ReaderWriterTests
    {
        [Fact]
        public void WriteAndRead()
        {
            byte[] actual = null;
            byte[] expected = new byte[8000];
            var random = new Random();
            random.NextBytes(expected);
            
            var reader = new InterMapReader("tests");
            var sub = reader.Subscribe(buffer => actual = buffer);
            
            var writer = new InterMapWriter("tests");
            writer.Enqueue(expected);

            SpinWait.SpinUntil(() => actual != null, TimeSpan.FromSeconds(60));
            Assert.NotNull(actual);
            Assert.Equal(expected.Length, actual.Length);
            for (int i = 0; i < actual.Length; i++)
            {
                Assert.Equal(expected[i], actual[i]);
            }
        }
    }
}
