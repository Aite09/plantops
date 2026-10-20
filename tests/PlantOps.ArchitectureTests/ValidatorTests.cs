using FluentValidation;
using NetArchTest.Rules;

namespace PlantOps.ArchitectureTests;

public sealed class ValidatorTests
{
    [Fact]
    public void Validators_are_sealed()
    {
        var result = Validators()
            .Should().BeSealed()
            .GetResult();

        result.ShouldPass();
    }

    // Validators are found by assembly scanning and run by ValidationBehavior; nothing else should use them.
    [Fact]
    public void Validators_are_not_public()
    {
        var result = Validators()
            .Should().NotBePublic()
            .GetResult();

        result.ShouldPass();
    }

    [Fact]
    public void Validator_names_end_with_Validator()
    {
        var result = Validators()
            .Should().HaveNameEndingWith("Validator")
            .GetResult();

        result.ShouldPass();
    }

    private static PredicateList Validators()
    {
        var validators = Types.InAssembly(Layers.Application)
            .That().Inherit(typeof(AbstractValidator<>));

        // A rule over no types always passes, so make sure the filter still finds the validators.
        validators.GetTypes().Should().NotBeEmpty();
        return validators;
    }
}
