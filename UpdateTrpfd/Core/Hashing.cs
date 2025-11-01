using System.Text;
namespace TrpfdManager.Core;
internal static class GfFnv1a64
{
    private const ulong Prime = 0x00000100000001B3, Basis = 0xCBF29CE484222645;
    public static ulong Hash(string s) { ulong h = Basis; foreach (var b in Encoding.UTF8.GetBytes(s)) { h ^= b; h *= Prime; } return h; }
}