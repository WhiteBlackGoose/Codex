using System;
using System.IO;

namespace CodexTestCSharpLibrary.Cases
{
    public interface IAnimal
    {
        void Eat();
    }

    public abstract class AbstractAnimal
    {
        public void Eat() { }
    }

    public class Cow : AbstractAnimal, IAnimal
    {
        // IAnimal.Eat() is implemented via base type's AbstractAnimal.Eat()
    }

    // Test variant using generics and properties

    public interface IFood<TMeat> where TMeat : IAnimal
    {
        TMeat Meat { get; }
    }

    public abstract class AbstractFood<TMeat>
    {
        public TMeat Meat { get; set; }
    }

    public class Hamburger : AbstractFood<Cow>, IFood<Cow>
    {
        // IFood<TMeat>.Meat is implemented via base type's AbstractFood<Cow>.Meat
    }
}
