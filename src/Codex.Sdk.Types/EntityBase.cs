using System;
using System.Collections.Generic;
using System.Text;

namespace Codex
{
    public class EntityBase : ISerializableEntity
    {
        public EntityBase()
        {
            Initialize();
        }

        protected virtual void Initialize()
        {
        }

        protected virtual void OnSerializingCore()
        {
        }

        protected virtual void OnDeserializedCore()
        {
        }

        void ISerializableEntity.OnSerializing()
        {
            OnSerializingCore();
        }

        void ISerializableEntity.OnDeserialized()
        {
            OnDeserializedCore();
        }
    }

    public interface ISerializableEntity
    {
        void OnDeserialized();

        void OnSerializing();
    }

}
