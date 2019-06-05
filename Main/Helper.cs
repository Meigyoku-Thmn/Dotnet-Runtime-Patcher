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
   public static class Helper {
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
      public static Delegate MakeDelegate(this MethodInfo method, object target = null) {
         var tArgs = new List<Type>();
         foreach (var param in method.GetParameters())
            tArgs.Add(param.ParameterType);
         tArgs.Add(method.ReturnType);
         var delDecltype = Expression.GetDelegateType(tArgs.ToArray());
         return method.CreateDelegate(delDecltype, target);
      }
   }
}
