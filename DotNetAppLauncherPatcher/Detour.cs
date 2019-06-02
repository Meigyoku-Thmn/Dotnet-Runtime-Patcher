using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using Harmony;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Sigil;

namespace Launcher {
   using System.Diagnostics;
   using static Helper;
   using PatchTuple = ValueTuple<Type, string, Type[], string>;
   public partial class Launcher {
      static List<PatchTuple> GetPatchMethods() {
         var WindowsGameWindowT = XnaGameAssembly.GetType("Microsoft.Xna.Framework.WindowsGameWindow");
         var TitleLocationT = XnaAssembly.GetType("Microsoft.Xna.Framework.TitleLocation");
         var SpriteFontXT = TargetAssembly.GetType("Microsoft.Xna.Framework.SpriteFontX");
         var IntT = typeof(int);
         var StringT = typeof(string);
         var MainT = TargetAssembly.GetType("THMHJ.Main");
         var CryT = TargetAssembly.GetType("THMHJ.Cry");
         var FontT = typeof(Font);
         var IGraphicsDeviceServiceT = XnaGraphicsAssembly.GetType("Microsoft.Xna.Framework.Graphics.IGraphicsDeviceService");
         var TextRenderingHintT = typeof(TextRenderingHint);
         var rs = new List<PatchTuple>() {
            // For replace game resource on the fly
            (CryT, "Decry", TypeL(StringT, IntT), nameof(Decry)),
            (CryT, "Decry", TypeL(StringT), nameof(Decry0)),
            // To change game title
            (WindowsGameWindowT, "SetTitle", default(Type[]), nameof(SetTitle)),
            // To Set Game Icon, since we use launcher method to hook code
            (WindowsGameWindowT, "GetDefaultIcon", default(Type[]), nameof(GetDefaultIcon)),
            // To set the game working directory for XNA
            (TitleLocationT, "get_Path", default(Type[]), nameof(TitleLocationPath)),
            // To set the game's fonts
            (SpriteFontXT, "Initialize", TypeL(FontT, IGraphicsDeviceServiceT, TextRenderingHintT), nameof(SpriteFontXInitialize)),
         };
         return rs;
      }
      [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
      public static extern bool SetWindowText(IntPtr hWnd, string lpString);
      static MethodInfo __currentReentrantMethod;
      static List<MethodInfo> states = new List<MethodInfo>();
      public delegate bool PrefixDelegate(ref MethodInfo __state);
      static DynamicMethod PrefixFactory(MethodBase method) {
         states.Add(__currentReentrantMethod);
         var emit = Emit<PrefixDelegate>.NewDynamicMethod(method.Name + "_Prefix");
         var statesField = AccessTools.Field(typeof(Launcher), nameof(states));
         // [__state = Launcher.states[LoadConstant(__incrementNumber)]]
         emit.LoadArgument(0);
         emit.LoadField(statesField);
         emit.LoadConstant(states.Count - 1);
         emit.CallVirtual(AccessTools.Method(statesField.FieldType, "get_Item"));
         emit.StoreIndirect(typeof(MethodInfo));
         // [return false]
         emit.LoadConstant(false);
         emit.Return();
         emit.CreateDelegate(); // Finalize
         var DynMethodPrpty = AccessTools.Property(emit.GetType(), "DynMethod");
         var dynMethod = (DynamicMethod)DynMethodPrpty.GetValue(emit);
         dynMethod.DefineParameter(1, ParameterAttributes.None, "__state");
         return dynMethod;
      }
      static void SetupHook() {
         var PatchMethods = GetPatchMethods();
         foreach (var patchMethod in PatchMethods) {
            var original = AccessTools.Method(patchMethod.Item1, patchMethod.Item2, patchMethod.Item3);
            var prefix = AccessTools.Method(typeof(Launcher), nameof(PrefixFactory));
            var postfix = AccessTools.Method(typeof(Launcher), patchMethod.Item4);
            __currentReentrantMethod = Harmony.Patch(original);
            Harmony.Patch(original, new HarmonyMethod(prefix), new HarmonyMethod(postfix));
         }
      }
      // ===========================================================
      static Regex ignoredFiles = new Regex(
         @"^(:?(?:4\.xna)|(?:5\.xna)|(?:8\.xna)|(?:.*?\.dat))$", RegexOptions.Compiled);
      public static string MapResourcePath(string path) {
         path = path.Replace('/', '\\');
         var fileName = Path.GetFileNameWithoutExtension(path);
         var fileExt = Path.GetExtension(path).ToLower();
         var newExt = default(string);
         var graphicsRootPath = @"Content\Graphics";
         var dataRootPath = @"Content\Data";
         if (path.IndexOf(dataRootPath, 0, dataRootPath.Length) == 0 && !ignoredFiles.IsMatch(fileName + fileExt))
            newExt = ".txt";
         else if (fileExt == ".dat" || path.IndexOf(graphicsRootPath, 0, graphicsRootPath.Length) == 0)
            newExt = ".png";
         if (newExt != null) path = Path.Combine(Path.GetDirectoryName(path), fileName + newExt);
         return path;
      }
      static bool IsTextSet = false;
      public static void SetTitle(object __instance, Form ___mainForm, string title) {
         if (IsTextSet == true) return;
         var version = AccessTools.Field(TargetAssembly.GetType("THMHJ.Main"), "version").GetValue(null);
         SetWindowText(___mainForm.Handle, $"Đông Phương Mạc Hoa Tế ～ Touhou Fantastic Danmaku Festival | Version {version} | Bản Việt hóa Version 1.0");
         IsTextSet = true;
      }
      public static void GetDefaultIcon(ref Icon __result) {
         __result = TargetIcon;
      }
      public static void TitleLocationPath(ref string __result) {
         __result = Path.GetDirectoryName(TargetAssembly.Location);
      }
      public static dynamic ReentrantDecry;
      public static void Decry(ref string FileName, ref int type, ref Stream __result, MethodInfo __state) {
         if (ReentrantDecry == null) ReentrantDecry = CreateDelegate(__state);
         var modeFilePath = MapResourcePath(FileName);
         var newFileName = Path.Combine(DebugResourceWorkingPath, modeFilePath);
         var message = "Load debug mod: ";
         var isNotSameFolder = ResourceWorkingPath != DebugResourceWorkingPath;
         if (isNotSameFolder && !File.Exists(newFileName)) {
            message = "Load mod: ";
            newFileName = Path.Combine(ResourceWorkingPath, modeFilePath);
         }
         if (isNotSameFolder == false) message = "Load mod: ";
         if (!File.Exists(newFileName))
            __result = ReentrantDecry((dynamic)FileName, (dynamic)type);
         else {
            Console.WriteLine(message + FileName);
            __result = new MemoryStream(File.ReadAllBytes(newFileName));
         }
      }
      public static dynamic ReentrantDecry0;
      public static void Decry0(ref string FileName, ref Stream __result, MethodInfo __state) {
         if (ReentrantDecry0 == null) ReentrantDecry0 = CreateDelegate(__state);
         var modeFilePath = MapResourcePath(FileName);
         var newFileName = Path.Combine(DebugResourceWorkingPath, modeFilePath);
         var message = "Load debug mod: ";
         var isNotSameFolder = ResourceWorkingPath != DebugResourceWorkingPath;
         if (isNotSameFolder && !File.Exists(newFileName)) {
            message = "Load mod: ";
            newFileName = Path.Combine(ResourceWorkingPath, modeFilePath);
         }
         if (isNotSameFolder == false) message = "Load mod: ";
         if (!File.Exists(newFileName))
            __result = ReentrantDecry0((dynamic)FileName);
         else {
            Console.WriteLine(message + FileName);
            __result = new MemoryStream(File.ReadAllBytes(newFileName));
         }
      }
      public static dynamic SFXInitialize;
      public static void SpriteFontXInitialize(object __instance, Font font, object gds, TextRenderingHint trh, MethodInfo __state) {
         if (SFXInitialize == null) SFXInitialize = CreateDelegate(__state);
         if (font.OriginalFontName == "Cambria")
            font = new Font("Arial", font.Size, font.Style, font.Unit);
         SFXInitialize((dynamic)__instance, (dynamic)font, (dynamic)gds, (dynamic)trh);
      }
   }
}
