using Codex.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Codex
{
    /// <summary>
    /// Defines on/off state of experimental features
    /// </summary>
    public static class Features
    {
        public static FeatureSwitch AddDefinitionForInheritedInterfaceImplementations = false;
    }

    public class FeatureSwitch
    {
        private bool globalValue;
        private AsyncLocal<bool> localValue = null;

        public bool Value => localValue?.Value ?? globalValue;

        public FeatureSwitch(bool initialGlobalValue)
        {
            globalValue = initialGlobalValue;
        }

        public IDisposable EnableLocal(bool enabled = true)
        {
            localValue = localValue ?? new AsyncLocal<bool>();
            var priorValue = localValue.Value;
            localValue.Value = enabled;
            return new DisposeAction(() => localValue.Value = priorValue);
        }

        public IDisposable EnableGlobal(bool enabled = true)
        {
            var priorValue = globalValue;
            globalValue = enabled;
            return new DisposeAction(() => globalValue = priorValue);
        }

        public static implicit operator bool(FeatureSwitch f)
        {
            return f.Value;
        }

        public static implicit operator FeatureSwitch(bool enabled)
        {
            return new FeatureSwitch(enabled);
        }
    }
}
