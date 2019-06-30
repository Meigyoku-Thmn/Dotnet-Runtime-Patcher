using Harmony;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Windows.Forms;

namespace RuntimePatcher {
   public class TargetInfo {
      public string Hash;
      public long Size;
      public TargetInfo(string Hash, long Size) {
         this.Hash = Hash; this.Size = Size;
      }
   }
   public static class Helper {
      public static void Log(this TextWriter log, string str = null, bool noPrint = false) {
         DateTime now = DateTime.Now;
         log.Write($"[{now.ToString("s")}] ");
         if (!noPrint) Console.WriteLine(str);
         log.WriteLine(str);
      }
      public static Uri Concat(this Uri baseUri, params string[] uris) {
         return new Uri(baseUri, Path.Combine(uris));
      }
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
      public static Timer SetTimeout(Action<object, EventArgs> handler, int delay) {
         var timer = new Timer();
         timer.Interval = delay;
         timer.Enabled = true;
         timer.Tick += new EventHandler((sender, e) => timer.Stop());
         timer.Tick += new EventHandler(handler);
         timer.Start();
         return timer;
      }
      public static bool IsValidPath(this string path) {
         bool bOk = false;
         try { new FileInfo(path); bOk = true; }
         catch (ArgumentException) { }
         catch (PathTooLongException) { }
         catch (NotSupportedException) { }
         return bOk;
      }
      static int CtorCount = 0;
      // TODO: do something for blank struct
      public static DynamicMethod MakeMethod(this ConstructorInfo ctor) {
         var paramTypes = ctor.GetParameters().Select(para => para.ParameterType).ToArray();
         var method = new DynamicMethod(
            $"New{ctor.Name}_{CtorCount++}", ctor.DeclaringType, paramTypes.ToArray(),
            ctor.DeclaringType.Module
         );
         ILGenerator gen = method.GetILGenerator();
         for (var i = 0; i < paramTypes.Length; i++)
            gen.Emit(OpCodes.Ldarg, i);
         gen.Emit(OpCodes.Newobj, ctor);
         gen.Emit(OpCodes.Ret);
         return method;
      }
      public delegate object GetHandler(object target = null);
      static int PropCount = 0;
      public static GetHandler MakeGetDelegate(this PropertyInfo property) {
         var isStatic = property.GetMethod.IsStatic;
         var isValueType = property.DeclaringType.IsValueType;
         var isReturnValueType = property.PropertyType.IsValueType;
         var method = new DynamicMethod(
            $"Get_{property.DeclaringType.Name}.{property.Name}_{PropCount++}",
            typeof(object), new Type[] { typeof(object) },
            property.DeclaringType.Module
         );
         ILGenerator gen = method.GetILGenerator();
         if (!isStatic) {
            gen.Emit(OpCodes.Ldarg_0);
            if (isValueType) {
               gen.Emit(OpCodes.Unbox, property.DeclaringType);
               gen.Emit(OpCodes.Call, property.GetMethod);
            }
            else {
               gen.Emit(OpCodes.Callvirt, property.GetMethod);
            }
         }
         else {
            gen.Emit(OpCodes.Call, property.GetMethod);
         }
         if (isReturnValueType)
            gen.Emit(OpCodes.Box, property.PropertyType);
         gen.Emit(OpCodes.Ret);
         return (GetHandler)method.CreateDelegate(typeof(GetHandler));
      }
      public static GetHandler MakeDelegate(this FieldInfo field) {
         var isStatic = field.IsStatic;
         var isValueType = field.DeclaringType.IsValueType;
         var isReturnValueType = field.FieldType.IsValueType;
         var method = new DynamicMethod(
            $"Read_{field.DeclaringType.Name}.{field.Name}_{PropCount++}",
            typeof(object), new Type[] { typeof(object) },
            field.DeclaringType.Module
         );
         ILGenerator gen = method.GetILGenerator();
         if (!isStatic) {
            gen.Emit(OpCodes.Ldarg_0);
            if (isValueType)
               gen.Emit(OpCodes.Unbox_Any, field.DeclaringType);
            gen.Emit(OpCodes.Ldfld, field);
         }
         else {
            gen.Emit(OpCodes.Ldsfld, field);
         }
         if (isReturnValueType)
            gen.Emit(OpCodes.Box, field.FieldType);
         gen.Emit(OpCodes.Ret);
         return (GetHandler)method.CreateDelegate(typeof(GetHandler));
      }
      public static FastInvokeHandler MakeDelegate(this MethodInfo method) {
         if (method is DynamicMethod)
            return Harmony.MethodInvoker.GetHandler((DynamicMethod)method, method.Module);
         else
            return Harmony.MethodInvoker.GetHandler(method);
      }
   }
}
