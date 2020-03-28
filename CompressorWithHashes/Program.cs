using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace CompressorWithHashes
{
    class Program
    {
        public static Dictionary<string, long> sizes = new Dictionary<string, long>();
        public static List<string> FoldersToProcess = new List<string>();
        public static string HardLinksDir;
        public static bool RefreshHardLinksDir;
        public static long processIndex = 0;
        public static long totalSize = 0;

        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode)]
        static extern bool CreateHardLink(
          string lpFileName,
          string lpExistingFileName,
          IntPtr lpSecurityAttributes
          );

        static void RunOptions(InputOptions opts)
        {
            foreach (var input in opts.FunctionsRuntimeVersions)
            {
                string siteExtensionFolderPath = Environment.ExpandEnvironmentVariables(Path.Combine(@"%programFiles(x86)%\SiteExtensions\Functions", input));
                FoldersToProcess.Add(siteExtensionFolderPath);
                Console.WriteLine(siteExtensionFolderPath);
            }
            HardLinksDir = opts.HardlinksDir;
            RefreshHardLinksDir = opts.ForceRefresh;
            //handle options

        }
        static void HandleParseError(IEnumerable<Error> errs)
        {
            //handle errors
        }

        static void Main(string[] args)
        {
            // Set up. Parase cmd line args
            CommandLine.Parser.Default.ParseArguments<InputOptions>(args)
             .WithParsed(RunOptions)
             .WithNotParsed(HandleParseError);
            string expandedHandLinkDir = Environment.ExpandEnvironmentVariables(HardLinksDir);
            if (RefreshHardLinksDir && Directory.Exists(expandedHandLinkDir))
            {
                Directory.Delete(HardLinksDir, true);
            }
            if (!Directory.Exists(Environment.ExpandEnvironmentVariables(expandedHandLinkDir)))
            {
                Console.WriteLine($"Creating.. folder: {expandedHandLinkDir}");
                var x = Directory.CreateDirectory(expandedHandLinkDir);
            }

            foreach (var topFolder in FoldersToProcess)
            {
                if (!Directory.Exists(topFolder))
                {
                    continue;
                }
                // TODO: Generate as part of build 

                Dictionary<string, List<string>> fileHashDictionary = ComputeHashes(topFolder);

                using (StreamWriter hashesFile =
            new StreamWriter(Path.Combine(topFolder, "Hashes.txt")))
                {
                    foreach (string fileHash in fileHashDictionary.Keys)
                    {
                        foreach (string fileName in fileHashDictionary[fileHash])
                        {
                            hashesFile.WriteLine($"Hash:{fileHash} FileName:{fileName.Replace(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "%programfiles(x86)%")}");
                        }
                    }
                }
            }
            // Read hashes file from each folder to process and build dictionary
            // Read Hashes from file and build dictionary
            Console.WriteLine($"Building hashes from files.. at {DateTime.Now}");
            Dictionary<string, List<string>> filesComputedFromHashesFile = new Dictionary<string, List<string>>();
            foreach (var topFolder in FoldersToProcess)
            {
                string hashesTextFile = Path.Combine(topFolder, "Hashes.txt");
                if (!File.Exists(hashesTextFile))
                {
                    continue;
                }
                string[] fileHashes = File.ReadAllLines(hashesTextFile);
                foreach (string line in fileHashes)
                {
                    string[] parsedLine = line.Split(' ');
                    string hash = parsedLine[0].Replace("Hash:", "").Trim();
                    string fileName = Environment.ExpandEnvironmentVariables(parsedLine[1].Replace("FileName:", "").Trim());
                    if (filesComputedFromHashesFile.ContainsKey(hash))
                    {
                        filesComputedFromHashesFile[hash].Add(fileName);
                    }
                    else
                    {
                        filesComputedFromHashesFile[hash] = new List<string>();
                        filesComputedFromHashesFile[hash].Add(fileName);
                    }
                }
            }
            // Create Hardlinks and delete files
            Console.WriteLine($"Creating hard links..at {DateTime.Now}");
            CreateHardlinks(expandedHandLinkDir, filesComputedFromHashesFile);
            Console.WriteLine($"done....at {DateTime.Now}");
        }

        private static void CreateHardlinks(string expandedHandLinkDir, Dictionary<string, List<string>> filesComputedFromHashesFile)
        {
            foreach (var entry in filesComputedFromHashesFile.Where(x => x.Value.Count > 1).Select(x => x))
            {
                string newFileName = Path.Combine(expandedHandLinkDir, entry.Key.Replace("/", "-")) + Path.GetExtension(entry.Value[0]);
                string existingFileName = Environment.ExpandEnvironmentVariables(entry.Value[0]);
                if (!File.Exists(newFileName))
                {
                    Console.WriteLine($"Copying full path {Path.GetFullPath(existingFileName)} to {Path.GetFullPath(newFileName)}");
                     File.Copy(existingFileName, newFileName, true);
                }

                int success = 0;
                foreach (var subFile in entry.Value)
                {
                    try
                    {
                        File.SetAttributes(subFile, FileAttributes.Normal);
                        //Console.WriteLine($"Deleting :{subFile}");
                        File.Delete(subFile);

                        //Console.WriteLine($"{subFile} <-> {newFileName}");
                        bool hardLinkResult = CreateHardLink(subFile, newFileName, IntPtr.Zero);

                        success++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"{subFile}: {ex.Message}");
                    }
                }

                if (success == 0) File.Delete(newFileName);
            }
        }

        private static Dictionary<string, List<string>> ComputeHashes(string topFolder)
        {
            Console.WriteLine($"Compressing {topFolder}...");

            DirectoryInfo di = new DirectoryInfo(topFolder);
            Dictionary<string, List<string>> fileHashes = ScanFolderAndComputeHashes(di, new Dictionary<string, List<string>>());
            PrintSavings(fileHashes);
            return fileHashes;
        }

        private static void PrintSavings(Dictionary<string, List<string>> files)
        {
            long saved = 0;
            foreach (var entry in files.Where(x => x.Value.Count > 1).Select(x => x))
            {
                saved += (entry.Value.Count - 1) * sizes[entry.Key];
            }

            Console.WriteLine($"Current savings: {saved} bytes, {saved / 1024 / 1024} MBs, totalSize = {totalSize / 1024 / 1024} MB, savings = {(double)saved / totalSize * 100}%");
        }

        private static Dictionary<string, List<string>> ScanFolderAndComputeHashes(DirectoryInfo source, Dictionary<string, List<string>> files)
        {
            try
            {
                // Compute File Hash for each file in the directory
                foreach (FileInfo fi in source.GetFiles())
                {
                    long fileLen = fi.Length;
                    totalSize += fileLen;

                    string hash = GetHash(fi.FullName);
                    if (!files.ContainsKey(hash))
                    {
                        files.Add(hash, new List<string>());
                    }
                    if (!sizes.ContainsKey(hash))
                    {
                        sizes.Add(hash, fileLen);
                    }

                    if (processIndex++ % 250 == 0) PrintSavings(files);

                    files[hash].Add(fi.FullName);
                }

                // Copy each subdirectory using recursion.
                foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
                {
                    ScanFolderAndComputeHashes(diSourceSubDir, files);
                }
                return files;
            }
            catch (Exception ex)
            {
                // silent drop, let's scan through what we can 
                // ignore folder
                // TODO: improve
                return null;
            }
        }

        private static string GetHash(string filename)
        {
            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(filename))
            {
                return Convert.ToBase64String(md5.ComputeHash(stream));
            }
        }
    }
}
