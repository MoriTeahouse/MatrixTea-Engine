namespace MatrixTea.Engine.Core;

/// <summary>Commits scene lifetime changes after callbacks finish.</summary>
public sealed class SceneStack
{
    private readonly Stack<IScene> _stack = new();
    private readonly Queue<Action> _pending = new();
    private bool _dispatching;
    public IScene? Current => _stack.Count > 0 ? _stack.Peek() : null;
    public int Count => _stack.Count;
    public void Push(IScene scene, EngineContext context)
    {
        ArgumentNullException.ThrowIfNull(scene);
        Dispatch(() => { scene.Enter(context); _stack.Push(scene); });
    }
    public void Pop(EngineContext context) => Dispatch(() =>
    {
        if (_stack.Count > 0) _stack.Pop().Exit(context);
    });
    public void Replace(IScene scene, EngineContext context)
    {
        ArgumentNullException.ThrowIfNull(scene);
        Dispatch(() =>
        {
            while (_stack.Count > 0) _stack.Pop().Exit(context);
            scene.Enter(context);
            _stack.Push(scene);
        });
    }
    public void Clear(EngineContext context) => Dispatch(() =>
    {
        while (_stack.Count > 0) _stack.Pop().Exit(context);
    });
    public void Update(EngineContext context, TimeSpan delta) => Dispatch(() => Current?.Update(context, delta));
    public void Render(EngineContext context, IRenderSurface surface) => Dispatch(() => Current?.Render(context, surface));
    private void Dispatch(Action action)
    {
        if (_dispatching) { _pending.Enqueue(action); return; }
        _dispatching = true;
        try
        {
            action();
            int transitions = 0;
            while (_pending.Count > 0)
            {
                if (++transitions > 256) throw new InvalidOperationException("Recursive scene transition loop.");
                _pending.Dequeue()();
            }
        }
        catch { _pending.Clear(); throw; }
        finally { _dispatching = false; }
    }
}
