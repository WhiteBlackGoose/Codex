﻿using System;
using Windows.UI.Xaml;

namespace Codex.Uno.Wasm
{
    public class Program
    {
        private static App _app;

        public static int Main(string[] args)
        {
            Windows.UI.Xaml.Application.Start(_ => _app = new App());

            return 0;
        }
    }
}
