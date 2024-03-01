using System.Reflection;

namespace BetterNativeHook.Properties
{
    [Obfuscation(Exclude = true, ApplyToMembers = true, Feature = "all")]
    internal static class MelonModInfo
    {
        public const string Name = "BetterNativeHook";

        public const string Description = "The mod to do what NativeHook wasn't made for";

        public const string Author = "PBalint817";

        public const string Version = "1.0.0";

        public const string DownloadLink = "";

        //Lower == Greater priority
        public const int Priority = 0;
    }
}
