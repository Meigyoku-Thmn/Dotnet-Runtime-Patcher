using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
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
         var rootPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
         var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
         // STEP 1: List all servers/packages, then ask user to select a package
         Console.WriteLine($"Get server list from {SystemCfgUrl}\n");
         var systemCfgStr = wc.DownloadString(SystemCfgUrl);
         var systemCfg = JObject.Parse(systemCfgStr);
         var servers = systemCfg["servers"].Aggregate(new List<JObject>(), (acc, serverUrl) => {
            var serverUri = new Uri(new Uri((string)serverUrl), "server.jsonc");
            try {
               var serverCfgStr = wc.DownloadString(serverUri);
               var serverCfg = JObject.Parse(serverCfgStr);
               serverCfg["url"] = serverUrl;
               acc.Add(serverCfg);
            }
            catch (Exception err) {
               Console.WriteLine($"Url: {serverUri}");
               Console.WriteLine(err.Message);
            }
            return acc;
         });
         var count = 0;
         var packages = new List<Package>();
         foreach (var serverCfg in servers) {
            Console.WriteLine(" " + serverCfg["title"]);
            foreach (JProperty package in serverCfg["packages"]) {
               Console.WriteLine($"  [{count++}] {package.Name}: {package.Value}");
               packages.Add(new Package(package.Name, (string)serverCfg["id"], (string)serverCfg["url"]));
            }
         }
         Console.Write($"\nPlease select your desired package (from 0 to {count - 1}): ");
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
         Console.WriteLine(targetPath);
         var targetInfo = GetChecksumAndSize(targetPath);

         // STEP 3: download package
         Console.WriteLine();
         var versionUri = new Uri(new Uri(selectedPackage.ServerUrl), selectedPackage.Name + "/versions.jsonc");
         var versionCfgStr = wc.DownloadString(versionUri);
         var versionCfg = JObject.Parse(versionCfgStr);
         var targetVersion = versionCfg[targetInfo.Hash];
         if (targetVersion == null || (long)targetVersion["size"] != targetInfo.Size) {
            Console.WriteLine("We can't find proper patch for your application file, maybe your app is a newer version, or we don't have a patch for it.");
            return;
         }
         var patchRoot = (string)targetVersion["name"] + '/';
         var filesUri = new Uri(new Uri(selectedPackage.ServerUrl), selectedPackage.Name + "/files.jsonc");
         var filesCfgStr = wc.DownloadString(filesUri);
         var filesCfg = JObject.Parse(filesCfgStr);
         var files = filesCfg.Children<JProperty>().Where(file => file.Name.IndexOf(patchRoot, 0, patchRoot.Length) != -1);
         var oldFilesPath = Path.Combine(rootPath, selectedPackage.Id, selectedPackage.Name, "files.jsonc");
         JObject oldFiles;
         try {
            oldFiles = JObject.Parse(File.ReadAllText(oldFilesPath));
         }
         catch (FileNotFoundException) {
            oldFiles = new JObject();
         }
         var writeCount = 0;
         var skipCount = 0;
         foreach (var file in files) {
            var oldChecksum = oldFiles[file.Name];
            if (oldChecksum != null && (uint)file.Value == (uint)oldChecksum) {
               skipCount++;
               continue;
            }
            var fileUri = new Uri(new Uri(selectedPackage.ServerUrl), Path.Combine(selectedPackage.Name, file.Name));
            var filePath = Path.Combine(rootPath, selectedPackage.Id, selectedPackage.Name, file.Name);
            Console.WriteLine(fileUri);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            wc.DownloadFile(fileUri, filePath);
            writeCount++;
         }
         Console.WriteLine($"Downloaded {writeCount} file(s)");
         Console.WriteLine($"Skipped {skipCount} file(s)");
         File.WriteAllText(oldFilesPath, filesCfgStr);
         var versionsPath = Path.Combine(rootPath, selectedPackage.Id, selectedPackage.Name, "versions.jsonc");
         File.WriteAllText(versionsPath, versionCfgStr);

         // STEP 4: create config file
         var defaultShortcutName = $"{targetVersion["name"]}_{selectedPackage.Name}";
         var exeCfg = new JObject();
         exeCfg["targetPath"] = targetPath;
         exeCfg["package"] = $"{selectedPackage.Id}/{selectedPackage.Name}"; ;
         File.WriteAllText(Path.Combine(rootPath, defaultShortcutName + ".jsonc"), exeCfg.ToString());

         // STEP 5: create shortcut on desktop
         Console.WriteLine("\nPlease set a name of a shortcut file that we will create on your desktop:");
         Console.Write($"(Default: {defaultShortcutName}) > ");
         var newShortcutName = Console.ReadLine().Trim();
         if (newShortcutName.Length == 0) newShortcutName = defaultShortcutName;
         if (!newShortcutName.IsValidPath()) {
            Console.WriteLine("Invalid path, use the default instead.");
            newShortcutName = defaultShortcutName;
         }
         var arguments = $"\"{defaultShortcutName}\"";
         CreateShortcut(
            Path.Combine(desktopPath, newShortcutName + ".lnk"),
            Path.Combine(rootPath, "Dotnet-Runtime-Patcher.exe"),
            arguments,
            targetPath + ", 0"
         );
         Console.WriteLine("Created a shortcut on desktop.");
         Console.WriteLine("\nAll done! Press enter to exit this wizard...");
         Console.ReadLine();
      }
      static void CreateShortcut(string shortcutAddress, string targetPath, string arguments, string iconPath) {
         var shell = new IWshRuntimeLibrary.WshShell();
         var shortcut = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(shortcutAddress);
         shortcut.TargetPath = targetPath;
         shortcut.Arguments = arguments;
         shortcut.IconLocation = iconPath;
         shortcut.Save();
      }
   }
}
