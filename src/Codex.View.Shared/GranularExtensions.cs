using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
#if GRANULAR
using IServiceProvider = System.Windows.Markup.InitializeContext;
#endif

namespace Codex.View
{
    public static class GranularExtensions
    {
#if GRANULAR
        public static IPropertyPathElement AsTriggerProperty(this DependencyProperty dependencyProperty)
        {
            return new DependencyPropertyPathElement(dependencyProperty);
        }
#else
        public static DependencyProperty AsTriggerProperty(this DependencyProperty dependencyProperty)
        {
            return dependencyProperty;
        }
        
        //public static Type GetMarkupTargetType(this IServiceProvider provider)
        //{
        //    var targetProvider = (IProvideValueTarget)provider.GetService(typeof())
        //}
#endif

        public static Trigger WithSetters(this Trigger trigger, params Setter[] setters)
        {
            foreach (var setter in setters)
            {
                trigger.Setters.Add(setter);
            }

            return trigger;
        }
    }
}
