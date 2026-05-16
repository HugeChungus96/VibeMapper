using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SharpDX.XInput;
using ToyControlApp.Models;

namespace ToyControlApp.Services
{
    public class ControllerInputService : IDisposable
    {
        private readonly Controller[] _controllers;
        private readonly Dictionary<ControllerInput, ControllerBinding> _controllerBindings;
        private readonly HashSet<ControllerInput> _pressedInputs;
        private readonly Timer _pollTimer;
        private readonly object _lockObject = new object();

        private SharpDX.XInput.State[] _previousStates;
        private bool _isActive;

        public event EventHandler<ControllerBinding> ControllerBindingTriggered;
        public event EventHandler<ControllerBinding> ControllerBindingReleased; // NEW: For hold mode

        public bool IsActive => _isActive;

        public ControllerInputService()
        {
            _controllers = new Controller[]
            {
                new Controller(UserIndex.One),
                new Controller(UserIndex.Two),
                new Controller(UserIndex.Three),
                new Controller(UserIndex.Four)
            };

            _controllerBindings = new Dictionary<ControllerInput, ControllerBinding>();
            _pressedInputs = new HashSet<ControllerInput>();
            _previousStates = new SharpDX.XInput.State[4];

            // Poll controllers at 60Hz
            _pollTimer = new Timer(PollControllers, null, Timeout.Infinite, Timeout.Infinite);
        }

        public void StartHook()
        {
            if (_isActive) return;

            lock (_lockObject)
            {
                // Initialize previous states
                for (int i = 0; i < _controllers.Length; i++)
                {
                    if (_controllers[i].IsConnected)
                    {
                        _previousStates[i] = _controllers[i].GetState();
                    }
                }

                _isActive = true;
                _pollTimer.Change(0, 16); // ~60Hz polling
            }
        }

        public void StopHook()
        {
            if (!_isActive) return;

            lock (_lockObject)
            {
                _isActive = false;
                _pollTimer.Change(Timeout.Infinite, Timeout.Infinite);
                _pressedInputs.Clear();
                // Clear registered bindings so switching profiles doesn't leave stale
                // controller bindings active after the hook is restarted.
                _controllerBindings.Clear();
            }
        }

        public void AddControllerBinding(ControllerBinding binding)
        {
            lock (_lockObject)
            {
                _controllerBindings[binding.Input] = binding;
            }
        }

        public void RemoveControllerBinding(ControllerInput input)
        {
            lock (_lockObject)
            {
                _controllerBindings.Remove(input);
            }
        }

        public void ClearControllerBindings()
        {
            lock (_lockObject)
            {
                _controllerBindings.Clear();
            }
        }

        public IEnumerable<ControllerBinding> GetControllerBindings()
        {
            lock (_lockObject)
            {
                return new List<ControllerBinding>(_controllerBindings.Values);
            }
        }

        public List<Controller> GetConnectedControllers()
        {
            var connected = new List<Controller>();
            for (int i = 0; i < _controllers.Length; i++)
            {
                if (_controllers[i].IsConnected)
                {
                    connected.Add(_controllers[i]);
                }
            }
            return connected;
        }

        private void PollControllers(object state)
        {
            if (!_isActive) return;

            lock (_lockObject)
            {
                for (int controllerIndex = 0; controllerIndex < _controllers.Length; controllerIndex++)
                {
                    var controller = _controllers[controllerIndex];
                    if (!controller.IsConnected) continue;

                    var currentState = controller.GetState();
                    var previousState = _previousStates[controllerIndex];

                    // Check for button presses
                    CheckButtonInputs(controllerIndex, currentState.Gamepad, previousState.Gamepad);

                    // Check for trigger inputs
                    CheckTriggerInputs(controllerIndex, currentState.Gamepad, previousState.Gamepad);

                    // Check for thumbstick inputs
                    CheckThumbstickInputs(controllerIndex, currentState.Gamepad, previousState.Gamepad);

                    _previousStates[controllerIndex] = currentState;
                }
            }
        }

        private void CheckButtonInputs(int controllerIndex, Gamepad current, Gamepad previous)
        {
            var buttonMappings = new Dictionary<GamepadButtonFlags, ControllerInput>
            {
                { GamepadButtonFlags.A, new ControllerInput(controllerIndex, ControllerInputType.ButtonA) },
                { GamepadButtonFlags.B, new ControllerInput(controllerIndex, ControllerInputType.ButtonB) },
                { GamepadButtonFlags.X, new ControllerInput(controllerIndex, ControllerInputType.ButtonX) },
                { GamepadButtonFlags.Y, new ControllerInput(controllerIndex, ControllerInputType.ButtonY) },
                { GamepadButtonFlags.LeftShoulder, new ControllerInput(controllerIndex, ControllerInputType.LeftBumper) },
                { GamepadButtonFlags.RightShoulder, new ControllerInput(controllerIndex, ControllerInputType.RightBumper) },
                { GamepadButtonFlags.Back, new ControllerInput(controllerIndex, ControllerInputType.Back) },
                { GamepadButtonFlags.Start, new ControllerInput(controllerIndex, ControllerInputType.Start) },
                { GamepadButtonFlags.LeftThumb, new ControllerInput(controllerIndex, ControllerInputType.LeftStickClick) },
                { GamepadButtonFlags.RightThumb, new ControllerInput(controllerIndex, ControllerInputType.RightStickClick) },
                { GamepadButtonFlags.DPadUp, new ControllerInput(controllerIndex, ControllerInputType.DPadUp) },
                { GamepadButtonFlags.DPadDown, new ControllerInput(controllerIndex, ControllerInputType.DPadDown) },
                { GamepadButtonFlags.DPadLeft, new ControllerInput(controllerIndex, ControllerInputType.DPadLeft) },
                { GamepadButtonFlags.DPadRight, new ControllerInput(controllerIndex, ControllerInputType.DPadRight) }
            };

            foreach (var mapping in buttonMappings)
            {
                bool wasPressed = (previous.Buttons & mapping.Key) != 0;
                bool isPressed = (current.Buttons & mapping.Key) != 0;

                if (isPressed && !wasPressed) // Button just pressed
                {
                    HandleInputPressed(mapping.Value);
                }
                else if (!isPressed && wasPressed) // Button just released
                {
                    HandleInputReleased(mapping.Value);
                }
            }
        }

        private void CheckTriggerInputs(int controllerIndex, Gamepad current, Gamepad previous)
        {
            const byte triggerThreshold = 128; // Trigger threshold (0-255)

            // Left Trigger
            var leftTriggerInput = new ControllerInput(controllerIndex, ControllerInputType.LeftTrigger);
            bool wasLeftPressed = previous.LeftTrigger > triggerThreshold;
            bool isLeftPressed = current.LeftTrigger > triggerThreshold;

            if (isLeftPressed && !wasLeftPressed)
                HandleInputPressed(leftTriggerInput);
            else if (!isLeftPressed && wasLeftPressed)
                HandleInputReleased(leftTriggerInput);

            // Right Trigger
            var rightTriggerInput = new ControllerInput(controllerIndex, ControllerInputType.RightTrigger);
            bool wasRightPressed = previous.RightTrigger > triggerThreshold;
            bool isRightPressed = current.RightTrigger > triggerThreshold;

            if (isRightPressed && !wasRightPressed)
                HandleInputPressed(rightTriggerInput);
            else if (!isRightPressed && wasRightPressed)
                HandleInputReleased(rightTriggerInput);
        }

        private void CheckThumbstickInputs(int controllerIndex, Gamepad current, Gamepad previous)
        {
            const short stickThreshold = 16384; // Thumbstick threshold (about 50% deflection)

            // Left Stick Directions
            CheckStickDirection(controllerIndex, ControllerInputType.LeftStickUp,
                current.LeftThumbY > stickThreshold, previous.LeftThumbY > stickThreshold);
            CheckStickDirection(controllerIndex, ControllerInputType.LeftStickDown,
                current.LeftThumbY < -stickThreshold, previous.LeftThumbY < -stickThreshold);
            CheckStickDirection(controllerIndex, ControllerInputType.LeftStickLeft,
                current.LeftThumbX < -stickThreshold, previous.LeftThumbX < -stickThreshold);
            CheckStickDirection(controllerIndex, ControllerInputType.LeftStickRight,
                current.LeftThumbX > stickThreshold, previous.LeftThumbX > stickThreshold);

            // Right Stick Directions
            CheckStickDirection(controllerIndex, ControllerInputType.RightStickUp,
                current.RightThumbY > stickThreshold, previous.RightThumbY > stickThreshold);
            CheckStickDirection(controllerIndex, ControllerInputType.RightStickDown,
                current.RightThumbY < -stickThreshold, previous.RightThumbY < -stickThreshold);
            CheckStickDirection(controllerIndex, ControllerInputType.RightStickLeft,
                current.RightThumbX < -stickThreshold, previous.RightThumbX < -stickThreshold);
            CheckStickDirection(controllerIndex, ControllerInputType.RightStickRight,
                current.RightThumbX > stickThreshold, previous.RightThumbX > stickThreshold);
        }

        private void CheckStickDirection(int controllerIndex, ControllerInputType inputType, bool isPressed, bool wasPressed)
        {
            var input = new ControllerInput(controllerIndex, inputType);

            if (isPressed && !wasPressed)
                HandleInputPressed(input);
            else if (!isPressed && wasPressed)
                HandleInputReleased(input);
        }

        private void HandleInputPressed(ControllerInput input)
        {
            if (_pressedInputs.Contains(input)) return;

            _pressedInputs.Add(input);

            if (_controllerBindings.TryGetValue(input, out var binding))
            {
                ControllerBindingTriggered?.Invoke(this, binding);
            }
        }

        private void HandleInputReleased(ControllerInput input)
        {
            if (_pressedInputs.Contains(input))
            {
                _pressedInputs.Remove(input);

                if (_controllerBindings.TryGetValue(input, out var binding))
                {
                    // Check if any device bindings are in hold mode
                    bool hasHoldModeBindings = false;
                    foreach (var deviceBinding in binding.DeviceBindings)
                    {
                        if (deviceBinding.IsHoldMode)
                        {
                            hasHoldModeBindings = true;
                            break;
                        }
                    }

                    if (hasHoldModeBindings)
                    {
                        ControllerBindingReleased?.Invoke(this, binding);
                    }
                }
            }
        }

        public void Dispose()
        {
            StopHook();
            _pollTimer?.Dispose();

            // Note: Controller objects in SharpDX don't implement IDisposable
            // They are lightweight wrappers and don't need explicit disposal
        }
    }
}