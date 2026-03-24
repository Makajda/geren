namespace Geren.Server.Exporter.Common;

internal class Spinner : IDisposable {
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _spinnerTask;
    private readonly int _left;
    private readonly int _top;
    private readonly ConsoleColor _color;
    private readonly char[] _frames;
    private bool _disposed;

    public Spinner(string message = "Loading", ConsoleColor color = ConsoleColor.Cyan, string frames = @"|/-\") {
        _color = color;
        _frames = frames.ToCharArray();

        Console.CursorVisible = false;
        Console.Write(message + " ");

        if (Console.IsOutputRedirected)
            _spinnerTask = Task.CompletedTask;
        else {
            _left = Console.CursorLeft;
            _top = Console.CursorTop;

            _spinnerTask = Task.Run(() => Draw(_cts.Token));
        }
    }

    private void Draw(CancellationToken token) {
        int i = 0;
        while (!token.IsCancellationRequested) {
            try {
                Console.SetCursorPosition(_left, _top);

                var oldColor = Console.ForegroundColor;
                Console.ForegroundColor = _color;

                Console.Write(_frames[i]);

                Console.ForegroundColor = oldColor;

                i = (i + 1) % _frames.Length;
                Thread.Sleep(100);
            }
            catch (ArgumentOutOfRangeException) { break; }
        }
    }

    public void Dispose() {
        GC.SuppressFinalize(this);
        if (_disposed) return;

        _cts.Cancel();
        _spinnerTask.Wait();

        try {
            if (!Console.IsOutputRedirected)
                Console.SetCursorPosition(_left, _top);

            Console.Write(" done");
            Console.WriteLine();
        }
        catch { }

        Console.CursorVisible = true;
        _cts.Dispose();
        _disposed = true;
    }
}
