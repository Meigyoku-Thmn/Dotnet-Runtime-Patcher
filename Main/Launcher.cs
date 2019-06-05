using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Drawing;
using Harmony;
using System.Collections.Generic;

namespace Launcher {
   public partial class Launcher {
      static public HarmonyInstance Harmony { get; private set; }
      static public Assembly TargetAssembly { get; private set; }
      static public IReadOnlyDictionary<string, Assembly> ReferenceAssemblies { get; private set; }
      static public Icon TargetIcon { get; private set; }
      static public string TargetVersion { get; private set; }
      static void Main(string[] args) {
         AppDomain.CurrentDomain.AssemblyResolve += ResolveEventHandler;
         AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(Unhandled);
         Console.OutputEncoding = Encoding.Unicode;
         Console.InputEncoding = Encoding.Unicode;
         Harmony = HarmonyInstance.Create("THFDF_HACK_VIETNAMESE");
         var referenceAssemblies = TargetAssembly.GetReferencedAssemblies().ToArray();
         ReferenceAssemblies = referenceAssemblies.Aggregate(
            new SortedDictionary<string, Assembly>(),
            (acc, e) => {
               acc.Add(e.Name, Assembly.Load(e));
               return acc;
            }
         );
      }
      private static void Unhandled(object sender, UnhandledExceptionEventArgs args) {
         StreamWriter streamWriter = new StreamWriter("Error.txt");
         DateTime now = DateTime.Now;
         streamWriter.Write("[" + now.Hour.ToString("00") + ":" + now.Minute.ToString("00") + ":" + now.Second.ToString("00") + "]\n" + args.ExceptionObject.ToString());
         streamWriter.Close();
         MessageBox.Show(args.ExceptionObject.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
      }
      private static Assembly ResolveEventHandler(object sender, ResolveEventArgs args) {
         var assemblies = AppDomain.CurrentDomain.GetAssemblies();
         Assembly result = assemblies.Where(a => args.Name.Equals(a.FullName)).FirstOrDefault();
         return result;
      }
   }
}
