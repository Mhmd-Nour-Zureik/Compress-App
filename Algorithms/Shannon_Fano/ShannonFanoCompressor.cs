using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Accord;
using Compresion_RAR.Algorithms.Shannon_Fano;
using Compresion_RAR.Enums;
using Compresion_RAR.Views;

namespace Compresion_RAR.Algorithms.ShannoFano

{
    public class ShannonFanoCompressor
    {
        // Compress : 
        public async static void Compress(byte[] inputData, string outputFilePath, ProgressWindow pw, CancellationToken ct = default)
        {
            if (inputData == null || inputData.Length == 0)
                throw new ArgumentException("Input data is empty.", nameof(inputData));
            ct.ThrowIfCancellationRequested();
            await pw.WaitIfPausedAsync();
            ConcurrentDictionary<byte, int> freq = new ConcurrentDictionary<byte, int>();
            _ = Parallel.ForEach(
                source: Partitioner.Create(0, inputData.Length),
                body: async range =>
                {
                    Dictionary<byte, int> local = new Dictionary<byte, int>();
                    for (int i = range.Item1; i < range.Item2; i++)
                    {
                        ct.ThrowIfCancellationRequested();
                        await pw.WaitIfPausedAsync();
                        byte b = inputData[i];
                        if (local.ContainsKey(b))
                        {
                            local[b]++;
                        }
                        else
                        {
                            local[b] = 1;
                        }
                    }
                    foreach (KeyValuePair<byte, int> kvp in local)
                    {
                        ct.ThrowIfCancellationRequested();
                        await pw.WaitIfPausedAsync();
                        _ = freq.AddOrUpdate(kvp.Key, kvp.Value, (_, old) => old + kvp.Value);
                    }
                });

            Dictionary<byte, int> frequencyTable = freq.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            List<Symbol> symbols = frequencyTable.Select(kvp => new Symbol(kvp.Key, kvp.Value)).OrderByDescending(s => s.Frequency)
                                        .ToList();
            Dictionary<byte, string> codes = new Dictionary<byte, string>();
            GenerateCodes(symbols, codes, string.Empty , pw , ct);

            int partCount = Environment.ProcessorCount;
            string[] segments = new string[partCount];
            _ = Parallel.For(0, partCount, async partId =>
              {
                  ct.ThrowIfCancellationRequested();
                  await pw.WaitIfPausedAsync();
                  int start = partId * inputData.Length / partCount;
                  int end = (partId + 1) * inputData.Length / partCount;
                  var sbPart = new StringBuilder();
                  for (int i = start; i < end; i++)
                  {
                      ct.ThrowIfCancellationRequested();
                      await pw.WaitIfPausedAsync();
                      _ = sbPart.Append(codes[inputData[i]]);
                  }
                  segments[partId] = sbPart.ToString();
              });
            string bitString = string.Concat(segments);

            using (FileStream outFs = File.Create(outputFilePath))
            using (BinaryWriter writer = new BinaryWriter(outFs, Encoding.UTF8))
            {
                writer.Write((byte)Algorithm.SHANNON_FANO);
                writer.Write(frequencyTable.Count);
                foreach (var kvp in frequencyTable)
                {
                    ct.ThrowIfCancellationRequested();
                    await pw.WaitIfPausedAsync();
                    writer.Write(kvp.Key);
                    writer.Write(kvp.Value);
                }

                int pad = (8 - (bitString.Length % 8)) % 8;
                writer.Write(pad);
                bitString = bitString.PadRight(bitString.Length + pad, '0');
                ct.ThrowIfCancellationRequested();
                await pw.WaitIfPausedAsync();
                for (int i = 0; i < bitString.Length; i += 8)
                    writer.Write(Convert.ToByte(bitString.Substring(i, 8), 2));
            }
        }

        // Decompress :
        public async static void Decompress(string inputStream, string outputFilePath, ProgressWindow pw, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            await pw.WaitIfPausedAsync();

            Dictionary<byte, int> freq;
            int pad;
            byte[] data;

            using (var inFs = File.OpenRead(inputStream))
            using (var reader = new BinaryReader(inFs, Encoding.UTF8))
            {
                var alg = (Algorithm)reader.ReadByte();
                if (alg != Algorithm.SHANNON_FANO)
                    throw new InvalidDataException("Stream is not Shannon-Fano encoded.");

                int count = reader.ReadInt32();
                freq = new Dictionary<byte, int>();
                for (int i = 0; i < count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    await pw.WaitIfPausedAsync();
                    freq[reader.ReadByte()] = reader.ReadInt32();
                }

                pad = reader.ReadInt32();
                data = reader.ReadBytes((int)(inFs.Length - inFs.Position));
            } 

            ct.ThrowIfCancellationRequested();
            await pw.WaitIfPausedAsync();

            var symbols = freq.Select(kvp => new Symbol(kvp.Key, kvp.Value))
                              .OrderByDescending(s => s.Frequency)
                              .ToList();
            var codes = new Dictionary<byte, string>();
            GenerateCodes(symbols, codes, string.Empty, pw, ct);

            var root = new DecodeNode();
            foreach (var kvp in codes)
            {
                ct.ThrowIfCancellationRequested();
                await pw.WaitIfPausedAsync();
                var node = root;
                foreach (char bit in kvp.Value)
                {
                    node = bit == '0' ? node.GetOrCreateLeft() : node.GetOrCreateRight();
                }
                node.Symbol = kvp.Key;
            }

            using (var outputStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var cur = root;
                foreach (var b in data)
                {
                    var bits = Convert.ToString(b, 2).PadLeft(8, '0');
                    foreach (var bit in bits)
                    {
                        ct.ThrowIfCancellationRequested();
                        await pw.WaitIfPausedAsync();
                        cur = bit == '0' ? cur.Left : cur.Right;

                        if (cur.Symbol.HasValue)
                        {
                            outputStream.WriteByte(cur.Symbol.Value);
                            cur = root;
                        }
                    }
                }
            } 


            if (pad > 0)
            {
                byte[] fileBytes = File.ReadAllBytes(outputFilePath);
                byte[] adjustedBytes = new byte[fileBytes.Length - pad];
                Array.Copy(fileBytes, 0, adjustedBytes, 0, adjustedBytes.Length);
                File.WriteAllBytes(outputFilePath, adjustedBytes); 
            }

            pw.ReportProgress(100, "Decompression complete");
        }

        private async static void GenerateCodes(List<Symbol> symbols, Dictionary<byte, string> codes, string prefix, ProgressWindow pw, CancellationToken ct = default)
        {
            if (symbols.Count == 0) return;
            if (symbols.Count == 1)
            {
                codes[symbols[0].Value] = prefix.Length > 0 ? prefix : "0";
                return;
            }
            ct.ThrowIfCancellationRequested();
            await pw.WaitIfPausedAsync();
            int total = symbols.Sum(s => s.Frequency);
            int acc = 0, split = 0;
            for (int i = 0; i < symbols.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                await pw.WaitIfPausedAsync();
                acc += symbols[i].Frequency;
                if (acc >= total / 2)
                {
                    split = i;
                    break;
                }
            }
            var first = symbols.Take(split + 1).ToList();
            var second = symbols.Skip(split + 1).ToList();
            GenerateCodes(first, codes, prefix + "0" , pw , ct);
            GenerateCodes(second, codes, prefix + "1" , pw , ct);
        }

    }
}
