using MatrixTea.Engine.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MatrixTea.Engine.MonoGame;

/// <summary>
/// MonoGame host base class that wires the MatrixTea runtime into Game.
/// </summary>
public abstract class MatrixTeaGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private bool _contentLoaded;
    private MonoGameRenderSurface? _renderSurface;
    private bool _wasInactive;

    protected MatrixTeaGame(string title, EngineOptions? options = null)
    {
        Options = options ?? EngineOptions.Create(title);
        Engine = new EngineApplication(Options);
        _graphics = new GraphicsDeviceManager(this);
        IsFixedTimeStep = false;
        _graphics.SynchronizeWithVerticalRetrace = Options.SynchronizeWithVerticalRetrace;
        Content.RootDirectory = "Content";
        WindowTitle = Options.Title;
    }

    protected EngineApplication Engine { get; }

    protected EngineOptions Options { get; }

    protected SpriteBatch? SpriteBatch { get; private set; }

    protected GraphicsDeviceManager Graphics => _graphics;

    protected virtual Color ClearColor => Color.Black;

    protected override void Initialize()
    {
        Window.Title = WindowTitle;
        IsMouseVisible = true;
        base.Initialize();
    }

    protected override void LoadContent()
    {
        SpriteBatch = new SpriteBatch(GraphicsDevice);
        _renderSurface = new MonoGameRenderSurface(GraphicsDevice, SpriteBatch);
        _contentLoaded = true;
        OnLoadContent();
        Engine.ResetTiming();
        base.LoadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        if (!IsActive && Options.PauseWhenInactive)
        { _wasInactive = true; Engine.ResetTiming(); base.Update(gameTime); return; }
        if (_wasInactive) { Engine.ResetTiming(); _wasInactive = false; }
        TimeSpan captured = Engine.Context.Clock.Capture();
        TimeSpan frameDelta = Options.UseHighResolutionTiming ? captured : gameTime.ElapsedGameTime;
        OnBeforeFixedUpdates(gameTime);
        int updateCount = Engine.Pump(frameDelta);

        for (int index = 0; index < updateCount; index++)
        {
            Engine.Update(Engine.Context.Options.TargetUpdateStep);
            OnFixedUpdate(Engine.Context.Options.TargetUpdateStep);
        }

        OnUpdate(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(ClearColor);
        OnDraw(gameTime);
        if (_renderSurface != null) Engine.Render(_renderSurface);
        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        try { Engine.Scenes.Clear(Engine.Context); }
        finally
        {
            SpriteBatch?.Dispose(); SpriteBatch = null;
            _renderSurface = null; _contentLoaded = false;
            base.UnloadContent();
        }
    }

    protected override void Dispose(bool disposing)
    {
        try { base.Dispose(disposing); }
        finally { if (disposing) Engine.Dispose(); }
    }

    /// <summary>Captures input once per host frame, before any fixed updates.</summary>
    protected virtual void OnBeforeFixedUpdates(GameTime gameTime) { }

    protected virtual void OnLoadContent()
    {
    }

    protected virtual void OnFixedUpdate(TimeSpan fixedDelta)
    {
    }

    protected virtual void OnUpdate(GameTime gameTime)
    {
    }

    protected virtual void OnDraw(GameTime gameTime)
    {
    }

    protected string WindowTitle { get; set; }

    protected bool ContentLoaded => _contentLoaded;
}

public sealed class MonoGameRenderSurface : IRenderSurface
{
    public MonoGameRenderSurface(GraphicsDevice graphicsDevice, SpriteBatch? spriteBatch)
    {
        GraphicsDevice = graphicsDevice;
        SpriteBatch = spriteBatch;
    }

    public GraphicsDevice GraphicsDevice { get; }

    public SpriteBatch? SpriteBatch { get; }
}
