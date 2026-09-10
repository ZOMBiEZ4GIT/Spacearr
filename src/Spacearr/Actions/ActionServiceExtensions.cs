namespace Spacearr.Actions;

public static class ActionServiceExtensions
{
    public static IServiceCollection AddSpacearrActions(this IServiceCollection services)
    {
        services.AddSingleton<ConfirmTokens>();
        services.AddScoped<ActionPlanner>();
        return services;
    }
}
