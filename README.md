<div align="center">
  
![VibeMapper logo plus name_logo and name horizontal](https://github.com/user-attachments/assets/553d67a3-cce9-405a-88e5-7516aa052c9c)

</div>

# VibeMapper
A Windows application that uses [Intiface Central](https://intiface.com/central/) and the [Buttplug](https://buttplug.io/) protocol to map keyboard, mouse, and controller inputs to connected devices, it works on gamepads too but I'm sure by now thanks to the name of that beautiful protocol you are well aware of its intended purpose.


## Features

-  **Controller Support** - Xbox controllers with full button/trigger/stick mapping
-  **Keyboard Bindings** - Global hotkey support with hold mode
-  **Mouse Input** - Left/right/middle click, wheel, and side buttons
-  **Profile System** - Save and load different control schemes
-  **Multi-Device** - Control multiple devices simultaneously
-  **Flexible Timing** - Timed or hold-mode vibrations
-  **Emergency Stop** - F8 key instantly disables all hooks

## Requirements

- **Windows 10/11**
- **Bluetooth support**
- **Intiface Central**


## Usage

1. Download and install Intiface Central from https://intiface.com/central/
2. Run Intiface Central, start the server, go to devices, hit start scanning, it should detect your toys and connect automatically.
3. Grab VibeMapper from [Releases page]([link](https://github.com/LivingTh1ng/VibeMapper/releases)), launch it, and click connect.
4. Create a new profile in "Manage Profiles" and load it.
5. Click "Add Binding" in any of the three boxes to add a keyboard, controller, or mouse binding.
6. Select input key, what device to bind it to, duration and intensity (you can bind multiple devices here at a time with different settings each) click ok to add binding for this key.
7. Save profile, and start the relevant hooks when ready to play, to deactivate hit stop hook buttons or press F8 as an emergency off switch.

This did not happen on my machines but Windows Defender might tell you this app or Intiface Central is a virus, this is probably because of the mouse, keyboard, and controller hooks, feel free to tell it to kick rocks.

If your Intiface uses a different port, edit the port.txt file with the port, save, and VibeMapper should use that one instead.

## Safety & Privacy
- This app runs with global input hooks - it can see your keystrokes/clicks when active
- Your device connection data stays local, nothing gets sent anywhere
- If you don't believe me just check the code
- Use F8 panic button if things get weird
- Don't blame me if you accidentally activate something during a work meeting

## Special Thanks
Special thanks to [Izzy Sullivan](https://www.instagram.com/akangelic/) for the adorable logo.

## Final Note
This project was born out of my surprise at not being able to find anything similar, so feel free the link me another tool that does exactly this so I can go bash my head against the nearest wall. This was my first C# .NET project, therefore the code is probably ass, please excuse or give it a go at fixing it yourself if you are interested. I probably will not update this beyond keeping it functional and relatively bug free so fork the crap out of it if you want. If you have any issues getting it working let me know and I'll do my best at helping you out.
