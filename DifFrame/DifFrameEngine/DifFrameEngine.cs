using System;
using System.Diagnostics;

namespace DifFrameEngine
{
    public class DifFrameEngine
    {
        private FrameProcessor _frameProcessor;
        private string _input_frames_filepath;
        private double _similarity_threshold;

        public DifFrameEngine(string input_frames_filepath, double similarity_threshold = 34.50)
        {
            _input_frames_filepath = input_frames_filepath;
            _similarity_threshold = similarity_threshold;
            _frameProcessor = new FrameProcessor(input_frames_filepath, similarity_threshold);
        }

        public void ProcessVideoCompleteLoop()
        {
            Console.WriteLine($"Processing video frames in {_input_frames_filepath} with PSNR threshold of {_similarity_threshold}");

            var sw = Stopwatch.StartNew();
            _frameProcessor.IdentifyDifferences();
            _frameProcessor.GenerateAndSaveDeltaFrame();
            Console.WriteLine($"Performance: {99 / sw.Elapsed.TotalSeconds} fps");

            Console.ReadLine();
        }
    }
}
