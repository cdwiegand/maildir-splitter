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
        static void Main(string[] args)
        {
            State state = new State(args);

            // test config
            if (string.IsNullOrEmpty(state.maildir)) throw new Exception("No MAILDIR env variable found, no argument to program - aborting!");
            if (!System.IO.Directory.Exists(state.maildir)) throw new Exception("Maildir path invalid!");
            else Log($"Using Maildir: {state.maildir}");

            // special case: give me a single file, and I'll process as if it's in a subdir of a MAILDIR (./cur/somethinghere.12345 -> ./2019/11/somethinghere.12345 or somethinghere.12345 -> ../2019/11/somethinghere.12345)
            if (!string.IsNullOrEmpty(state.file))
            {
                if (System.IO.File.Exists(state.file))
                    ProcessFile(state, EnsurePathFullyRooted(System.Environment.CurrentDirectory, state.file), newDir => {
                        EnsureDirectoryExists(newDir);
                        state.AddDedupeDir(newDir);
                    }, 1, 1);
                else
                    throw new Exception("Invalid file specified: " + file);
            }
            else if (!string.IsNullOrEmpty(index))
            {
                string[] files = System.IO.File.ReadAllLines(index);
                for (int i = 0; i < files.Length; i++)
                    ProcessFile(state, EnsurePathFullyRooted(System.Environment.CurrentDirectory, files[i]), newDir => {
                        EnsureDirectoryExists(newDir);
                        state.AddDedupeDir(newDir);
                    }, i, files.Length);
            }
            else
                do
                {
                    foreach (string folderName in folder.Split(','))
                        ProcessFolder(state, folderName.Trim());
                    System.Threading.Thread.Sleep(sleepSeconds * 1000); // I hate crontab in Docker, not reliable...
                    maxRuns--;
                }
                while (sleepSeconds > 0 && maxRuns > 0);

            if (state.runFdupes) {
                foreach (string dir in state.dedupeDirs) {                    
                    Process.Start(new ProcessStartInfo
                    {
                        WorkingDirectory = System.IO.Path.Combine(state.maildir, folderName),
                        FileName = "fdupes",
                        Arguments = "-dNr " + dir
                    }).WaitForExit();
                }
            }
        }
        static void ProcessFolder(State state, string folderName)
        {
            string folderPath = System.IO.Path.Combine(state.maildir, folderName);
            if (!System.IO.Directory.Exists(folderPath)) throw new Exception($"Folder effective path {folderPath} invalid!");
            else Log($"Scanning folder {folderPath} for files...");

            string[] files;
            //// cheat file of known files - useful if you have 1m+ files!
            if (state.useFind)
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    WorkingDirectory = System.IO.Path.Combine(state.maildir, folderName),
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
                ProcessFile(state, EnsurePathFullyRooted(folderPath, files[i]), 
                    newDir => {
                        EnsureDirectoryExists(newDir);
                        state.AddDedupeDir(newDir);
                    },
                    i, files.Length);            
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

        static void ProcessFile(State state, string filePath, Action<string> PreMove, int i, int filesLength)
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
                string dayFixed = (theDate.Day < 10 ? "0" : "") + theDate.Day.ToString();
                string newDir = System.IO.Path.Combine(state.maildir, theDate.Year.ToString(), monthFixed, dayFixed);
                string newFile = System.IO.Path.Combine(newDir, System.IO.Path.GetFileName(fullPathFixed));
                PreMove?.Invoke(newDir);
                System.IO.File.Move(fullPathFixed, newFile);
                Log(logPrefix + newFile);
            }
            catch (Exception ex) { Log($"!! {logPrefix} {ex.Message}"); }
        }
    }
}

