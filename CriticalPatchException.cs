using MelonLoader;
using System.Security;

namespace BetterNativeHook
{
    /// <summary>
    /// When thrown from a GenericNativeHook patch callback, the message will be displayed and the program will FailFast.
    /// <para>Essentially, this let's you decide if an error in your code is critical enough to cause further issues or corruption</para>
    /// </summary>
    [SecurityCritical]
    [PatchShield]
    public class CriticalPatchException : Exception
    {
        static string GetMessage(string? message) => $"[{MelonTrace.GetName(MelonTrace.GetMelonFromStackTrace())}]: " + message;
        public CriticalPatchException(string? message) : base(GetMessage(message))
        {
        }
        public CriticalPatchException(string? message, Exception innerException) : base(GetMessage(message), innerException)
        {
        }
    }
}