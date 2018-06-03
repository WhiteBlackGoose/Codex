using System;
using System.Collections;

namespace Codex.Automation.Workflow
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("I'm here");

            foreach(DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                Console.WriteLine($"{entry.Key}={entry.Value}");
            }
        }

        private static void DownloadFile(string url, string destination)
        {
        }
    }
}
