using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CSScriptLibrary;

// The VB.NET samples can be found here: https://github.com/oleg-shilo/cs-script/tree/master/Source/NuGet/content/vb

namespace CSScriptNativeApi {
   public class CodeDom_Roslyn {
      static string Root { get { return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); } }
      public static string LocateRoslynCompilers() {
         return @"C:\Home\Programming\Self\Game-Visual Novel Translation\Touhou Fantastic Danmaku Festival\Dotnet-Runtime-Patcher\packages\Microsoft.Net.Compilers.2.2.0\tools";
      }
      public static string LocateRoslynCSSProvider() {
         return Path.Combine(Root, "CSSRoslynProvider.dll");
      }
   }
}