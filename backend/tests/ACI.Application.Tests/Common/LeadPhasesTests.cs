using ACI.Application.Common;

namespace ACI.Application.Tests.Common;

/// <summary>
/// The phase a lead is in, as its sales tracker says it (phaseCompletion and currentPhase in
/// src/app/pages/leads/leadPipeline.ts): the first phase whose outcome and date are not recorded.
/// </summary>
public class LeadPhasesTests
{
    [Theory]
    [InlineData(null, 1)]
    [InlineData("", 1)]
    [InlineData("not json", 1)]
    [InlineData("[1,2]", 1)]
    [InlineData("{}", 1)]
    [InlineData("""{"outreachStatus":"attempted_no_answer"}""", 1)]
    // A meeting scheduled without its day is not phase 1 done yet.
    [InlineData("""{"outreachStatus":"contacted","contactOutcome":"meeting_scheduled"}""", 1)]
    [InlineData("""{"outreachStatus":"contacted","contactOutcome":"meeting_scheduled","meetingDate":"2026-11-20"}""", 2)]
    [InlineData("""{"outreachStatus":"contacted","contactOutcome":"meeting_scheduled","meetingDate":"2026-11-20","meetingAttended":true}""", 2)]
    [InlineData("""{"outreachStatus":"contacted","contactOutcome":"meeting_scheduled","meetingDate":"2026-11-20","meetingAttended":true,"stillInterested":true}""", 3)]
    [InlineData("""{"outreachStatus":"contacted","contactOutcome":"meeting_scheduled","meetingDate":"2026-11-20","meetingAttended":true,"stillInterested":true,"contractStatus":"yes","contractSentDate":"2026-11-21"}""", 4)]
    [InlineData("""{"outreachStatus":"contacted","contactOutcome":"meeting_scheduled","meetingDate":"2026-11-20","meetingAttended":true,"stillInterested":true,"contractStatus":"yes","contractSentDate":"2026-11-21","contractSigned":"yes","signatureDate":"2026-11-22"}""", 5)]
    // All five done: waiting at the deposit, to be converted.
    [InlineData("""{"outreachStatus":"contacted","contactOutcome":"meeting_scheduled","meetingDate":"2026-11-20","meetingAttended":true,"stillInterested":true,"contractStatus":"yes","contractSentDate":"2026-11-21","contractSigned":"yes","signatureDate":"2026-11-22","depositStatus":"paid","paymentDate":"2026-11-23"}""", 5)]
    // A later phase recorded with an earlier one open: the earlier one is where the lead is.
    [InlineData("""{"contractStatus":"yes","contractSentDate":"2026-11-21"}""", 1)]
    // Only the exact literal counts, as everywhere in the pipeline.
    [InlineData("""{"outreachStatus":"CONTACTED","contactOutcome":"meeting_scheduled","meetingDate":"2026-11-20"}""", 1)]
    public void ALeadIsInTheFirstPhaseNotDone(string? json, int phase) => LeadPhases.Current(json).Should().Be(phase);

    [Fact]
    public void TheLegacyDepositBooleanCountsOnlyWhenTheStatusIsAbsent()
    {
        const string signed = "\"outreachStatus\":\"contacted\",\"contactOutcome\":\"meeting_scheduled\",\"meetingDate\":\"2026-11-20\",\"meetingAttended\":true,\"stillInterested\":true,\"contractStatus\":\"yes\",\"contractSentDate\":\"2026-11-21\",\"contractSigned\":\"yes\",\"signatureDate\":\"2026-11-22\",\"paymentDate\":\"2026-11-23\"";
        LeadPhases.Completion("{" + signed + ",\"depositPaid\":true}")[4].Should().BeTrue();
        LeadPhases.Completion("{" + signed + ",\"depositPaid\":true,\"depositStatus\":\"pending\"}")[4].Should().BeFalse();
        LeadPhases.Completion("{" + signed + ",\"depositPaid\":false}")[4].Should().BeFalse();
    }
}
