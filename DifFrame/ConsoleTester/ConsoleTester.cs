using System;
using ProcessNode;

namespace ConsoleTester
{
    class Program
    {
        static void Main(string[] args)
        {
            var modeChoice = "q";
            while (true)
            {
                Console.WriteLine("Start node in (s) server mode or (c) client mode?");
                modeChoice = Console.ReadLine();
                if(modeChoice == "s" || modeChoice == "c")
                {
                    break;
                }
            }

            var fileDirectory = "";
            if(modeChoice == "s")
            {
                while (true)
                {
                    Console.WriteLine("Please supply file directory");
                    fileDirectory = Console.ReadLine();
                    // Check that file directory is valid

                    // If valid, break from loop
                }
            }
            
            var node = new Node(modeChoice == "s");
            if (modeChoice == "s")
            {
                node.StartConnection(fileDirectory);
            }
            else
            {
                node.StartConnection();
            }
        }
    }
}