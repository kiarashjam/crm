using ACI.Application.Common;

namespace ACI.Application.Tests.Common;

/// <summary>
/// A pipeline change is merged into the stored pipeline by key (RFC 7396), not written
/// over it.
/// </summary>
public class PipelineStatePatchTests
{
    private static string Merged(string? stored, string patch)
    {
        var result = PipelineStatePatch.Apply(stored, patch);
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Description : null);
        return result.Value;
    }

    [Fact]
    public void TheStepsSentReplaceTheirsAndEveryOtherStepIsKept()
    {
        // The contract steps the e-signature wrote, then an edit from a page opened before it.
        const string stored = """{"outreachStatus":"contacted","contractStatus":"yes","contractSigned":"pending"}""";

        Merged(stored, """{"meetingAttended":true,"outreachStatus":"contacted"}""")
            .Should().Be("""{"outreachStatus":"contacted","contractStatus":"yes","contractSigned":"pending","meetingAttended":true}""");
    }

    [Fact]
    public void AStepSentAsNullIsRemoved()
    {
        Merged("""{"meetingDate":"2026-11-20","contactOutcome":"meeting_scheduled"}""", """{"meetingDate":null}""")
            .Should().Be("""{"contactOutcome":"meeting_scheduled"}""");
    }

    [Fact]
    public void ANullForAStepThatIsNotThereChangesNothing()
    {
        Merged("""{"outreachStatus":"contacted"}""", """{"meetingDate":null}""").Should().Be("""{"outreachStatus":"contacted"}""");
    }

    [Fact]
    public void ANestedObjectMergesIntoTheOneStored()
    {
        Merged("""{"extra":{"a":1,"b":2},"x":true}""", """{"extra":{"b":null,"c":3}}""")
            .Should().Be("""{"extra":{"a":1,"c":3},"x":true}""");
        // An object over a value that is not one starts from empty, which drops its nulls.
        Merged("""{"extra":"text"}""", """{"extra":{"a":null,"b":1}}""").Should().Be("""{"extra":{"b":1}}""");
    }

    [Fact]
    public void NoStoredPipelineOrAnUnreadableOneStartsFromEmpty()
    {
        // As the status rule and the browser read them: not an object is an empty pipeline.
        Merged(null, """{"outreachStatus":"attempted_no_answer"}""").Should().Be("""{"outreachStatus":"attempted_no_answer"}""");
        Merged("{not json", """{"outreachStatus":"contacted"}""").Should().Be("""{"outreachStatus":"contacted"}""");
        Merged("[1,2]", """{"outreachStatus":"contacted"}""").Should().Be("""{"outreachStatus":"contacted"}""");
    }

    [Fact]
    public void TheLastOfARepeatedKeyWinsAsInJsonParse()
    {
        Merged("""{"a":1,"a":2}""", "{}").Should().Be("""{"a":2}""");
        Merged("""{"a":1}""", """{"a":3,"a":null}""").Should().Be("{}");
    }

    [Fact]
    public void AValueIsKeptExactlyAsItCame()
    {
        // Half of a surrogate pair, as JSON.stringify writes an emoji cut short. Decoding
        // and writing it again would fail, or change it.
        const string stored = """{"dropoutReasonOther":"Moved \ud83d","depositStatus":"pending"}""";

        Merged(stored, """{"depositStatus":"paid"}""")
            .Should().Be("""{"dropoutReasonOther":"Moved \ud83d","depositStatus":"paid"}""");
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"contacted\"")]
    [InlineData("null")]
    [InlineData("{not json")]
    public void AChangeThatIsNotAJsonObjectIsRefused(string patch)
    {
        PipelineStatePatch.Apply("""{"outreachStatus":"contacted"}""", patch).Error.Should().Be(PipelineStatePatch.NotAnObject);
    }

    [Fact]
    public void APipelineThatWouldOutgrowTheLimitIsRefused()
    {
        // Each change is capped, so without this the stored pipeline could grow a step at a time.
        var stored = "{\"notes\":\"" + new string('x', PipelineStatePatch.MaxLength - 40) + "\"}";

        PipelineStatePatch.Apply(stored, "{\"more\":\"" + new string('y', 60) + "\"}").Error.Should().Be(PipelineStatePatch.TooLarge);
    }
}
