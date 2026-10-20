using FluentAssertions.Execution;
// xunit.v3 has its own TestResult, and Xunit is a global using in every test project.
using TestResult = NetArchTest.Rules.TestResult;

namespace PlantOps.ArchitectureTests;

/// <summary>
/// Failure messages that list every offender. FluentAssertions' BeEmpty shows only the first one,
/// which hides how far a violation has spread.
/// </summary>
internal static class ArchitectureAssertions
{
    /// <summary>Asserts that a NetArchTest rule held for every type it was applied to.</summary>
    public static void ShouldPass(this TestResult result) =>
        Execute.Assertion
            .ForCondition(result.IsSuccessful)
            .FailWith("Expected every type to follow the rule, but these do not: {0}.", result.FailingTypeNames);

    /// <summary>Asserts that a reflection-based rule found no violations.</summary>
    public static void ShouldBeNone(this IReadOnlyCollection<string> violations, string because) =>
        Execute.Assertion
            .BecauseOf(because)
            .ForCondition(violations.Count == 0)
            .FailWith("Expected no violations{reason}, but found: {0}.", violations);
}
