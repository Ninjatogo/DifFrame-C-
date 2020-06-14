# DifFrame-C#
*Pack video frame differences into a single frame for efficient video upscaling.*

**Dif-Frame** is meant to be used as a video pre-processor to make video upscaling using machine learning-based tools faster. The way this is intend to work is by comparing each video frame with the subsequent frame to find the differences. Once the difference is found, it can be extracted and copied to a collection. In order to reduce the total amount of data that will be copied, the frames are divided into zones. Only zones that have a difference above a set threshold will be copied to the upscaling collection. Once the collection has enough parts to make a full frame it can be saved to disk to be upscaled.

After the difference collection is upscaled, the frame can be divided again to produce the individual frame pieces that can be pasted back into the video by superimposing the pieces over time.

------

## TODO

- [ ] Complete functionality for frame reassembly
- [ ] Save frame collector data to some sort of database for processing progress persistence
- [ ] Open frame processor for more output customization (e.g. frame block border size)
- [ ] Implement multi-workstation workload sharing
- [ ] Implement input frame on-demand frame generation from video files using FFMPEG (no more manually extracting and maintaining huge folder of frames)
- [ ] Implement GUI component

------

# Application Components

## Frame Collector

##### FrameCollector(string inDeltaFrameFileTemplate = "Frames_Deltas\\delta_")

##### void CollectTempBlock(int inFrameNumber, int inFrameX, int inFrameY)

##### void CollectStorageBlock(int inFrameNumber, int inFrameX, int inFrameY, int inDeltaFilename, int inFileBlockX, int inFileBlockY)

##### bool IsWorkingSetReady(int inWorkingSetSize)

##### Dictionary<int, Stack<(int FrameBlockX, int FrameBlockY)>> GetWorkingSet(int inWorkingSetSize)

##### (int, string) GetCurrentStoreDictFilename()

##### void IncrementStorageDictFilename()

## Frame Processor

##### FrameProcessor(string input_frame_directory, double similarity_threshold, int inMiniBatchSize = 2)

##### void GenerateAndSaveDeltaFrame()

##### void IdentifyDifferences(int[] inFileIndices = null)

##### void SetDicingRate(int inRate=2)

## Aspect Ratio Calculator

##### (int X, int Y) CalculateAspectRatio(int inWidth, int inHeight)

## Diff-Frame Engine

##### DifFrameEngine(string input_frames_filepath, double similarity_threshold = 34.50)

##### void ProcessVideoCompleteLoop()

## Network Data Tools

##### void SendInt(Socket inHandler, int inInt)

##### (bool sentSuccessfully, Exception innerException) SendIntCollections(Socket inHandler, int[] inArray)

##### (bool receivedSuccessfully, List<int[]> collections, Exception innerException) ReceiveIntCollections(Socket inHandler)

##### (List<byte[]> collection, int byteCount) SerializeIntCollection(List<int> inCollection)

##### int[] DeserializeIntArray(byte[] inCollection)

##### byte[] ConvertIntToByteArray(int inInt)

##### int ConvertByteArrayToInt(byte[] inArray)

## Network Client

##### void StartClient()

## Network Server

##### void StartServerListener()

- Starts listener loop to accept and bind new connections from clients
- Passes new connections to connection handler method task **HandleNewConnection**

##### Task HandleNewConnection(Socket inHandler, string inFileName, string inFileLocation, string inChecksum)

