using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Midi;
using System.Drawing;

namespace LaunchpadNET
{
    public class Interface
    {
        

        private Pitch[] rowBasePitch = new Pitch[8];
        private Control ccBaseControl = new Control();

        private enum LaunchpadType { Mini, MK2};

        LaunchpadType launchpadType;

        public struct LaunchpadColors {
            public static Color dimRed = Color.FromArgb(64 , 0 , 0);
            public static Color midRed = Color.FromArgb(128 , 0 , 0);
            public static Color fullRed = Color.FromArgb(255 , 0 , 0);
            public static Color dimAmber = Color.FromArgb(64 , 64 , 0);
            public static Color midAmber = Color.FromArgb(128 , 128, 0);
            public static Color fullAmber = Color.FromArgb(255 , 255 , 0);
            public static Color dimGreen = Color.FromArgb(0 , 64 , 0);
            public static Color midGreen = Color.FromArgb(0 , 128 , 0);
            public static Color fullGreen = Color.FromArgb(0 , 255 , 0);
        }
        

        public InputDevice targetInput;
        public OutputDevice targetOutput;

        public delegate void LaunchpadKeyEventHandler(object source, LaunchpadKeyEventArgs e);

        public delegate void LaunchpadCCKeyEventHandler(object source, LaunchpadCCKeyEventArgs e);

        /// <summary>
        /// Event Handler when a Launchpad Key is pressed.
        /// </summary>
        public event LaunchpadKeyEventHandler OnLaunchpadKeyPressed;
        /// <summary>
        /// Event Handler when a Launchpad Key is released.
        /// </summary>
        public event LaunchpadKeyEventHandler OnLaunchpadKeyReleased;
        //public event LaunchpadCCKeyEventHandler OnLaunchpadCCKeyPressed;

        public class LaunchpadCCKeyEventArgs : EventArgs
        {
            private int val;
            public LaunchpadCCKeyEventArgs(int _val)
            {
                val = _val;
            }
            public int GetVal()
            {
                return val;
            }
        }

        /// <summary>
        /// EventArgs for pressed Launchpad Key
        /// </summary>
        public class LaunchpadKeyEventArgs : EventArgs {
            private int x;
            private int y;
            private int velocity;
            public LaunchpadKeyEventArgs(int _pX , int _pY , int _velocity) {
                x = _pX;
                y = _pY;
                velocity = _velocity;
            }
            public int GetX() {
                return x;
            }
            public int GetY() {
                return y;
            }
            public int GetVelocity() {
                return velocity;
            }
        }

        public void scrollText(Color color, bool loop, string text) {
            switch(launchpadType) {
                case LaunchpadType.Mini:
                    //TODO: Not implemented
                    break;
                case LaunchpadType.MK2:
                    int color_val;
                    if (!launchpadMk2_colors.TryGetValue(color , out color_val)) {
                        Console.WriteLine($"<Interface:FlashLED> Can't find a color of {color.Name} in the dictionary");
                        return;
                    }
                    byte[] data = { 240 , 0 , 32 , 41 , 2 , 24 , 20 , (byte)color_val , loop ? (byte)1 : (byte)0 };
                    data.Concat(Encoding.ASCII.GetBytes(text));
                    data.Concat(new byte[] { 247 });
                    targetOutput.SendSysEx(data); // should probably catch an exception
                    break;
            }
        }

        private void sysExAnswer(SysExMessage m)
        {
            byte[] msg = m.Data;
            byte[] stopBytes = { 240, 0, 32, 41, 2, 24, 21, 247 };
        }

        private void midiPress(Midi.NoteOnMessage msg)
        {
            //Console.WriteLine("<MIDI> "+msg.Channel+" "+msg.Device+" "+msg.Pitch+" "+msg.Velocity);
            if (msg.Velocity > 0) {
                OnLaunchpadKeyPressed?.Invoke(this , new LaunchpadKeyEventArgs(midiNoteToLed(msg.Pitch)[1] , midiNoteToLed(msg.Pitch)[0] , msg.Velocity));
            } else {
                OnLaunchpadKeyReleased?.Invoke(this , new LaunchpadKeyEventArgs(midiNoteToLed(msg.Pitch)[1] , midiNoteToLed(msg.Pitch)[0] , msg.Velocity));
            }
        }

        private void midiCC(Midi.ControlChangeMessage msg) {
            //Console.WriteLine($"<MIDI> CC {msg.Channel} {msg.Device} {msg.Control} {msg.Value}");
            if (msg.Value > 0) {
                OnLaunchpadKeyPressed?.Invoke(this , new LaunchpadKeyEventArgs(midiCCToLed(msg.Control), -1, msg.Value)); // top row buttons
            } else {
                OnLaunchpadKeyReleased?.Invoke(this , new LaunchpadKeyEventArgs(midiCCToLed(msg.Control), -1, msg.Value)); // top row buttons
            }
        }

        /// <summary>
        /// Returns the LED coordinates of a MIdi note
        /// </summary>
        /// <param name="p">The Midi Note.</param>
        /// <returns>The X,Y coordinates.</returns>
        public int[] midiNoteToLed(Pitch p)
        {
            for (int x = 0; x <= 7; x++)
            {
                if (rowBasePitch[x]+10 > p) // if it's on this row
                {
                    if (launchpadType == LaunchpadType.MK2) {
                        int[] r1 = { Math.Abs(x - 7) , p - rowBasePitch[x] }; // find the column by subtracting the base
                        return r1;
                    } else {
                        int[] r1 = { x , p - rowBasePitch[x] }; // find the column by subtracting the base
                        return r1;
                    }
                }
            }
            int[] r2 = { -99, -99 };
            return r2;
        }

        public int midiCCToLed(Control c) {
            return c - ccBaseControl;
        }

        /// <summary>
        /// Returns the equilavent Midi Note to X and Y coordinates.
        /// </summary>
        /// <param name="x">The X coordinate of the LED</param>
        /// <param name="y">The Y coordinate of the LED</param>
        /// <returns>The midi note</returns>
        public Pitch ledToMidiNote(int x, int y)
        {
            if (launchpadType == LaunchpadType.MK2) {
                return rowBasePitch[x] + Math.Abs(7-y);
            } else {
                return rowBasePitch[x] + y;
            }
        }

        public void clearAllLEDs()
        {
            for (int x = -1; x < 8; x++)
            {
                for (int y = 0; y < 9; y++)
                {
                    setLED(x, y, 0);
                }
            }

            for (int tx = 1; tx < 9; tx++)
            {
                setTopLEDs(tx, 0);
            }
        }

        /// <summary>
        /// Fills Top Row LEDs.
        /// </summary>
        /// <param name="startX"></param>
        /// <param name="endX"></param>
        /// <param name="velo"></param>
        public void fillTopLEDs(int startX, int endX, int velo)
        {
            for (int x = 1; x < 9; x++)
            {
                if (x >= startX && x <= endX)
                {
                    setTopLEDs(x, velo);
                }
            }
        }

        /// <summary>
        /// Creates a rectangular mesh of LEDs.
        /// </summary>
        /// <param name="startX">Start X coordinate</param>
        /// <param name="startY">Start Y coordinate</param>
        /// <param name="endX">End X coordinate</param>
        /// <param name="endY">End Y coordinate</param>
        /// <param name="velo">Painting velocity</param>
        public void fillLEDs(int startX, int startY, int endX, int endY, int velo)
        {
            for (int x = 0; x < rowBasePitch.Length; x++)
            {
                for (int y = 0; y < rowBasePitch.Length; y++)
                {
                    if (x >= startX && y >= startY && x <= endX && y <= endY)
                        setLED(x, y, velo);
                }
            }
        }

        /// <summary>
        /// Sets a Top LED of the launchpad
        /// </summary>
        /// <param name="x"></param>
        /// <param name="velo"></param>
        /// //NOTUSED
        public void setTopLEDs(int x, int velo)
        {
            byte[] data = { 240, 0, 32, 41, 2, 24, 10, Convert.ToByte(103+x), Convert.ToByte(velo), 247 };
            targetOutput.SendSysEx(data);
        }

        /// <summary>
        /// Sets a LED of the Launchpad.
        /// </summary>
        /// <param name="x">The X coordinate.</param> I would consider this y
        /// <param name="y">The Y coordinate.</param>
        /// <param name="velo">The velocity.</param>
        /// for a launchpad mk1, bit 6 is always 0, 5/4 is green, 3 is clear other buffer, 2 is write to both buffers, 1/0 is red
        public void setLED(int x, int y, int velo)
        {
            if (x < 0) return;
            try {
                if (y >= 0) {
                    if (launchpadType == LaunchpadType.MK2) y = Math.Abs(7 - y); // old way
                    targetOutput.SendNoteOn(Channel.Channel1 , rowBasePitch[y] + x , velo);
                    //Console.WriteLine("<MIDI> Send NoteOn: "+(rowBasePitch[x]+y)+" "+velo);
                } else if (y == -1) {
                    // top row
                    //Console.WriteLine("<MIDI> Send CC: "+(x+ccBaseControl)+" "+velo);
                    targetOutput.SendControlChange(Channel.Channel1 , x + ccBaseControl , velo);
                }
            } catch (Midi.DeviceException) {
                Console.WriteLine("<< LAUNCHPAD.NET >> Midi.DeviceException");
                // probably disconnected do the right thing here or call some function thingy
                throw;
            }
        }

        public void setLED(int x, int y, Color color) {
            switch(launchpadType) {
                case LaunchpadType.Mini:
                    setLED(x , y , colorToVelocity(color) + 0x0C); // 0x0c for normal use (not flashing, not double buffered)
                    break;
                case LaunchpadType.MK2:
                    y = y>-1 ? Math.Abs(y - 7) : y;
                    byte[] data = { 240 , 0 , 32 , 41 , 2 , 24 , 11 , (y>=0) ? (byte)((y + 1) * 10 + x + 1) : (byte)(104+x), (byte)(color.R/4), (byte)(color.G/4), (byte)(color.B/4), 247 };
                    //Console.WriteLine($"Sent: {data[7]} {data[8]} {data[9]} {data[10]}");
                    targetOutput.SendSysEx(data); // should probably catch an exception
                    break;
            }
        }

        /// <summary>
        /// Returns all connected and installed Launchpads.
        /// </summary>
        /// <returns>Returns LaunchpadDevice array.</returns>
        public LaunchpadDevice[] getConnectedLaunchpads()
        {
            List<LaunchpadDevice> tempDevices = new List<LaunchpadDevice>();
            Midi.InputDevice.UpdateInstalledDevices();
            Midi.OutputDevice.UpdateInstalledDevices();

            foreach (InputDevice id in Midi.InputDevice.InstalledDevices)
            {
                foreach (OutputDevice od in Midi.OutputDevice.InstalledDevices)
                {
                    if (id.Name == od.Name)
                    {
                        if (id.Name.ToLower().Contains("launchpad"))
                        {
                            tempDevices.Add(new LaunchpadDevice(id.Name));
                        }
                    }
                }
            }

            return tempDevices.ToArray();
        }

        private int colorToVelocity(Color color) {
            switch(launchpadType) {
                case LaunchpadType.Mini:
                    return (0x10 * Math.Min(color.G / 64 , 3)) + Math.Min(color.R / 64 , 3);
                case LaunchpadType.MK2:
                    int retval;
                    launchpadMk2_colors.TryGetValue(color, out retval);
                    return retval;
                default:
                    return 0;
            }
        }

        public void flashLED(int x, int y, Color color) { // x is horizontal, y vertical
            switch(launchpadType) {
                case LaunchpadType.Mini:
                    //TODO: Not implemented
                    break;
                case LaunchpadType.MK2:
                    y = y>-1 ? Math.Abs(y - 7) : y;
                    int color_val;
                    if (!launchpadMk2_colors.TryGetValue(color , out color_val)) Console.WriteLine($"<Interface:FlashLED> Can't find a color of {color.Name} in the dictionary");
                    byte[] data = { 240 , 0 , 32 , 41 , 2 , 24 , 35 , 0, (y>=0) ? (byte)((y + 1) * 10 + x + 1) : (byte)(104+x), (byte)color_val, 247 };
                    targetOutput.SendSysEx(data); // should probably catch an exception
                    break;
            }
        }

        public void pulseLED(int x, int y, Color color) { // x is horizontal, y vertical
            switch(launchpadType) {
                case LaunchpadType.Mini:
                    //TODO: Not implemented
                    break;
                case LaunchpadType.MK2:
                    y = y>-1 ? Math.Abs(y - 7) : y;
                    int color_val;
                    if (!launchpadMk2_colors.TryGetValue(color , out color_val)) Console.WriteLine($"<Interface:FlashLED> Can't find a color of {color.Name} in the dictionary");
                    byte[] data = { 240 , 0 , 32 , 41 , 2 , 24 , 40 , 0, (y>=0) ? (byte)((y + 1) * 10 + x + 1) : (byte)(104+x), (byte)color_val, 247 };
                    targetOutput.SendSysEx(data); // should probably catch an exception
                    break;
            }
        }

        /// <summary>
        /// Function to connect with a LaunchpadDevice
        /// </summary>
        /// <param name="device">The Launchpad to connect to.</param>
        /// <returns>Returns bool if connection was successful.</returns>
        public bool connect(LaunchpadDevice device)
        {
            foreach(InputDevice id in Midi.InputDevice.InstalledDevices)
            {
                if (id.Name.ToLower() == device._midiName.ToLower()) {
                    targetInput = id;
                    Console.WriteLine($"Connecting to : {id.Name}");
                    targetInput.Open();
                    if (id.Name == "Launchpad MK2") {
                        Console.WriteLine($"Detected as MK2, using updated button positions");
                        rowBasePitch = rowBasePitch_launchpadmk2;
                        ccBaseControl = ccBaseControl_launchpadmk2;
                        launchpadType = LaunchpadType.MK2;
                    } else {
                        rowBasePitch = rowBasePitch_launchpadMini; // works-ish with a lot of them
                        ccBaseControl = ccBaseControl_launchpadMini;
                        launchpadType = LaunchpadType.Mini;
                    }
                    Console.WriteLine($"isReceiving: {targetInput.IsReceiving}, open: {targetInput.IsOpen}");
                    targetInput.NoteOn += new InputDevice.NoteOnHandler(midiPress);
                    targetInput.ControlChange += new InputDevice.ControlChangeHandler(midiCC);
                    targetInput.StartReceiving(null);
                }
            }
            foreach (OutputDevice od in Midi.OutputDevice.InstalledDevices)
            {
                if (od.Name.ToLower() == device._midiName.ToLower())
                {
                    targetOutput = od;
                    od.Open();
                }
            }

            return true; // targetInput.IsOpen && targetOutput.IsOpen;
        }

        public bool isConnected() {
            if (targetOutput == null || targetInput == null) return false;
            if (targetInput.IsOpen && targetOutput.IsOpen) return true;
            return false;
        }

        /// <summary>
        /// Disconnects a given LaunchpadDevice
        /// </summary>
        /// <param name="device">The Launchpad to disconnect.</param>
        /// <returns>Returns bool if disconnection was successful.</returns>
        public bool disconnect(LaunchpadDevice device)
        {
            if (targetInput.IsOpen && targetOutput.IsOpen)
            {
                targetInput.StopReceiving();
                targetInput.Close();
                targetOutput.Close();
            }
            return !targetInput.IsOpen && !targetOutput.IsOpen;
        }

        public class LaunchpadDevice
        {
            public string _midiName;
            //public int _midiDeviceId;

            public LaunchpadDevice(string name)
            {
                _midiName = name;
            }
        }

        // launchpad mini: 

        Pitch[] rowBasePitch_launchpadMini = new Pitch[8] { // the base note of each row (left-most)
            Pitch.CNeg1,
            Pitch.E0,
            Pitch.GSharp1,
            Pitch.C3,
            Pitch.E4,
            Pitch.GSharp5,
            Pitch.C7,
            Pitch.E8
        }; 
        Control ccBaseControl_launchpadMini = (Control)104; // the base control number of the top row (CC changes rather than note)

    

        // launchpad mk2:
        Pitch[] rowBasePitch_launchpadmk2 = new Pitch[8] { // the base note of each row (left-most)
            Pitch.BNeg1,
            Pitch.A0,
            Pitch.G1,
            Pitch.F2,
            Pitch.DSharp3,
            Pitch.CSharp4,
            Pitch.B4,
            Pitch.A5
        }; 
        Control ccBaseControl_launchpadmk2 = (Control)104; // the base control number of the top row (CC changes rather than note)

        Dictionary<Color , int> launchpadMk2_colors = new Dictionary<Color , int>() {
            {Color.Black, 0},
            {Color.Red, 5},
            {Color.Yellow,13},
            {Color.Green,21},
            {Color.LightBlue,29},
            {Color.Blue,67},
            {Color.Magenta,45},
            {Color.Pink,57},
            {Color.DarkOliveGreen,63},
            {Color.White,4},
            {Color.DarkBlue,38},
            {Color.DarkGreen,22},
            {Color.DarkMagenta,46},
            {Color.DarkRed,6},
            {Color.Orange,9},
            {Color.DarkOrange,126},
            {Color.Violet,48}
        };
    }
}
