
using System;
using System.Collections.Generic;
using System.Linq;

namespace maildir_splitter
{
    class State
    {
        public string maildir { get; private set; }
        public string folder { get; private set; }
        public string file { get; private set; }
        public string index { get; private set; }
        public bool useFind { get; private set; }
        public bool runFdupes { get; private set; }
        public int sleepSeconds { get; private set; }
        public int maxRuns { get; set; }
        public List<string> dedupeDirs { get; private set; } = new List<string>();

        public State(string[] args)
        {
            // maildir
            maildir = GetEnvIfPresent("MAILDIR") ?? GetArgIfPresent(args, "-maildir:");
            if (string.IsNullOrEmpty(maildir) && System.IO.Directory.Exists("/maildir"))
                maildir = "/maildir"; // default, if it exists

            // folder(s) on which to operate
            folder = GetEnvIfPresent("MAILFOLDERS") ?? GetArgIfPresent(args, "-folders:") ?? "cur,new"; // my defaults

            file = GetArgIfPresent(args, "-file:");
            index = GetArgIfPresent(args, "-index:"); // lets you cheat and use result of `find -type f .` in your cur or new folder

            useFind = (GetIfEnvBoolean("USE_FIND") ?? GetIfArgPresent(args, "-find"));
            runFdupes = (GetIfEnvBoolean("USE_FDUPES") ?? GetIfArgPresent(args, "-fdupes"));

            sleepSeconds = int.TryParse(GetEnvIfPresent("SLEEP") ?? GetArgIfPresent(args, "-sleep:"), out int testVal1) && testVal1 > 0 ? testVal1 : 0;
            maxRuns = int.TryParse(GetEnvIfPresent("MAXRUNS") ?? GetArgIfPresent(args, "-runs:"), out int testVal2) && testVal2 > 0 ? testVal2 : int.MaxValue;
        }

        public void AddDedupeDir(string dir)
        {
            if (!dedupeDirs.Contains(dir)) dedupeDirs.Add(dir);
        }

        private static string GetEnvIfPresent(string envName) => Environment.GetEnvironmentVariable(envName);

        private static string GetArgIfPresent(string[] args, string prefixedOptionWithColon)
        {
            string found = null;
            if (args != null && args.Length > 0)
            {
                string testVal = args.FirstOrDefault(p => p.StartsWith(prefixedOptionWithColon));
                if (!string.IsNullOrEmpty(testVal)) found = testVal.Substring(prefixedOptionWithColon.Length);
            }
            return found;
        }

        static bool? GetIfEnvBoolean(string envName)
        {
            string env = Environment.GetEnvironmentVariable(envName);
            if (string.IsNullOrEmpty(env)) return null;
            if (!bool.TryParse(env, out bool b)) return null; // not bool
            return b;
        }
        static bool GetIfArgPresent(string[] args, string option) => args != null && args.Length > 0 && args.Contains(option);
    }
}