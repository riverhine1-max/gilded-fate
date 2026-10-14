using System;
using UnityEngine;
namespace UnityEngine.InputSystem
{
    public enum Key { None, Space, Enter, Tab, Escape, E, P, Pause, PageDown, PageUp, RightArrow, LeftArrow, UpArrow, DownArrow, NumpadEnter, Q, S, D, C, G, H, I, M, A, B, W, Backspace, F9, F10, F11, F12 }
    public enum GamepadButton { South, East, West, North, A, B, X, Y, DpadUp, DpadDown, DpadLeft, DpadRight, LeftShoulder, RightShoulder, Start, Select, LeftStick, RightStick }
    public class InputDevice { public string name, layout, displayName, description; public bool enabled; }
    namespace Controls
    {
        public class ButtonControl { public bool isPressed, wasPressedThisFrame, wasReleasedThisFrame; public float ReadValue() => 0f; public InputDevice device; public string name; }
        public class Vector2Control { public float x, y; public Vector2 ReadValue() => default; public ButtonControl up, down, left, right; }
        public class StickControl : Vector2Control { }
        public class KeyControl : ButtonControl { }
        public class DpadControl : Vector2Control { }
        public class AxisControl { public float ReadValue() => 0f; }
        public class TouchControl { public ButtonControl press; public Vector2Control position; }
    }
    public class Pointer : InputDevice { public Controls.Vector2Control position, delta; public Controls.ButtonControl press; }
    public class Mouse : Pointer
    {
        public static Mouse current;
        public Controls.ButtonControl leftButton, rightButton, middleButton; public Controls.Vector2Control scroll;
    }
    public class Touchscreen : Pointer { public static new Touchscreen current; public Controls.TouchControl primaryTouch; }
    public class Keyboard : InputDevice
    {
        public static Keyboard current;
        public Controls.KeyControl anyKey, backspaceKey, cKey, dKey, downArrowKey, eKey, enterKey, escapeKey, f10Key, f11Key, f12Key, f9Key, gKey, hKey, iKey, leftArrowKey, leftCtrlKey, leftShiftKey, mKey, pKey, pageDownKey, pageUpKey, qKey, rightArrowKey, rightCtrlKey, rightShiftKey, sKey, spaceKey, tabKey, upArrowKey;
        public Controls.KeyControl isKey;
        public Controls.KeyControl this[Key k] => null;
    }
    public class Gamepad : InputDevice
    {
        public static Gamepad current;
        public Controls.ButtonControl buttonSouth, buttonEast, buttonWest, buttonNorth, startButton, selectButton, leftShoulder, rightShoulder, leftStickButton, rightStickButton, leftTrigger, rightTrigger;
        public Controls.StickControl leftStick, rightStick; public Controls.DpadControl dpad;
        public Controls.ButtonControl this[GamepadButton b] => null;
    }
    public static class InputSystem
    {
        public static T AddDevice<T>() where T : InputDevice, new() => new T();
        public static void RemoveDevice(InputDevice d) { }
        public static void QueueStateEvent<T>(InputDevice d, T state) where T : struct { }
        public static void Update() { }
    }
}
namespace UnityEngine.InputSystem.LowLevel
{
    public struct KeyboardState { public KeyboardState(params Key[] keys) { } }
    public struct GamepadState { public Vector2 leftStick, rightStick; public uint buttons; public float leftTrigger, rightTrigger; public GamepadState(params GamepadButton[] b) { leftStick = default; rightStick = default; buttons = 0; leftTrigger = 0; rightTrigger = 0; } }
}
