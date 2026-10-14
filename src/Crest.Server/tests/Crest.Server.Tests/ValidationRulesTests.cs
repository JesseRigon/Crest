using System.ComponentModel.DataAnnotations;
using Crest.Validation;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Crest.Server.Tests;

// The ruleset (docs/MVC/api.md › Validation): each rule as an attribute and through the fluent
// chain, the error map's shape (member wire names, object errors under ""), and the localizer seam.
public sealed class ValidationRulesTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("x", true)]
    public void NotEmptyIsWhitespaceAware(string? value, bool valid) => Assert.Equal(valid, Check(new NotEmptyAttribute(), value));

    [Fact]
    public void NotEmptyFailsEmptyCollectionsAndDefaults()
    {
        Assert.False(Check(new NotEmptyAttribute(), Array.Empty<string>()));
        Assert.True(Check(new NotEmptyAttribute(), new[] { "a" }));
        Assert.False(Check(new NotEmptyAttribute(), 0));
        Assert.False(Check(new NotEmptyAttribute(), Guid.Empty));
        Assert.True(Check(new NotEmptyAttribute(), 3));
    }

    [Fact]
    public void LengthsCountCharactersAndItems()
    {
        Assert.False(Check(new MinimumLengthAttribute(3), "ab"));
        Assert.True(Check(new MinimumLengthAttribute(3), "abc"));
        Assert.False(Check(new MaximumLengthAttribute(2), "abc"));
        Assert.True(Check(new MaximumLengthAttribute(2), new[] { 1, 2 }));
        Assert.True(Check(new LengthBetweenAttribute(2, 4), "abc"));
        Assert.False(Check(new LengthBetweenAttribute(2, 4), "abcde"));
        Assert.True(Check(new MinimumLengthAttribute(3), null));
    }

    [Fact]
    public void MatchesEmailAndUrl()
    {
        Assert.True(Check(new MatchesAttribute("^[a-z]+$"), "abc"));
        Assert.False(Check(new MatchesAttribute("^[a-z]+$"), "ab1"));
        Assert.True(Check(new EmailAttribute(), "someone@example.org"));
        Assert.False(Check(new EmailAttribute(), "someone@"));
        Assert.False(Check(new EmailAttribute(), "some one@example.org"));
        Assert.True(Check(new AbsoluteUrlAttribute(), "https://example.org/path"));
        Assert.False(Check(new AbsoluteUrlAttribute(), "/relative"));
        Assert.False(Check(new AbsoluteUrlAttribute(), "ftp://example.org"));
        Assert.True(Check(new AbsoluteUrlAttribute("ftp"), "ftp://example.org"));
    }

    [Fact]
    public void ComparisonsAgainstValuesAndOtherProperties()
    {
        Assert.True(Check(new GreaterThanAttribute(5), 6));
        Assert.False(Check(new GreaterThanAttribute(5), 5));
        Assert.True(Check(new GreaterThanOrEqualAttribute(5), 5));
        Assert.True(Check(new LessThanAttribute(5), 4.5m));
        Assert.False(Check(new LessThanOrEqualAttribute(5), 5.1));
        Assert.True(Check(new InclusiveBetweenAttribute(1, 3), 3));
        Assert.False(Check(new ExclusiveBetweenAttribute(1, 3), 3));
        Assert.True(Check(new InclusiveBetweenAttribute(1, 3), null));

        var model = new Dates { From = new DateTime(2026, 1, 1), To = new DateTime(2026, 1, 2) };
        Assert.Empty(Validate(model));
        model.To = model.From;
        var error = Assert.Single(Validate(model));
        Assert.Equal("to", Assert.Single(error.MemberNames));
        Assert.Equal("to must be greater than From.", error.ErrorMessage);
    }

    [Fact]
    public void EqualToAndNotEqualToAnotherProperty()
    {
        var password = new Password { New = "secret", Confirm = "secret", Current = "old" };
        Assert.Empty(Validate(password));

        password.Confirm = "other";
        password.Current = "secret";
        var errors = Validate(password);
        Assert.Equal(["confirm", "new"], errors.SelectMany(error => error.MemberNames).Order());
    }

    [Fact]
    public void PrecisionScale()
    {
        Assert.True(Check(new PrecisionScaleAttribute(5, 2), 123.45m));
        Assert.False(Check(new PrecisionScaleAttribute(5, 2), 1234.5m));
        Assert.False(Check(new PrecisionScaleAttribute(5, 2), 1.234m));
        Assert.True(Check(new PrecisionScaleAttribute(5, 2), 1.200m));
    }

    [Fact]
    public void MustCallsAMethodOfTheObject()
    {
        var order = new Order { Code = "ok", Lines = [new Line { Quantity = 1 }] };
        Assert.Empty(Validate(order));
        order.Code = "bad";
        Assert.Contains(Validate(order), error => error.MemberNames.Contains("code") && error.ErrorMessage == "code is not accepted.");
    }

    [Fact]
    public void ValidateChildReportsNestedAndIndexedKeys()
    {
        var order = new Order { Code = "ok", Lines = [new Line { Quantity = 1 }, new Line { Quantity = 0 }], Shipping = new Address { City = "" } };
        var errors = Validate(order);
        Assert.Contains(errors, error => error.MemberNames.Contains("lines[1].quantity"));
        Assert.Contains(errors, error => error.MemberNames.Contains("shipping.city"));
    }

    [Fact]
    public void FluentRulesCoverConditionsCollectionsChildrenAndObjectLevelPredicates()
    {
        var transfer = new Transfer { From = "a", To = "a", Kind = "cash", Amount = 0, Lines = [new Line { Quantity = 0 }], Shipping = new Address { City = " " } };
        var errors = new ValidationErrors().Add(Validate(transfer));

        Assert.Equal("to must not be equal to From.", Assert.Single(errors["to"]));
        Assert.Equal("amount must be greater than 0.", Assert.Single(errors["amount"]));
        Assert.Contains("lines[0].quantity", errors.Keys);
        Assert.Contains("shipping.city", errors.Keys);
        Assert.Equal("A transfer needs a reason.", Assert.Single(errors[""]));

        transfer.Kind = "card";
        Assert.DoesNotContain("amount", new ValidationErrors().Add(Validate(transfer)).Keys);
    }

    [Fact]
    public void FluentChainMessagesAndValueEquality()
    {
        var transfer = new Transfer { From = "", To = "b", Kind = "card", Amount = 1, Reason = "x", Reference = "nope" };
        var errors = new ValidationErrors().Add(Validate(transfer));
        Assert.Equal("Where does it come from?", Assert.Single(errors["from"]));
        Assert.Equal("reference must be equal to 'REF'.", Assert.Single(errors["reference"]));
    }

    [Fact]
    public void MessagesGoThroughTheLocalizer()
    {
        var services = new ServiceCollection().AddSingleton<IValidationMessageLocalizer>(new UpperCaseLocalizer()).BuildServiceProvider();
        var context = new ValidationContext(new Dates(), services, null) { MemberName = nameof(Dates.From), DisplayName = nameof(Dates.From) };

        var result = new NotEmptyAttribute().GetValidationResult(null, context);

        Assert.Equal("from MUST NOT BE EMPTY.", result!.ErrorMessage);
    }

    [Fact]
    public async Task AsyncRulesBuildTheSameErrorMap()
    {
        var errors = await new AsyncRules<Transfer>()
            .For(x => x.Reference).MustAsync((reference, _) => Task.FromResult(reference == "free"), "{0} '{1}' is taken.")
            .And.MustAsync((transfer, _) => Task.FromResult(transfer.Amount > 0), "Nothing to transfer.")
            .ValidateAsync(new Transfer { Reference = "taken", Amount = 0 }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("reference 'taken' is taken.", Assert.Single(errors["reference"]));
        Assert.Equal("Nothing to transfer.", Assert.Single(errors[""]));
        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void ErrorKeysAreWireNames()
    {
        var errors = new ValidationErrors().Add("DisplayName", "x").Add("items[0].Name", "y").Add("", "z");
        Assert.Equal(["", "displayName", "items[0].Name"], errors.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("renamed", MemberKey.Of(typeof(Renamed).GetProperty(nameof(Renamed.Original))!));
    }

    private static bool Check(ValidationAttribute attribute, object? value) =>
        attribute.GetValidationResult(value, new ValidationContext(new object()) { MemberName = "value", DisplayName = "value" }) == ValidationResult.Success;

    private static List<ValidationResult> Validate(object instance)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
        var flat = new List<ValidationResult>();
        foreach (var result in results)
        {
            if (result is CompositeValidationResult composite)
            {
                flat.AddRange(composite.Results);
            }
            else
            {
                flat.Add(result);
            }
        }

        return flat;
    }

    private sealed class Dates
    {
        public DateTime From { get; set; }

        [GreaterThan(nameof(From))]
        public DateTime To { get; set; }
    }

    private sealed class Password
    {
        [NotEqualTo(nameof(Current))]
        public string? New { get; set; }

        [EqualTo(nameof(New))]
        public string? Confirm { get; set; }

        public string? Current { get; set; }
    }

    private sealed class Line
    {
        [GreaterThan(0)]
        public int Quantity { get; set; }
    }

    private sealed class Address
    {
        [NotEmpty]
        public string? City { get; set; }
    }

    private sealed class Order
    {
        [Must(nameof(Accepts), "{0} is not accepted.")]
        public string? Code { get; set; }

        [ValidateChild]
        public List<Line> Lines { get; set; } = [];

        [ValidateChild]
        public Address? Shipping { get; set; }

        public static bool Accepts(string? code) => code == "ok";
    }

    private sealed class Transfer : Validated<Transfer>
    {
        public string? From { get; set; }

        public string? To { get; set; }

        public string? Kind { get; set; }

        public decimal Amount { get; set; }

        public string? Reason { get; set; }

        public string? Reference { get; set; }

        public List<Line> Lines { get; set; } = [];

        public Address? Shipping { get; set; }

        protected override void Rules(RuleSet<Transfer> rules)
        {
            rules.For(x => x.From).NotEmpty().WithMessage("Where does it come from?");
            rules.For(x => x.To).NotEmpty().NotEqual(x => x.From);
            rules.For(x => x.Amount).GreaterThan(0).When(x => x.Kind == "cash");
            rules.For(x => x.Reference).Equal("REF").Unless(x => x.Reference is null);
            rules.ForEach(x => x.Lines, line => line.For(l => l.Quantity).GreaterThan(0));
            rules.Child(x => x.Shipping);
            rules.Must(x => !string.IsNullOrEmpty(x.Reason), "A transfer needs a reason.");
        }
    }

    private sealed class Renamed
    {
        [System.Text.Json.Serialization.JsonPropertyName("renamed")]
        public string? Original { get; set; }
    }

    private sealed class UpperCaseLocalizer : IValidationMessageLocalizer
    {
        public string Localize(string template) => template.ToUpperInvariant();
    }
}
