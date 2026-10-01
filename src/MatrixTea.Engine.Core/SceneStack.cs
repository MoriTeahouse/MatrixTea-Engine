namespace MatrixTea.Engine.Core;

/// <summary>Single-thread scene lifetimes with deferred, bounded transitions.</summary>
public sealed class SceneStack
{
    private enum Operation { Push, Pop, Replace, Clear }
    private readonly record struct Transition(Operation Operation, IScene? Scene, EngineContext Context);
    private readonly Stack<IScene> _stack = new();
    private readonly HashSet<IScene> _active = new(ReferenceEqualityComparer.Instance);
    private readonly Queue<Transition> _pending = new();
    private bool _dispatching, _closed;
    private int _ownerThread;
    private const int TransitionLimit = 256;
    public IScene? Current => _stack.TryPeek(out var scene) ? scene : null;
    public int Count => _stack.Count;
    internal bool IsDispatching => _dispatching;
    public void Push(IScene scene, EngineContext context) => Schedule(Operation.Push, scene, context);
    public void Pop(EngineContext context) => Schedule(Operation.Pop, null, context);
    /// <summary>Replaces the entire stack; outgoing scenes exit before the new scene enters.</summary>
    public void Replace(IScene scene, EngineContext context) => Schedule(Operation.Replace, scene, context);
    public void Clear(EngineContext context) => Schedule(Operation.Clear, null, context);
    public void Update(EngineContext context, TimeSpan delta)
    {
        Begin(context);
        try { Current?.Update(context, delta); Drain(); }
        catch { _pending.Clear(); throw; }
        finally { _dispatching = false; }
    }
    public void Render(EngineContext context, IRenderSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface); Begin(context);
        try { Current?.Render(context, surface); Drain(); }
        catch { _pending.Clear(); throw; }
        finally { _dispatching = false; }
    }
    public void Shutdown(EngineContext context)
    {
        if (_closed) return;
        Begin(context); _closed = true; _pending.Clear();
        try { ExitAll(context); }
        finally { _pending.Clear(); _dispatching = false; }
    }
    private void CheckThread()
    {
        int thread = Environment.CurrentManagedThreadId;
        int owner = Interlocked.CompareExchange(ref _ownerThread, thread, 0);
        if (owner != 0 && owner != thread) throw new InvalidOperationException("SceneStack must be accessed on its owning thread.");
    }
    private void Begin(EngineContext context)
    {
        ArgumentNullException.ThrowIfNull(context); CheckThread();
        if (_closed) throw new ObjectDisposedException(nameof(SceneStack));
        if (_dispatching) throw new InvalidOperationException("Recursive scene update/render is not supported.");
        _dispatching = true;
    }
    private void Schedule(Operation operation, IScene? scene, EngineContext context)
    {
        ArgumentNullException.ThrowIfNull(context); CheckThread();
        if (_closed) throw new ObjectDisposedException(nameof(SceneStack));
        if (operation is Operation.Push or Operation.Replace) ArgumentNullException.ThrowIfNull(scene);
        if (_pending.Count >= TransitionLimit) throw new InvalidOperationException("Scene transition queue limit exceeded.");
        var transition = new Transition(operation, scene, context);
        if (_dispatching) { _pending.Enqueue(transition); return; }
        _dispatching = true;
        try { Apply(transition); Drain(); }
        catch { _pending.Clear(); throw; }
        finally { _dispatching = false; }
    }
    private void Drain()
    {
        int count = 0;
        while (_pending.TryDequeue(out var transition))
        {
            if (++count > TransitionLimit) throw new InvalidOperationException("Recursive scene transition loop.");
            Apply(transition);
        }
    }
    private void Apply(Transition transition)
    {
        if (transition.Scene != null && _active.Contains(transition.Scene)) throw new InvalidOperationException("A scene instance is already active.");
        switch (transition.Operation)
        {
            case Operation.Clear: ExitAll(transition.Context); break;
            case Operation.Pop: ExitOne(transition.Context); break;
            case Operation.Replace: ExitAll(transition.Context); Enter(transition.Scene!, transition.Context); break;
            case Operation.Push: Enter(transition.Scene!, transition.Context); break;
        }
    }
    private void Enter(IScene scene, EngineContext context)
    {
        try { scene.Enter(context); }
        catch (Exception enterError)
        {
            try { scene.Exit(context); }
            catch (Exception exitError) { throw new AggregateException("Scene entry and rollback both failed.", enterError, exitError); }
            throw;
        }
        _stack.Push(scene); _active.Add(scene);
    }
    private void ExitOne(EngineContext context)
    {
        if (!_stack.TryPop(out var scene)) return;
        _active.Remove(scene); scene.Exit(context);
    }
    private void ExitAll(EngineContext context)
    {
        List<Exception>? errors = null;
        while (_stack.Count > 0)
            try { ExitOne(context); } catch (Exception ex) { (errors ??= new()).Add(ex); }
        if (errors != null) throw new AggregateException("One or more scenes failed to exit.", errors);
    }
}
