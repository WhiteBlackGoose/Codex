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
        public static readonly TypedRoutedComamnd<IReferenceSymbol> GoToDefinition = CreateCommand<IReferenceSymbol>();
        public static readonly TypedRoutedComamnd<ITextLineSpanResult> GoToSpan = CreateCommand<ITextLineSpanResult>();
        public static readonly TypedRoutedComamnd<IReferenceSearchResult> GoToReference = CreateCommand<IReferenceSearchResult>();

        private static TypedRoutedComamnd<T> CreateCommand<T>([CallerMemberName] string name = null)
        {
            return new TypedRoutedComamnd<T>(name, typeof(Commands));
        }

    }

    public class TypedRoutedComamnd<T> : RoutedCommand
    {
        public TypedRoutedComamnd(string name, Type ownerType) 
            : base(name, ownerType)
        {
        }
#if BRIDGE
        public void RaiseExecuted(UIElement element, T parameter)
        {
            element.RaiseEvent(new ExecutedRoutedEventArgs(this, parameter, element));
        }
#endif
    }

    public class GoToDefinitionCommandBinding : TypedCommandBinding<IReferenceSymbol>
    {
        public GoToDefinitionCommandBinding() : base(Commands.GoToDefinition) { }
    }

    public class GoToSpanCommandBinding : TypedCommandBinding<ITextLineSpanResult>
    {
        public GoToSpanCommandBinding() : base(Commands.GoToSpan) { }
    }

    public class GoToReferenceCommandBinding : TypedCommandBinding<IReferenceSearchResult>
    {
        public GoToReferenceCommandBinding() : base(Commands.GoToReference) { }
    }

    public abstract class TypedCommandBinding<T> : CommandBinding
    {
        public event TypedExecutedRoutedEventHandler<T> CommandExecuted;

        public TypedCommandBinding(TypedRoutedComamnd<T> command)
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
