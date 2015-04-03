using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace PrototypeLive
{
    /// <summary>
    /// This class can be considered as an extension of System.Console class.
    /// This ColorConsole has the following extra features:
    /// [1] WriteLine method can provide color console output font and background.
    /// [2] ClearScreen method clears the console.
    /// [3] MakeSound (overloaded) method makes a message beep or a sound with any 
    ///     given frequency and duration.
    /// [4] A GetChar() method implemented using P/Invoke.
    /// Some server utility or admin applications may need these features.
    /// </summary>

    [Flags]
    public enum ConsoleColor
    {
        ForegroundBlue = 0x0001, // text color contains Blue.
        ForegroundGreen = 0x0002, // text color contains Green.
        ForegroundCyan = 0x0003, // text color contains Cyan.
        ForegroundRed = 0x0004, // text color contains Red.
        ForegroundPurple = 0x0005, // text color contains Yellow.
        ForegroundYellow = 0x0006, // text color contains Yellow.
        ForegroundIntensity = 0x0008, // text color is intensified.
        BackgroundBlue = 0x0010, // background color contains Blue.
        BackgroundGreen = 0x0020, // background color contains Green.
        BackgroundCyan = 0x0030, // background color contains Cyan.
        BackgroundRed = 0x0040, // background color contains Red.
        BackgroundPurple = 0x0050, // background color contains Yellow.
        BackgroundYellow = 0x0060, // background color contains Yellow.
        BackgroundIntensity = 0x0080, // background color is intensified.
    };

    public class STD_HANDLES
    {
        public static short STD_INPUT_HANDLE = -10;
        public static short STD_OUTPUT_HANDLE = -11;
        public static short STD_ERROR_HANDLE = -12;
    }

    //[System.Runtime.InteropServices.ComVisible(false), sysstruct()]
    [ComVisible(false)]
    public struct CONSOLE_SCREEN_BUFFER_INFO
    {
        public COORD dwSize;
        public COORD dwCursorPosition;
        public short wAttributes;
        public SMALL_RECT srWindow;
        public COORD dwMaximumWindowSize;
    } ;

    //[System.Runtime.InteropServices.ComVisible(false), sysstruct()]
    [ComVisible(false)]
    public struct COORD
    {
        public short X;
        public short Y;
    } ;

    //[System.Runtime.InteropServices.ComVisible(false), sysstruct()]
    [ComVisible(false)]
    public struct SMALL_RECT
    {
        public short Left;
        public short Top;
        public short Right;
        public short Bottom;
    };

    //For implementing ClearScreen() method: START
    [StructLayout(LayoutKind.Sequential)]
    struct Coord
    {
        public short X; // horizontal coordinate
        public short Y; // vertical coordinate
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ConsoleScreenBufferInfo
    {
        public Coord Size;	  // size in char col & rows of screen buffer
        public Coord CurPos;  // col&row of cursor in screen buffer
        public short Attr;	  // foreground/background color attributes
        public SmallRect Win; // coords of upper-left & lower-right corners
        public Coord MaxWin;  // max size of console window
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SmallRect
    {
        public short Left;   //  x of upper left corner
        public short Top;    //  y of upper left corner
        public short Right;  //  x of lower right corner
        public short Bottom; //  y or lower right corner
    }

    enum console
    {
        stdin = -10,
        stdout = -11,
        stderr = -12
    };
    //For implementing ClearScreen() method: END

    public class ColorConsole
    {
        private static CONSOLE_SCREEN_BUFFER_INFO csbi = new CONSOLE_SCREEN_BUFFER_INFO();
        private static int stdout
        {
            get
            {
                return GetStdHandle(STD_HANDLES.STD_OUTPUT_HANDLE);
            }
        }

        private static int stderr
        {
            get
            {
                return GetStdHandle(STD_HANDLES.STD_ERROR_HANDLE);
            }
        }

        [DllImport("kernel32", CharSet = CharSet.Auto)]
        private static extern int lstrlen(String s);

        [DllImport("kernel32", CharSet = CharSet.Auto)]
        private static extern int SetConsoleTextAttribute(int hConsoleOutput, short wAttributes);

        [DllImport("kernel32", CharSet = CharSet.Auto)]
        private static extern int GetStdHandle(short nStdHandle);

        [DllImport("kernel32", CharSet = CharSet.Auto)]
        private static extern void GetConsoleScreenBufferInfo(int hConsoleOutput, ref CONSOLE_SCREEN_BUFFER_INFO lpConsoleScreenBufferInfo);

        //For implementing ClearScreen() method: START
        [SuppressUnmanagedCodeSecurityAttribute()]
        [DllImport("kernel32.dll")]
        private static extern int FillConsoleOutputCharacter(int handle,
            char ch, short len, Coord xy, out int outLen);

        [SuppressUnmanagedCodeSecurityAttribute()]
        [DllImport("kernel32.dll")]
        private static extern int FillConsoleOutputAttribute(int handle,
            short attrib, int len, Coord xy, out int outLen);

        [SuppressUnmanagedCodeSecurityAttribute()]
        [DllImport("kernel32.dll")]
        private static extern int GetConsoleScreenBufferInfo(int handle,
            out ConsoleScreenBufferInfo b);

        [SuppressUnmanagedCodeSecurityAttribute()]
        [DllImport("kernel32.dll")]
        private static extern int SetConsoleCursorPosition(int handle,
            Coord xy);

        [SuppressUnmanagedCodeSecurityAttribute()]
        [DllImport("kernel32.dll")]
        private static extern int GetLastError();

        [SuppressUnmanagedCodeSecurityAttribute()]
        [DllImport("kernel32.dll")]
        private static extern int GetStdHandle(int std);
        //For implementing ClearScreen() method: END

        [DllImport("kernel32.dll", EntryPoint = "Beep", CharSet = CharSet.Auto)]
        private static extern bool MakeSound(uint dwFrequency, uint dwDurationInMillisecs);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBeep(uint beeptype);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal extern static bool SetConsoleTitle(string lpTitleStr);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal extern static int GetConsoleTitle(StringBuilder lpBuff, int buffSize);

        public static void ErrorWriteLine(string text)
        {
            GetConsoleScreenBufferInfo(stderr, ref csbi);
            SetConsoleTextAttribute(stderr, (short)(ConsoleColor.ForegroundRed | ConsoleColor.ForegroundIntensity | ConsoleColor.BackgroundYellow | ConsoleColor.BackgroundIntensity));
            Console.Error.WriteLine(text);
            SetConsoleTextAttribute(stderr, csbi.wAttributes);
        }

        /// <summary>
        /// Title property for setting and getting console title text.
        /// </summary>
        static public string Title
        {
            set { SetConsoleTitle(value); }
            get
            {
                StringBuilder buffer = new StringBuilder(128);
                GetConsoleTitle(buffer, 128);
                return buffer.ToString();
            }
        }

        /// <summary>
        /// ConsoleColor can be "|"ed to combine colors.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="color"></param>
        public static void WriteLine(string text, ConsoleColor color)
        {
            GetConsoleScreenBufferInfo(stdout, ref csbi);
            SetConsoleTextAttribute(stdout, (short)(color | ConsoleColor.ForegroundIntensity));  //so that Foreground is always Intensity
            Console.WriteLine(text);
            SetConsoleTextAttribute(stdout, csbi.wAttributes);
        }

        /// <summary>
        /// Clears up the current screen.
        /// </summary>
        /// <returns></returns>
        public static bool ClearScreen()
        {
            int rc = 0;			// generic return code

            int h = GetStdHandle((int)console.stdout);
            if (h == -1)
                return false;
            /* setup home coords */
            Coord home = new Coord();	// init home coord (0, 0)
            /* get number of character cells in current buffer */
            ConsoleScreenBufferInfo csbi = new ConsoleScreenBufferInfo();
            rc = GetConsoleScreenBufferInfo(h, out csbi);
            if (rc == 0)
                return false;
            short conSize = (short)(csbi.Size.X * csbi.Size.Y);
            /* fill screen with blanks */
            int ocLen = 0;
            rc = FillConsoleOutputCharacter(h, ' ', conSize, home, out ocLen);
            if (rc == 0)
                return false;
            /* get current text attr */
            rc = GetConsoleScreenBufferInfo(h, out csbi);
            if (rc == 0)
                return false;
            /* set buffers' attrib */
            int oaLen = 0;
            rc = FillConsoleOutputAttribute(h, csbi.Attr,
                conSize, home, out oaLen);
            if (rc == 0)
                return false;
            /* home the cursor */
            rc = SetConsoleCursorPosition(h, home);
            if (rc == 0)
                return false;
            return true;
        }

        /// <summary>
        /// Makes a message beep.
        /// </summary>
        public static void MakeSound()
        {
            //single beep: for console apps only
            Console.WriteLine("\x0007");    //or
            //Console.WriteLine("\a");   //same sound as above

            //The following two make the same sound
            //MessageBeep(uint.MaxValue);
            //uint MB_ICONEXCLAMATION = 10;
            //MessageBeep(MB_ICONEXCLAMATION);
        }

        /// <summary>
        /// Makes a sound with given frequency and duration (in millisecond).
        /// </summary>
        /// <param name="frequency"></param>
        /// <param name="durationInMs"></param>
        public static void MakeSound(int frequency, int durationInMs)
        {
            MakeSound((uint)frequency, (uint)durationInMs);
        }

        //For implementing GetChar() method: START.
        [DllImport("kernel32", EntryPoint = "ReadConsoleW")]
        static extern bool ReadConsole(IntPtr h, out IntPtr b, uint read, out uint got, IntPtr resv);

        //		[DllImport("kernel32")]   //already defined above
        //		static extern IntPtr GetStdHandle(int handle);

        [DllImport("kernel32")]
        static extern bool GetConsoleMode(IntPtr h, out uint mode);

        [DllImport("kernel32")]
        static extern bool SetConsoleMode(IntPtr h, uint mode);

        public static char GetChar()
        {
            IntPtr hOut = (IntPtr)GetStdHandle(-10); ;

            uint oldMode;
            GetConsoleMode(hOut, out oldMode);
            SetConsoleMode(hOut, 0);

            IntPtr buf = Marshal.AllocCoTaskMem(2);
            uint r = 0;

            ReadConsole(hOut, out buf, 2, out r, IntPtr.Zero);
            SetConsoleMode(hOut, oldMode);
            if (r == 0) return (char)0;
            char c = (char)buf;
            return c;
        }
        //For implementing GetChar() method: END.

        //*****************The following code is for implementing event in console
        public enum ConsoleEvent
        {
            CTRL_C = 0,		// From wincom.h
            CTRL_BREAK = 1,
            CTRL_CLOSE = 2,
            CTRL_LOGOFF = 5,
            CTRL_SHUTDOWN = 6
        }

        /// <summary>
        /// Class defining the event
        /// </summary>
        public class ConsoleControl
        {
            public delegate void ControlEventHandler(ConsoleEvent consoleEvent);
            public event ControlEventHandler ControlEvent;
            private ControlEventHandler eventHandler;

            [DllImport("kernel32.dll")]
            static extern bool SetConsoleCtrlHandler(ControlEventHandler e, bool add);

            public ConsoleControl()
            {
                //save this to a private var so the GC doesn't collect it...
                eventHandler = new ControlEventHandler(Handler);
                SetConsoleCtrlHandler(eventHandler, true);
            }

            private void Handler(ConsoleEvent consoleEvent)
            {
                if (ControlEvent != null)
                    ControlEvent(consoleEvent);
            }
        }

        /// <summary>
        /// Console event handler
        /// </summary>
        public class ConsoleEventHandler
        {
            public static void MyEventHandler(ConsoleEvent consoleEvent)
            {
                Console.WriteLine("Event handler output: event fired = {0}", consoleEvent);
            }
        }

        public static void TestMain()
        {
            ColorConsole.ClearScreen();
            ColorConsole.Title = "My Custom Console Title";
            ColorConsole.WriteLine("(ConsoleColor.BackgroundRed | ConsoleColor.BackgroundIntensity | ConsoleColor.ForegroundGreen)", (ConsoleColor.BackgroundRed | ConsoleColor.ForegroundGreen | ConsoleColor.BackgroundIntensity));
            ColorConsole.WriteLine("(ConsoleColor.ForegroundYellow | ConsoleColor.BackgroundBlue | ConsoleColor.BackgroundGreen)", (ConsoleColor.ForegroundYellow | ConsoleColor.BackgroundBlue | ConsoleColor.BackgroundGreen));
            ColorConsole.WriteLine("(ConsoleColor.ForegroundGreen)", (ConsoleColor.ForegroundGreen));
            ColorConsole.WriteLine("(ConsoleColor.ForegroundBlue)", (ConsoleColor.ForegroundBlue));
            ColorConsole.WriteLine("(ConsoleColor.ForegroundBlue | ConsoleColor.ForegroundGreen)", (ConsoleColor.ForegroundBlue | ConsoleColor.ForegroundGreen));
            ColorConsole.WriteLine("(ConsoleColor.ForegroundBlue | ConsoleColor.ForegroundYellow)", (ConsoleColor.ForegroundBlue | ConsoleColor.ForegroundYellow));
            ColorConsole.ErrorWriteLine("ErrorWriteLine: ERROR!!!!");
            //Test MakeSound
            ColorConsole.MakeSound();
            for (int i = 40; i > 0; i--)
                ColorConsole.MakeSound((int)Math.Pow(i, 2), 100);

            //Test GetChar()
            Console.WriteLine("Press any key, and it responds without user hitting return.");
            char input = ColorConsole.GetChar();
            Console.WriteLine("You just pressed " + input.ToString() + ". \nPress c to continue.");
            while (ColorConsole.GetChar() != 'c') ;

            //Test event firing and handling capability
            ConsoleControl objConsoleControl = new ConsoleControl();
            objConsoleControl.ControlEvent += new ConsoleControl.ControlEventHandler(ConsoleEventHandler.MyEventHandler);

            Console.WriteLine("Enter 'E' to exit, or Ctrl+C, Ctrl+break, ... to fire events.");
            //			while (true)
            //			{
            //				if (Console.ReadLine() == "E")
            //					break;
            //			}
            while (Console.ReadLine() != "E") ;   //same as commented code
        }

    }  //class
}
