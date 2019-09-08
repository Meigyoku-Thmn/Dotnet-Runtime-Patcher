using CommandLine;
using Newtonsoft.Json.Linq;
using ShellProgressBar;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static RuntimePatcher.Helper;
using SProgressBar = ShellProgressBar.ProgressBar;
using ConfiguratorInputOptions = RuntimePatcher.ConfiguratorInputOptions;
namespace Configurator {
   public class Program {
      static readonly string SystemCfgUrl = ConfigurationManager.AppSettings["SystemCfgUrl"];
      static readonly string LogFilePath = ConfigurationManager.AppSettings["LogFilePath"];
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
         ConfiguratorInputOptions InputOptions = new ConfiguratorInputOptions();
         IEnumerable<Error> InputErrors = new Error[0];
         Parser.Default.ParseArguments<ConfiguratorInputOptions>(args)
                  .WithNotParsed(errors => InputErrors = errors)
                  .WithParsed(o => InputOptions = o);
         if (InputErrors.Count() > 0) {
            log.Log("Invalid arg syntax!");
            Environment.Exit(-1);
         }
         if (InputOptions.SharedLock == ConfigurationManager.AppSettings["SharedLock"]) {
            Run(args, InputOptions);
         }
         else using (var mutex = new Mutex(false, ConfigurationManager.AppSettings["SharedLock"], out bool createdNew)) {
               if (!createdNew) {
                  Console.WriteLine("An instance of Configurator or Updater is already running!");
                  Environment.Exit(-1);
               }
               Run(args, InputOptions);
            }
      }

      public static void Run(string[] args, ConfiguratorInputOptions InputOptions) {
         ShellProgressBarHack.PatchExcerptFunc();

         var rootPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
         log = new StreamWriter(File.Open(Path.Combine(rootPath, LogFilePath), FileMode.Create, FileAccess.Write, FileShare.Read), Encoding.UTF8);
         var localPackageDirPath = Path.Combine(rootPath, "Pkg");
         var profileDirPath = Path.Combine(rootPath, "Prfl");
         AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(Unhandled);
         DateTime now = DateTime.Now;
         log.Log(now.ToString("F", new CultureInfo("en-US")));
         log.Log("Configurator for Dotnet Runtime Patcher by Meigyoku Thmn");
         log.Log($"Version {Application.ProductVersion}");
         Console.OutputEncoding = Encoding.UTF8;
         var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
         
         Package selectedPackage = new Package(null, null, null);
         string targetPath = null;
         if (InputOptions.Specify == true) {
            if (InputOptions.Id == null || InputOptions.Name == null || InputOptions.ServerUrl == null || InputOptions.TargetPath == null) {
               log.Log("Invalid arg syntax!");
               Environment.Exit(-1);
            }
            selectedPackage.Id = InputOptions.Id;
            selectedPackage.Name = InputOptions.Name;
            selectedPackage.ServerUrl = InputOptions.ServerUrl;
            targetPath = InputOptions.TargetPath;
            goto BeforeStep3;
         }

         // STEP 1: List all servers/packages, then ask user to select a package
         log.Log($"Get server list from {SystemCfgUrl}\n");
         string systemCfgStr;
         using (var wc = new WebClient { Encoding = Encoding.UTF8 }) {
            systemCfgStr = wc.DownloadString(SystemCfgUrl);
         }
         var systemCfg = JObject.Parse(systemCfgStr);
         var servers = systemCfg["servers"].Children<JProperty>().Aggregate(new List<JObject>(), (acc, server) => {
            var serverUri = new Uri((string)server.Value).Concat("server.jsonc");
            try {
               string serverCfgStr;
               using (var wc = new WebClient { Encoding = Encoding.UTF8 }) {
                  serverCfgStr = wc.DownloadString(serverUri);
               }
               var serverCfg = JObject.Parse(serverCfgStr);
               serverCfg["url"] = serverUri.ToString();
               serverCfg["id"] = server.Name;
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
         selectedPackage = packages[selectedIndex];
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
         targetPath = openFileDg.FileName;

BeforeStep3:
         log.Log(targetPath);
         var targetInfo = GetChecksumAndSize(targetPath);

         // STEP 3: download packages
         log.Log();
         log.Log("Selected Package:");
         log.Log($"  Name = {selectedPackage.Name}");
         log.Log($"  Id = {selectedPackage.Id}");
         log.Log($"  ServerUrl = {selectedPackage.ServerUrl}");
         var versionUri = new Uri(selectedPackage.ServerUrl).Concat(selectedPackage.Name, "versions.jsonc");
         string versionCfgStr;
         using (var wc = new WebClient { Encoding = Encoding.UTF8 }) {
            versionCfgStr = wc.DownloadString(versionUri);
         }
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
         string filesCfgStr;
         using (var wc = new WebClient { Encoding = Encoding.UTF8 }) {
            filesCfgStr = wc.DownloadString(filesUri);
         }
         var filesCfg = JObject.Parse(filesCfgStr);
         var files = filesCfg.Children<JProperty>().Where(file => file.Name.IndexOf(patchRoot, 0, patchRoot.Length) != -1);
         var oldFilesPath = Path.Combine(localPackageDirPath, selectedPackage.Id, selectedPackage.Name, "files.jsonc");
         JObject oldFiles;
         try {
            log.Log($"Read local file list: {oldFilesPath}");
            oldFiles = JObject.Parse(File.ReadAllText(oldFilesPath));
         }
         catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException) {
            log.Log("Local file list doesn't exist.");
            oldFiles = new JObject();
         }
         Console.Clear();
         Console.WriteLine(); // for some reason, on some computers, the first line of console is updated very slow, so I just leave the first line alone
         var writeCount = 0;
         var skipCount = 0;
         int totalTicks = files.Count();
         var options = new ProgressBarOptions {
            BackgroundCharacter = '\u2593',
            ProgressBarOnBottom = true,
            EnableTaskBarProgress = true,
            ForegroundColor = ConsoleColor.Yellow,
            ForegroundColorDone = ConsoleColor.Green,
            BackgroundColor = ConsoleColor.DarkGray,
         };
         var childOptions = new ProgressBarOptions {
            BackgroundCharacter = '\u2593',
            ProgressBarOnBottom = true,
            ForegroundColor = ConsoleColor.Yellow,
            ForegroundColorDone = ConsoleColor.Green,
            BackgroundColor = ConsoleColor.DarkGray,
         };
         var mainMessage = "Files downloading progress";
         using (var pbar = new SProgressBar(totalTicks, mainMessage, options)) {
            pbar.Tick(0, $"0/{totalTicks}");
            // use 5 connection as the maximium, unless you want your whole country blocked by github
            Parallel.ForEach(Partitioner.Create(files, EnumerablePartitionerOptions.NoBuffering), new ParallelOptions() { MaxDegreeOfParallelism = 5 }, (file) => {
               var oldChecksum = oldFiles[file.Name];
               if (oldChecksum != null && (uint)file.Value == (uint)oldChecksum) {
                  pbar.Tick($"{pbar.CurrentTick + 1}/{totalTicks} {mainMessage}");
                  Interlocked.Increment(ref skipCount);
                  return;
               }
               var fileUri = new Uri(selectedPackage.ServerUrl).Concat(selectedPackage.Name, file.Name);
               var filePath = Path.Combine(localPackageDirPath, selectedPackage.Id, selectedPackage.Name, file.Name);
               log.Log(fileUri.ToString(), noPrint: true, useLock: true);
               Directory.CreateDirectory(Path.GetDirectoryName(filePath));
               new Func<Task>(async () => {
                  using (var child = pbar.Spawn(100, fileUri.ToString(), childOptions))
                  using (var wc = new WebClient { Encoding = Encoding.UTF8 }) {
                     child.Tick(0);
                     wc.DownloadProgressChanged += (sender, e) => {
                        child.Tick(e.ProgressPercentage);
                     };
                     await wc.DownloadFileTaskAsync(fileUri, filePath);
                  }
               })().Wait();
               pbar.Tick($"{pbar.CurrentTick + 1}/{totalTicks} {mainMessage}");
               Interlocked.Increment(ref writeCount);
            });
         }
         log.Log($"Downloaded {writeCount} file(s)");
         log.Log($"Skipped {skipCount} file(s)");
         log.Log($"Write new file list to {oldFilesPath}");
         Directory.CreateDirectory(Path.GetDirectoryName(oldFilesPath));
         File.WriteAllText(oldFilesPath, filesCfgStr);
         var versionsPath = Path.Combine(localPackageDirPath, selectedPackage.Id, selectedPackage.Name, "versions.jsonc");
         log.Log($"Write {versionsPath}");
         Directory.CreateDirectory(Path.GetDirectoryName(versionsPath));
         File.WriteAllText(versionsPath, versionCfgStr);

         if (InputOptions.Specify == true) goto AfterAllSteps;

         var urlFilePath = Path.Combine(localPackageDirPath, selectedPackage.Id, "urls.jsonc");
         var urlFileCfg = new JObject();
         urlFileCfg["serverUrl"] = selectedPackage.ServerUrl;
         log.Log($"Write {urlFilePath}");
         Directory.CreateDirectory(Path.GetDirectoryName(urlFilePath));
         File.WriteAllText(urlFilePath, urlFileCfg.ToString());

         // STEP 4: create config file
         var defaultShortcutName = $"{targetVersion["name"]}_{selectedPackage.Name}";
         var exeCfg = new JObject();
         exeCfg["targetPath"] = targetPath;
         exeCfg["package"] = $"{selectedPackage.Id}/{selectedPackage.Name}";
         log.Log($"Write profile {defaultShortcutName + ".jsonc"} to root directory");
         var exeProfilePath = Path.Combine(profileDirPath, defaultShortcutName + ".jsonc");
         Directory.CreateDirectory(Path.GetDirectoryName(exeProfilePath));
         File.WriteAllText(exeProfilePath, exeCfg.ToString());

         // STEP 5: create shortcut on desktop
         log.Log("\nPlease set a name of a shortcut file that we will create on your desktop:");
         log.Log($"(Default: {defaultShortcutName})");
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
            typeof(RuntimePatcher.Helper).Assembly.Location,
            arguments,
            targetPath + ", 0"
         );
         log.Log("Created a shortcut on desktop.");

AfterAllSteps:
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
#if DEBUG
         log.Log(args.ExceptionObject.ToString());
#else
         log.Log(args.ExceptionObject.ToString(), true);
         Console.WriteLine((args.ExceptionObject as Exception).Message);
#endif
         log.Close();
         Console.WriteLine("Press enter key to exit.");
         Console.ReadLine();
         Environment.Exit(1);
      }
   }
}
