using Bridge;
using Bridge.Html5;
using Monaco;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[External]
public static partial class monaco
{
    public static partial class editor
    {
        public static extern IStandaloneCodeEditor create(HTMLElement domElement, EditorConstructionOptions options);

        [Virtual]
        public interface IStandaloneCodeEditor
        {
            void layout();
        }
    }
}

namespace Monaco
{
    [ObjectLiteral]
    public class EditorConstructionOptions : EditorOptions
    {
        /// <summary>
        /// The initial text content of the of the editor
        /// </summary>
        public string value { get; set; }

        public string language { get; set; }
    }

    [ObjectLiteral]
    public class EditorOptions
    {
        public bool readOnly { get; set; }
    }
}
