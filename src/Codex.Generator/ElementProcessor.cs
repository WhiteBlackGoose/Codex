using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Codex.Generator
{
    public class ProcessorContext
    {
        public ViewGenerator Generator { get; set; }
        public CodeStatementCollection Statements { get; set; }
        private int Id { get; set; }
        public string ParentName { get; set; }

        public string TargetElementName { get; set; }

        public string ViewModelVariableName { get; set; }

        public ProcessorContext CreateChildContext(CodeStatementCollection statements = null, string viewModelName = null)
        {
            throw new NotImplementedException();
        }

        public void AddStatements(params CodeStatement[] statements)
        {
            Statements.AddRange(statements);
        }

        public int NextId()
        {
            return Id++;
        }
    }
}
