using Xunit;

namespace AiteBar.Tests;

[CollectionDefinition("LocalizationStateTestCollection", DisableParallelization = true)]
public sealed class LocalizationStateTestCollection : ICollectionFixture<WpfTestFixture>
{
}
