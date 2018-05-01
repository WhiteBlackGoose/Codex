using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodexTestCSharpLibrary.Cases.InterfaceImplementation
{
    public interface IXedoc
    {

    }

    public interface ITypeWithParams<T0, T1>
    {
    }

    // Test interface implementations only applies to actual base types and not those which appear as type parameters 
    public interface XedocImpl : IXedoc, ITypeWithParams<IXedoc, string>
    {

    }
}
