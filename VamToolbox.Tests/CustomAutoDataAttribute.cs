using AutoFixture.Xunit2;

namespace VamToolbox.Tests;

public sealed class CustomAutoDataAttribute : AutoDataAttribute
{
    public CustomAutoDataAttribute() : base(() => new CustomFixture())
    {

    }
}