using System;
using System.Collections.Generic;
using System.Linq;

namespace maildir_splitter
{
    class Program
    {
        static string theLock = "";
        static void Log(string s)
        {
            lock(theLock)
            {
                Console.WriteLine(s);
                System.IO.File.AppendAllText("log.log", s + "\n");
            }
        }
        static void Main(string[] args)
        {
            // maildir
            string maildir = Environment.GetEnvironmentVariable("MAILDIR");
            if (string.IsNullOrEmpty(maildir) && args.Length > 0) maildir = args[0];
            if (string.IsNullOrEmpty(maildir)) throw new Exception("No MAILDIR env variable found, no argument to program - aborting!");
            if (!System.IO.Directory.Exists(maildir)) throw new Exception("Maildir path invalid!");
            else Log($"Using Maildir: {maildir}");

            // folder(s) on which to operate
            string folder = Environment.GetEnvironmentVariable("MAILFOLDERS");
            if (string.IsNullOrEmpty(folder) && args.Length > 1) folder = args[1];
            if (string.IsNullOrEmpty(folder)) folder = "cur,new"; // my defaults
            foreach (string folderName in folder.Split(','))
                ProcessFolder(maildir, folderName.Trim());
        }
        static void ProcessFolder(string maildir, string folderName)
        {
            string folderPath = System.IO.Path.Combine(maildir, folderName);
            if (!System.IO.Directory.Exists(folderPath)) throw new Exception($"Folder effective path {folderPath} invalid!");
            else Log($"Scanning folder {folderPath} for files...");

            //// cheat file of known files - useful if you have 1m+ files!
            //string[] files = null;
            //if (args.Length > 2)
            //    files = System.IO.File.ReadAllLines(args[2]);
            //else
            string[] files = System.IO.Directory.GetFiles(folderPath);

            // cache of subdirs we've created, so we can run quickly
            List<string> knownSubDirs = new List<string>();

            for (int i = 0; i < files.Length; i++)
                ProcessFile(maildir, folderPath, files[i], newDir =>
                {
                    if (!knownSubDirs.Contains(newDir))
                    {
                        System.IO.Directory.CreateDirectory(newDir);
                        knownSubDirs.Add(newDir);
                    }
                }, i, files.Length);
        }
        static void ProcessFile(string maildir, string folderPath, string filePath, Action<string> PreMove, int i, int filesLength)
        {
            string logPrefix = $"{i + 1}/{filesLength}: ";
            try
            {
                string fullPathFixed = filePath;
                if (!System.IO.Path.IsPathRooted(fullPathFixed))
                    fullPathFixed = System.IO.Path.Combine(folderPath, fullPathFixed); // prepend working dir path to this "just file name"
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

