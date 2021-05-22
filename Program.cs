using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace maildir_splitter
{
    class Program
    {
        static string theLock = "";
        static void Log(string s)
        {
            lock (theLock)
            {
                Console.WriteLine(s);
                System.IO.File.AppendAllText("log.log", s + "\n");
            }
        }
        static string GetArgIfPresent(string[] args, string prefixedOptionWithColon)
        {
            string found = null;
            if (args != null && args.Length > 0)
            {
                string testVal = args.FirstOrDefault(p => p.StartsWith(prefixedOptionWithColon));
                if (!string.IsNullOrEmpty(testVal)) found = testVal.Substring(prefixedOptionWithColon.Length);
            }
            return found;
        }
        static bool GetIfArgPresent(string[] args, string option)
        {
            return args != null && args.Length > 0 && args.Contains(option);
        }
        static void Main(string[] args)
        {
            // maildir
            string maildir = Environment.GetEnvironmentVariable("MAILDIR") ?? GetArgIfPresent(args, "-maildir:");

            // folder(s) on which to operate
            string folder = Environment.GetEnvironmentVariable("MAILFOLDERS") ?? GetArgIfPresent(args, "-folders:") ?? "cur,new"; // my defaults

            string file = GetArgIfPresent(args, "-file:");
            string index = GetArgIfPresent(args, "-index:"); // lets you cheat and use result of `find -type f .` in your cur or new folder
            bool useFind = GetIfArgPresent(args, "-find");

            // test config
            if (string.IsNullOrEmpty(maildir)) throw new Exception("No MAILDIR env variable found, no argument to program - aborting!");
            if (!System.IO.Directory.Exists(maildir)) throw new Exception("Maildir path invalid!");
            else Log($"Using Maildir: {maildir}");

            // special case: give me a single file, and I'll process as if it's in a subdir of a MAILDIR (./cur/somethinghere.12345 -> ./2019/11/somethinghere.12345 or somethinghere.12345 -> ../2019/11/somethinghere.12345)
            if (!string.IsNullOrEmpty(file))
            {
                if (System.IO.File.Exists(file))
                    ProcessFile(maildir, EnsurePathFullyRooted(System.Environment.CurrentDirectory, file), newDir => EnsureDirectoryExists(newDir), 1, 1);
                else
                    throw new Exception("Invalid file specified: " + file);
            }
            else if (!string.IsNullOrEmpty(index))
            {
                string[] files = System.IO.File.ReadAllLines(index);
                for (int i = 0; i < files.Length; i++)
                    ProcessFile(maildir, EnsurePathFullyRooted(System.Environment.CurrentDirectory, files[i]), newDir => EnsureDirectoryExists(newDir), i, files.Length);
            }
            else
                foreach (string folderName in folder.Split(','))
                    ProcessFolder(maildir, folderName.Trim(), useFind);
        }
        static void ProcessFolder(string maildir, string folderName, bool useFind)
        {
            string folderPath = System.IO.Path.Combine(maildir, folderName);
            if (!System.IO.Directory.Exists(folderPath)) throw new Exception($"Folder effective path {folderPath} invalid!");
            else Log($"Scanning folder {folderPath} for files...");

            string[] files;
            //// cheat file of known files - useful if you have 1m+ files!
            if (useFind)
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    WorkingDirectory = System.IO.Path.Combine(maildir, folderName),
                    FileName = "find",
                    Arguments = "-type f",
                    RedirectStandardOutput = true
                };
                Process p = Process.Start(psi);
                while (!p.HasExited) { System.Threading.Thread.Sleep(1000); Console.Write("."); }
                string output = p.StandardOutput.ReadToEnd();
                files = output.Split('\n');
            }
            else
                files = System.IO.Directory.GetFiles(folderPath);


            for (int i = 0; i < files.Length; i++)
                ProcessFile(maildir, EnsurePathFullyRooted(folderPath, files[i]), newDir => EnsureDirectoryExists(newDir), i, files.Length);
        }

        static List<string> StaticCreatedDirectories = new List<string>();
        static void EnsureDirectoryExists(string newDir)
        {
            // cache of subdirs we've created, so we can run quickly
            if (!StaticCreatedDirectories.Contains(newDir))
            {
                System.IO.Directory.CreateDirectory(newDir);
                StaticCreatedDirectories.Add(newDir);
            }
        }
        static string EnsurePathFullyRooted(string folderPath, string file)
        {
            if (!System.IO.Path.IsPathRooted(file))
                file = System.IO.Path.Combine(folderPath, file); // prepend working dir path to this "just file name"
            return file;
        }
        static void ProcessFile(string maildir, string filePath, Action<string> PreMove, int i, int filesLength)
        {
            string logPrefix = $"{i + 1}/{filesLength}: ";
            try
            {
                string fullPathFixed = filePath;
                string[] contents = System.IO.File.ReadAllLines(fullPathFixed);
                string dateStr = contents.FirstOrDefault(p => p.ToLower().StartsWith("date: "));
                DateTime theDate = new DateTime(1970, 1, 1);
                if (!string.IsNullOrEmpty(dateStr) && dateStr.ToLower().StartsWith("date: "))
                {
                    dateStr = dateStr.Substring("date: ".Length).Trim();
                    if (DateTime.TryParse(dateStr, out DateTime dt1))
                        theDate = dt1;
                    else if (dateStr.Contains(":") && dateStr.LastIndexOf(' ') > dateStr.LastIndexOf(':')) // Mon, 25 Oct 2004 18:56:17 EDT doesn't parse, so fix it - I don't need timezome, so strip after time
                    {
                        int newEnd = dateStr.LastIndexOf(' ', dateStr.IndexOf(':')); // Mon, 25 Oct 2004 18:56:17
                        dateStr = dateStr.Substring(0, newEnd).Trim();
                        if (DateTime.TryParse(dateStr, out DateTime dt2))
                            theDate = dt2;
                    }
                }
                string monthFixed = (theDate.Month < 10 ? "0" : "") + theDate.Month.ToString();
                string newDir = System.IO.Path.Combine(maildir, theDate.Year.ToString(), monthFixed);
                string newFile = System.IO.Path.Combine(newDir, System.IO.Path.GetFileName(fullPathFixed));
                PreMove?.Invoke(newDir);
                System.IO.File.Move(fullPathFixed, newFile);
                Log(logPrefix + newFile);
            }
            catch (Exception ex) { Log($"!! {logPrefix} {ex.Message}"); }
        }
    }
}

