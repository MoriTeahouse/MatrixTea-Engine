using MatrixTea.Engine.Core;
using MatrixTea.Engine.MonoGame;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using (var game = new SmokeGame()) game.Run();
Console.WriteLine("PASS: MonoGame/OpenGL fixed updates, reusable surface, deferred GPU scene replacement and shutdown.");

sealed class SmokeGame : MatrixTeaGame
{
    private int _frames;
    private ProbeScene? _initial, _replacement;
    public SmokeGame() : base("MatrixTea host verification", new EngineOptions { Title = "MatrixTea host verification", PauseWhenInactive = false })
    { Graphics.PreferredBackBufferWidth = 480; Graphics.PreferredBackBufferHeight = 270; }
    protected override void OnLoadContent()
    {
        if (IsFixedTimeStep) throw new Exception("Nested fixed pacing still enabled.");
        _replacement = new ProbeScene(Engine, () => CheckSurface(), null);
        _initial = new ProbeScene(Engine, () => CheckSurface(), _replacement);
        Engine.Scenes.Push(_initial, Engine.Context);
    }
    private void CheckSurface()
    {
        if (_initial?.Disposed == true && ReferenceEquals(Engine.Scenes.Current, _initial)) throw new Exception("Disposed scene is still active.");
    }
    protected override void OnDraw(GameTime gameTime)
    {
        if (++_frames < 30) return;
        if (Engine.Context.Metrics.UpdateCount == 0 || _initial?.Disposed != true || _replacement?.Draws == 0) throw new Exception("Host smoke did not advance.");
        Exit();
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && _replacement != null && !_replacement.Disposed) throw new Exception("Scene resource was not disposed.");
    }
    private sealed class ProbeScene(EngineApplication engine, Action check, IScene? next) : IScene
    {
        private Texture2D? _texture; private IRenderSurface? _surface;
        public string Name => "GPU probe";
        public bool Disposed { get; private set; }
        public int Draws { get; private set; }
        public void Enter(EngineContext context) { }
        public void Exit(EngineContext context) { _texture?.Dispose(); Disposed = true; }
        public void Update(EngineContext context, TimeSpan delta) { if (delta != context.Options.TargetUpdateStep) throw new Exception("Incorrect fixed step."); }
        public void Render(EngineContext context, IRenderSurface surface)
        {
            if (_surface != null && !ReferenceEquals(_surface, surface)) throw new Exception("Render surface allocated per frame.");
            _surface = surface; check();
            var graphics = (MonoGameRenderSurface)surface;
            _texture ??= new Texture2D(graphics.GraphicsDevice, 1, 1); _texture.SetData(new[] { Color.ForestGreen });
            graphics.SpriteBatch!.Begin(); graphics.SpriteBatch.Draw(_texture, new Rectangle(20, 20, 440, 230), Color.White); graphics.SpriteBatch.End();
            if (++Draws == 5 && next != null)
            {
                engine.Scenes.Replace(next, context);
                if (Disposed) throw new Exception("GPU resource disposed during render callback.");
            }
        }
    }
}
