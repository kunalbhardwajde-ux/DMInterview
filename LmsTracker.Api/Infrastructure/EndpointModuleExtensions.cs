using System.Reflection;

namespace LmsTracker.Api.Infrastructure;

public static class EndpointModuleExtensions
{
    public static IServiceCollection AddEndpointModules(this IServiceCollection services)
    {
        var moduleType = typeof(IEndpointModule);

        var moduleInstances = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && moduleType.IsAssignableFrom(t))
            .Select(Activator.CreateInstance)
            .Cast<IEndpointModule>()
            .ToArray();

        foreach (var module in moduleInstances)
        {
            services.AddSingleton(module);
        }

        return services;
    }

    public static IApplicationBuilder MapEndpointModules(this WebApplication app)
    {
        var modules = app.Services.GetServices<IEndpointModule>();

        foreach (var module in modules)
        {
            module.MapEndpoints(app);
        }

        return app;
    }
}
