using Gma.System.MouseKeyHook;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ToyControlApp.Models;

namespace ToyControlApp.Services
{
    public class MouseHookService : IDisposable
    {
        private IKeyboardMouseEvents _globalHook;
        private Dictionary<MouseInput, MouseInputBinding> _mouseBindings;
        private HashSet<MouseInput> _pressedInputs;

        // UPDATED: Changed event type to MouseInputBinding
        public event EventHandler<MouseInputBinding> MouseBindingTriggered;
        public event EventHandler<MouseInputBinding> MouseBindingReleased;

        public bool IsActive { get; private set; }

        public MouseHookService()
        {
            _mouseBindings = new Dictionary<MouseInput, MouseInputBinding>();
            _pressedInputs = new HashSet<MouseInput>();
        }

        public void StartHook()
        {
            if (IsActive) return;

            try
            {
                _globalHook = Hook.GlobalEvents();
                _globalHook.MouseDown += OnMouseDown;
                _globalHook.MouseUp += OnMouseUp;
                _globalHook.MouseWheel += OnMouseWheel;
                IsActive = true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to start mouse hook: {ex.Message}", ex);
            }
        }

        public void StopHook()
        {
            if (!IsActive) return;

            if (_globalHook != null)
            {
                _globalHook.MouseDown -= OnMouseDown;
                _globalHook.MouseUp -= OnMouseUp;
                _globalHook.MouseWheel -= OnMouseWheel;
            }
            _globalHook?.Dispose();
            _globalHook = null;
            _pressedInputs.Clear();
            // Clear registered bindings so switching profiles doesn't leave stale
            // mouse bindings active after the hook is restarted.
            _mouseBindings.Clear();
            IsActive = false;
        }

        public void AddMouseBinding(MouseInputBinding binding)
        {
            _mouseBindings[binding.Input] = binding;
        }

        public void RemoveMouseBinding(MouseInput input)
        {
            _mouseBindings.Remove(input);
        }

        public void ClearMouseBindings()
        {
            _mouseBindings.Clear();
        }

        public IEnumerable<MouseInputBinding> GetMouseBindings()
        {
            return _mouseBindings.Values;
        }

        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            var mouseInput = GetMouseInputFromButton(e.Button);
            if (mouseInput.HasValue)
            {
                HandleInputPressed(mouseInput.Value);
            }
        }

        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            var mouseInput = GetMouseInputFromButton(e.Button);
            if (mouseInput.HasValue)
            {
                HandleInputReleased(mouseInput.Value);
            }
        }

        private void OnMouseWheel(object sender, MouseEventArgs e)
        {
            // Handle mouse wheel
            var mouseInput = e.Delta > 0 ?
                new MouseInput(MouseInputType.WheelUp) :
                new MouseInput(MouseInputType.WheelDown);

            // For wheel events, we treat them as quick press/release since they're discrete events
            HandleInputPressed(mouseInput);

            // For wheel events, immediately trigger release for hold mode bindings
            // since wheel events are instantaneous
            HandleInputReleased(mouseInput);
        }

        private MouseInput? GetMouseInputFromButton(MouseButtons button)
        {
            return button switch
            {
                MouseButtons.Left => new MouseInput(MouseInputType.LeftClick),
                MouseButtons.Right => new MouseInput(MouseInputType.RightClick),
                MouseButtons.Middle => new MouseInput(MouseInputType.MiddleClick),
                MouseButtons.XButton1 => new MouseInput(MouseInputType.XButton1),
                MouseButtons.XButton2 => new MouseInput(MouseInputType.XButton2),
                _ => null
            };
        }

        private void HandleInputPressed(MouseInput input)
        {
            // For wheel events, don't track as "pressed" since they're instantaneous
            bool isWheelEvent = input.InputType == MouseInputType.WheelUp ||
                               input.InputType == MouseInputType.WheelDown;

            if (!isWheelEvent && _pressedInputs.Contains(input))
                return;

            if (!isWheelEvent)
                _pressedInputs.Add(input);

            if (_mouseBindings.TryGetValue(input, out var binding))
            {
                MouseBindingTriggered?.Invoke(this, binding);
            }
        }

        private void HandleInputReleased(MouseInput input)
        {
            // For wheel events, always process release for hold mode
            bool isWheelEvent = input.InputType == MouseInputType.WheelUp ||
                               input.InputType == MouseInputType.WheelDown;

            if (!isWheelEvent && !_pressedInputs.Contains(input))
                return;

            if (!isWheelEvent)
                _pressedInputs.Remove(input);

            if (_mouseBindings.TryGetValue(input, out var binding))
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
                    MouseBindingReleased?.Invoke(this, binding);
                }
            }
        }

        public void Dispose()
        {
            StopHook();
        }
    }
}