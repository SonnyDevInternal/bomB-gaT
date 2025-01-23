#define DebugBuild

using UnityEngine;

namespace Assets.Scripts
{
    public static class DebugClass
    {
        public static void Log(object message)
        {
#if DebugBuild
            Debug.Log(message);
#endif
        }

        public static void Error(object message)
        {
#if DebugBuild
            Debug.LogError(message);
#endif
        }

        public static void Warning(object message)
        {
#if DebugBuild
            Debug.LogWarning(message);
#endif
        }
    }
}
