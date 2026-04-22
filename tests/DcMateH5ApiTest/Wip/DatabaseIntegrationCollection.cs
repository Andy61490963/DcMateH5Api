using Xunit;

namespace DcMateH5ApiTest.Wip;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DatabaseIntegrationCollection
{
    public const string Name = "DatabaseIntegration";
}
