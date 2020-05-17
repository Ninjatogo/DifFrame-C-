using System;
using DifFrameEngine;

namespace ConsoleTester
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("DifFrame engine starting.");
            var engine = new DifFrameEngine.DifFrameEngine("SampleFrames-Mob_Psycho_100");
            engine.ProcessVideoCompleteLoop();
        }
    }
}
