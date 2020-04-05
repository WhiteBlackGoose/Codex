using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Codex.ObjectModel;

namespace Codex.Search
{
    public static class SearchUtilities
    {
        public static QualifiedNameTerms CreateNameTerm(this string nameTerm)
        {
            var terms = new QualifiedNameTerms();
            string secondaryNameTerm = string.Empty;
            if (!string.IsNullOrEmpty(nameTerm))
            {
                nameTerm = nameTerm.Trim();
                nameTerm = nameTerm.TrimStart('"');
                if (!string.IsNullOrEmpty(nameTerm))
                {
                    terms.RawNameTerm = nameTerm;

                    if (nameTerm.EndsWith("\""))
                    {
                        nameTerm = nameTerm.TrimEnd('"');
                        nameTerm += "^";
                    }

                    if (!string.IsNullOrEmpty(nameTerm))
                    {
                        if (nameTerm[0] == '*')
                        {
                            nameTerm = nameTerm.TrimStart('*');
                            secondaryNameTerm = nameTerm.Trim();
                            nameTerm = "^" + secondaryNameTerm;
                        }
                        else
                        {
                            nameTerm = "^" + nameTerm;
                        }
                    }
                }
            }

            terms.NameTerm = nameTerm;
            terms.SecondaryNameTerm = secondaryNameTerm;

            return terms;
        }

        public static QualifiedNameTerms ParseContainerAndName(string fullyQualifiedTerm)
        {
            QualifiedNameTerms terms = new QualifiedNameTerms();
            int indexOfLastDot = fullyQualifiedTerm.LastIndexOf('.');
            if (indexOfLastDot >= 0)
            {
                terms.ContainerTerm = fullyQualifiedTerm.Substring(0, indexOfLastDot);
            }

            terms.NameTerm = fullyQualifiedTerm.Substring(indexOfLastDot + 1);
            if (terms.NameTerm.Length > 0)
            {
                terms.RawNameTerm = terms.NameTerm;
                terms.NameTerm = "^" + terms.NameTerm;
            }
            return terms;
        }

        public class ReferenceSearchExtensionData : ExtensionData
        {
            public string ProjectScope;
        }

        public static Symbol SetProjectScope(this Symbol symbol, string projectScope)
        {
            var refData = symbol.GetReferenceSearchExtensionData();
            if (refData == null)
            {
                refData = new ReferenceSearchExtensionData();
                symbol.SetReferenceSearchExtensionData(refData);
            }

            refData.ProjectScope = projectScope;
            return symbol;
        }

        public static Symbol SetReferenceSearchExtensionData(this Symbol symbol, ReferenceSearchExtensionData data)
        {
            symbol.ExtData = data;
            return symbol;
        }

        public static ReferenceSearchExtensionData GetReferenceSearchExtensionData(this Symbol symbol)
        {
            return symbol.ExtData as ReferenceSearchExtensionData;
        }
    }
}
