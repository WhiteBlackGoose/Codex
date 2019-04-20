using System;
using System.Linq;

namespace Codex.Utilities
{
    /// <summary>
    /// Generic class for object that performs some action when disposed.
    /// </summary>
    public class DisposeAction : IDisposable
    {
        private Action[] _exitActions;

        /// <summary>
        /// Creates a new <see cref="DisposeAction"/> that disposes of the given <see cref="IDisposable"/>
        /// objects when disposed
        /// </summary>
        /// <param name="disposables">the objects to dispose when this object is disposed</param>
        public DisposeAction(params IDisposable[] disposables)
            : this(disposables.Select(scope => new Action(() => scope.Dispose())).ToArray())
        {
        }

        /// <summary>
        /// Creates a new <see cref="DisposeAction"/> that executes of the given <see cref="Action"/>
        /// functions when disposed
        /// </summary>
        /// <param name="exitActions">the functions to execute when this object is disposed</param>
        public DisposeAction(params Action[] exitActions)
        {
            _exitActions = exitActions;
        }

        /// <summary>
        /// Performs actions associated with this <see cref="DisposeAction"/>
        /// </summary>
        public void Dispose()
        {
            Array.ForEach(_exitActions, exitAction => exitAction());
        }
    }
}
