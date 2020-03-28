using CommandLine;
using System;
using System.Collections.Generic;
using System.Text;

namespace CompressorWithHashes
{
    public class InputOptions
    {
        [Option('r', "runtimeVersion", Required = true, HelpText = "Input runtime versions to process")]
        public IEnumerable<string> FunctionsRuntimeVersions { get; set; }

        // Omitting long name, defaults to name of property, ie "--verbose"
        [Option(
          'h',
          "hardlinksDir",
          Default = @"%programfiles(x86)%\FunctionsHardLinks",
          HelpText = "Directory with Hardlinks.")]
        public string HardlinksDir { get; set; }

        [Option('f',
          "forceRefresh",
          Default = false,
          HelpText = "Clean and rebuild hardlinks directory")]
        public bool ForceRefresh { get; set; }
    }
}
