using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodexTestCSharpLibrary.Cases.MemberOverride
{
    public abstract class XedocBase
    {
        public abstract void Abstract();

        public virtual void Virtual()
        {

        }
    }

    public class XedocImpl : XedocBase
    {
        public override void Abstract()
        {
        }

        public override void Virtual()
        {
        }
    }
}
