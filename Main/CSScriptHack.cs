using Harmony;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static Launcher.Helper;
namespace Launcher {
   internal static class CSScriptHack {
      private static ConstructorInfo HashtableC = AccessTools.Constructor(typeof(Hashtable), TypeL());
      private static MethodInfo PrecompileM = AccessTools.Method(
         typeof(csscript.Settings).Assembly.GetType("csscript.CSSUtils"), "Precompile");
      internal static object injectedObj = null;
      internal static void InjectObjectForPrecompilers(object obj) {
         injectedObj = obj;
         Launcher.Harmony.Patch(
            HashtableC, null,
            new HarmonyMethod(AccessTools.Method(typeof(CSScriptHack), nameof(HashtableCtor)))
         );
         var deh = new Hashtable();
      }
      internal static void HashtableCtor(Hashtable __instance) {
         var stackTrace = new StackTrace();
         var stackFrame = stackTrace.GetFrames()[2];
         var method = stackFrame.GetMethod();
         if (method == PrecompileM) {
            __instance.Add("TargetInfo", injectedObj);
            Launcher.Harmony.Unpatch(HashtableC, HarmonyPatchType.All, Launcher.HarmonyId);
         }
      }
   }
}
