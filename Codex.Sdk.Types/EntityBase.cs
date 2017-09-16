using System;
using System.Collections.Generic;
using System.Text;

namespace Codex
{
    public class EntityBase
    {
        protected virtual void Initialize()
        {
        }

        protected virtual void OnSerializingCore()
        {
        }

        protected virtual void OnDeserializingCore()
        {
        }
    }
}
