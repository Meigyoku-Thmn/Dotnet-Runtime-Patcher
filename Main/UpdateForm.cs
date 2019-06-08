using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Launcher.Helper;
namespace Launcher {
   public partial class UpdateForm : Form {
      internal StreamWriter log;
      public UpdateForm(string rootPath, string id, string packageName, string patchName) {
         log = new StreamWriter(File.Open(Path.Combine(Launcher.RootDirectory, "updater.log"), FileMode.Create, FileAccess.Write, FileShare.Read), Encoding.UTF8);
         DateTime now = DateTime.Now;
         log.Log(now.ToString("F", new CultureInfo("en-US")));
         log.Log("Dotnet Runtime Patcher by Meigyoku Thmn");
         log.Log($"Version {Launcher.CurrentVersion}");
         this.rootPath = rootPath;
         this.packageName = packageName;
         this.patchName = patchName;
         this.id = id;
         log.Log($"Package Name = {packageName}");
         log.Log($"Patch Name = {patchName}");
         log.Log($"Id = {id}");
         oldFilesPath = Path.Combine(rootPath, id, packageName, "files.jsonc");
         urlFilePath = Path.Combine(rootPath, id, "urls.jsonc");
         wc = new WebClient { Encoding = Encoding.UTF8 };
         InitializeComponent();
      }

      private void UpdateForm_Load(object sender, EventArgs e) {
         // we delay this run a bit, so the async task can fully utilize the message loop of current UI thread
         SetTimeout((_sender, _e) => RunCheckForUpdate(), 1); // zero delay will *not work*
      }
      WebClient wc;
      string id;
      string rootPath;
      string patchName;
      string packageName;
      string oldFilesPath;
      string urlFilePath;
      enum State { OnGoing, NoNeedToUpdate, CanUpdate, DidUpdated, Error };
      State state = State.OnGoing;
      bool appIsClose = false;
      List<ValueTuple<string, string>> pickedUrl = new List<ValueTuple<string, string>>();
      string filesCfgStr;
      public async void RunCheckForUpdate() {
         try {
            log.Log($"Read {urlFilePath}");
            var urlFileCfg = JObject.Parse(File.ReadAllText(urlFilePath));
            var serverUrl = urlFileCfg["serverUrl"];
            if (serverUrl == null) {
               log.Log("Server Url is missing!");
               lblMessage.Text = "Không tìm thấy server url.";
               state = State.Error;
               return;
            }
            var filesUri = new Uri((string)serverUrl).Concat(packageName, "files.jsonc");
            try {
               log.Log($"Downloading {filesUri}");
               filesCfgStr = await wc.DownloadStringTaskAsync(filesUri);
            }
            catch (WebException err) {
               if (err.Status == WebExceptionStatus.RequestCanceled) {
                  log.Log("Cancelled by user.");
                  state = State.Error;
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
            catch (FileNotFoundException) {
               log.Log("Local file list doesn't exist.");
               oldFiles = new JObject();
            }
            var verFile_newChecksum = filesCfg["versions.jsonc"];
            var verFile_oldChecksum = oldFiles["versions.jsonc"];
            if (verFile_newChecksum == null) {
               log.Log("versions.jsonc's checksum is missing on server!");
               lblMessage.Text = "Không tìm thấy checksum của file versions.jsonc trên server!";
               state = State.Error;
               return;
            }
            if (verFile_oldChecksum == null || (uint)verFile_newChecksum != (uint)verFile_oldChecksum) {
               var fileUri = new Uri((string)serverUrl).Concat(packageName, "versions.jsonc");
               var filePath = Path.Combine(rootPath, id, packageName, "versions.jsonc");
               log.Log("Found new versions.jsonc");
               pickedUrl.Add((fileUri.ToString(), filePath));
            }
            foreach (var file in files) {
               var oldChecksum = oldFiles[file.Name];
               if (oldChecksum != null && (uint)file.Value == (uint)oldChecksum) {
                  continue;
               }
               var fileUri = new Uri((string)serverUrl).Concat(packageName, file.Name);
               var filePath = Path.Combine(rootPath, id, packageName, file.Name);
               log.Log($"Found new {file.Name}");
               pickedUrl.Add((fileUri.ToString(), filePath));
            }
            OnUpdateChecked();
         }
         catch (Exception err) {
            log.Log(err.ToString());
            state = State.Error;
            lblMessage.Text = "Có lỗi đã xảy ra, xem file updater.log để biết chi tiết!";
         }
      }

      public void OnUpdateChecked() {
         if (pickedUrl.Count > 0)
            lblMessage.Text = "Đã có bản cập nhật mới! Bạn phải tắt ứng dụng hiện hành để có thể cập nhật gói mới.";
         else {
            log.Log("No need to update now.");
            lblMessage.Text = "Chưa có bản cập nhật mới.";
            SetTimeout((sender, e) => Close(), 5000);
            state = State.NoNeedToUpdate;
            return;
         }
         if (appIsClose == true)
            cmdUpdate.Enabled = true;
         state = State.CanUpdate;
      }

      public void EnableUpdateButton() {
         if (state == State.CanUpdate)
            cmdUpdate.Enabled = true;
         appIsClose = true;
      }

      private void cmdUpdate_Click(object sender, EventArgs e) {
         RunUpdate();
         cmdUpdate.Enabled = false;
      }

      private async void RunUpdate() {
         try {
            log.Log("Begin updating...");
            state = State.OnGoing;
            lblMessage.Text = "Đang cập nhật các file có sự thay đổi...";
            foreach (var urlItem in pickedUrl) {
               var url = urlItem.Item1;
               var path = urlItem.Item2;
               Directory.CreateDirectory(Path.GetDirectoryName(path));
               try {
                  log.Log($"Downloading {url}");
                  await wc.DownloadFileTaskAsync(url, path);
               }
               catch (WebException err) {
                  if (err.Status == WebExceptionStatus.RequestCanceled) {
                     log.Log("Cancelled by user.");
                     return;
                  }
                  throw;
               }
            }
            log.Log($"Write new {oldFilesPath}");
            File.WriteAllText(oldFilesPath, filesCfgStr);
            lblMessage.Text = "Đã cập nhật các file có sự thay đổi!";
            SetTimeout((sender, e) => Close(), 5000);
            state = State.DidUpdated;
         }
         catch (Exception err) {
            log.Log(err.ToString());
            state = State.Error;
            lblMessage.Text = "Có lỗi đã xảy ra, xem file updater.log để biết chi tiết!";
         }
      }

      private void OnCloseGuard() {
         if (state == State.OnGoing) {
            var rs = MessageBox.Show("Có phải bạn muốn hủy bỏ quá trình cập nhật này không?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk);
            if (rs == DialogResult.No) return;
         }
         wc.CancelAsync();
         Close();
      }

      private void cmdClose_Click(object sender, EventArgs e) {
         OnCloseGuard();
      }

      private void UpdateForm_FormClosing(object sender, FormClosingEventArgs e) {
         e.Cancel = true;
         OnCloseGuard();
      }

      private new void Close() {
         FormClosing -= UpdateForm_FormClosing;
         base.Close();
      }

      private void UpdateForm_FormClosed(object sender, FormClosedEventArgs e) {
         log.Close();
         
      }
   }
}
