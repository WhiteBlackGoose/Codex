using Codex.Sdk.Search;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Codex.Web
{
    public class CodexServiceApplicationModelConvention : IApplicationModelConvention
    {
        public void Apply(ApplicationModel application)
        {
            foreach (var controller in application.Controllers)
            {
                foreach (var action in controller.Actions.ToList())
                {
                    var serviceMethodAttribute = action.Attributes.OfType<ServiceMethodAttribute>().FirstOrDefault();
                    if (serviceMethodAttribute != null)
                    {
                        controller.Actions.Remove(action);

                        var getAction = new ActionModel(action);
                        getAction.Selectors[0].ActionConstraints.Add(new HttpMethodActionConstraint(new[] { HttpMethod.Get.Method }));

                        var postAction = new ActionModel(action);
                        postAction.Selectors[0].ActionConstraints.Add(new HttpMethodActionConstraint(new[] { HttpMethod.Post.Method }));
                        postAction.Parameters[0].BindingInfo = new BindingInfo() { BindingSource = BindingSource.Body };

                        controller.Actions.Add(getAction);
                        controller.Actions.Add(postAction);
                    }
                }
            }
        }
    }

    public class ServiceMethodAttribute : RouteAttribute
    {
        public ServiceMethodAttribute(CodexServiceMethod method)
            : base(method.ToString())
        {
        }
    }
}
