using System.Numerics;
using Bliss.CSharp;
using Bliss.CSharp.Interact;
using Bliss.CSharp.Logging;
using Bliss.CSharp.Rendering;
using Bliss.CSharp.Rendering.Vulkan;
using Bliss.CSharp.Rendering.Vulkan.Descriptor;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using SilkWindow = Silk.NET.Windowing.Window;

namespace Bliss.Test;

public class Game : Disposable {
    
    public static Game Instance { get; private set; }
    
    public Vk Vk { get; private set; }
    public IWindow Window { get; private set; }
    public BlissDevice Device { get; private set; }
    public BlissRenderer Renderer { get; private set; }
    public BlissDescriptorPool GlobalPool { get; private set; }
    
    private readonly double _fixedTimeStep;
    private double _timer;

    public Game() {
        Instance = this;
        this._fixedTimeStep = 1.0F / 60;
    }

    public void Run() {
        Logger.Info("Hello World! Bliss start...");
        
        Logger.Info("Initialize Vulkan...");
        this.Vk = Vk.GetApi();
        
        Logger.Info("Initialize Window...");
        this.Window = SilkWindow.Create(WindowOptions.DefaultVulkan with {
            Title = "Test Game!",
            Size = new Vector2D<int>(1270, 720)
        });
        
        this.Window.Update += this.RunLoop;
        this.Window.Render += this.Draw;
        
        this.Window.Initialize();
        
        if (this.Window.VkSurface == null) {
            throw new PlatformNotSupportedException("Windowing platform doesn't support Vulkan.");
        }
        
        Logger.Info("Initialize Device...");
        this.Device = new BlissDevice(this.Vk, this.Window);

        Logger.Info("Initialize Renderer...");
        this.Renderer = new BlissRenderer(this.Vk, this.Window, this.Device, false);

        Logger.Info("Initialize Global Pool...");
        this.GlobalPool = new BlissDescriptorPoolBuilder(this.Vk, this.Device)
            .SetMaxSets(BlissSwapChain.MaxDefaultFramesInFlight)
            .AddSize(DescriptorType.UniformBuffer, BlissSwapChain.MaxDefaultFramesInFlight)
            .Build();
        
        Logger.Info("Initialize Input...");
        Input.Init(this.Window);
        
        this.Init();
        
        Logger.Info("Start main Loops...");
        this.Window.Run();
        this.Vk.DeviceWaitIdle(this.Device.GetVkDevice());
    }
    
    protected virtual void RunLoop(double delta) {
        Input.BeginInput();
        
        this.Update(delta);
        this.AfterUpdate(delta);
        
        this._timer += delta;
        while (this._timer >= this._fixedTimeStep) {
            this.FixedUpdate();
            this._timer -= this._fixedTimeStep;
        }
        
        Input.EndInput();
    }

    protected virtual void Init() { }

    protected virtual void Update(double delta) {
        if (Input.IsKeyPressed(Key.A)) {
            Logger.Error("KEY [A] GOT PRESSED!");
        }
        
        if (Input.IsKeyDown(Key.S)) {
            Logger.Error("KEY [S] IS DOWN!");
        }
        
        if (Input.IsKeyReleased(Key.D)) {
            Logger.Error("KEY [D] IS RELEASED!");
        }

        foreach (var test in Input.GetPressedChars()) {
            Logger.Error(test + "");
        }
        
        if (Input.IsMouseDoubleClicked(MouseButton.Left, out Vector2 pos)) {
            Logger.Error($"Mouse clicked at pos: {pos}");
        }
        
        if (Input.IsMouseMoving(out Vector2 ghg)) {
            Logger.Error($"Mouse moved at pos: {ghg}");
        }

        if (Input.IsMouseScrolling(out ScrollWheel? scrollWheel)) {
            Logger.Error($"Mouse scroll: {scrollWheel!.Value.Y}");
        }

        if (Input.IsGamepadTriggerMoved(1)) {
            Logger.Error($"Trigger moved: {Input.GetGamepadTriggers(0)[1].Position}");
        }

        if (Input.IsGamepadThumpStickMoved(1)) {
            Logger.Error($"Thumpsticks moved: {Input.GetGamepadThumbsticks(0)[1].Position}");
        }
        
        if (Input.IsGamepadButtonDown(ButtonName.A)) {
            Logger.Error("PRESSED A");
        }
    }

    protected virtual void AfterUpdate(double delta) { }

    protected virtual void FixedUpdate() { }

    protected virtual void Draw(double delta) { }
    
    protected override void Dispose(bool disposing) {
        if (disposing) {
            this.Window.Dispose();
            this.Renderer.Dispose();
            this.GlobalPool.Dispose();
            this.Device.Dispose();
            this.Vk.Dispose();
        }
    }
}