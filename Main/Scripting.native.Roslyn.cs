using System;
using System.IO;
using System.Reflection;

// The VB.NET samples can be found here: https://github.com/oleg-shilo/cs-script/tree/master/Source/NuGet/content/vb

namespace CSScriptNativeApi {
   public class CodeDom_Roslyn {
      static string Root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
      static string Lib = Path.Combine(Root, "Lib");
      public static string LocateRoslynCompilers() {
#if DEBUG
         return Path.GetFullPath(
            Environment.ExpandEnvironmentVariables(
               @"%USERPROFILE%\.nuget\packages\microsoft.net.compilers\2.2.0\tools"));
#else
         return Path.Combine(Root, "RoslynCompiler");
#endif
      }
      public static string LocateRoslynCSSProvider() {
#if DEBUG
         return Path.Combine(Root, "CSSRoslynProvider.dll");
#else         
         return Path.Combine(Lib, "CSSRoslynProvider.dll");
#endif
      }
   }
}