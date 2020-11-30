using System;
using ProcessNode;
using Spectre.Console;

namespace ConsoleTester
{
    class Program
    {
        static void Main(string[] args)
        {
            string modeChoice;
            while (true)
            {
                modeChoice = AnsiConsole.Ask<string>("Start node in [blue](s) server mode[/] or [blue](c) client mode[/]?");
                if (modeChoice == "s" || modeChoice == "c")
                {
                    break;
                }
            }

            var fileDirectory = "";
            if (modeChoice == "s")
            {
                while (true)
                {
                    fileDirectory = AnsiConsole.Ask<string>("Please supply [blue]file directory[/]:");
                    // Check that file directory is valid

                    // If valid, break from loop
                    if (fileDirectory.Trim().Length > 0)
                    {
                        break;
                    }
                }
            }
            
            var node = new Node(modeChoice == "s", fileDirectory);
            node.StartConnection();
        }
    }
}