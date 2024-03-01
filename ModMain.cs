using MelonLoader;

namespace BetterNativeHook
{
    public class ModMain : MelonMod
    {
        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("Hello World!");
        }
    }
}