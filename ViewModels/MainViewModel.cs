using Buttplug.Client;
using SharpDX.XInput;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using ToyControlApp.Models;
using ToyControlApp.Services;
using KeyBindingModel = ToyControlApp.Models.KeyBinding;

namespace ToyControlApp.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ButtplugService _buttplugService;
        private readonly KeyboardHookService _keyboardHookService;
        private readonly ControllerInputService _controllerInputService;
        private readonly MouseHookService _mouseHookService;
        private readonly ProfileService _profileService;

        private string _connectionStatus = "Disconnected";
        private bool _keyboardHookActive = false;
        private bool _controllerHookActive = false;
        private bool _mouseHookActive = false;
        private KeyBindingModel _selectedKeyBinding;
        private ControllerBinding _selectedControllerBinding;
        private MouseInputBinding _selectedMouseBinding;
        private Profile _currentProfile;

        public ObservableCollection<ButtplugClientDevice> Devices { get; }
        public ObservableCollection<KeyBindingModel> KeyBindings { get; }
        public ObservableCollection<ControllerBinding> ControllerBindings { get; }
        public ObservableCollection<MouseInputBinding> MouseBindings { get; }

        public string ConnectionStatus
        {
            get => _connectionStatus;
            set
            {
                _connectionStatus = value;
                OnPropertyChanged();
            }
        }

        public bool KeyboardHookActive
        {
            get => _keyboardHookActive;
            set
            {
                _keyboardHookActive = value;
                OnPropertyChanged();
                // Refresh command states when hook status changes
                ((RelayCommand)StartKeyboardHookCommand).RaiseCanExecuteChanged();
                ((RelayCommand)StopKeyboardHookCommand).RaiseCanExecuteChanged();
            }
        }

        public bool ControllerHookActive
        {
            get => _controllerHookActive;
            set
            {
                _controllerHookActive = value;
                OnPropertyChanged();
                // Refresh command states when hook status changes
                ((RelayCommand)StartControllerHookCommand).RaiseCanExecuteChanged();
                ((RelayCommand)StopControllerHookCommand).RaiseCanExecuteChanged();
            }
        }

        public bool MouseHookActive
        {
            get => _mouseHookActive;
            set
            {
                _mouseHookActive = value;
                OnPropertyChanged();
                // Refresh command states when hook status changes
                ((RelayCommand)StartMouseHookCommand).RaiseCanExecuteChanged();
                ((RelayCommand)StopMouseHookCommand).RaiseCanExecuteChanged();
            }
        }

        public KeyBindingModel SelectedKeyBinding
        {
            get => _selectedKeyBinding;
            set
            {
                _selectedKeyBinding = value;
                OnPropertyChanged();
            }
        }

        public ControllerBinding SelectedControllerBinding
        {
            get => _selectedControllerBinding;
            set
            {
                _selectedControllerBinding = value;
                OnPropertyChanged();
            }
        }

        public MouseInputBinding SelectedMouseBinding
        {
            get => _selectedMouseBinding;
            set
            {
                _selectedMouseBinding = value;
                OnPropertyChanged();
            }
        }

        public string CurrentProfileName => _currentProfile?.Name ?? "No Profile";

        // Commands
        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand StopAllDevicesCommand { get; }
        public ICommand StartKeyboardHookCommand { get; }
        public ICommand StopKeyboardHookCommand { get; }
        public ICommand StartControllerHookCommand { get; }
        public ICommand StopControllerHookCommand { get; }
        public ICommand StartMouseHookCommand { get; }
        public ICommand StopMouseHookCommand { get; }
        public ICommand AddKeyBindingCommand { get; }
        public ICommand RemoveKeyBindingCommand { get; }
        public ICommand AddControllerBindingCommand { get; }
        public ICommand RemoveControllerBindingCommand { get; }
        public ICommand AddMouseBindingCommand { get; }
        public ICommand RemoveMouseBindingCommand { get; }
        public ICommand ToggleBothHooksCommand { get; }
        public ICommand ManageProfilesCommand { get; }
        public ICommand SaveProfileCommand { get; }

        public MainViewModel()
        {
            _buttplugService = new ButtplugService();
            _keyboardHookService = new KeyboardHookService();
            _controllerInputService = new ControllerInputService();
            _mouseHookService = new MouseHookService();
            _profileService = new ProfileService();

            Devices = new ObservableCollection<ButtplugClientDevice>();
            KeyBindings = new ObservableCollection<KeyBindingModel>();
            ControllerBindings = new ObservableCollection<ControllerBinding>();
            MouseBindings = new ObservableCollection<MouseInputBinding>();

            // Set up event handlers
            _buttplugService.ConnectionStatusChanged += OnConnectionStatusChanged;
            _buttplugService.DevicesChanged += OnDevicesChanged;
            _keyboardHookService.KeyBindingTriggered += OnKeyBindingTriggered;
            _keyboardHookService.KeyBindingReleased += OnKeyBindingReleased; // NEW: Handle key release for hold mode
            _keyboardHookService.SpecialKeyPressed += OnSpecialKeyPressed; // Add F8 handler
            _controllerInputService.ControllerBindingTriggered += OnControllerBindingTriggered;
            _controllerInputService.ControllerBindingReleased += OnControllerBindingReleased; // NEW: Handle controller release for hold mode
            _mouseHookService.MouseBindingTriggered += OnMouseBindingTriggered;
            _mouseHookService.MouseBindingReleased += OnMouseBindingReleased;

            // Initialize commands
            ConnectCommand = new RelayCommand(async () => await ConnectAsync(), () => !_buttplugService.IsConnected);
            DisconnectCommand = new RelayCommand(async () => await DisconnectAsync(), () => _buttplugService.IsConnected);
            StopAllDevicesCommand = new RelayCommand(async () => await StopAllDevicesAsync(), () => _buttplugService.IsConnected);

            StartKeyboardHookCommand = new RelayCommand(StartKeyboardHook, () => !_keyboardHookService.IsActive);
            StopKeyboardHookCommand = new RelayCommand(StopKeyboardHook, () => _keyboardHookService.IsActive);

            StartControllerHookCommand = new RelayCommand(StartControllerHook, () => !_controllerInputService.IsActive);
            StopControllerHookCommand = new RelayCommand(StopControllerHook, () => _controllerInputService.IsActive);

            StartMouseHookCommand = new RelayCommand(StartMouseHook, () => !_mouseHookService.IsActive);
            StopMouseHookCommand = new RelayCommand(StopMouseHook, () => _mouseHookService.IsActive);

            AddKeyBindingCommand = new RelayCommand(AddKeyBinding, () => _buttplugService.IsConnected);
            RemoveKeyBindingCommand = new RelayCommand<KeyBindingModel>(RemoveKeyBinding, binding => binding != null);

            AddControllerBindingCommand = new RelayCommand(AddControllerBinding, () => _buttplugService.IsConnected);
            RemoveControllerBindingCommand = new RelayCommand<ControllerBinding>(RemoveControllerBinding, binding => binding != null);

            AddMouseBindingCommand = new RelayCommand(AddMouseBinding, () => _buttplugService.IsConnected);
            RemoveMouseBindingCommand = new RelayCommand<MouseInputBinding>(RemoveMouseBinding, binding => binding != null);

            ToggleBothHooksCommand = new RelayCommand(ToggleBothHooks);
            ManageProfilesCommand = new RelayCommand(ManageProfiles);
            SaveProfileCommand = new RelayCommand(SaveCurrentProfile);

            ConnectionStatus = "Disconnected";

            // Load the last used profile
            LoadLastUsedProfile();
        }

        private async Task ConnectAsync()
        {
            try
            {
                await _buttplugService.ConnectAsync();
                // Commands will be updated automatically through property change events
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to connect: {ex.Message}", "Connection Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private async Task DisconnectAsync()
        {
            try
            {
                await _buttplugService.DisconnectAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during disconnect: {ex.Message}");
            }
        }

        private async Task StopAllDevicesAsync()
        {
            try
            {
                await _buttplugService.StopAllDevicesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping devices: {ex.Message}");
            }
        }

        private void StartKeyboardHook()
        {
            try
            {
                _keyboardHookService.StartHook();
                KeyboardHookActive = true;

                // Add existing bindings to the hook service
                foreach (var binding in KeyBindings)
                {
                    _keyboardHookService.AddKeyBinding(binding);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to start keyboard hook: {ex.Message}", "Hook Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void StopKeyboardHook()
        {
            _keyboardHookService.StopHook();
            KeyboardHookActive = false;
        }

        private void StartControllerHook()
        {
            try
            {
                _controllerInputService.StartHook();
                ControllerHookActive = true;

                // Add existing bindings to the hook service
                foreach (var binding in ControllerBindings)
                {
                    _controllerInputService.AddControllerBinding(binding);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to start controller hook: {ex.Message}", "Hook Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void StopControllerHook()
        {
            _controllerInputService.StopHook();
            ControllerHookActive = false;
        }

        private void StartMouseHook()
        {
            try
            {
                _mouseHookService.StartHook();
                MouseHookActive = true;

                // Add existing bindings to the hook service
                foreach (var binding in MouseBindings)
                {
                    _mouseHookService.AddMouseBinding(binding);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to start mouse hook: {ex.Message}", "Hook Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void StopMouseHook()
        {
            _mouseHookService.StopHook();
            MouseHookActive = false;
        }

        private void ToggleBothHooks()
        {
            // If any hook is active, stop all. If all are inactive, start all.
            if (_keyboardHookService.IsActive || _controllerInputService.IsActive || _mouseHookService.IsActive)
            {
                StopKeyboardHook();
                StopControllerHook();
                StopMouseHook();
            }
            else
            {
                StartKeyboardHook();
                StartControllerHook();
                StartMouseHook();
            }
        }

        private void AddKeyBinding()
        {
            var newBinding = new KeyBindingModel();
            var dialog = new KeyBindingDialog(newBinding, Devices.ToList());

            if (dialog.ShowDialog() == true)
            {
                // Check for duplicate key bindings
                if (KeyBindings.Any(kb => kb.Key == newBinding.Key))
                {
                    System.Windows.MessageBox.Show($"A binding for key '{newBinding.Key}' already exists.", "Duplicate Key Binding",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                KeyBindings.Add(newBinding);
                if (_keyboardHookService.IsActive)
                {
                    _keyboardHookService.AddKeyBinding(newBinding);
                }
            }
        }

        private void RemoveKeyBinding(KeyBindingModel binding)
        {
            if (binding != null)
            {
                KeyBindings.Remove(binding);
                if (_keyboardHookService.IsActive)
                {
                    _keyboardHookService.RemoveKeyBinding(binding.Key);
                }
            }
        }

        private void AddControllerBinding()
        {
            var connectedControllers = _controllerInputService.GetConnectedControllers();
            if (connectedControllers.Count == 0)
            {
                System.Windows.MessageBox.Show("No controllers are connected.", "No Controllers",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            var newBinding = new ControllerBinding();
            var dialog = new ControllerBindingDialog(newBinding, Devices.ToList(), connectedControllers);

            if (dialog.ShowDialog() == true)
            {
                // Check for duplicate controller bindings
                if (ControllerBindings.Any(cb => cb.Input.Equals(newBinding.Input)))
                {
                    System.Windows.MessageBox.Show($"A binding for '{newBinding.Input}' already exists.", "Duplicate Controller Binding",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                ControllerBindings.Add(newBinding);
                if (_controllerInputService.IsActive)
                {
                    _controllerInputService.AddControllerBinding(newBinding);
                }
            }
        }

        private void RemoveControllerBinding(ControllerBinding binding)
        {
            if (binding != null)
            {
                ControllerBindings.Remove(binding);
                if (_controllerInputService.IsActive)
                {
                    _controllerInputService.RemoveControllerBinding(binding.Input);
                }
            }
        }

        private void AddMouseBinding()
        {
            var newBinding = new MouseInputBinding();
            var dialog = new MouseBindingDialog(newBinding, Devices.ToList());

            if (dialog.ShowDialog() == true)
            {
                // Check for duplicate mouse bindings
                if (MouseBindings.Any(mb => mb.Input.Equals(newBinding.Input)))
                {
                    System.Windows.MessageBox.Show($"A binding for '{newBinding.Input}' already exists.", "Duplicate Mouse Binding",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                MouseBindings.Add(newBinding);
                if (_mouseHookService.IsActive)
                {
                    _mouseHookService.AddMouseBinding(newBinding);
                }
            }
        }

        private void RemoveMouseBinding(MouseInputBinding binding)
        {
            if (binding != null)
            {
                MouseBindings.Remove(binding);
                if (_mouseHookService.IsActive)
                {
                    _mouseHookService.RemoveMouseBinding(binding.Input);
                }
            }
        }

        private async void OnKeyBindingTriggered(object sender, KeyBindingModel binding)
        {
            try
            {
                // Execute all device bindings for this key
                foreach (var deviceBinding in binding.DeviceBindings)
                {
                    if (deviceBinding.IsHoldMode)
                    {
                        // NEW: Start hold vibration for hold mode devices
                        await _buttplugService.StartHoldVibrateAsync(
                            deviceBinding.DeviceName,
                            deviceBinding.Intensity
                        );
                    }
                    else
                    {
                        // Regular timed vibration for non-hold mode devices
                        await _buttplugService.VibrateDeviceAsync(
                            deviceBinding.DeviceName,
                            deviceBinding.Intensity,
                            deviceBinding.DurationMs
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error executing key binding: {ex.Message}");
            }
        }

        // NEW: Handle key binding release for hold mode
        private async void OnKeyBindingReleased(object sender, KeyBindingModel binding)
        {
            try
            {
                // Stop vibration for all hold mode devices
                foreach (var deviceBinding in binding.DeviceBindings)
                {
                    if (deviceBinding.IsHoldMode)
                    {
                        await _buttplugService.StopHoldVibrateAsync(deviceBinding.DeviceName);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping hold mode vibration: {ex.Message}");
            }
        }

        private async void OnControllerBindingTriggered(object sender, ControllerBinding binding)
        {
            try
            {
                // Execute all device bindings for this controller input
                foreach (var deviceBinding in binding.DeviceBindings)
                {
                    if (deviceBinding.IsHoldMode)
                    {
                        // NEW: Start hold vibration for hold mode devices
                        await _buttplugService.StartHoldVibrateAsync(
                            deviceBinding.DeviceName,
                            deviceBinding.Intensity
                        );
                    }
                    else
                    {
                        // Regular timed vibration for non-hold mode devices
                        await _buttplugService.VibrateDeviceAsync(
                            deviceBinding.DeviceName,
                            deviceBinding.Intensity,
                            deviceBinding.DurationMs
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error executing controller binding: {ex.Message}");
            }
        }

        // NEW: Handle controller binding release for hold mode
        private async void OnControllerBindingReleased(object sender, ControllerBinding binding)
        {
            try
            {
                // Stop vibration for all hold mode devices
                foreach (var deviceBinding in binding.DeviceBindings)
                {
                    if (deviceBinding.IsHoldMode)
                    {
                        await _buttplugService.StopHoldVibrateAsync(deviceBinding.DeviceName);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping hold mode vibration: {ex.Message}");
            }
        }

        private async void OnMouseBindingTriggered(object sender, MouseInputBinding binding)
        {
            try
            {
                // Execute all device bindings for this mouse input
                foreach (var deviceBinding in binding.DeviceBindings)
                {
                    if (deviceBinding.IsHoldMode)
                    {
                        // Start hold vibration for hold mode devices
                        await _buttplugService.StartHoldVibrateAsync(
                            deviceBinding.DeviceName,
                            deviceBinding.Intensity
                        );
                    }
                    else
                    {
                        // Regular timed vibration for non-hold mode devices
                        await _buttplugService.VibrateDeviceAsync(
                            deviceBinding.DeviceName,
                            deviceBinding.Intensity,
                            deviceBinding.DurationMs
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error executing mouse binding: {ex.Message}");
            }
        }

        // Handle mouse binding release for hold mode
        private async void OnMouseBindingReleased(object sender, MouseInputBinding binding)
        {
            try
            {
                // Stop vibration for all hold mode devices
                foreach (var deviceBinding in binding.DeviceBindings)
                {
                    if (deviceBinding.IsHoldMode)
                    {
                        await _buttplugService.StopHoldVibrateAsync(deviceBinding.DeviceName);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping hold mode vibration: {ex.Message}");
            }
        }

        private void OnSpecialKeyPressed(object sender, System.Windows.Forms.Keys key)
        {
            if (key == System.Windows.Forms.Keys.F8)
            {
                // Ensure UI updates happen on the UI thread
                if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == false)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => ToggleBothHooks());
                    return;
                }

                ToggleBothHooks();
            }
        }

        private void OnConnectionStatusChanged(object sender, string status)
        {
            // Ensure UI updates happen on the UI thread
            if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == false)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => OnConnectionStatusChanged(sender, status));
                return;
            }

            ConnectionStatus = status;
            // Refresh command states
            ((RelayCommand)ConnectCommand).RaiseCanExecuteChanged();
            ((RelayCommand)DisconnectCommand).RaiseCanExecuteChanged();
            ((RelayCommand)StopAllDevicesCommand).RaiseCanExecuteChanged();
            ((RelayCommand)AddKeyBindingCommand).RaiseCanExecuteChanged();
            ((RelayCommand)AddControllerBindingCommand).RaiseCanExecuteChanged();
            ((RelayCommand)AddMouseBindingCommand).RaiseCanExecuteChanged();
        }

        private void OnDevicesChanged(object sender, System.Collections.Generic.List<ButtplugClientDevice> devices)
        {
            // Ensure UI updates happen on the UI thread
            if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == false)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => OnDevicesChanged(sender, devices));
                return;
            }

            Devices.Clear();
            foreach (var device in devices)
            {
                Devices.Add(device);
            }
        }

        private void LoadLastUsedProfile()
        {
            try
            {
                var lastUsedProfileName = _profileService.GetLastUsedProfileName();
                if (!string.IsNullOrEmpty(lastUsedProfileName))
                {
                    var profiles = _profileService.LoadAllProfiles();
                    var profile = profiles.FirstOrDefault(p => p.Name == lastUsedProfileName);

                    if (profile != null)
                    {
                        LoadProfile(profile);
                    }
                }
                // If no last used profile or profile not found, start with empty bindings
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading last used profile: {ex.Message}");
                // Start with empty bindings if loading fails
            }
        }

        private void LoadProfile(Profile profile)
        {
            try
            {
                // Stop hooks before changing bindings
                var keyboardWasActive = _keyboardHookService.IsActive;
                var controllerWasActive = _controllerInputService.IsActive;
                var mouseWasActive = _mouseHookService.IsActive;

                if (keyboardWasActive) StopKeyboardHook();
                if (controllerWasActive) StopControllerHook();
                if (mouseWasActive) StopMouseHook();

                // Defensive: clear registered bindings on the hook services even if
                // they were inactive, so we never carry over bindings from a prior
                // profile when the hooks are next started.
                _keyboardHookService.ClearKeyBindings();
                _controllerInputService.ClearControllerBindings();
                _mouseHookService.ClearMouseBindings();

                // Stop any vibrations that may still be running from the old profile
                // (e.g. a hold-mode binding triggered just before the swap). Fire-and-forget
                // so we don't block the profile load on a websocket round-trip.
                if (_buttplugService.IsConnected)
                {
                    _ = _buttplugService.StopAllDevicesAsync().ContinueWith(t =>
                    {
                        if (t.Exception != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error stopping devices during profile swap: {t.Exception.GetBaseException().Message}");
                        }
                    });
                }

                // Clear existing bindings
                KeyBindings.Clear();
                ControllerBindings.Clear();
                MouseBindings.Clear();

                if (profile != null)
                {
                    // Load key bindings
                    foreach (var keyBindingData in profile.KeyBindings)
                    {
                        var keyBinding = keyBindingData.ToKeyBinding();
                        KeyBindings.Add(keyBinding);
                    }

                    // Load controller bindings
                    foreach (var controllerBindingData in profile.ControllerBindings)
                    {
                        var controllerBinding = controllerBindingData.ToControllerBinding();
                        ControllerBindings.Add(controllerBinding);
                    }

                    // Load mouse bindings
                    foreach (var mouseBindingData in profile.MouseBindings)
                    {
                        var mouseBinding = mouseBindingData.ToMouseBinding();
                        MouseBindings.Add(mouseBinding);
                    }

                    // Save as last used profile
                    _profileService.SetLastUsedProfileName(profile.Name);
                }
                else
                {
                    // Profile is null - clear current profile
                    _profileService.SetLastUsedProfileName("");
                }

                _currentProfile = profile;
                OnPropertyChanged(nameof(CurrentProfileName));

                // Restart hooks if they were active
                if (keyboardWasActive) StartKeyboardHook();
                if (controllerWasActive) StartControllerHook();
                if (mouseWasActive) StartMouseHook();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading profile: {ex.Message}", "Profile Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void SaveCurrentProfile()
        {
            if (_currentProfile == null) return;

            try
            {
                // Update profile with current bindings
                _currentProfile.KeyBindings.Clear();
                foreach (var keyBinding in KeyBindings)
                {
                    _currentProfile.KeyBindings.Add(new KeyBindingData(keyBinding));
                }

                _currentProfile.ControllerBindings.Clear();
                foreach (var controllerBinding in ControllerBindings)
                {
                    _currentProfile.ControllerBindings.Add(new ControllerBindingData(controllerBinding));
                }

                _currentProfile.MouseBindings.Clear();
                foreach (var mouseBinding in MouseBindings)
                {
                    _currentProfile.MouseBindings.Add(new MouseBindingData(mouseBinding));
                }

                _profileService.SaveProfile(_currentProfile);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error saving profile: {ex.Message}", "Profile Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private Profile GetCurrentProfileData()
        {
            if (_currentProfile == null) return null;

            var profile = new Profile(_currentProfile.Name)
            {
                Created = _currentProfile.Created,
                LastUsed = DateTime.Now
            };

            foreach (var keyBinding in KeyBindings)
            {
                profile.KeyBindings.Add(new KeyBindingData(keyBinding));
            }

            foreach (var controllerBinding in ControllerBindings)
            {
                profile.ControllerBindings.Add(new ControllerBindingData(controllerBinding));
            }

            foreach (var mouseBinding in MouseBindings)
            {
                profile.MouseBindings.Add(new MouseBindingData(mouseBinding));
            }

            return profile;
        }

        private void ManageProfiles()
        {
            var dialog = new ProfileDialog(_profileService, LoadProfile, GetCurrentProfileData, CurrentProfileName);
            dialog.Owner = System.Windows.Application.Current.MainWindow;

            if (dialog.ShowDialog() == true)
            {
                OnPropertyChanged(nameof(CurrentProfileName));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Simple RelayCommand implementation
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object parameter) => _execute();

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool> _canExecute;

        public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter) => _canExecute?.Invoke((T)parameter) ?? true;

        public void Execute(object parameter) => _execute((T)parameter);

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}