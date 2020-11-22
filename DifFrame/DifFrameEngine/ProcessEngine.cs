using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OpenCvSharp;
using OpenCvSharp.Quality;

namespace Difframe
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

    public class ProcessEngine
    {
        private FrameCollector _frameCollector;
        private int _currentFrameIndex;
        private Mat _currentFrameData;
        private int _frameWidth;
        private int _frameHeight;
        private int _frameDivisionDimensionX;
        private int _frameDivisionDimensionY;
        private List<string> _inputFramesFileNames;
        private Dictionary<int, Mat> _inputFrameData;
        private Dictionary<int, bool> _downloadCacheStaleFrames;
        private int _downloadedCacheEndFrameIndex;
        private string _inputFrameDirectory;
        private double _similarityThreshold;
        private int _miniBatchSize;
        private bool _localDataMode;
        private bool _readyToProcess;
        
        public ProcessEngine(bool inLocalDataMode = true, string inFrameDirectory = null, double inSimilarityThreshold = 34.50, int inMiniBatchSize = 2)
        {
            _frameCollector = new FrameCollector();
            _currentFrameIndex = -10;
            _downloadedCacheEndFrameIndex = -10;
            _currentFrameData = new Mat();
            _similarityThreshold = inSimilarityThreshold;
            _miniBatchSize = inMiniBatchSize;
            _frameWidth = 1;
            _frameHeight = 1;
            _frameDivisionDimensionX = 1;
            _frameDivisionDimensionY = 1;
            _localDataMode = inLocalDataMode;
            _downloadCacheStaleFrames = new Dictionary<int, bool>();

            if (inLocalDataMode == true && inFrameDirectory != null)
            {
                _inputFramesFileNames = new List<string>();
                _inputFrameDirectory = inFrameDirectory;
                LoadFilePaths();
                SetDicingRate();
                _readyToProcess = true;
            }
            if (inLocalDataMode == false)
            {
                _inputFrameData = new Dictionary<int, Mat>();
                SetDicingRate();
                _readyToProcess = false;
            }
        }

        private Mat GetFrameData(int inRequestedFrameIndex)
        {
            if (_localDataMode)
            {
                return Cv2.ImRead(_inputFramesFileNames[inRequestedFrameIndex]);
            }
            else
            {
                return _inputFrameData[inRequestedFrameIndex];
            }
        }

        public int GetLastFrameIndex()
        {
            if (_localDataMode)
            {
                return _inputFramesFileNames.Count - 1;
            }
            else
            {
                return _downloadedCacheEndFrameIndex;
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
                _currentFrameData = GetFrameData(inFrameIndex);
            }
        }

        private Mat ExtractDifferences(int inFrameX, int inFrameY)
        {
            var x = _frameWidth / _frameDivisionDimensionX * inFrameX;
            var y = _frameHeight / _frameDivisionDimensionY * inFrameY;
            var h = _frameHeight / _frameDivisionDimensionY;
            var w = _frameWidth / _frameDivisionDimensionX;

            var colorFrameBlock = _currentFrameData[y, y + h, x, x + w];
            colorFrameBlock = colorFrameBlock.CopyMakeBorder(2, 2, 2, 2, BorderTypes.Replicate);

            return colorFrameBlock;
        }

        private Mat GenerateDeltaFrame(int inDeltaFileName, int inCropWSize, int inCropHSize)
        {
            var workingSetDictionary = _frameCollector.GetWorkingSet(inCropWSize * inCropHSize);
            var workingSetTupleList = new List<(int frameNumber, (int blockXPos, int blockYPos))>();
            var imageStrips = new List<Mat>();

            var workingSetDictionaryKeysSorted = workingSetDictionary.Keys.ToImmutableSortedSet();

            foreach(var key in workingSetDictionaryKeysSorted)
            {
                foreach (var blockPos in workingSetDictionary[key])
                {
                    workingSetTupleList.Add((key, blockPos));
                }
            }

            int tupleIndexTracker = 0;
            for(int y = 0; y < inCropHSize; y++)
            {
                // Start off image array with one frame block to give loop something to append to
                var currentTupleSelection = workingSetTupleList[tupleIndexTracker];
                tupleIndexTracker++;
                UpdateLoadedFrame(currentTupleSelection.frameNumber + 1);
                var imageStrip = ExtractDifferences(currentTupleSelection.Item2.blockXPos, currentTupleSelection.Item2.blockYPos);
                _frameCollector.RecordDeltaFrameBlockData(currentTupleSelection.frameNumber, currentTupleSelection.Item2.blockXPos, currentTupleSelection.Item2.blockYPos, inDeltaFileName, 0, y);

                for(int x = 0; x < inCropWSize - 1; x++)
                {
                    currentTupleSelection = workingSetTupleList[tupleIndexTracker];
                    tupleIndexTracker++;
                    UpdateLoadedFrame(currentTupleSelection.frameNumber + 1);
                    var frameData = ExtractDifferences(currentTupleSelection.Item2.blockXPos, currentTupleSelection.Item2.blockYPos);
                    Cv2.HConcat(imageStrip, frameData, imageStrip);

                    _frameCollector.RecordDeltaFrameBlockData(currentTupleSelection.frameNumber, currentTupleSelection.Item2.blockXPos, currentTupleSelection.Item2.blockYPos, inDeltaFileName, x+1, y);
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

        private void IdentifyDifferencesSingleFrame(int inFileIndex)
        {
            if (inFileIndex + 1 < GetLastFrameIndex())
            {
                var frameA = GetFrameData(inFileIndex);
                var frameB = GetFrameData(inFileIndex + 1);

                var frameAResized = ScaleFrame(frameA);
                var frameBResized = ScaleFrame(frameB);

                var grayFrameA = new Mat();
                var grayFrameB = new Mat();
                Cv2.CvtColor(frameAResized, grayFrameA, ColorConversionCodes.RGB2GRAY);
                Cv2.CvtColor(frameBResized, grayFrameB, ColorConversionCodes.RGB2GRAY);

                Parallel.For(0, _frameDivisionDimensionY, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, ih =>
                {
                    for (int iw = 0; iw < _frameDivisionDimensionX; iw++)
                    {
                        var x = grayFrameA.Width / _frameDivisionDimensionX * iw;
                        var y = grayFrameA.Height / _frameDivisionDimensionY * ih;
                        var h = grayFrameA.Height / _frameDivisionDimensionY;
                        var w = grayFrameA.Width / _frameDivisionDimensionX;

                        var grayFrameBlockA = grayFrameA[y, y + h, x, x + w];
                        var grayFrameBlockB = grayFrameB[y, y + h, x, x + w];

                        var qualityMetrics = QualityPSNR.Compute(grayFrameBlockA, grayFrameBlockB, null);

                        if (qualityMetrics.Val0 <= _similarityThreshold)
                        {
                            _frameCollector.CollectTempBlock(inFileIndex, iw, ih);
                        }
                    }
                });
            }
        }




        public void MatConvertTest()
        {
            var frameA = GetFrameData(0);
            var grayFrameA = new Mat();
            Cv2.CvtColor(frameA, grayFrameA, ColorConversionCodes.RGB2GRAY);

            byte[] grayFrameBytes;
            var encodedData = Cv2.ImEncode(".jpg", frameA, out grayFrameBytes);

            var decodedData = Cv2.ImDecode(grayFrameBytes, ImreadModes.Color);

            var currentFileName = _frameCollector.GetCurrentStoreDictFilename();
            SaveSingleImageToDisk(currentFileName.Item2, decodedData);
        }

        public Dictionary<int, byte[]> GetFrameDataForUpload(int[] inFrameIndices)
        {
            var frameDatas = new Dictionary<int, byte[]>();
            foreach (var frameIndex in inFrameIndices)
            {
                frameDatas[frameIndex] = GetFrameData(frameIndex).ImEncode();
            }

            return frameDatas;
        }

        public byte[] GetFrameDataForUpload(int inFrameIndex)
        {
            return GetFrameData(inFrameIndex).ImEncode();
        }

        /// <summary>
        /// Downloads and converts byte arrays into grayscale image Mats
        /// </summary>
        /// <param name="inFrames"></param>
        public void LoadDownloadedFrameData(Dictionary<int, byte[]> inFrames)
        {
            // For each downloaded frame, convert from byte array to Mat and add to dictionary
            foreach(var frame in inFrames)
            {
                var decodedData = Cv2.ImDecode(frame.Value, ImreadModes.Grayscale);
                _inputFrameData[frame.Key] = decodedData;
            }

            var pruneList = new List<int>();

            // For each stale frame in stale frame dictionary, prune from download cache dictionary
            foreach(var frame in _downloadCacheStaleFrames)
            {
                if(_inputFrameData.ContainsKey(frame.Key - 1))
                {
                    // Check stale item index AND previous index are present in dictionary
                    if (_downloadCacheStaleFrames.ContainsKey(frame.Key - 1))
                    {
                        _inputFrameData.Remove(frame.Key);
                        pruneList.Add(frame.Key);
                    }
                }
                else
                {
                    _inputFrameData.Remove(frame.Key);
                    pruneList.Add(frame.Key);
                }
            }

            // Prune stale list
            foreach(var item in pruneList)
            {
                _downloadCacheStaleFrames.Remove(item);
            }
        }

        public void GenerateAndSaveDeltaFrame()
        {
            if (_readyToProcess)
            {
                var tempBatchCollection = new List<(string fileName, Mat fileData)>();
                while (_frameCollector.IsWorkingSetReady(_frameDivisionDimensionX * _frameDivisionDimensionY))
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
        }

        public void IdentifyDifferences(int[] inFileIndices)
        {
            if (_readyToProcess)
            {
                Parallel.ForEach(inFileIndices, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, (index) =>
                {
                    IdentifyDifferencesSingleFrame(index);
                });
            }
        }

        public void SetDicingRate(int inRate = 2)
        {
            if (GetLastFrameIndex() > 0)
            {
                try
                {
                    // Load one of the frames and use that as baseline spec for rest of collection
                    var img = GetFrameData(0);
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

        public void SetMiniBatchSize(int inMiniBatchSize = 2)
        {
            _miniBatchSize = inMiniBatchSize;
        }

        public void SetSimilarityThreshold(double inSimilarityThreshold = 34.50)
        {
            _similarityThreshold = inSimilarityThreshold;
        }

        public bool IsReadyToProcess()
        {
            return _readyToProcess;
        }

        public void UpdateProjectInput(string inFrameDirectory)
        {
            _inputFrameDirectory = inFrameDirectory;
            LoadFilePaths();
            SetDicingRate();
            _readyToProcess = true;
        }

        /// <summary>
        /// Extracts temp storage difference blocks from frame collector.
        /// Intended to be used by node clients when sending processed blocks to server.
        /// </summary>
        /// <param name="inPreferredExtractionSize"></param>
        /// <returns>Int array of blocks broken down to their base components (frame number, frame position X, frame position Y)</returns>
        public int[] GetDifferenceBlocks(int inPreferredExtractionSize = 250)
        {
            // Resize request size to allow array to fit 300 int limit of NT.ReceiveIntArray
            if(inPreferredExtractionSize > 300)
            {
                inPreferredExtractionSize = 300;
            }
            var workingSetDictionary = _frameCollector.GetWorkingSet(inPreferredExtractionSize);
            var workingSetList = new List<int>();

            var workingSetDictionaryKeysSorted = workingSetDictionary.Keys.ToImmutableSortedSet();

            foreach (var key in workingSetDictionaryKeysSorted)
            {
                foreach (var blockPos in workingSetDictionary[key])
                {
                    workingSetList.Add(key);
                    workingSetList.Add(blockPos.FrameBlockX);
                    workingSetList.Add(blockPos.FrameBlockY);
                }
            }

            return workingSetList.ToArray();
        }
    }
}