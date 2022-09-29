using Amicitia.IO;

namespace BetterEPLInjector
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Drag an EPL/BED to extract, or a directory to inject\n");
                Console.WriteLine("Press any key to close...");
                Console.ReadKey();
                return;
            }

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
            Console.ReadKey();
        }

        static void ExtractEPL(string inputEPL)
        {
            Console.WriteLine("EPL detected, looking for models to extract...\n");
            string newEPLDir = ($"{Path.GetDirectoryName(inputEPL)}\\{Path.GetFileNameWithoutExtension(inputEPL)}");
            Directory.CreateDirectory(newEPLDir);
            return;
        }

        static void InjectEPL(string inputDir)
        {
            Console.WriteLine("Directory detected, looking for models to inject...\n");
            string outputEPL = inputDir + ".EPL";

            if (!File.Exists(outputEPL))
            {
                outputEPL = inputDir + ".BED";
                if (!File.Exists(outputEPL))
                {
                    Console.WriteLine("Can't find EPL/BED to inject: {0}", Path.GetFileNameWithoutExtension(outputEPL));
                }
            }

            List<string> gmdFileList = new();
            List<EplContents> EplContentList = new();
            foreach (string gmdFiles in Directory.GetFiles(inputDir))
            {
                if (Path.GetExtension(gmdFiles) == ".GMD")
                {
                    gmdFileList.Add(gmdFiles);
                }
            }

            return;
        }
    }
}