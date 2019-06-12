using System;
using System.Text;
using System.Windows.Forms;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Drawing;
using Harmony;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using static Launcher.Helper;
using CSScriptLibrary;
using CSScriptNativeApi;
using System.Reflection.Emit;
using Sigil;
using System.Threading;
using System.Globalization;
using System.Configuration;
using System.Collections;

namespace Launcher {
   using PatchTuple = ValueTuple<Type, string, Type[], Type, string, Type[]>;
   using TranspilerTuple = ValueTuple<Type, string, Type[], Type, string, Type[]>;
   public partial class Launcher {
      static public HarmonyInstance Harmony { get; private set; }
      static public readonly string HarmonyId = "HARMONY_RUNTIME_PATCHER_INSTANCE";
      static public Assembly TargetAssembly { get; private set; }
      static public IReadOnlyDictionary<string, Assembly> ReferenceAssemblies { get; private set; }
      static public Icon TargetIcon { get; private set; }
      static public string TargetVersion { get; private set; }
      static public string TargetDirectory { get; private set; }
      static public string RootDirectory { get; private set; }
      static public string ProfileDirectory { get; private set; }
      static public string PackageDirectory { get; private set; }
      static public string CurrentVersion { get; private set; }
      static internal StreamWriter log;
      static readonly string LogFilePath = ConfigurationManager.AppSettings["LogFilePath"];
      static void Main(string[] args) {
         Application.EnableVisualStyles();
         Application.SetCompatibleTextRenderingDefault(false);
         if (args.Length == 0) {
            MessageBox.Show("Bạn phải cung cấp tên profile vào đối số cho Launcher này!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
         }
         CurrentVersion = Application.ProductVersion;
         AppDomain.CurrentDomain.AssemblyResolve += ResolveEventHandler;
         AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(Unhandled);
         Console.OutputEncoding = Encoding.Unicode;
         Console.InputEncoding = Encoding.Unicode;
         RootDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
         log = new StreamWriter(File.Open(Path.Combine(RootDirectory, LogFilePath), FileMode.Create, FileAccess.Write, FileShare.Read), Encoding.UTF8);
         PackageDirectory = Path.Combine(RootDirectory, "Pkg");
         ProfileDirectory = Path.Combine(RootDirectory, "Prfl");
         DateTime now = DateTime.Now;
         log.Log(now.ToString("F", new CultureInfo("en-US")));
         log.Log("Dotnet Runtime Patcher by Meigyoku Thmn");
         log.Log($"Version {CurrentVersion}");

         log.Log("Create Harmony Instance.");
         Harmony = HarmonyInstance.Create(HarmonyId);
#if DEBUG
         var DebugBuild = true;
#else
         var DebugBuild = false;
#endif
         log.Log("Set up CSScript.");
         CSScript.GlobalSettings.UseAlternativeCompiler = CodeDom_Roslyn.LocateRoslynCSSProvider();
         CSScript.GlobalSettings.RoslynDir = CodeDom_Roslyn.LocateRoslynCompilers();
         CSScript.EvaluatorConfig.DebugBuild = DebugBuild;

         log.Log($"Read {Path.Combine(ProfileDirectory, args[0] + ".jsonc")}");
         var exeCfg = JObject.Parse(File.ReadAllText(Path.Combine(ProfileDirectory, args[0] + ".jsonc")));
         var targetPath = (string)exeCfg["targetPath"];
         if (targetPath == null) {
            log.Log($"Target path doesn't exist in profile!");
            MessageBox.Show("Đường dẫn đến 'file thực thi' không tồn tại trong thiết lập!"); return;
         }
         TargetDirectory = Path.GetDirectoryName(targetPath);
         var package = (string)exeCfg["package"];
         if (package == null) {
            log.Log($"Package name in profile is missing!");
            MessageBox.Show("Không hề có thiết lập gói!"); return;
         }
         log.Log($"Read {Path.Combine(PackageDirectory, package, "versions.jsonc")}");
         var versionsCfg = JObject.Parse(File.ReadAllText(Path.Combine(PackageDirectory, package, "versions.jsonc")));
         var targetInfo = GetChecksumAndSize(targetPath);
         var version = versionsCfg[targetInfo.Hash];
         if (version == null || (uint)version["size"] != targetInfo.Size) {
            log.Log("Unable to find suitable patch for target executable file.");
            MessageBox.Show("Không tìm thấy bản patch phù hợp, có thể phần mềm của bạn đang là phiên bản khác rồi!"); return;
         }
         TargetVersion = (string)version["ver"];
         if (TargetVersion == null)
            log.Log("TargetVersion is null");
         var patchName = (string)version["name"];
         if (patchName == null)
            log.Log("patchName is null");
         TargetIcon = Icon.ExtractAssociatedIcon(targetPath);
         TargetAssembly = Assembly.LoadFrom(targetPath);
         var referenceAssemblies = TargetAssembly.GetReferencedAssemblies();
         ReferenceAssemblies = referenceAssemblies.Aggregate(
            new SortedDictionary<string, Assembly>(),
            (acc, e) => {
               acc.Add(e.Name, Assembly.Load(e));
               return acc;
            }
         );

         var tmp = package.Split('/');
         var id = tmp[0];
         var packageName = tmp[1];
         var mainScriptPath = Path.Combine(PackageDirectory, package, patchName, "Main.cs");
         log.Log("Begin loading patching script...");
         log.Log($"DebugMode = {DebugBuild}");
         log.Log($"Load {mainScriptPath}");
         CSScriptHack.InjectObjectForPrecompilers(new Hashtable() {
            { "Version", TargetVersion },
         });
         var script = new AsmHelper(CSScript.LoadFile(mainScriptPath, null, DebugBuild));
         Directory.SetCurrentDirectory(TargetDirectory);
         log.Log("Call DotnetPatching.Config.OnInit");
         script.GetStaticMethod("DotnetPatching.Config.OnInit")();
         log.Log("Call DotnetPatching.Detours.OnSetup");
         var OnSetup = script.GetStaticMethod("DotnetPatching.Detours.OnSetup");
         var detourList = (List<PatchTuple>)OnSetup();
         log.Log("Call DotnetPatching.Transpilers.OnSetup");
         OnSetup = script.GetStaticMethod("DotnetPatching.Transpilers.OnSetup");
         var transpilerList = (List<TranspilerTuple>)OnSetup();
         log.Log("Begin setting up Detour Functions...");
         SetupHook(detourList);
         log.Log("Begin setting up Transpilers to modify functions...");
         SetupTranspiler(transpilerList);
         log.Log("Run Updater...");
         var updateInfo = RunUpdater(id, packageName, patchName);
         log.Log("Launch the target executable...");
         TargetAssembly.EntryPoint.Invoke(null, new object[] { new string[] { } });
         log.Log("Enable updating.");
         updateInfo.mres.Wait(); // wait for UpdaterForm to fully loaded 
         try {
            if (!updateInfo.form.IsDisposed) updateInfo.form.Invoke(new Action(() => {
               updateInfo.form.EnableUpdateButton();
            }));
         }
         catch (InvalidOperationException) {
            if (updateInfo.form.IsHandleCreated) throw;
         }
         log.Log("Wait for updater to complete.");
         updateInfo.thread.Join();
         log.Log("Dotnet Runtime Patcher main thread ends.");
         log.Close();
      }
      static Delegate __currentReentrantMethod;
      public class HMState {
         public Delegate ReentrantDelegate;
         public HMState(Delegate ReentrantDelegate) {
            this.ReentrantDelegate = ReentrantDelegate;
         }
      }
      static List<HMState> states = new List<HMState>();
      public delegate bool PrefixDelegate(ref HMState __state);
      static DynamicMethod PrefixFactory(MethodBase method) {
         states.Add(new HMState(__currentReentrantMethod));
         var emit = Emit<PrefixDelegate>.NewDynamicMethod(method.Name + "_Prefix");
         var statesField = AccessTools.Field(typeof(Launcher), nameof(states));
         // [__state = Launcher.states[LoadConstant(__incrementNumber)]]
         emit.LoadArgument(0); // load __state param
         emit.LoadField(statesField); // load states list
         emit.LoadConstant(states.Count - 1); // load the last index of states list
         emit.CallVirtual(AccessTools.Method(statesField.FieldType, "get_Item")); // get the last element of states list, after this, the stack looks like: [..., __state, <last elem>]
         emit.StoreIndirect(typeof(HMState)); // __state = <last elem>, then pop both of them
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
            var postfix = AccessTools.Method(config.Item4, config.Item5, config.Item6);
            __currentReentrantMethod = Harmony.Patch(original).MakeDelegate();
            log.Log($"Hook method {original.DeclaringType?.FullName + '.' + original.Name}");
            Harmony.Patch(original, new HarmonyMethod(prefix), new HarmonyMethod(postfix));
         }
      }
      static void SetupTranspiler(List<TranspilerTuple> allTranspilerConfigs) {
         foreach (var config in allTranspilerConfigs) {
            var original = config.Item2 != ".ctor" ?
               AccessTools.Method(config.Item1, config.Item2, config.Item3) :
               AccessTools.Constructor(config.Item1, config.Item3) as MethodBase;
            var transpiler = AccessTools.Method(config.Item4, config.Item5, config.Item6);
            log.Log($"Transpile method {original.DeclaringType?.FullName + '.' + original.Name}");
            Harmony.Patch(original, null, null, new HarmonyMethod(transpiler));
         }
      }
      private static void Unhandled(object sender, UnhandledExceptionEventArgs args) {
         log.Log(args.ExceptionObject.ToString());
         log.Close();
         MessageBox.Show(args.ExceptionObject.ToString(), "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Hand);
         // game over
      }
      private static Assembly ResolveEventHandler(object sender, ResolveEventArgs args) {
         var assemblies = AppDomain.CurrentDomain.GetAssemblies();
         Assembly result = assemblies.Where(a => args.Name.Equals(a.FullName)).FirstOrDefault();

         return result;
      }
      static UpdaterInfo RunUpdater(string id, string packageName, string patchName) {
         var mres = new ManualResetEventSlim(false);
         var updateForm = new UpdateForm(id, packageName, patchName, mres);
         Thread t = new Thread(() => Application.Run(updateForm));
         t.Start();
         return new UpdaterInfo(updateForm, t, mres);
      }
      class UpdaterInfo {
         public UpdateForm form;
         public Thread thread;
         public ManualResetEventSlim mres;
         public UpdaterInfo(UpdateForm form, Thread thread, ManualResetEventSlim mres) {
            this.form = form; this.thread = thread; this.mres = mres;
         }
      }
   }
}
