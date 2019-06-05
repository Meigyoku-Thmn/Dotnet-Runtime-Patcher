//css_args -provider:%CSSCRIPT_DIR%\lib\CSSRoslynProvider.dll
//css_ref %CSSCRIPT_DIR%\lib\Bin\Roslyn\System.ValueTuple.dll
//css_nuget Crc32.NET
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Force.Crc32;

namespace MakeFileList {
   class Program {
      static string DirPath = Directory.GetCurrentDirectory() + '\\';
      static string FilterFilePath = Path.Combine(DirPath, ".fileignore");
      static string OutputFilePath = Path.Combine(DirPath, "files.jsonc");
      static void Main(string[] args) {
         var filters = File.ReadAllLines(FilterFilePath)
            .Concat(new string[] { @"files\.jsonc" })
            .Select(str => str.Trim())
            .Where(str => str.Length > 0 && str[0] != '#')
            .Select(str => new Regex(Regex.Escape(DirPath) + str, RegexOptions.Compiled))
            .ToArray()
            ;
         var filePaths = Directory.EnumerateFiles(DirPath, "*", SearchOption.AllDirectories)
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
   }
}
