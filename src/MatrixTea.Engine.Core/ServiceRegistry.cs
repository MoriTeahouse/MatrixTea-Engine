// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 MoriTeahouse (森之宿茶室)
using System.Diagnostics.CodeAnalysis;

namespace MatrixTea.Engine.Core;

/// <summary>
/// Small DI-free service registry for engine subsystems.
/// </summary>
public sealed class ServiceRegistry : IDisposable
{
    private readonly Dictionary<Type, object> _services;
    private readonly List<IDisposable> _owned = new();
    private readonly HashSet<object> _ownedIdentities = new(ReferenceEqualityComparer.Instance);
    private bool _disposed;
    public ServiceRegistry() : this(new Dictionary<Type, object>()) { }
    public int Count => _services.Count;

    internal ServiceRegistry(Dictionary<Type, object> services)
    {
        _services = services;
    }

    public void AddSingleton<TService>(TService service)
        where TService : class
    {
        ThrowIfDisposed(); ArgumentNullException.ThrowIfNull(service);
        _services[typeof(TService)] = service;
    }

    public void AddOwnedSingleton<TService>(TService service) where TService : class, IDisposable
    {
        AddSingleton(service);
        if (_ownedIdentities.Add(service)) _owned.Add(service);
    }
    public bool Remove<TService>() where TService : class { ThrowIfDisposed(); return _services.Remove(typeof(TService)); }

    public bool TryGet<TService>([NotNullWhen(true)] out TService? service)
        where TService : class
    {
        ThrowIfDisposed();
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; List<Exception>? errors = null;
        for (int index = _owned.Count - 1; index >= 0; index--)
            try { _owned[index].Dispose(); } catch (Exception ex) { (errors ??= new()).Add(ex); }
        _owned.Clear(); _ownedIdentities.Clear(); _services.Clear();
        if (errors != null) throw new AggregateException("One or more owned services failed to dispose.", errors);
    }
    private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(ServiceRegistry)); }
}
