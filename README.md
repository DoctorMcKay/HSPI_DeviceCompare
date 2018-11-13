# DeviceCompare for HomeSeer HS3

This free and open-source plugin adds a new event trigger to the HS3 event editor, allowing you to compare the value of one device with another.

It might be useful to use this to compare the temperature in a room with the thermostat setpoint, for example.

# Installation

Until this plugin is included in the HomeSeer plugin updater, you will need to install it manually.
Download HSPI_DeviceCompare.exe from the [latest release](https://github.com/DoctorMcKay/HSPI_DeviceCompare/releases/latest)
and drop it into your HS3 directory (where HomeSeerAPI.dll is located). Then restart HS3.

**Make sure you don't change the filename or the plugin won't work.** No additional DLLs are required.

# Configuration

There is no configuration necessary. Just install the plugin, enable it, then you can create an event using DeviceCompare.

# Creating an Event

You can use DeviceCompare in three ways:

1. Trigger an event when a device's value *is set* and it compares somehow to another device
	- "Compares somehow" means "is less than, less than or equal to, equal to, greater than, greater than or equal to, not equal to"
	- This will trigger even if a device's value is set to the value it already had
2. Trigger an event when a device's value *changes* and it compares somehow to another device
	- This will trigger only if a device's value is set to a different value from what it already had
3. Use as a condition in an event that is triggered in some other way
	- For example, an event that turns on the porch light at sunset, but only if the porch light is dimmer than the interior lights

To use DeviceCompare as a *trigger*, create an event and in the first line next to **IF**, select "A Device's Value Compares With Another...".
Select "This device's value was set:" if you want the event to trigger even if the device's value is set the same value it already had.
Select "This device's value changed:" if you want the event to trigger only if the device's value changed.

Select the first device to compare (which will be the device that triggers the event when set), select the comparison you want, then select the
device you want to compare the first to.

To use DeviceCompare as a *condition*, the process is similar except the top line next to **IF** should be some other trigger, and
the "This device's value was set/changed" box will not appear. In this case, it doesn't matter which device is on the left and which
is on the right.

# Software Support

This plugin is tested and works under:

- Windows (Windows 10 version 1803)
- Linux (Raspbian 9 Stretch)
- Mono version 4.6.2
