using System;
using ProcessNode;

namespace ConsoleTester
{
    class Program
    {
        static void Main(string[] args)
        {
            string modeChoice;
            while (true)
            {
                Console.WriteLine("Start node in (s) server mode or (c) client mode?");
                modeChoice = Console.ReadLine();
                if(modeChoice == "s" || modeChoice == "c")
                {
                    break;
                }
            }

            if (modeChoice == "s")
            {
                while (true)
                {
                    Console.WriteLine("Please supply file directory");
                    string fileDirectory = Console.ReadLine();
                    // Check that file directory is valid

                    // If valid, break from loop
                    if (fileDirectory.Trim().Length > 0)
                    {
                        break;
                    }
                }
            }
            
            var node = new Node(modeChoice == "s");
            node.StartConnection();
        }
    }
}