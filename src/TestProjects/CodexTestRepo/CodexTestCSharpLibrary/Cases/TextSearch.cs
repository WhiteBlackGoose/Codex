using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CodexTestCSharpLibrary
{
    /// <summary>
    /// This class is used to verify text search results
    /// </summary>
    class TextSearch
    {
        public static int GetNextLineNumber([CallerLineNumber] int lineNumber = 0) => lineNumber + 1;

        public static int CommentWithSameTextLineNumber1 = GetNextLineNumber();
        // Comment with same text

        public static int MultiLineCommentLineNumber = GetNextLineNumber();
        // Multiline comment where
        // we should find the comment when
        // searching for phrase spanning lines

        public static int CommentWithSameTextLineNumber2 = GetNextLineNumber();
        // Comment with SAME text

        public static int MultiCommentWithSameTextLineNumber3 = GetNextLineNumber();
        // Multiline comment 
        // with same text
    }
}
