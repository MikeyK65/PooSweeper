namespace PooSweep.Services;

/// <summary>Access to the DI container for controls created from XAML.</summary>
public static class ServiceHelper
{
    public static IServiceProvider? Services { get; set; }

    public static T? Get<T>() where T : class => Services?.GetService(typeof(T)) as T;
}
