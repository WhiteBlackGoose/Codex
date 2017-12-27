using Codex.ObjectModel;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;

namespace Codex.View
{
    public static partial class ViewUtilities
    {
        public static Visibility BoolVisibility(bool isVisible)
        {
            return isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        public static T Increment<T>(this T value, Counter counter)
        {
            counter.Increment();
            return value;
        }

        public static T Add<T>(this T value, Func<T, int> computeCount)
            where T : IResultsStats
        {
            computeCount(value);
            return value;
        }

        public static T AddTo<T>(this T value, Counter counter)
            where T : IResultsStats
        {
            counter.Add(value.Counter.Count);
            return value;
        }

        public static T AddFrom<T>(this T value, Counter counter)
            where T : IResultsStats
        {
            value.Counter.Add(counter.Count);
            return value;
        }

        public static DependencyProperty RegisterDependencyProperty<TOwner, TPropertyType>(
            [CallerMemberName] string dependencyPropertyName = null,
            Action<TOwner, TPropertyType> onPropertyChanged = null,
            TPropertyType defaultValue = default(TPropertyType))
            where TOwner : class
        {
            return DependencyProperty.Register(dependencyPropertyName.Substring(0, dependencyPropertyName.Length - "Property".Length),
                typeof(TPropertyType),
                typeof(TOwner),
                onPropertyChanged != null ?
                    new PropertyMetadata(defaultValue, (DependencyObject owner, DependencyPropertyChangedEventArgs args) =>
                    {
                        onPropertyChanged(owner as TOwner, (TPropertyType)args.NewValue);
                    }) : 
                    new PropertyMetadata(defaultValue));
        }

        public static string GetReferencesHeader(ReferenceKind referenceKind, int referenceCount, string symbolName)
        {
            string formatString = "";
            switch (referenceKind)
            {
                case ReferenceKind.Reference:
                    formatString = "{0} reference{1} to {2}";
                    break;
                case ReferenceKind.Definition:
                    formatString = "{0} definition{1} of {2}";
                    break;
                case ReferenceKind.Constructor:
                    formatString = "{0} constructor{1} of {2}";
                    break;
                case ReferenceKind.Instantiation:
                    formatString = "{0} instantiation{1} of {2}";
                    break;
                case ReferenceKind.DerivedType:
                    formatString = "{0} type{1} derived from {2}";
                    break;
                case ReferenceKind.InterfaceInheritance:
                    formatString = "{0} interface{1} inheriting from {2}";
                    break;
                case ReferenceKind.InterfaceImplementation:
                    formatString = "{0} implementation{1} of {2}";
                    break;
                case ReferenceKind.Override:
                    formatString = "{0} override{1} of {2}";
                    break;
                case ReferenceKind.InterfaceMemberImplementation:
                    formatString = "{0} implementation{1} of {2}";
                    break;
                case ReferenceKind.Write:
                    formatString = "{0} write{1} to {2}";
                    break;
                case ReferenceKind.Read:
                    formatString = "{0} read{1} of {2}";
                    break;
                case ReferenceKind.GuidUsage:
                    formatString = "{0} usage{1} of Guid {2}";
                    break;
                case ReferenceKind.EmptyArrayAllocation:
                    formatString = "{0} allocation{1} of empty arrays";
                    break;
                case ReferenceKind.MSBuildPropertyAssignment:
                    formatString = "{0} assignment{1} to MSBuild property {2}";
                    break;
                case ReferenceKind.MSBuildPropertyUsage:
                    formatString = "{0} usage{1} of MSBuild property {2}";
                    break;
                case ReferenceKind.MSBuildItemAssignment:
                    formatString = "{0} assignment{1} to MSBuild item {2}";
                    break;
                case ReferenceKind.MSBuildItemUsage:
                    formatString = "{0} usage{1} of MSBuild item {2}";
                    break;
                case ReferenceKind.MSBuildTargetDeclaration:
                    formatString = "{0} declaration{1} of MSBuild target {2}";
                    break;
                case ReferenceKind.MSBuildTargetUsage:
                    formatString = "{0} usage{1} of MSBuild target {2}";
                    break;
                case ReferenceKind.MSBuildTaskDeclaration:
                    formatString = "{0} import{1} of MSBuild task {2}";
                    break;
                case ReferenceKind.MSBuildTaskUsage:
                    formatString = "{0} call{1} to MSBuild task {2}";
                    break;
                case ReferenceKind.Text:
                    formatString = "{0} text search hit{1} for '{2}'";
                    break;
                default:
                    throw new NotImplementedException("Missing case for " + referenceKind);
            }

            return string.Format(formatString,
                    referenceCount,
                    referenceCount == 1 ? "" : "s",
                    symbolName);
        }

    }
}
