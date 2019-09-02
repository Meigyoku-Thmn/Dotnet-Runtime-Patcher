using HarmonyLib;
using ShellProgressBar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Configurator {
   internal class ShellProgressBarHack {
      private static MethodInfo ExcerptM = AccessTools.Method(
         typeof(ProgressBarOptions).Assembly.GetType("ShellProgressBar.StringExtensions"), "Excerpt");
      private static Harmony HarmonyInst = new Harmony("CONFIGURATOR");
      internal static void PatchExcerptFunc() {
         HarmonyInst.Patch(
            ExcerptM, new HarmonyMethod(AccessTools.Method(typeof(ShellProgressBarHack), nameof(Excerpt)))
         );
      }
      private static bool Excerpt(string phrase, int length, ref string __result) {
         if (string.IsNullOrEmpty(phrase) || phrase.Length < length) {
            __result = phrase; return false;
         }
         var leftLength = (int)Math.Floor((length - 3) / 2f);
         var rightLength = (int)Math.Floor((length - 3) / 2f);
         __result = phrase.Substring(0, leftLength) + "..." + phrase.Substring(phrase.Length - rightLength);
         return false;
      }
   }
}
