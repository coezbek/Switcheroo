using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Switcheroo
{
    /// <summary>
    /// Diagnostic log for the time it takes to open the switcher.
    /// Off by default. Start Switcheroo with --perflog to turn it on.
    /// Writes one line per opening to %TEMP%\switcheroo-perf.log.
    /// Stage times are milliseconds since the hook callback started.
    /// </summary>
    public static class PerfLog
    {
        public static readonly string Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "switcheroo-perf.log");

        public static bool Enabled { get; set; }

        private static readonly object Lock = new object();
        private static readonly Stopwatch Clock = Stopwatch.StartNew();

        private static StringBuilder _line;
        private static long _start;
        private static long _lastEnd;
        private static int _gen0, _gen1, _gen2;
        private static int _count;

        public static void Write(string text)
        {
            if (!Enabled) return;
            try
            {
                lock (Lock)
                {
                    File.AppendAllText(Path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + text + Environment.NewLine);
                }
            }
            catch
            {
                // Diagnostics must never break the switcher.
            }
        }

        /// <summary>Starts a new opening. eventTick is the GetTickCount time of the key press.</summary>
        public static void Begin(string trigger, int eventTick)
        {
            if (!Enabled) return;
            _start = Clock.ElapsedMilliseconds;
            _gen0 = GC.CollectionCount(0);
            _gen1 = GC.CollectionCount(1);
            _gen2 = GC.CollectionCount(2);
            _line = new StringBuilder();
            _line.Append("#").Append(++_count)
                .Append(" ").Append(trigger)
                .Append(" idleBefore=").Append(((_start - _lastEnd) / 1000.0).ToString("F1")).Append("s")
                .Append(" keyToHook=").Append(unchecked(Environment.TickCount - eventTick)).Append("ms");
        }

        /// <summary>Records the time since Begin under the given stage name.</summary>
        public static void Mark(string stage)
        {
            if (_line == null) return;
            _line.Append(" ").Append(stage).Append("=").Append(Clock.ElapsedMilliseconds - _start);
        }

        public static void Note(string text)
        {
            if (_line == null) return;
            _line.Append(" ").Append(text);
        }

        /// <summary>Writes the line. Call once the window is on screen and the UI thread is idle.</summary>
        public static void End()
        {
            if (_line == null) return;
            _lastEnd = Clock.ElapsedMilliseconds;
            _line.Append(" gc=")
                .Append(GC.CollectionCount(0) - _gen0).Append("/")
                .Append(GC.CollectionCount(1) - _gen1).Append("/")
                .Append(GC.CollectionCount(2) - _gen2);
            var text = _line.ToString();
            _line = null;
            Write(text);
        }
    }
}
