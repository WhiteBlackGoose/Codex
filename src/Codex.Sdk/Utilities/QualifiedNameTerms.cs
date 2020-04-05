using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Codex.ObjectModel;

namespace Codex.Search
{
    public class QualifiedNameTerms
    {
        public string ContainerTerm = string.Empty;
        public string RawNameTerm = string.Empty;
        public string NameTerm = string.Empty;
        public string SecondaryNameTerm = string.Empty;
        public string ExactNameTerm => NameTerm + "^";

        public bool HasContainerName
        {
            get
            {
                return !string.IsNullOrEmpty(ContainerTerm);
            }
        }

        public bool HasName
        {
            get
            {
                return !string.IsNullOrEmpty(NameTerm);
            }
        }
    }
}