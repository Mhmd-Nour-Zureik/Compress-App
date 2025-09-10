using Compresion_RAR.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Compresion_RAR.Algorithms
{
    public class HuffmanCompressor
    {
        public static async void Compress(byte[] data, string outputFilePath , ProgressWindow pw, CancellationToken ct = default)
        {
            if (data == null || data.Length == 0)
            {
                throw new InvalidOperationException("Data is empty.");
            }

            Dictionary<byte, int> frequencyTable = BuildFrequencyTable(data);

            if (frequencyTable.Count == 0)
            {
                throw new InvalidOperationException("Input file is empty.");
            }

            HuffmanNode huffmanTree = await BuildHuffmanTree(frequencyTable , pw , ct);

            Dictionary<byte, string> huffmanCodes = new Dictionary<byte, string>();
            GenerateCodes(huffmanTree, "", huffmanCodes);

            _ = data.Distinct().ToList();

            CompressData(data, outputFilePath, frequencyTable, huffmanCodes);
        }
        
        public static async Task Decompress(string inputFilePath, string outputFilePath, ProgressWindow pw, CancellationToken ct = default )
        {
            using (BinaryReader reader = new BinaryReader(File.Open(inputFilePath, FileMode.Open)))
            {
                int uniqueChars = reader.ReadInt32();
                Dictionary<byte, int> frequencyTable = new Dictionary<byte, int>();

                for (int i = 0; i < uniqueChars; i++)
                {
                    byte b = reader.ReadByte();
                    int freq = reader.ReadInt32();
                    frequencyTable[b] = freq;
                }
                ct.ThrowIfCancellationRequested();
                await pw.WaitIfPausedAsync();

                HuffmanNode huffmanTree = await BuildHuffmanTree(frequencyTable ,pw, ct);

                await pw.WaitIfPausedAsync();

                int remainingBits = reader.ReadInt32();
                byte[] compressedData = reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position));
                ct.ThrowIfCancellationRequested();
                await pw.WaitIfPausedAsync();

                byte[] decompressedData = await DecompressData(compressedData, remainingBits, huffmanTree ,pw, ct);
                await pw.WaitIfPausedAsync();

                ct.ThrowIfCancellationRequested();
                File.WriteAllBytes(outputFilePath, decompressedData);
            }
        }

        // Asynchronously build frequency table with parallel processing
        private static Dictionary<byte, int> BuildFrequencyTable(byte[] data)
        {
            Dictionary<byte, int> frequencyTable = new Dictionary<byte, int>();

            for (int i = 0; i < data.Length; i++)
            {
                byte b = data[i];
                if (frequencyTable.ContainsKey(b))
                {
                    frequencyTable[b]++;
                }
                else
                {
                    frequencyTable[b] = 1;
                }
            }

            return frequencyTable;
        }


        private static async Task<HuffmanNode> BuildHuffmanTree(Dictionary<byte, int> frequencyTable, ProgressWindow pw = null, CancellationToken ct = default)
        {
            SortedList<int, List<HuffmanNode>> priorityQueue = new SortedList<int, List<HuffmanNode>>();

            foreach (KeyValuePair<byte, int> pair in frequencyTable)
            {
                if (!priorityQueue.ContainsKey(pair.Value))
                {
                    priorityQueue[pair.Value] = new List<HuffmanNode>();
                }
                priorityQueue[pair.Value].Add(new HuffmanNode { Character = pair.Key, Frequency = pair.Value });
                ct.ThrowIfCancellationRequested();
                await pw.WaitIfPausedAsync();
            }

            while (priorityQueue.Count > 1)
            {
                ct.ThrowIfCancellationRequested();
                await pw.WaitIfPausedAsync();

                KeyValuePair<int, List<HuffmanNode>> first = GetMinEntry(priorityQueue);
                KeyValuePair<int, List<HuffmanNode>> second = GetMinEntry(priorityQueue);

                HuffmanNode merged = new HuffmanNode
                {
                    Frequency = first.Key + second.Key,
                    Left = first.Value[0],
                    Right = second.Value[0]
                };

                if (!priorityQueue.ContainsKey(merged.Frequency))
                {
                    priorityQueue[merged.Frequency] = new List<HuffmanNode>();
                }
                priorityQueue[merged.Frequency].Add(merged);

                ct.ThrowIfCancellationRequested();
                await pw.WaitIfPausedAsync();
            }
            return priorityQueue.Values[0][0]; // Return the root node
        }

        private static KeyValuePair<int, List<HuffmanNode>> GetMinEntry(SortedList<int, List<HuffmanNode>> queue)
        {
            int key = queue.Keys[0];
            List<HuffmanNode> value = queue[key];
            HuffmanNode node = value[0];
            value.RemoveAt(0);

            if (value.Count == 0)
            {
                queue.RemoveAt(0);
            }

            return new KeyValuePair<int, List<HuffmanNode>>(key, new List<HuffmanNode> { node });
        }

        private static void GenerateCodes(HuffmanNode node, string code, Dictionary<byte, string> huffmanCodes)
        {
            if (node == null)
            {
                return;
            }

            if (node.Character.HasValue)
            {
                huffmanCodes[node.Character.Value] = code;
                return;
            }

            GenerateCodes(node.Left, code + "0", huffmanCodes);
            GenerateCodes(node.Right, code + "1", huffmanCodes);
        }

        private static void CompressData(byte[] data, string outputFilePath, Dictionary<byte, int> frequencyTable, Dictionary<byte, string> huffmanCodes)
        {
            _ = data.Distinct().ToList();

            using (BinaryWriter writer = new BinaryWriter(File.Create(outputFilePath)))
            {
                writer.Write(frequencyTable.Count);
                foreach (KeyValuePair<byte, int> pair in frequencyTable)
                {
                    writer.Write(pair.Key);
                    writer.Write(pair.Value);
                }

                StringBuilder bitString = new StringBuilder();
                for (int i = 0; i < data.Length; i++)
                {
                    _ = bitString.Append(huffmanCodes[data[i]]);
                }

                string bitStringFinal = bitString.ToString();

                int padLength = 8 - (bitStringFinal.Length % 8);
                bitStringFinal += new string('0', padLength);
                writer.Write(padLength);

                for (int i = 0; i < bitStringFinal.Length; i += 8)
                {
                    string byteStr = bitStringFinal.Substring(i, Math.Min(8, bitStringFinal.Length - i));
                    byte b = Convert.ToByte(byteStr, 2);
                    writer.Write(b);
                }
            }
        }

        private static async Task<byte[]> DecompressData(byte[] compressedData, int padLength, HuffmanNode huffmanTree , ProgressWindow pw, CancellationToken ct = default)
        {
            List<byte> result = new List<byte>();
            HuffmanNode current = huffmanTree;

            for (int i = 0; i < compressedData.Length; i++)
            {
                byte b = compressedData[i];
                string bitString = Convert.ToString(b, 2).PadLeft(8, '0');
                ct.ThrowIfCancellationRequested();
                await pw.WaitIfPausedAsync();

                foreach (char bit in bitString)
                {
                    ct.ThrowIfCancellationRequested();
                    await pw.WaitIfPausedAsync();

                    current = bit == '0' ? current.Left : current.Right;

                    if (current?.Character.HasValue == true)
                    {
                        result.Add(current.Character.Value);
                        current = huffmanTree;
                    }
                }
            }
            ct.ThrowIfCancellationRequested();
            await pw.WaitIfPausedAsync();

            if (padLength > 0)
            {
                result.RemoveRange(result.Count - padLength, padLength);
            }

            return result.ToArray();
        }
    }
}