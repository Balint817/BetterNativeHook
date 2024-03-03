using MelonLoader;
using System.Security;

namespace BetterNativeHook
{
    /// <summary>
    /// Thrown by <see cref="FakeAssembly.BuildAssembly"/> if it fails to build the assembly. It will then FailFast the program to prevent further issues
    /// </summary>
    [SecurityCritical]
    [PatchShield]
    internal sealed class AssemblyBuildFailedException: Exception
    {
        static string GetMessage(FakeAssembly assembly) => $"Failed to build assembly for the hook of {assembly.BoundMethodData?.GetFullName()??"<null> (how did we get here?)"}";
        public AssemblyBuildFailedException(FakeAssembly assembly, Exception innerException): base(GetMessage(assembly), innerException)
        {
        }
    }
}