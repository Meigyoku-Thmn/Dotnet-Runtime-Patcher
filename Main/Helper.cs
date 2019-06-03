using Sigil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Launcher {
   static class Helper {
      [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
      private struct ACTCTX {
         public int cbSize;
         public uint dwFlags;
         public string lpSource;
         public ushort wProcessorArchitecture;
         public short wLangId;
         public string lpAssemblyDirectory;
         public string lpResourceName;
         public string lpApplicationName;
         public IntPtr hModule;
      }
      [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
      extern static IntPtr CreateActCtx(ref ACTCTX actctx);
      [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
      [return: MarshalAs(UnmanagedType.Bool)]
      static extern bool ActivateActCtx(IntPtr hActCtx, out IntPtr lpCookie);
      public static bool LoadManifest() {
         var manifestPath = @"app.manifest"; // path tới manifest file
         var searchedPath = @".\"; // path tới thư mục có chứa COM DLL mà cần được load
         ACTCTX info = new ACTCTX {
            cbSize = Marshal.SizeOf(typeof(ACTCTX)),
            lpSource = manifestPath,
            lpAssemblyDirectory = searchedPath
         };
         IntPtr m_hActCtx = IntPtr.Zero;
         IntPtr lpCookie = IntPtr.Zero;
         m_hActCtx = CreateActCtx(ref info);
         if (m_hActCtx != (IntPtr)(-1)) {
            ActivateActCtx(m_hActCtx, out lpCookie);
            Console.WriteLine("ActivateActCtx completed!");
         }
         else {
            var lastError = Marshal.GetLastWin32Error();
            Console.WriteLine(Directory.GetCurrentDirectory());
            Console.WriteLine(lastError);
            return false;
         }
         return true;
      }
      public static Type[] TypeL(params Type[] types) {
         return types;
      }
      public static dynamic GetChecksumAndSize(string file) {
         using (FileStream stream = File.OpenRead(file)) {
            var sha = new SHA256Managed();
            byte[] checksum = sha.ComputeHash(stream);
            return new {
               Hash = BitConverter.ToString(checksum).Replace("-", String.Empty),
               Size = stream.Length,
            };
         }
      }
      public static Delegate CreateDelegate(MethodInfo method) {
         var tArgs = new List<Type>();
         foreach (var param in method.GetParameters())
            tArgs.Add(param.ParameterType);
         tArgs.Add(method.ReturnType);
         var delDecltype = Expression.GetDelegateType(tArgs.ToArray());
         // return Delegate.CreateDelegate(delDecltype, method);
         return method.CreateDelegate(delDecltype);
      }
   }
}
