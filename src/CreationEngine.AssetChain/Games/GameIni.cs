using System.Runtime.InteropServices;
using System.Text;

namespace CreationEngine.AssetChain.Games;

internal static class GameIni
{
    internal static string? ReadValue(string path, string section, string key)
    {
        // Unlike MO2's QSettings, game INIs use Windows' quoting and empty-value rules.
        const string missing = "\u0001";
        var value = new StringBuilder(1024);
        while (GetPrivateProfileStringW(section, key, missing, value, (uint)value.Capacity, path)
               == value.Capacity - 1)
        {
            value.EnsureCapacity(checked(value.Capacity * 2));
        }

        var result = value.ToString();
        return result == missing ? null : result;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint GetPrivateProfileStringW(
        string section, string key, string defaultValue, StringBuilder value, uint size, string path);
}
