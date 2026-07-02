namespace MatrixTea.Engine.Core;

/// <summary>
/// Simple stack-based scene manager for menu/gameplay/editor flows.
/// </summary>
public sealed class SceneStack
{
    private readonly Stack<IScene> _stack = new();

    public IScene? Current => _stack.Count > 0 ? _stack.Peek() : null;

    public int Count => _stack.Count;

    public void Push(IScene scene, EngineContext context)
    {
        _stack.Push(scene);
        scene.Enter(context);
    }

    public void Pop(EngineContext context)
    {
        if (_stack.Count == 0)
        {
            return;
        }

        IScene scene = _stack.Pop();
        scene.Exit(context);
    }

    public void Replace(IScene scene, EngineContext context)
    {
        Clear(context);
        Push(scene, context);
    }

    public void Clear(EngineContext context)
    {
        while (_stack.Count > 0)
        {
            Pop(context);
        }
    }

    public void Update(EngineContext context, TimeSpan delta)
    {
        Current?.Update(context, delta);
    }

    public void Render(EngineContext context, IRenderSurface surface)
    {
        Current?.Render(context, surface);
    }
}