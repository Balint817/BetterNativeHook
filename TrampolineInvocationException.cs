using MelonLoader;
using System.Security;

namespace BetterNativeHook
{
    /// <summary>
    /// Thrown by <see cref="FakeAssembly.BuildAssembly"/> if it fails to build the assembly.
    /// <br/>
    /// It will then FailFast the program to prevent further issues.
    /// <br/>
    /// Made public so that generated code can access it, this should not be thrown manually.
    /// </summary>
    [SecurityCritical]
    [PatchShield]
    public sealed class TrampolineInvocationException : Exception
    {
        static string GetMessage(int fakeAssemblyIndex) => $"An exception was raised in the trampoline for {FakeAssembly.GetAssemblyByIndex(fakeAssemblyIndex)?.BoundMethodData?.GetFullName() ?? "<null> (this is probably the issue)"}";
        public TrampolineInvocationException(Exception innerException, int fakeAssemblyIndex) : base(GetMessage(fakeAssemblyIndex), innerException)
        {
        }
    }
}