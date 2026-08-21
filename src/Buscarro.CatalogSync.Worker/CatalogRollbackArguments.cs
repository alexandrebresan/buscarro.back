namespace Buscarro.CatalogSync.Worker;

public sealed record CatalogRollbackArguments(string Provider,int TargetGenerationId)
{
    public static CatalogRollbackArguments? Parse(string[] args)
    {
        if(args.Length==0)return null;
        if(args.Length!=5||!string.Equals(args[0],"rollback",StringComparison.Ordinal)||
            !string.Equals(args[1],"--provider",StringComparison.Ordinal)||string.IsNullOrWhiteSpace(args[2])||
            !string.Equals(args[3],"--target-generation",StringComparison.Ordinal)||!int.TryParse(args[4],out var id)||id<=0)
            throw new ArgumentException("Usage: rollback --provider <name> --target-generation <positive-id>.");
        return new(args[2].Trim(),id);
    }
}
