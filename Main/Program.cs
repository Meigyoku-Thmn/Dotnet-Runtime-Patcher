﻿using CSScriptLibrary;
using CSScriptNativeApi;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using static RuntimePatcher.Helper;

namespace RuntimePatcher {
   using PatchTuple = PatchInfo;
   using TranspilerTuple = PatchInfo;
   public class PatchInfo {
      public MethodBase original;
      public MethodInfo patchMethod;
      public MethodInfo reentrantMethod;
      public static PatchInfo PM(MethodBase original, MethodInfo patchMethod, MethodInfo reentrantMethod = null) {
         return new PatchInfo(original, patchMethod, reentrantMethod);
      }
      public PatchInfo(MethodBase original, MethodInfo patchMethod, MethodInfo reentrantMethod) {
         this.original = original;
         this.patchMethod = patchMethod;
         this.reentrantMethod = reentrantMethod;
      }
   }
   [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
   public class Options {
      public bool UpdateOnStart { get; set; } = true;
      public void Save() {
         var json = JsonConvert.SerializeObject(this, Formatting.Indented);
         File.WriteAllText(Path.Combine(RootDirectory, FileName), json);
      }
      private string RootDirectory;
      private string FileName;
      public Options(string RootDirectory, string OptionsFileName) {
         this.RootDirectory = RootDirectory;
         this.FileName = OptionsFileName;
         try {
            var json = File.ReadAllText(Path.Combine(RootDirectory, OptionsFileName));
            JsonConvert.PopulateObject(json, this);
         }
         catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException) {
         }
      }
   }
   public partial class Launcher {
      public const uint PROCESS_CALLBACK_FILTER_ENABLED = 0x1;
      [DllImport("Kernel32.dll")]
      public static extern bool SetProcessUserModeExceptionPolicy(uint dwFlags);
      [DllImport("Kernel32.dll")]
      public static extern bool GetProcessUserModeExceptionPolicy(out uint lpFlags);
      public static void DisableUMCallbackFilter() {
         try {
            uint flags;
            GetProcessUserModeExceptionPolicy(out flags);
            flags &= ~PROCESS_CALLBACK_FILTER_ENABLED;
            SetProcessUserModeExceptionPolicy(flags);
         }
         catch { }
      }
      static public readonly string HarmonyId = "HARMONY_RUNTIME_PATCHER_INSTANCE";
      static public readonly Harmony HarmonyInst = new Harmony(HarmonyId);
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
         Helper.GetInputOptions(args);
         DisableUMCallbackFilter();
         Application.EnableVisualStyles();
         Application.SetCompatibleTextRenderingDefault(false);
         if (args.Length == 0) {
            MessageBox.Show("Bạn phải cung cấp tên profile vào đối số cho Launcher này!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
         }
         CurrentVersion = Application.ProductVersion;
         AppDomain.CurrentDomain.AssemblyResolve += ResolveEventHandler;
         Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
         AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(Unhandled);
         RootDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
         if (Settings.InputOptions?.LogPath == null)
            log = StreamWriter.Null; // use a blackhole as log file
         else
            log = new StreamWriter(File.Open(Path.Combine(RootDirectory, Settings.InputOptions.LogPath), FileMode.Create, FileAccess.Write, FileShare.Read), Encoding.UTF8);
         PackageDirectory = Path.Combine(RootDirectory, "Pkg");
         ProfileDirectory = Path.Combine(RootDirectory, "Prfl");
         DateTime now = DateTime.Now;
         log.Log(now.ToString("F", new CultureInfo("en-US")));
         log.Log("Dotnet Runtime Patcher by Meigyoku Thmn");
         log.Log($"Version {CurrentVersion}");
         var DebugBuild = Settings.InputOptions.Debug;
         var pd = new ProgressDialog(IntPtr.Zero);
         pd.Title = "Đang tải dữ liệu";
         pd.Maximum = 100;
         pd.Value = 0;
         pd.Line1 = "Thiết đặt CSScript";
         pd.Line2 = "Xin vui lòng chờ trong giây lát...";
         pd.Line3 = " ";
         pd.ShowDialog(ProgressDialog.PROGDLG.Modal, ProgressDialog.PROGDLG.NoMinimize);
         Thread.Sleep(5000);

         var options = new Options(RootDirectory, "Options.jsonc");
         options.Save();
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
#if DEBUG
         // for debugging purpose
         // mainScriptPath = @"C:\Touhou-Dotnet-Patches\lang-vi\thfdf\Main.cs";
#endif
         log.Log("Begin loading patching script...");
         log.Log($"DebugMode = {DebugBuild}");

         var mutex = new Mutex(false, ConfigurationManager.AppSettings["SharedLock"], out bool createdNew);
         var updater = options.UpdateOnStart && createdNew ? new Updater(targetPath, id, packageName, patchName, RootDirectory, PackageDirectory, options) : null;
         if (!createdNew) log.Log("An instance of Configurator or Updater is already running!");
         if (updater != null) log.Log("Run Updater...");
         var updatingTask = updater?.RunAsync();

         pd.Value = 10;
         pd.Line1 = "Biên dịch tệp script";

         log.Log($"Load {mainScriptPath}");
         CSScriptHack.InjectObjectForPrecompilers(new Hashtable() {
            { "Version", TargetVersion },
         });
         var package_dirs = new ScriptParser(mainScriptPath, null).ResolvePackages()
                                                .Select(Path.GetDirectoryName)
                                                .ToList();
         package_dirs.ForEach(CSScript.GlobalSettings.AddSearchDir);
         var refAsms = ReferenceAssemblies.Select(asm => asm.Value.Location).ToArray();
         refAsms = new string[0];
         var script = new AsmHelper(CSScript.LoadFile(
            mainScriptPath, null, DebugBuild, refAsms));
         Directory.SetCurrentDirectory(TargetDirectory);

         pd.Value = 20;
         pd.CloseDialog();
         log.Log("Call DotnetPatching.Config.OnInit");
         dynamic status = script.GetStaticMethod("DotnetPatching.Config.OnInit")();
         if (status == false) goto EnableUpdateButtonStep;
         pd = new ProgressDialog(IntPtr.Zero);
         pd.Title = "Đang tải dữ liệu";
         pd.Maximum = 100;
         pd.Value = 0;
         pd.Line1 = "Thiết đặt Harmony";
         pd.Line2 = "Xin vui lòng chờ trong giây lát...";
         pd.Line3 = " ";
         pd.ShowDialog(ProgressDialog.PROGDLG.Modal, ProgressDialog.PROGDLG.NoMinimize);
         pd.Value = 30;
         log.Log("Call DotnetPatching.Detours.OnSetup");
         var OnSetup = script.GetStaticMethod("DotnetPatching.Detours.OnSetup");
         var detourList = (List<PatchTuple>)OnSetup();
         log.Log("Call DotnetPatching.Transpilers.OnSetup");
         OnSetup = script.GetStaticMethod("DotnetPatching.Transpilers.OnSetup");
         var transpilerList = (List<TranspilerTuple>)OnSetup();
         var total = detourList.Count + transpilerList.Count;
         log.Log("Begin setting up Detour Functions...");
         pd.Line2 = "Setting up Detour Functions";
         SetupHook(detourList, (count, message) => {
            pd.Line3 = message;
            pd.Value = (uint)(30 + 70 * count / total);
         });
         log.Log("Begin setting up Transpilers to modify functions...");
         SetupTranspiler(transpilerList, (count, message) => {
            pd.Line3 = message;
            pd.Value = (uint)(30 + 70 * count / total);
         });
         log.Log("Launch the target executable...");
         pd.Value = 100;
         pd.CloseDialog();
         TargetAssembly.EntryPoint.Invoke(null, new object[] { new string[] { } });
EnableUpdateButtonStep:
         log.Log("Enable updating.");

         if (updatingTask != null) {
            updater.EnableUpdateButton();
            log.Log("Wait for updater to complete.");
            updatingTask.Wait();
            updater.Close();
         }
         mutex.Dispose();

         log.Log("Dotnet Runtime Patcher main thread ends.");
         log.Close();
      }
      public class HMState {
         public HMState() { }
      }
      internal static List<HMState> states = new List<HMState>();
      public delegate bool PrefixDelegate(ref HMState __state);
      static DynamicMethod PrefixFactory(MethodBase method) {
         states.Add(new HMState());
         var dynMethod = new DynamicMethod(
            method.Name + "_Prefix", typeof(bool), new[] { typeof(HMState).MakeByRefType() },
            typeof(Launcher).Module
         );
         dynMethod.DefineParameter(1, ParameterAttributes.None, "__state");
         ILGenerator gen = dynMethod.GetILGenerator();
         var statesField = AccessTools.Field(typeof(Launcher), nameof(states));
         // [__state = Launcher.states[LoadConstant(__incrementNumber)]]
         gen.Emit(OpCodes.Ldarg_0); // load __state param
         gen.Emit(OpCodes.Ldsfld, statesField); // load states list
         gen.Emit(OpCodes.Ldc_I4, states.Count - 1); // load the last index of states list
         gen.Emit(OpCodes.Callvirt, AccessTools.Method(statesField.FieldType, "get_Item")); // get the last element of states list, after this, the stack looks like: [..., __state, <last elem>]
         gen.Emit(OpCodes.Stind_Ref); // __state = <last elem>, then pop both of them
         // [return false]
         gen.Emit(OpCodes.Ldc_I4_0);
         gen.Emit(OpCodes.Ret);
         return dynMethod;
      }
      static void SetupHook(List<PatchTuple> PatchMethods, Action<int, string> reportFunc = null) {
         int count = 0;
         foreach (var config in PatchMethods) {
            var original = config.original;
            var prefix = AccessTools.Method(typeof(Launcher), nameof(PrefixFactory));
            var postfix = config.patchMethod;
            if (postfix == null) throw new Exception($"Postfix for method {original.DeclaringType?.FullName + '.' + original.Name} is null!");
            var message = $"Hook method {original.DeclaringType?.FullName + '.' + original.Name}";
            log.Log(message);
            reportFunc?.Invoke(count, message);
            HarmonyInst.Patch(original, new HarmonyMethod(prefix), new HarmonyMethod(postfix));
            if (config.reentrantMethod != null)
               new ReversePatcher(HarmonyInst, original, config.reentrantMethod).Patch();
            count++;
         }
         reportFunc?.Invoke(count, null);
      }
      static void SetupTranspiler(List<TranspilerTuple> allTranspilerConfigs, Action<int, string> reportFunc = null) {
         var count = 0;
         foreach (var config in allTranspilerConfigs) {
            var original = config.original;
            var transpiler = config.patchMethod;
            if (transpiler == null) throw new Exception($"Transpiler for method {original.DeclaringType?.FullName + '.' + original.Name} is null!");
            var message = $"Transpile method {original.DeclaringType?.FullName + '.' + original.Name}";
            log.Log(message);
            reportFunc?.Invoke(count, message);
            HarmonyInst.Patch(original, null, null, new HarmonyMethod(transpiler));
            if (config.reentrantMethod != null)
               new ReversePatcher(HarmonyInst, original, config.reentrantMethod).Patch();
            count++;
         }
         reportFunc?.Invoke(count, null);
      }
      private static void Unhandled(object sender, UnhandledExceptionEventArgs args) {
#if DEBUG
         log.Log(args.ExceptionObject.ToString());
#else
         log.Log(args.ExceptionObject.ToString(), true);
         Console.WriteLine((args.ExceptionObject as Exception).Message);
#endif
         log.Close();
         MessageBox.Show(args.ExceptionObject.ToString(), "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Hand);
         // game over
      }
      private static Assembly ResolveEventHandler(object sender, ResolveEventArgs args) {
         var assemblies = AppDomain.CurrentDomain.GetAssemblies();
         Assembly result = assemblies.Where(a => args.Name.Equals(a.FullName)).FirstOrDefault();
         return result;
      }
   }
}
