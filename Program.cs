using GFDLibrary.IO;
using GFDLibrary.IO.Common;

namespace BetterEPLInjector
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Drag an EPL/BED to extract, or a directory to inject\n");
                return;
            }
            else
            {
                foreach (string arg in args)
                {
                    if (Path.GetExtension(arg) == ".EPL" || Path.GetExtension(arg) == ".BED")
                    {
                        ExtractEPL(arg);
                    }
                    else if (Path.GetExtension(arg) == "")
                    {
                        InjectEPL(arg);
                    }
                    else
                    {
                        Console.WriteLine("Invalid Input. Drag an EPL/BED to extract, or a directory to inject\n");
                    }
                }
            }
        }

        static void ExtractEPL(string inputEPL)
        {
            Console.WriteLine("EPL detected, looking for embeds to extract...\n");
            string newEPLDir = $"{Path.GetDirectoryName(inputEPL)}\\{Path.GetFileNameWithoutExtension(inputEPL)}";

            byte[] eplBytes = File.ReadAllBytes(inputEPL);
            List < (string modelName, long modelOffset, long modelSize) > modelEntryList = FindEmbeddedModels(eplBytes);

            if (modelEntryList == null) return;

            Directory.CreateDirectory(newEPLDir);

            foreach (var modelEntry in modelEntryList)
            {
                var (name, offset, size) = modelEntry;
                string newEmbedPath = $"{newEPLDir}\\{name}";
                Directory.CreateDirectory(Path.GetDirectoryName(newEmbedPath));

                using (var stream = File.Open(newEmbedPath, FileMode.Create))
                {
                    using (ResourceReader eplFile = new ResourceReader(new MemoryStream(eplBytes), false))
                    {
                        using (BinaryWriter eplEmbedFile = new BinaryWriter(stream))
                        {
                            eplFile.Seek(offset, SeekOrigin.Begin);
                            eplEmbedFile.Write(eplFile.ReadBytes((int)size));
                        }
                    }
                }
                Console.WriteLine($"Extracted {name}");
            }
            
            return;
        }
        static void InjectEPL(string inputDir)
        {
            Console.WriteLine("Directory detected, looking for embeds to inject...\n");
            string outputEPL = inputDir + ".EPL";

            if (!File.Exists(outputEPL))
            {
                outputEPL = inputDir + ".BED";
                if (!File.Exists(outputEPL))
                {
                    Console.WriteLine("Can't find EPL/BED to inject: {0}\n", Path.GetFileNameWithoutExtension(outputEPL));
                    return;
                }
            }

            List<string> embedFileList = new();

            byte[] eplBytes = File.ReadAllBytes(outputEPL);
            List<(string modelName, long modelOffset, long modelSize)> modelEntryList = FindEmbeddedModels(eplBytes);
            bool injectedFiles = false;
            int index = 0;

            File.Delete($"{outputEPL}_temp");
            File.Create($"{outputEPL}_temp").Close();

            foreach (var modelEntry in modelEntryList)
            {
                var (name, offset, size) = modelEntry;
                string embedFileDirectory = $"{inputDir}\\{name}";
                if (File.Exists(embedFileDirectory))
                {
                    Console.WriteLine($"Injecting {name}");
                    byte[] embedBytes = File.ReadAllBytes(embedFileDirectory);

                    using (ResourceReader eplFile = new ResourceReader(new MemoryStream(eplBytes), false))
                    {
                        using (var stream = File.Open($"{outputEPL}_temp", FileMode.Open))
                        {
                            using (BinaryWriter newEplFile = new BinaryWriter(stream))
                            {
                                long currentOffset = 0;
                                if (index != 0)
                                {
                                    var (name1, offset1, size1) = modelEntryList[index - 1];
                                    currentOffset = offset1 + size1;
                                    eplFile.Seek((int)currentOffset, SeekOrigin.Begin);
                                }
                                else
                                {
                                    eplFile.Seek(0, SeekOrigin.Begin);
                                }
                                newEplFile.Seek(0, SeekOrigin.End);
                                newEplFile.Write(eplFile.ReadBytes((int)offset - 4 - (int)currentOffset));
                                newEplFile.Close();
                            }
                        }
                    }

                    using (ResourceReader embedFile = new ResourceReader(new MemoryStream(embedBytes), false))
                    {
                        using (ResourceWriter newEplFile = new ResourceWriter(File.Open($"{outputEPL}_temp", FileMode.Open), false))
                        {
                            newEplFile.Endianness = Endianness.BigEndian;
                            newEplFile.SeekEnd(0);
                            newEplFile.Write(embedBytes.Length);
                            newEplFile.Write(embedBytes);
                            newEplFile.Close();
                        }
                    }
                    injectedFiles = true;
                }
                else
                {
                    Console.WriteLine($"Can't Find {name}, Skipping...");
                }
                index++;
            }

            using (ResourceReader eplFile = new ResourceReader(new MemoryStream(eplBytes), false)) //write until EOF
            {
                var (name, offset, size) = modelEntryList[modelEntryList.Count - 1];
                
                using (var stream = File.Open($"{outputEPL}_temp", FileMode.Open))
                {
                    using (BinaryWriter newEplFile = new BinaryWriter(stream))
                    {
                        long setOffset = offset + size;
                        eplFile.Seek(setOffset, SeekOrigin.Begin);
                        newEplFile.Seek(0, SeekOrigin.End);
                        newEplFile.Write(eplFile.ReadBytes(eplBytes.Length - (int)setOffset));
                        newEplFile.Close();
                    }
                }
            }

            if (injectedFiles == false)
            {
                Console.WriteLine("No Files Injected\n");
            }
            else
            {
                Console.WriteLine("Finished Injecting Files!\n");
                File.Copy($"{outputEPL}_temp", outputEPL, true);
                File.Delete($"{outputEPL}_temp");
            }
            return;
        }

        static List<(string modelName, long modelOffset, long modelSize)> FindEmbeddedModels(byte[] eplBytes)
        {
            List<(string modelName, long modelOffset, long modelSize)> modelEntryList = new();
            var offsets = new List<long>();
            byte[] magic = { 0x47, 0x46, 0x53, 0x30 };
            long currentOffset = 4;

            while (true)
            {
                var embedOffset = IndexOf(eplBytes, magic, currentOffset);
                if (embedOffset == -1)
                {
                    if (offsets.Count == 0)
                    {
                        Console.WriteLine("No Embeds Found!\n");
                        return null;
                    }
                    else
                    {
                        Console.WriteLine("Embeds Found...\n");
                    }
                    break;
                }
                offsets.Add(embedOffset);
                //Console.WriteLine(embedOffset);
                currentOffset = embedOffset + 4;
            }

            foreach (var entry in offsets)
            {
                using (ResourceReader eplFile = new ResourceReader(new MemoryStream(eplBytes), false))
                {
                    eplFile.Endianness = Endianness.BigEndian;
                    eplFile.Seek(entry - 4, SeekOrigin.Begin);
                    var dataLength = eplFile.ReadUInt32();
                    byte stringNull;
                    eplFile.Seek(entry - 17, SeekOrigin.Begin);
                    currentOffset = 0;
                    //Console.WriteLine("before {0}", eplFile.Position);
                    do
                    {
                        eplFile.Seek(entry - 17 + currentOffset, SeekOrigin.Begin);
                        //Console.WriteLine("during {0}", eplFile.Position);
                        stringNull = eplFile.ReadByte();
                        currentOffset -= 1;
                    } while (stringNull != 0);
                    //Console.WriteLine("after {0}", eplFile.Position);
                    int stringLength = eplFile.ReadByte();
                    string embedName = eplFile.ReadString(stringLength);
                    modelEntryList.Add((embedName, entry, dataLength));
                };

            }

            for (int i = 1; i < modelEntryList.Count; i++)
            {
                var (name0, offset0, size0) = modelEntryList[i - 1];
                var (name1, offset1, size1) = modelEntryList[i];
                if (offset0 + size0 > offset1)
                {
                    modelEntryList.RemoveAt(i);
                    i -= 1;
                }
            }

            return modelEntryList;
        }

        public static unsafe long IndexOf(byte[] haystack, byte[] needle, long startOffset = 0)
        {
            fixed (byte* h = haystack)
            fixed (byte* n = needle)
            {
                for (byte* hNext = h + startOffset, hEnd = h + haystack.LongLength + 1 - needle.LongLength, nEnd = n + needle.LongLength; hNext < hEnd; hNext++)
                    for (byte* hInc = hNext, nInc = n; *nInc == *hInc; hInc++)
                        if (++nInc == nEnd)
                            return hNext - h;
                return -1;
            }
        }

    }
}