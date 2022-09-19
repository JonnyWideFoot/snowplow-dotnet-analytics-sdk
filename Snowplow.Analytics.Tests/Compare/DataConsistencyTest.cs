using System.IO;
using FluentAssertions;
using Snowplow.Analytics.Json;
using Snowplow.Analytics.V2;
using Snowplow.Analytics.V3;
using Xunit;

namespace Snowplow.Analytics.Tests.Compare;

public sealed class EventTransformerComparison
{
    [Fact]
    public void CanParseSampleRecord_V2()
    {
        var record = File.ReadAllText("Data/SampleRecord.txt");

        var output1 = EventTransformer.Transform(record);
        var output2 = EventTransformer2.Transform(record);

        output1.Length.Should().Be(output2.Length);
        output1.Should().Be(output2);
    }

    [Fact]
    public void CanParseSampleRecord_V3()
    {
        var record = File.ReadAllText("Data/SampleRecord.txt");

        var output1 = EventTransformer.Transform(record);
        var output3 = EventTransformer3.Transform(record);

        output1.Length.Should().Be(output3.Length);
        output1.Should().Be(output3);
    }
}
