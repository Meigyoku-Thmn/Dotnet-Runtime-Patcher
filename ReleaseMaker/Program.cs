using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ReleaseMaker {
   class Program {
      static void Main(string[] args) {
         var dllFilePaths = new SortedSet<string>(Directory.GetFiles(".", "*.dll", SearchOption.TopDirectoryOnly));
         var exeFilePaths = new SortedSet<string>(Directory.GetFiles(".", "*.exe", SearchOption.TopDirectoryOnly).Where(str => Path.GetFileName(str) != "ReleaseMaker.exe"));
         var distDirPath = Directory.CreateDirectory("../../Distribution").FullName;
         var distLibPath = Directory.CreateDirectory("../../Distribution/Lib").FullName;
         var configFilePaths = new SortedSet<string>(Directory.GetFiles(".", "*.config", SearchOption.TopDirectoryOnly).Where(str => Path.GetFileName(str) != "ReleaseMaker.exe.config"));
         foreach (var dllFilePath in dllFilePaths) {
            Console.WriteLine(Path.GetFileName(dllFilePath));
            File.Copy(dllFilePath, Path.Combine(distLibPath, dllFilePath), true);
         }
         Console.WriteLine();
         foreach (var exeFilePath in exeFilePaths) {
            Console.WriteLine(Path.GetFileName(exeFilePath));
            File.Copy(exeFilePath, Path.Combine(distDirPath, exeFilePath), true);
         }
         Console.WriteLine();
         foreach (var configFilePath in configFilePaths) {
            Console.WriteLine(Path.GetFileName(configFilePath));
            File.Copy(configFilePath, Path.Combine(distDirPath, configFilePath), true);
         }
         Console.WriteLine();
         var RoslynCompilerDirPath = Path.GetFullPath(
            Environment.ExpandEnvironmentVariables(
               @"%USERPROFILE%\.nuget\packages\microsoft.net.compilers\2.2.0\tools"));
         Console.WriteLine("RoslynCompiler");
         DirectoryCopy(RoslynCompilerDirPath, Path.Combine(distDirPath, "RoslynCompiler"), true, true);
         Console.WriteLine("Done!");
         Console.ReadLine();
      }
      private static void DirectoryCopy(string sourceDirName, string destDirName, bool overwrite, bool copySubDirs) {
         // Get the subdirectories for the specified directory.
         DirectoryInfo dir = new DirectoryInfo(sourceDirName);

         if (!dir.Exists) {
            throw new DirectoryNotFoundException(
                "Source directory does not exist or could not be found: "
                + sourceDirName);
         }

         DirectoryInfo[] dirs = dir.GetDirectories();
         // If the destination directory doesn't exist, create it.
         if (!Directory.Exists(destDirName)) {
            Directory.CreateDirectory(destDirName);
         }

         // Get the files in the directory and copy them to the new location.
         FileInfo[] files = dir.GetFiles();
         foreach (FileInfo file in files) {
            string temppath = Path.Combine(destDirName, file.Name);
            Console.WriteLine(temppath);
            file.CopyTo(temppath, overwrite);
         }

         // If copying subdirectories, copy them and their contents to new location.
         if (copySubDirs) {
            foreach (DirectoryInfo subdir in dirs) {
               string temppath = Path.Combine(destDirName, subdir.Name);
               DirectoryCopy(subdir.FullName, temppath, overwrite, copySubDirs);
            }
         }
      }
   }
}
