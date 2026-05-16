using Buttplug.Client;
using System;
using System.Collections.Generic;
using System.IO;
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

            // Match ALL devices sharing this name. If a user has 3 Hush 2s connected
            // they all show up as "LVS-Hush2" and a binding to that name should drive
            // every instance simultaneously, not just the first one in the list.
            var devices = _devices.Where(d => d.Name == deviceName).ToList();
            if (devices.Count == 0) return;

            // Ensure intensity is between 0.0 and 1.0
            intensity = Math.Max(0.0, Math.Min(1.0, intensity));

            // For Lovense toys (20 levels), round to nearest 5% increment
            double roundedIntensity = Math.Round(intensity * 20) / 20.0;

            // Additional safety: if rounded intensity is 0 but original was > 0, set to minimum
            if (roundedIntensity == 0.0 && intensity > 0.0)
            {
                roundedIntensity = 0.05; // 5% minimum
            }

            System.Diagnostics.Debug.WriteLine($"Device(s) '{deviceName}' x{devices.Count}: Original intensity {intensity:F3} -> Rounded intensity {roundedIntensity:F3} ({roundedIntensity * 100:F0}%)");

            // Fire all devices in parallel so 3 identical toys vibrate together
            var startTasks = new List<Task>();
            foreach (var device in devices)
            {
                if (device.VibrateAttributes.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Device {deviceName} (index {device.Index}) does not support vibration; skipping");
                    continue;
                }

                var capturedDevice = device;
                startTasks.Add(SafeVibrateAsync(capturedDevice, roundedIntensity));
            }

            try
            {
                await Task.WhenAll(startTasks);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error starting vibration for one or more '{deviceName}' devices: {ex.Message}");
            }

            // Schedule stop for each device after duration
            if (durationMs > 0)
            {
                foreach (var device in devices)
                {
                    if (device.VibrateAttributes.Count == 0) continue;
                    var capturedDevice = device;
                    _ = Task.Delay(durationMs).ContinueWith(async _ =>
                    {
                        try
                        {
                            await capturedDevice.Stop();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error stopping device (index {capturedDevice.Index}): {ex.Message}");
                        }
                    });
                }
            }
        }

        private static async Task SafeVibrateAsync(ButtplugClientDevice device, double intensity)
        {
            try
            {
                await device.VibrateAsync(intensity);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error vibrating device '{device.Name}' (index {device.Index}): {ex.Message}");
            }
        }

        // Start hold mode vibration — fans out to all devices sharing this name.
        public async Task StartHoldVibrateAsync(string deviceName, double intensity)
        {
            if (!IsConnected) return;

            var devices = _devices.Where(d => d.Name == deviceName).ToList();
            if (devices.Count == 0) return;

            // Process intensity the same way as regular vibration
            intensity = Math.Max(0.0, Math.Min(1.0, intensity));
            double roundedIntensity = Math.Round(intensity * 20) / 20.0;

            if (roundedIntensity == 0.0 && intensity > 0.0)
            {
                roundedIntensity = 0.05; // 5% minimum
            }

            System.Diagnostics.Debug.WriteLine($"Hold Mode - Device(s) '{deviceName}' x{devices.Count}: Intensity {roundedIntensity:F3} ({roundedIntensity * 100:F0}%)");

            var tasks = new List<Task>();
            foreach (var device in devices)
            {
                if (device.VibrateAttributes.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Device {deviceName} (index {device.Index}) does not support vibration; skipping");
                    continue;
                }
                tasks.Add(SafeVibrateAsync(device, roundedIntensity));
            }

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error starting hold vibration for '{deviceName}': {ex.Message}");
            }

            // Track this as a hold vibration (no automatic stop)
            var holdKey = $"{deviceName}_hold";
            _holdVibrationsTracking[holdKey] = Task.CompletedTask; // Just mark it as active
        }

        // Stop hold mode vibration — stops every device sharing this name.
        public async Task StopHoldVibrateAsync(string deviceName)
        {
            if (!IsConnected) return;

            var devices = _devices.Where(d => d.Name == deviceName).ToList();
            if (devices.Count == 0) return;

            System.Diagnostics.Debug.WriteLine($"Stopping hold vibration for '{deviceName}' x{devices.Count}");

            var tasks = new List<Task>();
            foreach (var device in devices)
            {
                var capturedDevice = device;
                tasks.Add(SafeStopAsync(capturedDevice));
            }

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping hold vibration for '{deviceName}': {ex.Message}");
            }

            // Remove from tracking
            var holdKey = $"{deviceName}_hold";
            _holdVibrationsTracking.Remove(holdKey);
        }

        private static async Task SafeStopAsync(ButtplugClientDevice device)
        {
            try
            {
                await device.Stop();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping device '{device.Name}' (index {device.Index}): {ex.Message}");
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
