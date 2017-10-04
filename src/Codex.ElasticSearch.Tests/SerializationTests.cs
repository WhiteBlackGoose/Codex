using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.ElasticSearch.Tests
{
    [TestFixture]
    public class SerializationTests
    {
        [Test]
        public void TestOnlyInterfaceMembersSerialized()
        {
            Placeholder.NotImplemented("Also ensure only allowed stages are serialized");

            // TODO: Add your test code here
            Assert.Pass("Your first passing test");
        }
    }
}
