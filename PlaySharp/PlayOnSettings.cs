using Microsoft.Win32;

namespace PlaySharp
{

    /// <summary>
    /// A static class used to get settings from the locally installed PlayOn instance.
    /// </summary>
    public static class PlayOnSettings
    {
        /// <summary>
        /// The 32-bit/64-bit registry key where the PlayOn settings reside.
        /// </summary>
        public const string PlayOnRegistryKey32 = "Software\\MediaMall\\MediaMall\\CurrentVersion\\Settings";
        public const string PlayOnRegistryKey64 = "Software\\Wow6432Node\\MediaMall\\MediaMall\\CurrentVersion\\Settings";

        /// <summary>
        /// Gets the PlayOn Media Storage Location value from the registry.
        /// </summary>
        public static string GetMediaStorageLocation()
        {
            RegistryKey Reg;

            try
            {
                Reg = Registry.LocalMachine.OpenSubKey(PlayOnRegistryKey64);
            }
            catch
            {
                Reg = Registry.LocalMachine.OpenSubKey(PlayOnRegistryKey32);
            }

            foreach (string Item in Reg.GetValue("mediaStoragePaths").ToString().Split('*'))
            if (System.IO.Directory.Exists(Item))
                return Item;
            return "";
        }

        /// <summary>
        /// Gets the PlayOn Video Format value from the registry.
        /// </summary>
        public static string GetPlayLaterVideoFormat()
        {
            RegistryKey Reg;

            try
            {
                Reg = Registry.LocalMachine.OpenSubKey(PlayOnRegistryKey64);
            }
            catch
            {
                Reg = Registry.LocalMachine.OpenSubKey(PlayOnRegistryKey32);
            }

            if (Reg.GetValue("playLaterVideoFormat", 0).ToString() == "1")
                return ".plv";
            else
                return ".mp4";
        }
    }

}
