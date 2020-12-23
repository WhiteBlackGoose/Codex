using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CodexTestCSharpLibrary
{
    public record Record1(int Prop);

    public record RecordType(Record1 R1);
}

namespace System.Runtime.CompilerServices
{
    /// <nodoc />
    public sealed class IsExternalInit
    {
    }
}
