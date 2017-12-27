using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.CodeDom;
using System.Xml.Linq;
using Codex.View.Web;
using System.Web;

namespace Codex.Generator
{
    [TestFixture]
    public class ViewGenerator
    {
        public delegate void Processor(XElement element, ProcessorContext context);

        private readonly Dictionary<string, Processor> processors;
        private readonly Dictionary<string, ValueDescription> htmlAttributeMappings = new Dictionary<string, ValueDescription>()
        {
            { "class", "ClassName" },
            { "id", "Id" },
            { "src", "Src" },
            { "onClick", "OnClick" },
            { "height", Literal("Height") },
            { "width", Literal("Width") },
        };

        private readonly Dictionary<string, char> replacementTokens = new Dictionary<string, char>();

        public ViewGenerator()
        {
            processors = new Dictionary<string, Processor>()
            {
                { "ForEach", ProcessForEach },
                { "Render", ProcessRender },
            };
        }

        /// <summary>
        /// This test doesn't actually verify anything. It just provides an easy way of viewing mappings
        /// </summary>
        //[Test]
        public void GenerateView()
        {
            CodeNamespace nspace = new CodeNamespace("Codex.View");
            XDocument document = XDocument.Load(ViewsPath.ViewsFilePath);

            foreach (var viewModel in document.Root.Elements("ViewModel"))
            {
                ProcessViewModel(nspace, viewModel);
            }
        }

        private void ProcessViewModel(CodeNamespace nspace, XElement viewModel)
        {
            var viewModelClass = new CodeTypeDeclaration($"{viewModel.Attribute("Name").Value}")
            {
                IsClass = true,
                Attributes = MemberAttributes.Public
            };

            var renderMethod = new CodeMemberMethod()
            {
                Name = "Render",
                Attributes = MemberAttributes.Public
            };

            viewModelClass.Members.Add(renderMethod);

            var processorContext = new ProcessorContext()
            {
                Generator = this,
                Statements = renderMethod.Statements,
                TargetElementName = "parentElement",
                ViewModelVariableName = "this"
            };

            foreach (var node in viewModel.Nodes())
            {
                ProcessNode(node, processorContext);
            }
        }

        public void ProcessNode(XNode node, ProcessorContext context)
        {
            if (node is XElement)
            {
                ProcessElement((XElement)node, context);
            }
            else if (node is XText)
            {
                var text = ((XText)node).Value;
                text = text.Trim();
                if (text.Length == 0)
                {
                    return;
                }

                text = HttpUtility.HtmlDecode(text);
                text = GetValue(text, context, ValueHandling.Text);

                context.Statements.Add(new CodeSnippetStatement(
                    $@"{context.TargetElementName}.AppendChild(new Text(""{text}""));"));
            }
        }

        public Processor HtmlProcessor(string typeName = null)
        {
            return (element, context) =>
            {
                var elementTag = element.Name.LocalName;
                var id = context.NextId();
                var elementName = elementTag + id;
                var constructor = typeName == null ?
                    $@"new HTMLElement(""{elementTag}"")" :
                    $"new {typeName}()";
                context.AddStatements(new CodeSnippetStatement($"var {elementName} = new {constructor};"));

                foreach (var attribute in element.Attributes())
                {
                    var name = attribute.Name.LocalName;
                    var value = attribute.Value;
                    ValueDescription propertyDescription;
                    if (htmlAttributeMappings.ContainsKey(name))
                    {
                        propertyDescription = htmlAttributeMappings[name];
                    }
                    else
                    {
                        propertyDescription = name;
                    }

                    context.AddStatements(new CodeSnippetStatement($"{elementName}.{propertyDescription.Name} = {GetValue(value, context, propertyDescription.Handling)};"));
                }
            };
        }

        private string GetValue(string value, ProcessorContext context, ValueHandling valueHandling)
        {
            value.Trim();
            if (valueHandling == ValueHandling.Literal)
            {
                return value;
            }

            if (value.StartsWith("{") && !value.StartsWith("{{") && value.EndsWith("}") && !value.EndsWith("}}"))
            {
                return value.Substring(1, value.Length - 2);
            }

            var tokenizedValue = value;
            tokenizedValue = tokenizedValue.Replace("{{", GetReplacementToken(0));
            tokenizedValue = tokenizedValue.Replace("}}", GetReplacementToken(1));

            if (value.Contains("{"))
            {
                return "$\"" + value + "\"";
            }
            else
            {
                return "\"" + tokenizedValue.Replace(GetReplacementToken(0), "{").Replace(GetReplacementToken(1), "}") + "\"";
            }
        }

        private string GetReplacementToken(int tokenIndex)
        {
            return ((char)(char.MaxValue - tokenIndex)).ToString();
        }

        public void ProcessElement(XElement element, ProcessorContext context)
        {
            var processor = processors[element.Name.LocalName];
            processor(element, context);
        }

        public void ProcessRender(XElement element, ProcessorContext context)
        {
            var viewModelName = element.Attribute("Value").Value;
            context.Statements.Add(new CodeSnippetStatement($"{viewModelName}.Render({context.TargetElementName}, context);"));
        }

        public void ProcessForEach(XElement element, ProcessorContext context)
        {
            var id = context.NextId();
            var enumeratorName = $"e{id}";
            context.Statements.Add(new CodeSnippetStatement($"var {enumeratorName} = {element.Attribute("Items").Value}.GetEnumerator();"));
            var whileMoveNext = new CodeIterationStatement()
            {
                TestExpression = new CodeSnippetExpression($"{enumeratorName}.MoveNext()")
            };

            var childContext = context.CreateChildContext(statements: whileMoveNext.Statements, viewModelName: $"item{id}");

            foreach (var child in element.Nodes())
            {
                ProcessNode(child, childContext);
            }

            context.Statements.Add(whileMoveNext);
        }

        public static ValueDescription Literal(string name)
        {
            return new ValueDescription()
            {
                Name = name,
                Handling = ValueHandling.Literal
            };
        }

        public enum ValueHandling
        {
            Text,
            Literal
        }

        public class ValueDescription
        {
            public string Name;
            public ValueHandling Handling;

            public static implicit operator ValueDescription(string value)
            {
                return new ValueDescription()
                {
                    Name = value,
                    Handling = ValueHandling.Text
                };
            }
        }
    }
}
