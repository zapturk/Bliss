using System.Numerics;
using System.Runtime.InteropServices;
using Bliss.CSharp.Interact.Keyboards;
using Bliss.CSharp.Interact.Mice;
using Bliss.CSharp.Logging;
using Bliss.CSharp.Windowing.Events;
using SDL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Veldrid;
using Veldrid.OpenGL;
using Point = Bliss.CSharp.Transformations.Point;

namespace Bliss.CSharp.Windowing;

public class Sdl3Window : Disposable, IWindow {
    
    /// <summary>
    /// Specifies the initialization flags for SDL, which control which SDL subsystems are initialized when starting the window.
    /// </summary>
    private const SDL_InitFlags InitFlags = SDL_InitFlags.SDL_INIT_VIDEO | SDL_InitFlags.SDL_INIT_GAMEPAD | SDL_InitFlags.SDL_INIT_JOYSTICK;

    /// <summary>
    /// Sets the number of events that are processed in each loop iteration when handling window events.
    /// </summary>
    private const int EventsPerPeep = 64;

    /// <summary>
    /// Represents the native handle of the SDL window, which is used for low-level window operations and interactions.
    /// </summary>
    public nint Handle { get; private set; }

    /// <summary>
    /// Represents the unique identifier for the SDL window.
    /// This identifier can be used to reference or differentiate between multiple windows.
    /// </summary>
    public uint Id { get; private set; }

    /// <summary>
    /// Represents the source of the swapchain, which is used for managing the rendering surface and synchronization for the window.
    /// </summary>
    public SwapchainSource SwapchainSource { get; private set; }

    /// <summary>
    /// Indicates whether the window currently exists.
    /// </summary>
    public bool Exists { get; private set; }

    /// <summary>
    /// Indicates whether the window is currently focused.
    /// </summary>
    public bool IsFocused { get; private set; }
    
    /// <summary>
    /// Represents an event that is triggered whenever an SDL event occurs within the window.
    /// </summary>
    public event Action<SDL_Event>? SdlEvent; 
    
    /// <summary>
    /// Occurs when the window is resized.
    /// </summary>
    public event Action? Resized;
    
    /// <summary>
    /// Occurs after the window has closed.
    /// </summary>
    public event Action? Closed;

    /// <summary>
    /// Occurs when the window gains focus.
    /// </summary>
    public event Action? FocusGained;

    /// <summary>
    /// Occurs when the window loses focus.
    /// </summary>
    public event Action? FocusLost;

    /// <summary>
    /// Occurs when the window is shown.
    /// </summary>
    public event Action? Shown;

    /// <summary>
    /// Occurs when the window is hidden.
    /// </summary>
    public event Action? Hidden;

    /// <summary>
    /// Occurs when the window is exposed (made visible or unhidden).
    /// </summary>
    public event Action? Exposed;

    /// <summary>
    /// Occurs when the window is moved.
    /// </summary>
    public event Action<Point>? Moved;

    /// <summary>
    /// Occurs when the mouse enters the window.
    /// </summary>
    public event Action? MouseEntered;

    /// <summary>
    /// Occurs when the mouse leaves the window.
    /// </summary>
    public event Action? MouseLeft;

    /// <summary>
    /// Occurs when the mouse wheel is scrolled.
    /// </summary>
    public event Action<Vector2>? MouseWheel;

    /// <summary>
    /// Occurs when the mouse is moved.
    /// </summary>
    public event Action<Vector2>? MouseMove;

    /// <summary>
    /// Occurs when a mouse button is pressed.
    /// </summary>
    public event Action<MouseEvent>? MouseDown;

    /// <summary>
    /// Occurs when a mouse button is released.
    /// </summary>
    public event Action<MouseEvent>? MouseUp;

    /// <summary>
    /// Occurs when a key is pressed.
    /// </summary>
    public event Action<KeyEvent>? KeyDown;

    /// <summary>
    /// Occurs when a key is released.
    /// </summary>
    public event Action<KeyEvent>? KeyUp;

    /// <summary>
    /// Occurs when a drag-and-drop operation is performed.
    /// </summary>
    public event Action<DragDropEvent>? DragDrop;

    /// <summary>
    /// Represents the current state of the SDL window, such as whether it is resizable, full screen, maximized, minimized, hidden, etc.
    /// </summary>
    private WindowState _state;

    /// <summary>
    /// Contains a collection of SDL_Event objects used for polling and handling SDL events.
    /// </summary>
    private readonly SDL_Event[] _events;
    
    /// <summary>
    /// Holds information about the OpenGL platform specific to the current window, including the context, function pointers, and other related details.
    /// </summary>
    private OpenGLPlatformInfo? _openGlPlatformInfo;
    
    /// <summary>
    /// Stores the maximum supported OpenGL version as a tuple of major and minor version numbers.
    /// </summary>
    private (int, int)? _maxSupportedGlVersion;

    /// <summary>
    /// Stores the maximum supported OpenGL ES (GLES) version.
    /// The value is a tuple where the first item represents the major version, and the second item represents the minor version.
    /// </summary>
    private (int, int)? _maxSupportedGlEsVersion;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="Sdl3Window"/> class with the specified width, height, title, and window state.
    /// </summary>
    /// <param name="width">The width of the window in pixels.</param>
    /// <param name="height">The height of the window in pixels.</param>
    /// <param name="title">The title of the window.</param>
    /// <param name="state">The initial state of the window, specified as a <see cref="WindowState"/> value.</param>
    /// <exception cref="Exception">Thrown if SDL fails to initialize the subsystem required for creating the window.</exception>
    public unsafe Sdl3Window(int width, int height, string title, WindowState state) {
        this.Exists = true;
        
        SDL3.SDL_SetHint(SDL3.SDL_HINT_MOUSE_FOCUS_CLICKTHROUGH, "1");
        
        if (SDL3.SDL_InitSubSystem(InitFlags) == SDL_bool.SDL_FALSE) {
            throw new Exception($"Failed to initialise SDL! Error: {SDL3.SDL_GetError()}");
        }
        
        // Enable events.
        SDL3.SDL_SetGamepadEventsEnabled(SDL_bool.SDL_TRUE);
        SDL3.SDL_SetJoystickEventsEnabled(SDL_bool.SDL_TRUE);
        
        this.Handle = (nint) SDL3.SDL_CreateWindow(title, width, height, this.MapWindowState(state) | SDL_WindowFlags.SDL_WINDOW_OPENGL);
        this.Id = (uint) SDL3.SDL_GetWindowID((SDL_Window*) this.Handle);
        this.SwapchainSource = this.CreateSwapchainSource();
        
        this._events = new SDL_Event[EventsPerPeep];
    }
    
    /// <summary>
    /// Retrieves a module handle for the specified module.
    /// </summary>
    /// <param name="lpModuleName">A pointer to a null-terminated string that specifies the name of the module. If this parameter is null, GetModuleHandleW returns a handle to the file used to create the calling process.</param>
    /// <returns>A handle to the specified module, or null if the module is not found.</returns>
    /// <exception cref="System.ComponentModel.Win32Exception">Thrown if an error occurs when retrieving the module handle.</exception>
    [DllImport("kernel32", ExactSpelling = true)]
    private static extern unsafe nint GetModuleHandleW(ushort* lpModuleName);
    
    /// <summary>
    /// Retrieves the current state of the window.
    /// </summary>
    /// <returns>The current state of the window represented by the <see cref="WindowState"/> enumeration.</returns>
    public WindowState GetState() {
        return this._state;
    }

    /// <summary>
    /// Determines if the current window state matches the specified state.
    /// </summary>
    /// <param name="state">The window state to compare with the current state.</param>
    /// <returns>True if the current window state matches the specified state; otherwise, false.</returns>
    public bool HasState(WindowState state) {
        return this._state.HasFlag(state);
    }

    /// <summary>
    /// Sets the state of the window to the specified state.
    /// </summary>
    /// <param name="state">The desired state for the window, specified as a <see cref="WindowState"/>.</param>
    public unsafe void SetState(WindowState state) {
        this._state = state;

        if (state.HasFlag(WindowState.Resizable)) {
            SDL3.SDL_SetWindowResizable((SDL_Window*) this.Handle, SDL_bool.SDL_TRUE);
        }
        if (state.HasFlag(WindowState.FullScreen)) {
            SDL3.SDL_SetWindowFullscreen((SDL_Window*) this.Handle, SDL_bool.SDL_TRUE);
        }
        if (state.HasFlag(WindowState.BorderlessFullScreen)) {
            SDL3.SDL_SetWindowBordered((SDL_Window*) this.Handle, SDL_bool.SDL_TRUE);
        }
        if (state.HasFlag(WindowState.Maximized)) {
            SDL3.SDL_MaximizeWindow((SDL_Window*) this.Handle);
        }
        if (state.HasFlag(WindowState.Minimized)) {
            SDL3.SDL_MinimizeWindow((SDL_Window*) this.Handle);
        }
        if (state.HasFlag(WindowState.Hidden)) {
            SDL3.SDL_HideWindow((SDL_Window*) this.Handle);
        }
        if (state.HasFlag(WindowState.CaptureMouse)) {
            SDL3.SDL_CaptureMouse(SDL_bool.SDL_TRUE);
        }
        if (state.HasFlag(WindowState.AlwaysOnTop)) {
            SDL3.SDL_SetWindowAlwaysOnTop((SDL_Window*) this.Handle, SDL_bool.SDL_TRUE);
        }
    }

    /// <summary>
    /// Resets the window to its default state by clearing various settings such as resizability, fullscreen mode, border visibility, and always-on-top status.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the window handle is invalid.</exception>
    public unsafe void ClearState() {
        this._state = WindowState.None;
        
        SDL3.SDL_SetWindowResizable((SDL_Window*) this.Handle, SDL_bool.SDL_FALSE);
        SDL3.SDL_SetWindowFullscreen((SDL_Window*) this.Handle, SDL_bool.SDL_FALSE);
        SDL3.SDL_SetWindowBordered((SDL_Window*) this.Handle, SDL_bool.SDL_TRUE);
        SDL3.SDL_ShowWindow((SDL_Window*) this.Handle);
        SDL3.SDL_CaptureMouse(SDL_bool.SDL_FALSE);
        SDL3.SDL_SetWindowAlwaysOnTop((SDL_Window*) this.Handle, SDL_bool.SDL_FALSE);
    }

    /// <summary>
    /// Retrieves the title of the window.
    /// </summary>
    /// <returns>The title of the window as a string.</returns>
    public unsafe string GetTitle() {
        return SDL3.SDL_GetWindowTitle((SDL_Window*) this.Handle) ?? string.Empty;
    }

    /// <summary>
    /// Sets the title of the window.
    /// </summary>
    /// <param name="title">The new title to set for the window.</param>
    /// <exception cref="System.InvalidOperationException">Thrown if the title could not be set due to an internal error.</exception>
    public unsafe void SetTitle(string title) {
        if (SDL3.SDL_SetWindowTitle((SDL_Window*) this.Handle, title) == SDL_bool.SDL_FALSE) {
            Logger.Warn($"Failed to set the title of the window: [{this.Id}] Error: {SDL3.SDL_GetError()}");
        }
    }

    /// <summary>
    /// Retrieves the current width and height of the window in pixels.
    /// </summary>
    /// <returns>A tuple containing two integers representing the width and height of the window in pixels.</returns>
    public unsafe (int, int) GetSize() {
        int width;
        int height;
        
        if (SDL3.SDL_GetWindowSizeInPixels((SDL_Window*) this.Handle, &width, &height) == SDL_bool.SDL_FALSE) {
            Logger.Warn($"Failed to get the size of the window: [{this.Id}] Error: {SDL3.SDL_GetError()}");
        }

        return (width, height);
    }

    /// <summary>
    /// Sets the size of the window to the specified width and height.
    /// </summary>
    /// <param name="width">The new width of the window.</param>
    /// <param name="height">The new height of the window.</param>
    public unsafe void SetSize(int width, int height) {
        if (SDL3.SDL_SetWindowSize((SDL_Window*) this.Handle, width, height) == SDL_bool.SDL_FALSE) {
            Logger.Warn($"Failed to set the size of the window: [{this.Id}] Error: {SDL3.SDL_GetError()}");
        }
    }

    /// <summary>
    /// Gets the width of the window.
    /// </summary>
    /// <returns>The width of the window in pixels.</returns>
    public int GetWidth() {
        return this.GetSize().Item1;
    }

    /// <summary>
    /// Sets the width of the window.
    /// </summary>
    /// <param name="width">The new width of the window.</param>
    public void SetWidth(int width) {
        this.SetSize(width, this.GetHeight());
    }

    /// <summary>
    /// Retrieves the height of the window.
    /// </summary>
    /// <returns>The height of the window in pixels.</returns>
    public int GetHeight() {
        return this.GetSize().Item2;
    }

    /// <summary>
    /// Sets the height of the window to the specified value.
    /// </summary>
    /// <param name="height">The new height of the window.</param>
    public void SetHeight(int height) {
        this.SetSize(this.GetWidth(), height);
    }

    /// <summary>
    /// Retrieves the current position of the window.
    /// </summary>
    /// <returns>A tuple containing the x and y coordinates of the window's position.</returns>
    /// <exception cref="System.Exception">Thrown if there is an error retrieving the window's position.</exception>
    public unsafe (int, int) GetPosition() {
        int x;
        int y;
        
        if (SDL3.SDL_GetWindowPosition((SDL_Window*) this.Handle, &x, &y) == SDL_bool.SDL_FALSE) {
            Logger.Warn($"Failed to set the position to the window: [{this.Id}] Error: {SDL3.SDL_GetError()}");
        }
        
        return (x, y);
    }

    /// <summary>
    /// Sets the position of the window on the screen.
    /// </summary>
    /// <param name="x">The x-coordinate of the window position.</param>
    /// <param name="y">The y-coordinate of the window position.</param>
    public unsafe void SetPosition(int x, int y) {
        SDL3.SDL_SetWindowPosition((SDL_Window*) this.Handle, x, y);
    }

    /// <summary>
    /// Retrieves the current X-coordinate position of the window.
    /// </summary>
    /// <returns>The X-coordinate of the window's position.</returns>
    public int GetX() {
        return this.GetPosition().Item1;
    }

    /// <summary>
    /// Sets the X coordinate of the window's position.
    /// </summary>
    /// <param name="x">The new X coordinate of the window.</param>
    public void SetX(int x) {
        this.SetPosition(x, this.GetY());
    }

    /// <summary>
    /// Retrieves the Y-coordinate of the window's position.
    /// </summary>
    /// <returns>The Y-coordinate of the window's position.</returns>
    public int GetY() {
        return this.GetPosition().Item2;
    }

    /// <summary>
    /// Sets the Y-coordinate of the window's position.
    /// </summary>
    /// <param name="y">The new Y-coordinate.</param>
    public void SetY(int y) {
        this.SetPosition(this.GetX(), y);
    }

    /// <summary>
    /// Sets the icon for the SDL3 window using the provided image.
    /// </summary>
    /// <param name="image">The image to set as the window icon. It should be of type <see cref="Image{Rgba32}"/>.</param>
    /// <exception cref="Exception">Thrown if an error occurs while setting the window icon.</exception>
    public unsafe void SetIcon(Image<Rgba32> image) {
        byte[] data = new byte[image.Width * image.Height * 4];
        image.CopyPixelDataTo(data);

        fixed (byte* dataPtr = data) {
            SDL_Surface* surface = SDL3.SDL_CreateSurfaceFrom(image.Width, image.Height, SDL_PixelFormat.SDL_PIXELFORMAT_ABGR8888, (nint) dataPtr, image.Width * 4);

            if ((nint) surface == nint.Zero) {
                Logger.Error($"Failed to set Sdl3 window icon: {SDL3.SDL_GetError()}");
            }

            SDL3.SDL_SetWindowIcon((SDL_Window*) this.Handle, surface);
            SDL3.SDL_DestroySurface(surface);
        }
    }

    /// <summary>
    /// Processes all pending events for the window and invokes corresponding event handlers.
    /// </summary>
    /// <exception cref="System.ComponentModel.Win32Exception">Thrown if an error occurs when processing events.</exception>
    public void PumpEvents() {
        SDL3.SDL_PumpEvents();
        int eventsRead;
        
        do {
            eventsRead = SDL3.SDL_PeepEvents(this._events, SDL_EventAction.SDL_GETEVENT, SDL_EventType.SDL_EVENT_FIRST, SDL_EventType.SDL_EVENT_LAST);
            for (int i = 0; i < eventsRead; i++) {
                this.HandleEvent(this._events[i]);
            }
        } while (eventsRead == EventsPerPeep);
    }

    /// <summary>
    /// Converts a point from client-area coordinates to screen coordinates.
    /// </summary>
    /// <param name="point">The point in client-area coordinates to be converted.</param>
    /// <returns>The point in screen coordinates.</returns>
    public Point ClientToScreen(Point point) {
        return new Point(point.X + this.GetX(), point.Y + this.GetY());
    }

    /// <summary>
    /// Converts a point from screen coordinates to client coordinates.
    /// </summary>
    /// <param name="point">The point in screen coordinates to be converted.</param>
    /// <returns>The point in client coordinates.</returns>
    public Point ScreenToClient(Point point) {
        return new Point(point.X - this.GetX(), point.Y - this.GetY());
    }

    /// <summary>
    /// Retrieves or creates the OpenGL platform information for the current window.
    /// </summary>
    /// <param name="options">Options for configuring the graphics device.</param>
    /// <param name="backend">The graphics backend to use.</param>
    /// <returns>The OpenGL platform information associated with the current window.</returns>
    /// <exception cref="VeldridException">Thrown if unable to create the OpenGL context, potentially due to insufficient system support for the requested profile, version, or swapchain format.</exception>
    public unsafe OpenGLPlatformInfo GetOrCreateOpenGlPlatformInfo(GraphicsDeviceOptions options, GraphicsBackend backend) {
        if (this._openGlPlatformInfo == null) {
            SDL3.SDL_ClearError();

            this.SetGlContextAttributes(options, backend);

            SDL_GLContextState* contextHandle = SDL3.SDL_GL_CreateContext((SDL_Window*) this.Handle);
            string error = SDL3.SDL_GetError() ?? string.Empty;
        
            if (error != string.Empty) {
                throw new VeldridException($"Unable to create OpenGL Context: \"{error}\". This may indicate that the system does not support the requested OpenGL profile, version, or Swapchain format.");
            }

            int actualDepthSize;
            int actualStencilSize;

            SDL3.SDL_GL_GetAttribute(SDL_GLattr.SDL_GL_DEPTH_SIZE, &actualDepthSize);
            SDL3.SDL_GL_GetAttribute(SDL_GLattr.SDL_GL_STENCIL_SIZE, &actualStencilSize);
            SDL3.SDL_GL_SetSwapInterval(options.SyncToVerticalBlank ? 1 : 0);

            OpenGLPlatformInfo platformInfo = new OpenGLPlatformInfo(
                (nint) contextHandle,
                proc => SDL3.SDL_GL_GetProcAddress(proc),
                context => SDL3.SDL_GL_MakeCurrent((SDL_Window*) this.Handle, (SDL_GLContextState*) context),
                () => (nint) SDL3.SDL_GL_GetCurrentContext(),
                () => SDL3.SDL_GL_MakeCurrent((SDL_Window*) this.Handle, (SDL_GLContextState*) nint.Zero),
                context => SDL3.SDL_GL_DestroyContext((SDL_GLContextState*) context),
                () => SDL3.SDL_GL_SwapWindow((SDL_Window*) this.Handle),
                sync => SDL3.SDL_GL_SetSwapInterval(sync ? 1 : 0)
            );

            this._openGlPlatformInfo = platformInfo;
            return platformInfo;
        }
        else {
            return this._openGlPlatformInfo;
        }
    }

    /// <summary>
    /// Configures the SDL GL context attributes based on the provided graphics device options and backend.
    /// </summary>
    /// <param name="options">The options that specify various settings for the graphics device.</param>
    /// <param name="backend">The graphics backend in use (OpenGL or OpenGLES).</param>
    /// <exception cref="System.Exception">Thrown if the graphics backend is not OpenGL or OpenGLES.</exception>
    private void SetGlContextAttributes(GraphicsDeviceOptions options, GraphicsBackend backend) {
       if (backend != GraphicsBackend.OpenGL && backend != GraphicsBackend.OpenGLES) {
           throw new Exception($"GraphicsBackend must be: [{nameof(GraphicsBackend.OpenGL)}] or [{nameof(GraphicsBackend.OpenGLES)}]!");
       }

       SDL_GLcontextFlag contextFlags = options.Debug ? (SDL_GLcontextFlag.SDL_GL_CONTEXT_DEBUG_FLAG | SDL_GLcontextFlag.SDL_GL_CONTEXT_FORWARD_COMPATIBLE_FLAG) : SDL_GLcontextFlag.SDL_GL_CONTEXT_FORWARD_COMPATIBLE_FLAG;
       SDL3.SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_CONTEXT_FLAGS, (int) contextFlags);

       (int major, int minor) = this.GetMaxGlVersion(backend == GraphicsBackend.OpenGLES);

       if (backend == GraphicsBackend.OpenGL) {
           SDL3.SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_CONTEXT_PROFILE_MASK, (int) SDL_GLprofile.SDL_GL_CONTEXT_PROFILE_CORE);
           SDL3.SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_CONTEXT_MAJOR_VERSION, major);
           SDL3.SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_CONTEXT_MINOR_VERSION, minor);
       }
       else {
           SDL3.SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_CONTEXT_PROFILE_MASK, (int) SDL_GLprofile.SDL_GL_CONTEXT_PROFILE_ES);
           SDL3.SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_CONTEXT_MAJOR_VERSION, major);
           SDL3.SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_CONTEXT_MINOR_VERSION, minor);
       }

       int depthBits = 0;
       int stencilBits = 0;
       
       if (options.SwapchainDepthFormat.HasValue) {
           switch (options.SwapchainDepthFormat) {
               case PixelFormat.R16UNorm:
                   depthBits = 16;
                   break;
               case PixelFormat.D24UNormS8UInt:
                   depthBits = 24;
                   stencilBits = 8;
                   break;
               case PixelFormat.R32Float:
                   depthBits = 32;
                   break;
               case PixelFormat.D32FloatS8UInt:
                   depthBits = 32;
                   stencilBits = 8;
                   break;
               default:
                   throw new VeldridException($"Invalid depth format: [{options.SwapchainDepthFormat.Value}]!");
           }
       }

       SDL3.SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_DEPTH_SIZE, depthBits);
       SDL3.SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_STENCIL_SIZE, stencilBits);
       SDL3.SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_FRAMEBUFFER_SRGB_CAPABLE, options.SwapchainSrgbFormat ? 1 : 0);
    }
    
    /// <summary>
    /// Retrieves the maximum OpenGL or OpenGL ES version supported by the system.
    /// </summary>
    /// <param name="openGlEs">Specifies whether to query for OpenGL ES (true) or OpenGL (false) version.</param>
    /// <return>Returns a tuple containing the major and minor version numbers of the supported OpenGL or OpenGL ES version.</return>
    private (int, int) GetMaxGlVersion(bool openGlEs) {
        object glVersionLock = new object();
        
        lock (glVersionLock) {
            (int, int)? maxVersion = openGlEs ? this._maxSupportedGlEsVersion : this._maxSupportedGlVersion;

            if (maxVersion == null) {
                maxVersion = this.TestMaxGlVersion(openGlEs);

                if (openGlEs) {
                    this._maxSupportedGlEsVersion = maxVersion;
                }
                else {
                    this._maxSupportedGlVersion = maxVersion;
                }
            }

            return maxVersion.Value;
        }
    }

    /// <summary>
    /// Tests the maximum supported OpenGL version for OpenGL or OpenGL ES.
    /// </summary>
    /// <param name="openGlEs">Indicates whether to test for OpenGL ES versions. If false, tests for standard OpenGL versions.</param>
    /// <returns>
    /// A tuple containing two integers: the major and minor versions of the maximum supported OpenGL (or OpenGL ES) version.
    /// If no supported version is found, returns (0, 0).
    /// </returns>
    private (int, int) TestMaxGlVersion(bool openGlEs) {
        (int, int)[] testVersions = openGlEs 
            ? [
                (3, 2),
                (3, 0)
            ]
            : [
                (4, 6),
                (4, 3),
                (4, 0),
                (3, 3),
                (3, 0)
            ];

        foreach ((int major, int minor) in testVersions) {
            if (this.TestIndividualGlVersion(openGlEs, major, minor)) {
                return (major, minor);
            }
        }

        return (0, 0);
    }
    
    /// <summary>
    /// Tests the creation of an OpenGL or OpenGL ES context with the specified major and minor version numbers.
    /// </summary>
    /// <param name="openGlEs">Specifies whether to create an OpenGL ES context. Otherwise, creates an OpenGL context.</param>
    /// <param name="major">The major version number of the context to create.</param>
    /// <param name="minor">The minor version number of the context to create.</param>
    /// <return>True if the context was successfully created; otherwise, false.</return>
    private unsafe bool TestIndividualGlVersion(bool openGlEs, int major, int minor) {
        SDL_GLprofile profileMask = openGlEs ? SDL_GLprofile.SDL_GL_CONTEXT_PROFILE_ES : SDL_GLprofile.SDL_GL_CONTEXT_PROFILE_CORE;

        SDL3.SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_CONTEXT_PROFILE_MASK, (int) profileMask);
        SDL3.SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_CONTEXT_MAJOR_VERSION, major);
        SDL3.SDL_GL_SetAttribute(SDL_GLattr.SDL_GL_CONTEXT_MINOR_VERSION, minor);

        SDL_Window* window = SDL3.SDL_CreateWindow(string.Empty, 1, 1, SDL_WindowFlags.SDL_WINDOW_HIDDEN | SDL_WindowFlags.SDL_WINDOW_OPENGL);
        string windowError = SDL3.SDL_GetError() ?? string.Empty;
        
        if ((nint) window == nint.Zero || windowError != string.Empty) {
            SDL3.SDL_ClearError();
            Logger.Debug($"Unable to create version {major}.{minor} {profileMask} context.");
            return false;
        }

        SDL_GLContextState* context = SDL3.SDL_GL_CreateContext(window);
        string contextError = SDL3.SDL_GetError() ?? string.Empty;

        if (contextError != string.Empty) {
            SDL3.SDL_ClearError();
            Logger.Debug($"Unable to create version {major}.{minor} {profileMask} context.");
            SDL3.SDL_DestroyWindow(window);
            return false;
        }

        SDL3.SDL_GL_DestroyContext(context);
        SDL3.SDL_DestroyWindow(window);
        return true;
    }

    /// <summary>
    /// Creates a SwapchainSource for use with the current operating system.
    /// </summary>
    /// <returns>A SwapchainSource configured for the underlying windowing system.</returns>
    /// <exception cref="PlatformNotSupportedException">Thrown if the current operating system is not supported.</exception>
    private unsafe SwapchainSource CreateSwapchainSource() {
        if (OperatingSystem.IsWindows()) {
            nint hwnd = SDL3.SDL_GetPointerProperty(SDL3.SDL_GetWindowProperties((SDL_Window*) this.Handle), SDL3.SDL_PROP_WINDOW_WIN32_HWND_POINTER, nint.Zero);
            nint hInstance = GetModuleHandleW(null);
            return SwapchainSource.CreateWin32(hwnd, hInstance);
        }
        else if (OperatingSystem.IsLinux()) {
            if (SDL3.SDL_strcmp(SDL3.SDL_GetCurrentVideoDriver(), "x11") == 0) {
                nint display = SDL3.SDL_GetPointerProperty(SDL3.SDL_GetWindowProperties((SDL_Window*) this.Handle), SDL3.SDL_PROP_WINDOW_X11_DISPLAY_POINTER, nint.Zero);
                nint surface = SDL3.SDL_GetPointerProperty(SDL3.SDL_GetWindowProperties((SDL_Window*) this.Handle), SDL3.SDL_PROP_WINDOW_X11_WINDOW_NUMBER, 0);
                return SwapchainSource.CreateXlib(display, surface);
            }
            else if (SDL3.SDL_strcmp(SDL3.SDL_GetCurrentVideoDriver(), "wayland") == 0) {
                nint display = SDL3.SDL_GetPointerProperty(SDL3.SDL_GetWindowProperties((SDL_Window*) this.Handle), SDL3.SDL_PROP_WINDOW_WAYLAND_DISPLAY_POINTER, nint.Zero);
                nint surface = SDL3.SDL_GetPointerProperty(SDL3.SDL_GetWindowProperties((SDL_Window*) this.Handle), SDL3.SDL_PROP_WINDOW_WAYLAND_SURFACE_POINTER, nint.Zero);
                return SwapchainSource.CreateWayland(display, surface);
            }
            else {
                throw new Exception($"The driver: [{SDL3.SDL_GetCurrentVideoDriver()}] is not supported!");
            }
        }
        else if (OperatingSystem.IsMacOS()) {
            nint surface = SDL3.SDL_GetPointerProperty(SDL3.SDL_GetWindowProperties((SDL_Window*) this.Handle), SDL3.SDL_PROP_WINDOW_COCOA_WINDOW_POINTER, nint.Zero);
            return SwapchainSource.CreateNSWindow(surface);
        }
        
        throw new PlatformNotSupportedException("Filed to create a SwapchainSource!");
    }

    /// <summary>
    /// Maps a given <see cref="WindowState"/> to the corresponding <see cref="SDL_WindowFlags"/>.
    /// </summary>
    /// <param name="state">The state of the window to map, specified as <see cref="WindowState"/>.</param>
    /// <returns>The corresponding <see cref="SDL_WindowFlags"/> for the provided <paramref name="state"/>.</returns>
    /// <exception cref="Exception">Thrown when an invalid <see cref="WindowState"/> is provided.</exception>
    private SDL_WindowFlags MapWindowState(WindowState state) {
        switch (state) {
            case WindowState.Resizable:
                return SDL_WindowFlags.SDL_WINDOW_RESIZABLE;
            case WindowState.FullScreen:
                return SDL_WindowFlags.SDL_WINDOW_FULLSCREEN;
            case WindowState.BorderlessFullScreen:
                return SDL_WindowFlags.SDL_WINDOW_BORDERLESS;
            case WindowState.Maximized:
                return SDL_WindowFlags.SDL_WINDOW_MAXIMIZED;
            case WindowState.Minimized:
                return SDL_WindowFlags.SDL_WINDOW_MINIMIZED;
            case WindowState.Hidden:
                return SDL_WindowFlags.SDL_WINDOW_HIDDEN;
            case WindowState.CaptureMouse:
                return SDL_WindowFlags.SDL_WINDOW_MOUSE_CAPTURE;
            case WindowState.AlwaysOnTop:
                return SDL_WindowFlags.SDL_WINDOW_ALWAYS_ON_TOP;
            default:
                throw new Exception($"Invalid WindowState: [{state}]");
        }
    }

    /// <summary>
    /// Handles a given SDL event and triggers the appropriate window event based on the type of the SDL event.
    /// </summary>
    /// <param name="sdlEvent">The SDL event to handle.</param>
    private void HandleEvent(SDL_Event sdlEvent) {
        this.SdlEvent?.Invoke(sdlEvent);
        
        switch (sdlEvent.Type) {
            case SDL_EventType.SDL_EVENT_QUIT:
            case SDL_EventType.SDL_EVENT_TERMINATING:
                this.Exists = false;
                this.Closed?.Invoke();
                break;
            case SDL_EventType.SDL_EVENT_WINDOW_RESIZED:
            case SDL_EventType.SDL_EVENT_WINDOW_PIXEL_SIZE_CHANGED:
            case SDL_EventType.SDL_EVENT_WINDOW_MINIMIZED:
            case SDL_EventType.SDL_EVENT_WINDOW_MAXIMIZED:
            case SDL_EventType.SDL_EVENT_WINDOW_RESTORED:
                this.Resized?.Invoke();
                break;
            case SDL_EventType.SDL_EVENT_WINDOW_FOCUS_GAINED:
                this.IsFocused = true;
                this.FocusGained?.Invoke();
                break;
            case SDL_EventType.SDL_EVENT_WINDOW_FOCUS_LOST:
                this.IsFocused = false;
                this.FocusLost?.Invoke();
                break;
            case SDL_EventType.SDL_EVENT_WINDOW_SHOWN:
                this.Shown?.Invoke();
                break;
            case SDL_EventType.SDL_EVENT_WINDOW_HIDDEN:
                this.Hidden?.Invoke();
                break;
            case SDL_EventType.SDL_EVENT_WINDOW_MOUSE_ENTER:
                this.MouseEntered?.Invoke();
                break;
            case SDL_EventType.SDL_EVENT_WINDOW_MOUSE_LEAVE:
                this.MouseLeft?.Invoke();
                break;
            case SDL_EventType.SDL_EVENT_WINDOW_EXPOSED:
                this.Exposed?.Invoke();
                break;
            case SDL_EventType.SDL_EVENT_WINDOW_MOVED:
                this.Moved?.Invoke(new Point(sdlEvent.window.data1, sdlEvent.window.data2));
                break;
            case SDL_EventType.SDL_EVENT_MOUSE_WHEEL:
                this.MouseWheel?.Invoke(new Vector2(sdlEvent.wheel.x, sdlEvent.wheel.y));
                break;
            case SDL_EventType.SDL_EVENT_MOUSE_MOTION:
                this.MouseMove?.Invoke(new Vector2(sdlEvent.motion.y, sdlEvent.motion.x));
                break;
            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN:
                this.MouseDown?.Invoke(new MouseEvent((MouseButton) sdlEvent.button.Button, sdlEvent.button.down == SDL_bool.SDL_TRUE));
                break;
            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP:
                this.MouseUp?.Invoke(new MouseEvent((MouseButton) sdlEvent.button.Button, sdlEvent.button.down == SDL_bool.SDL_TRUE));
                break;
            case SDL_EventType.SDL_EVENT_KEY_DOWN:
                this.KeyDown?.Invoke(new KeyEvent(this.MapKey(sdlEvent.key.scancode), sdlEvent.key.down == SDL_bool.SDL_TRUE, sdlEvent.key.repeat == SDL_bool.SDL_TRUE));
                break;
            case SDL_EventType.SDL_EVENT_KEY_UP:
                this.KeyUp?.Invoke(new KeyEvent(this.MapKey(sdlEvent.key.scancode), sdlEvent.key.down == SDL_bool.SDL_TRUE, sdlEvent.key.repeat == SDL_bool.SDL_TRUE));
                break;
            case SDL_EventType.SDL_EVENT_DROP_FILE:
                this.DragDrop?.Invoke(new DragDropEvent((int) sdlEvent.drop.x, (int) sdlEvent.drop.y, sdlEvent.drop.GetSource() ?? string.Empty));
                break;
        }
    }

    /// <summary>
    /// Maps an SDL_Scancode to the corresponding KeyboardKey.
    /// </summary>
    /// <param name="scancode">The SDL_Scancode representing the key to be mapped.</param>
    /// <returns>The corresponding KeyboardKey for the given SDL_Scancode.</returns>
    private KeyboardKey MapKey(SDL_Scancode scancode) {
        return scancode switch {
            SDL_Scancode.SDL_SCANCODE_A => KeyboardKey.A,
            SDL_Scancode.SDL_SCANCODE_B => KeyboardKey.B,
            SDL_Scancode.SDL_SCANCODE_C => KeyboardKey.C,
            SDL_Scancode.SDL_SCANCODE_D => KeyboardKey.D,
            SDL_Scancode.SDL_SCANCODE_E => KeyboardKey.E,
            SDL_Scancode.SDL_SCANCODE_F => KeyboardKey.F,
            SDL_Scancode.SDL_SCANCODE_G => KeyboardKey.G,
            SDL_Scancode.SDL_SCANCODE_H => KeyboardKey.H,
            SDL_Scancode.SDL_SCANCODE_I => KeyboardKey.I,
            SDL_Scancode.SDL_SCANCODE_J => KeyboardKey.J,
            SDL_Scancode.SDL_SCANCODE_K => KeyboardKey.K,
            SDL_Scancode.SDL_SCANCODE_L => KeyboardKey.L,
            SDL_Scancode.SDL_SCANCODE_M => KeyboardKey.M,
            SDL_Scancode.SDL_SCANCODE_N => KeyboardKey.N,
            SDL_Scancode.SDL_SCANCODE_O => KeyboardKey.O,
            SDL_Scancode.SDL_SCANCODE_P => KeyboardKey.P,
            SDL_Scancode.SDL_SCANCODE_Q => KeyboardKey.Q,
            SDL_Scancode.SDL_SCANCODE_R => KeyboardKey.R,
            SDL_Scancode.SDL_SCANCODE_S => KeyboardKey.S,
            SDL_Scancode.SDL_SCANCODE_T => KeyboardKey.T,
            SDL_Scancode.SDL_SCANCODE_U => KeyboardKey.U,
            SDL_Scancode.SDL_SCANCODE_V => KeyboardKey.V,
            SDL_Scancode.SDL_SCANCODE_W => KeyboardKey.W,
            SDL_Scancode.SDL_SCANCODE_X => KeyboardKey.X,
            SDL_Scancode.SDL_SCANCODE_Y => KeyboardKey.Y,
            SDL_Scancode.SDL_SCANCODE_Z => KeyboardKey.Z,
            SDL_Scancode.SDL_SCANCODE_1 => KeyboardKey.Number1,
            SDL_Scancode.SDL_SCANCODE_2 => KeyboardKey.Number2,
            SDL_Scancode.SDL_SCANCODE_3 => KeyboardKey.Number3,
            SDL_Scancode.SDL_SCANCODE_4 => KeyboardKey.Number4,
            SDL_Scancode.SDL_SCANCODE_5 => KeyboardKey.Number5,
            SDL_Scancode.SDL_SCANCODE_6 => KeyboardKey.Number6,
            SDL_Scancode.SDL_SCANCODE_7 => KeyboardKey.Number7,
            SDL_Scancode.SDL_SCANCODE_8 => KeyboardKey.Number8,
            SDL_Scancode.SDL_SCANCODE_9 => KeyboardKey.Number9,
            SDL_Scancode.SDL_SCANCODE_0 => KeyboardKey.Number0,
            SDL_Scancode.SDL_SCANCODE_RETURN => KeyboardKey.Enter,
            SDL_Scancode.SDL_SCANCODE_ESCAPE => KeyboardKey.Escape,
            SDL_Scancode.SDL_SCANCODE_BACKSPACE => KeyboardKey.BackSpace,
            SDL_Scancode.SDL_SCANCODE_TAB => KeyboardKey.Tab,
            SDL_Scancode.SDL_SCANCODE_SPACE => KeyboardKey.Space,
            SDL_Scancode.SDL_SCANCODE_MINUS => KeyboardKey.Minus,
            SDL_Scancode.SDL_SCANCODE_EQUALS => KeyboardKey.Plus,
            SDL_Scancode.SDL_SCANCODE_LEFTBRACKET => KeyboardKey.BracketLeft,
            SDL_Scancode.SDL_SCANCODE_RIGHTBRACKET => KeyboardKey.BracketRight,
            SDL_Scancode.SDL_SCANCODE_BACKSLASH => KeyboardKey.BackSlash,
            SDL_Scancode.SDL_SCANCODE_SEMICOLON => KeyboardKey.Semicolon,
            SDL_Scancode.SDL_SCANCODE_APOSTROPHE => KeyboardKey.Quote,
            SDL_Scancode.SDL_SCANCODE_GRAVE => KeyboardKey.Grave,
            SDL_Scancode.SDL_SCANCODE_COMMA => KeyboardKey.Comma,
            SDL_Scancode.SDL_SCANCODE_PERIOD => KeyboardKey.Period,
            SDL_Scancode.SDL_SCANCODE_SLASH => KeyboardKey.Slash,
            SDL_Scancode.SDL_SCANCODE_CAPSLOCK => KeyboardKey.CapsLock,
            SDL_Scancode.SDL_SCANCODE_F1 => KeyboardKey.F1,
            SDL_Scancode.SDL_SCANCODE_F2 => KeyboardKey.F2,
            SDL_Scancode.SDL_SCANCODE_F3 => KeyboardKey.F3,
            SDL_Scancode.SDL_SCANCODE_F4 => KeyboardKey.F4,
            SDL_Scancode.SDL_SCANCODE_F5 => KeyboardKey.F5,
            SDL_Scancode.SDL_SCANCODE_F6 => KeyboardKey.F6,
            SDL_Scancode.SDL_SCANCODE_F7 => KeyboardKey.F7,
            SDL_Scancode.SDL_SCANCODE_F8 => KeyboardKey.F8,
            SDL_Scancode.SDL_SCANCODE_F9 => KeyboardKey.F9,
            SDL_Scancode.SDL_SCANCODE_F10 => KeyboardKey.F10,
            SDL_Scancode.SDL_SCANCODE_F11 => KeyboardKey.F11,
            SDL_Scancode.SDL_SCANCODE_F12 => KeyboardKey.F12,
            SDL_Scancode.SDL_SCANCODE_PRINTSCREEN => KeyboardKey.PrintScreen,
            SDL_Scancode.SDL_SCANCODE_SCROLLLOCK => KeyboardKey.ScrollLock,
            SDL_Scancode.SDL_SCANCODE_PAUSE => KeyboardKey.Pause,
            SDL_Scancode.SDL_SCANCODE_INSERT => KeyboardKey.Insert,
            SDL_Scancode.SDL_SCANCODE_HOME => KeyboardKey.Home,
            SDL_Scancode.SDL_SCANCODE_PAGEUP => KeyboardKey.PageUp,
            SDL_Scancode.SDL_SCANCODE_DELETE => KeyboardKey.Delete,
            SDL_Scancode.SDL_SCANCODE_END => KeyboardKey.End,
            SDL_Scancode.SDL_SCANCODE_PAGEDOWN => KeyboardKey.PageDown,
            SDL_Scancode.SDL_SCANCODE_RIGHT => KeyboardKey.Right,
            SDL_Scancode.SDL_SCANCODE_LEFT => KeyboardKey.Left,
            SDL_Scancode.SDL_SCANCODE_DOWN => KeyboardKey.Down,
            SDL_Scancode.SDL_SCANCODE_UP => KeyboardKey.Up,
            SDL_Scancode.SDL_SCANCODE_NUMLOCKCLEAR => KeyboardKey.NumLock,
            SDL_Scancode.SDL_SCANCODE_KP_DIVIDE => KeyboardKey.KeypadDivide,
            SDL_Scancode.SDL_SCANCODE_KP_MULTIPLY => KeyboardKey.KeypadMultiply,
            SDL_Scancode.SDL_SCANCODE_KP_MINUS => KeyboardKey.KeypadMinus,
            SDL_Scancode.SDL_SCANCODE_KP_PLUS => KeyboardKey.KeypadPlus,
            SDL_Scancode.SDL_SCANCODE_KP_ENTER => KeyboardKey.KeypadEnter,
            SDL_Scancode.SDL_SCANCODE_KP_1 => KeyboardKey.Keypad1,
            SDL_Scancode.SDL_SCANCODE_KP_2 => KeyboardKey.Keypad2,
            SDL_Scancode.SDL_SCANCODE_KP_3 => KeyboardKey.Keypad3,
            SDL_Scancode.SDL_SCANCODE_KP_4 => KeyboardKey.Keypad4,
            SDL_Scancode.SDL_SCANCODE_KP_5 => KeyboardKey.Keypad5,
            SDL_Scancode.SDL_SCANCODE_KP_6 => KeyboardKey.Keypad6,
            SDL_Scancode.SDL_SCANCODE_KP_7 => KeyboardKey.Keypad7,
            SDL_Scancode.SDL_SCANCODE_KP_8 => KeyboardKey.Keypad8,
            SDL_Scancode.SDL_SCANCODE_KP_9 => KeyboardKey.Keypad9,
            SDL_Scancode.SDL_SCANCODE_KP_0 => KeyboardKey.Keypad0,
            SDL_Scancode.SDL_SCANCODE_KP_PERIOD => KeyboardKey.KeypadDecimal,
            SDL_Scancode.SDL_SCANCODE_NONUSBACKSLASH => KeyboardKey.NonUsBackSlash,
            SDL_Scancode.SDL_SCANCODE_KP_EQUALS => KeyboardKey.KeypadPlus,
            SDL_Scancode.SDL_SCANCODE_F13 => KeyboardKey.F13,
            SDL_Scancode.SDL_SCANCODE_F14 => KeyboardKey.F14,
            SDL_Scancode.SDL_SCANCODE_F15 => KeyboardKey.F15,
            SDL_Scancode.SDL_SCANCODE_F16 => KeyboardKey.F16,
            SDL_Scancode.SDL_SCANCODE_F17 => KeyboardKey.F17,
            SDL_Scancode.SDL_SCANCODE_F18 => KeyboardKey.F18,
            SDL_Scancode.SDL_SCANCODE_F19 => KeyboardKey.F19,
            SDL_Scancode.SDL_SCANCODE_F20 => KeyboardKey.F20,
            SDL_Scancode.SDL_SCANCODE_F21 => KeyboardKey.F21,
            SDL_Scancode.SDL_SCANCODE_F22 => KeyboardKey.F22,
            SDL_Scancode.SDL_SCANCODE_F23 => KeyboardKey.F23,
            SDL_Scancode.SDL_SCANCODE_F24 => KeyboardKey.F24,
            SDL_Scancode.SDL_SCANCODE_MENU => KeyboardKey.Menu,
            SDL_Scancode.SDL_SCANCODE_LCTRL => KeyboardKey.ControlLeft,
            SDL_Scancode.SDL_SCANCODE_LSHIFT => KeyboardKey.ShiftLeft,
            SDL_Scancode.SDL_SCANCODE_LALT => KeyboardKey.AltLeft,
            SDL_Scancode.SDL_SCANCODE_RCTRL => KeyboardKey.ControlRight,
            SDL_Scancode.SDL_SCANCODE_RSHIFT => KeyboardKey.ShiftRight,
            SDL_Scancode.SDL_SCANCODE_RALT => KeyboardKey.AltRight,
            SDL_Scancode.SDL_SCANCODE_LGUI => KeyboardKey.WinLeft,
            SDL_Scancode.SDL_SCANCODE_RGUI => KeyboardKey.WinRight,
            _ => KeyboardKey.Unknown
        };
    }

    protected override unsafe void Dispose(bool disposing) {
        if (disposing) {
            SDL3.SDL_QuitSubSystem(InitFlags);
            SDL3.SDL_DestroyWindow((SDL_Window*) this.Handle);
        }
    }
}