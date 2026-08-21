using Buscarro.CatalogSync.Worker;

namespace Buscarro.Tests;

public sealed class CatalogRollbackArgumentsTests
{
    [Fact]
    public void Parses_exact_one_shot_rollback_shape()
    {
        var value=CatalogRollbackArguments.Parse(["rollback","--provider","Carzin","--target-generation","41"]);
        Assert.NotNull(value);Assert.Equal("Carzin",value.Provider);Assert.Equal(41,value.TargetGenerationId);
    }

    [Theory]
    [InlineData("rollback --provider Carzin")]
    [InlineData("rollback --provider Carzin --target-generation 0")]
    [InlineData("rollback --provider Carzin --target-generation 41 extra")]
    public void Rejects_incomplete_or_ambiguous_shape(string command)
        =>Assert.Throws<ArgumentException>(()=>CatalogRollbackArguments.Parse(command.Split(' ')));
}
