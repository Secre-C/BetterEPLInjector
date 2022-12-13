using GFDLibrary.IO;
using GFDLibrary.IO.Common;
using System.Text;

namespace BetterEPLInjector
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Drag an EPL, BED, EPT, or EPD to extract, or a directory to inject\n");
                return;
            }
            else
            {
                foreach (string arg in args)
                {
                    if (Path.GetExtension(arg).ToLower() == ".epl" || Path.GetExtension(arg).ToLower() == ".bed")
                    {
                        Extract(arg, "GFS0");
                    }
                    else if (Path.GetExtension(arg).ToLower() == ".ept" || Path.GetExtension(arg).ToLower() == ".epd")
                    {
                        Extract(arg, "DDS ");
                    }
                    else if (Path.GetExtension(arg) == "")
                    {
                        Inject(arg);
                    }
                    else
                    {
                        Console.WriteLine("Invalid Input. Drag an EPL, BED, or EPT to extract, or a directory to inject\n");
                    }
                }
            }
        }

        static void Extract(string inputEPL, string magic)
        {
            Console.WriteLine($"{Path.GetExtension(inputEPL)} detected, looking for embeds to extract...\n");
            string newEPLDir = $"{Path.GetDirectoryName(inputEPL)}\\{Path.GetFileNameWithoutExtension(inputEPL)}";

            byte[] eplBytes = File.ReadAllBytes(inputEPL);
            List < (string modelName, long modelOffset, long modelSize) > modelEntryList = FindEmbeddedModels(eplBytes, magic);

            if (modelEntryList == null) return;

            Directory.CreateDirectory(newEPLDir);

            foreach (var modelEntry in modelEntryList)
            {
                var (name, offset, size) = modelEntry;
                string newEmbedPath = $"{newEPLDir}\\{name}";
                Directory.CreateDirectory(Path.GetDirectoryName(newEmbedPath));

                using (var stream = File.Open(newEmbedPath, FileMode.Create))
                {
                    using (ResourceReader eplFile = new(new MemoryStream(eplBytes), false))
                    {
                        using (BinaryWriter eplEmbedFile = new(stream))
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
        static void Inject(string inputDir)
        {
            Console.WriteLine("Directory detected, looking for embeds to inject...\n");
            string magic = "GFS0";

            string outputEPL = inputDir + ".EPL";

            if (!File.Exists(outputEPL))
            {
                outputEPL = inputDir + ".BED";
                if (!File.Exists(outputEPL))
                {
                    outputEPL = inputDir + ".EPT";
                    magic = "DDS ";
                    if (!File.Exists(outputEPL))
                    {
                        outputEPL = inputDir + ".EPD";
                        if (!File.Exists(outputEPL))
                        {
                            Console.WriteLine("Can't find file to inject: {0}\n", Path.GetFileNameWithoutExtension(outputEPL));
                            return;
                        }
                    }
                }
            }

            byte[] eplBytes = File.ReadAllBytes(outputEPL);
            List<(string modelName, long modelOffset, long modelSize)> modelEntryList = FindEmbeddedModels(eplBytes, magic);
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

                    using (ResourceReader eplFile = new(new MemoryStream(eplBytes), false))
                    {
                        using (var stream = File.Open($"{outputEPL}_temp", FileMode.Open))
                        {
                            using (BinaryWriter newEplFile = new (stream))
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

                    using (ResourceReader embedFile = new (new MemoryStream(embedBytes), false))
                    {
                        using (ResourceWriter newEplFile = new (File.Open($"{outputEPL}_temp", FileMode.Open), false))
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
                    Console.WriteLine($"Can't Find {name}, Using Embed from Source...");
                    using (ResourceReader eplFile = new (new MemoryStream(eplBytes), false))
                    {
                        using (var stream = File.Open($"{outputEPL}_temp", FileMode.Open))
                        {
                            using (BinaryWriter newEplFile = new (stream))
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
                                newEplFile.Write(eplFile.ReadBytes((int)offset - (int)currentOffset));
                                newEplFile.Close();
                            }
                        }
                    }

                    using (ResourceReader eplFile = new (new MemoryStream(eplBytes), false))
                    {
                        using (var stream = File.Open($"{outputEPL}_temp", FileMode.Open))
                        {
                            using (BinaryWriter newEplFile = new (stream))
                            {
                                eplFile.Seek(offset, SeekOrigin.Begin);
                                newEplFile.Seek(0, SeekOrigin.End);
                                newEplFile.Write(eplFile.ReadBytes((int)size));
                                newEplFile.Close();
                            }
                        }
                    }
                }
                index++;
            }

            using (ResourceReader eplFile = new (new MemoryStream(eplBytes), false)) //write until EOF
            {
                var (name, offset, size) = modelEntryList[modelEntryList.Count - 1];
                
                using (var stream = File.Open($"{outputEPL}_temp", FileMode.Open))
                {
                    using (BinaryWriter newEplFile = new(stream))
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
            }
            File.Copy($"{outputEPL}_temp", outputEPL, true);
            File.Delete($"{outputEPL}_temp");
            return;
        }

        static List<(string modelName, long modelOffset, long modelSize)> FindEmbeddedModels(byte[] eplBytes, string magic)
        {
            List<(string modelName, long modelOffset, long modelSize)> modelEntryList = new();
            var offsets = new List<long>();

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            byte[] magicBytes = Encoding.GetEncoding(932).GetBytes(magic);

            long currentOffset = 4;

            while (true)
            {
                var embedOffset = IndexOf(eplBytes, magicBytes, currentOffset);
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
                currentOffset = embedOffset + 4;
            }

            foreach (var entry in offsets)
            {
                using (ResourceReader eplFile = new(new MemoryStream(eplBytes), false))
                {
                    eplFile.Endianness = Endianness.BigEndian;
                    eplFile.Seek(entry - 4, SeekOrigin.Begin);
                    var dataLength = eplFile.ReadUInt32(); //retrieve embed filesize
                    byte stringNull;

                    eplFile.Seek(entry - 17, SeekOrigin.Begin);
                    currentOffset = 0;

                    do
                    {
                        eplFile.Seek(entry - 17 + currentOffset, SeekOrigin.Begin);
                        stringNull = eplFile.ReadByte();
                        currentOffset -= 1;
                    } while (stringNull != 0); //find beginning of embed file name

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
                    modelEntryList.RemoveAt(i); //prevents files embedded in embeds from being extracted
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