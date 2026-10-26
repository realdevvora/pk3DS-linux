using System;
using Avalonia.Threading;
using pk3DS.Core;

namespace pk3DS.Mac.Services;

/// <summary>
/// Shows Core's progress in the main window. Core calls it from the thread running the operation, so each call is posted to the UI thread.
/// </summary>
public sealed class UiProgressSink(Action<string> log, Action<int> setMaximum, Action<int> setValue) : IProgressSink
{
    private int value;

    public void Log(string message) => Dispatcher.UIThread.Post(() => log(message));

    public void Reset(int max)
    {
        value = 0;
        Dispatcher.UIThread.Post(() => { setMaximum(max); setValue(0); });
    }

    public void Step()
    {
        int v = ++value;
        Dispatcher.UIThread.Post(() => setValue(v));
    }

    public void Set(int v)
    {
        value = v;
        Dispatcher.UIThread.Post(() => setValue(v));
    }
}
