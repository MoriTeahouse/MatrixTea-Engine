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

	protected MatrixTeaGame(string title, EngineOptions? options = null)
	{
		Options = options ?? EngineOptions.Create(title);
		Engine = new EngineApplication(Options);
		_graphics = new GraphicsDeviceManager(this);
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
		_contentLoaded = true;
		OnLoadContent();
		base.LoadContent();
	}

	protected override void Update(GameTime gameTime)
	{
		TimeSpan frameDelta = gameTime.ElapsedGameTime;
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
		Engine.Render(new MonoGameRenderSurface(GraphicsDevice, SpriteBatch));
		base.Draw(gameTime);
	}

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
