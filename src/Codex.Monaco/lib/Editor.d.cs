using Bridge;
using Bridge.Html5;
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
        public static extern IStandaloneCodeEditor create(HTMLElement domElement, IEditorConstructionOptions options);

        [ObjectLiteral]
        public struct IEditorConstructionOptions
        {
            public string value { get; set; }

            public string language { get; set; }
        }

        public interface IStandaloneCodeEditor
        {

        }
    }
}
