using Grove.Display.Rules.Models;

namespace Grove.Display.Rules.Services;

/// <summary>How the rule editor names and explains the built-in condition types; a type another feature registers is shown by its name.</summary>
internal static class RuleConditionDescriptions
{
    public static (string DisplayName, string Description, string Category) Describe(string name) => name switch
    {
        nameof(AllConditionGroup) => ("All", "All of its conditions are true.", "Groups"),
        nameof(AnyConditionGroup) => ("Any", "At least one of its conditions is true.", "Groups"),
        nameof(BooleanCondition) => ("Boolean", "Always true, or always false.", "General"),
        nameof(HomepageCondition) => ("Homepage", "The current page is the homepage, or is not.", "Request"),
        nameof(UrlCondition) => ("Url", "Compares the current address with a value.", "Request"),
        nameof(CultureCondition) => ("Culture", "Compares the current culture with a value.", "Request"),
        nameof(ContentTypeCondition) => ("Content type", "Compares the types of the displayed content items with a value.", "Content"),
        nameof(RoleCondition) => ("Role", "Compares the current user's roles with a value.", "Security"),
        nameof(IsAuthenticatedCondition) => ("Is authenticated", "The current user is signed in.", "Security"),
        nameof(IsAnonymousCondition) => ("Is anonymous", "The current user is not signed in.", "Security"),
        nameof(JavascriptCondition) => ("JavaScript", "A script that returns true or false.", "Scripting"),
        _ => (name, string.Empty, "Other"),
    };
}
