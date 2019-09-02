using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RuntimePatcher {
   class TaskBox {
      // https://www.developerfusion.com/article/71793/windows-7-task-dialogs/
      readonly public TaskDialog TaskDlg = new TaskDialog();
      public bool IsOpened { get; private set; }
      public TaskDialogResult Show() {
         if (IsOpened == true) throw new Exception("Task Dialog is already opened!");
         IsOpened = true; var rs = TaskDlg.Show(); IsOpened = false;
         return rs;
      }
      public void ShowAsync() {
         if (IsOpened == true) throw new Exception("Task Dialog is already opened!");
         IsOpened = true;
         new Thread(() => { TaskDlg.Show(); IsOpened = false; }).Start();
      }
      public void Close(TaskDialogResult taskDlgResult = TaskDialogResult.Close) {
         if (IsOpened == false) return;
         IsOpened = false;
         TaskDlg.Close(taskDlgResult);
      }
   }
}
