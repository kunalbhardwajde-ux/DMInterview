using LmsTracker.Api.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LmsTracker.Api.Tests;

public sealed class EndpointModuleExtensionsTests
{
    [Fact]
    public void AddEndpointModules_RegistersAllConcreteModules()
    {
        var services = new ServiceCollection();

        services.AddEndpointModules();

        using var provider = services.BuildServiceProvider();
        var registeredModules = provider.GetServices<IEndpointModule>().ToList();

        var expectedModuleCount = typeof(IEndpointModule).Assembly
            .GetTypes()
            .Count(t => t is { IsAbstract: false, IsInterface: false } && typeof(IEndpointModule).IsAssignableFrom(t));

        Assert.Equal(expectedModuleCount, registeredModules.Count);
        Assert.All(registeredModules, module => Assert.NotNull(module));
    }
}
