using Gma.System.MouseKeyHook;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ToyControlApp.Models;
using KeyBindingModel = ToyControlApp.Models.KeyBinding;

namespace ToyControlApp.Services
{
    public class KeyboardHookService : IDisposable
    {
        private IKeyboardMouseEvents _globalHook;
        private Dictionary<Keys, KeyBindingModel> _keyBindings;
        private HashSet<Keys> _pressedKeys;

        public event EventHandler<KeyBindingModel> KeyBindingTriggered;
        public event EventHandler<KeyBindingModel> KeyBindingReleased; // NEW: For hold mode
        public event EventHandler<Keys> SpecialKeyPressed;

        public bool IsActive { get; private set; }

        public KeyboardHookService()
        {
            _keyBindings = new Dictionary<Keys, KeyBindingModel>();
            _pressedKeys = new HashSet<Keys>();
        }

        public void StartHook()
        {
            if (IsActive) return;

            try
            {
                _globalHook = Hook.GlobalEvents();
                _globalHook.KeyDown += OnKeyDown;
                _globalHook.KeyUp += OnKeyUp;
                IsActive = true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to start keyboard hook: {ex.Message}", ex);
            }
        }

        public void StopHook()
        {
            if (!IsActive) return;

            if (_globalHook != null)
            {
                _globalHook.KeyDown -= OnKeyDown;
                _globalHook.KeyUp -= OnKeyUp;
            }
            _globalHook?.Dispose();
            _globalHook = null;
            _pressedKeys.Clear();
            // Clear registered bindings so a profile switch doesn't leave stale
            // entries that would re-fire when StartHook is called for a new profile.
            _keyBindings.Clear();
            IsActive = false;
        }

        public void AddKeyBinding(KeyBindingModel binding)
        {
            _keyBindings[binding.Key] = binding;
        }

        public void RemoveKeyBinding(Keys key)
        {
            _keyBindings.Remove(key);
        }

        public void ClearKeyBindings()
        {
            _keyBindings.Clear();
        }

        public IEnumerable<KeyBindingModel> GetKeyBindings()
        {
            return _keyBindings.Values;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            // Prevent multiple triggers while key is held down
            if (_pressedKeys.Contains(e.KeyCode)) return;

            _pressedKeys.Add(e.KeyCode);

            // Handle special keys (F8 for hook toggle)
            if (e.KeyCode == Keys.F8)
            {
                SpecialKeyPressed?.Invoke(this, e.KeyCode);
                e.Handled = true; // Prevent F8 from being processed by other applications
                return;
            }

            // Handle regular key bindings
            if (_keyBindings.TryGetValue(e.KeyCode, out var binding))
            {
                KeyBindingTriggered?.Invoke(this, binding);
            }
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            // Only process key up if the key was previously pressed (tracked)
            if (_pressedKeys.Contains(e.KeyCode))
            {
                _pressedKeys.Remove(e.KeyCode);

                // Fire key released event for hold mode bindings
                if (_keyBindings.TryGetValue(e.KeyCode, out var binding))
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
                        KeyBindingReleased?.Invoke(this, binding);
                    }
                }
            }
        }

        public void Dispose()
        {
            StopHook();
        }
    }
}