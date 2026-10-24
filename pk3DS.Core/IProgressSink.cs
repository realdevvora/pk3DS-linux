namespace pk3DS.Core;

/// <summary>
/// Receives log lines and progress updates from long-running operations, independent of any UI toolkit.
/// </summary>
public interface IProgressSink
{
    /// <summary>
    /// Appends a line to the operation log.
    /// </summary>
    void Log(string message);

    /// <summary>
    /// Restarts the progress counter at zero with a new maximum.
    /// </summary>
    void Reset(int max);

    /// <summary>
    /// Advances the progress counter by one.
    /// </summary>
    void Step();

    /// <summary>
    /// Sets the progress counter to an absolute position.
    /// </summary>
    void Set(int value);
}
