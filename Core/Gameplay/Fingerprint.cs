using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace HardModePatch.Core
{
    // A method body's fingerprint: its IL as opcode names and operand names (never metadata tokens, which
    // change on every rebuild), hashed. Hooks that skip or replace a body name the reviewed body's
    // fingerprint; a new mod version whose body differs is left unpatched instead of patched as Unverified.
    public static class Fingerprint
    {
        // The numbers the compiler puts in the names of closures, lambdas, cached delegates, iterators,
        // their fields and local functions count methods in the class, so adding an unrelated method
        // renumbers them. Covers <>c__DisplayClass71_0, <M>b__299_10, <M>d__24, <>9__299_10, <i>5__2 and
        // <M>g__Local|12_0.
        static readonly Regex GeneratedOrdinals = new Regex(@"(__|DisplayClass|\|)\d+(_\d+)?", RegexOptions.CultureInvariant);

        public static string WithoutGeneratedOrdinals(string instruction)
        {
            return GeneratedOrdinals.Replace(instruction, "$1#");
        }

        public static string Of(IEnumerable<string> instructions)
        {
            var text = new StringBuilder();
            foreach (var instruction in instructions) text.Append(instruction).Append('\n');
            using (var sha = SHA256.Create())
            {
                var digest = sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()));
                var hex = new StringBuilder();
                for (int index = 0; index < 8; index++) hex.Append(digest[index].ToString("x2"));
                return hex.ToString();
            }
        }
    }

    // A method whose body the patch depends on, and the reviewed fingerprint of that body.
    public sealed class Contract
    {
        public readonly string Name;
        public readonly string Expected;
        public Contract(string name, string expected) { Name = name; Expected = expected; }
    }
}
