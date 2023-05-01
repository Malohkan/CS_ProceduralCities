using ICities;
using System.Reflection;

namespace ProceduralCities
{

    public class ProceduralCitiesMod : IUserMod
    {

        public string Name
        {
            get { return $"Procedural Cities {Version}"; }
        }

        public string Description
        {
            get { return "Generate cities using algorithms"; }
        }


        public static string Version
        {
            get
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
            }
        }

    }

    // Inherit interfaces and implement your mod logic here
    // You can use as many files and subfolders as you wish to organise your code, as long
    // as it remains located under the Source folder.

}