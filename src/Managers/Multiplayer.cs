using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PolyMod.Managers
{
    public static class Multiplayer
    {
        public static void Init()
        {
            BuildConfigHelper.GetSelectedBuildConfig().buildServerURL = BuildServerURL.Localhost;
        }
    }
}