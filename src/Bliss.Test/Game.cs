using Bliss.CSharp;
using Bliss.CSharp.Interact;
using Bliss.CSharp.Logging;
using Bliss.CSharp.Windowing;
using Veldrid;
using Veldrid.Sdl2;

namespace Bliss.Test;

public class Game : Disposable {
    
    public static Game Instance { get; private set; }
    public GameSettings Settings { get; private set; }

    public Window Window { get; private set; }
    public GraphicsDevice GraphicsDevice { get; private set; }
    public CommandList CommandList { get; private set; }

    private double _fixedFrameRate;
    
    private readonly double _fixedUpdateTimeStep;
    private double _fixedUpdateTimer;
    
    public Game(GameSettings settings) {
        Instance = this;
        this.Settings = settings;
        this._fixedUpdateTimeStep = settings.FixedTimeStep;
    }

    public void Run() {
        Logger.Info("Hello World! Bliss start...");
        Logger.Info($"\t> CPU: {SystemInfo.Cpu}");
        Logger.Info($"\t> MEMORY: {SystemInfo.MemorySize} GB");
        Logger.Info($"\t> THREADS: {SystemInfo.Threads}");
        Logger.Info($"\t> OS: {SystemInfo.Os}");
        
        Logger.Info("Initialize window and graphics device...");
        GraphicsDeviceOptions options = new GraphicsDeviceOptions() {
            PreferStandardClipSpaceYDirection = true,
            PreferDepthRangeZeroToOne = true
        };
        
        this.Window = new Window(this.Settings.Width, this.Settings.Height, this.Settings.Title, options, this.Settings.Backend, out GraphicsDevice graphicsDevice);
        this.GraphicsDevice = graphicsDevice;
        
        Logger.Info("Initialize time...");
        Time.Init();
        
        Logger.Info($"Set target FPS to: {this.Settings.TargetFps}");
        this.SetTargetFps(this.Settings.TargetFps);
        
        Logger.Info("Initialize command list...");
        this.CommandList = this.GraphicsDevice.ResourceFactory.CreateCommandList();
        
        Logger.Info("Initialize input...");
        Input.Init(this.Window);
        
        this.Init();
        
        Logger.Info("Start main loops...");
        while (this.Window.Exists) {
            if (this.GetTargetFps() != 0 && Time.Timer.Elapsed.TotalSeconds <= this._fixedFrameRate) {
                continue;
            }
            Time.Update();
            
            Sdl2Events.ProcessEvents();
            Input.Begin(this.Window.PumpEvents());
            
            this.Update();
            this.AfterUpdate();

            this._fixedUpdateTimer += Time.Delta;
            while (this._fixedUpdateTimer >= this._fixedUpdateTimeStep) {
                this.FixedUpdate();
                this._fixedUpdateTimer -= this._fixedUpdateTimeStep;
            }
            
            this.Draw(this.GraphicsDevice, this.CommandList);
            Input.End();
        }
        
        Logger.Warn("Application shuts down!");
        this.OnClose();
    }
    
    protected virtual void Init() { }

    protected virtual void Update() { }
    
    protected virtual void AfterUpdate() { }

    protected virtual void FixedUpdate() { }

    protected virtual void Draw(GraphicsDevice graphicsDevice, CommandList commandList) { }
    
    protected virtual void OnClose() { }

    public int GetTargetFps() {
        return (int) (1.0F / this._fixedFrameRate);
    }

    public void SetTargetFps(int fps) {
        this._fixedFrameRate = 1.0F / fps;
    }
    
    protected override void Dispose(bool disposing) {
        if (disposing) {
            this.GraphicsDevice.Dispose();
            this.Window.Close();
            Input.Destroy();
        }
    }
}