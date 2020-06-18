using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;

namespace Difframe
{
    class FrameCollector
    {
        private Dictionary<int, Stack<(int FrameBlockX, int FrameBlockY)>> _diff_block_temporary_dictionary;
        private Dictionary<int, Stack<(int FrameBlockX, int FrameBlockY, int DeltaFilename, int FileBlockX, int FileBlockY)>> _diff_block_storage_dictionary;
        private int _temp_block_count;
        private int _storage_dict_current_filename;
        private string _deltaFrameFileTemplate;

        static readonly object _monitorObject = new object();

        public FrameCollector(string inDeltaFrameFileTemplate = "Frames_Deltas\\delta_")
        {
            _diff_block_temporary_dictionary = new Dictionary<int, Stack<(int FrameBlockX, int FrameBlockY)>>();
            _diff_block_storage_dictionary = new Dictionary<int, Stack<(int FrameBlockX, int FrameBlockY, int DeltaFilename, int FileBlockX, int FileBlockY)>>();
            _temp_block_count = 0;
            _storage_dict_current_filename = 0;
            _deltaFrameFileTemplate = inDeltaFrameFileTemplate;
        }

        public void CollectTempBlock(int inFrameNumber, int inFrameX, int inFrameY)
        {
            Monitor.Enter(_monitorObject);
            try
            {
                if (_diff_block_temporary_dictionary.ContainsKey(inFrameNumber) == false)
                {
                    _diff_block_temporary_dictionary[inFrameNumber] = new Stack<(int FrameBlockX, int FrameBlockY)>();
                }
                _diff_block_temporary_dictionary[inFrameNumber].Push((inFrameX, inFrameY));
                _temp_block_count++;
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(e.Message);
                Console.ResetColor();
            }
            finally
            {
                Monitor.Exit(_monitorObject);
            }
        }

        public void CollectStorageBlock(int inFrameNumber, int inFrameX, int inFrameY, int inDeltaFilename, int inFileBlockX, int inFileBlockY)
        {
            Monitor.Enter(_monitorObject);
            try
            {
                if (_diff_block_storage_dictionary.ContainsKey(inFrameNumber) == false)
                {
                    _diff_block_storage_dictionary[inFrameNumber] = new Stack<(int FrameBlockX, int FrameBlockY, int DeltaFilename, int FileBlockX, int FileBlockY)>();
                }
                _diff_block_storage_dictionary[inFrameNumber].Push((inFrameX, inFrameY, inDeltaFilename, inFileBlockX, inFileBlockY));
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(e.Message);
                Console.ResetColor();
            }
            finally
            {
                Monitor.Exit(_monitorObject);
            }
        }

        public bool IsWorkingSetReady(int inWorkingSetSize)
        {
            return _temp_block_count >= inWorkingSetSize;
        }

        public Dictionary<int, Stack<(int FrameBlockX, int FrameBlockY)>> GetWorkingSet(int inWorkingSetSize)
        {
            var workingSet = new Dictionary<int, Stack<(int FrameBlockX, int FrameBlockY)>>();
            var itemsAdded = 0;

            var _temp_dictionary_keys = _diff_block_temporary_dictionary.Keys.ToImmutableSortedSet();

            if(_temp_block_count >= inWorkingSetSize)
            {
                foreach (var key in _temp_dictionary_keys)
                {
                    while (_diff_block_temporary_dictionary.ContainsKey(key) && _diff_block_temporary_dictionary[key].Count > 0)
                    {
                        if (itemsAdded < inWorkingSetSize)
                        {
                            if (workingSet.ContainsKey(key) == false)
                            {
                                workingSet[key] = new Stack<(int FrameBlockX, int FrameBlockY)>();
                            }

                            workingSet[key].Push(_diff_block_temporary_dictionary[key].Pop());
                            _temp_block_count--;
                            itemsAdded++;
                        }
                        else
                        {
                            break;
                        }
                    }
                    if (itemsAdded >= inWorkingSetSize)
                    {
                        break;
                    }
                }
            }
            return workingSet;
        }

        public (int, string) GetCurrentStoreDictFilename()
        {
            return (_storage_dict_current_filename, $"{_deltaFrameFileTemplate}{_storage_dict_current_filename}.jpg");
        }

        public void IncrementStorageDictFilename()
        {
            _storage_dict_current_filename++;
        }
    }
}