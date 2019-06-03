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

// Đây là cách "Inject" đơn giản nhất: coi target như là 1 class library, dùng khi muốn chạy code của ta trước khi target kịp chạy
namespace Launcher {
   using static Helper;
   public partial class Launcher {
      [DllImport("steam_api.dll", SetLastError = true)]
      public static extern bool SteamAPI_Init();
      enum Version {
         V1_05_EN, V1_05_SC_CN, V1_05_TC_CN, V1_05_JP,
      }
      static Dictionary<string, Version> VersionMap = new Dictionary<string, Version>() {
         { "v1.05en", Version.V1_05_EN },
         { "v1.05jp", Version.V1_05_JP },
         { "v1.05sc-cn", Version.V1_05_SC_CN },
         { "v1.05tc-cn", Version.V1_05_TC_CN },
      };
      static HarmonyInstance Harmony;
      static Assembly TargetAssembly;
      static Assembly XnaAssembly;
      static Assembly XnaGameAssembly;
      static Assembly XnaGraphicsAssembly;
      static Icon TargetIcon;
      static readonly string ConfigFileName = "Config.xml";
      static string THFDF_SteamID;
      static string TargetPath;
      static string ResourceWorkingPath;
      static string DebugResourceWorkingPath;
      static Version Ver;
      static void LoadConfigAndValidateExe() {
         var config = XElement.Load("Config.xml", LoadOptions.SetLineInfo);
         THFDF_SteamID = config.Element("steam")?.Attribute("game-id")?.Value;
         if (THFDF_SteamID == null) throw new Exception($"Steam game id is missing in {ConfigFileName}!");
         TargetPath = config.Element("target")?.Attribute("path")?.Value;
         if (TargetPath == null) throw new Exception($"Target path is missing in {ConfigFileName}!");
         ResourceWorkingPath = config.Element("resource")?.Attribute("path")?.Value;
         if (ResourceWorkingPath == null) throw new Exception($"Resource directory path is missing in {ConfigFileName}!");
         DebugResourceWorkingPath = config.Element("resource")?.Attribute("debug-path")?.Value;
         if (DebugResourceWorkingPath == null) DebugResourceWorkingPath = ResourceWorkingPath;
         var targetInfo = GetChecksumAndSize(TargetPath);
         var versions = config.Element("versions")?.Elements() ?? new XElement[0];
         var matchedVersion = versions
            .Where(version => {
               long size; if (!long.TryParse(version.Attribute("size")?.Value, out size)) size = -1;
               return version.Attribute("checksum")?.Value == (string)targetInfo.Hash &&
                  size == (long)targetInfo.Size;
            })
            .FirstOrDefault()
            ;
         if (matchedVersion == null) throw new Exception($"Unknown target, maybe you selected the wrong game, or the checksum and the size in {ConfigFileName} are incorrect!");
         var hasVer = VersionMap.TryGetValue(matchedVersion.Name.LocalName, out Ver);
         if (!hasVer) throw new Exception($"Invalid version name: {matchedVersion.Name.LocalName}");
      }
      /* Demo hack dịch Touhou FDF đồng thời sửa bug text rendering của anh StarX (đã nhiều tháng trôi qua từ đầu năm 2019 nhưng anh ấy không sửa, có lẽ do bug này (đúng ra nên gọi là unexpected graphics behavior bởi vì bug này không gây crash) không gây khó chịu nhiều nếu anh hiển thị game bằng Tiếng Trung, tuy nhiên với một end-user sử dụng văn tự latin để ghi Tiếng Việt như tôi thì không thể bỏ qua được):
       * - Hook vào các điểm chủ chốt (2 hàm Decry) để tạo một hệ thống modding (file replacer), bypass luôn quá trình giải mã
       * - Sửa lỗi kích thước kí tự space quá dài
       * - Sửa lỗi SetTitle không hoạt động với kí tự Unicode (dấu ?), lỗi này là do Microsoft và XNA Framework
       * - Đổi font chữ (game dev làm ơn hạn chế dùng font Serif, các vị đang gây mù lòa cho game thủ bọn tôi)
       * - Sửa lỗi ngắt dòng ở màn hình Achievement (xuất phát điểm nó là 1 cơ chế ngắt dòng dành cho văn bản Tiếng Trung, khi dịch game này sang Tiếng Anh, anh StarX chỉ đơn giản là sửa data (thêm kí tự space) cho nó ngắt dòng "đúng", tôi fix cho triệt để luôn
       * - Cho layer nhân vật trong hội thoại đè lên UI frame (giống Touhou 13 trở đi)
      */
      static void Main(string[] args) {
         AppDomain.CurrentDomain.AssemblyResolve += ResolveEventHandler;
         AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(Unhandled);
         Console.OutputEncoding = Encoding.Unicode;
         LoadConfigAndValidateExe();
         Harmony = HarmonyInstance.Create("THFDF_HACK_VIETNAMESE");
         TargetIcon = Icon.ExtractAssociatedIcon(TargetPath);
         TargetAssembly = Assembly.LoadFrom(TargetPath);
         var referenceAssemblies = TargetAssembly.GetReferencedAssemblies().ToArray();
         XnaAssembly = Assembly.Load(referenceAssemblies.First(e => e.Name == "Microsoft.Xna.Framework"));
         XnaGameAssembly = Assembly.Load(referenceAssemblies.First(e => e.Name == "Microsoft.Xna.Framework.Game"));
         XnaGraphicsAssembly = Assembly.Load(referenceAssemblies.First(e => e.Name == "Microsoft.Xna.Framework.Graphics"));
         SetupHook();
         SetupTranspiler();
         var workingDirectory = Path.GetDirectoryName(TargetPath);
         Directory.SetCurrentDirectory(workingDirectory);
         // Tham khảo: https://github.com/thpatch/thcrap/blob/master/thcrap/src/steam.cpp
         Environment.SetEnvironmentVariable("SteamAppId", THFDF_SteamID); // prevent the game from restarting itself
         // enable steam api, for games that don't load it from the start, this gives us steam overlay even when launching from outside of steam
         try { var steamInitialized = SteamAPI_Init(); }
         catch (Exception e) { Console.WriteLine(e.ToString()); }
         var programT = TargetAssembly.GetType("THMHJ.Program", true);
         var mainMethod = AccessTools.Method(programT, "Main");
         mainMethod.Invoke(null, new object[] { new string[] { } });
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
