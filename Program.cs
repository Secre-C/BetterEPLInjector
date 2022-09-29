using Amicitia.IO;

namespace BetterEPLInjector
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Drag an EPL/BCD to extract, or a directory to inject\n");
                Console.ReadKey();
                return;
            }
            foreach (string arg in args)
            {
                FileInfo argInfo = new FileInfo(arg);
                if (argInfo.Extension == ".EPL" || argInfo.Extension == ".BCD")
                {
                    ExtractEPL(arg);
                }
                else if (argInfo.Extension == "")
                {
                    InjectEPL(arg);
                }
                else
                {
                    Console.WriteLine("Invalid Input. Drag an EPL/BCD to extract, or a directory to inject\n");
                }
            }
            Console.ReadKey();
        }

        static void ExtractEPL(string inputEPL)
        {
            Console.WriteLine("EPL detected, looking for models to extract...\n");
            return;
        }

        static void InjectEPL(string InputDir)
        {
            Console.WriteLine("Directory detected, looking for models to inject...\n");
            foreach (string gmdFiles in Directory.GetFiles(InputDir))
            {
                FileInfo fileInfo = new(gmdFiles);
                if (fileInfo.Extension == ".GMD")
                {
                    Console.WriteLine(gmdFiles);
                }
            }
            return;
        }
    }
}