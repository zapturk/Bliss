using System.Numerics;
using Bliss.CSharp.Interact.Keyboards;
using Bliss.CSharp.Interact.Mice;
using Bliss.CSharp.Logging;
using Bliss.CSharp.Windowing;
using Bliss.CSharp.Windowing.Events;
using SDL;

namespace Bliss.CSharp.Interact;

// TODO: Rework that with SDL3!
public static class InputOld {

    public static IWindow Window { get; private set; }
    //public static InputSnapshot Snapshot { get; private set; }
    
    // Mouse
    private static (SDL_Cursor?, MouseCursor) _sdlCursor;
    private static List<float> _miceSrolling;
    private static List<Vector2> _miceMoving;
    private static List<MouseButton> _mouseButtonsPressed;
    private static List<MouseButton> _mouseButtonsDown;
    private static List<MouseButton> _mouseButtonsReleased;
    
    // Keyboard
    private static List<KeyboardKey> _keyboardKeysPressed;
    private static List<KeyboardKey> _keyboardKeysDown;
    private static List<KeyboardKey> _keyboardKeysReleased;
    
    // Gamepad
    //private static Dictionary<int, Gamepad> _gamepads;
    
    // Other
    //private static List<string> _filesDragDropped;
    
    /// <summary>
    /// Initializes mouse input tracking and event handlers for the specified window.
    /// </summary>
    /// <param name="window">The window instance to attach mouse input event handlers to.</param>
    public static void Init(IWindow window) {
        Window = window;
        //Sdl2Events.Subscribe(ProcessEvent);
        
        // Mouse
        _miceSrolling = new List<float>();
        _miceMoving = new List<Vector2>();
        _mouseButtonsPressed = new List<MouseButton>();
        _mouseButtonsDown = new List<MouseButton>();
        _mouseButtonsReleased = new List<MouseButton>();
        
        Window.MouseWheel += OnMouseWheel;
        Window.MouseMove += OnMouseMove;
        Window.MouseDown += OnMouseDown;
        Window.MouseUp += OnMouseUp;
        
        // Keyboard
        _keyboardKeysPressed = new List<KeyboardKey>();
        _keyboardKeysDown = new List<KeyboardKey>();
        _keyboardKeysReleased = new List<KeyboardKey>();
        
        Window.KeyDown += OnKeyDown;
        Window.KeyUp += OnKeyUp;
        
        // Gamepads
        //_gamepads = new Dictionary<int, Gamepad>();
        
        // Other
        //_filesDragDropped = new List<string>();
        
        //Window.DragDrop += OnDragDrop;
    }

    /// <summary>
    /// Sets the current input snapshot to the provided instance for processing input data.
    /// </summary>
    /// <param name="snapshot">The input snapshot containing the current state of input devices.</param>
    public static void Begin(/*InputSnapshot snapshot*/) {
        //Snapshot = snapshot;
    }

    /// <summary>
    /// Clears all tracked mouse input data and button states, resetting them for the next input processing cycle.
    /// </summary>
    public static void End() {
        
        // Mouse
        _miceSrolling.Clear();
        _miceMoving.Clear();
        _mouseButtonsPressed.Clear();
        _mouseButtonsReleased.Clear();
        
        // Keyboard
        _keyboardKeysPressed.Clear();
        _keyboardKeysReleased.Clear();
        
        // Gamepad
        //foreach (Gamepad gamepad in _gamepads.Values) {
        //    gamepad.CleanStates();
        //}
        
        // Other
        //_filesDragDropped.Clear();
    }
    
    /* ------------------------------------ Mouse ------------------------------------ */
    
    /// <summary>
    /// Checks if the cursor is currently shown.
    /// </summary>
    /// <returns>True if the cursor is shown; otherwise, false.</returns>
    public static bool IsCursorShown() {
        return SDL3.SDL_CursorVisible() == SDL_bool.SDL_TRUE;
    }
    
    /// <summary>
    /// Shows the cursor.
    /// </summary>
    public static void ShowCursor() {
        SDL3.SDL_ShowCursor();
    }
    
    /// <summary>
    /// Hides the cursor.
    /// </summary>
    public static void HideCursor() {
        SDL3.SDL_HideCursor();
    }

    /// <summary>
    /// Gets the current mouse cursor state.
    /// </summary>
    /// <returns>The current mouse cursor.</returns>
    public static MouseCursor GetMouseCursor() {
        return _sdlCursor.Item2;
        //SDL3.SDL_GetCursor()
    }
    
    /// <summary>
    /// Sets the mouse cursor to the specified state.
    /// </summary>
    /// <param name="mouseCursor">The desired mouse cursor state.</param>
    public static unsafe void SetMouseCursor(MouseCursor mouseCursor) {
        if (_sdlCursor.Item1 != null) {
            SDL3.SDL_DestroyCursor(_sdlCursor.Item1.Value);
            _sdlCursor.Item1 = null;
        }
        
        _sdlCursor.Item2 = mouseCursor;
        
        if (mouseCursor == MouseCursor.Default) {
            SDL3.SDL_SetCursor(SDL3.SDL_GetDefaultCursor());
        }
        else {
            _sdlCursor.Item1 = SDL3.CreateCursor(SDL3.Cursor);
            
            _sdlCursor.Item1 = Sdl2Native.SDL_CreateSystemCursor((SDL_SystemCursor) mouseCursor);
            Sdl2Native.SDL_SetCursor(_sdlCursor.Item1.Value);
        }
    }
    
    /// <summary>
    /// Checks if relative mouse mode is currently enabled.
    /// </summary>
    /// <returns>True if relative mouse mode is enabled; otherwise, false.</returns>
    public static bool IsRelativeMouseModeEnabled() {
        return Sdl2Helper.GetRelativeMouseMode();
    }
    
    /// <summary>
    /// Enables or disables relative mouse mode.
    /// </summary>
    /// <param name="enabled">True to enable relative mouse mode; false to disable it.</param>
    public static void SetRelativeMouseMode(bool enabled) {
        if (Sdl2Native.SDL_SetRelativeMouseMode(enabled) == -1) {
            Logger.Error("Relative mouse mode is not supported.");
        }
    }

    /// <summary>
    /// Gets the current mouse position.
    /// </summary>
    /// <returns>The current mouse position.</returns>
    public static Vector2 GetMousePosition() {
        return Snapshot.MousePosition;
    }
    
    /// <summary>
    /// Sets the mouse position to the specified coordinates.
    /// </summary>
    /// <param name="position">The desired mouse position.</param>
    public static void SetMousePosition(Vector2 position) {
        Window.SetMousePosition(position);
    }
    
    /// <summary>
    /// Checks if a specific mouse button is pressed.
    /// </summary>
    /// <param name="button">The mouse button to check.</param>
    /// <returns>True if the button is pressed; otherwise, false.</returns>
    public static bool IsMouseButtonPressed(MouseButton button) {
        return _mouseButtonsPressed.Contains(button);
    }

    /// <summary>
    /// Checks if a specific mouse button is currently down.
    /// </summary>
    /// <param name="button">The mouse button to check.</param>
    /// <returns>True if the button is down; otherwise, false.</returns>
    public static bool IsMouseButtonDown(MouseButton button) {
        return _mouseButtonsDown.Contains(button);
    }
    
    /// <summary>
    /// Checks if a specific mouse button has been released.
    /// </summary>
    /// <param name="button">The mouse button to check.</param>
    /// <returns>True if the button has been released; otherwise, false.</returns>
    public static bool IsMouseButtonReleased(MouseButton button) {
        return _mouseButtonsReleased.Contains(button);
    }
    
    /// <summary>
    /// Checks if a specific mouse button is currently up (not pressed).
    /// </summary>
    /// <param name="button">The mouse button to check.</param>
    /// <returns>True if the button is up; otherwise, false.</returns>
    public static bool IsMouseButtonUp(MouseButton button) {
        return !_mouseButtonsDown.Contains(button);
    }
    
    /// <summary>
    /// Checks if the mouse is currently moving and retrieves the position.
    /// </summary>
    /// <param name="pos">The mouse position, if moving.</param>
    /// <returns>True if the mouse is moving; otherwise, false.</returns>
    public static bool IsMouseMoving(out Vector2 pos) {
        if (_miceMoving.Count > 0) {
            pos = _miceMoving[0];
            return true;
        }

        pos = Vector2.Zero;
        return false;
    }
    
    /// <summary>
    /// Checks if the mouse is currently scrolling and retrieves the scroll delta.
    /// </summary>
    /// <param name="wheelDelta">The amount of scrolling, if scrolling.</param>
    /// <returns>True if the mouse is scrolling; otherwise, false.</returns>
    public static bool IsMouseScrolling(out float wheelDelta) {
        if (_miceSrolling.Count > 0) {
            wheelDelta = _miceSrolling[0];
            return true;
        }

        wheelDelta = 0;
        return false;
    }
    
    /* ------------------------------------ Keyboard ------------------------------------ */

    /// <summary>
    /// Checks if a specific key is currently pressed.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns>True if the key is pressed, false otherwise.</returns>
    public static bool IsKeyPressed(KeyboardKey key) {
        return _keyboardKeysPressed.Contains(key);
    }

    /// <summary>
    /// Checks if a specific key is currently down.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns>True if the key is down, false otherwise.</returns>
    public static bool IsKeyDown(KeyboardKey key) {
        return _keyboardKeysDown.Contains(key);
    }

    /// <summary>
    /// Checks if a specific key has been released.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns>True if the key has been released, false otherwise.</returns>
    public static bool IsKeyReleased(KeyboardKey key) {
        return _keyboardKeysReleased.Contains(key);
    }

    /// <summary>
    /// Checks if a specific key is currently up (not pressed).
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns>True if the key is up, false otherwise.</returns>
    public static bool IsKeyUp(KeyboardKey key) {
        return !_keyboardKeysDown.Contains(key);
    }

    /// <summary>
    /// Gets an array of characters that have been pressed.
    /// </summary>
    /// <returns>An array of pressed characters.</returns>
    //public static char[] GetPressedChars() {
    //    return Snapshot.KeyCharPresses.ToArray();
    //}

    /// <summary>
    /// Gets the current text from the clipboard.
    /// </summary>
    /// <returns>The text from the clipboard.</returns>
    public static string GetClipboardText() {
        return SDL3.SDL_GetClipboardText() ?? string.Empty;
    }

    /// <summary>
    /// Sets the specified text to the clipboard.
    /// </summary>
    /// <param name="text">The text to set to the clipboard.</param>
    public static void SetClipboardText(string text) {
        SDL3.SDL_SetClipboardText(text);
    }
    
    /* ------------------------------------ Gamepad ------------------------------------ */

//    /// <summary>
//    /// Checks if a specific gamepad is available.
//    /// </summary>
//    /// <param name="gamepad">The index of the gamepad to check.</param>
//    /// <returns>True if the gamepad is available, false otherwise.</returns>
//    public static bool IsGamepadAvailable(int gamepad) {
//        return gamepad <= _gamepads.Count - 1;
//    }
//    
//    /// <summary>
//    /// Gets the name of a specific gamepad.
//    /// </summary>
//    /// <param name="gamepad">The index of the gamepad.</param>
//    /// <returns>The name of the gamepad.</returns>
//    public static string GetGamepadName(uint gamepad) {
//        return _gamepads.ToArray()[gamepad].Value.Name;
//    }
//    
//    /// <summary>
//    /// Sets the rumble effect on a specific gamepad.
//    /// </summary>
//    /// <param name="gamepad">The index of the gamepad.</param>
//    /// <param name="lowFrequencyRumble">The low frequency rumble strength.</param>
//    /// <param name="highFrequencyRumble">The high frequency rumble strength.</param>
//    /// <param name="durationMs">The duration of the rumble in milliseconds.</param>
//    /// <returns>True if the rumble effect was set successfully, false otherwise.</returns>
//    public static bool SetGamepadRumble(int gamepad, ushort lowFrequencyRumble, ushort highFrequencyRumble, uint durationMs) {
//        return Sdl2Helper.SetControllerRumble(_gamepads.ToArray()[gamepad].Value, lowFrequencyRumble, highFrequencyRumble, durationMs);
//    }
//    
//    /// <summary>
//    /// Gets the axis movement value of a specific gamepad.
//    /// </summary>
//    /// <param name="gamepad">The index of the gamepad.</param>
//    /// <param name="axis">The gamepad axis to get the movement value of.</param>
//    /// <returns>The movement value of the specified axis.</returns>
//    public static float GetGamepadAxisMovement(int gamepad, GamepadAxis axis) {
//        return _gamepads.ToArray()[gamepad].Value.GetAxisMovement(axis);
//    }
//    
//    /// <summary>
//    /// Checks if a specific button on a gamepad is pressed.
//    /// </summary>
//    /// <param name="gamepad">The index of the gamepad.</param>
//    /// <param name="button">The button to check.</param>
//    /// <returns>True if the button is pressed, false otherwise.</returns>
//    public static bool IsGamepadButtonPressed(int gamepad, GamepadButton button) {
//        return _gamepads.ToArray()[gamepad].Value.IsButtonPressed(button);
//    }
//    
//    /// <summary>
//    /// Checks if a specific button on a gamepad is currently down.
//    /// </summary>
//    /// <param name="gamepad">The index of the gamepad.</param>
//    /// <param name="button">The button to check.</param>
//    /// <returns>True if the button is down, false otherwise.</returns>
//    public static bool IsGamepadButtonDown(int gamepad, GamepadButton button) {
//        return _gamepads.ToArray()[gamepad].Value.IsButtonDown(button);
//    }
//    
//    /// <summary>
//    /// Checks if a specific button on a gamepad was released.
//    /// </summary>
//    /// <param name="gamepad">The index of the gamepad.</param>
//    /// <param name="button">The button to check.</param>
//    /// <returns>True if the button was released, false otherwise.</returns>
//    public static bool IsGamepadButtonReleased(int gamepad, GamepadButton button) {
//        return _gamepads.ToArray()[gamepad].Value.IsButtonReleased(button);
//    }
//    
//    /// <summary>
//    /// Checks if a specific button on a gamepad is currently up.
//    /// </summary>
//    /// <param name="gamepad">The index of the gamepad.</param>
//    /// <param name="button">The button to check.</param>
//    /// <returns>True if the button is up, false otherwise.</returns>
//    public static bool IsGamepadButtonUp(int gamepad, GamepadButton button) {
//        return _gamepads.ToArray()[gamepad].Value.IsButtonUp(button);
//    }
//    
//    /* ------------------------------------ Other ------------------------------------ */
//
//    /// <summary>
//    /// Checks if a file was dragged and dropped onto the window, and retrieves the path of the first dropped file, if any.
//    /// </summary>
//    /// <param name="path">The path of the first dropped file, if any.</param>
//    /// <returns>True if a file was dragged and dropped onto the window, false otherwise.</returns>
//    public static bool IsFileDragDropped(out string path) {
//        if (_filesDragDropped.Count > 0) {
//            path = _filesDragDropped[0];
//            return true;
//        }
//        
//        path = string.Empty;
//        return false;
//    }
//    
//    /* ------------------------------------ Mouse ------------------------------------ */
//
//    private static void OnMouseWheel(MouseWheelEventArgs args) {
//        _miceSrolling.Add(args.WheelDelta);
//    }
//
//    private static void OnMouseMove(MouseMoveEventArgs args) {
//        _miceMoving.Add(args.MousePosition);
//    }
//    
//    private static void OnMouseDown(MouseEvent args) {
//        _mouseButtonsPressed.Add(args.MouseButton);
//        _mouseButtonsDown.Add(args.MouseButton);
//    }
//    
//    private static void OnMouseUp(MouseEvent args) {
//        _mouseButtonsDown.Remove(args.MouseButton);
//        _mouseButtonsReleased.Add(args.MouseButton);
//    }
    
    /* ------------------------------------ Keyboard ------------------------------------ */

    private static void OnKeyDown(KeyEvent keyEvent) {
        if (!keyEvent.Repeat) {
            _keyboardKeysPressed.Add(keyEvent.KeyboardKey);
            _keyboardKeysDown.Add(keyEvent.KeyboardKey);
        }
    }
    
    private static void OnKeyUp(KeyEvent keyEvent) {
        _keyboardKeysDown.Remove(keyEvent.KeyboardKey);
        _keyboardKeysReleased.Add(keyEvent.KeyboardKey);
    }
    
    /* ------------------------------------ Other ------------------------------------ */
    
//    private static void OnDragDrop(DragDropEvent args) {
//        _filesDragDropped.Add(args.File);
//    }

    /// <summary>
    /// Processes SDL events related to gamepad devices being added or removed, and updates the list of active gamepads accordingly.
    /// </summary>
    /// <param name="ev">The SDL event to process.</param>
//    private static void ProcessEvent(ref SDL_Event ev) {
//        switch (ev.type) {
//            case SDL_EventType.ControllerDeviceAdded:
//            case SDL_EventType.ControllerDeviceRemoved:
//                SDL_ControllerDeviceEvent deviceEvent = Unsafe.As<SDL_Event, SDL_ControllerDeviceEvent>(ref ev);
//                
//                if (deviceEvent.type == 1619) {
//                    Gamepad gamepad = new Gamepad(deviceEvent.which);
//                    _gamepads.Add(gamepad.ControllerIndex, gamepad);
//                }
//                else {
//                    Gamepad gamepad = _gamepads[deviceEvent.which];
//                    gamepad.Dispose();
//                    _gamepads.Remove(deviceEvent.which);
//                }
//                
//                break;
//        }
//
//        foreach (var gamepad in _gamepads.Values) {
//            gamepad.ProcessEvent(ref ev);
//        }
//    }

    /// <summary>
    /// Destroys resources (like free a mouse cursor).
    /// </summary>
    public static void Destroy() {
        
        // Mouse
        //SetMouseCursor(MouseCursor.Default); // To free the Cursor.
        
        //Window.MouseWheel -= OnMouseWheel;
        //Window.MouseMove -= OnMouseMove;
        //Window.MouseDown -= OnMouseDown;
        //Window.MouseUp -= OnMouseUp;
        
        // Keyboard
        Window.KeyDown -= OnKeyDown;
        Window.KeyUp -= OnKeyUp;
        
        // Gamepad
        //foreach (Gamepad gamepad in _gamepads.Values) {
        //    gamepad.Dispose();
        //}
        
        //_gamepads.Clear();
        
        // Other
        //Window.DragDrop -= OnDragDrop;
        
        // Event
        //Sdl2Events.Unsubscribe(ProcessEvent);
    }
}