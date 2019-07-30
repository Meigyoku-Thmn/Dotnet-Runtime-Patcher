using HarmonyLib;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using static RuntimePatcher.Helper;
namespace RuntimePatcher {
   internal static class CSScriptHack {
      private static ConstructorInfo HashtableC = AccessTools.Constructor(typeof(Hashtable), TypeL());
      private static MethodInfo PrecompileM = AccessTools.Method(
         typeof(csscript.Settings).Assembly.GetType("csscript.CSSUtils"), "Precompile");
      internal static object injectedObj = null;
      internal static void InjectObjectForPrecompilers(object obj) {
         injectedObj = obj;
         Launcher.HarmonyInst.Patch(
            HashtableC, null,
            new HarmonyMethod(AccessTools.Method(typeof(CSScriptHack), nameof(HashtableCtor)))
         );
         var deh = new Hashtable();
      }
      internal static void HashtableCtor(Hashtable __instance) {
         var stackTrace = new StackTrace(false);
         var stackFrame = stackTrace.GetFrames()[2];
         var method = stackFrame.GetMethod();
         if (method == PrecompileM) {
            __instance.Add("TargetInfo", injectedObj);
            Launcher.HarmonyInst.Unpatch(HashtableC, HarmonyPatchType.All, Launcher.HarmonyId);
         }
      }
   }
}
