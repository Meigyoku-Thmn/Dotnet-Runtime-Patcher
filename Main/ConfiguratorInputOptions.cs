using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RuntimePatcher {
   public class ConfiguratorInputOptions {
      [Option('s', "specify", Required = false, HelpText = "Specify package and server url.")]
      public bool Specify { get; set; }
      [Option("id", Required = false, HelpText = "Package id.")]
      public string Id { get; set; }
      [Option("name", Required = false, HelpText = "Package name.")]
      public string Name { get; set; }
      [Option("url", Required = false, HelpText = "Package server url.")]
      public string ServerUrl { get; set; }
      [Option("path", Required = false, HelpText = "Target path.")]
      public string TargetPath { get; set; }
      [Option("shrLock", Required = false, HelpText = "If you pass a correct mutex lock, the Configurator will not aquire any mutex.")]
      public string SharedLock { get; set; }
   }
}
