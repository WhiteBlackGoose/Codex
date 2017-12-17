using Bridge;
using Bridge.Html5;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static monaco.editor;

namespace Monaco
{
    public static class Editor
    {
        public static async Task<IStandaloneCodeEditor> Create(HTMLElement htmlElement, string text)
        {
            await MonacoLibrary.InitializeAsync();

            return create(htmlElement, new IEditorConstructionOptions
            {
                value = text,
                language = "javascript"
            });
        }
    }
}