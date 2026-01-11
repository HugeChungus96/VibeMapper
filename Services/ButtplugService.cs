using Buttplug.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ToyControlApp.Services
{
    public class ButtplugService
    {
        private ButtplugClient _client;
        private List<ButtplugClientDevice> _devices;
        private Dictionary<string, Task> _holdVibrationsTracking; // NEW: Track hold mode vibrations

        public event EventHandler<string> ConnectionStatusChanged;
        public event EventHandler<List<ButtplugClientDevice>> DevicesChanged;

        public bool IsConnected => _client?.Connected ?? false;
        public List<ButtplugClientDevice> Devices => _devices ?? new List<ButtplugClientDevice>();

        public ButtplugService()
        {
            _devices = new List<ButtplugClientDevice>();
            _holdVibrationsTracking = new Dictionary<string, Task>(); // NEW: Initialize hold tracking
        }

        public async Task ConnectAsync()
        {
            try
            {
                _client = new ButtplugClient("ToyControlApp");

                // Set up event handlers
                _client.DeviceAdded += OnDeviceAdded;
                _client.DeviceRemoved += OnDeviceRemoved;
                _client.ServerDisconnect += OnServerDisconnect;

                // Read port from config file, default to 12345 if file doesn't exist or is invalid
                int port = 12345;
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "port.txt");

                if (File.Exists(configPath))
                {
                    string content = File.ReadAllText(configPath).Trim();
                    if (int.TryParse(content, out int parsedPort))
                    {
                        port = parsedPort;
                    }
                }

                System.Windows.MessageBox.Show($"Using port: {port}\nConfig path: {configPath}");

                var connector = new ButtplugWebsocketConnector(new Uri($"ws://localhost:{port}"));
                await _client.ConnectAsync(connector);

                ConnectionStatusChanged?.Invoke(this, $"Connected to Buttplug Server on port {port}");

                // Start scanning for devices
                await _client.StartScanningAsync();
            }
            catch (Exception ex)
            {
                ConnectionStatusChanged?.Invoke(this, $"Connection failed: {ex.Message}");
                throw;
            }
        }

        public async Task DisconnectAsync()
        {
            if (_client != null)
            {
                try
                {
                    await _client.StopScanningAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error stopping scanning: {ex.Message}");
                }

                try
                {
                    await _client.DisconnectAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error disconnecting: {ex.Message}");
                }

                _client = null;
                _devices.Clear();
                _holdVibrationsTracking.Clear(); // NEW: Clear hold tracking
                DevicesChanged?.Invoke(this, _devices);
                ConnectionStatusChanged?.Invoke(this, "Disconnected");
            }
        }

        public async Task VibrateDeviceAsync(string deviceName, double intensity, int durationMs)
        {
            if (!IsConnected) return;

            var device = _devices.FirstOrDefault(d => d.Name == deviceName);
            if (device == null) return;

            try
            {
                // Check if device supports vibration
                if (device.VibrateAttributes.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Device {deviceName} does not support vibration");
                    return;
                }

                // FIXED: Properly handle intensity for discrete levels
                // Ensure intensity is between 0.0 and 1.0
                intensity = Math.Max(0.0, Math.Min(1.0, intensity));

                // For Lovense toys (20 levels), round to nearest 5% increment
                // This ensures we hit the discrete intensity levels properly
                double roundedIntensity = Math.Round(intensity * 20) / 20.0;

                // Additional safety: if rounded intensity is 0 but original was > 0, set to minimum level
                if (roundedIntensity == 0.0 && intensity > 0.0)
                {
                    roundedIntensity = 0.05; // 5% minimum
                }

                System.Diagnostics.Debug.WriteLine($"Device {deviceName}: Original intensity {intensity:F3} -> Rounded intensity {roundedIntensity:F3} ({roundedIntensity * 100:F0}%)");

                // Start vibration with properly rounded intensity
                await device.VibrateAsync(roundedIntensity);

                // Stop after duration
                if (durationMs > 0)
                {
                    _ = Task.Delay(durationMs).ContinueWith(async _ =>
                    {
                        try
                        {
                            await device.Stop();
                        }
                        catch (Exception ex)
                        {
                            // Handle device disconnection gracefully
                            System.Diagnostics.Debug.WriteLine($"Error stopping device: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error controlling device {deviceName}: {ex.Message}");
            }
        }

        // NEW: Start hold mode vibration
        public async Task StartHoldVibrateAsync(string deviceName, double intensity)
        {
            if (!IsConnected) return;

            var device = _devices.FirstOrDefault(d => d.Name == deviceName);
            if (device == null) return;

            try
            {
                // Check if device supports vibration
                if (device.VibrateAttributes.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Device {deviceName} does not support vibration");
                    return;
                }

                // Process intensity the same way as regular vibration
                intensity = Math.Max(0.0, Math.Min(1.0, intensity));
                double roundedIntensity = Math.Round(intensity * 20) / 20.0;

                if (roundedIntensity == 0.0 && intensity > 0.0)
                {
                    roundedIntensity = 0.05; // 5% minimum
                }

                System.Diagnostics.Debug.WriteLine($"Hold Mode - Device {deviceName}: Intensity {roundedIntensity:F3} ({roundedIntensity * 100:F0}%)");

                // Start continuous vibration
                await device.VibrateAsync(roundedIntensity);

                // Track this as a hold vibration (no automatic stop)
                var holdKey = $"{deviceName}_hold";
                _holdVibrationsTracking[holdKey] = Task.CompletedTask; // Just mark it as active
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error starting hold vibration for device {deviceName}: {ex.Message}");
            }
        }

        // NEW: Stop hold mode vibration
        public async Task StopHoldVibrateAsync(string deviceName)
        {
            if (!IsConnected) return;

            var device = _devices.FirstOrDefault(d => d.Name == deviceName);
            if (device == null) return;

            try
            {
                System.Diagnostics.Debug.WriteLine($"Stopping hold vibration for device {deviceName}");
                await device.Stop();

                // Remove from tracking
                var holdKey = $"{deviceName}_hold";
                _holdVibrationsTracking.Remove(holdKey);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping hold vibration for device {deviceName}: {ex.Message}");
            }
        }

        public async Task StopAllDevicesAsync()
        {
            if (!IsConnected) return;

            try
            {
                await _client.StopAllDevicesAsync();
                _holdVibrationsTracking.Clear(); // NEW: Clear all hold tracking when stopping all devices
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping all devices: {ex.Message}");
            }
        }

        private void OnDeviceAdded(object sender, DeviceAddedEventArgs e)
        {
            _devices.Add(e.Device);
            DevicesChanged?.Invoke(this, _devices);
        }

        private void OnDeviceRemoved(object sender, DeviceRemovedEventArgs e)
        {
            _devices.RemoveAll(d => d.Index == e.Device.Index);

            // NEW: Clean up any hold vibrations for removed device
            var deviceName = e.Device.Name;
            var holdKey = $"{deviceName}_hold";
            _holdVibrationsTracking.Remove(holdKey);

            DevicesChanged?.Invoke(this, _devices);
        }

        private void OnServerDisconnect(object sender, EventArgs e)
        {
            _devices.Clear();
            _holdVibrationsTracking.Clear(); // NEW: Clear hold tracking on disconnect
            DevicesChanged?.Invoke(this, _devices);
            ConnectionStatusChanged?.Invoke(this, "Server disconnected");
        }
    }
}
