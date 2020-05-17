using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OpenCvSharp;
using OpenCvSharp.Quality;

namespace DifFrameEngine
{
    public static class MyExtensions
    {
        public static IEnumerable<string> CustomSort(this IEnumerable<string> list)
        {
            int maxLen = list.Select(s => s.Length).Max();

            return list.Select(s => new
            {
                OrgStr = s,
                SortStr = System.Text.RegularExpressions.Regex.Replace(s, @"(\d+)|(\D+)", m => m.Value.PadLeft(maxLen, char.IsDigit(m.Value[0]) ? ' ' : '\xffff'))
            })
            .OrderBy(x => x.SortStr)
            .Select(x => x.OrgStr);
        }

    }

    class FrameProcessor
    {
        private FrameCollector _frameCollector;
        private int _currentFrameIndex;
        private Mat _currentFrameData;
        private int _frameWidth;
        private int _frameHeight;
        private int _frameDivisionDimensionX;
        private int _frameDivisionDimensionY;
        private List<string> _inputFramesFileNames;
        private string _inputFrameDirectory;
        private double _similarityThreshold;
        private int _miniBatchSize;

        public FrameProcessor(string input_frame_directory, double similarity_threshold, int inMiniBatchSize = 2)
        {
            _frameCollector = new FrameCollector();
            _currentFrameIndex = -10;
            _currentFrameData = new Mat();
            _inputFramesFileNames = new List<string>();
            _inputFrameDirectory = input_frame_directory;
            _similarityThreshold = similarity_threshold;
            _miniBatchSize = inMiniBatchSize;
            _frameWidth = 1;
            _frameHeight = 1;
            _frameDivisionDimensionX = 1;
            _frameDivisionDimensionY = 1;
            LoadFilePaths();
            SetDicingRate();
        }

        /*
        class FrameProcessor:
            frameCollector: FrameCollector

            def __init__(self, in_frame_input_directory, in_similarity_threshold):
                self.currentFrameIndex = -10
                self.currentFrameData = np.array([[0, 0, 0], [0, 0, 0]], np.uint8)
                self.frameCollector = FrameCollector()
                self.frameInputDirectoryPath = in_frame_input_directory
                self.frameInputPaths = []
                self.frameDeltaPaths = []
                self.similarityThreshold = in_similarity_threshold
                self.frameHeight = 1
                self.frameWidth = 1
                self.frameDivisionDimensionX = 1
                self.frameDivisionDimensionY = 1
                self.miniBatchSize = 4
                self.load_file_paths()
                self.set_dicing_rate(1)
        */        

        private void SaveSingleImageToDisk(string inFileName, Mat inFileData)
        {
            Cv2.ImWrite(inFileName, inFileData);
        }

        private void SaveImageBatchToDisk(List<ValueTuple<string, Mat>> inImages)
        {
            foreach(var image in inImages)
            {
                SaveSingleImageToDisk(image.Item1, image.Item2);
            }
        }

        private Mat ScaleFrame(Mat inFrame)
        {
            var scalePercentage = 80;
            var scaleFactor = scalePercentage / (double)100;
            var newWidth = inFrame.Width * scaleFactor;
            var newHeight = inFrame.Height * scaleFactor;

            var dst = new Mat();
            Cv2.Resize(inFrame, dst, new Size(newWidth, newHeight));

            return dst;
        }

        private void UpdateLoadedFrame(int inFrameIndex)
        {
            if(inFrameIndex != _currentFrameIndex)
            {
                _currentFrameIndex = inFrameIndex;
                _currentFrameData = Cv2.ImRead(_inputFramesFileNames[inFrameIndex + 1]);
            }
        }

        private Mat ExtractDifferences(int inFrameX, int inFrameY)
        {
            var x = _frameWidth / _frameDivisionDimensionX * inFrameX;
            var y = _frameHeight / _frameDivisionDimensionY * inFrameY;
            var h = _frameHeight / _frameDivisionDimensionY;
            var w = _frameWidth / _frameDivisionDimensionX;

            var colorFrameBlock = _currentFrameData[y, y + h, x, x + w];
            colorFrameBlock.CopyMakeBorder(2, 2, 2, 2, BorderTypes.Replicate);

            return colorFrameBlock;
        }

        private Mat GenerateDeltaFrame(int inDeltaFileName, int inCropWSize, int inCropHSize)
        {
            var workingSetDictionary = _frameCollector.GetWorkingSet(inCropWSize * inCropHSize);
            var workingSetTupleList = new List<(int frameNumber, (int blockXPos, int blockYPos))>();
            var imageStrips = new List<Mat>();

            foreach(var pair in workingSetDictionary)
            {
                foreach(var blockPos in pair.Value)
                {
                    workingSetTupleList.Add((pair.Key, blockPos));
                }
            }

            for(int y = 0; y < inCropHSize; y++)
            {
                // Start off image array with one frame block to give loop something to append to
                UpdateLoadedFrame(workingSetTupleList[y * inCropHSize].frameNumber);
                var imageStrip = ExtractDifferences(workingSetTupleList[y * inCropHSize].Item2.blockXPos, workingSetTupleList[y * inCropHSize].Item2.blockYPos);

                var currentTupleSelection = workingSetTupleList[y * inCropHSize];
                _frameCollector.CollectStorageBlock(currentTupleSelection.frameNumber, currentTupleSelection.Item2.blockXPos, currentTupleSelection.Item2.blockYPos, inDeltaFileName, 0, y);

                for(int x = 0; x < inCropWSize - 1; x++)
                {
                    currentTupleSelection = workingSetTupleList[(x + 1) + (y * inCropWSize)];
                    UpdateLoadedFrame(currentTupleSelection.frameNumber);
                    var frameData = ExtractDifferences(currentTupleSelection.Item2.blockXPos, currentTupleSelection.Item2.blockYPos);
                    Cv2.HConcat(imageStrip, frameData, imageStrip);

                    _frameCollector.CollectStorageBlock(currentTupleSelection.frameNumber, currentTupleSelection.Item2.blockXPos, currentTupleSelection.Item2.blockYPos, inDeltaFileName, x+1, y);
                }

                imageStrips.Add(imageStrip);
            }

            var imageBuffer = imageStrips[0];
            for(int z = 1; z < imageStrips.Count; z++)
            {
                Cv2.VConcat(imageBuffer, imageStrips[z], imageBuffer);
            }

            return imageBuffer;
        }

        public void GenerateAndSaveDeltaFrame()
        {
            var tempBatchCollection = new List<(string fileName, Mat fileData)>();
            while(_frameCollector.IsWorkingSetReady(_frameDivisionDimensionX * _frameDivisionDimensionY))
            {
                var currentFileName = _frameCollector.GetCurrentStoreDictFilename();
                var currentImageBuffer = GenerateDeltaFrame(currentFileName.Item1, _frameDivisionDimensionX, _frameDivisionDimensionY);
                tempBatchCollection.Add((currentFileName.Item2, currentImageBuffer));
                _frameCollector.IncrementStorageDictFilename();

                if (tempBatchCollection.Count > Environment.ProcessorCount * _miniBatchSize)
                {
                    Parallel.ForEach(tempBatchCollection, (batchItem) =>
                    {
                        SaveSingleImageToDisk(batchItem.fileName, batchItem.fileData);
                    });
                    tempBatchCollection.Clear();
                }
            }
            SaveImageBatchToDisk(tempBatchCollection);
        }

        private void IdentifyDifferencesSingleFrame(int inFileIndex)
        {
            if(inFileIndex + 1 < _inputFramesFileNames.Count)
            {
                var frameA = Cv2.ImRead(_inputFramesFileNames[inFileIndex]);
                var frameB = Cv2.ImRead(_inputFramesFileNames[inFileIndex + 1]);

                var frameAResized = ScaleFrame(frameA);
                var frameBResized = ScaleFrame(frameB);

                var grayFrameA = new Mat();
                var grayFrameB = new Mat();
                Cv2.CvtColor(frameAResized, grayFrameA, ColorConversionCodes.RGB2GRAY);
                Cv2.CvtColor(frameBResized, grayFrameB, ColorConversionCodes.RGB2GRAY);

                for(int ih = 0; ih < _frameDivisionDimensionY; ih++)
                {
                    for (int iw = 0; iw < _frameDivisionDimensionX; iw++)
                    {
                        var x = grayFrameA.Width / _frameDivisionDimensionX * iw;
                        var y = grayFrameA.Height / _frameDivisionDimensionY * ih;
                        var h = grayFrameA.Height / _frameDivisionDimensionY;
                        var w = grayFrameA.Width / _frameDivisionDimensionX;

                        var grayFrameBlockA = grayFrameA[y, y + h, x, x + w];
                        var grayFrameBlockB = grayFrameB[y, y + h, x, x + w];

                        var similarityScore = QualitySSIM.Compute(grayFrameBlockA, grayFrameBlockB, null).Val0;

                        if(similarityScore <= _similarityThreshold)
                        {
                            _frameCollector.CollectTempBlock(inFileIndex, iw, ih);
                        }
                    }
                }
            }
        }

        public void IdentifyDifferences(int[] inFileIndices = null)
        {
            if(inFileIndices != null)
            {
                Parallel.ForEach(inFileIndices, (index) =>
                {
                    IdentifyDifferencesSingleFrame(index);
                });
            }
            else
            {
                Parallel.For(0, _inputFramesFileNames.Count, index =>
                {
                    IdentifyDifferencesSingleFrame(index);
                });
            }
        }

        public void SetDicingRate(int inRate=2)
        {
            if (_inputFramesFileNames.Count > 0)
            {
                try
                {
                    // Load one of the frames and use that as baseline spec for rest of collection
                    var img = Cv2.ImRead(_inputFramesFileNames[0]);
                    _frameWidth = img.Width;
                    _frameHeight = img.Height;

                    var aspectRatio = AspectRatioCalculator.CalculateAspectRatio(_frameWidth, _frameHeight);
                    _frameDivisionDimensionX = aspectRatio.X * inRate;
                    _frameDivisionDimensionY = aspectRatio.Y * inRate;
                }
                catch(Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(e.Message);
                    Console.ResetColor();
                }
            }
        }

        private void LoadFilePaths()
        {
            foreach (string path in Directory.EnumerateFiles(_inputFrameDirectory))
            {
                _inputFramesFileNames.Add(path);
            }
            _inputFramesFileNames = _inputFramesFileNames.CustomSort().ToList();
        }
    }
}