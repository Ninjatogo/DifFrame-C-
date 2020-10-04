using System;
using System.IO;
using ProcessNode;

namespace ConsoleTester
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("DifFrame Node Started.");

            Console.Write("Create server (s) or client (c): ");
            var choice = Console.ReadLine();
            while((choice != "s") && (choice != "c"))
            {
                Console.WriteLine("Please enter valid choice for node type: server (s) or client (c).");
                choice = Console.ReadLine();
            }

            var node = new Node(choice == "s");
            if (choice == "s")
            {
                string projectDirectory;
                while (true)
                {
                    Console.WriteLine("Please supply project directory.");
                    projectDirectory = Console.ReadLine();

                    // Check that input is valid length
                    if (projectDirectory.Trim().Length <= 0)
                    {
                        continue;
                    }

                    // Check that path is accessible
                    if (Directory.Exists(projectDirectory) == false)
                    {
                        Console.WriteLine("Directory either doesn't exist, or you don't have permissions to access it.");
                        continue;
                    }
                    else { break; }
                }

                Console.WriteLine("Starting server node...");
                node.StartConnection(projectDirectory);
            }
            else
            {
                Console.WriteLine("Starting client node...");
                node.StartConnection();
            }

        }
    }
}
