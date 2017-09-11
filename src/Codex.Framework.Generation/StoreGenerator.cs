using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Framework.Generation
{
    internal class StoreGenerator
    {
        public CodeNamespace StoreNamespace;
        public CodeTypeDeclaration StoreType;

        public CodeMemberMethod InitializeMethod;
        public CodeMemberMethod FinalizeMethod;

        private string genericTypedStoreName;

        public StoreGenerator(string namespaceName, string storeTypeName, string genericTypedStoreName)
        {
            StoreNamespace = new CodeNamespace("Codex.ElasticSearch");
            StoreType = new CodeTypeDeclaration(storeTypeName)
            {
                IsPartial = true,
                IsClass = true,
            };

            StoreNamespace.Types.Add(StoreType);
            this.genericTypedStoreName = genericTypedStoreName;

            InitializeMethod = new CodeMemberMethod()
            {
                Name = "InitializeAsync",
                Attributes = MemberAttributes.Public,
                ReturnType = new CodeTypeReference(typeof(Task))
            }.AsyncMethod();
            StoreType.Members.Add(InitializeMethod);

            FinalizeMethod = new CodeMemberMethod()
            {
                Name = "FinalizeAsync",
                Attributes = MemberAttributes.Public,
                ReturnType = new CodeTypeReference(typeof(Task))
            }.AsyncMethod();
            StoreType.Members.Add(FinalizeMethod);

            //var storeBaseCreateStore = new CodeMemberMethod()
            //{
            //    Name = "CreateStoreAsync",
            //    Attributes = MemberAttributes.Public | MemberAttributes.Abstract,
            //    ReturnType = new CodeTypeReference("Task", new CodeTypeReference(nameof(IStore<ISearchEntity>), new CodeTypeReference(new CodeTypeParameter("TSearchType")))),

            //}
            //.Apply(m => m.TypeParameters.Add(new CodeTypeParameter("TSearchType").WithClassConstraint()))
            //.Apply(m => m.Parameters.Add(new CodeParameterDeclarationExpression(typeof(SearchType), "searchType")));

            foreach (var searchType in SearchTypes.RegisteredSearchTypes)
            {
                AddSearchType(searchType);
            }
        }

        public void AddSearchType(SearchType searchType)
        {
            string name = searchType.Name + "Store";
            var type = new CodeTypeReference(genericTypedStoreName, new CodeTypeReference(searchType.Type));

            var typedStoreField = new CodeMemberField(type, $"m_{name}")
            {
                Attributes = MemberAttributes.Private
            };

            StoreType.Members.Add(typedStoreField);

            StoreType.Members.Add(new CodeMemberProperty()
            {
                Type = type,
                Name = name,
                Attributes = MemberAttributes.Public,
                HasGet = true,
            }.Apply(p => p.GetStatements.Add(new CodeMethodReturnStatement(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), typedStoreField.Name)))));

            InitializeMethod.Statements.Add(new CodeAssignStatement(
                new CodeVariableReferenceExpression(typedStoreField.Name),
                new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(new CodeThisReferenceExpression(), "CreateStoreAsync", new CodeTypeReference(searchType.Type)),
                    new CodeFieldReferenceExpression(new CodeTypeReferenceExpression(typeof(SearchTypes)), searchType.Name)).AwaitExpression()));

            FinalizeMethod.Statements.Add(
                new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(new CodeVariableReferenceExpression(typedStoreField.Name), "FinalizeAsync")).AwaitExpression());
        }
    }
}
