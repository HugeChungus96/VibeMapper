using Buttplug.Client;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using ToyControlApp.Models;
using KeyBindingModel = ToyControlApp.Models.KeyBinding;

namespace ToyControlApp
{
    public partial class KeyBindingDialog : Window, INotifyPropertyChanged
    {
        private readonly KeyBindingModel _binding;
        private readonly List<ButtplugClientDevice> _availableDevices;
        private System.Windows.Forms.Keys _selectedKey = System.Windows.Forms.Keys.None;

        private DeviceBinding _selectedDeviceBinding;

        public ObservableCollection<DeviceBinding> DeviceBindings { get; }

        public DeviceBinding SelectedDeviceBinding
        {
            get => _selectedDeviceBinding;
            set
            {
                _selectedDeviceBinding = value;
                OnPropertyChanged();
            }
        }

        public KeyBindingDialog(KeyBindingModel binding, List<ButtplugClientDevice> devices)
        {
            InitializeComponent();
            _binding = binding;
            _availableDevices = devices;

            // Initialize device bindings collection
            DeviceBindings = new ObservableCollection<DeviceBinding>();

            // If editing existing binding, populate with existing device bindings
            if (_binding.DeviceBindings.Any())
            {
                foreach (var deviceBinding in _binding.DeviceBindings)
                {
                    DeviceBindings.Add(new DeviceBinding(deviceBinding.DeviceName, deviceBinding.Intensity, deviceBinding.DurationMs, deviceBinding.IsHoldMode));
                }
                _selectedKey = _binding.Key;
                KeyTextBox.Text = _selectedKey == System.Windows.Forms.Keys.None ? "Press a key..." : _selectedKey.ToString();
            }

            // Set up data context for binding
            DataContext = this;

            // Populate device combo box
            DeviceComboBox.ItemsSource = devices;
            if (devices.Count > 0)
            {
                DeviceComboBox.SelectedIndex = 0;
            }

            UpdateOkButtonState();
            UpdateDurationControlsVisibility(); // Set initial state

            // Focus the key textbox
            KeyTextBox.Focus();
        }

        private void KeyTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            // WPF reports Alt as Key.System; pull the real key out of SystemKey in that case.
            // This is also where bindings to Alt-modified keys can be captured later.
            var wpfKey = e.Key == Key.System ? e.SystemKey : e.Key;

            // Convert WPF key to Windows Forms key
            var key = (System.Windows.Forms.Keys)KeyInterop.VirtualKeyFromKey(wpfKey);

            // Ignore modifier keys alone (don't allow binding to just a modifier)
            if (key == System.Windows.Forms.Keys.ShiftKey ||
                key == System.Windows.Forms.Keys.ControlKey ||
                key == System.Windows.Forms.Keys.Menu || // Alt
                key == System.Windows.Forms.Keys.LMenu ||
                key == System.Windows.Forms.Keys.RMenu ||
                key == System.Windows.Forms.Keys.LShiftKey ||
                key == System.Windows.Forms.Keys.RShiftKey ||
                key == System.Windows.Forms.Keys.LControlKey ||
                key == System.Windows.Forms.Keys.RControlKey ||
                key == System.Windows.Forms.Keys.Alt ||
                key == System.Windows.Forms.Keys.LWin ||
                key == System.Windows.Forms.Keys.RWin ||
                key == System.Windows.Forms.Keys.None)
            {
                e.Handled = true; // still swallow so the textbox doesn't try to handle it
                return;
            }

            _selectedKey = key;
            KeyTextBox.Text = key.ToString();

            UpdateOkButtonState();
            // Always mark handled so WPF/TextBox doesn't try to interpret the key
            // (this is the root cause of Space being swallowed when using KeyDown).
            e.Handled = true;
        }

        // Handle hold mode checkbox changes
        private void HoldModeCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            UpdateDurationControlsVisibility();
        }

        // Update visibility of duration controls based on hold mode
        private void UpdateDurationControlsVisibility()
        {
            bool isHoldMode = HoldModeCheckBox.IsChecked == true;

            // Grey out and disable duration controls when hold mode is enabled
            DurationLabel.IsEnabled = !isHoldMode;
            DurationTextBox.IsEnabled = !isHoldMode;

            // Use theme resources instead of hardcoded colors
            DurationTextBox.Background = isHoldMode ?
                (System.Windows.Media.SolidColorBrush)FindResource("DarkDisabledBrush") :
                (System.Windows.Media.SolidColorBrush)FindResource("DarkSurfaceBrush");

            // Set helpful text when disabled
            if (isHoldMode && DurationTextBox.Text == "1000")
            {
                DurationTextBox.Text = "N/A (Hold Mode)";
            }
            else if (!isHoldMode && DurationTextBox.Text == "N/A (Hold Mode)")
            {
                DurationTextBox.Text = "1000";
            }
        }

        private void AddDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            if (DeviceComboBox.SelectedItem == null)
            {
                MessageBox.Show("Please select a device.", "No Device Selected",
                               MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool isHoldMode = HoldModeCheckBox.IsChecked == true;
            int duration = 1000; // Default duration

            // Only validate duration if not in hold mode
            if (!isHoldMode)
            {
                if (!int.TryParse(DurationTextBox.Text, out duration) || duration < 0)
                {
                    MessageBox.Show("Please enter a valid duration in milliseconds (0 or greater).", "Invalid Duration",
                                   MessageBoxButton.OK, MessageBoxImage.Warning);
                    DurationTextBox.Focus();
                    DurationTextBox.SelectAll();
                    return;
                }

                if (duration > 60000) // 1 minute max
                {
                    var result = MessageBox.Show("Duration is longer than 1 minute. Are you sure?", "Long Duration",
                                               MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result != MessageBoxResult.Yes)
                    {
                        DurationTextBox.Focus();
                        DurationTextBox.SelectAll();
                        return;
                    }
                }
            }

            var selectedDevice = (ButtplugClientDevice)DeviceComboBox.SelectedItem;

            // Check if device is already added
            if (DeviceBindings.Any(db => db.DeviceName == selectedDevice.Name))
            {
                MessageBox.Show($"Device '{selectedDevice.Name}' is already added to this key binding.",
                               "Device Already Added", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Create new device binding with hold mode support
            var deviceBinding = new DeviceBinding(
                selectedDevice.Name,
                IntensitySlider.Value / 100.0,
                duration,
                isHoldMode // Pass hold mode flag
            );

            DeviceBindings.Add(deviceBinding);
            UpdateOkButtonState();

            // Reset the form
            DeviceComboBox.SelectedIndex = 0;
            IntensitySlider.Value = 50;
            HoldModeCheckBox.IsChecked = false; // Reset hold mode
            DurationTextBox.Text = "1000";
            UpdateDurationControlsVisibility(); // Update visibility after reset
        }

        private void RemoveDeviceBinding_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is DeviceBinding binding)
            {
                DeviceBindings.Remove(binding);
                UpdateOkButtonState();
            }
        }

        private void UpdateOkButtonState()
        {
            // Enable OK button if we have a key and at least one device binding
            OkButton.IsEnabled = _selectedKey != System.Windows.Forms.Keys.None &&
                                DeviceBindings.Count > 0;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Validate inputs
            if (_selectedKey == System.Windows.Forms.Keys.None)
            {
                MessageBox.Show("Please select a key.", "Invalid Input",
                               MessageBoxButton.OK, MessageBoxImage.Warning);
                KeyTextBox.Focus();
                return;
            }

            if (DeviceBindings.Count == 0)
            {
                MessageBox.Show("Please add at least one device binding.", "No Device Bindings",
                               MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Update the binding
            _binding.Key = _selectedKey;
            _binding.DeviceBindings.Clear();

            foreach (var deviceBinding in DeviceBindings)
            {
                _binding.DeviceBindings.Add(new DeviceBinding(
                    deviceBinding.DeviceName,
                    deviceBinding.Intensity,
                    deviceBinding.DurationMs,
                    deviceBinding.IsHoldMode // Include hold mode
                ));
            }

            _binding.UpdateDescription();

            DialogResult = true;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}