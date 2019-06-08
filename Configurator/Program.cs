using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
      static StreamWriter log;
      [STAThread]
      public static void Main(string[] args) {
         var rootPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
         new StreamWriter(File.Open(Path.Combine(rootPath, "configurator.log"), FileMode.Create, FileAccess.Write, FileShare.Read), Encoding.UTF8);
         AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(Unhandled);
         DateTime now = DateTime.Now;
         log.Log(now.ToString("F", new CultureInfo("en-US")));
         log.Log("Dotnet Runtime Patcher by Meigyoku Thmn");
         log.Log($"Version {Launcher.Launcher.CurrentVersion}");
         Console.OutputEncoding = Encoding.Unicode;
         var wc = new WebClient { Encoding = Encoding.UTF8 };
         var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
         // STEP 1: List all servers/packages, then ask user to select a package
         log.Log($"Get server list from {SystemCfgUrl}\n");
         var systemCfgStr = wc.DownloadString(SystemCfgUrl);
         var systemCfg = JObject.Parse(systemCfgStr);
         var servers = systemCfg["servers"].Aggregate(new List<JObject>(), (acc, serverUrl) => {
            var serverUri = new Uri((string)serverUrl).Concat("server.jsonc");
            try {
               var serverCfgStr = wc.DownloadString(serverUri);
               var serverCfg = JObject.Parse(serverCfgStr);
               serverCfg["url"] = serverUrl;
               acc.Add(serverCfg);
            }
            catch (Exception err) {
               log.Log($"Url: {serverUri}");
               log.Log(err.Message);
            }
            return acc;
         });
         var count = 0;
         var packages = new List<Package>();
         foreach (var serverCfg in servers) {
            log.Log(" " + serverCfg["title"]);
            foreach (JProperty package in serverCfg["packages"]) {
               log.Log($"  [{count++}] {package.Name}: {package.Value}");
               packages.Add(new Package(package.Name, (string)serverCfg["id"], (string)serverCfg["url"]));
            }
         }
         log.Log($"\nPlease select your desired package (from 0 to {count - 1}): ");
         int selectedIndex;
         while (!int.TryParse(Console.ReadLine(), out selectedIndex) || (selectedIndex > count - 1 || selectedIndex < 0)) ;
         log.WriteLine(selectedIndex);
         var selectedPackage = packages[selectedIndex];
         if (selectedPackage.Id.IndexOf('/') != -1 || selectedPackage.Name.IndexOf('/') != -1 || selectedPackage.Id.IndexOf('\\') != -1 || selectedPackage.Name.IndexOf('\\') != -1) {
            log.Log($"Package name ({selectedPackage.Name}) or package id ({selectedPackage.Id}) has slash character, our system cannot allow such character.");
            return;
         }

         // STEP 2: ask user to select the exe that they want to patch
         log.Log("\nPlease select your target executable file.");
         OpenFileDialog openFileDg = new OpenFileDialog();
         openFileDg.Title = "Please select your target executable file";
         openFileDg.Filter = "Executable file|*.exe|All files|*.*";
         openFileDg.CheckFileExists = true;
         openFileDg.CheckPathExists = true;
         var dialogRs = openFileDg.ShowDialog();
         if (dialogRs == DialogResult.Cancel) return;
         var targetPath = openFileDg.FileName;
         log.Log(targetPath);
         var targetInfo = GetChecksumAndSize(targetPath);

         // STEP 3: download packages
         log.Log();
         log.Log("Selected Package:");
         log.Log($"  Name = {selectedPackage.Name}");
         log.Log($"  Id = {selectedPackage.Id}");
         log.Log($"  ServerUrl = {selectedPackage.ServerUrl}");
         var versionUri = new Uri(selectedPackage.ServerUrl).Concat(selectedPackage.Name, "versions.jsonc");
         var versionCfgStr = wc.DownloadString(versionUri);
         var versionCfg = JObject.Parse(versionCfgStr);
         var targetVersion = versionCfg[targetInfo.Hash];
         if (targetVersion == null || (long)targetVersion["size"] != targetInfo.Size) {
            log.Log("We can't find proper patch for your application file, maybe your app is a newer version, or we don't have a patch for it.");
            return;
         }
         var patchName = (string)targetVersion["name"];
         if (patchName == null || patchName.IndexOf("/") != -1 || patchName.IndexOf("\\") != -1) {
            log.Log("We found a patch, but its patch name is null or contains slash character, such character is invalid.");
            log.Log($"Hash: {targetInfo.Hash}");
            log.Log($"Name: {patchName}");
            log.Log($"Ver: {targetVersion["ver"]}");
            log.Log($"Size: {targetInfo.Size}");
            return;
         }
         var patchRoot = patchName + '/';
         var filesUri = new Uri(selectedPackage.ServerUrl).Concat(selectedPackage.Name, "files.jsonc");
         var filesCfgStr = wc.DownloadString(filesUri);
         var filesCfg = JObject.Parse(filesCfgStr);
         var files = filesCfg.Children<JProperty>().Where(file => file.Name.IndexOf(patchRoot, 0, patchRoot.Length) != -1);
         var oldFilesPath = Path.Combine(rootPath, selectedPackage.Id, selectedPackage.Name, "files.jsonc");
         JObject oldFiles;
         try {
            log.Log($"Read local file list: {oldFilesPath}");
            oldFiles = JObject.Parse(File.ReadAllText(oldFilesPath));
         }
         catch (FileNotFoundException) {
            log.Log("Local file list doesn't exist.");
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
            var fileUri = new Uri(selectedPackage.ServerUrl).Concat(selectedPackage.Name, file.Name);
            var filePath = Path.Combine(rootPath, selectedPackage.Id, selectedPackage.Name, file.Name);
            log.Log(fileUri.ToString());
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            wc.DownloadFile(fileUri, filePath);
            writeCount++;
         }
         log.Log($"Downloaded {writeCount} file(s)");
         log.Log($"Skipped {skipCount} file(s)");
         log.Log($"Write new file list to {oldFilesPath}");
         File.WriteAllText(oldFilesPath, filesCfgStr);
         var versionsPath = Path.Combine(rootPath, selectedPackage.Id, selectedPackage.Name, "versions.jsonc");
         log.Log($"Write {versionsPath}");
         File.WriteAllText(versionsPath, versionCfgStr);
         var urlFilePath = Path.Combine(rootPath, selectedPackage.Id, "urls.jsonc");
         var urlFileCfg = new JObject();
         urlFileCfg["serverUrl"] = selectedPackage.ServerUrl;
         log.Log($"Write {urlFilePath}");
         File.WriteAllText(urlFilePath, urlFileCfg.ToString());

         // STEP 4: create config file
         var defaultShortcutName = $"{targetVersion["name"]}_{selectedPackage.Name}";
         var exeCfg = new JObject();
         exeCfg["targetPath"] = targetPath;
         exeCfg["package"] = $"{selectedPackage.Id}/{selectedPackage.Name}";
         log.Log($"Write profile {defaultShortcutName + ".jsonc"} to root directory");
         File.WriteAllText(Path.Combine(rootPath, defaultShortcutName + ".jsonc"), exeCfg.ToString());

         // STEP 5: create shortcut on desktop
         log.Log("\nPlease set a name of a shortcut file that we will create on your desktop:");
         log.Log($"(Default: {defaultShortcutName}) > ");
         var newShortcutName = Console.ReadLine().Trim();
         log.WriteLine(newShortcutName);
         if (newShortcutName.Length == 0) newShortcutName = defaultShortcutName;
         if (!newShortcutName.IsValidPath()) {
            log.Log("Invalid path, use the default instead.");
            newShortcutName = defaultShortcutName;
         }
         var arguments = $"\"{defaultShortcutName}\"";
         CreateShortcut(
            Path.Combine(desktopPath, newShortcutName + ".lnk"),
            Path.Combine(rootPath, "Dotnet-Runtime-Patcher.exe"),
            arguments,
            targetPath + ", 0"
         );
         log.Log("Created a shortcut on desktop.");
         log.Log("\nAll done! Press enter to exit this wizard.");
         log.Close();
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
      private static void Unhandled(object sender, UnhandledExceptionEventArgs args) {
         log.Log(args.ExceptionObject.ToString());
         log.Close(); Environment.Exit(1);
      }
   }
}
