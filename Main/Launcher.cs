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
using Newtonsoft.Json;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using static Launcher.Helper;
using CSScriptLibrary;
using CSScriptNativeApi;
using System.Reflection.Emit;
using Sigil;

namespace Launcher {
   using PatchTuple = ValueTuple<Type, string, Type[], string>;
   using TranspilerTuple = ValueTuple<Type, string, Type[], string>;
   public partial class Launcher {
      static public HarmonyInstance Harmony { get; private set; }
      static public Assembly TargetAssembly { get; private set; }
      static public IReadOnlyDictionary<string, Assembly> ReferenceAssemblies { get; private set; }
      static public Icon TargetIcon { get; private set; }
      static public string TargetVersion { get; private set; }
      static public string TargetDirectory { get; private set; }
      static public string RootDirectory { get; private set; }
      static void Main(string[] args) {
         AppDomain.CurrentDomain.AssemblyResolve += ResolveEventHandler;
         AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(Unhandled);
         Console.OutputEncoding = Encoding.Unicode;
         Console.InputEncoding = Encoding.Unicode;
         RootDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

         var exeCfg = JObject.Parse(File.ReadAllText(Path.Combine(RootDirectory, args[0] + ".jsonc")));
         var targetPath = (string)exeCfg["targetPath"];
         if (targetPath == null) {
            MessageBox.Show("Đường dẫn đến 'file thực thi' không tồn tại trong thiết lập!"); return;
         }
         TargetDirectory = Path.GetDirectoryName(targetPath);
         var package = (string)exeCfg["package"];
         if (package == null) {
            MessageBox.Show("Không hề có thiết lập gói!"); return;
         }
         var versionsCfg = JObject.Parse(File.ReadAllText(Path.Combine(RootDirectory, package)));
         var targetInfo = GetChecksumAndSize(targetPath);
         var version = versionsCfg[targetInfo.Hash];
         if (version == null || (uint)version["size"] != targetInfo.Size) {
            MessageBox.Show("Không tìm thấy bản patch phù hợp, có thể phần mềm của bạn đang là phiên bản khác rồi!"); return;
         }
         TargetVersion = (string)version["ver"];
         var patchName = (string)version["name"];
         TargetIcon = Icon.ExtractAssociatedIcon(targetPath);
         TargetAssembly = Assembly.LoadFrom(targetPath);
         var referenceAssemblies = TargetAssembly.GetReferencedAssemblies().ToArray();
         ReferenceAssemblies = referenceAssemblies.Aggregate(
            new SortedDictionary<string, Assembly>(),
            (acc, e) => {
               acc.Add(e.Name, Assembly.Load(e));
               return acc;
            }
         );
         Harmony = HarmonyInstance.Create("THFDF_HACK_VIETNAMESE");
         var DebugBuild = true;
         CSScript.GlobalSettings.UseAlternativeCompiler = CodeDom_Roslyn.LocateRoslynCSSProvider();
         CSScript.GlobalSettings.RoslynDir = CodeDom_Roslyn.LocateRoslynCompilers();
         CSScript.EvaluatorConfig.DebugBuild = DebugBuild;

         var mainScriptPath = Path.Combine(RootDirectory, package, patchName, "Main.cs");
         var script = new AsmHelper(CSScript.LoadFile(mainScriptPath, null, DebugBuild));
         Directory.SetCurrentDirectory(TargetDirectory);
         script.GetStaticMethod("DotnetPatching.Config.OnInit")();
         var detourList = (List<PatchTuple>)script.GetStaticMethod("DotnetPatching.Detours.OnSetup")();
         var transpilerList = (List<TranspilerTuple>)script.GetStaticMethod("DotnetPatching.Transpiler.OnSetup")();
         SetupHook(detourList);
         SetupTranspiler(transpilerList);

         TargetAssembly.EntryPoint.Invoke(null, new object[] { });
      }
      static dynamic __currentReentrantMethod;
      static List<dynamic> states = new List<dynamic>();
      public delegate bool PrefixDelegate(ref dynamic __state);
      static DynamicMethod PrefixFactory(MethodBase method) {
         states.Add(__currentReentrantMethod);
         var emit = Emit<PrefixDelegate>.NewDynamicMethod(method.Name + "_Prefix");
         var statesField = AccessTools.Field(typeof(Launcher), nameof(states));
         // [__state = Launcher.states[LoadConstant(__incrementNumber)]]
         emit.LoadArgument(0);
         emit.LoadField(statesField);
         emit.LoadConstant(states.Count - 1);
         emit.CallVirtual(AccessTools.Method(statesField.FieldType, "get_Item"));
         emit.StoreIndirect(typeof(object));
         // [return false]
         emit.LoadConstant(false);
         emit.Return();
         emit.CreateDelegate(); // Finalize
         var DynMethodPrpty = AccessTools.Property(emit.GetType(), "DynMethod");
         var dynMethod = (DynamicMethod)DynMethodPrpty.GetValue(emit);
         dynMethod.DefineParameter(1, ParameterAttributes.None, "__state");
         return dynMethod;
      }
      static void SetupHook(List<PatchTuple> PatchMethods) {
         foreach (var config in PatchMethods) {
            var original = config.Item2 != ".ctor" ?
               AccessTools.Method(config.Item1, config.Item2, config.Item3) :
               AccessTools.Constructor(config.Item1, config.Item3) as MethodBase;
            var prefix = AccessTools.Method(typeof(Launcher), nameof(PrefixFactory));
            var postfix = AccessTools.Method(typeof(Launcher), config.Item4);
            __currentReentrantMethod = Harmony.Patch(original).MakeDelegate();
            Harmony.Patch(original, new HarmonyMethod(prefix), new HarmonyMethod(postfix));
         }
      }
      static void SetupTranspiler(List<TranspilerTuple> allTranspilerConfigs) {
         foreach (var config in allTranspilerConfigs) {
            var original = config.Item2 != ".ctor" ?
               AccessTools.Method(config.Item1, config.Item2, config.Item3) :
               AccessTools.Constructor(config.Item1, config.Item3) as MethodBase;
            var transpiler = AccessTools.Method(typeof(Launcher), config.Item4);
            Harmony.Patch(original, null, null, new HarmonyMethod(transpiler));
         }
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
