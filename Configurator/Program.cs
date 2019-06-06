using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Launcher.Helper;
namespace Configurator {
   public class Program {
      static readonly string SystemCfgUrl = "https://raw.githubusercontent.com/Meigyoku-Thmn/Dotnet-Runtime-Patcher/master/system.jsonc";
      class Package {
         public string Name;
         public string Id;
         public string ServerUrl;
         public Package(string Name, string Id, string ServerUrl) {
            this.Name = Name;
            this.Id = Id;
            this.ServerUrl = ServerUrl;
         }
      }
      [STAThread]
      public static void Main(string[] args) {
         Console.OutputEncoding = Encoding.Unicode;
         var wc = new WebClient();
         wc.Encoding = Encoding.UTF8;

         // STEP 1: List all servers/packages, then ask user to select a package
         var systemCfgStr = wc.DownloadString(SystemCfgUrl);
         var systemCfg = JObject.Parse(systemCfgStr);
         var servers = systemCfg["servers"].Select(serverUrl => {
            var serverUri = new Uri(new Uri((string)serverUrl), "server.jsonc");
            var serverCfgStr = wc.DownloadString(serverUri);
            var serverCfg = JObject.Parse(serverCfgStr);
            serverCfg["url"] = serverUrl;
            return serverCfg;
         });
         var count = 0;
         var packages = new List<Package>();
         foreach (var serverCfg in servers) {
            Console.WriteLine(" " + serverCfg["title"]);
            foreach (JProperty package in serverCfg["packages"]) {
               Console.WriteLine($"  [{count++}] {package.Name}: {package.Value}");
               packages.Add(new Package(package.Name, (string)package.Value, (string)serverCfg["url"]));
            }
         }
         Console.WriteLine($"Please select your desired package (from 0 to {count - 1}): ");
         int selectedIndex;
         while (!int.TryParse(Console.ReadLine(), out selectedIndex) || (selectedIndex > count - 1 || selectedIndex < 0)) ;
         var selectedPackage = packages[selectedIndex];

         // STEP 2: ask user to select the exe that they want to patch
         Console.WriteLine("\nPlease select your target executable file...");
         OpenFileDialog openFileDg = new OpenFileDialog();
         openFileDg.Title = "Please select your target executable file";
         openFileDg.Filter = "Executable file|*.exe|All files|*.*";
         openFileDg.CheckFileExists = true;
         openFileDg.CheckPathExists = true;
         var dialogRs = openFileDg.ShowDialog();
         if (dialogRs == DialogResult.Cancel) return;
         var targetPath = openFileDg.FileName;
         var targetInfo = GetChecksumAndSize(targetPath);

         // STEP 3: download package
         var versionUri = new Uri(new Uri(selectedPackage.ServerUrl), selectedPackage.Name + "/versions.jsonc");
         var versionCfgStr = wc.DownloadString(versionUri);
         var versionCfg = JObject.Parse(versionCfgStr);
         var targetVersion = versionCfg[targetInfo.Hash];
         if (targetVersion == null) {
            Console.WriteLine("We can't find proper patch for your application file, maybe your app is a newer version, or we don't have a patch for it.");
            return;
         }

         Console.WriteLine("Press enter to exit...");
         Console.ReadLine();
      }
   }
}
