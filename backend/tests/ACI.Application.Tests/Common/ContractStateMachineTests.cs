using ACI.Application.Common;
using ACI.Domain.Entities;

namespace ACI.Application.Tests.Common;

/// <summary>
/// The contract transitions that must be impossible.
/// </summary>
/// <remarks>
/// These are the SAME cases as <c>contractLifecycle.test.ts</c> on the client. If
/// the two tables ever disagree, the UI offers a button the server refuses — or
/// worse, hides one it would have allowed. Each test names the document you would
/// be left holding if the gate were missing.
/// </remarks>
public class ContractStateMachineTests
{
    private static readonly string[] AllStatuses =
    {
        ContractStatuses.Draft, ContractStatuses.Sent, ContractStatuses.SignedByClient,
        ContractStatuses.Countersigned, ContractStatuses.Declined, ContractStatuses.Voided,
    };

    private static readonly string[] AllActions =
    {
        ContractActions.Edit, ContractActions.Send, ContractActions.ClientSign,
        ContractActions.Decline, ContractActions.Countersign, ContractActions.Void,
        ContractActions.Resend,
    };

    [Fact]
    public void WalksDraftToFullyExecuted()
    {
        ContractStateMachine.Next(ContractStatuses.Draft, ContractActions.Send)
            .Should().Be(ContractStatuses.Sent);
        ContractStateMachine.Next(ContractStatuses.Sent, ContractActions.ClientSign)
            .Should().Be(ContractStatuses.SignedByClient);
        ContractStateMachine.Next(ContractStatuses.SignedByClient, ContractActions.Countersign)
            .Should().Be(ContractStatuses.Countersigned);
        ContractStateMachine.IsTerminal(ContractStatuses.Countersigned).Should().BeTrue();
    }

    [Fact]
    public void CannotCountersignBeforeTheClientHasSigned()
    {
        // Otherwise you produce an "executed" contract carrying only our own
        // signature, which looks complete and is not.
        ContractStateMachine.Can(ContractStatuses.Draft, ContractActions.Countersign).Should().BeFalse();
        ContractStateMachine.Can(ContractStatuses.Sent, ContractActions.Countersign).Should().BeFalse();
        ContractStateMachine.Next(ContractStatuses.Sent, ContractActions.Countersign).Should().BeNull();
    }

    [Fact]
    public void CannotEditOnceSent()
    {
        // The single failure that would make the whole feature worthless as
        // evidence: they sign one text and we keep another.
        ContractStateMachine.Can(ContractStatuses.Sent, ContractActions.Edit).Should().BeFalse();
        ContractStateMachine.Can(ContractStatuses.SignedByClient, ContractActions.Edit).Should().BeFalse();
        ContractStateMachine.Can(ContractStatuses.Countersigned, ContractActions.Edit).Should().BeFalse();
        ContractStateMachine.Can(ContractStatuses.Draft, ContractActions.Edit).Should().BeTrue();
    }

    [Fact]
    public void CannotSignTwice()
    {
        ContractStateMachine.Can(ContractStatuses.SignedByClient, ContractActions.ClientSign).Should().BeFalse();
        ContractStateMachine.Can(ContractStatuses.Countersigned, ContractActions.Countersign).Should().BeFalse();
    }

    [Fact]
    public void CannotSendTwice_WhichWouldLeaveTheFirstLinkLive()
    {
        ContractStateMachine.Can(ContractStatuses.Sent, ContractActions.Send).Should().BeFalse();
        // Resending is a distinct action and reuses the existing token.
        ContractStateMachine.Next(ContractStatuses.Sent, ContractActions.Resend)
            .Should().Be(ContractStatuses.Sent);
    }

    [Fact]
    public void NothingIsPossibleFromATerminalStatus()
    {
        foreach (var status in ContractStateMachine.TerminalStatuses)
        {
            ContractStateMachine.AllowedActions(status).Should().BeEmpty(status);
            foreach (var action in AllActions)
            {
                ContractStateMachine.Can(status, action).Should().BeFalse($"{status} + {action}");
            }
        }
    }

    [Fact]
    public void ADeclinedContractCannotBeResurrected()
    {
        ContractStateMachine.Can(ContractStatuses.Declined, ContractActions.Send).Should().BeFalse();
        ContractStateMachine.Can(ContractStatuses.Declined, ContractActions.Resend).Should().BeFalse();
        ContractStateMachine.Can(ContractStatuses.Declined, ContractActions.ClientSign).Should().BeFalse();
    }

    [Fact]
    public void EveryNonTerminalStatusCanBeAbandoned()
    {
        // A contract with no way out is a support ticket. Void is the escape hatch.
        foreach (var status in AllStatuses.Where(s => !ContractStateMachine.IsTerminal(s)))
        {
            ContractStateMachine.Can(status, ContractActions.Void).Should().BeTrue(status);
        }
    }

    [Fact]
    public void EveryReachableTargetIsARealStatus()
    {
        foreach (var status in AllStatuses)
        {
            foreach (var action in AllActions)
            {
                var target = ContractStateMachine.Next(status, action);
                if (target is not null) AllStatuses.Should().Contain(target, $"{status}+{action}");
            }
        }
    }

    [Fact]
    public void AllowedActionsAgreesWithCan()
    {
        // Otherwise the API advertises an action it would then refuse.
        foreach (var status in AllStatuses)
        {
            var allowed = ContractStateMachine.AllowedActions(status);
            foreach (var action in AllActions)
            {
                allowed.Contains(action).Should().Be(
                    ContractStateMachine.Can(status, action), $"{status}+{action}");
            }
        }
    }

    [Fact]
    public void AnUnknownOrMissingStatusPermitsNothing()
    {
        // Failing closed. A row with a status this build does not recognise must
        // not become a contract anyone can sign.
        ContractStateMachine.Can(null, ContractActions.ClientSign).Should().BeFalse();
        ContractStateMachine.Can("", ContractActions.ClientSign).Should().BeFalse();
        ContractStateMachine.Can("something_new", ContractActions.ClientSign).Should().BeFalse();
        ContractStateMachine.AllowedActions("something_new").Should().BeEmpty();
        ContractStateMachine.Next(null, ContractActions.Send).Should().BeNull();
    }

    [Theory]
    [InlineData("Jean Dupont")]
    [InlineData("J. Dupont")]
    [InlineData("Léa")]
    [InlineData("Ng Wei")]
    [InlineData("O'Brien")]
    public void AcceptsARealName(string name)
        => ContractStateMachine.IsSignatureNameValid(name).Should().BeTrue(name);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("x")]
    [InlineData("..")]
    [InlineData("--")]
    [InlineData("1234")]
    public void RejectsAPlaceholderMark(string? name)
        // A placeholder would leave nothing to point at later, and the mark is the
        // entire substance of the signature.
        => ContractStateMachine.IsSignatureNameValid(name).Should().BeFalse($"'{name}'");
    /* ------------------------------------------- what a signature may contain */

    [Fact]
    public void RefusesASignatureCarryingNewlines()
    {
        // The typed name is interpolated LINE BY LINE into the SIGNATURE RECORD that
        // goes to both parties as evidence. A name containing newlines could forge
        // lines in it — a second "Signed:" with an earlier date, a different
        // document hash, a "Note:" reserving a clause — all looking exactly as
        // authoritative as the real ones.
        const string forged =
            "Jean Dupont\nSigned       : 2019-04-01 09:00:00 UTC\n"
            + "Document hash: 0000000000000000000000000000000000000000000000000000000000000000\n"
            + "Note         : signed subject to clause 2 being struck out";

        ContractStateMachine.IsSignatureNameValid(forged).Should().BeFalse();
    }

    [Theory]
    [InlineData("Jean\rDupont")]
    [InlineData("Jean\tDupont")]
    [InlineData("Jean\u0000Dupont")]
    [InlineData("Jean\u001BDupont")]
    public void RefusesAnyControlCharacter(string name)
    {
        ContractStateMachine.IsSignatureNameValid(name).Should().BeFalse();
    }

    [Fact]
    public void RefusesANameTooLongForItsColumn()
    {
        // nvarchar(300). Left to the column it raised "String or binary data would
        // be truncated" — a 500 from the anonymous signing endpoint, at the exact
        // moment somebody was trying to sign. Refused here, the signer is told.
        var atLimit = new string('a', ContractStateMachine.MaxSignatureNameLength);
        var overLimit = new string('a', ContractStateMachine.MaxSignatureNameLength + 1);

        ContractStateMachine.IsSignatureNameValid(atLimit).Should().BeTrue();
        ContractStateMachine.IsSignatureNameValid(overLimit).Should().BeFalse();
    }

    [Fact]
    public void CountsLettersByRuneSoItAgreesWithTheBrowser()
    {
        // Adlam, used for Fulani. The client counts letters with /\p{L}/gu, which
        // counts code POINTS; char.IsLetter counts UTF-16 units, and a surrogate
        // pair is two units that are each "not a letter". So the page accepted this
        // name, submitted it, and the server refused it as a placeholder.
        const string adlam = "\U0001E900\U0001E922";
        ContractStateMachine.IsSignatureNameValid(adlam).Should().BeTrue();

        // And the existing refusals still hold — the rune count must not have made
        // the check laxer.
        foreach (var bad in new[] { "..", "--", "1234", "x", " " })
        {
            ContractStateMachine.IsSignatureNameValid(bad).Should().BeFalse(bad);
        }
    }

}
