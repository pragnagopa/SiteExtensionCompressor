using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Compressor
{
    class Program
    {
        public static Dictionary<string, List<string>> files = new Dictionary<string, System.Collections.Generic.List<string>>();
        public static Dictionary<string, long> sizes = new Dictionary<string, long>();
        public static long processIndex = 0;
        public static long totalSize = 0;

        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode)]
        static extern bool CreateHardLink(
          string lpFileName,
          string lpExistingFileName,
          IntPtr lpSecurityAttributes
          );

        public static string RepoFolder = "_reponoWorkers";

        static void Main(string[] args)
        {
            RepoFolder = "\\_repotest";
            Directory.CreateDirectory(RepoFolder);
            string testFilePath = @"C:\pgopa\SiteExtensionsCompressTEstNoWorkers\2.0.12961\32bit\appsettings.json";
            string testExpectedFileContent = File.ReadAllText(testFilePath);

            foreach (var topFolder in args)
                ProcessFolder(topFolder);

            foreach (var entry in files.Where(x => x.Value.Count > 1).Select(x => x))
            {
                string newFileName = Path.Combine(RepoFolder, entry.Key.Replace("/", "-")) + Path.GetExtension(entry.Value[0]);
                if (!File.Exists(newFileName)) File.Copy(entry.Value[0], newFileName, true);

                int success = 0;
                foreach (var subFile in entry.Value)
                {
                    try
                    {
                        File.SetAttributes(subFile, FileAttributes.Normal);
                        Console.WriteLine($"Deleting :{subFile}");
                        File.Delete(subFile);

                        Console.WriteLine($"{subFile} <-> {newFileName}");
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
            //VerifyReadingFileBeforeAndAfterCompress(testExpectedFileContent, testFilePath);
        }

        private static void ProcessFolder(string topFolder)
        {
            Console.WriteLine($"Compressing {topFolder}...");

            DirectoryInfo di = new DirectoryInfo(topFolder);
            ScanFolder(di);
            PrintSavings();
        }

        private static void PrintSavings()
        {
            long saved = 0;
            foreach (var entry in files.Where(x => x.Value.Count > 1).Select(x => x))
            {
                saved += (entry.Value.Count - 1) * sizes[entry.Key];
            }

            Console.WriteLine($"Current savings: {saved} bytes, {saved / 1024 / 1024} MBs, totalSize = {totalSize / 1024 / 1024} MB, savings = {(double)saved / totalSize * 100}%");
        }

        private static void ScanFolder(DirectoryInfo source)
        {
            try
            {
                // Copy each file into the new directory.
                foreach (FileInfo fi in source.GetFiles())
                {
                    long fileLen = fi.Length;
                    totalSize += fileLen;

                    //if (fi.Extension != ".exe" && fi.Extension != ".dll") continue;

                    string hash = GetHash(fi.FullName);
                    if (!files.ContainsKey(hash))
                    {
                        files.Add(hash, new List<string>());
                        sizes.Add(hash, fileLen);
                    }

                    if (processIndex++ % 250 == 0) PrintSavings();

                    files[hash].Add(fi.FullName);
                }

                // Copy each subdirectory using recursion.
                foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
                {
                    ScanFolder(diSourceSubDir);
                }
            }
            catch (Exception)
            {
                // silent drop, let's scan through what we can 
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

        private static bool VerifyReadingFileBeforeAndAfterCompress(string expectedFileContent, string filePath)
        {
            string fileContent = File.ReadAllText(filePath);
            if (fileContent == expectedFileContent)
            {
                return true;
            }
            return false;
        }

    }
}