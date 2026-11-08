using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using GHelper.Helpers;

public static class Logger
{
    public static string appPath = Environment.GetFolderPath(ProcessHelper.IsRunningAsSystem() ? Environment.SpecialFolder.CommonApplicationData : Environment.SpecialFolder.ApplicationData) + "\\GHelper";
    public static string logFile = appPath + "\\log.txt";

    // Logging used to open/append/close the file on every call. That put a synchronous
    // disk write on whatever thread happened to log (often the UI thread, sometimes once
    // per window message), which showed up as frame-pacing jitter while overlays were
    // running. Writes now go through a single background writer; the queue is bounded so
    // a stuck disk can never block the caller or grow memory without bound.
    private const int MaxQueuedLines = 8192;
    private static readonly BlockingCollection<string> queue = new(new ConcurrentQueue<string>());
    private static readonly Random _random = new Random();
    private static readonly Thread writerThread;
    private static volatile bool shuttingDown;

    static Logger()
    {
        writerThread = new Thread(WriteLoop)
        {
            IsBackground = true,
            Name = "GHelper.Logger"
        };
        writerThread.Start();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();
    }

    public static void WriteLine(string logMessage)
    {
        string line = $"{DateTime.Now}: {logMessage}";
        Debug.WriteLine(line);

        if (shuttingDown || queue.Count >= MaxQueuedLines) return;
        try { queue.Add(line); }
        catch (InvalidOperationException) { /* shutting down */ }
    }

    // Blocks until everything queued so far has been handed to the OS. Only used on exit
    // and by diagnostics; normal callers must never wait on the log file.
    public static void Flush()
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (queue.Count > 0 && DateTime.UtcNow < deadline)
            Thread.Sleep(5);
        Thread.Sleep(10);
    }

    private static void WriteLoop()
    {
        StreamWriter? writer = null;
        long lastFlush = Environment.TickCount64;
        try
        {
            foreach (string line in queue.GetConsumingEnumerable())
            {
                try
                {
                    if (writer is null)
                        writer = OpenWriter();
                    if (writer is null) continue;

                    writer.WriteLine(line);

                    // Flush as soon as the burst ends, and at least once a second under
                    // sustained output, so the log stays readable while never making the
                    // caller wait on the disk.
                    long now = Environment.TickCount64;
                    if (queue.Count == 0 || now - lastFlush >= 1000)
                    {
                        writer.Flush();
                        lastFlush = now;
                        if (queue.Count == 0 && _random.Next(100) == 1)
                        {
                            writer.Dispose();
                            writer = null;
                            Cleanup();
                        }
                    }
                }
                catch
                {
                    // A failed write must never kill the writer thread.
                    try { writer?.Dispose(); } catch { }
                    writer = null;
                }
            }
        }
        finally
        {
            try { writer?.Dispose(); } catch { }
        }
    }

    private static StreamWriter? OpenWriter()
    {
        try
        {
            if (!Directory.Exists(appPath)) Directory.CreateDirectory(appPath);
            return new StreamWriter(logFile, append: true, new UTF8Encoding(false));
        }
        catch
        {
            return null;
        }
    }

    private static void Shutdown()
    {
        if (shuttingDown) return;
        shuttingDown = true;
        try
        {
            queue.CompleteAdding();
            writerThread.Join(2000);
        }
        catch { }
    }

    public static void Cleanup()
    {
        try
        {
            var file = File.ReadAllLines(logFile);
            int skip = Math.Max(0, file.Count() - 2000);
            File.WriteAllLines(logFile, file.Skip(skip).ToArray());
        }
        catch { }
    }

}
