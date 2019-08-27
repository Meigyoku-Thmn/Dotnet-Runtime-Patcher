//css_nuget Crc32.NET
//css_nuget Newtonsoft.Json
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Force.Crc32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MakeFileList {
   class Program {
      static void CreateFileList(string DirPath, string FilterFilePath, string OutputFilePath) {
         var filters = File.ReadAllLines(FilterFilePath)
            .Concat(new string[] { @"files\.jsonc" })
            .Select(str => str.Trim())
            .Where(str => str.Length > 0 && str[0] != '#')
            .Select(str => new Regex(Regex.Escape(DirPath) + str, RegexOptions.Compiled))
            .ToArray()
            ;
         var filePaths = Directory.EnumerateFiles(DirPath, "*", SearchOption.AllDirectories)
            .Select(filePath => filePath.Replace('\\', '/'))
            .OrderBy(filePath => filePath)
            .Where(
               filePath => filters.All(
                  filter => !filter.IsMatch(filePath)
               )
            )
            ;
         var output = new StringBuilder();
         output.AppendLine("{");
         output.AppendLine("   // CRC32 Checksum for each file, we use this as a way to know if there is any changed file.");
         foreach (var filePath in filePaths) {
            var checksum = Crc32Algorithm.Compute(File.ReadAllBytes(filePath));
            var shortFilePath = filePath.Remove(0, DirPath.Length);
            output.AppendLine($"   \"{shortFilePath.Replace("\\", "/")}\": {checksum},");
         }
         output.Append("}");
         File.WriteAllText(OutputFilePath, output.ToString(), Encoding.UTF8);
      }
      static void Main(string[] args) {
         var serverCfgPath = @"server.jsonc";
         var serverCfg = JObject.Parse(File.ReadAllText(serverCfgPath));
         foreach (JProperty package in serverCfg["packages"]) {
            Console.WriteLine("Update file list for: " + package.Name);
            var DirPath = Path.Combine(Directory.GetCurrentDirectory(), package.Name) + Path.DirectorySeparatorChar;
            CreateFileList(DirPath, Path.Combine(DirPath, ".fileignore"), Path.Combine(DirPath, "files.jsonc"));
         }
      }
   }
}
