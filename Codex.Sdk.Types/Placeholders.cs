﻿using System;
using System.Collections.Generic;
using System.Text;
using Codex.ObjectModel;

namespace Codex
{
    public interface ISymbolLineSpanList
    {
        IReadOnlyList<SymbolSpan> ToList();
    }
}
