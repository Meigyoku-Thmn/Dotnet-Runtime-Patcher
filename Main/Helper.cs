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
   public class TargetInfo {
      public string Hash;
      public long Size;
      public TargetInfo(string Hash, long Size) {
         this.Hash = Hash; this.Size = Size;
      }
   }
   public static class Helper {
      public static Type[] TypeL(params Type[] types) {
         return types;
      }
      public static TargetInfo GetChecksumAndSize(string file) {
         using (FileStream stream = File.OpenRead(file)) {
            var sha = new SHA256Managed();
            byte[] checksum = sha.ComputeHash(stream);
            return new TargetInfo(BitConverter.ToString(checksum).Replace("-", String.Empty), stream.Length);
         }
      }
      public static Delegate MakeDelegate(this MethodInfo method, object target = null) {
         var tArgs = new List<Type>();
         foreach (var param in method.GetParameters())
            tArgs.Add(param.ParameterType);
         tArgs.Add(method.ReturnType);
         var delDecltype = Expression.GetDelegateType(tArgs.ToArray());
         return method.CreateDelegate(delDecltype, target);
      }
      public static bool IsValidPath(this string path) {
         bool bOk = false;
         try { new FileInfo(path); bOk = true; }
         catch (ArgumentException) { }
         catch (System.IO.PathTooLongException) { }
         catch (NotSupportedException) { }
         return bOk;
      }
   }
}
