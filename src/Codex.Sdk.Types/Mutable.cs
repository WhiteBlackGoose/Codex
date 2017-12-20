using System;
using System.Collections.Generic;
using System.Text;

namespace Codex
{
    public interface IMutable<TMutable, TImmutable>
    {
        T CopyFrom<T>(TImmutable value)
            where T : TMutable;
    }

    public static class MutableExtensions
    {
        //public static T Apply<TMutable, TImmutable, T>(this T target, TImmutable value)
        //    where T : IMutable<TMutable, TImmutable>, TMutable
        //{
        //    return target.CopyFrom<T>(value);
        //}
    }
}
