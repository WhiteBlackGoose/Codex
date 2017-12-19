using Codex.Sdk.Search;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Codex.View
{
    public static class Commands
    {
        public static readonly RoutedCommand GoToDefinition = CreateCommand();
        public static readonly RoutedCommand GoToSpan = CreateCommand();

        private static RoutedCommand CreateCommand([CallerMemberName] string name = null)
        {
            return new RoutedCommand(name, typeof(Commands));
        }
    }

    public class GoToDefinitionCommandBinding : TypedCommandBinding<IReferenceSymbol>
    {
        public GoToDefinitionCommandBinding() : base(Commands.GoToDefinition) { }
    }

    public class GoToSpanCommandBinding : TypedCommandBinding<ITextLineSpanResult>
    {
        public GoToSpanCommandBinding() : base(Commands.GoToSpan) { }
    }

    public abstract class TypedCommandBinding<T> : CommandBinding
    {
        public event TypedExecutedRoutedEventHandler<T> CommandExecuted;

        public TypedCommandBinding(RoutedCommand command)
        {
            Command = command;
            base.Executed += TypedCommandBinding_Executed;
        }

        private void TypedCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var parameter = e.Parameter;
            var typedParameter = (T)parameter;
            CommandExecuted?.Invoke(typedParameter);
        }
    }

    public delegate void TypedExecutedRoutedEventHandler<T>(T parameter);
}
