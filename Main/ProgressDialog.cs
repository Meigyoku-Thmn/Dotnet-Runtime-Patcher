using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace RuntimePatcher {
   public class ProgressDialog {
      private IntPtr _parentHandle;
      private Win32IProgressDialog pd = null;
      public ProgressDialog(IntPtr parentHandle) {
         this._parentHandle = parentHandle;
      }
      public void ShowDialog(params PROGDLG[] flags) {
         if (pd == null) {
            pd = (Win32IProgressDialog)new Win32ProgressDialog();
            pd.SetTitle(this._Title);
            pd.SetCancelMsg(this._CancelMessage, null);
            pd.SetLine(1, this._Line1, false, IntPtr.Zero);
            pd.SetLine(2, this._Line2, false, IntPtr.Zero);
            pd.SetLine(3, this._Line3, false, IntPtr.Zero);
            PROGDLG dialogFlags = PROGDLG.Normal;
            if (flags.Length != 0) {
               dialogFlags = flags[0];
               for (var i = 1; i < flags.Length; i++) {
                  dialogFlags = dialogFlags | flags[i];
               }
            }
            pd.StartProgressDialog(this._parentHandle, null, dialogFlags, IntPtr.Zero);
         }
      }
      public void CloseDialog() {
         if (pd != null) {
            pd.StopProgressDialog();
            Marshal.ReleaseComObject(pd);
            pd = null;
         }
      }
      private string _Title = string.Empty;
      public string Title {
         get {
            return this._Title;
         }
         set {
            this._Title = value;
            if (pd != null) {
               pd.SetTitle(this._Title);
            }
         }
      }
      private string _CancelMessage = string.Empty;
      public string CancelMessage {
         get {
            return this._CancelMessage;
         }
         set {
            this._CancelMessage = value;
            if (pd != null) {
               pd.SetCancelMsg(this._CancelMessage, null);
            }
         }
      }
      private string _Line1 = string.Empty;
      public string Line1 {
         get {
            return this._Line1;
         }
         set {
            this._Line1 = value;
            if (pd != null) {
               pd.SetLine(1, this._Line1, false, IntPtr.Zero);
            }
         }
      }
      private string _Line2 = string.Empty;
      public string Line2 {
         get {
            return this._Line2;
         }
         set {
            this._Line2 = value;
            if (pd != null) {
               pd.SetLine(2, this._Line2, false, IntPtr.Zero);
            }
         }
      }
      private string _Line3 = string.Empty;
      public string Line3 {
         get {
            return this._Line3;
         }
         set {
            this._Line3 = value;
            if (pd != null) {
               pd.SetLine(3, this._Line3, false, IntPtr.Zero);
            }
         }
      }
      private uint _value = 0;
      public uint Value {
         get {
            return this._value;
         }
         set {
            this._value = value;
            if (pd != null) {
               pd.SetProgress(this._value, this._maximum);
            }
         }
      }
      private uint _maximum = 100;
      public uint Maximum {
         get {
            return this._maximum;
         }
         set {
            this._maximum = value;
            if (pd != null) {
               pd.SetProgress(this._value, this._maximum);
            }
         }
      }
      public bool HasUserCancelled {
         get {
            if (pd != null) {
               return pd.HasUserCancelled();
            }
            else
               return false;
         }
      }
      #region "Win32 Stuff"
      // The below was copied from: http://pinvoke.net/default.aspx/Interfaces/IProgressDialog.html
      public static class shlwapi {
         [DllImport("shlwapi.dll", CharSet = CharSet.Auto)]
         static extern bool PathCompactPath(IntPtr hDC, [In, Out] StringBuilder pszPath, int dx);
      }
      [ComImport]
      [Guid("EBBC7C04-315E-11d2-B62F-006097DF5BD4")]
      [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
      public interface Win32IProgressDialog {
         void StartProgressDialog(
            IntPtr hwndParent, //HWND
            [MarshalAs(UnmanagedType.IUnknown)]    object punkEnableModless, //IUnknown
            PROGDLG dwFlags,  //DWORD
            IntPtr pvResevered //LPCVOID
         );
         void StopProgressDialog();
         void SetTitle(
            [MarshalAs(UnmanagedType.LPWStr)] string pwzTitle //LPCWSTR
         );
         void SetAnimation(
            IntPtr hInstAnimation, //HINSTANCE
            ushort idAnimation //UINT
         );
         [PreserveSig]
         [return: MarshalAs(UnmanagedType.Bool)]
         bool HasUserCancelled();
         void SetProgress(
            uint dwCompleted, //DWORD
            uint dwTotal //DWORD
         );
         void SetProgress64(
            ulong ullCompleted, //ULONGLONG
            ulong ullTotal //ULONGLONG
         );
         void SetLine(
            uint dwLineNum, //DWORD
            [MarshalAs(UnmanagedType.LPWStr)] string pwzString, //LPCWSTR
            [MarshalAs(UnmanagedType.VariantBool)] bool fCompactPath, //BOOL
            IntPtr pvResevered //LPCVOID
         );
         void SetCancelMsg(
            [MarshalAs(UnmanagedType.LPWStr)] string pwzCancelMsg, //LPCWSTR
            object pvResevered //LPCVOID
         );
         void Timer(
            PDTIMER dwTimerAction, //DWORD
            object pvResevered //LPCVOID
         );
      }
      [ComImport]
      [Guid("F8383852-FCD3-11d1-A6B9-006097DF5BD4")]
      public class Win32ProgressDialog {
      }
      public enum PDTIMER : uint //DWORD
      {
         Reset = (0x01),
         Pause = (0x02),
         Resume = (0x03)
      }
      [Flags]
      public enum PROGDLG : uint //DWORD
      {
         Normal = 0x00000000,
         Modal = 0x00000001,
         AutoTime = 0x00000002,
         NoTime = 0x00000004,
         NoMinimize = 0x00000008,
         NoProgressBar = 0x00000010
      }
      #endregion
   }
}
