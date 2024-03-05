using GFDLibrary.Effects;
using GFDLibrary.IO;
using GFDLibrary.IO.Common;
using System.IO;
using System.Reflection.PortableExecutable;
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
                foreach (var arg in args)
                {
                    if (File.Exists(arg))
                    {
                        ExtractEffectEmbeds(arg, Path.Join(Path.GetDirectoryName(arg), Path.GetFileNameWithoutExtension(arg)));
                    }
                    else if (Directory.Exists(arg))
                    {
                        InjectEffectEmbeds(arg);
                    }
                    else
                    {
                        Console.WriteLine("Invalid Input. Drag an EPL, BED, or EPT to extract, or a directory to inject\n");
                    }
                }
            }
        }

        static void ExtractEffectEmbeds(string inputEPL, string outputFolder)
        {
            string magic = string.Empty;
            var extension = Path.GetExtension(inputEPL).ToLower();

            if (extension == ".epl" || extension == ".bed") magic = "GFS0";
            else if (extension == ".ept" || extension == ".epd") magic = "DDS ";
            else
            {
                Console.WriteLine("Invalid filetype");
                return;
            }

            Console.WriteLine($"{Path.GetExtension(inputEPL)} detected, looking for embeds to extract...\n");

            var eplBytes = File.ReadAllBytes(inputEPL);
            var foundEmbeds = TryFindEmbeds(eplBytes, magic, out var modelEntryList);

            if (!foundEmbeds)
            {
                Console.WriteLine("Couldn't find any embeds.");
                return;
            }

            foreach (var (name, offset, size) in modelEntryList)
            {
                string newEmbedPath = Path.Join(outputFolder, name);
                Directory.CreateDirectory(Path.GetDirectoryName(newEmbedPath));

                using (var stream = File.Open(newEmbedPath, FileMode.Create))
                {
                    stream.Write(eplBytes[offset..(offset + size)]);
                }

                Console.WriteLine($"Extracted {name}");
            }

            foreach (var nestedFile in Directory.GetFiles(outputFolder))
            {
                ExtractEffectEmbeds(nestedFile, Path.Join(outputFolder, Path.GetFileNameWithoutExtension(nestedFile)));
            }
            
            return;
        }

        static bool InjectEffectEmbeds(string extractedFiles)
        {
            Console.WriteLine("Looking for embeds to inject...\n");

            var nestedFiles = Directory.GetDirectories(extractedFiles);

            var foundFile = TryFindOriginalResourceFile(extractedFiles, out var outputResource, out var magic);

            if (!foundFile)
            {
                Console.WriteLine($"Cannot find resource file: {extractedFiles}.XXX");
                return false;
            };

            // Recursively inject nested files
            foreach (var nestedFile in nestedFiles)
            {
                InjectEffectEmbeds(nestedFile);
            }

            byte[] eplBytes = File.ReadAllBytes(outputResource);
            var foundEmbeds = TryFindEmbeds(eplBytes, magic, out var modelEntryList);

            if (!foundEmbeds)
            {
                Console.WriteLine("Couldn't find embeds in original file.");
                return false;
            }

            int currentOffset = 0;

            var outputFileStream = File.Create(outputResource);
            var outputFileWriter = new EndianBinaryWriter(outputFileStream, Endianness.BigEndian);

            try
            {
                foreach (var (name, offset, size) in modelEntryList)
                {
                    string embedFileDirectory = $"{extractedFiles}\\{name}";

                    if (!File.Exists(embedFileDirectory))
                    {
                        Console.WriteLine($"Missing embed file: {embedFileDirectory}, using source file");
                        continue;
                    }

                    Console.WriteLine($"Injecting {name}");

                    // Write bytes up to embed
                    outputFileStream.Write(eplBytes[currentOffset..(offset - 4)]);

                    // Write embed data
                    var embedBytes = File.ReadAllBytes(embedFileDirectory);
                    outputFileWriter.Write(embedBytes.Length);
                    outputFileWriter.Write(embedBytes);

                    currentOffset = offset + size;
                }

                outputFileStream.Write(eplBytes[currentOffset..]);
            }
            finally
            {
                outputFileStream.Close();
                outputFileWriter.Close();
            }

            return true;
        }

        static bool TryFindEmbeds(byte[] data, string magic, out List<(string modelName, int modelOffset, int modelSize)> modelEntryList)
        {
            modelEntryList = [];

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            long currentOffset = 4;

            var resourceStream = new MemoryStream(data);
            var resourceReader = new EndianBinaryReader(resourceStream, Encoding.GetEncoding(932), Endianness.BigEndian);

            try
            {
                while (true)
                {
                    var embedOffset = (int)IndexOf(data, Encoding.ASCII.GetBytes(magic), currentOffset);

                    if (embedOffset == -1)
                    {
                        return modelEntryList.Count != 0;
                    }

                    // Position stream at the embed size value
                    resourceStream.Position = embedOffset - 4;
                    var embedSize = resourceReader.ReadInt32();

                    /* Find file name */
                    byte x;

                    // Move stream to the end of the embed name
                    resourceStream.Seek(-17, SeekOrigin.Current);

                    while (true)
                    {
                        x = (byte)resourceStream.ReadByte();

                        if (x == 0) break;
                        resourceStream.Seek(-2, SeekOrigin.Current);
                    };

                    currentOffset = embedOffset + embedSize;
                    modelEntryList.Add((resourceReader.ReadString(), embedOffset, embedSize));
                }
            }
            finally
            {
                resourceStream.Close();
                resourceReader.Close();
            }
        }

        static bool TryFindOriginalResourceFile(string extractedFilesDirectory, out string resourceFilePath, out string magicPattern)
        {
            (string, string)[] extensions = [(".EPL", "GFS0"), (".BED", "GFS0"), (".EPT", "DDS "), (".EPD", "DDS ")];

            resourceFilePath = string.Empty;
            magicPattern = string.Empty;

            foreach (var (extension, magic) in extensions)
            {
                resourceFilePath = extractedFilesDirectory + extension;
                magicPattern = magic;

                if (File.Exists(resourceFilePath))
                {
                    return true;
                }
            }

            return false;
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