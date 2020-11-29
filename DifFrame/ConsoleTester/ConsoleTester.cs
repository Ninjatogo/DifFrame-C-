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
                modeChoice = AnsiConsole.Ask<string>("Start node in [green](s) server mode[/] or [green](c) client mode[/]?");
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
                    fileDirectory = AnsiConsole.Ask<string>("Please supply [yellow]file directory[/]:");
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