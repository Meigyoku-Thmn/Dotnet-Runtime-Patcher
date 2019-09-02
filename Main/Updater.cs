using CommandLine;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RuntimePatcher {
   class Updater {
      static readonly string LogFilePath = ConfigurationManager.AppSettings["UpdaterLogFilePath"];
      internal StreamWriter log;
      string id;
      string patchName;
      string packageName;
      string oldFilesPath;
      string urlFilePath;
      string targetPath;
      string rootDirectory;
      Icon icon;
      WebClient wc;
      Options options;
      public Updater(string targetPath, string id, string packageName, string patchName, string rootDirectory, string packageDirectory, Options options) {
         this.options = options;
         this.targetPath = targetPath;
         this.rootDirectory = rootDirectory;
         if (Settings.InputOptions?.LogPath == null)
            log = StreamWriter.Null; // use a blackhole as log file
         else
            log = new StreamWriter(File.Open(Path.Combine(rootDirectory, LogFilePath), FileMode.Create, FileAccess.Write, FileShare.Read), Encoding.UTF8);
         log.Log(DateTime.Now.ToString("F", new CultureInfo("en-US")));
         log.Log("Dotnet Runtime Patcher by Meigyoku Thmn");
         log.Log($"Version {Launcher.CurrentVersion}");
         this.packageName = packageName;
         this.patchName = patchName;
         this.id = id;
         log.Log($"Package Name = {packageName}");
         log.Log($"Patch Name = {patchName}");
         log.Log($"Id = {id}");
         oldFilesPath = Path.Combine(packageDirectory, id, packageName, "files.jsonc");
         urlFilePath = Path.Combine(packageDirectory, id, "urls.jsonc");
         wc = new WebClient { Encoding = Encoding.UTF8 };
      }
      ManualResetEventSlim updatingAllowEvent = new ManualResetEventSlim(false);
      public void EnableUpdateButton() {
         updatingAllowEvent.Set();
      }
      public void Close() {
         log.Close();
         wc.Dispose();
      }
      public async Task RunAsync() {
         var dlg = new TaskBox();
         dlg.TaskDlg.Icon = TaskDialogStandardIcon.None;
         dlg.TaskDlg.Caption = "Trình cập nhật";
         dlg.TaskDlg.Text = "Đang kiểm tra xem có bản cập nhật nào cho gói hiện hành hay không...";
         dlg.TaskDlg.StandardButtons = TaskDialogStandardButtons.Cancel;
         dlg.TaskDlg.Closing += (sender, e) => {
            if (e.TaskDialogResult != TaskDialogResult.Cancel) return;
            wc.CancelAsync();
            e.Cancel = true;
            lock (dlg) dlg.Close();
         };
         dlg.ShowAsync();
         string serverUrl = null;
         try {
            log.Log($"Read {urlFilePath}");
            var urlFileCfg = JObject.Parse(File.ReadAllText(urlFilePath));
            serverUrl = (string)urlFileCfg["serverUrl"];
            if (serverUrl == null) {
               log.Log("Server Url is missing!");
               lock (dlg) dlg.Close();
               dlg = new TaskBox();
               dlg.TaskDlg.Icon = TaskDialogStandardIcon.Error;
               dlg.TaskDlg.Caption = "Trình cập nhật";
               dlg.TaskDlg.InstructionText = "Có lỗi đã xảy ra!";
               dlg.TaskDlg.Text = "Không tìm thấy server url.";
               dlg.TaskDlg.StandardButtons = TaskDialogStandardButtons.Ok;
               dlg.Show();
               return;
            }
            var filesUri = new Uri(serverUrl).Concat(packageName, "files.jsonc");
            string filesCfgStr;
            try {
               log.Log($"Downloading {filesUri}");
               filesCfgStr = await wc.DownloadStringTaskAsync(filesUri);
            }
            catch (WebException err) {
               if (err.Status == WebExceptionStatus.RequestCanceled) {
                  log.Log("Cancelled by user.");
                  return;
               };
               throw;
            }
            var filesCfg = JObject.Parse(filesCfgStr);
            var patchRoot = patchName + '/';
            var files = filesCfg.Children<JProperty>().Where(
               file => file.Name.IndexOf(patchRoot, 0, patchRoot.Length) != -1);
            JObject oldFiles;
            try {
               log.Log($"Read {oldFilesPath}");
               oldFiles = JObject.Parse(File.ReadAllText(oldFilesPath));
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException) {
               log.Log("Local file list doesn't exist.");
               oldFiles = new JObject();
            }
            var verFile_newChecksum = filesCfg["versions.jsonc"];
            var verFile_oldChecksum = oldFiles["versions.jsonc"];
            if (verFile_newChecksum == null) {
               log.Log("versions.jsonc's checksum is missing on server!");
               lock (dlg) dlg.Close();
               dlg = new TaskBox();
               dlg.TaskDlg.Icon = TaskDialogStandardIcon.Error;
               dlg.TaskDlg.Caption = "Trình cập nhật";
               dlg.TaskDlg.InstructionText = "Có lỗi đã xảy ra!";
               dlg.TaskDlg.Text = "Không tìm thấy checksum của file versions.jsonc trên server!";
               dlg.TaskDlg.StandardButtons = TaskDialogStandardButtons.Ok;
               dlg.Show();
               return;
            }
            var updateNeeded = false;
            if (verFile_oldChecksum == null || (uint)verFile_newChecksum != (uint)verFile_oldChecksum) {
               var fileUri = new Uri(serverUrl).Concat(packageName, "versions.jsonc");
               var filePath = Path.Combine(Launcher.PackageDirectory, id, packageName, "versions.jsonc");
               log.Log("Found new versions.jsonc");
               updateNeeded = true;
            }
            if (updateNeeded == false) foreach (var file in files) {
                  var oldChecksum = oldFiles[file.Name];
                  if (oldChecksum != null && (uint)file.Value == (uint)oldChecksum) {
                     continue;
                  }
                  var fileUri = new Uri(serverUrl).Concat(packageName, file.Name);
                  var filePath = Path.Combine(Launcher.PackageDirectory, id, packageName, file.Name);
                  log.Log($"Found new {file.Name}");
                  updateNeeded = true; break;
               }
            lock (dlg) dlg.Close();
            if (updateNeeded == false) {
               dlg = new TaskBox();
               dlg.TaskDlg.Icon = TaskDialogStandardIcon.Information;
               dlg.TaskDlg.Caption = "Trình cập nhật";
               dlg.TaskDlg.InstructionText = "Chưa có bản cập nhật mới.";
               dlg.TaskDlg.FooterCheckBoxText = "Kiểm tra cập nhật mỗi lần khởi động.";
               dlg.TaskDlg.FooterCheckBoxChecked = true;
               dlg.TaskDlg.Opened += async (sender, e) => {
                  for (var i = 5; i > 0; i--) {
                     dlg.TaskDlg.Text = $"(Hộp thoại sẽ tự đóng sau {i} giây nữa...)";
                     await Task.Delay(1000);
                  }
                  dlg.Close();
               };
               dlg.Show();
               options.UpdateOnStart = dlg.TaskDlg.FooterCheckBoxChecked.Value;
               options.Save();
               return;
            }
            dlg = new TaskBox();
            dlg.TaskDlg.Icon = TaskDialogStandardIcon.Information;
            dlg.TaskDlg.Caption = "Trình cập nhật";
            dlg.TaskDlg.InstructionText = "Đã có bản cập nhật mới!";
            dlg.TaskDlg.FooterCheckBoxText = "Kiểm tra cập nhật mỗi lần khởi động.";
            dlg.TaskDlg.FooterCheckBoxChecked = true;
            dlg.TaskDlg.Text = "Bạn phải tắt ứng dụng hiện hành để có thể cập nhật gói mới.";
            TaskDialogButton customButtonCancel = new TaskDialogButton("customButtonCancel", "Cancel") {
               Default = true
            };
            TaskDialogButton customButtonUpdate = new TaskDialogButton("customButtonUpdate", "Cập nhật") {
               Default = false
            };
            dlg.TaskDlg.Controls.Add(customButtonUpdate);
            dlg.TaskDlg.Controls.Add(customButtonCancel);
            dlg.TaskDlg.Opened += (sender, e) => {
               customButtonUpdate.Enabled = false;
               new Thread(() => {
                  updatingAllowEvent.Wait();
                  customButtonUpdate.Enabled = true;
               }).Start();
            };
            var doUpdate = false;
            customButtonCancel.Click += (sender, e) => { doUpdate = false; dlg.Close(); };
            customButtonUpdate.Click += (sender, e) => { doUpdate = true; dlg.Close(); };
            dlg.Show();
            options.UpdateOnStart = dlg.TaskDlg.FooterCheckBoxChecked.Value;
            options.Save();
            if (doUpdate == false) return;
            var inputOptions = new ConfiguratorInputOptions {
               Specify = true,
               Id = id,
               Name = packageName,
               ServerUrl = serverUrl,
               TargetPath = targetPath,
            };
            var arguments = Parser.Default.FormatCommandLine(inputOptions);
            Process.Start(Path.Combine(rootDirectory, ConfigurationManager.AppSettings["ConfiguratorName"]), arguments).WaitForExit();
         }
         catch (Exception err) {
            log.Log(err.ToString());
            lock (dlg) dlg.Close();
            dlg = new TaskBox();
            dlg.TaskDlg.Caption = "Trình cập nhật";
            dlg.TaskDlg.InstructionText = "Có lỗi đã xảy ra!";
            dlg.TaskDlg.Text = $"Xem file {LogFilePath} để biết chi tiết!";
            dlg.TaskDlg.StandardButtons = TaskDialogStandardButtons.Ok;
            dlg.Show();
         }
      }
   }
}
