using GFDLibrary.Models;
using GFDLibrary.IO.Common;
using System.Text;
using GFDLibrary;

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
                        ExtractEmbeds(arg, Path.Join(Path.GetDirectoryName(arg), Path.GetFileNameWithoutExtension(arg)));
                    }
                    else if (Directory.Exists(arg))
                    {
                        //InjectEffectEmbeds(arg);
                        ExtractInDirectory(arg, Path.Join(arg, "EPL"));
                    }
                    else
                    {
                        Console.WriteLine("Invalid Input. Drag an EPL, BED, or EPT to extract, or a directory to inject\n");
                    }
                }
            }
        }

        static void ExtractInDirectory(string path, string output)
        {
            var files = Directory.GetFiles(path, "*.*", new EnumerationOptions { RecurseSubdirectories = true });

            var _files = files.Where(x => Path.GetExtension(x).Equals(".epl", StringComparison.OrdinalIgnoreCase) ||
                Path.GetExtension(x).Equals(".bed", StringComparison.OrdinalIgnoreCase) ||
                Path.GetExtension(x).Equals(".ept", StringComparison.OrdinalIgnoreCase) ||
                Path.GetExtension(x).Equals(".epd", StringComparison.OrdinalIgnoreCase) ||
                Path.GetExtension(x).Equals(".gfs", StringComparison.OrdinalIgnoreCase) ||
                Path.GetExtension(x).Equals(".gmd", StringComparison.OrdinalIgnoreCase)
            );

            foreach (var file in _files)
            {
                try
                {
                    ExtractEmbeds(file, output);
                }
                catch { Console.WriteLine($"failed to extract {file}"); }
            }
        }

        static void ExtractEmbeds(string inputResource, string outputFolder)
        {
            var extension = Path.GetExtension(inputResource).ToLower();

            string magic;
            if (extension == ".epl" || extension == ".bed") magic = "GFS0";
            else if (extension == ".ept" || extension == ".epd") magic = "DDS ";
            else if (extension == ".gfs" || extension == ".gmd")
            {
                ExtractEmbedsFromModel(inputResource, outputFolder);
                return;
            }
            else
            {
                Console.WriteLine("Invalid filetype");
                return;
            }

            Console.WriteLine($"{Path.GetExtension(inputResource)} detected, looking for embeds to extract...\n");

            var eplBytes = File.ReadAllBytes(inputResource);
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
                    stream.Write(eplBytes.AsSpan()[offset..(offset + size)]);
                }

                ExtractEmbeds(newEmbedPath, outputFolder);

                Console.WriteLine($"Extracted {name}");
            }

            return;
        }

        static void ExtractEmbedsFromModel(string modelPath, string outputFolder)
        {
            try
            {
                var model = Resource.Load<ModelPack>(modelPath);

                if (model == null || model.Model == null)
                    return;

                ExtractEplFromModelNode(model.Model.RootNode, outputFolder);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Console.WriteLine("Failed to extract model");
            }
        }

        static void ExtractEplFromModelNode(Node node, string outputFolder)
        {
            foreach (var attachment in node.Attachments)
            {
                string outFile;

                if (attachment.Type == NodeAttachmentType.Epl)
                {
                    outFile = Path.Join(outputFolder, node.Name + ".EPL");
                }
                else if (attachment.Type == NodeAttachmentType.Model)
                {
                    outFile = Path.Join(outputFolder, node.Name + ".GMD");
                }
                else continue;

                attachment.GetValue().Save(outFile);
                ExtractEmbeds(outFile, outputFolder);
            }

            foreach (var child in node.Children)
            {
                ExtractEplFromModelNode(child, outputFolder);
            }
        }

        static void InjectEffectEmbeds(string extractedFiles)
        {
            Console.WriteLine("Looking for embeds to inject...\n");

            var nestedFiles = Directory.GetDirectories(extractedFiles);

            var foundFile = TryFindOriginalResourceFile(extractedFiles, out var outputResource, out var magic);

            if (!foundFile)
            {
                Console.WriteLine($"Cannot find resource file: {extractedFiles}.XXX");
                return;
            };

            byte[] eplBytes = File.ReadAllBytes(outputResource);
            var foundEmbeds = TryFindEmbeds(eplBytes, magic, out var modelEntryList);

            if (!foundEmbeds)
            {
                Console.WriteLine("Couldn't find embeds in original file.");
                return;
            }

            // Recursively inject nested files
            foreach (var nestedFile in nestedFiles)
            {
                InjectEffectEmbeds(nestedFile);
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
                    outputFileStream.Write(eplBytes.AsSpan()[currentOffset..(offset - 4)]);

                    // Write embed data
                    var embedBytes = File.ReadAllBytes(embedFileDirectory);
                    outputFileWriter.Write(embedBytes.Length);
                    outputFileWriter.Write(embedBytes);

                    currentOffset = offset + size;
                }

                outputFileStream.Write(eplBytes.AsSpan()[currentOffset..]);
            }
            finally
            {
                outputFileStream.Close();
                outputFileWriter.Close();
            }
        }

        static bool TryFindEmbeds(byte[] data, string magic, out List<(string modelName, int modelOffset, int modelSize)> modelEntryList)
        {
            modelEntryList = [];

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            int currentOffset = 4;
            var searchPattern = Encoding.ASCII.GetBytes(magic).AsSpan();

            var resourceStream = new MemoryStream(data);
            var resourceReader = new EndianBinaryReader(resourceStream, Encoding.GetEncoding(932), Endianness.BigEndian);

            try
            {
                while (true)
                {
                    var embedOffset = MemoryExtensions.IndexOf(data.AsSpan()[currentOffset..], searchPattern);

                    if (embedOffset == -1)
                    {
                        return modelEntryList.Count != 0;
                    }

                    embedOffset += currentOffset;

                    // Position stream at the embed size value
                    resourceStream.Position = embedOffset - 4;
                    var embedSize = resourceReader.ReadInt32();

                    // For rare cases that the embeds with extensions GFS are followed by a '0'
                    if (embedSize > data.Length)
                    {
                        currentOffset += 4;
                        continue;
                    }

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
    }
}
