namespace MatrixTea.Engine.Core;

/// <summary>
/// Small DI-free service registry for engine subsystems.
/// </summary>
public sealed class ServiceRegistry
{
    private readonly Dictionary<Type, object> _services;

    internal ServiceRegistry(Dictionary<Type, object> services)
    {
        _services = services;
    }

    public void AddSingleton<TService>(TService service)
        where TService : class
    {
        _services[typeof(TService)] = service;
    }

    public bool TryGet<TService>(out TService? service)
        where TService : class
    {
        if (_services.TryGetValue(typeof(TService), out object? value) && value is TService typed)
        {
            service = typed;
            return true;
        }

        service = null;
        return false;
    }

    public TService GetRequired<TService>()
        where TService : class
    {
        if (TryGet<TService>(out TService? service) && service is not null)
        {
            return service;
        }

        throw new InvalidOperationException($"Service '{typeof(TService).Name}' is not registered.");
    }
}