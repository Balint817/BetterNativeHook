namespace BetterNativeHook
{
    /// <summary>
    /// When thrown from a GenericNativeHook patch callback, the message will be displayed and the program will FailFast
    /// </summary>
    public class CriticalPatchException : Exception
    {
        static string GetMessage(string? message) => $"[{MelonTrace.GetMelonFromStackTrace().MelonInfo.Name ?? "<unknown melon>"}]: " + message;
        public CriticalPatchException(string? message) : base(GetMessage(message))
        {
        }
        public CriticalPatchException(string? message, Exception innerException) : base(GetMessage(message), innerException)
        {
        }
    }
}