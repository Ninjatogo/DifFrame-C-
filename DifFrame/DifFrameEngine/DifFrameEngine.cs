using System;

namespace DifFrameEngine
{
    public class DifFrameEngine
    {
        private FrameProcessor _frameProcessor;
        private string _input_frames_filepath;
        private double _similarity_threshold;

        public DifFrameEngine(string input_frames_filepath, double similarity_threshold = 0.92)
        {
            _input_frames_filepath = input_frames_filepath;
            _similarity_threshold = similarity_threshold;
            _frameProcessor = new FrameProcessor(input_frames_filepath, similarity_threshold);
        }

        public void ProcessVideoCompleteLoop()
        {
            Console.WriteLine($"Processing video frames in {_input_frames_filepath} with SSIM threshold of {_similarity_threshold}");
            _frameProcessor.IdentifyDifferences();
            _frameProcessor.GenerateAndSaveDeltaFrame();
        }
    }
}
