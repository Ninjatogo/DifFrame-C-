using System;
using Difframe;

namespace ConsoleTester
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Please supply file directory");
            var fileDirectory = "SampleFrames-Mob_Psycho_100";//Console.ReadLine();
            var engine = new ProcessEngine(false, fileDirectory);
            //engine.IdentifyDifferences();
            //Value = {CV_8UC1}
            engine.MatConvertTest();
        }
    }
}
