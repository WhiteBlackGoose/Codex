using Bridge;
using Bridge.Html5;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[External]
[Module(true, LoadName = "vs/editor/editor.main", Name = "monaco_editor_ignored")]
public static partial class monaco { }

namespace Monaco
{
    /// <summary>
    /// Responsible for loading the monaco editor javascript library using the loader.js
    /// included with monaco
    /// </summary>
    public static class MonacoLibrary
    {
        private static Task initializeTask;

        public static Task InitializeAsync()
        {
            if (initializeTask == null)
            {
                // Only initialize once
                initializeTask = InitializeCoreAsync();
            }

            return initializeTask;
        }

        private static async Task InitializeCoreAsync()
        {
            Script.Write("require.config({ paths: { 'vs': 'node_modules/monaco-editor/dev/vs' } })");
            await Bridge.Module.Load(typeof(monaco));
        }
    }
}
